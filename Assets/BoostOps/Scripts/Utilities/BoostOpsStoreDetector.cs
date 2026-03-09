using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Detects which app store the current app was installed from
    /// </summary>
    public static class BoostOpsStoreDetector
    {
        public enum AppStore
        {
            Unknown,
            GooglePlay,
            Amazon,
            Samsung,
            Huawei,
            iOS,           // iOS App Store (iPhone/iPad)
            macOS,         // macOS App Store (Mac)
            WindowsStore,
            Sideloaded
        }
        
        private static AppStore? cachedStore = null;
        
        /// <summary>
        /// Get the app store this app was installed from
        /// Results are cached for performance
        /// </summary>
        public static AppStore GetCurrentStore()
        {
            if (cachedStore.HasValue)
                return cachedStore.Value;
                
            cachedStore = DetectStore();
            BoostOpsLogger.LogDebug("StoreDetector", $"Detected app store: {cachedStore.Value}");
            return cachedStore.Value;
        }
        
        /// <summary>
        /// Check if we should show cross-promo for a specific store
        /// Since we default to Google Play, we're less restrictive now
        /// </summary>
        public static bool ShouldShowStorePromo(string storeUrl)
        {
            if (string.IsNullOrEmpty(storeUrl))
                return false;
                
            var currentStore = GetCurrentStore();
            
            // Only restrict if we're 100% certain of the store and it's clearly incompatible
            // For example, don't show iOS/macOS App Store links on Android
            if ((currentStore == AppStore.iOS || currentStore == AppStore.macOS) && storeUrl.Contains("play.google.com"))
                return false;
                
            // Otherwise, show the promo (cross-promotion generally works across stores)
            return true;
        }
        
        /// <summary>
        /// Get the best store URL for the current platform and store
        /// Returns platform-appropriate URL based on detected store or falls back to platform defaults
        /// Note: On Android, Unknown/Sideloaded cases default to Google Play for best user experience
        /// </summary>
        public static string GetBestStoreUrl(Campaign campaign)
        {
            if (campaign?.target_project?.store_urls == null)
                return null;
                
            var currentStore = GetCurrentStore();
            var storeUrls = campaign.target_project.store_urls;
            
            Debug.Log($"[BoostOpsStoreDetector] 🏪 GetBestStoreUrl called - Current store: {currentStore}");
            Debug.Log($"[BoostOpsStoreDetector] 🏪 Available URLs: Google='{storeUrls.google ?? "null"}', Apple='{storeUrls.apple ?? "null"}', Amazon='{storeUrls.amazon ?? "null"}'");
            
            // Try to match current store first
            switch (currentStore)
            {
                case AppStore.GooglePlay:
                    if (!string.IsNullOrEmpty(storeUrls.google))
                        return storeUrls.google;
                    break;
                    
                case AppStore.Amazon:
                    if (!string.IsNullOrEmpty(storeUrls.amazon))
                        return storeUrls.amazon;
                    break;
                    
                case AppStore.Samsung:
                    if (!string.IsNullOrEmpty(storeUrls.samsung))
                        return storeUrls.samsung;
                    break;
                    
                case AppStore.iOS:
                    if (!string.IsNullOrEmpty(storeUrls.apple))
                        return storeUrls.apple;
                    break;
                    
                case AppStore.macOS:
                    if (!string.IsNullOrEmpty(storeUrls.apple))  // macOS apps also use Apple App Store links
                        return storeUrls.apple;
                    break;
                    
                case AppStore.WindowsStore:
                    if (!string.IsNullOrEmpty(storeUrls.microsoft))
                        return storeUrls.microsoft;
                    break;
                    
                case AppStore.Huawei:
                    // Huawei AppGallery doesn't have a direct link in most campaigns
                    // Fall through to platform defaults
                    break;
                    
                case AppStore.Unknown:
                case AppStore.Sideloaded:
                    // For unknown or sideloaded apps, use platform defaults
                    // On Android: Default to Google Play for best user experience
                    // On other platforms: Use appropriate platform store
                    break;
            }
            
            // Default fallback logic based on platform
            // This handles Unknown/Sideloaded cases and provides best user experience
#if UNITY_IOS
            string result = storeUrls.apple;
            Debug.Log($"[BoostOpsStoreDetector] 🏪 iOS fallback selected: '{result ?? "null"}'");
            return result;
#elif UNITY_STANDALONE_WIN
            string result = storeUrls.microsoft;
            Debug.Log($"[BoostOpsStoreDetector] 🏪 Windows fallback selected: '{result ?? "null"}'");
            return result;
#elif UNITY_ANDROID
            // For Android: Default to Google Play (most common), then try alternatives
            // This ensures unknown/sideloaded Android apps get working store links
            string result = storeUrls.google ?? storeUrls.amazon ?? storeUrls.samsung;
            Debug.Log($"[BoostOpsStoreDetector] 🏪 Android fallback selected: '{result ?? "null"}' (Google: '{storeUrls.google ?? "null"}', Amazon: '{storeUrls.amazon ?? "null"}', Samsung: '{storeUrls.samsung ?? "null"}')");
            return result;
#else
            // For other platforms, try Google Play first, then Apple
            string result = storeUrls.google ?? storeUrls.apple;
            Debug.Log($"[BoostOpsStoreDetector] 🏪 Other platform fallback selected: '{result ?? "null"}' (Google: '{storeUrls.google ?? "null"}', Apple: '{storeUrls.apple ?? "null"}')");
            return result;
#endif
        }
        
        private static AppStore DetectStore()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return DetectAndroidStore();
#elif UNITY_IOS && !UNITY_EDITOR
            return AppStore.iOS;
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            return AppStore.macOS;
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return DetectWindowsStore();
#else
            // For editor and other platforms, mark as unknown
            BoostOpsLogger.LogDebug("StoreDetector", "Running in editor or unsupported platform - marking as unknown");
            return AppStore.Unknown;
#endif
        }
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AppStore DetectAndroidStore()
        {
            try
            {
                // Unity recommended approach: use Application.installerName
                string installer = Application.installerName;
                
                if (string.IsNullOrEmpty(installer))
                {
                    BoostOpsLogger.LogDebug("StoreDetector", "No installer found - marking as sideloaded");
                    return AppStore.Sideloaded; // No installer = sideloaded/debug build
                }
                
                BoostOpsLogger.LogDebug("StoreDetector", $"Installer package: {installer}");
                
                switch (installer.ToLower())
                {
                    case "com.android.vending":
                        return AppStore.GooglePlay;
                    case "com.amazon.venezia":
                        return AppStore.Amazon;
                    case "com.sec.android.app.samsungapps":
                        return AppStore.Samsung;
                    case "com.huawei.appmarket":
                        return AppStore.Huawei;
                    default:
                        BoostOpsLogger.LogDebug("StoreDetector", $"Unknown installer '{installer}' - marking as unknown");
                        return AppStore.Unknown; // Unknown installer = unknown store
                }
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("StoreDetector", $"Failed to detect Android store: {ex.Message} - marking as unknown");
                return AppStore.Unknown; // Detection failed = unknown store
            }
        }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static AppStore DetectWindowsStore()
        {
            // Standalone Windows builds are distributed directly or via MS Store
            // UWP API (Windows.ApplicationModel) is not available in StandaloneWindows64
            return AppStore.Sideloaded;
        }
#endif
        
        /// <summary>
        /// Force refresh store detection (clears cache)
        /// </summary>
        public static void RefreshDetection()
        {
            cachedStore = null;
        }
    }
}