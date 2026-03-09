using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BoostOps.Attribution
{
    /// <summary>
    /// Manages iOS attribution token collection for Apple Search Ads attribution
    /// Supports iOS 14.3+ via AdServices framework (iAd framework was deprecated and removed)
    /// </summary>
    public static class BoostOpsAttributionManager
    {
        private static string _cachedAttributionToken;
        private static DateTime _tokenCacheTime;
        private static bool _isCollecting = false;
        
        // Token is valid for 24 hours according to Apple
        private static readonly TimeSpan TOKEN_CACHE_DURATION = TimeSpan.FromHours(23); // Cache for 23 hours to be safe
        
#if UNITY_IOS && !UNITY_EDITOR
        
        // iOS 14.3+ synchronous API (AdServices framework)
        [DllImport("__Internal")]
        private static extern string _BoostOps_GetAttributionToken();
        
        // Legacy API - no longer supported (iAd framework was deprecated and removed by Apple)
        [DllImport("__Internal")]  
        private static extern void _BoostOps_RequestAttributionAsync(string gameObjectName, string callbackMethod);
        
        // Check if AdServices framework is available
        [DllImport("__Internal")]
        private static extern bool _BoostOps_IsAdServicesAvailable();
        
#endif
        
        /// <summary>
        /// Get cached attribution token if available and not expired
        /// </summary>
        public static string CachedAttributionToken
        {
            get
            {
                if (string.IsNullOrEmpty(_cachedAttributionToken) || 
                    DateTime.UtcNow - _tokenCacheTime > TOKEN_CACHE_DURATION)
                {
                    return null;
                }
                return _cachedAttributionToken;
            }
        }
        
        /// <summary>
        /// Get iOS attribution token for Apple Search Ads attribution
        /// Returns cached token if available, otherwise collects new one
        /// </summary>
        /// <param name="onComplete">Callback with attribution token (null if unavailable)</param>
        public static void GetAttributionToken(Action<string> onComplete)
        {
#if UNITY_IOS && !UNITY_EDITOR
            
            // Return cached token if still valid
            var cached = CachedAttributionToken;
            if (!string.IsNullOrEmpty(cached))
            {
                BoostOpsLogger.LogDebug("Attribution", "Using cached attribution token");
                onComplete?.Invoke(cached);
                return;
            }
            
            // Prevent multiple simultaneous collection attempts
            if (_isCollecting)
            {
                BoostOpsLogger.LogDebug("Attribution", "Attribution collection already in progress");
                onComplete?.Invoke(null);
                return;
            }
            
            _isCollecting = true;
            
            try
            {
                // Use AdServices framework (iOS 14.3+) - only supported attribution method
                if (_BoostOps_IsAdServicesAvailable())
                {
                    BoostOpsLogger.LogDebug("Attribution", "Using AdServices framework (iOS 14.3+)");
                    
                    string token = _BoostOps_GetAttributionToken();
                    
                    if (!string.IsNullOrEmpty(token))
                    {
                        CacheAttributionToken(token);
                        BoostOpsLogger.LogDebug("Attribution", "Attribution token collected successfully");
                        onComplete?.Invoke(token);
                    }
                    else
                    {
                        BoostOpsLogger.LogDebug("Attribution", "No attribution token available (user likely didn't come from Apple Search Ads)");
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    // AdServices framework not available (iOS < 14.3)
                    BoostOpsLogger.LogWarning("Attribution", "Attribution not supported on iOS < 14.3 (AdServices framework required)");
                    BoostOpsLogger.LogWarning("Attribution", "Note: iAd framework was deprecated and removed by Apple");
                    onComplete?.Invoke(null);
                }
            }
            catch (Exception e)
            {
                BoostOpsLogger.LogError("Attribution", $"Failed to get attribution token: {e.Message}");
                onComplete?.Invoke(null);
            }
            finally
            {
                _isCollecting = false;
            }
            
#else
            // Not iOS or in editor - no attribution token available
            BoostOpsLogger.LogDebug("Attribution", "Attribution tokens only available on iOS devices");
            onComplete?.Invoke(null);
#endif
        }
        
        /// <summary>
        /// Cache attribution token with timestamp
        /// </summary>
        public static void CacheAttributionToken(string token)
        {
            _cachedAttributionToken = token;
            _tokenCacheTime = DateTime.UtcNow;
            
            // Persist to PlayerPrefs for app restart scenarios
            PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.ATTRIBUTION_TOKEN, token);
            PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.ATTRIBUTION_TOKEN_TIME, _tokenCacheTime.ToBinary().ToString());
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Load cached attribution token from PlayerPrefs on app start
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadCachedToken()
        {
            string cachedToken = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.ATTRIBUTION_TOKEN, "");
            string cachedTimeStr = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.ATTRIBUTION_TOKEN_TIME, "");
            
            if (!string.IsNullOrEmpty(cachedToken) && !string.IsNullOrEmpty(cachedTimeStr))
            {
                if (long.TryParse(cachedTimeStr, out long timeBinary))
                {
                    DateTime cacheTime = DateTime.FromBinary(timeBinary);
                    
                    // Only use cached token if still valid
                    if (DateTime.UtcNow - cacheTime <= TOKEN_CACHE_DURATION)
                    {
                        _cachedAttributionToken = cachedToken;
                        _tokenCacheTime = cacheTime;
                        BoostOpsLogger.LogDebug("Attribution", "Loaded cached attribution token from PlayerPrefs");
                    }
                    else
                    {
                        // Clear expired token
                        ClearCachedToken();
                    }
                }
            }
        }
        
        /// <summary>
        /// Clear cached attribution token
        /// </summary>
        public static void ClearCachedToken()
        {
            _cachedAttributionToken = null;
            _tokenCacheTime = default;
            
            PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.ATTRIBUTION_TOKEN);
            PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.ATTRIBUTION_TOKEN_TIME);
            PlayerPrefs.Save();
            
            BoostOpsLogger.LogDebug("Attribution", "Cleared cached attribution token");
        }
        
        /// <summary>
        /// Get debug information about attribution state
        /// </summary>
        public static string GetDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== BoostOps Attribution Debug Info ===");
            info.AppendLine($"Platform: {Application.platform}");
            
