#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;

namespace BoostOps.Editor
{
    [InitializeOnLoad]
    internal static class BoostOpsUnityAnalyticsDefine
    {
        private const string SYMBOL = "UNITY_ANALYTICS";

        static BoostOpsUnityAnalyticsDefine()
        {
            UpdateSymbols();
        }

        private static void UpdateSymbols()
        {
            // Check if Unity Analytics classes are available (try multiple possible types)
            bool analyticsFound = AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.GetTypes().Any(type => 
                    type.FullName == "Unity.Services.Analytics.AnalyticsService" || 
                    type.FullName == "Unity.Analytics.Analytics" ||
                    type.FullName == "UnityEngine.Analytics.Analytics"));

            foreach (BuildTargetGroup targetGroup in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (targetGroup == BuildTargetGroup.Unknown) continue;

                try
                {
                    // Use reflection to safely handle NamedBuildTarget availability
                    var defines = GetDefinesForTargetGroup(targetGroup);
                    bool hasSymbol = defines.Contains(SYMBOL);

                    if (analyticsFound && !hasSymbol)
                    {
                        defines.Add(SYMBOL);
                        SetDefinesForTargetGroup(targetGroup, string.Join(";", defines));
                    }
                    else if (!analyticsFound && hasSymbol)
                    {
                        defines.Remove(SYMBOL);
                        SetDefinesForTargetGroup(targetGroup, string.Join(";", defines));
                    }
                }
                catch (System.Exception)
                {
                    // Ignore invalid target groups
                }
            }
        }

        /// <summary>
        /// Safely get scripting defines for target group using best available API
        /// </summary>
        private static System.Collections.Generic.List<string> GetDefinesForTargetGroup(BuildTargetGroup targetGroup)
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
                            var defines = (string)getDefinesMethod.Invoke(null, new object[] { namedBuildTarget });
                            return defines.Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();
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
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup)
                .Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();
#pragma warning restore CS0618 // Type or member is obsolete
        }
        
        /// <summary>
        /// Safely set scripting defines for target group using best available API
        /// </summary>
        private static void SetDefinesForTargetGroup(BuildTargetGroup targetGroup, string defines)
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
                        var setDefinesMethod = typeof(PlayerSettings).GetMethod("SetScriptingDefineSymbols", 
                            new System.Type[] { namedBuildTargetType, typeof(string) });
                        
                        if (setDefinesMethod != null)
                        {
                            setDefinesMethod.Invoke(null, new object[] { namedBuildTarget, defines });
                            return;
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
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
#endif 