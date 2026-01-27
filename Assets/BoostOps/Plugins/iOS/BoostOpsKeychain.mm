#import <Foundation/Foundation.h>
#import <Security/Security.h>

// BoostOps Keychain storage for cross-app persistent identifier
// Uses the same patterns as Branch and AppsFlyer for maximum compatibility
// Enables cross-app sharing via Keychain Access Groups (same Team ID)

// Service name for Keychain items (similar to AppsFlyer's "AppsFlyerKey")
static NSString *const kBoostOpsKeychainService = @"BoostOpsKey";

// Account name for the boostops_id Keychain item
static NSString *const kBoostOpsIdAccount = @"boostops_id";

// Log prefix for debugging
static NSString *const kLogPrefix = @"[BoostOps-Keychain]";

// Forward declarations
NSString* getTeamIdentifierFromProvisioningProfile(void);

// Helper function to get Team ID for Keychain Access Group
NSString* getTeamIdentifier() {
    // Method 1: Try to get Team ID from provisioning profile (most reliable)
    NSString *teamIdFromProfile = getTeamIdentifierFromProvisioningProfile();
    if (teamIdFromProfile && [teamIdFromProfile length] == 10) {
        NSLog(@"%@ Team ID from provisioning profile: %@", kLogPrefix, teamIdFromProfile);
        return teamIdFromProfile;
    }
    
    // Method 2: Try to get Team ID from Keychain access group probe
    NSDictionary *query = @{
        (__bridge NSString *)kSecClass: (__bridge NSString *)kSecClassGenericPassword,
        (__bridge NSString *)kSecAttrAccount: @"teamid-probe",
        (__bridge NSString *)kSecAttrService: @"",
        (__bridge NSString *)kSecReturnAttributes: @YES
    };
    
    CFDictionaryRef result = nil;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, (CFTypeRef *)&result);
    
    if (status == errSecSuccess && result) {
        NSString *accessGroup = [(__bridge NSDictionary *)result objectForKey:(__bridge NSString *)kSecAttrAccessGroup];
        if (accessGroup) {
            // Extract Team ID from access group (format: TEAMID.bundle.identifier)
            NSArray *components = [accessGroup componentsSeparatedByString:@"."];
            if (components.count > 0) {
                NSString *teamId = components[0];
                CFRelease(result);
                
                // Validate Team ID format (should be 10 alphanumeric characters)
                if ([teamId length] == 10) {
                    NSLog(@"%@ Team ID from Keychain access group: %@", kLogPrefix, teamId);
                    return teamId;
                } else {
                    NSLog(@"%@ Invalid Team ID format from access group: %@", kLogPrefix, teamId);
                }
            }
        }
        CFRelease(result);
    }
    
    // Method 3: Fallback to bundle identifier (no cross-app sharing)
    NSString *bundleId = [[NSBundle mainBundle] bundleIdentifier];
    if (bundleId) {
        NSLog(@"%@ Using bundle ID fallback (no cross-app sharing): %@", kLogPrefix, bundleId);
        return bundleId;
    }
    
    NSLog(@"%@ Warning: Could not determine Team ID, cross-app sharing disabled", kLogPrefix);
    return nil;
}

