using System;

namespace BoostOps
{
    /// <summary>
    /// Represents a native cross-promotion unit with tracking context
    /// Automatically manages unit instance ID for impression/click pairing
    /// Uses Null Object pattern - always returns a valid object (check IsAvailable instead of null)
    /// </summary>
    public class BoostOpsPromo
    {
        /// <summary>
        /// Whether this promo has an available campaign
        /// Check this instead of null - false means no campaigns were available
        /// </summary>
        public bool IsAvailable { get; internal set; }
        
        /// <summary>
        /// The underlying campaign data (null if IsAvailable = false)
        /// </summary>
        public Campaign Campaign { get; internal set; }
        
        /// <summary>
        /// Placement identifier where this promo is shown
        /// </summary>
        public string Placement { get; internal set; }
        
        /// <summary>
        /// Unique instance ID for this promo unit (auto-generated on first impression)
        /// Used to pair impression and click events for proper attribution
        /// </summary>
        public string UnitInstanceId { get; internal set; }
        
        /// <summary>
        /// Impression ID generated when the impression is tracked
        /// Used to link click events back to their originating impression
        /// </summary>
        public string ImpressionId { get; internal set; }
        
        /// <summary>
        /// Timestamp when the impression was tracked (milliseconds since epoch)
        /// Used to calculate time-to-click metrics
        /// </summary>
        public long? ImpressionTimestamp { get; internal set; }
        
        /// <summary>
        /// Format of this promo ("native", "app_wall", "banner", etc.)
        /// </summary>
        public string Format { get; internal set; }
        
        // Convenience accessors to campaign data (safe when unavailable)
        
        /// <summary>
        /// Campaign name (empty string if unavailable)
        /// </summary>
        public string Name => IsAvailable ? (Campaign?.name ?? "") : "";
        
        /// <summary>
        /// Campaign ID (empty string if unavailable)
        /// </summary>
        public string CampaignId => IsAvailable ? (Campaign?.campaign_id ?? "") : "";
        
        /// <summary>
        /// Target app/project information (null if unavailable)
        /// </summary>
        public TargetProject TargetProject => IsAvailable ? Campaign?.target_project : null;
        
        /// <summary>
        /// Campaign status (empty string if unavailable)
        /// </summary>
        public string Status => IsAvailable ? (Campaign?.status ?? "") : "";
        
        /// <summary>
        /// Campaign metadata string (raw JSON, empty string if unavailable)
        /// Use GetMetadataValue(key) for structured access
        /// </summary>
        public string Metadata => IsAvailable ? (Campaign?.metadata ?? "") : "";
        
        /// <summary>
        /// Get parsed metadata object (null if unavailable or invalid JSON)
        /// Format: { "key": "value", "another_key": "another_value" }
        /// </summary>
        public CampaignMetadata GetParsedMetadata()
        {
            return IsAvailable ? Campaign?.GetMetadata() : null;
        }
        
        /// <summary>
        /// Get a metadata value by key (null if unavailable or key doesn't exist)
        /// Example: GetMetadataValue("cross_platform_id") returns "com.luckyjackpotcasino.kenocasino"
        /// Common keys: "cross_platform_id", "category", "priority"
        /// </summary>
        public string GetMetadataValue(string key)
        {
            return IsAvailable ? Campaign?.GetMetadataValue(key) : null;
        }
        
        /// <summary>
        /// Check if metadata contains a specific key
        /// </summary>
        public bool HasMetadataKey(string key)
        {
            return IsAvailable && Campaign != null && Campaign.HasMetadataKey(key);
        }
        
        /// <summary>
        /// Get all metadata keys
        /// </summary>
        public string[] GetMetadataKeys()
        {
            return IsAvailable ? Campaign?.GetMetadataKeys() : new string[0];
        }
        
        /// <summary>
        /// Check if promo has valid campaign data
        /// Alias for IsAvailable for backward compatibility
        /// </summary>
        public bool IsValid => IsAvailable && Campaign != null && !string.IsNullOrEmpty(Campaign.campaign_id);
        
        /// <summary>
        /// Get store URL for current platform (null if unavailable)
        /// </summary>
        public string GetStoreUrl()
        {
            if (!IsAvailable) return null;
            
            var storeUrls = Campaign?.target_project?.store_urls;
            if (storeUrls == null) return null;
            
#if UNITY_IOS
            return storeUrls.apple;
#elif UNITY_ANDROID
            return storeUrls.google ?? storeUrls.amazon ?? storeUrls.samsung;
#elif UNITY_STANDALONE_OSX
            return storeUrls.apple; // Mac App Store
#else
            return storeUrls.google ?? storeUrls.apple;
#endif
        }
        
        /// <summary>
        /// Get creative asset by format (icon, banner, hero, etc.) - null if unavailable
        /// </summary>
        public Creative GetCreative(CreativeFormat format)
        {
            if (!IsAvailable) return null;
            return Campaign?.target_project?.FindCreative(format);
        }
        
        /// <summary>
        /// Get best creative variant for current platform - null if unavailable
        /// </summary>
        public CreativeVariant GetBestVariant(CreativeFormat format)
        {
            if (!IsAvailable) return null;
            return Campaign?.target_project?.FindBestVariant(format);
        }
        
        /// <summary>
        /// Internal constructor for available promos - use BoostOpsSDK.GetNativePromo() instead
        /// </summary>
        internal BoostOpsPromo(Campaign campaign, string placement, string format = "native")
        {
            IsAvailable = true;
            Campaign = campaign;
            Placement = placement;
            Format = format;
            // UnitInstanceId is set when TrackImpression is called
        }
        
        /// <summary>
        /// Private constructor for unavailable promos (Null Object pattern)
        /// </summary>
        private BoostOpsPromo(string placement, string format)
        {
            IsAvailable = false;
            Campaign = null;
            Placement = placement;
            Format = format;
        }
        
        /// <summary>
        /// Factory method to create an unavailable promo (Null Object pattern)
        /// Used when no campaigns are available for the requested placement/format
        /// </summary>
        internal static BoostOpsPromo Unavailable(string placement, string format)
        {
            return new BoostOpsPromo(placement, format);
        }
        
        /// <summary>
        /// Try to refresh this promo if it was previously unavailable.
        /// This attempts to lazy load campaigns if remote config has since been fetched.
        /// Returns true if the promo is now available (either already was or successfully refreshed).
        /// </summary>
        /// <returns>True if promo is now available, false if still unavailable</returns>
        public bool TryRefresh()
        {
            // Already available - nothing to do
            if (IsAvailable)
                return true;
            
            // Try to get a fresh promo with the same placement and format
            var freshPromo = BoostOpsSDK.GetNativePromo(Placement, Format);
            
            if (freshPromo != null && freshPromo.IsAvailable)
            {
                // Update this promo instance with the fresh campaign data
                this.IsAvailable = true;
                this.Campaign = freshPromo.Campaign;
                // Keep existing UnitInstanceId if already generated
                if (string.IsNullOrEmpty(this.UnitInstanceId) && !string.IsNullOrEmpty(freshPromo.UnitInstanceId))
                {
                    this.UnitInstanceId = freshPromo.UnitInstanceId;
                }
                
                UnityEngine.Debug.Log($"[BoostOpsPromo] Successfully refreshed promo for placement: {Placement}");
                return true;
            }
            
            // Still unavailable
            return false;
        }
    }
}

