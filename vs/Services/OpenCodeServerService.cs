using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenCodeStudio.Models;

namespace OpenCodeStudio.Services
{
    public class OpenCodeServerService : IOpenCodeServerService
    {
        private const int ConnectTimeoutMs = 30000;
        private const int HealthCheckIntervalMs = 500;

        private System.Diagnostics.Process _process;
        private HttpClient _httpClient;
        private ServerInfo _serverInfo;
        private ConnectionState _state = ConnectionState.Disconnected;
        private bool _ownsProcess;
        private string _binaryPath;

        public ServerInfo ServerInfo => _serverInfo;
        public ConnectionState State => _state;
        public bool PreferV2 { get; set; }
        public string BinaryPath => _binaryPath;
        public event Action<ConnectionState> StateChanged;

        public HttpClient GetClient()
        {
            return _httpClient;
        }

        public async Task<bool> StartAsync(string projectRoot)
        {
            Stop();
            SetState(ConnectionState.Connecting);

            // Reuse an OpenCode server that is already running - for example one
            // started by another Visual Studio window, or the OpenCode desktop
            // app. Sharing a single server keeps every client on the same live
            // session/event stream, so chats stay in sync everywhere.
            if (await TryAdoptSharedServerAsync())
            {
                Log.Info($"Reusing existing OpenCode server at {_serverInfo.BaseUrl}");
                SetState(ConnectionState.Connected);
                return true;
            }

            Log.Info($"Starting OpenCode server (cwd={projectRoot})");

            try
            {
                var opencodePath = CliBinaryLocator.Resolve(PreferV2);
                if (opencodePath == null)
                {
                    Log.Error("Could not locate the opencode executable on PATH or common locations");
                    SetState(ConnectionState.Error);
                    return false;
                }
                Log.Info($"Using opencode executable: {opencodePath} (v{CliCommand.DetectMajor(opencodePath)})");
                _binaryPath = opencodePath;

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    WorkingDirectory = projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // opencode v2 (как и v1 с заданным паролем) защищает сервер HTTP
                // Basic Auth — фикс CVE-2026-22812. Задаём СВОЙ пароль, чтобы
                // детерминированно авторизоваться и в HttpClient, и в WebView2.
                var serverUser = "opencode";
                var serverPass = Guid.NewGuid().ToString("N");
                try
                {
                    psi.EnvironmentVariables["OPENCODE_SERVER_USERNAME"] = serverUser;
                    psi.EnvironmentVariables["OPENCODE_SERVER_PASSWORD"] = serverPass;
                }
                catch { }

                // NOTE: we deliberately do NOT override XDG_* here. Using the
                // user's default OpenCode environment keeps history, settings and
                // credentials in sync with the OpenCode CLI and Desktop app.

                // .cmd/.bat shims cannot be started directly with UseShellExecute=false,
                // so launch them through cmd.exe.
                // A stable port keeps the WebView2 origin (http://127.0.0.1:PORT)
                // constant across restarts, so the OpenCode web UI keeps its
                // local state (selected server/project) like the desktop app.
                var port = ServerPortResolver.Resolve();
                Log.Info($"Using port {port}");
                var serveArgs = "serve --port " + port;
                var extraServe = SpendSettings.Current?.ServeArguments;
                if (!string.IsNullOrWhiteSpace(extraServe)) serveArgs += " " + extraServe.Trim();
                if (opencodePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                    opencodePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    psi.FileName = "cmd.exe";
                    psi.Arguments = "/c \"" + opencodePath + "\" " + serveArgs;
                }
                else
                {
                    psi.FileName = opencodePath;
                    psi.Arguments = serveArgs;
                }

                _process = System.Diagnostics.Process.Start(psi);
                if (_process == null)
                {
                    SetState(ConnectionState.Error);
                    return false;
                }

                _ownsProcess = true;
                ProcessBinding.BindToCurrentProcess(_process);

                // Мы сами задали порт, поэтому не зависим от формата строки
                // запуска (в v2 он мог измениться). Парсинг stdout остаётся
                // фолбэком на случай, если порт выбрать не удалось.
                ServerInfo resolvedInfo;
                if (port > 0)
                {
                    resolvedInfo = new ServerInfo("127.0.0.1", port);
                    DrainStreams(_process);
                }
                else
                {
                    resolvedInfo = await ResolveServerUrlAsync(_process, ConnectTimeoutMs);
                }
                if (resolvedInfo == null)
                {
                    Log.Error("Failed to detect server URL from process output");
                    SetState(ConnectionState.Error);
                    return false;
                }

                resolvedInfo.Username = serverUser;
                resolvedInfo.Password = serverPass;
                _serverInfo = resolvedInfo;
                Log.Info($"Server listening on {resolvedInfo.BaseUrl}");
                WriteSharedRegistry(resolvedInfo);

                var healthy = await WaitForHealthAsync(ConnectTimeoutMs);
                if (healthy)
                {
                    await InitializeHttpClientAsync();
                    SetState(ConnectionState.Connected);
                    Log.Info("Server healthy and connected");
                    return true;
                }

                Log.Error("Server started but health check never passed");
                SetState(ConnectionState.Error);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to start server", ex);
                SetState(ConnectionState.Error);
                return false;
            }
        }

