using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OpenCodeStudio.Commands;
using OpenCodeStudio.Services;

namespace OpenCodeStudio
{
    [Guid(Brand.PackageGuid)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(OpenCodeToolWindow))]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(Services.SpendSettings), Brand.Name, "Settings", 0, 0, true)]
    public sealed class OpenCodeStudioPackage : AsyncPackage
    {
        public const string PackageGuidString = Brand.PackageGuid;

        private DTE _dte;
        private SolutionEvents _solutionEvents;

        /// <summary>
        /// Общий контроллер сервера: переживает открытие/закрытие панели.
        /// </summary>
        private ServerController _serverController;
        private bool _configuredV2;
        private InterfaceMode _configuredInterface;
        private bool _configuredShowConsole;

        private SettingsWindow _settingsWindow;
        private bool _firstRunShown;

        private OpenCodeToolWindowControl _toolWindowControl;
        private CancellationTokenSource _extensionUpdateCts;
        private volatile bool _updateWindowOpen;
        private CancellationTokenSource _openCodeUpdateCts;
        private volatile bool _openCodeUpdateWindowOpen;
        private System.Windows.Threading.DispatcherTimer _ratingTimer;
        private bool _ratingShown;

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            Services.Log.Info("InitializeAsync step 1: base done");
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Настройки и общие сервисы.
            try
            {
                // Единый экземпляр настроек для сервисов (сервер, терминал, апдейты).
                Services.SpendSettings.Current = GetSpendSettings();

                // v2 разрешена в любом режиме (и Desktop/Web, и CLI).
                _serverController = new ServerController(() =>
                    GetSpendSettings()?.UseOpenCodeV2 ?? false);
                _configuredV2 = GetSpendSettings().UseOpenCodeV2;
                _configuredInterface = GetSpendSettings().InterfaceMode;
                _configuredShowConsole = GetSpendSettings().ShowConsoleButton;

                Services.SpendSettings.Applied += OnSpendSettingsApplied;
                Services.SpendSettings.CliInstallStarting += OnCliInstallStarting;
                Services.Log.Info("InitializeAsync step 2: settings ready");
            }
            catch (Exception ex)
            {
                Services.Log.Error("InitializeAsync step 2 failed", ex);
            }

            // Команды.
            try
            {
                await ShowOpenCodeWindowCommand.InitializeAsync(this);
                await ShowImportCommand.InitializeAsync(this);
                Services.Log.Info("InitializeAsync step 3: commands initialized");
            }
            catch (Exception ex)
            {
                Services.Log.Error("InitializeAsync step 3 failed", ex);
            }

            // DTE и события решений.
            try
            {
                _dte = await GetServiceAsync(typeof(DTE)) as DTE;
                if (_dte != null)
                {
                    _solutionEvents = _dte.Events.SolutionEvents;
                    _solutionEvents.Opened += OnSolutionOpened;
                }
                Services.Log.Info("InitializeAsync step 4: DTE events wired");
            }
            catch (Exception ex)
            {
                Services.Log.Error("InitializeAsync step 4 failed", ex);
            }

            // Первый запуск после установки или обновления (по версии расширения).
            try
            {
                if (Onboarding.ShouldShow() && !_firstRunShown)
                {
                    _firstRunShown = true;
                    // Помечаем сразу: окно больше не появится до следующего обновления,
                    // даже если пользователь закроет его крестиком.
                    Onboarding.MarkCompleted();
                    _ = JoinableTaskFactory.RunAsync(async () =>
                    {
                        try
                        {
                            // Let the shell finish initializing before showing a window.
                            await Task.Delay(2500);
                            await JoinableTaskFactory.SwitchToMainThreadAsync();
                            // Если opencode не найден — открываем окно на вкладке Install.
                            // Проверка версий — в фоне, не на UI-потоке.
                            var notInstalled = await Task.Run(() =>
                                !CliBinaryLocator.IsV1Available()
                                && !CliBinaryLocator.IsV2Available());
                            await ShowSettingsWindowAsync(notInstalled ? 0 : 1);
                        }
                        catch (Exception ex)
                        {
                            Services.Log.Error("First-run wizard failed", ex);
                        }
                    });
                }
                Services.Log.Info("InitializeAsync step 5: onboarding checked");
            }
            catch (Exception ex)
            {
                Services.Log.Error("InitializeAsync step 5 failed", ex);
            }

            // Фоновые циклы проверки обновлений.
            try
            {
                StartExtensionUpdateLoop();
                StartOpenCodeUpdateLoop();
                StartRatingPopupTimer();
                Services.Log.Info("InitializeAsync step 6: update loops started");
            }
            catch (Exception ex)
            {
                Services.Log.Error("InitializeAsync step 6 failed", ex);
            }

#if !JESK
            try
            {
                ShowRenameNotice();
                Services.Log.Info("InitializeAsync step 7: rename notice scheduled");
            }
            catch (Exception ex)
            {
                Services.Log.Error("InitializeAsync step 7 failed", ex);
            }
#endif
            Services.Log.Info("InitializeAsync completed");
        }

