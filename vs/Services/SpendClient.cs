using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OpenCodeJesk.Services
{
    /// <summary>Лимиты подписки OpenCode Go (окна 5 часов / неделя / месяц).</summary>
    public sealed class SpendLimits
    {
        public bool HasGo;
        public bool UseBalance;
        public double FiveUsed, FiveLimit, FivePct; public int FiveResetSec;
        public double WeekUsed, WeekLimit, WeekPct; public int WeekResetSec;
        public double MonthUsed, MonthLimit, MonthPct; public int MonthResetSec;
    }

    /// <summary>Одна строка расхода из истории консоли.</summary>
    public sealed class SpendRow
    {
        public string Id, Model, Provider, CreatedAt;
        public long Input, Output, Reasoning, CacheRead;
        public double Cost;
    }

    /// <summary>
    /// Порт парсера из OpenCodeSpend: ходит в JSON-API консоли opencode.ai
    /// по сессионной cookie. 1 USD = 100 000 000 microCents.
    /// </summary>
    internal sealed class SpendClient
    {
        private const string ApiBase = "https://opencode.ai/console/api";
        private const decimal Unit = 100_000_000m;
        private const int MaxPages = 200;

        // Один общий HttpClient + распаковка gzip/deflate.
        private static readonly HttpClient Http = CreateClient();
        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        }

        private readonly string _cookie;
        private string _org;

        public SpendClient(string cookie)
        {
            _cookie = cookie;
        }

        public bool HasSession => !string.IsNullOrEmpty(_cookie);

        public async Task<bool> ResolveOrgAsync(CancellationToken ct)
        {
            var root = await GetAsync("/orgs", ct);
            if (root is JArray arr && arr.Count > 0)
            {
                _org = (string)arr[0]?["id"];
                return !string.IsNullOrEmpty(_org);
            }
            return false;
        }

        public async Task<SpendLimits> GetLimitsAsync(CancellationToken ct)
        {
            var root = await GetAsync("/go/status", ct);
            var meters = root?["access"]?["meters"];
            var limits = new SpendLimits { UseBalance = (bool?)root?["useBalance"] ?? false };

            ReadMeter(meters?["fiveHour"], out limits.FiveUsed, out limits.FiveLimit, out limits.FivePct, out limits.FiveResetSec);
            ReadMeter(meters?["week"], out limits.WeekUsed, out limits.WeekLimit, out limits.WeekPct, out limits.WeekResetSec);
            ReadMeter(meters?["month"], out limits.MonthUsed, out limits.MonthLimit, out limits.MonthPct, out limits.MonthResetSec);

            limits.HasGo = limits.FiveLimit > 0 || limits.WeekLimit > 0 || limits.MonthLimit > 0;
            return limits;
        }

        public async Task<List<SpendRow>> GetUsageRowsAsync(string range, CancellationToken ct)
        {
            var rows = new List<SpendRow>();
            string cursor = null;
            for (int page = 0; page < MaxPages; page++)
            {
                var path = "/usage/rows?range=" + range + "&pageSize=100";
                if (!string.IsNullOrEmpty(cursor))
                    path += "&cursor=" + Uri.EscapeDataString(cursor);

                var root = await GetAsync(path, ct);
                if (!(root?["items"] is JArray items) || items.Count == 0) break;

                foreach (var it in items)
                    rows.Add(MapRow(it));

                var next = (string)root["nextCursor"];
                if (string.IsNullOrEmpty(next) || next == cursor) break;
                cursor = next;
            }
            return rows;
        }

        private static SpendRow MapRow(JToken it) => new SpendRow
        {
            Id = (string)it["id"],
            Model = (string)it["model"],
            Provider = (string)it["provider"],
            CreatedAt = (string)it["createdAt"],
            Input = (long?)it["inputTokens"] ?? 0,
            Output = (long?)it["outputTokens"] ?? 0,
            Reasoning = (long?)it["reasoningTokens"] ?? 0,
            CacheRead = (long?)it["cacheReadTokens"] ?? 0,
            Cost = (double)((decimal?)it["costMicroCents"] ?? 0m) / (double)Unit
        };

        private static void ReadMeter(JToken m, out double used, out double limit, out double pct, out int resetSec)
        {
            used = limit = pct = 0;
            resetSec = 0;
            if (m == null) return;

            used = (double)((decimal?)m["usedMicroCents"] ?? 0m) / (double)Unit;
            limit = (double)((decimal?)m["limitMicroCents"] ?? 0m) / (double)Unit;
            if (limit > 0) pct = used / limit * 100.0;

            var resetsAt = (string)m["resetsAt"];
            if (!string.IsNullOrEmpty(resetsAt) && DateTimeOffset.TryParse(resetsAt, out var dt))
                resetSec = Math.Max(0, (int)(dt - DateTimeOffset.UtcNow).TotalSeconds);
        }

        private async Task<JToken> GetAsync(string path, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, ApiBase + path))
            {
                req.Headers.TryAddWithoutValidation("Cookie", _cookie);
                req.Headers.TryAddWithoutValidation("Accept", "application/json");
                req.Headers.TryAddWithoutValidation("Referer", "https://opencode.ai/console/");
                req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                if (!string.IsNullOrEmpty(_org))
                    req.Headers.TryAddWithoutValidation("x-org-id", _org);

                using (var resp = await Http.SendAsync(req, ct))
                {
                    if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        throw new ConsoleAuthException();

                    if (!resp.IsSuccessStatusCode)
                        Log.Warn($"Console API {path} -> {(int)resp.StatusCode}");

                    resp.EnsureSuccessStatusCode();
                    var json = await resp.Content.ReadAsStringAsync();
                    return JToken.Parse(json);
                }
            }
        }
    }

    /// <summary>Cookie консоли отсутствует или протухла — нужен повторный вход.</summary>
    internal sealed class ConsoleAuthException : Exception
    {
        public ConsoleAuthException() : base("OpenCode console session is missing or expired") { }
    }
}
