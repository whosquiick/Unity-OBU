using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityOBU
{
    // The central entry point. Callers subscribe to Violated, register anything sensitive they
    // want wiped on a trip, and either let OBUSecurityBootstrap drive the checks or call
    // RunChecks themselves.
    public static class OBUSecurity
    {
        // Raised once per tripped check. Fired on the main thread from the guard.
        public static event Action<SecurityViolation> Violated;

        private static readonly List<Action> WipeHandlers = new List<Action>();
        private static readonly HashSet<SecurityCheck> AlreadyReported = new HashSet<SecurityCheck>();

        public static bool AnyViolationSeen { get; private set; }
        public static int DistinctViolationCount { get { return AlreadyReported.Count; } }

        public static void RegisterWipeHandler(Action handler)
        {
            if (handler != null && !WipeHandlers.Contains(handler))
            {
                WipeHandlers.Add(handler);
            }
        }

        public static void UnregisterWipeHandler(Action handler)
        {
            WipeHandlers.Remove(handler);
        }

        // Runs the enabled checks once and applies the configured response. Returns the list
        // of violations seen this pass (empty if clean).
        public static List<SecurityViolation> RunChecks(SecuritySettings settings)
        {
            var hits = new List<SecurityViolation>();
            if (settings == null || !settings.runtimeChecksEnabled)
            {
                return hits;
            }

            foreach (SecurityCheck check in settings.EnabledCheckList())
            {
                if (TryCheck(settings, check, out string detail))
                {
                    hits.Add(new SecurityViolation(check, detail));
                }
            }

            foreach (SecurityViolation v in hits)
            {
                Handle(settings, v);
            }

            return hits;
        }

        private static bool TryCheck(SecuritySettings settings, SecurityCheck check, out string detail)
        {
            detail = null;
            switch (check)
            {
                case SecurityCheck.Debugger:
                    return AndroidSecurityProbe.IsDebuggerAttached(out detail);
                case SecurityCheck.TracerPid:
                    return AndroidSecurityProbe.HasTracer(out detail);
                case SecurityCheck.Frida:
                    return AndroidSecurityProbe.LooksLikeFrida(out detail);
                case SecurityCheck.Root:
                    return AndroidSecurityProbe.LooksRooted(out detail);
                case SecurityCheck.Emulator:
                    return AndroidSecurityProbe.LooksLikeEmulator(out detail);
                case SecurityCheck.SignatureMismatch:
                    return !AndroidSecurityProbe.SignatureMatches(settings.expectedSigningSha256, out detail);
                case SecurityCheck.PackageMismatch:
                    return PackageMismatch(settings, out detail);
                case SecurityCheck.Debuggable:
                    return AndroidSecurityProbe.IsDebuggable(out detail);
                case SecurityCheck.UntrustedInstaller:
                    return AndroidSecurityProbe.UntrustedInstaller(settings.allowedInstallers, out detail);
                case SecurityCheck.Vpn:
                    return AndroidSecurityProbe.VpnActive(out detail);
                case SecurityCheck.NativeIntegrity:
                    return AndroidSecurityProbe.NativeIntegrityFailed(out detail);
                case SecurityCheck.Il2CppDumper:
                    return AndroidSecurityProbe.LooksLikeIl2CppDumper(out detail);
                case SecurityCheck.HookDetected:
                    return AndroidSecurityProbe.LooksHooked(out detail);
                default:
                    return false;
            }
        }

        private static bool PackageMismatch(SecuritySettings settings, out string detail)
        {
            detail = null;
            if (string.IsNullOrEmpty(settings.expectedPackageName))
            {
                return false;
            }

            string actual = Application.identifier;
            if (!string.Equals(actual, settings.expectedPackageName.Trim(), StringComparison.Ordinal))
            {
                detail = actual + " != " + settings.expectedPackageName.Trim();
                return true;
            }

            return false;
        }

        private static void Handle(SecuritySettings settings, SecurityViolation violation)
        {
            AnyViolationSeen = true;

            bool firstTime = AlreadyReported.Add(violation.Check);
            if (firstTime)
            {
                Debug.LogWarning("[UnityOBU] security signal: " + violation);
                try
                {
                    Violated?.Invoke(violation);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[UnityOBU] violation handler threw: " + ex);
                }
            }

            SecurityResponse response = settings.response;
            if (settings.escalationEnabled && AlreadyReported.Count >= Math.Max(1, settings.escalateAfterHits))
            {
                response = settings.escalatedResponse;
            }

            Apply(response);
        }

        private static void Apply(SecurityResponse response)
        {
            switch (response)
            {
                case SecurityResponse.LogOnly:
                case SecurityResponse.CallbackOnly:
                    break;
                case SecurityResponse.WipeSensitiveData:
                    WipeSensitive();
                    break;
                case SecurityResponse.WipeAndQuit:
                    WipeSensitive();
                    QuitApp();
                    break;
                case SecurityResponse.Quit:
                    QuitApp();
                    break;
            }
        }

        public static void WipeSensitive()
        {
            foreach (Action handler in WipeHandlers)
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[UnityOBU] wipe handler threw: " + ex);
                }
            }
        }

        private static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public static void ResetReportedState()
        {
            AlreadyReported.Clear();
            AnyViolationSeen = false;
        }
    }
}
