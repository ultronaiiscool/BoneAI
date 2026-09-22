# Install BoneAI

1. Install MelonLoader 0.7.3 and BoneLib 3.2.2.
2. On Quest, no PC software is required. On PCVR, install Codex only if you want optional ChatGPT/Codex account login.
3. Install LabFusion 1.14.2 if you want multiplayer features.
4. Install SpawnLab 1.0.1 if you want BoneAI to spawn items, NPCs, props, and vehicles.
5. Put the included universal `BoneAI.dll` in BONELAB's `Mods` folder. It is the same DLL on PCVR and Quest.
6. Start BONELAB and open **Preferences → BoneAI → AI Provider**.
7. On Quest choose **Quest Standalone OpenAI**, enter your API key in-game, and reconnect. The encrypted key remains available until you clear it in BoneAI.
8. On PCVR you may instead use **Codex Sign-In → Sign In With Codex**. Then open **BoneAI → Assistant**.

On PCVR, BoneAI directly starts the installed Codex App Server. There is no Python helper or second BoneAI DLL.

On standalone Quest, install the same DLL through LemonLoader. BoneAI calls OpenAI, Claude, Grok, DeepSeek, OpenRouter, or a compatible custom provider directly over HTTPS. No PC, bridge, or remote BoneAI host is used. Official Codex account login remains PCVR-only because it requires Codex App Server.

To use OpenAI, Claude, Grok, DeepSeek, or OpenRouter, choose it under **Preferences → BoneAI → AI Provider**, enter the provider key in **API Key (saved securely)**, and reconnect. Use **Clear Current API Key** to remove it. Local Ollama needs no secret but is normally useful only on PCVR.

Voice AI is an optional beta. It uses the OpenAI key entered under **AI Provider** and is enabled under **Preferences → BoneAI → Voice AI (Beta)**.

Use Thunderstore/r2modman or the GitHub Releases page for future versions.

For examples, multiplayer details, privacy information, and troubleshooting, read `README.md`.
