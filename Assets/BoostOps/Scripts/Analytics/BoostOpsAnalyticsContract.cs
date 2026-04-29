using UnityEngine;
using BoostOps.Internal;
using System.Collections.Generic;
using System;
using System.Linq;
using BoostOps.Attribution;
using BoostOps.Analytics;

namespace BoostOps
{
    /// <summary>
    /// BoostOps Analytics Event Contract (v1) Implementation
    /// Provides vendor-agnostic event taxonomy that fans out to GA4, Unity Analytics, and BoostOps backend
    /// without double-counting or naming collisions.
    /// 
    /// This class now uses a provider-based architecture with separate classes for each analytics platform:
    /// - FirebaseAnalyticsProvider: Handles Firebase/GA4 integration
    /// - UnityAnalyticsProvider: Handles Unity Analytics integration
    /// - BoostOpsAnalyticsProvider: Handles BoostOps backend communication
    /// </summary>
    public static class BoostOpsAnalyticsContract
    {
        /// <summary>
        /// Hardcoded endpoint for critical install events to bypass remote config race conditions
        /// Install events are too important to risk losing to timing issues with Unity/Firebase Remote Config
        /// </summary>
        private const string INSTALL_EVENTS_ENDPOINT = "https://analytics.boostops.io/v1/events";
        #region Contract Constants
        
        /// <summary>
        /// Event name prefix for all BoostOps events
        /// </summary>
        private const string EVENT_PREFIX = "boostops_";
        
        /// <summary>
        /// BoostOps Analytics Event Names - Centralized constants for consistency
        /// </summary>
        public static class EventNames
        {
            public const string IMPRESSION = "boostops_impression";
            public const string CLICK = "boostops_click";
            [System.Obsolete("Install events are deprecated. Use APP_OPEN with first_open=true instead (industry standard)")]
            public const string INSTALL = "boostops_install";
            public const string APP_OPEN = "boostops_open";  // Use with first_open=true for installs (industry standard)
            public const string PURCHASE = "boostops_purchase";
            public const string INSTALL_ATTRIBUTION_UPDATE = "boostops_install_attribution_update";
        }
        
        /// <summary>
        /// Channel types as defined in the contract
        /// </summary>
        public enum Channel
        {
            XPromo,     // "xpromo" - Cross-promotion
            BoostLink   // "boostlink" - BoostLink dynamic links
        }
        
        /// <summary>
        /// Attribution methods for install events
        /// </summary>
        public enum AttributionMethod
        {
            Probabilistic,    // "probabilistic"
            Deterministic     // "deterministic"
        }
        
        /// <summary>
        /// Event source types
        /// </summary>
        public enum EventSource
        {
            SDK,        // "sdk" - Direct from SDK
            Router      // "router" - From BoostLink router
        }
        
        #endregion
        
        #region Contract Event Methods
        
        // Legacy analytics methods removed - use new schema methods below
        
        // Install tracking: Use TrackAppOpen() with isFirstSession=true (industry standard approach)
        
        // Legacy helper methods removed - using new schema methods directly
        
        // NOTE: boostops_id is ALWAYS client-generated (never from server)
        // Server does not send back boostops_id in responses
        // All ID generation happens in BoostOpsIdentifierManager.GetBoostOpsId()
        

        
        #region Analytics Event Tracking (New Schema)
        
