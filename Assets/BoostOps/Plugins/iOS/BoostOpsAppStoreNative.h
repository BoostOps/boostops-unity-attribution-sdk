//
//  BoostOpsAppStoreNative.h
//  BoostOps Unity SDK - Native iOS App Store Sheet Integration
//
//  Copyright (c) 2024 BoostOps. All rights reserved.
//

#ifndef BoostOpsAppStoreNative_h
#define BoostOpsAppStoreNative_h

#import <Foundation/Foundation.h>
#import <StoreKit/StoreKit.h>
#import <UIKit/UIKit.h>

#ifdef __cplusplus
extern "C" {
#endif

// Unity C# callable functions
void BoostOpsNative_ShowAppStoreSheet(const char* appStoreId);
void BoostOpsNative_SetAppStoreSheetDelegate(void (*onPresented)(void), void (*onDismissed)(void), void (*onPurchaseStarted)(const char* appStoreId));
bool BoostOpsNative_IsAppStoreSheetAvailable(void);

#ifdef __cplusplus
}
#endif

// Native Objective-C interface
@interface BoostOpsAppStoreNative : NSObject <SKStoreProductViewControllerDelegate>

@property (nonatomic, strong) SKStoreProductViewController *storeViewController;
@property (nonatomic, weak) UIViewController *presentingViewController;

// Callbacks to Unity
@property (nonatomic, copy) void (^onSheetPresented)(void);
@property (nonatomic, copy) void (^onSheetDismissed)(void);
@property (nonatomic, copy) void (^onPurchaseStarted)(NSString *appStoreId);

+ (instancetype)sharedInstance;
- (void)showAppStoreSheetWithAppId:(NSString *)appStoreId;
- (BOOL)isStoreProductViewControllerAvailable;

@end

#endif /* BoostOpsAppStoreNative_h */ 