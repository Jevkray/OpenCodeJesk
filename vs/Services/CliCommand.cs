using System;
using System.Collections.Generic;
using System.IO;
using OpenCodeStudio.Models;

namespace OpenCodeStudio.Services
{
    public static class CliCommand
    {
        public static bool IsV2(string binary) => !string.IsNullOrEmpty(binary) &&
            Path.GetFileNameWithoutExtension(binary).Equals("opencode2", StringComparison.OrdinalIgnoreCase);

        public static string Arguments(string binary, ServerInfo server, string directory, string sessionId)
        {
            if (server == null) throw new InvalidOperationException("Сервер OpenCode не подключён.");
            var q = new Func<string, string>(PseudoTerminal.Quote);
            bool v2 = IsV2(binary);
            string arguments;
            if (v2)
            {
                arguments = "--server " + q(server.BaseUrl) + " " + q(directory);
            }
            else
                // В новых версиях opencode флага --dir нет: рабочий каталог
                // задаётся при запуске процесса (CreateProcess directory).
                arguments = "attach " + q(server.BaseUrl);
            if (!string.IsNullOrEmpty(sessionId)) arguments += " --session " + q(sessionId);
            // Дополнительные аргументы CLI из настроек.
            var extra = SpendSettings.Current?.CliArguments;
            if (!string.IsNullOrWhiteSpace(extra)) arguments += " " + extra.Trim();
            return arguments;
        }

        public static IDictionary<string, string> EnvironmentFor(ServerInfo server) => new Dictionary<string, string>
        {
            ["OPENCODE_SERVER_USERNAME"] = server.Username ?? "opencode",
            ["OPENCODE_SERVER_PASSWORD"] = server.Password ?? ""
        };
    }
}
