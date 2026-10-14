using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using OpenCodeStudio.Commands;
using OpenCodeStudio.Models;
using OpenCodeStudio.Resources;
using OpenCodeStudio.Services;

namespace OpenCodeStudio
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
    public partial class OpenCodeToolWindowControl : UserControl, IDisposable
    {
        private IServiceProvider _serviceProvider;

        private CoreWebView2Environment _environment;
        private IProjectRootResolver _projectRootResolver;
        private ServerController _serverController;

        private string _currentProjectRoot;
        private bool _isDisposed;
        private bool _isStarting;
        private bool _retryDisabled;
        private System.Threading.Timer _projRootTimer;

        private SpendMonitor _spendMonitor;
        private SpendSettings _spendSettings;
        private CliTerminalControl _cliTerminal;
        private string _activeSessionId;
        private string _activeDirectory;
        private bool _interfaceIsCli;
        private string _cliSettingsKey;

        /// <summary>Отпечаток настроек терминала: меняется — страницу пересоздаём.</summary>
        private string TerminalSettingsKey()
        {
            var s = _spendSettings;
            return string.Join("|",
                s?.TerminalFontFamily ?? "Consolas",
                (s?.TerminalFontSize ?? 13).ToString(),
                (s?.TerminalCursorBlink ?? true).ToString(),
                (s?.TerminalScrollback ?? 5000).ToString(),
                s?.CliArguments ?? "");
        }
        private string _pendingWebUrl;
        private bool _loginInProgress;
        private bool _loginMode;
        private bool _loginReturned;
        private bool _autoOpenAfterLogin;
        private System.Threading.Tasks.TaskCompletionSource<bool> _loginTcs;

        // Токен жизни окна: отменяет длительные операции (ожидание логина) при закрытии.
        private readonly System.Threading.CancellationTokenSource _lifetimeCts = new System.Threading.CancellationTokenSource();

        /// <summary>Обновление снапшота расходов (на UI-потоке).</summary>
        public event Action<SpendSnapshot> SnapshotUpdated;

        /// <summary>Последний полученный снапшот расходов.</summary>
        public SpendSnapshot LatestSnapshot { get; private set; }

        private bool _isReconnecting;
        private System.Threading.CancellationTokenSource _reconnectCts;
        private const int MaxReconnectAttempts = 30;
        private const int ReconnectDelayMs = 3000;

        private static readonly string ErrorPageTemplate;
        private static readonly string LoadingPageTemplate;

        static OpenCodeToolWindowControl()
        {
            var assembly = Assembly.GetExecutingAssembly();
            ErrorPageTemplate = LoadResourceString(assembly, "OpenCodeStudio.Resources.ErrorPage.html");
            LoadingPageTemplate = LoadResourceString(assembly, "OpenCodeStudio.Resources.LoadingPage.html");
        }

        private static string LoadResourceString(Assembly assembly, string name)
        {
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public OpenCodeToolWindowControl()
        {
            InitializeComponent();
            _ = InitWebViewCoreAsync()
                .ContinueWith(_ => WaitServerAsync(), TaskScheduler.Current);
        }

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
            var core = webView?.CoreWebView2;
            if (core == null) return;
            try
            {
                if (_spendSettings?.ShowConsoleButton == true)
                    await UsageInjector.InstallAsync(core);
                else
                    await UsageInjector.UninstallAsync(core);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Usage injection apply failed", ex);
            }
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
                if (_cliTerminal != null)
                {
                    rootGrid.Children.Remove(_cliTerminal);
                    _cliTerminal.Dispose();
                    _cliTerminal = null;
                }
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
                    var target = _serverController.GetHomeUrl();
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
            if (_serverController?.State != ConnectionState.Connected || string.IsNullOrEmpty(_currentProjectRoot))
                return;

            // Настройки терминала (шрифт, размер, курсор, прокрутка, аргументы)
            // применяются при пересоздании страницы — пересоздаём при их изменении.
            var terminalKey = TerminalSettingsKey();
            if (_cliTerminal != null && _cliSettingsKey != terminalKey)
            {
                rootGrid.Children.Remove(_cliTerminal);
                _cliTerminal.Dispose();
                _cliTerminal = null;
            }
            _cliSettingsKey = terminalKey;

            // Создаём терминал только когда панель уже видима: WebView2, созданный
            // внутри скрытого контейнера, не отрисовывается и не выполняет скрипты.
            if (_cliTerminal == null)
            {
                _cliTerminal = new CliTerminalControl();
                rootGrid.Children.Add(_cliTerminal);
            }
            _cliTerminal.Visibility = System.Windows.Visibility.Visible;

            string directory = _activeDirectory;
            string session = _activeSessionId;
            var fromUrl = ParseSessionFromUrl(_pendingWebUrl ?? webView.CoreWebView2?.Source);
            if (fromUrl != null) { directory = fromUrl.Item1 ?? directory; session = fromUrl.Item2; }
            if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath(_currentProjectRoot);

            // Общий сервер: чат создаётся один раз и передаётся в терминал, чтобы
            // в обоих режимах была одна и та же активная сессия.
            if (string.IsNullOrEmpty(session))
            {
                try
                {
                    var created = await _serverController.SessionService.CreateSessionAsync(directory, Brand.Name);
                    session = created?.Id;
                    if (!string.IsNullOrEmpty(created?.Directory)) directory = created.Directory;
                }
                catch (Exception ex) { Services.Log.Info("CLI session create: " + ex.Message); }
            }
            _activeSessionId = session;
            _activeDirectory = directory;
            await _cliTerminal.ActivateAsync(_serverController.ServerService, directory, session);
        }

        private Tuple<string, string> ParseSessionFromUrl(string url)
        {
            var info = _serverController?.ServerService?.ServerInfo;
            if (info == null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Authority != new Uri(info.BaseUrl).Authority) return null;
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            var session = segments.LastOrDefault(s => s.StartsWith("ses_", StringComparison.Ordinal));
            if (session == null) return null;
            string directory = null;
            if (segments.Length >= 2 && segments[1] == "session")
            {
                try
                {
                    var encoded = segments[0].Replace('-', '+').Replace('_', '/');
                    encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
                    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    if (Path.IsPathRooted(decoded) && Directory.Exists(decoded)) directory = decoded;
                }
                catch (FormatException) { }
            }
            return Tuple.Create(directory, session);
        }

        public void StopCli() => _cliTerminal?.Stop();

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

        private Dictionary<string, string> GetThemeColors()
        {
            var colors = new Dictionary<string, string>();
            try
            {
                colors["bg"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey));
                colors["textPrimary"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey));
                colors["textSecondary"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.CommandBarTextInactiveColorKey));
                colors["textAccent"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.AccentBorderColorKey));
                colors["border"] = ToHtml(VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBorderColorKey));
            }
            catch { }
            return colors;
        }

        private string GetThemeStyleBlock()
        {
            var c = GetThemeColors();
            string bg = c.TryGetValue("bg", out var v0) ? v0 : "#252526";
            string text = c.TryGetValue("textPrimary", out var v1) ? v1 : "#f1f1f1";
            string textMuted = c.TryGetValue("textSecondary", out var v2) ? v2 : "#999999";
            string accent = c.TryGetValue("textAccent", out var v3) ? v3 : "#007acc";
            string border = c.TryGetValue("border", out var v4) ? v4 : "#434346";
            var accentHover = LighterHex(accent, 0.20);
            var spinnerTrack = LighterHex(bg, 0.12);

            return $@"<style id=""ocstudio-page-theme"">
    :root {{
        --vs-bg: {bg};
        --vs-text: {text};
        --vs-text-muted: {textMuted};
        --vs-accent: {accent};
        --vs-accent-hover: {accentHover};
        --vs-border: {border};
        --vs-spinner-track: {spinnerTrack};
    }}
</style>";
        }

        private static string LighterHex(string hex, double amount)
        {
            try
            {
                int r = Convert.ToInt32(hex.Substring(1, 2), 16);
                int g = Convert.ToInt32(hex.Substring(3, 2), 16);
                int b = Convert.ToInt32(hex.Substring(5, 2), 16);
                r = Math.Min(255, (int)(r + (255 - r) * amount));
                g = Math.Min(255, (int)(g + (255 - g) * amount));
                b = Math.Min(255, (int)(b + (255 - b) * amount));
                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch { return hex; }
        }

        private static string ToHtml(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private async Task InitWebViewCoreAsync()
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpenCodeStudio",
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
            if (_loginMode)
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
            if (!_loginMode) return;
            var u = e.Uri ?? "";
            if (!ConsoleLoginService.IsReturnedToApp(u)) return;
            // Вход выполнен: opencode вернул нас на свою страницу. НЕ отменяем загрузку —
            // именно ответ этой навигации ставит сессионную cookie. Захват делаем в NavigationCompleted.
            Services.Log.Info("Console login: returned to opencode.ai, waiting for cookie");
            _loginReturned = true;
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
            var parsed = ParseSessionFromUrl(webView?.CoreWebView2?.Source);
            if (parsed != null)
            {
                _activeSessionId = parsed.Item2;
                if (!string.IsNullOrEmpty(parsed.Item1)) _activeDirectory = parsed.Item1;
            }
            // Вернулись на opencode.ai — забираем cookie именно после загрузки ответа.
            if (_loginMode && _loginReturned)
            {
                _loginReturned = false;
                _ = FinishConsoleCaptureAsync();
            }
            // Инъекция кнопки Console идемпотентна: включаем/снимаем по настройке
            // на каждой загрузке документа, чтобы смена применялась после reload.
            _ = ApplyUsageInjectionAsync();
            PushUsageState(_autoOpenAfterLogin);
            _autoOpenAfterLogin = false;
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
            CancelReconnect();
        }

        private void OnServerConnectionLost()
        {
            var state = _serverController?.State ?? ConnectionState.Disconnected;
            Debug.WriteLine($"Server connection lost! State: {state}. Auto-reconnecting...");
            BeginAutoReconnect();
        }

        private void OnServerConnectionRestored()
        {
            Debug.WriteLine("Server connection restored!");
            if (_isReconnecting)
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
        /// Automatically tries to restore the connection instead of showing an
        /// error page that requires a manual Retry click. Shows a progress page
        /// with the current attempt number and keeps retrying until the server
        /// is healthy again (or the attempt budget is exhausted).
        /// </summary>
        private void BeginAutoReconnect()
        {
            if (_isReconnecting || _isDisposed) return;
            _isReconnecting = true;

            try { _reconnectCts?.Cancel(); } catch { }
            _reconnectCts = new System.Threading.CancellationTokenSource();
            var ct = _reconnectCts.Token;

#pragma warning disable VSSDK007
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    int consecutiveFailures = 0;
                    for (int attempt = 1; attempt <= MaxReconnectAttempts && !ct.IsCancellationRequested; attempt++)
                    {
                        await ShowLoadingPageAsync($"{StringsHelper.UIReconnecting} ({attempt})");

                        try
                        {
                            var healthy = await _serverController.ServerService.CheckHealthAsync();

                            if (!healthy)
                            {
                                consecutiveFailures++;
                                // Give the connection a few quick retries before doing the
                                // heavy restart, so a transient blip does not kill a
                                // perfectly healthy server.
                                if (consecutiveFailures >= 3)
                                {
                                    var started = await _serverController.StartAsync(_currentProjectRoot);
                                    healthy = started && _serverController.State == ConnectionState.Connected;
                                    consecutiveFailures = 0;
                                }
                            }
                            else
                            {
                                consecutiveFailures = 0;
                            }

                            if (healthy)
                            {
                                Debug.WriteLine($"Reconnected on attempt {attempt}");
                                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                var url = _serverController.GetHomeUrl();
                                if (url != null && webView.CoreWebView2 != null)
                                {
                                    NavigateToSession(url);
                                    await ApplyInterfaceAsync();
                                }
                                else
                                    await StartFlowAsync();
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Reconnect attempt {attempt} failed: {ex}");
                        }

                        try { await Task.Delay(ReconnectDelayMs, ct); }
                        catch (OperationCanceledException) { return; }
                    }

                    if (!ct.IsCancellationRequested)
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        await ShowErrorPageAsync(StringsHelper.ErrorConnectionLost, true);
                    }
                }
                finally
                {
                    _isReconnecting = false;
                }
            });
