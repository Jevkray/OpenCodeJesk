using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using OpenCodeJesk.Commands;
using OpenCodeJesk.Models;
using OpenCodeJesk.Resources;
using OpenCodeJesk.Services;

namespace OpenCodeJesk
{
    /// <summary>
    /// Hosts the OpenCode web UI inside a WebView2.
    /// <para>
    /// Unlike the original implementation, this control does <b>not</b> proxy
    /// requests through a fake origin nor inject scripts to fake per-project
    /// storage. It simply points WebView2 straight at the OpenCode server's own
    /// web UI (http://127.0.0.1:port/), which is a
    /// self-contained single-page app that manages projects, sessions and its
    /// own theming. This removes an entire, fragile translation layer.
    /// </para>
    /// </summary>
    public partial class OpenCodeToolWindowControl : UserControl, IDisposable, IConsoleWebHost, IReconnectHost, ICliModeHost
    {
        private IServiceProvider _serviceProvider;

        private ConsoleCoordinator _console;
        private ReconnectCoordinator _reconnect;
        private CliModeCoordinator _cliMode;

        private CoreWebView2Environment _environment;
        private IProjectRootResolver _projectRootResolver;
        private ServerController _serverController;

        private string _currentProjectRoot;
        private bool _isDisposed;
        private bool _isStarting;
        private bool _retryDisabled;
        private System.Threading.Timer _projRootTimer;

        private SpendSettings _spendSettings;
        private string _activeSessionId;
        private string _activeDirectory;
        private bool _interfaceIsCli;

        private string _pendingWebUrl;

        // Токен жизни окна: отменяет длительные операции (ожидание логина) при закрытии.
        private readonly System.Threading.CancellationTokenSource _lifetimeCts = new System.Threading.CancellationTokenSource();

        /// <summary>Последний полученный снапшот расходов (из координатора консоли).</summary>
        public SpendSnapshot LatestSnapshot => _console?.LatestSnapshot;

        public OpenCodeToolWindowControl()
        {
            InitializeComponent();
            _console = new ConsoleCoordinator(this);
            _reconnect = new ReconnectCoordinator(this);
            _cliMode = new CliModeCoordinator(this);
            _ = InitWebViewCoreAsync()
                .ContinueWith(_ => WaitServerAsync(), TaskScheduler.Current);
        }

        // --- IConsoleWebHost: доступ координатора к UI-части окна ---

        CoreWebView2 IConsoleWebHost.GetCore() => webView?.CoreWebView2;
        string IConsoleWebHost.GetHomeUrl() => _serverController?.GetHomeUrl();
        void IConsoleWebHost.Navigate(string url) => webView?.CoreWebView2?.Navigate(url);
        Task IConsoleWebHost.ShowLoadingAsync(string message) => ShowLoadingPageAsync(message);
        SpendSettings IConsoleWebHost.GetSpendSettings() => _spendSettings;

        // --- IReconnectHost: доступ координатора реконнекта к UI-части окна ---

        string IReconnectHost.CurrentProjectRoot => _currentProjectRoot;
        bool IReconnectHost.IsConnected => _serverController?.State == ConnectionState.Connected;
        Task<bool> IReconnectHost.CheckHealthAsync() => _serverController.ServerService.CheckHealthAsync();
        Task<bool> IReconnectHost.StartServerAsync(string projectRoot) => _serverController.StartAsync(projectRoot);
        string IReconnectHost.GetHomeUrl() => _serverController?.GetHomeUrl();
        bool IReconnectHost.CanNavigate => webView?.CoreWebView2 != null;
        void IReconnectHost.NavigateHome(string url) => NavigateToSession(url);
        Task IReconnectHost.ApplyInterfaceAsync() => ApplyInterfaceAsync();
        Task IReconnectHost.StartFlowAsync() => StartFlowAsync();
        Task IReconnectHost.ShowLoadingAsync(string message) => ShowLoadingPageAsync(message);
        Task IReconnectHost.ShowErrorAsync(string message, bool showRetry) => ShowErrorPageAsync(message, showRetry);

