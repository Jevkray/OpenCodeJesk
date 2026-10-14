using System;
using System.IO;
using Newtonsoft.Json;
using OpenCodeStudio.Models;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Общий реестр сервера (server.json в StudioDir): позволяет нескольким
    /// окнам Visual Studio переиспользовать один запущенный сервер OpenCode,
    /// чтобы все клиенты делили один набор сессий и поток событий.
    /// </summary>
    internal static class SharedServerRegistry
    {
        private static string RegistryPath => Path.Combine(
            OpenCodeEnvironment.StudioDir, "server.json");

        /// <summary>Прочитать записанный сервер как ServerInfo или null.</summary>
        public static ServerInfo Read()
        {
            try
            {
                if (!File.Exists(RegistryPath)) return null;
                var record = JsonConvert.DeserializeObject<SharedServerRecord>(
                    File.ReadAllText(RegistryPath));
                if (record == null || record.Port <= 0) return null;
                return new ServerInfo(
                    string.IsNullOrEmpty(record.Host) ? "127.0.0.1" : record.Host,
                    record.Port)
                {
                    Username = record.Username,
                    Password = record.Password
                };
            }
            catch { return null; }
        }

        /// <summary>Сырой разбор server.json (для проверки ветки версии) или null.</summary>
        public static SharedServerRecord ReadRecord()
        {
            try
            {
                if (!File.Exists(RegistryPath)) return null;
                return JsonConvert.DeserializeObject<SharedServerRecord>(
                    File.ReadAllText(RegistryPath));
            }
            catch { return null; }
        }

        /// <summary>Удалить запись о сервере.</summary>
        public static void Remove()
        {
            try { if (File.Exists(RegistryPath)) File.Delete(RegistryPath); } catch { }
        }

        /// <summary>Записать сведения о сервере в общий реестр.</summary>
        public static void Write(ServerInfo info, string binaryPath, long binaryStamp, int pid)
        {
            try
            {
                Directory.CreateDirectory(OpenCodeEnvironment.StudioDir);
                var record = new SharedServerRecord
                {
                    Host = info.Host,
                    Port = info.Port,
                    Username = info.Username,
                    Password = info.Password,
                    Binary = binaryPath,
                    BinaryStamp = binaryStamp,
                    Pid = pid
                };
                File.WriteAllText(RegistryPath, JsonConvert.SerializeObject(record));
            }
            catch { }
        }

        /// <summary>
        /// Устарел ли записанный бинарь относительно текущего: путь или метка
        /// времени изменения не совпадают. Вызывающий передаёт, что сравнивать.
        /// </summary>
        public static bool IsStale(string currentBinary, long currentStamp)
        {
            try
            {
                if (!File.Exists(RegistryPath)) return false;
                var rec = JsonConvert.DeserializeObject<SharedServerRecord>(File.ReadAllText(RegistryPath));
                if (rec == null) return false;
                if (!string.IsNullOrEmpty(rec.Binary) && !string.IsNullOrEmpty(currentBinary)
                    && !string.Equals(rec.Binary, currentBinary, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (rec.BinaryStamp != 0 && currentStamp != 0 && rec.BinaryStamp != currentStamp)
                    return true;
            }
            catch { }
            return false;
        }
    }

    /// <summary>Запись о запущенном сервере OpenCode, общем для клиентов.</summary>
    internal sealed class SharedServerRecord
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Binary { get; set; }
        public long BinaryStamp { get; set; }
        public int Pid { get; set; }
    }
}
