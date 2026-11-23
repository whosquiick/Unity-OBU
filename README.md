# Unity OBU
Please note this was human-coded, then improved and optimised by ai.

Unity OBU is a build-time and runtime hardening package for Unity Android (IL2CPP) games. It raises the cost of dumping, asset ripping and casual reverse engineering. It does **not** make those things impossible.

If someone has a rooted device, Frida, a memory dumper or an IL2CPP dumper and enough time, they can still recover assets and logic. Treat every check and every encryption step here as a speed bump, not a vault. Anything that must stay secret (App IDs, API keys, economy rules, authoritative game state) belongs on a server you control, with proper auth.

## What it does

| Layer | What you get |
| --- | --- |
| Build gate | Editor window + `IPreprocessBuildWithReport` checklist that expects IL2CPP, high managed stripping, strip engine code, no Development Build, no Script Debugging. Can hard-fail the build. |
| Managed hardening | `[Keep]` attribute + generated `link.xml`, AES string encrypt/decrypt helpers, an Editor tool that turns a literal into an encrypted blob. |
| Asset protection | Encrypt selected assets into an `.obubundle`, ship it in StreamingAssets, decrypt in memory and load with `AssetBundle.LoadFromMemoryAsync`. |
| Runtime probes | Optional Android heuristics: debugger, TracerPid, Frida, root/Magisk, emulator, signing cert, package name, debuggable flag, installer source, VPN, hook frameworks, IL2CPP dumpers. Configurable response + escalation. |
| Native anti-dump | Optional `libobunative.so`: ptrace self-attach, TracerPid monitor, maps scan, CRC scaffold. Falls back to C# when absent. |
| IL2CPP metadata | Optional build-time `global-metadata.dat` obfuscation (exported Gradle), paired with your own native decrypt hook. |
| Bootstrap | Drop-in `OBUSecurityBootstrap` that runs the checks on startup and optionally re-checks on an interval. |

## What it does **not** do

- It does not stop Frida, memory dumps, rooted devices or IL2CPP dumpers from recovering code and assets once they are running inside (or against) your process.
- It does not replace a commercial obfuscator (Beebyte, Obfuz, etc.). Pair OBU with one if you need deeper method/type renaming.
- It does not protect Mono / non-IL2CPP builds. Use IL2CPP for Android release.
- The native `.so` is shipped as **source + build scripts**, not a prebuilt binary. You build it with the NDK (`native/build.sh` or CMake). Without it, OBU still runs on the pure-C# probes.
- IL2CPP metadata obfuscation needs a **native decrypt hook you provide** and an **exported Gradle project**. It does not stop a dynamic (in-memory) dump — only static unzip-the-APK dumping.
- Encrypted strings and bundles are still plaintext in memory after decrypt. Harden Memory / wipe handlers reduce the window; they do not close it.

## Requirements

- Unity 2021.3 LTS or newer (2022 / 6000 fine).
- Android IL2CPP player build.
- For the signing-cert check: a release keystore you control, and the SHA-256 of that cert stored in Settings.

## Install

### UPM from disk

1. Window → Package Manager → + → Add package from disk…
2. Point it at the extracted folder’s `package.json` (or the UPM zip contents).

### `.unitypackage`

Assets → Import Package → Custom Package… and pick `UnityOBU-1.0.0.unitypackage`.

After import, open **Tools → UnityOBU → Settings**. That creates `Assets/UnityOBU/Resources/UnityOBU/SecuritySettings.asset` on first open.

## Quick start

1. Open **Tools → UnityOBU → Settings**.
2. Leave **Enforce build checklist** on. Fix anything the checklist marks Fail before you ship.
3. Generate a string key and an asset-bundle key (or leave the auto-generated ones). Keep the asset out of public source control if the keys matter.
4. Fill **Expected package name** (button: “Fill current package name”) and, for release, the **signing cert SHA-256**.
5. Drop the **OBU Security Bootstrap** prefab from the sample into your first-loaded scene, or add the `OBUSecurityBootstrap` component yourself.
6. Build Android with IL2CPP. The pre-build gate will re-check and can write `Assets/UnityOBU/link.xml`.

