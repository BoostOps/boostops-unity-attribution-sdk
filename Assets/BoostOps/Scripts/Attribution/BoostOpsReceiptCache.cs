using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Receipt cache for automatic purchase enrichment
    /// 
    /// Caches purchase transaction details from native platforms (iOS StoreKit, Android Google Play)
    /// so they can be auto-injected into TrackPurchase() calls without requiring manual parameter passing.
    /// 
    /// This enables a clean 3-parameter API (amount, currency, productId) while still capturing
    /// critical data like receipts and transaction IDs.
    /// 
    /// Design Philosophy:
    /// - Receipts don't expire (matches iOS App Store / Android Google Play behavior)
    /// - iOS receipts are valid for app lifetime (until uninstall)
    /// - Android purchase tokens are valid for 3 years (Google policy)
    /// - Cache persists until replaced by new purchase or app restart
    /// - LRU eviction prevents unbounded memory growth (max 50 items)
    /// 
    /// Industry alignment: AppsFlyer, Branch, Tenjin, Singular all keep receipts
    /// available indefinitely or until explicitly cleared.
    /// </summary>
    internal static class BoostOpsReceiptCache
    {
        /// <summary>
        /// Cached purchase data from native platforms
        /// </summary>
        internal class CachedPurchase
        {
            public string ProductId { get; set; }
            public string TransactionId { get; set; }
            public string Receipt { get; set; }
            public string ProductName { get; set; }
            public string ProductType { get; set; }  // "consumable", "non_consumable", "subscription"
            public DateTime CacheTime { get; set; }  // For debugging/stats only, NOT for expiration
            
            // Subscription-specific fields (iOS)
            public string SubscriptionGroupId { get; set; }
            public string OriginalTransactionId { get; set; }
            public bool IsIntroductoryPricePeriod { get; set; }
            public bool IsTrialPeriod { get; set; }
            
            // Receipt validation status
            public bool ReceiptValidated { get; set; }
            public string ValidationError { get; set; }
        }
        
        // Cache of recent purchases (keyed by productId for quick lookup)
        private static Dictionary<string, CachedPurchase> _purchaseCache = new Dictionary<string, CachedPurchase>();
        
        // Most recent purchase (fallback if productId doesn't match)
        private static CachedPurchase _lastPurchase = null;
        
        // Maximum cache size (LRU eviction after this limit)
        // Typical apps have 5-20 products, so 50 provides comfortable headroom
        private const int MAX_CACHE_SIZE = 50;
        
        // Enable debug logging
        private static bool _debugEnabled = true;
        
        /// <summary>
        /// Cache a purchase from native platform (iOS StoreKit, Android Google Play)
        /// Called automatically by native plugins when a transaction completes
        /// 
        /// Implements LRU (Least Recently Used) eviction:
        /// - If cache is full (50 items), removes oldest entry
        /// - New purchases replace old ones for same productId
        /// - Receipts persist indefinitely (until replaced or app restart)
        /// </summary>
        internal static void CachePurchase(
            string productId,
            string transactionId,
            string receipt,
            string productName = null,
            string productType = null,
            string subscriptionGroupId = null,
            string originalTransactionId = null,
            bool isIntroductoryPricePeriod = false,
            bool isTrialPeriod = false)
        {
            try
            {
                // LRU eviction: Remove oldest entry if cache is full
                if (!_purchaseCache.ContainsKey(productId) && _purchaseCache.Count >= MAX_CACHE_SIZE)
                {
                    // Find oldest cached entry
                    string oldestKey = null;
                    DateTime oldestTime = DateTime.MaxValue;
                    
                    foreach (var kvp in _purchaseCache)
                    {
                        if (kvp.Value.CacheTime < oldestTime)
                        {
                            oldestTime = kvp.Value.CacheTime;
                            oldestKey = kvp.Key;
                        }
                    }
                    
                    if (oldestKey != null)
                    {
                        _purchaseCache.Remove(oldestKey);
                        if (_debugEnabled)
                        {
                            LogDebug($"🧹 LRU eviction: Removed {oldestKey} (cache full at {MAX_CACHE_SIZE} items)");
                        }
                    }
                }
                
                var cached = new CachedPurchase
                {
                    ProductId = productId,
                    TransactionId = transactionId,
                    Receipt = receipt,
                    ProductName = productName,
                    ProductType = productType,
                    CacheTime = DateTime.UtcNow,
                    SubscriptionGroupId = subscriptionGroupId,
                    OriginalTransactionId = originalTransactionId,
                    IsIntroductoryPricePeriod = isIntroductoryPricePeriod,
                    IsTrialPeriod = isTrialPeriod,
                    ReceiptValidated = false
                };
                
                // Store by productId (replaces old entry if exists)
                _purchaseCache[productId] = cached;
                
                // Also store as last purchase (fallback)
                _lastPurchase = cached;
                
                if (_debugEnabled)
                {
                    LogDebug($"📦 Cached purchase: productId={productId}, txnId={transactionId?.Substring(0, Math.Min(8, transactionId?.Length ?? 0))}..., " +
                            $"hasReceipt={!string.IsNullOrEmpty(receipt)}, type={productType} (cache: {_purchaseCache.Count}/{MAX_CACHE_SIZE})");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to cache purchase: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Try to retrieve cached purchase data for a specific productId
        /// Returns null if productId not found
        /// 
        /// NOTE: Receipts don't expire! They persist until:
        /// - New purchase with same productId replaces them
        /// - App restart clears cache
        /// - LRU eviction (cache > 50 items)
        /// </summary>
        internal static CachedPurchase TryGetCachedPurchase(string productId)
        {
            try
            {
                // Try exact productId match first
                if (!string.IsNullOrEmpty(productId) && _purchaseCache.TryGetValue(productId, out var cached))
                {
                    if (_debugEnabled)
                    {
                        var age = (DateTime.UtcNow - cached.CacheTime).TotalSeconds;
                        LogDebug($"✅ Retrieved cached purchase for {productId} (age: {age:F1}s)");
                    }
                    return cached;
                }
                
                // Fallback: Use last purchase if no exact match
                if (_lastPurchase != null)
                {
                    if (_debugEnabled)
                    {
                        var age = (DateTime.UtcNow - _lastPurchase.CacheTime).TotalSeconds;
                        LogDebug($"⚠️ No exact match for {productId}, using last purchase: {_lastPurchase.ProductId} (age: {age:F1}s)");
                    }
                    return _lastPurchase;
                }
                
                if (_debugEnabled)
                {
                    LogDebug($"❌ No cached purchase found for {productId} (cache is empty)");
                }
                return null;
            }
            catch (Exception ex)
            {
                LogError($"Failed to retrieve cached purchase: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get cache statistics (no cleanup needed - receipts don't expire)
        /// 
        /// NOTE: This method is now a no-op. Receipts persist until:
        /// - New purchase replaces them (same productId)
        /// - App restart clears cache
        /// - LRU eviction when cache exceeds 50 items
        /// </summary>
        [System.Obsolete("Receipts no longer expire. Cache is managed via LRU eviction. This method is now a no-op.")]
        internal static void CleanupExpiredEntries()
        {
            // No-op: Receipts don't expire
            // LRU eviction happens automatically in CachePurchase()
            if (_debugEnabled)
            {
                LogDebug($"ℹ️ Cache status: {_purchaseCache.Count} items (max: {MAX_CACHE_SIZE})");
            }
        }
        
        /// <summary>
        /// Clear all cached purchases (for testing or privacy)
        /// </summary>
        internal static void ClearAll()
        {
            _purchaseCache.Clear();
            _lastPurchase = null;
            if (_debugEnabled)
            {
                LogDebug("🗑️ Cleared all cached purchases");
            }
        }
        
        /// <summary>
        /// Get cache statistics (for debugging)
        /// </summary>
        internal static string GetCacheStats()
        {
            return $"Cache: {_purchaseCache.Count} entries, last: {(_lastPurchase != null ? _lastPurchase.ProductId : "none")}";
        }
        
        private static void LogDebug(string message)
        {
            Debug.Log($"[BoostOps.ReceiptCache] {message}");
        }
        
        private static void LogError(string message)
        {
            Debug.LogError($"[BoostOps.ReceiptCache] {message}");
        }
    }
}

