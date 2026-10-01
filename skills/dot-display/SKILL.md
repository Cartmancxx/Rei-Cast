---
name: dot-display
description: Publish the user's visible Your dot messages and replies to their Rei Cast D-CAST extended display through a local snapshot. Use for an explicit Dot-to-screen request, or while following the user's ongoing instruction to mirror Dot replies. This does not retrieve or intercept cloud conversations.
---

# Dot dialogue on Rei Cast

The screen reads `%USERPROFILE%\ReiCast\dot-dialogue.json`. A connected, online computer is required for Dot to publish to this file. This bridge is an explicit local write per reply, not a passive listener to all Dot conversations. Never claim an untested cloud sync works.

When the user has asked Dot to mirror this conversation, publish the latest actual user message and the visible answer being delivered. Do not publish reasoning, tool output, or unrelated chat history. Keep the complete visible text when it fits; the screen pages long answers. Limits are 600 characters for the user message, 4000 for the reply, 16 KB total. If a reply is longer, publish a clearly labeled excerpt and leave the full answer in Dot.

Use local file tools to write a UTF-8 JSON input containing `userMessage`, `reply`, and optionally `ttlSeconds` (default 300, range 10–1800). Do not embed conversation text in a shell command. Run the installed helper at `%USERPROFILE%\ReiCast\app\scripts\publish-dot.ps1` with `-InputFile <absolute input path>` through the connected computer's command tool. The helper writes atomically and timestamps the snapshot. It does not call any model or remote API. Use `source: demo` only for explicitly labeled tests.

Read `%USERPROFILE%\ReiCast\runtime.json` to verify `mode: dot` and `dialogueUpdatedUtc`, with a fresh heartbeat. A painted window does not prove the physical panel is receiving video. If the computer is offline or local tools are unavailable, skip publishing, explain once, and retain the main Dot reply; do not start polling or new agents to retry.

The user's ongoing instruction must be given in their Dot conversation. This skill cannot install a global turn hook or force every Dot turn to invoke it. Tell the user this when enabling the bridge. For native app configuration use the companion `rei-cast` skill or the plugin's README.
