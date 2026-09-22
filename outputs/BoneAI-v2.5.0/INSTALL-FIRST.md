# Install BoneAI

1. Install MelonLoader 0.7.3 and BoneLib 3.2.2.
2. Install Python 3 and Codex on PCVR. You can sign in from inside BONELAB after installation.
3. Install LabFusion 1.14.2 if you want multiplayer features.
4. Install SpawnLab 1.0.1 if you want BoneAI to spawn items, NPCs, props, and vehicles.
5. Drag this package's `Mods` and `UserLibs` folders into your BONELAB folder.
6. Start BONELAB and open **Preferences → BoneAI → Codex Sign-In**.
7. Choose **Sign In With Codex**. BoneAI opens the official browser page and copies the one-time code.
8. Complete sign-in, return to BONELAB, then open **BoneAI → Assistant**.

Do not start the Python helper yourself. BoneAI launches it automatically.

On standalone Quest, install through LemonLoader and use the Quest package. Python and Codex App Server do not run on the headset: set a secure `wss://` Codex App Server address and its session connection token under **Codex Sign-In**. The browser login authenticates that server without placing OAuth tokens in the mod.

To use Claude, Grok, DeepSeek, or OpenRouter instead, set the provider's documented API key as a Windows environment variable, choose it under **Preferences → BoneAI → AI Provider**, and reconnect. Local Ollama needs no secret.

Voice AI is an optional beta. It requires `OPENAI_API_KEY` and is enabled under **Preferences → BoneAI → Voice AI (Beta)**. Text chat does not require this key when using Codex.

Use Thunderstore/r2modman or the GitHub Releases page for future versions.

For examples, multiplayer details, privacy information, and troubleshooting, read `README.md`.
