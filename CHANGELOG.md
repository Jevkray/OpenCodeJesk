# Changelog

All notable changes to **OpenCode Studio** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [2.0.0.0] - 2026-10-15

### Added

#### Quick Start (first tab, opens on first run)

A step-by-step guide on a single page, so a new user can go from zero to a running agent without leaving the Settings window:

1. **Install** — a single **Install All** button installs **v2 first, then v1**, so both versions of the OpenCode CLI are available side by side (`opencode` = v1, `opencode2` = v2) and either can be used. The install streams a **live npm log**; a **non-linear progress bar** driven by the real output shows the current **percentage** and **elapsed time**, with a sweeping **shimmer** highlight and rounded corners. A **Cancel** button aborts a running install, and the bar turns **green** with *Download complete* on success.
2. **Version** — choose the CLI version to use: **v1** (stable) or **v2** (preview).
3. **Interface** — pick the mode, **Desktop (Web)** or **CLI**, and open the panel with a single button.
4. **Overview** — an at-a-glance map of where the extension, the data directory, the server and the logs live.

#### Sponsorship / Contacts

- A thank-you letter for supporting the project.
- Contacts: **Telegram, Email, Boosty and GitHub**.
- Crypto wallets for **TON, BTC and ETH**, each with a **QR code**, switched through **sub-tabs**, with an address field and a **Copy** button.

#### Updates

- A **leave-a-review** card linking to the Marketplace review page.
- A **rating popup** shown **once per session**, after roughly **two hours** of use, with a toggle in Settings to turn it off.
- A detailed **"What's new" changelog** (the bundled `CHANGELOG.md`, parsed and rendered) **instead of the raw installation log**.

#### UI and brand

- A **blue palette matched to the extension icon** — gradients and soft glows, used consistently across the panel and the Settings window.
- The **extension icon** in the Visual Studio menu and in the Settings window title.
- **Always-dark windows**, with restyled tabs and menus.

### Changed

- **Full rebrand to OpenCode Jesk.** The product is a single brand; the old **OpenCode Studio** name has been **removed entirely**. Renamed: the **namespace**, the **assembly**, the package **class**, the **VSCT** command tables and the **icons**. The data directory is now `%LOCALAPPDATA%\OpenCodeJesk` (with **migration** from the old `%LOCALAPPDATA%\OpenCodeStudio`), the environment variable is `OPENCODEJESK_OPENCODE_BIN`, and the pack URIs use the new name. `build.ps1` now builds the single Jesk brand.
- **Version bumped to 2.0.0.0.**
- **Internal refactor** — the "god" classes were split into focused services: `CliInstallService`, `Localization`, `CliVersionStatusService`, `CliBinaryLocator`, `ServerPortResolver`, `SharedServerRegistry`, `WebPageRenderer`, `SessionUrlParser`, `ConsoleCoordinator`, `ReconnectCoordinator`, `CliModeCoordinator`, `SettingsEditor`, `ImportPresenter`, `InstallStatusRenderer` and `UiPalette`. Dead code was removed, comments were translated to Russian, and the sources were organized into folders.

### Fixed

- **v1 ↔ v2 switching in the web panel** — the WebView2 cache is cleared whenever the version changes, so the panel no longer shows a stale page.
- **Version selection at start** — the restart/adopt decision now compares by **major** version.
- **CLI crash when switching versions** — "Session not found" is handled by validating the session and creating it if missing before the terminal opens.
- **npm installation** — runs with `--force` and stops the extension's own processes first, so locked shims no longer block the install.
- **Bunny rounding** — the running "bunny" no longer overflows the progress bar's edges.
- **Hover styles** — the bright blue highlight was removed; the tabs and the Sponsorship item now follow the side-menu style.

### Docs

- Updated **README (EN/RU)** and the **Marketplace description** with the **blue** style and new **collages** (a preview of all panel versions and a collage of all Settings tabs).
- Reworked the README structure.

## [1.2.0.32] - 2026-10-15

### Added
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

### Fixed
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

### Changed
- **Blue accent palette matched to the icon** (gradient); the extension windows are always dark, with tidied spacing.
- **Dead code removed** (`HealthInfo`, `CreateSessionRequest`, the `UsageWindow`/`StatsService` chain, unused members) and the install log (`UpdateLog`) dropped.
- **Internal refactor: services extracted** for clarity and testing — `CliInstallService`, `Localization`, `CliVersionStatusService`, `CliBinaryLocator`, `ServerPortResolver`, `SharedServerRegistry`, `WebPageRenderer`, `SessionUrlParser`, `ConsoleCoordinator`, `ReconnectCoordinator`, `CliModeCoordinator`, `SettingsEditor`, `ImportPresenter`, `InstallStatusRenderer` and `UiPalette`.
- Brand-aware Settings window titles.

