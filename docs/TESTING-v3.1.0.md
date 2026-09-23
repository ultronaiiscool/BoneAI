# BoneAI v3.1.0 spawn testing

This checklist is for a physical PCVR or Quest run and a two-client Fusion session. Compilation and unit tests alone do not prove in-game outcomes. Record BONELAB, MelonLoader/LemonLoader, BoneLib, and Fusion versions with any report.

## Offline

- [ ] Run with SpawnLab absent or disabled. Confirm BoneAI loads and `spawn.list` returns loaded base-game and installed-mod spawnables.
- [ ] Try `spawn.refresh` after installing a content mod and loading the scene.
- [ ] Search for a duplicate name; BoneAI should request an exact barcode rather than choosing arbitrarily.
- [ ] Spawn by exact barcode in front of the player and at an explicit nearby position. Confirm the initial result is pending, then `spawn.status` reports a local object handle after the callback.
- [ ] Try an invalid barcode or non-finite position. Confirm a clear failure and no object.
- [ ] Repeatedly spawn objects and inspect memory/performance; the catalog should not rescan every frame.

## Fusion host and client

- [ ] Join a networked level with two clients. Spawn once from the host and once from a client allowed to use dev tools. Verify each item on **both** clients and record the network entity ID from `spawn.status` where available.
- [ ] Set lobby dev-tool permission below the client's role or start a gamemode that disables the spawn gun. Confirm BoneAI refuses the request.
- [ ] Turn off BoneAI's Fusion Synchronization preference while online. Confirm spawning is refused rather than happening only locally.
- [ ] Try a blacklisted/singleplayer-only spawnable if safe. Confirm no false success; an unanswered request should become unconfirmed after two minutes.
- [ ] Disconnect during a request and check that no crash, runaway callback queue, or false peer-confirmation claim occurs.
- [ ] Repeat with a second Fusion client that does not have BoneAI. The spawn path uses ordinary Fusion messages, but peer observation must still be checked.

No result labelled `confirmed_local` proves peer visibility. Capture both clients' logs and screenshots for synchronization failures; remove any login codes or secrets before sharing.
