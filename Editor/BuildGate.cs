using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityOBU.Editor
{
    // Runs the checklist right before a build. If enforcement is on and anything is a Fail,
    // the build is stopped with a BuildFailedException so a bad release setup can't slip out.
    public class BuildGate : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            SecuritySettings settings = OBUSettingsService.Find();
            if (settings == null || !settings.enforceBuildChecklist)
            {
                return;
            }

            BuildTarget target = report.summary.platform;
            List<ChecklistItem> items = BuildChecklist.Evaluate(target);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[UnityOBU] Build hardening checklist for " + target + ":");
            bool hasFail = false;
            foreach (ChecklistItem item in items)
            {
                sb.AppendLine("  [" + item.Level + "] " + item.Name + " - " + item.Message);
                if (item.Level == CheckLevel.Fail)
                {
                    hasFail = true;
                }
            }

            if (settings.writeLinkXml)
            {
                string path = LinkXmlGenerator.Generate();
                sb.AppendLine("  Wrote " + path);
            }

            if (hasFail && settings.treatChecklistAsError)
            {
                Debug.LogError(sb.ToString());
                throw new BuildFailedException(
                    "[UnityOBU] Build blocked: hardening checklist has failures. Fix the items above, " +
                    "or turn off 'Treat checklist as error' in Tools > UnityOBU > Settings to build anyway.");
            }

            if (hasFail)
            {
                Debug.LogWarning(sb.ToString());
            }
            else
            {
                Debug.Log(sb.ToString());
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            SecuritySettings settings = OBUSettingsService.Find();
            if (settings == null || !settings.enforceBuildChecklist)
            {
                return;
            }

            Debug.Log("[UnityOBU] Build finished: " + report.summary.outputPath +
                      " (result: " + report.summary.result + "). Remember client-side hardening is best-effort; " +
                      "pair it with server-side auth for anything that must stay secret.");
        }
    }
}
