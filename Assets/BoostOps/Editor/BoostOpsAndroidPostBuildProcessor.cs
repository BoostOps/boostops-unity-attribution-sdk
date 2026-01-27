using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BoostOps.Editor
{
    /// <summary>
    /// Post-build processor that cleans up temporary Android dependency files
    /// </summary>
    public class BoostOpsAndroidPostBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            // Clean up the temporary dependencies file we copied during pre-build
            string tempDependenciesPath = "Assets/Plugins/Android/BoostOpsAndroidDependencies.xml";
            
            if (File.Exists(tempDependenciesPath))
            {
                AssetDatabase.DeleteAsset(tempDependenciesPath);
                Debug.Log("[BoostOps] 🧹 Cleaned up temporary Android dependencies file");
                AssetDatabase.Refresh();
            }
        }
    }
}

