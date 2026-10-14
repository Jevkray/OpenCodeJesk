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
        private readonly ImportPresenter _import;
        private bool _v1Available = true;
        private bool _v2Available = true;
        private System.Threading.CancellationTokenSource _installCts;
        private DispatcherTimer _installTimer;
        private DateTime _installStartedAt;
        private string _lastNpmLine;
        private readonly CliInstallService _cliInstall = new CliInstallService();
        private readonly CliVersionStatusService _versionStatus = new CliVersionStatusService();

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
            _import = new ImportPresenter(_service, sourcesPanel, statusText, importButton, importWarning, _solutionDir);
            if (_settings != null) Services.SpendSettings.Current = _settings;

            LoadSettings();
            Services.Localization.IsRussian = _settings?.Language == "ru";
            languageCombo.SelectedIndex = Services.Localization.IsRussian ? 1 : 0;
            SetVersionText();
            ApplyLanguage();
            menuList.SelectedIndex = Math.Max(0, Math.Min(menuList.Items.Count - 1, initialTab));
            RenderChangelog();
            Loaded += (_, __) => _import.Rebuild();

            // Детекция версий и статуса установки — в фоне, чтобы открытие окна
            // не подвешивало IDE. Детекция одна, результат переиспользуется.
            _ = RefreshVersionAndInstallStatusAsync();
        }

        // Детектит версии один раз и применяет результат и к комбобоксу версий,
        // и к строкам статуса установки.
        private async Task RefreshVersionAndInstallStatusAsync()
        {
            var info = await DetectInstalledVersionsAsync();
            await RefreshInstallStatusAsync(info);
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
        private async Task<CliVersionAvailability> DetectInstalledVersionsAsync(CliVersionAvailability info = null)
        {
            try
            {
                info = info ?? await _versionStatus.DetectAsync();
                _v1Available = info.V1Available;
                _v2Available = info.V2Available;
            }
            catch (Exception ex)
            {
                Log.Error("Version detection failed", ex);
                return null;
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
            return info;
        }

        /// <summary>
        /// Обновляет список статусов установки на вкладке Install. Детекция идёт
        /// в фоне, пока показывается состояние «проверка…».
        /// </summary>
        private async Task RefreshInstallStatusAsync(CliVersionAvailability info = null)
        {
            if (installStatusPanel == null) return;
            installStatusPanel.Children.Clear();
            installStatusPanel.Children.Add(new TextBlock
            {
                Text = Loc("install.status.checking"),
                Foreground = UiPalette.Muted
            });

            try
            {
                info = info ?? await _versionStatus.DetectAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Install status refresh failed", ex);
                return;
            }

            if (installStatusPanel == null) return;
            installStatusPanel.Children.Clear();

            installStatusPanel.Children.Add(InstallStatusRenderer.CliRow(
                Loc("install.status.v1"), info.V1Path, "npm i -g opencode-ai"));
            installStatusPanel.Children.Add(InstallStatusRenderer.CliRow(
                Loc("install.status.v2"), info.V2Path, "npm i -g @opencode/cli"));
            installStatusPanel.Children.Add(new TextBlock
            {
                Text = Loc("install.status.v2.plugins"),
                FontSize = 12,
                Foreground = UiPalette.Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -6, 0, 12)
            });
            installStatusPanel.Children.Add(InstallStatusRenderer.SimpleRow(
                Loc("install.status.web"),
                info.WebSupported
                    ? Loc("install.status.web.available") + " (" + info.WebBinaryPath + ")"
                    : Loc("install.status.web.unavailable"),
                null));
            installStatusPanel.Children.Add(InstallStatusRenderer.DesktopRow(info.DesktopAppPath));
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
            var f = SettingsEditor.FromSettings(_settings);
            interfaceCombo.SelectedIndex = f.InterfaceIndex;
            versionCombo.SelectedIndex = f.UseOpenCodeV2 ? 1 : 0;
            ApplyVersionLock();
            webZoom.Value = f.WebZoom;
            autoOpenCheck.IsChecked = f.AutoOpen;
            consoleButtonCheck.IsChecked = f.ShowConsoleButton;

            SelectFont(f.FontFamily);
            fontSize.Value = f.FontSize;
            cursorBlinkCheck.IsChecked = f.CursorBlink;
            scrollbackBox.Text = f.ScrollbackText;
            cliArgsBox.Text = f.CliArguments;
            startDirCombo.SelectedIndex = f.StartDirectoryIndex;

            openCodePathBox.Text = f.OpenCodePath;
            portBox.Text = f.PortText;
            serveArgsBox.Text = f.ServeArguments;
            idleBox.Text = f.IdleStopText;

            checkUpdatesCheck.IsChecked = f.CheckForUpdates;
            updateMinutesBox.Text = f.UpdateMinutesText;
            channelCombo.SelectedIndex = f.ChannelIndex;
            ratingPopupCheck.IsChecked = f.ShowRatingPopup;
        }

        private void SaveSettingsToStorage()
        {
            if (_settings == null) return;

            var f = new SettingsForm
            {
                InterfaceIndex = interfaceCombo.SelectedIndex,
                UseOpenCodeV2 = versionCombo.SelectedIndex == 1,
                WebZoom = webZoom.Value,
                AutoOpen = autoOpenCheck.IsChecked == true,
                ShowConsoleButton = consoleButtonCheck.IsChecked == true,
                FontFamily = (fontCombo.SelectedItem as ComboBoxItem)?.Content as string,
                FontSize = fontSize.Value,
                CursorBlink = cursorBlinkCheck.IsChecked == true,
                ScrollbackText = scrollbackBox.Text,
                CliArguments = cliArgsBox.Text,
                StartDirectoryIndex = startDirCombo.SelectedIndex,
                OpenCodePath = openCodePathBox.Text,
                PortText = portBox.Text,
                ServeArguments = serveArgsBox.Text,
                IdleStopText = idleBox.Text,
                CheckForUpdates = checkUpdatesCheck.IsChecked == true,
                UpdateMinutesText = updateMinutesBox.Text,
                ChannelIndex = channelCombo.SelectedIndex,
                ShowRatingPopup = ratingPopupCheck.IsChecked == true,
                RussianLanguage = Services.Localization.IsRussian,
            };

            SettingsEditor.Apply(f, _settings);
            _settings.SaveSettingsToStorage();
        }

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
                        changelogPanel.Children.Add(Text(block.Text, 16, FontWeights.Bold, UiPalette.Text, new Thickness(0, 0, 0, 10)));
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
                            UiPalette.Muted, new Thickness(8, 2, 0, 2)));
                        break;
                    default:
                        changelogPanel.Children.Add(Text(block.Text, 12, FontWeights.Normal,
                            UiPalette.Muted, new Thickness(0, 2, 0, 2)));
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

        /// <summary>Кнопка Install All: ставит сразу v2 и v1 одним проходом.</summary>
        private async void OnInstallAllClick(object sender, RoutedEventArgs e)
            => await InstallAllAsync();

        /// <summary>Кнопка Check versions: показывает фактические версии установленных v1/v2.</summary>
        private async void OnCheckVersionsClick(object sender, RoutedEventArgs e)
            => await CheckVersionsAsync();

        /// <summary>Один проход установки npm-пакета без busy/таймера и итоговых статусов.</summary>
        private async Task<(bool ok, List<string> log)> InstallPackageAsync(string packageSpec)
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
            return (ok, log);
        }

        /// <summary>Обёртка установки одного пакета: busy-цикл, таймер и итоговый статус.</summary>
        private async Task InstallCliAsync(string packageSpec)
        {
            // На время установки блокируем кнопки установки и ссылки; Cancel доступен.
            SetInstallBusy(true);
            _lastNpmLine = null;
            StartInstallTimer();

            _installCts = new System.Threading.CancellationTokenSource();
            try
            {
                var (ok, log) = await InstallPackageAsync(packageSpec);

                StopInstallTimer();
                if (_installCts.IsCancellationRequested)
                {
                    Log.Info("OpenCode install (" + packageSpec + ") cancelled");
                    if (installResult != null) installResult.Text = Loc("install.cancelled");
                    return;
                }

                var info = await DetectInstalledVersionsAsync();
                await RefreshInstallStatusAsync(info);
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

        /// <summary>
        /// Ставит v2, затем v1 в одном busy-цикле. Порядок важен: пакет v2
        /// (@opencode/cli) перекрывает opencode, поэтому v1 ставим последним —
        /// тогда opencode = v1, а opencode2 = v2.
        /// </summary>
        private async Task InstallAllAsync()
        {
            SetInstallBusy(true);
            _lastNpmLine = null;
            StartInstallTimer();

            _installCts = new System.Threading.CancellationTokenSource();
            try
            {
                var (okV2, logV2) = await InstallPackageAsync("@opencode/cli");
                if (_installCts.IsCancellationRequested)
                {
                    Log.Info("OpenCode install all cancelled after v2");
                    if (installResult != null) installResult.Text = Loc("install.cancelled");
                    return;
                }

                var (okV1, logV1) = await InstallPackageAsync("opencode-ai@latest");
                StopInstallTimer();
                if (_installCts.IsCancellationRequested)
                {
                    Log.Info("OpenCode install all cancelled after v1");
                    if (installResult != null) installResult.Text = Loc("install.cancelled");
                    return;
                }

                var info = await DetectInstalledVersionsAsync();
                await RefreshInstallStatusAsync(info);
                var ok = okV1 && okV2;
                Log.Info("OpenCode install all " + (ok ? "succeeded" : "failed"));
                if (installResult != null)
                {
                    if (ok)
                    {
                        installResult.Text = Loc("install.done");
                    }
                    else
                    {
                        var tail = logV2.Skip(Math.Max(0, logV2.Count - 5))
                            .Concat(logV1.Skip(Math.Max(0, logV1.Count - 5)));
                        installResult.Text = Loc("install.failed") + "\n" + string.Join("\n", tail);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("OpenCode install all failed", ex);
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

        /// <summary>Проверяет фактические версии найденных v1/v2 и выводит их в статус.</summary>
        private async Task CheckVersionsAsync()
        {
            if (installResult == null) return;
            installResult.Text = Loc("install.status.checking");
            try
            {
                var info = await _versionStatus.DetectAsync();
                var v1 = await DescribeVersionAsync("v1", info.V1Path);
                var v2 = await DescribeVersionAsync("v2", info.V2Path);
                installResult.Text = v1 + "\n" + v2;
            }
            catch (Exception ex)
            {
                Log.Error("Version check failed", ex);
                installResult.Text = Loc("install.failed") + " " + ex.Message;
            }
        }

        /// <summary>Строка вида "v1: 1.19.0 — <путь>" для бинаря или пометка об отсутствии.</summary>
        private async Task<string> DescribeVersionAsync(string label, string path)
        {
            if (string.IsNullOrEmpty(path))
                return label + ": " + Loc("install.version.notinstalled");
            var version = await CliBinaryLocator.GetVersionAsync(path);
            return label + ": " + (version ?? "?") + " — " + path;
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
            if (installAllButton != null) installAllButton.IsEnabled = !busy;
            if (checkVersionsButton != null) checkVersionsButton.IsEnabled = !busy;
            if (installSiteLink != null) installSiteLink.IsEnabled = !busy;
            if (installDocsLink != null) installDocsLink.IsEnabled = !busy;
            if (installProgress != null)
                installProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (installCancelButton != null)
                installCancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---- Import ------------------------------------------------------

        private void OnBrowseImportClick(object sender, RoutedEventArgs e) => _import.Browse(this);

        private void OnImportClick(object sender, RoutedEventArgs e) => _import.ImportSelected();
    }
}
