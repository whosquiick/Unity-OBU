using System;
using System.IO;
using UnityEngine;

namespace UnityOBU
{
    // Best-effort device/process probes for Android. Every method here is a heuristic.
    // Root tools, Frida and emulators all have ways to hide, and a false positive on a weird
    // OEM ROM is possible, so the guard treats these as signals, not proof.
    //
    // On non-Android platforms (and in the Editor) these return "not detected" so the same
    // code path runs everywhere without #if noise leaking into callers.
    public static class AndroidSecurityProbe
    {
        public static bool IsDebuggerAttached(out string detail)
        {
            detail = null;
            if (System.Diagnostics.Debugger.IsAttached)
            {
                detail = "managed debugger attached";
                return true;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var debug = new AndroidJavaClass("android.os.Debug"))
                {
                    if (debug.CallStatic<bool>("isDebuggerConnected"))
                    {
                        detail = "android.os.Debug reports a connected debugger";
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // Probe unavailable; treat as inconclusive.
            }
#endif
            return false;
        }

        // TracerPid in /proc/self/status is non-zero when something is ptrace-attached,
        // which covers most native debuggers and some dumpers.
        public static bool HasTracer(out string detail)
        {
            detail = null;
            try
            {
                const string path = "/proc/self/status";
                if (!File.Exists(path))
                {
                    return false;
                }

                foreach (string line in File.ReadAllLines(path))
                {
                    if (!line.StartsWith("TracerPid:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string value = line.Substring("TracerPid:".Length).Trim();
                    if (int.TryParse(value, out int pid) && pid != 0)
                    {
                        detail = "TracerPid=" + pid;
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception)
            {
                // /proc not readable on this platform.
            }

            return false;
        }

        // Looks for Frida's tell-tales: mapped gadget/agent libraries and its default ports.
        public static bool LooksLikeFrida(out string detail)
        {
            detail = null;
            try
            {
                const string maps = "/proc/self/maps";
                if (File.Exists(maps))
                {
                    string content = File.ReadAllText(maps);
                    if (content.IndexOf("frida", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        content.IndexOf("gum-js-loop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        content.IndexOf("gmain", StringComparison.OrdinalIgnoreCase) >= 0 && content.IndexOf("linjector", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        detail = "frida-like library mapped in process";
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }

            if (DefaultFridaPortOpen(out string port))
            {
                detail = "listener on " + port;
                return true;
            }

            return false;
        }

        public static bool LooksRooted(out string detail)
        {
            detail = null;
            string[] suspects =
            {
                "/system/bin/su", "/system/xbin/su", "/sbin/su", "/system/app/Superuser.apk",
                "/system/bin/magisk", "/data/adb/magisk", "/data/adb/modules",
                "/system/xbin/daemonsu", "/su/bin/su"
            };

            foreach (string p in suspects)
            {
                try
                {
                    if (File.Exists(p) || Directory.Exists(p))
                    {
                        detail = "found " + p;
                        return true;
                    }
                }
                catch (Exception)
                {
                }
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build"))
                {
                    string tags = build.GetStatic<string>("TAGS");
                    if (!string.IsNullOrEmpty(tags) && tags.Contains("test-keys"))
                    {
                        detail = "build signed with test-keys";
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

        public static bool LooksLikeEmulator(out string detail)
        {
            detail = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build"))
                {
                    string fingerprint = Safe(build, "FINGERPRINT");
                    string model = Safe(build, "MODEL");
                    string product = Safe(build, "PRODUCT");
                    string hardware = Safe(build, "HARDWARE");

                    if (fingerprint.StartsWith("generic") || fingerprint.Contains("vbox") || fingerprint.Contains("test-keys") && model.Contains("sdk") ||
                        model.Contains("Emulator") || model.Contains("Android SDK built for") ||
                        product.Contains("sdk") || product.Contains("vbox") || product.Contains("emulator") ||
                        hardware.Contains("goldfish") || hardware.Contains("ranchu") || hardware.Contains("vbox"))
                    {
                        detail = "emulator-like build props (" + hardware + ")";
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

        // Compares the running app's signing certificate SHA-256 against the expected value.
        public static bool SignatureMatches(string expectedSha256Hex, out string detail)
        {
            detail = null;
            if (string.IsNullOrEmpty(expectedSha256Hex))
            {
                return true; // nothing to compare against
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string actual = CurrentSigningSha256();
                if (string.IsNullOrEmpty(actual))
                {
                    detail = "could not read signing cert";
                    return false;
                }

                if (!string.Equals(actual, expectedSha256Hex.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                {
                    detail = "signing cert " + actual + " != expected";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                detail = "signature check error: " + ex.Message;
                return false;
            }
#else
            return true;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string CurrentSigningSha256()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
            {
                string pkg = activity.Call<string>("getPackageName");
                const int getSignatures = 0x40;
                using (var info = pm.Call<AndroidJavaObject>("getPackageInfo", pkg, getSignatures))
                {
                    var signatures = info.Get<AndroidJavaObject[]>("signatures");
                    if (signatures == null || signatures.Length == 0)
                    {
                        return null;
                    }

                    byte[] certBytes = signatures[0].Call<byte[]>("toByteArray");
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                    {
                        byte[] hash = sha.ComputeHash(certBytes);
                        var sb = new System.Text.StringBuilder(hash.Length * 2);
                        foreach (byte b in hash)
                        {
                            sb.Append(b.ToString("x2"));
                        }

                        return sb.ToString();
                    }
                }
            }
        }

        private static string Safe(AndroidJavaClass build, string field)
        {
            try
            {
                return build.GetStatic<string>(field) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
#endif

        private static bool DefaultFridaPortOpen(out string port)
        {
            port = null;
            int[] ports = { 27042, 27043 };
            foreach (int p in ports)
            {
                try
                {
                    using (var client = new System.Net.Sockets.TcpClient())
                    {
                        var result = client.BeginConnect("127.0.0.1", p, null, null);
                        bool ok = result.AsyncWaitHandle.WaitOne(60);
                        if (ok && client.Connected)
                        {
                            port = "127.0.0.1:" + p;
                            return true;
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            return false;
        }
    }
}
