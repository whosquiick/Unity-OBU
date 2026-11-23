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
        All = Debugger | TracerPid | Frida | Root | Emulator | SignatureMismatch | PackageMismatch
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
