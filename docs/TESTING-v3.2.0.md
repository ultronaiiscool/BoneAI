# BoneAI v3.2.0 physical test checklist

Compilation and source tests do not prove VR frame rate or Fusion peer behavior. Also run the [v3.1.0 multiplayer checklist](TESTING-v3.1.0.md).

- [ ] Sign in to Codex. Refresh the model picker, search and page through its returned models, optionally show hidden entries, select one, and send a prompt. Verify the server accepts the selected model; switch back to Codex default and retry.
- [ ] Restart BONELAB and confirm the selected model persists. Test a small and an older model only if they actually appear in `model/list`; record server errors for unavailable entries.
- [ ] Open Automatic Browser Voice in a browser with on-device speech support, allow its one-time language model download, then test without internet. Also test the browser-service fallback and typed command field.
- [ ] Repeat on Quest Browser. Record whether it exposes on-device speech and whether switching back to BONELAB suspends listening.
- [ ] Compare frame times before and after asking for nearby-object context and several game actions. Check `diagnostics.performance` and `[BoneAI] Slow game tool` log lines; report scene, object count, and headset.
- [ ] On a large mod collection, open Spawn Catalog just after startup. It may say the catalog is loading; retry after a few frames and confirm search/spawn works without one long warehouse-scan hitch. `spawn.refresh` now returns pending while its new catalog builds.
- [ ] Test avatar refresh with a large mod collection. It should return pending quickly; poll `avatar.catalog_status` until `refreshing` is false.
- [ ] Test movement/follow/stop, jumping, one- and multi-shot combat, and extreme physics values. Confirm no rig explosion, runaway teleport, or frame-long burst; the maximum of three attempted shots per request is explicit in the tool result.