## Encrypting a sensitive string

```csharp
// In the Editor: Tools → UnityOBU → Encrypt a String…
// Paste the plaintext, copy the blob, then at runtime:
string appId = StringCrypt.Reveal(SecretBlob);
```

Or call `StringCrypt.Encrypt(plain)` yourself at bake time. The key comes from `SecuritySettings.stringKey`.

## Protecting AssetBundles

1. Select the assets (or a folder) in the Project window.
2. **Assets → UnityOBU → Encrypt Selection into Protected Bundle**.
3. The encrypted file lands in `Assets/StreamingAssets/Protected/*.obubundle`.
4. At runtime:

```csharp
StartCoroutine(ProtectedBundleLoader.LoadFromStreamingAssets(
    "Protected/protected.obubundle",
    SecuritySettings.LoadOrDefault().assetBundleKey,
    bundle => { /* use bundle */ },
    err => Debug.LogError(err)));
```

Register a wipe handler if you want those bundles unloaded when a probe trips:

```csharp
OBUSecurity.RegisterWipeHandler(() => { if (myBundle != null) myBundle.Unload(true); });
```

## Runtime checks (heuristics)

All of these are **best-effort**. They can false-positive on weird OEM ROMs and they can be bypassed.

| Check | Signal |
| --- | --- |
| Debugger | `Debugger.IsAttached` + `android.os.Debug.isDebuggerConnected` |
| TracerPid | `/proc/self/status` TracerPid ≠ 0 |
| Frida | `frida` / `gum-js-loop` in `/proc/self/maps`, or ports 27042/27043 open |
| Root | Common `su` / Magisk paths, `test-keys` build tags |
| Emulator | `Build` fingerprint / model / hardware heuristics |
| Signature | SHA-256 of the running package’s signing cert vs expected |
| Package | `Application.identifier` vs expected |

Responses: LogOnly, CallbackOnly (`OBUSecurity.Violated`), WipeSensitiveData, WipeAndQuit, Quit. Default is LogOnly so you can tune without bricking Play Mode. **Run in Editor** is off by default — the Editor always looks “debuggable.”

## Recommended build settings

Mirrored by the checklist:

- Scripting Backend: **IL2CPP**
- Managed Stripping Level: **High** (with a `link.xml` for reflection targets)
- Strip Engine Code: **On**
- Development Build: **Off**
- Script Debugging: **Off**
- Android Minify (R8): **On** for release

Tag anything only reached through reflection / SendMessage / native callbacks with `[Keep("reason")]` and run **Tools → UnityOBU → Generate link.xml**.

## Pair with server-side auth

Client hardening alone will lose to a patient attacker. Same idea as locking down a Photon App ID:

- Put App IDs, matchmaking rules and economy behind a backend that authenticates players.
- Prefer Photon Custom Authentication / dashboard allowlists so a stolen App ID cannot open rooms by itself.
- Don’t ship long-lived secrets in the client. Short-lived tokens issued by your server beat any amount of local encryption.

## Testing notes

- First validate with **Response = LogOnly** on a rooted test device and on a normal device.
- Frida / Magisk / emulator checks are easy to trip on a developer phone; whitelist your own test builds with a define or by narrowing `enabledChecks`.
- Encrypted bundles: confirm `LoadFromStreamingAssets` works on device (Android StreamingAssets is inside the APK/`jar:` path).
- Signing check: compute the SHA-256 of your release cert (`keytool -list -v -keystore your.keystore`) and paste the hex (lowercase, no colons) into Settings.

## Project layout

```
Runtime/     player code (settings, crypt, probes, bootstrap, bundle loader)
Editor/      settings window, build gate, checklist, link.xml, encrypt menus
Samples~/    bootstrap prefab + sample scene (import via Package Manager → Samples)
```

## License

MIT. See `LICENSE.md`.

## Native anti-dump library (optional, 1.1.0)

