using UnityEngine;
using BoostOps;
using BoostOps.Attribution;

/// <summary>
/// Example usage of BoostOps SKAN (SKAdNetwork) API
/// Matches server schema format for consistency
/// </summary>
public class BoostOpsSKANExample : MonoBehaviour
{
    void Start()
    {
        // ========================================
        // RECOMMENDED: Let Server Control Schema
        // ========================================
        
        // Server provides full SKAN mapping in config.skan.mapping
        // SDK automatically loads it - no code needed!
        
        // Example: Load from server response JSON
        string serverMappingJson = @"
        {
          ""schema_version"": 1,
          ""mapping_id"": ""casino-game-v1-2025-10-17"",
          ""effective_from"": ""2025-10-17T00:00:00Z"",
          ""skan_version"": ""4"",
          ""mode"": ""hybrid"",
          ""window1"": {
            ""strategy"": ""max"",
            ""revenue_buckets"": [0, 0.99, 4.99, 9.99, 19.99, 49.99, 99.99],
            ""milestones"": [""tutorial_complete"", ""first_purchase"", ""level_5""],
            ""lock_on_max"": true,
            ""max_fine_value"": 63
          },
          ""window2"": {
            ""coarse"": { ""low"": 0.99, ""medium"": 9.99, ""high"": 49.99 },
            ""lock_on_high"": true
          },
          ""window3"": {
            ""coarse"": { ""low"": 0.99, ""medium"": 9.99, ""high"": 49.99 },
            ""lock_on_high"": true
          },
          ""tier_fallback"": ""prefer_fine_else_coarse"",
          ""downgrade_behavior"": ""reject""
        }";
        
        BoostOpsSKAN.LoadMappingFromJson(serverMappingJson);
        Debug.Log("✅ Loaded SKAN mapping from server");
        
        // ========================================
        // ADVANCED: Define Custom Mapping in Code
        // ========================================
        
        DefineCustomMappingInCode();
        
        // ========================================
        // TRACKING EVENTS (Automatic!)
        // ========================================
        
        // BoostOps automatically tracks:
        // - Purchases → BoostOpsSDK.TrackPurchase()
        // - Milestones → BoostOpsSDK.TrackConversionEvent()
        
        // Example purchase (SDK handles SKAN automatically):
        // In real code, use product.transactionID from Unity IAP
        BoostOpsSDK.TrackPurchase(
            amount: 4.99m,
            currency: "USD",
            productId: "starter_pack",
            transactionId: "example_transaction_id"  // Use product.transactionID in production
        );
        // → SDK converts USD amount → revenue bucket → CV=2
        // → Updates SKAN automatically!
        
        // Example milestone (SDK handles SKAN automatically):
        BoostOpsSDK.TrackConversionEvent("tutorial_complete");
        // → SDK finds milestone index → CV=8 (after revenue buckets)
        // → Updates SKAN automatically!
        
        // ========================================
        // MONITORING (Optional)
        // ========================================
        
        // Check SKAN status
        Debug.Log($"SKAN Version: {BoostOpsSKAN.GetSKANVersion()}");
        Debug.Log($"SKAN Available: {BoostOpsSKAN.IsSKANAvailable()}");
        Debug.Log($"Current CV: {BoostOpsSKAN.GetCurrentConversionValue()}");
        
        // Subscribe to updates
        BoostOpsSKAN.OnConversionValueUpdated((cv, coarse) =>
        {
            Debug.Log($"🎉 SKAN Updated: CV={cv}, Coarse={coarse}");
        });
        
        BoostOpsSKAN.OnConversionValueUpdateFailed((error) =>
        {
            Debug.LogError($"❌ SKAN Update Failed: {error}");
        });
    }
    
    void DefineCustomMappingInCode()
    {
        // Example: Casino game with whale detection
        var mapping = new BoostOpsSKANMapping
        {
            schema_version = 1,
            mapping_id = "casino-whales-2025-10-17",
            effective_from = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            skan_version = "4",
            mode = "hybrid",
            
            // Window 1 (Days 0-2): Fine values
            window1 = new SkanWindow1
            {
                strategy = "max",  // Use highest bucket reached
                
                // Revenue buckets (USD thresholds)
                revenue_buckets = new System.Collections.Generic.List<float>
                {
                    0f,      // CV=0: No purchase
                    0.99f,   // CV=1: $0.99-$4.98
                    4.99f,   // CV=2: $4.99-$9.98
                    9.99f,   // CV=3: $9.99-$19.98
                    19.99f,  // CV=4: $19.99-$49.98
                    49.99f,  // CV=5: $49.99-$99.98
                    99.99f,  // CV=6: $99.99-$199.98
                    199.99f  // CV=7: $200+
                },
                
                // Event milestones (after revenue buckets)
                milestones = new System.Collections.Generic.List<string>
                {
                    "tutorial_complete",  // CV=8
                    "first_purchase",     // CV=9
                    "level_5",            // CV=10
                    "level_10",           // CV=11
                    "high_roller"         // CV=12 ($500+ LTV)
                },
                
                lock_on_max = true,  // Lock window when reaching CV=63
                max_fine_value = 63  // Use full SKAN range
            },
            
            // Window 2 (Days 3-7): Coarse values only
            window2 = new SkanWindow2
            {
                coarse = new CoarseThresholds
                {
                    low = 0.99f,    // < $1
                    medium = 9.99f, // $1-$9.99
                    high = 49.99f   // $10+
                },
                lock_on_high = true  // Lock window on whales
            },
            
            // Window 3 (Days 8-35): Long-term tracking
            window3 = new SkanWindow3
            {
                coarse = new CoarseThresholds
                {
                    low = 0.99f,
                    medium = 9.99f,
                    high = 49.99f
                },
                lock_on_high = true
            },
            
            tier_fallback = "prefer_fine_else_coarse",  // Use fine if available
            downgrade_behavior = "reject"  // Strict monotonic increase
        };
        
        BoostOpsSKAN.SetMapping(mapping);
        
        Debug.Log("✅ Custom SKAN mapping defined");
        Debug.Log($"  Mapping ID: {mapping.mapping_id}");
        Debug.Log($"  Revenue Buckets: {mapping.window1.revenue_buckets.Count}");
        Debug.Log($"  Milestones: {mapping.window1.milestones.Count}");
    }
}
