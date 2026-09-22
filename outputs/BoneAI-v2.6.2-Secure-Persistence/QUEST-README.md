# BoneAI for standalone Quest — no PC

BoneAI v2.6.2 uses one managed AnyCPU `BoneAI.dll` on both PCVR and Quest. Install that DLL into the LemonLoader BONELAB `Mods` folder on the headset. BoneLib is required; Fusion and SpawnLab are optional unless you want their features. All dependencies must support your exact Quest BONELAB patch.

## Connect directly from the headset

1. Start BONELAB and open **Preferences → BoneAI → AI Provider**.
2. Choose **Quest Standalone OpenAI** or select another supported cloud provider.
3. Enter the provider API key in **API Key (saved securely)**.
4. Select **Reconnect**, then open **Assistant**.

BoneAI sends HTTPS requests directly from Quest, executes returned game tools on the headset, and continues the tool loop there. No PC, Python bridge, remote BoneAI host, or Codex CLI is involved. Keys are cleared from the visible field, stored with authenticated device-bound encryption, never logged, and remain available until **Clear Current API Key** is selected.

Codex browser/account login is PCVR-only because the officially supported flow is managed by Codex App Server and OpenAI does not publish an Android/Quest App Server runtime. Quest standalone instead uses OpenAI's official Responses API with an API key, or another supported provider's official API.

The DLL contains no updater. Hardware validation still requires a headset with matching Quest game/mod assemblies; report the exact BONELAB, LemonLoader, BoneLib, Fusion, and SpawnLab versions with any issue.
