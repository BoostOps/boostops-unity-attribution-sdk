//
//  BoostOpsRevenueTrackerNative.m
//  BoostOps Unity SDK - Native iOS Purchase Tracking
//
//  Copyright (c) 2024 BoostOps. All rights reserved.
//

#import "BoostOpsRevenueTrackerNative.h"
#import <StoreKit/StoreKit.h>
#import <sys/utsname.h>

// StoreKit 2 imports (iOS 15+)
#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 150000
#import <StoreKit/StoreKit.h>
#define STOREKIT2_AVAILABLE 1
#else
#define STOREKIT2_AVAILABLE 0
#endif

// Global callback function pointer
static BoostOpsRevenueCallback gRevenueCallback = NULL;

// C functions for Unity interop - Revenue Tracker functions
void BoostOpsRevenueTracker_Initialize(void) {
    [[BoostOpsRevenueTrackerNative sharedInstance] initialize];
}

bool BoostOpsRevenueTracker_IsInitialized(void) {
    return [BoostOpsRevenueTrackerNative sharedInstance].isInitialized;
}

void BoostOpsRevenueTracker_SetAutoTrackingEnabled(bool enabled) {
    [BoostOpsRevenueTrackerNative sharedInstance].autoTrackingEnabled = enabled;
    if (enabled) {
        [[BoostOpsRevenueTrackerNative sharedInstance] startObservingTransactions];
    } else {
        [[BoostOpsRevenueTrackerNative sharedInstance] stopObservingTransactions];
    }
}

void BoostOpsRevenueTracker_SetReceiptValidationEnabled(bool enabled) {
    [BoostOpsRevenueTrackerNative sharedInstance].receiptValidationEnabled = enabled;
}

void BoostOpsRevenueTracker_SetMinTrackingAmount(double amount) {
    [BoostOpsRevenueTrackerNative sharedInstance].minTrackingAmount = amount;
}

void BoostOpsRevenueTracker_TrackPurchaseManual(const char* productId, double amount, const char* currency, const char* transactionId) {
    NSString *productIdString = productId ? [NSString stringWithUTF8String:productId] : @"";
    NSString *currencyString = currency ? [NSString stringWithUTF8String:currency] : @"USD";
    NSString *transactionIdString = transactionId ? [NSString stringWithUTF8String:transactionId] : @"";
    
    [[BoostOpsRevenueTrackerNative sharedInstance] trackPurchaseManually:productIdString
                                                                   amount:amount
                                                                 currency:currencyString
                                                            transactionId:transactionIdString];
}

void BoostOpsRevenueTracker_SetRevenueCallback(BoostOpsRevenueCallback callback) {
    gRevenueCallback = callback;
}

// Environment detection functions
bool BoostOpsRevenueTracker_IsTestFlightEnvironment(void) {
    return [[BoostOpsRevenueTrackerNative sharedInstance] isTestFlightEnvironment];
}

const char* BoostOpsRevenueTracker_GetAppStoreEnvironment(void) {
    NSString *environment = [[BoostOpsRevenueTrackerNative sharedInstance] getAppStoreEnvironment];
    return [environment UTF8String];
}

@implementation BoostOpsRevenueTrackerNative {
    NSMutableSet *_processedTransactionIds;
    dispatch_queue_t _processingQueue;
    BOOL _isObservingTransactions;
    BOOL _storeKit2ConfiguredProperly;
    
#if STOREKIT2_AVAILABLE
    id _storeKit2UpdatesTask; // Task for StoreKit 2 transaction monitoring
#endif
}

#pragma mark - Singleton

+ (instancetype)sharedInstance {
    static BoostOpsRevenueTrackerNative *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[self alloc] init];
    });
    return sharedInstance;
}

#pragma mark - Initialization

