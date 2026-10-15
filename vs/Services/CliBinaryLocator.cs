using System;
using System.IO;
using System.Threading.Tasks;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Поиск и выбор бинаря opencode: каталоги установки, определение v1/v2 по
    /// фактической версии, Desktop-приложение и проверка поддержки подкоманды
    /// web. Отделено от жизненного цикла сервера (запуск/остановка/health).
    /// </summary>
    public static class CliBinaryLocator
    {
        /// <summary>Каталоги установки npm/node, где искать бинарь opencode.</summary>
        private static readonly string[] CommonDirs =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "nvmw", "nodejs"),
            Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? "", "nodejs"),
        };

        /// <summary>
        /// Полный путь к бинарю или null. Учитывает явное переопределение пути
        /// (OPENCODEJESK_OPENCODE_BIN), путь из настроек и автоопределение по
        /// фактической версии ("opencode --version"), а не по имени.
        /// </summary>
        public static string Resolve(bool preferV2)
        {
            // Явное переопределение пути к бинарю (например, для теста v2-беты
            // opencode2): OPENCODEJESK_OPENCODE_BIN=<полный путь>. Старое имя
            // OPENCODESTUDIO_OPENCODE_BIN поддерживается как fallback.
            var overridePath = Environment.GetEnvironmentVariable("OPENCODEJESK_OPENCODE_BIN")
                ?? Environment.GetEnvironmentVariable("OPENCODESTUDIO_OPENCODE_BIN");
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
                return overridePath;

            // Путь из настроек приоритетнее автоопределения.
            var configured = SpendSettings.Current?.OpenCodePath;
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;

            // Определяем бинарь по фактической версии ("opencode --version"), а
            // не по имени: пакет v2 ставит и "opencode", и "opencode2".
            var v2Path = FindV2Path();
            var v1Path = FindV1Path();

            if (preferV2)
            {
                if (v2Path != null) return v2Path;
                if (v1Path != null)
                {
                    Log.Warn("OpenCode v2 not found; falling back to opencode v1");
                    return v1Path;
                }
            }
            else
            {
                if (v1Path != null) return v1Path;
                if (v2Path != null) return v2Path;
            }

            // Версии определить не удалось — прежний порядок по именам.
            var names = preferV2
                ? new[] { "opencode2", "opencode" }
                : new[] { "opencode", "opencode2" };
            foreach (var name in names)
            {
                var path = FindByName(name, CommonDirs);
                if (path != null) return path;
            }

            return null;
        }

        /// <summary>
        /// Полный путь к v1-бинарю или null. v1 — бинарь с major==1; если версию
        /// определить не удалось, считаем v1 по имени "opencode".
        /// </summary>
        public static string FindV1Path()
        {
            foreach (var name in new[] { "opencode", "opencode2" })
            {
                var path = FindByName(name, CommonDirs);
                if (path == null) continue;
                var major = CliCommand.DetectMajor(path);
                if (major == 1) return path;
                if (major == 0 && name == "opencode") return path;
            }
            return null;
        }

        /// <summary>
        /// Полный путь к v2-бинарю или null. v2 — бинарь с major&gt;=2; если версию
        /// определить не удалось, считаем v2 по имени "opencode2".
        /// </summary>
        public static string FindV2Path()
        {
            foreach (var name in new[] { "opencode", "opencode2" })
            {
                var path = FindByName(name, CommonDirs);
                if (path == null) continue;
                var major = CliCommand.DetectMajor(path);
                if (major >= 2) return path;
                if (major == 0 && name == "opencode2") return path;
            }
            return null;
        }

        /// <summary>
        /// Асинхронная версия FindV1Path: поиск и проверка версии не выполняются
        /// на вызывающем (UI) потоке.
        /// </summary>
        public static async Task<string> FindV1PathAsync()
        {
            foreach (var name in new[] { "opencode", "opencode2" })
            {
                var path = await Task.Run(() => FindByName(name, CommonDirs)).ConfigureAwait(false);
                if (path == null) continue;
                var major = await CliCommand.DetectMajorAsync(path).ConfigureAwait(false);
                if (major == 1) return path;
                if (major == 0 && name == "opencode") return path;
            }
            return null;
        }

        /// <summary>
        /// Асинхронная версия FindV2Path: поиск и проверка версии не выполняются
        /// на вызывающем (UI) потоке.
        /// </summary>
        public static async Task<string> FindV2PathAsync()
        {
            foreach (var name in new[] { "opencode", "opencode2" })
            {
                var path = await Task.Run(() => FindByName(name, CommonDirs)).ConfigureAwait(false);
                if (path == null) continue;
                var major = await CliCommand.DetectMajorAsync(path).ConfigureAwait(false);
                if (major >= 2) return path;
                if (major == 0 && name == "opencode2") return path;
            }
            return null;
        }

        /// <summary>Установлен ли v1 (major==1, иначе по имени opencode).</summary>
        public static bool IsV1Available() => FindV1Path() != null;

        /// <summary>Установлен ли v2 (major&gt;=2, иначе по имени opencode2).</summary>
        public static bool IsV2Available() => FindV2Path() != null;

        /// <summary>
        /// Полный путь к приложению OpenCode Desktop или null, если не найдено.
        /// Проверяются стандартные каталоги установки.
        /// </summary>
        public static string FindDesktopAppPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? "";
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "";

            var candidates = new[]
            {
                Path.Combine(localAppData, "Programs", "@opencode-aidesktop", "OpenCode.exe"),
                Path.Combine(programFiles, "OpenCode", "OpenCode.exe"),
                Path.Combine(programFilesX86, "OpenCode", "OpenCode.exe"),
            };
            foreach (var path in candidates)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    return path;
            }
            return null;
        }

        // Кэш результата SupportsWeb: CLI-бинарь -> поддерживает ли подкоманду web.
        private static string _webSupportPath;
        private static bool _webSupportResult;

        /// <summary>Сбросить кэш SupportsWeb (например, после установки CLI).</summary>
        public static void ResetWebSupportCache()
        {
            _webSupportPath = null;
            _webSupportResult = false;
        }

        /// <summary>
        /// Асинхронная обёртка: запуск уходит в Task.Run, UI-поток не блокируется.
        /// Результат кладётся в общий кэш поддержки web.
        /// </summary>
        public static Task<bool> SupportsWebAsync(string cliPath)
        {
            if (string.IsNullOrWhiteSpace(cliPath)) return Task.FromResult(false);
            if (_webSupportPath != null &&
                string.Equals(_webSupportPath, cliPath, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(_webSupportResult);

            return Task.Run(async () =>
            {
                bool result;
                try
                {
                    var output = await RunCaptureAsync("/c \"" + cliPath + "\" --help")
                        .ConfigureAwait(false);
                    result = output.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch { result = false; }

                _webSupportPath = cliPath;
                _webSupportResult = result;
                return result;
            });
        }

        /// <summary>
        /// Строка версии бинаря opencode или null. Запускает "&lt;binary&gt; --version"
        /// (через RunCaptureAsync, для .cmd/.bat — cmd /c), берёт из первой строки
        /// с цифрами версионный токен вида "1.19.0".
        /// </summary>
        public static async Task<string> GetVersionAsync(string binary)
        {
            if (string.IsNullOrWhiteSpace(binary)) return null;

            string output;
            try
            {
                output = await RunCaptureAsync("/c \"" + binary + "\" --version").ConfigureAwait(false);
            }
            catch { return null; }

            if (string.IsNullOrEmpty(output)) return null;

            foreach (var raw in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = System.Text.RegularExpressions.Regex.Match(raw, @"\d+(\.\d+)+");
                if (match.Success) return match.Value;
            }
            return null;
        }

        /// <summary>
        /// Запускает cmd.exe с аргументами, асинхронно читает stdout/stderr, ждёт
        /// выхода не дольше 2 с и гарантированно убивает процесс по таймауту.
        /// </summary>
        private static async Task<string> RunCaptureAsync(string arguments)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                if (proc == null) return "";
                var stdout = proc.StandardOutput.ReadToEndAsync();
                var stderr = proc.StandardError.ReadToEndAsync();
                var exited = await Task.Run(() => proc.WaitForExit(2000)).ConfigureAwait(false);
                if (!exited) { try { proc.Kill(); } catch { } }
                var output = await stdout.ConfigureAwait(false);
                output += await stderr.ConfigureAwait(false);
                return output;
            }
        }

        private static string FindByName(string name, string[] dirs)
        {
            var direct = FindOnPath(name);
            if (direct != null) return direct;

            foreach (var dir in dirs)
            {
                var path = Path.Combine(dir, name + ".cmd");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static string FindOnPath(string name)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c where " + name + " 2>nul",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string cmdFallback = null;
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            return trimmed;
                        if (cmdFallback == null && trimmed.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                            cmdFallback = trimmed;
                    }
                    if (cmdFallback != null)
                        return cmdFallback;
                }
            }
            catch { }
            return null;
        }
    }
}