#pragma warning restore VSSDK007
        }

        private void CancelReconnect()
        {
            try { _reconnectCts?.Cancel(); } catch { }
            _reconnectCts?.Dispose();
            _reconnectCts = null;
            _isReconnecting = false;
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
                var existingUrl = _serverController.GetHomeUrl();
                if (existingUrl != null)
                {
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

            var sessionUrl = _serverController.GetHomeUrl();
            if (sessionUrl != null)
                NavigateToSession(sessionUrl);

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
                                && !_isStarting && !_isReconnecting)
                            {
                                BeginAutoReconnect();
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
                StartSpendMonitor();
            }
        }

        /// <summary>Запускает фоновый монитор расхода, если инъекция включена.</summary>
        private void StartSpendMonitor()
        {
            // временно: парсинг отключён, кнопка только переключает консоль
            return;

            if (_spendMonitor != null) return;

            _spendMonitor = new SpendMonitor(_spendSettings);
            _spendMonitor.Updated += OnSpendUpdated;
            _spendMonitor.AuthRequired += OnSpendAuthRequired;
            _spendMonitor.Start();
        }

        private void OnSpendUpdated(SpendSnapshot s)
        {
#pragma warning disable VSSDK007
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var core = webView?.CoreWebView2;
                    if (core == null) return;
                    LatestSnapshot = s;
                    SnapshotUpdated?.Invoke(s);
                    PushUsageState();
                }
                catch (Exception ex)
                {
                    Services.Log.Error("Failed to push usage to WebView2", ex);
                }
            });
