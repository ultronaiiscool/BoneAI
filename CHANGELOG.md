# Changelog

## 3.1.1 — Browser voice recovery

- Stopped the browser voice page from endlessly restarting after a fatal speech-service network error. It now reports the browser-side failure and lets the player retry deliberately.
- Added feature-detected on-device speech recognition with browser-managed language-model installation, where supported. Browsers without the experimental API keep the browser-service option.
- Added a typed-command fallback on the same localhost page so a speech outage does not block commands; the field clears after submission.
- Embedded the page as a separate, testable asset and added browser-behavior tests and safer JSON escaping for the wake word.
- No autoupdater and no change to the pinned Quest Codex native library.

## 3.1.0 — Built-in spawning

- Removed the SpawnLab runtime and Thunderstore dependency. BoneAI now indexes the loaded Marrow warehouse itself, including base-game and installed-mod spawnables.
- Added exact-barcode and unique-name resolution, ambiguity errors, a selectable spawn position/rotation, `spawn.status` for local callback results, and a manual Spawn Catalog Preferences page.
- Offline spawning uses BONELAB's `AssetSpawner`. Fusion sessions use the verified `NetworkAssetSpawner` server request, with a local permission/gamemode check; requests are blocked rather than silently spawning only locally when network synchronization is unavailable.
- Time-bounded unanswered Fusion callback registrations and kept peer visibility explicitly unconfirmed without a second-client observation.
- Added spawn-catalog matching tests. No third-party SpawnLab code or DLL is redistributed, and there is no autoupdater.
- The Android native Codex library is unchanged from v3; physical Quest and two-client Fusion testing are still required.

## 3.0.1 — Reliability and truthful action results

- Fixed cancellation of queued game actions so they cannot run after cancellation; actions already executing now return their real result rather than a misleading cancelled result.
- Added `pending` results for fire-and-forget SpawnLab, input, combat, and network requests. The assistant is instructed to verify the game world instead of claiming completion prematurely.
- Added local Fusion entity ownership checks before direct manipulation of registered network objects and made synchronization reports distinguish local acceptance from peer confirmation.
- Replaced wrong-best-match spawning with exact/unique catalog resolution and an ambiguity error when multiple spawnables match.
- Added typed argument schemas for core actions, improved tool selection, and bounded recent action history.
- Added automated dispatcher/schema smoke tests, source CI, a physical-device checklist, and release checksums. No autoupdater.
- Reuses the same pinned Android native Codex App Server library as v3.0.0. Physical Quest and two-client Fusion behavior still require user testing.

## 3.0.0 — Native Quest Codex

- Integrated an Android ARM64 Codex App Server from the BoneAI branch of the user's Codex fork. Quest can start it inside BONELAB and use persistent ChatGPT/Codex device-code sign-in without a PC or API key.
- Kept one managed `BoneAI.dll` for PCVR and Quest. The Quest package additionally contains the native `.so` and checksum under `UserLibs`; the native source remains in the pinned Codex fork.
- Restricted the embedded App Server to BoneAI's dynamic game tools and added a random per-session bearer token to its localhost WebSocket.
- Added checksum validation, native ABI version checking, private Android storage for Codex state, bounded startup readiness checks, and clean failure status.
- Reorganized the in-game BoneAI pages with clearer assistant, provider, voice, account, and runtime status controls.
- Kept OpenRouter Free, other API providers, voice beta, SpawnLab, Fusion, saved conversations, and the existing tool catalog.
- Android ARM64 build validation is available; physical Quest/LemonLoader gameplay testing remains necessary.
- No autoupdater.

## 2.7.0 — Free Voice

- Added **OpenRouter Free**, backed by the official `openrouter/free` router. Users bring their own OpenRouter key, while the router selects a currently available free model that supports the requested features, including tool calling when available.
- Made OpenRouter Free the default provider for new Quest installations. Existing provider selections are preserved.
- Added **Free Browser Voice (Beta)**. BoneAI hosts a localhost-only speech page, opens it in Edge/Chrome/Quest Browser, waits for the configurable wake word, and sends recognized commands back through a random per-session capability token.
- Browser voice requires no speech API key and BoneAI imposes no usage quota. Recognition availability, privacy, networking, and limits are controlled by the selected browser and its speech service.
- Added bounded HTTP parsing, body limits, CSP, no-store/no-referrer headers, constant-time token checks, and loopback-only binding to the browser voice bridge.
- Added bounded retry with jitter for temporary provider rate-limit and availability errors, plus official OpenRouter attribution headers.
- Removed the obsolete insecure-remote-Codex preference and improved Quest Codex errors so the mod no longer tries a nonexistent local App Server.
- Revalidated the official Codex boundary: browser/device-code login still requires a running Codex App Server. OpenAI does not publish an Android App Server target, so v2.7.0 does not copy private OAuth behavior or falsely claim standalone Quest Codex login.
- Contains no updater and does not modify an existing installed DLL during release creation.

## 2.6.2 — Secure Persistence

- Codex account sign-in now explicitly reuses and refreshes the official Codex App Server login across game restarts; **Sign Out** clears that Codex-owned login.
- Provider API keys entered in-game are now saved until cleared. Windows uses current-user DPAPI; Quest uses authenticated device-bound encryption. Plaintext keys are never written to preferences or logs.
- Added an explicit **Clear Current API Key** action that removes the selected provider key from memory and protected storage.
- Restricted Codex App Server connections to loopback and launch an owned server on a random local port, removing remote transport-token and insecure-LAN controls.
- Restricted built-in provider keys to each provider's official HTTPS host. Custom endpoints require HTTPS except for explicit localhost development; Ollama is localhost-only.
- Validates Codex browser-login URLs, caps WebSocket/API/voice response sizes, bounds pending requests and timeouts, and cleans up cancelled/disconnected requests.
- Added automatic secret redaction to logs and log-query tool results.
- Contains no updater.

