#if UNITY_IOS
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace BoostOps.Editor
{
    /// <summary>
    /// The shipped PrivacyInfo.xcprivacy declares no tracking. Projects that opt in to
    /// attTracking (iOS direct deals) get the copy in the Xcode project rewritten to declare
    /// tracking and att-events.boostopssdk.com as the only tracking domain.
    /// </summary>
    public static class BoostOpsPrivacyManifestPostProcess
    {
        private const string Marker = "BoostOps SDK privacy manifest";
        // Must match BoostOpsEndpoints.TrackingEventsHost.
        private const string TrackingDomain = "att-events.boostopssdk.com";

        // Data used for cross-company ad measurement once the user grants ATT.
        private static readonly string[] TrackingDataTypes =
        {
            "NSPrivacyCollectedDataTypeDeviceID",
            "NSPrivacyCollectedDataTypeProductInteraction",
            "NSPrivacyCollectedDataTypePurchaseHistory",
        };

        [PostProcessBuild(2)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget != BuildTarget.iOS)
                return;

            var settings = BoostOpsProjectSettings.GetOrCreateSettings();
            if (settings == null || !settings.attTracking)
                return;

            try
            {
                var manifests = Directory.GetFiles(pathToBuiltProject, "PrivacyInfo.xcprivacy", SearchOption.AllDirectories)
                    .Where(IsBoostOpsManifest)
                    .ToArray();

                if (manifests.Length == 0)
                {
                    Debug.LogError("[BoostOps] attTracking is on but the BoostOps PrivacyInfo.xcprivacy was not found in the Xcode project. " +
                                   $"Declare tracking and {TrackingDomain} in your app's privacy manifest manually.");
                }

                foreach (var path in manifests)
                {
                    DeclareTracking(path);
                    Debug.Log($"[BoostOps] Privacy manifest set to tracking ({TrackingDomain}): {path}");
                }

                WarnIfNoTrackingUsageDescription(Path.Combine(pathToBuiltProject, "Info.plist"));
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to update privacy manifest for attTracking: {e.Message}");
            }
        }

        private static bool IsBoostOpsManifest(string path)
        {
            if (path.IndexOf("boostops", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return File.ReadAllText(path).Contains(Marker);
        }

        private static void DeclareTracking(string path)
        {
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            var root = plist.root;

            root.SetBoolean("NSPrivacyTracking", true);
            root.CreateArray("NSPrivacyTrackingDomains").AddString(TrackingDomain);

            if (root.values.TryGetValue("NSPrivacyCollectedDataTypes", out var collected))
            {
                foreach (var entry in collected.AsArray().values)
                {
                    var dict = entry.AsDict();
                    if (dict.values.TryGetValue("NSPrivacyCollectedDataType", out var type)
                        && TrackingDataTypes.Contains(type.AsString()))
                    {
                        dict.SetBoolean("NSPrivacyCollectedDataTypeTracking", true);
                    }
                }
            }

            plist.WriteToFile(path);
        }

        private static void WarnIfNoTrackingUsageDescription(string infoPlistPath)
        {
            if (!File.Exists(infoPlistPath))
                return;

            var plist = new PlistDocument();
            plist.ReadFromFile(infoPlistPath);
            if (!plist.root.values.ContainsKey("NSUserTrackingUsageDescription"))
            {
                Debug.LogWarning("[BoostOps] attTracking is on but Info.plist has no NSUserTrackingUsageDescription. " +
                                 "Add the ATT prompt text, or the app can't request tracking permission.");
            }
        }
    }
}
#endif
