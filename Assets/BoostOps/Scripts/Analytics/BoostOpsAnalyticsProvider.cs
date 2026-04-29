using System.Collections.Generic;
using UnityEngine;
using BoostOps.Internal;
using BoostOps.Analytics;

namespace BoostOps
{
    /// <summary>
    /// BoostOps Analytics provider implementation
    /// Handles sending analytics events to the BoostOps backend
    /// </summary>
    public class BoostOpsAnalyticsProvider : IAnalyticsProvider
    {
        // Cache analytics config to avoid repeated remote calls
        private static AnalyticsConfig _cachedAnalyticsConfig;
        private static float _lastConfigFetchTime;
        private static bool _configInvalidated = false;
        private const float CONFIG_CACHE_DURATION = 300f; // Cache for 5 minutes (improved from 30s)
        private static bool _isInitialized = false;
        
        // ✅ HARDCODED DEFAULT ENDPOINT (Industry Standard - Fail-Open)
        // This ensures SDK always works, even without remote config
        // Server can override via app_open response
        private const string DEFAULT_ANALYTICS_ENDPOINT = "https://analytics.boostops.io/v1/events";
        
        // ✅ RUNTIME KILL SWITCH (Server Control)
        // Set by server response (app_open) to disable/enable analytics
        // Fail-open: defaults to true (enabled) if server never responds
        private static bool? _serverAnalyticsEnabled = null; // null = not set yet (fail-open)
        private static int[] _serverAcceptedSchemas = null; // null = not set yet (fail-open)
        
        public string ProviderName => "BoostOps Analytics";

        /// <summary>
        /// Check if BoostOps Analytics is available and configured
        /// 
        /// FAIL-OPEN APPROACH WITH SERVER KILL SWITCH:
        /// - Analytics enabled by default (fail-open)
        /// - Server can disable via app_open response (kill switch)
        /// - Respects server commands when available
        /// - Falls back to enabled if server never responds
        /// </summary>
        public bool IsAvailable 
        { 
            get 
            {
                // Check project key
                var settings = InternalSettingsCache.GetProjectSettings();
                if (settings == null || string.IsNullOrEmpty(settings.ProjectKey))
                {
                    BoostOpsLogger.LogDebug("Analytics", "No project key configured - analytics disabled");
                    return false;
                }
                    
                // Check if we're in local mode - disable analytics upload
                if (!settings.UseRemoteManagement)
                {
                    BoostOpsLogger.LogDebug("Analytics", "Local mode detected via project settings - analytics upload disabled");
                    return false;
                }
                
                // ALSO check runtime local mode state (in case localOnlyMode was set programmatically)
                try
                {
                    if (BoostOps.Internal.BoostOpsSDKInternal.IsSDKLocalMode)
                    {
                        BoostOpsLogger.LogDebug("Analytics", "Runtime local mode detected - analytics upload disabled");
                        return false;
                    }
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogDebug("Analytics", $"Could not check runtime local mode state: {ex.Message}");
                    // Continue - not fatal
                }
                
                // ✅ CHECK SERVER KILL SWITCH (if server has responded)
                if (_serverAnalyticsEnabled.HasValue && !_serverAnalyticsEnabled.Value)
                {
                    BoostOpsLogger.LogDebug("Analytics", "⚠️ Analytics disabled by server - respecting kill switch");
                    return false;
                }
                
                // ✅ FAIL-OPEN: Analytics is available!
                // If server hasn't responded yet (_serverAnalyticsEnabled == null) → enabled (fail-open)
                // If server says enabled → enabled
                // Server can override via app_open response (kill switch, schema validation, etc.)
                return true;
            }
        }

        public void Initialize()
        {
            if (!_isInitialized)
            {
                // BoostOpsLogger.LogDebug("Analytics", "BoostOps Analytics provider initialized (client will be initialized lazily when needed)");
                _isInitialized = true;
            }
        }
        
