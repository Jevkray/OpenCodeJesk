using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OpenCodeJesk.Services
{
    public enum ImportKind { Settings, Commands, Theme, Auth, History, State, Desktop }

    /// <summary>Один импортируемый (или информационный) элемент внутри источника.</summary>
    public sealed class ImportItem
    {
        public ImportKind Kind { get; set; }
        public string Label { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public bool IsDirectory { get; set; }
        public bool Found { get; set; }
        public string Detail { get; set; }
        public bool Selected { get; set; } = true;

        public bool Importable => Found && !string.IsNullOrEmpty(Target) &&
            !string.Equals(Path.GetFullPath(Source), Path.GetFullPath(Target), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Обнаруженное окружение / приложение / папка OpenCode.</summary>
    public sealed class ImportSource
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public string RootPath { get; set; }
        public bool IsActive { get; set; }
        public string Summary { get; set; }
        public List<ImportItem> Items { get; } = new List<ImportItem>();
    }

    public sealed class ImportResult
    {
        public List<string> Copied { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public List<string> Backups { get; } = new List<string>();
    }

    /// <summary>
    /// Находит источники данных OpenCode и переносит настройки / команды / темы /
    /// учётные данные / историю чатов между ними и окружением встроенного сервера.
    /// Каждая перезапись сопровождается резервной копией.
    /// </summary>
    public sealed class ImportService
    {
        private readonly OpenCodeEnvironment _env;

        public ImportService(OpenCodeEnvironment env = null)
        {
            // OpenCode Jesk использует общее с CLI и Desktop окружение OpenCode,
            // поэтому история и настройки синхронны везде. Мастер нацелен на
            // общее (глобальное) окружение; произвольные папки тоже можно импортировать.
            _env = env ?? OpenCodeEnvironment.Global();
        }

        public OpenCodeEnvironment Data => _env;

        public List<ImportSource> DetectSources(string customPath = null, string solutionDir = null)
        {
            var sources = new List<ImportSource>();

            sources.Add(BuildStudioSource());

            var desktop = BuildDesktopSource();
            if (desktop != null)
                sources.Add(desktop);

            var project = BuildProjectSource(solutionDir);
            if (project != null)
                sources.Add(project);

            if (!string.IsNullOrWhiteSpace(customPath) && Directory.Exists(customPath))
            {
                var custom = BuildCustomSource(customPath);
                if (custom.Items.Any(i => i.Found))
                    sources.Add(custom);
            }

            return sources;
        }

        /// <summary>Общее окружение OpenCode (уже активное).</summary>
        private ImportSource BuildStudioSource()
        {
            var s = new ImportSource
            {
                Name = "OpenCode environment (shared with CLI & Desktop)",
                Kind = "studio",
                RootPath = _env.ConfigDir,
                IsActive = true,
                Summary = _env.ConfigDir
            };

            Add(s, ImportKind.Settings, "Settings (opencode.json)", _env.ConfigFile, null, false, DescribeConfig);
            Add(s, ImportKind.Commands, "Custom commands", _env.CommandsDir, null, true, CountFiles);
            Add(s, ImportKind.Theme, "TUI / theme (tui.json)", _env.TuiFile, null, false, DescribeTui);
            Add(s, ImportKind.Auth, "Credentials (auth.json)", _env.AuthFile, null, false, DescribeAuth);
            Add(s, ImportKind.History, "Chat history (opencode.db)", _env.DatabaseFile, null, false, DescribeDb);
            Add(s, ImportKind.State, "State (model.json, prompt-history)", _env.StateDir, null, true, CountFiles);

            return s;
        }

        private ImportSource BuildDesktopSource()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ai.opencode.desktop");
            var exe = FindDesktopExe();
            if (!Directory.Exists(root) && exe == null) return null;

            var s = new ImportSource
            {
                Name = "OpenCode Desktop",
                Kind = "desktop",
                RootPath = Directory.Exists(root) ? root : exe,
                Summary = exe != null ? "Desktop app shares the global CLI config/data" : "Desktop data folder"
            };

            if (Directory.Exists(root))
            {
                var backupDir = Path.Combine(OpenCodeEnvironment.StudioDir, "imported", "desktop");
                Add(s, ImportKind.Desktop, "Desktop settings (opencode.settings)",
                    Path.Combine(root, "opencode.settings"), Path.Combine(backupDir, "opencode.settings"),
                    false, DescribeSize);
                Add(s, ImportKind.Desktop, "Desktop drafts (drafts.sqlite)",
                    Path.Combine(root, "drafts.sqlite"), Path.Combine(backupDir, "drafts.sqlite"),
                    false, DescribeDb);
            }
            return s;
        }

        private ImportSource BuildProjectSource(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return null;
            var oc = Path.Combine(dir, ".opencode");
            if (!Directory.Exists(oc)) return null;

            var s = new ImportSource
            {
                Name = "Current project (.opencode)",
                Kind = "project",
                RootPath = oc,
                Summary = dir
            };
            Add(s, ImportKind.Settings, "Project config", Path.Combine(oc, "opencode.json"),
                Path.Combine(oc, "opencode.json"), false, DescribeSize);
            Add(s, ImportKind.Settings, "AGENTS.md", Path.Combine(oc, "AGENTS.md"),
                Path.Combine(oc, "AGENTS.md"), false, DescribeSize);
            return s;
        }

        private ImportSource BuildCustomSource(string path)
        {
            var s = new ImportSource
            {
                Name = "Custom folder",
                Kind = "custom",
                RootPath = path,
                Summary = path
            };

            var configFile = FindFile(path, new[] { "opencode.json", @"config\opencode.json", @".config\opencode\opencode.json" });
            if (configFile != null)
                Add(s, ImportKind.Settings, "Settings (opencode.json)", configFile, _env.ConfigFile, false, DescribeConfig);

            var tuiFile = FindFile(path, new[] { "tui.json", @"config\tui.json", @".config\opencode\tui.json" });
            if (tuiFile != null)
                Add(s, ImportKind.Theme, "TUI / theme (tui.json)", tuiFile, _env.TuiFile, false, DescribeTui);

            var authFile = FindFile(path, new[] { "auth.json", @"share\opencode\auth.json", @".local\share\opencode\auth.json" });
            if (authFile != null)
                Add(s, ImportKind.Auth, "Credentials (auth.json)", authFile, _env.AuthFile, false, DescribeAuth);

            var db = FindFile(path, new[] { "opencode.db", @"share\opencode\opencode.db", @".local\share\opencode\opencode.db" });
            if (db != null)
                Add(s, ImportKind.History, "Chat history (opencode.db)", db, _env.DatabaseFile, false, DescribeDb);

            var cmdDir = FindDir(path, new[] { "command", @"config\command", @".config\opencode\command" });
            if (cmdDir != null)
                Add(s, ImportKind.Commands, "Custom commands", cmdDir, _env.CommandsDir, true, CountFiles);

            return s;
        }

        public ImportResult Import(ImportSource source, IEnumerable<ImportItem> items)
        {
            var result = new ImportResult();
            // Замена opencode.db/auth.json под работающим OpenCode (CLI/сервер/Desktop)
            // может уронить его — поэтому при запущенном OpenCode историю и ключи не трогаем.
            bool openCodeRunning = IsOpenCodeRunning();
            foreach (var item in items)
            {
                if (item == null || !item.Importable) continue;
                try
                {
                    if ((item.Kind == ImportKind.History || item.Kind == ImportKind.Auth) && openCodeRunning)
                    {
                        result.Errors.Add($"{item.Label}: OpenCode is running - close OpenCode (including the desktop app) and try again.");
                        continue;
                    }
                    if (item.IsDirectory)
                        CopyDirectory(item.Source, item.Target, result);
                    else
                        CopyFileWithBackup(item.Source, item.Target, result);

                    // Служебные файлы SQLite (-wal/-shm) переносятся вместе с базой.
                    if (item.Kind == ImportKind.History)
                    {
                        foreach (var side in new[] { "-wal", "-shm" })
                        {
                            var sf = item.Source + side;
                            if (File.Exists(sf))
                                CopyFileWithBackup(sf, item.Target + side, result);
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{item.Label}: {ex.Message}");
                }
            }
            return result;
        }

        /// <summary>
        /// Запущен ли OpenCode (CLI, локальный сервер или Desktop-приложение).
        /// Имена opencode/opencode2/OpenCode сопоставляются без учёта регистра.
        /// </summary>
        public static bool IsOpenCodeRunning()
        {
            try
            {
                foreach (var process in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        var name = process.ProcessName;
                        if (name.Equals("opencode", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("opencode2", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("OpenCode", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            catch { }
            return false;
        }

        private void CopyFileWithBackup(string source, string target, ImportResult result)
        {
            var dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(target))
            {
                var backup = target + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(target, backup, true);
                result.Backups.Add(backup);
            }
            File.Copy(source, target, true);
            result.Copied.Add(target);
        }

        private void CopyDirectory(string sourceDir, string targetDir, ImportResult result)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(sourceDir.Length).TrimStart('\\', '/');
                CopyFileWithBackup(file, Path.Combine(targetDir, rel), result);
            }
        }

        // ---- вспомогательные методы элементов -----------------------------

        private static void Add(ImportSource src, ImportKind kind, string label,
            string source, string target, bool isDir, Func<string, string> describe)
        {
            bool found = isDir ? Directory.Exists(source) : File.Exists(source);
            string detail = found ? SafeDescribe(describe, source) : "not found";
            src.Items.Add(new ImportItem
            {
                Kind = kind,
                Label = label,
                Source = source,
                Target = target,
                IsDirectory = isDir,
                Found = found,
                Detail = detail,
                Selected = found && !string.IsNullOrEmpty(target)
            });
        }

        private static string SafeDescribe(Func<string, string> f, string path)
        {
            try { return f(path) ?? ""; } catch { return ""; }
        }

        private static string DescribeConfig(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            var providers = json["provider"] as JObject;
            var model = json["model"]?.ToString();
            var parts = new List<string>();
            if (providers != null) parts.Add($"{providers.Count} providers");
            if (!string.IsNullOrEmpty(model)) parts.Add($"model: {model}");
            return string.Join(" \u00b7 ", parts);
        }

        private static string DescribeTui(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            var plugins = json["plugin"]?.Count() ?? 0;
            var theme = json["theme"]?.ToString();
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(theme)) parts.Add($"theme: {theme}");
            if (plugins > 0) parts.Add($"{plugins} plugins");
            return string.Join(" \u00b7 ", parts);
        }

        private static string DescribeAuth(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            return string.Join(", ", json.Properties().Select(p => p.Name));
        }

        private static string DescribeDb(string path)
        {
            var fi = new FileInfo(path);
            return FormatSize(fi.Length);
        }

        private static string DescribeSize(string path)
        {
            if (Directory.Exists(path)) return CountFiles(path);
            var fi = new FileInfo(path);
            return fi.Exists ? FormatSize(fi.Length) : "";
        }

        private static string CountFiles(string dir)
        {
            if (!Directory.Exists(dir)) return "";
            var n = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
            return n == 1 ? "1 file" : $"{n} files";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return $"{bytes / 1024.0 / 1024 / 1024:0.0} GB";
            if (bytes >= 1024L * 1024L) return $"{bytes / 1024.0 / 1024:0.0} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
            return $"{bytes} B";
        }

        // ---- вспомогательные методы поиска -------------------------------

        private static string FindFile(string root, IEnumerable<string> candidates)
        {
            foreach (var c in candidates)
            {
                var p = Path.Combine(root, c);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string FindDir(string root, IEnumerable<string> candidates)
        {
            foreach (var c in candidates)
            {
                var p = Path.Combine(root, c);
                if (Directory.Exists(p)) return p;
            }
            return null;
        }

        private static string FindDesktopExe()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "@opencode-aidesktop"),
            };
            foreach (var c in candidates)
                if (Directory.Exists(c)) return c;
            return null;
        }
    }
}