## 2.6.1 — Quest Standalone Fix

- Added true no-PC Quest operation through direct HTTPS provider connections from the headset.
- Added OpenAI Responses API support with structured BoneAI tool calling and `gpt-5.3-codex` as the default standalone OpenAI model.
- Added in-game provider API-key entry. Keys are session-only, cleared from the visible field, never written to disk, and never logged.
- Added a one-click **Quest Standalone OpenAI** setup option and made OpenAI direct mode the default for new Quest installations.
- Made the same in-game key path available to Claude, Grok, DeepSeek, OpenRouter, Custom, and Voice AI.
- Corrected the documentation: Codex account/browser login remains PCVR-only because the official App Server has no supported Android/Quest runtime.
- Still uses one universal `BoneAI.dll`, contains no Python bridge, and contains no updater.

## 2.6.0 — Compatibility Extended

- Replaced separate PCVR and Quest mod binaries with one AnyCPU `BoneAI.dll` used unchanged on both platforms.
- Folded the avatar/pallet catalog provider into `BoneAI.dll`; `BoneAI.Catalogs.dll` and the `UserLibs` install step are no longer required.
- Removed the Python bridge requirement. On PCVR, BoneAI directly launches the installed official Codex App Server; on Quest it connects to the configured remote App Server.
- Made installed-content discovery use Unity's platform-correct persistent data directory on both Windows and Android.
- Kept official browser/device-code Codex sign-in, all 350 tools, Fusion, SpawnLab, saved conversations, providers, performance work, and Voice AI beta.
- Contains no updater.

## 2.5.0

- Added official Codex device-code authentication from inside BONELAB: BoneAI copies the one-time code, opens the system browser, and detects completion automatically.
- Added a dedicated Codex Sign-In page with reconnect, sign-out, sign-in-page reopening, remote App Server address, and session-only transport-token entry.
- Added standalone Quest runtime detection. Quest never attempts to launch Python and instead connects to a securely hosted Codex App Server.
- Added authenticated remote App Server support using bearer transport tokens and secure `wss://` enforcement by default.
- Changed the mod assembly to AnyCPU for PCVR/Quest managed-runtime compatibility; no auto-updater is included.
- Retains the complete v2.4 performance, 350-tool, Fusion, SpawnLab, saved-conversation, provider, and Voice AI beta feature set.

## 2.4.0

- Removed the updater and all updater packaging; BoneAI updates are installed manually or through Thunderstore/r2modman.
- Reworked nearby-world queries with a short-lived, movement-aware cache, squared-distance filtering, and scene-change invalidation.
- Moved stale object-handle pruning off the per-frame path and removed its normal-case temporary allocations.
- Reduced repeated Unity/IL2CPP component traversal by describing NPCs, interactables, and component types from one cached scan per object.
- Cached Fusion player snapshots and SpawnLab catalog reflection results to avoid repeating expensive reflection work during multi-step actions.
- Precomputed tool-search text and cached the legacy catalog JSON, eliminating repeated concatenation and serialization across the 350-tool registry.
- Bounded provider tool-selection context to the current prompt and recent history instead of serializing the entire conversation every round.
- Cached saved-conversation menu snapshots so the closed/open Preferences refresh loop no longer continually sorts and clones history.
- Reused the voice activity polling buffer and throttled missing-key retries, reducing allocations in the beta microphone loop.
- Fixed cancellation-token lifetime races during overlapping cancel/send operations and serialized concurrent connection attempts.
- Added defensive reporting for providers that return HTML or other non-JSON errors.
- Added `diagnostics.performance` for physical world-scan counts, cache hit rate, average scan time, and cached-object count.
- Kept the complete v2.3 feature set: exactly 350 tools, standalone Preferences UI, saved conversations, voice beta, wake word, multi-provider support, SpawnLab, and Fusion integration.

## 2.3.0

- Includes the v2.2 AI providers: Claude, Grok, DeepSeek, OpenRouter, Ollama, and Custom.
- Expanded BoneAI to exactly 350 internal structured gameplay tools, with searchable discovery and provider-aware subsets for APIs with lower tool limits.
- Added a standalone BoneAI button to BONELAB Preferences, based on the verified Fusion 1.14.2 Preferences injection lifecycle.
- Redesigned the in-game interface into Assistant, Saved Conversations, Voice AI (Beta), AI Provider, Game Permissions, and Diagnostics pages.
- Added persistent saved-conversation metadata and one-click resume for Codex and direct API providers.
- Added opt-in microphone transcription, configurable transcript-gated wake word, listen-once mode, and optional AI speech replies.
- Kept AI work, follow/movement state, and voice polling independent of the open/closed Preferences menu.
- Simplified the release package to the main mod, bridge, and catalog library; new versions are installed manually or through Thunderstore/r2modman.

## 2.2.0

- Added official tool-calling adapters for Anthropic Claude, xAI Grok, DeepSeek, OpenRouter, and local Ollama.
- Added a configurable OpenAI-compatible provider for other services.
- Kept Codex App Server as the default and only login-based provider; API-provider secrets are read exclusively from environment variables.
- Added provider, model, and custom endpoint controls to BoneMenu.

## 2.1.1

- Added a valid Thunderstore `manifest.json` with current BONELAB dependencies.
- Added an exact 256×256 `icon.png` based on the official BoneAI logo.
- Added the full-resolution logo to the repository and README.
- Rebuilt the release archive with all required Thunderstore files at its root.

## 2.1.0

- Renamed the project, assemblies, menu, settings, bridge, and logs to BoneAI.
- Added the first-time-player installation package and redesigned README.
- Published the complete source and compiled release under the MIT License.
