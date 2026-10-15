using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OpenCodeJesk.Services
{
    public sealed class PseudoTerminal : IDisposable
    {
        private IntPtr _console;
        private Process _process;
        private FileStream _input;
        private StreamReader _output;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private volatile bool _disposed;

        public Func<string, Task> Output { get; set; }
        public event Action<int> Exited;
        public int ProcessId => _process?.Id ?? 0;
        public bool IsRunning => !_disposed && _process != null && !_process.HasExited;

        public static string Quote(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0)
                throw new ArgumentException("Недопустимый аргумент процесса.");
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (var ch in value)
            {
                if (ch == '\\') { slashes++; continue; }
                result.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes);
                result.Append(ch);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        public void Start(string executable, string arguments, string directory, int columns, int rows,
            IDictionary<string, string> environment = null)
        {
            if (_disposed || _process != null) throw new InvalidOperationException("Терминал уже запущен.");
            if (!File.Exists(executable)) throw new FileNotFoundException("CLI не найден.", executable);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            IntPtr inputRead = IntPtr.Zero, inputWrite = IntPtr.Zero;
            IntPtr outputRead = IntPtr.Zero, outputWrite = IntPtr.Zero;
            IntPtr attributes = IntPtr.Zero, envBlock = IntPtr.Zero;
            ProcessInfo processInfo = default;
            bool attributesInitialized = false;
            try
            {
                Check(CreatePipe(out inputRead, out inputWrite, IntPtr.Zero, 0));
                Check(CreatePipe(out outputRead, out outputWrite, IntPtr.Zero, 0));
                Marshal.ThrowExceptionForHR(CreatePseudoConsole(Size(columns, rows), inputRead, outputWrite, 0, out _console));
                IntPtr size = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
                attributes = Marshal.AllocHGlobal(size);
                Check(InitializeProcThreadAttributeList(attributes, 1, 0, ref size));
                attributesInitialized = true;
                Check(UpdateProcThreadAttribute(attributes, 0, (IntPtr)0x00020016, _console,
                    (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
                var startup = new StartupInfoEx
                {
                    // Не наследуем перенаправленные stdin/stdout хоста: ввод и вывод принадлежат ConPTY.
                    Startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfoEx)), Flags = 0x00000100 },
                    Attributes = attributes
                };
                envBlock = Marshal.StringToHGlobalUni(BuildEnvironment(environment));
                string application = executable;
                string command = Quote(executable) + " " + arguments;
                if (executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                    executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    application = Environment.GetEnvironmentVariable("COMSPEC") ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
                    command = Quote(application) + " /d /s /c \"" + command + "\"";
                }
                // Запускаем приостановленным, чтобы привязать дочернее дерево к VS до исполнения.
                Check(CreateProcess(application, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero, false,
                    0x00080000 | 0x00000400 | 0x00000004, envBlock, directory, ref startup, out processInfo));
                _process = Process.GetProcessById(processInfo.ProcessId);
                ProcessBinding.BindToCurrentProcess(_process);
                _input = new FileStream(new SafeFileHandle(inputWrite, true), FileAccess.Write, 4096, false);
                inputWrite = IntPtr.Zero;
                _output = new StreamReader(new FileStream(new SafeFileHandle(outputRead, true), FileAccess.Read, 4096, false),
                    new UTF8Encoding(false), false, 4096);
                outputRead = IntPtr.Zero;
                var readTask = ReadOutputAsync();
                var process = _process;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        process.WaitForExit();
                        if (!_disposed) Exited?.Invoke(process.ExitCode);
                        await readTask.ConfigureAwait(false);
                    }
                    catch (Exception ex) when (_disposed || ex is InvalidOperationException) { }
                });
                if (ResumeThread(processInfo.Thread) == uint.MaxValue) throw new Win32Exception();
            }
            catch
            {
                Dispose();
                throw;
            }
            finally
            {
                if (attributesInitialized) DeleteProcThreadAttributeList(attributes);
                if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
                if (envBlock != IntPtr.Zero) Marshal.FreeHGlobal(envBlock);
                foreach (var handle in new[] { inputRead, inputWrite, outputRead, outputWrite, processInfo.Process, processInfo.Thread })
                    if (handle != IntPtr.Zero) CloseHandle(handle);
            }
        }

        private async Task ReadOutputAsync()
        {
            var buffer = new char[4096];
            try
            {
                int count;
                while ((count = await _output.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    var handler = Output;
                    if (!_disposed && handler != null) await handler(new string(buffer, 0, count)).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (_disposed || ex is IOException || ex is ObjectDisposedException) { }
        }

        public async Task WriteAsync(string text)
        {
            if (_disposed || _input == null || string.IsNullOrEmpty(text)) return;
            var data = Encoding.UTF8.GetBytes(text);
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_disposed)
                {
                    await _input.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    await _input.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (_disposed || ex is IOException || ex is ObjectDisposedException) { }
            finally { _writeLock.Release(); }
        }

        public void Resize(int columns, int rows)
        {
            if (!_disposed && _console != IntPtr.Zero)
                Marshal.ThrowExceptionForHR(ResizePseudoConsole(_console, Size(columns, rows)));
        }

        private static Coord Size(int columns, int rows) => new Coord
        {
            X = (short)Math.Max(2, Math.Min(500, columns)),
            Y = (short)Math.Max(2, Math.Min(300, rows))
        };

        private static string BuildEnvironment(IDictionary<string, string> additions)
        {
            var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                values[(string)entry.Key] = (string)entry.Value;
            values["TERM"] = "xterm-256color";
            values["COLORTERM"] = "truecolor";
            if (additions != null)
                foreach (var entry in additions) values[entry.Key] = entry.Value;
            var result = new StringBuilder();
            foreach (var entry in values) result.Append(entry.Key).Append('=').Append(entry.Value).Append('\0');
            return result.Append('\0').ToString();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var process = _process;
            var console = _console;
            _console = IntPtr.Zero;
            // ClosePseudoConsole может ждать чтения вывода: не блокируем UI-поток.
            _ = Task.Run(() =>
            {
                try
                {
                    if (process != null && !process.HasExited)
                    {
                        using (var kill = Process.Start(new ProcessStartInfo("taskkill.exe", "/T /F /PID " + process.Id)
                        { UseShellExecute = false, CreateNoWindow = true }))
                            kill?.WaitForExit(5000);
                        if (!process.HasExited) process.Kill();
                    }
                }
                catch (Exception ex) { Log.Info("CLI shutdown: " + ex.Message); }
                finally
                {
                    if (console != IntPtr.Zero) ClosePseudoConsole(console);
                    _input?.Dispose();
                    _output?.Dispose();
                    process?.Dispose();
                }
            });
        }

        private static void Check(bool result) { if (!result) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        [StructLayout(LayoutKind.Sequential)] private struct Coord { public short X, Y; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int Size;
            public string Reserved, Desktop, Title;
            public int X, Y, Width, Height, Columns, Rows, Fill, Flags;
            public short Show, ReservedSize;
            public IntPtr ReservedData, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfoEx { public StartupInfo Startup; public IntPtr Attributes; }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr Process, Thread; public int ProcessId, ThreadId; }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr read, out IntPtr write, IntPtr security, uint size);
        [DllImport("kernel32.dll")] private static extern int CreatePseudoConsole(Coord size, IntPtr input, IntPtr output, uint flags, out IntPtr console);
        [DllImport("kernel32.dll")] private static extern int ResizePseudoConsole(IntPtr console, Coord size);
        [DllImport("kernel32.dll")] private static extern void ClosePseudoConsole(IntPtr console);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnedSize);
        [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(string app, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
            bool inherit, uint flags, IntPtr environment, string directory, [In] ref StartupInfoEx startup, out ProcessInfo process);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
