using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCodeJesk.Services;

namespace OpenCodeJesk
{
    /// <summary>
    /// Простое окно с терминалом OpenCode: без хрома Visual Studio, только
    /// xterm.js поверх ConPTY. Запускается сразу с открытым OpenCode.
    /// </summary>
    public partial class CliTerminalControl : UserControl, IDisposable
    {
        private PseudoTerminal _terminal;
        private IOpenCodeServerService _server;
        private string _directory, _session;
        private string _connectionKey;
        private Task _initialization;
        private bool _ready, _disposed, _starting;
        private int _columns = 100, _rows = 30;

        public CliTerminalControl() { InitializeComponent(); }

        public async Task ActivateAsync(IOpenCodeServerService server, string directory, string session)
        {
            if (_disposed || server?.ServerInfo == null) return;
            var key = server.BinaryPath + "\0" + server.ServerInfo.BaseUrl + "\0" + server.ServerInfo.Password
                + "\0" + directory + "\0" + session;
            Services.Log.Info("CLI: activate dir=" + directory + " session=" + (session ?? "-"));
            if (_connectionKey != key)
            {
                Stop();
                _connectionKey = key;
                Send(new { type = "reset" });
            }
            _server = server;
            // При возврате в тот же чат сохраняем терминал; другой чат подключаем отдельным клиентом.
            _directory = directory;
            _session = session;
            _initialization = _initialization ?? InitializeAsync();
            await _initialization;
            Services.Log.Info("CLI: initialized (ready=" + _ready + ")");
            if (_ready && (_terminal == null || !_terminal.IsRunning))
            {
                Stop();
                Send(new { type = "reset" });
                StartTerminal();
            }
            Send(new { type = "focus" });
            terminalView.Focus();
        }

        private static string Resource(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenCodeJesk.Resources." + name))
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }

        private async Task InitializeAsync()
        {
            var folder = Path.Combine(OpenCodeEnvironment.StudioDir, "TerminalWebView2");
            Directory.CreateDirectory(folder);
            var environment = await CoreWebView2Environment.CreateAsync(null, folder);
            await terminalView.EnsureCoreWebView2Async(environment);
            if (_disposed) return;
            var core = terminalView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.NavigationStarting += (_, e) =>
            {
                // Отменяем только внешние http(s) переходы. Внутренняя страница
                // (NavigateToString) идёт как about:blank — её отменять нельзя.
                if (!string.IsNullOrEmpty(e.Uri) &&
                    (e.Uri.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                     e.Uri.StartsWith("https:", StringComparison.OrdinalIgnoreCase)))
                    e.Cancel = true;
            };
            core.NavigationCompleted += (_, e) => Services.Log.Info(
                "CLI: page load success=" + e.IsSuccess + (e.IsSuccess ? "" : " error=" + e.WebErrorStatus));
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.WebMessageReceived += OnMessage;
            var s = SpendSettings.Current;
            var html = Resource("Terminal.html")
                .Replace("__XTERM_CSS__", Resource("xterm.css"))
                .Replace("__XTERM_JS__", Resource("xterm.js"))
                .Replace("__FIT_JS__", Resource("addon-fit.js"))
                .Replace("__FONT_FAMILY__", ((s?.TerminalFontFamily ?? "Consolas").Replace("'", "")) + ", monospace")
                .Replace("__FONT_SIZE__", (s?.TerminalFontSize ?? 13).ToString())
                .Replace("__CURSOR_BLINK__", (s?.TerminalCursorBlink ?? true) ? "true" : "false")
                .Replace("__SCROLLBACK__", (s?.TerminalScrollback ?? 5000).ToString());
            core.NavigateToString(html);
            Services.Log.Info("CLI: terminal page loaded into WebView2");
        }

        private async void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Источник NavigateToString в разных средах отличается (about:blank,
            // пустая строка), поэтому фильтровать по e.Source нельзя: навигацию
            // наружу всё равно блокирует NavigationStarting.
            if (_disposed) return;
            try
            {
                var message = JObject.Parse(e.TryGetWebMessageAsString());
                switch ((string)message["type"])
                {
                    case "ready":
                    case "resize":
                        _columns = Math.Max(2, Math.Min(500, (int?)message["cols"] ?? 100));
                        _rows = Math.Max(2, Math.Min(300, (int?)message["rows"] ?? 30));
                        if (!_ready)
                        {
                            _ready = true;
                            Services.Log.Info("CLI: page ready " + _columns + "x" + _rows);
                        }
                        if (_terminal == null && !_starting) StartTerminal();
                        else _terminal?.Resize(_columns, _rows);
                        break;
                    case "input":
                        var text = (string)message["data"];
                        if (text != null && text.Length <= 1024 * 1024 && _terminal != null)
                            await _terminal.WriteAsync(text);
                        break;
                    case "copy":
                        var selection = (string)message["data"];
                        if (!string.IsNullOrEmpty(selection) && selection.Length <= 1024 * 1024)
                            Clipboard.SetText(selection);
                        break;
                    case "paste":
                        if (Clipboard.ContainsText())
                            await terminalView.CoreWebView2.ExecuteScriptAsync("term.paste(" + JsonConvert.SerializeObject(Clipboard.GetText()) + ")");
                        break;
                }
            }
            catch (Exception ex) { Services.Log.Error("Terminal message failed", ex); }
        }

        private void StartTerminal()
        {
            if (_disposed || _starting || _server?.ServerInfo == null || !_ready) return;
            _starting = true;
            try
            {
                var terminal = new PseudoTerminal();
                _terminal = terminal;
                terminal.Output = async data =>
                {
                    if (_disposed) return;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!_disposed && _terminal == terminal) Send(new { type = "output", data });
                    });
                };
                terminal.Exited += code => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_disposed && _terminal == terminal)
                        Send(new { type = "output", data = "\r\n[OpenCode завершился: " + code + "]\r\n" });
                }));
                var arguments = CliCommand.Arguments(_server.BinaryPath, _server.ServerInfo, _directory, _session);
                terminal.Start(_server.BinaryPath, arguments, _directory, _columns, _rows,
                    CliCommand.EnvironmentFor(_server.ServerInfo));
                Log.Info("CLI connected to " + _server.ServerInfo.BaseUrl + " (pid=" + terminal.ProcessId + ")");
            }
            catch (Exception ex)
            {
                Stop();
                Log.Error("CLI startup failed", ex);
                Send(new { type = "output", data = "\r\n[Не удалось запустить OpenCode: " + ex.Message + "]\r\n" });
            }
            finally { _starting = false; }
        }

        private void Send(object message)
        {
            if (!_disposed && terminalView.CoreWebView2 != null)
                terminalView.CoreWebView2.PostWebMessageAsJson(JsonConvert.SerializeObject(message));
        }

        public void Stop()
        {
            var terminal = _terminal;
            _terminal = null;
            terminal?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            terminalView.Dispose();
        }
    }
}
