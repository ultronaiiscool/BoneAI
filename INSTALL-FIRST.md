# Install BoneAI v3

1. Install BONELAB, a compatible MelonLoader (PCVR) or LemonLoader (Quest), and BoneLib.
2. Copy `Mods/BoneAI.dll` from the v3 package into your game's `Mods` directory. It is one universal managed DLL for PCVR and Quest.
3. On Quest, if you want Codex account sign-in, also copy the package's `UserLibs/libcodex_app_server.so` and `.sha256` file into LemonLoader's `UserLibs` directory.
4. Install Fusion for multiplayer features and SpawnLab for spawning. BoneAI can load without either; their actions report unavailable when absent.
5. Open **Preferences → BoneAI → AI Provider**. Choose **Codex account sign-in** or **Free OpenRouter**.
6. For Codex, use **Codex Account → Sign In With Codex** and finish the one-time browser code. On PCVR, install the Codex CLI/App Server first. On Quest, the bundled native library runs inside BONELAB.
7. For OpenRouter Free, enter your own OpenRouter key under **API Key (saved securely)** and reconnect.
8. Use **Assistant · Home** to send a prompt. The prompt field clears when sent.

No Python bridge or autoupdater is included. The Android native library is a community build; Quest Codex account mode still needs physical headset validation. See `README.md` and `QUEST-README.md` for setup, limitations, and troubleshooting.