## [1.2.0] - 2026-10-14

### Added
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

### Changed
- Version bumped to **1.2.0**.
- One codebase now builds a single brand, **OpenCode Jesk**, via `build.ps1`.

## [1.1.5.1] - 2026-10-01

### Changed (minor)
- Minor maintenance hotfix: more reliable **Open Extension Manager** action (opens the Visual Studio extension manager; tries the Updates page first, then several fallbacks) and the update window stays hidden until the next Visual Studio restart after it is closed with the X button.
- Any button in the update window now also hides it until the next Visual Studio restart (instead of only "Not now"), and the window explains how to turn the notifications off in settings.

## [1.1.5] - 2026-09-30

### Fixed
- Closing the update window with the X button now suppresses it until the next Visual Studio restart (same as "Not now").
- "Open Extension Manager" now reliably opens Visual Studio's Extension Manager (several known command names plus a DTE command search); the Marketplace page is only a fallback.

## [1.1.5] - 2026-09-30

### Fixed
- Closing the update window with the X button now suppresses it until the next Visual Studio restart (same as "Not now").
- "Open Extension Manager" now reliably opens Visual Studio's Extension Manager (several known command names plus a DTE command search); the Marketplace page is only a fallback.

## [1.1.4] - 2026-09-30

### Added
- Background check for a newer OpenCode Studio release on the Visual Studio Marketplace (once a minute). When an update is available, a compact dark window shows the new version and its release notes with **Open Extension Manager** / **Marketplace page** / **Not now** buttons (Visual Studio itself installs updates in the background; "Not now" hides the window until the next restart).
- OpenCode itself is now checked for updates too (once a minute). If a newer CLI version is available, a compact window offers to update it right from the extension (`npm i -g opencode-ai@latest`) or shows the manual command; the panel picks up the new version on restart.
- New setting **Check for new versions** (Tools -> Options -> OpenCode Studio, and the Settings window). When disabled, the extension no longer shows update notifications.

## [1.1.3] - 2026-09-30

### Added
- The Settings window now shows the installed and the latest available OpenCode version, and offers an **Update** button (runs `npm i -g opencode-ai@latest`) when a newer version exists. Combined with the existing start-time check, a new OpenCode version is picked up automatically.

## [1.1.2] - 2026-09-24

### Changed
- **Always uses the latest installed opencode.** On start the server re-checks the binary (its path and modification time) and restarts if it was updated (or the v1/v2 mode changed), so a fresh start automatically picks up the latest installed version.
- The real PID of the server process is now written to `server.json`.
- The import wizard is now a plain **Settings** window with two tabs (**Import** and **Settings**); the welcome texts were removed.
- The first-run wizard button is now **Save selected** (it applies your choices, including settings, not only imports).
- Removed the "OpenCode Studio Usage Statistics" menu item and its window for now (statistics will return later; a button in the panel opens the OpenCode Console in the meantime).

## [1.1.1] - 2026-09-24

### Added
- **OpenCode v1 and v2 support.** The extension now secures the embedded server with its own password and sends Basic Auth on every request (and to WebView2), so it works both with stable opencode v1 and with opencode v2 / the v2 beta, which protect the server after the CVE-2026-22812 fix. The misleading "not in PATH" error is gone.
- Detect the OpenCode 2 beta binary (`opencode2`) in addition to `opencode`, plus a native **Use OpenCode v2.x (preview)** switch in the settings window and in Tools -> Options -> OpenCode Studio. Changing it restarts the server with the selected binary. `OPENCODESTUDIO_OPENCODE_BIN` can force a binary path.
- **Visual Studio 2022 and 2026 support** — one VSIX for both.

### Fixed
- **OpenCode v2 (beta) reconnect loop.** The v2 server answers every path with its web app, so the strict `{"healthy":true}` health check always failed and the extension kept restarting the server ("connection lost - reconnecting"). Health now only requires a successful HTTP response, which works for both v1 and v2.
- Server processes are now killed **with their child tree** (`taskkill /T`). Previously a `.cmd` shim left the Node process alive, so it kept holding the port and every restart picked a new random port.
- Readiness no longer depends on the server's exact startup log line.

## [1.1.0] - 2026-09-22

