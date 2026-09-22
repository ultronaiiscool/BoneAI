# BoneAI for standalone Quest — no PC

BoneAI v2.7.0 uses one managed AnyCPU `BoneAI.dll` on both PCVR and Quest. Install that DLL into the LemonLoader BONELAB `Mods` folder on the headset. BoneLib is required; Fusion and SpawnLab are optional unless you want their features. All dependencies must support your exact Quest BONELAB patch.

## Connect directly from the headset

1. Start BONELAB and open **Preferences → BoneAI → AI Provider**.
2. Choose **Free OpenRouter** (the default for new Quest installs) or select another supported provider.
3. Enter your OpenRouter key in **API Key (saved securely)**. The `openrouter/free` router uses currently available free models; OpenRouter/upstream rate limits still apply.
4. Select **Reconnect**, then open **Assistant**.

BoneAI sends HTTPS requests directly from Quest, executes returned game tools on the headset, and continues the tool loop there. No PC, Python bridge, remote BoneAI host, or Codex CLI is involved. Keys are cleared from the visible field, stored with authenticated device-bound encryption, never logged, and remain available until **Clear Current API Key** is selected.

Codex browser/account login is PCVR-only because the supported flow is managed by a continuously running Codex App Server and OpenAI does not publish an Android/Quest App Server target. The browser code alone cannot run Codex. Quest standalone instead defaults to OpenRouter Free with a user-provided OpenRouter key.

For keyless speech-to-text, choose **Voice AI (Beta) → Open Free Browser Voice**. The local page uses Quest Browser's SpeechRecognition implementation and waits for the configured wake word. Keep the page open; background suspension and browser speech-service availability are controlled by Quest Browser, so this remains beta.

The DLL contains no updater. Hardware validation still requires a headset with matching Quest game/mod assemblies; report the exact BONELAB, LemonLoader, BoneLib, Fusion, and SpawnLab versions with any issue.
