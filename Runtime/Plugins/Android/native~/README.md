# Native anti-dump library (libobunative.so)

Optional. Unity OBU works without it (the C# `NativeGuard` falls back to the pure-C# probes).
Building and shipping the `.so` adds native anti-debug and a background tamper monitor that
are harder to bypass than managed checks alone.

## What it does

- **ptrace self-attach** – forks a watcher that `PTRACE_ATTACH`es the main process, so a second
  debugger cannot attach. If the attach fails, something is already tracing us.
- **TracerPid monitor thread** – re-reads `/proc/self/status` on an interval.
- **maps scan** – looks for `frida-gum`/`frida-agent`/`frida-gadget`, Xposed/LSPosed/Riru,
  Substrate, Dobby/ShadowHook, and known IL2CPP dumper modules (Il2CppDumper, Zygisk variants,
  MelonLoader).
- **text-section CRC self-check** – scaffold for a code-integrity check (see below).

Results are returned as a bitmask from `obu_poll()` / `OBUNative.poll()`.

## Build with ndk-build

```bash
export ANDROID_NDK_HOME=~/Android/Sdk/ndk/26.1.10909125
./build.sh
```

That emits `libs/<abi>/libobunative.so` and copies them to
`Runtime/Plugins/Android/libs/<abi>/`.

## Build with CMake (Android Studio / standalone)

```bash
cmake -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake \
      -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24 -S native -B build/arm64
cmake --build build/arm64
```

Then copy `libobunative.so` into `Runtime/Plugins/Android/libs/arm64-v8a/`.

## Unity import settings

Select each `libobunative.so` in the Project window and in the Inspector:
- Platform: **Android**
- CPU: **ARM64** for `arm64-v8a`, **ARMv7** for `armeabi-v7a`

## text-section CRC

`g_text_crc_expected` is `0` by default, which disables the check so an un-provisioned build
never false-positives. To enable it, compute the CRC of your built `.text` section and bake the
value in (a post-link script, or patch the constant). The `.text` bounds are toolchain specific,
which is why the walk is left to integrators rather than hard-coded.

## Honesty

This is best-effort. A determined attacker on a rooted device can patch out the ptrace guard,
hide modules from `/proc/self/maps`, or hook `read()`. The point is to raise cost, not to be a
hard stop. Keep real secrets server-side.
