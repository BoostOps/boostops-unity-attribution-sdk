//
//  BoostOpsRevenueTrackerNative.h
//  BoostOps Unity SDK - Native iOS Purchase Tracking
//
//  Copyright (c) 2024 BoostOps. All rights reserved.
//

#ifndef BoostOpsRevenueTrackerNative_h
#define BoostOpsRevenueTrackerNative_h

#import <Foundation/Foundation.h>
#import <StoreKit/StoreKit.h>

#ifdef __cplusplus
extern "C" {
#endif

// Unity C# callable functions - Revenue Tracker
void BoostOpsRevenueTracker_Initialize(void);
bool BoostOpsRevenueTracker_IsInitialized(void);
void BoostOpsRevenueTracker_SetAutoTrackingEnabled(bool enabled);
void BoostOpsRevenueTracker_SetReceiptValidationEnabled(bool enabled);
void BoostOpsRevenueTracker_SetMinTrackingAmount(double amount);
void BoostOpsRevenueTracker_TrackPurchaseManual(const char* productId, double amount, const char* currency, const char* transactionId);

// Environment detection functions
bool BoostOpsRevenueTracker_IsTestFlightEnvironment(void);
const char* BoostOpsRevenueTracker_GetAppStoreEnvironment(void);

// Callback function pointer that Unity will set
typedef void (*BoostOpsRevenueCallback)(const char* eventData);
void BoostOpsRevenueTracker_SetRevenueCallback(BoostOpsRevenueCallback callback);

#ifdef __cplusplus
}
#endif

// Native Objective-C interface (used internally)
@interface BoostOpsRevenueTrackerNative : NSObject <SKPaymentTransactionObserver>

@property (nonatomic, strong) NSString *apiKey;
@property (nonatomic, assign) BOOL autoTrackingEnabled;
@property (nonatomic, assign) BOOL receiptValidationEnabled;
@property (nonatomic, assign) double minTrackingAmount;
@property (nonatomic, assign) BOOL isInitialized;

+ (instancetype)sharedInstance;
- (void)initialize;
- (void)startObservingTransactions;
- (void)stopObservingTransactions;
- (void)trackPurchaseManually:(NSString *)productId 
                       amount:(double)amount 
                     currency:(NSString *)currency 
                transactionId:(NSString *)transactionId;

// Environment detection methods
- (BOOL)isTestFlightEnvironment;
- (NSString *)getAppStoreEnvironment;

@end

#endif /* BoostOpsRevenueTrackerNative_h */ 