#if !JESK
        /// <summary>
        /// Уведомление о переименовании в OpenCode Jesk. Показывается при запуске
        /// Visual Studio, пока пользователь не отметит «не показывать снова».
        /// </summary>
        private void ShowRenameNotice()
        {
            var marker = System.IO.Path.Combine(Services.OpenCodeEnvironment.StudioDir, "rename-notice.done");
            try { if (System.IO.File.Exists(marker)) return; } catch { }

            _ = JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await Task.Delay(6000);
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    var window = new RenameNoticeWindow();
                    window.Show();
                    Services.Log.Info("Rename notice shown");
                }
                catch (Exception ex)
                {
                    Services.Log.Error("Rename notice failed", ex);
                }
            });
        }
#endif

        /// <summary>
        /// Фоновая проверка новой версии самого opencode: первая через ~30 c,
        /// затем каждые 60 c. Показывает окно только если есть новее и пользователь
        /// ещё не отклонил его в этой сессии VS.
        /// </summary>
        private void StartOpenCodeUpdateLoop()
        {
            _openCodeUpdateCts = new CancellationTokenSource();
            var ct = _openCodeUpdateCts.Token;
            var service = new OpenCodeUpdateService();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30000, ct);
                    while (!ct.IsCancellationRequested)
                    {
                        try
                        {
                            var installed = await service.GetInstalledVersionAsync();
                            var latest = await service.GetLatestVersionAsync();
                            if (!string.IsNullOrEmpty(installed) && !string.IsNullOrEmpty(latest) &&
                                OpenCodeUpdateService.IsNewer(latest, installed) &&
                                !OpenCodeUpdateWindow.Suppressed && !_openCodeUpdateWindowOpen)
                            {
                                await JoinableTaskFactory.SwitchToMainThreadAsync(ct);
                                if (!OpenCodeUpdateWindow.Suppressed && !_openCodeUpdateWindowOpen)
                                {
                                    var window = new OpenCodeUpdateWindow(installed, latest);
                                    _openCodeUpdateWindowOpen = true;
                                    window.Closed += (_, __) => _openCodeUpdateWindowOpen = false;
                                    window.Show();
                                }
                            }
                        }
                        catch (OperationCanceledException) { return; }
                        catch (Exception ex)
                        {
                            Services.Log.Warn("OpenCode update check failed: " + ex.Message);
                        }

                        await Task.Delay(UpdateCheckDelayMs(), ct);
                    }
                }
                catch (OperationCanceledException) { }
            }, ct);
        }

        /// <summary>
        /// Фоновая проверка новой версии самого расширения: первая через ~20 c,
        /// затем каждые 60 c. Показывает окно только если есть новее и пользователь
        /// ещё не отклонил его в этой сессии VS.
        /// </summary>
        private void StartExtensionUpdateLoop()
        {
            _extensionUpdateCts = new CancellationTokenSource();
            var ct = _extensionUpdateCts.Token;
            var service = new ExtensionUpdateService();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(20000, ct);
                    while (!ct.IsCancellationRequested)
                    {
                        try
                        {
                            var info = await service.CheckAsync();
                            if (info != null && info.IsNewer &&
                                !UpdateNotificationWindow.Suppressed && !_updateWindowOpen)
                            {
                                await JoinableTaskFactory.SwitchToMainThreadAsync(ct);
                                var checkForUpdates = GetSpendSettings()?.CheckForUpdates ?? true;
                                if (checkForUpdates && !UpdateNotificationWindow.Suppressed && !_updateWindowOpen)
                                {
                                    var window = new UpdateNotificationWindow(info, () =>
                                    {
                                        JoinableTaskFactory.Run(async () =>
                                        {
                                            await JoinableTaskFactory.SwitchToMainThreadAsync();
                                            if (!TryOpenExtensionManager())
                                                throw new InvalidOperationException("Extension Manager command not found");
                                            Services.Log.Info("Opened Extension Manager");
                                        });
                                    });
                                    _updateWindowOpen = true;
                                    window.Closed += (_, __) => _updateWindowOpen = false;
                                    window.Show();
                                }
                            }
                        }
                        catch (OperationCanceledException) { return; }
                        catch (Exception ex)
                        {
                            Services.Log.Warn("Extension update check failed: " + ex.Message);
                        }

                        await Task.Delay(UpdateCheckDelayMs(), ct);
                    }
                }
                catch (OperationCanceledException) { }
            }, ct);
        }

        /// <summary>Интервал между проверками обновлений (мин.), из настроек.</summary>
        private int UpdateCheckDelayMs()
        {
            var minutes = Math.Max(5, Math.Min(1440, GetSpendSettings()?.UpdateCheckMinutes ?? 60));
            return minutes * 60 * 1000;
        }

        /// <summary>
        /// Напоминание об отзыве: таймер на 2 часа, срабатывает один раз за сессию.
        /// Если расширение работало меньше — окно не появится.
        /// </summary>
        private void StartRatingPopupTimer()
        {
            _ratingTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(2) };
            _ratingTimer.Tick += (_, __) => OnRatingTimerTick();
            _ratingTimer.Start();
        }

        private void OnRatingTimerTick()
        {
            // Один шанс за сессию: в любом случае таймер больше не нужен.
            _ratingTimer?.Stop();
            if (_ratingShown) return;

            var settings = Services.SpendSettings.Current;
            if (settings == null || !settings.ShowRatingPopup || settings.RatingPopupDismissed) return;

            _ratingShown = true;
            try
            {
                var window = new RatingWindow();
                try { window.Owner = System.Windows.Application.Current?.MainWindow; } catch { }
                window.ShowDialog();
                if (window.DontShowAgain)
                {
                    settings.RatingPopupDismissed = true;
                    settings.SaveSettingsToStorage();
                    Services.Log.Info("Rating popup dismissed permanently");
                }
            }
            catch (Exception ex)
            {
                Services.Log.Error("Rating popup failed", ex);
            }
        }

        /// <summary>
        /// Надёжно открывает Manage Extensions внутри VS. Вызывать с UI-потока.
        /// </summary>
        private bool TryOpenExtensionManager()
        {
            if (_dte == null) { Services.Log.Info("Extension Manager: DTE is null"); return false; }

            // 1) известные имена команд (VS 2026 — ManageExtensions, затем VS 2022 и старые)
            var candidates = new[] {
                "ManageExtensions", "Extensions.ManageExtensions", "Extensions.ManageExtensionsDialog",
                "Extensions.ExtensionsAndUpdates", "Tools.ManageExtensions",
                "Tools.ExtensionsAndUpdates"
            };

            // 2) открыть сразу вкладку Updates: сначала с аргументом, затем без
            try
            {
                _dte.ExecuteCommand("ManageExtensions", "Updates");
                Services.Log.Info("Extension Manager opened (Updates tab) via command: ManageExtensions Updates");
                return true;
            }
            catch (Exception ex) { Services.Log.Info("Extension Manager: 'ManageExtensions Updates' -> " + ex.Message); }

            try
            {
                _dte.ExecuteCommand("ManageExtensions");
                Services.Log.Info("Extension Manager opened via command: ManageExtensions");
                return true;
            }
            catch (Exception ex) { Services.Log.Info("Extension Manager: 'ManageExtensions' -> " + ex.Message); }

            // 3) поиск среди всех команд DTE: Update + (Extension|Manage)
            try
            {
                foreach (EnvDTE.Command cmd in _dte.Commands)
                {
                    var n = cmd?.Name;
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (n.IndexOf("Extension", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         n.IndexOf("Manage", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        try
                        {
                            _dte.ExecuteCommand(n, "Updates");
                            Services.Log.Info("Extension Manager opened (Updates tab) via DTE search: " + n);
                            return true;
                        }
                        catch
                        {
                            try { _dte.ExecuteCommand(n); Services.Log.Info("Extension Manager opened via DTE search: " + n); return true; } catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { Services.Log.Info("Extension Manager: DTE enumeration failed: " + ex.Message); }

            // 4) остальные известные кандидаты
            foreach (var name in candidates)
            {
                try { _dte.ExecuteCommand(name); Services.Log.Info("Extension Manager opened via command: " + name); return true; }
                catch (Exception ex) { Services.Log.Info("Extension Manager: '" + name + "' -> " + ex.Message); }
            }

            // 5) перебор ManageExtensions / ExtensionsAndUpdates среди всех команд DTE (как было)
            try
            {
                foreach (EnvDTE.Command cmd in _dte.Commands)
                {
                    var n = cmd?.Name;
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n.IndexOf("ManageExtensions", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("ExtensionsAndUpdates", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try { _dte.ExecuteCommand(n); Services.Log.Info("Extension Manager opened via DTE search: " + n); return true; } catch { }
                    }
                }
            }
            catch (Exception ex) { Services.Log.Info("Extension Manager: DTE enumeration failed: " + ex.Message); }

            // 6) поиск по меню/тулбарам (по подписи), устойчиво к локализации
            try
            {
                var needles = new[] { "manage extensions", "extensions and updates", "управление расширениями" };
                if (_dte.CommandBars is System.Collections.IEnumerable bars)
                {
                    foreach (Microsoft.VisualStudio.CommandBars.CommandBar bar in bars)
                    {
                        var hit = FindControlRecursive(bar.Controls, needles);
                        if (hit != null)
                        {
                            try { hit.Execute(); Services.Log.Info("Extension Manager opened via menu caption: " + hit.Caption); return true; } catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { Services.Log.Info("Extension Manager: CommandBars search failed: " + ex.Message); }

            // 7) диагностика в лог: команды со словом extension
            try
            {
                int logged = 0;
                foreach (EnvDTE.Command cmd in _dte.Commands)
                {
                    var n = cmd?.Name;
                    if (!string.IsNullOrEmpty(n) && n.IndexOf("extension", StringComparison.OrdinalIgnoreCase) >= 0 && logged < 30)
                    { Services.Log.Info("DTE ext command candidate: " + n); logged++; }
                }
                Services.Log.Info("Extension Manager: no command matched (logged " + logged + " candidates)");
            }
            catch { }

            return false;
        }

        private static Microsoft.VisualStudio.CommandBars.CommandBarControl FindControlRecursive(Microsoft.VisualStudio.CommandBars.CommandBarControls controls, string[] needles)
        {
            try
            {
                foreach (Microsoft.VisualStudio.CommandBars.CommandBarControl c in controls)
                {
                    var cap = (c?.Caption ?? "").Replace("&", "").Trim().ToLowerInvariant();
                    foreach (var n in needles)
                        if (cap.Contains(n)) return c;

                    if (c is Microsoft.VisualStudio.CommandBars.CommandBarPopup popup)
                    {
                        var sub = FindControlRecursive(popup.Controls, needles);
                        if (sub != null) return sub;
                    }
                }
            }
            catch { }
            return null;
        }

        private Services.SpendSettings GetSpendSettings()
            => (Services.SpendSettings)GetDialogPage(typeof(Services.SpendSettings));

        /// <summary>
        /// Перед установкой CLI освобождает файлы npm-шимов: останавливает наш
        /// сервер и CLI-терминал и делает best-effort остановку фоновой службы
        /// opencode. Процессы по имени не убиваются.
        /// </summary>
        private async void OnCliInstallStarting()
        {
            try
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                try { _serverController?.Stop(); } catch { }
                try { _toolWindowControl?.StopCli(); } catch { }
                // «opencode service stop» здесь не вызываем: у v1 такой команды нет,
                // процесс зависал и держал opencode.exe (из-за этого npm падал с EPERM).
                Services.Log.Info("OpenCode stopped before CLI install");
            }
            catch (Exception ex)
            {
                Services.Log.Warn("OnCliInstallStarting failed: " + ex.Message);
            }
        }

        private void OnSpendSettingsApplied()
        {
            _ = JoinableTaskFactory.RunAsync(async () =>
            {
                Services.SpendSettings.Current = GetSpendSettings();
                try
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    var useV2 = GetSpendSettings().UseOpenCodeV2;
                    var mode = GetSpendSettings().InterfaceMode;
                    var showConsole = GetSpendSettings().ShowConsoleButton;
                    if (showConsole != _configuredShowConsole)
                    {
                        // Смена кнопки Console: обновляем настройки панели, включаем/
                        // снимаем инъекцию и перезагружаем панель.
                        _configuredShowConsole = showConsole;
                        if (_toolWindowControl != null)
                        {
                            _toolWindowControl.SetSpendSettings(GetSpendSettings());
                            await _toolWindowControl.ApplyUsageInjectionAsync();
                            _toolWindowControl.ReloadPanel();
                        }
                    }
                    if (useV2 != _configuredV2)
                    {
                        // Смена версии v1/v2 требует перезапуска сервера: пересоздаём панель.
                        _serverController?.Stop();
                        _configuredV2 = useV2;
                        _configuredInterface = mode;
                        await ShowOpenCodeWindowAsync();
                        return;
                    }
                    if (mode != _configuredInterface)
                    {
                        _configuredInterface = mode;
                        _toolWindowControl?.SetSpendSettings(GetSpendSettings());
                        if (mode == InterfaceMode.CLI)
                        {
                            var opened = await FindToolWindowAsync(typeof(OpenCodeToolWindow), 0, false, DisposalToken);
                            if (opened is OpenCodeToolWindow tool && tool.Control != null)
                            {
                                tool.Control.SetSpendSettings(GetSpendSettings());
                                await tool.Control.ApplyInterfaceAsync();
                            }
                            else
                            {
                                await ShowOpenCodeWindowAsync();
                            }
                        }
                        else
                        {
                            await ShowOpenCodeWindowAsync();
                        }
                    }
                    else if (_toolWindowControl != null)
                    {
                        _toolWindowControl.SetSpendSettings(GetSpendSettings());
                        await _toolWindowControl.ApplyInterfaceAsync();
                    }
                }
                catch (Exception ex)
                {
                    Services.Log.Error("Restart after settings change failed", ex);
                }
            });
        }

        private string GetSolutionDir()
        {
            try
            {
                var full = _dte?.Solution?.FullName;
                if (!string.IsNullOrEmpty(full))
                    return System.IO.Path.GetDirectoryName(full);
            }
            catch { }
            return null;
        }

        private void OnSolutionOpened()
        {
            _ = JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    await Task.Delay(1000);
                    if (GetSpendSettings()?.AutoOpenOnSolutionOpen == true)
                        await ShowOpenCodeWindowAsync();
                    else
                        await RefreshOpenCodeWindowAsync();
                }
                catch (Exception ex)
                {
                    Services.Log.Error("OnSolutionOpened failed", ex);
                }
            });
        }

        internal async Task ShowOpenCodeWindowAsync()
        {
            var window = await ShowToolWindowAsync(
                typeof(OpenCodeToolWindow), 0, create: true, DisposalToken);

            if (window is OpenCodeToolWindow toolWindow && toolWindow?.Control != null)
            {
                toolWindow.SetServiceProvider(this);
                _toolWindowControl = toolWindow.Control;
                toolWindow.Control.SetServerController(_serverController);
                toolWindow.Control.SetSpendSettings(GetSpendSettings());

                await toolWindow.Control.StartAsync();
            }
        }

        internal async Task RefreshOpenCodeWindowAsync()
        {
            var window = await FindToolWindowAsync(
                typeof(OpenCodeToolWindow), 0, false, DisposalToken);

            if (window is OpenCodeToolWindow toolWindow && toolWindow?.Control != null)
            {
                _toolWindowControl = toolWindow.Control;
                toolWindow.Control.SetServerController(_serverController);
                toolWindow.Control.SetSpendSettings(GetSpendSettings());
                await toolWindow.Control.StartAsync();
            }
        }

        /// <summary>
        /// Единственный экземпляр окна Settings. Повторные вызовы активируют уже
        /// открытое окно вместо создания нового.
        /// </summary>
        internal async Task<SettingsWindow> ShowSettingsWindowAsync(int initialTab = 1)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return _settingsWindow;
            }

            var window = new SettingsWindow(new ImportService(), GetSolutionDir(),
                GetSpendSettings(), null, initialTab);
            window.Closed += (_, __) => _settingsWindow = null;
            _settingsWindow = window;
            window.Show();
            return window;
        }

        /// <summary>
        /// Opens the import/setup wizard on demand (also shown automatically on
        /// first run).
        /// </summary>
        internal Task ShowImportWindowAsync() => ShowSettingsWindowAsync();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _extensionUpdateCts?.Cancel();
                _extensionUpdateCts?.Dispose();
                _openCodeUpdateCts?.Cancel();
                _openCodeUpdateCts?.Dispose();
                _ratingTimer?.Stop();
                Services.SpendSettings.Applied -= OnSpendSettingsApplied;
                Services.SpendSettings.CliInstallStarting -= OnCliInstallStarting;
                _toolWindowControl?.Dispose();
                _serverController?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
