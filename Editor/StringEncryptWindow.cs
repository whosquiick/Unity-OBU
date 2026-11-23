using UnityEditor;
using UnityEngine;

namespace UnityOBU.Editor
{
    // A small helper: paste a sensitive literal, get back the encrypted blob to store in code.
    // At runtime you call StringCrypt.Reveal(blob) to get the plaintext back. This keeps the
    // literal out of the binary's string table, which defeats casual `strings`-style dumps.
    public class StringEncryptWindow : EditorWindow
    {
        private string _plain = "";
        private string _blob = "";

        [MenuItem("Tools/UnityOBU/Encrypt a String...")]
        public static void Open()
        {
            GetWindow<StringEncryptWindow>(true, "UnityOBU String Encrypt", true);
        }

        private void OnGUI()
        {
            SecuritySettings settings = OBUSettingsService.GetOrCreate();

            EditorGUILayout.HelpBox(
                "Encrypts with the project string key. Store the blob in code and call " +
                "StringCrypt.Reveal(blob) at runtime. The plaintext still exists in memory after " +
                "Reveal, so this hides literals from static dumps, not from a live attacker.",
                MessageType.Info);

            EditorGUILayout.LabelField("Plaintext");
            _plain = EditorGUILayout.TextArea(_plain, GUILayout.MinHeight(50));

            if (GUILayout.Button("Encrypt"))
            {
                _blob = StringCrypt.Encrypt(_plain, settings.stringKey);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Encrypted blob (base64)");
            EditorGUILayout.SelectableLabel(_blob, EditorStyles.textArea, GUILayout.MinHeight(60));

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_blob)))
            {
                if (GUILayout.Button("Copy blob to clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = _blob;
                }

                if (GUILayout.Button("Copy as C# field"))
                {
                    EditorGUIUtility.systemCopyBuffer =
                        "// decrypt with StringCrypt.Reveal(...)\n" +
                        "private const string Secret = \"" + _blob + "\";";
                }
            }
        }
    }
}
