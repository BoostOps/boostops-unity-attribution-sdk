#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;

namespace BoostOps.Editor
{
    /// <summary>
    /// Automatically detects Unity Remote Config availability and sets UNITY_REMOTE_CONFIG define.
    /// Scans loaded assemblies for Unity.Services.RemoteConfig.RemoteConfigService at editor startup.
    /// </summary>
    [InitializeOnLoad]
    internal static class BoostOpsUnityRemoteConfigDefine
    {
        private const string SYMBOL = "UNITY_REMOTE_CONFIG";

        static BoostOpsUnityRemoteConfigDefine()
        {
            UpdateSymbols();
        }

        private static void UpdateSymbols()
        {
            bool found = AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.GetTypes().Any(
                    type => type.FullName == "Unity.Services.RemoteConfig.RemoteConfigService"));

            foreach (BuildTargetGroup targetGroup in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (targetGroup == BuildTargetGroup.Unknown) continue;

                try
                {
                    var defines = GetDefinesForTargetGroup(targetGroup);
                    bool hasSymbol = defines.Contains(SYMBOL);

                    if (found && !hasSymbol)
                    {
                        defines.Add(SYMBOL);
                        SetDefinesForTargetGroup(targetGroup, string.Join(";", defines));
                    }
                    else if (!found && hasSymbol)
                    {
                        defines.Remove(SYMBOL);
                        SetDefinesForTargetGroup(targetGroup, string.Join(";", defines));
                    }
                }
                catch (System.Exception)
                {
                    continue;
                }
            }
        }

        private static System.Collections.Generic.List<string> GetDefinesForTargetGroup(BuildTargetGroup targetGroup)
        {
            try
            {
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

#pragma warning disable CS0618
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup)
                .Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();
#pragma warning restore CS0618
        }

        private static void SetDefinesForTargetGroup(BuildTargetGroup targetGroup, string defines)
        {
            try
            {
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

#pragma warning disable CS0618
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
#pragma warning restore CS0618
        }
    }
}
#endif
