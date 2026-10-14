using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using OpenCodeStudio.Services;

namespace OpenCodeStudio
{
    /// <summary>
    /// Компактное уведомление о новой версии расширения на Marketplace.
    /// </summary>
    public partial class UpdateNotificationWindow : Window
    {
        private const string MarketplaceUrl = Brand.MarketplaceUrl;

        /// <summary>Пользователь нажал «Not now» в этой сессии VS.</summary>
        public static bool Suppressed { get; private set; }

        private System.Drawing.Color _bg = System.Drawing.Color.FromArgb(0x1e, 0x1e, 0x1e);
        private System.Drawing.Color _panel = System.Drawing.Color.FromArgb(0x25, 0x25, 0x26);
        private System.Drawing.Color _text = System.Drawing.Color.FromArgb(0xf1, 0xf1, 0xf1);
        private System.Drawing.Color _muted = System.Drawing.Color.FromArgb(0x9a, 0x9a, 0x9a);
        private System.Drawing.Color _accent = System.Drawing.Color.FromArgb(0x00, 0x78, 0xd4);

        private readonly Action _openExtensionManager;

        private static SolidColorBrush Brush(System.Drawing.Color c)
            => new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));

        public UpdateNotificationWindow(UpdateInfo info, Action openExtensionManager = null)
        {
            InitializeComponent();

            _openExtensionManager = openExtensionManager;

            titleText.Text = $"{Brand.Name} {info.Latest} is available";
            subtitleText.Text = $"You have {info.Installed}";
            notesText.Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? "See the Marketplace page for details."
                : info.ReleaseNotes;

            ApplyTheme();

            // Закрытие крестиком приравнивается к «Not now»: окно не всплывает до перезапуска VS.
            Closed += (_, __) => Suppressed = true;
        }

        private void ApplyTheme()
        {
            try
            {
                _bg = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
                _panel = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTabGradientBeginColorKey);
                _text = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
                _muted = VSColorTheme.GetThemedColor(EnvironmentColors.CommandBarTextInactiveColorKey);
                _accent = VSColorTheme.GetThemedColor(EnvironmentColors.AccentBorderColorKey);
            }
            catch { }

            Background = Brush(_bg);
            titleText.Foreground = Brush(_text);
            subtitleText.Foreground = Brush(_muted);
            hintText.Foreground = Brush(_muted);
            whatsNewLabel.Foreground = Brush(_text);
            notesText.Background = Brush(_bg);
            notesText.Foreground = Brush(_text);
            notesText.BorderBrush = Brush(_muted);
            notNowButton.Foreground = Brush(_text);
            notNowButton.Background = Brush(_panel);
            notNowButton.BorderBrush = Brush(_muted);
            marketplaceButton.Foreground = Brush(_text);
            marketplaceButton.Background = Brush(_panel);
            marketplaceButton.BorderBrush = Brush(_muted);
            extensionManagerButton.Foreground = Brushes.White;
            extensionManagerButton.Background = Brush(_accent);
            extensionManagerButton.BorderBrush = Brush(_accent);
        }

        private void OnExtensionManagerClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_openExtensionManager == null)
                    throw new InvalidOperationException("Extension Manager callback is not available.");
                _openExtensionManager();
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to open Extension Manager: " + ex.Message);
                OpenMarketplace();
            }
            Suppressed = true;
            Close();
        }

        private void OnMarketplaceClick(object sender, RoutedEventArgs e)
        {
            OpenMarketplace();
            Suppressed = true;
            Close();
        }

        private static void OpenMarketplace()
        {
            try
            {
                Process.Start(new ProcessStartInfo(MarketplaceUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to open Marketplace page: " + ex.Message);
            }
        }

        private void OnNotNowClick(object sender, RoutedEventArgs e)
        {
            Suppressed = true;
            Close();
        }
    }
}
