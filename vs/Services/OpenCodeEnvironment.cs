using System;
using System.IO;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Окружение данных OpenCode (config / data / state / cache).
    /// OpenCode учитывает переменные базовых каталогов XDG во всех ОС, поэтому
    /// окружение полностью описывается этими четырьмя каталогами.
    ///
    /// <list type="bullet">
    ///   <item><see cref="Global"/> — стандартное окружение CLI/Desktop
    ///   (~/.config/opencode, ~/.local/share/opencode, ...).</item>
    /// </list>
    /// </summary>
    public sealed class OpenCodeEnvironment
    {
        public string ConfigDir { get; }
        public string DataDir { get; }
        public string StateDir { get; }
        public string CacheDir { get; }

        /// <summary>Читаемое человеком название этого окружения.</summary>
        public string Name { get; set; }

        public OpenCodeEnvironment(string configDir, string dataDir, string stateDir, string cacheDir)
        {
            ConfigDir = configDir;
            DataDir = dataDir;
            StateDir = stateDir;
            CacheDir = cacheDir;
        }

        /// <summary>Стандартное глобальное окружение CLI / Desktop (значения XDG по умолчанию).</summary>
        public static OpenCodeEnvironment Global()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new OpenCodeEnvironment(
                Path.Combine(home, ".config", "opencode"),
                Path.Combine(home, ".local", "share", "opencode"),
                Path.Combine(home, ".local", "state", "opencode"),
                Path.Combine(home, ".cache", "opencode")) { Name = "Global (CLI / Desktop)" };
        }

        public static string StudioDir
        {
            get
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(local, "OpenCodeJesk");
                MigrateDataDir(local, dir);
                return dir;
            }
        }

        /// <summary>
        /// Одноразовая миграция каталога данных при переименовании: если новый
        /// каталог ещё не создан, а старый (%LOCALAPPDATA%\OpenCodeStudio)
        /// существует — переносим его. При ошибке молча оставляем как есть.
        /// </summary>
        private static void MigrateDataDir(string local, string newDir)
        {
            try
            {
                if (Directory.Exists(newDir)) return;
                var oldDir = Path.Combine(local, "OpenCodeStudio");
                if (Directory.Exists(oldDir)) Directory.Move(oldDir, newDir);
            }
            catch { }
        }

        public string ConfigFile => Path.Combine(ConfigDir, "opencode.json");
        public string TuiFile => Path.Combine(ConfigDir, "tui.json");
        public string CommandsDir => Path.Combine(ConfigDir, "command");
        public string AuthFile => Path.Combine(DataDir, "auth.json");
        public string DatabaseFile => Path.Combine(DataDir, "opencode.db");

        /// <summary>Каталог текущей запущенной сборки расширения.</summary>
        public static string ExtensionDir
        {
            get
            {
                try
                {
                    var loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    return string.IsNullOrEmpty(loc) ? null : Path.GetDirectoryName(loc);
                }
                catch { return null; }
            }
        }
    }
}
