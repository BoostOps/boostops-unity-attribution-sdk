using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoostOps.Core
{
    /// <summary>
    /// Shared configuration models for parsing remote config JSON
    /// Used by both editor and runtime to ensure consistent parsing
    /// </summary>
    
    [Serializable]
    public class RemoteCampaignConfig
    {
        [SerializeField] public VersionInfo version_info;
        [SerializeField] public SourceProject source_project;
        [SerializeField] public List<Campaign> campaigns = new List<Campaign>();
        [SerializeField] public AppWallsConfig app_walls; // App wall configuration
    }

    [Serializable]
    public class VersionInfo
    {
        [SerializeField] public string api_version;
        [SerializeField] public string schema_version;
        [SerializeField] public string contract_version;
        [SerializeField] public string server_version;
        [SerializeField] public string client_min_version;
        [SerializeField] public string last_updated;
        [SerializeField] public string environment;
    }

    [Serializable]
    public class SourceProject
    {
        [SerializeField] public string bundle_id;
        [SerializeField] public string name;
        [SerializeField] public string project_id;
        [SerializeField] public int min_player_days;
        [SerializeField] public int min_sessions;
        [SerializeField] public FrequencyCapData frequency_cap;
        [SerializeField] public string interstitial_icon_cta;
        [SerializeField] public string interstitial_icon_text;
        [SerializeField] public string interstitial_rich_cta;
        [SerializeField] public string interstitial_rich_text;
        
        // Structured format (JsonUtility-compatible)
        [SerializeField] public StoreUrls store_urls;
        [SerializeField] public StoreIds store_ids;
        [SerializeField] public PlatformIds platform_ids;
    }

    [Serializable]
    public class StoreUrls
    {
        [SerializeField] public string apple;
        [SerializeField] public string google;
        [SerializeField] public string amazon;
        [SerializeField] public string microsoft;
        [SerializeField] public string samsung;
        [SerializeField] public string web;
        
        public bool HasAnyLinks()
        {
            return !string.IsNullOrEmpty(apple) || !string.IsNullOrEmpty(google) || 
                   !string.IsNullOrEmpty(amazon) || !string.IsNullOrEmpty(microsoft) || 
                   !string.IsNullOrEmpty(samsung);
        }
    }

    [Serializable]
    public class StoreIds
    {
        [SerializeField] public string apple;
        [SerializeField] public string google;
        [SerializeField] public string amazon;
        [SerializeField] public string microsoft;
        [SerializeField] public string samsung;
    }

    [Serializable]
    public class PlatformIds
    {
        [SerializeField] public string ios_bundle_id;
        [SerializeField] public string android_package_name;
    }

    [Serializable]
    public class FrequencyCapData
    {
        [SerializeField] public string time_unit;
        [SerializeField] public int impressions;
    }

    [Serializable]
    public class Campaign
    {
        [SerializeField] public string campaign_id;
        [SerializeField] public string name;
        [SerializeField] public string status;
        [SerializeField] public TargetProject target_project;
        [SerializeField] public FrequencyCapData frequency_cap;
        [SerializeField] public int min_sessions;
        [SerializeField] public int min_player_days;
        [SerializeField] public string created_at;
        [SerializeField] public string updated_at;
        [SerializeField] public string metadata;
        [SerializeField] public string[] formats; // Campaign formats: "native", "interstitial", "video", etc.

        /// <summary>
        /// Check if campaign has valid store URLs for cross-promotion
        /// </summary>
        public bool HasValidStoreUrl()
        {
            return target_project?.store_urls?.HasAnyLinks() == true;
        }
    }

    [Serializable]
    public class TargetProject
    {
        [SerializeField] public string project_id;
        [SerializeField] public StoreUrls store_urls;
        [SerializeField] public StoreIds store_ids;
        [SerializeField] public PlatformIds platform_ids;
        [SerializeField] public Creative[] creatives;
    }

    [Serializable]
    public class Creative
    {
        [SerializeField] public string format;
        [SerializeField] public bool prefetch;
        [SerializeField] public CreativeVariant[] variants;
        [SerializeField] public int ttl_hours;
        [SerializeField] public string creative_id;
        [SerializeField] public string orientation;
    }

    [Serializable]
    public class CreativeVariant
    {
        [SerializeField] public string url;
        [SerializeField] public string sha256;
        [SerializeField] public string local_key;
        [SerializeField] public string resolution;
    }
    
    // App Wall models
    [Serializable]
    public class AppWallsConfig
    {
        [SerializeField] public AppWallDefault @default; // Using @ to escape "default" keyword
    }
    
    [Serializable]
    public class AppWallDefault
    {
        [SerializeField] public AppWallApp[] items;  // Items displayed in the app wall
        [SerializeField] public object ab_test;      // A/B testing configuration (null if not testing)
        [SerializeField] public bool enabled;        // Whether this app wall is enabled
        [SerializeField] public object metadata;     // Free-form container metadata (extensible JSON object)
        [SerializeField] public object schedule;     // Schedule configuration (null if always active)
        [SerializeField] public object frequency;    // Frequency cap configuration (null if no cap)
        [SerializeField] public int max_shown;       // Maximum number of items to show
        [SerializeField] public object targeting;    // Targeting rules (null if no targeting)
        [SerializeField] public string sort_order;   // Sort order: "manual", "random", etc.
        [SerializeField] public string container_id; // Unique container identifier
    }
    
    [Serializable]
    public class AppWallApp
    {
        [SerializeField] public bool enabled;        // Whether this item is enabled
        [SerializeField] public object metadata;     // Free-form metadata JSON object (e.g., {"cross_platform_id": "com.example.app"})
        [SerializeField] public int position;        // Position in the app wall (0-based)
        [SerializeField] public Creative[] creatives;  // Creative assets (icon, hero, etc.)
        [SerializeField] public StoreIds store_ids;  // Store IDs for each platform
        [SerializeField] public StoreUrls store_urls;  // Store URLs for each platform
        [SerializeField] public string campaign_id;  // Deterministic campaign ID
        [SerializeField] public string campaign_slug;  // Campaign slug for analytics
        [SerializeField] public int display_order;   // Display order (may differ from position)
        [SerializeField] public string target_project_id;  // BoostOps project ID
        [SerializeField] public string target_project_name;  // Human-readable project name
        
        // Legacy fields (may be deprecated in future)
        [SerializeField] public string app_id;       // Optional app identifier
        [SerializeField] public string source_type;  // "organic" or "sponsored"
        
        /// <summary>
        /// Get the best icon creative for this app
        /// </summary>
        public Creative GetIconCreative()
        {
            if (creatives == null) return null;
            
            foreach (var creative in creatives)
            {
                if (creative != null && creative.format == "icon")
                    return creative;
            }
            
            return null;
        }
        
        /// <summary>
        /// Get the icon URL for current platform
        /// </summary>
        public string GetIconUrl()
        {
            var iconCreative = GetIconCreative();
            if (iconCreative == null || iconCreative.variants == null || iconCreative.variants.Length == 0)
                return null;
            
            // Return the first variant's URL (could be enhanced to pick best resolution)
            return iconCreative.variants[0]?.url;
        }
        
        /// <summary>
        /// Get local key for prefetched icon
        /// </summary>
        public string GetIconLocalKey()
        {
            var iconCreative = GetIconCreative();
            if (iconCreative == null || iconCreative.variants == null || iconCreative.variants.Length == 0)
                return null;
            
            return iconCreative.variants[0]?.local_key;
        }
        
        /// <summary>
        /// Get the appropriate store URL for current platform
        /// </summary>
        public string GetStoreUrl()
        {
            if (store_urls == null)
                return null;
            
#if UNITY_IOS
            return store_urls.apple;
#elif UNITY_ANDROID
            return store_urls.google ?? store_urls.amazon ?? store_urls.samsung;
#elif UNITY_WSA || UNITY_STANDALONE_WIN
            return store_urls.microsoft;
#elif UNITY_WEBGL
            return store_urls.web;
#else
            // Fallback
            return store_urls.apple ?? store_urls.google;
#endif
        }
        
        /// <summary>
        /// Get the appropriate store ID for current platform
        /// </summary>
        public string GetStoreId()
        {
            if (store_ids == null)
                return null;
            
#if UNITY_IOS
            return store_ids.apple;
#elif UNITY_ANDROID
            return store_ids.google ?? store_ids.amazon ?? store_ids.samsung;
#elif UNITY_WSA || UNITY_STANDALONE_WIN
            return store_ids.microsoft;
#else
            return store_ids.apple ?? store_ids.google;
#endif
        }
    }
    
    // Note: Campaign parsing now uses BoostOps.Core classes for JSON deserialization
    // Data is then converted to main BoostOps namespace classes for runtime use
    //
    // ⚠️ METADATA IS FREE-FORM JSON:
    // The "metadata" field in AppWallApp and AppWallDefault is a flexible object type
    // that can contain any JSON structure. Unity's JsonUtility will deserialize nested JSON
    // into this field. Common metadata examples include:
    //   - cross_platform_id: Shared identifier across platforms
    //   - cta_text: Custom call-to-action text
    //   - theme_color: Custom theme color
    //   - custom_tracking: Any app-specific metadata
    // 
    // Note: To access metadata values, you'll need to use reflection or cast to appropriate types,
    // or serialize back to JSON and parse with a more flexible library if complex access is needed.
}
