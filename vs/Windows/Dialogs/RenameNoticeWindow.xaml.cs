using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using OpenCodeStudio.Services;

namespace OpenCodeStudio
{
    /// <summary>
    /// Уведомление о переименовании OpenCode Studio → OpenCode Jesk.
    /// Показывается только в сборке Studio.
    /// </summary>
    public partial class RenameNoticeWindow : Window
    {
        private const string DismissMarker = "rename-notice.done";

        // Окно всегда тёмное, независимо от темы VS.
        private static readonly System.Drawing.Color _bg = System.Drawing.Color.FromArgb(0x1e, 0x1e, 0x1e);
        private static readonly System.Drawing.Color _panel = System.Drawing.Color.FromArgb(0x25, 0x25, 0x26);
        private static readonly System.Drawing.Color _text = System.Drawing.Color.FromArgb(0xf1, 0xf1, 0xf1);
        private static readonly System.Drawing.Color _border = System.Drawing.Color.FromArgb(0x3a, 0x3a, 0x3a);

        private static SolidColorBrush Brush(System.Drawing.Color c)
            => new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));

        public RenameNoticeWindow()
        {
            InitializeComponent();
            ApplyTheme();
            UpdateDeadline();
            try { dontShowCheck.IsChecked = File.Exists(MarkerPath); } catch { }
        }

        private static string MarkerPath
            => Path.Combine(OpenCodeEnvironment.StudioDir, DismissMarker);

        private void ApplyTheme()
        {
            Background = Brush(_bg);

            foreach (var button in new[] { laterButton, getButton })
            {
                button.Foreground = Brush(_text);
                button.Background = Brush(_panel);
                button.BorderBrush = Brush(_border);
            }
            getButton.Foreground = Brushes.White;
            getButton.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x6F, 0xD8));
            getButton.BorderBrush = getButton.Background;
        }

        private void UpdateDeadline()
        {
            try
            {
                var ends = DateTime.ParseExact(Brand.SupportEndsIso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var days = (ends.Date - DateTime.Today).Days;
                if (days > 1)
                    deadlineText.Text = $"Support for OpenCode Studio ends on {ends:dd.MM.yyyy} ({days} days left). After that date only OpenCode Jesk is available.";
                else if (days >= 0)
                    deadlineText.Text = $"Support for OpenCode Studio ends today ({ends:dd.MM.yyyy}). Please switch to OpenCode Jesk.";
                else
                    deadlineText.Text = $"Support for OpenCode Studio ended on {ends:dd.MM.yyyy}. Please install OpenCode Jesk to keep receiving updates.";
            }
            catch { }
        }

        private void PersistIfRequested()
        {
            if (dontShowCheck.IsChecked != true) return;
            try
            {
                Directory.CreateDirectory(OpenCodeEnvironment.StudioDir);
                File.WriteAllText(MarkerPath, DateTime.Now.ToString("o"));
            }
            catch { }
        }

        private void OnGetClick(object sender, RoutedEventArgs e)
        {
            PersistIfRequested();
            try { Process.Start(new ProcessStartInfo(Brand.SuccessorUrl) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("Open rename link failed", ex); }
            Close();
        }

        private void OnLaterClick(object sender, RoutedEventArgs e)
        {
            PersistIfRequested();
            Close();
        }
    }
}
