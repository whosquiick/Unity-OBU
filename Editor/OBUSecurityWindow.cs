using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityOBU.Editor
{
    public class OBUSecurityWindow : EditorWindow
    {
        private SecuritySettings _settings;
        private SerializedObject _serialized;
        private Vector2 _scroll;
        private List<ChecklistItem> _checklist;

        [MenuItem("Tools/UnityOBU/Settings", false, 0)]
        public static void Open()
        {
            var window = GetWindow<OBUSecurityWindow>("Unity OBU");
            window.minSize = new Vector2(420, 520);
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            _settings = OBUSettingsService.GetOrCreate();
            _serialized = new SerializedObject(_settings);
            _checklist = BuildChecklist.Evaluate(EditorUserBuildSettings.activeBuildTarget);
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                Refresh();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Unity OBU raises the cost of dumping and asset ripping on Android IL2CPP builds. " +
                "It cannot make them impossible. A determined attacker with Frida, a rooted device " +
                "or an IL2CPP dumper can still recover assets and logic. Keep anything that must stay " +
                "secret on a server you control.",
                MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Build hardening checklist (" + EditorUserBuildSettings.activeBuildTarget + ")", EditorStyles.boldLabel);
            DrawChecklist();
            if (GUILayout.Button("Re-evaluate checklist"))
            {
                _checklist = BuildChecklist.Evaluate(EditorUserBuildSettings.activeBuildTarget);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            _serialized.Update();
            DrawProp("enforceBuildChecklist");
            DrawProp("treatChecklistAsError");
            DrawProp("writeLinkXml");
            EditorGUILayout.Space(4);
            DrawKeyRow("stringKey", "String key");
            DrawKeyRow("assetBundleKey", "Asset bundle key");
            EditorGUILayout.Space(4);
            DrawProp("protectIl2CppMetadata");
            DrawKeyRow("metadataKey", "Metadata key");
            EditorGUILayout.Space(4);
            DrawProp("runtimeChecksEnabled");
            DrawProp("useNativeGuard");
            DrawProp("enabledChecks");
            DrawProp("response");
            DrawProp("escalationEnabled");
            DrawProp("escalateAfterHits");
            DrawProp("escalatedResponse");
            DrawProp("recheckInterval");
            DrawProp("expectedPackageName");
            DrawProp("expectedSigningSha256");
            DrawProp("allowedInstallers");
            DrawProp("runInEditor");
            _serialized.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate link.xml"))
            {
                LinkXmlGenerator.GenerateMenu();
            }

            if (GUILayout.Button("Open String Encrypt tool"))
            {
                StringEncryptWindow.Open();
            }

            if (GUILayout.Button("Fill current package name"))
            {
                _settings.expectedPackageName = Application.identifier;
                EditorUtility.SetDirty(_settings);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawChecklist()
        {
            if (_checklist == null)
            {
                return;
            }

            foreach (ChecklistItem item in _checklist)
            {
                MessageType type = item.Level == CheckLevel.Pass ? MessageType.None
                    : item.Level == CheckLevel.Warn ? MessageType.Warning : MessageType.Error;
                string prefix = item.Level == CheckLevel.Pass ? "OK" : item.Level.ToString();
                EditorGUILayout.HelpBox(prefix + " - " + item.Name + ": " + item.Message, type);
            }
        }

        private void DrawProp(string name)
        {
            SerializedProperty prop = _serialized.FindProperty(name);
            if (prop != null)
            {
                EditorGUILayout.PropertyField(prop, true);
            }
        }

        private void DrawKeyRow(string name, string label)
        {
            EditorGUILayout.BeginHorizontal();
            SerializedProperty prop = _serialized.FindProperty(name);
            if (prop != null)
            {
                prop.stringValue = EditorGUILayout.PasswordField(label, prop.stringValue);
            }

            if (GUILayout.Button("Regenerate", GUILayout.Width(90)))
            {
                if (prop != null)
                {
                    prop.stringValue = OBUSettingsService.NewKey();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
