using System;

namespace OpenCodeStudio.Services
{
    /// <summary>Сырые значения формы настроек (дно между окном и хранилищем).</summary>
    public sealed class SettingsForm
    {
        public int InterfaceIndex;
        public bool UseOpenCodeV2;
        public double WebZoom;
        public bool AutoOpen;
        public bool ShowConsoleButton;
        public string FontFamily;
        public double FontSize;
        public bool CursorBlink;
        public string ScrollbackText;
        public string CliArguments;
        public int StartDirectoryIndex;
        public string OpenCodePath;
        public string PortText;
        public string ServeArguments;
        public string IdleStopText;
        public bool CheckForUpdates;
        public string UpdateMinutesText;
        public int ChannelIndex;
        public bool ShowRatingPopup;
        public bool RussianLanguage;
    }

    /// <summary>Чтение/запись настроек без зависимости от контролов WPF.</summary>
    public static class SettingsEditor
    {
        /// <summary>Строит DTO из модели с теми же дефолтами, что и раньше.</summary>
        public static SettingsForm FromSettings(SpendSettings s) => new SettingsForm
        {
            InterfaceIndex = s?.InterfaceMode == InterfaceMode.CLI ? 1 : 0,
            UseOpenCodeV2 = s?.UseOpenCodeV2 == true,
            WebZoom = s?.WebZoomPercent ?? 100,
            AutoOpen = s?.AutoOpenOnSolutionOpen ?? false,
            ShowConsoleButton = s?.ShowConsoleButton ?? true,
            FontFamily = s?.TerminalFontFamily ?? "Consolas",
            FontSize = s?.TerminalFontSize ?? 13,
            CursorBlink = s?.TerminalCursorBlink ?? true,
            ScrollbackText = (s?.TerminalScrollback ?? 5000).ToString(),
            CliArguments = s?.CliArguments ?? "",
            StartDirectoryIndex = (int)(s?.StartDirectory ?? StartDirectoryMode.GitRoot),
            OpenCodePath = s?.OpenCodePath ?? "",
            PortText = (s?.PreferredPort ?? 4096).ToString(),
            ServeArguments = s?.ServeArguments ?? "",
            IdleStopText = (s?.IdleStopMinutes ?? 0).ToString(),
            CheckForUpdates = s?.CheckForUpdates ?? true,
            UpdateMinutesText = (s?.UpdateCheckMinutes ?? 60).ToString(),
            ChannelIndex = (int)(s?.Channel ?? UpdateChannel.Stable),
            ShowRatingPopup = s?.ShowRatingPopup ?? true,
            RussianLanguage = s?.Language == "ru",
        };

        /// <summary>Переносит значения DTO в модель с прежним приведением/границами.</summary>
        public static void Apply(SettingsForm f, SpendSettings s)
        {
            if (s == null) return;

            s.InterfaceMode = f.InterfaceIndex == 1 ? InterfaceMode.CLI : InterfaceMode.Web;
            s.UseOpenCodeV2 = f.UseOpenCodeV2;
            s.WebZoomPercent = Clamp((int)f.WebZoom, 50, 200);
            s.AutoOpenOnSolutionOpen = f.AutoOpen;
            s.ShowConsoleButton = f.ShowConsoleButton;
            s.Language = f.RussianLanguage ? "ru" : "en";

            s.TerminalFontFamily = f.FontFamily ?? "Consolas";
            s.TerminalFontSize = Clamp((int)f.FontSize, 8, 28);
            s.TerminalCursorBlink = f.CursorBlink;
            s.TerminalScrollback = ParseInt(f.ScrollbackText, 5000, 500, 50000);
            s.CliArguments = f.CliArguments ?? "";
            s.StartDirectory = (StartDirectoryMode)Math.Max(0, f.StartDirectoryIndex);

            s.OpenCodePath = (f.OpenCodePath ?? "").Trim();
            s.PreferredPort = ParseInt(f.PortText, 4096, 1, 65535);
            s.ServeArguments = f.ServeArguments ?? "";
            s.IdleStopMinutes = ParseInt(f.IdleStopText, 0, 0, 100000);

            s.CheckForUpdates = f.CheckForUpdates;
            s.UpdateCheckMinutes = ParseInt(f.UpdateMinutesText, 60, 5, 1440);
            s.Channel = (UpdateChannel)Math.Max(0, f.ChannelIndex);
            s.ShowRatingPopup = f.ShowRatingPopup;
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static int ParseInt(string text, int fallback, int min, int max)
            => int.TryParse((text ?? "").Trim(), out var v) ? Clamp(v, min, max) : fallback;
    }
}
