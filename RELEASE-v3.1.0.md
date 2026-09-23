# BoneAI v3.1.0 — Built-in spawning

BoneAI no longer requires SpawnLab. Its own spawn catalog reads the live BONELAB Marrow warehouse, including loaded base-game and installed-mod spawnables. The AI can search by name or barcode, ask for an exact barcode when names are ambiguous, specify a spawn position/rotation, and check a request through `spawn.status`. A new **Spawn Catalog** page under BoneAI Preferences supports manual search, spawn, and status checks.

Offline, BoneAI uses BONELAB's asset spawner. In a Fusion networked level it uses Fusion's existing server-routed `NetworkAssetSpawner` request and checks the local player's spawn-gun permissions first. It refuses an online local-only spawn when Fusion synchronization is unavailable or disabled. A local callback can confirm the initiating client received the spawned object and network entity ID; it **cannot** prove every peer saw it. Test with two clients before relying on multiplayer behavior.

The Quest ZIP includes the universal `Mods/BoneAI.dll`, the same pinned v3 Android Codex App Server library under `UserLibs`, native source archive, licenses, setup instructions, and tests. BoneLib remains required; Fusion is required only for multiplayer. SpawnLab's DLL and decompiled source are **not** bundled or copied into BoneAI. No autoupdater is included.

The managed DLL compiled against the installed BONELAB/BoneLib/Fusion stack, and source smoke tests passed. This release has not been validated on physical Quest hardware or in a live two-client Fusion session. See `TESTING-v3.1.0.md` in the ZIP.
