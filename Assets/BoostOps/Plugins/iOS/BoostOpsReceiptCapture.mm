#import <Foundation/Foundation.h>
#import <StoreKit/StoreKit.h>

// Unity callback bridge
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

#pragma mark - Receipt Capture Delegate

/// <summary>
/// StoreKit transaction observer that captures receipt data
/// for automatic enrichment of TrackPurchase() calls
/// 
/// This observer runs in the background and caches the most recent
/// transaction details (receipt, transaction ID, product metadata)
/// so the Unity layer can auto-inject them without manual parameter passing.
/// </summary>
@interface BoostOpsReceiptCaptureObserver : NSObject <SKPaymentTransactionObserver>
@property (nonatomic, assign) BOOL isInitialized;
@end

@implementation BoostOpsReceiptCaptureObserver

- (instancetype)init {
    self = [super init];
    if (self) {
        _isInitialized = NO;
    }
    return self;
}

/// <summary>
/// Called when transactions are updated in the payment queue
/// We capture the receipt data but DON'T call finishTransaction
/// (Unity IAP or developer's code handles that)
/// </summary>
- (void)paymentQueue:(SKPaymentQueue *)queue updatedTransactions:(NSArray<SKPaymentTransaction *> *)transactions {
    for (SKPaymentTransaction *transaction in transactions) {
        switch (transaction.transactionState) {
            case SKPaymentTransactionStatePurchased:
            case SKPaymentTransactionStateRestored:
                [self captureReceiptForTransaction:transaction];
                break;
                
            case SKPaymentTransactionStateFailed:
            case SKPaymentTransactionStateDeferred:
            case SKPaymentTransactionStatePurchasing:
                // Don't capture for these states
                break;
        }
    }
}

/// <summary>
/// Extract and cache receipt data from a successful transaction
/// 
/// NOTE: Objective-C exceptions disabled in Unity - using nil checks instead of @try/@catch
/// </summary>
- (void)captureReceiptForTransaction:(SKPaymentTransaction *)transaction {
    if (!transaction) {
        NSLog(@"[BoostOps.ReceiptCapture] ❌ Nil transaction");
        return;
    }
    
    NSString *productId = transaction.payment.productIdentifier;
    NSString *transactionId = transaction.transactionIdentifier;
    
    if (!productId || !transactionId) {
        NSLog(@"[BoostOps.ReceiptCapture] ⚠️ Missing productId or transactionId");
        return;
    }
    
    // Get app receipt (iOS 7+)
    NSString *receiptString = nil;
    NSURL *receiptURL = [[NSBundle mainBundle] appStoreReceiptURL];
    if (receiptURL) {
        NSData *receiptData = [NSData dataWithContentsOfURL:receiptURL];
        if (receiptData) {
            receiptString = [receiptData base64EncodedStringWithOptions:0];
        }
    }
    
    // Extract product metadata (requires SKProduct, which we might not have)
    // We'll get this from SKPayment instead
    NSString *productName = nil;
    NSString *productType = @"unknown";  // Will be enriched by Unity layer if needed
    
    // Extract subscription metadata (iOS 11.2+)
    NSString *subscriptionGroupId = nil;
    NSString *originalTransactionId = nil;
    BOOL isIntroductoryPricePeriod = NO;
    BOOL isTrialPeriod = NO;
    
    #if __IPHONE_OS_VERSION_MAX_ALLOWED >= 110200
    if (@available(iOS 11.2, *)) {
        if (transaction.payment.paymentDiscount) {
            // This is a discounted subscription (intro offer or promo)
            isIntroductoryPricePeriod = YES;
        }
    }
    #endif
    
    // Get original transaction (for subscriptions/restorations)
    if (transaction.originalTransaction) {
        originalTransactionId = transaction.originalTransaction.transactionIdentifier;
    }
    
    // Build JSON payload for Unity
    NSMutableDictionary *payload = [NSMutableDictionary dictionary];
    payload[@"productId"] = productId ? productId : @"";
    payload[@"transactionId"] = transactionId ? transactionId : @"";
    payload[@"receipt"] = receiptString ? receiptString : @"";
    payload[@"productName"] = productName ? productName : @"";
    payload[@"productType"] = productType ? productType : @"";
    payload[@"subscriptionGroupId"] = subscriptionGroupId ? subscriptionGroupId : @"";
    payload[@"originalTransactionId"] = originalTransactionId ? originalTransactionId : @"";
    payload[@"isIntroductoryPricePeriod"] = @(isIntroductoryPricePeriod);
    payload[@"isTrialPeriod"] = @(isTrialPeriod);
    
    // Convert to JSON string
    NSError *error = nil;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    if (jsonData && !error) {
        NSString *jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        
        // Send to Unity (BoostOpsManager GameObject, OnReceiptCaptured method)
        UnitySendMessage("BoostOpsManager", "OnReceiptCaptured", [jsonString UTF8String]);
        
        NSLog(@"[BoostOps.ReceiptCapture] ✅ Cached receipt for %@ (txn: %@...)", productId,
              [transactionId substringToIndex:MIN(8, transactionId.length)]);
    } else {
        NSLog(@"[BoostOps.ReceiptCapture] ❌ Failed to serialize JSON: %@", error.localizedDescription);
    }
}

