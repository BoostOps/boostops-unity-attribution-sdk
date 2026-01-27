#import <Foundation/Foundation.h>
#import <StoreKit/StoreKit.h>

// SKAN API availability check
#define HAS_SKAN_V4 __has_include(<StoreKit/SKAdNetwork.h>)

extern "C" {
    
    /// Check SKAN version support (returns 0, 3, or 4)
    int _BoostOps_GetSKANVersion() {
#if HAS_SKAN_V4
        if (@available(iOS 16.1, *)) {
            return 4; // SKAN 4.0 (iOS 16.1+) - coarse value, lock window
        } else if (@available(iOS 15.4, *)) {
            return 3; // SKAN 3.0 (iOS 15.4+) - postback with view-through
        } else if (@available(iOS 14.0, *)) {
            return 2; // SKAN 2.x (iOS 14.0+) - basic conversion value
        }
#endif
        return 0; // SKAN not available
    }
    
    /// Update conversion value (iOS 14.0+, SKAN 2.x/3.x)
    /// @param conversionValue: 0-63 value to report
    /// @param callbackObjectName: Unity GameObject name for callback
    /// @param callbackMethodName: Unity method name for callback
    void _BoostOps_UpdateConversionValue(int conversionValue, const char* callbackObjectName, const char* callbackMethodName) {
#if HAS_SKAN_V4
        if (@available(iOS 14.0, *)) {
            // Validate conversion value range
            if (conversionValue < 0 || conversionValue > 63) {
                NSLog(@"[BoostOps SKAN] ❌ Invalid conversion value: %d (must be 0-63)", conversionValue);
                
                // Send error callback
                if (callbackObjectName && callbackMethodName) {
                    NSString *errorJson = [NSString stringWithFormat:@"{\"success\":false,\"error\":\"Invalid conversion value: %d\",\"value\":%d}", conversionValue, conversionValue];
                    UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
                }
                return;
            }
            
            NSLog(@"[BoostOps SKAN] 📤 Updating conversion value to: %d", conversionValue);
            
            // For iOS 16.1+, prefer the new API but fall back to old one
            if (@available(iOS 16.1, *)) {
                // Use SKAN 4.0 API with fine value only (no coarse value)
                [SKAdNetwork updatePostbackConversionValue:conversionValue completionHandler:^(NSError * _Nullable error) {
                    if (error) {
                        NSLog(@"[BoostOps SKAN] ❌ Failed to update conversion value: %@", error.localizedDescription);
                        
                        if (callbackObjectName && callbackMethodName) {
                            NSString *errorJson = [NSString stringWithFormat:@"{\"success\":false,\"error\":\"%@\",\"value\":%d}", error.localizedDescription, conversionValue];
                            UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
                        }
                    } else {
                        NSLog(@"[BoostOps SKAN] ✅ Conversion value updated successfully to: %d", conversionValue);
                        
                        if (callbackObjectName && callbackMethodName) {
                            NSString *successJson = [NSString stringWithFormat:@"{\"success\":true,\"value\":%d}", conversionValue];
                            UnitySendMessage(callbackObjectName, callbackMethodName, [successJson UTF8String]);
                        }
                    }
                }];
            } else {
                // Use legacy API for iOS 14.0-16.0
                [SKAdNetwork updateConversionValue:conversionValue];
                
                // Legacy API has no callback, so we assume success
                NSLog(@"[BoostOps SKAN] ✅ Conversion value updated (legacy API, no confirmation): %d", conversionValue);
                
                if (callbackObjectName && callbackMethodName) {
                    NSString *successJson = [NSString stringWithFormat:@"{\"success\":true,\"value\":%d,\"legacy\":true}", conversionValue];
                    UnitySendMessage(callbackObjectName, callbackMethodName, [successJson UTF8String]);
                }
            }
        } else {
            NSLog(@"[BoostOps SKAN] ⚠️ SKAN not available on this iOS version");
            
            if (callbackObjectName && callbackMethodName) {
                NSString *errorJson = @"{\"success\":false,\"error\":\"SKAN not available on iOS < 14.0\"}";
                UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
            }
        }
#else
        NSLog(@"[BoostOps SKAN] ⚠️ SKAdNetwork framework not available");
        
        if (callbackObjectName && callbackMethodName) {
            NSString *errorJson = @"{\"success\":false,\"error\":\"SKAdNetwork framework not available\"}";
            UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
        }
#endif
    }
    
    /// Update conversion value with coarse value (iOS 16.1+, SKAN 4.0)
    /// @param fineValue: 0-63 fine-grained value
    /// @param coarseValue: "low", "medium", or "high"
    /// @param callbackObjectName: Unity GameObject name for callback
    /// @param callbackMethodName: Unity method name for callback
    void _BoostOps_UpdateConversionValueCoarse(int fineValue, const char* coarseValue, const char* callbackObjectName, const char* callbackMethodName) {
#if HAS_SKAN_V4
        if (@available(iOS 16.1, *)) {
            // Validate fine value range
            if (fineValue < 0 || fineValue > 63) {
                NSLog(@"[BoostOps SKAN] ❌ Invalid fine value: %d (must be 0-63)", fineValue);
                
                if (callbackObjectName && callbackMethodName) {
                    NSString *errorJson = [NSString stringWithFormat:@"{\"success\":false,\"error\":\"Invalid fine value: %d\",\"fineValue\":%d}", fineValue, fineValue];
                    UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
                }
                return;
            }
            
            // Parse coarse value
            NSString *coarseValueStr = coarseValue ? [NSString stringWithUTF8String:coarseValue] : @"low";
            SKAdNetworkCoarseConversionValue coarseEnum;
            
            if ([coarseValueStr isEqualToString:@"high"]) {
                coarseEnum = SKAdNetworkCoarseConversionValueHigh;
            } else if ([coarseValueStr isEqualToString:@"medium"]) {
                coarseEnum = SKAdNetworkCoarseConversionValueMedium;
            } else {
                coarseEnum = SKAdNetworkCoarseConversionValueLow;
            }
            
            NSLog(@"[BoostOps SKAN] 📤 Updating conversion value (SKAN 4.0): fine=%d, coarse=%@", fineValue, coarseValueStr);
            
            [SKAdNetwork updatePostbackConversionValue:fineValue coarseValue:coarseEnum completionHandler:^(NSError * _Nullable error) {
                if (error) {
                    NSLog(@"[BoostOps SKAN] ❌ Failed to update conversion value: %@", error.localizedDescription);
                    
                    if (callbackObjectName && callbackMethodName) {
                        NSString *errorJson = [NSString stringWithFormat:@"{\"success\":false,\"error\":\"%@\",\"fineValue\":%d,\"coarseValue\":\"%@\"}", error.localizedDescription, fineValue, coarseValueStr];
                        UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
                    }
                } else {
                    NSLog(@"[BoostOps SKAN] ✅ Conversion value updated successfully: fine=%d, coarse=%@", fineValue, coarseValueStr);
                    
                    if (callbackObjectName && callbackMethodName) {
                        NSString *successJson = [NSString stringWithFormat:@"{\"success\":true,\"fineValue\":%d,\"coarseValue\":\"%@\"}", fineValue, coarseValueStr];
                        UnitySendMessage(callbackObjectName, callbackMethodName, [successJson UTF8String]);
                    }
                }
            }];
        } else {
            NSLog(@"[BoostOps SKAN] ⚠️ SKAN 4.0 coarse values require iOS 16.1+, falling back to fine value only");
            
            // Fall back to fine value only for older iOS versions
            _BoostOps_UpdateConversionValue(fineValue, callbackObjectName, callbackMethodName);
        }
#else
        NSLog(@"[BoostOps SKAN] ⚠️ SKAdNetwork framework not available");
        
        if (callbackObjectName && callbackMethodName) {
            NSString *errorJson = @"{\"success\":false,\"error\":\"SKAdNetwork framework not available\"}";
            UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
        }
#endif
    }
    
    /// Update conversion value with coarse value and lock window (iOS 16.1+, SKAN 4.0)
    /// @param fineValue: 0-63 fine-grained value
    /// @param coarseValue: "low", "medium", or "high"
    /// @param lockWindow: true to lock the measurement window
    /// @param callbackObjectName: Unity GameObject name for callback
    /// @param callbackMethodName: Unity method name for callback
    void _BoostOps_UpdateConversionValueCoarseLocked(int fineValue, const char* coarseValue, bool lockWindow, const char* callbackObjectName, const char* callbackMethodName) {
#if HAS_SKAN_V4
        if (@available(iOS 16.1, *)) {
            // Validate fine value range
            if (fineValue < 0 || fineValue > 63) {
                NSLog(@"[BoostOps SKAN] ❌ Invalid fine value: %d (must be 0-63)", fineValue);
                
                if (callbackObjectName && callbackMethodName) {
                    NSString *errorJson = [NSString stringWithFormat:@"{\"success\":false,\"error\":\"Invalid fine value: %d\",\"fineValue\":%d}", fineValue, fineValue];
                    UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
                }
                return;
            }
            
            // Parse coarse value
            NSString *coarseValueStr = coarseValue ? [NSString stringWithUTF8String:coarseValue] : @"low";
            SKAdNetworkCoarseConversionValue coarseEnum;
            
            if ([coarseValueStr isEqualToString:@"high"]) {
                coarseEnum = SKAdNetworkCoarseConversionValueHigh;
            } else if ([coarseValueStr isEqualToString:@"medium"]) {
                coarseEnum = SKAdNetworkCoarseConversionValueMedium;
            } else {
                coarseEnum = SKAdNetworkCoarseConversionValueLow;
            }
            
            NSLog(@"[BoostOps SKAN] 📤 Updating conversion value (SKAN 4.0 with lock): fine=%d, coarse=%@, lock=%d", fineValue, coarseValueStr, lockWindow);
            
            [SKAdNetwork updatePostbackConversionValue:fineValue coarseValue:coarseEnum lockWindow:lockWindow completionHandler:^(NSError * _Nullable error) {
                if (error) {
                    NSLog(@"[BoostOps SKAN] ❌ Failed to update conversion value: %@", error.localizedDescription);
                    
                    if (callbackObjectName && callbackMethodName) {
                        NSString *errorJson = [NSString stringWithFormat:@"{\"success\":false,\"error\":\"%@\",\"fineValue\":%d,\"coarseValue\":\"%@\",\"lockWindow\":%d}", error.localizedDescription, fineValue, coarseValueStr, lockWindow];
                        UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
                    }
                } else {
                    NSLog(@"[BoostOps SKAN] ✅ Conversion value updated successfully: fine=%d, coarse=%@, lock=%d", fineValue, coarseValueStr, lockWindow);
                    
                    if (callbackObjectName && callbackMethodName) {
                        NSString *successJson = [NSString stringWithFormat:@"{\"success\":true,\"fineValue\":%d,\"coarseValue\":\"%@\",\"lockWindow\":%d}", fineValue, coarseValueStr, lockWindow];
                        UnitySendMessage(callbackObjectName, callbackMethodName, [successJson UTF8String]);
                    }
                }
            }];
        } else {
            NSLog(@"[BoostOps SKAN] ⚠️ SKAN 4.0 lock window requires iOS 16.1+, falling back to coarse value only");
            
            // Fall back to coarse value without lock for older iOS versions
            _BoostOps_UpdateConversionValueCoarse(fineValue, coarseValue, callbackObjectName, callbackMethodName);
        }
#else
        NSLog(@"[BoostOps SKAN] ⚠️ SKAdNetwork framework not available");
        
        if (callbackObjectName && callbackMethodName) {
            NSString *errorJson = @"{\"success\":false,\"error\":\"SKAdNetwork framework not available\"}";
            UnitySendMessage(callbackObjectName, callbackMethodName, [errorJson UTF8String]);
        }
#endif
    }
    
    /// Register app for ad network attribution (should be called on app launch)
    void _BoostOps_RegisterForAdNetworkAttribution() {
#if HAS_SKAN_V4
        if (@available(iOS 14.0, *)) {
            [SKAdNetwork registerAppForAdNetworkAttribution];
            NSLog(@"[BoostOps SKAN] ✅ Registered app for ad network attribution");
        } else {
            NSLog(@"[BoostOps SKAN] ⚠️ SKAN registration requires iOS 14.0+");
        }
#else
        NSLog(@"[BoostOps SKAN] ⚠️ SKAdNetwork framework not available");
#endif
    }
}


