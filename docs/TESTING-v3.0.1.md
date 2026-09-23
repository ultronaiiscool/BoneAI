# BoneAI v3.0.1 physical test checklist

Automated source tests and a PC-targeted compile cannot prove Quest runtime behavior or multiplayer replication. Test with a disposable save/session first. Record BONELAB, LemonLoader/MelonLoader, BoneLib, Fusion, and SpawnLab versions and retain `Latest.log` when reporting failures.

## PCVR and standalone Quest

- [ ] Start offline. Confirm the BoneAI Preferences entry opens without an exception and the version is 3.0.1.
- [ ] Sign in with Codex, restart BONELAB, and confirm sign-in persistence. Log out and confirm the session is cleared. Do not share credentials or full token-bearing logs.
- [ ] Send a plain chat prompt. Start a second prompt, cancel it while queued, and confirm it does not later change the game.
- [ ] Search the avatar and SpawnLab catalogs. Try an ambiguous spawn name: BoneAI should ask for a more specific match, not spawn an arbitrary item.
- [ ] Spawn one item. The initial response may be `pending`; then use a world query and visually verify the item appears.
- [ ] Pick up and release a nearby item. Check the hand/object state; if the action is `pending`, do not treat it as confirmed.
- [ ] Attempt an unavailable object/action. It should fail cleanly without crashing or fabricating success.
- [ ] Close Preferences and confirm chat/action processing continues.
- [ ] On Quest, restart the headset/game and verify native App Server startup and device-code sign-in without a PC. Check that the DLL and `.so` are installed in the ZIP's `Mods` and `UserLibs` locations.

## Fusion two-client test

- [ ] Join the same session with two test clients, one host and one client. Record which peer has BoneAI installed.
- [ ] Spawn an item from the host and then client. Observe whether each peer sees the same object and whether Fusion assigns expected ownership.
- [ ] Grab, release, fire, change avatar, sit, and damage a test player. Check each outcome on **both** clients, not only the initiating client.
- [ ] Attempt direct manipulation of an entity owned by the other peer. BoneAI should refuse it.
- [ ] Disconnect a peer during a pending request; verify no crash and no unearned success message.
- [ ] Repeat a basic session with one non-BoneAI Fusion client. No custom BoneAI networking is required by the current implementation, but interoperability still needs observation.

Do not mark a category as synchronized solely because its local tool call returned. Capture the action ID, local result, and both clients' observed state for a bug report.
