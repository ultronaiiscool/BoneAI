# BoneAI v3.3.0 headset test checklist

Run the [v3.2 checklist](TESTING-v3.2.0.md) as well. Source tests do not establish microphone permissions, cloud availability, or Quest multitasking behavior.

- [ ] With no STT keys saved, verify Automatic uses browser on-device speech if supported, otherwise browser speech service, and always leaves typed commands usable.
- [ ] With a Groq free-tier key saved, force the browser speech service to fail, then confirm a spoken wake-word command is transcribed and submitted once. Check that no key appears in the browser page, URL, or log.
- [ ] With a Cloudflare Workers AI token and valid Account ID saved, repeat with Groq unavailable. Check that an exhausted free allocation reports failure instead of a false success.
- [ ] Clear each STT key from the Voice menu, reopen the page, and verify the service is no longer offered. Restart BONELAB and verify securely saved keys remain usable until cleared.
- [ ] Repeat in Quest Browser. Record microphone permission and whether switching back to the game suspends its page.
- [ ] With both web providers unreachable, verify typed commands still work and the game remains responsive.
