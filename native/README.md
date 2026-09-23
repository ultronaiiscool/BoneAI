# Quest native App Server source

BoneAI v3's Quest Codex account mode combines the universal `BoneAI.dll` in this repository with an Android ARM64 Codex App Server library built from the community fork at [`ultronaiiscool/codex-termux`](https://github.com/ultronaiiscool/codex-termux/tree/quest/boneai-v3-secure). The source revision for v3 is `9413e0ce0db4d5147f760664d0c1b01ca655fba5`.

The native Rust workspace and Android build inputs from that revision are included as [`codex-android-source-v3.zip`](codex-android-source-v3.zip) (SHA-256 `E83AF5F169AABE7FCB0D4B230FD03BA26F5CFF191A9BA508AF5D0634737CEFE0`). It includes workspace manifests, `Cargo.lock`, the Android C header, build scripts, CI workflow, and license/notice files. External Cargo dependencies are pinned by `Cargo.lock` and remain available from their upstream open-source distributions; their entire source trees are not vendored into this repository.

The native fork owns the Rust App Server, Android C ABI, and its GitHub Actions build. The source archive preserves the Rust workspace for reproducibility without copying unrelated desktop documentation and web assets into the live mod tree. BoneAI's release package contains the compiled library, checksum, source archive, and required licenses/notices alongside the managed mod.

The v3 native branch differs from the earlier Android proof of concept in three important ways:

- WebSocket access requires a random per-game-session bearer token. BoneAI passes only its SHA-256 digest to the native ABI.
- The embedded server exposes only the dynamically registered BoneAI game tools to the model. It excludes built-in shell, filesystem, web, MCP, plugin, and child-agent tools.
- The ABI advertises security version 1, which the managed mod checks before starting it. The native server uses BONELAB's private Android storage for account state.

The Android port is community-built and not an official OpenAI Android distribution. Its presence in the package does not prove compatibility with every Quest/BONELAB/LemonLoader combination; physical headset testing remains necessary. Do not substitute an older `.so` with the same name: BoneAI verifies both the packaged SHA-256 checksum and the secure ABI version.

No native library or Codex service is required for PCVR. PCVR account mode uses the official locally installed Codex App Server. API-key providers on either platform also do not use the native library.
