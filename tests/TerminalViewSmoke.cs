using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCodeStudio.Services;

public static class TerminalViewSmoke
{
    [STAThread]
    public static int Main()
    {
        var app = new Application();
        var view = new WebView2();
        var window = new Window { Title = "OpenCode terminal check", Width = 900, Height = 550, Content = view, ShowInTaskbar = false };
        int result = 1;
        window.Loaded += async delegate
        {
            PseudoTerminal terminal = null;
            try
            {
                var environment = await CoreWebView2Environment.CreateAsync(null,
                    Path.Combine(Path.GetTempPath(), "opencode", "terminal-view-smoke"));
                await view.EnsureCoreWebView2Async(environment);
                var ready = new TaskCompletionSource<bool>();
                int cols = 0, rows = 0;
                view.CoreWebView2.WebMessageReceived += async delegate(object sender, CoreWebView2WebMessageReceivedEventArgs e)
                {
                    var message = JObject.Parse(e.TryGetWebMessageAsString());
                    var type = (string)message["type"];
                    if (type == "ready" || type == "resize")
                    {
                        cols = (int)message["cols"]; rows = (int)message["rows"];
                        if (terminal != null) terminal.Resize(cols, rows);
                        ready.TrySetResult(true);
                    }
                    if (type == "input" && terminal != null) await terminal.WriteAsync((string)message["data"]);
                };
                string html = Resource("Terminal.html").Replace("__XTERM_CSS__", Resource("xterm.css"))
                    .Replace("__XTERM_JS__", Resource("xterm.js")).Replace("__FIT_JS__", Resource("addon-fit.js"));
                view.CoreWebView2.NavigateToString(html);
                if (await Task.WhenAny(ready.Task, Task.Delay(15000)) != ready.Task) throw new Exception("xterm не отправил ready");
                terminal = new PseudoTerminal();
                terminal.Output = async data => await window.Dispatcher.InvokeAsync(() =>
                    view.CoreWebView2.PostWebMessageAsJson(JsonConvert.SerializeObject(new { type = "output", data = data })));
                terminal.Start(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /q /k chcp 65001",
                    Path.GetTempPath(), cols, rows, new Dictionary<string, string> { { "VIEW_SMOKE", "view-env-ok" } });
                await Task.Delay(1000);
                await view.CoreWebView2.ExecuteScriptAsync("term.input('echo %VIEW_SMOKE%\\r')");
                await view.CoreWebView2.ExecuteScriptAsync("term.input('echo Привет-тест\\r')");
                await Task.Delay(1500);
                var text = await view.CoreWebView2.ExecuteScriptAsync("Array.from({length:term.buffer.active.length},(_,i)=>term.buffer.active.getLine(i).translateToString()).join('\\n')");
                if (!text.Contains("view-env-ok") || !text.Contains("Привет-тест")) throw new Exception("Нет результата ввода в xterm: " + text);
                int previousCols = cols;
                window.Width = 650; window.Height = 400;
                await Task.Delay(1000);
                if (cols == previousCols) throw new Exception("xterm не изменил размер");
                Console.WriteLine("PASS WebView2 + xterm: offline assets, ready, keyboard bridge, UTF-8, rendering, resize");
                result = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { if (terminal != null) terminal.Dispose(); view.Dispose(); window.Close(); app.Shutdown(); }
        };
        app.Run(window);
        return result;
    }

    private static string Resource(string name)
    {
        using (var stream = typeof(PseudoTerminal).Assembly.GetManifestResourceStream("OpenCodeStudio.Resources." + name))
        using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
    }
}
