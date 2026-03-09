using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BoostOps
{
    /// <summary>
    /// Automatic revenue tracking for BoostOps SDK
    /// This system automatically tracks ALL purchases via native platform integration
    /// with attribution data for comprehensive BoostOps analytics.
    /// </summary>
    public static class BoostOpsRevenueTracker
    {
        #region Configuration & State
        
        /// <summary>
        /// Whether automatic revenue tracking is enabled
        /// Controls automatic detection and tracking of Unity IAP purchases
        /// </summary>
        public static bool AutoRevenueTrackingEnabled { get; private set; } = true;
        
        /// <summary>
        /// Whether to validate receipts with Apple servers
        /// Note: BoostOps is attribution-focused - receipt validation is optional
        /// Enable only if you need server-side purchase verification for content access control
        /// </summary>
        public static bool ReceiptValidationEnabled { get; private set; } = false;
        
        /// <summary>
        /// Whether to track revenue events with attribution data
        /// </summary>
        public static bool AttributionTrackingEnabled { get; private set; } = true;
        
        /// <summary>
        /// Minimum purchase amount to track (in USD cents)
        /// </summary>
        public static int MinTrackingAmountCents { get; private set; } = 1; // $0.01
        
        /// <summary>
        /// Maximum time to wait for attribution data (in seconds)
        /// </summary>
        public static float AttributionTimeoutSeconds { get; private set; } = 5f;
        
        private static bool isInitialized = false;
        private static readonly Dictionary<string, PurchaseData> pendingPurchases = new Dictionary<string, PurchaseData>();
        private static readonly HashSet<string> processedTransactionIds = new HashSet<string>();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Fired when a purchase is automatically tracked
        /// </summary>
        public static event Action<AutoRevenueEvent> OnRevenueTracked;
        
        /// <summary>
        /// Fired when revenue tracking fails
        /// </summary>
        public static event Action<string, Exception> OnRevenueTrackingError;
        
        /// <summary>
        /// Fired when attribution data is attached to a purchase
        /// </summary>
        public static event Action<string, AttributionData> OnAttributionAttached;
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the revenue tracker
        /// PRIMARY: Native StoreKit/Google Play tracking (works with ANY IAP system)
        /// OPTIONAL: Unity IAP integration available for enhanced features
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;
            
            try
            {
                LogDebug("🚀 BoostOps Revenue Tracker - EXPLICIT TRACKING ONLY (industry standard)");
                LogDebug("   ℹ️  Automatic native purchase monitoring has been REMOVED");
                LogDebug("   ✅ Use explicit calls: BoostOpsSDK.TrackPurchase() in your ProcessPurchase() method");
                
                // Set up attribution tracking
                if (AttributionTrackingEnabled)
                {
                    SetupAttributionTracking();
                }
                
                // Load configuration
                LoadConfiguration();
                
                isInitialized = true;
                LogDebug("✅ BoostOps Revenue Tracker initialized (explicit tracking only)");
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize revenue tracker: {ex.Message}");
                OnRevenueTrackingError?.Invoke("initialization", ex);
            }
        }
        
        #endregion
        
        #region Configuration
        
        /// <summary>
        /// Enable or disable automatic revenue tracking
        /// Controls whether Unity IAP purchases are automatically detected and tracked
        /// </summary>
        public static void SetAutoRevenueTrackingEnabled(bool enabled)
        {
            AutoRevenueTrackingEnabled = enabled;
            PlayerPrefs.SetInt("BoostOps_AutoRevenueTracking", enabled ? 1 : 0);
            LogDebug($"Auto revenue tracking {(enabled ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// Enable or disable receipt validation
        /// </summary>
        public static void SetReceiptValidationEnabled(bool enabled)
        {
            ReceiptValidationEnabled = enabled;
            PlayerPrefs.SetInt("BoostOps_ReceiptValidation", enabled ? 1 : 0);
            LogDebug($"Receipt validation {(enabled ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// Enable or disable attribution tracking
        /// </summary>
        public static void SetAttributionTrackingEnabled(bool enabled)
        {
            AttributionTrackingEnabled = enabled;
            PlayerPrefs.SetInt("BoostOps_AttributionTracking", enabled ? 1 : 0);
            LogDebug($"Attribution tracking {(enabled ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// Set minimum purchase amount to track
        /// </summary>
        public static void SetMinTrackingAmount(decimal usdAmount)
        {
            MinTrackingAmountCents = (int)(usdAmount * 100);
            PlayerPrefs.SetInt("BoostOps_MinTrackingAmountCents", MinTrackingAmountCents);
            LogDebug($"Min tracking amount set to ${usdAmount:F2}");
        }
        
        private static void LoadConfiguration()
        {
            AutoRevenueTrackingEnabled = PlayerPrefs.GetInt("BoostOps_AutoRevenueTracking", 1) == 1;
            ReceiptValidationEnabled = PlayerPrefs.GetInt("BoostOps_ReceiptValidation", 0) == 1; // Changed default to 0
            AttributionTrackingEnabled = PlayerPrefs.GetInt("BoostOps_AttributionTracking", 1) == 1;
            MinTrackingAmountCents = PlayerPrefs.GetInt("BoostOps_MinTrackingAmountCents", 1);
        }
        
        #endregion
        
        
        #region Manual Purchase Tracking
        
        /// <summary>
        /// Manually track a purchase (for non-Unity IAP purchases)
        /// Provides comprehensive revenue tracking for all payment methods
        /// </summary>
        public static void TrackPurchase(string transactionId, string productId, decimal amount, string currency, Dictionary<string, object> properties = null)
        {
            if (!AutoRevenueTrackingEnabled)
            {
                LogDebug("Auto revenue tracking disabled, ignoring manual purchase");
                return;
            }
            
            try
            {
                var purchaseData = new PurchaseData
                {
                    TransactionId = transactionId,
                    ProductId = productId,
                    ProductType = "manual",
                    LocalizedPrice = (double)amount,  // Cast decimal to double
                    IsoCurrencyCode = currency,
                    Receipt = null,
                    PurchaseTime = DateTime.UtcNow.ToString("o"),  // ISO 8601 format
                    Timestamp = DateTime.UtcNow.ToString("o"),  // ISO 8601 format
                    Platform = GetPlatformString(),
                    StoreSpecificId = productId,
                    DeveloperPayload = null
                };

                // Note: Custom properties removed due to Unity serialization limitations
                // Dictionary<string, object> is not Unity-serializable

                TrackPurchaseImmediate(purchaseData);
                LogDebug($"Manual purchase tracked: {productId} - {amount} {currency}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to track manual purchase: {ex.Message}");
                OnRevenueTrackingError?.Invoke("manual_purchase", ex);
            }
        }
        
        /// <summary>
        /// Track a purchase with receipt data for enhanced validation
        /// </summary>
        public static void TrackPurchaseWithReceipt(string transactionId, string productId, decimal amount, string currency, string receipt, Dictionary<string, object> properties = null)
        {
            var purchaseData = new PurchaseData
            {
                TransactionId = transactionId,
                ProductId = productId,
                ProductType = "manual_with_receipt",
                LocalizedPrice = (double)amount,  // Cast decimal to double
                IsoCurrencyCode = currency,
                Receipt = receipt,
                PurchaseTime = DateTime.UtcNow.ToString("o"),  // ISO 8601 format
                Timestamp = DateTime.UtcNow.ToString("o"),  // ISO 8601 format
                Platform = GetPlatformString(),
                StoreSpecificId = productId,
                DeveloperPayload = null
            };

            // Note: Custom properties removed due to Unity serialization limitations
            // Dictionary<string, object> is not Unity-serializable

            TrackPurchaseImmediate(purchaseData);
        }
        
        #endregion
        
        #region Internal Purchase Processing
        
        /// <summary>
        /// Core method called by native iOS/Android when purchases occur.
        /// This is the main entry point for all purchase tracking on mobile
        /// </summary>
        /// <param name="nativePurchaseData">Purchase data from native StoreKit/Google Play</param>
        internal static void ProcessNativePurchase(PurchaseData nativePurchaseData)
        {
            if (nativePurchaseData == null)
            {
                LogError("Received null native purchase data");
                return;
            }
            
            LogDebug($"[DEBUG] ProcessNativePurchase called: {nativePurchaseData.ProductId} (TxnID: {nativePurchaseData.TransactionId})");
            
            try
            {
                // Check for duplicate processing
                if (!string.IsNullOrEmpty(nativePurchaseData.TransactionId) && 
                    processedTransactionIds.Contains(nativePurchaseData.TransactionId))
                {
                    LogDebug($"[DEBUG] DUPLICATE - Native purchase already processed: {nativePurchaseData.TransactionId}");
                    return;
                }
                
                LogDebug($"[DEBUG] NEW - Processing native purchase for first time: {nativePurchaseData.TransactionId}");
                
                // Validate purchase amount
                if (nativePurchaseData.Amount < (MinTrackingAmountCents / 100.0))
                {
                    LogDebug($"Native purchase amount {nativePurchaseData.Amount} below minimum threshold");
                    return;
                }
                
                // Mark as processed
                if (!string.IsNullOrEmpty(nativePurchaseData.TransactionId))
                {
                    processedTransactionIds.Add(nativePurchaseData.TransactionId);
                }
                
                // Track the purchase with attribution
                TrackPurchaseWithAttribution(nativePurchaseData);
                
                LogDebug($"Processed native purchase: {nativePurchaseData.ProductId} - {nativePurchaseData.Amount} {nativePurchaseData.Currency}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to process native purchase: {ex.Message}");
                OnRevenueTrackingError?.Invoke(nativePurchaseData.TransactionId ?? "unknown", ex);
            }
        }
        
        #endregion
        
        #region Attribution Tracking
        
        private static void SetupAttributionTracking()
        {
            // Hook into BoostOps attribution system
            if (BoostOpsInstallAttribution.Instance != null)
            {
                BoostOpsInstallAttribution.OnInstallAttributed += OnInstallAttributed;
                BoostOpsInstallAttribution.OnConversionTracked += OnConversionTracked;
            }
        }
        
        private static void OnInstallAttributed(InstallAttributionData attribution)
        {
            LogDebug($"Install attributed: {attribution.CampaignId} from {attribution.SourceAppId}");
        }
        
        private static void OnConversionTracked(ConversionData conversion)
        {
            LogDebug($"Conversion tracked: {conversion.ConversionType} - {conversion.Value}");
        }
        
        private static void TrackPurchaseWithAttribution(PurchaseData purchaseData)
        {
            // Store purchase data temporarily
            pendingPurchases[purchaseData.TransactionId] = purchaseData;
            
            // Try to get attribution data
            GetAttributionDataAsync(purchaseData.TransactionId, (transactionId, attributionData) =>
            {
                if (pendingPurchases.TryGetValue(transactionId, out var storedPurchase))
                {
                    storedPurchase.AttributionData = attributionData;
                    TrackPurchaseImmediate(storedPurchase);
                    pendingPurchases.Remove(transactionId);
                    
                    if (attributionData != null)
                    {
                        OnAttributionAttached?.Invoke(transactionId, attributionData);
                    }
                }
            });
        }
        
        private static void GetAttributionDataAsync(string transactionId, Action<string, AttributionData> callback)
        {
            // Start coroutine to get attribution data with timeout
            // Create a temporary GameObject to run the coroutine (no SDK dependency needed)
            var tempObject = new GameObject("BoostOpsRevenueTracker_Temp");
            var tempComponent = tempObject.AddComponent<CoroutineRunner>();
            tempComponent.StartCoroutine(GetAttributionDataCoroutine(transactionId, callback, tempObject));
        }
        
        private static System.Collections.IEnumerator GetAttributionDataCoroutine(string transactionId, Action<string, AttributionData> callback, GameObject tempObject = null)
        {
            var startTime = Time.time;
            AttributionData attributionData = null;
            
            // Wait for attribution data or timeout
            while (Time.time - startTime < AttributionTimeoutSeconds)
            {
                // Try to get attribution data
                attributionData = GetCurrentAttributionData();
                if (attributionData != null) break;
                
                yield return new WaitForSeconds(0.1f);
            }
            
            callback(transactionId, attributionData);
            
            // Clean up temporary GameObject if created
            if (tempObject != null)
            {
                UnityEngine.Object.Destroy(tempObject);
            }
        }
        
        private static AttributionData GetCurrentAttributionData()
        {
            // Try to get attribution from BoostOps Install Attribution
            if (BoostOpsInstallAttribution.Instance != null && BoostOpsInstallAttribution.Instance.IsAttributedInstall)
            {
                var installAttribution = BoostOpsInstallAttribution.Instance.CurrentAttribution;
                if (installAttribution != null)
                {
                    return new AttributionData
                    {
                        CampaignId = installAttribution.CampaignId,
                        SourceAppId = installAttribution.SourceAppId,
                        AttributionSource = installAttribution.AttributionSource,
                        InstallTimestamp = installAttribution.InstallTimestamp.ToString("o")  // Convert DateTime to ISO 8601 string
                    };
                }
            }
            
            // Try to get attribution from user properties
            var sourceApp = GetUserProperty("cross_promo_source_app");
            var campaignId = GetUserProperty("cross_promo_install_campaign");
            
            if (!string.IsNullOrEmpty(sourceApp) || !string.IsNullOrEmpty(campaignId))
            {
                return new AttributionData
                {
                    CampaignId = campaignId,
                    SourceAppId = sourceApp,
                    AttributionSource = "cross_promo"
                };
            }
            
            return null;
        }
        
        private static string GetUserProperty(string key)
        {
            return PlayerPrefs.GetString($"BoostOps_UserProperty_{key}", null);
        }
        
        #endregion
        
        #region Purchase Tracking
        
        private static void TrackPurchaseImmediate(PurchaseData purchaseData)
        {
            try
            {
                // Create revenue event
                var revenueEvent = CreateRevenueEvent(purchaseData);
                
                // Send to BoostOps Analytics
                SendToBoostOpsAnalytics(revenueEvent);
                
                // Send to BoostOps Manager
                SendToBoostOpsManager(revenueEvent);
                
                // Fire event
                OnRevenueTracked?.Invoke(revenueEvent);
                
                LogDebug($"Revenue tracked: {purchaseData.ProductId} - {purchaseData.LocalizedPrice} {purchaseData.IsoCurrencyCode}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to track revenue: {ex.Message}");
                OnRevenueTrackingError?.Invoke("revenue_tracking", ex);
            }
        }
        
        private static AutoRevenueEvent CreateRevenueEvent(PurchaseData purchaseData)
        {
            var revenueEvent = new AutoRevenueEvent
            {
                TransactionId = purchaseData.TransactionId,
                ProductId = purchaseData.ProductId,
                LocalizedPrice = purchaseData.LocalizedPrice,  // Already double now
                IsoCurrencyCode = purchaseData.IsoCurrencyCode,
                LocalizedTitle = purchaseData.LocalizedTitle,
                LocalizedDescription = purchaseData.LocalizedDescription,
                Timestamp = purchaseData.Timestamp,  // Already string now
                USDValue = ConvertToUSD(purchaseData.LocalizedPrice, purchaseData.IsoCurrencyCode),
                Platform = GetCurrentPlatform(),
                AttributionData = purchaseData.AttributionData,
                HasReceipt = purchaseData.HasReceipt
            };
            
            return revenueEvent;
        }
        
        
        private static void SendToBoostOpsAnalytics(AutoRevenueEvent revenueEvent)
        {
            // Track purchase using explicit API
            BoostOpsAnalyticsContract.TrackPurchase(
                amount: (decimal)revenueEvent.LocalizedPrice,
                currency: revenueEvent.IsoCurrencyCode,
                productId: revenueEvent.ProductId,
                transactionId: revenueEvent.TransactionId
            );
        }
        
        private static void SendToBoostOpsManager(AutoRevenueEvent revenueEvent)
        {
            // Log purchase tracking (no external SDK dependency needed)
            Debug.Log($"[BoostOpsRevenueTracker] Purchase tracked: {revenueEvent.TransactionId} - {revenueEvent.LocalizedPrice} {revenueEvent.IsoCurrencyCode}");
        }
        
        #endregion
        
        #region Utility Methods
        
        private static int ConvertToUSDCents(double amount, string currency)
        {
            // Simple currency conversion (in real implementation, use exchange rates)
            var usdAmount = ConvertToUSD(amount, currency);
            return (int)(usdAmount * 100);
        }
        
        private static double ConvertToUSD(double amount, string currency)
        {
            // Simple currency conversion (in real implementation, use exchange rates)
            // For now, assume 1:1 conversion for simplicity
            return amount;
        }
        
        private static string GetCurrentPlatform()
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_WEBGL
            return "WebGL";
#else
            return "Unknown";
#endif
        }
        
        private static string GetPlatformString()
        {
            return GetCurrentPlatform();
        }
        
        private static ReceiptData ParseReceiptData(string receipt)
        {
            try
            {
                return JsonUtility.FromJson<ReceiptData>(receipt);
            }
            catch (Exception ex)
            {
                LogError($"Failed to parse receipt data: {ex.Message}");
                return null;
            }
        }
        
        private static void LogDebug(string message)
        {
            BoostOpsLogger.LogDebug("Revenue", message);
        }
        
        private static void LogError(string message)
        {
            BoostOpsLogger.LogError("Revenue", message);
        }
        
        private static void LogWarning(string message)
        {
            BoostOpsLogger.LogWarning("Revenue", message);
        }
        
        #endregion
    }
    
    #region Data Classes
    
    /// <summary>
    /// Data structure for pending purchase processing
    /// </summary>
    [Serializable]
    public class PurchaseData
    {
        public string TransactionId;
        public string OriginalTransactionId;  // NEW: For subscription renewals (iOS)
        public string ProductId;
        public string ProductType = "manual";
        public double LocalizedPrice;
        public string IsoCurrencyCode;
        public string LocalizedTitle;
        public string LocalizedDescription;
        public string Timestamp;  // ISO 8601 format
        public string PurchaseTime;
        public string Receipt;
        public bool HasReceipt;
        public ReceiptData ParsedReceipt;
        public AttributionData AttributionData;
        public PurchaseSource Source = PurchaseSource.NativeAutomatic;
        public string Platform;
        public string Store;
        public string StoreSpecificId;
        public string DeveloperPayload;
        public bool IsRestored;
        public double Amount;
        public string Currency;
        public int Quantity = 1;  // NEW: Quantity purchased
        public ReceiptData ReceiptData;
        public AttributionData Attribution;
        
        // NEW: Subscription metadata (iOS)
        public bool IsSubscription;
        public string SubscriptionPeriod;  // ISO 8601 duration format (P1M, P1Y, etc)
        public decimal? IntroductoryPrice;  // Intro offer price if applicable
        public int? IntroductoryPriceCycles;  // Number of intro price periods
        public bool IsTrial;  // Whether in free trial period
    }
    
    /// <summary>
    /// Attribution data for purchase tracking
    /// </summary>
    [Serializable]
    public class AttributionData
    {
        public string CampaignId;
        public string SourceAppId;
        public string AttributionSource;
        public string InstallTimestamp;  // Changed from DateTime? to string
    }
    
    /// <summary>
    /// Final revenue event data
    /// </summary>
    [Serializable]
    public class AutoRevenueEvent
    {
        public string TransactionId;
        public string ProductId;
        public double LocalizedPrice;  // Changed from decimal to double (Unity-serializable)
        public string IsoCurrencyCode;
        public string LocalizedTitle;
        public string LocalizedDescription;
        public string Timestamp;  // Changed from DateTime to string (Unity-serializable)
        public double USDValue;  // Changed from decimal to double (Unity-serializable)
        public string Platform;
        public AttributionData AttributionData;
        public bool HasReceipt;
        // Removed Dictionary - not Unity-serializable
        // Use List<KeyValuePair> or custom serializable class if needed
    }
    
    /// <summary>
    /// Receipt data structure
    /// </summary>
    [Serializable]
    public class ReceiptData
    {
        public string Store;
        public string TransactionID;
        public string Payload;
    }
    
    /// <summary>
    /// Google Play receipt data
    /// </summary>
    [Serializable]
    public class GooglePlayReceiptData
    {
        public string json;
        public string signature;
    }
    
    /// <summary>
    /// Source of the purchase tracking
    /// </summary>
    public enum PurchaseSource
    {
        /// <summary>
        /// Purchase tracked through Unity IAP integration
        /// </summary>
        UnityIAP,
        
        /// <summary>
        /// Purchase tracked manually via API call
        /// </summary>
        Manual,
        
        /// <summary>
        /// Purchase automatically detected by native iOS/Android code
        /// </summary>
        NativeAutomatic,
        
        /// <summary>
        /// Purchase tracked through server-side integration
        /// </summary>
        ServerSide
    }
    
    #endregion
    
    /// <summary>
    /// Helper class for running coroutines when BoostOpsManager is not available
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        // This class exists solely to provide MonoBehaviour functionality for coroutines
        // when BoostOpsManager is internal and not accessible
    }
    
} 