// Enhanced Team ID detection from embedded provisioning profile
NSString* getTeamIdentifierFromProvisioningProfile() {
    NSString *profilePath = [[NSBundle mainBundle] pathForResource:@"embedded" ofType:@"mobileprovision"];
    if (!profilePath) {
        NSLog(@"%@ No embedded provisioning profile found", kLogPrefix);
        return nil;
    }
    
    // Read provisioning profile with error handling
    NSError *readError = nil;
    NSString *profileString = [NSString stringWithContentsOfFile:profilePath 
                                                        encoding:NSISOLatin1StringEncoding 
                                                           error:&readError];
    if (!profileString) {
        NSLog(@"%@ Failed to read provisioning profile: %@", kLogPrefix, readError.localizedDescription ?: @"Unknown error");
        return nil;
    }
    
    // Extract plist from binary provisioning profile
    NSScanner *scanner = [NSScanner scannerWithString:profileString];
    if (![scanner scanUpToString:@"<plist" intoString:nil]) {
        NSLog(@"%@ No plist found in provisioning profile", kLogPrefix);
        return nil;
    }
    
    NSString *plistString;
    if (![scanner scanUpToString:@"</plist>" intoString:&plistString]) {
        NSLog(@"%@ Incomplete plist in provisioning profile", kLogPrefix);
        return nil;
    }
    
    plistString = [plistString stringByAppendingString:@"</plist>"];
    NSData *plistData = [plistString dataUsingEncoding:NSISOLatin1StringEncoding];
    
    if (!plistData) {
        NSLog(@"%@ Failed to convert plist string to data", kLogPrefix);
        return nil;
    }
    
    // Parse plist with error handling
    NSError *parseError = nil;
    NSDictionary *plist = [NSPropertyListSerialization propertyListWithData:plistData 
                                                                    options:0 
                                                                     format:nil 
                                                                      error:&parseError];
    if (!plist) {
        NSLog(@"%@ Failed to parse provisioning profile plist: %@", kLogPrefix, parseError.localizedDescription ?: @"Unknown error");
        return nil;
    }
    
    // Extract Team ID from application-identifier in entitlements
    NSString *appIdentifier = plist[@"Entitlements"][@"application-identifier"];
    if (appIdentifier) {
        // Format: "TEAMID.com.company.app" → Extract "TEAMID"
        NSArray *components = [appIdentifier componentsSeparatedByString:@"."];
        if (components.count > 0) {
            NSString *teamId = components[0];
            
            // Validate Team ID format (10 characters, alphanumeric)
            NSCharacterSet *alphanumeric = [NSCharacterSet alphanumericCharacterSet];
            NSCharacterSet *teamIdCharSet = [NSCharacterSet characterSetWithCharactersInString:teamId];
            
            if ([teamId length] == 10 && [alphanumeric isSupersetOfSet:teamIdCharSet]) {
                NSLog(@"%@ Extracted Team ID from provisioning profile: %@", kLogPrefix, teamId);
                return teamId;
            } else {
                NSLog(@"%@ Invalid Team ID format in provisioning profile: %@", kLogPrefix, teamId);
            }
        }
    }
    
    // Alternative: Try to get from team identifier field
    NSArray *teamIdentifiers = plist[@"TeamIdentifier"];
    if (teamIdentifiers && [teamIdentifiers isKindOfClass:[NSArray class]] && teamIdentifiers.count > 0) {
        NSString *teamId = teamIdentifiers[0];
        if ([teamId length] == 10) {
            NSLog(@"%@ Extracted Team ID from TeamIdentifier field: %@", kLogPrefix, teamId);
            return teamId;
        }
    }
    
    NSLog(@"%@ No valid Team ID found in provisioning profile", kLogPrefix);
    return nil;
}

// Create Keychain query dictionary for BoostOps ID
NSMutableDictionary* createKeychainQuery() {
    NSMutableDictionary *query = [[NSMutableDictionary alloc] init];
    
    // Basic Keychain item configuration
    [query setObject:(__bridge id)kSecClassGenericPassword forKey:(__bridge id)kSecClass];
    [query setObject:kBoostOpsKeychainService forKey:(__bridge id)kSecAttrService];
    [query setObject:kBoostOpsIdAccount forKey:(__bridge id)kSecAttrAccount];
    
    // Enable cross-app sharing if Team ID is available
    NSString *teamId = getTeamIdentifier();
    if (teamId) {
        // Create access group: TEAMID.io.boostops.shared
        NSString *accessGroup = [NSString stringWithFormat:@"%@.io.boostops.shared", teamId];
        [query setObject:accessGroup forKey:(__bridge id)kSecAttrAccessGroup];
        NSLog(@"%@ Using access group: %@", kLogPrefix, accessGroup);
    }
    
    // Set accessibility (survives device restart, requires unlock)
    [query setObject:(__bridge id)kSecAttrAccessibleAfterFirstUnlock forKey:(__bridge id)kSecAttrAccessible];
    
    return query;
}

