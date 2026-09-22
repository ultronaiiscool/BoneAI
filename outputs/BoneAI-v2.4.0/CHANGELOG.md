# Changelog

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