#pragma warning restore VSSDK007
        }

        private void OnSpendAuthRequired()
        {
            // Авто-вход больше не запускаем: показываем состояние входа в поповере.
#pragma warning disable VSSDK007
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                PushUsageState();
            });
#pragma warning restore VSSDK007
        }

        /// <summary>Отправляет текущее состояние использования в веб-UI (UI-поток).</summary>
        private void PushUsageState(bool autoOpen = false)
        {
            try
            {
                var core = webView?.CoreWebView2;
                if (core == null) return;
                var state = ConsoleSessionStore.Load() == null ? "login" : (LatestSnapshot != null ? "ready" : "loading");
                UsageInjector.Push(core, UsageInjector.BuildPayload(state, LatestSnapshot, true, autoOpen));
            }
            catch (Exception ex) { Services.Log.Error("Failed to push usage state", ex); }
        }

        private async Task FinishConsoleCaptureAsync()
        {
            try
            {
                var core = webView?.CoreWebView2;
                if (core == null) { _loginTcs?.TrySetResult(false); return; }

                for (int attempt = 1; attempt <= 10; attempt++)
                {
                    var cookie = await ConsoleLoginService.ReadCookieAsync(core);
                    if (string.IsNullOrEmpty(cookie))
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    var names = string.Join(",", cookie.Split(';')
                        .Select(p => p.Split('=')[0].Trim())
                        .Where(n => n.Length > 0));
                    Services.Log.Info($"Console login: attempt {attempt}, cookie names = {names}");

                    bool ok;
                    try { ok = await new SpendClient(cookie).ResolveOrgAsync(System.Threading.CancellationToken.None); }
                    catch (ConsoleAuthException) { ok = false; }
                    catch (Exception ex) { Services.Log.Warn("Console login: org check error: " + ex.Message); ok = false; }

                    if (ok)
                    {
                        ConsoleSessionStore.Save(cookie);
                        Services.Log.Info("Console login: valid session cookie saved");
                        _loginTcs?.TrySetResult(true);
                        return;
                    }

                    Services.Log.Warn($"Console login: cookie not valid yet (attempt {attempt})");
                    await Task.Delay(1500);
                }

                Services.Log.Warn("Console login: no valid cookie after retries");
                _loginTcs?.TrySetResult(false);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Console login: capture failed", ex);
                _loginTcs?.TrySetResult(false);
            }
        }

        private async Task BeginConsoleLoginAsync()
        {
            if (_loginInProgress) return;
            _loginInProgress = true;
            try
            {
                _loginTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                _loginMode = true;
                _loginReturned = false;
                await ShowLoadingPageAsync("Вход в OpenCode Console...");
                var core = webView?.CoreWebView2;
                if (core != null)
                    core.Navigate(ConsoleLoginService.LoginUrl);

                var timeout = Task.Delay(TimeSpan.FromMinutes(5));
                var finished = await Task.WhenAny(_loginTcs.Task, timeout);
                var ok = finished == _loginTcs.Task && _loginTcs.Task.Result;
                Services.Log.Info("Console login finished, success=" + ok);
            }
            catch (Exception ex)
            {
                Services.Log.Error("Console login failed", ex);
            }
            finally
            {
                try
                {
                    _autoOpenAfterLogin = true;
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var home = _serverController?.GetHomeUrl();
                    if (home != null && webView?.CoreWebView2 != null)
                    {
                        Services.Log.Info("Console login: returning to " + home);
                        webView.CoreWebView2.Navigate(home);
                    }
                    else
                    {
                        PushUsageState(true);
                    }
                    _spendMonitor?.Stop();
                    _spendMonitor = null;
                    StartSpendMonitor();
                }
                catch (Exception ex)
                {
                    Services.Log.Error("Console login: failed to return to app", ex);
                }
                _loginInProgress = false;
                _loginMode = false;
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
#pragma warning disable VSSDK007
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    try
                    {
                        var core = webView?.CoreWebView2;
                        if (core == null) return;
                        var current = core.Source ?? "";
                        if (current.StartsWith("https://opencode.ai", StringComparison.OrdinalIgnoreCase))
                        {
                            var home = _serverController?.GetHomeUrl();
                            if (home != null) core.Navigate(home);
                        }
                        else
                        {
                            core.Navigate("https://opencode.ai/console");
                        }
                    }
                    catch (Exception ex) { Services.Log.Error("toggle-console failed", ex); }
                });
