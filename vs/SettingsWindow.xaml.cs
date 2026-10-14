using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using OpenCodeStudio.Services;

namespace OpenCodeStudio
{
    /// <summary>
    /// Своё окно Settings в теме OpenCode: боковое меню разделов и карточки
    /// контролов. Открывается из меню и при первом запуске. Раздел Import
    /// сохраняет прежнее поведение мастера импорта.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly ImportService _service;
        private readonly string _solutionDir;
        private readonly Services.SpendSettings _settings;
        private readonly Action _onFinished;
        private readonly Dictionary<ImportItem, CheckBox> _checks = new Dictionary<ImportItem, CheckBox>();
        private string _customPath;
        private bool _v1Available = true;
        private bool _v2Available = true;
        private System.Threading.CancellationTokenSource _installCts;
        private DispatcherTimer _installTimer;
        private DateTime _installStartedAt;
        private string _lastNpmLine;
        private readonly CliInstallService _cliInstall = new CliInstallService();

        private static readonly SolidColorBrush TextBrush = Brush(0xf1, 0xf1, 0xf1);
        private static readonly SolidColorBrush MutedBrush = Brush(0x9a, 0x9a, 0x9a);
        private static readonly SolidColorBrush AccentBrush = Brush(0x2A, 0x78, 0xFC);
        private static readonly FontFamily MonoFont = new FontFamily("Consolas");

