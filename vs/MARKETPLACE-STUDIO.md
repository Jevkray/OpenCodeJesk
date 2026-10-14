# 🟣 OpenCode Studio (final version)

**The OpenCode AI coding agent — natively inside Visual Studio.**

*Works with Visual Studio 2022 and 2026.*

![Marketplace](https://img.shields.io/visual-studio-marketplace/v/jevkray.OpenCodeStudio?label=marketplace&color=7C3AED)
![Visual Studio](https://img.shields.io/badge/Visual%20Studio-2022%20%7C%202026-7C3AED)
![License](https://img.shields.io/badge/license-MIT-7C3AED)

---

> ## ⚠️ This extension is now called **OpenCode Jesk**
>
> We renamed **OpenCode Studio → OpenCode Jesk**. It is the **same extension** with full feature parity: your settings, providers, credentials and chat history are kept — **nothing needs to be reconfigured**.
>
> **Why:** to give the extension an unambiguous name, because an unrelated Visual Studio Code extension already uses the same name.
>
> **➡️ Install the new version: [OpenCode Jesk on the Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=jevkray.OpenCodeJesk)**
>
> **How to switch:**
> 1. Install **OpenCode Jesk** from the link above.
> 2. Remove **OpenCode Studio** in *Extensions → Manage Extensions*.
> 3. Reopen the panel — both use the same OpenCode data, so your chats appear immediately.
>
> **Support for OpenCode Studio ends on 20.10.2026.** After that date only **OpenCode Jesk** is available and updated.

---

## 📸 Preview

![OpenCode Studio — Desktop (Web) panel](https://raw.githubusercontent.com/Jevkray/OpenCodeStudio/main/docs/images/jesk-desktop.png)
*The panel in **Desktop (Web)** mode — the full OpenCode web UI inside the tool window.*

![OpenCode Studio — CLI (terminal) panel](https://raw.githubusercontent.com/Jevkray/OpenCodeStudio/main/docs/images/jesk-cli.png)
*The panel in **CLI (terminal)** mode — a plain terminal with OpenCode already running.*

## 🆕 What's new in 1.2.0 (final)

- **Desktop (Web)** and **CLI (terminal)** interface modes in the same panel; the active chat is carried over between modes.
- A dedicated **Settings** window (General, Terminal, Server, Updates, Import) with placeholders, detailed descriptions and a RU/EN switch.
- Terminal: font family and size, cursor blink, scrollback lines, extra CLI arguments, start directory.
- Server: opencode binary path, preferred port, extra `serve` arguments, stop after idle minutes.
- Updates: check for new versions, interval and release channel (stable / beta).
- **Auto-open panel** on solution open, **web zoom**, and a toggle for the OpenCode Console (limits) button.
- **opencode v1 and v2 (preview)** support; new-chat view defaults to the current project.
- Safe import with backups (refuses to overwrite history while OpenCode is running).

## ✨ Highlights

- 🧠 **Real OpenCode, not a clone** — the complete official web UI, rendered directly in Visual Studio.
- 🔀 **opencode v1 and v2 (beta)** — a native version switch with automatic server restart.
- 🔄 **Shared history** — the same chats as the CLI and desktop app, across every Visual Studio window.
- ⌨️ **Desktop / CLI** — the Web panel, or a plain terminal with OpenCode already running inside the same panel.
- 📈 **Profile & usage in one click** — the console button opens your OpenCode profile and limits.
- 🔒 **Local-first** — loopback only, zero telemetry.
- 🪶 **Lightweight** — one VSIX for Visual Studio 2022 and 2026.

## 🚀 Getting started

```
View   ->  Other Windows  ->  OpenCode Studio     (the agent)
Tools  ->  OpenCode Studio Settings               (settings & import)
```

> **Requirement:** the OpenCode CLI (v1 or v2 beta) must be installed and available on your `PATH`.

## 🔗 Syncing chat history

OpenCode stores every session **against the project directory it was created in**. OpenCode Studio uses the same data as the CLI and desktop app, so all chats are already there — grouped by project path. Line up the project paths and everything synchronizes.

## 🛡️ Privacy

- 🖧 the server binds to `127.0.0.1` only
- 🔑 credentials are stored by OpenCode itself in your own environment
- 🚫 no telemetry, no analytics

## 💬 Contacts & Support

- 📨 Telegram: https://t.me/eugenekray
- ✉️ Email: krasovskyworks@gmail.com
- 💜 Support the project: https://boosty.to/jevkray
- 🐙 Source code and issues: https://github.com/Jevkray/OpenCodeStudio

## 📜 License

Released under the [MIT License](https://github.com/Jevkray/OpenCodeStudio/blob/main/LICENSE).
