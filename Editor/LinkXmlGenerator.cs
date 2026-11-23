using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityOBU.Editor
{
    // Writes a link.xml that preserves every type/member tagged with [Keep]. With High
    // stripping, anything only reached through reflection or native callbacks needs to be
    // listed here or IL2CPP will strip it and you get null/MissingMethod at runtime.
    public static class LinkXmlGenerator
    {
        private const string OutputPath = "Assets/UnityOBU/link.xml";

        [MenuItem("Tools/UnityOBU/Generate link.xml")]
        public static void GenerateMenu()
        {
            string path = Generate();
            AssetDatabase.Refresh();
            Debug.Log("[UnityOBU] Wrote " + path);
            EditorUtility.RevealInFinder(path);
        }

        public static string Generate()
        {
            var byAssembly = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>();

            foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                foreach (System.Type type in types)
                {
                    if (type == null)
                    {
                        continue;
                    }

                    if (type.GetCustomAttribute<KeepAttribute>() != null)
                    {
                        Add(byAssembly, asm.GetName().Name, type.FullName);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("<linker>");
            foreach (var pair in byAssembly)
            {
                sb.AppendLine("  <assembly fullname=\"" + pair.Key + "\">");
                foreach (string typeName in pair.Value)
                {
                    sb.AppendLine("    <type fullname=\"" + typeName + "\" preserve=\"all\" />");
                }

                sb.AppendLine("  </assembly>");
            }

            // Always keep the OBU runtime; the player reflects into it for settings.
            if (!byAssembly.ContainsKey("UnityOBU.Runtime"))
            {
                sb.AppendLine("  <assembly fullname=\"UnityOBU.Runtime\" preserve=\"all\" />");
            }

            sb.AppendLine("</linker>");

            string dir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(OutputPath, sb.ToString());
            return OutputPath;
        }

        private static void Add(System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> map, string asm, string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return;
            }

            if (!map.TryGetValue(asm, out var set))
            {
                set = new System.Collections.Generic.HashSet<string>();
                map[asm] = set;
            }

            set.Add(type);
        }
    }
}
