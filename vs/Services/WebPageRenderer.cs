using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.PlatformUI;
using OpenCodeStudio.Resources;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Генерация служебных страниц веб-панели (загрузка и ошибка):
    /// тема Visual Studio подставляется в HTML-шаблоны из ресурсов.
    /// </summary>
    internal static class WebPageRenderer
    {
        private static readonly string ErrorPageTemplate;
        private static readonly string LoadingPageTemplate;

        static WebPageRenderer()
        {
            var assembly = Assembly.GetExecutingAssembly();
            ErrorPageTemplate = LoadResourceString(assembly, "OpenCodeStudio.Resources.ErrorPage.html");
            LoadingPageTemplate = LoadResourceString(assembly, "OpenCodeStudio.Resources.LoadingPage.html");
        }

        private static string LoadResourceString(Assembly assembly, string name)
        {
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>Страница загрузки с указанным сообщением.</summary>
        public static string BuildLoadingPage(string message)
        {
            return LoadingPageTemplate
                .Replace("{theme-style}", GetThemeStyleBlock())
                .Replace("{message}", message);
        }

        /// <summary>Страница ошибки с кнопками повтора и настроек.</summary>
        public static string BuildErrorPage(string message, bool showRetry, bool showSettings, string settingsLabel)
        {
            var escapedMessage = System.Net.WebUtility.HtmlEncode(message)
                .Replace("\n", "<br>")
                .Replace("\\n", "<br>");
            var retryButton = showRetry
                ? $@"<button id=""retryBtn"" onclick=""handleRetry()"">{StringsHelper.UIRetry}</button>"
                : "";
            var retryScript = showRetry
                ? @"var disabled=false;function handleRetry(){if(disabled)return;disabled=true;var b=document.getElementById('retryBtn');b.disabled=true;b.style.opacity='0.5';b.style.cursor='not-allowed';try{window.chrome.webview.postMessage('retry')}catch(e){}}"
                : "";
            var settingsButton = showSettings
                ? $@"<button id=""settingsBtn"" onclick=""openSettings()"" style=""margin-left:8px"">{System.Net.WebUtility.HtmlEncode(settingsLabel)}</button>"
                : "";
            var settingsScript = showSettings
                ? @"function openSettings(){try{window.chrome.webview.postMessage('settings')}catch(e){}}"
                : "";

            return ErrorPageTemplate
                .Replace("{theme-style}", GetThemeStyleBlock())
                .Replace("{message}", escapedMessage)
                .Replace("{retryButton}", retryButton)
                .Replace("{retryScript}", retryScript)
                .Replace("{settingsButton}", settingsButton)
                .Replace("{settingsScript}", settingsScript);
        }

        private static Dictionary<string, string> GetThemeColors()
        {
            var colors = new Dictionary<string, string>();
            try
            {
                colors["bg"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey));
                colors["textPrimary"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey));
                colors["textSecondary"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.CommandBarTextInactiveColorKey));
                colors["textAccent"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.AccentBorderColorKey));
                colors["border"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBorderColorKey));
            }
            catch { }
            return colors;
        }

        private static string GetThemeStyleBlock()
        {
            var c = GetThemeColors();
            string bg = c.TryGetValue("bg", out var v0) ? v0 : "#252526";
            string text = c.TryGetValue("textPrimary", out var v1) ? v1 : "#f1f1f1";
            string textMuted = c.TryGetValue("textSecondary", out var v2) ? v2 : "#999999";
            string accent = c.TryGetValue("textAccent", out var v3) ? v3 : "#007acc";
            string border = c.TryGetValue("border", out var v4) ? v4 : "#434346";
            var accentHover = LighterHex(accent, 0.20);
            var spinnerTrack = LighterHex(bg, 0.12);

            return $@"<style id=""ocstudio-page-theme"">
    :root {{
        --vs-bg: {bg};
        --vs-text: {text};
        --vs-text-muted: {textMuted};
        --vs-accent: {accent};
        --vs-accent-hover: {accentHover};
        --vs-border: {border};
        --vs-spinner-track: {spinnerTrack};
    }}
</style>";
        }

        private static string LighterHex(string hex, double amount)
        {
            try
            {
                int r = Convert.ToInt32(hex.Substring(1, 2), 16);
                int g = Convert.ToInt32(hex.Substring(3, 2), 16);
                int b = Convert.ToInt32(hex.Substring(5, 2), 16);
                r = Math.Min(255, (int)(r + (255 - r) * amount));
                g = Math.Min(255, (int)(g + (255 - g) * amount));
                b = Math.Min(255, (int)(b + (255 - b) * amount));
                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch { return hex; }
        }

        private static string ToHtml(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
