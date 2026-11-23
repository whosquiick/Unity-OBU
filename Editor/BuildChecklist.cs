using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace UnityOBU.Editor
{
    public enum CheckLevel
    {
        Pass,
        Warn,
        Fail
    }

    public struct ChecklistItem
    {
        public string Name;
        public CheckLevel Level;
        public string Message;

        public ChecklistItem(string name, CheckLevel level, string message)
        {
            Name = name;
            Level = level;
            Message = message;
        }
    }

    // Reads PlayerSettings for the active target and reports whether the build is set up the
    // way the hardening guide wants. Pure read-only evaluation so the window and the build
    // gate can share it.
    public static class BuildChecklist
    {
        public static List<ChecklistItem> Evaluate(BuildTarget target)
        {
            var items = new List<ChecklistItem>();
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var named = NamedBuildTarget.FromBuildTargetGroup(group);

            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(named);
            items.Add(backend == ScriptingImplementation.IL2CPP
                ? new ChecklistItem("Scripting backend", CheckLevel.Pass, "IL2CPP")
                : new ChecklistItem("Scripting backend", CheckLevel.Fail,
                    "Backend is " + backend + ". IL2CPP compiles to native and has no IL metadata for dnSpy-style decompilers. Mono builds hand out your DLLs."));

            ManagedStrippingLevel stripping = PlayerSettings.GetManagedStrippingLevel(named);
            if (stripping == ManagedStrippingLevel.High)
            {
                items.Add(new ChecklistItem("Managed stripping", CheckLevel.Pass, "High"));
            }
            else if (stripping == ManagedStrippingLevel.Medium)
            {
                items.Add(new ChecklistItem("Managed stripping", CheckLevel.Warn,
                    "Medium. High removes more unused metadata; make sure your link.xml preserves reflection targets first."));
            }
            else
            {
                items.Add(new ChecklistItem("Managed stripping", CheckLevel.Fail,
                    "Set to " + stripping + ". Use High (with a link.xml) to strip unused types and names."));
            }

            bool dev = EditorUserBuildSettings.development;
            items.Add(dev
                ? new ChecklistItem("Development build", CheckLevel.Fail,
                    "Development Build is ON. It ships debug symbols and opens the profiler/debugger. Turn it off for release.")
                : new ChecklistItem("Development build", CheckLevel.Pass, "Off"));

            items.Add(EditorUserBuildSettings.allowDebugging
                ? new ChecklistItem("Script debugging", CheckLevel.Fail, "Script Debugging is ON. Turn it off for release.")
                : new ChecklistItem("Script debugging", CheckLevel.Pass, "Off"));

            if (target == BuildTarget.Android)
            {
                items.Add(PlayerSettings.Android.targetArchitectures != 0
                    ? new ChecklistItem("Android ABIs", CheckLevel.Pass, PlayerSettings.Android.targetArchitectures.ToString())
                    : new ChecklistItem("Android ABIs", CheckLevel.Warn, "No target architecture selected."));

                items.Add(PlayerSettings.Android.minifyRelease
                    ? new ChecklistItem("Minify (R8)", CheckLevel.Pass, "Release minification on")
                    : new ChecklistItem("Minify (R8)", CheckLevel.Warn,
                        "R8 minification is off. Turning it on shrinks and obfuscates the Java/Kotlin layer."));
            }

            bool engineStrip = PlayerSettings.stripEngineCode;
            items.Add(engineStrip
                ? new ChecklistItem("Strip engine code", CheckLevel.Pass, "On")
                : new ChecklistItem("Strip engine code", CheckLevel.Warn, "Strip Engine Code is off."));

            return items;
        }

        public static bool HasFailures(List<ChecklistItem> items)
        {
            foreach (var i in items)
            {
                if (i.Level == CheckLevel.Fail)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
