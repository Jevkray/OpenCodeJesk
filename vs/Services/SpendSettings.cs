using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Настройки OpenCode Jesk. Читаются кастомным окном Settings и сервисами
    /// через статический <see cref="Current"/>.
    /// </summary>
    public class SpendSettings : DialogPage
    {
        /// <summary>Текущий экземпляр настроек, доступный сервисам без прокидывания.</summary>
        public static SpendSettings Current { get; set; }

        [Category("Interface")]
        [DisplayName("Interface mode")]
        [Description("Desktop opens the Web panel; CLI opens the OpenCode terminal inside the same panel (no Visual Studio terminal chrome). Independent of the OpenCode v1/v2 setting.")]
        public InterfaceMode InterfaceMode { get; set; } = InterfaceMode.Web;

        [Category("Interface")]
        [DisplayName("Use OpenCode v2.x (preview)")]
        [Description("Prefer the OpenCode 2 beta binary (opencode2) when both v1 and v2 are installed.")]
        public bool UseOpenCodeV2 { get; set; } = false;

        [Browsable(false)]
        public bool AutoOpenOnSolutionOpen { get; set; } = false;

        /// <summary>Язык интерфейса окна настроек: "en" или "ru".</summary>
        [Browsable(false)]
        public string Language { get; set; } = "en";

        /// <summary>Кнопка входа в OpenCode Console (лимиты) в панели.</summary>
        [Browsable(false)]
        public bool ShowConsoleButton { get; set; } = true;

        [Browsable(false)]
        public int WebZoomPercent { get; set; } = 100; // 50..200

        [Browsable(false)]
        public string TerminalFontFamily { get; set; } = "Consolas";

        [Browsable(false)]
        public int TerminalFontSize { get; set; } = 13; // 8..28

        [Browsable(false)]
        public bool TerminalCursorBlink { get; set; } = true;

        [Browsable(false)]
        public int TerminalScrollback { get; set; } = 5000; // 500..50000

        [Browsable(false)]
        public string CliArguments { get; set; } = "";

        [Browsable(false)]
        public StartDirectoryMode StartDirectory { get; set; } = StartDirectoryMode.GitRoot;

        [Browsable(false)]
        public string OpenCodePath { get; set; } = ""; // пусто = авто

        [Browsable(false)]
        public int PreferredPort { get; set; } = 4096;

        [Browsable(false)]
        public string ServeArguments { get; set; } = "";

        [Browsable(false)]
        public int IdleStopMinutes { get; set; } = 0; // 0 = не останавливать

        [Category("Updates")]
        [DisplayName("Check for new versions")]
        [Description("Check the Visual Studio Marketplace for a newer " + Brand.Name + " version and show a notification. Disable it if you prefer to stay on your current version.")]
        public bool CheckForUpdates { get; set; } = true;

        [Browsable(false)]
        public int UpdateCheckMinutes { get; set; } = 60; // 5..1440

        [Browsable(false)]
        public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;

        [Browsable(false)]
        public bool ShowRatingPopup { get; set; } = true;

        [Browsable(false)]
        public bool RatingPopupDismissed { get; set; } = false;

        public static event Action Applied;

        /// <summary>Сигнал перед установкой CLI: расширение освобождает файлы npm-шимов.</summary>
        public static event Action CliInstallStarting;

        public static void RaiseCliInstallStarting() { try { CliInstallStarting?.Invoke(); } catch { } }

        public void NotifyApplied() => Applied?.Invoke();

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            try { Applied?.Invoke(); } catch { }
        }
    }

    public enum InterfaceMode { Web, CLI }
    public enum StartDirectoryMode { GitRoot, SolutionDir, ProjectDir, LastUsed }
    public enum UpdateChannel { Stable, Beta }
}
