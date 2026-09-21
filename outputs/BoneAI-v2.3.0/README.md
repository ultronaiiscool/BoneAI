# BoneAI

<p align="center">
  <img src="https://raw.githubusercontent.com/ultronaiiscool/BoneAI/main/assets/logo.png" alt="BoneAI logo" width="520">
</p>

**Talk to BONELAB. BoneAI looks around, understands your request, and uses real game actions to carry it out.**

Ask normal questions or give commands such as:

> Spawn a Ford in front of me.

> Find the nearest pistol, bring it to my right hand, and reload it.

> Follow player 2.

> Change me to my Morty avatar and make me stronger.

BoneAI is a Windows PCVR MelonLoader/BoneLib mod. Codex remains the default and uses your existing ChatGPT/Codex sign-in. Version 2.3 keeps every v2.2 provider and updater feature, expands the internal catalog to 350 structured tools, adds a standalone Preferences interface with saved conversations, and introduces opt-in voice input/output with a configurable wake word as a clearly marked beta.

## Start here

### What you need

- BONELAB PCVR `1.744.58126`
- MelonLoader `0.7.3` using the .NET 6 runtime
- BoneLib `3.2.2`
- Python 3 available as `python`, `python.exe`, or `py`
- The Codex desktop app or Codex CLI, signed in with your ChatGPT account
- LabFusion `1.14.2` for multiplayer features (optional for offline play)
- SpawnLab `1.0.1` for spawning (BoneAI still loads without it, but spawn tools report that the provider is unavailable)

Only BoneAI's own files are included in this repository and release. BoneLib, Fusion, SpawnLab, Python, BONELAB, and Codex belong to their respective authors and must be installed separately.

### Install BoneAI

1. Download the latest `BoneAI-v*.zip` from Releases.
2. Open the ZIP. You will see `Mods`, `UserLibs`, and `Plugins` folders.
3. Drag all three folders into your BONELAB folder and allow Windows to merge them.
4. Make sure Codex is installed, open it once, and sign in.
5. Start BONELAB.
6. Open BONELAB **Preferences → BoneAI → Assistant**, type into **Prompt**, and select **Send**.

That is the complete BoneAI install. You do not need to launch the bridge yourself: `BoneAI.dll` automatically starts the adjacent `start_boneai_bridge.py` helper when BONELAB launches.

Your BONELAB folder should contain:

```text
BONELAB/
├─ Mods/
│  ├─ BoneLib.dll
│  ├─ LabFusion.dll                 (optional, for multiplayer)
│  ├─ SpawnLab.dll                  (required only for spawning)
│  ├─ BoneAI.dll
│  └─ start_boneai_bridge.py
├─ UserLibs/
│  └─ BoneAI.Catalogs.dll
└─ Plugins/
   └─ BoneAI.Updater.dll
```

## AI providers

Use **Preferences → BoneAI → AI Provider**, choose **Change Provider**, edit **Model** if desired, and select **Reconnect**. Provider selection and model names are saved in `UserData/MelonPreferences.cfg`.

| Provider | Authentication | Default endpoint |
|---|---|---|
| Codex | ChatGPT/Codex sign-in; no key in BoneAI | Local Codex App Server |
| Claude | `ANTHROPIC_API_KEY` | `https://api.anthropic.com/v1/messages` |
| Grok | `XAI_API_KEY` | `https://api.x.ai/v1/chat/completions` |
| DeepSeek | `DEEPSEEK_API_KEY` | `https://api.deepseek.com/chat/completions` |
| OpenRouter | `OPENROUTER_API_KEY` | `https://openrouter.ai/api/v1/chat/completions` |
| Ollama | No secret required | `http://127.0.0.1:11434/v1/chat/completions` |
| Custom | `BONEAI_API_KEY` | Set **Custom Base URL** |

Set keys as Windows user environment variables, then restart BONELAB. BoneAI never saves or logs them. Claude uses Anthropic's native Messages/tool-use protocol. Grok, DeepSeek, OpenRouter, Ollama, and Custom use their documented OpenAI-compatible function-calling protocol. The chosen model must support tools; OpenRouter support depends on the routed model, and Ollama requires the named model to be installed locally.

Codex is the only provider that uses account login. Consumer Claude/Grok/DeepSeek logins are not reused because their official third-party API authentication uses API keys.

## Automatic updates

