using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using OpenCodeStudio.Models;

namespace OpenCodeStudio.Services
{
    public static class CliCommand
    {
        // Кэш мажорной версии по полному пути к бинарю.
        private static readonly object MajorLock = new object();
        private static readonly Dictionary<string, int> MajorCache =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static bool IsV2(string binary)
        {
            if (string.IsNullOrEmpty(binary)) return false;
            var major = DetectMajor(binary);
            if (major > 0) return major >= 2;
            // Версию определить не удалось — судим по имени, как раньше.
            return Path.GetFileNameWithoutExtension(binary).Equals("opencode2", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Мажорная версия бинаря opencode: запускает "&lt;binary&gt; --version"
        /// (скрытно, таймаут 2 c), берёт первое целое из вывода. Кэшируется по
        /// пути. При неудаче — 0. С UI-потока использовать DetectMajorAsync.
        /// </summary>
        public static int DetectMajor(string binary)
        {
            if (string.IsNullOrEmpty(binary)) return 0;
            string key;
            try { key = Path.GetFullPath(binary); }
            catch { key = binary; }

            lock (MajorLock)
            {
                if (MajorCache.TryGetValue(key, out var cached)) return cached;
            }

            // Синхронный путь: может блокировать до 2 с. С UI-потока — только async.
            var major = RunVersionProbeAsync(binary).GetAwaiter().GetResult();

            lock (MajorLock) { MajorCache[key] = major; }
            return major;
        }

        /// <summary>
        /// Асинхронная обёртка: запуск уходит в Task.Run, UI-поток не блокируется.
        /// Результат кладётся в тот же кэш, что и у DetectMajor.
        /// </summary>
        public static Task<int> DetectMajorAsync(string binary)
        {
            if (string.IsNullOrEmpty(binary)) return Task.FromResult(0);
            string key;
            try { key = Path.GetFullPath(binary); }
            catch { key = binary; }

            lock (MajorLock)
            {
                if (MajorCache.TryGetValue(key, out var cached)) return Task.FromResult(cached);
            }

            return Task.Run(async () =>
            {
                var major = await RunVersionProbeAsync(binary).ConfigureAwait(false);
                lock (MajorLock) { MajorCache[key] = major; }
                return major;
            });
        }

        /// <summary>Сбросить кэш версий (например, после установки CLI).</summary>
        public static void ResetVersionCache()
        {
            lock (MajorLock) { MajorCache.Clear(); }
        }

        private static Task<int> RunVersionProbeAsync(string binary)
        {
            string fileName, arguments;
            if (binary.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                binary.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "cmd.exe";
                arguments = "/c \"" + binary + "\" --version";
            }
            else
            {
                fileName = binary;
                arguments = "--version";
            }
            return RunAndParseAsync(fileName, arguments);
        }

        /// <summary>
        /// Запускает процесс, асинхронно читает stdout/stderr, ждёт выхода не
        /// дольше 2 с и гарантированно убивает процесс по таймауту.
        /// </summary>
        private static async Task<int> RunAndParseAsync(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return 0;
                    var stdout = proc.StandardOutput.ReadToEndAsync();
                    var stderr = proc.StandardError.ReadToEndAsync();
                    var exited = await Task.Run(() => proc.WaitForExit(2000)).ConfigureAwait(false);
                    if (!exited) { try { proc.Kill(); } catch { } }
                    var output = await stdout.ConfigureAwait(false);
                    output += await stderr.ConfigureAwait(false);
                    return ParseFirstInt(output);
                }
            }
            catch { return 0; }
        }

        private static int ParseFirstInt(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var i = 0;
            while (i < text.Length && !char.IsDigit(text[i])) i++;
            var start = i;
            while (i < text.Length && char.IsDigit(text[i])) i++;
            if (i == start) return 0;
            return int.TryParse(text.Substring(start, i - start), out var value) ? value : 0;
        }

        public static string Arguments(string binary, ServerInfo server, string directory, string sessionId)
        {
            if (server == null) throw new InvalidOperationException("Сервер OpenCode не подключён.");
            var q = new Func<string, string>(PseudoTerminal.Quote);
            bool v2 = IsV2(binary);
            string arguments;
            if (v2)
            {
                arguments = "--server " + q(server.BaseUrl) + " " + q(directory);
            }
            else
                // В новых версиях opencode флага --dir нет: рабочий каталог
                // задаётся при запуске процесса (CreateProcess directory).
                arguments = "attach " + q(server.BaseUrl);
            if (!string.IsNullOrEmpty(sessionId)) arguments += " --session " + q(sessionId);
            // Дополнительные аргументы CLI из настроек.
            var extra = SpendSettings.Current?.CliArguments;
            if (!string.IsNullOrWhiteSpace(extra)) arguments += " " + extra.Trim();
            return arguments;
        }

        public static IDictionary<string, string> EnvironmentFor(ServerInfo server) => new Dictionary<string, string>
        {
            ["OPENCODE_SERVER_USERNAME"] = server.Username ?? "opencode",
            ["OPENCODE_SERVER_PASSWORD"] = server.Password ?? ""
        };
    }
}
