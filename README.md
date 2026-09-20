# BONELAB AI Agent 1.0.3

BONELAB AI Agent is a PCVR MelonLoader/BoneLib mod that connects BONELAB to the official local Codex App Server. It provides persistent conversational turns, streamed responses, structured game actions, permission controls, compact world perception, stable object handles, and optional use of Fusion's existing replication paths.

## Verified target

- BONELAB: `1.744.58126` (Unity `2021.3.16f1`)
- MelonLoader: `0.7.3` Open Beta, .NET 6 runtime
- BoneLib: `3.2.2`
- LabFusion: `1.14.2` (optional; the mod still loads offline)
- Platform: 64-bit Windows PCVR

The build was compiled directly against the assemblies from this exact installed stack. Other game patches may change IL2CPP APIs.

## Install and run

1. Ensure MelonLoader 0.7.3 and BoneLib 3.2.2 are already installed.
2. Copy both `BonelabAIAgent.dll` and `start_codex_bridge.py` into BONELAB's `Mods` folder. They must remain beside one another.
3. Sign into the Codex desktop app/CLI with your ChatGPT account. No API key or token is stored by this mod.
4. Start BONELAB. The DLL checks `127.0.0.1:4500`, automatically launches the adjacent Python script in a hidden process when needed, waits for readiness, and then connects. Open **BoneMenu → AI Agent**, enter a prompt, then select **Send**.

The script uses only Python's standard library. The DLL supplies a fixed port and BONELAB parent PID. The script first searches `PATH`, then the Codex desktop installation under `%LOCALAPPDATA%\\OpenAI\\Codex\\bin`, and starts `codex app-server --listen ws://127.0.0.1:4500`. The child process is stopped when the owning BONELAB process exits. Disable **Auto-start Python Bridge** in BoneMenu or set `AutoStartPythonBridge=false` in MelonPreferences to manage it manually.

The WebSocket binds only to `127.0.0.1`. Authentication remains owned by the installed Codex client. The mod does not read, receive, store, or log credentials.

## BoneMenu

The menu contains prompt input, Send, Cancel Request, Reconnect, New Conversation, permission switches, Fusion synchronization preference, debug logging, connection status, current action, the latest response, and an in-headset response notification.

## Implemented structured tools

The agent receives an explicit catalog rather than relying on an in-DLL natural-language command parser.

- Player: state, position, rotation, avatar, held inventory, teleport, current health, heal, damage, avatar swap by barcode, runtime strength/grip/speed/agility/vitality overrides, restore overrides.
- World: scene info, nearby compact object listing, fuzzy object search, detailed object inspection, stable per-session handles.
- Spawn: SpawnLab's full base-game/downloaded pallet catalog, fuzzy matching, refresh, Fusion-aware item/NPC/prop/vehicle spawning, pooled despawn.
- Interaction: grip-aware grab, release, pull to hand, use/activate, button/lever/open/close dispatch when the target exposes a real public interaction entry point, push and throw.
- Physics: force, impulse, velocity, physics position, physics rotation.
- Combat: aim held item, fire real gun components, reload through the gun API, target damage when a real damage receiver is present, hit/punch/kick impulses.
- Movement: move/navigate to position, follow object, stop, turn, jump impulse.
- Vehicle: enter a real Seat and exit the active Seat.
- Fusion: session, host/client state, players, and synchronization report.
- Diagnostics: loaded assemblies/mods, recent MelonLoader error lines, BoneLib headset notification.

Every tool returns `success`, `failed`, or `cancelled`, an action ID, result data, and a failure reason when applicable. Missing/disappeared objects and unsupported interaction components fail explicitly. A request is limited to 12 action rounds and can be cancelled.

## Fusion synchronization

The mod does not claim that a local transform is networked. With Fusion 1.14.2 loaded, these normal game paths are already patched and replicated by Fusion:

| Action category | Path used | Other clients need AI Agent? |
|---|---|---|
| Asset spawn | Installed SpawnLab 1.0.0: Fusion `NetworkAssetSpawner` online, Fusion `LocalAssetSpawner` offline | No |
| Grip/release | Marrow `Grip.Snatch` / `Hand.DetachObject` | No |
| Gun fire | Real `Gun.Fire` path | No |
| Seat enter/exit | Real `Seat` methods | No |
| Avatar swap | `RigManager.SwapAvatarCrate` | No |
| Networked prop physics | Real rigidbody/entity ownership path; final authority still belongs to Fusion | No |
| NPC damage/death | Real target damage/puppet path when the component exposes it | No |

Local health, strength, speed, agility, vitality, Codex conversation state, UI, and incremental local player movement are intentionally local-only. This release does **not** introduce a custom peer RPC protocol; consequently it neither trusts remote AI requests nor pretends those local overrides replicate. `Fusion Synchronization` is a safety preference and status control, not a bypass of Fusion host/ownership rules.

## Safety and prompt injection

