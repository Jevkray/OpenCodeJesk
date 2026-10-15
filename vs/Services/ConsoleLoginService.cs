using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Захватывает сессионную cookie opencode.ai через WebView2 после входа
    /// пользователя в консоль. Пока не подключён к UI.
    /// </summary>
    internal static class ConsoleLoginService
    {
        public const string Home = "https://opencode.ai";
        public const string AuthHome = "https://auth.opencode.ai";
        public const string LoginUrl = "https://opencode.ai/console/login";

        /// <summary>Собирает cookie домашнего домена и auth-поддомена в "name=value; ...".</summary>
        public static async Task<string> ReadCookieAsync(CoreWebView2 core)
        {
            var parts = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var url in new[] { Home, AuthHome })
            {
                var cookies = await core.CookieManager.GetCookiesAsync(url);
                foreach (var c in cookies)
                {
                    if (string.IsNullOrEmpty(c.Name) || !seen.Add(c.Name)) continue;
                    parts.Add(c.Name + "=" + c.Value);
                }
            }
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        /// <summary>
        /// Определяет, что opencode вернул навигацию на свою страницу (вход выполнен):
        /// домашний домен, но не шаги авторизации/входа.
        /// </summary>
        public static bool IsReturnedToApp(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!url.StartsWith(Home, StringComparison.OrdinalIgnoreCase)) return false;
            return url.IndexOf("/authorize", StringComparison.OrdinalIgnoreCase) < 0
                && url.IndexOf("/auth/callback", StringComparison.OrdinalIgnoreCase) < 0
                && url.IndexOf("/console/login", StringComparison.OrdinalIgnoreCase) < 0
                && url.IndexOf("/console/auth/", StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}
