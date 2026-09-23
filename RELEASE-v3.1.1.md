# BoneAI v3.1.1 — Browser voice recovery

This patch retains all v3.1.0 features, including the built-in spawn catalog and Fusion-aware spawning. It fixes the browser voice page's response to `Speech error: network`: the page no longer loops on a failed browser speech service. Where supported, players can choose on-device speech recognition and install the browser's language model. If speech is unavailable, commands can be typed on the same page.

The `network` error originates in the browser's speech-recognition service, not the BoneAI bridge. On-device speech is experimental and browser-dependent. Neither browser service nor Quest background listening is guaranteed. The typed-command fallback remains available without speech recognition.

The ZIP contains the universal `Mods/BoneAI.dll`, the unchanged pinned v3 Android Codex App Server library under `UserLibs`, native source archive and licenses, and setup instructions. BoneLib is required; Fusion is optional for multiplayer. No autoupdater or SpawnLab DLL is included.

The managed DLL compiled against the installed BONELAB/BoneLib/Fusion assemblies and the browser-page behavior tests passed. Physical Quest and two-client Fusion tests remain outstanding; see `TESTING-v3.1.1.md`.
