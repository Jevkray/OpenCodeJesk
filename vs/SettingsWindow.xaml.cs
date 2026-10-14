using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using OpenCodeStudio.Services;

namespace OpenCodeStudio
{
    /// <summary>
    /// Своё окно Settings в теме OpenCode: боковое меню разделов и карточки
    /// контролов. Открывается из меню и при первом запуске. Раздел Import
    /// сохраняет прежнее поведение мастера импорта.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly ImportService _service;
        private readonly string _solutionDir;
        private readonly Services.SpendSettings _settings;
        private readonly Action _onFinished;
        private readonly Dictionary<ImportItem, CheckBox> _checks = new Dictionary<ImportItem, CheckBox>();
        private string _customPath;
        private string _lang = "en";
        private bool _v1Available = true;
        private bool _v2Available = true;
        private System.Threading.CancellationTokenSource _installCts;
        private DispatcherTimer _installTimer;
        private DateTime _installStartedAt;
        private string _lastNpmLine;

        private static readonly SolidColorBrush TextBrush = Brush(0xf1, 0xf1, 0xf1);
        private static readonly SolidColorBrush MutedBrush = Brush(0x9a, 0x9a, 0x9a);
        private static readonly SolidColorBrush AccentBrush = Brush(0x2A, 0x78, 0xFC);
        private static readonly FontFamily MonoFont = new FontFamily("Consolas");

