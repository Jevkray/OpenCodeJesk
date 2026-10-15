using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using OpenCodeJesk.Services;

namespace OpenCodeJesk
{
    /// <summary>
    /// Небольшое окно с просьбой оценить расширение в Marketplace. Показывается
    /// один раз за сессию, если пользователь не отключил напоминание.
    /// </summary>
    public partial class RatingWindow : Window
    {
        /// <summary>Не показывать напоминание снова: чекбокс или «Я уже оставил».</summary>
        public bool DontShowAgain { get; private set; }

        // Окно всегда тёмное, независимо от темы VS.
        private readonly System.Drawing.Color _panel = System.Drawing.Color.FromArgb(0x25, 0x25, 0x26);
        private readonly System.Drawing.Color _text = System.Drawing.Color.FromArgb(0xf1, 0xf1, 0xf1);
        private readonly System.Drawing.Color _muted = System.Drawing.Color.FromArgb(0x9a, 0x9a, 0x9a);
        private readonly System.Drawing.Color _border = System.Drawing.Color.FromArgb(0x3a, 0x3a, 0x3a);

        private static SolidColorBrush Brush(System.Drawing.Color c)
            => new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));

        public RatingWindow()
        {
            InitializeComponent();
            ApplyLanguage();
            ApplyTheme();
            Closed += (_, __) => { if (dontShowCheck.IsChecked == true) DontShowAgain = true; };
        }

        private void ApplyLanguage()
        {
            var ru = Services.SpendSettings.Current?.Language == "ru";
            titleText.Text = ru ? ("Нравится " + Brand.Name + "?") : ("Enjoying " + Brand.Name + "?");
            bodyText.Text = ru
                ? "Если расширение вам помогает, оставьте, пожалуйста, короткий отзыв в Marketplace. Это занимает минуту."
                : "If the extension helps you, please leave a short review on the Marketplace. It takes a minute.";
            dontShowCheck.Content = ru ? "Больше не показывать" : "Don't show this again";
            alreadyButton.Content = ru ? "Я уже оставил" : "I already did";
            reviewButton.Content = ru ? "Оставить отзыв" : "Leave a review";
        }

        private void ApplyTheme()
        {
            titleText.Foreground = Brush(_text);
            bodyText.Foreground = Brush(_muted);
            alreadyButton.Foreground = Brush(_text);
            alreadyButton.Background = Brush(_panel);
            alreadyButton.BorderBrush = Brush(_border);
            reviewButton.Foreground = Brushes.White;
            reviewButton.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x78, 0xFC));
            reviewButton.BorderBrush = reviewButton.Background;
        }

        private void OnReviewClick(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(Brand.ReviewUrl) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("Open review link failed", ex); }
            Close();
        }

        private void OnAlreadyClick(object sender, RoutedEventArgs e)
        {
            DontShowAgain = true;
            Close();
        }
    }
}
