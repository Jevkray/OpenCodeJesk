using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCodeStudio.Services
{
    /// <summary>Расход по одной модели.</summary>
    public sealed class ModelSpend
    {
        public string Model;
        public double Cost;
        public long Tokens;
    }

    /// <summary>Расход за один локальный день с разбивкой по моделям.</summary>
    public sealed class DaySpend
    {
        public string Date;
        public double Cost;
        public Dictionary<string, double> ByModel = new Dictionary<string, double>();
    }

    /// <summary>Снимок состояния расходов/лимитов для UI.</summary>
    public sealed class SpendSnapshot
    {
        public DateTimeOffset GeneratedAt;
        public bool AuthRequired;
        public bool HasGo;
        public double TodayCost;
        public SpendLimits Limits;
        public List<ModelSpend> ByModel = new List<ModelSpend>();
        public List<DaySpend> ByDay = new List<DaySpend>();
        public List<SpendRow> Rows = new List<SpendRow>();
    }

    /// <summary>
    /// Фоновый опрос консоли opencode.ai. Пока не подключён к tool window.
    /// </summary>
    internal sealed class SpendMonitor : IDisposable
    {
        private readonly SpendSettings _settings;
        private CancellationTokenSource _cts;
        private Task _loop;

        public event Action<SpendSnapshot> Updated;
        public event Action AuthRequired;

        public SpendMonitor(SpendSettings settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (_loop != null) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _loop = Task.Run(() => RunAsync(token));
        }

        private async Task RunAsync(CancellationToken ct)
        {
            // Парсинг лимитов и истории отключён; интервалы фиксированы.
            int limitsSec = 5;
            int historySec = 60;

            string cookie = null;
            SpendClient client = null;
            SpendLimits limits = null;
            var rows = new List<SpendRow>();
            var lastLimits = DateTime.MinValue;
            var lastHistory = DateTime.MinValue;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (string.IsNullOrEmpty(cookie))
                    {
                        cookie = ConsoleSessionStore.Load();
                        if (string.IsNullOrEmpty(cookie))
                        {
                            await Task.Delay(10000, ct);
                            continue;
                        }

                        client = new SpendClient(cookie);
                        var ok = await client.ResolveOrgAsync(ct);
                        if (!ok)
                        {
                            // Без org сессию рабочей не считаем и API не дёргаем.
                            Log.Warn("OpenCode console: could not resolve org");
                            cookie = null;
                            client = null;
                            await Task.Delay(15000, ct);
                            continue;
                        }
                        lastLimits = lastHistory = DateTime.MinValue;
                    }

                    var now = DateTime.UtcNow;
                    bool didWork = false;

                    if ((now - lastLimits).TotalSeconds >= limitsSec)
                    {
                        limits = await client.GetLimitsAsync(ct);
                        lastLimits = now;
                        didWork = true;
                    }

                    if ((now - lastHistory).TotalSeconds >= historySec)
                    {
                        // Сначала широкий диапазон; при ошибке откатываемся на 30d.
                        try
                        {
                            rows = await client.GetUsageRowsAsync("90d", ct);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            Log.Error("Spend history 90d failed, retry 30d", ex);
                            try
                            {
                                rows = await client.GetUsageRowsAsync("30d", ct);
                            }
                            catch (OperationCanceledException) { throw; }
                            catch (Exception ex2)
                            {
                                Log.Error("Spend history 30d failed too", ex2);
                            }
                        }
                        lastHistory = now;
                        didWork = true;
                    }

                    if (didWork)
                        Updated?.Invoke(BuildSnapshot(limits, rows));

                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsoleAuthException)
                {
                    // Временно не чистим cookie — только логируем для диагностики.
                    Log.Warn("Console session rejected (401) — keeping cookie for diagnostics");
                    // Сбрасываем состояние: возможно, пользователь уже перелогинился.
                    cookie = null;
                    client = null;
                    limits = null;
                    rows = new List<SpendRow>();
                    lastLimits = lastHistory = DateTime.MinValue;
                    AuthRequired?.Invoke();
                    try { await Task.Delay(60000, ct); }
                    catch (OperationCanceledException) { break; }
                }
                catch (Exception ex)
                {
                    Log.Error("Spend monitor loop failed", ex);
                    try { await Task.Delay(5000, ct); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private static SpendSnapshot BuildSnapshot(SpendLimits limits, List<SpendRow> rows)
        {
            var snap = new SpendSnapshot
            {
                GeneratedAt = DateTimeOffset.Now,
                Limits = limits,
                HasGo = limits?.HasGo ?? false,
                Rows = rows ?? new List<SpendRow>()
            };

            var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var byModel = new Dictionary<string, ModelSpend>();
            var byDay = new Dictionary<string, DaySpend>();

            foreach (var row in rows ?? new List<SpendRow>())
            {
                if (!DateTimeOffset.TryParse(row.CreatedAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                    continue;

                var date = dt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                if (!byDay.TryGetValue(date, out var day))
                    byDay[date] = day = new DaySpend { Date = date };
                day.Cost += row.Cost;

                if (date == today)
                    snap.TodayCost += row.Cost;

                if (row.Model == null) continue;

                if (!byModel.TryGetValue(row.Model, out var ms))
                    byModel[row.Model] = ms = new ModelSpend { Model = row.Model };
                ms.Cost += row.Cost;
                ms.Tokens += row.Input + row.Output + row.Reasoning + row.CacheRead;

                day.ByModel.TryGetValue(row.Model, out var prev);
                day.ByModel[row.Model] = prev + row.Cost;
            }

            snap.ByModel = byModel.Values.OrderByDescending(m => m.Cost).ToList();
            snap.ByDay = byDay.Values.OrderByDescending(d => d.Date, StringComparer.Ordinal).ToList();
            return snap;
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            _loop = null;
        }

        public void Dispose() => Stop();
    }
}
