using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// FrequencyCap moved to internal DLL - no longer imported

namespace BoostOps
{
    /// <summary>
    /// Campaign parsing modes to control which formats are attempted
    /// </summary>
    public enum CampaignParsingMode
    {
        /// <summary>Try all formats including Unity Remote Settings (for server/production use)</summary>
        All,
        /// <summary>Skip Unity Remote Settings parsing, only try structured formats (for demo/local files)</summary>
        LocalOnly
    }

    /// <summary>
    /// Unity Remote Settings schema - simplified to use Campaign format directly
    /// No more dual formats - just use the comprehensive Campaign structure
    /// </summary>
    [Serializable]
    public class CrossPromoConfig
    {
        [SerializeField] public bool crossPromoEnabled = true;
        [SerializeField] public Campaign[] activeCampaigns = new Campaign[0];
        
        public bool CrossPromoEnabled => crossPromoEnabled;
        public Campaign[] ActiveCampaigns => activeCampaigns ?? new Campaign[0];
    }
    

    
    /// <summary>
    /// Unity Remote Settings wrapper for the crossPromoConfig
    /// </summary>
    [Serializable]
    public class UnityRemoteSettingsWrapper
    {
        [SerializeField] public CrossPromoConfig crossPromoConfig;
        
        public CrossPromoConfig CrossPromoConfig => crossPromoConfig;
    }

    /// <summary>
    /// Represents campaign scheduling configuration
    /// </summary>
    [Serializable]
    public class CampaignSchedule
    {
        [SerializeField] public string start_date; // ISO 8601 format
        [SerializeField] public string end_date;   // ISO 8601 format (optional)
        [SerializeField] public int[] days; // [1,2,3,4,5] for Mon-Fri (0=Sun, 6=Sat), empty = all days
        [SerializeField] public int start_hour = -1; // Optional hour (0-23), -1 = not set
        [SerializeField] public int end_hour = -1;   // Optional hour (0-23), -1 = not set
        
        // Helper properties
        public DateTime StartDate => ParseDateTime(start_date);
        public DateTime? EndDate => string.IsNullOrEmpty(end_date) ? null : (DateTime?)ParseDateTime(end_date);
        public int[] DaysOfWeek => days ?? new int[0];
        public bool HasHourRestrictions => start_hour >= 0 && end_hour >= 0;
        
        /// <summary>
        /// Check if the schedule is active right now
        /// </summary>
        public bool IsActive(DateTime now)
        {
            // Check date range
            if (!IsWithinDateRange(now)) return false;
            
            // Check day of week (if specified)
            if (days != null && days.Length > 0)
            {
                int currentDayOfWeek = (int)now.DayOfWeek; // 0=Sunday, 1=Monday, ..., 6=Saturday
                
                if (!System.Array.Exists(days, day => day == currentDayOfWeek))
                    return false;
            }
            
            // Check hour range (if specified)
            if (HasHourRestrictions)
            {
                int currentHour = now.Hour;
                return currentHour >= start_hour && currentHour < end_hour;
            }
            
            return true;
        }
        
        private bool IsWithinDateRange(DateTime now)
        {
            var start = StartDate;
            var end = EndDate;
            return start <= now && (end == null || end >= now);
        }
        
        private DateTime ParseDateTime(string dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString))
                return DateTime.UtcNow;
                
            if (DateTime.TryParse(dateTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result))
                return result;
                
            return DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Campaign metadata structure for JSON parsing
    /// Uses a Dictionary for flexible key-value storage
    /// </summary>
    [Serializable]
    public class CampaignMetadata
    {
        // Store as dictionary for flexible key-value access
        private Dictionary<string, string> _data = null;
        
