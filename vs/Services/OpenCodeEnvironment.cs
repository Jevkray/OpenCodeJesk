using System;
using System.IO;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Represents an OpenCode data environment (config / data / state / cache).
    /// OpenCode honours the XDG base-directory variables on every OS, so an
    /// environment is fully described by these four directories.
    ///
    /// <list type="bullet">
    ///   <item><see cref="Global"/> - the default CLI/desktop environment
    ///   (~/.config/opencode, ~/.local/share/opencode, ...).</item>
    ///   <item><see cref="ForStudio"/> - OpenCode Studio's own isolated
    ///   environment under %LOCALAPPDATA%\OpenCodeStudio\opencode, so the
    ///   extension never mutates the user's CLI setup.</item>
    /// </list>
    /// </summary>
    public sealed class OpenCodeEnvironment
    {
        public string ConfigDir { get; }
        public string DataDir { get; }
        public string StateDir { get; }
        public string CacheDir { get; }

        /// <summary>Human-readable label for this environment.</summary>
        public string Name { get; set; }

        public OpenCodeEnvironment(string configDir, string dataDir, string stateDir, string cacheDir)
        {
            ConfigDir = configDir;
            DataDir = dataDir;
            StateDir = stateDir;
            CacheDir = cacheDir;
        }

        /// <summary>The default global CLI / desktop environment (XDG defaults).</summary>
        public static OpenCodeEnvironment Global()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new OpenCodeEnvironment(
                Path.Combine(home, ".config", "opencode"),
                Path.Combine(home, ".local", "share", "opencode"),
                Path.Combine(home, ".local", "state", "opencode"),
                Path.Combine(home, ".cache", "opencode")) { Name = "Global (CLI / Desktop)" };
        }

        /// <summary>OpenCode Studio's private environment.</summary>
        public static OpenCodeEnvironment ForStudio()
        {
            var root = StudioRoot;
            return new OpenCodeEnvironment(
                Path.Combine(root, "config"),
                Path.Combine(root, "data"),
                Path.Combine(root, "state"),
                Path.Combine(root, "cache")) { Name = Brand.Name };
        }

        public static string StudioDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenCodeStudio");

        public static string StudioRoot => Path.Combine(StudioDir, "opencode");

        public string ConfigFile => Path.Combine(ConfigDir, "opencode.json");
        public string TuiFile => Path.Combine(ConfigDir, "tui.json");
        public string CommandsDir => Path.Combine(ConfigDir, "command");
        public string ThemesDir => Path.Combine(ConfigDir, "themes");
        public string AuthFile => Path.Combine(DataDir, "auth.json");
        public string DatabaseFile => Path.Combine(DataDir, "opencode.db");

        // XDG base directories (the parents of the opencode-specific folders).
        public string XdgConfigHome => Path.GetDirectoryName(ConfigDir);
        public string XdgDataHome => Path.GetDirectoryName(DataDir);
        public string XdgStateHome => Path.GetDirectoryName(StateDir);
        public string XdgCacheHome => Path.GetDirectoryName(CacheDir);

        /// <summary>Applies this environment's XDG variables to a child process.</summary>
        public void ApplyTo(System.Diagnostics.ProcessStartInfo psi)
        {
            try
            {
                psi.EnvironmentVariables["XDG_CONFIG_HOME"] = XdgConfigHome;
                psi.EnvironmentVariables["XDG_DATA_HOME"] = XdgDataHome;
                psi.EnvironmentVariables["XDG_STATE_HOME"] = XdgStateHome;
                psi.EnvironmentVariables["XDG_CACHE_HOME"] = XdgCacheHome;
            }
            catch { }
        }

        /// <summary>Directory of the currently running extension assembly.</summary>
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
