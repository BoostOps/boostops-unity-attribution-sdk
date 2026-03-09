using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BoostOps.Core;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Builder utility for creating BoostOps analytics events using the new schema
    /// </summary>
    public static class BoostOpsEventBuilder
    {
        /// <summary>
        /// Create an event with comprehensive identifier set for maximum attribution coverage
        /// Identifiers are properly organized: top-level (universal), context (device-stable), event (install-time)
        /// </summary>
        /// <param name="eventType">Type of event (e.g., "boostops_open", "boostops_impression", "boostops_click", "boostops_purchase")</param>
        /// <param name="includeInstallTimeExtras">Include install-time only identifiers (ASA token, install referrer)</param>
        /// <param name="includeInstallTimestamp">Include app install timestamp (for first open events)</param>
        /// <returns>Complete AnalyticsEventData with all available identifiers in proper locations</returns>
        public static AnalyticsEventData CreateEvent(string eventType, bool includeInstallTimeExtras = false, bool includeInstallTimestamp = false)
        {
            // Get comprehensive identifier payload
            var identifiers = BoostOpsIdentifierManager.CreateIdentifierPayload(includeInstallTimeExtras);
            
            var eventData = new AnalyticsEventData
            {
                event_type = eventType,
                schema_version = 7,  // v7: Production release with install_time_ms for SDK migration detection
                timestamp_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                elapsed_realtime_ms = BoostOpsDeviceInfo.GetElapsedRealtimeMilliseconds(), // Monotonic clock (tamper-proof)
                event_id = System.Guid.NewGuid().ToString("N"),  // Unique event ID (never changes on retry) - database UNIQUE INDEX
                // Note: nonce is set in SendBatchCoroutine per attempt (NOT here)
                
                // Four-tier ID hierarchy (schema v6) - universal correlation identifiers
                boostops_id = GetIdentifierValue(identifiers, "boostops_id"),
                install_id = GetIdentifierValue(identifiers, "install_id"),
                custom_user_id = GetIdentifierValue(identifiers, "custom_user_id"),
                session_id = GetStoredSessionId(), // Use stored session ID for consistency within session
                // Note: project_key is sent in HTTP header (BoostOps-Project-Key) ONLY, not in payload
                // Note: storefront_country moved to context (environmental data)
                
                // TOP-LEVEL: Critical routing flags (for Cloudflare edge routing)
                is_unity_editor = Application.isEditor,
                is_debug_build = BoostOps.BoostOpsEnvironment.IsDebugBuild(),
                is_testflight = BoostOps.BoostOpsEnvironment.IsTestFlight(),
                is_emulator = BoostOps.BoostOpsEnvironment.IsEmulator(),
                
                // Privacy consent (top-level for compliance)
                consent = CreateConsentData(),
                
                context = CreateEventContext(identifiers),
                @event = CreateEventData(identifiers, includeInstallTimeExtras)
            };
            
            // Include app install timestamp for first open events (SDK migration detection)
            if (includeInstallTimestamp)
            {
                long installTimeSeconds = BoostOpsDeviceInfo.GetAppInstallTimestamp();
                if (installTimeSeconds > 0)
                {
                    eventData.install_time_ms = installTimeSeconds * 1000; // Convert to milliseconds for consistency
                    // Debug.Log($"[BoostOps] CreateEvent - install_time_ms set to: {eventData.install_time_ms} ({DateTimeOffset.FromUnixTimeSeconds(installTimeSeconds):yyyy-MM-dd HH:mm:ss} UTC)");
                }
            }
            
            // CRITICAL: Verify and recover install_id (essential for revenue attribution)
            // This is especially important on Android where timing issues can cause missing install_id
            if (string.IsNullOrEmpty(eventData.install_id))
            {
                Debug.LogWarning($"[BoostOps] ⚠️ CreateEvent({eventType}) - install_id was null/empty from identifiers, attempting direct fetch...");
                eventData.install_id = BoostOpsIdentifierManager.GetInstallId();
                if (string.IsNullOrEmpty(eventData.install_id))
                {
                    Debug.LogError($"[BoostOps] ❌ CreateEvent({eventType}) - FATAL: Could not get install_id! Event will be sent without it.");
                }
                else
                {
                    Debug.Log($"[BoostOps] ✅ CreateEvent({eventType}) - Recovered install_id: {eventData.install_id}");
                }
            }
            
            // Debug: Output consolidated event payload
            LogEventPayload(eventData);
            
            return eventData;
        }
        
        /// <summary>
        /// Get project key from BoostOps project settings (public key for authentication)
        /// </summary>
        private static string GetProjectKey()
        {
            try
            {
                var settings = BoostOps.Internal.InternalSettingsCache.GetProjectSettings();
                return settings?.ProjectKey ?? "";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to get project key: {e.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Helper to safely extract identifier value from dictionary
        /// </summary>
        private static string GetIdentifierValue(Dictionary<string, object> identifiers, string key)
        {
            if (identifiers.ContainsKey(key) && identifiers[key] != null)
            {
                return identifiers[key].ToString();
            }
            return null;
        }
        
        /// <summary>
        /// Create clean JSON representation of event data (excludes empty fields)
        /// </summary>
        private static string CreateCleanEventJson(AnalyticsEventData eventData)
        {
            var json = new System.Text.StringBuilder();
            json.AppendLine("{");
            
            // Top-level fields
            json.AppendLine($"  \"event_type\": \"{eventData.event_type}\",");
            json.AppendLine($"  \"schema_version\": {eventData.schema_version},");
            json.AppendLine($"  \"timestamp_ms\": {eventData.timestamp_ms},");
            json.AppendLine($"  \"event_id\": \"{eventData.event_id}\",");
            json.AppendLine($"  \"nonce\": \"{eventData.nonce}\",");
            
            if (!string.IsNullOrEmpty(eventData.boostops_id))
                json.AppendLine($"  \"boostops_id\": \"{eventData.boostops_id}\",");
            // Note: project_key is sent ONLY in HTTP header, not in payload
            if (!string.IsNullOrEmpty(eventData.session_id))
                json.AppendLine($"  \"session_id\": \"{eventData.session_id}\",");
            // Note: storefront_country moved to context (environmental data)
            
            // Consent (top-level for compliance)
            if (eventData.consent != null)
            {
                json.AppendLine("  \"consent\": {");
                var consentFields = new List<string>();
                
                if (!string.IsNullOrEmpty(eventData.consent.framework)) consentFields.Add($"    \"framework\": \"{eventData.consent.framework}\"");
                if (!string.IsNullOrEmpty(eventData.consent.consent_method)) consentFields.Add($"    \"consent_method\": \"{eventData.consent.consent_method}\"");
                if (!string.IsNullOrEmpty(eventData.consent.legal_basis)) consentFields.Add($"    \"legal_basis\": \"{eventData.consent.legal_basis}\"");
                if (!string.IsNullOrEmpty(eventData.consent.consent_source)) consentFields.Add($"    \"consent_source\": \"{eventData.consent.consent_source}\"");
                
                if (consentFields.Count > 0)
                {
                    json.AppendLine(string.Join(",\n", consentFields));
                }
                json.AppendLine("  },");
            }
            
            // Context (only non-empty fields)
            if (eventData.context != null)
            {
                json.AppendLine("  \"context\": {");
                var contextFields = new List<string>();
                
                if (!string.IsNullOrEmpty(eventData.context.source)) contextFields.Add($"    \"source\": \"{eventData.context.source}\"");
                if (!string.IsNullOrEmpty(eventData.context.platform)) contextFields.Add($"    \"platform\": \"{eventData.context.platform}\"");
                if (!string.IsNullOrEmpty(eventData.context.os_version)) contextFields.Add($"    \"os_version\": \"{eventData.context.os_version}\"");
                if (!string.IsNullOrEmpty(eventData.context.app_version)) contextFields.Add($"    \"app_version\": \"{eventData.context.app_version}\"");
                if (!string.IsNullOrEmpty(eventData.context.app_identifier)) contextFields.Add($"    \"app_identifier\": \"{eventData.context.app_identifier}\"");
                if (!string.IsNullOrEmpty(eventData.context.sdk_version)) contextFields.Add($"    \"sdk_version\": \"{eventData.context.sdk_version}\"");
                if (!string.IsNullOrEmpty(eventData.context.store)) contextFields.Add($"    \"store\": \"{eventData.context.store}\"");
                if (!string.IsNullOrEmpty(eventData.context.store_id)) contextFields.Add($"    \"store_id\": \"{eventData.context.store_id}\"");
                if (!string.IsNullOrEmpty(eventData.context.device_model)) contextFields.Add($"    \"device_model\": \"{eventData.context.device_model}\"");
                if (!string.IsNullOrEmpty(eventData.context.device_brand)) contextFields.Add($"    \"device_brand\": \"{eventData.context.device_brand}\"");
                if (!string.IsNullOrEmpty(eventData.context.country)) contextFields.Add($"    \"country\": \"{eventData.context.country}\"");
                if (!string.IsNullOrEmpty(eventData.context.storefront_country)) contextFields.Add($"    \"storefront_country\": \"{eventData.context.storefront_country}\"");
                if (eventData.context.timezone_offset_minutes.HasValue) contextFields.Add($"    \"timezone_offset_minutes\": {eventData.context.timezone_offset_minutes.Value}");
                if (!string.IsNullOrEmpty(eventData.context.locale)) contextFields.Add($"    \"locale\": \"{eventData.context.locale}\"");
                if (!string.IsNullOrEmpty(eventData.context.language)) contextFields.Add($"    \"language\": \"{eventData.context.language}\"");
                if (!string.IsNullOrEmpty(eventData.context.connection_type)) contextFields.Add($"    \"connection_type\": \"{eventData.context.connection_type}\"");
                // Note: timestamp is at top-level (milliseconds precision) - not duplicated in context
                
                // Device identifiers (only non-empty)
                // Note: install_id and custom_user_id moved to top-level (schema v6)
                if (!string.IsNullOrEmpty(eventData.context.app_account_token)) contextFields.Add($"    \"app_account_token\": \"{eventData.context.app_account_token}\"");
                if (!string.IsNullOrEmpty(eventData.context.idfv)) contextFields.Add($"    \"idfv\": \"{eventData.context.idfv}\"");
                if (!string.IsNullOrEmpty(eventData.context.idfa)) contextFields.Add($"    \"idfa\": \"{eventData.context.idfa}\"");
                if (!string.IsNullOrEmpty(eventData.context.asid_sha256)) contextFields.Add($"    \"asid_sha256\": \"{eventData.context.asid_sha256}\"");
                if (!string.IsNullOrEmpty(eventData.context.gaid)) contextFields.Add($"    \"gaid\": \"{eventData.context.gaid}\"");
                if (!string.IsNullOrEmpty(eventData.context.firebase_app_id)) contextFields.Add($"    \"firebase_app_id\": \"{eventData.context.firebase_app_id}\"");
                if (!string.IsNullOrEmpty(eventData.context.windows_device_id)) contextFields.Add($"    \"windows_device_id\": \"{eventData.context.windows_device_id}\"");
                if (!string.IsNullOrEmpty(eventData.context.windows_machine_guid)) contextFields.Add($"    \"windows_machine_guid\": \"{eventData.context.windows_machine_guid}\"");
                if (!string.IsNullOrEmpty(eventData.context.msaid)) contextFields.Add($"    \"msaid\": \"{eventData.context.msaid}\"");
                
                // Join context fields
                if (contextFields.Count > 0)
                {
                    json.AppendLine(string.Join(",\n", contextFields));
                }
                json.AppendLine("  },");
            }
            
            // Event data (only non-empty fields)
            if (eventData.@event != null)
            {
                json.AppendLine("  \"event\": {");
                var eventFields = new List<string>();
                
                // Only add non-empty event fields
                if (eventData.@event.first_open.HasValue) eventFields.Add($"    \"first_open\": {(eventData.@event.first_open.Value ? "true" : "false")}");
                if (!string.IsNullOrEmpty(eventData.@event.campaign_slug)) eventFields.Add($"    \"campaign_slug\": \"{eventData.@event.campaign_slug}\"");
                if (!string.IsNullOrEmpty(eventData.@event.placement)) eventFields.Add($"    \"placement\": \"{eventData.@event.placement}\"");
                if (!string.IsNullOrEmpty(eventData.@event.format)) eventFields.Add($"    \"format\": \"{eventData.@event.format}\"");
                // Note: source_store_id is in context.store_id (universal) - not duplicated here
                // Note: source_project_id is derived server-side from project_key (not sent from SDK)
                if (!string.IsNullOrEmpty(eventData.@event.target_store_id)) eventFields.Add($"    \"target_store_id\": \"{eventData.@event.target_store_id}\"");
                if (!string.IsNullOrEmpty(eventData.@event.target_project_id)) eventFields.Add($"    \"target_project_id\": \"{eventData.@event.target_project_id}\"");
                if (eventData.@event.campaign_id.HasValue) eventFields.Add($"    \"campaign_id\": {eventData.@event.campaign_id.Value}");
                
                // Install-time identifiers (first_open events only)
                if (!string.IsNullOrEmpty(eventData.@event.asa_token)) eventFields.Add($"    \"asa_token\": \"{eventData.@event.asa_token}\"");
                if (!string.IsNullOrEmpty(eventData.@event.skan_source_id)) eventFields.Add($"    \"skan_source_id\": \"{eventData.@event.skan_source_id}\"");
                if (!string.IsNullOrEmpty(eventData.@event.install_referrer_click_id)) eventFields.Add($"    \"install_referrer_click_id\": \"{eventData.@event.install_referrer_click_id}\"");
                if (!string.IsNullOrEmpty(eventData.@event.attribution_click_id)) eventFields.Add($"    \"attribution_click_id\": \"{eventData.@event.attribution_click_id}\"");
                
                // Join event fields
                if (eventFields.Count > 0)
                {
                    json.AppendLine(string.Join(",\n", eventFields));
                }
                json.Append("  }");
            }
            
            json.AppendLine("\n}");
            return json.ToString();
        }
        
        /// <summary>
        /// Log consolidated event payload for debugging
        /// </summary>
        private static void LogEventPayload(AnalyticsEventData eventData)
        {
            // Event logging removed to reduce verbosity - use debug mode to see server response
        }
        
        /// <summary>
        /// Get or generate session ID that persists for the current app session
        /// NOTE: Session ID is regenerated on SDK init (cold start) and app resume/focus
        /// This method serves as a fallback if GetStoredSessionId is called before initialization
        /// </summary>
        private static string GetStoredSessionId()
        {
            // Check if we have a current session ID
            string currentSessionId = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.CURRENT_SESSION_ID, "");
            
            if (string.IsNullOrEmpty(currentSessionId))
            {
                // Fallback: Generate new session ID if not set (shouldn't happen if SDK is initialized properly)
                currentSessionId = BoostOpsULIDGenerator.GenerateSessionId();
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.CURRENT_SESSION_ID, currentSessionId);
                PlayerPrefs.Save();
                Debug.LogWarning("[BoostOps] Session ID was empty - generated fallback session ID. This shouldn't happen if SDK is initialized.");
            }
            
            return currentSessionId;
        }
        
        /// <summary>
        /// Session timeout in seconds (industry standard for attribution SDKs: AppsFlyer, Branch, Adjust)
        /// If app is backgrounded for less than this time, continue same session
        /// If app is backgrounded for more than this time, start new session
        /// 30 seconds is optimal for cross-promotion (user clicks ad → app store → returns)
        /// </summary>
        private const float SESSION_TIMEOUT_SECONDS = 30f;
        
        /// <summary>
        /// Force regeneration of session ID
        /// Called on: SDK initialization (cold start), or after session timeout exceeded
        /// </summary>
        public static void RegenerateSessionId()
        {
            string newSessionId = BoostOpsULIDGenerator.GenerateSessionId();
            PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.CURRENT_SESSION_ID, newSessionId);
            PlayerPrefs.Save();
            Debug.Log($"[BoostOps] Generated new session ID: {newSessionId}");
        }
        
        /// <summary>
        /// Record timestamp when app goes to background
        /// Used to determine if session timeout has been exceeded on resume
        /// </summary>
        public static void RecordBackgroundTimestamp()
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.LAST_BACKGROUND_TIMESTAMP, timestamp.ToString());
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Check if session timeout has been exceeded (app was backgrounded for > SESSION_TIMEOUT_SECONDS)
        /// Returns true if a new session should be started
        /// </summary>
        public static bool ShouldStartNewSession()
        {
            string lastBackgroundStr = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.LAST_BACKGROUND_TIMESTAMP, "");
            
            // If no timestamp recorded, start new session
            if (string.IsNullOrEmpty(lastBackgroundStr))
            {
                return true;
            }
            
            // Parse timestamp
            if (!long.TryParse(lastBackgroundStr, out long lastBackgroundMs))
            {
                return true;
            }
            
            // Calculate time since background
            long currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long elapsedMs = currentTimeMs - lastBackgroundMs;
            float elapsedSeconds = elapsedMs / 1000f;
            
            // Check if timeout exceeded
            bool timeoutExceeded = elapsedSeconds > SESSION_TIMEOUT_SECONDS;
            
            // Debug: Uncomment for detailed session timeout logging
            // if (timeoutExceeded)
            // {
            //     Debug.Log($"[BoostOps] Session timeout exceeded: {elapsedSeconds:F1}s > {SESSION_TIMEOUT_SECONDS}s");
            // }
            // else
            // {
            //     Debug.Log($"[BoostOps] Within session timeout: {elapsedSeconds:F1}s <= {SESSION_TIMEOUT_SECONDS}s");
            // }
            
            return timeoutExceeded;
        }
        

        
        /// <summary>
        /// Create event context with platform information and device-stable identifiers
        /// Device-stable IDs are placed in context to avoid repetition across events
        /// </summary>
        public static EventContext CreateEventContext(Dictionary<string, object> identifiers)
        {
            var context = new EventContext
            {
                // Platform and environment information
                source = GetEventSource(),
                platform = GetBuildTargetPlatform(),
                os_version = SystemInfo.operatingSystem,
                app_version = Application.version,
                app_identifier = Application.identifier, // Bundle ID (com.company.appname)
                sdk_version = GetSDKVersion(),
                store = GetStoreIdentifier(),
                store_id = GetStoreId(),
                device_model = GetSimulatorAwareDeviceModel(),
                device_brand = GetSimulatorAwareDeviceBrand(),
                
                // Environment detection (detailed metadata for analytics)
                environment = BoostOps.BoostOpsEnvironment.GetEnvironment(),
                installer_source = BoostOps.BoostOpsEnvironment.GetInstallerSource(),
                
                country = GetCountryCode(),
                storefront_country = GetIdentifierValue(identifiers, "storefront_country"),
                region = null, // TODO: Implement region detection if needed
                city = null, // TODO: Implement city detection if needed  
                timezone_offset_minutes = GetTimezoneOffsetMinutes(), // e.g., -480 for PST, -420 for PDT
                locale = GetDeviceLocale(), // e.g., "en_US", "es_MX", "pt_BR"
                language = ExtractLanguageFromLocale(GetDeviceLocale()), // e.g., "en", "es", "zh"
                carrier = null, // TODO: Implement carrier detection if needed
                connection_type = GetConnectionType(),
                ip_address = null, // Server-side populated
                // Note: timestamp is at top-level (milliseconds precision) - not duplicated in context
                
                // Device-stable identifiers (cross-app correlation, every call)
                // Note: install_id and custom_user_id moved to top-level (schema v6)
                app_account_token = GetIdentifierValue(identifiers, "app_account_token"),
                idfv = GetIdentifierValue(identifiers, "idfv"),
                idfa = GetIdentifierValue(identifiers, "idfa"),
                asid_sha256 = GetIdentifierValue(identifiers, "asid_sha256b64"), // FIXED: Match the key name from BoostOpsIdentifierManager
                gaid = GetIdentifierValue(identifiers, "gaid"),
                firebase_app_id = GetIdentifierValue(identifiers, "firebase_app_id"),
                
                // Windows-specific identifiers
                windows_device_id = GetIdentifierValue(identifiers, "windows_device_id"),
                windows_machine_guid = GetIdentifierValue(identifiers, "windows_machine_guid"),
                msaid = GetIdentifierValue(identifiers, "msaid")
            };
            
            return context;
        }
        

        
        /// <summary>
        /// Create event data with install-time identifiers (only for install events)
        /// Install-time IDs are placed in event object to cut bandwidth for non-install events
        /// </summary>
        public static EventData CreateEventData(Dictionary<string, object> identifiers, bool includeInstallTimeExtras)
        {
            var eventData = new EventData();
            
            // Add install-time identifiers only when requested (install events)
            if (includeInstallTimeExtras)
            {
                eventData.asa_token = GetIdentifierValue(identifiers, "asa_token");
                eventData.skan_source_id = GetIdentifierValue(identifiers, "skan_source_id");
                eventData.install_referrer_click_id = GetIdentifierValue(identifiers, "install_referrer_click_id");
                
                // Universal attribution click ID (from install referrer/attribution API)
                eventData.attribution_click_id = GetIdentifierValue(identifiers, "attribution_click_id");
            }
            
            // Device identifiers are now properly placed in context object only (industry standard)
            
            return eventData;
        }
        
        /// <summary>
        /// Create consent data for privacy compliance (GDPR/CCPA/etc)
        /// </summary>
        public static ConsentData CreateConsentData()
        {
            var consent = new ConsentData();
            
            // Get current consent status from BoostOps consent manager
            // This integrates with the app's privacy consent system
            var consentManager = BoostOpsConsentManager.Instance;
            if (consentManager != null)
            {
                // Framework identification - developer determines if GDPR/CCPA applies
                // SDK does not make assumptions about user location
                consent.framework = ""; // Reserved for future use
                consent.gdpr_consent_required = false; // Developer must set this via consent manager
                consent.ccpa_consent_required = false; // Developer must set this via consent manager
                
                // Enhanced consent metadata (new fields)
                consent.consent_timestamp = consentManager.GetConsentTimestamp();
                consent.consent_version = consentManager.GetConsentVersion();
                consent.consent_language = consentManager.GetConsentLanguage();
                consent.consent_method = consentManager.GetConsentMethod();
                consent.consent_source = consentManager.GetConsentSource();
                consent.legal_basis = consentManager.GetLegalBasis();
                
                // Withdrawal tracking
                consent.withdrawal_timestamp = consentManager.GetWithdrawalTimestamp();
                consent.withdrawal_method = consentManager.GetWithdrawalMethod();
                
                // GDPR-specific consent (structured) - developer determines if GDPR applies
                if (consentManager.HasAnalyticsConsent() || consentManager.HasMarketingConsent())
                {
                    consent.gdpr = new GDPRConsent
                    {
                        applies = false, // Developer must determine GDPR applicability
                        consent_given = consentManager.HasAnalyticsConsent(),
                        analytics = consentManager.HasAnalyticsConsent(),
                        advertising = consentManager.HasMarketingConsent(),
                        measurement = consentManager.HasAnalyticsConsent(),
                        legal_basis = consentManager.GetLegalBasis()
                    };
                }
                
                // ATT consent (iOS tracking authorization)
                #if UNITY_IOS && !UNITY_EDITOR
                consent.att = new ATTConsent
                {
                    status = PrivacyConsentHelpers.GetATTStatus(),
                    authorized_time = PrivacyConsentHelpers.GetATTAuthorizedTime(),
                    idfa_available = PrivacyConsentHelpers.GetIDFAAvailable()
                };
                #endif
                
                // Android privacy settings
                #if UNITY_ANDROID && !UNITY_EDITOR
                consent.android = new AndroidPrivacy
                {
                    advertising_id = PrivacyConsentHelpers.GetAndroidAdvertisingIdConsent(),
                    limited_ad_tracking = PrivacyConsentHelpers.GetAndroidLimitedAdTracking()
                };
                #endif
            }
            else
            {
                // Fallback: No consent manager configured
                // Set basic defaults for compliance logging
                consent.framework = "none";
                consent.gdpr_consent_required = false;
                consent.ccpa_consent_required = false;
                consent.legal_basis = "legitimate_interest";
                consent.consent_method = "implicit";
                consent.consent_source = "default";
            }
            
            return consent;
        }
        
        /// <summary>
        /// Create a BoostOps impression event with cross-promotion support
        /// Note: source_store_id is available in context.store_id (not duplicated in event)
        /// Note: source_project_id is derived server-side from project_key (not sent from SDK)
        /// </summary>
        public static AnalyticsEventData CreateImpressionEvent(string campaignSlug, string placement = null, 
            string format = null, int? durationMs = null,
            string targetStoreId = null, string targetProjectId = null, string networkCampaignId = null, 
            float? revenueShareRate = null, long? estimatedCpmMicros = null, string channel = null, string impressionId = null)
        {
            var eventData = CreateEvent("boostops_impression");
            eventData.@event.campaign_slug = campaignSlug;
            eventData.@event.placement = placement; // Legacy field
            eventData.@event.placement_id = placement; // New field
            eventData.@event.format = format;
            eventData.@event.channel = channel;
            eventData.@event.duration_ms = durationMs;
            
            // Use provided impression_id, or generate a new one
            // Caller should generate and store impression_id on the display object for later use in click events
            eventData.@event.impression_id = impressionId ?? BoostOpsImpressionTracker.GenerateImpressionId();
            eventData.@event.impression_timestamp = eventData.timestamp_ms;
            
            // Cross-promotion fields
            // Note: source_store_id is in context.store_id (universal) - not duplicated here
            // Note: source_project_id is derived server-side from project_key (not sent from SDK)
            eventData.@event.target_store_id = targetStoreId;
            eventData.@event.target_project_id = targetProjectId;
            eventData.@event.network_campaign_id = networkCampaignId;
            eventData.@event.revenue_share_rate = revenueShareRate;
            eventData.@event.estimated_cpm_micros = estimatedCpmMicros;
            
            return eventData;
        }
        
        /// <summary>
        /// Create a BoostOps click event with cross-promotion support
        /// Note: source_store_id is available in context.store_id (not duplicated in event)
        /// Note: source_project_id is derived server-side from project_key (not sent from SDK)
        /// </summary>
        public static AnalyticsEventData CreateClickEvent(string campaignSlug, string placement = null,
            ClickCoordinates clickCoordinates = null, int? timeToClickMs = null,
            string targetStoreId = null, string targetProjectId = null,
            string networkCampaignId = null, string deepLinkUrl = null, string redirectUrl = null, 
            long? clickValueMicros = null, float? revenueShareRate = null, string format = null, string channel = null,
            string impressionId = null, long? impressionTimestamp = null, string containerImpressionId = null, string clickId = null)
        {
            var eventData = CreateEvent("boostops_click");
            eventData.@event.campaign_slug = campaignSlug;
            eventData.@event.placement = placement; // Legacy field
            eventData.@event.placement_id = placement; // New field
            eventData.@event.format = format;
            eventData.@event.channel = channel;
            eventData.@event.click_coordinates = clickCoordinates;
            
            // Generate or use provided click_id (for deterministic Android attribution via Play referrer)
            eventData.@event.click_id = clickId ?? System.Guid.NewGuid().ToString("N");
            
            // Link to impression if provided (passed from display object)
            if (!string.IsNullOrEmpty(impressionId))
            {
                eventData.@event.impression_id = impressionId;
                eventData.@event.impression_timestamp = impressionTimestamp;
                
                // Calculate time_to_click_ms if not provided
                if (!timeToClickMs.HasValue && impressionTimestamp.HasValue)
                {
                    eventData.@event.time_to_click_ms = (int)(eventData.timestamp_ms - impressionTimestamp.Value);
                }
                else
                {
                    eventData.@event.time_to_click_ms = timeToClickMs;
                }
            }
            else
            {
                // No impression - click without impression (e.g., deep link, direct launch)
                eventData.@event.time_to_click_ms = timeToClickMs;
            }
            
            // Link to container impression (for app walls)
            if (!string.IsNullOrEmpty(containerImpressionId))
            {
                eventData.@event.container_impression_id = containerImpressionId;
            }
            
            // Cross-promotion fields
            // Note: source_store_id is in context.store_id (universal) - not duplicated here
            // Note: source_project_id is derived server-side from project_key (not sent from SDK)
            eventData.@event.target_store_id = targetStoreId;
            eventData.@event.target_project_id = targetProjectId;
            eventData.@event.network_campaign_id = networkCampaignId;
            eventData.@event.deep_link_url = deepLinkUrl;
            eventData.@event.redirect_url = redirectUrl;
            eventData.@event.click_value_micros = clickValueMicros;
            eventData.@event.revenue_share_rate = revenueShareRate;
            
            return eventData;
        }
        
        /// <summary>
        /// Create app wall impression event - uses standard impression event (boostops_impression) with format="app_wall"
        /// Contains nested items array with data for all items displayed in the app wall
        /// Each item has its own impression_id (for click linking), wall has container_impression_id (for grouping)
        /// Note: source_store_id is available in context.store_id (not duplicated in event)
        /// Note: source_project_id is derived server-side from project_key (not sent from SDK)
        /// </summary>
        public static AnalyticsEventData CreateAppWallImpressionEvent(string placement, List<Dictionary<string, object>> items, string containerImpressionId)
        {
            // Use standard impression event type (not a separate event type)
            var eventData = CreateEvent("boostops_impression");
            eventData.@event.campaign_slug = "app_wall_container";  // Container-level slug
            eventData.@event.placement = placement;
            eventData.@event.placement_id = placement;
            eventData.@event.format = "app_wall";  // Format distinguishes this from other impression types
            eventData.@event.channel = "xpromo";
            
            // Container impression ID (groups all items in this wall)
            eventData.@event.container_impression_id = containerImpressionId;
            eventData.@event.impression_timestamp = eventData.timestamp_ms;
            
            // Note: source_store_id is in context.store_id (universal) - not duplicated here
            // Note: source_project_id is derived server-side from project_key (not sent from SDK)
            
            // Add nested items array (each item has its own impression_id for click linking)
            eventData.@event.items = items;
            
            return eventData;
        }
        
        /// <summary>
        /// Create a BoostOps install event with install-time identifiers
        /// DEPRECATED: Use CreateAppOpenEvent with isFirstSession=true instead (industry standard)
        /// </summary>
        [System.Obsolete("Use CreateAppOpenEvent with isFirstSession=true instead. Separate install events are deprecated in favor of industry standard first session approach.")]
        public static AnalyticsEventData CreateInstallEvent(bool? organic = null, bool? reinstall = null,
            long? installSizeBytes = null, int? installDurationMs = null)
        {
            // Redirect to app open event with first session flag (industry standard)
            return CreateAppOpenEvent("install", isFirstSession: true, organic: organic, reinstall: reinstall, 
                installSizeBytes: installSizeBytes, installDurationMs: installDurationMs);
        }
        
        /// <summary>
        /// Create a BoostOps app open event with comprehensive identifiers (industry standard approach)
        /// All identifiers (boostops_id, session_id, platform-specific) are auto-included
        /// When isFirstSession=true, includes install-time attribution data (replaces separate install event)
        /// </summary>
        public static AnalyticsEventData CreateAppOpenEvent(string launchType = "cold", string deeplinkUrl = null, 
            long? timeSinceInstallMs = null, bool? isFirstSession = null, bool? organic = null, bool? reinstall = null,
            long? installSizeBytes = null, int? installDurationMs = null,
            string attributionChannel = null, string attributionCampaignSlug = null, string attributionCampaign = null,
            bool? isReengagement = null, string attributionModel = null, string touchType = null, long? touchTs = null)
        {
            // Include install-time extras if this is the first session (industry standard)
            bool includeInstallTimeExtras = isFirstSession == true;
            bool includeInstallTimestamp = isFirstSession == true;  // Include install_time for SDK migration detection
            var eventData = CreateEvent("boostops_open", includeInstallTimeExtras, includeInstallTimestamp);
            
            // Industry standard: first_open flag (replaces separate install event)
            eventData.@event.first_open = isFirstSession;
            
            // Debug logging for first open with install_time_ms
            if (isFirstSession == true && eventData.install_time_ms.HasValue)
            {
                var installDate = DateTimeOffset.FromUnixTimeMilliseconds(eventData.install_time_ms.Value);
                var now = DateTimeOffset.UtcNow;
                var daysSinceInstall = (now - installDate).TotalDays;
                // Debug.Log($"[BoostOps] 📱 FIRST OPEN EVENT - install_time_ms: {eventData.install_time_ms.Value} ({installDate:yyyy-MM-dd HH:mm:ss} UTC) - {daysSinceInstall:F1} days ago");
            }
            
            // Industry standard app open fields
            eventData.@event.launch_type = launchType ?? "cold";
            eventData.@event.entry_point = GetEntryPoint(deeplinkUrl);
            eventData.@event.session_reason = GetSessionReason(isFirstSession, launchType);
            eventData.@event.deep_link_url = deeplinkUrl;  // Legacy field
            eventData.@event.time_since_install_ms = timeSinceInstallMs;
            
            // Attribution fields for lifecycle events
            eventData.@event.attribution_channel = attributionChannel;
            eventData.@event.attribution_campaign_slug = attributionCampaignSlug;
            eventData.@event.attribution_campaign = attributionCampaign;
            eventData.@event.is_reengagement = isReengagement;
            eventData.@event.attribution_model = attributionModel;
            eventData.@event.touch_type = touchType;
            eventData.@event.touch_ts = touchTs;
            
            // App version tracking
            eventData.@event.app_version_updated = GetAppVersionUpdated();
            eventData.@event.previous_app_version = GetPreviousAppVersion();
            
            // Note: network_type is in context.connection_type (universal) - not duplicated here
            // Note: country is in context.country (universal) - not duplicated here
            // Note: locale is in context.locale (universal) - not duplicated here
            // Note: language is in context.language (universal) - not duplicated here
            // Note: timezone_offset_minutes is in context (universal) - not duplicated here
            
            eventData.@event.screen_width = Screen.width;
            eventData.@event.screen_height = Screen.height;
            eventData.@event.device_orientation = GetDeviceOrientation();
            eventData.@event.battery_saver = GetBatterySaverMode();
            eventData.@event.low_power_mode = GetLowPowerMode();
            
            // Notification permissions
            eventData.@event.push_permission = GetPushPermissionStatus();
            eventData.@event.notifications_enabled = GetNotificationsEnabled();
            
            // Deeplink attribution (if present)
            if (!string.IsNullOrEmpty(deeplinkUrl))
            {
                eventData.@event.deeplink = ParseDeeplinkData(deeplinkUrl);
            }
            
            // Install-specific fields (only included when isFirstSession=true)
            if (isFirstSession == true)
            {
                eventData.@event.organic = organic;
                eventData.@event.reinstall = reinstall;
                eventData.@event.install_size_bytes = installSizeBytes;
                eventData.@event.install_duration_ms = installDurationMs;
                
                // Android: Add Play Install Referrer data
                #if UNITY_ANDROID && !UNITY_EDITOR
                eventData.@event.play_install_referrer = GetPlayInstallReferrerData();
                #endif
            }
            
            return eventData;
        }
        
        /// <summary>
        /// Create a BoostOps purchase event with comprehensive identifiers
        /// All identifiers (boostops_id, app_account_token, platform-specific) are auto-included
        /// </summary>
        public static AnalyticsEventData CreatePurchaseEvent(string currency, decimal amount, string productId,
            string transactionId = null, string receipt = null, int? quantity = 1, bool? isSubscription = null,
            bool? isTrial = null, int? renewalNumber = null,
            string attributionChannel = null, string attributionCampaignSlug = null, string attributionCampaign = null,
            bool? isReengagement = null, string attributionModel = null, string touchType = null, long? touchTs = null,
            // Subscription metadata
            string subscriptionPeriod = null, string originalTransactionId = null,
            decimal? introductoryPrice = null, int? introductoryPriceCycles = null,
            // Purchase history hints
            bool? firstPurchase = null, int? purchaseCount = null)
        {
            // Standard event with all identifiers automatically included
            var eventData = CreateEvent("boostops_purchase");
            
            // CRITICAL: Verify install_id is present (essential for Android revenue attribution)
            if (string.IsNullOrEmpty(eventData.install_id))
            {
                Debug.LogError("[BoostOps] ❌ CRITICAL: CreatePurchaseEvent - install_id is null/empty! Attempting recovery...");
                // Attempt to recover by explicitly fetching install_id
                eventData.install_id = BoostOpsIdentifierManager.GetInstallId();
                if (string.IsNullOrEmpty(eventData.install_id))
                {
                    Debug.LogError("[BoostOps] ❌ FATAL: Could not recover install_id for purchase event! Revenue attribution WILL FAIL.");
                }
                else
                {
                    Debug.Log($"[BoostOps] ✅ Recovered install_id for purchase event: {eventData.install_id}");
                }
            }
            else
            {
                Debug.Log($"[BoostOps] ✅ CreatePurchaseEvent - install_id present: {eventData.install_id}");
            }
            
            eventData.@event.currency = currency?.ToUpper();
            eventData.@event.amount_micros = CurrencyMicros.ToMicros(amount);
            eventData.@event.product_id = productId;
            eventData.@event.transaction_id = transactionId;
            eventData.@event.receipt = receipt;
            eventData.@event.quantity = quantity;
            eventData.@event.is_subscription = isSubscription;
            eventData.@event.is_trial = isTrial;
            eventData.@event.renewal_number = renewalNumber;
            
            // Subscription metadata (iOS parity)
            eventData.@event.subscription_period = subscriptionPeriod;
            eventData.@event.original_transaction_id = originalTransactionId;
            eventData.@event.introductory_price_micros = introductoryPrice.HasValue ? CurrencyMicros.ToMicros(introductoryPrice.Value) : (long?)null;
            eventData.@event.introductory_price_cycles = introductoryPriceCycles;
            
            // Purchase history hints (for segmentation and LTV analysis)
            eventData.@event.first_purchase = firstPurchase;
            eventData.@event.purchase_count = purchaseCount;
            
            // Attribution fields for lifecycle events
            eventData.@event.attribution_channel = attributionChannel;
            eventData.@event.attribution_campaign_slug = attributionCampaignSlug;
            eventData.@event.attribution_campaign = attributionCampaign;
            eventData.@event.is_reengagement = isReengagement;
            eventData.@event.attribution_model = attributionModel;
            eventData.@event.touch_type = touchType;
            eventData.@event.touch_ts = touchTs;
            
            return eventData;
        }
        
        /// <summary>
        /// Create a BoostOps attribution update event
        /// </summary>
        public static AnalyticsEventData CreateAttributionUpdateEvent(string installToken, string attributionSource,
            SKANData skanData = null, float? confidenceScore = null, string attributionMethod = null)
        {
            var eventData = CreateEvent("boostops_install_attribution_update");
            // Note: install_token is now auto-included at top-level by CreateEvent()
            eventData.@event.attribution_source = attributionSource;
            eventData.@event.skan = skanData;
            eventData.@event.attribution_confidence = confidenceScore;
            eventData.@event.attribution_method = attributionMethod;
            
            return eventData;
        }
        
        // Backend analytics events removed - these are calculated on BoostOps servers, not sent by SDK
        
        /// <summary>
        /// Add privacy consent information (deprecated - consent is now at top-level)
        /// </summary>
        [System.Obsolete("Consent is now handled at the top-level event data. Use CreateConsentData() instead.")]
        public static void AddPrivacyConsent(EventData eventData, ConsentData consentData)
        {
            // This method is deprecated since consent moved to top-level
        }
        
        /// <summary>
        /// Add Apple attribution data
        /// </summary>
        public static void AddAppleAttribution(EventData eventData, string attributionToken = null,
            long? campaignId = null, long? adGroupId = null, long? keywordId = null)
        {
            if (!string.IsNullOrEmpty(attributionToken) || campaignId.HasValue)
            {
                eventData.apple_search_ads = new AppleAttributionData
                {
                    token = attributionToken,
                    campaign_id = campaignId,
                    ad_group_id = adGroupId,
                    keyword_id = keywordId
                };
            }
        }
        
        /// <summary>
        /// Add SKAdNetwork data
        /// </summary>
        public static void AddSKANData(EventData eventData, SKANData skanData)
        {
            eventData.skan = skanData;
        }
        
        /// <summary>
        /// Add Google Play Install Referrer data
        /// </summary>
        public static void AddPlayInstallReferrer(EventData eventData, string referrer, 
            long? clickTimestamp = null, long? installBeginTimestamp = null)
        {
            if (!string.IsNullOrEmpty(referrer))
            {
                eventData.play_install_referrer = new PlayInstallReferrerData
                {
                    referrer = referrer,
                    click_ts = clickTimestamp,
                    install_begin_ts = installBeginTimestamp
                };
            }
        }
        
        /// <summary>
        /// Add custom game data
        /// </summary>
        public static void AddGameData(EventData eventData, int? level = null, string character = null,
            string guildId = null, int? powerScore = null, bool? tutorialCompleted = null)
        {
            if (eventData.custom_data == null)
                eventData.custom_data = new CustomData();
                
            eventData.custom_data.game = new GameData
            {
                level = level,
                character = character,
                guild_id = guildId,
                power_score = powerScore,
                tutorial_completed = tutorialCompleted
            };
        }
        
        /// <summary>
        /// Add custom ecommerce data
        /// </summary>
        public static void AddEcommerceData(EventData eventData, string cartId = null, int? checkoutStep = null,
            string shippingMethod = null, string paymentMethod = null, string couponCode = null)
        {
            if (eventData.custom_data == null)
                eventData.custom_data = new CustomData();
                
            eventData.custom_data.ecommerce = new EcommerceData
            {
                cart_id = cartId,
                checkout_step = checkoutStep,
                shipping_method = shippingMethod,
                payment_method = paymentMethod,
                coupon_code = couponCode
            };
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Get event source: "sdk" for builds, "sdk-simulator" for editor
        /// </summary>
        private static string GetEventSource()
        {
            return Application.isEditor ? "sdk-simulator" : "sdk";
        }
        
        /// <summary>
        /// Get platform based on build target, not runtime platform
        /// </summary>
        private static string GetBuildTargetPlatform()
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#elif UNITY_STANDALONE_WIN
            return "windows";
#elif UNITY_STANDALONE_OSX
            return "macos";
#elif UNITY_STANDALONE_LINUX
            return "linux";
#elif UNITY_WEBGL
            return "webgl";
#else
            // Fallback to runtime detection for unsupported platforms
            return GetPlatformString();
#endif
        }
        
        /// <summary>
        /// Get device model with simulator suffix when in editor
        /// </summary>
        private static string GetSimulatorAwareDeviceModel()
        {
            var deviceModel = SystemInfo.deviceModel;
            if (Application.isEditor)
            {
#if UNITY_IOS
                return "iPhone15,2-Simulator"; // Generic iOS simulator
#elif UNITY_ANDROID
                return "Pixel8-Simulator"; // Generic Android simulator
#else
                return deviceModel + "-Simulator";
#endif
            }
            return deviceModel;
        }
        
        /// <summary>
        /// Get device brand with simulator awareness
        /// </summary>
        private static string GetSimulatorAwareDeviceBrand()
        {
            if (Application.isEditor)
            {
#if UNITY_IOS
                return "Apple";
#elif UNITY_ANDROID
                return "Google";
#else
                return "Simulator";
#endif
            }
            return GetDeviceBrand();
        }
        
        private static string GetPlatformString()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    return "ios";
                case RuntimePlatform.Android:
                    return "android";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "windows";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "macos";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "linux";
                case RuntimePlatform.WebGLPlayer:
                    return "webgl";
                default:
                    return Application.platform.ToString().ToLower();
            }
        }
        
        private static string GetStoreIdentifier()
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            // Detect actual Android store from installer package name
            string installer = BoostOps.BoostOpsEnvironment.GetInstallerSource();
            
            // Map installer source to store identifier
            if (installer.Contains("google") || installer == "google_play")
                return "google";
            else if (installer.Contains("amazon"))
                return "amazon";
            else if (installer.Contains("samsung"))
                return "samsung";
            else if (installer.Contains("huawei"))
                return "huawei";
            else if (installer.Contains("xiaomi") || installer.Contains("mi"))
                return "xiaomi";
            else if (installer.Contains("oppo"))
                return "oppo";
            else if (installer.Contains("vivo"))
                return "vivo";
            else if (installer == "sideload" || installer == "unknown")
                return "sideload";
            else
                return "google"; // Default to google for unknown Android stores