        /// <summary>
        /// Apply server config from app_open response
        /// This allows the server to:
        /// - Disable analytics via kill switch (disabled: true)
        /// - Set accepted schema versions
        /// - Override endpoint
        /// 
        /// SAFETY: Uses "disabled" field (opt-in to disable)
        /// - disabled: false or missing → analytics ENABLED ✅
        /// - disabled: true → analytics DISABLED (kill switch) ❌
        /// </summary>
        public static void ApplyServerConfig(bool disabled, int[] acceptedSchemas = null, string endpoint = null)
        {
            // ✅ SAFE: disabled=true means kill switch ON, disabled=false means enabled
            _serverAnalyticsEnabled = !disabled;  // Invert: disabled → not enabled
            _serverAcceptedSchemas = acceptedSchemas;
            
            if (disabled)
            {
                BoostOpsLogger.LogWarning("Analytics", "⚠️ Analytics DISABLED by server (kill switch activated)");
            }
            else
            {
                // BoostOpsLogger.LogDebug("Analytics", $"✅ Analytics ENABLED by server (schemas: {string.Join(", ", acceptedSchemas ?? new int[0])})");
            }
            
            // Update endpoint if provided
            if (!string.IsNullOrEmpty(endpoint))
            {
                // TODO: Update analytics client endpoint if it changes
                BoostOpsLogger.LogDebug("Analytics", $"ℹ️ Server endpoint override: {endpoint}");
            }
        }
        
        /// <summary>
        /// Check if a schema version is accepted by the server
        /// Fail-open: if server hasn't responded, accept all schemas
        /// </summary>
        public static bool IsSchemaAccepted(int schemaMajorVersion)
        {
            // Fail-open: if server hasn't set schemas, accept all
            if (_serverAcceptedSchemas == null || _serverAcceptedSchemas.Length == 0)
            {
                return true;
            }
            
            // Check if schema is in accepted list
            foreach (var accepted in _serverAcceptedSchemas)
            {
                if (accepted == schemaMajorVersion)
                {
                    return true;
                }
            }
            
            BoostOpsLogger.LogDebug("Analytics", $"⚠️ Schema v{schemaMajorVersion} not accepted by server (accepted: {string.Join(", ", _serverAcceptedSchemas)})");
            return false;
        }
        
        /// <summary>
        /// Ensure the BoostOps Analytics Client is initialized and ready to send events
        /// This should be called before flushing the queue to ensure events can be sent
        /// </summary>
        public void EnsureInitialized()
        {
            EnsureClientInitialized();
        }

        /// <summary>
        /// Ensure the BoostOps Analytics Client is properly configured for non-install events
        /// Handles the case where install events may have already initialized the client with hardcoded endpoint
        /// </summary>
        private void EnsureClientInitialized()
        {
            try
            {
                // Get project settings for project key
                var settings = InternalSettingsCache.GetProjectSettings();
                if (settings == null || string.IsNullOrEmpty(settings.ProjectKey))
                {
                    BoostOpsLogger.LogDebug("Analytics", "Cannot initialize BoostOps Analytics client - no project key available");
                    return;
                }

                // ✅ FAIL-OPEN: Always use default endpoint
                // Remote config can override later via app_open response
                string endpointUrl = DEFAULT_ANALYTICS_ENDPOINT;
                
                // Try to get endpoint from remote config (optional override)
                var analyticsConfig = GetAnalyticsConfigFromRemote();
                if (analyticsConfig != null && !string.IsNullOrEmpty(analyticsConfig.endpoint))
                {
                    endpointUrl = analyticsConfig.endpoint;
                    // BoostOpsLogger.LogDebug("Analytics", $"✅ Using remote config endpoint: {endpointUrl}");
                }
                else
                {
                    // BoostOpsLogger.LogDebug("Analytics", $"ℹ️ Using default endpoint (remote config unavailable): {endpointUrl}");
                }

                // Check if client is already initialized
                if (BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.IsInitialized)
                {
                    // Already initialized - skip redundant initialization
                    return;
                }

                // Initialize the BoostOps Analytics Client with endpoint (from remote config or default)
                // Enable debug mode to see full server JSON responses
                BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.Initialize(
                    settings.ProjectKey,
                    endpointUrl,  // Uses remote config endpoint or default editor endpoint
                    isDevelopmentMode: true  // TEMPORARY: Enable to debug server response
                );

                // NOTE: Accepted schema versions are now only read from event responses (not remote config)
                // This ensures we always respect the server's real-time requirements
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to initialize BoostOps Analytics client: {ex.Message}");
            }
        }

