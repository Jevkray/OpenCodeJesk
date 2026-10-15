# 🔵 OpenCode Jesk

**The OpenCode AI coding agent — natively inside Visual Studio.**

*Formerly OpenCode Studio. A fast, self-contained tool window with a settings/import wizard and shared chat history. Works with Visual Studio 2022 and 2026.*

![Marketplace](https://img.shields.io/visual-studio-marketplace/v/jevkray.OpenCodeJesk?label=marketplace&color=2A78FC)
![Installs](https://img.shields.io/visual-studio-marketplace/i/jevkray.OpenCodeJesk?color=2A78FC)
![Visual Studio](https://img.shields.io/badge/Visual%20Studio-2022%20%7C%202026-2A78FC)
![License](https://img.shields.io/badge/license-MIT-2A78FC)

---

> **Renamed: OpenCode Studio → OpenCode Jesk.** Same extension, new name, fully compatible — your settings, providers, credentials and chat history are kept. **OpenCode Studio support ends on 20.10.2026.** Switch to the new [OpenCode Jesk on the Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeJesk).

---

## 📸 Preview

![OpenCode Jesk — all panel versions](https://raw.githubusercontent.com/Jevkray/OpenCodeJesk/main/docs/images/preview-collage.png)
*OpenCode Jesk — all panel versions.*

## 🆕 What's new

- **2.0.0.0** — a big update:
  - **Quick Start** (first tab, opens on first run) — a guided page that takes you from zero to a running agent:
    - **Install All** — one click installs the OpenCode CLI **v2 then v1**, so both versions work side by side.
    - Live **npm log**, a **non-linear progress bar** (real progress, **percentage** and **elapsed time**, a sweeping shimmer and rounded corners), a **Cancel** button and a green *Download complete*.
    - Pick the **v1 / v2** version and the **Desktop (Web) / CLI** interface, then open the panel.
    - An overview of where the extension, server, data and logs live.
  - **Sponsorship / Contacts** tab — a thank-you note, contacts (Telegram, Email, Boosty, GitHub) and **TON / BTC / ETH** wallets with **QR codes**, sub-tabs, an address field and Copy.
  - **Updates** tab — a leave-a-review card, a one-time rating popup (after ~2 h, with a toggle in Settings) and a detailed **“What’s new” changelog** instead of the raw install log.
  - **Blue palette** matched to the icon (gradients, soft glows), the extension icon in the menu and the Settings title, always-dark windows.
  - **Full rebrand to OpenCode Jesk** — one brand (namespace, assembly, package class, VSCT and icons renamed; data dir `%LOCALAPPDATA%\OpenCodeJesk` with migration; `OPENCODEJESK_OPENCODE_BIN`; new pack URIs) and an internal service-extraction refactor.
  - **Fixed:** v1↔v2 web switching (WebView2 cache), start-time version selection, CLI crash on version switch (“Session not found”), npm install (`--force`) and hover/rounding polish.
- **1.2.0** — **Desktop (Web)** and **CLI (terminal)** modes in the same panel; shared chat between modes; a dedicated **Settings** window (General, Terminal, Server, Updates, Import); auto-open on solution open; **web zoom**; terminal font/size/cursor/scrollback; server port/arguments/idle stop; marketplace update checks; **v1 / v2 preview** support; safe import with backups; and the rename to **OpenCode Jesk**.
- **1.1.5.1** — more reliable **Open Extension Manager** action; the update window hides until the next Visual Studio restart on any action and explains how to turn notifications off.
- **1.1.5** — closing the update window hides it until the next restart; “Open Extension Manager” opens the Visual Studio extension manager.
- **1.1.4** — background update checks for both the extension and the OpenCode CLI, with a compact dark window and **Open Extension Manager / Marketplace page / Not now** actions; new **Check for new versions** setting.

**Full changelog (EN / RU):** [UPDATES.md](https://github.com/Jevkray/OpenCodeJesk/blob/main/UPDATES.md)

## ✨ Highlights

- 🧠 **Real OpenCode, not a clone** — the complete official web UI, rendered directly in Visual Studio.
- 🔀 **opencode v1 and v2 (preview)** — a native switch in Settings and in **Tools → Options → OpenCode Jesk**; the extension auto-detects `opencode2` and handles the v2 server's HTTP Basic Auth.
- 🔄 **Shared history** — the same chats as the CLI and desktop app, across every Visual Studio window.
- 🗂️ **Current project by default** — new-chat view selects the open solution/repository; other projects remain selectable.
- ⌨️ **Desktop / CLI** — the Web panel, or a plain terminal with OpenCode already running inside the same panel; both support v1 or v2.
- 📈 **Profile & usage in one click** — the statistics button opens your OpenCode Console (real spend and limits) inside the panel.
- 📥 **Settings & import wizard** — import settings, themes, credentials and history in one click.
- 🔁 **Auto-reconnect** — recovers the session by itself if the server drops.
- 🔒 **Local-first** — loopback only, zero telemetry.
- 🪶 **Lightweight** — one VSIX for Visual Studio 2022 and 2026.

## 🚀 Getting started

```
View   ->  Other Windows  ->  OpenCode Jesk          (the agent)
Tools  ->  OpenCode Jesk Settings                    (Quick Start & settings)
```

> **Requirement:** the OpenCode CLI (v1 or v2 preview) must be installed and available on your `PATH`. The **Quick Start** tab installs it for you.

## ⌨️ Web and CLI

Choose **Desktop (Web)** or **CLI** in **Settings → Interface**. There is no mode selector in the panel. The version switch **Use OpenCode v2.x (preview)** applies to both modes. CLI shows a plain terminal with OpenCode already running inside the same panel — no Visual Studio terminal chrome — started automatically in the current project directory. Visual Studio keeps the panel's docked position and size.

Both modes use the same authenticated OpenCode server. A chat selected in Web can be continued in CLI. History sharing is within the selected OpenCode version, without automatic conversion between v1 and v2 database formats.

## 🔗 Syncing chat history

OpenCode stores every session **against the project directory it was created in**. Because OpenCode Jesk shares the same data as the CLI and desktop app, all chats are already available — but grouped by project path.

If a project lives at a **different absolute path** than when its chats were created (another drive, another user name, a moved or renamed folder), those sessions stay under the original path. **Line up the project paths and everything synchronizes.**

1. Open each project at the **same absolute path** it originally used.
2. If the path changed, move the project back, or import the old data via **Tools → OpenCode Jesk Settings**, pointing at the folder with the original `opencode.db`.
3. Reopen the tool window — the sessions appear under the matching project.

## ⚙️ Settings

![OpenCode Jesk — Settings: all tabs](https://raw.githubusercontent.com/Jevkray/OpenCodeJesk/main/docs/images/settings-collage.png)
*Settings: all tabs.*

## 🛡️ Privacy

- 🖧 the server binds to `127.0.0.1` only
- 🔑 credentials are stored by OpenCode itself in your own environment
- 🚫 no telemetry, no analytics
- 📈 the usage overlay signs in to the OpenCode Console and reads your usage and limits from `opencode.ai` using your own session cookie (stored encrypted with DPAPI) — no other data leaves your machine

## 💬 Contacts & Support

- 📨 Telegram: https://t.me/eugenekray
- ✉️ Email: krasovskyworks@gmail.com
- 💙 Support the project: https://boosty.to/jevkray
- 🐙 Source code and issues: https://github.com/Jevkray/OpenCodeJesk
- 📜 Full changelog: https://github.com/Jevkray/OpenCodeJesk/blob/main/UPDATES.md

## 📜 License

Released under the [MIT License](https://github.com/Jevkray/OpenCodeJesk/blob/main/LICENSE).
