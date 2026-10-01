---
name: rei-cast
description: Configure or extend the user's Rei Cast D-CAST extended-display companion, its pixel avatar, weather, display selection and layout, refresh frequency, Windows login startup, or Codex task status display. Use when the user explicitly refers to Rei Cast or the water-cooler assistant display.
---

# Rei Cast

This plugin accompanies a lightweight native Windows app. Its passive Codex event observer runs independently of the agent and requires no AI calls or MCP server.

1. Read `%USERPROFILE%/ReiCast/config.json` and `runtime.json` to inspect current configuration and the latest heartbeat. A heartbeat older than 30 seconds is not proof the app is running.
2. Keep the user's settings. Use `scripts/install.ps1` relative to the plugin root to install or update; it preserves config and migrates legacy LocalAppData settings. Install in the user's physical `ReiCast/app` folder: MSIX LocalAppData redirection caused the old Windows logon process to report file-not-found. Login startup uses a per-user Windows Run entry and a 30-second delayed, interactive per-user Task Scheduler fallback. A transient launcher records `startup-launch.json` and then exits. Both run as the current user without elevation, and the app's mutex coalesces duplicate launches. The tray option controls both. Verify a fresh scheduled launch and result 0, not just registration.
3. Edit configuration atomically. The app reloads it within 10 seconds. Keep metrics >= 2 seconds, event polling >= 2 seconds, weather >= 15 minutes. Use `codexHome` only for a local Codex directory. `avatarPath` may point to a replacement local image.
4. To switch weather location, resolve the requested city to coordinates and update `city`, `latitude`, and `longitude` together. Default: Chengdu, 30.5728, 104.0668.
5. Display `auto` selects one recognized non-primary D-CAST/DeepCool/JZFS display. A unique 854 x 480 or 480 x 854 secondary screen is the legacy fallback. Ambiguous or unknown screens require manual selection in Settings. Preserve `displayIdentity` so numbering changes do not switch the target. Layout `auto` fits landscape, portrait, or compact canvases to the actual resolution. The app hides in the tray when the target is absent. Never force another display's resolution or alter the vendor cooler software.
6. For extensions use the local JSON contract in `EXTENSIONS.md`. Read/write only local status metadata; never send chat content to a remote service. Do not fabricate progress percentages or turn tool-call count into task progress.
7. Source is in `src/`; compile with `scripts/build.ps1` and verify with `scripts/verify.ps1`. Preview rendering: `ReiCast.exe --render-preview <absolute png path> [idle|busy|dot] [WxH]`. Display inspection: `ReiCast.exe --list-displays <absolute json path>`; keep machine identifiers local.
8. The current transcript reader is a compatibility adapter for local Codex logs, not a stable official API. It detects recent local work and explicit completion. It cannot see remote/cloud tasks and marks long-silent tasks as waiting for a status update.
9. The public avatar is an original MIT-licensed pixel robot. Preserve provenance in `ASSET.md` and preserve any user-selected local portrait during upgrades. Do not publish a user's private avatar, configuration, or dialogue snapshots. Keep the app low overhead: no browser engine, no animated video, no continuous model calls, no hardware monitoring driver.
10. On a black screen after wake, first inspect `runtime.json` (`lastPaint`, `paintCount`, `displayPower`, `powerNotifications`, `recoveryCount`). Use `scripts/repair.ps1` for a bounded HWND and paint recovery. A visible Windows window and fresh paint counter do not prove that the physical D-CAST screen is receiving frames. If restarting/recovering the app does not restore the panel, ask the user to toggle D-CAST off then on in DeepCreative; do not claim an app fix repaired the vendor transport. Do not disable sleep or automatically restart fan-control software as a workaround.

The native app is not a conversational model. It is the visual companion for the user's existing Codex activity.

For Your dot conversation display, use the bundled `dot-display` skill. It publishes actual visible messages through local `dot-dialogue.json`; it does not read cloud chats. `enableDotDialogue` controls display and `dialoguePageSeconds` controls pagination. A user must give the mirroring instruction in Dot and allow local computer access. Do not imply this ordinary chat installed a Dot-wide hook or saved a preference in Dot.