        /// <summary>
        /// Parse metadata JSON string into key-value dictionary
        /// Returns null if parsing fails or metadata is empty
        /// </summary>
        public static CampaignMetadata Parse(string metadataJson)
        {
            if (string.IsNullOrEmpty(metadataJson))
                return null;
                
            try
            {
                // Parse JSON into a dictionary
                var data = new Dictionary<string, string>();
                
                // Simple JSON parser for flat key-value pairs
                metadataJson = metadataJson.Trim();
                if (metadataJson.StartsWith("{") && metadataJson.EndsWith("}"))
                {
                    // Remove outer braces
                    metadataJson = metadataJson.Substring(1, metadataJson.Length - 2);
                    
                    // Split by comma (simple parser, doesn't handle nested objects)
                    var pairs = metadataJson.Split(',');
                    foreach (var pair in pairs)
                    {
                        var keyValue = pair.Split(new[] { ':' }, 2);
                        if (keyValue.Length == 2)
                        {
                            var key = keyValue[0].Trim().Trim('"').Trim();
                            var value = keyValue[1].Trim().Trim('"').Trim();
                            data[key] = value;
                        }
                    }
                }
                
                if (data.Count > 0)
                {
                    return new CampaignMetadata { _data = data };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Campaign] Failed to parse metadata JSON: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get a metadata value by key
        /// Returns null if key doesn't exist
        /// </summary>
        public string GetValue(string key)
        {
            if (_data == null || string.IsNullOrEmpty(key))
                return null;
                
            return _data.ContainsKey(key) ? _data[key] : null;
        }
        
        /// <summary>
        /// Check if metadata contains a specific key
        /// </summary>
        public bool HasKey(string key)
        {
            return _data != null && _data.ContainsKey(key);
        }
        
        /// <summary>
        /// Check if metadata is valid (has any keys)
        /// </summary>
        public bool IsValid()
        {
            return _data != null && _data.Count > 0;
        }
        
        /// <summary>
        /// Get all keys in the metadata
        /// </summary>
        public string[] GetKeys()
        {
            if (_data == null)
                return new string[0];
                
            var keys = new string[_data.Count];
            _data.Keys.CopyTo(keys, 0);
            return keys;
        }
    }

    /// <summary>
    /// Campaign data model matching BoostOps dashboard structure v1.0.0
    /// Compatible with both Unity Remote Config and Firebase Remote Config
    /// </summary>
    [Serializable]
    public class Campaign
    {
        [SerializeField] public string campaign_id;
        [SerializeField] public string name;
        [SerializeField] public string status; // active, paused, ended, draft
        [SerializeField] public TargetProject target_project;
        // Frequency cap removed - internal campaign management only, but kept for JSON compatibility
        [SerializeField] public object frequency_cap; // Ignored field for JSON compatibility
        [SerializeField] public int min_player_days = -1; // -1 means use source project default
        [SerializeField] public int min_sessions = -1;    // -1 means use source project default
        
        // Ad format targeting - which display formats this campaign supports
        [SerializeField] public string[] formats; // e.g., ["interstitial", "app_wall", "banner"]

        [SerializeField] public CampaignSchedule schedule; // New nested schedule object
        [SerializeField] public string created_at;
        [SerializeField] public string updated_at;
        [SerializeField] public string metadata; // Optional metadata JSON string for custom use cases
        
        // Cached parsed metadata
        private CampaignMetadata _parsedMetadata = null;
        
        // Legacy fields for backward compatibility (hidden from inspector)
        [System.NonSerialized] private string start_date; // Use schedule.start_date instead
        [System.NonSerialized] private string end_date;   // Use schedule.end_date instead
        [System.NonSerialized] private bool is_repeating;  // No longer needed with new schedule system
        [System.NonSerialized] private List<string> days_of_week; // Use schedule.days instead
        
        // Legacy properties for backward compatibility
        public string Id => campaign_id;
        public string CampaignId => campaign_id;  // Add missing CampaignId property
        public string Name => name;
        public TargetProject TargetProject => target_project;
        public Creative[] Creatives => target_project?.creatives ?? new Creative[0]; // Add Creatives property

        public int MinimumPlayerDay => min_player_days;
        public int MinimumSession => min_sessions;
        
        // Frequency cap properties removed - internal campaign management only
        

        
        // Effective values that respect source project defaults and SDK fallbacks
        public int GetEffectiveMinimumPlayerDay(SourceProject sourceProject = null)
        {
            if (min_player_days >= 0) return min_player_days; // Campaign override
            if (sourceProject != null) return sourceProject.DefaultMinimumPlayerDay; // Source default
            return 0; // SDK default
        }
        
        public int GetEffectiveMinimumSession(SourceProject sourceProject = null)
        {
            if (min_sessions >= 0) return min_sessions; // Campaign override
            if (sourceProject != null) return sourceProject.DefaultMinimumSession; // Source default
            return 1; // SDK default
        }
        public DateTime StartDate => schedule?.StartDate ?? DateTime.UtcNow;
        public DateTime? EndDate => schedule?.EndDate;
        public CampaignSchedule Schedule => schedule ?? new CampaignSchedule();
        public string Status => status;
        public DateTime CreatedAt => ParseDateTime(created_at);
        public DateTime UpdatedAt => ParseDateTime(updated_at);
        
        // Legacy properties for backward compatibility
        [System.Obsolete("Use Schedule.IsActive(DateTime.Now) instead")]
        public bool IsRepeating => false; // Deprecated - scheduling is now handled by Schedule object
        [System.Obsolete("Use Schedule.DaysOfWeek instead")]
        public List<string> DaysOfWeek => new List<string>(); // Deprecated - use Schedule.DaysOfWeek
        
        // Helper properties
        public bool IsActive => status == "active" && Schedule.IsActive(DateTime.Now);
        
        /// <summary>
        /// Check if campaign supports a specific format
        /// </summary>
        public bool SupportsFormat(string format)
        {
            // If no formats specified, campaign doesn't support any format
            // Backend must explicitly declare supported formats
            if (formats == null || formats.Length == 0)
            {
                UnityEngine.Debug.LogWarning($"[Campaign] Campaign '{name}' has no formats declared - will not be available for format '{format}'. Backend should set 'formats' array.");
                return false;
            }
                
            return System.Array.Exists(formats, f => 
                string.Equals(f, format, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Get supported formats (returns empty array if none declared)
        /// </summary>
        public string[] GetFormats()
        {
            return formats ?? new string[0];
        }
        
        [System.Obsolete("Use Schedule.IsActive(DateTime.Now) instead")]
        public bool IsWithinDateRange => Schedule.IsActive(DateTime.Now);
        
        [System.Obsolete("Use Schedule.IsActive(DateTime.Now) instead")]
        public bool IsValidForToday => Schedule.IsActive(DateTime.Now);
        
        private DateTime ParseDateTime(string dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString)) return DateTime.MinValue;
            
            if (DateTime.TryParse(dateTimeString, out DateTime result))
            {
                return result;
            }
            
            // Fallback for various date formats
            return DateTime.MinValue;
        }
        
        // Utility methods for editor and analytics
        public string ExtractIosAppStoreId(string iosStoreUrl)
        {
            if (string.IsNullOrEmpty(iosStoreUrl)) return null;
            
            // Extract app ID from iOS App Store URL
            // Format: https://apps.apple.com/us/app/app-name/id123456789
            var match = System.Text.RegularExpressions.Regex.Match(iosStoreUrl, @"id(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
        
        public string ExtractAndroidPackageId(string androidStoreUrl)
        {
            if (string.IsNullOrEmpty(androidStoreUrl)) return null;
            
            // Extract package ID from Google Play Store URL
            // Format: https://play.google.com/store/apps/details?id=com.example.app
            var match = System.Text.RegularExpressions.Regex.Match(androidStoreUrl, @"id=([^&]+)");
            return match.Success ? match.Groups[1].Value : null;
        }
        
        public string GetIconUrl()
        {
            // Get the first icon creative URL
            var iconCreative = target_project?.creatives?.FirstOrDefault(c => c.format == "icon");
            return iconCreative?.variants?.FirstOrDefault()?.url;
        }
        
        public string GetConstructedIconUrl()
        {
            // Return the icon URL or construct one if needed
            var iconUrl = GetIconUrl();
            if (!string.IsNullOrEmpty(iconUrl)) return iconUrl;
            
            // Fallback: construct from app store URLs if available
            var iosId = ExtractIosAppStoreId(target_project?.store_urls?.apple);
            if (!string.IsNullOrEmpty(iosId))
            {
                return $"https://is1-ssl.mzstatic.com/image/thumb/Purple126/{iosId}/512x512bb.jpg";
            }
            
            return null;
        }
        
        public bool HasValidStoreUrl(string platform = null)
        {
            if (target_project?.store_urls == null) return false;
            
            if (string.IsNullOrEmpty(platform))
            {
                // Check if any store URL is valid
                return !string.IsNullOrEmpty(target_project.store_urls.apple) ||
                       !string.IsNullOrEmpty(target_project.store_urls.google) ||
                       !string.IsNullOrEmpty(target_project.store_urls.amazon);
            }
            
            switch (platform.ToLower())
            {
                case "ios":
                    return !string.IsNullOrEmpty(target_project.store_urls.apple);
                case "android":
                    return !string.IsNullOrEmpty(target_project.store_urls.google);
                case "amazon":
                    return !string.IsNullOrEmpty(target_project.store_urls.amazon);
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Get parsed metadata object from JSON string
        /// Returns cached result on subsequent calls
        /// </summary>
        public CampaignMetadata GetMetadata()
        {
            if (_parsedMetadata == null && !string.IsNullOrEmpty(metadata))
            {
                _parsedMetadata = CampaignMetadata.Parse(metadata);
            }
            return _parsedMetadata;
        }
        
        /// <summary>
        /// Get a metadata value by key
        /// Returns null if metadata is invalid, missing, or key doesn't exist
        /// Example: GetMetadataValue("cross_platform_id") returns "com.luckyjackpotcasino.kenocasino"
        /// </summary>
        public string GetMetadataValue(string key)
        {
            var meta = GetMetadata();
            return meta?.GetValue(key);
        }
        
        /// <summary>
        /// Check if metadata contains a specific key
        /// </summary>
        public bool HasMetadataKey(string key)
        {
            var meta = GetMetadata();
            return meta != null && meta.HasKey(key);
        }
        
        /// <summary>
        /// Check if metadata contains any valid data
        /// </summary>
        public bool HasValidMetadata()
        {
            var meta = GetMetadata();
            return meta != null && meta.IsValid();
        }
        
        /// <summary>
        /// Get all metadata keys
        /// </summary>
        public string[] GetMetadataKeys()
        {
            var meta = GetMetadata();
            return meta?.GetKeys() ?? new string[0];
        }
    }
    
    [Serializable]
    public class TargetProject
    {
        [SerializeField] public string project_id;
        [SerializeField] public StoreUrls store_urls;
        [SerializeField] public StoreIds store_ids;
        [SerializeField] public PlatformIds platform_ids;
        [SerializeField] public Creative[] creatives;      // Creative assets with variants (icon, hero, banner, native)
        [SerializeField] public UniversalLink universal_link; // Optional fallback
        
        // Legacy properties for backward compatibility
        public string Id => project_id;
        public Creative[] Creatives => creatives ?? new Creative[0];
        
        /// <summary>
        /// Find a creative by format (icon, banner, hero, native)
        /// </summary>
        public Creative FindCreative(CreativeFormat format)
        {
            return Creatives.FirstOrDefault(c => c.Format == format);
        }
        
        /// <summary>
        /// Find the best variant for a specific format and device preferences
        /// </summary>
        public CreativeVariant FindBestVariant(CreativeFormat format, string platform = "", string locale = "", 
                                              CreativeOrientation orientation = CreativeOrientation.Any)
        {
            var creative = FindCreative(format);
            return creative?.SelectBestVariant(platform, locale, orientation);
        }
        
        /// <summary>
        /// Returns true if all creatives can be loaded offline
        /// </summary>
        public bool IsFullyOfflineCapable => Creatives.Length > 0 && Creatives.All(c => c.IsFullyOfflineCapable);
        
        /// <summary>
        /// Returns true if any creatives require online access
        /// </summary>
        public bool RequiresOnline => Creatives.Any(c => c.RequiresOnline);
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
        
        public string iOS => apple;
        public string Android => google;
        
        public string GetUrlForCurrentPlatform()
        {
#if UNITY_IOS
            return apple;
#elif UNITY_ANDROID
            return google;
#elif UNITY_WSA || UNITY_WINRT || UNITY_STANDALONE_WIN
            return microsoft;
#else
            return GetFirstAvailableUrl();
#endif
        }
        
        public string GetFirstAvailableUrl()
        {
            return apple ?? google ?? web ?? amazon ?? samsung ?? microsoft;
        }
        
        public bool HasAnyLinks()
        {
            return !string.IsNullOrEmpty(apple) || !string.IsNullOrEmpty(google) || 
                   !string.IsNullOrEmpty(web) || !string.IsNullOrEmpty(amazon) || 
                   !string.IsNullOrEmpty(microsoft) || !string.IsNullOrEmpty(samsung);
        }
    }

    [Serializable]
    public class StoreLinks
    {
        [SerializeField] public string apple;
        [SerializeField] public string google;
        [SerializeField] public string amazon;
        [SerializeField] public string samsung;
        [SerializeField] public string microsoft;
        [SerializeField] public string web;
        
        // Legacy properties for backward compatibility
        public string iOS => apple;
        public string Android => google;
        public string Amazon => amazon;
        public string Samsung => samsung;
        public string Windows => microsoft;
        public string Web => web;
        
        /// <summary>
        /// Get the appropriate store URL for the current platform
        /// </summary>
        public string GetUrlForCurrentPlatform()
        {
#if UNITY_IOS
            return apple;
#elif UNITY_ANDROID
            return google;
#elif UNITY_STANDALONE_WIN
            return microsoft;
#elif UNITY_WEBGL
            return web;
#else
            return web ?? apple ?? google; // Default fallback
#endif
        }
        
        /// <summary>
        /// Get the first available store URL
        /// </summary>
        public string GetFirstAvailableUrl()
        {
            return apple ?? google ?? web ?? amazon ?? samsung ?? microsoft;
        }
        
        /// <summary>
        /// Check if any store links are available
        /// </summary>
        public bool HasAnyLinks()
        {
            return !string.IsNullOrEmpty(apple) || !string.IsNullOrEmpty(google) || 
                   !string.IsNullOrEmpty(web) || !string.IsNullOrEmpty(amazon) || 
                   !string.IsNullOrEmpty(microsoft);
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
    public class UniversalLink
    {
        [SerializeField] public string url;
        [SerializeField] public FallbackUrls fallback_urls;
        
        public string Url => url;
        public FallbackUrls FallbackUrls => fallback_urls;
    }
    
    [Serializable]
    public class FallbackUrls
    {
        [SerializeField] public string apple;
        [SerializeField] public string google;
        [SerializeField] public string web;
        
        public string iOS => apple;
        public string Android => google;
        public string Web => web;
    }

    /// <summary>
    /// Source project configuration with defaults for cross-promotion eligibility
    /// </summary>
    [Serializable]
    public class SourceProject
    {
        [SerializeField] public string bundle_id;
        [SerializeField] public string name;
        [SerializeField] public int min_player_days;
        [SerializeField] public int min_sessions;
        [SerializeField] public int frequency_cap; // Global frequency cap across all campaigns
        [SerializeField] public string interstitial_icon_cta;
        [SerializeField] public string interstitial_icon_text;
        [SerializeField] public string interstitial_rich_cta;
        [SerializeField] public string interstitial_rich_text;
        
        public string BundleId => bundle_id ?? "";
        public string Name => name ?? "";
        public int DefaultMinimumPlayerDay => min_player_days;
        public int DefaultMinimumSession => min_sessions;
        // DefaultFrequencyCap removed - internal campaign management only
        public string DefaultIconInterstitialButtonText => interstitial_icon_cta ?? "Play Now!";
        public string DefaultIconInterstitialDescription => interstitial_icon_text ?? "Try this awesome game!";
        public string DefaultRichInterstitialButtonText => interstitial_rich_cta ?? "Play Now!";
        public string DefaultRichInterstitialDescription => interstitial_rich_text ?? "Join millions of players in this amazing adventure!";
    }

    /// <summary>
    /// Version information for BoostOps configuration compatibility
    /// </summary>
    [Serializable]
    public class BoostOpsVersionInfo
    {
        [SerializeField] public string api_version;
        [SerializeField] public string schema_version;
        [SerializeField] public string client_min_version;
        [SerializeField] public string server_version;
        [SerializeField] public string contract_version;
        [SerializeField] public string last_updated;
        
        public string ApiVersion => api_version ?? "1.0.0";
        public string SchemaVersion => schema_version ?? "1.0.0";
        public string ClientMinVersion => client_min_version ?? "1.0.0";
        public string ServerVersion => server_version ?? "1.0.0";
        public string ContractVersion => contract_version ?? "1.0.0";
        public DateTime LastUpdated => ParseDateTime(last_updated);
        
        private DateTime ParseDateTime(string dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString)) return DateTime.MinValue;
            return DateTime.TryParse(dateTimeString, out DateTime result) ? result : DateTime.MinValue;
        }
    }
    
    /// <summary>
    /// Analytics control configuration for remote kill switches and API versioning
    /// Implements fail-closed approach - analytics disabled by default
    /// </summary>
    [Serializable]
    public class AnalyticsConfig
    {
        [SerializeField] public bool enabled = false;                   // Fail-closed: default disabled
        [SerializeField] public string endpoint;                       // Analytics endpoint URL
        [SerializeField] public string min_sdk_version;                // Minimum SDK version required
        [SerializeField] public int[] accepted_schema_versions;        // Accepted schema versions
        [SerializeField] public int backoff_seconds = 86400;           // Backoff duration on errors (24h)
        [SerializeField] public bool kill_switch = false;              // Emergency kill switch
        [SerializeField] public string kill_reason;                    // Reason for kill switch
        [SerializeField] public long expires_at;                       // Config expiration timestamp
        
        // Properties with safe defaults
        public bool IsEnabled => enabled && !kill_switch;
        public string Endpoint => endpoint ?? "https://analytics.boostops.io/v1";
        public string MinSdkVersion => min_sdk_version ?? "0.0.0";
        public int[] AcceptedSchemaVersions => accepted_schema_versions ?? new int[] { 1 };
        public int BackoffSeconds => Math.Max(backoff_seconds, 300);   // Minimum 5 min backoff
        public string KillReason => kill_reason ?? "Service temporarily unavailable";
        public bool IsExpired => expires_at > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires_at;
        
        /// <summary>
        /// Check if analytics should be allowed for the current SDK version
        /// </summary>
        public bool IsCompatibleWithSdk(string sdkVersion)
        {
            if (string.IsNullOrEmpty(sdkVersion) || string.IsNullOrEmpty(MinSdkVersion))
                return false;
                
            return CompareVersions(sdkVersion, MinSdkVersion) >= 0;
        }
        
        /// <summary>
        /// Check if schema version is accepted
        /// </summary>
        public bool IsSchemaVersionAccepted(int schemaVersion)
        {
            return AcceptedSchemaVersions?.Contains(schemaVersion) == true;
        }
        
        /// <summary>
        /// Simple semantic version comparison (major.minor.patch)
        /// Returns: -1 if v1 < v2, 0 if equal, 1 if v1 > v2
        /// </summary>
        private int CompareVersions(string v1, string v2)
        {
            try
            {
                var parts1 = v1.Split('.').Select(int.Parse).ToArray();
                var parts2 = v2.Split('.').Select(int.Parse).ToArray();
                
                for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
                {
                    int p1 = i < parts1.Length ? parts1[i] : 0;
                    int p2 = i < parts2.Length ? parts2[i] : 0;
                    
                    if (p1 != p2) return p1.CompareTo(p2);
                }
                return 0;
            }
            catch
            {
                return -1; // Fail-closed: assume incompatible on parse error
            }
        }
    }
    
    /// <summary>
    /// SKAN conversion value schema configuration (iOS attribution)
    /// Can be overridden by server, with critical defaults as fallback
    /// </summary>
    [Serializable]
    public class SKANSchemaConfig
    {
        [SerializeField] public string schema_name;                    // Schema identifier
        [SerializeField] public string schema_version;                 // Version for tracking changes
        [SerializeField] public bool use_server_schema = true;         // Allow server override
        [SerializeField] public SKANRuleConfig[] rules;                // Conversion rules from server
        [SerializeField] public string last_updated;                   // When schema was last updated
        
        // Properties
        public string SchemaName => schema_name ?? "default";
        public string SchemaVersion => schema_version ?? "1.0";
        public bool UseServerSchema => use_server_schema;
        public SKANRuleConfig[] Rules => rules ?? new SKANRuleConfig[0];
        public DateTime LastUpdated
        {
            get
            {
                if (string.IsNullOrEmpty(last_updated)) return DateTime.MinValue;
                return DateTime.TryParse(last_updated, out DateTime result) ? result : DateTime.MinValue;
            }
        }
    }
    
    /// <summary>
    /// Individual SKAN conversion rule from server config
    /// Simplified version for JSON serialization (no complex conditions)
    /// </summary>
    [Serializable]
    public class SKANRuleConfig
    {
        [SerializeField] public string event_type;                     // Event to match (e.g., "purchase", "level_complete")
        [SerializeField] public int fine_value;                        // SKAN value 0-63
        [SerializeField] public string coarse_value;                   // "low", "medium", "high"
        [SerializeField] public string description;                    // Human-readable description
        [SerializeField] public bool lock_window;                      // Lock measurement window (SKAN 4.0)
        [SerializeField] public SKANRuleCondition condition;           // Optional condition for rule matching
        
        // Properties
        public string EventType => event_type ?? "";
        public int FineValue => Math.Max(0, Math.Min(63, fine_value)); // Enforce 0-63 range
        public string CoarseValue => coarse_value ?? "low";
        public string Description => description ?? "";
        public bool LockWindow => lock_window;
        public SKANRuleCondition Condition => condition;
    }
    
    /// <summary>
    /// Simple condition for SKAN rule matching (server-friendly)
    /// Uses key-value comparisons instead of complex code
    /// </summary>
    [Serializable]
    public class SKANRuleCondition
    {
        [SerializeField] public string field;                          // Field to check (e.g., "amount", "level")
        [SerializeField] public string operator_type;                  // ">=", "<=", "==", ">", "<", "range"
        [SerializeField] public float value;                           // Value to compare against
        [SerializeField] public float max_value;                       // For range operator
        
        // Properties
        public string Field => field ?? "";
        public string OperatorType => operator_type ?? ">=";
        public float Value => value;
        public float MaxValue => max_value;
        
        /// <summary>
        /// Evaluate condition against event data
        /// </summary>
        public bool Matches(System.Collections.Generic.Dictionary<string, object> eventData)
        {
            if (string.IsNullOrEmpty(Field)) return true; // No condition = always match
            if (eventData == null || !eventData.ContainsKey(Field)) return false;
            
            // Extract numeric value from event data
            float eventValue = 0f;
            var fieldValue = eventData[Field];
            
            if (fieldValue is float floatVal) eventValue = floatVal;
            else if (fieldValue is double doubleVal) eventValue = (float)doubleVal;
            else if (fieldValue is int intVal) eventValue = intVal;
            else if (fieldValue is decimal decVal) eventValue = (float)decVal;
            else if (!float.TryParse(fieldValue?.ToString(), out eventValue)) return false;
            
            // Apply operator
            switch (OperatorType)
            {
                case ">=": return eventValue >= Value;
                case "<=": return eventValue <= Value;
                case ">":  return eventValue > Value;
                case "<":  return eventValue < Value;
                case "==": return Math.Abs(eventValue - Value) < 0.001f;
                case "range": return eventValue >= Value && eventValue < MaxValue;
                default: return false;
            }
        }
    }
    
    /// <summary>
    /// Container for the boostops_config JSON structure from Remote Config with version support
    /// Supports both Unity Remote Config and Firebase Remote Config formats
    /// </summary>
    [Serializable]
    public class BoostOpsConfig
    {
        // Version information for compatibility checking
        [SerializeField] public BoostOpsVersionInfo version_info;
        
        // Source project configuration with defaults
        [SerializeField] public SourceProject source_project;
        
        // Analytics control settings (fail-closed by default)
        [SerializeField] public AnalyticsConfig analytics_config;
        
        // SKAN conversion value schema (iOS attribution)
        [SerializeField] public SKANSchemaConfig skan_schema;
        
        // Dictionary-like structure for campaigns (campaign_id -> Campaign)
        // Note: Unity JsonUtility doesn't support Dictionary serialization directly
        [SerializeField] public List<CampaignEntry> campaigns = new List<CampaignEntry>();
        
        // SDK compatibility constants
        public static readonly string SUPPORTED_API_VERSION = "1.0.0";
        public static readonly string SUPPORTED_SCHEMA_VERSION = "1.0.0";
        public static readonly string SDK_VERSION = "2.0.6";
        
        /// <summary>
        /// Parse campaigns from Remote Config JSON with version validation
        /// Handles both Unity Remote Config and Firebase Remote Config formats
        /// </summary>
        public static BoostOpsConfig ParseFromJson(string json)
        {
            return ParseFromJson(json, CampaignParsingMode.All);
        }

        /// <summary>
        /// Parse campaigns from Remote Config JSON with specific parsing mode
        /// </summary>
        public static BoostOpsConfig ParseFromJson(string json, CampaignParsingMode mode)
        {
            var config = new BoostOpsConfig();
            
            try
            {
                if (string.IsNullOrEmpty(json) || json.Trim() == "{}")
                {
                    Debug.LogWarning("[BoostOps] Empty or null JSON provided");
                    return config;
                }

                // Only try Unity Remote Settings formats when in All mode (skip for LocalOnly/Demo mode)
                if (mode == CampaignParsingMode.All)
                {
                    // First try Unity Remote Settings format (crossPromoConfig)
                    try
                    {
                        var unityRemoteSettings = JsonUtility.FromJson<UnityRemoteSettingsWrapper>(json);
                        if (unityRemoteSettings?.crossPromoConfig != null)
                        {
                            return ParseFromUnityRemoteSettings(unityRemoteSettings.crossPromoConfig);
                        }
                    }
                    catch (Exception)
                    {
                        // Continue to other formats if Unity Remote Settings parsing fails
                    }

                    // Try direct CrossPromoConfig format
                    try
                    {
                        BoostOpsLogger.LogDebug("Config", "Attempting CrossPromoConfig parsing...");
                        var crossPromoConfig = JsonUtility.FromJson<CrossPromoConfig>(json);
                        BoostOpsLogger.LogDebug("Config", $"CrossPromoConfig parsed: {(crossPromoConfig != null ? "not null" : "null")}");
                        
                        if (crossPromoConfig != null)
                        {
                            BoostOpsLogger.LogDebug("Config", $"activeCampaigns: {(crossPromoConfig.activeCampaigns != null ? $"array with {crossPromoConfig.activeCampaigns.Length} items" : "null")}");
                        }
                        
                        if (crossPromoConfig?.activeCampaigns != null && crossPromoConfig.activeCampaigns.Length > 0)
                        {
                            Debug.Log($"[BoostOps] Found CrossPromoConfig format with {crossPromoConfig.activeCampaigns.Length} campaigns");
                            return ParseFromUnityRemoteSettings(crossPromoConfig);
                        }
                        else
                        {
                            Debug.Log("[BoostOps] CrossPromoConfig format detected but no campaigns found, continuing to structured format");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[BoostOps] CrossPromoConfig parsing failed: {ex.Message}, continuing to structured format");
                        // Continue to structured format
                    }
                }
                else
                {
                    BoostOpsLogger.LogDebug("Config", $"Skipping Unity Remote Settings parsing (mode: {mode})");
                }

                // Try parsing with BoostOps.Core format first (for new server JSON files)
                try
                {
                    BoostOpsLogger.LogDebug("Config", "Attempting BoostOps.Core format parsing...");
                    var coreConfig = JsonUtility.FromJson<BoostOps.Core.RemoteCampaignConfig>(json);
                    
                    if (coreConfig?.campaigns != null && coreConfig.campaigns.Count > 0)
                    {
                        BoostOpsLogger.LogDebug("Config", $"Found {coreConfig.campaigns.Count} campaigns in BoostOps.Core format - converting to runtime format");
                        
                        // Apply Unity JsonUtility Dictionary workaround for campaign target_project store_ids/store_urls
                        ApplyCoreConfigDictionaryWorkaround(coreConfig, json);
                        
                        // Convert from BoostOps.Core.Campaign to BoostOps.Campaign (same logic as remote config providers)
                        foreach (var coreCampaign in coreConfig.campaigns)
                        {
                            var runtimeCampaign = ConvertCoreToRuntimeCampaign(coreCampaign);
                            
                            // Add as CampaignEntry (expected format for config.campaigns)
                            config.campaigns.Add(new CampaignEntry
                            {
                                campaignId = runtimeCampaign.campaign_id,
                                campaign = runtimeCampaign
                            });
                        }
                        config.version_info = coreConfig.version_info != null ? new BoostOpsVersionInfo
                        {
                            schema_version = coreConfig.version_info.schema_version,
                            api_version = coreConfig.version_info.api_version,
                            last_updated = coreConfig.version_info.last_updated
                        } : null;
                        
                        BoostOpsLogger.LogDebug("Config", $"Successfully converted {config.campaigns.Count} campaigns from BoostOps.Core format");
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    BoostOpsLogger.LogError("Config", $"BoostOps.Core format parsing failed: {ex.Message}");
                    BoostOpsLogger.LogError("Config", "All campaign parsing now uses the BoostOps.Core format - no legacy formats supported");
                    return config; // Early return - no legacy parsing
                }

                // Try structured config with version info (legacy runtime format)
                try
                {
                    BoostOpsLogger.LogDebug("Config", "Attempting legacy structured config parsing...");
                    var structuredConfig = JsonUtility.FromJson<JsonConfigWrapper>(json);
                    
                    if (structuredConfig != null)
                    {
                        BoostOpsLogger.LogDebug("Config", $"Found {structuredConfig.campaigns?.Length ?? 0} campaigns in legacy structured config");
                    }
                    
                    if (structuredConfig != null && structuredConfig.version_info != null)
                    {
                        config.version_info = structuredConfig.version_info;
                        
                        // Map ingest config to analytics config
                        if (structuredConfig.ingest != null)
                        {
                            config.analytics_config = structuredConfig.ingest.ToAnalyticsConfig();
                            BoostOpsLogger.LogDebug("Config", $"Loaded analytics config - enabled: {config.analytics_config.enabled}, endpoint: {config.analytics_config.endpoint}");
                        }
                        else
                        {
                            BoostOpsLogger.LogDebug("Config", "No ingest config found - analytics will be disabled");
                        }
                        
                        // Validate version compatibility
                        var compatibility = ValidateVersionCompatibility(config.version_info);
                        if (!compatibility.IsCompatible)
                        {
                            Debug.LogError($"[BoostOps] Version compatibility issue: {compatibility.Message}");
                            if (compatibility.IsBlocking)
                            {
                                Debug.LogError("[BoostOps] Blocking compatibility issue detected. Aborting campaign loading.");
                                return config;
                            }
                            else
                            {
                                Debug.LogWarning("[BoostOps] Non-blocking compatibility issue. Continuing with caution.");
                            }
                        }
                        
                        // Parse source_project from structured format
                        if (structuredConfig.source_project != null)
                        {
                            config.source_project = structuredConfig.source_project;
                            BoostOpsLogger.LogDebug("Config", $"Loaded source_project with IconDescription: '{config.source_project.DefaultIconInterstitialDescription}'");
                        }
                        else
                        {
                            Debug.LogWarning("[BoostOps] No source_project found in structured config");
                        }
                        
                        // Parse campaigns from structured format
                        if (structuredConfig.campaigns != null)
                        {
                                                BoostOpsLogger.LogDebug("Config", $"Processing {structuredConfig.campaigns.Length} campaigns...");
                    foreach (var campaign in structuredConfig.campaigns)
                    {
                        BoostOpsLogger.LogDebug("Config", $"Validating campaign: {campaign?.campaign_id ?? "null"}");
                        if (IsValidCampaignStructure(campaign))
                        {
                            config.campaigns.Add(new CampaignEntry
                            {
                                campaignId = campaign.campaign_id,
                                campaign = campaign
                            });
                            BoostOpsLogger.LogDebug("Config", $"Successfully added campaign: {campaign.campaign_id}");
                        }
                        else
                        {
                            BoostOpsLogger.LogWarning("Config", $"Campaign validation failed for: {campaign?.campaign_id ?? "null"}");
                        }
                    }
                        }
                        else
                        {
                            Debug.LogWarning("[BoostOps] No campaigns array found in structured config");
                        }
                        
                        BoostOpsLogger.LogInfo("Config", $"Successfully parsed {config.campaigns.Count} campaigns from structured JSON");
                        return config;
                    }
                    else
                    {
                        Debug.LogWarning("[BoostOps] Structured config parsing failed - missing version_info or null structuredConfig");
                    }
                }
                catch (Exception ex)
                {
                    // Fall back to legacy parsing if structured parsing fails
                    Debug.LogWarning($"[BoostOps] Structured JSON parsing failed: {ex.Message}, attempting legacy format parsing");
                }

                // Legacy format parsing - handle dynamic keys by parsing as raw dictionary
                // This is for backward compatibility with older config formats
                config.version_info = new BoostOpsVersionInfo
                {
                    api_version = SUPPORTED_API_VERSION,
                    schema_version = SUPPORTED_SCHEMA_VERSION,
                    contract_version = SUPPORTED_API_VERSION,
                    last_updated = System.DateTime.UtcNow.ToString("O")
                };

                // Parse campaigns directly from the JSON string using manual parsing
                var campaignCount = ParseLegacyCampaigns(json, config);
                Debug.Log($"[BoostOps] Successfully parsed {campaignCount} campaigns from legacy JSON format");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Failed to parse campaigns JSON: {ex.Message}");
            }
            
            return config;
        }
        
        /// <summary>
        /// Convert from BoostOps.Core.Campaign (used for JSON deserialization) to BoostOps.Campaign (used for runtime)
        /// This is the same conversion logic used in remote config providers
        /// </summary>
        private static Campaign ConvertCoreToRuntimeCampaign(BoostOps.Core.Campaign coreCampaign)
        {
            var campaign = new Campaign();
            
            // Basic fields
            campaign.campaign_id = coreCampaign.campaign_id;
            campaign.name = coreCampaign.name;
            campaign.status = coreCampaign.status;
            campaign.min_sessions = coreCampaign.min_sessions;
            campaign.min_player_days = coreCampaign.min_player_days;
            campaign.created_at = coreCampaign.created_at;
            campaign.updated_at = coreCampaign.updated_at;
            
            // Frequency cap
            if (coreCampaign.frequency_cap != null)
            {
                campaign.frequency_cap = new BoostOps.Core.FrequencyCapJson
                {
                    time_unit = coreCampaign.frequency_cap.time_unit,
                    impressions = coreCampaign.frequency_cap.impressions
                };
            }
            
            // Target project
            if (coreCampaign.target_project != null)
            {
                campaign.target_project = new TargetProject();
                campaign.target_project.project_id = coreCampaign.target_project.project_id;
                
                // Store URLs
                if (coreCampaign.target_project.store_urls != null)
                {
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍 BEFORE conversion - Core URLs:");
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍   Core Google: '{coreCampaign.target_project.store_urls.google ?? "null"}'");
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍   Core Apple: '{coreCampaign.target_project.store_urls.apple ?? "null"}'");
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍   Core Amazon: '{coreCampaign.target_project.store_urls.amazon ?? "null"}'");
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍   Core Samsung: '{coreCampaign.target_project.store_urls.samsung ?? "null"}'");
                    
                    campaign.target_project.store_urls = new StoreUrls
                    {
                        apple = coreCampaign.target_project.store_urls.apple,
                        google = coreCampaign.target_project.store_urls.google,
                        amazon = coreCampaign.target_project.store_urls.amazon,
                        microsoft = coreCampaign.target_project.store_urls.microsoft,
                        samsung = coreCampaign.target_project.store_urls.samsung,
                        web = coreCampaign.target_project.store_urls.web
                    };
                    
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍 AFTER conversion - Runtime URLs:");
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍   Runtime Google: '{campaign.target_project.store_urls.google ?? "null"}'");
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍   Runtime Apple: '{campaign.target_project.store_urls.apple ?? "null"}'");
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍   Runtime Amazon: '{campaign.target_project.store_urls.amazon ?? "null"}'");
                    Debug.Log($"[ConvertCoreToRuntimeCampaign] 🔍   Runtime Samsung: '{campaign.target_project.store_urls.samsung ?? "null"}'");
                }
                
                // Store IDs
                if (coreCampaign.target_project.store_ids != null)
                {
                    campaign.target_project.store_ids = new StoreIds
                    {
                        apple = coreCampaign.target_project.store_ids.apple,
                        google = coreCampaign.target_project.store_ids.google,
                        amazon = coreCampaign.target_project.store_ids.amazon,
                        microsoft = coreCampaign.target_project.store_ids.microsoft,
                        samsung = coreCampaign.target_project.store_ids.samsung
                    };
                }
                
                // Platform IDs
                if (coreCampaign.target_project.platform_ids != null)
                {
                    campaign.target_project.platform_ids = new PlatformIds
                    {
                        ios_bundle_id = coreCampaign.target_project.platform_ids.ios_bundle_id,
                        android_package_name = coreCampaign.target_project.platform_ids.android_package_name
                    };
                }
                
                // Creatives (simplified conversion)
                if (coreCampaign.target_project.creatives != null && coreCampaign.target_project.creatives.Length > 0)
                {
                    campaign.target_project.creatives = coreCampaign.target_project.creatives.Select(coreCreative =>
                    {
                        var creative = new Creative();
                        creative.format = coreCreative.format;
                        creative.creative_id = coreCreative.creative_id;
                        
                        if (coreCreative.variants != null && coreCreative.variants.Length > 0)
                        {
                            creative.variants = coreCreative.variants.Select(coreVariant => new CreativeVariant
                            {
                                url = coreVariant.url,
                                local_key = coreVariant.local_key,
                                resolution = coreVariant.resolution,
                                sha256 = coreVariant.sha256
                            }).ToArray();
                        }
                        
                        return creative;
                    }).ToArray();
                }
            }
            
            return campaign;
        }
        
        /// <summary>
        /// Parse campaigns from Unity Remote Settings crossPromoConfig format
        /// This is the preferred method for Unity Remote Settings integration
        /// </summary>
        public static BoostOpsConfig ParseFromUnityRemoteSettings(CrossPromoConfig crossPromoConfig)
        {
            var config = new BoostOpsConfig();
            
            if (crossPromoConfig == null)
            {
                Debug.LogWarning("[BoostOps] Null crossPromoConfig provided");
                return config;
            }
            
            // Set default version info for Unity Remote Settings
            config.version_info = new BoostOpsVersionInfo
            {
                api_version = SUPPORTED_API_VERSION,
                schema_version = "unity_remote_settings_1.0.0",
                contract_version = SUPPORTED_API_VERSION,
                last_updated = System.DateTime.UtcNow.ToString("O")
            };
            
            // Check if cross-promotion is enabled
            if (!crossPromoConfig.CrossPromoEnabled)
            {
                Debug.Log("[BoostOps] Cross-promotion is disabled in Unity Remote Settings");
                return config;
            }
            
            // Use campaigns directly - no conversion needed
            var activeCampaigns = crossPromoConfig.ActiveCampaigns;
            foreach (var campaign in activeCampaigns)
            {
                if (campaign != null && !string.IsNullOrEmpty(campaign.campaign_id))
                {
                    try
                    {
                        if (IsValidCampaignStructure(campaign))
                        {
                            config.campaigns.Add(new CampaignEntry
                            {
                                campaignId = campaign.campaign_id,
                                campaign = campaign
                            });
                        }
                        else
                        {
                            Debug.LogWarning($"[BoostOps] Invalid campaign structure for {campaign.campaign_id}, skipping");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[BoostOps] Failed to process campaign {campaign.campaign_id}: {ex.Message}");
                    }
                }
            }
            
            Debug.Log($"[BoostOps] Successfully parsed {config.campaigns.Count} campaigns from Unity Remote Settings (crossPromoConfig)");
            return config;
        }
        
        /// <summary>
        /// Helper method specifically for Unity Remote Settings integration
        /// Use this when you have the config key from Unity Remote Settings
        /// </summary>
        public static BoostOpsConfig ParseFromUnityRemoteSettingsJson(string json)
        {
            try
            {
                // Try to parse as UnityRemoteSettingsWrapper first
                var wrapper = JsonUtility.FromJson<UnityRemoteSettingsWrapper>(json);
                if (wrapper?.crossPromoConfig != null)
                {
                    return ParseFromUnityRemoteSettings(wrapper.crossPromoConfig);
                }
                
                // Try to parse as direct CrossPromoConfig
                var crossPromoConfig = JsonUtility.FromJson<CrossPromoConfig>(json);
                if (crossPromoConfig != null)
                {
                    return ParseFromUnityRemoteSettings(crossPromoConfig);
                }
                
                Debug.LogError("[BoostOps] JSON does not match Unity Remote Settings crossPromoConfig format");
                return new BoostOpsConfig();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Failed to parse Unity Remote Settings JSON: {ex.Message}");
                return new BoostOpsConfig();
            }
        }
        
        /// <summary>
        /// Validate version compatibility between server and client
        /// </summary>
        public static VersionCompatibilityResult ValidateVersionCompatibility(BoostOpsVersionInfo versionInfo)
        {
            if (versionInfo == null)
            {
                return new VersionCompatibilityResult
                {
                    IsCompatible = true, // Assume compatible if no version info
                    IsBlocking = false,
                    Message = "No version information provided, assuming compatibility"
                };
            }

            // Parse versions for comparison
            if (!TryParseVersion(versionInfo.ApiVersion, out var serverApiVersion))
            {
                return new VersionCompatibilityResult
                {
                    IsCompatible = false,
                    IsBlocking = true,
                    Message = $"Invalid server API version format: {versionInfo.ApiVersion}"
                };
            }

            if (!TryParseVersion(SUPPORTED_API_VERSION, out var clientApiVersion))
            {
                return new VersionCompatibilityResult
                {
                    IsCompatible = false,
                    IsBlocking = true,
                    Message = "Invalid client API version format"
                };
            }

            // Check for major version compatibility
            if (serverApiVersion.Major != clientApiVersion.Major)
            {
                return new VersionCompatibilityResult
                {
                    IsCompatible = false,
                    IsBlocking = true,
                    Message = $"Major version mismatch: Server v{versionInfo.ApiVersion}, Client v{SUPPORTED_API_VERSION}"
                };
            }

            // Check for minor version compatibility (warnings only)
            if (serverApiVersion.Minor > clientApiVersion.Minor)
            {
                return new VersionCompatibilityResult
                {
                    IsCompatible = false,
                    IsBlocking = false,
                    Message = $"Server minor version newer: Server v{versionInfo.ApiVersion}, Client v{SUPPORTED_API_VERSION}. Some features may not work."
                };
            }

            if (serverApiVersion.Minor < clientApiVersion.Minor)
            {
                return new VersionCompatibilityResult
                {
                    IsCompatible = false,
                    IsBlocking = false,
                    Message = $"Client minor version newer: Server v{versionInfo.ApiVersion}, Client v{SUPPORTED_API_VERSION}. Using backward compatibility."
                };
            }

            return new VersionCompatibilityResult
            {
                IsCompatible = true,
                IsBlocking = false,
                Message = $"Versions compatible: Server v{versionInfo.ApiVersion}, Client v{SUPPORTED_API_VERSION}"
            };
        }

        private static bool TryParseVersion(string versionString, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(versionString)) return false;
            
            // Handle semantic versioning (remove any pre-release or build info)
            var cleanVersion = versionString.Split('-')[0].Split('+')[0];
            return Version.TryParse(cleanVersion, out version);
        }

        private static bool IsValidCampaignStructure(Campaign campaign)
        {
            if (campaign == null)
            {
                Debug.LogWarning("[BoostOps] Campaign validation failed: campaign is null");
                return false;
            }
            
            if (string.IsNullOrEmpty(campaign.campaign_id))
            {
                Debug.LogWarning($"[BoostOps] Campaign validation failed: campaign_id is null/empty for campaign: {campaign.name}");
                return false;
            }
            
            // Name is optional for local cross-promo - only used for debug logging
            
            if (campaign.target_project == null)
            {
                Debug.LogWarning($"[BoostOps] Campaign validation failed: target_project is null for campaign: {campaign.campaign_id}");
                return false;
            }
            
            if (string.IsNullOrEmpty(campaign.target_project.project_id))
            {
                Debug.LogWarning($"[BoostOps] Campaign validation failed: target_project.project_id is null/empty for campaign: {campaign.campaign_id}");
                return false;
            }
            
            if (campaign.target_project.store_urls == null)
            {
                Debug.LogWarning($"[BoostOps] Campaign validation failed: target_project.store_urls is null for campaign: {campaign.campaign_id}");
                return false;
            }
            
            BoostOpsLogger.LogDebug("Config", $"Campaign validation passed for: {campaign.campaign_id}");
            return true;
        }
        
        public List<Campaign> GetAllCampaigns()
        {
            var result = new List<Campaign>();
            foreach (var entry in campaigns)
            {
                if (entry.campaign != null)
                {
                    result.Add(entry.campaign);
                }
            }
            return result;
        }
        
        public Campaign GetCampaignById(string campaignId)
        {
            foreach (var entry in campaigns)
            {
                if (entry.campaignId == campaignId && entry.campaign != null)
                {
                    return entry.campaign;
                }
            }
            return null;
        }
        
        public List<Campaign> GetActiveCampaigns()
        {
            var result = new List<Campaign>();
            foreach (var entry in campaigns)
            {
                if (entry.campaign != null && entry.campaign.IsActive)
                {
                    result.Add(entry.campaign);
                }
            }
            return result;
        }
        
        /// <summary>
        /// Parse legacy format campaigns with dynamic keys (without SimpleJSON dependency)
        /// </summary>
        private static int ParseLegacyCampaigns(string json, BoostOpsConfig config)
        {
            try
            {
                // Remove whitespace and check basic structure
                json = json.Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}"))
                {
                    Debug.LogError("[BoostOps] Invalid JSON format");
                    return 0;
                }

                // Simple manual parsing for campaign objects
                // Look for patterns like "campaign_id": { ... }
                var campaignPattern = @"""([^""]+)""\s*:\s*\{";
                var matches = System.Text.RegularExpressions.Regex.Matches(json, campaignPattern);
                
                int parsedCount = 0;
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var campaignId = match.Groups[1].Value;
                    
                    // Skip version_info and other non-campaign keys
                    if (campaignId == "version_info" || campaignId == "campaigns") continue;
                    
                    try
                    {
                        // Extract the campaign JSON object
                        var campaignJson = ExtractJsonObject(json, match.Index);
                        if (campaignJson != null)
                        {
                            var campaign = JsonUtility.FromJson<Campaign>(campaignJson);
                            if (campaign != null)
                            {
                                // Ensure campaign_id is set
                                if (string.IsNullOrEmpty(campaign.campaign_id))
                                {
                                    campaign.campaign_id = campaignId;
                                }
                                
                                // Validate campaign structure
                                if (IsValidCampaignStructure(campaign))
                                {
                                    config.campaigns.Add(new CampaignEntry
                                    {
                                        campaignId = campaignId,
                                        campaign = campaign
                                    });
                                    parsedCount++;
                                }
                                else
                                {
                                    Debug.LogWarning($"[BoostOps] Invalid campaign structure for {campaignId}, skipping");
                                }
                            }
                        }
                    }
                    catch (System.Exception campaignEx)
                    {
                        Debug.LogError($"[BoostOps] Failed to parse campaign {campaignId}: {campaignEx.Message}");
                    }
                }
                
                return parsedCount;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Legacy campaign parsing failed: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Extract a JSON object from a string starting at a given position
        /// </summary>
        private static string ExtractJsonObject(string json, int startIndex)
        {
            try
            {
                // Find the start of the object (after the key and colon)
                int objectStart = json.IndexOf('{', startIndex);
                if (objectStart == -1) return null;
                
                // Count braces to find the matching closing brace
                int braceCount = 0;
                int objectEnd = -1;
                bool inString = false;
                bool escaped = false;
                
                for (int i = objectStart; i < json.Length; i++)
                {
                    char c = json[i];
                    
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    
                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    
                    if (c == '"')
                    {
                        inString = !inString;
                        continue;
                    }
                    
                    if (!inString)
                    {
                        if (c == '{')
                        {
                            braceCount++;
                        }
                        else if (c == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                objectEnd = i;
                                break;
                            }
                        }
                    }
                }
                
                if (objectEnd > objectStart)
                {
                    return json.Substring(objectStart, objectEnd - objectStart + 1);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] JSON object extraction failed: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Apply manual parsing workaround for Unity JsonUtility Dictionary issues in BoostOps.Core.RemoteCampaignConfig
        /// Unity JsonUtility fails to deserialize Dictionary fields, so we manually parse them using regex
        /// </summary>
        private static void ApplyCoreConfigDictionaryWorkaround(BoostOps.Core.RemoteCampaignConfig coreConfig, string json)
        {
            if (coreConfig?.campaigns == null || string.IsNullOrEmpty(json))
                return;
                
            try
            {
                Debug.Log("[BoostOps] 🔧 Applying Unity JsonUtility Dictionary workaround for campaigns");
                
                // Find each campaign in the JSON and manually parse its store_ids/store_urls
                for (int i = 0; i < coreConfig.campaigns.Count; i++)
                {
                    var campaign = coreConfig.campaigns[i];
                    if (campaign?.target_project == null) continue;
                    
                    string campaignId = campaign.campaign_id ?? $"campaign_{i}";
                    
                    // Find this campaign's JSON section
                    var campaignPattern = $@"""campaign_id"":\s*""{System.Text.RegularExpressions.Regex.Escape(campaignId)}""[^}}]*""target_project"":\s*\{{([^}}]*(?:\{{[^}}]*\}}[^}}]*)*)\}}";
                    var campaignMatch = System.Text.RegularExpressions.Regex.Match(json, campaignPattern, System.Text.RegularExpressions.RegexOptions.Singleline);
                    
                    if (campaignMatch.Success)
                    {
                        string targetProjectJson = campaignMatch.Groups[1].Value;
                        
                        // Parse store_ids
                        var storeIdsMatch = System.Text.RegularExpressions.Regex.Match(targetProjectJson, @"""store_ids"":\s*\{([^}]*)\}");
                        if (storeIdsMatch.Success && campaign.target_project.store_ids == null)
                        {
                            string storeIdsContent = storeIdsMatch.Groups[1].Value;
                            campaign.target_project.store_ids = new BoostOps.Core.StoreIds();
                            
                            // Parse individual store IDs
                            var appleMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""apple"":\s*""([^""]*)""");
                            var googleMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""google"":\s*""([^""]*)""");
                            var amazonMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""amazon"":\s*""([^""]*)""");
                            var microsoftMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""microsoft"":\s*""([^""]*)""");
                            var samsungMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""samsung"":\s*""([^""]*)""");
                            
                            if (appleMatch.Success) campaign.target_project.store_ids.apple = appleMatch.Groups[1].Value;
                            if (googleMatch.Success) campaign.target_project.store_ids.google = googleMatch.Groups[1].Value;
                            if (amazonMatch.Success) campaign.target_project.store_ids.amazon = amazonMatch.Groups[1].Value;
                            if (microsoftMatch.Success) campaign.target_project.store_ids.microsoft = microsoftMatch.Groups[1].Value;
                            if (samsungMatch.Success) campaign.target_project.store_ids.samsung = samsungMatch.Groups[1].Value;
                            
                            Debug.Log($"[BoostOps] 🔧 Fixed store_ids for campaign '{campaignId}' - Apple: '{campaign.target_project.store_ids.apple}', Google: '{campaign.target_project.store_ids.google}'");
                        }
                        
