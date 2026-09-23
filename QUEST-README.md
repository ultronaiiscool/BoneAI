# BoneAI v3 on standalone Quest

BoneAI v3 uses the same `BoneAI.dll` on PCVR and Quest. The optional Quest Codex account mode also needs `libcodex_app_server.so`, an Android ARM64 build from the [BoneAI security branch](https://github.com/ultronaiiscool/codex-termux/tree/quest/boneai-v3-secure) of the Codex fork. No PC is needed while playing.

## Install

1. Install a LemonLoader version and BoneLib build compatible with your exact BONELAB Quest patch. Fusion is needed for multiplayer features; SpawnLab is not required.
2. Copy `Mods/BoneAI.dll` from the v3 package into LemonLoader's `Mods` directory.
3. For Codex account mode, copy `UserLibs/libcodex_app_server.so` and `UserLibs/libcodex_app_server.so.sha256` into LemonLoader's `UserLibs` directory. The library is about 256 MB and BoneAI keeps a private runtime copy on the headset.
4. Launch BONELAB. Open **Preferences → BoneAI → AI Provider → Codex account sign-in**, then **Codex Account → Sign In With Codex**.
5. Enter the one-time code in the official browser sign-in page and return to BONELAB.

The native App Server runs inside BONELAB and keeps Codex account state in the game's private Android files directory. A random per-session WebSocket token protects its localhost connection. The embedded model receives BoneAI's game tools; shell, filesystem, web, plugin, and child-agent tools are not registered in this mode. The one-time code is not an API key. **Sign Out** removes the saved Codex login.

The native library is community built. OpenAI has not released an official Android App Server package. This beta is compile-verified on Android ARM64, but still needs physical Quest and LemonLoader testing. If the native library fails to load, BoneAI keeps running and shows the failure in **Codex Account → Local App Server** and the MelonLoader log.

## Free-model alternative

Open **AI Provider → Free OpenRouter**, enter your own OpenRouter key in **API Key (saved securely)**, and reconnect. The `openrouter/free` router selects available free models. OpenRouter and upstream providers determine rate limits. BoneAI clears the visible key field and retains the encrypted key until **Clear Current API Key** is selected. This mode does not require the native library.

## Voice beta

**Voice · Beta → Open Free Browser Voice** opens a localhost page that waits for your wake word and sends the following command to BoneAI. Browser speech support, service availability, and background behavior depend on Quest Browser; BoneAI has no speech quota. Keep the browser page open.

There is no autoupdater. Quest Codex account mode is experimental until it has been tested on physical hardware.
