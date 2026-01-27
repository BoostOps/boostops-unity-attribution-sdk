#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;

namespace BoostOps.Editor
{
    /// <summary>
    /// Automatically detects Firebase Analytics availability and sets BOOSTOPS_FIREBASE_ANALYTICS define
    /// This ensures BoostOps works regardless of how Firebase was installed (UPM, DLLs, etc.)
    /// </summary>
    [InitializeOnLoad]
    internal static class BoostOpsFirebaseDefine
    {
        private const string SYMBOL = "BOOSTOPS_FIREBASE_ANALYTICS";

        static BoostOpsFirebaseDefine()
        {
            UpdateSymbols();
        }

        private static void UpdateSymbols()
        {
            // Check if Firebase Analytics is actually available by looking for the class
            bool analyticsFound = AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.GetTypes().Any(
                    type => type.FullName == "Firebase.Analytics.FirebaseAnalytics"));

            if (analyticsFound)
            {
                BoostOpsLogger.LogDebug("Analytics", "Firebase Analytics classes detected - ensuring BOOSTOPS_FIREBASE_ANALYTICS is defined");
            }
            else
            {
                BoostOpsLogger.LogDebug("Analytics", "Firebase Analytics classes not found - removing BOOSTOPS_FIREBASE_ANALYTICS define");
            }

            // Update symbols for all build target groups
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
                        BoostOpsLogger.LogDebug("Analytics", $"Added {SYMBOL} define for {targetGroup}");
                    }
                    else if (!analyticsFound && hasSymbol)
                    {
                        defines.Remove(SYMBOL);
                        SetDefinesForTargetGroup(targetGroup, string.Join(";", defines));
                        BoostOpsLogger.LogDebug("Analytics", $"Removed {SYMBOL} define for {targetGroup}");
                    }
                }
                catch (System.Exception)
                {
                    // Some build target groups might not be valid on this platform
                    // Just skip them silently
                    continue;
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