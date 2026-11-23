using System;

namespace UnityOBU
{
    // How the runtime reacts when a tamper/debug/dump heuristic trips.
    public enum SecurityResponse
    {
        // Only write to the log. Useful while you are still tuning thresholds.
        LogOnly = 0,

        // Raise OBUSecurity.Violated so your own code can decide what to do.
        CallbackOnly = 1,

        // Clear decrypted caches and loaded protected bundles, then keep running.
        WipeSensitiveData = 2,

        // Wipe sensitive data and quit the application.
        WipeAndQuit = 3,

        // Quit immediately without the wipe step.
        Quit = 4
    }

    [Flags]
    public enum SecurityCheck
    {
        None = 0,
        Debugger = 1 << 0,
        TracerPid = 1 << 1,
        Frida = 1 << 2,
        Root = 1 << 3,
        Emulator = 1 << 4,
        SignatureMismatch = 1 << 5,
        PackageMismatch = 1 << 6,

        // Added in 1.1.0
        Debuggable = 1 << 7,
        UntrustedInstaller = 1 << 8,
        Vpn = 1 << 9,
        NativeIntegrity = 1 << 10,
        Il2CppDumper = 1 << 11,
        HookDetected = 1 << 12,

        All = Debugger | TracerPid | Frida | Root | Emulator | SignatureMismatch | PackageMismatch |
              Debuggable | UntrustedInstaller | Vpn | NativeIntegrity | Il2CppDumper | HookDetected,

        // A sensible default that leaves out the noisier opt-in checks (emulator, VPN,
        // installer source) so a normal release doesn't false-positive on day one.
        Recommended = Debugger | TracerPid | Frida | Root | SignatureMismatch | PackageMismatch |
                      Debuggable | NativeIntegrity | Il2CppDumper | HookDetected
    }

    public struct SecurityViolation
    {
        public readonly SecurityCheck Check;
        public readonly string Detail;

        public SecurityViolation(SecurityCheck check, string detail)
        {
            Check = check;
            Detail = detail;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Detail) ? Check.ToString() : Check + ": " + Detail;
        }
    }
}