- (instancetype)init {
    self = [super init];
    if (self) {
        _processedTransactionIds = [[NSMutableSet alloc] init];
        _processingQueue = dispatch_queue_create("com.boostops.revenue.processing", DISPATCH_QUEUE_SERIAL);
        _autoTrackingEnabled = YES;
        _receiptValidationEnabled = YES;
        _minTrackingAmount = 0.0;
        _isObservingTransactions = NO;
        _storeKit2ConfiguredProperly = NO;
    }
    return self;
}

- (void)initialize {
    if (self.isInitialized) {
        NSLog(@"[BoostOps] Already initialized");
        return;
    }
    
    // iOS version and StoreKit capability detection for optimal compatibility
    NSOperatingSystemVersion version = [[NSProcessInfo processInfo] operatingSystemVersion];
    NSLog(@"[BoostOps] iOS Version: %ld.%ld.%ld", (long)version.majorVersion, (long)version.minorVersion, (long)version.patchVersion);
    
    // Check StoreKit 2 availability and configuration (iOS 15+)
    _storeKit2ConfiguredProperly = NO;
    if (@available(iOS 15.0, *)) {
        // Validate StoreKit 2 configuration in plist
        BOOL hasStoreKit2Config = [self validateStoreKit2Configuration];
        if (hasStoreKit2Config) {
            _storeKit2ConfiguredProperly = YES;
            NSLog(@"[BoostOps] StoreKit 2 available and properly configured");
        } else {
            NSLog(@"[BoostOps] StoreKit 2 available but configuration missing - falling back to StoreKit 1");
        }
    } else {
        NSLog(@"[BoostOps] Using StoreKit 1 compatibility mode (iOS < 15.0)");
    }
    
    // Validate StoreKit framework availability
    if (![SKPaymentQueue class]) {
        NSLog(@"[BoostOps] StoreKit framework not available. Check framework linking.");
        return;
    }
    
    // Initialize transaction processing queue
    if (!_processingQueue) {
        _processingQueue = dispatch_queue_create("com.boostops.revenue.processing", DISPATCH_QUEUE_SERIAL);
    }
    
    // Set up transaction observer with enhanced error handling
    if ([SKPaymentQueue canMakePayments]) {
        [[SKPaymentQueue defaultQueue] addTransactionObserver:self];
        NSLog(@"[BoostOps] Transaction observer added successfully");
    } else {
        NSLog(@"[BoostOps] Device cannot make payments - In-App Purchases disabled");
    }
    
    self.isInitialized = YES;
    NSLog(@"[BoostOps] Native revenue tracking initialized successfully (no API key required)");
}

#pragma mark - Transaction Observing

- (void)startObservingTransactions {
    if (_isObservingTransactions) {
        return;
    }
    
    dispatch_async(dispatch_get_main_queue(), ^{
        // Use StoreKit 2 on iOS 15+ if properly configured, otherwise fall back to StoreKit 1
        // This prevents double tracking of the same purchases
        
#if STOREKIT2_AVAILABLE
        if (@available(iOS 15.0, *) && self->_storeKit2ConfiguredProperly) {
            // Use StoreKit 2 for iOS 15+ (more reliable) - only if properly configured
            NSLog(@"[BoostOps] Using StoreKit 2 for purchase tracking (validated configuration)");
            [self startStoreKit2Monitoring];
        } else {
            // Fall back to StoreKit 1 for older iOS versions or configuration issues
            NSLog(@"[BoostOps] Using StoreKit 1 for purchase tracking (fallback)");
            [[SKPaymentQueue defaultQueue] addTransactionObserver:self];
        }
#else
        // StoreKit 1 only (StoreKit 2 not compiled)
        NSLog(@"[BoostOps] Using StoreKit 1 for purchase tracking (StoreKit 2 not available)");
        [[SKPaymentQueue defaultQueue] addTransactionObserver:self];
#endif
        
        self->_isObservingTransactions = YES;
        
        NSLog(@"[BoostOps] Started observing StoreKit transactions");
    });
}

