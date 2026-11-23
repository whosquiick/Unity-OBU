# Changelog

## 1.1.0

- Native anti-dump library (`libobunative.so`): ptrace self-attach, TracerPid monitor thread, `/proc/self/maps` scan for Frida/Xposed/Substrate/Dobby and IL2CPP dumpers, text-section CRC scaffold. C/C++ source + ndk-build/CMake scripts; C# `NativeGuard` wrapper with graceful fallback to pure C# when the `.so` is absent.
- Optional IL2CPP `global-metadata.dat` obfuscation at build time (exported Gradle projects), with documented requirement for a paired native decrypt hook. Realistic docs on what this does and does not stop.
- IL2CPP dumper detection (Il2CppDumper, Zygisk variants, MelonLoader) via maps heuristics.
- New anti-tamper checks: debuggable-flag, installer-source allowlist, VPN/proxy interface, hook-framework detection, native integrity.
- Response escalation: switch to a stronger response after N distinct signals.
- `SecurityCheck.Recommended` default profile; strengthened existing probes.

## 1.0.0

- First release of Unity OBU.
- Editor settings window with build hardening checklist (IL2CPP, stripping, no development build).
- Pre/post build gate that can block a release when the checklist fails.
- link.xml generator for types marked with `[Keep]`.
- AES string encryption helper and Editor tool.
- Encrypted AssetBundle workflow (`.obubundle`) with in-memory decrypt + load.
- Android runtime probes for debugger, TracerPid, Frida, root/Magisk, emulator, signing cert and package name (heuristics; optional).
- Drop-in `OBUSecurityBootstrap` prefab/sample.