        private static SolidColorBrush Brush(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));

        public SettingsWindow(ImportService service, string solutionDir,
            Services.SpendSettings settings = null, Action onFinished = null, int initialTab = 1)
        {
            InitializeComponent();
            _service = service ?? new ImportService();
            _solutionDir = solutionDir;
            _settings = settings;
            _onFinished = onFinished;
            if (_settings != null) Services.SpendSettings.Current = _settings;

            LoadSettings();
            Services.Localization.IsRussian = _settings?.Language == "ru";
            languageCombo.SelectedIndex = Services.Localization.IsRussian ? 1 : 0;
            SetVersionText();
            ApplyLanguage();
            menuList.SelectedIndex = Math.Max(0, Math.Min(menuList.Items.Count - 1, initialTab));
            RenderChangelog();
            Loaded += (_, __) => RebuildImport();

            // Детекция версий и статуса установки — в фоне, чтобы открытие окна
            // не подвешивало IDE.
            _ = DetectInstalledVersionsAsync();
            _ = RefreshInstallStatusAsync();
        }

        // ---- локализация -------------------------------------------------

        /// <summary>Проставляет тексты окна из локализации для текущего языка.</summary>
        private void ApplyLanguage()
        {
            Title = Loc("window.title");
            Localize(this);
            SetItems(interfaceCombo, Loc("combo.interface.0"), Loc("combo.interface.1"));
            SetVersionItems();
            SetItems(startDirCombo, Loc("combo.startdir.0"), Loc("combo.startdir.1"),
                Loc("combo.startdir.2"), Loc("combo.startdir.3"));
            SetItems(channelCombo, Loc("combo.channel.0"), Loc("combo.channel.1"));
            _ = RefreshInstallStatusAsync();
        }

        /// <summary>Локализованная строка по ключу для текущего языка.</summary>
        private string Loc(string key) => Services.Localization.Get(key);

        /// <summary>
        /// Показывает оба пункта версии, неактивные — с пометкой «не установлено».
        /// </summary>
        private void SetVersionItems()
        {
            var v1 = Loc("combo.version.0");
            var v2 = Loc("combo.version.1");
            var missing = " " + Loc("combo.version.notinstalled");
            if (versionCombo.Items.Count >= 2
                && versionCombo.Items[0] is ComboBoxItem i0
                && versionCombo.Items[1] is ComboBoxItem i1)
            {
                i0.Content = _v1Available ? v1 : v1 + missing;
                i1.Content = _v2Available ? v2 : v2 + missing;
            }
            else
            {
                SetItems(versionCombo, v1, v2);
            }
        }

        /// <summary>
        /// Определяет установленные ветки opencode в фоне (не на UI-потоке) и
        /// обновляет комбобокс версий после завершения.
        /// </summary>
        private async Task DetectInstalledVersionsAsync()
        {
            try
            {
                var v1 = await OpenCodeServerService.FindV1PathAsync();
                var v2 = await OpenCodeServerService.FindV2PathAsync();
                _v1Available = v1 != null;
                _v2Available = v2 != null;
            }
            catch (Exception ex)
            {
                Log.Error("Version detection failed", ex);
                return;
            }

            if (versionCombo.Items.Count >= 2
                && versionCombo.Items[0] is ComboBoxItem v1Item
                && versionCombo.Items[1] is ComboBoxItem v2Item)
            {
                v1Item.IsEnabled = _v1Available;
                v2Item.IsEnabled = _v2Available;
            }

            if (versionCombo.SelectedIndex == 1 && !_v2Available)
                versionCombo.SelectedIndex = _v1Available ? 0 : -1;
            else if (versionCombo.SelectedIndex == 0 && !_v1Available)
                versionCombo.SelectedIndex = _v2Available ? 1 : -1;

            SetVersionItems();
            ApplyVersionLock();
        }

        /// <summary>
        /// Обновляет список статусов установки на вкладке Install. Детекция идёт
        /// в фоне, пока показывается состояние «проверка…».
        /// </summary>
        private async Task RefreshInstallStatusAsync()
        {
            if (installStatusPanel == null) return;
            installStatusPanel.Children.Clear();
            installStatusPanel.Children.Add(new TextBlock
            {
                Text = Loc("install.status.checking"),
                Foreground = MutedBrush
            });

            string v1, v2, desktop, webCli;
            bool webAvailable;
            try
            {
                v1 = await OpenCodeServerService.FindV1PathAsync();
                v2 = await OpenCodeServerService.FindV2PathAsync();
                desktop = OpenCodeServerService.FindDesktopAppPath();
                webCli = v1 ?? v2;
                webAvailable = await OpenCodeServerService.SupportsWebAsync(webCli);
            }
            catch (Exception ex)
            {
                Log.Error("Install status refresh failed", ex);
                return;
            }

            if (installStatusPanel == null) return;
            installStatusPanel.Children.Clear();

            installStatusPanel.Children.Add(BuildCliRow(
                Loc("install.status.v1"), v1, "npm i -g opencode-ai"));
            installStatusPanel.Children.Add(BuildCliRow(
                Loc("install.status.v2"), v2, "npm i -g @opencode/cli"));
            installStatusPanel.Children.Add(new TextBlock
            {
                Text = Loc("install.status.v2.plugins"),
                FontSize = 12,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -6, 0, 12)
            });
            installStatusPanel.Children.Add(BuildSimpleRow(
                Loc("install.status.web"),
                webAvailable
                    ? Loc("install.status.web.available") + " (" + webCli + ")"
                    : Loc("install.status.web.unavailable"),
                null));
            installStatusPanel.Children.Add(BuildDesktopRow(desktop));
        }

        // Заголовок строки статуса: «пункт  статус».
        private UIElement BuildHeader(string title, string status)
        {
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TextBrush,
                FontWeight = FontWeights.SemiBold
            });
            head.Children.Add(new TextBlock
            {
                Text = "  " + status,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            return head;
        }

        // Строка CLI: при установке — моноширинный путь, иначе команда установки.
        private UIElement BuildCliRow(string title, string path, string installCommand)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(BuildHeader(title,
                path != null ? Loc("install.status.installed") : Loc("install.status.notinstalled")));
            row.Children.Add(new TextBlock
            {
                Text = path ?? installCommand,
                FontFamily = MonoFont,
                FontSize = 12,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            return row;
        }

        private UIElement BuildSimpleRow(string title, string status, string detail)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(BuildHeader(title, status));
            if (!string.IsNullOrEmpty(detail))
                row.Children.Add(new TextBlock
                {
                    Text = detail,
                    FontSize = 12,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            return row;
        }

        // Строка Desktop: путь либо ссылка на скачивание.
        private UIElement BuildDesktopRow(string path)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            row.Children.Add(BuildHeader(Loc("install.status.desktop"),
                path != null ? Loc("install.status.installed") : Loc("install.status.notinstalled")));

            if (path != null)
            {
                row.Children.Add(new TextBlock
                {
                    Text = path,
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            else
            {
                var line = new TextBlock
                {
                    FontSize = 12,
                    Foreground = MutedBrush,
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

        private static void Localize(DependencyObject node)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(node))
            {
                if (!(child is DependencyObject dep)) continue;

                if (child is FrameworkElement fe && !string.IsNullOrEmpty(fe.Uid))
                {
                    // Get возвращает сам ключ, если строки нет — такой элемент пропускаем.
                    var text = Services.Localization.Get(fe.Uid);
                    if (!string.Equals(text, fe.Uid, StringComparison.Ordinal))
                    {
                        if (child is TextBlock tb) tb.Text = text;
                        else if (child is Expander ex) ex.Header = text;
                        else if (child is ContentControl cc) cc.Content = text;
                    }
                }

                Localize(dep);
            }
        }

        private static void SetItems(ComboBox combo, params string[] texts)
        {
            for (int i = 0; i < combo.Items.Count && i < texts.Length; i++)
                if (combo.Items[i] is ComboBoxItem item) item.Content = texts[i];
        }

        private void SetVersionText()
        {
            var version = typeof(SettingsWindow).Assembly.GetName().Version;
            versionText.Text = Brand.Name + " " + version;
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            Services.Localization.IsRussian = languageCombo.SelectedIndex == 1;
            ApplyLanguage();
            if (_settings != null)
            {
                _settings.Language = Services.Localization.IsRussian ? "ru" : "en";
                _settings.SaveSettingsToStorage();
            }
        }

        // ---- чтение / запись настроек -----------------------------------

        private void LoadSettings()
        {
            interfaceCombo.SelectedIndex = _settings?.InterfaceMode == InterfaceMode.CLI ? 1 : 0;
            versionCombo.SelectedIndex = _settings?.UseOpenCodeV2 == true ? 1 : 0;
            ApplyVersionLock();
            webZoom.Value = _settings?.WebZoomPercent ?? 100;
            autoOpenCheck.IsChecked = _settings?.AutoOpenOnSolutionOpen ?? false;
            consoleButtonCheck.IsChecked = _settings?.ShowConsoleButton ?? true;

            SelectFont(_settings?.TerminalFontFamily ?? "Consolas");
            fontSize.Value = _settings?.TerminalFontSize ?? 13;
            cursorBlinkCheck.IsChecked = _settings?.TerminalCursorBlink ?? true;
            scrollbackBox.Text = (_settings?.TerminalScrollback ?? 5000).ToString();
            cliArgsBox.Text = _settings?.CliArguments ?? "";
            startDirCombo.SelectedIndex = (int)(_settings?.StartDirectory ?? StartDirectoryMode.GitRoot);

            openCodePathBox.Text = _settings?.OpenCodePath ?? "";
            portBox.Text = (_settings?.PreferredPort ?? 4096).ToString();
            serveArgsBox.Text = _settings?.ServeArguments ?? "";
            idleBox.Text = (_settings?.IdleStopMinutes ?? 0).ToString();

            checkUpdatesCheck.IsChecked = _settings?.CheckForUpdates ?? true;
            updateMinutesBox.Text = (_settings?.UpdateCheckMinutes ?? 60).ToString();
            channelCombo.SelectedIndex = (int)(_settings?.Channel ?? UpdateChannel.Stable);
            ratingPopupCheck.IsChecked = _settings?.ShowRatingPopup ?? true;
        }

        private void SaveSettingsToStorage()
        {
            if (_settings == null) return;

            _settings.InterfaceMode = interfaceCombo.SelectedIndex == 1 ? InterfaceMode.CLI : InterfaceMode.Web;
            _settings.UseOpenCodeV2 = versionCombo.SelectedIndex == 1;
            _settings.WebZoomPercent = Clamp((int)webZoom.Value, 50, 200);
            _settings.AutoOpenOnSolutionOpen = autoOpenCheck.IsChecked == true;
            _settings.ShowConsoleButton = consoleButtonCheck.IsChecked == true;
            _settings.Language = Services.Localization.IsRussian ? "ru" : "en";

            _settings.TerminalFontFamily = (fontCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "Consolas";
            _settings.TerminalFontSize = Clamp((int)fontSize.Value, 8, 28);
            _settings.TerminalCursorBlink = cursorBlinkCheck.IsChecked == true;
            _settings.TerminalScrollback = ParseInt(scrollbackBox.Text, 5000, 500, 50000);
            _settings.CliArguments = cliArgsBox.Text ?? "";
            _settings.StartDirectory = (StartDirectoryMode)Math.Max(0, startDirCombo.SelectedIndex);

            _settings.OpenCodePath = openCodePathBox.Text?.Trim() ?? "";
            _settings.PreferredPort = ParseInt(portBox.Text, 4096, 1, 65535);
            _settings.ServeArguments = serveArgsBox.Text ?? "";
            _settings.IdleStopMinutes = ParseInt(idleBox.Text, 0, 0, 100000);

            _settings.CheckForUpdates = checkUpdatesCheck.IsChecked == true;
            _settings.UpdateCheckMinutes = ParseInt(updateMinutesBox.Text, 60, 5, 1440);
            _settings.Channel = (UpdateChannel)Math.Max(0, channelCombo.SelectedIndex);
            _settings.ShowRatingPopup = ratingPopupCheck.IsChecked == true;

            _settings.SaveSettingsToStorage();
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static int ParseInt(string text, int fallback, int min, int max)
            => int.TryParse((text ?? "").Trim(), out var v) ? Clamp(v, min, max) : fallback;

        // ---- события UI --------------------------------------------------

        private void OnMenuChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelGeneral == null) return; // до полной инициализации
            int index = menuList.SelectedIndex;
            panelInstall.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            panelGeneral.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelTerminal.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            panelServer.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            panelUpdates.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
            panelImport.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
            if (index == 4) RenderChangelog();
        }

        /// <summary>Кнопка «Оставить отзыв»: открывает форму отзыва в браузере.</summary>
        private void OnLeaveReviewClick(object sender, RoutedEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Brand.ReviewUrl) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("Open review link failed", ex); }
        }

        /// <summary>
        /// Рендерит changelog во вкладке Updates: разбирает встроенный
        /// CHANGELOG.md и строит элементы с inline **жирным**.
        /// </summary>
        private void RenderChangelog()
        {
            if (changelogPanel == null) return;
            changelogPanel.Children.Clear();

            foreach (var block in ChangelogService.Parse(ChangelogService.ReadAll()))
            {
                switch (block.Kind)
                {
                    case ChangelogBlockKind.Title:
                        changelogPanel.Children.Add(Text(block.Text, 16, FontWeights.Bold, TextBrush, new Thickness(0, 0, 0, 10)));
                        break;
                    case ChangelogBlockKind.Version:
                        changelogPanel.Children.Add(new Border
                        {
                            Height = 1,
                            Background = Brush(0x3a, 0x3a, 0x3a),
                            Margin = new Thickness(0, 6, 0, 0)
                        });
                        changelogPanel.Children.Add(Text(block.Text, 14, FontWeights.SemiBold,
                            Brush(0x5E, 0xAC, 0xFC), new Thickness(0, 16, 0, 6)));
                        break;
                    case ChangelogBlockKind.Category:
                        changelogPanel.Children.Add(Text(block.Text, 12, FontWeights.SemiBold,
                            Brush(0xcf, 0xcf, 0xcf), new Thickness(0, 8, 0, 2)));
                        break;
                    case ChangelogBlockKind.Bullet:
                        changelogPanel.Children.Add(Text("•  " + block.Text, 12, FontWeights.Normal,
                            MutedBrush, new Thickness(8, 2, 0, 2)));
                        break;
                    default:
                        changelogPanel.Children.Add(Text(block.Text, 12, FontWeights.Normal,
                            MutedBrush, new Thickness(0, 2, 0, 2)));
                        break;
                }
            }
        }

        /// <summary>TextBlock с поддержкой inline **жирного** (чётные Run — обычные, нечётные — жирные).</summary>
        private static TextBlock Text(string text, double fontSize, FontWeight weight, Brush brush, Thickness margin)
        {
            var tb = new TextBlock
            {
                FontSize = fontSize,
                FontWeight = weight,
                Foreground = brush,
                Margin = margin,
                TextWrapping = TextWrapping.Wrap
            };
            var parts = (text ?? "").Split(new[] { "**" }, StringSplitOptions.None);
            for (var i = 0; i < parts.Length; i++)
                tb.Inlines.Add(new Run(parts[i]) { FontWeight = i % 2 == 1 ? FontWeights.Bold : weight });
            return tb;
        }

        private void OnWebZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (webZoomText != null) webZoomText.Text = ((int)e.NewValue) + "%";
        }

        private void OnFontSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (fontSizeText != null) fontSizeText.Text = ((int)e.NewValue).ToString();
        }

        private void OnFontChanged(object sender, RoutedEventArgs e) { }

        private void OnInterfaceChanged(object sender, SelectionChangedEventArgs e) => ApplyVersionLock();

        /// <summary>
        /// Доступность выбора версии зависит только от установленных версий;
        /// v2 разрешена и в Desktop (Web), и в CLI.
        /// </summary>
        private void ApplyVersionLock()
        {
            if (interfaceCombo == null || versionCombo == null) return;
            versionCombo.IsEnabled = _v1Available || _v2Available;
        }

        /// <summary>Выбирает шрифт в списке; если его там нет — добавляет.</summary>
        private void SelectFont(string family)
        {
            if (string.IsNullOrWhiteSpace(family)) family = "Consolas";
            ComboBoxItem match = null;
            foreach (var item in fontCombo.Items)
            {
                if (item is ComboBoxItem ci && string.Equals(ci.Content as string, family, StringComparison.OrdinalIgnoreCase))
                { match = ci; break; }
            }
            if (match == null)
            {
                match = new ComboBoxItem { Content = family };
                fontCombo.Items.Add(match);
            }
            fontCombo.SelectedItem = match;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsToStorage();
                _settings?.NotifyApplied();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save settings", ex);
            }

            Onboarding.MarkCompleted();
            _onFinished?.Invoke();
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Onboarding.MarkCompleted();
            _onFinished?.Invoke();
            Close();
        }

        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select the opencode binary",
                Filter = "Executables (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == true)
                openCodePathBox.Text = dialog.FileName;
        }

        /// <summary>Открывает внешние ссылки вкладки Install в браузере.</summary>
        private void OnHyperlink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
            e.Handled = true;
        }

        /// <summary>Кнопка Install v1: ставит opencode-ai@latest и обновляет статус.</summary>
        private async void OnInstallOpenCodeClick(object sender, RoutedEventArgs e)
            => await InstallCliAsync("opencode-ai@latest");

        /// <summary>Кнопка Install v2: ставит @opencode/cli и обновляет статус.</summary>
        private async void OnInstallV2Click(object sender, RoutedEventArgs e)
            => await InstallCliAsync("@opencode/cli");

        /// <summary>Ставит указанный npm-пакет через CliInstallService и обновляет статус.</summary>
        private async Task InstallCliAsync(string packageSpec)
        {
            // На время установки блокируем кнопки установки и ссылки; Cancel доступен.
            SetInstallBusy(true);
            _lastNpmLine = null;
            StartInstallTimer();

            _installCts = new System.Threading.CancellationTokenSource();
            try
            {
                var log = new List<string>();
                var ok = await _cliInstall.InstallAsync(packageSpec, line =>
                {
                    lock (log) { log.Add(line); if (log.Count > 200) log.RemoveAt(0); }
                    // Живой вывод: последняя строка npm на UI-потоке.
                    Dispatcher.Invoke(() =>
                    {
                        _lastNpmLine = line;
                        UpdateInstallStatus();
                    });
                }, _installCts.Token);

                StopInstallTimer();
                if (_installCts.IsCancellationRequested)
                {
                    Log.Info("OpenCode install (" + packageSpec + ") cancelled");
                    if (installResult != null) installResult.Text = Loc("install.cancelled");
                    return;
                }

                await DetectInstalledVersionsAsync();
                await RefreshInstallStatusAsync();
                Log.Info("OpenCode install (" + packageSpec + ") " + (ok ? "succeeded" : "failed"));
                if (installResult != null)
                    installResult.Text = ok
                        ? Loc("install.done")
                        : Loc("install.failed") + "\n" + string.Join("\n",
                            log.Skip(Math.Max(0, log.Count - 10)));
            }
            catch (Exception ex)
            {
                Log.Error("OpenCode install (" + packageSpec + ") failed", ex);
                if (installResult != null) installResult.Text = Loc("install.failed") + " " + ex.Message;
            }
            finally
            {
                StopInstallTimer();
                SetInstallBusy(false);
                _installCts?.Dispose();
                _installCts = null;
            }
        }

        /// <summary>Запускает таймер прошедшего времени установки (тик 1 c).</summary>
        private void StartInstallTimer()
        {
            _installStartedAt = DateTime.Now;
            if (_installTimer == null)
            {
                _installTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _installTimer.Tick += (_, __) => UpdateInstallStatus();
            }
            _installTimer.Start();
            UpdateInstallStatus();
        }

        /// <summary>Останавливает таймер по завершении/ошибке/отмене.</summary>
        private void StopInstallTimer() => _installTimer?.Stop();

        /// <summary>Показывает прошедшее время хода установки и последнюю строку npm.</summary>
        private void UpdateInstallStatus()
        {
            if (installResult == null) return;
            var elapsed = DateTime.Now - _installStartedAt;
            var time = (int)elapsed.TotalMinutes + ":" + elapsed.Seconds.ToString("00");
            var text = Loc("install.running") + " " + time;
            if (!string.IsNullOrEmpty(_lastNpmLine)) text += "\n" + "npm: " + _lastNpmLine;
            installResult.Text = text;
        }

        /// <summary>Кнопка Cancel: отменяет текущую установку и убивает дерево процессов.</summary>
        private void OnInstallCancelClick(object sender, RoutedEventArgs e)
        {
            StopInstallTimer();
            if (installResult != null) installResult.Text = Loc("install.cancelled");
            _installCts?.Cancel();
        }

        /// <summary>Переключает вкладку Install между рабочим и свободным состоянием.</summary>
        private void SetInstallBusy(bool busy)
        {
            if (installButton != null) installButton.IsEnabled = !busy;
            if (installV2Button != null) installV2Button.IsEnabled = !busy;
            if (installSiteLink != null) installSiteLink.IsEnabled = !busy;
            if (installDocsLink != null) installDocsLink.IsEnabled = !busy;
            if (installProgress != null)
                installProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (installCancelButton != null)
                installCancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---- Import ------------------------------------------------------

        private void RebuildImport()
        {
            sourcesPanel.Children.Clear();
            _checks.Clear();
            statusText.Text = "";

            var sources = _service.DetectSources(_customPath, _solutionDir);
            if (sources.Count == 0)
            {
                sourcesPanel.Children.Add(new TextBlock
                {
                    Text = "No OpenCode data was found on this machine yet. " +
                           "You can start using " + Brand.Name + " right away - settings will be created automatically.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 4, 0, 4)
                });
                importButton.IsEnabled = true;
                return;
            }

            importButton.IsEnabled = true;
            if (importWarning != null)
                importWarning.Visibility = ImportService.IsOpenCodeRunning()
                    ? Visibility.Visible : Visibility.Collapsed;
            foreach (var source in sources)
                sourcesPanel.Children.Add(BuildSourceCard(source));
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
                Foreground = TextBrush
            });
            if (source.IsActive)
            {
                header.Children.Add(new Border
                {
                    Margin = new Thickness(8, 0, 0, 0),
                    Padding = new Thickness(6, 1, 6, 1),
                    CornerRadius = new CornerRadius(3),
                    Background = AccentBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = "in use", FontSize = 10, Foreground = Brushes.White }
                });
            }
            card.Children.Add(header);

            card.Children.Add(new TextBlock
            {
                Text = source.Summary,
                FontSize = 11,
                Foreground = MutedBrush,
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
                    Foreground = item.Found ? TextBrush : MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (item.Importable) _checks[item] = check;
                Grid.SetColumn(check, 0);
                row.Children.Add(check);

                var detail = new TextBlock
                {
                    Text = item.Detail,
                    FontSize = 11,
                    Foreground = MutedBrush,
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

        private void OnBrowseImportClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select a folder that contains OpenCode data (config, auth.json, opencode.db...)";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _customPath = dialog.SelectedPath;
                    RebuildImport();
                }
            }
        }

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            var selected = _checks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            if (selected.Count == 0)
            {
                statusText.Text = "Nothing selected.";
                return;
            }

            importButton.IsEnabled = false;
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
                statusText.Text = msg;
            }
            catch (Exception ex)
            {
                Log.Error("Import failed", ex);
                statusText.Text = "Import failed: " + ex.Message;
            }
            finally
            {
                importButton.IsEnabled = true;
            }
        }
    }
}
