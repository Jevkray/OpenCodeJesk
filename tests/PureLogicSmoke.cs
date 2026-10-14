using System;
using System.Reflection;
using System.Runtime.Serialization;
using OpenCodeStudio.Services;

// Проверка чистой логики сервисов расширения (без UI и без сервера).
public static class PureLogicSmoke
{
    public static int Main()
    {
        try
        {
            CheckSettingsApply();
            CheckSettingsDefaults();
            CheckSessionUrl();
            CheckChangelog();
            CheckLocalization();
            Console.WriteLine("PASS pure logic: settings, session URL, changelog, localization");
            return 0;
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            Console.Error.WriteLine("FAIL " + inner.Message);
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void CheckSettingsApply()
    {
        // Объект без вызова конструктора: базовый тип DialogPage тянет за собой VS SDK.
        var settings = (SpendSettings)FormatterServices.GetUninitializedObject(typeof(SpendSettings));
        try
        {
            var form = new SettingsForm
            {
                PortText = "99999",
                ScrollbackText = "10",
                UpdateMinutesText = "0",
                InterfaceIndex = 1,
                RussianLanguage = true,
            };
            SettingsEditor.Apply(form, settings);

            Assert(settings.PreferredPort == 65535, "Порт 99999 должен клампиться в 65535, а не " + settings.PreferredPort);
            Assert(settings.TerminalScrollback == 500, "Scrollback 10 должен подняться до 500, а не " + settings.TerminalScrollback);
            Assert(settings.UpdateCheckMinutes == 5, "UpdateCheckMinutes 0 должен стать 5, а не " + settings.UpdateCheckMinutes);
            Assert(settings.InterfaceMode == InterfaceMode.CLI, "InterfaceIndex=1 должен дать CLI");
            Assert(settings.Language == "ru", "RussianLanguage=true должен дать ru, а не " + settings.Language);

            var bad = new SettingsForm { PortText = "abc" };
            SettingsEditor.Apply(bad, settings);
            Assert(settings.PreferredPort == 4096, "Нечисловой порт должен дать fallback 4096, а не " + settings.PreferredPort);
        }
        finally
        {
            GC.SuppressFinalize(settings);
        }
    }

    private static void CheckSettingsDefaults()
    {
        var form = SettingsEditor.FromSettings(null);
        Assert(form.WebZoom == 100, "Дефолтный WebZoom должен быть 100, а не " + form.WebZoom);
        Assert(form.PortText == "4096", "Дефолтный порт должен быть 4096, а не " + form.PortText);
        Assert(form.FontFamily == "Consolas", "Дефолтный шрифт должен быть Consolas, а не " + form.FontFamily);
    }

    private static void CheckSessionUrl()
    {
        // SessionUrlParser внутренний — вызываем через рефлексию.
        var type = typeof(SettingsEditor).Assembly.GetType("OpenCodeStudio.Services.SessionUrlParser");
        Assert(type != null, "Тип SessionUrlParser не найден");
        var method = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
        Assert(method != null, "Метод SessionUrlParser.Parse не найден");
        Func<string, string, Tuple<string, string>> parse = (url, baseUrl) =>
            (Tuple<string, string>)method.Invoke(null, new object[] { url, baseUrl });

        const string server = "http://127.0.0.1:4096";
        // Qzpc — base64 от "C:\".
        var ok = parse("http://127.0.0.1:4096/Qzpc/session/ses_abc", server);
        Assert(ok != null && ok.Item2 == "ses_abc", "Валидный URL должен дать session ses_abc");

        Assert(parse("http://evil.example/Qzpc/session/ses_abc", server) == null, "Чужой хост должен дать null");
        Assert(parse("http://127.0.0.1:4096/Qzpc/session/abc", server) == null, "URL без ses_ должен дать null");
    }

    private static void CheckChangelog()
    {
        var blocks = ChangelogService.Parse("[1.2.0]: https://example\n## [1.2.0]\n### Added\n- item");
        Assert(blocks.Count == 3, "Должно быть 3 блока (ссылка-сноска не считается), а не " + blocks.Count);
        Assert(blocks[0].Kind == ChangelogBlockKind.Version && blocks[0].Text == "[1.2.0]", "Первый блок должен быть версия [1.2.0]");
        Assert(blocks[1].Kind == ChangelogBlockKind.Category && blocks[1].Text == "Added", "Второй блок должен быть категория Added");
        Assert(blocks[2].Kind == ChangelogBlockKind.Bullet && blocks[2].Text == "item", "Третий блок должен быть пункт item");
    }

    private static void CheckLocalization()
    {
        Assert(Localization.Get("nonexistent.key") == "nonexistent.key", "Отсутствующий ключ должен вернуть сам ключ");
    }
}
