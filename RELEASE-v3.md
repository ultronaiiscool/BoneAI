# BoneAI v3.0.0

BoneAI v3 combines the PCVR mod with a standalone Quest Codex account mode. The same managed `BoneAI.dll` works on both platforms; Quest Codex mode adds an Android ARM64 native App Server library. There is no PC, Python bridge, or autoupdater required for Quest play.

## Included

- Secure Quest App Server with a per-session localhost bearer token, private account storage, native ABI check, and game-tool-only model access.
- Persistent Codex device-code sign-in and sign-out on PCVR and Quest.
- BoneAI's independent Preferences menu, saved conversations, voice beta, OpenRouter Free and other supported providers, SpawnLab catalog tools, Fusion-aware tools, and the existing game-action catalog.
- The pinned native Rust source/build snapshot, license notices, checksum, and a reproducible package script.

## Install

Extract the release ZIP. Copy `Mods/BoneAI.dll` into BONELAB's `Mods` folder. On Quest, copy `UserLibs/libcodex_app_server.so` and its `.sha256` file into LemonLoader's `UserLibs` folder if you want Codex account sign-in. Install the separately maintained BoneLib, Fusion, and SpawnLab dependencies appropriate for your BONELAB platform and version. See `INSTALL-FIRST.md` and `QUEST-README.md` in the ZIP for the full steps.

## Verification and limits

The managed DLL builds against the inspected local PCVR BONELAB assemblies with zero warnings. The native library is produced by the pinned Android ARM64 CI build, with ELF/export checks. Physical Quest/LemonLoader gameplay and account-flow testing are still needed; treat Quest Codex mode as experimental despite this being a normal v3 release. Fusion synchronization remains limited to actions supported by Fusion's verified paths; BoneAI does not claim that local-only modifications synchronize to other players. Voice remains beta and browser recognition depends on the selected browser and its speech service.

This release does not replace or modify any currently installed BoneAI DLL automatically.