#pragma warning restore VSSDK007
                return;
            }
            if (message == "login")
            {
#pragma warning disable VSSDK007
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(() => BeginConsoleLoginAsync());
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
        }

        private async Task ShowLoadingPageAsync(string message)
        {
            var html = LoadingPageTemplate
                .Replace("{theme-style}", GetThemeStyleBlock())
                .Replace("{message}", message);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.NavigateToString(html);
            }
        }

        private async Task ShowErrorPageAsync(string message, bool showRetry)
        {
            var escapedMessage = System.Net.WebUtility.HtmlEncode(message)
                .Replace("\n", "<br>")
                .Replace("\\n", "<br>");
            var retryButton = showRetry
                ? $@"<button id=""retryBtn"" onclick=""handleRetry()"">{StringsHelper.UIRetry}</button>"
                : "";
            var retryScript = showRetry
                ? @"var disabled=false;function handleRetry(){if(disabled)return;disabled=true;var b=document.getElementById('retryBtn');b.disabled=true;b.style.opacity='0.5';b.style.cursor='not-allowed';try{window.chrome.webview.postMessage('retry')}catch(e){}}"
                : "";

            var html = ErrorPageTemplate
                .Replace("{theme-style}", GetThemeStyleBlock())
                .Replace("{message}", escapedMessage)
                .Replace("{retryButton}", retryButton)
                .Replace("{retryScript}", retryScript);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.NavigateToString(html);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            try { _lifetimeCts.Cancel(); _lifetimeCts.Dispose(); } catch { }
            _projRootTimer?.Dispose();
            CancelReconnect();
            if (_spendMonitor != null)
            {
                _spendMonitor.Updated -= OnSpendUpdated;
                _spendMonitor.AuthRequired -= OnSpendAuthRequired;
            }
            _spendMonitor?.Dispose();
            _spendMonitor = null;
            _cliTerminal?.Dispose();
            _cliTerminal = null;
            webView?.Dispose();
        }
    }
}
