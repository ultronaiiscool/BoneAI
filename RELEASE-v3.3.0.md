# BoneAI v3.3.0 — More Voice Paths

This release keeps the v3.2 Codex model picker, gameplay optimizations, spawn catalog, Fusion integration, and all existing tools. Its new work is in the optional Browser Voice beta.

Automatic recognition now attempts browser on-device speech first, then the browser speech service, then Groq Whisper, then Cloudflare Whisper. The latter two are used only when you add their own credentials under **Preferences → BoneAI → Voice · Beta**. Groq needs a Groq API key; Cloudflare needs a Workers AI token and Account ID. Keys stay in BoneAI's protected store and are never sent to the local browser page. The browser sends short WAV recordings to BoneAI's loopback bridge, which forwards them to the selected provider over HTTPS. The typed-command box always remains available.

Groq and Cloudflare offer limited free allocations, not unlimited service. Third-party availability, browser microphone support, and Quest background-browser behavior vary. This release was compiled and source-tested; it has not been verified in a live PCVR or Quest session. The Android native Codex library is unchanged, and no autoupdater is included.

Install `Mods/BoneAI.dll` for PCVR. Standalone Quest additionally needs `UserLibs/libcodex_app_server.so` from the Quest bundle for Codex account sign-in. BoneLib is required; Fusion is optional. Restart BONELAB after replacing the DLL.