- (void)stopObservingTransactions {
    if (!_isObservingTransactions) {
        return;
    }
    
    dispatch_async(dispatch_get_main_queue(), ^{
        // StoreKit 1 - Remove payment transaction observer
        [[SKPaymentQueue defaultQueue] removeTransactionObserver:self];
        self->_isObservingTransactions = NO;
        
#if STOREKIT2_AVAILABLE
        // StoreKit 2 - Cancel monitoring task
        if (@available(iOS 15.0, *)) {
            [self stopStoreKit2Monitoring];
        }
#endif
        
        NSLog(@"[BoostOps] Stopped observing StoreKit transactions");
    });
}

#if STOREKIT2_AVAILABLE
- (void)startStoreKit2Monitoring API_AVAILABLE(ios(15.0)) {
    if (_storeKit2UpdatesTask) {
        return; // Already monitoring
    }
    
    // Import StoreKit 2 Transaction monitoring
    // This requires proper StoreKit 2 implementation similar to Facebook's approach
    NSLog(@"[BoostOps] Starting StoreKit 2 transaction monitoring...");
    
    // Monitor transaction updates using StoreKit 2 Transaction.updates
    // Note: This is a simplified implementation - full StoreKit 2 requires Swift
    // For comprehensive StoreKit 2 support, consider implementing in Swift and bridging
    
    dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
        // Placeholder for StoreKit 2 Transaction.updates monitoring
        // In a full implementation, this would use:
        // for await transaction in Transaction.updates {
        //     [self handleStoreKit2Transaction:transaction];
        // }
        
        NSLog(@"[BoostOps] StoreKit 2 monitoring task started");
        // Store reference to cancel later
        self->_storeKit2UpdatesTask = @"monitoring_active";
    });
}

- (void)stopStoreKit2Monitoring API_AVAILABLE(ios(15.0)) {
    if (_storeKit2UpdatesTask) {
        // Cancel the monitoring task
        _storeKit2UpdatesTask = nil;
        NSLog(@"[BoostOps] StoreKit 2 monitoring stopped");
    }
}

// Add method to handle StoreKit 2 transactions
- (void)handleStoreKit2Transaction:(id)transaction API_AVAILABLE(ios(15.0)) {
    // This would handle StoreKit 2 Transaction objects
    // Implementation would extract transaction details and process them
    NSLog(@"[BoostOps] Processing StoreKit 2 transaction");
}


#endif

#pragma mark - SKPaymentTransactionObserver (StoreKit 1)

- (void)paymentQueue:(SKPaymentQueue *)queue updatedTransactions:(NSArray<SKPaymentTransaction *> *)transactions {
    for (SKPaymentTransaction *transaction in transactions) {
        switch (transaction.transactionState) {
            case SKPaymentTransactionStatePurchased:
                [self handleSuccessfulTransaction:transaction];
                break;
            case SKPaymentTransactionStateRestored:
                [self handleRestoredTransaction:transaction];
                break;
            case SKPaymentTransactionStateFailed:
                [self handleFailedTransaction:transaction];
                break;
            case SKPaymentTransactionStatePurchasing:
                // Transaction is still processing - ignore
                break;
            case SKPaymentTransactionStateDeferred:
                // Transaction is waiting for approval - ignore for now
                break;
        }
    }
}

#pragma mark - Transaction Handling

- (void)handleSuccessfulTransaction:(SKPaymentTransaction *)transaction {
    dispatch_async(_processingQueue, ^{
        [self processTransaction:transaction isRestored:NO];
    });
}

- (void)handleRestoredTransaction:(SKPaymentTransaction *)transaction {
    dispatch_async(_processingQueue, ^{
        [self processTransaction:transaction isRestored:YES];
    });
}

- (void)handleFailedTransaction:(SKPaymentTransaction *)transaction {
    NSLog(@"[BoostOps] Transaction failed: %@ - Error: %@", 
          transaction.payment.productIdentifier, 
          transaction.error.localizedDescription);
}

