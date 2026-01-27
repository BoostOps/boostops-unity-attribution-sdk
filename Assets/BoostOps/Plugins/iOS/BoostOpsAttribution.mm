#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

// iOS 14.3+ AdServices framework (preferred)
#if __has_include(<AdServices/AdServices.h>)
#import <AdServices/AdServices.h>
#define HAS_ADSERVICES 1
#else
#define HAS_ADSERVICES 0
#endif

// Note: iAd framework was deprecated and removed by Apple
// We only use the modern AdServices framework (iOS 14.3+)

// Unity callback function
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

extern "C" {
    
    /// Check if AdServices framework is available (iOS 14.3+)
    bool _BoostOps_IsAdServicesAvailable() {
#if HAS_ADSERVICES
        if (@available(iOS 14.3, *)) {
            return true;
        }
#endif
        return false;
    }
    
    /// Get attribution token using AdServices framework (iOS 14.3+)
    /// Returns token immediately or null if not available
    const char* _BoostOps_GetAttributionToken() {
#if HAS_ADSERVICES
        if (@available(iOS 14.3, *)) {
            NSError *error = nil;
            NSString *token = [AAAttribution attributionTokenWithError:&error];
            
            if (error) {
                NSLog(@"[BoostOps] Attribution token error: %@", error.localizedDescription);
                
                // Common error codes:
                // 1 = AAAttributionErrorCodeInternalError
                // 2 = AAAttributionErrorCodeUnsupportedPlatform
                switch (error.code) {
                        case 1:
                            NSLog(@"[BoostOps] Attribution internal error - likely no Apple Search Ads attribution");
                            break;
                        case 2:
                            NSLog(@"[BoostOps] Attribution unsupported platform");
                            break;
                        default:
                            NSLog(@"[BoostOps] Attribution unknown error code: %ld", (long)error.code);
                            break;
                    }
                    return nullptr;
                }
                
                if (token && token.length > 0) {
                    NSLog(@"[BoostOps] Attribution token collected successfully (length: %lu)", (unsigned long)token.length);
                    
                    // Convert NSString to C string
                    const char* cString = [token UTF8String];
                    
                    // Allocate memory and copy string (Unity will manage this memory)
                    char* result = (char*)malloc(strlen(cString) + 1);
                    strcpy(result, cString);
                    return result;
                } else {
                    NSLog(@"[BoostOps] Attribution token is empty - user likely didn't come from Apple Search Ads");
                    return nullptr;
                }
        }
#endif
        NSLog(@"[BoostOps] AdServices framework not available");
        return nullptr;
    }
    
    /// Legacy attribution method - no longer supported
    /// Note: iAd framework was deprecated and removed by Apple
    /// Use AdServices framework (iOS 14.3+) instead via _BoostOps_GetAttributionToken()
    void _BoostOps_RequestAttributionAsync(const char* gameObjectName, const char* callbackMethod) {
        NSLog(@"[BoostOps] ⚠️ Legacy attribution method called - iAd framework is deprecated and removed");
        NSLog(@"[BoostOps] Use AdServices framework (iOS 14.3+) instead via _BoostOps_GetAttributionToken()");
        
        // Send back null result to indicate unavailable
        NSString *objectName = [NSString stringWithUTF8String:gameObjectName];
        NSString *methodName = [NSString stringWithUTF8String:callbackMethod];
        UnitySendMessage([objectName UTF8String], [methodName UTF8String], "null");
    }
}