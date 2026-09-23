# BoneAI v3.1.1 test checklist

All v3.1.0 spawn and Fusion checks still apply; see [the v3.1.0 checklist](TESTING-v3.1.0.md). These checks cover the browser voice patch and require real browser/headset testing.

- [ ] On PCVR, open Browser Voice and grant microphone permission. If browser speech is functional, say the wake word and a command. Confirm BoneAI receives exactly that command.
- [ ] If the browser service returns `network`, confirm the page displays a useful explanation, changes to **Start listening**, and does not repeatedly retry on its own.
- [ ] If **On-device** is enabled, select it, let the browser install the language model if requested, and test after disabling internet. If disabled, record the browser version; do not report unsupported on-device speech as a mod crash.
- [ ] Type a command into the fallback field while speech is stopped. Confirm the field clears and the command reaches BoneAI once. A `submitted` message means accepted by the local bridge, not that the game action succeeded.
- [ ] Deny microphone permission; confirm speech stops with a permission explanation and typed commands still work.
- [ ] On Quest, confirm the page loads, test whichever speech modes Quest Browser actually exposes, and check whether switching back to BONELAB suspends recognition.
- [ ] With a wake word containing `<`, `>`, quotes, or `</script>`, confirm the page renders it literally and no extra script executes.
