using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OpenCodeJesk.Models;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Управляет жизненным циклом сервера OpenCode независимо от tool window.
    /// Поддерживает передачу владения между окнами с взаимным исключением.
    /// </summary>
    public class ServerController : IDisposable
    {
        private readonly IOpenCodeServerService _serverService;
        private readonly Func<bool> _useV2;
        private IConnectionMonitor _connectionMonitor;
        private IOpenCodeSessionService _sessionService;

        private string _currentProjectRoot;
        private string _currentSessionId;

        private CancellationTokenSource _shutdownCts;
        private CancellationTokenSource _workspaceCheckCts;
        private const int AgentCheckIntervalMs = 10000;
        private const int WorkspaceCheckIntervalMs = 5000;
        private bool _isDisposed;

        public IOpenCodeServerService ServerService => _serverService;
        public IOpenCodeSessionService SessionService => _sessionService;
        public ConnectionState State => _serverService.State;
        public string CurrentSessionId => _currentSessionId;
        public string CurrentProjectRoot => _currentProjectRoot;

        public void UpdateProjectRoot(string newRoot)
        {
            if (!string.Equals(newRoot, _currentProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                _currentProjectRoot = newRoot;
            }
        }

        public event Action<ConnectionState> ServerStateChanged;
        public event Action ConnectionLost;
        public event Action ConnectionRestored;
        public event Action WorkspaceMismatch;

        public ServerController(Func<bool> useV2 = null)
        {
            _useV2 = useV2;
            _serverService = new OpenCodeServerService();
            _serverService.StateChanged += s => ServerStateChanged?.Invoke(s);
        }

        /// <summary>
        /// Запускает сервер для заданного корня проекта и подбирает активную сессию.
        /// </summary>
        public async Task<bool> StartAsync(string projectRoot)
        {
            CancelShutdown();
            _currentProjectRoot = projectRoot;

            _serverService.PreferV2 = _useV2?.Invoke() ?? false;
            bool wantV2 = _serverService.PreferV2;

            _sessionService = new OpenCodeSessionService(_serverService);

            var running = await _serverService.CheckHealthAsync();

            if (running)
            {
                // Подключённый сервер мог быть поднят другой версией (например,
                // раньше в этой же или прошлой сессии). Сверяем фактическую
                // ветку с выбранной и при расхождении перезапускаем.
                var curMajor = CliCommand.DetectMajor(_serverService.BinaryPath);
                bool curV2 = curMajor >= 2;
                if (curMajor == 0) curV2 = wantV2;
                Log.Info($"Server version check: current=v{(curV2 ? 2 : 1)} want=v{(wantV2 ? 2 : 1)}");
                if (curV2 != wantV2)
                {
                    _serverService.Stop();
                    running = false;
                }
            }

            if (!running)
            {
                var started = await _serverService.StartAsync(projectRoot);
                if (!started || _serverService.State != ConnectionState.Connected)
                    return false;
            }

            // Ищем существующую сессию проекта. Специально НЕ создаём здесь новую:
            // веб-UI OpenCode делает это по требованию, а автосоздание пустой
            // сессии при каждой смене проекта только засоряет список.
            // Если ничего не найдено — просто открываем главную веб-UI.
            Models.Session curSession = null;
            try
            {
                var sessions = await _sessionService.ListSessionsAsync(projectRoot);
                foreach (var s in sessions)
                {
                    if (string.Equals(
                        ProjectRootResolver.NormalizePath(s.Directory ?? ""),
                        projectRoot,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        curSession = s;
                        break;
                    }
                }
            }
            catch { }

            _currentSessionId = curSession?.Id;

            // Запускаем мониторинг соединения
            if (_connectionMonitor == null)
            {
                _connectionMonitor = new ConnectionMonitor(_serverService, 5000);
                _connectionMonitor.ConnectionLost += () => ConnectionLost?.Invoke();
                _connectionMonitor.ConnectionRestored += () => ConnectionRestored?.Invoke();
            }
            _connectionMonitor.Start();

            StartWorkspaceCheck();

            // Остановка по простою — только если явно включена в настройках (0 = выкл).
            var idleMinutes = SpendSettings.Current?.IdleStopMinutes ?? 0;
            if (idleMinutes > 0) StartShutdownCountdown(idleMinutes);

            return true;
        }


        /// <summary>
        /// Открывает черновик чата текущего проекта; остальные проекты доступны
        /// через штатный интерфейс OpenCode. Сессию создаёт сам веб-клиент.
        /// </summary>
        public string GetHomeUrl()
        {
            if (_serverService.ServerInfo == null)
                return null;
            return _serverService.ServerInfo.BaseUrl + "/";
        }

        /// <summary>
        /// Для v2 возвращает одноразовую pairing-ссылку
        /// (baseUrl + /auth/connect/&lt;code&gt;), переход на которую ставит
        /// сессионную cookie и открывает веб-приложение. Для v1 — null.
        /// </summary>
        public async Task<string> GetPairingConnectUrlAsync()
        {
            try
            {
                if (!CliCommand.IsV2(_serverService.BinaryPath)) return null;
                var client = _serverService.GetClient();
                if (client == null || _serverService.ServerInfo == null) return null;

                using (var body = new System.Net.Http.StringContent("", Encoding.UTF8, "application/json"))
                using (var response = await client.PostAsync("/api/pair", body))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Info($"Pair request failed: {(int)response.StatusCode}");
                        return null;
                    }
                    var json = await response.Content.ReadAsStringAsync();
                    var code = JObject.Parse(json)["code"]?.ToString();
                    if (string.IsNullOrEmpty(code)) return null;
                    return _serverService.ServerInfo.BaseUrl + "/auth/connect/" + code;
                }
            }
            catch (Exception ex)
            {
                Log.Info("Pair request error: " + ex.Message);
                return null;
            }
        }


        /// <summary>
        /// Активный чат папки — тот, где отправлено последнее сообщение.
        /// Терминал не сообщает свой чат, поэтому выбираем по времени
        /// последнего сообщения (пустые только что созданные чаты пропускаем).
        /// </summary>
        public async Task<Session> GetActiveSessionAsync(string directory)
        {
            if (_sessionService == null || string.IsNullOrEmpty(directory)) return null;
            try
            {
                var sessions = await _sessionService.ListSessionsAsync(directory);
                if (sessions == null || sessions.Count == 0) return null;
                var ordered = new System.Collections.Generic.List<Session>(sessions);
                ordered.Sort((a, b) => (b.Time?.Updated ?? 0).CompareTo(a.Time?.Updated ?? 0));

                Session best = null;
                long bestTime = -1;
                int inspected = 0;
                foreach (var session in ordered)
                {
                    if (session == null) continue;
                    if (inspected++ >= 5) break;
                    // Запросы только по НЕСКОЛЬКИМ свежим кандидатам, чтобы не тормозить.
                    var sent = await _sessionService.GetLastMessageTimeAsync(session.Id, directory);
                    if (sent > 0 && sent > bestTime) { bestTime = sent; best = session; }
                }
                return best ?? ordered[0];
            }
            catch (Exception ex)
            {
                Log.Info("Active session lookup: " + ex.Message);
                return null;
            }
        }

        public void Stop()
        {
            CancelWorkspaceCheck();
            CancelShutdown();
            _connectionMonitor?.Dispose();
            _connectionMonitor = null;
            _serverService.Stop();
        }

        private void StartWorkspaceCheck()
        {
            _workspaceCheckCts?.Cancel();
            _workspaceCheckCts = new CancellationTokenSource();
            var ct = _workspaceCheckCts.Token;

            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(WorkspaceCheckIntervalMs, ct);
                    if (ct.IsCancellationRequested) return;

                    if (_serverService.State != ConnectionState.Connected) continue;

                    try
                    {
                        var pathInfo = await _sessionService.GetServerPathAsync();
                        if (pathInfo == null) continue;

                        var dir = ProjectRootResolver.NormalizePath(pathInfo.Directory ?? "");
                        if (string.IsNullOrEmpty(dir)) continue;

                        if (!string.Equals(dir, _currentProjectRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"Server directory changed: {_currentProjectRoot} -> {dir}");
                            _currentProjectRoot = dir;
                            WorkspaceMismatch?.Invoke();
                        }
                    }
                    catch { }
                }
            });
        }

        private void CancelWorkspaceCheck()
        {
            _workspaceCheckCts?.Cancel();
            _workspaceCheckCts?.Dispose();
            _workspaceCheckCts = null;
        }

        private void StartShutdownCountdown(int idleMinutes)
        {
            _shutdownCts?.Cancel();
            _shutdownCts = new CancellationTokenSource();
            var ct = _shutdownCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        var busy = await IsAgentBusyAsync(ct);
                        if (busy)
                        {
                            await Task.Delay(AgentCheckIntervalMs, ct);
                            continue;
                        }

                        await Task.Delay(idleMinutes * 60 * 1000, ct);

                        if (!ct.IsCancellationRequested)
                        {
                            var stillBusy = await IsAgentBusyAsync(ct);
                            if (stillBusy) continue;

                            System.Diagnostics.Debug.WriteLine("ServerController: idle timeout, stopping server");
                            Stop();
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ServerController shutdown error: {ex}");
                }
            });
        }

        private void CancelShutdown()
        {
            if (_shutdownCts != null)
            {
                _shutdownCts.Cancel();
                _shutdownCts.Dispose();
                _shutdownCts = null;
            }
        }

        private async Task<bool> IsAgentBusyAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_currentSessionId)
                || _serverService.State != ConnectionState.Connected)
                return false;

            try
            {
                var client = _serverService.GetClient();
                if (client == null) return false;

                var response = await client.GetAsync(
                    $"/session/{_currentSessionId}/status", ct);
                if (!response.IsSuccessStatusCode) return false;

                var json = await response.Content.ReadAsStringAsync();
                return json.Contains("\"status\":\"busy\"")
                    || json.Contains("\"status\":\"working\"");
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Stop();
        }
    }
}