`Plugins/BoneAI.Updater.dll` checks this repository's latest non-draft GitHub Release once at startup. When a newer release exists, it downloads the matching ZIP, verifies its published SHA-256 digest, and stages it. After BONELAB closes, the Python helper validates an allowlist of package paths, backs up existing BoneAI files under `UserData/BoneAI/Backup`, and installs the update. A failed check, missing digest, hash mismatch, unexpected file, or network outage only disables that update; it does not stop BoneAI.

## First conversation

BoneAI enables game actions by default. For a quick check:

1. Open **Preferences → BoneAI → Assistant**.
2. Confirm **Connection Status** says connected.
3. Enter: `Tell me my current avatar and what I am holding.`
4. Select **Send**. The prompt box clears immediately.
5. Try: `Find the nearest grabbable object and bring it to my right hand.`

BoneAI now has its own top-level Preferences button instead of appearing inside BoneMenu. Its interface includes Assistant, Saved Conversations, Voice AI (Beta), AI Provider, Game Permissions, and Diagnostics pages. AI requests, movement/follow state, and wake-word listening run independently of the interface and continue when Preferences is closed.

## Voice AI beta

Voice is optional and off by default. It uses the game microphone through Unity, sends completed utterances to OpenAI's official transcription endpoint, and can read replies using OpenAI text-to-speech. Because Codex account authentication cannot be reused for the audio API, voice requires `OPENAI_API_KEY` as a Windows user environment variable even when Codex is your chat provider.

1. Set `OPENAI_API_KEY`, then restart BONELAB.
2. Open **Preferences → BoneAI → Voice AI (Beta)**.
3. Enable **Voice AI Beta**. Enable **Require Wake Word** for hands-free mode.
4. Edit **Wake Word** (default: `Hey BoneAI`) or use **Listen Once** to accept one utterance without the wake word.
5. Optionally enable **Speak AI Replies**.

Wake-word detection is transcript-gated: short microphone recordings are transcribed, then BoneAI checks for the configured phrase before authorizing a command. It is not offline keyword spotting. Audio replies are AI-generated. Voice is labelled beta because microphone routing and permissions vary between Windows VR setups; a voice failure does not stop text chat or gameplay tools.

## What BoneAI can do

BoneAI exposes exactly 350 structured game tools. The catalog combines the original high-level actions with component-specific world queries, SpawnLab presets, verified interaction invocations, directional physics, combat strengths, player presets, turn presets, and radius scans. Codex receives the full namespaced catalog. Providers with documented tool-count limits receive a prompt-relevant subset plus `tools.search`, so they can discover and call anything in the full catalog without exceeding their API limit. The DLL never treats free-form text as a completed game action.

| Area | Capabilities |
|---|---|
| Player | Read state, teleport, heal/damage, set health, strength, speed, agility/jump, vitality, and restore runtime overrides |
| Avatars | Search installed avatar catalogs, inspect catalog status, refresh, and switch by name or barcode |
| World | Inspect the scene, raycast what you are looking at, find nearby NPCs, weapons, grips, seats, buttons, doors, and other objects |
| Spawning | Search SpawnLab's base-game and installed-mod catalog, spawn items/NPCs/props/vehicles, refresh the catalog, and despawn |
| Interaction | Grab, release, pull to hand, bring objects over, use, activate, press, pull, open, close, push, and throw |
| Combat | Aim, shoot, reload, damage, punch, kick, hit, attack nearby targets, attack Fusion players, and throw objects at targets |
| Movement | Move to positions or objects, go to players, follow, stop, turn, and jump |
| Physics | Apply force/impulse, set velocity, move rigidbodies, and rotate rigidbodies |
| Vehicles | Enter and exit standard Marrow seats |
| Fusion | Read session/player state, find and follow players, switch avatars, attack through Fusion's damage sender, and report sync behavior |
| Diagnostics | List loaded mods/capabilities, read recent MelonLoader errors, and show headset notifications |

Every action has an ID and returns `success`, `failed`, or `cancelled` with real result data. If an object disappears or an API cannot perform the requested interaction, BoneAI reports the failure instead of claiming success.

## Avatar discovery

`BoneAI.Catalogs.dll` builds a unified avatar list from the live Marrow warehouse, installed pallet manifests, and WristHub's generated avatar index when WristHub is present. WristHub is not required. Results are deduplicated and searchable by display name or barcode.

If the list is empty, confirm `BoneAI.Catalogs.dll` is in `UserLibs`, then ask BoneAI to run `avatar.catalog_status` and `avatar.refresh`.

## Fusion multiplayer

BoneAI uses normal, verified BONELAB/Fusion paths. It does not label a local-only transform as synchronized and does not bypass host or object authority.