@end

#pragma mark - C Interface (called from Unity C#)

static BoostOpsReceiptCaptureObserver *_observer = nil;
static BOOL _isInitialized = NO;

/// <summary>
/// Initialize the receipt capture observer
/// 
/// IMPORTANT: This is called with a 2-second delay on iOS to prevent
/// StoreKit deadlocks during app startup (industry best practice)
/// 
/// NOTE: Objective-C exceptions disabled in Unity - using nil checks instead of @try/@catch
/// </summary>
extern "C" void _BoostOpsReceiptCapture_Initialize() {
    if (_isInitialized) {
        NSLog(@"[BoostOps.ReceiptCapture] Already initialized");
        return;
    }
    
    _observer = [[BoostOpsReceiptCaptureObserver alloc] init];
    if (!_observer) {
        NSLog(@"[BoostOps.ReceiptCapture] ❌ Failed to create observer");
        return;
    }
    
    [[SKPaymentQueue defaultQueue] addTransactionObserver:_observer];
    _observer.isInitialized = YES;
    _isInitialized = YES;
    
    NSLog(@"[BoostOps.ReceiptCapture] ✅ Initialized (observer added to StoreKit queue)");
}

/// <summary>
/// Shutdown the receipt capture observer (cleanup)
/// 
/// NOTE: Objective-C exceptions disabled in Unity - using nil checks instead of @try/@catch
/// </summary>
extern "C" void _BoostOpsReceiptCapture_Shutdown() {
    if (!_isInitialized || !_observer) {
        return;
    }
    
    [[SKPaymentQueue defaultQueue] removeTransactionObserver:_observer];
    _observer.isInitialized = NO;
    _observer = nil;
    _isInitialized = NO;
    
    NSLog(@"[BoostOps.ReceiptCapture] 🛑 Shutdown complete");
}

/// <summary>
/// Check if observer is initialized
/// </summary>
extern "C" BOOL _BoostOpsReceiptCapture_IsInitialized() {
    return _isInitialized;
}

/// <summary>
/// Manually capture the current app receipt (without transaction)
/// Useful for server-side validation at app startup
/// 
/// NOTE: Objective-C exceptions disabled in Unity - using nil checks instead of @try/@catch
/// </summary>
extern "C" const char* _BoostOpsReceiptCapture_GetAppReceipt() {
    NSURL *receiptURL = [[NSBundle mainBundle] appStoreReceiptURL];
    if (!receiptURL) {
        return "";
    }
    
    NSData *receiptData = [NSData dataWithContentsOfURL:receiptURL];
    if (!receiptData) {
        return "";
    }
    
    NSString *receiptString = [receiptData base64EncodedStringWithOptions:0];
    if (!receiptString) {
        return "";
    }
    
    // Allocate persistent C string (Unity will copy it)
    const char *cString = [receiptString UTF8String];
    if (!cString) {
        return "";
    }
    
    char *result = (char*)malloc(strlen(cString) + 1);
    if (!result) {
        return "";
    }
    
    strcpy(result, cString);
    return result;
}