Only text submitted through the local AI Agent prompt authorizes actions. Player names, server names, object names, map text, mod descriptions, logs, and other world data are labeled untrusted context. The action dispatcher exposes only its registered BONELAB tools. It exposes no shell, process execution, arbitrary file writes/deletion, credential access, DLL loading, or general network-request tool. Startup may execute only the fixed adjacent `start_codex_bridge.py` file; its path and arguments cannot be controlled by AI prompts.

The log reader is fixed to `MelonLoader/Latest.log`. The Codex endpoint defaults to localhost. Never change it to an untrusted remote server.

## Performance

Nearby-world scans occur only for context/tool requests, never every frame. Results are capped. Object handles and the spawn catalog source are reused. WebSocket traffic runs off the Unity main thread; Unity/Marrow actions are dispatched back to the main thread. Movement/follow updates perform one small operation per frame.

## Troubleshooting

- **Unavailable / connection refused:** confirm Python and Codex are on `PATH`, confirm `start_codex_bridge.py` is beside the DLL, and inspect `[AIAgent] Python bridge` lines. You can also check `http://127.0.0.1:4500/readyz` locally.
- **No response:** verify Codex is signed in, select Reconnect, and inspect `MelonLoader/Latest.log` for `[AIAgent]`.
- **Spawnable not found:** select SpawnLab's Refresh List once or ask the agent to use `spawn.refresh`, then use `spawn.list`. SpawnLab reads both base-game and downloaded pallet files.
- **Object handle expired:** repeat the nearby search. Handles are intentionally invalidated when Unity destroys the object or changes level.
- **Fusion action is local:** check `fusion.get_sync_report`. Local player-stat overrides and AI state are not networked.
- **Interaction fails:** the target did not expose one of the verified public interaction methods. Use physical manipulation or a grip instead.

## Known limitations

- Text input is the core interface. Speech-to-text is not bundled.
- Navigation is straight-line, collision-unaware locomotion; it is not a level navmesh planner and does not automate climbing or crouching.
- Vehicle throttle/steering varies by vehicle rig and is not generalized in this release; entering and exiting standard Marrow seats is supported.
- Generic doors/buttons/levers vary across modded levels. The agent invokes real public interaction methods when present or can manipulate their rigidbodies; it reports failure otherwise.
- Player-stat overrides are runtime changes and are deliberately restored on request rather than modifying avatar crate assets.
- A spawned object callback can complete after the first tool response; the object appears in the next nearby query even if the immediate result says `pending`.
- Runtime validation still requires launching the user's VR game and exercising the relevant content; a successful build cannot prove behavior of every third-party spawnable or level object.

## Build from source

Install .NET SDK 6 and set the installation location without committing a machine-specific path:

```powershell
dotnet build BonelabAIAgent.csproj -c Release -p:BONELAB_DIR="D:\Games\BONELAB"
```

The project references MelonLoader, BoneLib, Unity, and generated IL2CPP assemblies from that installation. Build output is `bin/Release/net6.0/BonelabAIAgent.dll`.

## Architecture

- `AI`: Codex App Server WebSocket client and structured multi-turn coordinator.
- `Tools`: registry, action states, main-thread dispatch, and authoritative results.
- `Game`: player/world/spawn/interaction/combat/movement/physics/vehicle adapters.
- `Fusion`: version-isolated optional Fusion discovery and reporting.
- `UI`: BoneMenu controls and headset notifications.
- `Infrastructure`: configuration, logging, and Unity main-thread queue.

The supported Codex transport follows the official [Codex App Server documentation](https://developers.openai.com/codex/app-server).

## Installed DLL audit

All 21 DLLs present in the target `Mods` folder were inventoried by assembly metadata and public type inspection. The agent uses or safely benefits from:

- **SpawnLab 1.0.0:** direct private-entry bridge to its real catalog and spawn method. This is the sole spawn provider; it preserves SpawnLab's verified online/offline decision.
- **LabFusion 1.14.2:** session/player inspection and existing replication patches.
- **BoneLib 3.2.2:** player access, BoneMenu, notifications, and Marrow helpers.
- **Force Pull Anything 1.0.0:** its automatic `Grip` patch makes agent grip actions compatible without a second integration.
- **StrengthMod / Stat Changer:** their implementation confirmed the active avatar and rig multiplier fields used for runtime player controls. The agent does not rewrite either mod's saved settings.
- **ModioModNetworker:** continues to observe ordinary Fusion spawn traffic generated by SpawnLab; no private network messages are injected.

QuickItem duplicates spawning and was not used because SpawnLab is the requested, more complete provider. PowerTools, Portals, Echolocation, WristHub, Killer Mode, Nukes server utilities, UltimateRagdoll, LaserEyes, ForeverMortal, FlappyLab, NoIntro, AudioImportLib, and their internal/private surfaces are detected and reported by `mods.get_capabilities`, but are not remotely toggled. That avoids hidden global cheats, server administration, filesystem/download operations, gamemode mutation, and non-network-safe portal state entering the AI permission surface.
