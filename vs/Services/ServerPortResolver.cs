using System;
using System.IO;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Подбор порта для локального сервера opencode. Стабильный origin: сначала
    /// предпочтительный порт (из настроек), затем последний успешно
    /// использованный, и лишь потом случайный свободный.
    /// </summary>
    public static class ServerPortResolver
    {
        private const int PreferredPort = 4096;

        private static string LastPortPath => Path.Combine(
            OpenCodeEnvironment.StudioDir, "last-port.txt");

        /// <summary>Возвращает свободный порт (стабильный, если возможно).</summary>
        public static int Resolve()
        {
            var preferred = SpendSettings.Current?.PreferredPort ?? PreferredPort;
            if (preferred < 1 || preferred > 65535) preferred = PreferredPort;
            if (IsPortFree(preferred)) { SaveLastPort(preferred); return preferred; }

            var last = ReadLastPort();
            if (last > 0 && last != preferred && IsPortFree(last)) return last;

            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                listener.Start();
                var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                SaveLastPort(port);
                return port;
            }
            catch { return 0; }
        }

        private static int ReadLastPort()
        {
            try
            {
                if (!File.Exists(LastPortPath)) return 0;
                return int.TryParse(File.ReadAllText(LastPortPath).Trim(), out var p) ? p : 0;
            }
            catch { return 0; }
        }

        private static void SaveLastPort(int port)
        {
            try
            {
                Directory.CreateDirectory(OpenCodeEnvironment.StudioDir);
                File.WriteAllText(LastPortPath, port.ToString());
            }
            catch { }
        }

        private static bool IsPortFree(int port)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(
                    System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch { return false; }
        }
    }
}