| Action | Multiplayer behavior | BoneAI needed by other players? |
|---|---|---|
| Spawn | SpawnLab uses Fusion's `NetworkAssetSpawner` online and local spawning offline | No |
| Grab/release | Marrow grip/hand paths already handled by Fusion | No |
| Gun fire | Real gun firing path | No |
| Seat enter/exit | Real Marrow seat path | No |
| Avatar swap | Fusion `LocalAvatar.SwapAvatarCrate` while online | No |
| Fusion-player damage | Fusion `PlayerSender.SendPlayerDamage` with a real Marrow attack | No |
| Prop/NPC state | Uses ordinary game/Fusion ownership and replication when available | No |
| Local health/stat boosts | Intentionally local only | Not synchronized |
| Conversation and menu | Local only | Not synchronized |

BoneAI does not accept remote AI commands. Other players, player names, chat, map text, object names, and mod descriptions are context—not trusted instructions.

## Connection and privacy

For Codex, the Python helper uses only Python's standard library. It finds the installed Codex executable, starts:

```text
codex app-server --listen ws://127.0.0.1:4500
```

and stops it when BONELAB exits. The connection is localhost-only. Authentication remains inside Codex; BoneAI does not read, store, or log passwords, OAuth tokens, or API keys.

BoneAI's tool surface is limited to its registered BONELAB actions. It does not provide prompts with shell access, arbitrary process execution, arbitrary filesystem access, credential access, DLL loading, or unrestricted web requests.

## Troubleshooting

### Connection says unavailable

- Open Codex and confirm you are signed in.
- Confirm Python 3 is installed and available from the command line.
- Confirm `start_boneai_bridge.py` is beside `BoneAI.dll` in `Mods`.
- Select **Reconnect** in **Preferences → BoneAI → AI Provider**.
- Search `MelonLoader/Latest.log` for `[BoneAI]`.

### The Python bridge exits with code 1

This normally means Codex could not be found. Install/open Codex, sign in, then restart BONELAB. The bridge searches the system `PATH` and the standard Codex desktop installation folders under `%LOCALAPPDATA%`.

### Spawning fails

Install SpawnLab `1.0.0`, restart BONELAB, and refresh SpawnLab's catalog once. Then ask BoneAI to use `spawn.refresh` or `spawn.list` before spawning.

### An object command fails

Ask BoneAI to look at the object or search nearby again. Object handles are deliberately invalidated when an object is destroyed or a level changes. Some modded doors/buttons expose no safe public interaction method; BoneAI reports that limitation and can often manipulate the object's rigidbody instead.

### Fusion action appears local

Ask BoneAI for `fusion.get_sync_report`. Local player stat changes, UI, conversation state, and movement assistance are local by design.

## Known limitations

- Voice is beta, requires an OpenAI API key, and may fail on unsupported Windows/VR microphone routes; text remains fully supported.
- Navigation is collision-unaware incremental movement, not full navmesh pathfinding.
- Climbing, crouching, and generalized vehicle steering are not automated.
- Modded interaction components vary widely; unsupported controls fail cleanly.
- A spawn callback may finish after the initial tool response; the object will appear in the next nearby query.
- The exact supported game stack matters because BONELAB uses generated IL2CPP assemblies.

## Build from source

Install the .NET 6 SDK and point the build at a compatible BONELAB installation:

```powershell
dotnet build BoneAI.csproj -c Release -p:BONELAB_DIR="D:\Games\BONELAB"
```

Outputs:

- `bin/Release/net6.0/BoneAI.dll`
- `bin/Release/net6.0/BoneAI.Catalogs.dll`

The code is separated into Codex communication, tool dispatch, game controllers, Fusion compatibility, BoneMenu UI, avatar catalogs, and infrastructure. Version-sensitive reflection is isolated in the relevant compatibility adapters.

Provider transports follow the official [Codex App Server](https://developers.openai.com/codex/app-server/), [Anthropic tool use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/overview), [xAI function calling](https://docs.x.ai/developers/tools/function-calling), [DeepSeek function calling](https://api-docs.deepseek.com/guides/function_calling), [OpenRouter tool calling](https://openrouter.ai/docs/features/tool-calling), and [Ollama tool calling](https://docs.ollama.com/capabilities/tool-calling) documentation.

## License

BoneAI source code is available under the MIT License. BONELAB, BoneLib, LabFusion, SpawnLab, and Codex are separate projects and are not redistributed here.
