//
//  BoostOpsAppStoreNative.m
//  BoostOps Unity SDK - Native iOS App Store Sheet Integration
//
//  Copyright (c) 2024 BoostOps. All rights reserved.
//

#import "BoostOpsAppStoreNative.h"

// Global callback function pointers for Unity
static void (*gOnSheetPresented)(void) = NULL;
static void (*gOnSheetDismissed)(void) = NULL;
static void (*gOnPurchaseStarted)(const char* appStoreId) = NULL;

#pragma mark - C Functions for Unity

void BoostOpsNative_ShowAppStoreSheet(const char* appStoreId) {
    if (!appStoreId) {
        NSLog(@"[BoostOps] Error: App Store ID cannot be null");
        return;
    }
    
    NSString *appStoreIdString = [NSString stringWithUTF8String:appStoreId];
    [[BoostOpsAppStoreNative sharedInstance] showAppStoreSheetWithAppId:appStoreIdString];
}

void BoostOpsNative_SetAppStoreSheetDelegate(void (*onPresented)(void), void (*onDismissed)(void), void (*onPurchaseStarted)(const char* appStoreId)) {
    gOnSheetPresented = onPresented;
    gOnSheetDismissed = onDismissed;
    gOnPurchaseStarted = onPurchaseStarted;
}

bool BoostOpsNative_IsAppStoreSheetAvailable(void) {
    return [[BoostOpsAppStoreNative sharedInstance] isStoreProductViewControllerAvailable];
}

#pragma mark - BoostOpsAppStoreNative Implementation

@implementation BoostOpsAppStoreNative

+ (instancetype)sharedInstance {
    static BoostOpsAppStoreNative *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[self alloc] init];
    });
    return sharedInstance;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        // Initialize properties
        _storeViewController = nil;
        _presentingViewController = nil;
    }
    return self;
}

- (BOOL)isStoreProductViewControllerAvailable {
    // Comprehensive availability check (Facebook SDK pattern)
    
    // 1. Check if class exists (framework linked)
    if ([SKStoreProductViewController class] == nil) {
        NSLog(@"[BoostOps] SKStoreProductViewController class not found - StoreKit framework may not be linked");
        return NO;
    }
    
    // 2. Check iOS version compatibility
    NSOperatingSystemVersion ios6 = {6, 0, 0};
    if (![[NSProcessInfo processInfo] isOperatingSystemAtLeastVersion:ios6]) {
        NSLog(@"[BoostOps] iOS version too old for SKStoreProductViewController (requires iOS 6.0+)");
        return NO;
    }
    
    // 3. Check if running on actual device (not simulator in some test scenarios)
    #if TARGET_IPHONE_SIMULATOR
        NSLog(@"[BoostOps] Note: Running on simulator - App Store sheet will work but won't show actual App Store");
    #endif
    
    // 4. Verify StoreKit functionality
    if (![SKPaymentQueue canMakePayments]) {
        NSLog(@"[BoostOps] Warning: Device restrictions may prevent App Store access");
        // Still return YES as sheet can display, just purchases might be restricted
    }
    
    return YES;
}