        public async Task<bool> CheckHealthAsync()
        {
            if (_httpClient == null) return false;
            try
            {
                var response = await _httpClient.GetAsync("/global/health");
                // opencode v2 отдаёт SPA-страницу на любой путь (включая
                // /global/health), поэтому тело не парсим: успешный ответ
                // означает, что сервер жив и авторизация прошла.
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public void Stop()
        {
            _httpClient?.Dispose();
            _httpClient = null;
            _serverInfo = null;

            // Only kill the server if this instance started it. When we merely
            // adopted a server started elsewhere, leave it running.
            if (_ownsProcess && _process != null)
            {
                Log.Info("Stopping OpenCode server");
                if (!_process.HasExited)
                {
                    // .cmd-шим (node) не умирает вместе с cmd.exe: убиваем всё
                    // дерево, иначе процесс держит порт и серверы копятся.
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = "/T /F /PID " + _process.Id,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using (var killer = System.Diagnostics.Process.Start(psi))
                            killer?.WaitForExit(5000);
                    }
                    catch { try { _process.Kill(); } catch { } }
                    try { _process.WaitForExit(3000); } catch { }
                }
                _process.Dispose();
                RemoveSharedRegistry();
            }
            _process = null;
            _ownsProcess = false;
            SetState(ConnectionState.Disconnected);
        }

        /// <summary>
        /// Tries to reuse a server recorded by another OpenCode Studio instance
        /// (other Visual Studio windows). Returns true when a healthy server was
        /// found and adopted.
        /// </summary>
        private async Task<bool> TryAdoptSharedServerAsync()
        {
            var info = ReadSharedRegistry();
            if (info == null) return false;

            // Не переиспользуем сервер, запущенный другим/старым бинарём opencode:
            // после обновления CLI поднимем свежий. Проверяем только при старте.
            if (IsRecordedBinaryStale()) return false;

            // Записанный сервер мог быть поднят неподходящей версией (v1/v2).
            // Если версию определить удалось и ветка не совпадает с выбранной —
            // не переиспользуем, поднимем свой сервер нужной версии.
            try
            {
                var rec = JsonConvert.DeserializeObject<SharedServerRecord>(
                    File.ReadAllText(RegistryPath));
                if (rec != null)
                {
                    var recMajor = CliCommand.DetectMajor(rec.Binary);
                    bool recV2 = recMajor >= 2;
                    if (recMajor != 0 && recV2 != PreferV2) return false;
                }
            }
            catch { }

            try
            {
                using (var client = CreateHttpClient(info))
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    var response = await client.GetAsync("/global/health");
                    if (!response.IsSuccessStatusCode) return false;
                }
            }
            catch { return false; }

            _serverInfo = info;
            _binaryPath = CliBinaryLocator.Resolve(PreferV2);
            _process = null;
            _ownsProcess = false;
            await InitializeHttpClientAsync();
            return true;
        }

        private static string RegistryPath => Path.Combine(
            OpenCodeEnvironment.StudioDir, "server.json");

        private void WriteSharedRegistry(ServerInfo info)
        {
            try
            {
                Directory.CreateDirectory(OpenCodeEnvironment.StudioDir);
                var record = new SharedServerRecord
                {
                    Host = info.Host,
                    Port = info.Port,
                    Username = info.Username,
                    Password = info.Password,
                    Binary = _binaryPath,
                    BinaryStamp = CurrentBinaryStamp(),
                    Pid = _process?.Id ?? 0
                };
                File.WriteAllText(RegistryPath, JsonConvert.SerializeObject(record));
            }
            catch { }
        }

        private bool IsRecordedBinaryStale()
        {
            try
            {
                if (!File.Exists(RegistryPath)) return false;
                var rec = JsonConvert.DeserializeObject<SharedServerRecord>(File.ReadAllText(RegistryPath));
                if (rec == null) return false;
                var current = CliBinaryLocator.Resolve(PreferV2);
                if (!string.IsNullOrEmpty(rec.Binary) && !string.IsNullOrEmpty(current)
                    && !string.Equals(rec.Binary, current, StringComparison.OrdinalIgnoreCase))
                    return true;
                var stamp = CurrentBinaryStamp();
                if (rec.BinaryStamp != 0 && stamp != 0 && rec.BinaryStamp != stamp)
                    return true;
            }
            catch { }
            return false;
        }

        private long CurrentBinaryStamp()
        {
            try
            {
                var path = !string.IsNullOrEmpty(_binaryPath) ? _binaryPath : CliBinaryLocator.Resolve(PreferV2);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return File.GetLastWriteTimeUtc(path).Ticks;
            }
            catch { }
            return 0;
        }

