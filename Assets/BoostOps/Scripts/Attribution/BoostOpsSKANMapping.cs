using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoostOps.Attribution
{
    /// <summary>
    /// SKAN Conversion Mapping - Matches BoostOps Server Schema
    /// 
    /// This schema matches the server's `skan.mapping` object exactly.
    /// Server controls schema in production, SDK uses it for local testing.
    /// 
    /// Server JSON Example:
    /// {
    ///   "schema_version": 1,
    ///   "mapping_id": "hybrid-v1-2025-10-16",
    ///   "effective_from": "2025-10-16T00:00:00Z",
    ///   "skan_version": "4",
    ///   "mode": "hybrid",
    ///   "window1": { ... },
    ///   "window2": { ... },
    ///   "window3": { ... },
    ///   "tier_fallback": "prefer_fine_else_coarse",
    ///   "downgrade_behavior": "reject"
    /// }
    /// </summary>
    [Serializable]
    public class BoostOpsSKANMapping
    {
        // === SCHEMA METADATA ===
        
        /// <summary>
        /// Schema version (integer for server compatibility)
        /// Increment when schema structure changes
        /// </summary>
        public int schema_version = 1;
        
        /// <summary>
        /// Unique identifier for this mapping configuration
        /// Critical for decoding historical SKAN postbacks after schema changes
        /// Format: "{app}-{variant}-{date}" (e.g., "casino-whale-hunters-2025-10-16")
        /// </summary>
        public string mapping_id = "default-v1-2025-10-17";
        
        /// <summary>
        /// ISO 8601 timestamp when this schema became active
        /// Used for versioned rollout and postback attribution
        /// </summary>
        public string effective_from = "2025-10-17T00:00:00Z";
        
        /// <summary>
        /// SKAN API version ("3" or "4")
        /// SKAN 4.0 adds multiple postback windows and coarse values
        /// </summary>
        public string skan_version = "4";
        
        /// <summary>
        /// Conversion mode:
        /// - "hybrid": Revenue buckets + event milestones
        /// - "revenue_only": Only revenue bucketing
        /// - "event_only": Only event milestones
        /// </summary>
        public string mode = "hybrid";
        
        // === POSTBACK WINDOWS (SKAN 4.0) ===
        
        /// <summary>
        /// Window 1: Days 0-2 (fine values + coarse)
        /// Primary measurement window with fine-grained conversion values
        /// </summary>
        public SkanWindow1 window1 = new SkanWindow1();
        
        /// <summary>
        /// Window 2: Days 3-7 (coarse values only)
        /// Extended measurement for user engagement
        /// </summary>
        public SkanWindow2 window2 = new SkanWindow2();
        
        /// <summary>
        /// Window 3: Days 8-35 (coarse values only)
        /// Long-term user value tracking
        /// </summary>
        public SkanWindow3 window3 = new SkanWindow3();
        
        // === BEHAVIOR POLICIES ===
        
        /// <summary>
        /// Privacy tier fallback policy when fine values unavailable:
        /// - "prefer_fine_else_coarse": Use fine if available, coarse otherwise (default)
        /// - "coarse_only": Always use coarse (maximum privacy)
        /// - "fine_only": Only use fine (may lose some conversions)
        /// </summary>
        public string tier_fallback = "prefer_fine_else_coarse";
        
        /// <summary>
        /// Conversion value update policy:
        /// - "reject": Strict monotonic increase (newValue > oldValue)
        /// - "allow_equal": Allow same value (newValue >= oldValue)
        /// </summary>
        public string downgrade_behavior = "reject";
        
        // === HELPER METHODS ===
        
        /// <summary>
        /// Get conversion value for a purchase event based on revenue amount (USD)
        /// </summary>
        /// <param name="amountUsd">Purchase amount in USD</param>
        /// <param name="isFirstPurchase">True if this is the user's first purchase</param>
        /// <returns>Fine conversion value (0-63)</returns>
        public int GetConversionValueForPurchase(decimal amountUsd, bool isFirstPurchase)
        {
            if (window1 == null || window1.revenue_buckets == null || window1.revenue_buckets.Count == 0)
                return 0;
            
            // Find the bucket index for this revenue amount
            int bucketIndex = 0;
            for (int i = window1.revenue_buckets.Count - 1; i >= 0; i--)
            {
                if (amountUsd >= (decimal)window1.revenue_buckets[i])
                {
                    bucketIndex = i;
                    break;
                }
            }
            
            // Apply strategy
            if (window1.strategy == "max")
            {
                // Max strategy: Use highest bucket reached
                return Math.Min(bucketIndex, window1.max_fine_value);
            }
            else if (window1.strategy == "sum")
            {
                // Sum strategy: Add to current value (implemented in manager)
                return Math.Min(bucketIndex, window1.max_fine_value);
            }
            
            return bucketIndex;
        }
        
        /// <summary>
        /// Get conversion value for a milestone event
        /// </summary>
        /// <param name="eventName">Event name (e.g., "tutorial_complete")</param>
        /// <returns>Fine conversion value, or -1 if event not in milestones</returns>
        public int GetConversionValueForMilestone(string eventName)
        {
            if (window1 == null || window1.milestones == null)
                return -1;
            
            // Find milestone index (milestones are ordered by importance)
            for (int i = 0; i < window1.milestones.Count; i++)
            {
                if (window1.milestones[i] == eventName)
                {
                    // Return value offset by revenue bucket count
                    int baseValue = window1.revenue_buckets?.Count ?? 0;
                    return Math.Min(baseValue + i + 1, window1.max_fine_value);
                }
            }
            
            return -1; // Not a tracked milestone
        }
        
        /// <summary>
        /// Get coarse value for a revenue amount (for windows 2 & 3)
        /// </summary>
        public string GetCoarseValueForRevenue(decimal amountUsd, int windowNumber)
        {
            var thresholds = windowNumber == 2 ? window2?.coarse : window3?.coarse;
            if (thresholds == null) return "low";
            
            if (amountUsd >= (decimal)thresholds.high) return "high";
            if (amountUsd >= (decimal)thresholds.medium) return "medium";
            return "low";
        }
        
        /// <summary>
        /// Check if a conversion value is valid (within max_fine_value)
        /// </summary>
        public bool IsValidConversionValue(int value)
        {
            return value >= 0 && value <= (window1?.max_fine_value ?? 63);
        }
        
        /// <summary>
        /// Check if a new value can update the current value (respects downgrade_behavior)
        /// </summary>
        public bool CanUpdateValue(int oldValue, int newValue)
        {
            if (downgrade_behavior == "allow_equal")
                return newValue >= oldValue;
            else // "reject" (default)
                return newValue > oldValue;
        }
        
        /// <summary>
        /// Check if window should lock on reaching max/high value
        /// </summary>
        public bool ShouldLockWindow(int windowNumber, int fineValue, string coarseValue)
        {
            if (windowNumber == 1 && window1?.lock_on_max == true)
            {
                return fineValue >= window1.max_fine_value;
            }
            else if (windowNumber == 2 && window2?.lock_on_high == true)
            {
                return coarseValue == "high";
            }
            else if (windowNumber == 3 && window3?.lock_on_high == true)
            {
                return coarseValue == "high";
            }
            
            return false;
        }
        
        // === SERIALIZATION ===
        
        /// <summary>
        /// Parse SKAN mapping from JSON (matches server format)
        /// </summary>
        public static BoostOpsSKANMapping FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<BoostOpsSKANMapping>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps SKAN] Failed to parse SKAN mapping: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Convert SKAN mapping to JSON
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }
        
        /// <summary>
        /// Create default SKAN mapping (matches server default)
        /// </summary>
        public static BoostOpsSKANMapping CreateDefault()
        {
            return new BoostOpsSKANMapping
            {
                schema_version = 1,
                mapping_id = "default-v1-2025-10-17",
                effective_from = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                skan_version = "4",
                mode = "hybrid",
                
                window1 = new SkanWindow1
                {
                    strategy = "max",
                    revenue_buckets = new List<float> { 0f, 0.99f, 4.99f, 9.99f, 19.99f, 49.99f, 99.99f },
                    milestones = new List<string> { "tutorial_complete", "first_purchase", "level_5", "level_10" },
                    lock_on_max = true,
                    max_fine_value = 63
                },
                
                window2 = new SkanWindow2
                {
                    coarse = new CoarseThresholds { low = 0.99f, medium = 9.99f, high = 49.99f },
                    lock_on_high = true
                },
                
                window3 = new SkanWindow3
                {
                    coarse = new CoarseThresholds { low = 0.99f, medium = 9.99f, high = 49.99f },
                    lock_on_high = true
                },
                
                tier_fallback = "prefer_fine_else_coarse",
                downgrade_behavior = "reject"
            };
        }
    }
    
    // === WINDOW CONFIGURATIONS ===
    
    /// <summary>
    /// SKAN Window 1 (Days 0-2): Fine values + coarse
    /// Primary measurement window with fine-grained conversion values (0-63)
    /// </summary>
    [Serializable]
    public class SkanWindow1
    {
        /// <summary>
        /// Conversion value calculation strategy:
        /// - "max": Use highest bucket/milestone reached
        /// - "sum": Add values (cumulative)
        /// </summary>
        public string strategy = "max";
        
        /// <summary>
        /// Revenue bucket thresholds in USD (ascending order)
        /// Example: [0, 0.99, 4.99, 19.99, 49.99, 99.99]
        /// User with $7.50 purchase → bucket index 2 ($4.99-$9.99)
        /// </summary>
        public List<float> revenue_buckets = new List<float>();
        
        /// <summary>
        /// Event milestones (ordered by importance)
        /// Example: ["tutorial_complete", "first_purchase", "level_5"]
        /// Conversion values assigned sequentially after revenue buckets
        /// </summary>
        public List<string> milestones = new List<string>();
        
        /// <summary>
        /// Lock measurement window when reaching max fine value
        /// Prevents further updates after whale detected
        /// </summary>
        public bool lock_on_max = true;
        
        /// <summary>
        /// Maximum fine conversion value (0-63)
        /// Can reserve higher values for debug/testing (e.g., 55 to reserve 56-63)
        /// </summary>
        public int max_fine_value = 63;
    }
    
    /// <summary>
    /// SKAN Window 2 (Days 3-7): Coarse values only
    /// Extended measurement for user engagement ("low", "medium", "high")
    /// </summary>
    [Serializable]
    public class SkanWindow2
    {
        /// <summary>
        /// Coarse value thresholds in USD
        /// low: $0-$0.99, medium: $1-$9.99, high: $10+
        /// </summary>
        public CoarseThresholds coarse = new CoarseThresholds();
        
        /// <summary>
        /// Lock window when reaching "high" coarse value
        /// </summary>
        public bool lock_on_high = true;
    }
    
    /// <summary>
    /// SKAN Window 3 (Days 8-35): Coarse values only
    /// Long-term user value tracking
    /// </summary>
    [Serializable]
    public class SkanWindow3
    {
        /// <summary>
        /// Coarse value thresholds in USD
        /// </summary>
        public CoarseThresholds coarse = new CoarseThresholds();
        
        /// <summary>
        /// Lock window when reaching "high" coarse value
        /// </summary>
        public bool lock_on_high = true;
    }
    
    /// <summary>
    /// Coarse value thresholds (for windows 2 & 3)
    /// </summary>
    [Serializable]
    public class CoarseThresholds
    {
        /// <summary>Minimum revenue for "low" coarse value</summary>
        public float low = 0.99f;
        
        /// <summary>Minimum revenue for "medium" coarse value</summary>
        public float medium = 9.99f;
        
        /// <summary>Minimum revenue for "high" coarse value</summary>
        public float high = 49.99f;
    }
}

