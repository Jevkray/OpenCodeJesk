using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Periodically checks OpenCode server health and fires connection state events.
    /// </summary>
    public class ConnectionMonitor : IConnectionMonitor
    {
        private readonly IOpenCodeServerService _serverService;
        private readonly int _intervalMs;
        private Timer _timer;
        private bool _disposed;

        public bool IsConnected { get; private set; }

        public event Action ConnectionLost;
        public event Action ConnectionRestored;

        public ConnectionMonitor(IOpenCodeServerService serverService, int intervalMs = 5000)
        {
            _serverService = serverService;
            _intervalMs = intervalMs;
        }

        public void Start()
        {
            Stop();
            IsConnected = _serverService.State == ConnectionState.Connected;
            _timer = new Timer(OnTimerTick, null, _intervalMs, _intervalMs);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private void OnTimerTick(object state)
        {
            if (_disposed) return;

            var previousState = IsConnected;

            _ = CheckAndNotifyAsync(previousState);
        }

        private async System.Threading.Tasks.Task CheckAndNotifyAsync(bool wasConnected)
        {
            if (_disposed) return;

            try
            {
                var healthy = await _serverService.CheckHealthAsync();
                IsConnected = healthy;

                if (!healthy)
                {
                    _serverService.UpdateConnectionState(false);
                    if (wasConnected)
                        ConnectionLost?.Invoke();
                }
                else
                {
                    _serverService.UpdateConnectionState(true);
                    if (!wasConnected)
                        ConnectionRestored?.Invoke();
                }
            }
            catch
            {
                _serverService.UpdateConnectionState(false);
                if (IsConnected)
                {
                    IsConnected = false;
                    ConnectionLost?.Invoke();
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            Stop();
        }
    }
}
