using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BoostOps.Attribution
{
    /// <summary>
    /// Defines the mapping between app events and SKAN conversion values (0-63)
    /// Can be customized per app or loaded from server configuration
    /// 
    /// Schema v2.0 additions (Phase 1 - Industry-validated enhancements):
    /// - MappingId: Unique identifier for this schema version (for postback decoding)
    /// - EffectiveFrom: ISO date when this schema became active
    /// - PriceMode: How to handle multi-currency revenue bucketing
    /// - MaxFineValue: Explicit ceiling for fine conversion values
    /// - DowngradeBehavior: Whether to allow equal or reject downgrade attempts
    /// 
    /// Schema v2.1 additions (Phase 2 - Advanced features):
    /// - Window2LockOnHigh: Lock window 2 when "high" coarse value is reached
    /// - Window3LockOnHigh: Lock window 3 when "high" coarse value is reached
    /// - TierFallback: How to handle privacy tier variations
    /// - AakEnabled: Enable AdAttributionKit (Apple's successor to SKAN)
    /// </summary>
    [Serializable]
    public class BoostOpsSKANConversionSchema
    {
        // === SCHEMA METADATA ===
        public string SchemaName = "default";
        public string SchemaVersion = "1.0";
        
        // === PHASE 1: VERSION TRACKING (Critical for postback decoding) ===
        /// <summary>
        /// Unique identifier for this schema mapping (e.g., "hybrid-v1-2025-10-17")
        /// Used to decode historical postbacks correctly after schema changes
        /// </summary>
        public string MappingId = "default-v1-2025-10-17";
        
        /// <summary>
        /// ISO 8601 date when this schema became active (e.g., "2025-10-17T00:00:00Z")
        /// Used for versioned rollout and postback attribution
        /// </summary>
        public string EffectiveFrom = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        
        // === PHASE 1: CURRENCY HANDLING ===
        /// <summary>
        /// How to handle revenue bucketing across multiple currencies:
        /// - "fx_usd": Convert all amounts to USD using cached exchange rates (default, simplest)
        /// - "local": Use local currency thresholds (requires per-currency buckets)
        /// </summary>
        public string PriceMode = "fx_usd";
        
        /// <summary>
        /// Base currency for revenue thresholds (typically "USD")
        /// Used with fx_usd mode to normalize all purchases
        /// </summary>
        public string Currency = "USD";
        
        // === PHASE 1: VALUE CONSTRAINTS ===
        /// <summary>
        /// Maximum allowed fine conversion value (0-63)
        /// Default: 63 (SKAN max)
        /// Can be lowered to reserve ranges for debug/QA
        /// </summary>
        public int MaxFineValue = 63;
        
        /// <summary>
        /// Behavior when a rule would downgrade the conversion value:
        /// - "reject": Never downgrade (monotonic increase only) - DEFAULT
        /// - "allow_equal": Allow same value updates (for coarse/lock changes)
        /// </summary>
        public string DowngradeBehavior = "reject";
        
        // === PHASE 2: PER-WINDOW LOCK CONTROL (SKAN 4.0) ===
        /// <summary>
        /// Lock window 2 (days 3-7) when "high" coarse value is reached
        /// Prevents further updates in window 2 after high-value user detected
        /// Default: false (allow continued updates)
        /// </summary>
        public bool Window2LockOnHigh = false;
        
        /// <summary>
        /// Lock window 3 (days 8-35) when "high" coarse value is reached
        /// Prevents further updates in window 3 after high-value user detected
        /// Default: false (allow continued updates)
        /// </summary>
        public bool Window3LockOnHigh = false;
        
        // === PHASE 2: PRIVACY TIER FALLBACK ===
        /// <summary>
        /// How to handle SKAN privacy tier variations (crowd anonymity):
        /// - "prefer_fine_else_coarse": Use fine value if available, coarse if suppressed (DEFAULT)
        /// - "coarse_only": Always use coarse value (max privacy)
        /// - "fine_only": Only use fine value (fail if suppressed)
        /// 
        /// Apple may suppress fine values based on user population density.
        /// This policy determines SDK behavior when fine values are unavailable.
        /// </summary>
        public string TierFallback = "prefer_fine_else_coarse";
        
        // === PHASE 2: AAK FORWARD-COMPATIBILITY ===
        /// <summary>
        /// Enable AdAttributionKit (AAK) - Apple's successor to SKAN
        /// AAK mirrors SKAN APIs but adds support for re-engagement campaigns
        /// 
        /// Set to true when:
        /// - Your app supports iOS 17.4+ re-engagement
        /// - You want to prepare for SKAN deprecation
        /// 
        /// Default: false (use SKAN only)
        /// </summary>
        public bool AakEnabled = false;
        
        // === CONVERSION RULES ===
        public List<ConversionRule> Rules = new List<ConversionRule>();
        
        /// <summary>
        /// Get conversion value for an event based on the schema rules
        /// Returns null if no matching rule found
        /// </summary>
        public ConversionValueResult? GetConversionValueForEvent(string eventType, Dictionary<string, object> eventData = null)
        {
            // Find first matching rule (rules should be ordered by priority)
            foreach (var rule in Rules)
            {
                if (rule.Matches(eventType, eventData))
                {
                    return rule.GetConversionValue(eventData);
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Create default schema with common event mappings
        /// This is a starting point - customize for your app or load from server
        /// </summary>
        public static BoostOpsSKANConversionSchema CreateDefaultSchema()
        {
            var schema = new BoostOpsSKANConversionSchema
            {
                SchemaName = "default",
                SchemaVersion = "2.1", // Phase 2 enhancements
                
                // Phase 1: Version tracking
                MappingId = "default-v2.1-2025-10-17",
                EffectiveFrom = "2025-10-17T00:00:00Z",
                
                // Phase 1: Currency handling
                PriceMode = "fx_usd", // Normalize to USD (simplest)
                Currency = "USD",
                
                // Phase 1: Value constraints
                MaxFineValue = 63, // Use full SKAN range
                DowngradeBehavior = "reject", // Monotonic only (industry best practice)
                
                // Phase 2: Per-window lock control
                Window2LockOnHigh = false, // Allow continued updates
                Window3LockOnHigh = false, // Allow continued updates
                
                // Phase 2: Privacy tier fallback
                TierFallback = "prefer_fine_else_coarse", // Graceful degradation
                
                // Phase 2: AAK forward-compatibility
                AakEnabled = false // SKAN only (default)
            };
            
            // IMPORTANT: Conversion values can only INCREASE, never decrease
            // Structure your schema so values go up as user engagement increases
            
            // ===== INSTALL VS REINSTALL (0-2) =====
            // Distinguish new installs from reinstalls (industry best practice)
            
            schema.AddRule("app_open", fineValue: 1, coarseValue: "low",
                description: "First install",
                condition: data => GetBool(data, "is_first_install"));
            
            schema.AddRule("app_open", fineValue: 2, coarseValue: "low",
                description: "Reinstall (returning user)",
                condition: data => !GetBool(data, "is_first_install") && GetBool(data, "is_first_session"));
            
            // ===== ENGAGEMENT TIERS (3-15) =====
            
            // App launch tiers
            schema.AddRule("app_launch", fineValue: 1, coarseValue: "low", 
                description: "First app launch");
            
            schema.AddRule("app_launch", fineValue: 3, coarseValue: "low",
                description: "Second app launch",
                condition: data => GetInt(data, "launch_count") >= 2);
            
            schema.AddRule("app_launch", fineValue: 3, coarseValue: "low",
                description: "5+ app launches (engaged user)",
                condition: data => GetInt(data, "launch_count") >= 5);
            
            // Tutorial/onboarding completion
            schema.AddRule("tutorial_complete", fineValue: 5, coarseValue: "low",
                description: "Tutorial completed");
            
            // Early engagement milestones
            schema.AddRule("level_complete", fineValue: 7, coarseValue: "low",
                description: "First level completed",
                condition: data => GetInt(data, "level") == 1);
            
            schema.AddRule("level_complete", fineValue: 9, coarseValue: "low",
                description: "Level 3 completed (retention signal)",
                condition: data => GetInt(data, "level") >= 3);
            
            schema.AddRule("level_complete", fineValue: 12, coarseValue: "medium",
                description: "Level 10 completed (strong retention)",
                condition: data => GetInt(data, "level") >= 10);
            
            // Registration/profile creation
            schema.AddRule("registration_complete", fineValue: 10, coarseValue: "medium",
                description: "User registration completed");
            
            // Social engagement
            schema.AddRule("share", fineValue: 11, coarseValue: "medium",
                description: "Content shared");
            
            schema.AddRule("invite_sent", fineValue: 13, coarseValue: "medium",
                description: "Friend invite sent");
            
            // ===== PURCHASE TIERS (16-63) =====
            // Reserve higher values for revenue events
            
            // First purchase (any amount)
            schema.AddRule("purchase", fineValue: 16, coarseValue: "medium",
                description: "First purchase (any amount)",
                condition: data => GetBool(data, "is_first_purchase"));
            
            // Revenue brackets (example - customize for your pricing)
            schema.AddRule("purchase", fineValue: 20, coarseValue: "medium",
                description: "Purchase $0.99-$4.99",
                condition: data => {
                    var amount = GetDecimal(data, "amount");
                    return amount >= 0.99m && amount < 5.00m;
                });
            
            schema.AddRule("purchase", fineValue: 25, coarseValue: "medium",
                description: "Purchase $5.00-$9.99",
                condition: data => {
                    var amount = GetDecimal(data, "amount");
                    return amount >= 5.00m && amount < 10.00m;
                });
            
            schema.AddRule("purchase", fineValue: 30, coarseValue: "high",
                description: "Purchase $10.00-$19.99",
                condition: data => {
                    var amount = GetDecimal(data, "amount");
                    return amount >= 10.00m && amount < 20.00m;
                });
            
            schema.AddRule("purchase", fineValue: 35, coarseValue: "high",
                description: "Purchase $20.00-$49.99",
                condition: data => {
                    var amount = GetDecimal(data, "amount");
                    return amount >= 20.00m && amount < 50.00m;
                });
            
            schema.AddRule("purchase", fineValue: 45, coarseValue: "high",
                description: "Purchase $50.00-$99.99",
                condition: data => {
                    var amount = GetDecimal(data, "amount");
                    return amount >= 50.00m && amount < 100.00m;
                });
            
            schema.AddRule("purchase", fineValue: 55, coarseValue: "high",
                description: "Purchase $100+",
                condition: data => GetDecimal(data, "amount") >= 100.00m,
                shouldLockWindow: true); // Lock window for high-value purchases
            
            // Subscription events (typically higher value)
            schema.AddRule("subscription_started", fineValue: 40, coarseValue: "high",
                description: "Subscription started",
                condition: data => {
                    var amount = GetDecimal(data, "amount");
                    return amount < 10.00m; // Monthly low-tier
                });
            
            schema.AddRule("subscription_started", fineValue: 50, coarseValue: "high",
                description: "Premium subscription started",
                condition: data => GetDecimal(data, "amount") >= 10.00m,
                shouldLockWindow: true);
            
            // Subscription renewal (repeat revenue signal)
            schema.AddRule("subscription_renewed", fineValue: 60, coarseValue: "high",
                description: "Subscription renewed (LTV signal)",
                shouldLockWindow: true);
            
            // Ultimate conversion: high LTV user (whale detection)
            schema.AddRule("purchase", fineValue: 63, coarseValue: "high",
                description: "Whale: $500+ lifetime revenue",
                condition: data => GetDecimal(data, "cumulative_revenue") >= 500.00m,
                shouldLockWindow: true);
            
            return schema;
        }
        
        /// <summary>
        /// Create schema from JSON (for server-side configuration)
        /// </summary>
        public static BoostOpsSKANConversionSchema FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<BoostOpsSKANConversionSchema>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps SKAN] Failed to parse schema JSON: {ex.Message}");
                return CreateDefaultSchema();
            }
        }
        
        /// <summary>
        /// Export schema to JSON (for server storage)
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this, prettyPrint: true);
        }
        
        /// <summary>
        /// Validate if a conversion value is within allowed range
        /// </summary>
        public bool IsValidConversionValue(int value)
        {
            return value >= 0 && value <= MaxFineValue;
        }
        
        /// <summary>
        /// Check if updating from oldValue to newValue is allowed
        /// </summary>
        public bool CanUpdateValue(int oldValue, int newValue)
        {
            // First install (-1) can always update
            if (oldValue < 0) return true;
            
            // Check value ceiling
            if (!IsValidConversionValue(newValue)) return false;
            
            // Apply downgrade behavior
            if (DowngradeBehavior == "allow_equal")
            {
                return newValue >= oldValue; // Allow equal or higher
            }
            else // "reject" (default)
            {
                return newValue > oldValue; // Strict monotonic increase
            }
        }
        
        /// <summary>
        /// Get effective currency mode
        /// </summary>
        public bool IsLocalCurrencyMode()
        {
            return PriceMode == "local";
        }
        
        /// <summary>
        /// Get effective currency mode
        /// </summary>
        public bool IsFxUsdMode()
        {
            return PriceMode == "fx_usd" || string.IsNullOrEmpty(PriceMode);
        }
        
        /// <summary>
        /// Convert an amount in any currency to USD for revenue bucketing (Phase 1 - V1.0)
        /// 
        /// V1.0: Uses hardcoded rates from BoostOpsCurrencyHelper (updated monthly)
        /// V2.0+: Will use server-side conversion with real-time rates
        /// 
        /// If PriceMode is "local", returns original amount (no conversion)
        /// If PriceMode is "fx_usd", converts to USD using hardcoded rates
        /// </summary>
        public decimal ConvertToUsd(decimal amount, string currency)
        {
            // Local mode: don't convert
            if (IsLocalCurrencyMode())
                return amount;
            
            // FX_USD mode: convert to USD (v1: hardcoded rates)
            return BoostOps.Utilities.BoostOpsCurrencyHelper.ConvertToUsd(amount, currency);
        }
        
        /// <summary>
        /// Check if window 2 should lock on high coarse value (Phase 2)
        /// </summary>
        public bool ShouldLockWindow2OnHigh()
        {
            return Window2LockOnHigh;
        }
        
        /// <summary>
        /// Check if window 3 should lock on high coarse value (Phase 2)
        /// </summary>
        public bool ShouldLockWindow3OnHigh()
        {
            return Window3LockOnHigh;
        }
        
        /// <summary>
        /// Get tier fallback policy (Phase 2)
        /// </summary>
        public string GetTierFallbackPolicy()
        {
            if (string.IsNullOrEmpty(TierFallback))
                return "prefer_fine_else_coarse"; // Default
            return TierFallback;
        }
        
        /// <summary>
        /// Check if fine values should be preferred (Phase 2)
        /// </summary>
        public bool PreferFineValues()
        {
            return GetTierFallbackPolicy() != "coarse_only";
        }
        
        /// <summary>
        /// Check if AdAttributionKit (AAK) is enabled (Phase 2)
        /// </summary>
        public bool IsAakEnabled()
        {
            return AakEnabled;
        }
        
        /// <summary>
        /// Add a rule to the schema
        /// </summary>
        private void AddRule(string eventType, int fineValue, string coarseValue = "low", 
            string description = "", Func<Dictionary<string, object>, bool> condition = null, 
            bool shouldLockWindow = false)
        {
            Rules.Add(new ConversionRule
            {
                EventType = eventType,
                FineValue = fineValue,
                CoarseValue = coarseValue,
                Description = description,
                Condition = condition,
                ShouldLockWindow = shouldLockWindow
            });
        }
        
        // Helper methods for extracting typed values from event data
        private static int GetInt(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key)) return 0;
            var value = data[key];
            if (value is int intValue) return intValue;
            if (int.TryParse(value?.ToString(), out var parsed)) return parsed;
            return 0;
        }
        
        private static decimal GetDecimal(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key)) return 0m;
            var value = data[key];
            if (value is decimal decValue) return decValue;
            if (value is float floatValue) return (decimal)floatValue;
            if (value is double doubleValue) return (decimal)doubleValue;
            if (decimal.TryParse(value?.ToString(), out var parsed)) return parsed;
            return 0m;
        }
        
        private static bool GetBool(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key)) return false;
            var value = data[key];
            if (value is bool boolValue) return boolValue;
            if (bool.TryParse(value?.ToString(), out var parsed)) return parsed;
            return false;
        }
    }
    
    /// <summary>
    /// Individual conversion rule that maps events to SKAN values
    /// </summary>
    [Serializable]
    public class ConversionRule
    {
        public string EventType;
        public int FineValue; // 0-63
        public string CoarseValue; // "low", "medium", "high" (SKAN 4.0+)
        public string Description;
        public bool ShouldLockWindow; // Lock measurement window (SKAN 4.0+)
        
        // Note: Condition functions can't be serialized to JSON
        // For server-side schemas, use a simple condition language instead
        [NonSerialized]
        public Func<Dictionary<string, object>, bool> Condition;
        
        /// <summary>
        /// Check if this rule matches the given event
        /// </summary>
        public bool Matches(string eventType, Dictionary<string, object> eventData)
        {
            if (EventType != eventType) return false;
            if (Condition == null) return true;
            
            try
            {
                return Condition(eventData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps SKAN] Rule condition failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get the conversion value for this rule
        /// </summary>
        public ConversionValueResult GetConversionValue(Dictionary<string, object> eventData)
        {
            return new ConversionValueResult
            {
                FineValue = FineValue,
                CoarseValue = CoarseValue,
                ShouldLockWindow = ShouldLockWindow,
                Description = Description
            };
        }
    }
    
    /// <summary>
    /// Result of a conversion value calculation
    /// </summary>
    public struct ConversionValueResult
    {
        public int FineValue; // 0-63 for SKAN reporting
        public string CoarseValue; // "low", "medium", "high" for SKAN 4.0
        public bool ShouldLockWindow; // Lock measurement window
        public string Description; // For debugging
        
        public override string ToString()
        {
            return $"Fine: {FineValue}, Coarse: {CoarseValue}, Lock: {ShouldLockWindow} ({Description})";
        }
    }
}


