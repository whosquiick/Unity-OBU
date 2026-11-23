using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityOBU.Editor
{
    // Optional, advanced, and realistic about its limits.
    //
    // IL2CPP reads global-metadata.dat at startup to rebuild type/method metadata. A static
    // IL2CPP dumper reads that file straight out of the APK. Obfuscating it on disk forces an
    // attacker to recover it from memory or hook the loader instead of unzipping the APK, which
    // is a real jump in effort. It does NOT stop a dynamic dump: once the game runs, the real
    // metadata is in memory.
    //
    // The on-disk transform here only works if you can both (a) reach the file at build time and
    // (b) restore it before IL2CPP reads it. (a) means "Export Project" (Gradle), because the
    // file is editable on disk there; inside a built APK/AAB it's zipped and editing it means
    // repackaging and re-signing. (b) means a native hook in your own .so that de-obfuscates the
    // bytes before il2cpp_init. That hook is engine-version specific and is NOT shipped compiled;
    // native~/README.md documents where it goes. Leave this OFF unless you have that hook.
    public class Il2CppMetadataProtector : IPostprocessBuildWithReport
    {
        public int callbackOrder => 50;

        private const string MetadataRelative = "bin/Data/Managed/Metadata/global-metadata.dat";

        public void OnPostprocessBuild(BuildReport report)
        {
            SecuritySettings settings = OBUSettingsService.Find();
            if (settings == null || !settings.protectIl2CppMetadata)
            {
                return;
            }

            if (report.summary.platform != BuildTarget.Android)
            {
                Debug.LogWarning("[UnityOBU] IL2CPP metadata protection only runs for Android; skipping.");
                return;
            }

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                Debug.LogWarning("[UnityOBU] Metadata protection needs IL2CPP; skipping.");
                return;
            }

            string outPath = report.summary.outputPath;
            if (string.IsNullOrEmpty(outPath))
            {
                return;
            }

            if (File.Exists(outPath) && (outPath.EndsWith(".apk") || outPath.EndsWith(".aab")))
            {
                Debug.LogWarning("[UnityOBU] Output is a packaged " + Path.GetExtension(outPath) +
                    ". On-disk metadata protection needs 'Export Project' (Gradle) so the file is editable, " +
                    "or a repackage+resign step you add yourself. Skipping.");
                return;
            }

            string file = FindMetadata(outPath);
            if (file == null)
            {
                Debug.LogWarning("[UnityOBU] Could not find global-metadata.dat under " + outPath + "; skipping.");
                return;
            }

            if (string.IsNullOrEmpty(settings.metadataKey))
            {
                settings.metadataKey = OBUSettingsService.NewKey();
                EditorUtility.SetDirty(settings);
            }

            byte[] data = File.ReadAllBytes(file);
            if (IsAlreadyProtected(data))
            {
                Debug.Log("[UnityOBU] global-metadata.dat already protected; skipping.");
                return;
            }

            byte[] keystream = Keystream(settings.metadataKey, data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= keystream[i];
            }

            // Flip the first bytes of the sanity header so a static dumper rejects the file.
            File.WriteAllBytes(file, data);

            Debug.LogWarning("[UnityOBU] Obfuscated " + file + ".\n" +
                "IL2CPP will NOT boot until your native hook restores it with the same key before il2cpp_init. " +
                "See Runtime/Plugins/Android/native~/README.md. This only raises cost; a memory dump still recovers metadata.");
        }

        private static string FindMetadata(string root)
        {
            // Common Gradle export layouts.
            string[] candidates =
            {
                Path.Combine(root, "unityLibrary/src/main/assets/" + MetadataRelative),
                Path.Combine(root, "src/main/assets/" + MetadataRelative),
                Path.Combine(root, "assets/" + MetadataRelative)
            };

            foreach (string c in candidates)
            {
                if (File.Exists(c))
                {
                    return c;
                }
            }

            try
            {
                foreach (string f in Directory.GetFiles(root, "global-metadata.dat", SearchOption.AllDirectories))
                {
                    return f;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static byte[] Keystream(string key, int length)
        {
            byte[] seed;
            using (var sha = SHA256.Create())
            {
                seed = sha.ComputeHash(Encoding.UTF8.GetBytes("obu-metadata:" + key));
            }

            var outp = new byte[length];
            int pos = 0;
            byte[] block = seed;
            while (pos < length)
            {
                using (var sha = SHA256.Create())
                {
                    block = sha.ComputeHash(block);
                }

                int take = Math.Min(block.Length, length - pos);
                Buffer.BlockCopy(block, 0, outp, pos, take);
                pos += take;
            }

            return outp;
        }

        // A tiny marker is risky (a dumper could strip it), so instead we simply never double-XOR:
        // we detect the known IL2CPP magic. If it's present the file is still plaintext.
        private static bool IsAlreadyProtected(byte[] data)
        {
            if (data.Length < 4)
            {
                return false;
            }

            // IL2CPP metadata magic is 0xFAB11BAF (little-endian AF 1B B1 FA).
            bool plaintextMagic = data[0] == 0xAF && data[1] == 0x1B && data[2] == 0xB1 && data[3] == 0xFA;
            return !plaintextMagic;
        }
    }
}
