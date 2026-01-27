using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BoostOps.Editor
{
    /// <summary>
    /// Build processor that handles BoostOps Android dependencies at build time
    /// Similar to AppsFlyer and Google Mobile Ads approach
    /// </summary>
    public class BoostOpsAndroidBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            Debug.Log("[BoostOps] Processing Android dependencies for build...");
            
            // Ensure the standard Plugins/Android directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Plugins"))
            {
                AssetDatabase.CreateFolder("Assets", "Plugins");
            }
            
            if (!AssetDatabase.IsValidFolder("Assets/Plugins/Android"))
            {
                AssetDatabase.CreateFolder("Assets/Plugins", "Android");
            }

            // Copy our dependencies file to the standard location for EDM4U processing
            string sourcePath = "Assets/BoostOps/Editor/BoostOpsAndroidDependencies.xml";
            string targetPath = "Assets/Plugins/Android/BoostOpsAndroidDependencies.xml";
            
            if (File.Exists(sourcePath))
            {
                // Copy the dependencies file to where EDM4U expects it
                AssetDatabase.CopyAsset(sourcePath, targetPath);
                Debug.Log($"[BoostOps] ✅ Copied Android dependencies to: {targetPath}");
                
                // Refresh to ensure Unity sees the new file
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogWarning($"[BoostOps] ⚠️ Android dependencies file not found at: {sourcePath}");
            }
        }
    }
}

