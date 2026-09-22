# BoneAI for standalone Quest

BoneAI v2.6 uses one managed AnyCPU `BoneAI.dll` on both PCVR and Quest. Install that DLL into the LemonLoader BONELAB `Mods` folder on the headset. The avatar catalog provider is built in; there is no BoneAI `UserLibs` DLL. BoneLib is required; Fusion and SpawnLab are optional unless you want their features. All dependencies must support your exact Quest BONELAB patch.

## Codex sign-in

BoneAI runs on the headset, but Codex App Server must run on a PC/server because it is not available as an Android program.

1. Expose that App Server through a trusted `wss://` endpoint with Codex capability-token authentication.
2. In BONELAB, open **Preferences → BoneAI → Codex Sign-In**.
3. Enter the `wss://` address and the connection token.
4. Choose **Sign In With Codex**.
5. BoneAI copies the one-time code and opens the Quest browser. Enter the code, complete login, and return to BONELAB.

OAuth credentials stay on the App Server host. The connection token remains only in memory on the headset and is cleared when BONELAB closes. BoneAI rejects remote unencrypted `ws://` connections unless you explicitly enable the private-LAN testing option.

The universal DLL does not include or launch Python on Quest and contains no updater. Hardware validation still requires a headset with matching Quest game/mod assemblies; report the exact BONELAB, LemonLoader, BoneLib, Fusion, and SpawnLab versions with any issue.
