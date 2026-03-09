using UnityEngine;
using BoostOps.Analytics;
using BoostOps.Internal;

namespace BoostOps
{
    /// <summary>
    /// Public API for BoostOps Analytics functionality.
    /// Provides helpers for Unity Analytics setup and testing.
    /// </summary>
    public static class BoostOpsAnalytics
    {
        /// <summary>
        /// Checks if Unity Analytics is properly initialized and ready to receive events.
        /// Returns false if Unity Analytics package is not installed, not initialized,
        /// or consent/data collection hasn't been started.
        /// </summary>
        /// <returns>True if Unity Analytics is ready, false otherwise</returns>
        public static bool IsUnityAnalyticsReady()
        {
            return UnityAnalyticsGuard.EnsureReady();
        }

        /// <summary>
        /// Forces a re-check of Unity Analytics readiness on the next analytics call.
        /// Call this after your app completes the consent flow and starts data collection.
        /// </summary>
        public static void RefreshUnityAnalyticsStatus()
        {
            UnityAnalyticsGuard.RefreshReadinessCheck();
            AnalyticsProviderFactory.RefreshAvailableProviders();
        }

        /// <summary>
        /// Helper method for testing - enables Unity Analytics data collection programmatically.
        /// This bypasses the normal consent flow and should ONLY be used in test environments
        /// where consent UI isn't needed (e.g., automated tests, CI/CD builds).
        /// 
        /// ⚠️ DO NOT use this in production builds - always implement proper consent flow.
        /// </summary>
        public static void EnableUnityDataCollectionForTesting()
        {
            UnityAnalyticsGuard.EnableUnityDataCollectionForTesting();
        }

        /// <summary>
        /// Gets information about the current Unity Analytics setup status.
        /// Useful for debugging analytics integration issues.
        /// </summary>
        /// <returns>Debug information about Unity Analytics status</returns>
        public static string GetUnityAnalyticsDebugInfo()
        {
            try
            {
                bool isReady = UnityAnalyticsGuard.EnsureReady();
                
                var info = "Unity Analytics Debug Info:\n";
                info += $"  Ready: {isReady}\n";
                
                // Check package availability
                var newAnalyticsType = System.Type.GetType("Unity.Services.Analytics.AnalyticsService, Unity.Services.Analytics");
                var oldAnalyticsType = System.Type.GetType("UnityEngine.Analytics.Analytics, UnityEngine.UnityAnalyticsModule");
                
                info += $"  New Analytics API Available: {newAnalyticsType != null}\n";
                info += $"  Legacy Analytics API Available: {oldAnalyticsType != null}\n";
                
                if (newAnalyticsType != null)
                {
                    info += "  Using: Unity Services Analytics (newer API)\n";
                }
                else if (oldAnalyticsType != null)
                {
                    info += "  Using: Legacy Unity Analytics\n";
                }
                else
                {
                    info += "  Using: No Unity Analytics package detected\n";
                }
                
                // Check BoostOps settings
                var settings = InternalSettingsCache.GetProjectSettings();
                if (settings != null)
                {
                    info += $"  BoostOps Unity Analytics Enabled: {settings.BoostOpsAnalytics}\n";
                }
                
                return info;
            }
            catch (System.Exception ex)
            {
                return $"Unity Analytics Debug Info: Error - {ex.Message}";
            }
        }

        /// <summary>
        /// Shows the Unity Analytics setup warning message in the console.
        /// Useful for debugging when analytics events aren't appearing.
        /// </summary>
        public static void ShowUnityAnalyticsSetupGuide()
        {
            BoostOpsLogger.LogWarning("Analytics", 
                "Unity Analytics Setup Guide:\n" +
                "📋 Required setup steps:\n" +
                "   1. Install Unity Analytics package from Package Manager\n" +
                "   2. Initialize Unity Gaming Services: await UnityServices.InitializeAsync()\n" +
                "   3. Implement consent UI to ask player permission\n" +
                "   4. Start data collection after consent:\n" +
                "      • Unity SDK ≤ 6.1: AnalyticsService.Instance.StartDataCollection()\n" +
                "      • Unity SDK 6.2+: EndUserConsent.SetConsentState(...AnalyticsIntent = Granted...)\n" +
                "   5. Configure events in Unity Analytics Event Manager\n" +
                "📖 For detailed setup guide, see Assets/BoostOps/Documentation/Unity-Analytics-Setup.md"
            );
        }
    }
} 