using UnityEngine;
using BoostOps.Internal;
using BoostOps.Analytics;
using System.Collections.Generic;

namespace BoostOps.Diagnostics
{
    /// <summary>
    /// Debug utility to help diagnose app open event issues
    /// Note: Auto-debug is disabled by default to avoid warnings during initialization.
    /// Call ManualDebug() from your code after BoostOpsSDK.Initialize() to run diagnostics.
    /// </summary>
    public static class AppOpenEventDebugger
    {
        // Disabled automatic execution to prevent warnings before SDK initialization
        // To enable, uncomment the attribute below:
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void DebugAppOpenEventFlow()
        {
            Debug.Log("=== BoostOps App Open Event Debug ===");
            
            // Check if settings have been cached yet
            if (!InternalSettingsCache.HasCachedSettings())
            {
                Debug.LogWarning("[AppOpenDebug] ⚠️ BoostOps SDK not initialized yet! Call BoostOpsSDK.Initialize() first.");
                Debug.Log("=== End App Open Event Debug (Skipped) ===");
                return;
            }
            
            // Check project settings
            var settings = InternalSettingsCache.GetProjectSettings();
            if (settings == null)
            {
                Debug.LogError("[AppOpenDebug] ❌ Project settings are NULL!");
                return;
            }
            
            Debug.Log($"[AppOpenDebug] 🔍 Project Key: '{settings.ProjectKey}' (length: {settings.ProjectKey?.Length ?? 0})");
            Debug.Log($"[AppOpenDebug] 🔍 UseRemoteManagement: {settings.UseRemoteManagement}");
            Debug.Log($"[AppOpenDebug] 🔍 BoostOpsAnalytics (derived): {settings.BoostOpsAnalytics}");
            
            // Check first launch status
            bool isFirstLaunch = PlayerPrefs.GetInt(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED, 0) == 0;
            Debug.Log($"[AppOpenDebug] 🔍 Is First Launch: {isFirstLaunch}");
            
            // Check available analytics providers
            var providers = AnalyticsProviderFactory.GetAvailableProviders();
            Debug.Log($"[AppOpenDebug] 🔍 Available Analytics Providers: {providers.Count}");
            foreach (var provider in providers)
            {
                Debug.Log($"[AppOpenDebug]   - {provider.GetType().Name}: Available={provider.IsAvailable}");
            }
            
            // Predict what will happen
            bool hasProjectKey = !string.IsNullOrEmpty(settings.ProjectKey);
            bool willSendFirstSession = hasProjectKey && (settings.UseRemoteManagement || true); // forceManagedMode=true
            bool willSendRegularSession = hasProjectKey && !isFirstLaunch;
            
            Debug.Log($"[AppOpenDebug] 📊 Predictions:");
            Debug.Log($"[AppOpenDebug]   - Has Project Key: {hasProjectKey}");
            Debug.Log($"[AppOpenDebug]   - Will Send First Session Event: {willSendFirstSession}");
            Debug.Log($"[AppOpenDebug]   - Will Send Regular Session Event: {willSendRegularSession}");
            
            if (!hasProjectKey)
            {
                Debug.LogWarning("[AppOpenDebug] ⚠️ No project key configured - events will not be sent!");
            }
            
            if (!settings.UseRemoteManagement)
            {
                Debug.LogWarning("[AppOpenDebug] ⚠️ UseRemoteManagement is false - only first session events will be sent (with forceManagedMode=true)!");
            }
            
            Debug.Log("=== End App Open Event Debug ===");
        }
        
        /// <summary>
        /// Test app open event manually
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void TestAppOpenEvent()
        {
            Debug.Log("=== Testing App Open Event Manually ===");
            
            bool isFirstLaunch = PlayerPrefs.GetInt(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED, 0) == 0;
            
            Debug.Log($"[AppOpenTest] 🧪 Calling TrackAppOpen - isFirstSession: {isFirstLaunch}");
            
            // Call the same method that SDK uses
            BoostOpsAnalyticsContract.TrackAppOpen(
                launchType: "manual_test", 
                isFirstSession: isFirstLaunch ? true : (bool?)null,
                organic: null,        // Let server determine organic vs attributed
                reinstall: null,      // Let server determine reinstall status
                forceManagedMode: true
            );
            
            Debug.Log("[AppOpenTest] ✅ TrackAppOpen call completed");
            Debug.Log("=== End Manual Test ===");
        }
        
        /// <summary>
        /// Manual trigger for debugging (call from inspector or console)
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void ManualDebug()
        {
            DebugAppOpenEventFlow();
        }
        
        /// <summary>
        /// Reset first launch flag for testing
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void ResetFirstLaunchFlag()
        {
            PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED);
            PlayerPrefs.Save();
            Debug.Log("[AppOpenDebug] 🔄 Reset first launch flag - next app start will be treated as first launch");
        }
        
        /// <summary>
        /// Enable debug logging temporarily
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void EnableDebugLogging()
        {
            BoostOpsLogger.IsDebugLoggingEnabled = true;
            Debug.Log("[AppOpenDebug] 🔊 Debug logging enabled");
        }
    }
}
