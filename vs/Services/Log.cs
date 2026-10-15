using System;
using System.IO;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Minimal, dependency-free file logger for OpenCode Jesk. Writes to
    /// %LOCALAPPDATA%\OpenCodeStudio\logs\opencode-studio.log so users can
    /// diagnose connection issues without a debugger attached.
    /// </summary>
    public static class Log
    {
        private static readonly object Sync = new object();
        private static string _path;
        private static bool _disabled;

        private static string LogPath
        {
            get
            {
                if (_path != null) return _path;
                try
                {
                    var dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "OpenCodeStudio", "logs");
                    Directory.CreateDirectory(dir);
                    _path = Path.Combine(dir, "opencode-studio.log");
                }
                catch
                {
                    _disabled = true;
                    _path = null;
                }
                return _path;
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);

        public static void Error(string message) => Write("ERROR", message);

        public static void Error(string message, Exception ex)
            => Write("ERROR", message + " :: " + ex);

        private static void Write(string level, string message)
        {
            if (_disabled) return;
            try
            {
                var path = LogPath;
                if (path == null) return;
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                lock (Sync)
                {
                    TrimIfLarge(path);
                    File.AppendAllText(path, line);
                }
            }
            catch { _disabled = true; }
        }

        private static void TrimIfLarge(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length < 2 * 1024 * 1024) return;
                var backup = path + ".old";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(path, backup);
            }
            catch { }
        }
    }
}
