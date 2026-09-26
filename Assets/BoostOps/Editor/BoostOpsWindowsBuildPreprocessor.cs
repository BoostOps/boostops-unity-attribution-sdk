#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BoostOps.Editor
{
    /// <summary>
    /// Pre-build validator for Windows targets. Catches the one realistic
    /// misconfiguration that would otherwise silently break Microsoft Store
    /// campaign attribution at runtime: a project that has set
    /// <c>BoostOpsProjectSettings.microsoftStoreId</c> (signaling intent to ship
    /// through the Microsoft Store) but is building StandaloneWindows without
    /// the <c>Microsoft.Windows.SDK.Contracts</c> NuGet package installed.
    ///
    /// In that situation the runtime guard <c>#if WINDOWS_UWP || ENABLE_WINMD_SUPPORT</c>
    /// stays closed, the WinRT call to <c>StoreContext.GetDefault()</c> never
    /// fires, and every install on Windows looks organic — but the build still
    /// succeeds and the developer doesn't notice until campaign reports come
    /// back empty. This preprocessor turns that into a build-time failure with
    /// an actionable error message instead.
    ///
    /// What it does NOT do:
    /// <list type="bullet">
    ///   <item>Block UWP builds. UWP gets WinRT for free; nothing to validate.</item>
    ///   <item>Block bare-.exe Standalone builds with no <c>microsoftStoreId</c>.
    ///         Those devs are shipping to Steam / Itch / EGS and don't need the
    ///         Microsoft Store SDK at all.</item>
    ///   <item>Edit scripting defines. <see cref="BoostOpsWindowsWinmdDefine"/>
    ///         handles that; we just call its <c>Refresh()</c> here as belt and
    ///         suspenders before the build kicks off.</item>
    /// </list>
    /// </summary>
    public class BoostOpsWindowsBuildPreprocessor : IPreprocessBuildWithReport
    {
        // Run after the platform define-sync hooks so the most up-to-date scripting
        // defines are reflected in the build, but before any BoostOps build steps
        // that would actually consume the resulting .exe.
        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;

            // UWP target: Unity auto-defines WINDOWS_UWP, the WinRT projection is
            // baked into the toolchain, and Microsoft Store distribution is the
            // only deployment path. Nothing to validate; just confirm in logs.
            if (target == BuildTarget.WSAPlayer)
            {
                Debug.Log(
                    "[BoostOps] Windows build preprocessor: UWP build — Microsoft Store campaign attribution will be active automatically (WINDOWS_UWP is auto-defined).");
                return;
            }

            // StandaloneWindows / StandaloneWindows64: this is where the
            // MS Store-vs-Steam ambiguity lives. Validate.
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            // Make sure the ENABLE_WINMD_SUPPORT define reflects the current state
            // of the project's NuGet packages right now — it may have changed since
            // the editor last refreshed.
            try
            {
                BoostOpsWindowsWinmdDefine.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Windows build preprocessor: define sync failed: {ex.Message}");
            }

            string microsoftStoreId = TryReadMicrosoftStoreId();
            bool sdkContractsPresent = BoostOpsWindowsWinmdDefine.IsWindowsSdkContractsPresent();

            // No MS Store ID configured → the dev is almost certainly shipping a
            // bare .exe to Steam / Itch / EGS / direct download. Nothing to do.
            if (string.IsNullOrEmpty(microsoftStoreId))
            {
                if (sdkContractsPresent)
                {
                    Debug.Log(
                        "[BoostOps] Windows build preprocessor: StandaloneWindows build with Microsoft.Windows.SDK.Contracts present — MS Store attribution will activate at runtime if this build is MSIX-wrapped and shipped through the Microsoft Store.");
                }
                else
                {
                    Debug.Log(
                        "[BoostOps] Windows build preprocessor: StandaloneWindows build (no microsoftStoreId set) — Microsoft Store attribution disabled; build will run as a bare .exe.");
                }
                return;
            }

            // microsoftStoreId IS set → the dev intends to ship through MS Store.
            // The NuGet must be present or the runtime gate stays closed.
            if (!sdkContractsPresent)
            {
                throw new BuildFailedException(
                    "[BoostOps] Microsoft Store campaign attribution is misconfigured.\n\n" +
                    $"Build target: {target}\n" +
                    $"BoostOpsProjectSettings.microsoftStoreId: '{microsoftStoreId}' (set)\n" +
                    "Microsoft.Windows.SDK.Contracts NuGet: NOT installed\n\n" +
                    "Setting `microsoftStoreId` signals you intend to ship this build through the\n" +
                    "Microsoft Store, but the SDK can't read campaign IDs from StoreContext\n" +
                    "without the WinRT contracts package. Pick one of:\n\n" +
                    "  1) Install the `Microsoft.Windows.SDK.Contracts` NuGet package\n" +
                    "     (≥ 10.0.19041) — see Assets/BoostOps/README.md for the full setup,\n" +
                    "     then re-run the build. ENABLE_WINMD_SUPPORT will be added\n" +
                    "     automatically by BoostOpsWindowsWinmdDefine.\n\n" +
                    "  2) If this is a Steam / Itch / EGS / direct-download build and you do\n" +
                    "     NOT want Microsoft Store attribution, clear the `microsoftStoreId`\n" +
                    "     field in BoostOpsProjectSettings and rebuild.\n\n" +
                    "Aborting build to prevent shipping a Windows binary that silently reports\n" +
                    "every install as organic.");
            }

            Debug.Log(
                $"[BoostOps] Windows build preprocessor: StandaloneWindows build configured for Microsoft Store (microsoftStoreId='{microsoftStoreId}', SDK Contracts present, ENABLE_WINMD_SUPPORT will be applied). ✓");
        }

        /// <summary>
        /// Reads <c>microsoftStoreId</c> from the BoostOps project settings asset.
        /// Returns empty string if settings don't exist or can't be loaded — we
        /// treat "no settings" as "no MS Store intent" rather than failing the
        /// build, since plenty of projects don't have settings configured yet
        /// when they kick off a first Windows build.
        /// </summary>
        private static string TryReadMicrosoftStoreId()
        {
            try
            {
                const string AssetPath = "Assets/Resources/BoostOps/BoostOpsProjectSettings.asset";
                var settings = AssetDatabase.LoadAssetAtPath<BoostOpsProjectSettings>(AssetPath);
                if (settings == null)
                {
                    return "";
                }

                return string.IsNullOrEmpty(settings.microsoftStoreId)
                    ? ""
                    : settings.microsoftStoreId;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[BoostOps] Windows build preprocessor: could not read BoostOpsProjectSettings: {ex.Message} (skipping MS Store validation)");
                return "";
            }
        }
    }
}
#endif
