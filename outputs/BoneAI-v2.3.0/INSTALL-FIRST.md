# Install BoneAI

1. Install MelonLoader 0.7.3 and BoneLib 3.2.2.
2. Install Python 3. For the default provider, install the Codex desktop app and sign in to Codex.
3. Install LabFusion 1.14.2 if you want multiplayer features.
4. Install SpawnLab 1.0.1 if you want BoneAI to spawn items, NPCs, props, and vehicles.
5. Drag this package's `Mods`, `UserLibs`, and `Plugins` folders into your BONELAB folder.
6. Start BONELAB and open **Preferences → BoneAI → Assistant**.

Do not start the Python helper yourself. BoneAI launches it automatically.

To use Claude, Grok, DeepSeek, or OpenRouter instead, set the provider's documented API key as a Windows environment variable, choose it under **Preferences → BoneAI → AI Provider**, and reconnect. Local Ollama needs no secret.

Voice AI is an optional beta. It requires `OPENAI_API_KEY` and is enabled under **Preferences → BoneAI → Voice AI (Beta)**. Text chat does not require this key when using Codex.

For examples, multiplayer details, privacy information, and troubleshooting, read `README.md`.
