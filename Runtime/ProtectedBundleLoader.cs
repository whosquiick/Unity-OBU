using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityOBU
{
    // Loads an OBU-encrypted AssetBundle. The file is read (from StreamingAssets or an
    // absolute path), decrypted in memory, then handed to AssetBundle.LoadFromMemoryAsync so
    // the plaintext bundle is never written back to storage.
    //
    // Caveat worth repeating: once decrypted the bundle lives in process memory, so a tool
    // that can read the process (Frida, a dumper on a rooted device) can still recover it.
    // This stops trivial file copying and static unpackers, not a live attacker.
    public static class ProtectedBundleLoader
    {
        // Load from a file inside StreamingAssets (works with the jar:// path on Android).
        public static IEnumerator LoadFromStreamingAssets(
            string relativePath, string password, Action<AssetBundle> onLoaded, Action<string> onError = null)
        {
            string full = Path.Combine(Application.streamingAssetsPath, relativePath);
            byte[] raw = null;

            if (full.Contains("://") || full.Contains(":///"))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(full))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Report(onError, "Could not read protected bundle: " + request.error);
                        yield break;
                    }

                    raw = request.downloadHandler.data;
                }
            }
            else
            {
                if (!File.Exists(full))
                {
                    Report(onError, "Protected bundle not found: " + full);
                    yield break;
                }

                raw = File.ReadAllBytes(full);
            }

            yield return LoadFromBytes(raw, password, onLoaded, onError);
        }

        // Load from raw encrypted bytes you obtained however you like (download, cache, etc).
        public static IEnumerator LoadFromBytes(
            byte[] protectedData, string password, Action<AssetBundle> onLoaded, Action<string> onError = null)
        {
            byte[] plain;
            try
            {
                plain = BundleCrypt.DecryptBytes(protectedData, password);
            }
            catch (Exception ex)
            {
                Report(onError, "Decrypt failed: " + ex.Message);
                yield break;
            }

            AssetBundleCreateRequest createRequest = AssetBundle.LoadFromMemoryAsync(plain);
            yield return createRequest;

            // Drop our reference to the plaintext as soon as the bundle owns it.
            Array.Clear(plain, 0, plain.Length);

            if (createRequest.assetBundle == null)
            {
                Report(onError, "Decrypted data was not a valid AssetBundle.");
                yield break;
            }

            onLoaded?.Invoke(createRequest.assetBundle);
        }

        private static void Report(Action<string> onError, string message)
        {
            if (onError != null)
            {
                onError(message);
            }
            else
            {
                Debug.LogError("[UnityOBU] " + message);
            }
        }
    }
}
