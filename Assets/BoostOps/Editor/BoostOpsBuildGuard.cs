#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BoostOps.Editor
{
    /// <summary>
    /// Build guard to check for analytics symbol consistency before builds
    /// Warns if there are mismatches between standard and BoostOps custom defines
    /// </summary>
    public class BoostOpsBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var defines = GetDefinesForTargetGroup(report.summary.platformGroup);
            
            CheckFirebaseAnalyticsConsistency(string.Join(";", defines), report.summary.platformGroup);
            CheckUnityAnalyticsConsistency(string.Join(";", defines), report.summary.platformGroup);
        }
        
        /// <summary>
        /// Safely get scripting defines for target group using best available API
        /// </summary>
        private static string GetDefinesForTargetGroup(BuildTargetGroup targetGroup)
        {
            try
            {
                // Try to use NamedBuildTarget approach (Unity 2022.2+)
                var namedBuildTargetType = System.Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
                if (namedBuildTargetType != null)
                {
                    var fromBuildTargetGroupMethod = namedBuildTargetType.GetMethod("FromBuildTargetGroup", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (fromBuildTargetGroupMethod != null)
                    {
                        var namedBuildTarget = fromBuildTargetGroupMethod.Invoke(null, new object[] { targetGroup });
                        var getDefinesMethod = typeof(PlayerSettings).GetMethod("GetScriptingDefineSymbols", 
                            new System.Type[] { namedBuildTargetType });
                        
                        if (getDefinesMethod != null)
                        {
                            return (string)getDefinesMethod.Invoke(null, new object[] { namedBuildTarget });
                        }
                    }
                }
            }
            catch
            {
                // Fall through to legacy approach
            }
            
            // Fallback to legacy approach
#pragma warning disable CS0618 // Type or member is obsolete
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private void CheckFirebaseAnalyticsConsistency(string defines, BuildTargetGroup platformGroup)
        {
            bool hasStandardDefine = defines.Contains("FIREBASE_ANALYTICS");
            bool hasBoostOpsDefine = defines.Contains("BOOSTOPS_FIREBASE_ANALYTICS");

            if (hasBoostOpsDefine && !hasStandardDefine)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BoostOps] Build Guard: BOOSTOPS_FIREBASE_ANALYTICS is defined but FIREBASE_ANALYTICS is not for {platformGroup}. " +
                    "This is normal if Firebase was installed as DLLs rather than through Package Manager.");
            }
            else if (hasStandardDefine && !hasBoostOpsDefine)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BoostOps] Build Guard: FIREBASE_ANALYTICS is defined but BOOSTOPS_FIREBASE_ANALYTICS is not for {platformGroup}. " +
                    "BoostOps should have automatically detected Firebase. Check the BoostOpsFirebaseDefine script.");
            }
            else if (!hasStandardDefine && !hasBoostOpsDefine)
            {
                UnityEngine.Debug.Log(
                    $"[BoostOps] Build Guard: No Firebase Analytics symbols detected for {platformGroup}. " +
                    "Firebase Analytics features will be disabled.");
            }
            else
            {
                UnityEngine.Debug.Log(
                    $"[BoostOps] Build Guard: Firebase Analytics symbols consistent for {platformGroup}. ✓");
            }
        }

        private void CheckUnityAnalyticsConsistency(string defines, BuildTargetGroup platformGroup)
        {
            bool hasUnityAnalytics = defines.Contains("UNITY_ANALYTICS");
            
            if (hasUnityAnalytics)
            {
                UnityEngine.Debug.Log(
                    $"[BoostOps] Build Guard: Unity Analytics detected for {platformGroup}. ✓");
            }
            else
            {
                UnityEngine.Debug.Log(
                    $"[BoostOps] Build Guard: Unity Analytics not detected for {platformGroup}. " +
                    "Unity Analytics features will be disabled.");
            }
        }
    }
}
#endif 