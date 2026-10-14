using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.Web.WebView2.Core;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Веб-часть хоста, нужная координатору консоли (без прямых ссылок на окно).
    /// </summary>
    public interface IConsoleWebHost
    {
        CoreWebView2 GetCore();
        string GetHomeUrl();
        void Navigate(string url);
        Task ShowLoadingAsync(string message);
        SpendSettings GetSpendSettings();
    }

    /// <summary>
    /// Блок Console/Usage: монитор расходов, инъекция Usage и вход в OpenCode Console.
    /// </summary>
    public sealed class ConsoleCoordinator : IDisposable
    {
        private readonly IConsoleWebHost _host;
        private SpendMonitor _spendMonitor;
        private bool _loginInProgress, _loginMode, _loginReturned, _autoOpenAfterLogin;
        private TaskCompletionSource<bool> _loginTcs;

        public SpendSnapshot LatestSnapshot { get; private set; }

        public ConsoleCoordinator(IConsoleWebHost host)
        {
            _host = host;
        }

        /// <summary>Идёт ли сейчас интерактивный вход в консоль (навигация OAuth).</summary>
        public bool IsLoginMode => _loginMode;

        /// <summary>
        /// Включает или снимает инъекцию кнопки Console. Идемпотентно, можно
        /// вызывать на каждой навигации и после смены настроек.
        /// </summary>
        public async Task ApplyInjectionAsync(bool showConsoleButton)
        {
            var core = _host.GetCore();
            if (core == null) return;
            try
            {
                if (showConsoleButton)
                    await UsageInjector.InstallAsync(core);
                else
                    await UsageInjector.UninstallAsync(core);
            }
            catch (Exception ex)
            {
                Log.Error("Usage injection apply failed", ex);
            }
        }

        /// <summary>Запускает фоновый монитор расхода, если инъекция включена.</summary>
        public void StartMonitor()
        {
            // временно: парсинг отключён, кнопка только переключает консоль
            return;

            if (_spendMonitor != null) return;

            _spendMonitor = new SpendMonitor(_host.GetSpendSettings());
            _spendMonitor.Updated += OnSpendUpdated;
            _spendMonitor.AuthRequired += OnSpendAuthRequired;
            _spendMonitor.Start();
        }

        /// <summary>Останавливает монитор и отпускает подписки.</summary>
        public void StopMonitor()
        {
            if (_spendMonitor != null)
            {
                _spendMonitor.Updated -= OnSpendUpdated;
                _spendMonitor.AuthRequired -= OnSpendAuthRequired;
            }
            _spendMonitor?.Dispose();
            _spendMonitor = null;
        }

        public void Dispose() => StopMonitor();

        private void OnSpendUpdated(SpendSnapshot s)
        {
#pragma warning disable VSSDK007
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var core = _host.GetCore();
                    if (core == null) return;
                    LatestSnapshot = s;
                    PushState();
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to push usage to WebView2", ex);
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
                PushState();
            });