        public void TrackImpression(string eventName, Dictionary<string, string> parameters)
        {
            if (!IsAvailable) return;
            
            // Ensure the analytics client is initialized before sending events
            EnsureClientInitialized();
            
            // Convert generic parameters back to BoostOps-specific format
            var campaignSlug = parameters.TryGetValue("campaign_slug", out var cs) ? cs : "";
            var placement = parameters.TryGetValue("placement", out var pl) ? pl : "";
            var format = parameters.TryGetValue("format", out var fmt) ? fmt : "banner";
            var sourceStoreId = parameters.TryGetValue("source_store_id", out var ssi) ? ssi : "";
            // Note: source_project_id removed - server derives from project_key
            var targetStoreId = parameters.TryGetValue("target_store_id", out var tsi) ? tsi : "";
            var targetProjectId = parameters.TryGetValue("target_project_id", out var tpi) ? tpi : "";
            var networkCampaignId = parameters.TryGetValue("network_campaign_id", out var nci) ? nci : "";
            var channel = parameters.TryGetValue("channel", out var ch) ? ch : "";
            
            int? durationMs = null;
            if (parameters.TryGetValue("duration_ms", out var durationStr) && int.TryParse(durationStr, out var duration))
                durationMs = duration;
                
            float? revenueShareRate = null;
            if (parameters.TryGetValue("revenue_share_rate", out var revenueStr) && float.TryParse(revenueStr, out var revenue))
                revenueShareRate = revenue;
            
            // Use the proper BoostOps event builder and client
            // Note: source_store_id is in context.store_id (universal) - not passed here
            var eventData = BoostOps.Analytics.BoostOpsEventBuilder.CreateImpressionEvent(
                campaignSlug, placement, format, durationMs,
                targetStoreId, targetProjectId,
                networkCampaignId, revenueShareRate, null, channel
            );
            
            // Always queue the event first
            BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.QueueEvent(eventData);
            
            // If analytics is available, flush immediately. Otherwise it will be flushed when remote config loads.
            if (IsAvailable)
            {
                // Analytics ready - flush the queue immediately to ensure prompt delivery
                BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.FlushQueue((success) => {
                    if (!success) {
                        BoostOpsLogger.LogWarning("Analytics", $"⚠️ Impression event flush failed");
                    }
                });
            }
            else
            {
            }
        }

        public void TrackClick(string eventName, Dictionary<string, string> parameters)
        {
            if (!IsAvailable) return;
            
            // Ensure the analytics client is initialized before sending events
            EnsureClientInitialized();
            
            // Convert generic parameters back to BoostOps-specific format
            var campaignSlug = parameters.TryGetValue("campaign_slug", out var cs2) ? cs2 : "";
            var placement = parameters.TryGetValue("placement", out var pl2) ? pl2 : "";
            var sourceStoreId = parameters.TryGetValue("source_store_id", out var ssi) ? ssi : "";
            // Note: source_project_id removed - server derives from project_key
            var targetStoreId = parameters.TryGetValue("target_store_id", out var tsi) ? tsi : "";
            var targetProjectId = parameters.TryGetValue("target_project_id", out var tpi) ? tpi : "";
            var networkCampaignId = parameters.TryGetValue("network_campaign_id", out var nci2) ? nci2 : "";
            var deepLinkUrl = parameters.TryGetValue("deep_link_url", out var dlu) ? dlu : "";
            var format = parameters.TryGetValue("format", out var fmt2) ? fmt2 : "";
            var channel = parameters.TryGetValue("channel", out var ch2) ? ch2 : "";
            
            // Extract impression linking parameters (CRITICAL for attribution)
            var impressionId = parameters.TryGetValue("impression_id", out var impId) ? impId : null;
            long? impressionTimestamp = null;
            if (parameters.TryGetValue("impression_timestamp", out var impTsStr) && long.TryParse(impTsStr, out var impTs))
                impressionTimestamp = impTs;
            
            // Extract container impression ID (for app walls)
            var containerImpressionId = parameters.TryGetValue("container_impression_id", out var contId) ? contId : null;
            
            // Debug log for impression linkage
            if (!string.IsNullOrEmpty(impressionId))
            {
                string containerInfo = !string.IsNullOrEmpty(containerImpressionId) ? $", container: {containerImpressionId}" : "";
                BoostOpsLogger.LogDebug("Analytics", $"🔗 Click linked to impression: {impressionId}{containerInfo}");
            }
            else
            {
                BoostOpsLogger.LogWarning("Analytics", $"⚠️ Click WITHOUT impression_id (campaign: {campaignSlug}, placement: {placement})");
            }
            
            int? clickX = null;
            if (parameters.TryGetValue("click_x", out var clickXStr) && int.TryParse(clickXStr, out var x))
                clickX = x;
                
            int? clickY = null;
            if (parameters.TryGetValue("click_y", out var clickYStr) && int.TryParse(clickYStr, out var y))
                clickY = y;
                
            int? timeToClickMs = null;
            if (parameters.TryGetValue("time_to_click_ms", out var timeStr) && int.TryParse(timeStr, out var time))
                timeToClickMs = time;
            
            // Use the proper BoostOps event builder and client
            var clickCoords = (clickX.HasValue && clickY.HasValue) ? 
                new BoostOps.Analytics.ClickCoordinates { x = clickX.Value, y = clickY.Value } : null;
            
            // Note: source_store_id is in context.store_id (universal) - not passed here
            var eventData = BoostOps.Analytics.BoostOpsEventBuilder.CreateClickEvent(
                campaignSlug, placement, clickCoords, timeToClickMs,
                targetStoreId, targetProjectId,
                networkCampaignId, deepLinkUrl, null, null, null, format, channel,
                impressionId, impressionTimestamp, // CRITICAL: Link click back to impression for attribution
                containerImpressionId // Link to app wall container (if applicable)
            );
            
            // Always queue the event first
            BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.QueueEvent(eventData);
            
            // If analytics is available, flush immediately. Otherwise it will be flushed when remote config loads.
            if (IsAvailable)
            {
                // Analytics ready - flush the queue immediately to ensure prompt delivery
                BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.FlushQueue((success) => {
                    if (!success) {
                        BoostOpsLogger.LogWarning("Analytics", $"⚠️ Click event flush failed");
                    }
                });
            }
            else
            {
            }
        }

