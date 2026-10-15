using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenCodeJesk.Services
{
    /// <summary>Логика вкладки Import: карточки источников и перенос выбранных данных.</summary>
    public sealed class ImportPresenter
    {
        private readonly ImportService _service;
        private readonly StackPanel _sourcesPanel;
        private readonly TextBlock _statusText;
        private readonly Button _importButton;
        private readonly TextBlock _warning;
        private readonly string _solutionDir;
        private readonly Dictionary<ImportItem, CheckBox> _checks = new Dictionary<ImportItem, CheckBox>();

        public ImportPresenter(ImportService service, StackPanel sourcesPanel, TextBlock statusText,
                               Button importButton, TextBlock warning, string solutionDir)
        {
            _service = service;
            _sourcesPanel = sourcesPanel;
            _statusText = statusText;
            _importButton = importButton;
            _warning = warning;
            _solutionDir = solutionDir;
        }

        public string CustomPath { get; set; }

        public void Rebuild()
        {
            _sourcesPanel.Children.Clear();
            _checks.Clear();
            _statusText.Text = "";

            var sources = _service.DetectSources(CustomPath, _solutionDir);
            if (sources.Count == 0)
            {
                _sourcesPanel.Children.Add(new TextBlock
                {
                    Text = "No OpenCode data was found on this machine yet. " +
                           "You can start using " + Brand.Name + " right away - settings will be created automatically.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = UiPalette.Muted,
                    Margin = new Thickness(0, 4, 0, 4)
                });
                _importButton.IsEnabled = true;
                return;
            }

            _importButton.IsEnabled = true;
            if (_warning != null)
                _warning.Visibility = ImportService.IsOpenCodeRunning()
                    ? Visibility.Visible : Visibility.Collapsed;
            foreach (var source in sources)
                _sourcesPanel.Children.Add(BuildSourceCard(source));
        }

        private UIElement BuildSourceCard(ImportSource source)
        {
            var card = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock
            {
                Text = source.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = UiPalette.Text
            });
            if (source.IsActive)
            {
                header.Children.Add(new Border
                {
                    Margin = new Thickness(8, 0, 0, 0),
                    Padding = new Thickness(6, 1, 6, 1),
                    CornerRadius = new CornerRadius(3),
                    Background = UiPalette.Accent,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = "in use", FontSize = 10, Foreground = Brushes.White }
                });
            }
            card.Children.Add(header);

            card.Children.Add(new TextBlock
            {
                Text = source.Summary,
                FontSize = 11,
                Foreground = UiPalette.Muted,
                Margin = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            foreach (var item in source.Items)
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new CheckBox
                {
                    Content = item.Label,
                    IsChecked = item.Selected,
                    IsEnabled = item.Importable,
                    Foreground = item.Found ? UiPalette.Text : UiPalette.Muted,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (item.Importable) _checks[item] = check;
                Grid.SetColumn(check, 0);
                row.Children.Add(check);

                var detail = new TextBlock
                {
                    Text = item.Detail,
                    FontSize = 11,
                    Foreground = UiPalette.Muted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0),
                    MaxWidth = 320,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                if (source.IsActive) detail.Text = item.Found ? item.Detail + "  (already active)" : item.Detail;
                Grid.SetColumn(detail, 1);
                row.Children.Add(detail);

                card.Children.Add(row);
            }

            return card;
        }

        public void Browse(Window owner)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select a folder that contains OpenCode data (config, auth.json, opencode.db...)";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    CustomPath = dialog.SelectedPath;
                    Rebuild();
                }
            }
        }

        public void ImportSelected()
        {
            var selected = _checks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            if (selected.Count == 0)
            {
                _statusText.Text = "Nothing selected.";
                return;
            }

            _importButton.IsEnabled = false;
            try
            {
                var result = _service.Import(null, selected);

                var msg = $"Imported {result.Copied.Count} item(s).";
                if (result.Backups.Count > 0) msg += $" {result.Backups.Count} backup(s) created.";
                if (result.Errors.Count > 0)
                {
                    msg += $" {result.Errors.Count} error(s): " + string.Join("; ", result.Errors.Take(3));
                    Log.Warn("Import errors: " + string.Join(" | ", result.Errors));
                }
                Log.Info("Settings import completed. " + msg);
                _statusText.Text = msg;
            }
            catch (Exception ex)
            {
                Log.Error("Import failed", ex);
                _statusText.Text = "Import failed: " + ex.Message;
            }
            finally
            {
                _importButton.IsEnabled = true;
            }
        }
    }
}
