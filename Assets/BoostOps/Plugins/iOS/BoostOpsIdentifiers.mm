#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AdSupport/AdSupport.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <AdServices/AdServices.h>

// BoostOps iOS Identifier Collection
// Minimal native implementations for identifiers Unity doesn't provide

// Log prefix for debugging
static NSString *const kLogPrefix = @"[BoostOps-Identifiers]";

extern "C" {
    
    // Get Identifier for Vendor (IDFV)
    // Returns: IDFV string or NULL if not available
    // Caller must free the returned string
    char* GetIOSIDFV() {
        // Get IDFV using UIDevice
        NSUUID *idfv = [[UIDevice currentDevice] identifierForVendor];
        if (idfv) {
            NSString *idfvString = [idfv UUIDString];
            NSLog(@"%@ IDFV: %@", kLogPrefix, idfvString);
            
            // Create C string copy for Unity
            const char *cString = [idfvString UTF8String];
            if (cString) {
                char *returnString = (char *)malloc(strlen(cString) + 1);
                if (returnString) {
                    strcpy(returnString, cString);
                    return returnString;
                } else {
                    NSLog(@"%@ Error: Failed to allocate memory for IDFV", kLogPrefix);
                }
            }
        } else {
            NSLog(@"%@ IDFV not available", kLogPrefix);
        }
        return NULL;
    }
    
    // Get Identifier for Advertisers (IDFA)
    // Returns: IDFA string or NULL if not available/authorized
    // Caller must free the returned string
    char* GetIOSIDFA() {
        // Check ATT authorization first
        if (@available(iOS 14, *)) {
            ATTrackingManagerAuthorizationStatus status = [ATTrackingManager trackingAuthorizationStatus];
            if (status != ATTrackingManagerAuthorizationStatusAuthorized) {
                NSLog(@"%@ IDFA not available - ATT not authorized (status: %ld)", kLogPrefix, (long)status);
                return NULL;
            }
        }
        
        // Get IDFA
        NSUUID *idfa = [[ASIdentifierManager sharedManager] advertisingIdentifier];
        if (idfa && [[ASIdentifierManager sharedManager] isAdvertisingTrackingEnabled]) {
            NSString *idfaString = [idfa UUIDString];
            
            // Check for zero IDFA (00000000-0000-0000-0000-000000000000)
            if (![idfaString isEqualToString:@"00000000-0000-0000-0000-000000000000"]) {
                NSLog(@"%@ IDFA: %@", kLogPrefix, idfaString);
                
                // Create C string copy for Unity
                const char *cString = [idfaString UTF8String];
                if (cString) {
                    char *returnString = (char *)malloc(strlen(cString) + 1);
                    if (returnString) {
                        strcpy(returnString, cString);
                        return returnString;
                    } else {
                        NSLog(@"%@ Error: Failed to allocate memory for IDFA", kLogPrefix);
                    }
                }
            } else {
                NSLog(@"%@ IDFA is zero identifier (tracking disabled)", kLogPrefix);
            }
        } else {
            NSLog(@"%@ IDFA not available or tracking disabled", kLogPrefix);
        }
        return NULL;
    }
    
    // Get Apple Search Ads Attribution Token
    // Returns: ASA token string or NULL if not available
    // Caller must free the returned string
    char* GetIOSASAToken() {
        if (@available(iOS 14.3, *)) {
            // Use the new AdServices framework for iOS 14.3+
            NSError *error = nil;
            NSString *token = [AAAttribution attributionTokenWithError:&error];
            
            if (token && !error) {
                NSLog(@"%@ ASA Token obtained (length: %lu)", kLogPrefix, (unsigned long)token.length);
                
                // Create C string copy for Unity
                const char *cString = [token UTF8String];
                if (cString) {
                    char *returnString = (char *)malloc(strlen(cString) + 1);
                    if (returnString) {
                        strcpy(returnString, cString);
                        return returnString;
                    } else {
                        NSLog(@"%@ Error: Failed to allocate memory for ASA token", kLogPrefix);
                    }
                }
            } else {
                NSLog(@"%@ ASA Token not available: %@", kLogPrefix, error.localizedDescription ?: @"Unknown error");
            }
        } else {
            NSLog(@"%@ ASA Token requires iOS 14.3+", kLogPrefix);
        }
        return NULL;
    }
    
    // Get SKAdNetwork Source Identifier
    // Returns: SKAN source ID string or NULL if not available
    // Caller must free the returned string
    char* GetIOSSKANSourceId() {
        // Note: SKAdNetwork source identifier is typically only available in specific contexts
        // and may not be directly accessible via public APIs
        
        if (@available(iOS 14.0, *)) {
            // Try to get from app launch options or stored attribution data
            // This is typically set during app launch from SKAN attribution
            
            // Check if we have stored SKAN data from app launch
            NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
            NSString *storedSourceId = [defaults stringForKey:@"BoostOps_SKAN_SourceId"];
            
            if (storedSourceId && storedSourceId.length > 0) {
                NSLog(@"%@ SKAN Source ID from stored data: %@", kLogPrefix, storedSourceId);
                
                const char *cString = [storedSourceId UTF8String];
                if (cString) {
                    char *returnString = (char *)malloc(strlen(cString) + 1);
                    if (returnString) {
                        strcpy(returnString, cString);
                        return returnString;
                    }
                }
            }
            
            // SKAdNetwork doesn't provide a direct API to get the source identifier
            // This would typically come from the install attribution data
            NSLog(@"%@ SKAN Source ID not directly accessible via public APIs", kLogPrefix);
        } else {
            NSLog(@"%@ SKAN requires iOS 14.0+", kLogPrefix);
        }
        
        return NULL;
    }
    
    // Store SKAN Source ID (called during app launch if attribution data is available)
    // This allows the SDK to store SKAN data when it's available during app launch
    void StoreIOSSKANSourceId(const char* sourceId) {
        if (!sourceId || strlen(sourceId) == 0) {
            NSLog(@"%@ Cannot store empty SKAN Source ID", kLogPrefix);
            return;
        }
        
        NSString *sourceIdString = [NSString stringWithUTF8String:sourceId];
        if (sourceIdString) {
            NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
            [defaults setObject:sourceIdString forKey:@"BoostOps_SKAN_SourceId"];
            [defaults synchronize];
            
            NSLog(@"%@ Stored SKAN Source ID: %@", kLogPrefix, sourceIdString);
        }
    }
    
    // Request App Tracking Transparency permission
    // This is a convenience method for requesting ATT permission
    void RequestIOSATTPermission() {
        if (@available(iOS 14, *)) {
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
                NSLog(@"%@ ATT Permission result: %ld", kLogPrefix, (long)status);
                
                // Post notification to Unity if needed
                // Unity can listen for this via NotificationCenter if desired
                [[NSNotificationCenter defaultCenter] postNotificationName:@"BoostOps_ATT_StatusChanged" 
                                                                    object:nil 
                                                                  userInfo:@{@"status": @(status)}];
            }];
        } else {
            NSLog(@"%@ ATT not required on iOS < 14", kLogPrefix);
        }
    }
    
    // Get device locale in industry-standard format (e.g., "en_US", "es_MX", "pt_BR")
    // Returns: Locale string or NULL if not available
    // Caller must free the returned string
    char* GetIOSLocale() {
        // Get current locale identifier (returns nil if unavailable)
        NSLocale *currentLocale = [NSLocale currentLocale];
        if (!currentLocale) {
            NSLog(@"%@ Error: NSLocale currentLocale is nil", kLogPrefix);
            return NULL;
        }
        
        NSString *locale = [currentLocale localeIdentifier];
        if (!locale || locale.length == 0) {
            NSLog(@"%@ Error: Locale identifier is nil or empty", kLogPrefix);
            return NULL;
        }
        
        // Convert to underscore format (en_US instead of en-US) for consistency
        locale = [locale stringByReplacingOccurrencesOfString:@"-" withString:@"_"];
        
        NSLog(@"%@ Device locale: %@", kLogPrefix, locale);
        
        // Convert to C string
        const char *cString = [locale UTF8String];
        if (!cString) {
            NSLog(@"%@ Error: Failed to convert locale to C string", kLogPrefix);
            return NULL;
        }
        
        // Allocate memory for return string
        char *returnString = (char *)malloc(strlen(cString) + 1);
        if (!returnString) {
            NSLog(@"%@ Error: Failed to allocate memory for locale", kLogPrefix);
            return NULL;
        }
        
        strcpy(returnString, cString);
        return returnString;
    }
}
