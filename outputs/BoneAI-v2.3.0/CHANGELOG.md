# Changelog

## 2.3.0

- Includes every change from v2.2: Claude, Grok, DeepSeek, OpenRouter, Ollama, Custom providers, and the verified auto-updater.
- Expanded BoneAI to exactly 350 internal structured gameplay tools, with searchable discovery and provider-aware subsets for APIs with lower tool limits.
- Added a standalone BoneAI button to BONELAB Preferences, based on the verified Fusion 1.14.2 Preferences injection lifecycle.
- Redesigned the in-game interface into Assistant, Saved Conversations, Voice AI (Beta), AI Provider, Game Permissions, and Diagnostics pages.
- Added persistent saved-conversation metadata and one-click resume for Codex and direct API providers.
- Added opt-in microphone transcription, configurable transcript-gated wake word, listen-once mode, and optional AI speech replies.
- Kept AI work, follow/movement state, and voice polling independent of the open/closed Preferences menu.
- Added managed-profile detection so Thunderstore/r2modman installs update through the manager without creating duplicate root-level DLLs.

## 2.2.0

- Added official tool-calling adapters for Anthropic Claude, xAI Grok, DeepSeek, OpenRouter, and local Ollama.
- Added a configurable OpenAI-compatible provider for other services.
- Kept Codex App Server as the default and only login-based provider; API-provider secrets are read exclusively from environment variables.
- Added an independent `BoneAI.Updater.dll` MelonLoader plugin with GitHub Release checks, SHA-256 verification, ZIP path allowlisting, post-exit installation, and backups.
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
