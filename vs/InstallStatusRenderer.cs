using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace OpenCodeStudio
{
    /// <summary>Строит строки статуса установки на вкладке Install.</summary>
    public static class InstallStatusRenderer
    {
        private static string Loc(string key) => Services.Localization.Get(key);

        // Заголовок строки статуса: «пункт  статус».
        public static UIElement Header(string title, string status)
        {
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = UiPalette.Text,
                FontWeight = FontWeights.SemiBold
            });
            head.Children.Add(new TextBlock
            {
                Text = "  " + status,
                Foreground = UiPalette.Muted,
                VerticalAlignment = VerticalAlignment.Center
            });
            return head;
        }

        // Строка CLI: при установке — моноширинный путь, иначе команда установки.
        public static UIElement CliRow(string title, string path, string installCommand)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(Header(title,
                path != null ? Loc("install.status.installed") : Loc("install.status.notinstalled")));
            row.Children.Add(new TextBlock
            {
                Text = path ?? installCommand,
                FontFamily = UiPalette.Mono,
                FontSize = 12,
                Foreground = UiPalette.Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            return row;
        }

        public static UIElement SimpleRow(string title, string status, string detail)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(Header(title, status));
            if (!string.IsNullOrEmpty(detail))
                row.Children.Add(new TextBlock
                {
                    Text = detail,
                    FontSize = 12,
                    Foreground = UiPalette.Muted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            return row;
        }

        // Строка Desktop: путь либо ссылка на скачивание.
        public static UIElement DesktopRow(string path)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            row.Children.Add(Header(Loc("install.status.desktop"),
                path != null ? Loc("install.status.installed") : Loc("install.status.notinstalled")));

            if (path != null)
            {
                row.Children.Add(new TextBlock
                {
                    Text = path,
                    FontFamily = UiPalette.Mono,
                    FontSize = 12,
                    Foreground = UiPalette.Muted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            else
            {
                var line = new TextBlock
                {
                    FontSize = 12,
                    Foreground = UiPalette.Muted,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                line.Inlines.Add(new Run(Loc("install.status.desktop.hint") + " "));
                var link = new Hyperlink(new Run("https://opencode.ai/download"))
                {
                    NavigateUri = new Uri("https://opencode.ai/download")
                };
                link.RequestNavigate += OnHyperlink;
                line.Inlines.Add(link);
                row.Children.Add(line);
            }
            return row;
        }

        private static void OnHyperlink(object sender, RequestNavigateEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
            e.Handled = true;
        }
    }
}
