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

BoneAI v3 uses one managed `BoneAI.dll` for Windows PCVR and standalone Quest/LemonLoader. Quest Codex account mode also includes an Android ARM64 native App Server library from the [BoneAI build branch of the Codex fork](https://github.com/ultronaiiscool/codex-termux/tree/quest/boneai-v3-secure). The library runs inside BONELAB, so no PC or Python bridge is needed while playing. Quest can still use OpenRouter Free or another supported API provider without the native library.

For native source provenance and the security boundary, see [native/README.md](native/README.md). The pinned Rust workspace and Android build inputs are included in `native/codex-android-source-v3.zip`, so Quest source is available directly from BoneAI as well as its fork. Release builders can package a successful native CI artifact with the compiled DLL using [scripts/package-v3.ps1](scripts/package-v3.ps1).

## Start here

### What you need

- BONELAB PCVR `1.744.58126`
- MelonLoader `0.7.3` using the .NET 6 runtime
- BoneLib `3.2.2`
- Internet access and either Codex account sign-in or a key for a supported API provider
- Codex CLI/App Server on PCVR only if you choose account sign-in; Quest account mode uses the bundled Android native library
- LabFusion `1.14.2` for multiplayer features (optional for offline play)
- No SpawnLab installation is needed. BoneAI reads the game's live Marrow spawnable warehouse and uses BONELAB spawning offline or Fusion's network spawner online.

The v3.1 Quest bundle includes BoneAI's DLL and the Android native App Server library. BoneLib, Fusion, and BONELAB belong to their respective authors and must be installed separately. SpawnLab is neither bundled nor required.

### Install BoneAI

1. Download the latest `BoneAI-v*.zip` from Releases.
2. Copy `BoneAI.dll` into BONELAB's `Mods` folder. For Quest Codex account mode also copy `libcodex_app_server.so` and its `.sha256` file into LemonLoader's `UserLibs` folder.
3. Use this exact same DLL for PCVR MelonLoader or Quest LemonLoader.
4. On Quest, no PC-side BoneAI software is required. On PCVR, install Codex only if you want ChatGPT/Codex account login.
5. Start BONELAB.
6. For Quest Codex, open **Preferences → BoneAI → AI Provider → Codex account sign-in**, then **Codex Account → Sign In With Codex**. Finish the device-code flow and return to BONELAB.
7. For the free-model option, choose **Free OpenRouter**, enter your own OpenRouter key, and reconnect.
8. Open **Assistant · Home**, type into **Ask BoneAI**, and select **Send prompt**.

Quest Codex uses the native library inside the game; PCVR starts a locally installed Codex App Server. API providers connect directly over HTTPS. Python and a bridge script are not required.

Your BONELAB folder should contain:

```text
BONELAB/
├─ Mods/
│  ├─ BoneLib.dll
│  ├─ LabFusion.dll                 (optional, for multiplayer)
│  └─ BoneAI.dll
└─ UserLibs/
   ├─ libcodex_app_server.so       (Quest Codex only)
   └─ libcodex_app_server.so.sha256
```

## AI providers

Use **Preferences → BoneAI → AI Provider**, choose **Change Provider**, edit **Model** if desired, and select **Reconnect**. Provider selection and model names are saved in `UserData/MelonPreferences.cfg`.

| Provider | Authentication | Default endpoint |
|---|---|---|
| Codex (PCVR/Quest beta) | ChatGPT/Codex device-code sign-in; no API key in BoneAI | Local Codex App Server |
| OpenRouter Free (PCVR/Quest) | User-provided OpenRouter key; free model router | `https://openrouter.ai/api/v1/chat/completions` with `openrouter/free` |
| OpenAI (PCVR/Quest) | OpenAI API key entered in-game or `OPENAI_API_KEY` | `https://api.openai.com/v1/responses` |
| Claude | `ANTHROPIC_API_KEY` | `https://api.anthropic.com/v1/messages` |
| Grok | `XAI_API_KEY` | `https://api.x.ai/v1/chat/completions` |
| DeepSeek | `DEEPSEEK_API_KEY` | `https://api.deepseek.com/chat/completions` |
| OpenRouter | `OPENROUTER_API_KEY` | `https://openrouter.ai/api/v1/chat/completions` |
| Ollama | No secret required | `http://127.0.0.1:11434/v1/chat/completions` |
| Custom | `BONEAI_API_KEY` | Set **Custom Base URL** |

Enter a key under **AI Provider → API Key (saved securely)**. BoneAI immediately clears the visible field and saves only encrypted data: current-user DPAPI on Windows and authenticated device-bound encryption on Quest. The selected key remains available after restarts until **Clear Current API Key** is chosen. PCVR users may alternatively set the provider's environment variable before starting BONELAB. OpenAI uses the Responses API; Claude uses Anthropic's native Messages/tool-use protocol. Grok, DeepSeek, OpenRouter, Ollama, and Custom use their documented OpenAI-compatible function-calling protocol. The chosen model must support tools.

OpenRouter Free does not mean anonymous access: create an OpenRouter key and enter it in BoneAI, but select the `openrouter/free` router so inference is routed to currently available free models. Availability and rate limits belong to OpenRouter and the upstream free providers. Codex is the only provider that uses account login. Consumer Claude/Grok/DeepSeek logins are not reused because their official third-party API authentication uses API keys.

## Codex browser sign-in

BoneAI uses Codex App Server's official `chatgptDeviceCode` login flow. The game receives only a verification URL and one-time code. Your password, ChatGPT session, and resulting OAuth tokens remain inside Codex App Server and are never returned to or stored by BoneAI.

1. Open **Preferences → BoneAI → Codex Account**.
2. Choose **Sign In With Codex**.
3. BoneAI copies the code and opens the official Codex device sign-in page in the system browser.
4. Finish signing in and return to BONELAB. The status changes to **Connected: Codex** without a restart.

Use **Open Sign-In Page Again** if the browser was closed. Codex App Server securely saves and refreshes the account login, so it remains signed in across BONELAB restarts. **Sign Out** calls the official Codex logout operation and clears that Codex-owned login. BoneAI never copies or stores the OAuth tokens.

## Standalone Quest setup — no PC

1. Install the universal `BoneAI.dll` with LemonLoader and Quest-compatible BoneLib/Fusion versions matching your BONELAB patch. SpawnLab is not required.
2. For Codex account mode, place the bundled `libcodex_app_server.so` and `libcodex_app_server.so.sha256` in LemonLoader's `UserLibs` folder. This beta needs roughly 260 MB for the library plus space for a private runtime copy.
3. Start BONELAB and open **Preferences → BoneAI → AI Provider**.
4. Choose **Codex account sign-in** and then **Codex Account → Sign In With Codex**. Complete the code in the browser and return to BONELAB.
5. Alternatively, choose **Free OpenRouter** and enter your key in **API Key (saved securely)**.
6. Open **Assistant · Home**.

All inference requests and tool-call loops run from the headset. A PC, Python bridge, remote WebSocket host, and Codex CLI are not used in this mode. The universal DLL is managed AnyCPU; exact Quest compatibility still depends on matching BONELAB, LemonLoader, BoneLib, and Fusion versions.

Quest Codex account mode uses a community-built Android port of Codex App Server from the pinned fork commit. This feature is experimental, not an official OpenAI Android distribution. The native server requires a random per-session WebSocket token, keeps account state in BONELAB's private Android files directory, and exposes only BoneAI's dynamic game tools to the model. Browser sign-in is the device-code ceremony; the native App Server continues to run inside BONELAB. Quest still defaults to OpenRouter Free on existing and new installations until you select Codex.

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

Voice is optional and off by default. There are two modes:

- **Free Browser Voice** opens a localhost page in Edge, Chrome, or Quest Browser. The browser performs speech recognition, waits for the configured wake word, and submits the following command to BoneAI. It requires no STT API key and BoneAI imposes no quota. Choose **On-device** if the browser exposes that experimental capability; its language model may require a one-time download. Otherwise, **Browser speech service** may send audio to the browser vendor and requires that service to work. Support, privacy, availability, and limits depend on the browser. A typed-command field on the same page works even when speech recognition is unavailable.
- **In-game microphone voice** records through Unity, uses OpenAI transcription, and optionally reads replies using OpenAI text-to-speech. This mode requires an OpenAI API key.

1. Open **Preferences → BoneAI → Voice AI (Beta)** and edit **Wake Word** if desired (default: `Hey BoneAI`).
2. Choose **Open Free Browser Voice**, allow microphone access in the browser, press **Start listening**, and keep that page open.
3. Say the wake word followed by a command, or say the command within eight seconds after the wake word.
4. For the original in-game mode, enter an OpenAI key, enable **Voice AI Beta**, and optionally enable **Speak AI Replies**.

Browser wake-word detection is transcript-gated by the browser, not offline keyword spotting inside the DLL. The local bridge binds only to loopback and uses a fresh 256-bit session token. On standalone Quest, switching between BONELAB and Quest Browser may suspend one application; continuous background listening therefore depends on the headset/browser version and remains beta. A voice failure does not stop text chat or gameplay tools.

If the page reports **browser speech service could not connect** (previously `Speech error: network`), the browser's recognition service failed; this does not mean BoneAI or your AI provider is offline. Stop and retry once, select **On-device** if available, or use the typed-command field. If On-device is disabled, that browser does not expose the required speech API; try a browser that does. Microphone access and, for the browser-service mode, internet access must be allowed. BoneAI cannot repair an unavailable third-party speech service.

## What BoneAI can do

BoneAI exposes over 350 structured game tools. The catalog combines high-level actions with component-specific world queries, built-in spawn presets, verified interaction invocations, directional physics, combat strengths, player presets, and radius scans. Codex receives the full namespaced catalog. Providers with documented tool-count limits receive a prompt-relevant subset plus `tools.search`, so they can discover and call anything in the full catalog without exceeding their API limit. The DLL never treats free-form text as a completed game action.

| Area | Capabilities |
|---|---|
| Player | Read state, teleport, heal/damage, set health, strength, speed, agility/jump, vitality, and restore runtime overrides |
| Avatars | Search installed avatar catalogs, inspect catalog status, refresh, and switch by name or barcode |
| World | Inspect the scene, raycast what you are looking at, find nearby NPCs, weapons, grips, seats, buttons, doors, and other objects |
| Spawning | Search the game's loaded base-game and installed-mod spawnables, spawn by unique name or barcode, choose a position, check callback status, and despawn; a manual Spawn Catalog page is available in BoneAI Preferences |
| Interaction | Grab, release, pull to hand, bring objects over, use, activate, press, pull, open, close, push, and throw |
| Combat | Aim, shoot, reload, damage, punch, kick, hit, attack nearby targets, attack Fusion players, and throw objects at targets |
| Movement | Move to positions or objects, go to players, follow, stop, turn, and jump |
| Physics | Apply force/impulse, set velocity, move rigidbodies, and rotate rigidbodies |
| Vehicles | Enter and exit standard Marrow seats |
| Fusion | Read session/player state, find and follow players, switch avatars, attack through Fusion's damage sender, and report sync behavior |
| Diagnostics | List loaded mods/capabilities, read recent MelonLoader errors, and show headset notifications |

Every action has an ID and returns `success`, `failed`, `cancelled`, or `pending`. `pending` means the game accepted a request but BoneAI cannot yet confirm its outcome; it is **not** a completed action. After spawning, use `spawn.status` with the returned action ID to check for a local callback, and inspect the world or a second client if needed. A cancelled action is skipped if it has not started; cancellation after execution begins cannot undo a game-side effect.

## Avatar discovery

The catalog provider built into `BoneAI.dll` builds a unified avatar list from the live Marrow warehouse, installed pallet manifests, and WristHub's generated avatar index when WristHub is present. WristHub is not required. Results are deduplicated and searchable by display name or barcode.

If the list is empty, ask BoneAI to run `avatar.catalog_status` and `avatar.refresh` and verify that installed content is visible to the game.

## Fusion multiplayer

BoneAI uses BONELAB/Fusion paths and checks local ownership before direct mutation of registered network entities. Calling a game or Fusion API does not, by itself, prove that a peer observed the outcome. Confirm important actions from a second client; `pending` responses must not be presented as synchronized success.

| Action | Multiplayer behavior | BoneAI needed by other players? |
|---|---|---|
| Spawn | BoneAI uses Fusion's `NetworkAssetSpawner` server route online and checks Fusion's local spawn-gun permission first. `spawn.status` reports a local callback and network entity ID when available, not peer confirmation. Offline it uses BONELAB's `AssetSpawner` callback. | No custom BoneAI protocol; peer test required |
| Grab/release | Marrow hand/grip path; hand attachment is checked locally | No custom BoneAI protocol; peer test required |
| Gun fire | Real gun firing path; resulting hit/peer state is not synchronously confirmed | No custom BoneAI protocol; peer test required |
| Seat enter/exit | Marrow seat path; local seat state is checked | No custom BoneAI protocol; peer test required |
| Avatar swap | Fusion `LocalAvatar.SwapAvatarCrate` request online | No custom BoneAI protocol; peer test required |
| Fusion-player damage | Fusion `PlayerSender.SendPlayerDamage` request with a Marrow attack | No custom BoneAI protocol; peer test required |
| Prop/NPC state | Direct mutation is blocked for registered entities not locally owned; ownership alone does not prove replication | Depends on underlying game/Fusion path; peer test required |
| Local health/stat boosts | Intentionally local only | Not synchronized |
| Conversation and menu | Local only | Not synchronized |

BoneAI does not accept remote AI commands. Other players, player names, chat, map text, object names, and mod descriptions are context—not trusted instructions.

## Connection and privacy

On PCVR, BoneAI locates the installed Codex executable and directly starts an owned App Server on a random loopback-only port, equivalent to:

```text
codex app-server --listen ws://127.0.0.1:<random-port>
```

and stops it when BONELAB exits. Codex connections are localhost-only. Account authentication remains inside Codex. BoneAI sees device-login status but never reads, stores, or logs passwords or OAuth tokens. Built-in provider keys can only be sent to their provider's official HTTPS host; logs automatically redact loaded secrets.

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

Wait until the level and Marrow warehouse have loaded, then use `spawn.refresh` or `spawn.list`. Ask for an exact barcode if a name is ambiguous. An accepted request returns `pending`; call `spawn.status` with its action ID to check local completion. In Fusion, spawning is blocked when the level is not networked or lobby/gamemode spawn permissions deny it, rather than silently creating a local-only object.

### An object command fails

Ask BoneAI to look at the object or search nearby again. Object handles are deliberately invalidated when an object is destroyed or a level changes. Some modded doors/buttons expose no safe public interaction method; BoneAI reports that limitation and can often manipulate the object's rigidbody instead.

### Fusion action appears local

Ask BoneAI for `fusion.get_sync_report`. Local player stat changes, UI, conversation state, and movement assistance are local by design.

## Known limitations

- Voice is beta. Browser voice requires browser SpeechRecognition support and may pause when Quest Browser is backgrounded; in-game voice still requires an OpenAI API key. Text remains fully supported.
- Navigation is collision-unaware incremental movement, not full navmesh pathfinding.
- Climbing, crouching, and generalized vehicle steering are not automated.
- Modded interaction components vary widely; unsupported controls fail cleanly.
- Fusion's spawn callback confirms an object on the initiating client, not that every peer loaded or saw it. If no callback arrives within two minutes, BoneAI marks it unconfirmed and releases its callback registration; a slow peer may still finish later. Similar game input requests may be pending when there is no safe outcome callback.
- The Android native library is reused from the verified v3.0.0 build; v3.1.1 changes managed code and the browser page. Automated tests and compilation do not replace physical Quest and two-client Fusion testing. See [the test checklist](docs/TESTING-v3.1.1.md).
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

BoneAI source code is available under the MIT License. BONELAB, BoneLib, LabFusion, SpawnLab, and Codex are separate projects and are not redistributed here. BoneAI's new spawn implementation is its own code; the third-party SpawnLab DLL and decompiled source are not included.

The built-in spawner was prompted by [SpawnLab by ChappieStudios](https://thunderstore.io/c/bonelab/p/ChappieStudios/SpawnLab/). SpawnLab remains an independent project and may still be installed for its own menu; BoneAI no longer calls or requires it.
