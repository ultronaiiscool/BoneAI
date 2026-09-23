# BoneAI v3.2.0 — Model choice and smoother gameplay

BoneAI retains all v3.1.1 features and adds a Codex model picker under **Codex Account → Choose Codex Model**. It reads the signed-in App Server's model catalog, supports search and browsing older/hidden entries, and saves the selected model and advertised default reasoning effort. Availability depends on the user's Codex account and server; no model is hard-coded as guaranteed.

Browser voice now has **Automatic** (on-device first where supported, browser service otherwise), **On-device** (local-only), and **Browser speech service** modes, plus the typed-command fallback. BoneAI does not bundle a speech model; offline recognition requires browser support and may need a one-time model download. A browser network error may still occur if no local option is available.

Gameplay responsiveness changes bound main-thread dispatch work and world scans, refresh the spawn catalog in per-frame batches, reduce routine hierarchy inspection, move avatar manifest refresh and audio encoding off the game thread, throttle movement teleports, and cap high-impact gun/physics actions. A slow tool logs its duration for diagnosis. These changes were built and source-tested, but physical VR and two-client Fusion performance still require testing; see `TESTING-v3.2.0.md`.

The ZIP includes `Mods/BoneAI.dll`, the unchanged pinned v3 Android Codex library in `UserLibs`, native source archive, licenses, and setup documentation. BoneLib is required; Fusion remains optional for multiplayer. No autoupdater or SpawnLab DLL is included.
