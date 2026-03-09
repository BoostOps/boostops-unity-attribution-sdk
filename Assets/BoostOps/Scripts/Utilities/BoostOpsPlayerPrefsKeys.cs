using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Centralized constants for all BoostOps PlayerPrefs keys
    /// Ensures proper scoping and prevents collision with other systems
    /// </summary>
    public static class BoostOpsPlayerPrefsKeys
    {
        // Session & Day Tracking (Unity convention: lowercase_snake_case)
        public const string SESSION_COUNT = "boostops.session_count";
        public const string CURRENT_SESSION_ID = "boostops.current_session_id";
        public const string LAST_BACKGROUND_TIMESTAMP = "boostops.last_background_timestamp"; // For session timeout logic
        public const string LAST_APP_VERSION = "boostops.last_app_version";
        public const string FIRST_LAUNCH_TIME = "boostops.first_launch_time";
        public const string FIRST_LAUNCH_TRACKED = "boostops.first_launch_tracked";
        public const string FIRST_LAUNCH_DATE = "boostops.first_launch_date";
        public const string LAST_SESSION_START_TIME = "boostops.last_session_start_time";
        public const string LAST_APP_OPEN_TIME = "boostops.last_app_open_time";
        public const string APP_START_TIME = "boostops.app_start_time";
        public const string IMPRESSIONS_DATA = "boostops.impressions"; // Single key with date validation
        public const string CLICKS_DATA = "boostops.clicks"; // Single key with date validation
        
        // Attribution & Install Tracking
        public const string ATTRIBUTION_DATA = "boostops.attribution";
        public const string CONVERSION_HISTORY = "boostops.conversion_history";
        public const string HAS_LAUNCHED_BEFORE = "boostops.has_launched_before";
        public const string PENDING_ATTRIBUTION_CAMPAIGN = "boostops.pending_attribution_campaign";
        public const string PENDING_ATTRIBUTION_SOURCE = "boostops.pending_attribution_source";
        
        // Deep Link Protection
        public const string CAPTURED_DEEP_LINK = "boostops.captured_deep_link";
        public const string DEEP_LINK_TIMESTAMP = "boostops.deep_link_timestamp";
        
        // Install Referrer
        public const string INSTALL_REFERRER_RAW = "boostops.install_referrer_raw";
        public const string INSTALL_REFERRER_PROCESSED = "boostops.install_referrer_processed";
        public const string INSTALL_REFERRER_CLICK_TS = "boostops.install_referrer_click_ts";
        public const string INSTALL_REFERRER_INSTALL_BEGIN_TS = "boostops.install_referrer_install_begin_ts";
        
        // Revenue Tracking Settings
        public const string AUTO_REVENUE_TRACKING = "boostops.auto_revenue_tracking";
        public const string RECEIPT_VALIDATION = "boostops.receipt_validation";
        public const string ATTRIBUTION_TRACKING = "boostops.attribution_tracking";
        public const string ATTRIBUTION_TOKEN = "boostops.attribution_token";
        public const string ATTRIBUTION_TOKEN_TIME = "boostops.attribution_token_time";
        public const string INSTALL_TOKEN = "boostops.install_token";           // Legacy: Use BOOSTOPS_ID instead
        public const string BOOSTOPS_ID = "boostops.id";                        // Universal cross-app correlation ID
        public const string INSTALL_ID = "boostops.install_id";                 // Per-app installation ID (resets on uninstall)
        public const string CUSTOM_USER_ID = "boostops.custom_user_id";         // Developer-provided custom user identifier
        public const string APP_ACCOUNT_TOKEN = "boostops.app_account_token";   // Apple App Account Token (AAT)
        
        // Privacy-Safe Device Identifiers (Hashed)
        public const string HASHED_DEVICE_ID = "boostops.hashed_device_id";
        public const string HASHED_IDFV = "boostops.hashed_idfv";
        public const string HASHED_IDFA = "boostops.hashed_idfa";
        public const string RAW_IDFA_CACHE = "boostops.raw_idfa_cache"; // For detecting resets
        public const string MIN_TRACKING_AMOUNT_CENTS = "boostops.min_tracking_amount_cents";
        public const string USER_PROPERTY_PREFIX = "boostops.user_property."; // Append property key
        
        // Analytics Service Control
        public const string ANALYTICS_DISABLED = "boostops.analytics_disabled";
        public const string ANALYTICS_BACKOFF_UNTIL = "boostops.analytics_backoff_until";
        public const string ANALYTICS_DISABLE_REASON = "boostops.analytics_disable_reason";
        
        // Purchase Tracking
        public const string HAS_MADE_PURCHASE = "boostops.has_made_purchase";
        
        // Legacy Keys (for migration/cleanup only - DO NOT USE in new code)
        // PascalCase format (pre-v1.0)
        private const string LEGACY_V0_SESSION_COUNT = "BoostOps_SessionCount";
        private const string LEGACY_V0_CURRENT_SESSION_ID = "BoostOps_CurrentSessionId";
        private const string LEGACY_V0_LAST_APP_VERSION = "BoostOps_LastAppVersion";
        private const string LEGACY_V0_FIRST_LAUNCH_TIME = "BOOSTOPS_FIRST_LAUNCH_TIME";
        private const string LEGACY_V0_FIRST_LAUNCH_TRACKED = "BoostOps_FirstLaunchTracked";
        private const string LEGACY_V0_FIRST_LAUNCH_DATE = "BoostOps_FirstLaunchDate";
        private const string LEGACY_V0_ATTRIBUTION = "BoostOps_Attribution";
        private const string LEGACY_V0_BOOSTOPS_ID = "BoostOps_ID";
        private const string LEGACY_V0_INSTALL_ID = "BoostOps_InstallID";
        private const string LEGACY_V0_APP_ACCOUNT_TOKEN = "BoostOps_AppAccountToken";
        private const string LEGACY_V0_HASHED_DEVICE_ID = "BoostOps_HashedDeviceId";
        private const string LEGACY_V0_HAS_LAUNCHED_BEFORE = "BoostOps_HasLaunchedBefore";
        private const string LEGACY_V0_CAPTURED_DEEP_LINK = "BoostOps_CapturedDeepLink";
        private const string LEGACY_V0_DEEP_LINK_TIMESTAMP = "BoostOps_DeepLinkTimestamp";
        private const string LEGACY_V0_INSTALL_REFERRER_RAW = "BoostOps_InstallReferrerRaw";
        private const string LEGACY_V0_ANALYTICS_DISABLED = "BoostOps_AnalyticsDisabled";
        private const string LEGACY_V0_ANALYTICS_BACKOFF_UNTIL = "BoostOps_BackoffUntil";
        private const string LEGACY_V0_ANALYTICS_DISABLE_REASON = "BoostOps_DisableReason";
        private const string LEGACY_V0_HAS_MADE_PURCHASE = "BoostOps_HasMadePurchase";
        // Pre-release format (before constants were added)
        private const string LEGACY_ATTRIBUTION = "boostops_attribution";
        private const string LEGACY_CONVERSION_HISTORY = "boostops_conversion_history";
        private const string LEGACY_CAPTURED_DEEP_LINK = "boostops_captured_deep_link";
        private const string LEGACY_DEEP_LINK_TIMESTAMP = "boostops_deep_link_timestamp";
        private const string LEGACY_INSTALL_REFERRER_RAW = "boostops_install_referrer_raw";
        private const string LEGACY_INSTALL_REFERRER_PROCESSED = "boostops_install_referrer_processed";
        private const string LEGACY_PENDING_ATTRIBUTION_CAMPAIGN = "pending_attribution_campaign";
        private const string LEGACY_PENDING_ATTRIBUTION_SOURCE = "pending_attribution_source";
        private const string LEGACY_HAS_LAUNCHED_BEFORE = "has_launched_before";
        
        /// <summary>
        /// Get impression data key (single key, date validated internally)
        /// </summary>
        /// <returns>Impression data key</returns>
        public static string GetImpressionKey()
        {
            return IMPRESSIONS_DATA;
        }
        
        /// <summary>
        /// Get click data key (single key, date validated internally)
        /// </summary>
        /// <returns>Click data key</returns>
        public static string GetClickKey()
        {
            return CLICKS_DATA;
        }
        
        /// <summary>
        /// Get user property key for a specific property
        /// </summary>
        /// <param name="propertyKey">Property key name (will be converted to lowercase)</param>
        /// <returns>Properly scoped user property key in format: boostops.user_property.key_name</returns>
        public static string GetUserPropertyKey(string propertyKey)
        {
            // Convert to lowercase and replace spaces with underscores for Unity convention
            string normalizedKey = propertyKey.ToLower().Replace(' ', '_').Replace('-', '_');
            return USER_PROPERTY_PREFIX + normalizedKey;
        }
        
        /// <summary>
        /// Migrate PlayerPrefs from legacy PascalCase format to Unity-style lowercase_snake_case
        /// Should be called once during SDK initialization
        /// </summary>
        public static void MigrateLegacyPlayerPrefs()
        {
            // Migrate from BoostOps_PascalCase → boostops.snake_case
            MigrateKey(LEGACY_V0_SESSION_COUNT, SESSION_COUNT);
            MigrateKey(LEGACY_V0_CURRENT_SESSION_ID, CURRENT_SESSION_ID);
            MigrateKey(LEGACY_V0_LAST_APP_VERSION, LAST_APP_VERSION);
            MigrateKey(LEGACY_V0_FIRST_LAUNCH_TIME, FIRST_LAUNCH_TIME);
            MigrateKey(LEGACY_V0_FIRST_LAUNCH_TRACKED, FIRST_LAUNCH_TRACKED);
            MigrateKey(LEGACY_V0_FIRST_LAUNCH_DATE, FIRST_LAUNCH_DATE);
            MigrateKey(LEGACY_V0_ATTRIBUTION, ATTRIBUTION_DATA);
            MigrateKey(LEGACY_V0_BOOSTOPS_ID, BOOSTOPS_ID);
            MigrateKey(LEGACY_V0_INSTALL_ID, INSTALL_ID);
            MigrateKey(LEGACY_V0_APP_ACCOUNT_TOKEN, APP_ACCOUNT_TOKEN);
            MigrateKey(LEGACY_V0_HASHED_DEVICE_ID, HASHED_DEVICE_ID);
            MigrateKey(LEGACY_V0_HAS_LAUNCHED_BEFORE, HAS_LAUNCHED_BEFORE);
            MigrateKey(LEGACY_V0_CAPTURED_DEEP_LINK, CAPTURED_DEEP_LINK);
            MigrateKey(LEGACY_V0_DEEP_LINK_TIMESTAMP, DEEP_LINK_TIMESTAMP);
            MigrateKey(LEGACY_V0_INSTALL_REFERRER_RAW, INSTALL_REFERRER_RAW);
            MigrateKey(LEGACY_V0_ANALYTICS_DISABLED, ANALYTICS_DISABLED);
            MigrateKey(LEGACY_V0_ANALYTICS_BACKOFF_UNTIL, ANALYTICS_BACKOFF_UNTIL);
            MigrateKey(LEGACY_V0_ANALYTICS_DISABLE_REASON, ANALYTICS_DISABLE_REASON);
            MigrateKey(LEGACY_V0_HAS_MADE_PURCHASE, HAS_MADE_PURCHASE);
            
            PlayerPrefs.Save();
            
            Debug.Log("[BoostOps] PlayerPrefs migration from legacy format completed");
        }
        
        /// <summary>
        /// Helper method to migrate a single key
        /// </summary>
        private static void MigrateKey(string oldKey, string newKey)
        {
            // If new key already exists, don't overwrite it
            if (PlayerPrefs.HasKey(newKey))
            {
                return;
            }
            
            // If old key doesn't exist, nothing to migrate
            if (!PlayerPrefs.HasKey(oldKey))
            {
                return;
            }
            
            // Try all possible types since PlayerPrefs doesn't have a type check
            try
            {
                // Try string first (most common) - GetString returns "" for non-string keys
                string stringValue = PlayerPrefs.GetString(oldKey, "__BOOSTOPS_NOT_FOUND__");
                if (stringValue != "__BOOSTOPS_NOT_FOUND__")
                {
                    // Empty string IS a valid value - migrate it
                    PlayerPrefs.SetString(newKey, stringValue);
                    PlayerPrefs.DeleteKey(oldKey);
                    Debug.Log($"[BoostOps] Migrated string: {oldKey} → {newKey} (value: '{stringValue}')");
                    return;
                }
                
                // Try int (GetInt returns 0 by default, so use a sentinel value)
                int intValue = PlayerPrefs.GetInt(oldKey, int.MinValue);
                if (intValue != int.MinValue)
                {
                    PlayerPrefs.SetInt(newKey, intValue);
                    PlayerPrefs.DeleteKey(oldKey);
                    Debug.Log($"[BoostOps] Migrated int: {oldKey} → {newKey} (value: {intValue})");
                    return;
                }
                
                // Try float (GetFloat returns 0.0 by default, so use a sentinel value)
                float floatValue = PlayerPrefs.GetFloat(oldKey, float.MinValue);
                if (!float.IsNaN(floatValue) && floatValue != float.MinValue)
                {
                    PlayerPrefs.SetFloat(newKey, floatValue);
                    PlayerPrefs.DeleteKey(oldKey);
                    Debug.Log($"[BoostOps] Migrated float: {oldKey} → {newKey} (value: {floatValue})");
                    return;
                }
                
                // If we get here, we couldn't determine the type - just delete the old key
                Debug.LogWarning($"[BoostOps] Could not determine type for {oldKey} - deleting old key");
                PlayerPrefs.DeleteKey(oldKey);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to migrate key {oldKey}: {e.Message}");
            }
        }
        
        /// <summary>
        /// Clean up all BoostOps PlayerPrefs (for testing/reset purposes)
        /// </summary>
        public static void ClearAllBoostOpsPlayerPrefs()
        {
            // Session & Day Tracking
            PlayerPrefs.DeleteKey(SESSION_COUNT);
            PlayerPrefs.DeleteKey(CURRENT_SESSION_ID);
            PlayerPrefs.DeleteKey(LAST_APP_VERSION);
            PlayerPrefs.DeleteKey(FIRST_LAUNCH_TIME);
            PlayerPrefs.DeleteKey(FIRST_LAUNCH_TRACKED);
            PlayerPrefs.DeleteKey(FIRST_LAUNCH_DATE);
            
            // Attribution & Install Tracking  
            PlayerPrefs.DeleteKey(ATTRIBUTION_DATA);
            PlayerPrefs.DeleteKey(CONVERSION_HISTORY);
            PlayerPrefs.DeleteKey(HAS_LAUNCHED_BEFORE);
            PlayerPrefs.DeleteKey(PENDING_ATTRIBUTION_CAMPAIGN);
            PlayerPrefs.DeleteKey(PENDING_ATTRIBUTION_SOURCE);
            
            // Deep Link Protection
            PlayerPrefs.DeleteKey(CAPTURED_DEEP_LINK);
            PlayerPrefs.DeleteKey(DEEP_LINK_TIMESTAMP);
            
            // Install Referrer
            PlayerPrefs.DeleteKey(INSTALL_REFERRER_RAW);
            PlayerPrefs.DeleteKey(INSTALL_REFERRER_PROCESSED);
            PlayerPrefs.DeleteKey(INSTALL_REFERRER_CLICK_TS);
            PlayerPrefs.DeleteKey(INSTALL_REFERRER_INSTALL_BEGIN_TS);
            
            // Revenue Tracking Settings
            PlayerPrefs.DeleteKey(AUTO_REVENUE_TRACKING);
            PlayerPrefs.DeleteKey(RECEIPT_VALIDATION);
            PlayerPrefs.DeleteKey(ATTRIBUTION_TRACKING);
            PlayerPrefs.DeleteKey(MIN_TRACKING_AMOUNT_CENTS);
            PlayerPrefs.DeleteKey(APP_ACCOUNT_TOKEN);
            PlayerPrefs.DeleteKey(HASHED_DEVICE_ID);
            PlayerPrefs.DeleteKey(ANALYTICS_DISABLED);
            PlayerPrefs.DeleteKey(ANALYTICS_BACKOFF_UNTIL);
            PlayerPrefs.DeleteKey(ANALYTICS_DISABLE_REASON);
            PlayerPrefs.DeleteKey(HAS_MADE_PURCHASE);
            
            // Clean up impression and click data (single keys)
            PlayerPrefs.DeleteKey(IMPRESSIONS_DATA);
            PlayerPrefs.DeleteKey(CLICKS_DATA);
            
            // Clean up legacy keys (for migration) - using constants to ensure consistency
            // PascalCase format (v0.x)
            PlayerPrefs.DeleteKey(LEGACY_V0_SESSION_COUNT);
            PlayerPrefs.DeleteKey(LEGACY_V0_CURRENT_SESSION_ID);
            PlayerPrefs.DeleteKey(LEGACY_V0_LAST_APP_VERSION);
            PlayerPrefs.DeleteKey(LEGACY_V0_FIRST_LAUNCH_DATE);
            PlayerPrefs.DeleteKey(LEGACY_V0_ATTRIBUTION);
            PlayerPrefs.DeleteKey(LEGACY_V0_BOOSTOPS_ID);
            PlayerPrefs.DeleteKey(LEGACY_V0_INSTALL_ID);
            PlayerPrefs.DeleteKey(LEGACY_V0_CAPTURED_DEEP_LINK);
            PlayerPrefs.DeleteKey(LEGACY_V0_DEEP_LINK_TIMESTAMP);
            PlayerPrefs.DeleteKey(LEGACY_V0_INSTALL_REFERRER_RAW);
            // Pre-release format
            PlayerPrefs.DeleteKey(LEGACY_ATTRIBUTION);
            PlayerPrefs.DeleteKey(LEGACY_CONVERSION_HISTORY);
            PlayerPrefs.DeleteKey(LEGACY_CAPTURED_DEEP_LINK);
            PlayerPrefs.DeleteKey(LEGACY_DEEP_LINK_TIMESTAMP);
            PlayerPrefs.DeleteKey(LEGACY_INSTALL_REFERRER_RAW);
            PlayerPrefs.DeleteKey(LEGACY_INSTALL_REFERRER_PROCESSED);
            PlayerPrefs.DeleteKey(LEGACY_PENDING_ATTRIBUTION_CAMPAIGN);
            PlayerPrefs.DeleteKey(LEGACY_PENDING_ATTRIBUTION_SOURCE);
            PlayerPrefs.DeleteKey(LEGACY_HAS_LAUNCHED_BEFORE);
            
            PlayerPrefs.Save();
        }
    }
} 