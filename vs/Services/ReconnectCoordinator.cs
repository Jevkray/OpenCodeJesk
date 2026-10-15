using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using OpenCodeJesk.Resources;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Часть окна, нужная координатору реконнекта (без прямых ссылок на окно).
    /// </summary>
    public interface IReconnectHost
    {
        string CurrentProjectRoot { get; }
        bool IsConnected { get; }
        Task<bool> CheckHealthAsync();
        Task<bool> StartServerAsync(string projectRoot);
        string GetHomeUrl();
        bool CanNavigate { get; }
        void NavigateHome(string url);
        Task ApplyInterfaceAsync();
        Task StartFlowAsync();
        Task ShowLoadingAsync(string message);
        Task ShowErrorAsync(string message, bool showRetry);
    }

    /// <summary>
    /// Авто-восстановление соединения: вместо страницы ошибки с ручной кнопкой
    /// Retry показывает прогресс и повторяет проверки health до успеха или
    /// исчерпания бюджета попыток.
    /// </summary>
    public sealed class ReconnectCoordinator : IDisposable
    {
        private readonly IReconnectHost _host;
        private bool _isReconnecting;
        private System.Threading.CancellationTokenSource _reconnectCts;
        private bool _isDisposed;
        private const int MaxReconnectAttempts = 30;
        private const int ReconnectDelayMs = 3000;

        public ReconnectCoordinator(IReconnectHost host)
        {
            _host = host;
        }

        public bool IsActive => _isReconnecting;

        /// <summary>Запускает петлю авто-переподключения (идемпотентно).</summary>
        public void Begin()
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
                        await _host.ShowLoadingAsync($"{StringsHelper.UIReconnecting} ({attempt})");

                        try
                        {
                            var healthy = await _host.CheckHealthAsync();

                            if (!healthy)
                            {
                                consecutiveFailures++;
                                // Даём соединению несколько быстрых повторов перед
                                // тяжёлым перезапуском: короткий сбой не должен
                                // убивать здоровый сервер.
                                if (consecutiveFailures >= 3)
                                {
                                    var started = await _host.StartServerAsync(_host.CurrentProjectRoot);
                                    healthy = started && _host.IsConnected;
                                    consecutiveFailures = 0;
                                }
                            }
                            else
                            {
                                consecutiveFailures = 0;
                            }

                            if (healthy)
                            {
                                System.Diagnostics.Debug.WriteLine($"Reconnected on attempt {attempt}");
                                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                                var url = _host.GetHomeUrl();
                                if (url != null && _host.CanNavigate)
                                {
                                    _host.NavigateHome(url);
                                    await _host.ApplyInterfaceAsync();
                                }
                                else
                                    await _host.StartFlowAsync();
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Reconnect attempt {attempt} failed: {ex}");
                        }

                        try { await Task.Delay(ReconnectDelayMs, ct); }
                        catch (OperationCanceledException) { return; }
                    }

                    if (!ct.IsCancellationRequested)
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        await _host.ShowErrorAsync(StringsHelper.ErrorConnectionLost, true);
                    }
                }
                finally
                {
                    _isReconnecting = false;
                }
            });
#pragma warning restore VSSDK007
        }

        /// <summary>Прерывает петлю переподключения.</summary>
        public void Cancel()
        {
            try { _reconnectCts?.Cancel(); } catch { }
            _reconnectCts?.Dispose();
            _reconnectCts = null;
            _isReconnecting = false;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Cancel();
        }
    }
}
