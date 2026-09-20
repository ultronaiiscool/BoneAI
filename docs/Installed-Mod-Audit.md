# Installed mod DLL audit

Audit date: 2026-09-20. Target: BONELAB 1.744.58126, MelonLoader 0.7.3, BoneLib 3.2.2, LabFusion 1.14.2.

All DLLs in the target `Mods` directory were inventoried and their managed type surfaces inspected. This report separates useful agent integrations from DLLs that should remain isolated.

| DLL | Version | Finding for the AI agent |
|---|---:|---|
| AudioImportLib.dll | 1.0.0 | Audio support library; no gameplay-control surface needed. |
| BONELABFusionKillerMode.dll | 0.3.20 | Gamemode, map, door, item-spawn, and spectator systems. Not exposed: mutating an active multiplayer gamemode would violate authority and user expectations. |
| BoneLib.dll | 3.2.2 | Used directly for player references, BoneMenu, and notifications. |
| Echolocation.dll | 0.0.0 | Contains a good reflection pattern for Fusion remote-player rigs, but its target cache is internal. The agent queries Fusion directly instead. |
| FlappyLab.dll | 1.0.0 | Self-contained minigame; no general control API. |
| Force Pull Anything.dll | 1.0.0 | Intended to patch every Grip with ForcePullGrip. The smoke test shows its old `SLZ.Interaction.Grip` reference fails on this game build, so the agent does not depend on it. |
| ForeverMortal.dll | 2.0.1 | Health/death mod. Smoke test fails on an old BoneLib `OnLevelInitialized` method, so it is not integrated. |
| LabFusion.dll | 1.14.2 | Used for session/player discovery. SpawnLab uses its real NetworkAssetSpawner; normal grips, guns, seats, avatars, NPCs, and pooled despawns retain Fusion patches. |
| LaserEyes.dll | 0.0.0 | Combat effect. Smoke test reports an unstripping failure; not integrated. |
| ModioModNetworker.dll | 2.8.1 | Observes Fusion spawn traffic and mod availability. It continues to work automatically; the AI is not given its downloader/filesystem surface. |
| NoIntro.dll | 1.0.0 | Startup-only convenience; no agent capability. |
| NukesFusionServerUtilities.dll | 1.0.4 | Server panic/admin behavior. Deliberately excluded from AI actions. |
| Portals.dll | 1.6.0 | Public portal spawning exists, but it uses a local AssetSpawner path and is not claimed multiplayer-safe. Excluded until a verified network protocol exists. |
| PowerTools.dll | 1.1.0 | Gravity, ammo, ragdoll, vaulting, and death options. Several Harmony patches fail against old `SLZ.*` types in the smoke test. Not integrated. |
| QuickItem.dll | 1.0.0 | Has a correct Fusion-aware saved-item spawner, but duplicates SpawnLab and exposes its useful methods privately. SpawnLab is the selected provider. |
| SpawnLab.dll | 1.0.0 | Fully integrated. The agent reads its real catalog entries and invokes its real private Spawn entrypoint. SpawnLab chooses NetworkAssetSpawner online and LocalAssetSpawner offline. |
| Stat_Changer__Simple_Edition_.dll | 1.0.0 | Confirms rig fields such as speedMultiplier/jumpMultiplier and avatar strength. The agent uses direct runtime overrides and does not alter this mod's menu state. |
| StrengthMod.dll | 1.0.0 | Patches Avatar.ComputeBaseStats with strength/agility multipliers. The agent's runtime values coexist but do not rewrite saved StrengthMod preferences. |
| UltimateRagdoll.dll | 1.0.0 | Player ragdoll/footstep behavior; not exposed because generalized safe recovery was not verifiable. |
| WristHub.Core.dll | 4.4.7 | Very large UI, download, Fusion voice/admin, portal, avatar, filesystem, and persistence surface. Deliberately isolated; only assembly metadata can be reported. |
| WristHub.dll | 4.4.7 | Universal loader for WristHub.Core; no direct agent integration. |

## Smoke-test outcome

The compiled `BonelabAIAgent.dll` was installed and loaded in a hidden `-batchmode -nographics -quit` run. The log confirmed:

- BONELAB AI Agent 1.0.0 registered.
- Unity 2021.3.16f1 and BONELAB build 1.744.58126 detected.
- Fusion 1.14.2 bridge detected.
- SpawnLab 1.0.0 initialized.
- Codex connection refusal was caught and reported cleanly because the bridge was intentionally not running.

Errors listed above for other mods pre-existed independently of the AI agent. No AI-agent exception or static initialization failure appeared in the smoke-test log.