        // --- ICliModeHost: доступ координатора CLI-режима к UI-части окна ---

        System.Windows.Controls.Panel ICliModeHost.TerminalPanel => rootGrid;
        string ICliModeHost.CurrentUrl => webView?.CoreWebView2?.Source;
        string ICliModeHost.PendingUrl => _pendingWebUrl;
        SpendSettings ICliModeHost.GetSpendSettings() => _spendSettings;

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void SetSpendSettings(SpendSettings settings)
        {
            _spendSettings = settings;
            ApplyZoom();
        }

        /// <summary>Применяет масштаб веб-панели из настроек.</summary>
        private void ApplyZoom()
        {
            try
            {
                var percent = Math.Max(50, Math.Min(200, _spendSettings?.WebZoomPercent ?? 100));
                if (webView != null) webView.ZoomFactor = percent / 100.0;
            }
            catch (Exception ex) { Services.Log.Info("Apply zoom failed: " + ex.Message); }
        }

        /// <summary>
        /// Включает или снимает инъекцию кнопки Console в зависимости от
        /// настройки ShowConsoleButton. Идемпотентно, можно вызывать на каждой
        /// навигации и после смены настроек.
        /// </summary>
        public async Task ApplyUsageInjectionAsync()
        {
            await _console.ApplyInjectionAsync(_spendSettings?.ShowConsoleButton == true);
        }

        /// <summary>Перезагружает веб-панель (применение смены настроек).</summary>
        public void ReloadPanel()
        {
            try { webView?.CoreWebView2?.Reload(); }
            catch (Exception ex) { Services.Log.Info("Reload panel failed: " + ex.Message); }
        }

        /// <summary>
        /// Показывает либо веб-панель, либо собственный терминал с OpenCode,
        /// в зависимости от настройки Interface mode. Терминал живёт в этой же
        /// панели, поэтому Visual Studio сохраняет его положение и размер.
        /// </summary>
        public async Task ApplyInterfaceAsync()
        {
            if (_isDisposed) return;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            bool cli = _spendSettings?.InterfaceMode == InterfaceMode.CLI;
            Services.Log.Info("Interface apply: mode=" + (cli ? "CLI" : "Web") + " wasCli=" + _interfaceIsCli);
            if (!cli)
            {
                // Возврат к веб-панели: терминал убираем.
                _cliMode.Hide();
                webView.Visibility = System.Windows.Visibility.Visible;
                // Перезагружаем веб-панель только при реальном возврате из CLI,
                // чтобы не сбивать собственную загрузку при старте.
                if (_interfaceIsCli)
                {
                    // При возврате из CLI запоминаем самый свежий чат этой папки,
                    // чтобы передать его в CLI при следующем входе, но в веб
                    // переходим на корень сервера: проектный/сессионный маршрут
                    // (`/{base64}/session/...`) отдаёт «Unrecognised route!».
                    var webDirectory = _activeDirectory
                        ?? (string.IsNullOrEmpty(_currentProjectRoot) ? null : Path.GetFullPath(_currentProjectRoot));
                    var latest = await _serverController.GetActiveSessionAsync(webDirectory);
                    if (latest != null)
                    {
                        _activeSessionId = latest.Id;
                        if (!string.IsNullOrEmpty(latest.Directory)) _activeDirectory = latest.Directory;
                    }
                    var target = await ResolveWebUrlAsync();
                    if (target != null && webView.CoreWebView2 != null)
                        NavigateToSession(target);
                    else
                        webView.CoreWebView2?.Reload();
                }
                _interfaceIsCli = false;
                webView.Focus();
                return;
            }
            _interfaceIsCli = true;
            webView.Visibility = System.Windows.Visibility.Collapsed;

            // CLI-ветку целиком ведёт координатор; он же возвращает актуальные
            // сессию и каталог для сохранения в окне.
            var resolved = await _cliMode.ShowAsync(
                _serverController, _activeDirectory, _activeSessionId, _currentProjectRoot);
            _activeSessionId = resolved.Item1;
            _activeDirectory = resolved.Item2;
        }

