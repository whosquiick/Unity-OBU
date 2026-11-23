# Changelog

## 1.0.0

- First release of Unity OBU.
- Editor settings window with build hardening checklist (IL2CPP, stripping, no development build).
- Pre/post build gate that can block a release when the checklist fails.
- link.xml generator for types marked with `[Keep]`.
- AES string encryption helper and Editor tool.
- Encrypted AssetBundle workflow (`.obubundle`) with in-memory decrypt + load.
- Android runtime probes for debugger, TracerPid, Frida, root/Magisk, emulator, signing cert and package name (heuristics; optional).
- Drop-in `OBUSecurityBootstrap` prefab/sample.
