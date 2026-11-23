using System;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;
#endif

namespace UnityOBU
{
    // Bridge to the optional libobunative.so. Prefers the IL2CPP DllImport entry points; if the
    // library isn't present it reports unavailable and the caller uses the pure-C# probes. All
    // calls are wrapped so a missing symbol never throws into gameplay.
    public static class NativeGuard
    {
        // Mirrors the TamperBit enum in obu_native.cpp.
        [Flags]
        public enum NativeFlags
        {
            None = 0,
            Tracer = 1 << 0,
            Frida = 1 << 1,
            Hook = 1 << 2,
            TextCrc = 1 << 3,
            Dumper = 1 << 4
        }

        private static bool _probed;
        private static bool _available;

        public static bool IsAvailable
        {
            get
            {
                if (!_probed)
                {
                    _available = Probe();
                    _probed = true;
                }

                return _available;
            }
        }

        // Starts the native watcher threads (ptrace guard + monitor). Safe to call repeatedly.
        public static void Start()
        {
            if (!IsAvailable)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                obu_start();
            }
            catch (Exception)
            {
                _available = false;
            }
#endif
        }

        // Returns the accumulated native tamper flags, or None when the library is absent.
        public static NativeFlags Poll()
        {
            if (!IsAvailable)
            {
                return NativeFlags.None;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return (NativeFlags)obu_poll();
            }
            catch (Exception)
            {
                _available = false;
            }
#endif
            return NativeFlags.None;
        }

        public static int TracerPid()
        {
            if (!IsAvailable)
            {
                return -1;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return obu_tracer_pid();
            }
            catch (Exception)
            {
                _available = false;
            }
#endif
            return -1;
        }

        private static bool Probe()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Touch a cheap entry point. A missing library throws DllNotFoundException here.
                obu_tracer_pid();
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport("obunative", EntryPoint = "obu_start")]
        private static extern int obu_start();

        [DllImport("obunative", EntryPoint = "obu_poll")]
        private static extern int obu_poll();

        [DllImport("obunative", EntryPoint = "obu_tracer_pid")]
        private static extern int obu_tracer_pid();
#endif
    }
}