        private static SolidColorBrush Brush(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));

        // Локализация: Uid элемента -> текст. Ключи combo.* заполняются в коде.
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            { "window.title", Brand.Name + " Settings" },
            { "menu.title", "Settings" },
            { "menu.install", "Install" },
            { "menu.general", "General" },
            { "menu.terminal", "Terminal" },
            { "menu.server", "Server" },
            { "menu.updates", "Updates" },
            { "menu.import", "Import" },

            { "install.status.title", "OpenCode installation status" },
            { "install.status.v1", "OpenCode CLI (v1)" },
            { "install.status.v2", "OpenCode CLI (v2)" },
            { "install.status.v2.plugins", "v1 plugins are not compatible with v2 (v2 has its own plugin API)." },
            { "install.status.installed", "installed" },
            { "install.status.notinstalled", "not installed" },
            { "install.status.checking", "Checking…" },
            { "install.status.web", "Web interface" },
            { "install.status.web.available", "available" },
            { "install.status.web.unavailable", "unavailable — the web interface ships with the CLI; install the OpenCode CLI above." },
            { "install.status.desktop", "OpenCode Desktop app" },
            { "install.status.desktop.hint", "Download:" },
            { "install.guide.title", "Install OpenCode for Windows" },
            { "install.guide.hint", "OpenCode is distributed as an npm package. Install Node.js first, then run one of the commands below in a terminal." },
            { "install.guide.site", "Official site:" },
            { "install.guide.v1", "Stable (v1):" },
            { "install.guide.v2", "v2:" },
            { "install.guide.docs", "Documentation:" },
            { "install.button.v1", "Install v1" },
            { "install.button.v2", "Install v2" },
            { "install.note", "v2 installs both opencode and opencode2 and overwrites opencode from v1. To run both versions side by side, install v2 first, then v1 — then opencode = v1 and opencode2 = v2." },
            { "install.v2.size", "The v2 package is large (~300 MB); installation may take several minutes — progress and time are shown below." },
            { "install.cancel", "Cancel" },
            { "install.running", "Installing OpenCode CLI…" },
            { "install.done", "OpenCode CLI installed. Status updated." },
            { "install.failed", "Installation failed:" },
            { "install.cancelled", "Installation cancelled." },
            { "install.why.title", "Why OpenCode" },
            { "install.why.text", "Open source; works with many providers and models; runs from the terminal, IDE and desktop; shared history everywhere." },
            { "install.about.title", "What is OpenCode" },
            { "install.about.text", "OpenCode is an open-source AI coding agent that writes and edits code in your project, right where it lives." },
            { "install.about.details", "More" },
            { "install.about.desc", "OpenCode is an open-source AI coding agent for writing code. It runs in the terminal, in your IDE and as a desktop app.\n\nWhat it can do: writes and edits code in your project, understands your repository context, supports dozens of providers and models (including your own API keys), and keeps multiple sessions with shared history.\n\nHow to install:\n1. Install Node.js / npm.\n2. Run npm i -g opencode-ai (stable) — or npm i -g @opencode/cli for v2.\n3. Check it with opencode --version.\n4. Run opencode in a terminal, or come back to this extension.\n\nWhy it is handy: one account and shared history across the CLI, the desktop app and this extension; everything runs locally." },

            { "general.interface.title", "Interface" },
            { "general.interface.mode", "Interface mode" },
            { "general.interface.details", "Details" },
            { "general.interface.desc", "Desktop opens the full OpenCode web interface inside the tool window — the same experience as the browser or desktop app. CLI runs the same OpenCode, but as a text terminal you drive with typed commands. Example: if you prefer terminal hotkeys and a keyboard-driven flow, choose CLI." },
            { "general.version", "OpenCode version" },
            { "general.version.hint", "Choose the OpenCode branch to run." },
            { "general.version.details", "Details" },
            { "general.version.desc", "v1 is the stable branch, updated rarely and best for daily work. v2 (preview) is a newer branch with a reworked API and early features that may have rough edges. Example: if v2 fails to start or misbehaves, switch back to v1 — your settings stay the same." },
            { "general.zoom.title", "Web zoom" },
            { "general.zoom.hint", "Scales the OpenCode web panel (50–200%)." },
            { "general.zoom.details", "Details" },
            { "general.zoom.desc", "Sets the zoom level of the web panel only (not the terminal). Larger values make text and controls bigger and easier to read; smaller values fit more on screen. Example: 125% makes the text noticeably larger, 80% shows more content at once." },
            { "general.panel.title", "Panel behavior" },
            { "general.autoopen", "Auto-open panel when a solution opens" },
            { "general.autoopen.hint", "Opens the OpenCode tool window automatically whenever you open a solution." },
            { "general.autoopen.details", "Details" },
            { "general.autoopen.desc", "When enabled, the OpenCode tool window opens automatically every time you open a solution, so you can start right away without using the menu. Turn it off if you prefer to open the panel manually. Example: disable it for large solutions where you do not always need OpenCode." },
            { "general.console", "Show OpenCode authorization button (limits)" },
            { "general.console.hint", "Shows the panel button that signs you in to OpenCode Console to check your usage limits." },
            { "general.console.details", "Details" },
            { "general.console.desc", "Shows the button in the OpenCode panel that opens the OpenCode Console sign-in (authorization). Use it to sign in and see your usage limits and remaining quota. Example: turn it off for a cleaner panel if you never check limits." },

            { "terminal.appearance.title", "Terminal appearance" },
            { "terminal.font", "Font family" },
            { "terminal.font.details", "Details" },
            { "terminal.font.desc", "The font used to draw terminal text. Monospaced fonts (Consolas, Cascadia Mono) are best: every character has the same width, so tables, code and menus line up neatly. The change applies the next time the terminal is opened." },
            { "terminal.font.current", "Current terminal font: {0} — {1} pt" },
            { "terminal.fontsize", "Font size" },
            { "terminal.fontsize.details", "Details" },
            { "terminal.fontsize.desc", "Size of terminal text in points. Bigger is easier to read; smaller fits more lines. Example: set 15–16 for HiDPI displays, 11–12 to see more output at once." },
            { "terminal.cursorblink", "Cursor blink" },
            { "terminal.cursorblink.details", "Details" },
            { "terminal.cursorblink.desc", "Makes the terminal cursor blink so it is easy to spot. Turn it off if blinking distracts you. Example: some users prefer a steady cursor when reading long output." },
            { "terminal.scrollback", "Scrollback lines" },
            { "terminal.scrollback.hint", "500–50000 lines." },
            { "terminal.scrollback.details", "Details" },
            { "terminal.scrollback.desc", "How many previous lines the terminal keeps in its buffer, so you can scroll back. Larger values use more memory. Example: 5000 is fine for most work; raise it to 50000 if you need long build logs." },
            { "terminal.command.title", "Command" },
            { "terminal.cliargs", "Extra CLI arguments" },
            { "terminal.cliargs.hint", "Appended to the opencode arguments when the CLI terminal starts." },
            { "terminal.cliargs.details", "Details" },
            { "terminal.cliargs.desc", "Extra command-line arguments added right after 'opencode' when the CLI terminal starts. Use them to pass a model, a profile or other flags. Example: --model sonnet starts with the Sonnet model; separate multiple arguments with spaces." },
            { "terminal.startdir", "Start directory" },
            { "terminal.startdir.details", "Details" },
            { "terminal.startdir.desc", "Which folder the CLI terminal starts in. Git root is the repository root, Solution folder is where the .sln lives, .csproj folder is the project's folder. Example: choose .csproj folder if each project has its own OpenCode config." },

            { "server.binary.title", "OpenCode binary" },
            { "server.path", "Path to opencode binary" },
            { "server.path.hint", "Leave empty to auto-detect opencode/opencode2 on PATH." },
            { "server.path.details", "Details" },
            { "server.path.desc", "Full path to the opencode executable. Leave it empty and " + Brand.Name + " finds opencode/opencode2 on PATH automatically. Set it when you use a custom build or a non-standard install. Example: C:\\tools\\opencode\\opencode.exe." },
            { "server.browse", "Browse…" },
            { "server.title", "Server" },
            { "server.port", "Preferred port" },
            { "server.port.details", "Details" },
            { "server.port.desc", "The port the local OpenCode server prefers to listen on. If it is busy, a free port is used instead. Example: 4096 by default; change it if another tool already uses that port." },
            { "server.serveargs", "Extra serve arguments" },
            { "server.serveargs.details", "Details" },
            { "server.serveargs.desc", "Extra arguments passed to the 'opencode serve' command that starts the local server. Example: --print-logs writes server logs to the console for debugging; leave empty for normal use." },
            { "server.idle", "Stop server after idle minutes" },
            { "server.idle.hint", "0 = never stop." },
            { "server.idle.details", "Details" },
            { "server.idle.desc", "Automatically stops the local server after this many minutes without activity, freeing memory. 0 keeps it running always. Example: 30 stops an idle server after half an hour; 0 if you want instant responses." },

            { "updates.title", "Updates" },
            { "updates.check", "Check for new versions" },
            { "updates.check.hint", "Looks for a newer " + Brand.Name + " version on the Marketplace and shows a notification." },
            { "updates.check.details", "Details" },
            { "updates.check.desc", "When on, " + Brand.Name + " periodically checks the Marketplace for a newer version and shows a notification. Example: keep it on to learn about updates; turn it off on offline machines." },
            { "updates.minutes", "Check every (minutes)" },
            { "updates.minutes.details", "Details" },
            { "updates.minutes.desc", "How often to check for updates, in minutes. Smaller values find updates sooner but make more network requests. Example: 60 checks once an hour; 1440 once a day." },
            { "updates.channel", "Channel" },
            { "updates.channel.details", "Details" },
            { "updates.channel.desc", "Which release channel to follow. Stable only offers tested versions; Beta (next) also offers preview builds with new features. Example: choose Beta if you want early features and can tolerate occasional issues." },
            { "updates.review.title", "Enjoying " + Brand.Name + "?" },
            { "updates.review.hint", "A quick review on the Visual Studio Marketplace helps a lot and takes a minute." },
            { "updates.review.btn", "Leave a review" },
            { "updates.rating.show", "Show the review reminder" },
            { "updates.rating.show.hint", "Occasionally, after a couple of hours of use, a small window asks you to rate the extension." },
            { "updates.whatsnew.title", "What's new" },
            { "updates.whatsnew.hint", "Release notes: what changed and what was fixed in each version." },

            { "import.title", "Import OpenCode data" },
            { "import.hint", "Bring in settings, commands, themes, credentials and chat history from a detected OpenCode environment." },
            { "import.addfolder", "Add folder…" },
            { "import.importbtn", "Import selected" },

            { "bottom.language", "Language" },
            { "common.cancel", "Cancel" },
            { "common.save", "Save" },

            { "combo.interface.0", "Desktop (Web)" },
            { "combo.interface.1", "CLI (terminal)" },
            { "combo.version.0", "v1 (stable)" },
            { "combo.version.1", "v2 (preview)" },
            { "combo.version.notinstalled", "(not installed)" },
            { "combo.startdir.0", "Git root" },
            { "combo.startdir.1", "Solution folder" },
            { "combo.startdir.2", ".csproj folder" },
            { "combo.startdir.3", "Last used" },
            { "combo.channel.0", "Stable" },
            { "combo.channel.1", "Beta (next)" }
        };

        private static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            { "window.title", "Настройки " + Brand.Name },
            { "menu.title", "Настройки" },
            { "menu.install", "Установка" },
            { "menu.general", "Основные" },
            { "menu.terminal", "Терминал" },
            { "menu.server", "Сервер" },
            { "menu.updates", "Обновления" },
            { "menu.import", "Импорт" },

            { "general.interface.title", "Интерфейс" },
            { "general.interface.mode", "Режим интерфейса" },
            { "general.interface.details", "Подробнее" },
            { "general.interface.desc", "Рабочий стол открывает полный веб-интерфейс OpenCode внутри панели — так же, как в браузере или приложении. CLI запускает тот же OpenCode, но в виде текстового терминала, и вы работаете с ним командами. Пример: если удобнее горячие клавиши терминала и работа с клавиатуры — выберите CLI." },
            { "general.version", "Версия OpenCode" },
            { "general.version.hint", "Выберите ветку OpenCode для запуска." },
            { "general.version.details", "Подробнее" },
            { "general.version.desc", "v1 — стабильная ветка, меняется редко и подходит для повседневной работы. v2 (предпросмотр) — новая ветка с переработанным API и ранними возможностями, где могут быть шероховатости. Пример: если v2 не запускается или ведёт себя странно — вернитесь на v1, настройки сохранятся." },
            { "general.zoom.title", "Масштаб веб-панели" },
            { "general.zoom.hint", "Масштабирует веб-панель OpenCode (50–200%)." },
            { "general.zoom.details", "Подробнее" },
            { "general.zoom.desc", "Задаёт масштаб только веб-панели (не терминала). Большие значения увеличивают текст и элементы — удобнее читать; меньшие — вмещают больше на экране. Пример: 125% заметно увеличивает текст, 80% показывает больше содержимого сразу." },
            { "general.panel.title", "Поведение панели" },
            { "general.autoopen", "Авто-открытие панели при открытии решения" },
            { "general.autoopen.hint", "Автоматически открывает окно OpenCode при открытии решения." },
            { "general.autoopen.details", "Подробнее" },
            { "general.autoopen.desc", "Если включено, окно OpenCode открывается автоматически при открытии решения, и можно сразу работать без вызова из меню. Отключите, если предпочитаете открывать панель вручную. Пример: отключите для больших решений, где OpenCode нужен не всегда." },
            { "general.console", "Показывать кнопку авторизации OpenCode (лимиты)" },
            { "general.console.hint", "Показывает в панели кнопку входа в OpenCode Console — для проверки лимитов использования." },
            { "general.console.details", "Подробнее" },
            { "general.console.desc", "Показывает в панели кнопку авторизации в OpenCode Console. Через неё выполняется вход и видно лимиты использования и остаток квоты. Пример: выключите, если лимиты не проверяете и хотите более чистую панель." },

            { "terminal.appearance.title", "Внешний вид терминала" },
            { "terminal.font", "Шрифт" },
            { "terminal.font.details", "Подробнее" },
            { "terminal.font.desc", "Шрифт для отрисовки текста терминала. Лучше моноширинные (Consolas, Cascadia Mono): все символы одинаковой ширины, поэтому таблицы, код и меню выравниваются. Изменение применится при следующем открытии терминала." },
            { "terminal.font.current", "Текущий шрифт терминала: {0} — {1} pt" },
            { "terminal.fontsize", "Размер шрифта" },
            { "terminal.fontsize.details", "Подробнее" },
            { "terminal.fontsize.desc", "Размер текста терминала в пунктах. Крупнее — легче читать, меньше — вмещается больше строк. Пример: 15–16 для HiDPI-экранов, 11–12 чтобы видеть больше вывода сразу." },
            { "terminal.cursorblink", "Мигание курсора" },
            { "terminal.cursorblink.details", "Подробнее" },
            { "terminal.cursorblink.desc", "Заставляет курсор терминала мигать, чтобы его было легче заметить. Отключите, если мигание отвлекает. Пример: при чтении длинного вывода некоторым удобнее неподвижный курсор." },
            { "terminal.scrollback", "Строки истории" },
            { "terminal.scrollback.hint", "500–50000 строк." },
            { "terminal.scrollback.details", "Подробнее" },
            { "terminal.scrollback.desc", "Сколько предыдущих строк терминал держит в буфере для прокрутки назад. Большие значения расходуют больше памяти. Пример: 5000 хватает обычно; поднимите до 50000 для длинных логов сборки." },
            { "terminal.command.title", "Команда" },
            { "terminal.cliargs", "Доп. аргументы CLI" },
            { "terminal.cliargs.hint", "Добавляются к аргументам opencode при запуске терминала CLI." },
            { "terminal.cliargs.details", "Подробнее" },
            { "terminal.cliargs.desc", "Дополнительные аргументы командной строки, добавляемые сразу после 'opencode' при запуске терминала CLI. Через них задают модель, профиль и другие флаги. Пример: --model sonnet запускает с моделью Sonnet; несколько аргументов разделяйте пробелами." },
            { "terminal.startdir", "Стартовая папка" },
            { "terminal.startdir.details", "Подробнее" },
            { "terminal.startdir.desc", "С какой папки запускается терминал CLI. Корень Git — корень репозитория, Папка решения — где лежит .sln, Папка .csproj — папка проекта. Пример: выберите папку .csproj, если у каждого проекта своя конфигурация OpenCode." },

            { "server.binary.title", "Бинарник OpenCode" },
            { "server.path", "Путь к бинарнику opencode" },
            { "server.path.hint", "Оставьте пустым для автоопределения opencode/opencode2 в PATH." },
            { "server.path.details", "Подробнее" },
            { "server.path.desc", "Полный путь к исполняемому файлу opencode. Оставьте пустым — " + Brand.Name + " сам найдёт opencode/opencode2 в PATH. Задайте, если используете свою сборку или нестандартную установку. Пример: C:\\tools\\opencode\\opencode.exe." },
            { "server.browse", "Обзор…" },
            { "server.title", "Сервер" },
            { "server.port", "Предпочитаемый порт" },
            { "server.port.details", "Подробнее" },
            { "server.port.desc", "Порт, на котором локальный сервер OpenCode предпочитает слушать. Если он занят, берётся свободный. Пример: по умолчанию 4096; смените, если порт занят другой программой." },
            { "server.serveargs", "Доп. аргументы serve" },
            { "server.serveargs.details", "Подробнее" },
            { "server.serveargs.desc", "Дополнительные аргументы команды 'opencode serve', запускающей локальный сервер. Пример: --print-logs выводит логи сервера в консоль для отладки; для обычной работы оставьте пустым." },
            { "server.idle", "Останавливать сервер после простоя (мин)" },
            { "server.idle.hint", "0 = не останавливать." },
            { "server.idle.details", "Подробнее" },
            { "server.idle.desc", "Автоматически останавливает локальный сервер после указанного числа минут без активности, освобождая память. 0 — не останавливать. Пример: 30 остановит сервер после получаса простоя; 0 — если нужен мгновенный отклик." },

            { "updates.title", "Обновления" },
            { "updates.check", "Проверять новые версии" },
            { "updates.check.hint", "Ищет новую версию " + Brand.Name + " в Marketplace и показывает уведомление." },
            { "updates.check.details", "Подробнее" },
            { "updates.check.desc", "Если включено, " + Brand.Name + " периодически проверяет Marketplace на новую версию и показывает уведомление. Пример: держите включённым, чтобы узнавать об обновлениях; отключите на машинах без сети." },
            { "updates.minutes", "Проверять каждые (мин)" },
            { "updates.minutes.details", "Подробнее" },
            { "updates.minutes.desc", "Как часто проверять обновления, в минутах. Меньшие значения находят обновления быстрее, но чаще обращаются к сети. Пример: 60 — раз в час; 1440 — раз в сутки." },
            { "updates.channel", "Канал" },
            { "updates.channel.details", "Подробнее" },
            { "updates.channel.desc", "Какой канал выпусков отслеживать. Стабильный предлагает только проверенные версии; Бета (следующая) — ещё и предварительные сборки с новыми возможностями. Пример: выберите Бету, если хотите новые функции раньше и готовы к редким проблемам." },
            { "updates.review.title", "Нравится " + Brand.Name + "?" },
            { "updates.review.hint", "Отзыв в Visual Studio Marketplace очень помогает и занимает минуту." },
            { "updates.review.btn", "Оставить отзыв" },
            { "updates.rating.show", "Показывать напоминание об отзыве" },
            { "updates.rating.show.hint", "Иногда, после пары часов работы, появляется небольшое окно с просьбой оценить расширение." },
            { "updates.whatsnew.title", "Что нового" },
            { "updates.whatsnew.hint", "Список изменений: что добавлено и исправлено в каждой версии." },

            { "import.title", "Импорт данных OpenCode" },
            { "import.hint", "Перенести настройки, команды, темы, учётные данные и историю чатов из обнаруженного окружения OpenCode." },
            { "import.addfolder", "Добавить папку…" },
            { "import.importbtn", "Импортировать выбранное" },

            { "install.status.title", "Статус установки OpenCode" },
            { "install.status.v1", "OpenCode CLI (v1)" },
            { "install.status.v2", "OpenCode CLI (v2)" },
            { "install.status.v2.plugins", "v1-плагины не совместимы с v2 (у v2 свой API плагинов)." },
            { "install.status.installed", "установлено" },
            { "install.status.notinstalled", "не установлено" },
            { "install.status.checking", "Проверка…" },
            { "install.status.web", "Веб-интерфейс" },
            { "install.status.web.available", "доступен" },
            { "install.status.web.unavailable", "недоступен — веб-интерфейс поставляется вместе с CLI; установите OpenCode CLI выше." },
            { "install.status.desktop", "Приложение OpenCode Desktop" },
            { "install.status.desktop.hint", "Скачать:" },
            { "install.guide.title", "Установка OpenCode для Windows" },
            { "install.guide.hint", "OpenCode распространяется как npm-пакет. Сначала установите Node.js, затем выполните одну из команд ниже в терминале." },
            { "install.guide.site", "Официальный сайт:" },
            { "install.guide.v1", "Стабильная (v1):" },
            { "install.guide.v2", "v2:" },
            { "install.guide.docs", "Документация:" },
            { "install.button.v1", "Установить v1" },
            { "install.button.v2", "Установить v2" },
            { "install.note", "v2 ставит и opencode, и opencode2 и перекрывает opencode у v1. Чтобы обе версии работали рядом, установите сначала v2, затем v1 — тогда opencode = v1, а opencode2 = v2." },
            { "install.v2.size", "Пакет v2 большой (~300 МБ), установка может занять несколько минут — прогресс и время видны ниже." },
            { "install.cancel", "Отмена" },
            { "install.running", "Установка OpenCode CLI…" },
            { "install.done", "OpenCode CLI установлен. Статус обновлён." },
            { "install.failed", "Установка не удалась:" },
            { "install.cancelled", "Установка отменена." },
            { "install.why.title", "Почему OpenCode" },
            { "install.why.text", "Открытый исходный код; множество провайдеров и моделей; работа из терминала, IDE и десктопа; общая история." },
            { "install.about.title", "Что такое OpenCode" },
            { "install.about.text", "OpenCode — открытый ИИ-агент для написания кода, который пишет и правит код прямо в вашем проекте." },
            { "install.about.details", "Подробнее" },
            { "install.about.desc", "OpenCode — открытый ИИ-агент для написания кода. Работает в терминале, в вашей IDE и как десктоп-приложение.\n\nЧто умеет: пишет и правит код в вашем проекте, понимает контекст репозитория, поддерживает десятки провайдеров и моделей (в том числе собственные ключи), несколько сессий с общей историей.\n\nКак установить:\n1. Установите Node.js / npm.\n2. Выполните npm i -g opencode-ai (стабильная ветка) — или npm i -g @opencode/cli для v2.\n3. Проверьте: opencode --version.\n4. Запустите opencode в терминале или вернитесь в это расширение.\n\nПочему удобно: один аккаунт и общая история между CLI, десктопом и этим расширением; всё работает локально." },

            { "bottom.language", "Язык" },
            { "common.cancel", "Отмена" },
            { "common.save", "Сохранить" },

            { "combo.interface.0", "Рабочий стол (Web)" },
            { "combo.interface.1", "CLI (терминал)" },
            { "combo.version.0", "v1 (стабильная)" },
            { "combo.version.1", "v2 (предпросмотр)" },
            { "combo.version.notinstalled", "(не установлено)" },
            { "combo.startdir.0", "Корень Git" },
            { "combo.startdir.1", "Папка решения" },
            { "combo.startdir.2", "Папка .csproj" },
            { "combo.startdir.3", "Последняя" },
            { "combo.channel.0", "Стабильный" },
            { "combo.channel.1", "Бета (следующая)" }
        };

        public SettingsWindow(ImportService service, string solutionDir,
            Services.SpendSettings settings = null, Action onFinished = null, int initialTab = 1)
        {
            InitializeComponent();
            _service = service ?? new ImportService();
            _solutionDir = solutionDir;
            _settings = settings;
            _onFinished = onFinished;
            if (_settings != null) Services.SpendSettings.Current = _settings;

            LoadSettings();
            _lang = _settings?.Language == "ru" ? "ru" : "en";
            languageCombo.SelectedIndex = _lang == "ru" ? 1 : 0;
            SetVersionText();
            ApplyLanguage();
            menuList.SelectedIndex = Math.Max(0, Math.Min(menuList.Items.Count - 1, initialTab));
            RenderChangelog();
            Loaded += (_, __) => RebuildImport();

            // Детекция версий и статуса установки — в фоне, чтобы открытие окна
            // не подвешивало IDE.
            _ = DetectInstalledVersionsAsync();
            _ = RefreshInstallStatusAsync();
        }

        // ---- локализация -------------------------------------------------

        /// <summary>Проставляет тексты окна из словаря для текущего языка.</summary>
        private void ApplyLanguage()
        {
            var d = _lang == "ru" ? Ru : En;
            Title = d["window.title"];
            Localize(this, d);
            SetItems(interfaceCombo, d["combo.interface.0"], d["combo.interface.1"]);
            SetVersionItems(d);
            SetItems(startDirCombo, d["combo.startdir.0"], d["combo.startdir.1"],
                d["combo.startdir.2"], d["combo.startdir.3"]);
            SetItems(channelCombo, d["combo.channel.0"], d["combo.channel.1"]);
            _ = RefreshInstallStatusAsync();
        }

        /// <summary>Локализованная строка по ключу для текущего языка.</summary>
        private string Loc(string key)
        {
            var d = _lang == "ru" ? Ru : En;
            return d.TryGetValue(key, out var v) ? v : key;
        }

        /// <summary>
        /// Показывает оба пункта версии, неактивные — с пометкой «не установлено».
        /// </summary>
        private void SetVersionItems(Dictionary<string, string> d)
        {
            var v1 = d["combo.version.0"];
            var v2 = d["combo.version.1"];
            var missing = " " + d["combo.version.notinstalled"];
            if (versionCombo.Items.Count >= 2
                && versionCombo.Items[0] is ComboBoxItem i0
                && versionCombo.Items[1] is ComboBoxItem i1)
            {
                i0.Content = _v1Available ? v1 : v1 + missing;
                i1.Content = _v2Available ? v2 : v2 + missing;
            }
            else
            {
                SetItems(versionCombo, v1, v2);
            }
        }

        /// <summary>
        /// Определяет установленные ветки opencode в фоне (не на UI-потоке) и
        /// обновляет комбобокс версий после завершения.
        /// </summary>
        private async Task DetectInstalledVersionsAsync()
        {
            try
            {
                var v1 = await OpenCodeServerService.FindV1PathAsync();
                var v2 = await OpenCodeServerService.FindV2PathAsync();
                _v1Available = v1 != null;
                _v2Available = v2 != null;
            }
            catch (Exception ex)
            {
                Log.Error("Version detection failed", ex);
                return;
            }

            if (versionCombo.Items.Count >= 2
                && versionCombo.Items[0] is ComboBoxItem v1Item
                && versionCombo.Items[1] is ComboBoxItem v2Item)
            {
                v1Item.IsEnabled = _v1Available;
                v2Item.IsEnabled = _v2Available;
            }

            if (versionCombo.SelectedIndex == 1 && !_v2Available)
                versionCombo.SelectedIndex = _v1Available ? 0 : -1;
            else if (versionCombo.SelectedIndex == 0 && !_v1Available)
                versionCombo.SelectedIndex = _v2Available ? 1 : -1;

            SetVersionItems(_lang == "ru" ? Ru : En);
            ApplyVersionLock();
        }

        /// <summary>
        /// Обновляет список статусов установки на вкладке Install. Детекция идёт
        /// в фоне, пока показывается состояние «проверка…».
        /// </summary>
        private async Task RefreshInstallStatusAsync()
        {
            if (installStatusPanel == null) return;
            installStatusPanel.Children.Clear();
            installStatusPanel.Children.Add(new TextBlock
            {
                Text = Loc("install.status.checking"),
                Foreground = MutedBrush
            });

            string v1, v2, desktop, webCli;
            bool webAvailable;
            try
            {
                v1 = await OpenCodeServerService.FindV1PathAsync();
                v2 = await OpenCodeServerService.FindV2PathAsync();
                desktop = OpenCodeServerService.FindDesktopAppPath();
                webCli = v1 ?? v2;
                webAvailable = await OpenCodeServerService.SupportsWebAsync(webCli);
            }
            catch (Exception ex)
            {
                Log.Error("Install status refresh failed", ex);
                return;
            }

            if (installStatusPanel == null) return;
            installStatusPanel.Children.Clear();

            installStatusPanel.Children.Add(BuildCliRow(
                Loc("install.status.v1"), v1, "npm i -g opencode-ai"));
            installStatusPanel.Children.Add(BuildCliRow(
                Loc("install.status.v2"), v2, "npm i -g @opencode/cli"));
            installStatusPanel.Children.Add(new TextBlock
            {
                Text = Loc("install.status.v2.plugins"),
                FontSize = 12,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -6, 0, 12)
            });
            installStatusPanel.Children.Add(BuildSimpleRow(
                Loc("install.status.web"),
                webAvailable
                    ? Loc("install.status.web.available") + " (" + webCli + ")"
                    : Loc("install.status.web.unavailable"),
                null));
            installStatusPanel.Children.Add(BuildDesktopRow(desktop));
        }

        // Заголовок строки статуса: «пункт  статус».
        private UIElement BuildHeader(string title, string status)
        {
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TextBrush,
                FontWeight = FontWeights.SemiBold
            });
            head.Children.Add(new TextBlock
            {
                Text = "  " + status,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            return head;
        }

        // Строка CLI: при установке — моноширинный путь, иначе команда установки.
        private UIElement BuildCliRow(string title, string path, string installCommand)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(BuildHeader(title,
                path != null ? Loc("install.status.installed") : Loc("install.status.notinstalled")));
            row.Children.Add(new TextBlock
            {
                Text = path ?? installCommand,
                FontFamily = MonoFont,
                FontSize = 12,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            return row;
        }

        private UIElement BuildSimpleRow(string title, string status, string detail)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(BuildHeader(title, status));
            if (!string.IsNullOrEmpty(detail))
                row.Children.Add(new TextBlock
                {
                    Text = detail,
                    FontSize = 12,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            return row;
        }

        // Строка Desktop: путь либо ссылка на скачивание.
        private UIElement BuildDesktopRow(string path)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            row.Children.Add(BuildHeader(Loc("install.status.desktop"),
                path != null ? Loc("install.status.installed") : Loc("install.status.notinstalled")));

            if (path != null)
            {
                row.Children.Add(new TextBlock
                {
                    Text = path,
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            else
            {
                var line = new TextBlock
                {
                    FontSize = 12,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                line.Inlines.Add(new Run(Loc("install.status.desktop.hint") + " "));
                var link = new Hyperlink(new Run("https://opencode.ai/download"))
                {
                    NavigateUri = new Uri("https://opencode.ai/download")
                };
                link.RequestNavigate += OnHyperlink;
                line.Inlines.Add(link);
                row.Children.Add(line);
            }
            return row;
        }

        private static void Localize(DependencyObject node, Dictionary<string, string> d)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(node))
            {
                if (!(child is DependencyObject dep)) continue;

                if (child is FrameworkElement fe && !string.IsNullOrEmpty(fe.Uid) &&
                    d.TryGetValue(fe.Uid, out var text))
                {
                    if (child is TextBlock tb) tb.Text = text;
                    else if (child is Expander ex) ex.Header = text;
                    else if (child is ContentControl cc) cc.Content = text;
                }

                Localize(dep, d);
            }
        }

        private static void SetItems(ComboBox combo, params string[] texts)
        {
            for (int i = 0; i < combo.Items.Count && i < texts.Length; i++)
                if (combo.Items[i] is ComboBoxItem item) item.Content = texts[i];
        }

        private void SetVersionText()
        {
            var version = typeof(SettingsWindow).Assembly.GetName().Version;
            versionText.Text = Brand.Name + " " + version;
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            _lang = languageCombo.SelectedIndex == 1 ? "ru" : "en";
            ApplyLanguage();
            if (_settings != null)
            {
                _settings.Language = _lang;
                _settings.SaveSettingsToStorage();
            }
        }

        // ---- чтение / запись настроек -----------------------------------

        private void LoadSettings()
        {
            interfaceCombo.SelectedIndex = _settings?.InterfaceMode == InterfaceMode.CLI ? 1 : 0;
            versionCombo.SelectedIndex = _settings?.UseOpenCodeV2 == true ? 1 : 0;
            ApplyVersionLock();
            webZoom.Value = _settings?.WebZoomPercent ?? 100;
            autoOpenCheck.IsChecked = _settings?.AutoOpenOnSolutionOpen ?? false;
            consoleButtonCheck.IsChecked = _settings?.ShowConsoleButton ?? true;

            SelectFont(_settings?.TerminalFontFamily ?? "Consolas");
            fontSize.Value = _settings?.TerminalFontSize ?? 13;
            cursorBlinkCheck.IsChecked = _settings?.TerminalCursorBlink ?? true;
            scrollbackBox.Text = (_settings?.TerminalScrollback ?? 5000).ToString();
            cliArgsBox.Text = _settings?.CliArguments ?? "";
            startDirCombo.SelectedIndex = (int)(_settings?.StartDirectory ?? StartDirectoryMode.GitRoot);

            openCodePathBox.Text = _settings?.OpenCodePath ?? "";
            portBox.Text = (_settings?.PreferredPort ?? 4096).ToString();
            serveArgsBox.Text = _settings?.ServeArguments ?? "";
            idleBox.Text = (_settings?.IdleStopMinutes ?? 0).ToString();

            checkUpdatesCheck.IsChecked = _settings?.CheckForUpdates ?? true;
            updateMinutesBox.Text = (_settings?.UpdateCheckMinutes ?? 60).ToString();
            channelCombo.SelectedIndex = (int)(_settings?.Channel ?? UpdateChannel.Stable);
            ratingPopupCheck.IsChecked = _settings?.ShowRatingPopup ?? true;
        }

        private void SaveSettingsToStorage()
        {
            if (_settings == null) return;

            _settings.InterfaceMode = interfaceCombo.SelectedIndex == 1 ? InterfaceMode.CLI : InterfaceMode.Web;
            _settings.UseOpenCodeV2 = versionCombo.SelectedIndex == 1;
            _settings.WebZoomPercent = Clamp((int)webZoom.Value, 50, 200);
            _settings.AutoOpenOnSolutionOpen = autoOpenCheck.IsChecked == true;
            _settings.ShowConsoleButton = consoleButtonCheck.IsChecked == true;
            _settings.Language = _lang;

            _settings.TerminalFontFamily = (fontCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "Consolas";
            _settings.TerminalFontSize = Clamp((int)fontSize.Value, 8, 28);
            _settings.TerminalCursorBlink = cursorBlinkCheck.IsChecked == true;
            _settings.TerminalScrollback = ParseInt(scrollbackBox.Text, 5000, 500, 50000);
            _settings.CliArguments = cliArgsBox.Text ?? "";
            _settings.StartDirectory = (StartDirectoryMode)Math.Max(0, startDirCombo.SelectedIndex);

            _settings.OpenCodePath = openCodePathBox.Text?.Trim() ?? "";
            _settings.PreferredPort = ParseInt(portBox.Text, 4096, 1, 65535);
            _settings.ServeArguments = serveArgsBox.Text ?? "";
            _settings.IdleStopMinutes = ParseInt(idleBox.Text, 0, 0, 100000);

            _settings.CheckForUpdates = checkUpdatesCheck.IsChecked == true;
            _settings.UpdateCheckMinutes = ParseInt(updateMinutesBox.Text, 60, 5, 1440);
            _settings.Channel = (UpdateChannel)Math.Max(0, channelCombo.SelectedIndex);
            _settings.ShowRatingPopup = ratingPopupCheck.IsChecked == true;

            _settings.SaveSettingsToStorage();
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static int ParseInt(string text, int fallback, int min, int max)
            => int.TryParse((text ?? "").Trim(), out var v) ? Clamp(v, min, max) : fallback;

        // ---- события UI --------------------------------------------------

        private void OnMenuChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelGeneral == null) return; // до полной инициализации
            int index = menuList.SelectedIndex;
            panelInstall.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            panelGeneral.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelTerminal.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            panelServer.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            panelUpdates.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
            panelImport.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
            if (index == 4) RenderChangelog();
        }

        /// <summary>Кнопка «Оставить отзыв»: открывает форму отзыва в браузере.</summary>
        private void OnLeaveReviewClick(object sender, RoutedEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Brand.ReviewUrl) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("Open review link failed", ex); }
        }

        /// <summary>
        /// Рендерит changelog во вкладке Updates: разбирает встроенный
        /// CHANGELOG.md и строит элементы с inline **жирным**.
        /// </summary>
        private void RenderChangelog()
        {
            if (changelogPanel == null) return;
            changelogPanel.Children.Clear();

            foreach (var block in ChangelogService.Parse(ChangelogService.ReadAll()))
            {
                switch (block.Kind)
                {
                    case ChangelogBlockKind.Title:
                        changelogPanel.Children.Add(Text(block.Text, 16, FontWeights.Bold, TextBrush, new Thickness(0, 0, 0, 10)));
                        break;
                    case ChangelogBlockKind.Version:
                        changelogPanel.Children.Add(new Border
                        {
                            Height = 1,
                            Background = Brush(0x3a, 0x3a, 0x3a),
                            Margin = new Thickness(0, 6, 0, 0)
                        });
                        changelogPanel.Children.Add(Text(block.Text, 14, FontWeights.SemiBold,
                            Brush(0x5E, 0xAC, 0xFC), new Thickness(0, 16, 0, 6)));
                        break;
                    case ChangelogBlockKind.Category:
                        changelogPanel.Children.Add(Text(block.Text, 12, FontWeights.SemiBold,
                            Brush(0xcf, 0xcf, 0xcf), new Thickness(0, 8, 0, 2)));
                        break;
                    case ChangelogBlockKind.Bullet:
                        changelogPanel.Children.Add(Text("•  " + block.Text, 12, FontWeights.Normal,
                            MutedBrush, new Thickness(8, 2, 0, 2)));
                        break;
                    default:
                        changelogPanel.Children.Add(Text(block.Text, 12, FontWeights.Normal,
                            MutedBrush, new Thickness(0, 2, 0, 2)));
                        break;
                }
            }
        }

        /// <summary>TextBlock с поддержкой inline **жирного** (чётные Run — обычные, нечётные — жирные).</summary>
        private static TextBlock Text(string text, double fontSize, FontWeight weight, Brush brush, Thickness margin)
        {
            var tb = new TextBlock
            {
                FontSize = fontSize,
                FontWeight = weight,
                Foreground = brush,
                Margin = margin,
                TextWrapping = TextWrapping.Wrap
            };
            var parts = (text ?? "").Split(new[] { "**" }, StringSplitOptions.None);
            for (var i = 0; i < parts.Length; i++)
                tb.Inlines.Add(new Run(parts[i]) { FontWeight = i % 2 == 1 ? FontWeights.Bold : weight });
            return tb;
        }

        private void OnWebZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (webZoomText != null) webZoomText.Text = ((int)e.NewValue) + "%";
        }

        private void OnFontSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (fontSizeText != null) fontSizeText.Text = ((int)e.NewValue).ToString();
        }

        private void OnFontChanged(object sender, RoutedEventArgs e) { }

        private void OnInterfaceChanged(object sender, SelectionChangedEventArgs e) => ApplyVersionLock();

        /// <summary>
        /// Доступность выбора версии зависит только от установленных версий;
        /// v2 разрешена и в Desktop (Web), и в CLI.
        /// </summary>
        private void ApplyVersionLock()
        {
            if (interfaceCombo == null || versionCombo == null) return;
            versionCombo.IsEnabled = _v1Available || _v2Available;
        }

        /// <summary>Выбирает шрифт в списке; если его там нет — добавляет.</summary>
        private void SelectFont(string family)
        {
            if (string.IsNullOrWhiteSpace(family)) family = "Consolas";
            ComboBoxItem match = null;
            foreach (var item in fontCombo.Items)
            {
                if (item is ComboBoxItem ci && string.Equals(ci.Content as string, family, StringComparison.OrdinalIgnoreCase))
                { match = ci; break; }
            }
            if (match == null)
            {
                match = new ComboBoxItem { Content = family };
                fontCombo.Items.Add(match);
            }
            fontCombo.SelectedItem = match;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsToStorage();
                _settings?.NotifyApplied();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save settings", ex);
            }

            Onboarding.MarkCompleted();
            _onFinished?.Invoke();
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Onboarding.MarkCompleted();
            _onFinished?.Invoke();
            Close();
        }

        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select the opencode binary",
                Filter = "Executables (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == true)
                openCodePathBox.Text = dialog.FileName;
        }

        /// <summary>Открывает внешние ссылки вкладки Install в браузере.</summary>
        private void OnHyperlink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
            e.Handled = true;
        }

        /// <summary>Кнопка Install v1: ставит opencode-ai@latest и обновляет статус.</summary>
        private async void OnInstallOpenCodeClick(object sender, RoutedEventArgs e)
            => await InstallCliAsync("opencode-ai@latest");

        /// <summary>Кнопка Install v2: ставит @opencode/cli и обновляет статус.</summary>
        private async void OnInstallV2Click(object sender, RoutedEventArgs e)
            => await InstallCliAsync("@opencode/cli");

        /// <summary>Ставит указанный npm-пакет, сбрасывает кэши и обновляет статус.</summary>
        private async Task InstallCliAsync(string packageSpec)
        {
            // На время установки блокируем кнопки установки и ссылки; Cancel доступен.
            SetInstallBusy(true);
            _lastNpmLine = null;
            StartInstallTimer();

            _installCts = new System.Threading.CancellationTokenSource();
            try
            {
                // Просим расширение остановить свои процессы и службу opencode,
                // чтобы npm мог перезаписать shim-файлы в %APPDATA%\npm.
                Services.SpendSettings.RaiseCliInstallStarting();
                await Task.Delay(2000);

                var log = new List<string>();
                var ok = await new OpenCodeUpdateService().InstallAsync(packageSpec, line =>
                {
                    lock (log) { log.Add(line); if (log.Count > 200) log.RemoveAt(0); }
                    // Живой вывод: последняя строка npm на UI-потоке.
                    Dispatcher.Invoke(() =>
                    {
                        _lastNpmLine = line;
                        UpdateInstallStatus();
                    });
                }, _installCts.Token);

                StopInstallTimer();
                if (_installCts.IsCancellationRequested)
                {
                    Log.Info("OpenCode install (" + packageSpec + ") cancelled");
                    if (installResult != null) installResult.Text = Loc("install.cancelled");
                    return;
                }

                CliCommand.ResetVersionCache();
                OpenCodeServerService.ResetWebSupportCache();
                await DetectInstalledVersionsAsync();
                await RefreshInstallStatusAsync();
                Log.Info("OpenCode install (" + packageSpec + ") " + (ok ? "succeeded" : "failed"));
                if (installResult != null)
                    installResult.Text = ok
                        ? Loc("install.done")
                        : Loc("install.failed") + "\n" + string.Join("\n",
                            log.Skip(Math.Max(0, log.Count - 10)));
            }
            catch (Exception ex)
            {
                Log.Error("OpenCode install (" + packageSpec + ") failed", ex);
                if (installResult != null) installResult.Text = Loc("install.failed") + " " + ex.Message;
            }
            finally
            {
                StopInstallTimer();
                SetInstallBusy(false);
                _installCts?.Dispose();
                _installCts = null;
            }
        }

        /// <summary>Запускает таймер прошедшего времени установки (тик 1 c).</summary>
        private void StartInstallTimer()
        {
            _installStartedAt = DateTime.Now;
            if (_installTimer == null)
            {
                _installTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _installTimer.Tick += (_, __) => UpdateInstallStatus();
            }
            _installTimer.Start();
            UpdateInstallStatus();
        }

        /// <summary>Останавливает таймер по завершении/ошибке/отмене.</summary>
        private void StopInstallTimer() => _installTimer?.Stop();

        /// <summary>Показывает прошедшее время хода установки и последнюю строку npm.</summary>
        private void UpdateInstallStatus()
        {
            if (installResult == null) return;
            var elapsed = DateTime.Now - _installStartedAt;
            var time = (int)elapsed.TotalMinutes + ":" + elapsed.Seconds.ToString("00");
            var text = Loc("install.running") + " " + time;
            if (!string.IsNullOrEmpty(_lastNpmLine)) text += "\n" + "npm: " + _lastNpmLine;
            installResult.Text = text;
        }

        /// <summary>Кнопка Cancel: отменяет текущую установку и убивает дерево процессов.</summary>
        private void OnInstallCancelClick(object sender, RoutedEventArgs e)
        {
            StopInstallTimer();
            if (installResult != null) installResult.Text = Loc("install.cancelled");
            _installCts?.Cancel();
        }

        /// <summary>Переключает вкладку Install между рабочим и свободным состоянием.</summary>
        private void SetInstallBusy(bool busy)
        {
            if (installButton != null) installButton.IsEnabled = !busy;
            if (installV2Button != null) installV2Button.IsEnabled = !busy;
            if (installSiteLink != null) installSiteLink.IsEnabled = !busy;
            if (installDocsLink != null) installDocsLink.IsEnabled = !busy;
            if (installProgress != null)
                installProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (installCancelButton != null)
                installCancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---- Import ------------------------------------------------------

        private void RebuildImport()
        {
            sourcesPanel.Children.Clear();
            _checks.Clear();
            statusText.Text = "";

            var sources = _service.DetectSources(_customPath, _solutionDir);
            if (sources.Count == 0)
            {
                sourcesPanel.Children.Add(new TextBlock
                {
                    Text = "No OpenCode data was found on this machine yet. " +
                           "You can start using " + Brand.Name + " right away - settings will be created automatically.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 4, 0, 4)
                });
                importButton.IsEnabled = true;
                return;
            }

            importButton.IsEnabled = true;
            if (importWarning != null)
                importWarning.Visibility = ImportService.IsOpenCodeRunning()
                    ? Visibility.Visible : Visibility.Collapsed;
            foreach (var source in sources)
                sourcesPanel.Children.Add(BuildSourceCard(source));
        }

        private UIElement BuildSourceCard(ImportSource source)
        {
            var card = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock
            {
                Text = source.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush
            });
            if (source.IsActive)
            {
                header.Children.Add(new Border
                {
                    Margin = new Thickness(8, 0, 0, 0),
                    Padding = new Thickness(6, 1, 6, 1),
                    CornerRadius = new CornerRadius(3),
                    Background = AccentBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = "in use", FontSize = 10, Foreground = Brushes.White }
                });
            }
            card.Children.Add(header);

            card.Children.Add(new TextBlock
            {
                Text = source.Summary,
                FontSize = 11,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            foreach (var item in source.Items)
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var check = new CheckBox
                {
                    Content = item.Label,
                    IsChecked = item.Selected,
                    IsEnabled = item.Importable,
                    Foreground = item.Found ? TextBrush : MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (item.Importable) _checks[item] = check;
                Grid.SetColumn(check, 0);
                row.Children.Add(check);

                var detail = new TextBlock
                {
                    Text = item.Detail,
                    FontSize = 11,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0),
                    MaxWidth = 320,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                if (source.IsActive) detail.Text = item.Found ? item.Detail + "  (already active)" : item.Detail;
                Grid.SetColumn(detail, 1);
                row.Children.Add(detail);

                card.Children.Add(row);
            }

            return card;
        }

        private void OnBrowseImportClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select a folder that contains OpenCode data (config, auth.json, opencode.db...)";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _customPath = dialog.SelectedPath;
                    RebuildImport();
                }
            }
        }

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            var selected = _checks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            if (selected.Count == 0)
            {
                statusText.Text = "Nothing selected.";
                return;
            }

            importButton.IsEnabled = false;
            try
            {
                var result = _service.Import(null, selected);

                var msg = $"Imported {result.Copied.Count} item(s).";
                if (result.Backups.Count > 0) msg += $" {result.Backups.Count} backup(s) created.";
                if (result.Errors.Count > 0)
                {
                    msg += $" {result.Errors.Count} error(s): " + string.Join("; ", result.Errors.Take(3));
                    Log.Warn("Import errors: " + string.Join(" | ", result.Errors));
                }
                Log.Info("Settings import completed. " + msg);
                statusText.Text = msg;
            }
            catch (Exception ex)
            {
                Log.Error("Import failed", ex);
                statusText.Text = "Import failed: " + ex.Message;
            }
            finally
            {
                importButton.IsEnabled = true;
            }
        }
    }
}
