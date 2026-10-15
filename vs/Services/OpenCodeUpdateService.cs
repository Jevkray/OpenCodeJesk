using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Проверяет установленную и последнюю доступную версию opencode и умеет
    /// обновлять глобальный npm-пакет.
    /// </summary>
    public sealed class OpenCodeUpdateService
    {
        private const string NpmLatestUrl = "https://registry.npmjs.org/opencode-ai/latest";
        private const string NpmBetaUrl = "https://registry.npmjs.org/@opencode/cli/latest";

        /// <summary>Канал обновлений (из настроек): stable или beta.</summary>
        private static UpdateChannel Channel => SpendSettings.Current?.Channel ?? UpdateChannel.Stable;

        /// <summary>Версия установленного opencode или null при ошибке.</summary>
        public async Task<string> GetInstalledVersionAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c opencode --version 2>nul",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    var text = (await p.StandardOutput.ReadToEndAsync()).Trim();
                    p.WaitForExit(10000);
                    if (string.IsNullOrEmpty(text)) return null;
                    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var version = lines.Length > 0 ? lines[0].Trim() : null;
                    return version;
                }
            }
            catch { return null; }
        }

        /// <summary>Последняя версия из npm-реестра или null при ошибке.</summary>
        public async Task<string> GetLatestVersionAsync()
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    var url = Channel == UpdateChannel.Beta ? NpmBetaUrl : NpmLatestUrl;
                    var json = await client.GetStringAsync(url);
                    var version = (string)JObject.Parse(json)["version"];
                    return version;
                }
            }
            catch { return null; }
        }

        /// <summary>Запускает обновление opencode; true при коде выхода 0.</summary>
        public async Task<bool> UpdateAsync(Action<string> onLog = null)
        {
            var target = Channel == UpdateChannel.Beta ? "@opencode/cli" : "opencode-ai@latest";
            return await RunNpmAsync(target, onLog);
        }

        /// <summary>Ставит/обновляет произвольный npm-пакет (кнопки Install v1/v2).</summary>
        public Task<bool> InstallAsync(string packageSpec, Action<string> onLog = null, CancellationToken ct = default)
            => RunNpmAsync(packageSpec, onLog, ct);

        private static async Task<bool> RunNpmAsync(string target, Action<string> onLog, CancellationToken ct = default)
        {
            try
            {
                CleanupStaleInstallDirs(onLog);
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c npm i -g " + target + " --force --no-fund --no-audit --loglevel=info",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    // Запоминаем PID: убивать нужно всё дерево (cmd -> node -> ...),
                    // иначе дочерний node остаётся жив и держит блокировку npm.
                    var pid = p.Id;
                    using (ct.Register(() => KillTree(pid)))
                    {
                        p.OutputDataReceived += (_, e) => { if (e.Data != null) onLog?.Invoke(e.Data); };
                        p.ErrorDataReceived += (_, e) => { if (e.Data != null) onLog?.Invoke(e.Data); };
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        // 30 минут на установку: v2 тянет бинарники под все платформы.
                        var completed = await Task.Run(() => p.WaitForExit(1800000));
                        if (!completed || ct.IsCancellationRequested)
                        {
                            KillTree(pid);
                            return false;
                        }
                        if (p.ExitCode == 0)
                        {
                            // Бинарь мог смениться — сбрасываем кэши определения.
                            CliCommand.ResetVersionCache();
                            CliBinaryLocator.ResetWebSupportCache();
                            return true;
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                onLog?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Best-effort удаляет временные каталоги неудачных npm-установок в
        /// %APPDATA%\npm\node_modules (.имя содержит opencode или cli). Они
        /// остаются от прерванных установок и приводят к EPERM/EEXIST.
        /// </summary>
        private static void CleanupStaleInstallDirs(Action<string> onLog)
        {
            try
            {
                var root = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "npm", "node_modules");
                if (!System.IO.Directory.Exists(root)) return;
                foreach (var path in System.IO.Directory.GetDirectories(root))
                {
                    var name = System.IO.Path.GetFileName(path);
                    if (!name.StartsWith(".", StringComparison.Ordinal)) continue;
                    if (name.IndexOf("opencode", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("cli", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try
                    {
                        System.IO.Directory.Delete(path, true);
                        onLog?.Invoke("Removed stale npm dir: " + name);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>Убивает процесс вместе со всеми дочерними (taskkill /T).</summary>
        private static void KillTree(int pid)
        {
            try
            {
                using (var k = Process.Start(new ProcessStartInfo(
                    "taskkill.exe", "/T /F /PID " + pid)
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                    k?.WaitForExit(5000);
            }
            catch { }
        }

        /// <summary>Корректное (не строковое) сравнение версий: latest &gt; installed.</summary>
        public static bool IsNewer(string latest, string installed)
        {
            var a = Parse(latest);
            var b = Parse(installed);
            if (a == null || b == null) return false;

            var len = Math.Max(a.Length, b.Length);
            for (var i = 0; i < len; i++)
            {
                var x = i < a.Length ? a[i] : 0;
                var y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }

        private static int[] Parse(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;

            var parts = version.Trim().TrimStart('v', 'V').Split('.');
            var result = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                var digits = new string(Array.FindAll(parts[i].ToCharArray(), char.IsDigit));
                if (digits.Length == 0 || !int.TryParse(digits, out result[i])) return null;
            }
            return result;
        }
    }
}
