using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OpenCodeJesk.Models;
using OpenCodeJesk.Services;

public static class TerminalSmoke
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "opencode", "terminal-smoke-" + Guid.NewGuid().ToString("N"));
    public static int Main(string[] args)
    {
        try { CheckAsync(args).GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Assert(bool result, string message) { if (!result) throw new Exception(message); }
    private static async Task WaitAsync(Func<bool> predicate, string message, int seconds = 20)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (!predicate() && DateTime.UtcNow < end) await Task.Delay(100);
        Assert(predicate(), message);
    }

    private static async Task CheckAsync(string[] binaries)
    {
        Directory.CreateDirectory(Root);
        var project = Path.Combine(Root, "Проект с пробелами");
        Directory.CreateDirectory(project);
        var output = new StringBuilder();
        using (var terminal = new PseudoTerminal())
        {
            terminal.Output = data => { lock (output) output.Append(data); return Task.CompletedTask; };
            terminal.Start(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /q /k chcp 65001", project, 80, 24,
                new Dictionary<string, string> { { "STUDIO_TERMINAL_CHECK", "env-ok" } });
            await Task.Delay(1000);
            await terminal.WriteAsync("echo %STUDIO_TERMINAL_CHECK%\r");
            await terminal.WriteAsync("echo Привет-UTF8\r");
            await terminal.WriteAsync("cd\r");
            try
            {
                await WaitAsync(() => { lock (output) return output.ToString().Contains("env-ok") && output.ToString().Contains("Привет-UTF8"); }, "Нет ввода/UTF-8/окружения");
            }
            catch { lock (output) Console.WriteLine("OUTPUT: " + output); throw; }
            terminal.Resize(120, 35);
            await terminal.WriteAsync("mode con\r");
            await WaitAsync(() => { lock (output) return output.ToString().Contains("120"); }, "Resize не применился");
            Assert(terminal.IsRunning, "Процесс завершился при изменении размера");
            await terminal.WriteAsync("exit\r");
            await WaitAsync(() => !terminal.IsRunning, "CLI не завершился после exit");
        }
        Console.WriteLine("PASS ConPTY: input, UTF-8, environment, resize, exit");

        Assert(PseudoTerminal.Quote("C:\\a b\\") == "\"C:\\a b\\\\\"", "Неверное экранирование завершающего слеша");
        foreach (var binary in binaries) await CheckCliAsync(binary, project);
    }

    private static async Task CheckCliAsync(string binary, string project)
    {
        int port;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var server = new ServerInfo("127.0.0.1", port) { Username = "opencode", Password = Guid.NewGuid().ToString("N") };
        var env = CliCommand.EnvironmentFor(server);
        foreach (var key in new[] { "CONFIG", "DATA", "STATE", "CACHE" }) env["XDG_" + key + "_HOME"] = Path.Combine(Root, key);
        var psi = new ProcessStartInfo(binary, "serve --hostname 127.0.0.1 --port " + port)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = project };
        if (binary.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = Environment.GetEnvironmentVariable("COMSPEC");
            psi.Arguments = "/d /s /c \"" + PseudoTerminal.Quote(binary) + " serve --hostname 127.0.0.1 --port " + port + "\"";
        }
        foreach (var pair in env) psi.EnvironmentVariables[pair.Key] = pair.Value;
        using (var process = Process.Start(psi))
        using (var client = new HttpClient())
        {
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            client.Timeout = TimeSpan.FromSeconds(3);
            client.BaseAddress = new Uri(server.BaseUrl);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(server.Username + ":" + server.Password)));
            try
            {
                bool up = false;
                for (int i = 0; i < 60 && !up; i++)
                {
                    Assert(!process.HasExited, "Сервер завершился: " + binary);
                    try { up = (await client.GetAsync(server.BaseUrl)).IsSuccessStatusCode; } catch (HttpRequestException) { }
                    if (!up) await Task.Delay(500);
                }
                Assert(up, "Сервер не запустился");
                string session;
                bool v2 = CliCommand.IsV2(binary);
                string endpoint = v2 ? "/api/session" : "/session";
                {
                    var body = new JObject(new JProperty("title", "Studio CLI sync check"));
                    if (v2) body["location"] = new JObject(new JProperty("directory", project));
                    var response = await client.PostAsync(server.BaseUrl + endpoint + "?directory=" + Uri.EscapeDataString(project),
                        new StringContent(body.ToString(), Encoding.UTF8, "application/json"));
                    response.EnsureSuccessStatusCode();
                    var created = JObject.Parse(await response.Content.ReadAsStringAsync());
                    session = (string)(v2 ? created["data"]["id"] : created["id"]);
                }
                var api = new OpenCodeSessionService(new SmokeServer(binary, server, client));
                var selected = await api.GetSessionAsync(session, project);
                Assert(selected.Id == session && selected.Directory == project, "Неверная папка/сессия в API " + binary);
                var sessions = await api.ListSessionsAsync(project);
                Assert(sessions.Exists(item => item.Id == session), "Чат не найден в общей истории " + binary);
                var output = new StringBuilder();
                using (var terminal = new PseudoTerminal())
                {
                    terminal.Output = async data =>
                    {
                        lock (output) output.Append(data);
                        if (data.Contains("\x1b[6n")) await terminal.WriteAsync("\x1b[1;1R");
                        if (data.Contains("\x1b]11;?")) await terminal.WriteAsync("\x1b]11;rgb:1e1e/1e1e/1e1e\x1b\\");
                        if (data.Contains("\x1b]10;?")) await terminal.WriteAsync("\x1b]10;rgb:e6e6/e6e6/e6e6\x1b\\");
                    };
                    terminal.Start(binary, CliCommand.Arguments(binary, server, project, session), project, 110, 35, env);
                    await WaitAsync(() => { lock (output) return output.Length > 300; }, "CLI не вывел интерфейс", 40);
                    await Task.Delay(5000);
                    string text; lock (output) text = output.ToString();
                    File.WriteAllText(Path.Combine(Root, v2 ? "v2-output.txt" : "v1-output.txt"), text);
                    Assert(terminal.IsRunning, "CLI завершился: " + text.Substring(Math.Max(0, text.Length - 1500)));
                    Assert(!text.Contains("Unauthorized"), "CLI не авторизовался");
                    terminal.Resize(90, 28);
                    await terminal.WriteAsync("\x1b");
                    await Task.Delay(500);
                    Assert(terminal.IsRunning, "CLI не пережил resize/input");
                    Assert(text.Contains("Studio CLI sync check"), "CLI не открыл сессию общего сервера");
                    var read = await client.GetStringAsync(server.BaseUrl + endpoint + "/" + session + "?directory=" + Uri.EscapeDataString(project));
                    Assert(read.Contains(session), "Чат не доступен через Web API");
                    await client.DeleteAsync(server.BaseUrl + endpoint + "/" + session + "?directory=" + Uri.EscapeDataString(project));
                    int pid = terminal.ProcessId;
                    terminal.Dispose();
                    await WaitAsync(() => { try { return Process.GetProcessById(pid).HasExited; } catch (ArgumentException) { return true; } }, "CLI остался после Dispose");
                }
                Console.WriteLine("PASS CLI " + (v2 ? "v2" : "v1") + " (" + Path.GetExtension(binary) + "): authenticated shared server, TUI, resize, input, shutdown");
            }
            finally
            {
                if (!process.HasExited)
                {
                    using (var kill = Process.Start(new ProcessStartInfo("taskkill.exe", "/T /F /PID " + process.Id)
                        { UseShellExecute = false, CreateNoWindow = true })) kill.WaitForExit(5000);
                    process.WaitForExit(5000);
                }
            }
        }
        Console.WriteLine("Smoke data: " + Root);
    }

    private sealed class SmokeServer : IOpenCodeServerService
    {
        private readonly HttpClient _client;
        public SmokeServer(string binary, ServerInfo info, HttpClient client)
        { BinaryPath = binary; ServerInfo = info; _client = client; }
        public string BinaryPath { get; private set; }
        public ServerInfo ServerInfo { get; private set; }
        public bool PreferV2 { get; set; }
        public ConnectionState State { get { return ConnectionState.Connected; } }
        public event Action<ConnectionState> StateChanged { add { } remove { } }
        public HttpClient GetClient() { return _client; }
        public Task<bool> StartAsync(string root) { return Task.FromResult(true); }
        public Task<bool> CheckHealthAsync() { return Task.FromResult(true); }
        public void UpdateConnectionState(bool connected) { }
        public void Stop() { }
    }
}
