using System.Collections;
using UnityEngine;

namespace UnityOBU
{
    // Drop one of these in your first-loaded scene (the Samples prefab is pre-wired). It loads
    // the settings asset, runs the checks at startup and, if an interval is set, keeps
    // re-checking so a debugger or injector attached after launch still gets noticed.
    [AddComponentMenu("UnityOBU/OBU Security Bootstrap")]
    [DefaultExecutionOrder(-10000)]
    public class OBUSecurityBootstrap : MonoBehaviour
    {
        [Tooltip("Leave empty to load the asset from Resources/UnityOBU/SecuritySettings.")]
        [SerializeField] private SecuritySettings overrideSettings;

        [Tooltip("Survive scene loads so periodic re-checks keep running.")]
        [SerializeField] private bool persistAcrossScenes = true;

        private SecuritySettings _settings;

        private void Awake()
        {
            _settings = overrideSettings != null ? overrideSettings : SecuritySettings.LoadOrDefault();

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!_settings.runInEditor)
            {
                return;
            }
#endif
            if (!_settings.runtimeChecksEnabled)
            {
                return;
            }

            if (_settings.useNativeGuard)
            {
                NativeGuard.Start();
            }

            OBUSecurity.RunChecks(_settings);

            if (_settings.recheckInterval > 0.01f)
            {
                StartCoroutine(RecheckLoop());
            }
        }

        private IEnumerator RecheckLoop()
        {
            var wait = new WaitForSeconds(_settings.recheckInterval);
            while (true)
            {
                yield return wait;
                OBUSecurity.RunChecks(_settings);
            }
        }
    }
}
