using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OpenCodeStudio.Models;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Manages OpenCode server lifecycle independently of any tool window.
    /// Supports ownership transfer between windows with mutual exclusion.
    /// </summary>
    public class ServerController : IDisposable
    {
        private readonly IOpenCodeServerService _serverService;
        private readonly Func<bool> _useV2;
        private IConnectionMonitor _connectionMonitor;
        private IOpenCodeSessionService _sessionService;

        private string _currentProjectRoot;
        private string _currentProjectDirectory;
        private string _currentSessionId;
        private string _currentSessionDirectory;

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
                _currentProjectDirectory = System.IO.Path.GetFullPath(newRoot);
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
        /// Start server for the given project root, creating or finding a session.
        /// </summary>
        public async Task<bool> StartAsync(string projectRoot)
        {
            CancelShutdown();
            _currentProjectRoot = projectRoot;
            _currentProjectDirectory = System.IO.Path.GetFullPath(projectRoot);

            _serverService.PreferV2 = _useV2?.Invoke() ?? false;

            _sessionService = new OpenCodeSessionService(_serverService);

            var running = await _serverService.CheckHealthAsync();

            if (!running)
            {
                var started = await _serverService.StartAsync(projectRoot);
                if (!started || _serverService.State != ConnectionState.Connected)
                    return false;
            }

            try
            {
                var path = await _sessionService.GetPathAsync(projectRoot);
                if (!string.IsNullOrEmpty(path?.Directory))
                    _currentProjectDirectory = path.Directory;
            }
            catch (Exception ex)
            {
                Log.Info("Project directory lookup failed: " + ex.Message);
            }

            // Find an existing session for this project. We deliberately do NOT
            // create one here: the OpenCode web UI does that on demand, and
            // auto-creating an empty session on every project change only adds
            // clutter. If nothing is found we simply open the web UI home.
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
            // Keep the directory exactly as OpenCode reports it (Windows path
            // with backslashes). The web client encodes this value into the URL,
            // so a normalized (forward-slash) path would not match the project.
            _currentSessionDirectory = curSession?.Directory;

            // Start connection monitoring
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
        /// Get the navigation URL for the current project/session.
        /// Points straight at the OpenCode server's own web UI
        /// (http://127.0.0.1:port/{encoded-dir}/session/{id}). The directory
        /// segment uses the same UTF-8 base64url encoding as the OpenCode web
        /// client (its <c>cn()</c> helper), so the app resolves the right
        /// project without any proxying.
        /// </summary>
        public string GetSessionUrl()
        {
            if (_serverService.ServerInfo == null || string.IsNullOrEmpty(_currentSessionId))
                return null;

            var baseUrl = _serverService.ServerInfo.BaseUrl;
            var dir = !string.IsNullOrEmpty(_currentSessionDirectory)
                ? _currentSessionDirectory
                : (_currentProjectRoot ?? "");
            var encoded = ToUrlSafeBase64(dir);
            return $"{baseUrl}/{encoded}/session/{_currentSessionId}";
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
        /// URL конкретного чата в веб-интерфейсе — используется при возврате
        /// из CLI, чтобы открыть тот же чат.
        /// </summary>
        public string GetSessionUrl(string directory, string sessionId)
        {
            if (_serverService.ServerInfo == null || string.IsNullOrEmpty(sessionId))
                return null;
            var dir = !string.IsNullOrEmpty(directory)
                ? directory
                : (_currentProjectDirectory ?? _currentProjectRoot ?? "");
            return $"{_serverService.ServerInfo.BaseUrl}/{ToUrlSafeBase64(dir)}/session/{sessionId}";
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

        private static string ToUrlSafeBase64(string text)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Stop();
        }
    }
}
