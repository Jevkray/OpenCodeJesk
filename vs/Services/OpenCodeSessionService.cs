using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCodeJesk.Models;
using OpenCodeJesk.Resources;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Manages OpenCode sessions via the HTTP API.
    /// </summary>
    public class OpenCodeSessionService : IOpenCodeSessionService
    {
        private readonly IOpenCodeServerService _serverService;

        public OpenCodeSessionService(IOpenCodeServerService serverService)
        {
            _serverService = serverService;
        }

        public async Task<List<Session>> ListSessionsAsync(string directory)
        {
            var client = _serverService.GetClient();
            if (client == null)
                throw new InvalidOperationException(StringsHelper.ErrorServerNotConnected);

            var encoded = Uri.EscapeDataString(directory);
            bool v2 = CliCommand.IsV2(_serverService.BinaryPath);
            var response = await client.GetAsync($"{(v2 ? "/api/session" : "/session")}?directory={encoded}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to list sessions: {response.StatusCode}");

            var json = await ReadUtf8Async(response.Content);
            if (!v2) return JsonConvert.DeserializeObject<List<Session>>(json);
            var result = new List<Session>();
            foreach (var item in JObject.Parse(json)["data"] ?? new JArray())
            {
                var session = item.ToObject<Session>();
                session.Directory = (string)item["location"]?["directory"];
                result.Add(session);
            }
            return result;
        }

        public async Task<Session> GetSessionAsync(string id, string directory)
        {
            var client = _serverService.GetClient();
            if (client == null) throw new InvalidOperationException(StringsHelper.ErrorServerNotConnected);
            bool v2 = CliCommand.IsV2(_serverService.BinaryPath);
            var path = (v2 ? "/api/session/" : "/session/") + Uri.EscapeDataString(id)
                + "?directory=" + Uri.EscapeDataString(directory);
            using (var response = await client.GetAsync(path))
            {
                response.EnsureSuccessStatusCode();
                var data = JObject.Parse(await ReadUtf8Async(response.Content));
                if (v2) data = (JObject)data["data"];
                var session = data.ToObject<Session>();
                if (v2) session.Directory = (string)data["location"]?["directory"];
                return session;
            }
        }

        /// <summary>
        /// Reads response content as UTF-8 explicitly. .NET Framework's
        /// ReadAsStringAsync falls back to the system codepage when the server
        /// omits a charset, which corrupts non-ASCII project paths (e.g. Cyrillic)
        /// and makes session matching fail.
        /// </summary>
        private static async Task<string> ReadUtf8Async(HttpContent content)
        {
            var bytes = await content.ReadAsByteArrayAsync();
            return Encoding.UTF8.GetString(bytes);
        }

        public async Task<Session> CreateSessionAsync(string directory, string title = null)
        {
            var client = _serverService.GetClient();
            if (client == null)
                throw new InvalidOperationException(StringsHelper.ErrorServerNotConnected);

            bool v2 = CliCommand.IsV2(_serverService.BinaryPath);
            var body = new JObject(new JProperty("title", title ?? "VS OpenCode"));
            if (v2) body["location"] = new JObject(new JProperty("directory", directory));

            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            var encoded = Uri.EscapeDataString(directory);
            var endpoint = v2 ? "/api/session" : "/session";
            var response = await client.PostAsync($"{endpoint}?directory={encoded}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await ReadUtf8Async(response.Content);
                throw new HttpRequestException(
                    $"Failed to create session (HTTP {response.StatusCode}): {errorBody}");
            }

            var responseJson = await ReadUtf8Async(response.Content);
            if (!v2) return JsonConvert.DeserializeObject<Session>(responseJson);
            var data = (JObject)JObject.Parse(responseJson)["data"];
            var session = data.ToObject<Session>();
            session.Directory = (string)data["location"]?["directory"];
            return session;
        }

        public async Task<long> GetLastMessageTimeAsync(string id, string directory)
        {
            var client = _serverService.GetClient();
            if (client == null) return -1;
            try
            {
                bool v2 = CliCommand.IsV2(_serverService.BinaryPath);
                var path = (v2 ? "/api/session/" : "/session/") + Uri.EscapeDataString(id)
                    + "/message?directory=" + Uri.EscapeDataString(directory);
                using (var response = await client.GetAsync(path))
                {
                    if (!response.IsSuccessStatusCode) return -1;
                    var token = JToken.Parse(await ReadUtf8Async(response.Content));
                    if (v2 && token is JObject wrapper) token = wrapper["data"] ?? new JArray();
                    if (!(token is JArray array) || array.Count == 0) return 0;
                    long best = 0;
                    foreach (var item in array)
                    {
                        var created = (long?)(item["info"]?["time"]?["created"] ?? item["time"]?["created"]);
                        if (created.HasValue && created.Value > best) best = created.Value;
                    }
                    return best;
                }
            }
            catch (Exception ex)
            {
                Log.Info("Last message lookup: " + ex.Message);
                return -1;
            }
        }

        public async Task<PathInfo> GetPathAsync(string directory)
        {
            if (CliCommand.IsV2(_serverService.BinaryPath))
                return new PathInfo { Directory = System.IO.Path.GetFullPath(directory) };
            var client = _serverService.GetClient();
            if (client == null)
                throw new InvalidOperationException(StringsHelper.ErrorServerNotConnected);

            var encoded = Uri.EscapeDataString(directory);
            var response = await client.GetAsync($"/path?directory={encoded}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to get path: {response.StatusCode}");

            var json = await ReadUtf8Async(response.Content);
            return JsonConvert.DeserializeObject<PathInfo>(json);
        }

        public async Task<PathInfo> GetServerPathAsync()
        {
            if (CliCommand.IsV2(_serverService.BinaryPath)) return null;
            var client = _serverService.GetClient();
            if (client == null)
                throw new InvalidOperationException(StringsHelper.ErrorServerNotConnected);

            var response = await client.GetAsync("/path");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to get server path: {response.StatusCode}");

            var json = await ReadUtf8Async(response.Content);
            return JsonConvert.DeserializeObject<PathInfo>(json);
        }

        public async Task<List<ProjectInfo>> ListProjectsAsync(string directory)
        {
            var client = _serverService.GetClient();
            if (client == null)
                throw new InvalidOperationException(StringsHelper.ErrorServerNotConnected);

            var encoded = Uri.EscapeDataString(directory);
            var response = await client.GetAsync($"/project?directory={encoded}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to list projects: {response.StatusCode}");

            var json = await ReadUtf8Async(response.Content);
            return JsonConvert.DeserializeObject<List<ProjectInfo>>(json);
        }

        public async Task<ProjectInfo> GetCurrentProjectAsync(string directory)
        {
            var client = _serverService.GetClient();
            if (client == null)
                throw new InvalidOperationException(StringsHelper.ErrorServerNotConnected);

            var encoded = Uri.EscapeDataString(directory);
            var response = await client.GetAsync($"/project/current?directory={encoded}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Failed to get current project: {response.StatusCode}");

            var json = await ReadUtf8Async(response.Content);
            return JsonConvert.DeserializeObject<ProjectInfo>(json);
        }
    }
}
