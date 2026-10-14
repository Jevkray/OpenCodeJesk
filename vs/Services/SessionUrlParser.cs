using System;
using System.IO;
using System.Linq;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Разбор URL веб-панели OpenCode: извлекает активную сессию и каталог проекта.
    /// </summary>
    internal static class SessionUrlParser
    {
        /// <summary>
        /// Возвращает (directory, sessionId) из URL панели или null, если URL
        /// не принадлежит серверу либо в нём нет идентификатора сессии.
        /// </summary>
        public static Tuple<string, string> Parse(string url, string serverBaseUrl)
        {
            if (string.IsNullOrEmpty(serverBaseUrl) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Authority != new Uri(serverBaseUrl).Authority) return null;
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            var session = segments.LastOrDefault(s => s.StartsWith("ses_", StringComparison.Ordinal));
            if (session == null) return null;
            string directory = null;
            if (segments.Length >= 2 && segments[1] == "session")
            {
                try
                {
                    var encoded = segments[0].Replace('-', '+').Replace('_', '/');
                    encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
                    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    if (Path.IsPathRooted(decoded) && Directory.Exists(decoded)) directory = decoded;
                }
                catch (FormatException) { }
            }
            return Tuple.Create(directory, session);
        }
    }
}