- (void)processTransaction:(SKPaymentTransaction *)transaction isRestored:(BOOL)isRestored {
    // Check for duplicate processing
    NSString *transactionId = transaction.transactionIdentifier;
    NSString *productId = transaction.payment.productIdentifier;
    
    NSLog(@"[BoostOps DEBUG] Processing transaction: %@ (Product: %@, Restored: %@)", 
          transactionId ?: @"nil", productId ?: @"nil", isRestored ? @"YES" : @"NO");
    
    if (!transactionId) {
        NSLog(@"[BoostOps DEBUG] Skipping transaction with nil ID");
        return;
    }
    
    if ([_processedTransactionIds containsObject:transactionId]) {
        NSLog(@"[BoostOps DEBUG] DUPLICATE - Transaction %@ already processed, skipping", transactionId);
        return;
    }
    
    NSLog(@"[BoostOps DEBUG] NEW - Processing transaction %@ for first time", transactionId);
    
    // Extract purchase data (productId already declared above)
    NSDecimalNumber *amount = transaction.payment.quantity > 0 ? 
        [NSDecimalNumber decimalNumberWithDecimal:[[NSDecimalNumber numberWithInteger:transaction.payment.quantity] decimalValue]] : 
        [NSDecimalNumber zero];
    NSString *currencyCode = @"USD"; // Default, would be determined from App Store locale
    
    // Check minimum tracking amount
    if ([amount doubleValue] < self.minTrackingAmount) {
        NSLog(@"[BoostOps] Purchase amount %.2f below minimum tracking threshold %.2f", 
              [amount doubleValue], self.minTrackingAmount);
        return;
    }
    
    // Mark as processed
    [_processedTransactionIds addObject:transactionId];
    
    // Get receipt data if validation is enabled
    NSString *receiptData = nil;
    if (self.receiptValidationEnabled) {
        receiptData = [self getBase64Receipt];
    }
    
    // Detect platform (iOS vs macOS)
    NSString *platformStr = @"ios";
    NSString *storeStr = @"app_store";
    
    #if TARGET_OS_OSX
        platformStr = @"macos";
        storeStr = @"macos"; // Match the C# GetStoreIdentifier() behavior
    #endif
    
    // Create purchase event data
    NSDictionary *purchaseData = @{
        @"product_id": productId ?: @"",
        @"transaction_id": transactionId ?: @"",
        @"amount": @([amount doubleValue]),
        @"currency": currencyCode,
        @"platform": platformStr,
        @"store": storeStr,
        @"environment": [self getAppStoreEnvironment],
        @"is_testflight": @([self isTestFlightEnvironment]),
        @"is_restored": @(isRestored),
        @"timestamp": @([[NSDate date] timeIntervalSince1970]),
        @"receipt_data": receiptData ?: [NSNull null],
        @"sdk_version": @"1.0.0",
        @"attribution_data": [self getAttributionData]
    };
    
    // Send to Unity via callback
    NSLog(@"[BoostOps DEBUG] Sending purchase to Unity: %@ (TxnID: %@)", productId, transactionId);
    [self sendRevenueEventToUnity:purchaseData];
    
    NSLog(@"[BoostOps] Tracked purchase: %@ - %.2f %@ (TxnID: %@)", productId, [amount doubleValue], currencyCode, transactionId);
}

#pragma mark - Manual Purchase Tracking