#pragma warning restore VSSDK007
        }

        /// <summary>Отправляет текущее состояние использования в веб-UI (UI-поток).</summary>
        public void PushState(bool autoOpen = false)
        {
            try
            {
                var core = _host.GetCore();
                if (core == null) return;
                var state = ConsoleSessionStore.Load() == null ? "login" : (LatestSnapshot != null ? "ready" : "loading");
                UsageInjector.Push(core, UsageInjector.BuildPayload(state, LatestSnapshot, true, autoOpen));
            }
            catch (Exception ex) { Log.Error("Failed to push usage state", ex); }
        }

        /// <summary>Реакция на начало навигации: фиксируем возврат OAuth на opencode.ai.</summary>
        public void NotifyNavigationStarting(string uri)
        {
            if (!_loginMode) return;
            if (!ConsoleLoginService.IsReturnedToApp(uri ?? "")) return;
            // Вход выполнен: opencode вернул нас на свою страницу. НЕ отменяем загрузку —
            // именно ответ этой навигации ставит сессионную cookie. Захват делаем в NavigationCompleted.
            Log.Info("Console login: returned to opencode.ai, waiting for cookie");
            _loginReturned = true;
        }

        /// <summary>Реакция на завершение навигации: захват cookie, инъекция, публикация состояния.</summary>
        public void NotifyNavigationCompleted()
        {
            // Вернулись на opencode.ai — забираем cookie именно после загрузки ответа.
            if (_loginMode && _loginReturned)
            {
                _loginReturned = false;
                _ = FinishConsoleCaptureAsync();
            }
            // Инъекция кнопки Console идемпотентна: включаем/снимаем по настройке
            // на каждой загрузке документа, чтобы смена применялась после reload.
            _ = ApplyInjectionAsync(_host.GetSpendSettings()?.ShowConsoleButton == true);
            PushState(_autoOpenAfterLogin);
            _autoOpenAfterLogin = false;
        }

        /// <summary>Вход в OpenCode Console.</summary>
        public async Task LoginAsync()
        {
            if (_loginInProgress) return;
            _loginInProgress = true;
            try
            {
                _loginTcs = new TaskCompletionSource<bool>();
                _loginMode = true;
                _loginReturned = false;
                await _host.ShowLoadingAsync("Вход в OpenCode Console...");
                var core = _host.GetCore();
                if (core != null)
                    core.Navigate(ConsoleLoginService.LoginUrl);

                var timeout = Task.Delay(TimeSpan.FromMinutes(5));
                var finished = await Task.WhenAny(_loginTcs.Task, timeout);
                var ok = finished == _loginTcs.Task && _loginTcs.Task.Result;
                Log.Info("Console login finished, success=" + ok);
            }
            catch (Exception ex)
            {
                Log.Error("Console login failed", ex);
            }
            finally
            {
                try
                {
                    _autoOpenAfterLogin = true;
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var home = _host.GetHomeUrl();
                    if (home != null && _host.GetCore() != null)
                    {
                        Log.Info("Console login: returning to " + home);
                        _host.Navigate(home);
                    }
                    else
                    {
                        PushState(true);
                    }
                    StopMonitor();
                    StartMonitor();
                }
                catch (Exception ex)
                {
                    Log.Error("Console login: failed to return to app", ex);
                }
                _loginInProgress = false;
                _loginMode = false;
            }
        }

        /// <summary>Обработка сообщения "toggle-console" из веб-страницы.</summary>
        public void HandleToggleConsole()
        {
#pragma warning disable VSSDK007
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    var core = _host.GetCore();
                    if (core == null) return;
                    var current = core.Source ?? "";
                    if (current.StartsWith("https://opencode.ai", StringComparison.OrdinalIgnoreCase))
                    {
                        var home = _host.GetHomeUrl();
                        if (home != null) core.Navigate(home);
                    }
                    else
                    {
                        core.Navigate("https://opencode.ai/console");
                    }
                }
                catch (Exception ex) { Log.Error("toggle-console failed", ex); }
            });
#pragma warning restore VSSDK007
        }

        private async Task FinishConsoleCaptureAsync()
        {
            try
            {
                var core = _host.GetCore();
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
                    Log.Info($"Console login: attempt {attempt}, cookie names = {names}");

                    bool ok;
                    try { ok = await new SpendClient(cookie).ResolveOrgAsync(System.Threading.CancellationToken.None); }
                    catch (ConsoleAuthException) { ok = false; }
                    catch (Exception ex) { Log.Warn("Console login: org check error: " + ex.Message); ok = false; }

                    if (ok)
                    {
                        ConsoleSessionStore.Save(cookie);
                        Log.Info("Console login: valid session cookie saved");
                        _loginTcs?.TrySetResult(true);
                        return;
                    }

                    Log.Warn($"Console login: cookie not valid yet (attempt {attempt})");
                    await Task.Delay(1500);
                }

                Log.Warn("Console login: no valid cookie after retries");
                _loginTcs?.TrySetResult(false);
            }
            catch (Exception ex)
            {
                Log.Error("Console login: capture failed", ex);
                _loginTcs?.TrySetResult(false);
            }
        }
    }
}