                        // Parse store_urls
                        var storeUrlsMatch = System.Text.RegularExpressions.Regex.Match(targetProjectJson, @"""store_urls"":\s*\{([^}]*)\}");
                        if (storeUrlsMatch.Success && campaign.target_project.store_urls == null)
                        {
                            string storeUrlsContent = storeUrlsMatch.Groups[1].Value;
                            campaign.target_project.store_urls = new BoostOps.Core.StoreUrls();
                            
                            // Parse individual store URLs
                            var appleMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""apple"":\s*""([^""]*)""");
                            var googleMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""google"":\s*""([^""]*)""");
                            var amazonMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""amazon"":\s*""([^""]*)""");
                            var microsoftMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""microsoft"":\s*""([^""]*)""");
                            var samsungMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""samsung"":\s*""([^""]*)""");
                            var webMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""web"":\s*""([^""]*)""");
                            
                            if (appleMatch.Success) campaign.target_project.store_urls.apple = appleMatch.Groups[1].Value;
                            if (googleMatch.Success) campaign.target_project.store_urls.google = googleMatch.Groups[1].Value;
                            if (amazonMatch.Success) campaign.target_project.store_urls.amazon = amazonMatch.Groups[1].Value;
                            if (microsoftMatch.Success) campaign.target_project.store_urls.microsoft = microsoftMatch.Groups[1].Value;
                            if (samsungMatch.Success) campaign.target_project.store_urls.samsung = samsungMatch.Groups[1].Value;
                            if (webMatch.Success) campaign.target_project.store_urls.web = webMatch.Groups[1].Value;
                            
                            Debug.Log($"[BoostOps] 🔧 Fixed store_urls for campaign '{campaignId}' - Apple: '{campaign.target_project.store_urls.apple}', Google: '{campaign.target_project.store_urls.google}'");
                        }
                        
                        // Parse platform_ids  
                        var platformIdsMatch = System.Text.RegularExpressions.Regex.Match(targetProjectJson, @"""platform_ids"":\s*\{([^}]*)\}");
                        if (platformIdsMatch.Success && campaign.target_project.platform_ids == null)
                        {
                            string platformIdsContent = platformIdsMatch.Groups[1].Value;
                            campaign.target_project.platform_ids = new BoostOps.Core.PlatformIds();
                            
                            var iosBundleMatch = System.Text.RegularExpressions.Regex.Match(platformIdsContent, @"""ios_bundle_id"":\s*""([^""]*)""");
                            var androidPackageMatch = System.Text.RegularExpressions.Regex.Match(platformIdsContent, @"""android_package_name"":\s*""([^""]*)""");
                            
                            if (iosBundleMatch.Success) campaign.target_project.platform_ids.ios_bundle_id = iosBundleMatch.Groups[1].Value;
                            if (androidPackageMatch.Success) campaign.target_project.platform_ids.android_package_name = androidPackageMatch.Groups[1].Value;
                            
                            Debug.Log($"[BoostOps] 🔧 Fixed platform_ids for campaign '{campaignId}' - iOS: '{campaign.target_project.platform_ids.ios_bundle_id}', Android: '{campaign.target_project.platform_ids.android_package_name}'");
                        }
                    }
                }
                
                Debug.Log("[BoostOps] ✅ Dictionary workaround applied successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ Failed to apply dictionary workaround: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Result of version compatibility check
    /// </summary>
    public struct VersionCompatibilityResult
    {
        public bool IsCompatible;
        public bool IsBlocking; // True if this should prevent operation
        public string Message;
    }
    
    [Serializable]
    public class CampaignEntry
    {
        [SerializeField] public string campaignId;
        [SerializeField] public Campaign campaign;
    }
    
    /// <summary>
    /// Wrapper for campaign list from remote config (legacy support)
    /// </summary>
    [Serializable]
    public class CampaignList
    {
        [SerializeField] public List<Campaign> campaigns;
        
        public List<Campaign> GetCampaigns()
        {
            return campaigns ?? new List<Campaign>();
        }
    }
    
    /// <summary>
    /// Static utility class for parsing campaigns from various sources
    /// Supports Firebase Remote Config, Unity Remote Settings, and JSON strings
    /// </summary>
    public static class CampaignParser
    {
        /// <summary>
        /// Load campaigns from Resources folder for offline operation
        /// Loads from Resources/BoostOps/cross_promo_local.json by default
        /// </summary>
        /// <param name="resourcePath">Path in Resources folder (without .json extension)</param>
        /// <returns>Parsed campaigns or empty list if not found</returns>
        public static List<Campaign> LoadCampaignsFromResources(string resourcePath = "BoostOps/cross_promo_local")
        {
            try
            {
                var textAsset = Resources.Load<TextAsset>(resourcePath);
                if (textAsset == null)
                {
                    BoostOpsLogger.LogWarning("CampaignParser", $"Campaign file not found in Resources: {resourcePath}");
                    return new List<Campaign>();
                }

                BoostOpsLogger.LogDebug("CampaignParser", $"Loading campaigns from Resources: {resourcePath}");
                var jsonContent = textAsset.text;
                
                var campaigns = ParseCampaignsFromJson(jsonContent, CampaignParsingMode.LocalOnly);
                BoostOpsLogger.LogInfo("CampaignParser", $"Loaded {campaigns.Count} campaigns from Resources");
                
                return campaigns;
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("CampaignParser", $"Error loading campaigns from Resources: {ex.Message}");
                return new List<Campaign>();
            }
        }
        

        
        /// <summary>
        /// Load campaigns from Resources - reliable cross-platform loading
        /// </summary>
        /// <returns>Parsed campaigns from Resources</returns>
        public static List<Campaign> LoadCampaignsOffline()
        {
            BoostOpsLogger.LogDebug("CampaignParser", "Loading campaigns from Resources...");
            
            // Load directly from Resources (reliable, synchronous, cross-platform)
            var resourceCampaigns = LoadCampaignsFromResources("BoostOps/cross_promo_local");
            if (resourceCampaigns.Count > 0)
            {
                BoostOpsLogger.LogInfo("CampaignParser", "Loaded campaigns from Resources");
                return resourceCampaigns;
            }
            
            BoostOpsLogger.LogWarning("CampaignParser", "No campaigns found in Resources/BoostOps/cross_promo_local.json");
            return new List<Campaign>();
        }
        
        /// <summary>
        /// Load full config (including analytics settings) from Resources folder
        /// </summary>
        /// <param name="resourcePath">Path in Resources folder (without .json extension)</param>
        /// <returns>Full BoostOpsConfig or null if not found</returns>
        public static BoostOpsConfig LoadConfigFromResources(string resourcePath = "BoostOps/cross_promo_local")
        {
            try
            {
                var textAsset = Resources.Load<TextAsset>(resourcePath);
                if (textAsset == null)
                {
                    BoostOpsLogger.LogWarning("CampaignParser", $"Config file not found in Resources: {resourcePath}");
                    return null;
                }

                BoostOpsLogger.LogDebug("CampaignParser", $"Loading config from Resources: {resourcePath}");
                var jsonContent = textAsset.text;
                
                var config = BoostOpsConfig.ParseFromJson(jsonContent, CampaignParsingMode.LocalOnly);
                BoostOpsLogger.LogInfo("CampaignParser", $"Loaded config with {config.campaigns.Count} campaigns from Resources");
                
                return config;
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("CampaignParser", $"Error loading config from Resources: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Parse campaigns from Unity Remote Settings using the crossPromoConfig key
        /// This is the preferred method for Unity Remote Settings integration
        /// </summary>
        public static List<Campaign> ParseCampaignsFromUnityRemoteSettings(string configKey = "crossPromoConfig")
        {
            var campaigns = new List<Campaign>();
            
            try
            {
#if UNITY_REMOTE_SETTINGS
                // Get the JSON string from Unity Remote Settings
                var jsonString = UnityEngine.RemoteSettings.GetString(configKey, "{}");
                
                if (string.IsNullOrEmpty(jsonString) || jsonString.Trim() == "{}")
                {
                    Debug.LogWarning($"[BoostOps] No data found for Unity Remote Settings key: {configKey}");
                    return campaigns;
                }
                
                var config = BoostOpsConfig.ParseFromUnityRemoteSettingsJson(jsonString);
                campaigns = config.GetAllCampaigns();
                
                Debug.Log($"[BoostOps] Loaded {campaigns.Count} campaigns from Unity Remote Settings key: {configKey}");
#else
                Debug.LogWarning("[BoostOps] Unity Remote Settings not available. Install Unity Remote Settings package.");
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Failed to parse campaigns from Unity Remote Settings: {ex.Message}");
            }
            
            return campaigns;
        }
        
        /// <summary>
        /// Parse campaigns from Unity Remote Settings with a CrossPromoConfig object
        /// Use this if you already have the config object from Unity Remote Settings
        /// </summary>
        public static List<Campaign> ParseCampaignsFromCrossPromoConfig(CrossPromoConfig crossPromoConfig)
        {
            if (crossPromoConfig == null)
            {
                Debug.LogWarning("[BoostOps] Null CrossPromoConfig provided");
                return new List<Campaign>();
            }
            
            var config = BoostOpsConfig.ParseFromUnityRemoteSettings(crossPromoConfig);
            return config.GetAllCampaigns();
        }

        /// <summary>
        /// Parse campaigns from Firebase Remote Config or other remote config sources
        /// </summary>
        public static List<Campaign> ParseCampaignsFromRemoteConfig(string configKey = "boostops_config")
        {
            var campaigns = new List<Campaign>();
            
            try
            {
#if FIREBASE_REMOTE_CONFIG
                var jsonString = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue(configKey).StringValue;
                
                if (string.IsNullOrEmpty(jsonString))
                {
                    Debug.LogWarning($"[BoostOps] No data found for Firebase Remote Config key: {configKey}");
                    return campaigns;
                }
                
                var config = BoostOpsConfig.ParseFromJson(jsonString);
                campaigns = config.GetAllCampaigns();
                
                Debug.Log($"[BoostOps] Loaded {campaigns.Count} campaigns from Firebase Remote Config key: {configKey}");
#else
                Debug.LogWarning("[BoostOps] Firebase Remote Config not available. Using Unity Remote Settings fallback or local data.");
                
                // Fallback to Unity Remote Settings
                campaigns = ParseCampaignsFromUnityRemoteSettings(configKey);
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Failed to parse campaigns from Firebase Remote Config: {ex.Message}");
                
                // Fallback to Unity Remote Settings
                Debug.Log("[BoostOps] Attempting Unity Remote Settings fallback...");
                campaigns = ParseCampaignsFromUnityRemoteSettings("crossPromoConfig");
            }
            
            return campaigns;
        }
        
        /// <summary>
        /// Parse campaigns from JSON string - supports multiple formats
        /// Handles Unity Remote Settings, structured configs, and legacy formats
        /// </summary>
        public static List<Campaign> ParseCampaignsFromJson(string json)
        {
            return ParseCampaignsFromJson(json, CampaignParsingMode.All);
        }

        /// <summary>
        /// Parse campaigns from JSON string with specific parsing mode
        /// </summary>
        public static List<Campaign> ParseCampaignsFromJson(string json, CampaignParsingMode mode)
        {
            if (string.IsNullOrEmpty(json) || json.Trim() == "{}")
            {
                Debug.LogWarning("[BoostOps] Empty JSON provided to parser");
                return new List<Campaign>();
            }
            
            try
            {
                BoostOpsLogger.LogDebug("Config", $"Parsing JSON in {mode} mode (length: {json.Length} chars)");
                
                // Try parsing with the main parser (handles all formats)
                var config = BoostOpsConfig.ParseFromJson(json, mode);
                if (config.campaigns.Count > 0)
                {
                    BoostOpsLogger.LogInfo("Config", $"Successfully parsed {config.campaigns.Count} campaigns");
                    return config.GetAllCampaigns();
                }
                
                // Fallback: try parsing as legacy array format (only in All mode)
                if (mode == CampaignParsingMode.All)
                {
                    var legacyList = JsonUtility.FromJson<CampaignList>($"{{\"campaigns\":{json}}}");
                    if (legacyList?.campaigns != null && legacyList.campaigns.Count > 0)
                    {
                        Debug.LogWarning("[BoostOps] Parsed using legacy array format - consider updating to newer format");
                        return legacyList.campaigns;
                    }
                }
                
                Debug.LogWarning("[BoostOps] No valid campaign format detected in JSON");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Failed to parse campaigns JSON: {ex.Message}");
            }
            
            return new List<Campaign>();
        }
        
        /// <summary>
        /// Validate campaign data with comprehensive checks
        /// </summary>
        public static bool IsValidCampaign(Campaign campaign)
        {
            if (campaign == null) return false;
            
            // Required fields check - name is optional (only used for debugging)
            if (string.IsNullOrEmpty(campaign.campaign_id))
            {
                return false;
            }
            
            // Check target project if present
            if (campaign.target_project != null)
            {
                            if (string.IsNullOrEmpty(campaign.target_project.project_id) ||
                campaign.target_project.store_urls == null ||
                !campaign.target_project.store_urls.HasAnyLinks())
                {
                    return false;
                }
            }
            
            // Date validation if provided
            if (campaign.schedule != null && !string.IsNullOrEmpty(campaign.schedule.start_date))
            {
                if (!System.DateTime.TryParse(campaign.schedule.start_date, out _))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Filter campaigns based on eligibility criteria with source project defaults support
        /// </summary>
        public static List<Campaign> FilterEligibleCampaigns(List<Campaign> campaigns, 
            int playerDay = 0, int sessionCount = 1, Dictionary<string, int> dailyImpressions = null, SourceProject sourceProject = null)
        {
            if (campaigns == null || campaigns.Count == 0) return new List<Campaign>();
            
            var eligible = new List<Campaign>();
            dailyImpressions = dailyImpressions ?? new Dictionary<string, int>();
            
            foreach (var campaign in campaigns)
            {
                Debug.Log($"[BoostOps Filter] Checking campaign: {campaign?.name} (ID: {campaign?.campaign_id})");
                
                if (!IsValidCampaign(campaign))
                {
                    Debug.LogWarning($"[BoostOps Filter] ❌ Skipping invalid campaign: {campaign?.campaign_id ?? "null"}");
                    continue;
                }
                Debug.Log($"[BoostOps Filter] ✅ Campaign is valid");
                
                // Check if campaign is active and enabled
                if (!campaign.IsActive)
                {
                    Debug.Log($"[BoostOps Filter] ❌ Campaign not active: IsActive = {campaign.IsActive}");
                    Debug.Log($"[BoostOps Filter]   Status: '{campaign.status}' (should be 'active')");
                    Debug.Log($"[BoostOps Filter]   Schedule Active: {campaign.Schedule.IsActive(DateTime.Now)}");
                    Debug.Log($"[BoostOps Filter]   Start Date: {campaign.Schedule.start_date}");
                    Debug.Log($"[BoostOps Filter]   Current Time: {DateTime.Now}");
                    Debug.Log($"[BoostOps Filter]   Days Array: [{string.Join(",", campaign.Schedule.days ?? new int[0])}]");
                    continue;
                }
                Debug.Log($"[BoostOps Filter] ✅ Campaign is active");
                
                // Check schedule (date range, days of week, hours if specified)
                if (!campaign.Schedule.IsActive(DateTime.Now))
                {
                    Debug.Log($"[BoostOps Filter] ❌ Campaign schedule not active at {DateTime.Now}");
                    continue;
                }
                Debug.Log($"[BoostOps Filter] ✅ Campaign schedule is active");
                
                // Check player day requirement (respects source project defaults)
                int effectiveMinPlayerDay = campaign.GetEffectiveMinimumPlayerDay(sourceProject);
                if (playerDay < effectiveMinPlayerDay)
                {
                    Debug.Log($"[BoostOps Filter] ❌ Player day too low: {playerDay} < {effectiveMinPlayerDay}");
                    continue;
                }
                Debug.Log($"[BoostOps Filter] ✅ Player day requirement met: {playerDay} >= {effectiveMinPlayerDay}");
                
                // Check session requirement (respects source project defaults)
                int effectiveMinSession = campaign.GetEffectiveMinimumSession(sourceProject);
                if (sessionCount < effectiveMinSession)
                {
                    Debug.Log($"[BoostOps Filter] ❌ Session count too low: {sessionCount} < {effectiveMinSession}");
                    continue;
                }
                Debug.Log($"[BoostOps Filter] ✅ Session requirement met: {sessionCount} >= {effectiveMinSession}");
                
                // Frequency cap checking removed - internal campaign management only
                // TODO: Frequency cap logic should be handled in BoostOps.Internal.dll
                
                Debug.Log($"[BoostOps Filter] 🎉 Campaign {campaign.campaign_id} passed all filters!");
                eligible.Add(campaign);
            }
            
            return eligible;
        }
    }
    
    /// <summary>
    /// Analytics ingest configuration from JSON (maps to AnalyticsConfig)
    /// </summary>
    [Serializable]
    public class AnalyticsIngestConfig
    {
        [SerializeField] public string mode = "FULL";
        [SerializeField] public bool enabled = false;
        [SerializeField] public string endpoint;
        [SerializeField] public int backoff_seconds = 86400;
        [SerializeField] public string min_sdk_version = "1.0.0";
        [SerializeField] public int[] accepted_schema_major;
        
        /// <summary>
        /// Convert ingest config to AnalyticsConfig format
        /// </summary>
        public AnalyticsConfig ToAnalyticsConfig()
        {
            return new AnalyticsConfig
            {
                enabled = enabled,
                endpoint = endpoint,
                min_sdk_version = min_sdk_version,
                accepted_schema_versions = accepted_schema_major,
                backoff_seconds = backoff_seconds,
                kill_switch = false, // Not provided in ingest format
                kill_reason = null,
                expires_at = 0 // Not provided in ingest format
            };
        }
    }
    
    /// <summary>
    /// Structured wrapper for JSON configuration parsing with Unity JsonUtility
    /// Supports both array-based and object-based campaign formats
    /// </summary>
    [Serializable]
    public class JsonConfigWrapper
    {
        [SerializeField] public BoostOpsVersionInfo version_info;
        [SerializeField] public SourceProject source_project;
        [SerializeField] public AnalyticsIngestConfig ingest; // Analytics configuration
        [SerializeField] public Campaign[] campaigns; // Array format for JsonUtility compatibility
        [SerializeField] public BoostOps.Core.AppWallsConfig app_walls; // App walls configuration
        
        public BoostOpsVersionInfo VersionInfo => version_info;
        public SourceProject SourceProject => source_project;
        public AnalyticsIngestConfig Ingest => ingest;
        public Campaign[] Campaigns => campaigns ?? new Campaign[0];
        public BoostOps.Core.AppWallsConfig AppWalls => app_walls;
    }

    /// <summary>
    /// Creative format types for campaign assets
    /// </summary>
    public enum CreativeFormat
    {
        Icon,       // App icon (square, usually 512x512)
        Banner,     // Horizontal banner for banner ads
        Hero,       // Large hero image for rich interstitials  
        Native      // Flexible native ad creative
    }
    
    /// <summary>
    /// Creative orientation for device-specific display
    /// </summary>
    public enum CreativeOrientation
    {
        Any,        // Works in any orientation
        Portrait,   // Tall/vertical orientation
        Landscape   // Wide/horizontal orientation
    }
    
    /// <summary>
    /// Individual variant of a creative (specific resolution, platform, locale, etc.)
    /// Each variant represents one actual file that can be downloaded or loaded locally
    /// </summary>
    [Serializable]
    public class CreativeVariant
    {
        [SerializeField] public string resolution;         // "512x512", "1920x1080", etc.
        [SerializeField] public string url;                // Download URL (online mode)
        [SerializeField] public string sha256;             // Hash for cache validation
        [SerializeField] public string local_key;          // Key for Resources/Addressables (offline mode)
        [SerializeField] public string platform;           // "ios", "android", "amazon" (optional)
        [SerializeField] public string locale;             // "en", "es", "ja" (optional)
        [SerializeField] public string variant_tag;        // Custom tag for A/B testing (optional)
        
        public string Resolution => resolution ?? "";
        public string Url => url ?? "";
        public string Sha256 => sha256 ?? "";
        public string LocalKey => local_key ?? "";
        public string Platform => platform ?? "";
        public string Locale => locale ?? "";
        public string VariantTag => variant_tag ?? "";
        
        /// <summary>
        /// Returns true if this variant can be loaded offline (has local_key)
        /// </summary>
        public bool IsOfflineCapable => !string.IsNullOrEmpty(local_key);
        
        /// <summary>
        /// Returns true if this variant requires online access (has URL but no local_key)
        /// </summary>
        public bool RequiresOnline => !string.IsNullOrEmpty(url) && string.IsNullOrEmpty(local_key);
        
        /// <summary>
        /// Parse resolution string to get width and height
        /// </summary>
        public (int width, int height) GetResolution()
        {
            if (string.IsNullOrEmpty(resolution)) return (0, 0);
            
            var parts = resolution.Split('x');
            if (parts.Length == 2 && 
                int.TryParse(parts[0], out int width) && 
                int.TryParse(parts[1], out int height))
            {
                return (width, height);
            }
            return (0, 0);
        }
    }
    
    /// <summary>
    /// Creative definition with format, orientation, and multiple variants
    /// Supports device-specific asset selection and offline operation
    /// </summary>
    [Serializable]
    public class Creative
    {
        [SerializeField] public string creative_id;        // Unique identifier for this creative
        [SerializeField] public string format;             // "icon", "banner", "hero", "native"
        [SerializeField] public string orientation;        // "any", "portrait", "landscape"
        [SerializeField] public bool prefetch = false;     // Download at Init() for instant display
        [SerializeField] public int ttl_hours = 24;        // Cache time-to-live in hours
        [SerializeField] public CreativeVariant[] variants; // Different resolutions/platforms/locales
        
        public string CreativeId => creative_id ?? "";
        public CreativeFormat Format => ParseFormat(format);
        public CreativeOrientation Orientation => ParseOrientation(orientation);
        public bool Prefetch => prefetch;
        public int TtlHours => ttl_hours;
        public CreativeVariant[] Variants => variants ?? new CreativeVariant[0];
        
        /// <summary>
        /// Returns true if all variants can be loaded offline
        /// </summary>
        public bool IsFullyOfflineCapable => Variants.Length > 0 && Variants.All(v => v.IsOfflineCapable);
        
        /// <summary>
        /// Returns true if any variants require online access
        /// </summary>
        public bool RequiresOnline => Variants.Any(v => v.RequiresOnline);
        
        /// <summary>
        /// Find the best variant for current device/preferences
        /// </summary>
        public CreativeVariant SelectBestVariant(string preferredPlatform = "", string preferredLocale = "", 
                                                CreativeOrientation preferredOrientation = CreativeOrientation.Any)
        {
            if (Variants.Length == 0) return null;
            if (Variants.Length == 1) return Variants[0];
            
            var candidates = Variants.ToList();
            
            // Filter by platform if specified
            if (!string.IsNullOrEmpty(preferredPlatform))
            {
                var platformMatches = candidates.Where(v => string.IsNullOrEmpty(v.Platform) || 
                                                           v.Platform.Equals(preferredPlatform, StringComparison.OrdinalIgnoreCase)).ToList();
                if (platformMatches.Count > 0) candidates = platformMatches;
            }
            
            // Filter by locale if specified  
            if (!string.IsNullOrEmpty(preferredLocale))
            {
                var localeMatches = candidates.Where(v => string.IsNullOrEmpty(v.Locale) || 
                                                         v.Locale.Equals(preferredLocale, StringComparison.OrdinalIgnoreCase)).ToList();
                if (localeMatches.Count > 0) candidates = localeMatches;
            }
            
            // Prefer cached variants if available (TODO: implement cache checking)
            // For now, just return the first candidate or highest resolution
            return candidates.OrderByDescending(v => GetResolutionScore(v.Resolution)).FirstOrDefault();
        }
        
        private static CreativeFormat ParseFormat(string format)
        {
            if (string.IsNullOrEmpty(format)) return CreativeFormat.Icon;
            
            switch (format.ToLowerInvariant())
            {
                case "icon": return CreativeFormat.Icon;
                case "banner": return CreativeFormat.Banner;
                case "hero": return CreativeFormat.Hero;
                case "native": return CreativeFormat.Native;
                default: return CreativeFormat.Icon;
            }
        }
        
        private static CreativeOrientation ParseOrientation(string orientation)
        {
            if (string.IsNullOrEmpty(orientation)) return CreativeOrientation.Any;
            
            switch (orientation.ToLowerInvariant())
            {
                case "any": return CreativeOrientation.Any;
                case "portrait": return CreativeOrientation.Portrait;
                case "landscape": return CreativeOrientation.Landscape;
                default: return CreativeOrientation.Any;
            }
        }
        
        private static int GetResolutionScore(string resolution)
        {
            if (string.IsNullOrEmpty(resolution)) return 0;
            
            var parts = resolution.Split('x');
            if (parts.Length == 2 && 
                int.TryParse(parts[0], out int width) && 
                int.TryParse(parts[1], out int height))
            {
                return width * height; // Simple area-based scoring
            }
            return 0;
        }
        
    }
} 