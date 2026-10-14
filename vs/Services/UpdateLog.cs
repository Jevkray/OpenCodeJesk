using System;
using System.Collections.Generic;
using System.IO;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Простой журнал проверок и установок обновлений:
    /// %LOCALAPPDATA%\OpenCodeStudio\logs\updates.log. Ошибки глотаются.
    /// </summary>
    public static class UpdateLog
    {
        private static readonly object Sync = new object();

        private static string LogPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpenCodeStudio", "logs");
                return Path.Combine(dir, "updates.log");
            }
        }

        /// <summary>Добавляет строку "[yyyy-MM-dd HH:mm:ss] message".</summary>
        public static void Write(string message)
        {
            try
            {
                var path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                lock (Sync) { File.AppendAllText(path, line); }
            }
            catch { }
        }

        /// <summary>Возвращает последние maxLines строк (или "" если файла нет).</summary>
        public static string ReadTail(int maxLines = 200)
        {
            try
            {
                var path = LogPath;
                if (!File.Exists(path)) return "";
                var lines = File.ReadAllLines(path);
                if (lines.Length <= maxLines) return string.Join(Environment.NewLine, lines);
                var tail = new List<string>(maxLines);
                for (var i = lines.Length - maxLines; i < lines.Length; i++) tail.Add(lines[i]);
                return string.Join(Environment.NewLine, tail);
            }
            catch { return ""; }
        }
    }
}
