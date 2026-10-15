using System;
using System.IO;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Отслеживает показ окна настроек при первом запуске. Маркер хранит версию
    /// расширения, поэтому окно показывается один раз после установки или
    /// обновления, а не при каждом запуске Visual Studio.
    /// </summary>
    public static class Onboarding
    {
        private const string MarkerName = "onboarding.done";

        private static string CurrentVersion()
        {
            try { return typeof(Onboarding).Assembly.GetName().Version?.ToString() ?? "0"; }
            catch { return "0"; }
        }

        /// <summary>Показывать ли окно: нет маркера или версия расширения изменилась.</summary>
        public static bool ShouldShow()
        {
            var version = CurrentVersion();
            foreach (var path in CandidatePaths())
            {
                try { if (File.Exists(path)) return File.ReadAllText(path).Trim() != version; }
                catch { }
            }
            return true;
        }

        public static void MarkCompleted()
        {
            var version = CurrentVersion();
            foreach (var path in CandidatePaths())
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(path, version);
                    return;
                }
                catch { }
            }
        }

        private static string[] CandidatePaths()
        {
            var list = new System.Collections.Generic.List<string>();
            var extDir = OpenCodeEnvironment.ExtensionDir;
            if (!string.IsNullOrEmpty(extDir))
                list.Add(Path.Combine(extDir, MarkerName));
            list.Add(Path.Combine(OpenCodeEnvironment.StudioDir, MarkerName));
            return list.ToArray();
        }
    }
}
