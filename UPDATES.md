# Updates / Обновления

**[🇬🇧 English](#-english) · [🇷🇺 Русский](#-русский)**

The full update history of **OpenCode Jesk** (formerly OpenCode Studio), kept in the project as a changelog.
Полная история обновлений **OpenCode Jesk** (ранее OpenCode Studio), хранится в проекте как лог изменений.

---

## 🇬🇧 English

All notable changes to **OpenCode Jesk** are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

### [2.0.0.0] - 2026-10-15

#### Added

**Quick Start (first tab, opens on first run)**

A step-by-step guide on a single page, so a new user can go from zero to a running agent without leaving the Settings window:

1. **Install** — a single **Install All** button installs **v2 first, then v1**, so both versions of the OpenCode CLI are available side by side (`opencode` = v1, `opencode2` = v2) and either can be used.
   - A **live npm log** streams the raw installer output while the install runs.
   - A **non-linear progress bar** driven by the real output shows the current **percentage** and the **elapsed time**.
   - A sweeping **shimmer** highlight runs across the bar, with rounded corners.
   - A **Cancel** button aborts a running install.
   - On success the bar turns **green** with the label *Download complete*.
2. **Version** — choose the CLI version to use: **v1** (stable) or **v2** (preview).
3. **Interface** — pick the mode, **Desktop (Web)** or **CLI**, and open the panel with a single button.
4. **Overview** — an at-a-glance map of where the extension, the data directory, the server and the logs live.

**Sponsorship / Contacts**

- A thank-you letter for supporting the project.
- Contacts:
  - **Telegram**
  - **Email**
  - **Boosty**
  - **GitHub**
- Crypto wallets for **TON, BTC and ETH**:
  - Each wallet shows a **QR code**.
  - Wallets are switched through **sub-tabs**.
  - Each has an address field and a **Copy** button.

**Updates**

- A **leave-a-review** card linking to the Marketplace review page.
- A **rating popup**:
  - Shown **once per session**.
  - Appears after roughly **two hours** of use.
  - Can be turned off with a toggle in Settings.
- A detailed **"What's new" changelog** (the bundled `CHANGELOG.md`, parsed and rendered) **instead of the raw installation log**.

**UI and brand**

- A **blue palette matched to the extension icon** — gradients and soft glows, used consistently across the panel and the Settings window.
- The **extension icon** in the Visual Studio menu and in the Settings window title.
- **Always-dark windows**, with restyled tabs and menus.

#### Changed

- **Full rebrand to OpenCode Jesk.** The product is a single brand; the old **OpenCode Studio** name has been **removed entirely**. Renamed:
  - the **namespace**,
  - the **assembly**,
  - the package **class**,
  - the **VSCT** command tables,
  - the **icons**.
  - The data directory is now `%LOCALAPPDATA%\OpenCodeJesk` (with **migration** from the old `%LOCALAPPDATA%\OpenCodeStudio`).
  - The environment variable is `OPENCODEJESK_OPENCODE_BIN`.
  - The pack URIs use the new name.
  - The project builds the single Jesk brand.
- **Version bumped to 2.0.0.0.**
- **Internal refactor** — the "god" classes were split into focused services: `CliInstallService`, `Localization`, `CliVersionStatusService`, `CliBinaryLocator`, `ServerPortResolver`, `SharedServerRegistry`, `WebPageRenderer`, `SessionUrlParser`, `ConsoleCoordinator`, `ReconnectCoordinator`, `CliModeCoordinator`, `SettingsEditor`, `ImportPresenter`, `InstallStatusRenderer` and `UiPalette`. Dead code was removed, comments were translated to Russian, and the sources were organized into folders.

#### Fixed

- **v1 ↔ v2 switching in the web panel** — the WebView2 cache is cleared whenever the version changes, so the panel no longer shows a stale page.
- **Version selection at start** — the restart/adopt decision now compares by **major** version.
- **CLI crash when switching versions** — "Session not found" is handled by validating the session and creating it if missing before the terminal opens.
- **npm installation** — runs with `--force` and stops the extension's own processes first, so locked shims no longer block the install.
- **Bunny rounding** — the running "bunny" no longer overflows the progress bar's edges.
- **Hover styles** — the bright blue highlight was removed; the tabs and the Sponsorship item now follow the side-menu style.

#### Docs

- Updated **README (EN/RU)** and the **Marketplace description** with the **blue** style and new **collages** (a preview of all panel versions and a collage of all Settings tabs).
- Reworked the README structure.

### [1.2.0.32] - 2026-10-15

#### Added
- Interface choice in Settings only: **Desktop (Web)** shows the Web panel; **CLI** shows a plain terminal with OpenCode already running, inside the same OpenCode Studio panel (no Visual Studio terminal chrome). The separate v1 / v2 preview switch applies to both interfaces.
- Web and Terminal connect to the same authenticated server for the selected OpenCode version. Switching from Web to CLI can resume the selected chat. No database copying or cross-version conversion is performed.
- The terminal lives in the OpenCode Studio panel, so Visual Studio keeps its docked position and size, and the CLI is started automatically (v1 or v2) in the current project.
- New-chat view defaults to the current Visual Studio solution/repository directory. Other projects remain selectable in OpenCode.
- **Auto-connect for the v2 web panel.** The panel pairs through the local server (`/api/pair` → `/auth/connect/<code>`) before WebView2 navigates, so it opens already signed in and the "Connect to a server" screen is never shown.
- **Updates tab: review card and rating popup.** A short review invite links to the Marketplace review page, and a one-time popup asks for a rating once per session, after about two hours of use. A new setting (**Show the review reminder**) turns the popup off.
- **Updates tab shows the detailed changelog** (parsed from the bundled `CHANGELOG.md`) instead of the raw installation log.
- **Extension icon in the Visual Studio menu and in the Settings window title**, making the extension easier to identify.
- **Install tab with one-click v1/v2 CLI install**: progress and elapsed time, a Cancel button, a live install log, and a note about installing both versions side by side. Installed `opencode` / web / desktop presence is detected, and the OpenCode version is read via `--version` (so a v2 that installs as `opencode` is recognized).
- **v2 support in the embedded Web mode** (the earlier "v2 is CLI-only" restriction was removed).

#### Fixed
- **Save selected** now applies settings even when no import items are selected.
- Changing only the interface mode does not restart the shared OpenCode server.
- Session lookup uses the v2 API when the preview binary is selected.
- **Version switch in the web panel.** The WebView2 cache is now cleared on every version switch (previously once per session), so the panel no longer shows a stale page after switching v1 ↔ v2; a migration stamp file was added.
- **Version selection at start.** The restart/adopt decision now compares by **major** version, and the v2 server output is logged.
- **CLI crash when switching versions** ("Session not found"): the session is validated and, if missing, created before the terminal opens.
- **CLI installation through npm** could hang or fail on locked shims. Install now stops the extension's own OpenCode server/CLI and the `opencode` service first, runs `npm` with `--force`, cleans stale directories, closes the child's stdin, kills the npm process tree on timeout, and shows a live log with a Cancel button.
- Removed the hanging `opencode service stop` that kept `opencode.exe` open and caused EPERM during install.
- Fixed the CLI `--dir` error and the web "Unrecognised route!" error.
- OpenCode detection runs off the UI thread and initialization is guarded against exceptions.

#### Changed
- **Blue accent palette matched to the icon** (gradient); the extension windows are always dark, with tidied spacing.
- **Dead code removed** (`HealthInfo`, `CreateSessionRequest`, the `UsageWindow`/`StatsService` chain, unused members) and the install log (`UpdateLog`) dropped.
- **Internal refactor: services extracted** for clarity and testing — `CliInstallService`, `Localization`, `CliVersionStatusService`, `CliBinaryLocator`, `ServerPortResolver`, `SharedServerRegistry`, `WebPageRenderer`, `SessionUrlParser`, `ConsoleCoordinator`, `ReconnectCoordinator`, `CliModeCoordinator`, `SettingsEditor`, `ImportPresenter`, `InstallStatusRenderer` and `UiPalette`.
- Brand-aware Settings window titles.

### [1.2.0] - 2026-10-14

#### Added
- **Interface modes: Desktop (Web) and CLI (terminal)** in the same tool window. Desktop keeps the WebView2 integration; CLI shows a plain terminal with OpenCode already running inside the same panel (no Visual Studio terminal chrome), started automatically in the current project directory.
- **Chat syncing between modes** — Web and CLI connect to the same authenticated server, and a chat selected in Web can be continued in CLI. History sharing stays within the selected OpenCode version (no automatic v1/v2 database conversion).
- **Dedicated Settings window** with a section menu: **General**, **Terminal**, **Server**, **Updates**, **Import** (replaces the first-run wizard tabs).
- **Auto-open on solution open** — the panel opens automatically when a solution is opened (toggle in General).
- **Web zoom** (50–200%) for the Desktop panel.
- **Terminal options** — font family, font size, cursor blink and scrollback lines.
- **Server options** — opencode binary path, preferred port, extra serve arguments and idle stop.
- **Update options** — check for new versions, check interval and release channel.
- **opencode v1 / v2 (preview)** support (v2 is available in CLI mode).
- **Safe import** — existing data is backed up before anything is written.
- **Renamed to OpenCode Jesk** (from OpenCode Studio) — a new VSIX identity and brand with its own icon; OpenCode Studio support ends on 20.10.2026.

#### Changed
- Version bumped to **1.2.0**.
- One codebase now builds a single brand, **OpenCode Jesk**, with a single-brand build.

### [1.1.5.1] - 2026-10-01

#### Changed (minor)
- Minor maintenance hotfix: more reliable **Open Extension Manager** action (opens the Visual Studio extension manager; tries the Updates page first, then several fallbacks) and the update window stays hidden until the next Visual Studio restart after it is closed with the X button.
- Any button in the update window now also hides it until the next Visual Studio restart (instead of only "Not now"), and the window explains how to turn the notifications off in settings.

### [1.1.5] - 2026-09-30

#### Fixed
- Closing the update window with the X button now suppresses it until the next Visual Studio restart (same as "Not now").
- "Open Extension Manager" now reliably opens Visual Studio's Extension Manager (several known command names plus a DTE command search); the Marketplace page is only a fallback.

### [1.1.4] - 2026-09-30

#### Added
- Background check for a newer OpenCode Studio release on the Visual Studio Marketplace (once a minute). When an update is available, a compact dark window shows the new version and its release notes with **Open Extension Manager** / **Marketplace page** / **Not now** buttons (Visual Studio itself installs updates in the background; "Not now" hides the window until the next restart).
- OpenCode itself is now checked for updates too (once a minute). If a newer CLI version is available, a compact window offers to update it right from the extension (`npm i -g opencode-ai@latest`) or shows the manual command; the panel picks up the new version on restart.
- New setting **Check for new versions** (Tools -> Options -> OpenCode Studio, and the Settings window). When disabled, the extension no longer shows update notifications.

### [1.1.3] - 2026-09-30

#### Added
- The Settings window now shows the installed and the latest available OpenCode version, and offers an **Update** button (runs `npm i -g opencode-ai@latest`) when a newer version exists. Combined with the existing start-time check, a new OpenCode version is picked up automatically.

### [1.1.2] - 2026-09-24

#### Changed
- **Always uses the latest installed opencode.** On start the server re-checks the binary (its path and modification time) and restarts if it was updated (or the v1/v2 mode changed), so a fresh start automatically picks up the latest installed version.
- The real PID of the server process is now written to `server.json`.
- The import wizard is now a plain **Settings** window with two tabs (**Import** and **Settings**); the welcome texts were removed.
- The first-run wizard button is now **Save selected** (it applies your choices, including settings, not only imports).
- Removed the "OpenCode Studio Usage Statistics" menu item and its window for now (statistics will return later; a button in the panel opens the OpenCode Console in the meantime).

### [1.1.1] - 2026-09-24

#### Added
- **OpenCode v1 and v2 support.** The extension now secures the embedded server with its own password and sends Basic Auth on every request (and to WebView2), so it works both with stable opencode v1 and with opencode v2 / the v2 beta, which protect the server after the CVE-2026-22812 fix. The misleading "not in PATH" error is gone.
- Detect the OpenCode 2 beta binary (`opencode2`) in addition to `opencode`, plus a native **Use OpenCode v2.x (preview)** switch in the settings window and in Tools -> Options -> OpenCode Studio. Changing it restarts the server with the selected binary. `OPENCODESTUDIO_OPENCODE_BIN` can force a binary path.
- **Visual Studio 2022 and 2026 support** — one VSIX for both.

#### Fixed
- **OpenCode v2 (beta) reconnect loop.** The v2 server answers every path with its web app, so the strict `{"healthy":true}` health check always failed and the extension kept restarting the server ("connection lost - reconnecting"). Health now only requires a successful HTTP response, which works for both v1 and v2.
- Server processes are now killed **with their child tree** (`taskkill /T`). Previously a `.cmd` shim left the Node process alive, so it kept holding the port and every restart picked a new random port.
- Readiness no longer depends on the server's exact startup log line.

### [1.1.0] - 2026-09-22

#### Added
- **Statistics button in the OpenCode panel.** It sits just left of the context button in the prompt footer, and on opencode.ai pages it docks to the corner. One click opens your OpenCode profile and console right inside the tool window - check real usage and subscription limits without leaving Visual Studio, and click again to return to your chats.
- **Sign-in to the OpenCode Console** inside the embedded browser (Google, GitHub or email). The session cookie is captured after the return navigation and stored encrypted with DPAPI.
- **Tools -> Options -> OpenCode Studio -> Usage & Limits** settings page, plus a checkbox in the first-run wizard.
- Reworked bilingual documentation (English and Russian) and a Marketplace description with a preview.

#### Fixed
- **Stable server port** - the preferred port, then the last used one, then a free one. The embedded UI keeps a constant origin, so local settings, theme and chat history no longer reset between restarts.
- Sign-in reliably returns to the app after authentication and restarts the monitor.
- The injected button no longer triggers the context tooltip.

#### Notes
- The built-in usage dashboard (remaining Go limits and spend charts inside the extension) is in progress - see the roadmap issue on GitHub.

### [1.0.10] - 2026-09-20

#### Added
- Reworked bilingual documentation (English and Russian) with a preview screenshot, feature overview and contacts (Telegram, email, Boosty).
- A dedicated **Syncing chat history** guide: chats are grouped by project path, so matching the original project paths makes the full history appear everywhere.
- Marketplace description with a preview section.

### [1.0.9] - 2026-09-20

#### Changed
- The tool window now opens the OpenCode web UI **home**, so all projects and every chat are visible - exactly like the OpenCode desktop app.
- The server uses a **stable port** (4096 when free), keeping the WebView2 origin constant so the web UI remembers its state between restarts.
- The extension no longer creates an empty session on every project change.

### [1.0.8] - 2026-09-20

#### Fixed
- **Chat history now loads in the tool window.** The navigation URL now encodes the project directory exactly as OpenCode reports it (a Windows path with backslashes); previously a normalized forward-slash path produced a different base64 and the web UI could not resolve the project.
- API responses are now decoded as UTF-8 explicitly, so non-ASCII (e.g. Cyrillic) project paths match correctly on .NET Framework.

### [1.0.7] - 2026-09-20

#### Fixed
- **Chat history now syncs** with the OpenCode CLI and desktop app: OpenCode Studio uses the shared default OpenCode environment instead of an isolated one.
- **Multiple Visual Studio windows share one OpenCode server** (reused via a registry file), so sessions and live agent state stay in sync between windows.
- **History no longer appears to reset** when a solution is opened or closed: the last project root is remembered as a stable fallback.

#### Changed
- The import wizard now targets the shared environment and is intended for importing from external folders/backups.

### [1.0.6] - 2026-09-20

#### Added
- New **OpenCode Studio** brand and icon set (light and dark variants).
- Command, tool window and menu icons.
- First-run **import wizard** with multi-source detection (CLI, Desktop, custom folder).
- **Isolated OpenCode environment** under `%LOCALAPPDATA%\OpenCodeStudio`.
- File logging to `%LOCALAPPDATA%\OpenCodeStudio\logs\opencode-studio.log`.
- `git`-agnostic CI workflow for building the VSIX.

#### Changed
- **Re-architected**: WebView2 now talks directly to the OpenCode server origin instead of a `*.vsoc-app` proxy.
- Removed the COM host bridge, request proxy and injected scripts.
- Tool window caption is now "OpenCode Studio".
- Server no longer shuts down after being idle.

#### Fixed
- Server URL detection no longer aborts on the first read (the `ReadLineAsync` race).
- Connection drops are recovered automatically with a progress page and retries.
- Prefers the `.exe` OpenCode executable; runs `.cmd`/`.bat` shims via `cmd.exe`.

### [1.0.0] - 2026-09-19

#### Added
- Initial public release: OpenCode web UI embedded in a Visual Studio tool window.

**Releases:** https://github.com/Jevkray/OpenCodeJesk/releases

---

## 🇷🇺 Русский

Все значимые изменения **OpenCode Jesk** документируются здесь. Формат основан на [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

### [2.0.0.0] - 2026-10-15

#### Добавлено

**Quick Start (первая вкладка, открывается при первом запуске)**

Пошаговый гайд на одной странице, чтобы новый пользователь мог пройти путь от нуля до работающего агента, не покидая окно настроек:

1. **Install** — одна кнопка **Install All** ставит **сначала v2, затем v1**, поэтому обе версии OpenCode CLI доступны рядом (`opencode` = v1, `opencode2` = v2) и можно пользоваться любой.
   - **Живой лог npm** в реальном времени показывает вывод установщика.
   - **Нелинейный прогресс-бар**, управляемый реальным выводом, показывает текущий **процент** и **затраченное время**.
   - По полосе бежит **блик**, края скруглены.
   - Кнопка **Cancel** прерывает выполняющуюся установку.
   - При успехе полоса становится **зелёной** с подписью *Загрузка завершена*.
2. **Version** — выбор версии CLI: **v1** (стабильная) или **v2** (preview).
3. **Interface** — режим **Desktop (Web)** или **CLI** и кнопка открытия панели.
4. **Overview** — обзорная схема: где лежат расширение, каталог данных, сервер и логи.

**Sponsorship / Contacts**

- Благодарственное письмо за поддержку проекта.
- Контакты:
  - **Telegram**
  - **Email**
  - **Boosty**
  - **GitHub**
- Крипто-кошельки **TON, BTC и ETH**:
  - У каждого кошелька — **QR-код**.
  - Кошельки переключаются через **подвкладки**.
  - У каждого есть поле адреса и кнопка **Copy**.

**Updates**

- Блок **оставить отзыв** со ссылкой на страницу отзывов Marketplace.
- **Всплывающее окно оценки**:
  - Показывается **раз за сессию**.
  - Появляется примерно после **двух часов** работы.
  - Отключается переключателем в настройках.
- Подробный журнал **«What's new»** (встроенный `CHANGELOG.md`, распарсенный и отрендеренный) **вместо сырого журнала установки**.

**UI и бренд**

- **Синяя палитра в цвет иконки расширения** — градиенты и мягкие свечения, единообразно в панели и окне настроек.
- **Иконка расширения** в меню Visual Studio и в заголовке окна настроек.
- **Всегда тёмные окна** с обновлёнными вкладками и меню.

#### Изменено

- **Полный ребрендинг в OpenCode Jesk.** Продукт — единый бренд; старое имя **OpenCode Studio** **полностью удалено**. Переименованы:
  - **namespace**,
  - **сборка**,
  - класс **пакета**,
  - таблицы команд **VSCT**,
  - **иконки**.
  - Каталог данных теперь `%LOCALAPPDATA%\OpenCodeJesk` (с **миграцией** из старого `%LOCALAPPDATA%\OpenCodeStudio`).
  - Переменная окружения — `OPENCODEJESK_OPENCODE_BIN`.
  - pack-URI используют новое имя.
  - Проект собирает единый бренд Jesk.
- **Версия поднята до 2.0.0.0.**
- **Внутренний рефакторинг** — «god»-классы разбиты на сфокусированные сервисы: `CliInstallService`, `Localization`, `CliVersionStatusService`, `CliBinaryLocator`, `ServerPortResolver`, `SharedServerRegistry`, `WebPageRenderer`, `SessionUrlParser`, `ConsoleCoordinator`, `ReconnectCoordinator`, `CliModeCoordinator`, `SettingsEditor`, `ImportPresenter`, `InstallStatusRenderer` и `UiPalette`. Удалён мёртвый код, комментарии переведены на русский, исходники разложены по папкам.

#### Исправлено

- **Переключение v1 ↔ v2 в веб-панели** — кэш WebView2 очищается при каждой смене версии, панель больше не показывает устаревшую страницу.
- **Выбор версии при старте** — решение о перезапуске/принятии сервера теперь сравнивает по **major**-версии.
- **Падение CLI при смене версии** — «Session not found» обрабатывается валидацией сессии и её созданием при отсутствии до открытия терминала.
- **Установка npm** — запускается с `--force` и сначала останавливает собственные процессы расширения, поэтому заблокированные shim-файлы больше не мешают установке.
- **Скругление «зайчика»** — бегущий «зайчик» больше не вылезает за края прогресс-бара.
- **Hover-стили** — убран ярко-синий хайлайт; вкладки и пункт Sponsorship теперь следуют стилю бокового меню.

#### Документация

- Обновлены **README (EN/RU)** и **описание Marketplace** в **синем** стиле с новыми **коллажами** (превью всех версий панели и коллаж всех вкладок настроек).
- Переработана структура README.

### [1.2.0.32] - 2026-10-15

#### Добавлено
- Выбор интерфейса только в настройках: **Desktop (Web)** показывает веб-панель; **CLI** показывает простое окно терминала с уже запущенным OpenCode внутри той же панели OpenCode Studio (без хрома терминала Visual Studio). Отдельный переключатель v1 / v2 preview действует на оба интерфейса.
- Web и терминал подключаются к одному аутентифицированному серверу для выбранной версии OpenCode. Переход из Web в CLI позволяет продолжить выбранный чат. Копирования БД или кросс-версионной конвертации не делается.
- Терминал живёт в панели OpenCode Studio, поэтому Visual Studio сохраняет положение и размер, а CLI запускается автоматически (v1 или v2) в текущем проекте.
- Форма нового чата по умолчанию открывается в каталоге открытого решения/репозитория Visual Studio. Другие проекты остаются доступны в OpenCode.
- **Автоподключение для веб-панели v2.** Панель спаривается через локальный сервер (`/api/pair` → `/auth/connect/<code>`) до перехода WebView2, поэтому открывается уже авторизованной и экран «Connect to a server» не показывается.
- **Вкладка Updates: карточка отзыва и попап оценки.** Короткое приглашение оставить отзыв ведёт на страницу отзывов Marketplace, а одноразовый попап просит оценку раз за сессию, примерно после двух часов работы. Новая настройка (**Show the review reminder**) отключает попап.
- **Вкладка Updates показывает подробный журнал** (распарсенный из встроенного `CHANGELOG.md`) вместо сырого журнала установки.
- **Иконка расширения в меню Visual Studio и в заголовке окна настроек**, чтобы расширение было легче узнать.
- **Вкладка Install с установкой CLI v1/v2 в один клик**: прогресс и затраченное время, кнопка Cancel, живой лог установки и пометка об установке обеих версий рядом. Определяется наличие установленных `opencode` / web / desktop, а версия OpenCode читается через `--version` (поэтому v2, устанавливающаяся как `opencode`, распознаётся).
- **Поддержка v2 во встроенном Web-режиме** (прежнее ограничение «v2 только для CLI» снято).

#### Исправлено
- **Save selected** теперь применяет настройки, даже когда не выбрано ни одного элемента импорта.
- Смена только режима интерфейса не перезапускает общий сервер OpenCode.
- Поиск сессии использует API v2, когда выбран preview-бинарь.
- **Переключение версии в веб-панели.** Кэш WebView2 теперь очищается при каждой смене версии (раньше — раз за сессию), панель больше не показывает устаревшую страницу после переключения v1 ↔ v2; добавлен файл-метка миграции.
- **Выбор версии при старте.** Решение о перезапуске/принятии теперь сравнивает по **major**-версии, а вывод сервера v2 логируется.
- **Падение CLI при смене версий** («Session not found»): сессия валидируется и при отсутствии создаётся до открытия терминала.
- **Установка CLI через npm** могла зависнуть или упасть на заблокированных shim-файлах. Теперь установка сначала останавливает собственные сервер/CLI расширения и сервис `opencode`, запускает `npm` с `--force`, чистит устаревшие каталоги, закрывает stdin дочернего процесса, убивает дерево процессов npm по таймауту и показывает живой лог с кнопкой Cancel.
- Убран зависающий `opencode service stop`, державший `opencode.exe` открытым и вызывавший EPERM при установке.
- Исправлены ошибка CLI `--dir` и веб-ошибка «Unrecognised route!».
- Определение OpenCode выполняется вне UI-потока, инициализация защищена от исключений.

#### Изменено
- **Синяя акцентная палитра в цвет иконки** (градиент); окна расширения всегда тёмные, отступы приведены в порядок.
- **Удалён мёртвый код** (`HealthInfo`, `CreateSessionRequest`, цепочка `UsageWindow`/`StatsService`, неиспользуемые члены) и убран журнал установки (`UpdateLog`).
- **Внутренний рефакторинг: выделены сервисы** для ясности и тестирования — `CliInstallService`, `Localization`, `CliVersionStatusService`, `CliBinaryLocator`, `ServerPortResolver`, `SharedServerRegistry`, `WebPageRenderer`, `SessionUrlParser`, `ConsoleCoordinator`, `ReconnectCoordinator`, `CliModeCoordinator`, `SettingsEditor`, `ImportPresenter`, `InstallStatusRenderer` и `UiPalette`.
- Заголовки окна настроек с учётом бренда.

### [1.2.0] - 2026-10-14

#### Добавлено
- **Режимы интерфейса: Desktop (Web) и CLI (терминал)** в одном окне-инструменте. Desktop сохраняет интеграцию WebView2; CLI показывает простое окно терминала с уже запущенным OpenCode внутри той же панели (без хрома терминала Visual Studio), запускаемым автоматически в каталоге текущего проекта.
- **Синхронизация чатов между режимами** — Web и CLI подключаются к одному аутентифицированному серверу, и чат, выбранный в Web, можно продолжить в CLI. Общая история остаётся в пределах выбранной версии OpenCode (без автоматической конвертации БД v1/v2).
- **Отдельное окно Settings** с меню разделов: **General**, **Terminal**, **Server**, **Updates**, **Import** (заменяет вкладки мастера первого запуска).
- **Авто-открытие при открытии решения** — панель открывается автоматически при открытии решения (переключатель в General).
- **Масштаб веб-панели** (50–200%) для Desktop.
- **Настройки терминала** — шрифт, размер, мигание курсора и число строк истории.
- **Настройки сервера** — путь к бинарю opencode, предпочитаемый порт, дополнительные аргументы serve и остановка по простою.
- **Настройки обновлений** — проверка новых версий, интервал проверки и канал релиза.
- Поддержка **opencode v1 / v2 (preview)** (v2 доступна в CLI-режиме).
- **Безопасный импорт** — перед записью существующие данные резервируются.
- **Переименование в OpenCode Jesk** (из OpenCode Studio) — новая идентичность VSIX и бренд со своей иконкой; поддержка OpenCode Studio завершается 20.10.2026.

#### Изменено
- Версия поднята до **1.2.0**.
- Один код теперь собирает единый бренд, **OpenCode Jesk**, сборкой одного бренда.

### [1.1.5.1] - 2026-10-01

#### Изменено (минорно)
- Мелкий сервисный хотфикс: надёжнее работает **Open Extension Manager** (открывает менеджер расширений Visual Studio; сначала пробует вкладку Updates, затем несколько запасных вариантов), а окно обновления остаётся скрытым до следующего перезапуска Visual Studio после закрытия кнопкой X.
- Любая кнопка в окне обновления теперь тоже скрывает его до следующего перезапуска Visual Studio (а не только «Not now»), и в окне поясняется, как отключить уведомления в настройках.

### [1.1.5] - 2026-09-30

#### Исправлено
- Закрытие окна обновления кнопкой X теперь подавляет его до следующего перезапуска Visual Studio (как и «Not now»).
- «Open Extension Manager» теперь надёжно открывает менеджер расширений Visual Studio (несколько известных имён команд плюс поиск команды через DTE); страница Marketplace — только запасной вариант.

### [1.1.4] - 2026-09-30

#### Добавлено
- Фоновая проверка новой версии OpenCode Studio в Visual Studio Marketplace (раз в минуту). Когда есть обновление, компактное тёмное окно показывает новую версию и её release notes с кнопками **Open Extension Manager** / **Marketplace page** / **Not now** (сам Visual Studio ставит обновления расширений в фоне; «Not now» скрывает окно до следующего перезапуска).
- Сам OpenCode тоже проверяется на обновления (раз в минуту). Если доступна новая версия CLI, компактное окно предлагает обновить её прямо из расширения (`npm i -g opencode-ai@latest`) или показывает ручную команду; панель подхватывает новую версию при перезапуске.
- Новая настройка **Check for new versions** (Сервис -> Параметры -> OpenCode Studio и окно настроек). При отключении расширение больше не показывает уведомления об обновлениях.

### [1.1.3] - 2026-09-30

#### Добавлено
- Окно настроек теперь показывает установленную и последнюю доступную версию OpenCode и предлагает кнопку **Update** (запускает `npm i -g opencode-ai@latest`), когда вышла новая версия. Вместе с проверкой при старте новая версия OpenCode подхватывается автоматически.

### [1.1.2] - 2026-09-24

#### Изменено
- **Всегда используется последняя установленная версия opencode.** При старте сервер заново проверяет бинарь (путь и время изменения) и перезапускается, если он обновился (или сменился режим v1/v2), поэтому свежий запуск автоматически подхватывает последнюю установленную версию.
- Реальный PID серверного процесса теперь пишется в `server.json`.
- Мастер импорта теперь — простое окно **Settings** с двумя вкладками (**Import** и **Settings**); приветственные тексты убраны.
- Кнопка мастера первого запуска теперь **Save selected** (применяет выбор, включая настройки, а не только импорт).
- Убран пункт меню «OpenCode Studio Usage Statistics» и его окно на время (статистика вернётся позже; пока кнопка в панели открывает OpenCode Console).

### [1.1.1] - 2026-09-24

#### Добавлено
- **Поддержка OpenCode v1 и v2.** Расширение теперь защищает встроенный сервер собственным паролем и отправляет Basic Auth в каждом запросе (и в WebView2), поэтому работает и со стабильным opencode v1, и с opencode v2 / бета-версией v2, которые защищают сервер после фикса CVE-2026-22812. Вводящая в заблуждение ошибка «not in PATH» исчезла.
- Определяется бинарь беты OpenCode 2 (`opencode2`) в дополнение к `opencode`, плюс нативный переключатель **Use OpenCode v2.x (preview)** в окне настроек и в Сервис -> Параметры -> OpenCode Studio. Его смена перезапускает сервер с выбранным бинарём. `OPENCODESTUDIO_OPENCODE_BIN` может принудительно задать путь к бинарю.
- **Поддержка Visual Studio 2022 и 2026** — один VSIX для обеих.

#### Исправлено
- **Цикл переподключения OpenCode v2 (beta).** Сервер v2 отвечает своим веб-приложением на любой путь, поэтому строгая проверка здоровья `{"healthy":true}` всегда падала, и расширение постоянно перезапускало сервер («связь потеряна — переподключение»). Теперь проверка здоровья требует лишь успешного HTTP-ответа, что работает и для v1, и для v2.
- Серверные процессы теперь убиваются **вместе с деревом дочерних** (`taskkill /T`). Раньше shim `.cmd` оставлял Node-процесс живым, тот держал порт, и каждый перезапуск брал новый случайный порт.
- Готовность больше не зависит от точной строки лога запуска сервера.

### [1.1.0] - 2026-09-22

#### Добавлено
- **Кнопка статистики в панели OpenCode.** Она находится чуть левее кнопки контекста в футере промпта, а на страницах opencode.ai прикрепляется к углу. Один клик открывает ваш профиль и консоль OpenCode прямо в окне-инструменте — проверяйте реальный расход и лимиты подписки, не покидая Visual Studio, и кликните снова, чтобы вернуться к чатам.
- **Вход в OpenCode Console** во встроенном браузере (Google, GitHub или email). Сессионная cookie захватывается после возвратной навигации и хранится зашифрованной через DPAPI.
- Страница настроек **Сервис -> Параметры -> OpenCode Studio -> Usage & Limits**, плюс галочка в мастере первого запуска.
- Переработана двуязычная документация (английский и русский) и описание Marketplace с превью.

#### Исправлено
- **Стабильный порт сервера** — предпочитаемый порт, затем последний использовавшийся, затем свободный. Встроенный UI сохраняет постоянный origin, поэтому локальные настройки, тема и история чатов больше не сбрасываются между перезапусками.
- Вход надёжно возвращается в приложение после аутентификации и перезапускает монитор.
- Внедрённая кнопка больше не вызывает всплывающую подсказку контекста.

#### Примечания
- Встроенный дашборд использования (остаток лимитов Go и графики расходов внутри расширения) в работе — см. issue дорожной карты на GitHub.

### [1.0.10] - 2026-09-20

#### Добавлено
- Переработана двуязычная документация (английский и русский) со скриншотом-превью, обзором функций и контактами (Telegram, email, Boosty).
- Отдельный гайд **Syncing chat history**: чаты группируются по пути проекта, поэтому совпадение исходных путей делает полную историю видимой везде.
- Описание Marketplace с разделом превью.

### [1.0.9] - 2026-09-20

#### Изменено
- Окно-инструмент теперь открывает **главную** страницу веб-UI OpenCode, поэтому видны все проекты и все чаты — точно как в desktop-приложении OpenCode.
- Сервер использует **стабильный порт** (4096, когда свободен), сохраняя постоянный origin WebView2, чтобы веб-UI помнил своё состояние между перезапусками.
- Расширение больше не создаёт пустую сессию при каждой смене проекта.

### [1.0.8] - 2026-09-20

#### Исправлено
- **История чатов теперь загружается в окне-инструменте.** Навигационный URL теперь кодирует каталог проекта ровно так, как его сообщает OpenCode (Windows-путь с обратными слэшами); раньше нормализованный путь с прямыми слэшами давал другой base64, и веб-UI не мог разрешить проект.
- Ответы API теперь декодируются как UTF-8 явно, поэтому не-ASCII (например, кириллические) пути проектов корректно сопоставляются на .NET Framework.

### [1.0.7] - 2026-09-20

#### Исправлено
- **История чатов теперь синхронизируется** с OpenCode CLI и desktop-приложением: OpenCode Studio использует общее стандартное окружение OpenCode, а не изолированное.
- **Несколько окон Visual Studio используют один сервер OpenCode** (переиспользуется через файл реестра), поэтому сессии и живое состояние агента синхронны между окнами.
- **История больше не выглядит сбрасывающейся** при открытии или закрытии решения: последний корень проекта запоминается как стабильный запасной вариант.

#### Изменено
- Мастер импорта теперь целится в общее окружение и предназначен для импорта из внешних папок/бэкапов.

### [1.0.6] - 2026-09-20

#### Добавлено
- Новый бренд **OpenCode Studio** и набор иконок (светлый и тёмный варианты).
- Иконки команд, окна-инструмента и меню.
- **Мастер импорта** первого запуска с определением нескольких источников (CLI, Desktop, произвольная папка).
- **Изолированное окружение OpenCode** в `%LOCALAPPDATA%\OpenCodeStudio`.
- Файловое логирование в `%LOCALAPPDATA%\OpenCodeStudio\logs\opencode-studio.log`.
- CI-workflow, не зависящий от `git`, для сборки VSIX.

#### Изменено
- **Ре-архитектура**: WebView2 теперь обращается напрямую к origin сервера OpenCode вместо прокси `*.vsoc-app`.
- Убраны мост COM-хоста, прокси запросов и внедряемые скрипты.
- Заголовок окна-инструмента — «OpenCode Studio».
- Сервер больше не выключается по простою.

#### Исправлено
- Определение URL сервера больше не прерывается на первом чтении (гонка `ReadLineAsync`).
- Обрывы связи восстанавливаются автоматически страницей прогресса и повторами.
- Предпочитается `.exe`-исполняемый файл OpenCode; shim `.cmd`/`.bat` запускаются через `cmd.exe`.

### [1.0.0] - 2026-09-19

#### Добавлено
- Первый публичный релиз: веб-UI OpenCode, встроенный в окно-инструмент Visual Studio.

**Релизы:** https://github.com/Jevkray/OpenCodeJesk/releases
