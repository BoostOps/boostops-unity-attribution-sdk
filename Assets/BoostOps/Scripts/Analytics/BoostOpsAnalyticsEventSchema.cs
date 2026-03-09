using System;
using System.Collections.Generic;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Analytics event data structure for API requests (Comprehensive Identifier Schema)
    /// Matches BoostOps comprehensive identifier specification for maximum attribution coverage
    /// 
    /// Schema Version History:
    /// - v1: Initial release with core event tracking (impression, click, app_open, purchase)
    /// - v2: Added attribution fields (attribution_channel, attribution_campaign_slug, attribution_campaign, 
    ///       is_reengagement, attribution_model, touch_type, touch_ts) and channel parameter for cross-promo events
    /// - v3: Replaced source_project/target_project with source_store_id/source_project_id/target_store_id/target_project_id; 
    ///       added screen_width/screen_height for device identification
    /// - v4: Unity-style PlayerPrefs keys (boostops.snake_case dot notation); Client-only boostops_id generation (never from server);
    ///       Triple-redundant ID storage (memory cache + native storage + PlayerPrefs fallback); Fixed iOS Keychain memory corruption bug;
    ///       Fixed Android SharedPreferences fallback logic; Added automatic migration from legacy PascalCase keys
    /// - v5: CRITICAL FIX - Fixed first_open PlayerPrefs key inconsistency (was using both legacy "BoostOps_FirstLaunchTracked" 
    ///       and new "boostops.first_launch_tracked" causing first_open=true on every session instead of only first install);
    ///       All first launch tracking now uses consistent BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED constant
    /// - v6: Added elapsed_realtime_ms (monotonic clock) at top-level for fraud detection and clock tampering detection;
    ///       Promoted install_id and custom_user_id to top-level (four-tier ID hierarchy: boostops_id, install_id, custom_user_id, session_id);
    ///       Simplified install_id format (32 hex chars, no dashes);
    ///       Added install_time_ms (Unix milliseconds) for SDK migration detection vs true new installs;
    ///       All timestamps in milliseconds for consistency (timestamp_ms, elapsed_realtime_ms, install_time_ms)
    /// - v7: Schema version increment for production release
    /// 
    /// Identifier Organization (Schema v7):
    /// - Top Level: Four-tier ID hierarchy (boostops_id, install_id, install_time_ms, custom_user_id, session_id) - universal correlation identifiers
    /// - Context Object: Device-stable identifiers (app_account_token, idfv, idfa, asid_sha256b64, gaid, firebase_app_id)
    /// - Event Level: Install-time identifiers (asa_token, skan_source_id, install_referrer_click_id) - install events only
    /// </summary>
    [Serializable]
    public class AnalyticsEventData
    {
        // Top-level event metadata
        public string event_type;                    // Required: boostops_open (with first_open=true for installs), boostops_impression, boostops_click, boostops_purchase, etc.
        public int schema_version;                   // Schema version for safe evolution (1, 2, 3...)
        public long timestamp_ms;                    // HIGH PRECISION: Unix timestamp in milliseconds when event occurred (wall clock, can be manipulated)
        public long? elapsed_realtime_ms;            // MONOTONIC CLOCK: Milliseconds since device boot (tamper-proof, matches Firebase naming)
        public string event_id;                      // Unique event identifier (generated once, never changes) - for database UNIQUE INDEX (project_id, event_id)
        public string nonce;                         // Per-attempt nonce (regenerated on retry) - for network replay attack prevention at edge
        
        // Universal correlation identifiers (four-tier ID hierarchy)
        public string boostops_id;                   // Primary BoostOps ID (ULID: boid_XXXXXXXXXXXXXXXXXXXXXXXX) - persistent user identity
        public string install_id;                    // Installation ID (32 hex chars, no dashes) - per-app installation tracking (resets on uninstall)
        public long? install_time_ms;                // App install timestamp (Unix milliseconds) - when app was first installed (for SDK migration detection)
        public string custom_user_id;                // Developer-provided custom user identifier - for app-specific user tracking
        public string session_id;                    // Session identifier (sess_XXXXXXXX) - per-session tracking
        // Note: project_key is sent in HTTP header (BoostOps-Project-Key) ONLY, not in payload for security
        // Note: storefront_country moved to context (environmental data)
        
        // TOP-LEVEL: Critical routing flags (for Cloudflare edge routing test vs production)
        /// <summary>Is running in Unity Editor (development/testing in Unity IDE)</summary>
        public bool? is_unity_editor;                // Unity Editor detection
        
        /// <summary>Android: Is this a debuggable build (FLAG_DEBUGGABLE). Always false on iOS.</summary>
        public bool? is_debug_build;                 // Android debug detection
        
        /// <summary>iOS: Is this a TestFlight build. Always false on Android.</summary>
        public bool? is_testflight;                  // iOS TestFlight detection
        
        /// <summary>Is running on emulator/simulator (both platforms)</summary>
        public bool? is_emulator;                    // Emulator detection
        
        // Privacy Consent Tracking (top-level for compliance)
        public ConsentData consent;                  // User privacy consent status
        
        // Event context (platform, environment, and device-stable identifiers)
        public EventContext context;                 // Platform info + device-stable IDs
        
        // Event-specific data (JSONB field in database, includes install-time IDs)
        public EventData @event;                     // All event-specific fields go here
    }
    
    /// <summary>
    /// Event context containing platform, environment, and device-stable identifiers
    /// Device-stable IDs are included here to keep payloads lean by avoiding repetition across events
    /// NOTE: Context timestamp uses seconds precision (different from top-level milliseconds)
    /// </summary>
    [Serializable]
    public class EventContext
    {
        // Platform and environment information
        public string source;                        // "sdk", "router", "server"
        public string platform;                      // "ios", "android", "unity"
        public string os_version;                    // Platform version (iOS 17.1.2, Android 14)
        public string app_version;                   // Customer's app version
        public string app_identifier;                // Application bundle ID (com.company.appname)
        public string sdk_version;                   // BoostOps SDK version
        public string store;                         // "ios", "google", "amazon", "huawei"
        public string store_id;                      // Platform-specific store identifier (iOS: App Store ID, Android/Amazon/Samsung: package name, Windows: store ID)
        public string device_model;                  // "iPhone15,2", "SM-G998U"
        public string device_brand;                  // "Apple", "Samsung"
        
        // Environment detection (detailed metadata for analytics)
        /// <summary>App environment: "production", "testflight", "google_play", "development", "emulator", "sideload"</summary>
        public string environment;                   // Overall environment classification
        
        /// <summary>Android: Installer package ("google_play", "sideload", "amazon_appstore"). iOS: always "app_store"</summary>
        public string installer_source;              // Who installed the app
        
        public string country;                       // ISO 3166-1 alpha-2: "US", "DE", "JP" (device/locale country)
        public string storefront_country;            // App Store/Play Store account country (e.g., "US", "GB", "JP")
        public string region;                        // State/province: "CA", "NY"
        public string city;                          // "San Francisco"
        public int? timezone_offset_minutes;         // Timezone offset in minutes: -480 for PST, -420 for PDT
        public string locale;                        // Device locale: "en_US", "es_MX", "pt_BR"
        public string language;                      // Device language (ISO 639-1): "en", "es", "zh"
        public string carrier;                       // "Verizon", "T-Mobile"
        public string connection_type;               // "wifi", "cellular"
        public string ip_address;                    // Client IP address
        // Note: timestamp is at top-level (milliseconds precision) - not duplicated in context
        
        // Device-stable identifiers (cross-app correlation, every call)
        // Note: install_id and custom_user_id moved to top-level (schema v6)
        
        /// <summary>iOS App Account Token - deterministic join for ASA install ⇔ StoreKit receipts</summary>
        public string app_account_token;             // iOS only, UUID format
        
        /// <summary>iOS Identifier for Vendor - backup join key inside iOS app portfolio</summary>
        public string idfv;                          // iOS only, used for cross-promo within same developer
        
        /// <summary>iOS Identifier for Advertising - only when ATT opt-in granted</summary>
        public string idfa;                          // iOS only, omit when empty/unavailable
        
        /// <summary>Android App-Set-ID hash - survives GAID deprecation, developer-scoped</summary>
        public string asid_sha256;                   // Android only, SHA-256 Base64url encoded
        
        /// <summary>Google Advertising ID - deterministic ad-network joins until sunset</summary>
        public string gaid;                          // Android only, until Google sunsets GAID
        
        /// <summary>Firebase App Instance ID - links GA4 events to Google Ads SKAN schema</summary>
        public string firebase_app_id;               // If GA4 integration present, both platforms
        
        /// <summary>Windows device ID (Unity SystemInfo.deviceUniqueIdentifier - MD5 of hardware serials)</summary>
        public string windows_device_id;             // Windows Standalone only, Unity-computed hash
        
        /// <summary>Windows Machine GUID (HKLM Cryptography key - stable per OS install, cross-app visible)</summary>
        public string windows_machine_guid;          // Windows Standalone only, analogous to IDFV
        
        /// <summary>Windows Advertising ID (user-resettable, from registry)</summary>
        public string msaid;                         // Windows Standalone only, null if user disabled
    }
    
    /// <summary>
    /// Event-specific data (flexible JSONB structure)
    /// Includes install-time identifiers for first session events only to minimize bandwidth
    /// </summary>
    [Serializable] 
    public class EventData
    {
        // Session tracking (industry standard approach)
        /// <summary>True if this is the first session after app install (replaces separate install event)</summary>
        public bool? first_open;                     // Industry standard field for first session detection
        
        // App Open / Session Start Parameters (Industry Standard)
        /// <summary>How the app was launched: "cold" (first open in memory), "warm" (resume from background). Use entry_point to determine if via deeplink.</summary>
        public string launch_type;                   // cold | warm
        
        /// <summary>Entry point that triggered the app open (what caused the launch)</summary>
        public string entry_point;                   // icon | universal_link | url_scheme | push | widget | siri | 3d_touch | spotlight | handoff | nfc | other
        
        /// <summary>Reason for this session starting</summary>
        public string session_reason;                // resume | first_launch | upgrade | os_restart
        
        /// <summary>Whether app version was updated since last launch</summary>
        public bool? app_version_updated;            // true/false
        
        /// <summary>Previous app version (when app_version_updated=true)</summary>
        public string previous_app_version;          // e.g., "2.0.7"
        
        // Note: network_type is in context.connection_type (universal) - not duplicated here
        // Note: country is in context.country (universal) - not duplicated here
        // Note: locale is in context.locale (universal) - not duplicated here
        // Note: language is in context.language (universal) - not duplicated here
        // Note: timezone_offset_minutes is in context (universal) - not duplicated here
        
        /// <summary>Screen width in pixels (for device identification)</summary>
        public int? screen_width;                    // e.g., 1170 for iPhone 13
        
        /// <summary>Screen height in pixels (for device identification)</summary>
        public int? screen_height;                   // e.g., 2532 for iPhone 13
        
        /// <summary>Push notification permission status</summary>
        public string push_permission;               // authorized | denied | not_determined
        
        /// <summary>Whether notifications are enabled</summary>
        public bool? notifications_enabled;          // true/false
        
        /// <summary>Device orientation at launch</summary>
        public string device_orientation;            // portrait | landscape
        
        /// <summary>Whether device is in battery saver mode</summary>
        public bool? battery_saver;                  // true/false
        
        /// <summary>Whether device is in low power mode (iOS)</summary>
        public bool? low_power_mode;                 // true/false (iOS only)
        
        // Deeplink & Campaign Attribution (when present)
        /// <summary>Normalized deeplink and UTM parameters</summary>
        public DeeplinkData deeplink;                // Structured deeplink attribution data
        
        /// <summary>Google Play Install Referrer data (Android first session only)</summary>
        public PlayInstallReferrerData play_install_referrer;  // Android attribution data
        
        // Install-time identifiers (included only in first session events)
        /// <summary>Apple Search Ads attribution token - first session events only</summary>
        public string asa_token;                     // iOS install attribution data
        
        /// <summary>SKAN source identifier - first session events only if captured</summary>
        public string skan_source_id;                // 4-digit SKAN source code
        
        /// <summary>Google Play Install Referrer click ID - first session events only</summary>
        public string install_referrer_click_id;     // Android attribution data
        
        /// <summary>Attribution click ID from install referrer/attribution API - first_open events only</summary>
        public string attribution_click_id;          // Universal attribution click identifier across platforms
        
        // Attribution & Identity
        public string user_id;                       // Customer's internal user ID
        
        // Cross-Promotion Attribution (CRITICAL for BoostOps)
        // Note: source_store_id is available in context.store_id (universal) - not duplicated here
        // Note: source_project_id is derived server-side from project_key (not sent from SDK)
        
        /// <summary>Store ID of the target app (iOS: App Store ID, Android: package name)</summary>
        public string target_store_id;               // Store ID of app being promoted (e.g., "1234567890" or "com.example.targetapp")
        
        /// <summary>BoostOps project ID of the target app</summary>
        public string target_project_id;             // BoostOps project ID of app being promoted (e.g., "bo_prod_xyz789")
        
        public string network_campaign_id;          // Cross-promo campaign ID
        public string placement_id;                  // Where ad appears in source app
        
        // Campaign attribution
        public int? campaign_id;                     // Numeric campaign ID for joins
        public string campaign_slug;                 // Human-readable campaign name
        public int? ad_group_id;                     // Ad group identifier
        public int? creative_id;                     // Creative/ad identifier
        public string keyword;                       // Search keyword (if applicable)
        
        // App context (for current event)
        public string project_slug;                  // Current app identifier
        
        // Revenue & Commerce (ALL AMOUNTS IN MICROS)
        public string currency;                      // ISO 4217 currency code: "USD", "EUR"
        public long? amount_micros;                  // $9.99 = 9,990,000 micros
        public long? tax_micros;                     // Tax amount in micros
        public long? discount_micros;                // Discount amount in micros
        public string product_id;                    // Product SKU/identifier
        public string product_name;                  // Human-readable product name
        public string product_category;              // Product category/type
        public int? quantity;                        // Number of items
        public string transaction_id;                // Transaction identifier (store order/transaction ID)
        public string original_transaction_id;       // iOS subscription renewal tracking
        public string receipt;                       // Store receipt or purchase token for server-side validation
        public bool? is_trial;                       // Is this a trial purchase
        public bool? is_subscription;                // Is this a subscription
        public string billing_period;                // "monthly", "yearly"
        public int? renewal_number;                  // Which renewal (if subscription)
        
        // NEW: iOS Subscription Metadata (Industry Standard)
        /// <summary>ISO 8601 duration format for subscription period (P1M = 1 month, P1Y = 1 year, etc)</summary>
        public string subscription_period;           // e.g., "P1M", "P1Y", "P1W"
        
        /// <summary>Introductory offer price in micros (for trial or discounted periods)</summary>
        public long? introductory_price_micros;
        
        /// <summary>Number of introductory price billing cycles</summary>
        public int? introductory_price_cycles;
        
        // Purchase History Hints (for segmentation and LTV analysis)
        /// <summary>True if this is the user's first purchase ever in this app</summary>
        public bool? first_purchase;                 // True for first purchase, false for repeat purchases
        
        /// <summary>Total number of purchases user has made (including this one)</summary>
        public int? purchase_count;                  // 1 for first purchase, 2 for second, etc.
        
        // Cross-promotion specific (Impression/Click Events)
        /// <summary>Unique impression identifier for linking impression ↔ click events</summary>
        public string impression_id;                 // UUID minted at impression render time (e.g., "imp_01KA9T4BVGG0GGSEWSQNE0FRGD")
        /// <summary>Container impression ID for app walls - groups multiple item impressions together</summary>
        public string container_impression_id;       // UUID for the container (e.g., app wall view) - ETL explodes items into individual impressions
        public string placement;                     // Where ad was shown (legacy - use placement_id)
        public string format;                        // "banner", "interstitial", "video", "native", "app_wall"
        public string channel;                       // Marketing channel: "xpromo", "paid", "organic", etc.
        public int? duration_ms;                     // How long shown (milliseconds)
        
        // App Wall specific (nested items array for app_wall_impression events)
        public List<Dictionary<string, object>> items;  // Array of items displayed in app wall
        public int? position;                        // Position/index of item in container (0-based)
        public bool? viewable;                       // Was impression viewable
        public bool? above_fold;                     // Was above the fold
        public float? completion_rate;               // Video completion rate (0.0-1.0)
        
        // Click specific
        public string click_id;                      // Unique click identifier (for deterministic Android attribution via Play referrer)
        public ClickCoordinates click_coordinates;   // Where user clicked
        public int? time_to_click_ms;                // Time from impression to click (calculated from impression_timestamp)
        public long? impression_timestamp;           // Timestamp of original impression (for time_to_click calculation)
        public string referrer;                      // Where click came from
        public float? click_through_rate;            // CTR for this creative
        
        // Cross-App Navigation (Click Events)
        public string deep_link_url;                 // Deep link used for cross-app navigation
        public string redirect_url;                  // Short link for app store redirect
        public bool? store_redirect;                 // Did we redirect to store
        public int? attribution_window_hours;        // Attribution window in hours
        
        // Revenue Context (Cross-Promotion)
        public float? revenue_share_rate;            // Revenue share rate (0.0-1.0)
        public long? estimated_cpm_micros;           // Estimated CPM in micros
        public long? impression_value_micros;        // Per impression value in micros
        public long? click_value_micros;             // Per click value in micros
        
        // Install specific
        public bool? organic;                        // Is organic install
        public bool? reinstall;                      // Is this a reinstall
        public long? install_size_bytes;             // App size in bytes
        public int? install_duration_ms;             // Time to install
        
        // Attribution update specific
        public string attribution_source;            // Source of attribution update: "skadnetwork", "play_referrer", etc.
        public string attribution_method;            // "deterministic", "probabilistic"
        public float? attribution_confidence;       // Attribution confidence (0.0-1.0)
        
        // Attribution for Lifecycle Events (App Open, Purchase, etc.)
        public string attribution_channel;           // Channel of last eligible touch: "xpromo", "ua:facebook", "email", "organic", "unknown"
        public string attribution_campaign_slug;     // Campaign slug from the winning touch (stable join key)
        public string attribution_campaign;          // Human-readable campaign label (optional)
        public bool? is_reengagement;                // True if touch happened after install (vs install-source)
        public string attribution_model;             // Attribution model used: "last_touch", "first_touch", etc.
        public string touch_type;                    // Type of winning touch: "click", "impression"
        public long? touch_ts;                       // Timestamp of the winning touch (Unix seconds)
        
        // App open specific (legacy fields - use new industry standard fields above)
        // Note: deep_link_url is defined above in "Cross-App Navigation" section (line 287)
        public long? time_since_install_ms;          // Time since install
        
        // Privacy & Device Identifiers (hashed with algorithm prefix)
        public string idfa_hash;                     // "sha256:a1b2c3d4..."
        public string idfv_hash;                     // "sha256:f6e5d4c3..."
        public string gaid_hash;                     // "sha256:9z8y7x6w..."
        public string android_id_hash;               // "sha256:4u5v6w7x..."
        public string custom_user_id;                // Customer's own user ID
        public string fingerprint_hash;              // Device fingerprint
        
        // Platform-Specific Attribution
        public AppleAttributionData apple_search_ads;     // Apple Search Ads
        public SKANData skan;                        // SKAdNetwork data
        public AAKData aak;                          // AdAttributionKit (iOS 17.4+)
        // play_install_referrer moved to industry standard section above
        public GoogleAdsData google_ads;             // Google Ads attribution
        
        // Custom Data Namespacing
        public CustomData custom_data;               // Game, ecommerce, SaaS specific data
    }
    
    // Supporting data structures for complex fields
    
    [Serializable]
    public class ClickCoordinates
    {
        public int x;
        public int y;
    }
    
    [Serializable]
    public class ConsentData
    {
        // Framework identification (backward compatible)
        public string framework;                     // "tcf_v2", "ccpa", "custom", "gdpr", "lgpd", "pipeda"
        public long? consent_timestamp;              // Unix timestamp when given
        public string consent_method;                // "banner", "settings", "implied", "modal"
        public string consent_string;                // TCF string or equivalent
        
        // Platform-specific consent (existing structure)
        public GDPRConsent gdpr;                     // GDPR specific consent
        public ATTConsent att;                       // Apple ATT consent
        public AndroidPrivacy android;               // Android privacy settings
        
        // Enhanced consent tracking (new fields)
        public string consent_version;               // Version of consent policy user agreed to
        public string consent_language;              // Language consent was presented in ("en", "de", "fr")
        public string consent_source;                // "first_launch", "settings_page", "privacy_update"
        public string legal_basis;                   // "consent", "legitimate_interest", "contract", "legal_obligation"
        
        // Framework requirements (automatic detection)
        public bool? gdpr_consent_required;          // Is GDPR consent required for this user/region?
        public bool? ccpa_consent_required;          // Is CCPA consent required for this user/region?
        
        // Withdrawal tracking
        public long? withdrawal_timestamp;           // When consent was withdrawn (if applicable)
        public string withdrawal_method;             // How consent was withdrawn
    }
    
    [Serializable]
    public class GDPRConsent
    {
        public bool? applies;                        // Is GDPR applicable
        public bool? consent_given;                  // Overall consent status
        public bool? analytics;                      // Analytics purpose consent
        public bool? advertising;                    // Advertising purpose consent
        public bool? measurement;                    // Measurement purpose consent
        public string legal_basis;                   // "consent", "legitimate_interest"
    }
    
    [Serializable]
    public class ATTConsent
    {
        public string status;                        // "authorized", "denied", "restricted"
        public long? authorized_time;                // When tracking authorized
        public bool? idfa_available;                 // Can access IDFA
    }
    
    [Serializable]
    public class AndroidPrivacy
    {
        public bool? advertising_id;                 // GAID consent
        public bool? limited_ad_tracking;            // LAT preference
    }
    
    [Serializable]
    public class AppleAttributionData
    {
        public string token;                         // "3.1.AdServices.AttributionToken..."
        public long? campaign_id;                    // Apple campaign ID
        public long? ad_group_id;                    // Apple ad group ID
        public long? keyword_id;                     // Apple keyword ID
        public long? creative_set_id;                // Apple creative set ID
    }
    
    [Serializable]
    public class SKANData
    {
        public string version;                       // "4.0"
        public int? postback_sequence;               // 1st, 2nd, or 3rd postback
        public int? conversion_value;                // 0-63 encoded value
        public string coarse_value;                  // "low", "medium", "high"
        public string source_identifier;             // 4-digit hierarchy
        public int? fidelity_type;                   // 1=click, 0=view
        public bool? lock_window;                    // CV locked for window
        public bool? redownload;                     // Is reinstall
        public long? campaign_id;                    // Apple campaign ID
        public string attribution_signature;        // Apple crypto proof
        public long? postback_timestamp;             // When postback received
    }
    
    [Serializable]
    public class AAKData
    {
        public string conversion_type;               // "install", "reengagement"
        public string marketplace_identifier;       // "com.apple.app"
        public int? attribution_window;              // Attribution window seconds
        public int? cooldown_window;                 // Re-engagement cooldown
    }
    
    // PlayInstallReferrerData class moved to industry standard section below
    
    [Serializable]
    public class GoogleAdsData
    {
        public string campaign_id;                   // Google Ads campaign ID
        public string ad_group_id;                   // Google Ads ad group ID
        public string creative_id;                   // Google Ads creative ID
        public string keyword;                       // Search keyword
        public string match_type;                    // "broad", "exact", "phrase"
    }
    
    [Serializable]
    public class CustomData
    {
        public GameData game;                        // Game-specific fields
        public EcommerceData ecommerce;              // E-commerce specific fields
        public SaaSData saas;                        // SaaS specific fields
    }
    
    [Serializable]
    public class GameData
    {
        public int? level;                           // Player level
        public string character;                     // Character type
        public string guild_id;                      // Guild membership
        public int? power_score;                     // Player power
        public long? last_login;                     // Last login timestamp
        public bool? tutorial_completed;             // Tutorial status
        public int? achievements_unlocked;           // Number of achievements
    }
    
    [Serializable]
    public class EcommerceData
    {
        public string cart_id;                       // Shopping cart ID
        public int? checkout_step;                   // Which checkout step
        public string shipping_method;               // Shipping type
        public string payment_method;                // Payment type
        public string coupon_code;                   // Coupon used
        public string category;                      // Product category
        public string brand;                         // Product brand
        public string variant;                       // Product variant
    }
    
    [Serializable]
    public class SaaSData
    {
        public string plan_tier;                     // Subscription tier
        public int? seat_count;                      // Number of seats
        public string billing_cycle;                 // "monthly", "annual"
        public int? trial_days_remaining;            // Days left in trial
        public FeatureUsage feature_usage;           // Feature usage stats
        public int? integration_count;               // Connected integrations
    }
    
    [Serializable]
    public class FeatureUsage
    {
        public int? api_calls;                       // API calls made
        public int? storage_gb;                      // Storage used in GB
        public int? users_active;                    // Active users
    }
    
    // Backend analytics data structures removed - these are calculated on BoostOps servers, not sent by SDK
    
    /// <summary>
    /// Normalized deeplink and UTM attribution data (industry standard)
    /// </summary>
    [Serializable]
    public class DeeplinkData
    {
        /// <summary>Original deeplink URL</summary>
        public string url;                           // Original deeplink URL
        
        /// <summary>Normalized scheme, host, and path</summary>
        public string scheme_host_path;              // e.g., "https://app.example.com/"
        
        /// <summary>UTM source parameter</summary>
        public string utm_source;                    // e.g., "xpromo", "ads", "organic"
        
        /// <summary>UTM medium parameter</summary>
        public string utm_medium;                    // e.g., "interstitial", "banner", "email"
        
        /// <summary>UTM campaign parameter</summary>
        public string utm_campaign;                  // e.g., "keno2_launch_q3"
        
        /// <summary>UTM term parameter</summary>
        public string utm_term;                      // e.g., "casino games"
        
        /// <summary>UTM content parameter</summary>
        public string utm_content;                   // e.g., "red_button"
        
        /// <summary>BoostOps click ID</summary>
        public string bo_click_id;                   // e.g., "clk_01JC7K0A1S4N4JH7P8"
        
        /// <summary>Third-party attribution click ID (MMP SDK B)</summary>
        public string branch_click_id;               // External attribution parameter
        
        /// <summary>Third-party attribution click ID (MMP SDK A)</summary>
        public string af_c_id;                       // External attribution parameter
        
        /// <summary>How the deeplink was matched</summary>
        public string matched_type;                  // universal_link | custom_scheme | web_to_app
        
        /// <summary>Whether this was a deferred deeplink</summary>
        public bool? is_deferred;                    // true/false
    }
    
    /// <summary>
    /// Google Play Install Referrer data (Android first session only)
    /// </summary>
    [Serializable]
    public class PlayInstallReferrerData
    {
        /// <summary>Install referrer string from Play Store</summary>
        public string referrer;                      // e.g., "utm_source=ads&utm_campaign=keno2"
        
        /// <summary>Click timestamp (Unix seconds)</summary>
        public long? click_ts;                       // When user clicked the ad
        
        /// <summary>Install begin timestamp (Unix seconds)</summary>
        public long? install_begin_ts;               // When install started
        
        /// <summary>Click ID from referrer (if present)</summary>
        public string click_id;                      // gclid or partner click ID
    }
    
    /// <summary>
    /// Currency conversion utility for handling micros
    /// </summary>
    public static class CurrencyMicros
    {
        private const long MICROS_PER_UNIT = 1_000_000L;
        
        /// <summary>
        /// Convert dollar amount to micros
        /// Example: $9.99 -> 9,990,000 micros
        /// </summary>
        public static long ToMicros(decimal amount)
        {
            return (long)(amount * MICROS_PER_UNIT);
        }
        
        /// <summary>
        /// Convert micros back to decimal amount
        /// Example: 9,990,000 micros -> $9.99
        /// </summary>
        public static decimal FromMicros(long micros)
        {
            return (decimal)micros / MICROS_PER_UNIT;
        }
        
        /// <summary>
        /// Convert float amount to micros with rounding
        /// </summary>
        public static long ToMicros(float amount)
        {
            return (long)Math.Round(amount * MICROS_PER_UNIT);
        }
        
        /// <summary>
        /// Convert double amount to micros with rounding
        /// </summary>
        public static long ToMicros(double amount)
        {
            return (long)Math.Round(amount * MICROS_PER_UNIT);
        }
    }
    
    /// <summary>
    /// Event field validation utility
    /// </summary>
    public static class EventValidation
    {
        /// <summary>
        /// Validate required fields by event type according to schema
        /// </summary>
        public static bool ValidateRequiredFields(string eventType, EventData eventData)
        {
            switch (eventType)
            {
                case BoostOpsAnalyticsContract.EventNames.IMPRESSION:
                    return !string.IsNullOrEmpty(eventData?.campaign_slug);
                    
                case BoostOpsAnalyticsContract.EventNames.CLICK:
                    return !string.IsNullOrEmpty(eventData?.campaign_slug);
                    
                case BoostOpsAnalyticsContract.EventNames.APP_OPEN:
                    return true; // All fields optional for app open (includes installs with first_open=true)
                    
                case BoostOpsAnalyticsContract.EventNames.PURCHASE:
                    return !string.IsNullOrEmpty(eventData?.currency) && 
                           eventData?.amount_micros.HasValue == true && 
                           !string.IsNullOrEmpty(eventData?.product_id);
                           
                case BoostOpsAnalyticsContract.EventNames.INSTALL_ATTRIBUTION_UPDATE:
                    // Note: boostops_id is now validated at top-level, not in event data
                    return !string.IsNullOrEmpty(eventData?.attribution_source);
                           
                default:
                    return true; // Unknown event types are valid by default
            }
        }
        
        /// <summary>
        /// Validate currency code format (ISO 4217)
        /// </summary>
        public static bool IsValidCurrency(string currency)
        {
            return !string.IsNullOrEmpty(currency) && 
                   currency.Length == 3 && 
                   currency == currency.ToUpper();
        }
        
        /// <summary>
        /// Validate country code format (ISO 3166-1 alpha-2)
        /// </summary>
        public static bool IsValidCountryCode(string country)
        {
            return !string.IsNullOrEmpty(country) && 
                   country.Length == 2 && 
                   country == country.ToUpper();
        }
    }
}