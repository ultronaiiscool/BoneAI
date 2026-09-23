# BoneAI v3.0.1 — Reliability update

This release keeps the v3 PCVR/standalone Quest architecture and its game-tool catalog. It fixes queued-action cancellation, stops reporting unconfirmed game requests as completed, checks local Fusion object ownership before direct manipulation, and avoids choosing the wrong spawnable when a catalog search is ambiguous. Core tools now provide typed argument schemas; tool selection and action-history memory use are improved.

The Quest ZIP contains `Mods/BoneAI.dll`, `UserLibs/libcodex_app_server.so`, license notices, the pinned native source archive, and installation guides. The native library is the unchanged, verified v3.0.0 Android build. Install the DLL and native library using the directory layout in the ZIP; install matching BoneLib, Fusion, and SpawnLab separately. There is no autoupdater.

An action marked `pending` was requested but is not confirmed. Verify it in the game or with a second Fusion client before treating it as complete. Build and automated source tests passed, but this release has **not** been validated on a physical Quest or with two live Fusion clients. Follow `docs/TESTING-v3.0.1.md` and report reproducible issues with logs, versions, and the action ID.

The release ZIP and DLL SHA-256 checksums are attached separately.