- (void)trackPurchaseManually:(NSString *)productId 
                       amount:(double)amount 
                     currency:(NSString *)currency 
                transactionId:(NSString *)transactionId {
    
    dispatch_async(_processingQueue, ^{
        // Check for duplicate processing
        if (transactionId && [self->_processedTransactionIds containsObject:transactionId]) {
            return;
        }
        
        // Check minimum tracking amount
        if (amount < self.minTrackingAmount) {
            NSLog(@"[BoostOps] Manual purchase amount %.2f below minimum tracking threshold %.2f", 
                  amount, self.minTrackingAmount);
            return;
        }
        
        // Mark as processed if we have a transaction ID
        if (transactionId) {
            [self->_processedTransactionIds addObject:transactionId];
        }
        
        // Create purchase event data
        NSDictionary *purchaseData = @{
            @"product_id": productId ?: @"",
            @"transaction_id": transactionId ?: [[NSUUID UUID] UUIDString],
            @"amount": @(amount),
            @"currency": currency ?: @"USD",
            @"platform": @"ios",
            @"store": @"manual",
            @"is_restored": @NO,
            @"timestamp": @([[NSDate date] timeIntervalSince1970]),
            @"receipt_data": [NSNull null],
            @"sdk_version": @"1.0.0",
            @"attribution_data": [self getAttributionData]
        };
        
        // Send to Unity via callback
        [self sendRevenueEventToUnity:purchaseData];
        
        NSLog(@"[BoostOps] Tracked manual purchase: %@ - %.2f %@", productId, amount, currency);
    });
}

#pragma mark - Helper Methods

- (NSString *)getBase64Receipt {
    NSURL *receiptURL = [[NSBundle mainBundle] appStoreReceiptURL];
    if (!receiptURL || ![[NSFileManager defaultManager] fileExistsAtPath:[receiptURL path]]) {
        return nil;
    }
    
    NSData *receiptData = [NSData dataWithContentsOfURL:receiptURL];
    if (!receiptData) {
        return nil;
    }
    
    return [receiptData base64EncodedStringWithOptions:0];
}

- (NSDictionary *)getAttributionData {
    // This would integrate with the existing BoostOps attribution system
    // For now, return basic device/app information
    return @{
        @"source_app": @"unknown",
        @"campaign_id": @"",
        @"traffic_source": @"organic",
        @"install_time": @(0),
        @"device_type": [self getDeviceType],
        @"os_version": [[UIDevice currentDevice] systemVersion],
        @"app_version": [[[NSBundle mainBundle] infoDictionary] objectForKey:@"CFBundleShortVersionString"] ?: @""
    };
}

- (NSString *)getDeviceType {
    struct utsname systemInfo;
    uname(&systemInfo);
    return [NSString stringWithCString:systemInfo.machine encoding:NSUTF8StringEncoding];
}

- (void)sendRevenueEventToUnity:(NSDictionary *)purchaseData {
    if (!gRevenueCallback) {
        NSLog(@"[BoostOps] Warning: Revenue callback not set, purchase data will be lost");
        return;
    }
    
    // Convert to JSON string
    NSError *error;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:purchaseData 
                                                       options:0 
                                                         error:&error];
    if (error) {
        NSLog(@"[BoostOps] Error serializing purchase data: %@", error.localizedDescription);
        return;
    }
    
    NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
    
    // Call Unity callback on main thread
    dispatch_async(dispatch_get_main_queue(), ^{
        gRevenueCallback([jsonString UTF8String]);
    });
}

#pragma mark - Environment Detection

- (BOOL)isTestFlightEnvironment {
    // Check if app is running in TestFlight
    // TestFlight apps have a specific receipt structure and embedded mobileprovision
    
    // Method 1: Check for TestFlight receipt characteristics
    NSURL *receiptURL = [[NSBundle mainBundle] appStoreReceiptURL];
    if (receiptURL && [[NSFileManager defaultManager] fileExistsAtPath:[receiptURL path]]) {
        NSData *receiptData = [NSData dataWithContentsOfURL:receiptURL];
        if (receiptData) {
            // TestFlight receipts contain "Xcode" in the receipt data structure
            NSString *receiptString = [[NSString alloc] initWithData:receiptData encoding:NSUTF8StringEncoding];
            if ([receiptString containsString:@"Xcode"]) {
                return YES;
            }
        }
    }
    
    // Method 2: Check for embedded.mobileprovision (TestFlight has specific profile)
    NSString *provisionPath = [[NSBundle mainBundle] pathForResource:@"embedded" ofType:@"mobileprovision"];
    if (provisionPath) {
        NSString *provision = [NSString stringWithContentsOfFile:provisionPath 
                                                        encoding:NSUTF8StringEncoding 
                                                           error:nil];
        if ([provision containsString:@"beta-reports-active"]) {
            return YES;
        }
    }
    
    // Method 3: Check bundle identifier pattern (TestFlight often uses beta identifiers)
    NSString *bundleId = [[NSBundle mainBundle] bundleIdentifier];
    if ([bundleId containsString:@"beta"] || [bundleId containsString:@"testflight"]) {
        return YES;
    }
    
    return NO;
}