#elif UNITY_STANDALONE_OSX
            // macOS: Check if installed from Mac App Store
            // Mac App Store apps have a receipt at:
            // /Applications/YourApp.app/Contents/_MASReceipt/receipt
            try
            {
                string receiptPath = System.IO.Path.Combine(Application.dataPath, "../Contents/_MASReceipt/receipt");
                if (System.IO.File.Exists(receiptPath))
                {
                    return "macos";
                }
            }
            catch
            {
                // If we can't check, assume standalone
            }
            return "standalone";  // Direct download or other distribution
#elif UNITY_WSA || UNITY_WINRT || UNITY_STANDALONE_WIN
            return "microsoft";
#elif UNITY_STANDALONE_LINUX
            return "standalone";
#elif UNITY_WEBGL
            return "webgl";
#else
            return "other";
#endif
        }
        
        /// <summary>
        /// Get platform-specific store ID
        /// iOS: App Store ID (e.g., "1114393474")
        /// Android/Amazon/Samsung: Package name (e.g., "com.app.package")
        /// Windows: Store ID
        /// </summary>
        private static string GetStoreId()
        {
            try
            {
                var settings = BoostOps.Internal.InternalSettingsCache.GetProjectSettings();
                if (settings == null) return null;
                
#if UNITY_IOS
                // iOS: Return App Store ID
                return !string.IsNullOrEmpty(settings.AppleAppStoreId) ? settings.AppleAppStoreId : null;
#elif UNITY_ANDROID
                // Android: Return package name (works for Google Play, Amazon, Samsung)
                // Try Android package name first, then fall back to Amazon/Samsung specific IDs
                if (!string.IsNullOrEmpty(settings.AndroidPackageName))
                    return settings.AndroidPackageName;
                if (!string.IsNullOrEmpty(settings.AmazonStoreId))
                    return settings.AmazonStoreId;
                if (!string.IsNullOrEmpty(settings.SamsungStoreId))
                    return settings.SamsungStoreId;
                return null;
#elif UNITY_WSA || UNITY_WINRT || UNITY_STANDALONE_WIN
                // Windows: Return Windows Store ID
                return !string.IsNullOrEmpty(settings.MicrosoftStoreId) ? settings.MicrosoftStoreId : null;
#else
                // Other platforms: Return null
                return null;
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to get store ID: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get connection type (wifi/cellular/unknown)
        /// </summary>
        private static string GetConnectionType()
        {
            try
            {
                switch (Application.internetReachability)
                {
                    case NetworkReachability.ReachableViaCarrierDataNetwork:
                        return "cellular";
                    case NetworkReachability.ReachableViaLocalAreaNetwork:
                        return "wifi";
                    case NetworkReachability.NotReachable:
                        return "none";
                    default:
                        return "unknown";
                }
            }
            catch
            {
                return "unknown";
            }
        }
        
        private static string GetDeviceBrand()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    return "Apple";
                case RuntimePlatform.Android:
                    // SystemInfo.deviceModel often contains brand for Android
                    var model = SystemInfo.deviceModel;
                    if (model.ToLower().Contains("samsung")) return "Samsung";
                    if (model.ToLower().Contains("google")) return "Google";
                    if (model.ToLower().Contains("oneplus")) return "OnePlus";
                    if (model.ToLower().Contains("xiaomi")) return "Xiaomi";
                    return "Android";
                default:
                    return "Unknown";
            }
        }
        
        private static string GetSDKVersion()
        {
            // SDK Version 2.0.6: Schema v6 with elapsed_realtime_ms + three-tier ID hierarchy + simplified install_id
            return "2.0.6";
        }
        
        private static string GetCountryCode()
        {
            // Extract country from device locale (e.g., "en_GB@numbers=latn" → "GB")
            string locale = GetDeviceLocale();
            string country = ExtractCountryFromLocale(locale);
            
            // Debug log for locale parsing (helps identify UN M.49 regions)
            if (!string.IsNullOrEmpty(locale))
            {
                Debug.Log($"[BoostOps] Locale parsing: '{locale}' → country: '{country ?? "null (UN M.49 region or no country)"}' ");
            }
            
            // Return clean ISO 3166-1 alpha-2 country code (no locale modifiers)
            return country;
        }
        
        #region Industry Standard App Open Helper Methods
        
        /// <summary>
        /// Determine entry point that triggered the app open (industry standard)
        /// Detects deep links, push notifications, widgets, and other launch sources
        /// </summary>
        private static string GetEntryPoint(string deeplinkUrl)
        {
            // Priority 1: Check for deep link (explicit signal)
            if (!string.IsNullOrEmpty(deeplinkUrl))
            {
                if (deeplinkUrl.StartsWith("http"))
                    return "universal_link";  // iOS Universal Link or Android App Link
                else
                    return "url_scheme";      // Custom scheme (myapp://...)
            }
            
            // Priority 2: Check for push notification launch
            #if UNITY_IOS || UNITY_ANDROID
            if (WasLaunchedFromPushNotification())
                return "push";
            #endif
            
            // Priority 3: Check for widget launch (platform-specific)
            #if UNITY_IOS
            if (WasLaunchedFromWidget())
                return "widget";
            #endif
            
            // Priority 4: Check for Siri/Shortcuts (iOS)
            #if UNITY_IOS
            if (WasLaunchedFromSiri())
                return "siri";
            #endif
            
            // Priority 5: Check for 3D Touch quick action (iOS)
            #if UNITY_IOS
            if (WasLaunchedFrom3DTouch())
                return "3d_touch";
            #endif
            
            // Default: User tapped app icon (most common)
            return "icon";
        }
        
        /// <summary>
        /// Check if app was launched from a push notification
        /// Uses Unity's push notification state or native platform detection
        /// </summary>
        private static bool WasLaunchedFromPushNotification()
        {
            try
            {
                #if UNITY_IOS && !UNITY_EDITOR
                // iOS: Check launch options for remote notification
                // Note: Would need native plugin to access UIApplication launch options
                // For now, stub that returns false
                return false;
                #elif UNITY_ANDROID && !UNITY_EDITOR
                // Android: Check Intent extras for notification data
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    AndroidJavaObject intent = currentActivity.Call<AndroidJavaObject>("getIntent");
                    
                    // Check if intent has notification extras
                    AndroidJavaObject extras = intent.Call<AndroidJavaObject>("getExtras");
                    if (extras != null)
                    {
                        // Common notification extra keys
                        bool hasNotificationId = extras.Call<bool>("containsKey", "notification_id");
                        bool hasGcmNotification = extras.Call<bool>("containsKey", "gcm.notification.body");
                        bool hasFcmNotification = extras.Call<bool>("containsKey", "google.delivered_priority");
                        
                        return hasNotificationId || hasGcmNotification || hasFcmNotification;
                    }
                }
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to check push notification launch: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if app was launched from a widget (iOS)
        /// </summary>
        private static bool WasLaunchedFromWidget()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            try
            {
                // iOS: Check for widget launch via NSUserActivity
                // Would need native plugin to properly detect
                // For now, stub that returns false
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to check widget launch: {ex.Message}");
            }
            #endif
            
            return false;
        }
        
        /// <summary>
        /// Check if app was launched from Siri/Shortcuts (iOS)
        /// </summary>
        private static bool WasLaunchedFromSiri()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            try
            {
                // iOS: Check for Siri Shortcut launch
                // Would need native plugin to access NSUserActivity
                // For now, stub that returns false
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to check Siri launch: {ex.Message}");
            }
            #endif
            
            return false;
        }
        
        /// <summary>
        /// Check if app was launched from 3D Touch quick action (iOS)
        /// </summary>
        private static bool WasLaunchedFrom3DTouch()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            try
            {
                // iOS: Check for 3D Touch quick action
                // Would need native plugin to access UIApplicationShortcutItem
                // For now, stub that returns false
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to check 3D Touch launch: {ex.Message}");
            }
            #endif
            
            return false;
        }
        
        /// <summary>
        /// Determine session reason based on context
        /// </summary>
        private static string GetSessionReason(bool? isFirstSession, string launchType)
        {
            if (isFirstSession == true)
                return "first_launch";
            
            // Could be enhanced with more logic to detect upgrades, OS restarts, etc.
            return "resume";
        }
        
        /// <summary>
        /// Check if app version was updated since last launch
        /// </summary>
        private static bool? GetAppVersionUpdated()
        {
            try
            {
                string currentVersion = Application.version;
                string lastVersion = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.LAST_APP_VERSION, "");
                
                if (string.IsNullOrEmpty(lastVersion))
                {
                    // First time tracking version
                    PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.LAST_APP_VERSION, currentVersion);
                    PlayerPrefs.Save();
                    return null;  // Unknown for first launch
                }
                
                bool wasUpdated = lastVersion != currentVersion;
                if (wasUpdated)
                {
                    PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.LAST_APP_VERSION, currentVersion);
                    PlayerPrefs.Save();
                }
                
                return wasUpdated;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Get previous app version (when app was updated)
        /// </summary>
        private static string GetPreviousAppVersion()
        {
            try
            {
                return PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.LAST_APP_VERSION, null);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Get current network type
        /// </summary>
        private static string GetNetworkType()
        {
            try
            {
                switch (Application.internetReachability)
                {
                    case NetworkReachability.ReachableViaCarrierDataNetwork:
                        return "cellular";
                    case NetworkReachability.ReachableViaLocalAreaNetwork:
                        return "wifi";
                    case NetworkReachability.NotReachable:
                        return "offline";
                    default:
                        return "unknown";
                }
            }
            catch
            {
                return "unknown";
            }
        }
        
        /// <summary>
        /// Get device locale in industry-standard format (e.g., "en_US", "es_MX", "pt_BR")
        /// Uses native implementations for accurate locale detection
        /// Returns null if native locale is unavailable (which is correct - don't mask issues)
        /// </summary>
        private static string GetDeviceLocale()
        {
            try
            {
                // Use BoostOpsIdentifierManager which has native implementations
                string locale = BoostOpsIdentifierManager.GetDeviceLocale();
                
                if (!string.IsNullOrEmpty(locale))
                {
                    return locale;
                }
                
                // If null, log warning and return null (don't hide the issue!)
                Debug.LogWarning("[BoostOps] Device locale is null - native implementation may have failed");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to get device locale: {e.Message}");
                return null; // Return null on error - don't hide the issue!
            }
        }
        
        /// <summary>
        /// Extract language code from locale string (e.g., "en_US" → "en")
        /// </summary>
        private static string ExtractLanguageFromLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return null;
            }
            
            // Split on underscore: "en_US" → ["en", "US"]
            string[] parts = locale.Split('_');
            
            // Return first part (language code)
            return parts.Length > 0 ? parts[0] : null;
        }
        
        /// <summary>
        /// Extract country/region code from locale string (BCP-47 compliant)
        /// Examples: 
        /// - "en_US" → "US" (ISO 3166-1 alpha-2)
        /// - "zh_Hans_CN" → "CN" (ignores script "Hans")
        /// - "es-419" → null (419 is UN M.49 region, not a country)
        /// - "en-001" → null (001 is UN M.49 "World", not a country)
        /// - "zh-Hant-TW" → "TW" (ignores script "Hant")
        /// 
        /// BCP-47 region subtags can be:
        /// - 2 letters (ISO 3166-1 alpha-2 country code) → extract as country
        /// - 3 digits (UN M.49 numeric region code) → NOT a country, return null
        /// - 4 letters (script identifier like "Hans", "Hant") → skip
        /// </summary>
        private static string ExtractCountryFromLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return null;
            }
            
            // Strip locale modifiers first (e.g., "en_GB@numbers=latn" → "en_GB")
            locale = locale.Split('@')[0];
            
            // Split on both underscore and hyphen (BCP-47 uses hyphen, some systems use underscore)
            // "zh-Hant-TW" → ["zh", "Hant", "TW"]
            // "es_MX" → ["es", "MX"]
            string[] parts = locale.Split(new char[] { '_', '-' });
            
            // Find the first valid country code (2 letters, not 3 digits or 4 letters)
            // Scan from end to beginning to prioritize country over script
            for (int i = parts.Length - 1; i >= 1; i--)
            {
                string part = parts[i];
                
                // Valid country code: exactly 2 letters
                if (part.Length == 2 && System.Text.RegularExpressions.Regex.IsMatch(part, "^[A-Za-z]{2}$"))
                {
                    return part.ToUpper();
                }
                
                // Skip 3-digit UN M.49 region codes (e.g., "419", "001")
                // Skip 4-letter script identifiers (e.g., "Hans", "Hant")
            }
            
            return null;
        }
        
        /// <summary>
        /// Get timezone offset in minutes
        /// </summary>
        private static int? GetTimezoneOffsetMinutes()
        {
            try
            {
                return (int)System.TimeZoneInfo.Local.GetUtcOffset(System.DateTime.Now).TotalMinutes;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Get device orientation at launch
        /// </summary>
        private static string GetDeviceOrientation()
        {
            try
            {
                switch (Screen.orientation)
                {
                    case ScreenOrientation.Portrait:
                    case ScreenOrientation.PortraitUpsideDown:
                        return "portrait";
                    case ScreenOrientation.LandscapeLeft:
                    case ScreenOrientation.LandscapeRight:
                        return "landscape";
                    default:
                        return "unknown";
                }
            }
            catch
            {
                return "unknown";
            }
        }
        
        /// <summary>
        /// Check if device is in battery saver mode
        /// </summary>
        private static bool? GetBatterySaverMode()
        {
            // Unity doesn't provide direct access to battery saver mode
            // This would need platform-specific implementation
            return null;
        }
        
        /// <summary>
        /// Check if device is in low power mode (iOS)
        /// </summary>
        private static bool? GetLowPowerMode()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            // This would need native iOS implementation
            // For now, return null
            return null;
            #else
            return null;
            #endif
        }
        
        /// <summary>
        /// Get push notification permission status
        /// </summary>
        private static string GetPushPermissionStatus()
        {
            // Unity doesn't provide direct access to notification permissions
            // This would need platform-specific implementation
            return "not_determined";
        }
        
        /// <summary>
        /// Check if notifications are enabled
        /// </summary>
        private static bool? GetNotificationsEnabled()
        {
            // Unity doesn't provide direct access to notification settings
            // This would need platform-specific implementation
            return null;
        }
        
        /// <summary>
        /// Parse deeplink URL into structured data
        /// </summary>
        private static DeeplinkData ParseDeeplinkData(string deeplinkUrl)
        {
            if (string.IsNullOrEmpty(deeplinkUrl))
                return null;
                
            try
            {
                var uri = new System.Uri(deeplinkUrl);
                var deeplinkData = new DeeplinkData
                {
                    url = deeplinkUrl,
                    scheme_host_path = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}"
                };
                
                // Parse query parameters for UTM data (simple Unity-compatible parsing)
                var query = uri.Query;
                if (!string.IsNullOrEmpty(query))
                {
                    var parameters = ParseQueryString(query);
                    deeplinkData.utm_source = GetQueryParameter(parameters, "utm_source");
                    deeplinkData.utm_medium = GetQueryParameter(parameters, "utm_medium");
                    deeplinkData.utm_campaign = GetQueryParameter(parameters, "utm_campaign");
                    deeplinkData.utm_term = GetQueryParameter(parameters, "utm_term");
                    deeplinkData.utm_content = GetQueryParameter(parameters, "utm_content");
                    deeplinkData.bo_click_id = GetQueryParameter(parameters, "bo_click_id");
                    deeplinkData.branch_click_id = GetQueryParameter(parameters, "branch_click_id");
                    deeplinkData.af_c_id = GetQueryParameter(parameters, "af_c_id");
                }
                
                // Determine match type
                deeplinkData.matched_type = uri.Scheme == "https" ? "universal_link" : "custom_scheme";
                deeplinkData.is_deferred = false;  // Would need to be set by deeplink handler
                
                return deeplinkData;
            }
            catch
            {
                return new DeeplinkData { url = deeplinkUrl };
            }
        }
        
        /// <summary>
        /// Get Google Play Install Referrer data (Android only)
        /// </summary>
        private static PlayInstallReferrerData GetPlayInstallReferrerData()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Read raw install referrer from PlayerPrefs (saved by BoostOpsInstallReferrerNative)
                string rawReferrer = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_RAW, null);
                
                if (string.IsNullOrEmpty(rawReferrer))
                {
                    return null; // Organic install or referrer not yet available
                }
                
                // Read timestamps from PlayerPrefs (saved by BoostOpsInstallAttribution)
                // These are Unix seconds from Google Play Install Referrer API
                long? clickTs = null;
                long? installBeginTs = null;
                
                string clickTsStr = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_CLICK_TS, null);
                if (!string.IsNullOrEmpty(clickTsStr) && long.TryParse(clickTsStr, out long clickTsParsed))
                {
                    clickTs = clickTsParsed;
                }
                
                string installBeginTsStr = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_INSTALL_BEGIN_TS, null);
                if (!string.IsNullOrEmpty(installBeginTsStr) && long.TryParse(installBeginTsStr, out long installBeginTsParsed))
                {
                    installBeginTs = installBeginTsParsed;
                }
                
                return new PlayInstallReferrerData
                {
                    referrer = rawReferrer,
                    click_ts = clickTs,
                    install_begin_ts = installBeginTs
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to get Play Install Referrer data: {e.Message}");
                return null;
            }
            #else
            return null;
            #endif
        }
        
        /// <summary>
        /// Simple query string parser for Unity (no System.Web dependency)
        /// </summary>
        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>();
            
            if (string.IsNullOrEmpty(query))
                return result;
                
            // Remove leading '?' if present
            if (query.StartsWith("?"))
                query = query.Substring(1);
                
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = System.Uri.UnescapeDataString(keyValue[0]);
                    var value = System.Uri.UnescapeDataString(keyValue[1]);
                    result[key] = value;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get parameter value from parsed query string
        /// </summary>
        private static string GetQueryParameter(Dictionary<string, string> parameters, string key)
        {
            return parameters.TryGetValue(key, out string value) ? value : null;
        }

        
        #endregion
    }
    
    /// <summary>
    /// Fluent builder for creating complex events
    /// </summary>
    public class EventBuilder
    {
        private AnalyticsEventData _event;
        
        public EventBuilder(string eventType)
        {
            _event = BoostOpsEventBuilder.CreateEvent(eventType);
        }
        
        public EventBuilder WithCampaign(string campaignSlug, int? campaignId = null)
        {
            _event.@event.campaign_slug = campaignSlug;
            _event.@event.campaign_id = campaignId;
            return this;
        }
        
        public EventBuilder WithRevenue(string currency, decimal amount, string productId)
        {
            _event.@event.currency = currency?.ToUpper();
            _event.@event.amount_micros = CurrencyMicros.ToMicros(amount);
            _event.@event.product_id = productId;
            return this;
        }
        

        
        public EventBuilder WithGameData(int? level = null, string character = null)
        {
            BoostOpsEventBuilder.AddGameData(_event.@event, level, character);
            return this;
        }
        
        public AnalyticsEventData Build()
        {
            return _event;
        }
        
        // Static factory methods for common event types
        public static EventBuilder Impression(string campaignSlug) => new EventBuilder("boostops_impression").WithCampaign(campaignSlug);
        public static EventBuilder Click(string campaignSlug) => new EventBuilder("boostops_click").WithCampaign(campaignSlug);
        [System.Obsolete("Use AppOpen() with WithFirstOpen(true) instead. Separate install events are deprecated in favor of industry standard first session approach.")]
        public static EventBuilder Install() => new EventBuilder("boostops_install");
        public static EventBuilder AppOpen() => new EventBuilder("boostops_open");
        public static EventBuilder Purchase(string currency, decimal amount, string productId) 
            => new EventBuilder("boostops_purchase").WithRevenue(currency, amount, productId);
    }

    #region Privacy Consent Helper Methods
    
    /// <summary>
    /// Helper methods for platform-specific privacy consent data
    /// </summary>
    public static class PrivacyConsentHelpers
    {
        #if UNITY_IOS && !UNITY_EDITOR
        
        /// <summary>
        /// Get current ATT (App Tracking Transparency) status on iOS
        /// </summary>
        public static string GetATTStatus()
        {
            try
            {
                // Using Unity's built-in iOS Device API
                switch (UnityEngine.iOS.Device.advertisingTrackingEnabled)
                {
                    case true: return "authorized";
                    case false: return "denied";
                }
            }
            catch
            {
                return "not_determined";
            }
            return "not_determined";
        }
        
        /// <summary>
        /// Get timestamp when ATT was authorized (if available)
        /// </summary>
        public static long? GetATTAuthorizedTime()
        {
            // This would need to be stored when ATT permission is granted
            // For now, return null as we don't have this stored
            return null;
        }
        
        /// <summary>
        /// Check if IDFA is available on iOS
        /// </summary>
        public static bool? GetIDFAAvailable()
        {
            try
            {
                var idfa = UnityEngine.iOS.Device.advertisingIdentifier;
                return !string.IsNullOrEmpty(idfa) && idfa != "00000000-0000-0000-0000-000000000000";
            }
            catch
            {
                return false;
            }
        }
        
        #else
        
        public static string GetATTStatus() => null;
        public static long? GetATTAuthorizedTime() => null;
        public static bool? GetIDFAAvailable() => null;
        
        #endif
        
        #if UNITY_ANDROID && !UNITY_EDITOR
        
        /// <summary>
        /// Check if user has consented to advertising ID usage on Android
        /// </summary>
        public static bool? GetAndroidAdvertisingIdConsent()
        {
            // This would integrate with Google Play Services to check GAID consent
            // For now, return null as we don't have this integrated
            return null;
        }
        
        /// <summary>
        /// Check if user has enabled Limited Ad Tracking on Android
        /// </summary>
        public static bool? GetAndroidLimitedAdTracking()
        {
            // This would integrate with Google Play Services to check LAT preference
            // For now, return null as we don't have this integrated
            return null;
        }
        
        #else
        
        public static bool? GetAndroidAdvertisingIdConsent() => null;
        public static bool? GetAndroidLimitedAdTracking() => null;
        
        #endif
        
        #endregion
    }
    
    #endregion
}