        public void StopCli() => _cliMode.Stop();

        /// <summary>
        /// Set the shared server controller. Must be called before StartAsync.
        /// </summary>
        public void SetServerController(ServerController controller)
        {
            if (_serverController == controller) return;

            if (_serverController != null)
            {
                _serverController.ConnectionLost -= OnServerConnectionLost;
                _serverController.ConnectionRestored -= OnServerConnectionRestored;
            }

            _serverController = controller;
            if (_serverController != null)
            {
                _serverController.ConnectionLost += OnServerConnectionLost;
                _serverController.ConnectionRestored += OnServerConnectionRestored;
            }
        }

        private void SetCurrentProjectRoot(string root)
        {
            _currentProjectRoot = root;
        }

        private async Task InitWebViewCoreAsync()
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpenCodeJesk",
                    "WebView2");

                Directory.CreateDirectory(userDataFolder);

                _environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder);

                await webView.EnsureCoreWebView2Async(_environment);

                var core = webView.CoreWebView2;

                core.WebMessageReceived += OnWebMessageReceived;
                core.NewWindowRequested += OnNewWindowRequested;
                core.NavigationStarting += OnNavigationStarting;
                core.NavigationCompleted += OnNavigationCompleted;
                core.BasicAuthenticationRequested += OnBasicAuthenticationRequested;

                core.Settings.AreDefaultContextMenusEnabled = true;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsZoomControlEnabled = true;

                await ApplyUsageInjectionAsync();

                await ShowLoadingPageAsync(StringsHelper.UILoading);
            }
            catch (Exception ex)
            {
                Services.Log.Error("WebView2 init failed", ex);
                await ShowErrorPageAsync(
                    $"{StringsHelper.ErrorWebViewInitFailed}: {ex}", false);
            }
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // Во время входа OAuth открывается как новое окно: оставляем навигацию
            // внутри WebView2, иначе вход не завершается (cookie не появляется).
            if (_console.IsLoginMode)
            {
                try { e.Handled = true; webView.CoreWebView2.Navigate(e.Uri); } catch { }
                return;
            }

            // Let target=_blank links open in the user's real browser instead of
            // spawning an unmanaged popup inside the tool window.
            try
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
            }
            catch { }
        }

        private void OnBasicAuthenticationRequested(
            object sender, CoreWebView2BasicAuthenticationRequestedEventArgs e)
        {
            // opencode v2 защищает сервер Basic Auth. Отдаём пароль, который
            // мы сами задали при запуске процесса (см. OpenCodeServerService).
            var info = _serverController?.ServerService?.ServerInfo;
            if (info == null || !info.HasAuth) return;
            try
            {
                e.Response.UserName = info.Username ?? "opencode";
                e.Response.Password = info.Password;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Basic auth handoff failed", ex);
            }
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            _console.NotifyNavigationStarting(e.Uri);
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                Debug.WriteLine($"Navigation failed: {e.WebErrorStatus}");
                return;
            }
            _pendingWebUrl = null;
            // Запоминаем активный чат веб-панели, чтобы передать его в CLI.
            var parsed = SessionUrlParser.Parse(webView?.CoreWebView2?.Source, _serverController?.ServerService?.ServerInfo?.BaseUrl);
            if (parsed != null)
            {
                _activeSessionId = parsed.Item2;
                if (!string.IsNullOrEmpty(parsed.Item1)) _activeDirectory = parsed.Item1;
            }
            // Захват cookie после входа, инъекция кнопки Console и публикация состояния.
            _console.NotifyNavigationCompleted();
        }

        private async Task WaitServerAsync()
        {
            await Task.Delay(500);
            if (_serverController == null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ShowOpenCodeWindowCommand.Instance.RefreshWindow();
            }
        }

        public async Task StartAsync()
        {
            if (_isStarting) return;
            _isStarting = true;

            try
            {
                if (webView.CoreWebView2 != null && _serverController != null)
                {
                    var isRunning = _serverController.State == ConnectionState.Connected;
                    Debug.WriteLine($"Tool window init: server running={isRunning}, proj={_currentProjectRoot}");
                }

                await StartFlowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Start flow failed: {ex}");
                await ShowErrorPageAsync($"Failed: {ex}", true);
            }
            finally
            {
                _isStarting = false;
            }
        }

        public void OnWindowClosing()
        {
            StopCli();
            _projRootTimer?.Dispose();
            _projRootTimer = null;
            _reconnect.Cancel();
        }

        private void OnServerConnectionLost()
        {
            var state = _serverController?.State ?? ConnectionState.Disconnected;
            Debug.WriteLine($"Server connection lost! State: {state}. Auto-reconnecting...");
            _reconnect.Begin();
        }

        private void OnServerConnectionRestored()
        {
            Debug.WriteLine("Server connection restored!");
            if (_reconnect.IsActive)
                return;
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var url = _serverController?.GetHomeUrl();
                if (url != null && webView.CoreWebView2 != null)
                    webView.CoreWebView2.Navigate(url);
                else
                    await StartFlowAsync();
            });
        }

        /// <summary>
        /// URL для открытия веб-панели: для v2 — свежая pairing-ссылка (ставит
        /// cookie), иначе — обычный адрес главной. Код одноразовый, поэтому
        /// вызывается перед каждой навигацией.
        /// </summary>
        private async Task<string> ResolveWebUrlAsync()
        {
            if (_serverController == null) return null;
            var pair = await _serverController.GetPairingConnectUrlAsync();
            return pair ?? _serverController.GetHomeUrl();
        }

        private async Task StartFlowAsync()
        {
            await ShowLoadingPageAsync(StringsHelper.UIConnecting);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_projectRootResolver == null)
                _projectRootResolver = new ProjectRootResolver(_serviceProvider);
            var newProjectRoot = _projectRootResolver.ResolveProjectRoot();
            Debug.WriteLine($"Project root: {newProjectRoot} (current: {_currentProjectRoot})");

            if (_serverController == null) return;

            if (string.Equals(newProjectRoot, _currentProjectRoot, StringComparison.OrdinalIgnoreCase)
                && _serverController.State == ConnectionState.Connected
                && !string.IsNullOrEmpty(_serverController.CurrentSessionId))
            {
                var existingUrl = await ResolveWebUrlAsync();
                if (existingUrl != null)
                {
                    await EnsureWebViewCacheFreshAsync();
                    NavigateToSession(existingUrl);
                    await ApplyInterfaceAsync();
                    return;
                }
            }

            SetCurrentProjectRoot(newProjectRoot);

            var success = await _serverController.StartAsync(_currentProjectRoot);
            if (!success)
            {
                await ShowErrorPageAsync(StringsHelper.ErrorServerStartFailed, true);
                return;
            }

            var sessionUrl = await ResolveWebUrlAsync();
            if (sessionUrl != null)
            {
                await EnsureWebViewCacheFreshAsync();
                NavigateToSession(sessionUrl);
            }

            await ApplyInterfaceAsync();

            _projRootTimer?.Dispose();
            _projRootTimer = new System.Threading.Timer(_ =>
            {
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    try
                    {
                        if (_serverController?.ServerService != null)
                        {
                            var healthy = await _serverController.ServerService.CheckHealthAsync();
                            if (!healthy && _serverController.State != ConnectionState.Connecting
                                && !_isStarting && !_reconnect.IsActive)
                            {
                                _reconnect.Begin();
                                return;
                            }
                        }

                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        if (_projectRootResolver == null)
                            _projectRootResolver = new ProjectRootResolver(_serviceProvider);
                        var resolved = _projectRootResolver.ResolveProjectRoot();
                        if (!string.Equals(resolved, _currentProjectRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.WriteLine($"proj_root changed: {_currentProjectRoot} -> {resolved}");
                            SetCurrentProjectRoot(resolved);
                            _serverController?.UpdateProjectRoot(resolved);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"timer: {ex}");
                    }
                });
            }, null, 5000, 5000);

            if (_serverController.State == ConnectionState.Connected)
            {
                _console.StartMonitor();
            }
        }

        private void NavigateToSession(string sessionUrl)
        {
            if (webView.CoreWebView2 == null) return;
            Services.Log.Info($"Navigating to {sessionUrl}");
            _pendingWebUrl = sessionUrl;
            webView.CoreWebView2.Navigate(sessionUrl);
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            if (message == "toggle-console")
            {
                _console.HandleToggleConsole();
                return;
            }
            if (message == "login")
            {
#pragma warning disable VSSDK007
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(() => _console.LoginAsync());
#pragma warning restore VSSDK007
                return;
            }
            if (message == "retry" && !_retryDisabled)
            {
                _retryDisabled = true;
#pragma warning disable VSSDK007
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    try
                    {
                        await StartFlowAsync();
                    }
                    finally
                    {
                        _retryDisabled = false;
                    }
                });
