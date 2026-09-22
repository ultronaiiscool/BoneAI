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

BoneAI v2.6 **Compatibility Extended** uses the same universal `BoneAI.dll` on Windows PCVR and standalone Quest/LemonLoader. The avatar catalog and all BoneAI runtime code are inside that single DLL. Codex remains the default and supports Hermes-style browser login: choose **Sign In With Codex**, enter the displayed one-time code in the browser, and return to BONELAB.

## Start here

### What you need

- BONELAB PCVR `1.744.58126`
- MelonLoader `0.7.3` using the .NET 6 runtime
- BoneLib `3.2.2`
- Codex CLI/App Server on the same PC, or on a securely reachable PC/server for Quest
- LabFusion `1.14.2` for multiplayer features (optional for offline play)
- SpawnLab `1.0.1` for spawning (BoneAI still loads without it, but spawn tools report that the provider is unavailable)

Only BoneAI's own DLL is included in the release. BoneLib, Fusion, SpawnLab, BONELAB, and Codex belong to their respective authors and must be installed separately.

### Install BoneAI

1. Download the latest `BoneAI-v*.zip` from Releases.
2. Copy `BoneAI.dll` into BONELAB's `Mods` folder.
3. Use this exact same DLL for PCVR MelonLoader or Quest LemonLoader.
4. Make sure Codex is installed. You do not need to sign in before opening BONELAB.
5. Start BONELAB.
6. Open **Preferences → BoneAI → Codex Sign-In → Sign In With Codex**.
7. BoneAI copies the one-time code and opens the official sign-in page. Enter the code, sign in, and return to BONELAB.
8. Open **Assistant**, type into **Prompt**, and select **Send**.

That is the complete BoneAI install. On PCVR, `BoneAI.dll` directly starts the installed official Codex App Server—Python and a bridge script are not required.

Your BONELAB folder should contain:

```text
BONELAB/
├─ Mods/
│  ├─ BoneLib.dll
│  ├─ LabFusion.dll                 (optional, for multiplayer)
│  ├─ SpawnLab.dll                  (required only for spawning)
│  └─ BoneAI.dll
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

## Codex browser sign-in

BoneAI uses Codex App Server's official `chatgptDeviceCode` login flow. The game receives only a verification URL and one-time code. Your password, ChatGPT session, and resulting OAuth tokens remain inside Codex App Server and are never returned to or stored by BoneAI.

1. Open **Preferences → BoneAI → Codex Sign-In**.
2. Choose **Sign In With Codex**.
3. BoneAI copies the code and opens the official Codex device sign-in page in the system browser.
4. Finish signing in and return to BONELAB. The status changes to **Connected: Codex** without a restart.

Use **Open Sign-In Page Again** if the browser was closed. **Sign Out** signs the connected Codex App Server out. A secure connection token entered in-game is held in memory for the current game session only and is never written to preferences or logs.

## Standalone Quest setup

The mod and its game actions run on the headset under LemonLoader. Codex App Server itself is a host program and is not distributed as an Android/Quest runtime, so the headset connects to Codex App Server running on a PC or server. Browser login alone cannot replace that host process. The browser performs account authorization while the trusted backend retains the credentials.

1. Install the same universal `BoneAI.dll` with LemonLoader and Quest-compatible BoneLib/Fusion/SpawnLab versions matching your BONELAB patch.
2. Run Codex App Server on a PC/server reachable through a trusted HTTPS/WSS endpoint, with App Server capability-token authentication enabled.
3. In **BoneAI → Codex Sign-In**, set the `wss://` App Server address and enter its connection token.
4. Choose **Sign In With Codex**. Quest opens its browser, the code is entered once, and returning to BONELAB completes the connection.

BoneAI refuses unencrypted remote `ws://` by default. **Allow Insecure Private LAN** exists for isolated testing only and should not be used on public or shared networks. Quest never attempts to launch a local host process. The universal DLL is managed AnyCPU; because Quest game assemblies and installed mod versions must match, validate it against your headset's exact BONELAB/LemonLoader setup before public distribution.

## Updating

Install new versions through Thunderstore/r2modman or download a newer GitHub release and replace BoneAI's files manually.

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

The catalog provider built into `BoneAI.dll` builds a unified avatar list from the live Marrow warehouse, installed pallet manifests, and WristHub's generated avatar index when WristHub is present. WristHub is not required. Results are deduplicated and searchable by display name or barcode.

If the list is empty, ask BoneAI to run `avatar.catalog_status` and `avatar.refresh` and verify that installed content is visible to the game.

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

On PCVR, BoneAI locates the installed Codex executable and directly starts:

```text
codex app-server --listen ws://127.0.0.1:4500
```

and stops it when BONELAB exits. The default PC connection is localhost-only. Account authentication remains inside Codex. BoneAI sees device-login status but never reads, stores, or logs passwords or OAuth tokens. Remote transport bearer tokens are session-only.

BoneAI's tool surface is limited to its registered BONELAB actions. It does not provide prompts with shell access, arbitrary process execution, arbitrary filesystem access, credential access, DLL loading, or unrestricted web requests.

## Troubleshooting

### Connection says unavailable

- Open **BoneAI → Codex Sign-In** and choose **Sign In With Codex**.
- Confirm the Codex desktop app or CLI is installed.
- Select **Reconnect** in **Preferences → BoneAI → Codex Sign-In**.
- Search `MelonLoader/Latest.log` for `[BoneAI]`.

### The Codex App Server exits

Install/open Codex, then restart BONELAB. BoneAI searches the system `PATH` and standard Codex desktop installation folders under `%LOCALAPPDATA%`. Account sign-in can be completed afterward from BoneAI's Codex Sign-In page.

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

Output: `bin/Release/net6.0/BoneAI.dll`. This one managed DLL contains the catalog provider and is packaged unchanged for PCVR and Quest.

The code is separated into Codex communication, tool dispatch, game controllers, Fusion compatibility, BoneMenu UI, avatar catalogs, and infrastructure. Version-sensitive reflection is isolated in the relevant compatibility adapters.

Provider transports follow the official [Codex App Server](https://developers.openai.com/codex/app-server/), [Anthropic tool use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/overview), [xAI function calling](https://docs.x.ai/developers/tools/function-calling), [DeepSeek function calling](https://api-docs.deepseek.com/guides/function_calling), [OpenRouter tool calling](https://openrouter.ai/docs/features/tool-calling), and [Ollama tool calling](https://docs.ollama.com/capabilities/tool-calling) documentation.

## License

BoneAI source code is available under the MIT License. BONELAB, BoneLib, LabFusion, SpawnLab, and Codex are separate projects and are not redistributed here.
