using System;
using System.Windows;
using System.Windows.Media;
using OpenCodeStudio.Services;

namespace OpenCodeStudio
{
    /// <summary>
    /// Компактное уведомление о новой версии opencode (CLI).
    /// </summary>
    public partial class OpenCodeUpdateWindow : Window
    {
        /// <summary>Пользователь нажал «Not now» в этой сессии VS.</summary>
        public static bool Suppressed { get; private set; }

        private readonly OpenCodeUpdateService _service = new OpenCodeUpdateService();
        private readonly string _latest;

        // Окно всегда тёмное, независимо от темы VS.
        private readonly System.Drawing.Color _bg = System.Drawing.Color.FromArgb(0x1e, 0x1e, 0x1e);
        private readonly System.Drawing.Color _panel = System.Drawing.Color.FromArgb(0x25, 0x25, 0x26);
        private readonly System.Drawing.Color _text = System.Drawing.Color.FromArgb(0xf1, 0xf1, 0xf1);
        private readonly System.Drawing.Color _muted = System.Drawing.Color.FromArgb(0x9a, 0x9a, 0x9a);
        private readonly System.Drawing.Color _border = System.Drawing.Color.FromArgb(0x3a, 0x3a, 0x3a);
        private readonly System.Drawing.Color _accent = System.Drawing.Color.FromArgb(0x2A, 0x78, 0xFC);

        private static SolidColorBrush Brush(System.Drawing.Color c)
            => new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));

        public OpenCodeUpdateWindow(string installed, string latest)
        {
            InitializeComponent();

            _latest = latest;

            titleText.Text = "OpenCode update available";
            subtitleText.Text = $"OpenCode {latest} is available\nYou have {installed}";
            instructionsText.Text =
                "You can update right here, or run: npm i -g opencode-ai@latest\n\n" +
                "Restart the panel after the update — the extension picks up the new version " +
                "automatically when the server starts.";

            ApplyTheme();

            // Закрытие крестиком приравнивается к «Not now»: окно не всплывает до перезапуска VS.
            Closed += (_, __) => Suppressed = true;
        }

        private void ApplyTheme()
        {
            Background = Brush(_bg);
            titleText.Foreground = Brush(_text);
            subtitleText.Foreground = Brush(_muted);
            instructionsText.Foreground = Brush(_muted);
            statusText.Foreground = Brush(_muted);
            notNowButton.Foreground = Brush(_text);
            notNowButton.Background = Brush(_panel);
            notNowButton.BorderBrush = Brush(_border);
            closeButton.Foreground = Brushes.White;
            closeButton.Background = Brush(_accent);
            closeButton.BorderBrush = Brush(_accent);
            updateButton.Foreground = Brushes.White;
            updateButton.Background = Brush(_accent);
            updateButton.BorderBrush = Brush(_accent);
        }

        private async void OnUpdateClick(object sender, RoutedEventArgs e)
        {
            updateButton.IsEnabled = false;
            notNowButton.IsEnabled = false;
            statusText.Visibility = Visibility.Visible;
            statusText.Foreground = Brush(_muted);
            statusText.Text = "Updating...";

            bool ok;
            try
            {
                ok = await _service.UpdateAsync();
            }
            catch (Exception ex)
            {
                Services.Log.Warn("OpenCode update failed: " + ex.Message);
                ok = false;
            }

            if (ok)
            {
                statusText.Text = $"Updated to {_latest}. Restart the panel to use it.";
                updateButton.Visibility = Visibility.Collapsed;
                notNowButton.Visibility = Visibility.Collapsed;
                closeButton.Visibility = Visibility.Visible;
            }
            else
            {
                statusText.Text = "Update failed. Run: npm i -g opencode-ai@latest";
                updateButton.IsEnabled = true;
                notNowButton.IsEnabled = true;
            }
        }

        private void OnNotNowClick(object sender, RoutedEventArgs e)
        {
            Suppressed = true;
            Close();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
