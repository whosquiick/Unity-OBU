using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityOBU.Editor
{
    // Builds an AssetBundle from the assets you select, then encrypts it to an .obubundle that
    // ships in StreamingAssets. Load it at runtime with ProtectedBundleLoader.
    public static class AssetProtectionMenu
    {
        private const string StagingDir = "Temp/UnityOBU_BundleStaging";

        [MenuItem("Assets/UnityOBU/Encrypt Selection into Protected Bundle", false, 2000)]
        public static void EncryptSelection()
        {
            Object[] selection = Selection.objects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("UnityOBU", "Select one or more assets (or a folder) first.", "OK");
                return;
            }

            SecuritySettings settings = OBUSettingsService.GetOrCreate();
            if (string.IsNullOrEmpty(settings.assetBundleKey))
            {
                settings.assetBundleKey = OBUSettingsService.NewKey();
                EditorUtility.SetDirty(settings);
            }

            string bundleName = "protected";
            var names = new System.Collections.Generic.List<string>();
            foreach (Object obj in selection)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { path }))
                    {
                        string child = AssetDatabase.GUIDToAssetPath(guid);
                        if (!AssetDatabase.IsValidFolder(child))
                        {
                            names.Add(child);
                        }
                    }
                }
                else
                {
                    names.Add(path);
                }
            }

            if (names.Count == 0)
            {
                EditorUtility.DisplayDialog("UnityOBU", "Nothing bundle-able in the selection.", "OK");
                return;
            }

            if (Directory.Exists(StagingDir))
            {
                Directory.Delete(StagingDir, true);
            }

            Directory.CreateDirectory(StagingDir);

            var build = new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = names.ToArray()
            };

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                StagingDir, new[] { build }, BuildAssetBundleOptions.None, target);

            if (manifest == null)
            {
                EditorUtility.DisplayDialog("UnityOBU", "AssetBundle build failed. Check the console.", "OK");
                return;
            }

            string plainBundle = Path.Combine(StagingDir, bundleName);
            if (!File.Exists(plainBundle))
            {
                EditorUtility.DisplayDialog("UnityOBU", "Built manifest but no bundle file at " + plainBundle, "OK");
                return;
            }

            string outDir = "Assets/StreamingAssets/Protected";
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            string outPath = Path.Combine(outDir, bundleName + ".obubundle");
            BundleCrypt.EncryptFile(plainBundle, outPath, settings.assetBundleKey);

            AssetDatabase.Refresh();
            Debug.Log("[UnityOBU] Wrote protected bundle: " + outPath +
                      "\nLoad it with ProtectedBundleLoader.LoadFromStreamingAssets(\"Protected/" +
                      bundleName + ".obubundle\", settings.assetBundleKey, ...).");
            EditorUtility.RevealInFinder(outPath);
        }

        // Encrypts an already-built bundle file you point at, without rebuilding.
        [MenuItem("Tools/UnityOBU/Encrypt Existing Bundle File...")]
        public static void EncryptExistingFile()
        {
            string src = EditorUtility.OpenFilePanel("Pick a built AssetBundle", Application.dataPath, "");
            if (string.IsNullOrEmpty(src))
            {
                return;
            }

            string dst = EditorUtility.SaveFilePanel("Save protected bundle", Application.dataPath, Path.GetFileName(src), "obubundle");
            if (string.IsNullOrEmpty(dst))
            {
                return;
            }

            SecuritySettings settings = OBUSettingsService.GetOrCreate();
            BundleCrypt.EncryptFile(src, dst, settings.assetBundleKey);
            AssetDatabase.Refresh();
            Debug.Log("[UnityOBU] Encrypted " + src + " -> " + dst);
        }
    }
}
