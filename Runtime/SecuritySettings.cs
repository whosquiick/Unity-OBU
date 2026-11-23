using System.Collections.Generic;
using UnityEngine;

namespace UnityOBU
{
    // Lives in Resources so a runtime build can load it without an Editor reference.
    // The Editor window writes this asset; the runtime guard reads it.
    public class SecuritySettings : ScriptableObject
    {
        public const string ResourcePath = "UnityOBU/SecuritySettings";

        [Header("Build gate")]
        [Tooltip("Fail the build when the active target is not set up the way the checklist wants (IL2CPP, high stripping, no dev build).")]
        public bool enforceBuildChecklist = true;

        [Tooltip("Treat checklist warnings as hard errors instead of logging them.")]
        public bool treatChecklistAsError = true;

        [Tooltip("Write a generated link.xml next to the build to keep preservation rules explicit.")]
        public bool writeLinkXml = true;

        [Header("String encryption")]
        [Tooltip("Key material used by StringCrypt. Changing it invalidates strings already baked by the weaver menu.")]
        public string stringKey = "";

        [Header("Asset protection")]
        [Tooltip("Key used to encrypt and decrypt protected asset bundles. Keep it out of source control for real projects.")]
        public string assetBundleKey = "";

        [Header("Runtime checks")]
        public bool runtimeChecksEnabled = true;

        [Tooltip("Which heuristics to run. All of these are best-effort and can be bypassed.")]
        public SecurityCheck enabledChecks = SecurityCheck.All;

        [Tooltip("How the app reacts the first time a check trips.")]
        public SecurityResponse response = SecurityResponse.LogOnly;

        [Tooltip("Seconds between periodic re-checks after startup. Zero means startup only.")]
        public float recheckInterval = 20f;

        [Tooltip("Expected application id (package name). Leave blank to skip the package check.")]
        public string expectedPackageName = "";

        [Tooltip("SHA-256 (hex, lowercase) of the signing certificate you ship with. Leave blank to skip the signature check.")]
        public string expectedSigningSha256 = "";

        [Header("Editor only")]
        [Tooltip("Run the runtime checks while playing in the Editor. Off by default because the Editor always looks 'debuggable'.")]
        public bool runInEditor = false;

        private static SecuritySettings _cached;

        public static SecuritySettings LoadOrDefault()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<SecuritySettings>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<SecuritySettings>();
                _cached.name = "SecuritySettings (runtime default)";
            }

            return _cached;
        }

        public IEnumerable<SecurityCheck> EnabledCheckList()
        {
            foreach (SecurityCheck value in System.Enum.GetValues(typeof(SecurityCheck)))
            {
                if (value == SecurityCheck.None || value == SecurityCheck.All)
                {
                    continue;
                }

                if ((enabledChecks & value) == value)
                {
                    yield return value;
                }
            }
        }
    }
}
