using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityOBU.Editor
{
    // Finds or creates the single SecuritySettings asset. It has to live under a Resources
    // folder so a player build can Resources.Load it without an Editor reference.
    public static class OBUSettingsService
    {
        private const string Dir = "Assets/UnityOBU/Resources/UnityOBU";
        private const string AssetPath = Dir + "/SecuritySettings.asset";

        public static SecuritySettings GetOrCreate()
        {
            var existing = Find();
            if (existing != null)
            {
                return existing;
            }

            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }

            var settings = ScriptableObject.CreateInstance<SecuritySettings>();
            if (string.IsNullOrEmpty(settings.stringKey))
            {
                settings.stringKey = NewKey();
            }

            if (string.IsNullOrEmpty(settings.assetBundleKey))
            {
                settings.assetBundleKey = NewKey();
            }

            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return settings;
        }

        public static SecuritySettings Find()
        {
            var atPath = AssetDatabase.LoadAssetAtPath<SecuritySettings>(AssetPath);
            if (atPath != null)
            {
                return atPath;
            }

            string[] guids = AssetDatabase.FindAssets("t:SecuritySettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<SecuritySettings>(path);
            }

            return null;
        }

        public static string NewKey()
        {
            byte[] bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return System.Convert.ToBase64String(bytes);
        }
    }
}
