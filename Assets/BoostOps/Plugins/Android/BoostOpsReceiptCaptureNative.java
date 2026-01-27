package com.boostops.sdk;

import android.util.Log;
import com.unity3d.player.UnityPlayer;
import org.json.JSONObject;
import org.json.JSONException;

/**
 * Android receipt capture for automatic purchase enrichment
 * 
 * Caches the most recent Google Play purchase data (purchase token, order ID)
 * so it can be auto-injected into TrackPurchase() calls without manual parameter passing.
 * 
 * This enables a clean 3-parameter API (amount, currency, productId)
 * while still capturing critical data for server-side receipt validation.
 * 
 * NOTE: This class provides a lightweight caching layer. It does NOT initialize
 * Google Play Billing Library (Unity IAP handles that). It only stores purchase
 * data that Unity IAP provides.
 */
public class BoostOpsReceiptCaptureNative {
    private static final String TAG = "BoostOps.ReceiptCapture";
    private static final String UNITY_GAME_OBJECT = "BoostOpsManager";
    private static final String UNITY_CALLBACK = "OnNativeReceiptCaptured";
    
    private static BoostOpsReceiptCaptureNative instance;
    private static boolean isInitialized = false;
    
    // Cached purchase data (most recent)
    private static String cachedProductId = null;
    private static String cachedOrderId = null;
    private static String cachedPurchaseToken = null;
    private static String cachedPurchaseData = null;  // Full JSON
    private static String cachedSignature = null;
    private static long cachedTimestamp = 0;
    
    /**
     * Get singleton instance
     */
    public static BoostOpsReceiptCaptureNative getInstance() {
        if (instance == null) {
            instance = new BoostOpsReceiptCaptureNative();
        }
        return instance;
    }
    
    /**
     * Initialize the receipt capture system
     * Called from Unity C# layer
     */
    public static void initialize() {
        if (isInitialized) {
            Log.d(TAG, "Already initialized");
            return;
        }
        
        try {
            isInitialized = true;
            Log.d(TAG, "✅ Initialized (ready to cache purchases)");
        } catch (Exception ex) {
            Log.e(TAG, "❌ Failed to initialize: " + ex.getMessage());
        }
    }
    
    /**
     * Cache a purchase from Unity IAP or native Google Play Billing
     * 
     * @param productId Product SKU (e.g., "com.game.coins_1000")
     * @param orderId Google Play order ID (e.g., "GPA.1234-5678-9012-34567")
     * @param purchaseToken Google Play purchase token (for validation)
     * @param purchaseData Full purchase JSON data from Google Play
     * @param signature Purchase signature from Google Play (for validation)
     */
    public static void cachePurchase(String productId, String orderId, String purchaseToken,
                                     String purchaseData, String signature) {
        try {
            cachedProductId = productId;
            cachedOrderId = orderId;
            cachedPurchaseToken = purchaseToken;
            cachedPurchaseData = purchaseData;
            cachedSignature = signature;
            cachedTimestamp = System.currentTimeMillis();
            
            Log.d(TAG, String.format("📦 Cached purchase: productId=%s, orderId=%s..., hasToken=%b",
                    productId,
                    orderId != null ? orderId.substring(0, Math.min(12, orderId.length())) : "null",
                    purchaseToken != null && !purchaseToken.isEmpty()));
            
            // Optional: Send callback to Unity
            sendCacheCallbackToUnity(productId, orderId, purchaseToken, purchaseData, signature);
            
        } catch (Exception ex) {
            Log.e(TAG, "❌ Failed to cache purchase: " + ex.getMessage());
        }
    }
    
    /**
     * Get cached product ID
     */
    public static String getCachedProductId() {
        if (isCacheExpired()) {
            return null;
        }
        return cachedProductId;
    }
    
    /**
     * Get cached order ID
     */
    public static String getCachedOrderId() {
        if (isCacheExpired()) {
            return null;
        }
        return cachedOrderId;
    }
    
    /**
     * Get cached purchase token
     */
    public static String getCachedPurchaseToken() {
        if (isCacheExpired()) {
            return null;
        }
        return cachedPurchaseToken;
    }
    
    /**
     * Get cached purchase data (full JSON)
     */
    public static String getCachedPurchaseData() {
        if (isCacheExpired()) {
            return null;
        }
        return cachedPurchaseData;
    }
    
    /**
     * Get cached signature
     */
    public static String getCachedSignature() {
        if (isCacheExpired()) {
            return null;
        }
        return cachedSignature;
    }
    
    /**
     * Check if cache is expired (5 seconds)
     */
    private static boolean isCacheExpired() {
        if (cachedTimestamp == 0) {
            return true;
        }
        long age = System.currentTimeMillis() - cachedTimestamp;
        return age > 5000;  // 5 seconds
    }
    
    /**
     * Clear cached data
     */
    public static void clearCache() {
        cachedProductId = null;
        cachedOrderId = null;
        cachedPurchaseToken = null;
        cachedPurchaseData = null;
        cachedSignature = null;
        cachedTimestamp = 0;
        Log.d(TAG, "🗑️ Cleared cache");
    }
    
    /**
     * Get cache statistics (for debugging)
     */
    public static String getCacheStats() {
        if (cachedTimestamp == 0) {
            return "Cache: empty";
        }
        long age = System.currentTimeMillis() - cachedTimestamp;
        return String.format("Cache: %s (age: %dms)", cachedProductId, age);
    }
    
    /**
     * Send cache callback to Unity (optional notification)
     */
    private static void sendCacheCallbackToUnity(String productId, String orderId,
                                                  String purchaseToken, String purchaseData,
                                                  String signature) {
        try {
            // Build JSON payload for Unity
            JSONObject payload = new JSONObject();
            payload.put("productId", productId != null ? productId : "");
            payload.put("orderId", orderId != null ? orderId : "");
            payload.put("purchaseToken", purchaseToken != null ? purchaseToken : "");
            payload.put("signature", signature != null ? signature : "");
            payload.put("timestamp", System.currentTimeMillis());
            
            // Send to Unity (if GameObject exists)
            UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, UNITY_CALLBACK, payload.toString());
            
        } catch (JSONException ex) {
            Log.e(TAG, "Failed to build callback JSON: " + ex.getMessage());
        } catch (Exception ex) {
            // GameObject might not exist - that's OK, cache still works
            Log.d(TAG, "Unity callback skipped (GameObject not found)");
        }
    }
    
    /**
     * Parse product ID from purchase data JSON
     * Helper method for Unity IAP integration
     */
    public static String extractProductIdFromPurchaseData(String purchaseDataJson) {
        try {
            JSONObject json = new JSONObject(purchaseDataJson);
            return json.optString("productId", null);
        } catch (Exception ex) {
            Log.e(TAG, "Failed to parse productId from purchase data: " + ex.getMessage());
            return null;
        }
    }
    
    /**
     * Parse order ID from purchase data JSON
     * Helper method for Unity IAP integration
     */
    public static String extractOrderIdFromPurchaseData(String purchaseDataJson) {
        try {
            JSONObject json = new JSONObject(purchaseDataJson);
            return json.optString("orderId", null);
        } catch (Exception ex) {
            Log.e(TAG, "Failed to parse orderId from purchase data: " + ex.getMessage());
            return null;
        }
    }
}

