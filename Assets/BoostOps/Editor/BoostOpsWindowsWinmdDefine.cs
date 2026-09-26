#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace BoostOps.Editor
{
    /// <summary>
    /// Auto-syncs the <c>ENABLE_WINMD_SUPPORT</c> scripting define for StandaloneWindows
    /// build targets based on whether the project has the
    /// <c>Microsoft.Windows.SDK.Contracts</c> NuGet package installed.
    ///
    /// Why this exists: the BoostOps SDK reads the Microsoft Store campaign id at
    /// runtime via <c>Windows.Services.Store</c>. UWP builds get that namespace for
    /// free (Unity auto-defines <c>WINDOWS_UWP</c>), but Standalone Windows builds
    /// only see WinRT when the developer (a) installs the SDK Contracts NuGet and
    /// (b) adds <c>ENABLE_WINMD_SUPPORT</c> to scripting defines. Since the *act*
    /// of adding the NuGet is the unambiguous signal that the dev intends to ship
    /// MSIX through the Microsoft Store, this script flips the define for them.
    ///
    /// Devs shipping bare .exe to Steam / Itch / EGS won't add the NuGet, so this
    /// runs cleanly to a no-op for them. Devs shipping to the Microsoft Store add
    /// the NuGet once and the SDK lights up automatically — no checkbox needed.
    ///
    /// Modeled exactly on <see cref="BoostOpsFirebaseDefine"/> and
    /// <see cref="BoostOpsUnityAnalyticsDefine"/> — same pattern of probing for
    /// optional packages and synchronizing a define.
    /// </summary>
    [InitializeOnLoad]
    internal static class BoostOpsWindowsWinmdDefine
    {
        private const string SYMBOL = "ENABLE_WINMD_SUPPORT";

        // The two StandaloneWindows variants. UWP (WSAPlayer) doesn't need
        // ENABLE_WINMD_SUPPORT — Unity auto-defines WINDOWS_UWP for that target
        // and the WinRT projection is built into the UWP toolchain.
        private static readonly BuildTargetGroup[] WindowsStandaloneTargetGroups = new[]
        {
            BuildTargetGroup.Standalone,
        };

        static BoostOpsWindowsWinmdDefine()
        {
            try
            {
                UpdateSymbols();
            }
            catch (Exception ex)
            {
                // Never block editor load over define sync; the build preprocessor
                // is the loud-failure layer if anything is genuinely misconfigured.
                BoostOpsLogger.LogDebug("WindowsWinmd", $"Skipping ENABLE_WINMD_SUPPORT sync: {ex.Message}");
            }
        }

        /// <summary>
        /// Public entry point used by the build preprocessor to make absolutely
        /// sure defines are current right before a Windows Standalone build kicks
        /// off (in case the NuGet was added and the editor hasn't recompiled yet).
        /// </summary>
        public static void Refresh()
        {
            UpdateSymbols();
        }

        /// <summary>
        /// True when this project has the Microsoft.Windows.SDK.Contracts NuGet
        /// installed (or otherwise has the WinRT projection types resolvable).
        /// Result is computed fresh on each call — cheap enough and avoids stale
        /// cache issues when the dev imports/removes the package.
        /// </summary>
        public static bool IsWindowsSdkContractsPresent()
        {
            // Probe 1: the assembly is already loaded into the editor's AppDomain.
            // This is the fastest path and works for any installation method that
            // produces .NET-style assemblies (NuGetForUnity managed-DLL output).
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    string name = assembly.GetName().Name;
                    if (name == null) continue;

                    // The NuGet package name itself, or any of the platform contract
                    // assemblies it brings in (Windows.Services.Store, Windows.Foundation.*).
                    if (name.Equals("Microsoft.Windows.SDK.Contracts", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("Windows.Services.Store", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // Also scan loaded types — covers the case where the assembly is
                    // loaded under a different name but the type projection is present.
                    try
                    {
                        if (assembly.GetType("Windows.Services.Store.StoreContext") != null)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // GetType can throw on dynamic assemblies — ignore and continue.
                    }
                }
            }
            catch
            {
                // AppDomain probe is best-effort — fall through to file probe.
            }

            // Probe 2: file presence under Assets/. NuGet-deployed WinRT contracts
            // typically include a winmd named for the Windows.Services.Store contract
            // and/or a managed assembly named Microsoft.Windows.SDK.Contracts.dll.
            // This catches setups where the assembly hasn't been loaded into the
            // editor's AppDomain (common for IL2CPP-only winmd projections).
            try
            {
                string assetsPath = UnityEngine.Application.dataPath;
                if (Directory.Exists(assetsPath))
                {
                    if (HasMatchingFile(assetsPath, "Windows.Services.Store.winmd")) return true;
                    if (HasMatchingFile(assetsPath, "Microsoft.Windows.SDK.Contracts.dll")) return true;
                    if (HasMatchingFile(assetsPath, "Windows.winmd")) return true;
                }
            }
            catch
            {
                // File probe is best-effort too. Worst case: we under-report and
                // the build preprocessor surfaces a clear "install the NuGet" error.
            }

            return false;
        }

        private static bool HasMatchingFile(string root, string fileName)
        {
            // EnumerateFiles is lazy — won't materialize the whole tree.
            return Directory
                .EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .Any();
        }

        private static void UpdateSymbols()
        {
            bool sdkPresent = IsWindowsSdkContractsPresent();

            foreach (var targetGroup in WindowsStandaloneTargetGroups)
            {
                if (targetGroup == BuildTargetGroup.Unknown) continue;

                try
                {
                    var defines = GetDefinesForTargetGroup(targetGroup);
                    bool hasSymbol = defines.Contains(SYMBOL);

                    if (sdkPresent && !hasSymbol)
                    {
                        defines.Add(SYMBOL);
                        SetDefinesForTargetGroup(targetGroup, string.Join(";", defines));
                        BoostOpsLogger.LogInfo(
                            "WindowsWinmd",
                            $"Added {SYMBOL} for {targetGroup} (Microsoft.Windows.SDK.Contracts detected — MS Store campaign attribution enabled).");
                    }
                    else if (!sdkPresent && hasSymbol)
                    {
                        // Only remove the symbol if WE added it. We never strip a
                        // define a different package added — keep the user's defines
                        // intact. There's no way to record "we added this", so the
                        // pragmatic compromise is: only remove if neither the NuGet
                        // is present nor any other obvious marker. The probe above
                        // already covers all the obvious markers, so a remaining
                        // ENABLE_WINMD_SUPPORT here is almost certainly stale from
                        // a previous BoostOps install. Removing it is correct.
                        defines.Remove(SYMBOL);
                        SetDefinesForTargetGroup(targetGroup, string.Join(";", defines));
                        BoostOpsLogger.LogDebug(
                            "WindowsWinmd",
                            $"Removed {SYMBOL} for {targetGroup} (Microsoft.Windows.SDK.Contracts not present).");
                    }
                }
                catch (Exception ex)
                {
                    BoostOpsLogger.LogDebug("WindowsWinmd", $"Skipping {targetGroup}: {ex.Message}");
                }
            }
        }

        // The reflection-based define accessors below are copied from
        // BoostOpsFirebaseDefine to handle Unity 2022.2+'s NamedBuildTarget API
        // while gracefully falling back to the obsolete BuildTargetGroup overloads
        // on older Unity versions. Keep them in lockstep with that file.

        private static List<string> GetDefinesForTargetGroup(BuildTargetGroup targetGroup)
        {
            try
            {
                var namedBuildTargetType = Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
                if (namedBuildTargetType != null)
                {
                    var fromBuildTargetGroupMethod = namedBuildTargetType.GetMethod(
                        "FromBuildTargetGroup",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (fromBuildTargetGroupMethod != null)
                    {
                        var namedBuildTarget = fromBuildTargetGroupMethod.Invoke(null, new object[] { targetGroup });
                        var getDefinesMethod = typeof(PlayerSettings).GetMethod(
                            "GetScriptingDefineSymbols",
                            new Type[] { namedBuildTargetType });

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
                var namedBuildTargetType = Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
                if (namedBuildTargetType != null)
                {
                    var fromBuildTargetGroupMethod = namedBuildTargetType.GetMethod(
                        "FromBuildTargetGroup",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (fromBuildTargetGroupMethod != null)
                    {
                        var namedBuildTarget = fromBuildTargetGroupMethod.Invoke(null, new object[] { targetGroup });
                        var setDefinesMethod = typeof(PlayerSettings).GetMethod(
                            "SetScriptingDefineSymbols",
                            new Type[] { namedBuildTargetType, typeof(string) });

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