#if UNITY_IOS && !UNITY_EDITOR
            info.AppendLine($"AdServices Available: {_BoostOps_IsAdServicesAvailable()}");
#else
            info.AppendLine("AdServices Available: false (not iOS)");
#endif
            
            info.AppendLine($"Cached Token: {(!string.IsNullOrEmpty(_cachedAttributionToken) ? _cachedAttributionToken.Substring(0, Math.Min(20, _cachedAttributionToken.Length)) + "..." : "null")}");
            info.AppendLine($"Cache Time: {_tokenCacheTime:yyyy-MM-dd HH:mm:ss} UTC");
            info.AppendLine($"Cache Valid: {CachedAttributionToken != null}");
            info.AppendLine($"Is Collecting: {_isCollecting}");
            
            return info.ToString();
        }
    }
    
    /// <summary>
    /// Callback component for legacy async attribution API - no longer used
    /// Note: Kept for backward compatibility, but iAd framework was deprecated and removed
    /// </summary>
    internal class AttributionTokenCallback : MonoBehaviour
    {
        private Action<string> _onComplete;
        
        public void Initialize(Action<string> onComplete)
        {
            _onComplete = onComplete;
            
            // Auto-destroy after timeout to prevent memory leaks
            StartCoroutine(TimeoutCoroutine());
        }
        
        /// <summary>
        /// Called by native iOS code with attribution result - no longer used
        /// Note: Legacy iAd framework was deprecated and removed by Apple
        /// </summary>
        public void OnAttributionTokenReceived(string attributionData)
        {
            try
            {
                BoostOpsLogger.LogDebug("Attribution", $"Legacy attribution callback received: {(!string.IsNullOrEmpty(attributionData) ? "data present" : "no data")}");
                
                // Parse attribution data if present
                string token = null;
                if (!string.IsNullOrEmpty(attributionData) && attributionData != "null")
                {
                    // For legacy API, we might get a JSON response - extract what we need
                    // This is a simplified version - you might need more complex parsing
                    token = attributionData;
                    BoostOpsAttributionManager.CacheAttributionToken(token);
                }
                
                _onComplete?.Invoke(token);
            }
            catch (Exception e)
            {
                BoostOpsLogger.LogError("Attribution", $"Error in attribution callback: {e.Message}");
                _onComplete?.Invoke(null);
            }
            finally
            {
                // Clean up
                Destroy(gameObject);
            }
        }
        
        private IEnumerator TimeoutCoroutine()
        {
            yield return new WaitForSeconds(10f); // 10 second timeout
            
            if (gameObject != null)
            {
                BoostOpsLogger.LogWarning("Attribution", "Attribution collection timed out");
                _onComplete?.Invoke(null);
                Destroy(gameObject);
            }
        }
    }
}