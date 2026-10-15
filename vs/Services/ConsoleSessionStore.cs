using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Хранит сессионную cookie opencode.ai (для API консоли) в зашифрованном
    /// виде через DPAPI текущего пользователя.
    /// </summary>
    internal static class ConsoleSessionStore
    {
        private static string FilePath => Path.Combine(
            OpenCodeEnvironment.StudioDir, "console-session.dat");

        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var plain = ProtectedData.Unprotect(
                    File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
                var cookie = Encoding.UTF8.GetString(plain);
                return string.IsNullOrWhiteSpace(cookie) ? null : cookie;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read console session", ex);
                return null;
            }
        }

        public static void Save(string cookie)
        {
            try
            {
                Directory.CreateDirectory(OpenCodeEnvironment.StudioDir);
                var encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(cookie), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FilePath, encrypted);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save console session", ex);
            }
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }
    }
}