- (UIViewController *)getRootViewController {
    // Get the root view controller from Unity (industry standard pattern)
    UIWindow *keyWindow = nil;
    
    // iOS 13+ method with better error handling
    if (@available(iOS 13.0, *)) {
        NSSet<UIScene *> *connectedScenes = [UIApplication sharedApplication].connectedScenes;
        for (UIScene *scene in connectedScenes) {
            if ([scene isKindOfClass:[UIWindowScene class]]) {
                UIWindowScene *windowScene = (UIWindowScene *)scene;
                if (windowScene.activationState == UISceneActivationStateForegroundActive) {
                    // Get the key window from the scene
                    for (UIWindow *window in windowScene.windows) {
                        if (window.isKeyWindow) {
                            keyWindow = window;
                            break;
                        }
                    }
                    if (!keyWindow && windowScene.windows.count > 0) {
                        keyWindow = windowScene.windows.firstObject;
                    }
                    if (keyWindow) break;
                }
            }
        }
    }
    
    // Fallback for iOS 12 and below, or if iOS 13+ method failed
    if (!keyWindow) {
        if (@available(iOS 13.0, *)) {
            // iOS 13+ but no active scene found
            NSLog(@"[BoostOps] Warning: No active window scene found, trying application windows");
        }
        
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
        keyWindow = [UIApplication sharedApplication].keyWindow;
        
        // If keyWindow is nil, try to find any window
        if (!keyWindow) {
            NSArray<UIWindow *> *windows = [UIApplication sharedApplication].windows;
            for (UIWindow *window in windows) {
                if (window.isKeyWindow) {
                    keyWindow = window;
                    break;
                }
            }
            if (!keyWindow && windows.count > 0) {
                keyWindow = windows.firstObject;
            }
        }
#pragma clang diagnostic pop
    }
    
    if (!keyWindow) {
        NSLog(@"[BoostOps] Error: Could not find any window");
        return nil;
    }
    
    // Get root view controller with safety checks
    UIViewController *rootViewController = keyWindow.rootViewController;
    if (!rootViewController) {
        NSLog(@"[BoostOps] Error: No root view controller found");
        return nil;
    }
    
    // Navigate to the top-most presented view controller
    while (rootViewController.presentedViewController) {
        rootViewController = rootViewController.presentedViewController;
    }
    
    return rootViewController;
}

- (void)showAppStoreSheetWithAppId:(NSString *)appStoreId {
    if (!appStoreId || [appStoreId length] == 0) {
        NSLog(@"[BoostOps] Error: App Store ID cannot be null or empty");
        return;
    }
    
    // Check if StoreKit is available
    if (![self isStoreProductViewControllerAvailable]) {
        NSLog(@"[BoostOps] Error: SKStoreProductViewController not available");
        return;
    }
    
    // Check if we already have a sheet showing
    if (self.storeViewController && self.storeViewController.presentingViewController) {
        NSLog(@"[BoostOps] App Store sheet already showing");
        return;
    }
    
    // Get the root view controller
    UIViewController *rootViewController = [self getRootViewController];
    if (!rootViewController) {
        NSLog(@"[BoostOps] Error: Could not find root view controller");
        return;
    }
    
    self.presentingViewController = rootViewController;
    
    // Create and configure the store view controller
    self.storeViewController = [[SKStoreProductViewController alloc] init];
    self.storeViewController.delegate = self;
    
    // Set up the parameters
    NSDictionary *parameters = @{
        SKStoreProductParameterITunesItemIdentifier: appStoreId
    };
    
    NSLog(@"[BoostOps] Loading App Store sheet for app ID: %@", appStoreId);
    
    // Load the product information
    [self.storeViewController loadProductWithParameters:parameters completionBlock:^(BOOL result, NSError *error) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (result) {
                // Successfully loaded, present the view controller
                [self.presentingViewController presentViewController:self.storeViewController 
                                                             animated:YES 
                                                           completion:^{
                    NSLog(@"[BoostOps] App Store sheet presented successfully");
                    
                    // Call Unity callback
                    if (gOnSheetPresented) {
                        gOnSheetPresented();
                    }
                    
                    if (self.onSheetPresented) {
                        self.onSheetPresented();
                    }
                }];
            } else {
                // Failed to load
                NSLog(@"[BoostOps] Failed to load App Store product: %@", error.localizedDescription);
                
                // Clean up
                self.storeViewController = nil;
                self.presentingViewController = nil;
            }
        });
    }];
}

#pragma mark - SKStoreProductViewControllerDelegate

- (void)productViewControllerDidFinish:(SKStoreProductViewController *)viewController {
    NSLog(@"[BoostOps] App Store sheet dismissed");
    
    // Dismiss the view controller
    [viewController dismissViewControllerAnimated:YES completion:^{
        // Call Unity callback
        if (gOnSheetDismissed) {
            gOnSheetDismissed();
        }
        
        if (self.onSheetDismissed) {
            self.onSheetDismissed();
        }
        
        // Clean up
        self.storeViewController = nil;
        self.presentingViewController = nil;
    }];
}

#pragma mark - Cleanup

- (void)dealloc {
    if (self.storeViewController) {
        [self.storeViewController dismissViewControllerAnimated:NO completion:nil];
        self.storeViewController = nil;
    }
    self.presentingViewController = nil;
}

@end 