extern "C" {
    
    // Store BoostOps ID in Keychain with cross-app sharing
    // Returns: 1 = success, 0 = failure
    int storeBoostOpsIdInKeychain(const char* boostopsId) {
        if (!boostopsId || strlen(boostopsId) == 0) {
            NSLog(@"%@ Error: Cannot store empty BoostOps ID", kLogPrefix);
            return 0;
        }
        
        NSString *idString = [NSString stringWithUTF8String:boostopsId];
        NSData *idData = [idString dataUsingEncoding:NSUTF8StringEncoding];
        
        if (!idData) {
            NSLog(@"%@ Error: Failed to convert BoostOps ID to data", kLogPrefix);
            return 0;
        }
        
        NSMutableDictionary *query = createKeychainQuery();
        if (!query) {
            NSLog(@"%@ Error: Failed to create Keychain query", kLogPrefix);
            return 0;
        }
        
        NSMutableDictionary *updateQuery = [query mutableCopy];
        if (!updateQuery) {
            NSLog(@"%@ Error: Failed to create update query", kLogPrefix);
            return 0;
        }
        
        // Set the data to store
        [updateQuery setObject:idData forKey:(__bridge id)kSecValueData];
        
        // Try to update existing item first
        OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)@{(__bridge id)kSecValueData: idData});
        
        if (status == errSecItemNotFound) {
            // Item doesn't exist, create new one
            status = SecItemAdd((__bridge CFDictionaryRef)updateQuery, NULL);
        }
        
        if (status == errSecSuccess) {
            NSLog(@"%@ ✅ Successfully stored BoostOps ID in Keychain", kLogPrefix);
            return 1;
        } else {
            NSLog(@"%@ ❌ Failed to store BoostOps ID in Keychain. Status: %d", kLogPrefix, (int)status);
            
            // Log specific error cases
            if (status == errSecMissingEntitlement) {
                NSLog(@"%@ Error: Missing Keychain entitlements. Add Keychain Sharing capability.", kLogPrefix);
            } else if (status == errSecNotAvailable) {
                NSLog(@"%@ Error: Keychain not available (device locked or simulator)", kLogPrefix);
            }
            
            return 0;
        }
    }
    
    // Retrieve BoostOps ID from Keychain
    // Returns: C string with BoostOps ID, or NULL if not found
    // Caller must free the returned string
    char* retrieveBoostOpsIdFromKeychain() {
        NSMutableDictionary *query = createKeychainQuery();
        if (!query) {
            NSLog(@"%@ Error: Failed to create Keychain query", kLogPrefix);
            return NULL;
        }
        
        // Request the data to be returned
        [query setObject:@YES forKey:(__bridge id)kSecReturnData];
        [query setObject:(__bridge id)kSecMatchLimitOne forKey:(__bridge id)kSecMatchLimit];
        
        CFDataRef result = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, (CFTypeRef *)&result);
        
        if (status == errSecSuccess && result) {
            NSData *idData = (__bridge_transfer NSData *)result;
            NSString *idString = [[NSString alloc] initWithData:idData encoding:NSUTF8StringEncoding];
            
            if (idString && idString.length > 0) {
                NSLog(@"%@ ✅ Successfully retrieved BoostOps ID from Keychain", kLogPrefix);
                
                // Create C string copy for Unity
                const char *cString = [idString UTF8String];
                if (cString) {
                    char *returnString = (char *)malloc(strlen(cString) + 1);
                    if (returnString) {
                        strcpy(returnString, cString);
                        return returnString;
                    } else {
                        NSLog(@"%@ Error: Failed to allocate memory for return string", kLogPrefix);
                    }
                } else {
                    NSLog(@"%@ Error: Failed to convert NSString to UTF8", kLogPrefix);
                }
            } else {
                NSLog(@"%@ Error: Retrieved data is not valid UTF8 string", kLogPrefix);
            }
        } else if (status == errSecItemNotFound) {
            NSLog(@"%@ BoostOps ID not found in Keychain (first launch)", kLogPrefix);
        } else {
            NSLog(@"%@ ❌ Failed to retrieve BoostOps ID from Keychain. Status: %d", kLogPrefix, (int)status);
        }
        
        return NULL;
    }
    
    // Delete BoostOps ID from Keychain (for testing/reset)
    // Returns: 1 = success, 0 = failure
    int deleteBoostOpsIdFromKeychain() {
        NSMutableDictionary *query = createKeychainQuery();
        if (!query) {
            NSLog(@"%@ Error: Failed to create Keychain query", kLogPrefix);
            return 0;
        }
        
        OSStatus status = SecItemDelete((__bridge CFDictionaryRef)query);
        
        if (status == errSecSuccess) {
            NSLog(@"%@ ✅ Successfully deleted BoostOps ID from Keychain", kLogPrefix);
            return 1;
        } else if (status == errSecItemNotFound) {
            NSLog(@"%@ BoostOps ID was not found in Keychain (already deleted)", kLogPrefix);
            return 1; // Consider this success
        } else {
            NSLog(@"%@ ❌ Failed to delete BoostOps ID from Keychain. Status: %d", kLogPrefix, (int)status);
            return 0;
        }
    }
    
    // Check if BoostOps ID exists in Keychain (without retrieving it)
    // Returns: 1 = exists, 0 = not found
    int boostOpsIdExistsInKeychain() {
        NSMutableDictionary *query = createKeychainQuery();
        if (!query) {
            NSLog(@"%@ Error: Failed to create Keychain query", kLogPrefix);
            return 0;
        }
        [query setObject:(__bridge id)kSecMatchLimitOne forKey:(__bridge id)kSecMatchLimit];
        
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, NULL);
        
        if (status == errSecSuccess) {
            NSLog(@"%@ BoostOps ID exists in Keychain", kLogPrefix);
            return 1;
        } else {
            NSLog(@"%@ BoostOps ID does not exist in Keychain. Status: %d", kLogPrefix, (int)status);
            return 0;
        }
    }
    
    // Debug function: List all BoostOps Keychain items
    void debugBoostOpsKeychainItems() {
        NSLog(@"%@ === DEBUG: BoostOps Keychain Items ===", kLogPrefix);
        
        NSDictionary *query = @{
            (__bridge NSString *)kSecClass: (__bridge NSString *)kSecClassGenericPassword,
            (__bridge NSString *)kSecAttrService: kBoostOpsKeychainService,
            (__bridge NSString *)kSecReturnAttributes: @YES,
            (__bridge NSString *)kSecReturnData: @YES,
            (__bridge NSString *)kSecMatchLimit: (__bridge NSString *)kSecMatchLimitAll
        };
        
        CFArrayRef result = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, (CFTypeRef *)&result);
        
        if (status == errSecSuccess && result) {
            NSArray *items = (__bridge_transfer NSArray *)result;
            NSLog(@"%@ Found %lu BoostOps Keychain items:", kLogPrefix, (unsigned long)items.count);
            
            for (NSDictionary *item in items) {
                NSString *account = item[(__bridge NSString *)kSecAttrAccount];
                NSString *service = item[(__bridge NSString *)kSecAttrService];
                NSString *accessGroup = item[(__bridge NSString *)kSecAttrAccessGroup];
                NSData *data = item[(__bridge NSString *)kSecValueData];
                NSString *value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
                
                NSLog(@"%@   Account: %@, Service: %@, AccessGroup: %@, Value: %@", 
                      kLogPrefix, account, service, accessGroup, 
                      value ? [value substringToIndex:MIN(20, value.length)] : @"<invalid>");
            }
        } else {
            NSLog(@"%@ No BoostOps Keychain items found. Status: %d", kLogPrefix, (int)status);
        }
        
        NSLog(@"%@ === END DEBUG ===", kLogPrefix);
    }
    
    // Free native string allocated with malloc()
    // CRITICAL: Unity must call this to free strings returned from retrieveBoostOpsIdFromKeychain()
    void freeNativeString(char* str) {
        if (str != NULL) {
            free(str);
        }
    }
}