# GitHub repo copy

**Suggested repository name:** `Unity-OBU`

**Short description (GitHub "About"):**
Unity OBU — build-time + runtime hardening for Unity Android IL2CPP. Encrypts strings and asset bundles, gates release builds, and runs best-effort anti-debug / anti-tamper checks. Raises the bar against dumping and asset ripping; it does not make them impossible.

**Topics:**
`unity` `unity3d` `security` `obfuscation` `il2cpp` `android` `anti-tamper` `anti-debug` `asset-protection` `upm-package` `csharp`

**Release v1.0.0 notes (draft):**

First release of Unity OBU.

- Editor settings window and build hardening checklist (IL2CPP, high stripping, no development build)
- Pre/post build gate that can block a bad release setup
- `[Keep]` + generated `link.xml`
- AES string encryption helper and Encrypt-a-String tool
- Encrypted AssetBundle (`.obubundle`) workflow with in-memory load
- Android runtime probes (debugger, TracerPid, Frida, root, emulator, signing cert, package name) — heuristics, optional
- Drop-in security bootstrap sample

**Honesty note for the release body:** Client-side protection raises the bar. It cannot fully prevent dumping or asset ripping. Pair with server-side auth for anything that must stay secret.