                /// <summary>
        /// Track impression event with optional cross-promotion context
        /// Routes to all available providers (BoostOps, Firebase, Unity) based on configuration
        /// Note: source_store_id is available in context.store_id (not passed as parameter)
        /// Note: source_project_id is derived server-side from project_key (not sent from SDK)
        /// </summary>
        public static string TrackImpression(string campaignSlug, string placement = null,
            string format = "banner", int? durationMs = null,
            string targetStoreId = null, string targetProjectId = null, string networkCampaignId = null, 
            float? revenueShareRate = null, string channel = null, string[] targetStoreIds = null, string[] targetProjectIds = null,
            string impressionId = null)
        {
#if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("Analytics", $"TrackImpression: campaign={campaignSlug}, format={format}, channel={channel}");
#endif
            
            // Generate impression_id if not provided - ALL impressions need an ID for click linking
            if (string.IsNullOrEmpty(impressionId))
            {
                impressionId = BoostOps.Analytics.BoostOpsImpressionTracker.GenerateImpressionId();
            }
            
            // Create parameters for provider interface
            var parameters = new Dictionary<string, string>
            {
                ["campaign_slug"] = campaignSlug ?? "",
                ["placement"] = placement ?? "",
                ["format"] = format ?? "banner",
                // Note: source_store_id is in context.store_id (universal) - not included here
                // Note: source_project_id is derived server-side from project_key (not sent from SDK)
                ["target_store_id"] = targetStoreId ?? "",
                ["target_project_id"] = targetProjectId ?? "",
                ["network_campaign_id"] = networkCampaignId ?? "",
                ["channel"] = channel ?? ""
            };
            
            // Add plural target fields for multi-target impressions (e.g., app wall)
            if (targetStoreIds != null && targetStoreIds.Length > 0)
            {
                // Serialize as JSON array: ["id1","id2","id3"]
                var escapedIds = targetStoreIds.Select(id => "\"" + id.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
                parameters["target_store_ids"] = "[" + string.Join(",", escapedIds) + "]";
            }
            if (targetProjectIds != null && targetProjectIds.Length > 0)
            {
                // Serialize as JSON array: ["id1","id2","id3"]
                var escapedIds = targetProjectIds.Select(id => "\"" + id.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
                parameters["target_project_ids"] = "[" + string.Join(",", escapedIds) + "]";
            }
            
            if (durationMs.HasValue)
                parameters["duration_ms"] = durationMs.Value.ToString();
            if (revenueShareRate.HasValue)
                parameters["revenue_share_rate"] = revenueShareRate.Value.ToString();
            
            // Add impression_id to parameters
            parameters["impression_id"] = impressionId;
            
            // Send to all available providers (respects local mode settings)
            AnalyticsProviderFactory.SendToAllProviders(provider => 
            {
            provider.TrackImpression(EventNames.IMPRESSION, parameters);
            });
            
            // Return the impression_id for click linking
            return impressionId;
        }
        
        /// <summary>
        /// Track click event with optional cross-promotion context
        /// Routes to all available providers (BoostOps, Firebase, Unity) based on configuration
        /// Note: source_store_id is available in context.store_id (not passed as parameter)
        /// Note: source_project_id is derived server-side from project_key (not sent from SDK)
        /// </summary>
        public static void TrackClick(string campaignSlug, string placement = null,
            int? clickX = null, int? clickY = null, int? timeToClickMs = null,
            string targetStoreId = null, string targetProjectId = null,
            string networkCampaignId = null, string deepLinkUrl = null, string format = null, string channel = null, int? position = null,
            string impressionId = null, long? impressionTimestamp = null, string containerImpressionId = null, string clickId = null)
        {
            // Create parameters for provider interface
            var parameters = new Dictionary<string, string>
            {
                ["campaign_slug"] = campaignSlug ?? "",
                ["placement"] = placement ?? "",
                // Note: source_store_id is in context.store_id (universal) - not included here
                // Note: source_project_id is derived server-side from project_key (not sent from SDK)
                ["target_store_id"] = targetStoreId ?? "",
                ["target_project_id"] = targetProjectId ?? "",
                ["network_campaign_id"] = networkCampaignId ?? "",
                ["deep_link_url"] = deepLinkUrl ?? "",
                ["format"] = format ?? "",
                ["channel"] = channel ?? "",
                ["impression_id"] = impressionId ?? "",
                ["impression_timestamp"] = impressionTimestamp?.ToString() ?? "",
                ["container_impression_id"] = containerImpressionId ?? "",
                ["click_id"] = clickId ?? ""
            };
            
            if (clickX.HasValue)
                parameters["click_x"] = clickX.Value.ToString();
            if (clickY.HasValue)
                parameters["click_y"] = clickY.Value.ToString();
            if (timeToClickMs.HasValue)
                parameters["time_to_click_ms"] = timeToClickMs.Value.ToString();
            if (position.HasValue)
                parameters["position"] = position.Value.ToString();
            
        // Send to all available providers (respects local mode settings)
        AnalyticsProviderFactory.SendToAllProviders(provider => 
        {
            provider.TrackClick(EventNames.CLICK, parameters);
        });
        }
        
        /// <summary>
        /// Track app wall impression with nested items array
        /// Uses standard impression event (event_type="boostops_impression") with format="app_wall"
        /// Sends a single impression event containing data for all displayed items
        /// Each item has its own impression_id for click linking
        /// Note: source_store_id is available in context.store_id (not passed as parameter)
        /// Note: source_project_id is derived server-side from project_key (not sent from SDK)
        /// </summary>
        public static void TrackAppWallImpression(string placement, List<Dictionary<string, object>> items, string containerImpressionId)
        {
            try
            {
                // Create standard impression event with format="app_wall" and nested items
                var eventData = BoostOpsEventBuilder.CreateAppWallImpressionEvent(
                    placement: placement,
                    items: items,
                    containerImpressionId: containerImpressionId
                    // Note: source_store_id is in context.store_id (universal) - not duplicated here
                    // Note: source_project_id is derived server-side from project_key
                );
                
                // Queue the event for sending
                BoostOpsAnalyticsClient.Instance.QueueEvent(eventData);
                
                // Flush immediately to ensure app_wall impressions are tracked promptly
                BoostOpsAnalyticsClient.Instance.FlushQueue((success) => {
                    if (!success)
                    {
                        BoostOpsLogger.LogWarning("Analytics", "⚠️ App wall impression flush failed");
                    }
                });
                
#if BOOSTOPS_DEBUG_LOGGING
                BoostOpsLogger.LogDebug("Analytics", $"Tracked app_wall impression: placement={placement}, format=app_wall, items={items.Count}, container_impression_id={containerImpressionId}");
#endif
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to track app_wall impression: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Track install event - DEPRECATED: Use TrackAppOpen with isFirstSession=true instead (industry standard)
        /// </summary>
        [System.Obsolete("Use TrackAppOpen with isFirstSession=true instead. Separate install events are deprecated in favor of industry standard first session approach.")]
        public static void TrackInstall(bool organic = false, bool reinstall = false, bool forceManagedMode = false)
        {
            // Redirect to app open event with first session flag (industry standard)
            TrackAppOpen(launchType: "install", isFirstSession: true, organic: organic, reinstall: reinstall, forceManagedMode: forceManagedMode);
        }
        
        /// <summary>
        /// Send first session event directly to BoostOps Analytics using hardcoded endpoint (industry standard)
        /// Bypasses remote config, queuing, and provider system for maximum reliability
        /// ONLY sends in managed mode - respects local/demo mode settings
        /// </summary>
        private static void TrackFirstSessionToBoostOpsDirectly(string launchType, string deeplinkUrl, bool? organic, bool? reinstall, bool forceManagedMode = false,
            string attributionChannel = null, string attributionCampaignSlug = null, string attributionCampaign = null,
            bool? isReengagement = null, string attributionModel = null, string touchType = null, long? touchTs = null)
        {
            // DEBUG: Record this call for double-send detection
            BoostOps.Internal.AppOpenEventDebugger.RecordCall(launchType, true, "TrackFirstSessionToBoostOpsDirectly");
            
#if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("Analytics", $"TrackFirstSessionToBoostOpsDirectly: launch={launchType}, forced={forceManagedMode}, attribution_channel={attributionChannel}");
#endif
            
            try
            {
                // Get project settings to check if we're in managed mode
                var settings = InternalSettingsCache.GetProjectSettings();
                if (settings == null || string.IsNullOrEmpty(settings.ProjectKey))
                {
                    BoostOpsLogger.LogWarning("Analytics", "First session event skipped - no project key configured");
                    return;
                }
                
                // Check if we're in managed mode - only send to BoostOps servers in managed mode
                if (!settings.UseRemoteManagement && !forceManagedMode)
                {
                    BoostOpsLogger.LogDebug("Analytics", "First session event skipped - local/demo mode");
                    return;
                }
                
                // BoostOpsLogger.LogInfo("Analytics", "🎯 CRITICAL FIRST SESSION EVENT - sending to BoostOps");
                
                // Initialize analytics client directly with hardcoded endpoint
                BoostOpsAnalyticsClient.Instance.Initialize(
                    settings.ProjectKey, 
                    INSTALL_EVENTS_ENDPOINT,  // Hardcoded - no remote config dependency
                    isDevelopmentMode: true  // TEMPORARY: Enable to debug server response
                );
                
                // CRITICAL: Create app open event with first session flag (industry standard)
                // This should ONLY be called for the very first app launch after install
                var appOpenEvent = BoostOpsEventBuilder.CreateAppOpenEvent(launchType, deeplinkUrl, isFirstSession: true, organic: organic, reinstall: reinstall,
                    attributionChannel: attributionChannel, attributionCampaignSlug: attributionCampaignSlug, attributionCampaign: attributionCampaign,
                    isReengagement: isReengagement, attributionModel: attributionModel, touchType: touchType, touchTs: touchTs);
                
                // Verify first_open is true (critical install attribution field)
                if (appOpenEvent.@event.first_open != true)
                {
                    Debug.LogError("[BoostOps] CRITICAL BUG: first_open should be true for first session but was: " + appOpenEvent.@event.first_open);
                    appOpenEvent.@event.first_open = true; // Force correct value
                }
                
                BoostOpsAnalyticsClient.Instance.QueueEvent(appOpenEvent);
                
                // Flush the queue immediately to send the first session event
                BoostOpsAnalyticsClient.Instance.FlushQueue((success) =>
                {
                    if (success)
                    {
                        // BoostOpsLogger.LogInfo("Analytics", "✅ First session event sent successfully");
                    }
                    else
                    {
                        BoostOpsLogger.LogError("Analytics", "❌ First session event failed to send");
                    }
                });
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Critical first session event failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Send regular session event directly to BoostOps Analytics using hardcoded endpoint
        /// Similar to TrackFirstSessionToBoostOpsDirectly but without install attribution data
        /// </summary>
        private static void TrackRegularSessionToBoostOpsDirectly(string launchType, string deeplinkUrl, bool forceManagedMode = false,
            string attributionChannel = null, string attributionCampaignSlug = null, string attributionCampaign = null,
            bool? isReengagement = null, string attributionModel = null, string touchType = null, long? touchTs = null)
        {
            // DEBUG: Record this call for double-send detection
            BoostOps.Internal.AppOpenEventDebugger.RecordCall(launchType, false, "TrackRegularSessionToBoostOpsDirectly");
            
#if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("Analytics", $"TrackRegularSessionToBoostOpsDirectly: launch={launchType}, attribution_channel={attributionChannel}");
#endif
            
            try
            {
                // Get project settings to check if we're in managed mode
                var settings = InternalSettingsCache.GetProjectSettings();
                if (settings == null || string.IsNullOrEmpty(settings.ProjectKey))
                {
                    BoostOpsLogger.LogWarning("Analytics", "Regular session event skipped - no project key");
                    return;
                }
                
                // Check if we're in managed mode - only send to BoostOps servers in managed mode
                if (!settings.UseRemoteManagement && !forceManagedMode)
                {
                    BoostOpsLogger.LogDebug("Analytics", "Regular session event skipped - local/demo mode");
                    return;
                }
                
                BoostOpsLogger.LogInfo("Analytics", "🔄 Regular session event - sending to BoostOps");
                
                // Initialize analytics client directly with hardcoded endpoint (reuse same endpoint as first session)
                BoostOpsAnalyticsClient.Instance.Initialize(
                    settings.ProjectKey, 
                    INSTALL_EVENTS_ENDPOINT,  // Hardcoded - no remote config dependency
                    isDevelopmentMode: true  // TEMPORARY: Enable to debug server response
                );
                
                // CRITICAL: Create app open event with first_open explicitly set to FALSE for regular sessions
                // This ensures backend never sees first_open=true for non-install sessions
                var appOpenEvent = BoostOpsEventBuilder.CreateAppOpenEvent(launchType, deeplinkUrl, isFirstSession: false,
                    attributionChannel: attributionChannel, attributionCampaignSlug: attributionCampaignSlug, attributionCampaign: attributionCampaign,
                    isReengagement: isReengagement, attributionModel: attributionModel, touchType: touchType, touchTs: touchTs);
                
                // Double-check first_open is false (safety check for critical field)
                if (appOpenEvent.@event.first_open != false)
                {
                    Debug.LogError("[BoostOps] CRITICAL BUG: first_open should be false for regular session but was: " + appOpenEvent.@event.first_open);
                    appOpenEvent.@event.first_open = false; // Force correct value
                }
                
                BoostOpsAnalyticsClient.Instance.QueueEvent(appOpenEvent);
                
                // Flush the queue immediately to send the regular session event
                BoostOpsAnalyticsClient.Instance.FlushQueue((success) =>
                {
                    if (!success)
                    {
                        BoostOpsLogger.LogWarning("Analytics", "Regular session event failed to send");
                    }
                });
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Regular session event failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Send regular app open event to Firebase and Unity Analytics only
        /// BoostOps events are sent directly via TrackFirstSessionToBoostOpsDirectly/TrackRegularSessionToBoostOpsDirectly
        /// </summary>
        private static void TrackRegularAppOpenToProviders(string sessionId, string launchType, string deeplinkUrl)
        {
            // Create parameters for provider interface
            var parameters = new Dictionary<string, string>
            {
                ["session_id"] = sessionId ?? "",
                ["launch_type"] = launchType ?? "cold",
                ["deep_link_url"] = deeplinkUrl ?? ""
            };
            
            var timeSinceInstallMs = GetTimeSinceInstallMs();
            if (timeSinceInstallMs.HasValue)
                parameters["time_since_install_ms"] = timeSinceInstallMs.Value.ToString();
            
            // Send to Firebase and Unity Analytics only (NOT BoostOps - that's sent directly)
            // BoostOps events are sent via TrackFirstSessionToBoostOpsDirectly or TrackRegularSessionToBoostOpsDirectly
            var firebaseProvider = AnalyticsProviderFactory.GetProvider<FirebaseAnalyticsProvider>();
            firebaseProvider?.TrackEvent(EventNames.APP_OPEN, parameters);
            
            var unityProvider = AnalyticsProviderFactory.GetProvider<UnityAnalyticsProvider>();
            unityProvider?.TrackEvent(EventNames.APP_OPEN, parameters);
            
#if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("Analytics", "Sent app_open to Firebase and Unity Analytics (BoostOps sent separately via direct method)");
#endif
        }
        
        /// <summary>
        /// Send install event directly to BoostOps Analytics using hardcoded endpoint
        /// DEPRECATED: Use TrackFirstSessionToBoostOpsDirectly instead
        /// </summary>
        [System.Obsolete("Use TrackFirstSessionToBoostOpsDirectly instead")]
        private static void TrackInstallToBoostOpsDirectly(bool organic, bool reinstall, bool forceManagedMode = false)
        {
            try
            {
                // Get project settings to check if we're in managed mode
                var settings = InternalSettingsCache.GetProjectSettings();
                if (settings == null || string.IsNullOrEmpty(settings.ProjectKey))
                {
                    Debug.LogWarning("[BoostOps Analytics] Install event skipped - no project key configured");
                    return;
                }
                
                // Check if we're in managed mode - only send to BoostOps servers in managed mode
                if (!settings.UseRemoteManagement && !forceManagedMode)
                {
                    Debug.Log("[BoostOps Analytics] 🎯 Install event skipped - SDK in local/demo mode (not sending to BoostOps servers)");
                    return;
                }
                
                Debug.Log($"[BoostOps Analytics] 🎯 CRITICAL INSTALL EVENT (Managed Mode) - using assume endpoint: {INSTALL_EVENTS_ENDPOINT}");
                
                // Initialize analytics client directly with hardcoded endpoint
                BoostOpsAnalyticsClient.Instance.Initialize(
                    settings.ProjectKey, 
                    INSTALL_EVENTS_ENDPOINT,  // Hardcoded - no remote config dependency
                    isDevelopmentMode: true  // TEMPORARY: Enable to debug server response
                );
                
                // Create install event, queue it, and flush immediately
                var installEvent = BoostOpsEventBuilder.CreateInstallEvent(organic, reinstall);
                BoostOpsAnalyticsClient.Instance.QueueEvent(installEvent);
                
                // Flush the queue immediately to send the install event
                BoostOpsAnalyticsClient.Instance.FlushQueue((success) =>
                {
                    if (success)
                    {
                        Debug.Log("[BoostOps Analytics] ✅ CRITICAL INSTALL EVENT sent successfully to BoostOps servers");
                    }
                    else
                    {
                        Debug.LogError("[BoostOps Analytics] ❌ CRITICAL INSTALL EVENT failed to send to BoostOps servers");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps Analytics] Critical install event failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Track app open event (industry standard approach)
        /// When isFirstSession=true, includes install attribution data and uses hardcoded endpoint for reliability
        /// Routes to all available providers (BoostOps, Firebase, Unity) based on configuration
        /// </summary>
        public static void TrackAppOpen(string sessionId = null, string launchType = "cold", 
            string deeplinkUrl = null, bool? isFirstSession = null, bool? organic = null, bool? reinstall = null, bool forceManagedMode = false,
            string attributionChannel = null, string attributionCampaignSlug = null, string attributionCampaign = null,
            bool? isReengagement = null, string attributionModel = null, string touchType = null, long? touchTs = null)
        {
            // DEBUG: Record this call for double-send detection
            BoostOps.Internal.AppOpenEventDebugger.RecordCall(launchType, isFirstSession, "TrackAppOpen");
            
#if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("Analytics", $"TrackAppOpen: launch={launchType}, first={isFirstSession}, attribution_channel={attributionChannel}");
#endif
            
            // Industry standard: Always send app open events directly to BoostOps for reliability
            if (isFirstSession == true)
            {
                // CRITICAL: Send to BoostOps Analytics immediately using hardcoded endpoint for first session
                TrackFirstSessionToBoostOpsDirectly(launchType, deeplinkUrl, organic, reinstall, forceManagedMode, 
                    attributionChannel, attributionCampaignSlug, attributionCampaign, isReengagement, attributionModel, touchType, touchTs);
            }
            else
            {
                // Regular app open - also send directly to BoostOps for reliability (same as first session but without install attribution)
                TrackRegularSessionToBoostOpsDirectly(launchType, deeplinkUrl, forceManagedMode,
                    attributionChannel, attributionCampaignSlug, attributionCampaign, isReengagement, attributionModel, touchType, touchTs);
            }
            
            // Also send to other providers (Firebase, Unity Analytics) for regular sessions
            if (isFirstSession != true)
            {
                TrackRegularAppOpenToProviders(sessionId, launchType, deeplinkUrl);
            }
            
            // Update SKAN conversion value for iOS attribution (only on first launch)
            #if UNITY_IOS
            if (isFirstSession == true && BoostOpsSKANManager.Instance != null)
            {
                var eventData = new Dictionary<string, object>
                {
                    ["launch_count"] = 1
                };
                BoostOpsSKANManager.Instance.UpdateConversionValueForEvent("app_launch", eventData);
            }
            #endif
        }
        
        #endregion
        
        #region Cross-Promotion Convenience Methods (SDK Events Only) - DEPRECATED
        
        /// <summary>
        /// [DEPRECATED in Schema v3] Use TrackImpression() with explicit sourceStoreId, sourceProjectId, targetStoreId, targetProjectId instead
        /// </summary>
        [System.Obsolete("TrackCrossPromoImpression is deprecated in Schema v3. Use TrackImpression() with explicit sourceStoreId, sourceProjectId, targetStoreId, targetProjectId parameters instead.", true)]
        public static void TrackCrossPromoImpression(string sourceProject, string targetProject, 
            string networkCampaignId, string placementId, string campaignSlug, string creativeId = null,
            string format = "banner", int? durationMs = null, float? revenueShareRate = null, 
            decimal? estimatedCpm = null, string currency = "USD", string channel = "xpromo")
        {
            // Method removed in Schema v3 - use TrackImpression() instead
            throw new System.NotSupportedException("TrackCrossPromoImpression is deprecated in Schema v3. Use TrackImpression() with explicit sourceStoreId, sourceProjectId, targetStoreId, targetProjectId parameters instead.");
        }
        
        /// <summary>
        /// [DEPRECATED in Schema v3] Use TrackClick() with explicit sourceStoreId, sourceProjectId, targetStoreId, targetProjectId instead
        /// </summary>
        [System.Obsolete("TrackCrossPromoClick is deprecated in Schema v3. Use TrackClick() with explicit sourceStoreId, sourceProjectId, targetStoreId, targetProjectId parameters instead.", true)]
        public static void TrackCrossPromoClick(string sourceProject, string targetProject, 
            string networkCampaignId, string placementId, string campaignSlug, string deepLinkUrl,
            string creativeId = null, int? clickX = null, int? clickY = null, int? timeToClickMs = null,
            decimal? clickValue = null, float? revenueShareRate = null, string currency = "USD", string format = null, string channel = "xpromo")
        {
            // Method removed in Schema v3 - use TrackClick() instead
            throw new System.NotSupportedException("TrackCrossPromoClick is deprecated in Schema v3. Use TrackClick() with explicit sourceStoreId, sourceProjectId, targetStoreId, targetProjectId parameters instead.");
        }
        
        #endregion
        

        
        /// <summary>
        /// Get stored BoostOps ID (universal cross-app correlation identifier)
        /// </summary>
        public static string GetStoredBoostOpsId()
        {
            return PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.BOOSTOPS_ID, null);
        }
        

        
        /// <summary>
        /// Calculate time since app was first installed in milliseconds
        /// </summary>
        private static long? GetTimeSinceInstallMs()
        {
            // Use consistent PlayerPrefs key
            var firstLaunchTimeKey = BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TIME;
            
            if (!PlayerPrefs.HasKey(firstLaunchTimeKey))
            {
                // First time - store current time
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                PlayerPrefs.SetString(firstLaunchTimeKey, currentTime.ToString());
                PlayerPrefs.Save();
                return 0; // Just installed
            }
            
            // Calculate time since first launch
            var firstLaunchTimeStr = PlayerPrefs.GetString(firstLaunchTimeKey);
            if (long.TryParse(firstLaunchTimeStr, out long firstLaunchTime))
            {
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var timeSinceInstallSeconds = currentTime - firstLaunchTime;
                return timeSinceInstallSeconds * 1000; // Convert to milliseconds
            }
            
            return null; // Unable to calculate
        }
        
        // Legacy OnInstallTokenReceived event removed - boostops_id is client-generated only
        
        /// <summary>
        /// Initialize complete analytics system from project settings
        /// This initializes both the legacy BoostOps Analytics Client and the new provider system
        /// (BoostOps, Firebase, Unity) based on configuration and availability
        /// </summary>
        /// <param name="isDevelopmentMode">Use development endpoints and enable debug JSON payload logging</param>
        public static void InitializeAnalyticsFromSettings(bool isDevelopmentMode = false)
        {
            BoostOpsLogger.LogDebug("Analytics", "Initializing complete analytics system...");
            
            try
            {
                // Enable debug logging for analytics visibility during development
                BoostOpsLogger.IsDebugLoggingEnabled = true;
                // BoostOpsLogger.LogInfo("Analytics", "🔧 Debug logging enabled for analytics system");
                
                // Initialize analytics provider system (BoostOps, Firebase, Unity)
                var availableProviders = AnalyticsProviderFactory.GetAvailableProviders();
                
                // BoostOpsLogger.LogInfo("Analytics", $"✅ Analytics provider system initialized with {availableProviders.Count} available providers");
                
                // Make provider status always visible (not just debug)
                // foreach (var provider in availableProviders)
                // {
                //     BoostOpsLogger.LogInfo("Analytics", $"  ✓ {provider.ProviderName} - Available");
                // }
                
                // Log disabled providers for debugging
                var allProviders = AnalyticsProviderFactory.GetProviders();
                var disabledProviders = allProviders.Where(p => !p.IsAvailable).ToList();
                
                foreach (var provider in disabledProviders)
                {
                    BoostOpsLogger.LogInfo("Analytics", $"  ✗ {provider.ProviderName} - Disabled");
                }
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to initialize analytics system: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Queue an analytics event for batch sending (high performance)
        /// </summary>
        /// <param name="eventType">Event type</param>
        /// <param name="parameters">Event parameters</param>
        public static void QueueAnalyticsEvent(string eventType, Dictionary<string, string> parameters)
        {
            // Enhanced debugging for install event troubleshooting
            BoostOpsLogger.LogDebug("Analytics", $"🔍 QueueAnalyticsEvent called for: {eventType}");
            
            var eventData = BoostOpsEventBuilder.CreateEvent(eventType);
            BoostOpsAnalyticsClient.Instance.QueueEvent(eventData);
            BoostOpsLogger.LogDebug("Analytics", $"📤 {eventType} event queued successfully (Queue size: {BoostOpsAnalyticsClient.Instance.QueuedEventCount})");
        }
        
        /// <summary>
        /// Send analytics event immediately (use sparingly)
        /// This method is for BoostOps backend-specific events that require immediate confirmation
        /// In local mode, this will skip sending to prevent server errors
        /// </summary>
        /// <param name="eventType">Event type</param>
        /// <param name="parameters">Event parameters</param>
        /// <param name="onComplete">Callback when complete</param>
        public static void SendAnalyticsEventImmediate(string eventType, Dictionary<string, string> parameters, Action<bool> onComplete = null)
        {
            // Check if in local mode - skip BoostOps server calls
            var settings = InternalSettingsCache.GetProjectSettings();
            if (settings?.UseRemoteManagement != true)
            {
                BoostOpsLogger.LogDebug("Analytics", $"🏠 Local mode - skipping immediate BoostOps server event: {eventType}");
                onComplete?.Invoke(false);  // Return false since server event wasn't sent
                return;
            }
            
            if (!BoostOpsAnalyticsClient.Instance.IsInitialized)
            {
                BoostOpsLogger.LogWarning("Analytics", $"❌ Analytics client not initialized - {eventType} event DROPPED!");
                BoostOpsLogger.LogWarning("Analytics", "💡 Ensure BoostOpsManager.Initialize() was called and project key is set in settings.");
                BoostOpsLogger.LogWarning("Analytics", "🔍 Use BoostOpsManager.Instance.DebugAnalyticsProviders() for detailed diagnostics.");
                onComplete?.Invoke(false);
                return;
            }
            
            var eventData = BoostOpsEventBuilder.CreateEvent(eventType);
            BoostOpsAnalyticsClient.Instance.QueueEvent(eventData);
            
            // For immediate events, flush the queue right away
            BoostOpsAnalyticsClient.Instance.FlushQueue((success) =>
            {
                // Note: Individual event responses not available in batch mode
                // Install token handling would need to be implemented differently if needed
                onComplete?.Invoke(success);
            });
        }
        
        /// <summary>
        /// Flush all queued events immediately
        /// </summary>
        /// <param name="onComplete">Callback when flush is complete</param>
        public static void FlushAnalyticsQueue(Action<bool> onComplete = null)
        {
            BoostOpsAnalyticsClient.Instance.FlushQueue(onComplete);
        }
        
        /// <summary>
        /// Get current analytics queue status
        /// </summary>
        /// <returns>Number of queued events</returns>
        public static int GetQueuedEventCount()
        {
            return BoostOpsAnalyticsClient.Instance.QueuedEventCount;
        }
        
        /// <summary>
        /// Check if a parameter key is reserved for top-level analytics event fields
        /// </summary>
        private static bool IsReservedField(string key)
        {
            var reservedFields = new[]
            {
                "hashed_idfv", "ios_attribution_token", "ios_attribution_data", "ios_attribution_type",
                "campaign_id", "channel", "source_app_id", "target_app_id", "event_type"
            };
            return reservedFields.Contains(key);
        }
        
        /// <summary>
        /// Track an in-app purchase.
        ///
        /// As of SDK 1.1.0, purchases are delivered via the dedicated
        /// BoostOps purchase endpoint (POST /v1/purchases) — not the generic
        /// event log. The endpoint is idempotent on (project_id, store,
        /// transaction_id) and the SDK persists pending purchases to disk
        /// so an app kill mid-flight will not lose the revenue event.
        ///
        /// The SDK still:
        ///   - Mirrors the purchase to Unity Analytics and Firebase
        ///     Analytics (when configured).
        ///   - Bumps the iOS SKAN conversion value when applicable.
        ///   - Maintains local first_purchase / purchase_count counters.
        /// </summary>
        /// <param name="amount">Purchase amount in local currency (REQUIRED)</param>
        /// <param name="currency">ISO 4217 currency code: USD, EUR, GBP, JPY, etc. (REQUIRED)</param>
        /// <param name="productId">Product identifier from app store (REQUIRED)</param>
        /// <param name="transactionId">Store transaction ID (REQUIRED) - The unique store-issued identifier. Used as the dedup key.</param>
        /// <param name="receipt">Store receipt or purchase token (OPTIONAL) - For server-side validation.</param>
        public static void TrackPurchase(
            decimal amount,
            string currency,
            string productId,
            string transactionId = null,
            string receipt = null)
        {
            TrackPurchase(new BoostOpsPurchaseInfo
            {
                Amount = amount,
                Currency = currency,
                ProductId = productId,
                TransactionId = transactionId,
                Receipt = receipt
            });
        }

        /// <summary>
        /// Track an in-app purchase with full control over subscription
        /// metadata, original transaction IDs, sandbox flags, and timestamps.
        ///
        /// Use this overload when:
        ///   - The purchase is a subscription (set IsSubscription / IsTrial).
        ///   - You have a separate OriginalTransactionId (subscription renewals).
        ///   - You need to override sandbox detection or timestamps.
        ///   - You want a stable ClientEventId across retries.
        /// </summary>
        public static void TrackPurchase(BoostOpsPurchaseInfo info)
        {
            if (info == null)
            {
                BoostOpsLogger.LogError("Analytics", "TrackPurchase called with null info");
                return;
            }

            // Local first-purchase / purchase-count counters (used for SKAN bump
            // and third-party mirroring; not sent to the dedicated endpoint).
            bool isFirstPurchase = PlayerPrefs.GetInt("BoostOps_HasMadePurchase", 0) == 0;
            int currentPurchaseCount = PlayerPrefs.GetInt("BoostOps_PurchaseCount", 0);
            int newPurchaseCount = currentPurchaseCount + 1;

            if (isFirstPurchase)
            {
                PlayerPrefs.SetInt("BoostOps_HasMadePurchase", 1);
            }
            PlayerPrefs.SetInt("BoostOps_PurchaseCount", newPurchaseCount);
            PlayerPrefs.Save();

            if (string.IsNullOrEmpty(info.TransactionId))
            {
                BoostOpsLogger.LogWarning("Analytics",
                    $"⚠️ TrackPurchase missing transaction_id for {info.ProductId}. The dedicated purchases endpoint requires it for dedup; the call will be rejected.");
            }

#if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("Analytics",
                $"TrackPurchase: {info.ProductId} - {info.Amount} {info.Currency}, txnId={info.TransactionId ?? "(none)"}, hasReceipt={!string.IsNullOrEmpty(info.Receipt)}, first_purchase={isFirstPurchase}, purchase_count={newPurchaseCount}");
#endif

            // 1) Build and ship the typed purchase request to the dedicated endpoint.
            var request = BuildPurchaseRequest(info);
            BoostOps.Analytics.BoostOpsPurchaseClient.Instance.TrackPurchase(request);

            // 2) Mirror to Unity Analytics + Firebase Analytics (third-party
            //    dashboards still expect a "purchase" event to show up there).
            var mirrorParams = new Dictionary<string, object>
            {
                ["amount"] = info.Amount,
                ["currency"] = info.Currency ?? "USD",
                ["product_id"] = info.ProductId ?? "",
                ["transaction_id"] = info.TransactionId ?? "",
                ["receipt"] = info.Receipt ?? "",
                ["quantity"] = 1,
                ["first_purchase"] = isFirstPurchase,
                ["purchase_count"] = newPurchaseCount,
                ["is_subscription"] = info.IsSubscription,
                ["is_trial"] = info.IsTrial
            };
            var filteredMirrorParams = FilterSensitiveParameters(mirrorParams);

            var unityProvider = AnalyticsProviderFactory.GetProvider<UnityAnalyticsProvider>();
            unityProvider?.TrackPurchase(EventNames.PURCHASE, filteredMirrorParams);

            var firebaseProvider = AnalyticsProviderFactory.GetProvider<FirebaseAnalyticsProvider>();
            firebaseProvider?.TrackPurchase(EventNames.PURCHASE, filteredMirrorParams);

            // 3) Update SKAN conversion value (iOS attribution).
#if UNITY_IOS
            if (BoostOpsSKANManager.Instance != null)
            {
                var skanEventData = new Dictionary<string, object>
                {
                    ["amount"] = info.Amount,
                    ["is_first_purchase"] = isFirstPurchase,
                    ["purchase_count"] = newPurchaseCount
                };

                BoostOpsSKANManager.Instance.UpdateConversionValueForEvent("purchase", skanEventData);
            }
#endif

#if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("Analytics",
                $"✅ Purchase tracked: {info.ProductId} - {info.Amount} {info.Currency} (txn: {info.TransactionId})");
#endif
        }

        /// <summary>
        /// Translate the public BoostOpsPurchaseInfo into the wire-shape
        /// BoostOpsPurchaseRequest that the dedicated endpoint accepts.
        /// Auto-derives store, sandbox flag, country, and timestamp when the
        /// caller has not provided them.
        /// </summary>
        private static BoostOps.Analytics.BoostOpsPurchaseRequest BuildPurchaseRequest(BoostOpsPurchaseInfo info)
        {
            // Store: caller override wins; otherwise infer from runtime platform.
            string store = !string.IsNullOrEmpty(info.Store)
                ? info.Store.ToLowerInvariant()
                : InferStoreFromPlatform();

            // amount_micros: convert local-currency decimal to integer micros.
            long amountMicros = BoostOps.Analytics.CurrencyMicros.ToMicros(info.Amount);
            if (amountMicros < 0) amountMicros = 0;

            // Sandbox: caller override wins; otherwise infer from environment.
            bool isSandbox = info.IsSandboxOverride.HasValue
                ? info.IsSandboxOverride.Value
                : InferSandbox();

            // Timestamp: default(DateTime) means "use now".
            DateTime ts = info.PurchaseTimestamp == default(DateTime)
                ? DateTime.UtcNow
                : info.PurchaseTimestamp.ToUniversalTime();

            string clientEventId = !string.IsNullOrEmpty(info.ClientEventId)
                ? info.ClientEventId
                : Guid.NewGuid().ToString("N");

            // Build the shared envelope (schema/timestamps/identifiers/routing
            // flags/consent/context). Same builder the events endpoint uses,
            // so the two pipelines collect an identical surface area.
            var common = BoostOps.Analytics.BoostOpsCommonPayloadBuilder.Build(
                includeInstallTimestamp: false,
                includeInstallTimeExtras: false);

            return new BoostOps.Analytics.BoostOpsPurchaseRequest
            {
                Common                  = common,

                store                   = store,
                transaction_id          = info.TransactionId,
                original_transaction_id = string.IsNullOrEmpty(info.OriginalTransactionId) ? info.TransactionId : info.OriginalTransactionId,
                product_id              = info.ProductId,
                amount_micros           = amountMicros,
                currency                = string.IsNullOrEmpty(info.Currency) ? "USD" : info.Currency.ToUpperInvariant(),
                country                 = string.IsNullOrEmpty(info.Country) ? null : info.Country.ToUpperInvariant(),
                receipt                 = string.IsNullOrEmpty(info.Receipt) ? null : info.Receipt,
                receipt_format          = string.IsNullOrEmpty(info.ReceiptFormat) ? null : info.ReceiptFormat.ToLowerInvariant(),
                is_subscription         = info.IsSubscription,
                is_trial                = info.IsTrial,
                is_sandbox              = isSandbox,
                purchase_timestamp      = ts.ToString("o"),
                client_event_id         = clientEventId,
            };
        }

        private static string InferStoreFromPlatform()
        {
#if UNITY_IOS
            return "app_store";
#elif UNITY_ANDROID
            return "google_play";
#else
            // Editor and other platforms: best-effort guess. Server will reject
            // with a clear validation error if the inferred store isn't valid;
            // the caller can override via BoostOpsPurchaseInfo.Store.
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer: return "app_store";
                case RuntimePlatform.Android: return "google_play";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxEditor:
                    // Editor: default to app_store so dev-time tests still produce a valid payload.
                    return "app_store";
                default:
                    return "app_store";
            }
#endif
        }

        private static bool InferSandbox()
        {
            try
            {
                if (BoostOpsEnvironment.IsEditor()) return true;
#if UNITY_IOS && !UNITY_EDITOR
                if (BoostOpsEnvironment.IsTestFlight()) return true;
#endif
                if (BoostOpsEnvironment.IsDebugBuild()) return true;
            }
            catch { /* environment helpers can throw on unsupported platforms */ }
            return false;
        }

        
        /// <summary>
        /// [OBSOLETE - NOT USED] Track conversion event for attribution analysis.
        /// 
        /// As of SDK v2.0, conversion events are SKAN-only (no server calls) to minimize costs.
        /// BoostOps now only tracks install (app_open) and purchase events on the server.
        /// For engagement analytics, developers should use Firebase or Unity Analytics.
        /// 
        /// This method is kept for backward compatibility but does nothing.
        /// </summary>
        [System.Obsolete("TrackConversionEvent no longer sends events to server. Use Firebase/Unity Analytics for engagement tracking.", false)]
        internal static void TrackConversionEvent(string eventName, Dictionary<string, object> parameters)
        {
            // NO-OP: Conversion events are now SKAN-only (local, no server)
            // BoostOps focuses on attribution: install + purchase events only
            // Developers should use Firebase/Unity Analytics for engagement analytics
            
#if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("Analytics", $"⚠️ TrackConversionEvent called but not sent to server (SKAN-only): {eventName}");
#endif
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Create base parameter set for impression/click events
        /// </summary>
        private static Dictionary<string, string> CreateBaseParameters(
            string channel,
            string sourceAppId,
            string targetAppId,
            string placementId,
            string campaignId,
            string creativeId,
            string source = null)
        {
            var parameters = new Dictionary<string, string>
            {
                ["channel"] = channel,
                ["source_app_id"] = sourceAppId,
                ["target_app_id"] = targetAppId,
                ["placement_id"] = placementId,
                ["campaign_id"] = campaignId,
                ["creative_id"] = creativeId
            };
            
            if (!string.IsNullOrEmpty(source))
            {
                parameters["source"] = source;
            }
            
            return parameters;
        }
        
        /// <summary>
        /// Convert Channel enum to contract string
        /// </summary>
        private static string GetChannelString(Channel channel)
        {
            return channel switch
            {
                Channel.XPromo => "xpromo",
                Channel.BoostLink => "boostlink",
                _ => "xpromo"
            };
        }
        
        /// <summary>
        /// Convert EventSource enum to contract string
        /// </summary>
        private static string GetSourceString(EventSource source)
        {
            return source switch
            {
                EventSource.SDK => "sdk",
                EventSource.Router => "router",
                _ => "sdk"
            };
        }
        
        /// <summary>
        /// Convert AttributionMethod enum to contract string
        /// </summary>
        private static string GetAttributionMethodString(AttributionMethod method)
        {
            return method switch
            {
                AttributionMethod.Probabilistic => "probabilistic",
                AttributionMethod.Deterministic => "deterministic",
                _ => "probabilistic"
            };
        }
        
        /// <summary>
        /// Filter sensitive device identifiers from parameters before sending to third-party analytics
        /// Removes hashed IDFV, IDFA, and device ID parameters that should not be sent to Unity/Google Analytics
        /// </summary>
        /// <param name="originalParameters">Original parameter dictionary (string, string)</param>
        /// <returns>Filtered parameter dictionary without sensitive device identifiers</returns>
        private static Dictionary<string, string> FilterSensitiveParameters(Dictionary<string, string> originalParameters)
        {
            if (originalParameters == null)
                return new Dictionary<string, string>();
                
            var filteredParameters = new Dictionary<string, string>();
            
            // List of sensitive parameter keys that should be filtered out
            var sensitiveKeys = new HashSet<string>
            {
                "idfv",                // Hashed IDFV
                "hashed_idfv",         // Alternative naming
                "idfa",                // Hashed IDFA
                "hashed_idfa",         // Alternative naming
                "advertising_id",      // Alternative naming for IDFA
                "vendor_id",           // Alternative naming for IDFV
                "gaid",                // Hashed GAID
                "hashed_gaid",         // Alternative naming
                "android_id",          // Hashed Android ID
                "hashed_android_id",   // Alternative naming
                "ios_attribution_token", // iOS attribution tokens
                "ios_attribution_data",  // iOS attribution data
                "apple_attribution_token", // Apple attribution tokens
                "play_install_referrer", // Android Play Install Referrer
                "install_token"        // BoostOps install tokens
            };
            
            // Copy all parameters except sensitive ones
            foreach (var kvp in originalParameters)
            {
                if (!sensitiveKeys.Contains(kvp.Key))
                {
                    filteredParameters[kvp.Key] = kvp.Value;
                }
            }
            
            return filteredParameters;
        }
        
        /// <summary>
        /// Filter sensitive device identifiers from parameters before sending to third-party analytics
        /// Removes hashed IDFV, IDFA, and device ID parameters that should not be sent to Unity/Google Analytics
        /// </summary>
        /// <param name="originalParameters">Original parameter dictionary (string, object)</param>
        /// <returns>Filtered parameter dictionary without sensitive device identifiers</returns>
        private static Dictionary<string, object> FilterSensitiveParameters(Dictionary<string, object> originalParameters)
        {
            if (originalParameters == null)
                return new Dictionary<string, object>();
                
            var filteredParameters = new Dictionary<string, object>();
            
            // List of sensitive parameter keys that should be filtered out
            var sensitiveKeys = new HashSet<string>
            {
                "idfv",                // Hashed IDFV
                "hashed_idfv",         // Alternative naming
                "idfa",                // Hashed IDFA
                "hashed_idfa",         // Alternative naming
                "advertising_id",      // Alternative naming for IDFA
                "vendor_id",           // Alternative naming for IDFV
                "gaid",                // Hashed GAID
                "hashed_gaid",         // Alternative naming
                "android_id",          // Hashed Android ID
                "hashed_android_id",   // Alternative naming
                "ios_attribution_token", // iOS attribution tokens
                "ios_attribution_data",  // iOS attribution data
                "apple_attribution_token", // Apple attribution tokens
                "play_install_referrer", // Android Play Install Referrer
                "install_token"        // BoostOps install tokens
            };
            
            // Copy all parameters except sensitive ones
            foreach (var param in originalParameters)
            {
                if (!sensitiveKeys.Contains(param.Key))
                {
                    filteredParameters[param.Key] = param.Value;
                }
                else
                {
                    BoostOpsLogger.LogDebug("Analytics", $"Filtered sensitive parameter '{param.Key}' from third-party analytics");
                }
            }
            
            return filteredParameters;
        }
        

        
        /// <summary>
        /// Get source app store ID in the correct format for the current platform
        /// </summary>
        /// <returns>Platform-appropriate source store ID (iOS: Apple App Store ID, Android: package name, etc.)</returns>
        // Cached source project ID from remote config
        private static string _cachedSourceProjectId = null;
        private static bool _sourceProjectIdWarningLogged = false;
        
        /// <summary>
        /// Set the source project ID from remote config
        /// Called internally when remote config is loaded
        /// </summary>
        internal static void SetSourceProjectId(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                Debug.LogWarning("[BoostOpsAnalyticsContract] ⚠️ Attempted to set empty source project ID from remote config");
                return;
            }
            
            _cachedSourceProjectId = projectId;
            _sourceProjectIdWarningLogged = false; // Reset warning flag
            // Debug.Log($"[BoostOpsAnalyticsContract] ✅ Source project ID cached: {projectId}");
        }
        
        /// <summary>
        /// Get the source project ID (from BoostOps project settings)
        /// Returns empty string if project ID is not configured
        /// </summary>
        public static string GetSourceProjectId()
        {
            // Return cached source project ID from project settings
            // This is set at SDK initialization from BoostOpsProjectSettings.projectId
            // The projectId is fetched from the BoostOps backend when you register/login in the Editor Window
            
            if (string.IsNullOrEmpty(_cachedSourceProjectId) && !_sourceProjectIdWarningLogged)
            {
                Debug.LogWarning("[BoostOpsAnalyticsContract] ⚠️ Source project ID not available - project not registered.");
                Debug.LogWarning("[BoostOpsAnalyticsContract] 💡 Register your project in the BoostOps Editor Window to get a project ID.");
                Debug.LogWarning("[BoostOpsAnalyticsContract] 💡 Cross-promotion events will be missing source_project_id field until registered.");
                _sourceProjectIdWarningLogged = true;
            }
            
            return _cachedSourceProjectId ?? "";
        }
        
        /// <summary>
        /// Check if source project ID has been cached from remote config (for debugging)
        /// </summary>
        public static bool IsSourceProjectIdAvailable()
        {
            return !string.IsNullOrEmpty(_cachedSourceProjectId);
        }
        
        /// <summary>
        /// Log diagnostic information about source project ID status (for debugging)
        /// </summary>
        public static void LogSourceProjectIdStatus()
        {
            if (string.IsNullOrEmpty(_cachedSourceProjectId))
            {
                Debug.LogWarning("[BoostOpsAnalyticsContract] ❌ Source project ID: NOT SET");
                Debug.LogWarning("[BoostOpsAnalyticsContract] 💡 This means remote config hasn't been loaded or source_project.project_id is missing from the JSON");
            }
            else
            {
                Debug.Log($"[BoostOpsAnalyticsContract] ✅ Source project ID: {_cachedSourceProjectId}");
            }
        }
        
        /// <summary>
        /// Get the source store ID (platform-specific)
        /// </summary>
        public static string GetSourceStoreId()
        {
            var projectSettings = InternalSettingsCache.GetProjectSettings();
            if (projectSettings == null)
            {
                Debug.LogWarning("[BoostOpsAnalyticsContract] No project settings available for source store ID");
                return "";
            }

#if UNITY_IOS
            // Use Apple App Store ID for iOS
            return projectSettings.AppleAppStoreId ?? "";
#elif UNITY_ANDROID
            // Use Android package name for Android
            return projectSettings.AndroidPackageName ?? "";
#elif UNITY_WSA || UNITY_WINRT
            // Use Microsoft Store ID for Windows/UWP
            return projectSettings.MicrosoftStoreId ?? "";
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            // For standalone builds, try to determine the most appropriate ID
            // Default to Apple App Store ID on macOS, Microsoft Store ID on Windows
#if UNITY_STANDALONE_OSX
            return projectSettings.AppleAppStoreId ?? "";
#elif UNITY_STANDALONE_WIN
            return projectSettings.MicrosoftStoreId ?? "";
#else
            return projectSettings.AppleAppStoreId ?? ""; // Linux fallback
#endif
#else
            // Fallback: try Apple App Store ID first, then others
            if (!string.IsNullOrEmpty(projectSettings.AppleAppStoreId))
                return projectSettings.AppleAppStoreId;
            if (!string.IsNullOrEmpty(projectSettings.AndroidPackageName))
                return projectSettings.AndroidPackageName;
            if (!string.IsNullOrEmpty(projectSettings.MicrosoftStoreId))
                return projectSettings.MicrosoftStoreId;
            if (!string.IsNullOrEmpty(projectSettings.AmazonStoreId))
                return projectSettings.AmazonStoreId;
            if (!string.IsNullOrEmpty(projectSettings.SamsungStoreId))
                return projectSettings.SamsungStoreId;
            return "";
#endif
        }

        /// <summary>
        /// Get the target project ID (BoostOps project key) from campaign
        /// </summary>
        public static string GetTargetProjectId(Campaign campaign)
        {
            if (campaign?.target_project == null)
            {
                Debug.LogWarning("[GetTargetProjectId] No target project in campaign");
                return "";
            }
            
            return campaign.target_project.project_id ?? "";
        }
        
        /// <summary>
        /// Get target app store ID in the correct format for the current platform
        /// Uses new structured store_ids format for direct access (more efficient than URL parsing)
        /// </summary>
        /// <param name="campaign">Campaign containing target app information</param>
        /// <returns>Platform-appropriate store ID (iOS: numeric App Store ID, Android/Amazon/Samsung: package name, Windows: Store ID)</returns>
        public static string GetTargetStoreId(Campaign campaign)
        {
            if (campaign?.target_project?.store_ids == null)
            {
                Debug.LogWarning("[GetTargetStoreId] No store IDs available in campaign - falling back to URL parsing");
                return GetTargetStoreIdFromUrls(campaign); // Fallback to old method
            }
            
            var storeIds = campaign.target_project.store_ids;
            
#if UNITY_IOS
            // Use iOS App Store ID (numeric) - direct access
            if (!string.IsNullOrEmpty(storeIds.apple))
            {
                return storeIds.apple;
            }
            Debug.LogWarning("[GetTargetStoreId] No Apple store ID available for iOS platform");
            return "";
            
#elif UNITY_ANDROID
            // First priority: Check if we're in a specific Android store environment
            var projectSettings = InternalSettingsCache.GetProjectSettings();
            
            // Amazon Appstore detection - direct store ID access
            if (!string.IsNullOrEmpty(storeIds.amazon) || 
                (!string.IsNullOrEmpty(projectSettings?.AmazonStoreId) && IsAmazonDevice()))
            {
                if (!string.IsNullOrEmpty(storeIds.amazon))
                {
                    return storeIds.amazon;
                }
                Debug.LogWarning("[GetTargetStoreId] Amazon environment detected but no Amazon store ID available");
            }
            
            // Samsung Galaxy Store detection - direct store ID access  
            if (!string.IsNullOrEmpty(storeIds.samsung) && IsSamsungDevice())
            {
                return storeIds.samsung;
            }
            
            // Default: Google Play Store - direct store ID access
            if (!string.IsNullOrEmpty(storeIds.google))
            {
                return storeIds.google;
            }
            
            Debug.LogWarning("[GetTargetStoreId] No valid Google Play store ID available for Android platform");
            return "";
            
#elif UNITY_WSA || UNITY_WINRT
            // Use Microsoft Store ID - direct access
            if (!string.IsNullOrEmpty(storeIds.microsoft))
            {
                return storeIds.microsoft;
            }
            Debug.LogWarning("[GetTargetStoreId] No Windows store ID available for Windows platform");
            return "";
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            // For standalone builds, try platform-appropriate store IDs
#if UNITY_STANDALONE_OSX
            if (!string.IsNullOrEmpty(storeIds.apple))
            {
                return storeIds.apple;
            }
#elif UNITY_STANDALONE_WIN  
            if (!string.IsNullOrEmpty(storeIds.microsoft))
            {
                return storeIds.microsoft;
            }
#endif
            // Try any available store ID as fallback
            if (!string.IsNullOrEmpty(storeIds.apple))
                return storeIds.apple;
            if (!string.IsNullOrEmpty(storeIds.google))
                return storeIds.google;
            if (!string.IsNullOrEmpty(storeIds.microsoft))
                return storeIds.microsoft;
            if (!string.IsNullOrEmpty(storeIds.amazon))
                return storeIds.amazon;
            if (!string.IsNullOrEmpty(storeIds.samsung))
                return storeIds.samsung;
            
            Debug.LogWarning("[GetTargetStoreId] No valid store IDs available for standalone platform");
            return "";
            
#else
            // Ultimate fallback: try store IDs in priority order
            if (!string.IsNullOrEmpty(storeIds.apple))
            {
                var appleId = storeIds.apple;
                Debug.Log($"[GetTargetStoreId] Using Apple Store ID (fallback): '{appleId}'");
                return appleId;
            }
            if (!string.IsNullOrEmpty(storeIds.google))
            {
                var googleId = storeIds.google;  
                Debug.Log($"[GetTargetStoreId] Using Google Play Store ID (fallback): '{googleId}'");
                return googleId;
            }
            if (!string.IsNullOrEmpty(storeIds.microsoft))
            {
                var windowsId = storeIds.microsoft;
                Debug.Log($"[GetTargetStoreId] Using Microsoft Store ID (fallback): '{windowsId}'");
                return windowsId;
            }
            if (!string.IsNullOrEmpty(storeIds.amazon))
            {
                var amazonId = storeIds.amazon;
                Debug.Log($"[GetTargetStoreId] Using Amazon Store ID (fallback): '{amazonId}'");
                return amazonId;
            }
            if (!string.IsNullOrEmpty(storeIds.samsung))
            {
                var samsungId = storeIds.samsung;
                Debug.Log($"[GetTargetStoreId] Using Samsung Store ID (fallback): '{samsungId}'");
                return samsungId;
            }
            
            Debug.LogWarning("[GetTargetStoreId] No valid store IDs available for any platform");
            return "";
#endif
        }
        
        /// <summary>
        /// Fallback method that uses old URL parsing approach when store_ids are not available
        /// </summary>
        private static string GetTargetStoreIdFromUrls(Campaign campaign)
        {
            if (campaign?.target_project?.store_urls == null)
            {
                Debug.LogWarning("[GetTargetStoreIdFromUrls] No store URLs available in campaign");
                return "";
            }
            
            var storeUrls = campaign.target_project.store_urls;
            
#if UNITY_IOS
            // Parse iOS App Store ID from URL
            var iosUrl = storeUrls.apple;
            if (!string.IsNullOrEmpty(iosUrl))
            {
                var result = iosUrl.Split('/').LastOrDefault()?.Replace("id", "") ?? "";
                Debug.Log($"[GetTargetStoreIdFromUrls] Extracted iOS ID: '{result}'");
                return result;
            }
            return "";
            
#elif UNITY_ANDROID
            // Parse Android package name from Google Play URL
            var googleUrl = storeUrls.google;
            if (!string.IsNullOrEmpty(googleUrl))
            {
                var packageName = ExtractPackageNameFromUrl(googleUrl);
                if (!string.IsNullOrEmpty(packageName))
                {
                    Debug.Log($"[GetTargetStoreIdFromUrls] Extracted Google Play package: '{packageName}'");
                    return packageName;
                }
            }
            return "";
            
#elif UNITY_WSA || UNITY_WINRT || UNITY_STANDALONE_WIN
            // Parse Microsoft Store ID from URL
            var windowsUrl = storeUrls.microsoft;
            if (!string.IsNullOrEmpty(windowsUrl))
            {
                var storeId = ExtractStoreIdFromUrl(windowsUrl);
                if (!string.IsNullOrEmpty(storeId))
                {
                    Debug.Log($"[GetTargetStoreIdFromUrls] Extracted Microsoft Store ID: '{storeId}'");
                    return storeId;
                }
            }
            return "";
            
#else
            // Try platform-appropriate fallbacks for standalone builds
            // Prioritize based on the current platform
            if (!string.IsNullOrEmpty(storeUrls.apple))
            {
                var iosId = storeUrls.apple.Split('/').LastOrDefault()?.Replace("id", "") ?? "";
                if (!string.IsNullOrEmpty(iosId))
                {
                    Debug.Log($"[GetTargetStoreIdFromUrls] Extracted Apple Store ID: '{iosId}'");
                    return iosId;
                }
            }
            if (!string.IsNullOrEmpty(storeUrls.google))
            {
                var packageName = ExtractPackageNameFromUrl(storeUrls.google);
                if (!string.IsNullOrEmpty(packageName))
                {
                    Debug.Log($"[GetTargetStoreIdFromUrls] Extracted Google Play package: '{packageName}'");
                    return packageName;
                }
            }
            if (!string.IsNullOrEmpty(storeUrls.microsoft))
            {
                var winId = ExtractStoreIdFromUrl(storeUrls.microsoft);
                if (!string.IsNullOrEmpty(winId))
                {
                    Debug.Log($"[GetTargetStoreIdFromUrls] Extracted Microsoft Store ID: '{winId}'");
                    return winId;
                }
            }
            if (!string.IsNullOrEmpty(storeUrls.amazon))
            {
                var amazonId = ExtractPackageNameFromUrl(storeUrls.amazon);
                if (!string.IsNullOrEmpty(amazonId))
                {
                    Debug.Log($"[GetTargetStoreIdFromUrls] Extracted Amazon Store ID: '{amazonId}'");
                    return amazonId;
                }
            }
            
            Debug.LogWarning("[GetTargetStoreIdFromUrls] No valid store URLs found for any platform");
            return "";
#endif
        }
        
        /// <summary>
        /// Extract package name from various Android store URL formats
        /// </summary>
        private static string ExtractPackageNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            
            try
            {
                // Google Play: https://play.google.com/store/apps/details?id=com.package.name
                if (url.Contains("play.google.com") && url.Contains("id="))
                {
                    var packageName = url.Split("id=").LastOrDefault()?.Split('&').FirstOrDefault();
                    if (!string.IsNullOrEmpty(packageName)) return packageName;
                }
                
                // Amazon: https://www.amazon.com/dp/B[ASIN] - more complex, may need different approach
                // For now, try to extract any package-like identifier
                if (url.Contains("amazon.com"))
                {
                    // This may need more sophisticated parsing depending on Amazon URL format
                    // For now, return empty and log warning
                    Debug.LogWarning($"[ExtractPackageNameFromUrl] Amazon URL format not fully supported: {url}");
                    return "";
                }
                
                // Samsung: Similar to Google Play format
                if (url.Contains("galaxystore.samsung.com") && url.Contains("id="))
                {
                    var packageName = url.Split("id=").LastOrDefault()?.Split('&').FirstOrDefault();
                    if (!string.IsNullOrEmpty(packageName)) return packageName;
                }
                
                // Generic fallback: look for id= parameter
                if (url.Contains("id="))
                {
                    var packageName = url.Split("id=").LastOrDefault()?.Split('&').FirstOrDefault();
                    if (!string.IsNullOrEmpty(packageName)) return packageName;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ExtractPackageNameFromUrl] Error parsing URL '{url}': {ex.Message}");
            }
            
            return "";
        }
        
        /// <summary>
        /// Extract store ID from Windows Store URLs
        /// </summary>
        private static string ExtractStoreIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            
            try
            {
                // Windows Store: https://www.microsoft.com/store/apps/[app-name]/[id]
                // or: https://apps.microsoft.com/store/detail/[app-name]/[id]
                if (url.Contains("microsoft.com") || url.Contains("apps.microsoft.com"))
                {
                    var segments = url.Split('/');
                    // Usually the ID is the last segment
                    var potentialId = segments.LastOrDefault();
                    if (!string.IsNullOrEmpty(potentialId) && potentialId.Length > 5)
                    {
                        return potentialId;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ExtractStoreIdFromUrl] Error parsing Windows Store URL '{url}': {ex.Message}");
            }
            
            return "";
        }
        
        /// <summary>
        /// Check if running on Amazon device (Fire TV, Fire Tablet, etc.)
        /// </summary>
        private static bool IsAmazonDevice()
        {
            try
            {
                // Check device manufacturer or model for Amazon indicators
                return SystemInfo.deviceModel?.ToLower().Contains("amazon") == true ||
                       SystemInfo.deviceModel?.ToLower().Contains("fire") == true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Check if running on Samsung device
        /// </summary>
        private static bool IsSamsungDevice()
        {
            try
            {
                return SystemInfo.deviceModel?.ToLower().Contains("samsung") == true ||
                       SystemInfo.deviceModel?.ToLower().Contains("galaxy") == true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Get current app's store ID in the correct format
        /// </summary>
        public static string GetCurrentAppStoreId()
        {
            var settings = InternalSettingsCache.GetProjectSettings();
            
#if UNITY_IOS
            // Return iOS App Store ID (numeric)
            return settings?.AppleAppStoreId ?? "";
#elif UNITY_ANDROID
            // Return Android package name
            return Application.identifier;
#else
            // Fallback: prefer iOS Store ID if available
            if (!string.IsNullOrEmpty(settings?.AppleAppStoreId))
                return settings.AppleAppStoreId;
            
            return Application.identifier;
#endif
        }
        
        #endregion
        
        #region Campaign Convenience Methods
        
        // Legacy campaign convenience methods removed - use TrackImpression(), TrackClick(), TrackAppOpen() with new schema instead
        
        // TrackBoostLinkOpen removed - use TrackAppOpen() with deeplinkUrl parameter instead
        
        /// <summary>
        /// Get creative ID from campaign (helper method)
        /// </summary>
        private static string GetCreativeId(Campaign campaign)
        {
            return campaign?.target_project?.creatives?.FirstOrDefault()?.creative_id;
        }
        
        #endregion
        
        #region Backend-Specific Implementations
        
        // Note: Analytics provider methods moved to separate provider classes
        // - FirebaseAnalyticsProvider: Handles Firebase Analytics integration
        // - UnityAnalyticsProvider: Handles Unity Analytics integration  
        // - BoostOpsAnalyticsProvider: Handles BoostOps backend communication
        // Use AnalyticsProviderFactory to access providers
        
        /// <summary>
        /// Get store identifier for analytics (using standardized names for backend consistency)
        /// </summary>
        public static string GetStoreIdentifier()
        {
            var detectedStore = BoostOpsStoreDetector.GetCurrentStore();
            
            // Use consistent naming for backend analytics
            switch (detectedStore)
            {
                case BoostOpsStoreDetector.AppStore.GooglePlay:
                    return "google";
                case BoostOpsStoreDetector.AppStore.Amazon:
                    return "amazon";
                case BoostOpsStoreDetector.AppStore.Samsung:
                    return "samsung";
                case BoostOpsStoreDetector.AppStore.Huawei:
                    return "huawei";
                case BoostOpsStoreDetector.AppStore.iOS:
                    return "ios";
                case BoostOpsStoreDetector.AppStore.macOS:
                    return "macos";
                case BoostOpsStoreDetector.AppStore.WindowsStore:
                    return "windows";
                case BoostOpsStoreDetector.AppStore.Sideloaded:
                    return "sideloaded";
                default:
                    return "unknown";
            }
        }

        
        #endregion
    }
} 