The package ships the C/C++ source for `libobunative.so` under
`Runtime/Plugins/Android/native~/`. Building it adds native anti-debug that is harder to bypass
than managed checks:

- ptrace self-attach so a second debugger can't attach,
- a TracerPid monitor thread,
- a `/proc/self/maps` scan for Frida (gum/agent/gadget), Xposed/LSPosed/Riru, Substrate,
  Dobby/ShadowHook and known IL2CPP dumpers,
- a text-section CRC scaffold (see the native README to provision it).

### Build it

```bash
export ANDROID_NDK_HOME=~/Android/Sdk/ndk/26.1.10909125
cd Runtime/Plugins/Android/native~
./build.sh          # ndk-build; or use the CMake invocation in native~/README.md
```

That drops `libobunative.so` into `Runtime/Plugins/Android/libs/<abi>/`. Select each `.so` in
Unity and set Platform=Android, CPU=ARM64 / ARMv7.

### Use it

`NativeGuard` loads the library through IL2CPP `DllImport`. If the `.so` isn't present,
`NativeGuard.IsAvailable` is false and OBU uses the pure-C# probes instead — nothing breaks.
Turn it on with **Use Native Guard** in Settings; the bootstrap calls `NativeGuard.Start()` and
the `NativeIntegrity` check folds the native flags into the normal response flow.

## IL2CPP dump protection (advanced, realistic)

IL2CPP compiles C# to native, so there's no IL for dnSpy-style decompilers — that alone is the
single biggest win and it's free with the checklist. Beyond that:

- **Dumper detection**: the `Il2CppDumper` check looks for Il2CppDumper / Zygisk-Il2CppDumper /
  MelonLoader modules mapped into the process.
- **global-metadata.dat obfuscation** (opt-in): IL2CPP reads `global-metadata.dat` at startup;
  a static dumper reads it straight from the APK. OBU can XOR-obfuscate that file at build time
  so static dumping fails. **This only works on an exported Gradle project** (the file is
  editable on disk there) and **requires a native hook you add** to de-obfuscate the bytes
  before `il2cpp_init`. `native~/README.md` documents where that hook goes. It is **not** a
  binary we can ship compiled, because the hook is engine-version specific.
- **Split sensitive constants out of code**: keep App IDs, URLs, economy numbers and the like in
  encrypted strings (`StringCrypt`) or, better, fetch them from a server at runtime. Even perfect
  metadata protection doesn't help if the secret is a plain constant in a method body.

What none of this stops: a dynamic dump. Once the game is running the real metadata and all
decrypted data are in memory, and a rooted device with Frida or an IL2CPP dumper can read it.
Metadata obfuscation raises the cost of the easy path; it is not a vault.

## Anti-tamper checks added in 1.1.0

| Check | Signal |
| --- | --- |
| Debuggable | `ApplicationInfo.FLAG_DEBUGGABLE` set in a release build (often a repackage tell) |
| UntrustedInstaller | Installer package not in your allowlist (store-only apps; sideload trips it) |
| Vpn | tun/ppp/ipsec/wg interface present (optional; VPN users trip it) |
| HookDetected | Xposed/LSPosed/Riru/Substrate/Dobby mapped or on disk |
| Il2CppDumper | Known IL2CPP dumper module mapped |
| NativeIntegrity | Native library reported a tamper flag |

### Response escalation

Set a base `response` (e.g. LogOnly) and, with **escalation enabled**, a stronger
`escalatedResponse` (e.g. WipeAndQuit) that kicks in after N distinct checks trip. This lets you
tolerate a single noisy signal while still reacting hard to a real, multi-signal attack.

## Honest bottom line

Even with the native anti-dump library, metadata obfuscation and every check enabled, a patient
attacker on a rooted device can defeat Unity OBU: patch out the ptrace guard, hide modules,
dump metadata and decrypted data from memory. What all of this buys you is **cost** — it turns a
five-minute unzip-and-rip into real reverse-engineering work, and it catches the lazy 95%.
Anything that absolutely must stay secret belongs on a server you control, behind real auth.
