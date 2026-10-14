using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OpenCodeStudio.Services
{
    /// <summary>Информация о доступной новой версии самого расширения.</summary>
    public sealed class UpdateInfo
    {
        public string Installed { get; set; }
        public string Latest { get; set; }
        public bool IsNewer { get; set; }
        public string ReleaseNotes { get; set; }
    }

    /// <summary>
    /// Проверяет последнюю версию OpenCode Studio на Visual Studio Marketplace
    /// и подтягивает release notes из GitHub. Все ошибки проглатываются.
    /// </summary>
    public sealed class ExtensionUpdateService
    {
        private const string MarketplaceQueryUrl =
            "https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery";
        private const string GitHubLatestReleaseUrl =
            "https://api.github.com/repos/Jevkray/OpenCodeStudio/releases/latest";
        private const int ReleaseNotesLimit = 1500;

        /// <summary>Проверка версии; null при любой ошибке.</summary>
        public async Task<UpdateInfo> CheckAsync()
        {
            try
            {
                var installed = typeof(ExtensionUpdateService).Assembly.GetName().Version?.ToString();
                var latest = await GetLatestVersionAsync();
                if (string.IsNullOrEmpty(installed) || string.IsNullOrEmpty(latest)) return null;

                return new UpdateInfo
                {
                    Installed = installed,
                    Latest = latest,
                    IsNewer = OpenCodeUpdateService.IsNewer(latest, installed),
                    ReleaseNotes = await GetReleaseNotesAsync()
                };
            }
            catch { return null; }
        }

        private static async Task<string> GetLatestVersionAsync()
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(
                        "Accept", "application/json;api-version=7.2-preview.1");

                    var body = new StringContent(
                        "{\"filters\":[{\"criteria\":[{\"filterType\":7,\"value\":\"jevkray.OpenCodeStudio\"}]}],\"flags\":914}",
                        Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(MarketplaceQueryUrl, body);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    return (string)JObject.Parse(json)["results"]?[0]?["extensions"]?[0]?["versions"]?[0]?["version"];
                }
            }
            catch { return null; }
        }

        private static async Task<string> GetReleaseNotesAsync()
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "OpenCodeStudio");
                    var json = await client.GetStringAsync(GitHubLatestReleaseUrl);
                    var body = ((string)JObject.Parse(json)["body"])?.Trim();
                    if (string.IsNullOrEmpty(body)) return null;
                    return body.Length > ReleaseNotesLimit
                        ? body.Substring(0, ReleaseNotesLimit) + "..."
                        : body;
                }
            }
            catch { return null; }
        }
    }
}
