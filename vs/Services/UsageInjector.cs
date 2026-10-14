using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

namespace OpenCodeStudio.Services
{
    /// <summary>
    /// Встраивает в веб-UI OpenCode кнопку/поповер расхода и пушит в него данные.
    /// </summary>
    internal static class UsageInjector
    {
        private const string ResourceName = "OpenCodeStudio.Resources.UsageInjector.js";
        private static string _script;
        private static string _scriptId;

        /// <summary>
        /// Регистрирует скрипт для выполнения на каждом новом документе.
        /// Идемпотентно: повторные вызовы ничего не добавляют.
        /// </summary>
        public static async Task InstallAsync(CoreWebView2 core)
        {
            if (_scriptId != null) return;
            if (_script == null)
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                using (var reader = new StreamReader(stream))
                {
                    _script = reader.ReadToEnd();
                }
            }
            _scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(_script);
        }

        /// <summary>Снимает ранее зарегистрированный скрипт (если он был).</summary>
        public static Task UninstallAsync(CoreWebView2 core)
        {
            if (_scriptId == null) return Task.CompletedTask;
            try { core.RemoveScriptToExecuteOnDocumentCreated(_scriptId); }
            catch (Exception ex) { Log.Warn("UsageInjector uninstall failed: " + ex.Message); }
            _scriptId = null;
            return Task.CompletedTask;
        }

        public static void Push(CoreWebView2 core, string json)
        {
            try
            {
                core.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                Log.Error("Usage push failed", ex);
            }
        }

        /// <summary>JSON для инъекции: state, todayCost, showLimits, лимиты Go и строки расхода.</summary>
        public static string BuildPayload(string state, SpendSnapshot s, bool showLimits, bool autoOpen)
        {
            var limits = s?.Limits;
            return JsonConvert.SerializeObject(new
            {
                state,
                todayCost = s?.TodayCost ?? 0,
                showLimits,
                autoOpen,
                limits = new
                {
                    hasGo = limits?.HasGo ?? false,
                    fivePct = limits?.FivePct ?? 0,
                    weekPct = limits?.WeekPct ?? 0,
                    monthPct = limits?.MonthPct ?? 0,
                    fiveUsed = limits?.FiveUsed ?? 0,
                    fiveLimit = limits?.FiveLimit ?? 0,
                    fiveResetSec = limits?.FiveResetSec ?? 0,
                    weekUsed = limits?.WeekUsed ?? 0,
                    weekLimit = limits?.WeekLimit ?? 0,
                    weekResetSec = limits?.WeekResetSec ?? 0,
                    monthUsed = limits?.MonthUsed ?? 0,
                    monthLimit = limits?.MonthLimit ?? 0,
                    monthResetSec = limits?.MonthResetSec ?? 0
                },
                rows = (s?.Rows ?? new List<SpendRow>()).Select(r => new
                {
                    model = r.Model,
                    provider = r.Provider,
                    createdAt = r.CreatedAt,
                    cost = r.Cost
                })
            });
        }
    }
}
