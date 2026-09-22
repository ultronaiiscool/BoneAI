# BoneAI for standalone Quest

This package contains the managed AnyCPU BoneAI build for LemonLoader. Install its `Mods` and `UserLibs` folders into the matching LemonLoader BONELAB data folder on the headset. BoneLib is required; Fusion and SpawnLab are optional unless you want their features. All dependencies must support your exact Quest BONELAB patch.

## Codex sign-in

BoneAI runs on the headset, but Codex App Server must run on a PC/server because it is not available as an Android program.

1. Expose that App Server through a trusted `wss://` endpoint with Codex capability-token authentication.
2. In BONELAB, open **Preferences → BoneAI → Codex Sign-In**.
3. Enter the `wss://` address and the connection token.
4. Choose **Sign In With Codex**.
5. BoneAI copies the one-time code and opens the Quest browser. Enter the code, complete login, and return to BONELAB.

OAuth credentials stay on the App Server host. The connection token remains only in memory on the headset and is cleared when BONELAB closes. BoneAI rejects remote unencrypted `ws://` connections unless you explicitly enable the private-LAN testing option.

The Quest build does not include or launch Python and contains no updater. Hardware validation still requires a headset with matching Quest game/mod assemblies; report the exact BONELAB, LemonLoader, BoneLib, Fusion, and SpawnLab versions with any issue.