#pragma warning restore VSSDK007
            }
            if (message == "settings")
            {
#pragma warning disable VSSDK007
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    try
                    {
                        if (_serviceProvider is OpenCodeJeskPackage pkg)
                            await pkg.ShowSettingsWindowAsync(1);
                    }
                    catch (Exception ex) { Services.Log.Error("Open settings from page failed", ex); }
                });
#pragma warning restore VSSDK007
            }
        }

        private async Task ShowLoadingPageAsync(string message)
        {
            var html = WebPageRenderer.BuildLoadingPage(message);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.NavigateToString(html);
            }
        }

        private async Task ShowErrorPageAsync(string message, bool showRetry, bool showSettings = false)
        {
            var html = WebPageRenderer.BuildErrorPage(message, showRetry, showSettings, WebSettingsLabel());

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.NavigateToString(html);
            }
        }

        /// <summary>Подпись кнопки перехода в настройки (RU/EN).</summary>
        private string WebSettingsLabel()
            => _spendSettings?.Language == "ru" ? "Открыть настройки" : "Open Settings";

        /// <summary>
        /// Очищает только кэш WebView2 (не cookie), если сменилась мажорная
        /// версия OpenCode (v1 ↔ v2). Иначе WebView2 отдаёт закэшированные
        /// ассеты старой версии с того же адреса 127.0.0.1:4096, и веб-клиент
        /// падает с UnsupportedContentType. Проверяется при каждом старте панели.
        /// </summary>
        private async Task EnsureWebViewCacheFreshAsync()
        {
            var core = webView?.CoreWebView2;
            if (core == null) return;

            int major;
            try { major = CliCommand.DetectMajor(_serverController?.ServerService?.BinaryPath); }
            catch { major = 0; }
            if (major <= 0) major = 1;

            var stamp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenCodeJesk", "webview-major2.txt");
            int saved = 0;
            try
            {
                if (File.Exists(stamp) && int.TryParse(File.ReadAllText(stamp).Trim(), out var v))
                    saved = v;
            }
            catch { }

            if (saved == major) return;

            var kinds = CoreWebView2BrowsingDataKinds.DiskCache
                | CoreWebView2BrowsingDataKinds.ServiceWorkers
                | CoreWebView2BrowsingDataKinds.CacheStorage;
            try
            {
                await core.Profile.ClearBrowsingDataAsync(kinds);
                Services.Log.Info("WebView cache cleared for opencode major " + major);
            }
            catch { }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(stamp));
                File.WriteAllText(stamp, major.ToString());
            }
            catch { }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            try { _lifetimeCts.Cancel(); _lifetimeCts.Dispose(); } catch { }
            _projRootTimer?.Dispose();
            _reconnect?.Dispose();
            _reconnect = null;
            _console?.Dispose();
            _console = null;
            _cliMode?.Dispose();
            _cliMode = null;
            webView?.Dispose();
        }
    }
}
