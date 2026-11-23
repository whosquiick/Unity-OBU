# GitHub repo copy

**Repository:** `Unity-OBU` (public, https://github.com/whosquiick/Unity-OBU)

**Short description (GitHub "About"):**
Unity OBU — build-time + runtime hardening for Unity Android IL2CPP. String/asset-bundle encryption, a release build gate, native anti-dump (ptrace/maps/CRC), IL2CPP metadata obfuscation, and best-effort anti-debug / anti-tamper checks. Raises the cost of dumping and asset ripping; it does not make them impossible.

**Topics:**
`unity` `unity3d` `security` `obfuscation` `il2cpp` `android` `anti-tamper` `anti-debug` `anti-dump` `asset-protection` `frida-detection` `upm-package` `csharp`

## Release v1.1.0 notes (draft)

**Unity OBU v1.1.0 — native anti-dump + IL2CPP dump protection**

Added
- **Native anti-dump library** (`libobunative.so`, C/C++ source + ndk-build/CMake scripts): ptrace self-attach, TracerPid monitor thread, `/proc/self/maps` scan (Frida gum/agent/gadget, Xposed/LSPosed/Riru, Substrate, Dobby/ShadowHook, IL2CPP dumpers), text-section CRC scaffold. C# `NativeGuard` wrapper loads it via DllImport and falls back to pure C# when the `.so` is absent.
- **IL2CPP dump protection**: optional build-time `global-metadata.dat` obfuscation for exported Gradle projects (paired with a native decrypt hook you provide), plus dumper detection (Il2CppDumper, Zygisk-Il2CppDumper, MelonLoader) and docs on splitting sensitive constants out of code.
- **More anti-tamper**: debuggable-flag, installer-source allowlist, VPN/proxy interface, hook-framework detection, native integrity.
- **Response escalation**: switch to a stronger response after N distinct signals.
- `SecurityCheck.Recommended` default profile; strengthened existing probes.

Honesty
Even with native anti-dump and metadata obfuscation, a determined attacker on a rooted device (Frida, memory dumps, IL2CPP dumpers) can still defeat this. It raises the cost significantly and catches the lazy majority. Keep real secrets server-side behind real auth. No "unbreakable" claims.

Notes
- The native `.so` ships as source; build it with the Android NDK (`native~/build.sh` or CMake).
- Metadata obfuscation requires "Export Project" (Gradle) and your own native decrypt hook; it stops static (unzip-the-APK) dumping, not in-memory dumping.
- Not opened in a real Unity Editor or tested on a device in this build; C# compiles clean against stubs and the native source compiles/links.

**Assets to attach:** `UnityOBU-1.1.0.unitypackage`, `UnityOBU-1.1.0-UPM.zip`
