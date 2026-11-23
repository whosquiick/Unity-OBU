using System;
using System.IO;
using UnityEngine;

namespace UnityOBU
{
    // 1.1.0 additions: repackaging/debuggable/installer/VPN checks, plus hook and IL2CPP
    // dumper heuristics layered on top of the maps scan. Same rule as the rest of the probe:
    // these are signals, not proof, and a rooted device can defeat them.
    public static partial class AndroidSecurityProbe
    {
        // The android:debuggable flag should be false in a release build. If it's true, either
        // the build was made debuggable or the APK was repackaged to allow attaching.
        public static bool IsDebuggable(out string detail)
        {
            detail = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var info = activity.Call<AndroidJavaObject>("getApplicationInfo"))
                {
                    int flags = info.Get<int>("flags");
                    const int FLAG_DEBUGGABLE = 1 << 1;
                    if ((flags & FLAG_DEBUGGABLE) != 0)
                    {
                        detail = "application is marked debuggable";
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                detail = "debuggable check error: " + ex.Message;
            }
#endif
            return false;
        }

        // Compares the installer package against an allowlist. Off unless you pass expected
        // installers; sideloaded builds will trip this, which is the point for store-only apps.
        public static bool UntrustedInstaller(string allowedCsv, out string detail)
        {
            detail = null;
            if (string.IsNullOrEmpty(allowedCsv))
            {
                return false;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string installer = CurrentInstaller();
                if (string.IsNullOrEmpty(installer))
                {
                    detail = "no installer recorded (sideloaded or adb)";
                    return true;
                }

                foreach (string allowed in allowedCsv.Split(','))
                {
                    if (string.Equals(allowed.Trim(), installer, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                detail = "installed by " + installer;
                return true;
            }
            catch (Exception ex)
            {
                detail = "installer check error: " + ex.Message;
                return false;
            }
#else
            return false;
#endif
        }

        public static bool VpnActive(out string detail)
        {
            detail = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Named VPN interfaces are the cheap tell. A proxy MITM often rides a tun/ppp link.
                foreach (string iface in Directory.GetDirectories("/sys/class/net"))
                {
                    string name = Path.GetFileName(iface);
                    if (name.StartsWith("tun") || name.StartsWith("ppp") || name.StartsWith("ipsec") || name.StartsWith("wg"))
                    {
                        detail = "vpn-like interface " + name;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }
#endif
            return false;
        }

        // Hook frameworks beyond Frida: Xposed/LSPosed/Riru, Substrate, Dobby/ShadowHook.
        public static bool LooksHooked(out string detail)
        {
            detail = null;
            if (MapsContainsAny(new[] { "xposed", "lsposed", "riru", "edxposed", "substrate", "dobby", "shadowhook", "whale", "epic." }, out string hit))
            {
                detail = "hook framework mapped: " + hit;
                return true;
            }

            string[] files = { "/system/framework/XposedBridge.jar", "/system/lib/libriru.so", "/system/lib64/libriru.so" };
            foreach (string f in files)
            {
                try
                {
                    if (File.Exists(f))
                    {
                        detail = "found " + f;
                        return true;
                    }
                }
                catch (Exception)
                {
                }
            }

            return false;
        }

        // Known IL2CPP dumpers leave their own modules or helper libs mapped while running.
        public static bool LooksLikeIl2CppDumper(out string detail)
        {
            detail = null;
            if (MapsContainsAny(new[] { "il2cppdumper", "il2cpp_dumper", "zygisk", "melonloader", "sharprijndael", "libll2cpp" }, out string hit))
            {
                detail = "dumper module mapped: " + hit;
                return true;
            }

            return false;
        }

        // Pulls native tamper flags from libobunative.so if it shipped.
        public static bool NativeIntegrityFailed(out string detail)
        {
            detail = null;
            if (!NativeGuard.IsAvailable)
            {
                return false;
            }

            NativeGuard.NativeFlags flags = NativeGuard.Poll();
            if (flags == NativeGuard.NativeFlags.None)
            {
                return false;
            }

            detail = "native flags: " + flags;
            return true;
        }

        private static bool MapsContainsAny(string[] needles, out string hit)
        {
            hit = null;
            try
            {
                const string maps = "/proc/self/maps";
                if (!File.Exists(maps))
                {
                    return false;
                }

                string content = File.ReadAllText(maps);
                foreach (string needle in needles)
                {
                    if (content.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hit = needle;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string CurrentInstaller()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
            {
                string pkg = activity.Call<string>("getPackageName");
                try
                {
                    // API 30+: InstallSourceInfo.getInstallingPackageName()
                    using (var src = pm.Call<AndroidJavaObject>("getInstallSourceInfo", pkg))
                    {
                        string name = src.Call<string>("getInstallingPackageName");
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }
                catch (Exception)
                {
                    // Fall through to the deprecated call on older devices.
                }

                return pm.Call<string>("getInstallerPackageName", pkg);
            }
        }
#endif
    }
}