### Added
- **Statistics button in the OpenCode panel.** It sits just left of the context button in the prompt footer, and on opencode.ai pages it docks to the corner. One click opens your OpenCode profile and console right inside the tool window - check real usage and subscription limits without leaving Visual Studio, and click again to return to your chats.
- **Sign-in to the OpenCode Console** inside the embedded browser (Google, GitHub or email). The session cookie is captured after the return navigation and stored encrypted with DPAPI.
- **Tools -> Options -> OpenCode Studio -> Usage & Limits** settings page, plus a checkbox in the first-run wizard.
- Reworked bilingual documentation (English and Russian) and a Marketplace description with a preview.

### Fixed
- **Stable server port** - the preferred port, then the last used one, then a free one. The embedded UI keeps a constant origin, so local settings, theme and chat history no longer reset between restarts.
- Sign-in reliably returns to the app after authentication and restarts the monitor.
- The injected button no longer triggers the context tooltip.

### Notes
- The built-in usage dashboard (remaining Go limits and spend charts inside the extension) is in progress - see the roadmap issue on GitHub.

## [1.0.10] - 2026-09-20

### Added
- Reworked bilingual documentation (English and Russian) with a preview screenshot, feature overview and contacts (Telegram, email, Boosty).
- A dedicated **Syncing chat history** guide: chats are grouped by project path, so matching the original project paths makes the full history appear everywhere.
- Marketplace description with a preview section.

## [1.0.9] - 2026-09-20

### Changed
- The tool window now opens the OpenCode web UI **home**, so all projects and every chat are visible - exactly like the OpenCode desktop app.
- The server uses a **stable port** (4096 when free), keeping the WebView2 origin constant so the web UI remembers its state between restarts.
- The extension no longer creates an empty session on every project change.

## [1.0.8] - 2026-09-20

### Fixed
- **Chat history now loads in the tool window.** The navigation URL now encodes the project directory exactly as OpenCode reports it (a Windows path with backslashes); previously a normalized forward-slash path produced a different base64 and the web UI could not resolve the project.
- API responses are now decoded as UTF-8 explicitly, so non-ASCII (e.g. Cyrillic) project paths match correctly on .NET Framework.

## [1.0.7] - 2026-09-20

### Fixed
- **Chat history now syncs** with the OpenCode CLI and desktop app: OpenCode Studio uses the shared default OpenCode environment instead of an isolated one.
- **Multiple Visual Studio windows share one OpenCode server** (reused via a registry file), so sessions and live agent state stay in sync between windows.
- **History no longer appears to reset** when a solution is opened or closed: the last project root is remembered as a stable fallback.

### Changed
- The import wizard now targets the shared environment and is intended for importing from external folders/backups.

## [1.0.6] - 2026-09-20

### Added
- New **OpenCode Studio** brand and icon set (light and dark variants).
- Command, tool window and menu icons.
- First-run **import wizard** with multi-source detection (CLI, Desktop, custom folder).
- **Isolated OpenCode environment** under `%LOCALAPPDATA%\OpenCodeStudio`.
- File logging to `%LOCALAPPDATA%\OpenCodeStudio\logs\opencode-studio.log`.
- `git`-agnostic CI workflow for building the VSIX.

### Changed
- **Re-architected**: WebView2 now talks directly to the OpenCode server origin instead of a `*.vsoc-app` proxy.
- Removed the COM host bridge, request proxy and injected scripts.
- Tool window caption is now "OpenCode Studio".
- Server no longer shuts down after being idle.

### Fixed
- Server URL detection no longer aborts on the first read (the `ReadLineAsync` race).
- Connection drops are recovered automatically with a progress page and retries.
- Prefers the `.exe` OpenCode executable; runs `.cmd`/`.bat` shims via `cmd.exe`.

## [1.0.0] - 2026-09-19

### Added
- Initial public release: OpenCode web UI embedded in a Visual Studio tool window.

[2.0.0.0]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v2.0.0.0
[1.2.0.32]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.2.0.32
[1.2.0]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.2.0
[1.1.5.1]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.1.5.1
[1.1.5]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.1.5
[1.1.5]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.1.5
[1.1.4]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.1.4
[1.1.3]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.1.3
[1.1.2]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.1.2
[1.1.1]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.1.1
[1.1.0]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.1.0
[1.0.10]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.0.10
[1.0.9]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.0.9
[1.0.8]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.0.8
[1.0.7]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.0.7
[1.0.6]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.0.6
[1.0.0]: https://github.com/Jevkray/OpenCodeJesk/releases/tag/v1.0.0