        private static ServerInfo ReadSharedRegistry()
        {
            try
            {
                if (!File.Exists(RegistryPath)) return null;
                var record = JsonConvert.DeserializeObject<SharedServerRecord>(
                    File.ReadAllText(RegistryPath));
                if (record == null || record.Port <= 0) return null;
                return new ServerInfo(
                    string.IsNullOrEmpty(record.Host) ? "127.0.0.1" : record.Host,
                    record.Port)
                {
                    Username = record.Username,
                    Password = record.Password
                };
            }
            catch { return null; }
        }

        private static void RemoveSharedRegistry()
        {
            try { if (File.Exists(RegistryPath)) File.Delete(RegistryPath); } catch { }
        }

        public void UpdateConnectionState(bool connected)
        {
            if (connected)
                SetState(ConnectionState.Connected);
            else
                SetState(ConnectionState.Disconnected);
        }

        private static async Task<ServerInfo> ResolveServerUrlAsync(
            System.Diagnostics.Process process, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<ServerInfo>();
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            async Task ReadStreamAsync(System.IO.StreamReader reader, string label)
            {
                try
                {
                    // Keep the same pending read across timeout ticks. Calling
                    // ReadLineAsync() again while a previous read is still in
                    // flight throws InvalidOperationException, which would abort
                    // detection entirely.
                    Task<string> lineTask = null;
                    while (DateTime.UtcNow < deadline)
                    {
                        if (lineTask == null)
                            lineTask = reader.ReadLineAsync();
                        var delayTask = Task.Delay(1000);
                        var completed = await Task.WhenAny(lineTask, delayTask);
                        if (completed == delayTask) continue;
                        var line = await lineTask;
                        lineTask = null;
                        if (line == null) break;
                        System.Diagnostics.Debug.WriteLine($"OpenCode {label}: {line}");
                        if (TryParseListenLine(line, out string host, out int port))
                        {
                            tcs.TrySetResult(new ServerInfo(host, port));
                            return;
                        }
                    }
                }
                catch { }
            }

            var stdoutTask = ReadStreamAsync(process.StandardOutput, "stdout");
            var stderrTask = ReadStreamAsync(process.StandardError, "stderr");
            var timeoutTask = Task.Delay(timeoutMs);

            await Task.WhenAny(tcs.Task, timeoutTask);
            return tcs.Task.IsCompleted ? await tcs.Task : null;
        }

        private static bool TryParseListenLine(string line, out string host, out int port)
        {
            host = null;
            port = 0;
            if (string.IsNullOrEmpty(line)) return false;
            const string prefix = "opencode server listening on http://";
            var idx = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            var url = line.Substring(idx + prefix.Length).Trim();
            var colonIdx = url.LastIndexOf(':');
            if (colonIdx < 0) return false;
            host = url.Substring(0, colonIdx);
            return int.TryParse(url.Substring(colonIdx + 1), out port);
        }

        private async Task<bool> WaitForHealthAsync(int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (await TryConnectAsync(_serverInfo))
                    return true;
                await Task.Delay(HealthCheckIntervalMs);
            }
            return false;
        }

        private async Task<bool> TryConnectAsync(ServerInfo info)
        {
            try
            {
                using (var client = CreateHttpClient(info))
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    var response = await client.GetAsync("/global/health");
                    // Любой ответ означает, что сервер слушает. 401/403 приходит,
                    // когда opencode требует Basic Auth (фикс CVE-2026-22812) —
                    // это НЕ повод считать сервер незапущенным.
                    if (!response.IsSuccessStatusCode)
                        Log.Info($"Health check returned {(int)response.StatusCode}; server is reachable");
                    return true;
                }
            }
            catch { }
            return false;
        }

        private async Task InitializeHttpClientAsync()
        {
            _httpClient?.Dispose();
            _httpClient = CreateHttpClient(_serverInfo);
            await Task.CompletedTask;
        }

        private static HttpClient CreateHttpClient(ServerInfo info)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(info.BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            if (info != null && info.HasAuth)
            {
                var raw = (info.Username ?? "opencode") + ":" + info.Password;
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
            }
            return client;
        }

        // Читаем stdout/stderr в фоне, чтобы процесс не заблокировался на
        // заполненном буфере (строку запуска при заданном порте не парсим).
        private static void DrainStreams(System.Diagnostics.Process process)
        {
            try
            {
                // Счётчики на процесс: первые строки вывода пишем в журнал
                // (диагностика старта v2), остальное — только в Debug.
                int stdoutLines = 0;
                int stderrLines = 0;
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    System.Diagnostics.Debug.WriteLine("OpenCode stdout: " + e.Data);
                    if (stdoutLines++ < 40) Log.Info("OpenCode stdout: " + e.Data);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    System.Diagnostics.Debug.WriteLine("OpenCode stderr: " + e.Data);
                    if (stderrLines++ < 40) Log.Info("OpenCode stderr: " + e.Data);
                };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch { }
        }

        private void SetState(ConnectionState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                StateChanged?.Invoke(newState);
            }
        }
    }

    /// <summary>Record of a running OpenCode server shared between clients.</summary>
    internal sealed class SharedServerRecord
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Binary { get; set; }
        public long BinaryStamp { get; set; }
        public int Pid { get; set; }
    }
}
