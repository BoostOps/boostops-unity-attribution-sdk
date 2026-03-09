using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Native bridge for automatic receipt capture on iOS and Android
    /// 
    /// Connects to platform-specific receipt capture plugins that monitor
    /// StoreKit (iOS) and Google Play Billing (Android) for purchase transactions.
    /// 
    /// Captured data is automatically cached in BoostOpsReceiptCache for
    /// enrichment of TrackPurchase() calls.
    /// </summary>
    internal static class BoostOpsReceiptCaptureNative
    {
        #if UNITY_IOS && !UNITY_EDITOR
        // iOS native plugin (StoreKit observer)
        [DllImport("__Internal")]
        private static extern void _BoostOpsReceiptCapture_Initialize();
        
        [DllImport("__Internal")]
        private static extern void _BoostOpsReceiptCapture_Shutdown();
        
        [DllImport("__Internal")]
        private static extern bool _BoostOpsReceiptCapture_IsInitialized();
        
        [DllImport("__Internal")]
        private static extern string _BoostOpsReceiptCapture_GetAppReceipt();
        
        // NOTE: Receipt capture callbacks are now handled via UnitySendMessage
        // See BoostOpsManager.OnReceiptCaptured() method
        
        #elif UNITY_ANDROID && !UNITY_EDITOR
        // Android native plugin (Google Play Billing observer)
        private static AndroidJavaClass _nativeClass;
        
        private static AndroidJavaClass GetNativeClass()
        {
            if (_nativeClass == null)
            {
                _nativeClass = new AndroidJavaClass("com.boostops.sdk.BoostOpsReceiptCaptureNative");
            }
            return _nativeClass;
        }
        
        #endif
        
        private static bool _isInitialized = false;
        
        /// <summary>
        /// Initialize the native receipt capture system
        /// Should be called with a 2-second delay on iOS to prevent StoreKit deadlocks
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            try
            {
                #if UNITY_IOS && !UNITY_EDITOR
                _BoostOpsReceiptCapture_Initialize();
                _isInitialized = true;
                Debug.Log("[BoostOps.ReceiptCaptureNative] ✅ iOS StoreKit observer initialized");
                
                #elif UNITY_ANDROID && !UNITY_EDITOR
                GetNativeClass().CallStatic("initialize");
                _isInitialized = true;
                Debug.Log("[BoostOps.ReceiptCaptureNative] ✅ Android Google Play observer initialized");
                
                #else
                _isInitialized = true;
                Debug.Log("[BoostOps.ReceiptCaptureNative] ℹ️ Receipt capture not available in Editor");
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps.ReceiptCaptureNative] ❌ Failed to initialize: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Shutdown the native receipt capture system
        /// </summary>
        public static void Shutdown()
        {
            if (!_isInitialized) return;
            
            try
            {
                #if UNITY_IOS && !UNITY_EDITOR
                _BoostOpsReceiptCapture_Shutdown();
                _isInitialized = false;
                Debug.Log("[BoostOps.ReceiptCaptureNative] 🛑 iOS StoreKit observer shutdown");
                
                #elif UNITY_ANDROID && !UNITY_EDITOR
                GetNativeClass().CallStatic("clearCache");
                _isInitialized = false;
                Debug.Log("[BoostOps.ReceiptCaptureNative] 🛑 Android cache cleared");
                
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps.ReceiptCaptureNative] Failed to shutdown: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if native receipt capture is initialized
        /// </summary>
        public static bool IsInitialized()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            return _BoostOpsReceiptCapture_IsInitialized();
            #elif UNITY_ANDROID && !UNITY_EDITOR
            return _isInitialized;
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// Get the app receipt (iOS only) for server-side validation
        /// </summary>
        public static string GetAppReceipt()
        {
            try
            {
                #if UNITY_IOS && !UNITY_EDITOR
                return _BoostOpsReceiptCapture_GetAppReceipt();
                #else
                return null;
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps.ReceiptCaptureNative] Failed to get app receipt: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Manually cache a purchase (for Unity IAP integration)
        /// Android only - iOS uses automatic StoreKit observer
        /// </summary>
        public static void CachePurchaseManually(string productId, string orderId, string purchaseToken,
                                                  string purchaseData, string signature)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                GetNativeClass().CallStatic("cachePurchase", productId, orderId, purchaseToken,
                                            purchaseData, signature);
                
                // Also cache in C# layer for immediate access
                BoostOpsReceiptCache.CachePurchase(
                    productId: productId,
                    transactionId: orderId,
                    receipt: purchaseToken,  // Android uses purchase token as "receipt"
                    productName: null,
                    productType: null
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps.ReceiptCaptureNative] Failed to cache purchase manually: {ex.Message}");
            }
            #endif
        }
        
        /// <summary>
        /// Get cache statistics (for debugging)
        /// </summary>
        public static string GetCacheStats()
        {
            try
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                return GetNativeClass().CallStatic<string>("getCacheStats");
                #else
                return BoostOpsReceiptCache.GetCacheStats();
                #endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps.ReceiptCaptureNative] Failed to get cache stats: {ex.Message}");
                return "Error";
            }
        }
    }
}