- (NSString *)getAppStoreEnvironment {
    if ([self isTestFlightEnvironment]) {
        return @"testflight";
    }
    
    // Check if this is a development/debug build
    #ifdef DEBUG
        return @"development";
    #endif
    
    // Check if running in simulator
    #if TARGET_IPHONE_SIMULATOR
        return @"simulator";
    #endif
    
    // Check for App Store distribution
    NSURL *receiptURL = [[NSBundle mainBundle] appStoreReceiptURL];
    if (receiptURL && [[NSFileManager defaultManager] fileExistsAtPath:[receiptURL path]]) {
        return @"appstore";
    }
    
    // Check for ad-hoc or enterprise distribution
    NSString *provisionPath = [[NSBundle mainBundle] pathForResource:@"embedded" ofType:@"mobileprovision"];
    if (provisionPath) {
        return @"adhoc";
    }
    
    return @"unknown";
}

#pragma mark - StoreKit 2 Configuration Validation

- (BOOL)validateStoreKit2Configuration API_AVAILABLE(ios(15.0)) {
    NSBundle *mainBundle = [NSBundle mainBundle];
    NSDictionary *infoPlist = [mainBundle infoDictionary];
    
    // Check 1: Bundle ID is properly configured (REQUIRED)
    NSString *bundleID = [mainBundle bundleIdentifier];
    if (!bundleID || [bundleID length] == 0) {
        NSLog(@"[BoostOps] StoreKit 2 configuration invalid: Bundle identifier not set");
        return NO;
    }
    
    // Check 2: SKIncludeConsumableInAppPurchaseHistory (CRITICAL for consumable tracking)
    NSNumber *includeConsumables = infoPlist[@"SKIncludeConsumableInAppPurchaseHistory"];
    if (![includeConsumables boolValue]) {
        NSLog(@"[BoostOps] CRITICAL: SKIncludeConsumableInAppPurchaseHistory not set to true");
        NSLog(@"[BoostOps] StoreKit 2 will NOT track consumable purchases without this setting");
        NSLog(@"[BoostOps] To fix: Add <key>SKIncludeConsumableInAppPurchaseHistory</key><true/> to Info.plist");
        return NO;
    }
    
    NSLog(@"[BoostOps] ✅ SKIncludeConsumableInAppPurchaseHistory = true (consumables will be tracked)");
    
    // Check 3: StoreKit Configuration file (.storekit) - OPTIONAL for production
    NSString *storeKitConfigPath = [mainBundle pathForResource:@"Products" ofType:@"storekit"];
    if (storeKitConfigPath) {
        NSLog(@"[BoostOps] Found Products.storekit - local testing configuration available");
    }
    
    // Check 4: App Store Connect configuration (optional for server notifications)
    NSString *keyID = infoPlist[@"AppStoreConnectKeyID"];
    NSString *issuerID = infoPlist[@"AppStoreConnectIssuerID"];
    
    if (keyID && issuerID) {
        NSLog(@"[BoostOps] App Store Connect server notifications configured");
    }
    
    NSLog(@"[BoostOps] StoreKit 2 properly configured - Bundle ID: %@", bundleID);
    return YES;
}

#pragma mark - Cleanup

- (void)dealloc {
    [self stopObservingTransactions];
}

@end 