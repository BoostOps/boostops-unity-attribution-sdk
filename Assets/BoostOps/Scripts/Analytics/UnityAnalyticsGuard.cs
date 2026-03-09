using UnityEngine;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Helper class to ensure Unity Analytics is properly initialized before sending events.
    /// Unity Analytics silently drops events if consent/data collection hasn't been started.
    /// </summary>
    internal static class UnityAnalyticsGuard
    {
        private static bool _hasWarned = false;

        /// <summary>
        /// Checks if Unity Analytics is ready to receive events.
        /// Shows warning on first failure to help developers understand setup requirements.
        /// Always performs a fresh check instead of caching to ensure accuracy.
        /// </summary>
        /// <returns>True if Unity Analytics is ready, false if not ready or unavailable</returns>
        public static bool EnsureReady()
        {
            // Always check freshly - don't cache the result to avoid timing issues
            // Analytics initialization is infrequent, so reflection cost is minimal
            
            try
            {
                // First, check if Unity Services are initialized at all
                var unityServicesType = System.Type.GetType("Unity.Services.Core.UnityServices, Unity.Services.Core");
                if (unityServicesType != null)
                {
                    var stateProperty = unityServicesType.GetProperty("State", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (stateProperty != null)
                    {
                        var state = stateProperty.GetValue(null);
                        var stateTypeName = state?.GetType().Name;
                        
                        // If Unity Services aren't initialized yet, return false without warning
                        if (stateTypeName != "Initialized")
                        {
                            // BoostOpsLogger.LogDebug("Analytics", $"Unity Services not yet initialized (State: {stateTypeName ?? "null"})");
                            return false;
                        }
                    }
                }
                
                // Try Unity Services Analytics (newer API) first
                var newAnalyticsType = System.Type.GetType("Unity.Services.Analytics.AnalyticsService, Unity.Services.Analytics");
                if (newAnalyticsType != null)
                {
                    var instanceProperty = newAnalyticsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        var instance = instanceProperty.GetValue(null);
                        if (instance != null)
                        {
                            // Check if data collection is enabled - try multiple property names for different Unity versions
                            var isDataCollectionEnabledProperty = instance.GetType().GetProperty("IsDataCollectionEnabled");
                            if (isDataCollectionEnabledProperty != null)
                            {
                                var isEnabled = (bool)isDataCollectionEnabledProperty.GetValue(instance);
                                if (isEnabled)
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                // For newer Unity Analytics versions (6.1+), check IsActive instead
                                var isActiveProperty = instance.GetType().GetProperty("IsActive");
                                if (isActiveProperty != null)
                                {
                                    var isActive = (bool)isActiveProperty.GetValue(instance);
                                    if (isActive)
                                    {
                                        return true;
                                    }
                                }
                            }

                            // For Unity 6.2+, also check consent state if available
                            var endUserConsentType = System.Type.GetType("Unity.Services.Analytics.EndUserConsent, Unity.Services.Analytics");
                            if (endUserConsentType != null)
                            {
                                var getConsentStateMethod = endUserConsentType.GetMethod("GetConsentState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                if (getConsentStateMethod != null)
                                {
                                    var consentState = getConsentStateMethod.Invoke(null, null);
                                    if (consentState != null)
                                    {
                                        var analyticsIntentProperty = consentState.GetType().GetProperty("AnalyticsIntent");
                                        if (analyticsIntentProperty != null)
                                        {
                                            var analyticsIntent = analyticsIntentProperty.GetValue(consentState);
                                            // Check if AnalyticsIntent is Granted (enum value typically 1)
                                            var consentStatusType = System.Type.GetType("Unity.Services.Analytics.ConsentStatus, Unity.Services.Analytics");
                                            if (consentStatusType != null)
                                            {
                                                var grantedField = consentStatusType.GetField("Granted");
                                                if (grantedField != null)
                                                {
                                                    var grantedValue = grantedField.GetValue(null);
                                                    if (analyticsIntent.Equals(grantedValue))
                                                    {
                                                        return true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Fallback to legacy Unity Analytics API
                var oldAnalyticsType = System.Type.GetType("UnityEngine.Analytics.Analytics, UnityEngine.UnityAnalyticsModule");
                if (oldAnalyticsType != null)
                {
                    // For old API, check if data collection is enabled
                    var isDataCollectionEnabledProperty = oldAnalyticsType.GetProperty("IsDataCollectionEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (isDataCollectionEnabledProperty != null)
                    {
                        var isEnabled = (bool)isDataCollectionEnabledProperty.GetValue(null);
                        if (isEnabled)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                // This typically means Unity Services aren't initialized yet
                BoostOpsLogger.LogDebug("Analytics", $"Unity Services not ready (Target invocation failed): {ex.InnerException?.Message ?? ex.Message}");
                return false; // Don't show warning for initialization timing issues
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Unity Analytics readiness check failed: {ex.Message}");
            }

            // Not ready - show warning once (but only if it's not just a timing issue)
            if (!_hasWarned)
            {
                _hasWarned = true;
                ShowSetupWarning();
            }

            return false;
        }

        /// <summary>
        /// Shows a helpful warning message to guide developers through Unity Analytics setup
        /// </summary>
        private static void ShowSetupWarning()
        {
            BoostOpsLogger.LogWarning("Analytics", 
                "Unity Analytics is not ready to receive events. Events will be skipped until setup is complete.\n" +
                "📋 Required setup steps:\n" +
                "   1. Initialize Unity Gaming Services: await UnityServices.InitializeAsync()\n" +
                "   2. Obtain player consent (GDPR/CCPA/etc.)\n" +
                "   3. Start data collection:\n" +
                "      • Unity SDK ≤ 6.1: AnalyticsService.Instance.StartDataCollection()\n" +
                "      • Unity SDK 6.2+: EndUserConsent.SetConsentState(...AnalyticsIntent = Granted...)\n" +
                "   4. Configure events in Unity Analytics Event Manager\n" +
                "📖 For detailed setup guide, see BoostOps documentation."
            );
        }

        /// <summary>
        /// Forces a re-check of Unity Analytics readiness on next EnsureReady() call.
        /// Useful after the app completes consent flow.
        /// </summary>
        public static void RefreshReadinessCheck()
        {
            // No caching to refresh, as EnsureReady now always checks fresh.
        }

        /// <summary>
        /// Helper method for testing - enables Unity Analytics data collection programmatically.
        /// Only for use in test environments where consent UI isn't needed.
        /// </summary>
        public static void EnableUnityDataCollectionForTesting()
        {
            try
            {
                // Try new API first
                var newAnalyticsType = System.Type.GetType("Unity.Services.Analytics.AnalyticsService, Unity.Services.Analytics");
                if (newAnalyticsType != null)
                {
                    var instanceProperty = newAnalyticsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        var instance = instanceProperty.GetValue(null);
                        if (instance != null)
                        {
                            var startDataCollectionMethod = instance.GetType().GetMethod("StartDataCollection");
                            if (startDataCollectionMethod != null)
                            {
                                startDataCollectionMethod.Invoke(instance, null);
                                RefreshReadinessCheck();
                                BoostOpsLogger.LogDebug("Analytics", "Unity Analytics data collection enabled for testing");
                                return;
                            }
                        }
                    }
                }

                // Fallback to old API
                var oldAnalyticsType = System.Type.GetType("UnityEngine.Analytics.Analytics, UnityEngine.UnityAnalyticsModule");
                if (oldAnalyticsType != null)
                {
                    var startDataCollectionMethod = oldAnalyticsType.GetMethod("StartDataCollection", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (startDataCollectionMethod != null)
                    {
                        startDataCollectionMethod.Invoke(null, null);
                        RefreshReadinessCheck();
                        BoostOpsLogger.LogDebug("Analytics", "Unity Analytics data collection enabled for testing (legacy API)");
                        return;
                    }
                }

                BoostOpsLogger.LogWarning("Analytics", "Unable to enable Unity Analytics data collection - no compatible API found");
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to enable Unity Analytics for testing: {ex.Message}");
            }
        }
    }
} 