        public void TrackInstall(string eventName, Dictionary<string, string> parameters)
        {
            if (!IsAvailable) return;
            
            // Ensure the analytics client is initialized before sending events
            EnsureClientInitialized();
            
            // Parse install parameters
            bool? organic = null;
            if (parameters.TryGetValue("organic", out var organicStr) && bool.TryParse(organicStr, out var org))
                organic = org;
                
            bool? reinstall = null;
            if (parameters.TryGetValue("reinstall", out var reinstallStr) && bool.TryParse(reinstallStr, out var rei))
                reinstall = rei;
            
            // Use app open event builder with first session flag (industry standard)
            var eventData = BoostOps.Analytics.BoostOpsEventBuilder.CreateAppOpenEvent(
                launchType: "cold",
                deeplinkUrl: null,
                timeSinceInstallMs: null,
                isFirstSession: true,
                organic: organic,
                reinstall: reinstall
            );
            
            // Queue the event (this method is only called by provider system, not for critical installs)
            BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.QueueEvent(eventData);
            
        }

        // TrackPurchase removed in SDK 1.1.0. Purchases now flow through
        // BoostOps.Analytics.BoostOpsPurchaseClient → POST /v1/purchases.
        // BoostOpsAnalyticsContract.TrackPurchase is the single entry point.

        public void TrackEvent(string eventName, Dictionary<string, string> parameters)
        {
            if (!IsAvailable) return;
            
            // Ensure the analytics client is initialized before sending events
            EnsureClientInitialized();
            
            AnalyticsEventData eventData;
            
            // Handle specific event types with dedicated builders
            if (eventName == "boostops_open")
            {
                // Use dedicated app open event builder
                var launchType = parameters.TryGetValue("launch_type", out var lt) ? lt : "cold";
                var deeplinkUrl = parameters.TryGetValue("deep_link_url", out var du) ? du : null;
                
                long? timeSinceInstallMs = null;
                if (parameters.TryGetValue("time_since_install_ms", out var timeStr) && long.TryParse(timeStr, out var time))
                    timeSinceInstallMs = time;
                
                eventData = BoostOps.Analytics.BoostOpsEventBuilder.CreateAppOpenEvent(launchType, deeplinkUrl, timeSinceInstallMs);
            }
            else
            {
                // Use generic event builder for other events
                eventData = BoostOps.Analytics.BoostOpsEventBuilder.CreateEvent(eventName);
                
                // Add additional parameters to the event data (for generic events)
                // Note: The event builder already handles core identifiers automatically
            }
            
            // If analytics is available, send immediately. Otherwise queue for later.
            if (IsAvailable)
            {
                // Analytics ready - send immediately
                BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.QueueEvent(eventData);
                BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.FlushQueue();
            }
            else
            {
                // Analytics not ready - queue for when remote config loads
                BoostOps.Analytics.BoostOpsAnalyticsClient.Instance.QueueEvent(eventData);
            }
        }

