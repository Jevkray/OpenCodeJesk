<div align="center">

<img src="https://raw.githubusercontent.com/Jevkray/OpenCodeStudio/main/vs/Resources/PreviewImageJesk.png" width="240" alt="OpenCode Jesk" />

# 🔵 OpenCode Jesk

**The OpenCode AI coding agent — natively inside Visual Studio.**

*Formerly OpenCode Studio. A fast, self-contained tool window with a settings/import wizard and shared chat history.*

[![Marketplace](https://img.shields.io/visual-studio-marketplace/v/jevkray.OpenCodeJesk?label=marketplace&color=2A78FC)](https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeJesk)
[![Installs](https://img.shields.io/visual-studio-marketplace/i/jevkray.OpenCodeJesk?color=2A78FC)](https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeJesk)
[![Rating](https://img.shields.io/visual-studio-marketplace/r/jevkray.OpenCodeJesk?color=2A78FC)](https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeJesk)
![Visual Studio](https://img.shields.io/badge/Visual%20Studio-2022%20%7C%202026-2A78FC)
![License](https://img.shields.io/badge/license-MIT-2A78FC)
![.NET](https://img.shields.io/badge/.NET%20Framework-4.7.2-2A78FC)

**[English](README.md) · [Русский](README.ru.md)**

</div>

---

> [!IMPORTANT]
> **Renamed: OpenCode Studio → OpenCode Jesk.** This is the same extension under a new name, and it stays fully compatible — your settings, providers, credentials and chat history are kept. **OpenCode Studio support ends on 20.10.2026.** Please switch to the new [OpenCode Jesk on the Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeJesk).

> [!TIP]
> **One environment, everywhere.** OpenCode Jesk shares the same data as the OpenCode CLI and desktop app — your providers, settings, credentials and **every chat** are already there. No setup, no duplication.

## 📸 Preview

![OpenCode Jesk — all panel versions](https://raw.githubusercontent.com/Jevkray/OpenCodeStudio/main/docs/images/preview-collage.png)
*OpenCode Jesk — all panel versions.*

![OpenCode Jesk — Desktop (Web) panel](https://raw.githubusercontent.com/Jevkray/OpenCodeStudio/main/docs/images/jesk-desktop.png)
*The panel in **Desktop (Web)** mode — the full OpenCode web UI inside the tool window.*

![OpenCode Jesk — CLI (terminal) panel](https://raw.githubusercontent.com/Jevkray/OpenCodeStudio/main/docs/images/jesk-cli.png)
*The panel in **CLI (terminal)** mode — a plain terminal with OpenCode already running.*

## 🆕 What's new

- **1.2.0** - **Desktop (Web)** and **CLI (terminal)** interface modes in the same panel; shared chat between modes; a dedicated **Settings** window (General, Terminal, Server, Updates, Import); auto-open on solution open; **web zoom**; terminal font, size, cursor and scrollback; server port/arguments/idle stop; marketplace update checks; **v1 / v2 preview** support; safe import with backups; and the rename to **OpenCode Jesk**.
- **1.1.5.1** (minor hotfix) - More reliable **Open Extension Manager** action (opens the Visual Studio extension manager, Updates page first); the update window now hides until the next Visual Studio restart on any action, and explains how to turn the notifications off in settings.
- **1.1.5** - Fixed: closing the update window hides it until the next restart; "Open Extension Manager" opens the Visual Studio extension manager.
- **1.1.4** - Background update checks (once a minute). For **OpenCode Jesk itself**: a compact dark-themed window shows the new version and its release notes with **Open Extension Manager** / **Marketplace page** / **Not now** buttons (Visual Studio already installs extension updates in the background). For **OpenCode CLI**: the window offers to update it right from the extension (`npm i -g opencode-ai@latest`) or shows the manual command; the panel picks up the new version on restart. New setting **Check for new versions** turns extension update notifications off.
- **1.1.3** - The Settings window shows the installed and the latest available OpenCode version, with an **Update** button when a newer release exists. A new OpenCode version is now picked up automatically.
- **1.1.2**
  - Always uses the **latest installed opencode**: on start the server re-checks the binary (path + modification time) and restarts if it changed; `server.json` now records the real server process PID.
  - The first-run wizard is now a **Settings** window with **Import** and **Settings** tabs; the button is **Save selected** and applies both imports and settings.
- **1.1.0** - Statistics button in the OpenCode panel: open your profile and check real usage without leaving Visual Studio.

## ✨ Why OpenCode Jesk?

| | |
|---|---|
| 🧠 | **Real OpenCode, not a clone** — the complete official web UI, rendered directly in Visual Studio. |
| 🔄 | **Shared history** — the same chats as the CLI and desktop app, across every Visual Studio window. |
| 🗂️ | **Current project by default** — new-chat view selects the open Visual Studio solution/repository; other projects and chats remain accessible. |
| ⌨️ | **Desktop / CLI** — the Web panel, or a plain terminal with OpenCode already running inside the same panel, for both v1 and v2 preview. |
| 📈 | **Profile & usage in one click** — the statistics button opens your OpenCode console inside the panel, so you can check real spend and limits without switching to a browser. |
| 📥 | **Settings & import wizard** — import settings, themes, credentials and history in one click. |
| 🔁 | **Auto-reconnect** — recovers the session by itself if the server drops. |
| 🔒 | **Local-first** — loopback only, zero telemetry. |
| 🪶 | **Lightweight** — one VSIX for Visual Studio 2022 and 2026. |

- ⚙️ **Update check toggle** - turn update notifications on or off in settings.

## ⌨️ Web and CLI

Choose **Desktop (Web)** or **CLI** in **Settings → Settings → Interface**. The mode selector is only in settings, not in the panel. The **Use OpenCode v2.x (preview)** option is independent and applies to both modes.

Desktop keeps the existing WebView2 integration. CLI shows a plain terminal with OpenCode already running — no Visual Studio terminal chrome, no extra UI, just the terminal and the agent. It lives in the same OpenCode Jesk panel, so Visual Studio keeps its docked position and size, and it starts automatically in the project directory.

Both modes connect to the same server. Switching from Web to CLI can continue the selected chat. History remains within the selected OpenCode version — v1/v2 database formats are not converted automatically. Opening Jesk defaults the new-chat view to the solution's Git root, or the solution directory if there is no repository.

Verification: build Release, then run `tests\Run-TerminalSmoke.ps1`. The check uses isolated test data and covers the terminal (ConPTY), v1/v2 shared sessions, and terminal rendering in WebView2.

## 🚀 Quick start

```text
  View   →  Other Windows  →  OpenCode Jesk          🔹 the agent
  Tools  →  OpenCode Jesk Settings                   📥 setup wizard
```

> [!NOTE]
> **Requirement:** the [OpenCode CLI](https://opencode.ai) must be installed and available on your `PATH`.

<details>
<summary><b>📦 Installation</b></summary>

**From the Visual Studio Marketplace (recommended)**

1. In Visual Studio open **Extensions → Manage Extensions**.
2. Search for **OpenCode Jesk** and install it.
3. Restart Visual Studio.

**From a VSIX**

1. Download the latest `OpenCodeJesk-x.y.z.vsix` from [Releases](https://github.com/Jevkray/OpenCodeStudio/releases).
2. Close all Visual Studio instances and double-click the file.

</details>

## 🔗 Syncing chat history

OpenCode stores every session **against the project directory it was created in**. Because OpenCode Jesk shares the same data as the CLI and desktop app, all chats are already available — but grouped by project path.

> [!IMPORTANT]
> If a project lives at a **different absolute path** than when its chats were created (another drive, another user name, a moved or renamed folder), those sessions stay under the original path. **Line up the project paths and everything synchronizes.**

1. Open each project at the **same absolute path** it originally used — e.g. `C:\Users\<you>\source\repos\MyApp`.
2. If the path changed, move the project back, or import the old data via **Tools → OpenCode Jesk Settings** pointing at the folder with the original `opencode.db`.
3. Reopen the tool window — the sessions appear under the matching project.

Once the paths match, **all chats sync** between the CLI, the desktop app and every Visual Studio window. 🎉

## Usage statistics

Statistics are not part of this release yet. Use the statistics button in the OpenCode panel to open the OpenCode Console (usage and limits) directly.

## 🧱 Architecture

OpenCode Jesk keeps a deliberately small, decoupled core.

```text
 ┌──────────────────────────────────────────────────────────────┐
 │  OpenCodeJeskPackage            VS package & commands      │
 ├──────────────────────────────────────────────────────────────┤
 │  ServerController                server + session lifecycle  │
 │    ├── OpenCodeServerService      launches `opencode serve`  │
 │    ├── OpenCodeSessionService     /session, /project API     │
 │    ├── ConnectionMonitor          periodic health checks     │
 │    └── ProcessBinding             dies with Visual Studio    │
 ├──────────────────────────────────────────────────────────────┤
 │  OpenCodeToolWindowControl        WebView2 host              │
 │    └── opens http://127.0.0.1:<port>/ (the web UI home)      │
 ├──────────────────────────────────────────────────────────────┤
 │  SettingsWindow / ImportService   settings & import wizard   │
 │    └── OpenCodeEnvironment / Log                             │
 └──────────────────────────────────────────────────────────────┘
```

> [!TIP]
> **Design principles**
> - **No proxy layer** — WebView2 talks to the real server origin, so the UI is authentic and update-proof.
> - **Shared data** — runs in your default environment, in sync with the CLI and desktop app.
> - **One server per machine** — every Visual Studio window reuses the same OpenCode server.
> - **Local by default** — loopback only, no outbound calls except your providers.

## 🧩 Requirements

| Component | Version |
|---|---|
| 🖥️ Visual Studio | 2022 and 2026 |
| ⚡ OpenCode CLI | v1 and v2 (beta), installed and on `PATH` |
| 🛠️ .NET SDK *(build from source)* | .NET 10 |
| 🌐 WebView2 Runtime | bundled with Windows 10/11 and VS |

## 🔨 Building from source

```bash
git clone https://github.com/Jevkray/OpenCodeStudio.git
cd OpenCodeStudio
dotnet build vs/OpenCodeJesk.csproj -c Release
```

The VSIX is produced at `vs/bin/Release/net472/OpenCodeJesk.vsix`. To build the release VSIX and copy it to the repository root and Desktop, run `build.ps1`. For a debug run, open `OpenCodeJesk.sln` and press **F5** to launch the experimental instance. 🧪

## ⚙️ Settings

![OpenCode Jesk — Settings: all tabs](https://raw.githubusercontent.com/Jevkray/OpenCodeStudio/main/docs/images/settings-collage.png)
*Settings: all tabs.*

## 🛡️ Privacy

- 🖧 the server binds to `127.0.0.1` only
- 🔑 credentials are stored by OpenCode itself in your own environment
- 🚫 no telemetry, no analytics
- 📈 the usage overlay signs in to the OpenCode Console and reads your usage and limits from `opencode.ai` using your own session cookie (stored encrypted with DPAPI) — no other data leaves your machine.

## 💬 Contacts & Support

<div align="center">

[![Telegram](https://img.shields.io/badge/Telegram-@eugenekray-2CA5E0?logo=telegram&logoColor=white)](https://t.me/eugenekray)
[![Email](https://img.shields.io/badge/Email-krasovskyworks@gmail.com-EA4335?logo=gmail&logoColor=white)](mailto:krasovskyworks@gmail.com)
[![Boosty](https://img.shields.io/badge/Support-Boosty-FF6A00?logo=boosty&logoColor=white)](https://boosty.to/jevkray)
[![Issues](https://img.shields.io/badge/Issues-GitHub-181717?logo=github&logoColor=white)](https://github.com/Jevkray/OpenCodeStudio/issues)

</div>

## 📜 License

Released under the [MIT License](LICENSE).

<div align="center"><sub>Built for the OpenCode community · Not affiliated with OpenCode or Microsoft.</sub></div>