        /// <summary>
        /// Invalidate analytics config cache (call when remote config updates)
        /// </summary>
        public static void InvalidateConfigCache()
        {
            _configInvalidated = true;
            _cachedAnalyticsConfig = null;
            BoostOpsLogger.LogDebug("Analytics", "Analytics config cache invalidated");
        }
        
        /// <summary>
        /// Get analytics configuration from remote config (cached for 5 minutes)
        /// Returns null if no remote config available (fail-closed)
        /// </summary>
        private AnalyticsConfig GetAnalyticsConfigFromRemote()
        {
            try
            {
                // Check cache first
                if (!_configInvalidated && 
                    _cachedAnalyticsConfig != null && 
                    Time.time - _lastConfigFetchTime < CONFIG_CACHE_DURATION)
                {
                    return _cachedAnalyticsConfig;
                }
                
                // Fetch fresh config
                _configInvalidated = false;
                var analyticsConfig = BoostOps.Internal.BoostOpsManagerInternal.GetAnalyticsConfig();
                
                if (analyticsConfig != null)
                {
                    _cachedAnalyticsConfig = analyticsConfig;
                    _lastConfigFetchTime = Time.time;
                    
#if BOOSTOPS_DEBUG_LOGGING
                    BoostOpsLogger.LogDebug("Analytics", $"Fetched analytics config - enabled: {analyticsConfig.enabled}");
#endif
                }
                
                return analyticsConfig;
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to get remote analytics config: {ex.Message}");
                return null; // Fail-closed on any error
            }
        }
        
        /// <summary>
        /// Send analytics event to BoostOps backend with mixed parameter types
        /// </summary>
        /// <param name="endpoint">Analytics endpoint (e.g., "impression", "click", "install")</param>
        /// <param name="parameters">Event parameters (mixed types)</param>
        private void SendToBoostOpsBackend(string endpoint, Dictionary<string, object> parameters)
        {
            try
            {
                // TODO: Implement actual HTTP request to BoostOps backend
                // This should make a POST request to: {BOOSTOPS_BACKEND_URL}/events/{endpoint}
                // with the parameters as JSON payload
                
                BoostOpsLogger.LogDebug("Analytics", $"BoostOps Backend -> /events/{endpoint} with {parameters.Count} parameters (mixed types)");
                
                // Log parameters for debugging (including store info)
                
                // Log store detection for debugging
                
                // Future implementation:
                // 1. Get BoostOps backend URL from settings
                // 2. Serialize parameters to JSON (handles mixed types properly)
                // 3. Make authenticated HTTP POST request
                // 4. Handle response and retry logic
                // 5. Queue events for offline scenarios
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"BoostOps backend error for {endpoint}: {ex.Message}");
                // Continue silently - don't crash the app for analytics failures
            }
        }

        /// <summary>
        /// Send analytics event to BoostOps backend
        /// </summary>
        /// <param name="endpoint">Analytics endpoint (e.g., "impression", "click", "install")</param>
        /// <param name="parameters">Event parameters</param>
        private void SendToBoostOpsBackend(string endpoint, Dictionary<string, string> parameters)
        {
            try
            {
                // TODO: Implement actual HTTP request to BoostOps backend
                // This should make a POST request to: {BOOSTOPS_BACKEND_URL}/events/{endpoint}
                // with the parameters as JSON payload
                
                BoostOpsLogger.LogDebug("Analytics", $"BoostOps Backend -> /events/{endpoint} with {parameters.Count} parameters");
                
                // Log parameters for debugging (including store info)
                
                // Log store detection for debugging
                
                // Future implementation:
                // 1. Get BoostOps backend URL from settings
                // 2. Serialize parameters to JSON
                // 3. Make authenticated HTTP POST request
                // 4. Handle response and retry logic
                // 5. Queue events for offline scenarios
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"BoostOps backend error for {endpoint}: {ex.Message}");
                // Continue silently - don't crash the app for analytics failures
            }
        }
    }
} 