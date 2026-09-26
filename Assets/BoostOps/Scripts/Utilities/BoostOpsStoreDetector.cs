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
        /// Normalize a Microsoft Store destination into a protocol link that launches the Store
        /// app directly (ms-windows-store://pdp/?productid=XXXX) rather than opening the browser.
        ///
        /// Precedence:
        ///   1. An explicit Microsoft Store ID (store_ids.microsoft) -> protocol link. This is the
        ///      canonical source of truth and never depends on how the server formats the URL.
        ///   2. An existing ms-windows-store:// link -> passthrough.
        ///   3. An apps.microsoft.com / microsoft.com web URL -> extract the product ID and convert
        ///      to a protocol link so we open the Store app instead of the browser.
        ///   4. Any other non-empty URL -> returned as-is (don't drop a potentially working link).
        /// Returns null when nothing resolvable is provided.
        /// </summary>
        public static string ResolveMicrosoftStoreUrl(string microsoftUrl, string microsoftStoreId)
        {
            // 1) Canonical: build the protocol link straight from the Store ID.
            if (!string.IsNullOrEmpty(microsoftStoreId))
                return "ms-windows-store://pdp/?productid=" + microsoftStoreId;

            if (!string.IsNullOrEmpty(microsoftUrl))
            {
                // 2) Already a protocol link.
                if (microsoftUrl.StartsWith("ms-windows-store:", System.StringComparison.OrdinalIgnoreCase))
                    return microsoftUrl;

                // 3) Web URL - pull the product ID (e.g. .../store/detail/9P0480XJGXFG or
                //    ?productid=9P0480XJGXFG) and convert to the Store app protocol.
                if (microsoftUrl.IndexOf("microsoft.com", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        microsoftUrl,
                        @"(?:detail/|productid=)([A-Za-z0-9]{10,14})",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                        return "ms-windows-store://pdp/?productid=" + match.Groups[1].Value;
                }

                // 4) Unrecognized but non-empty - keep it.
                return microsoftUrl;
            }

            return null;
        }

        /// <summary>
        /// Append a Microsoft Store custom campaign ID (cid) to a Store destination so the click
        /// shows up in Partner Center's Acquisitions-by-campaign report. Works for both the
        /// ms-windows-store:// protocol link and an apps.microsoft.com web URL. No-ops for
        /// non-Microsoft URLs, empty campaign IDs, or when a cid is already present.
        ///
        /// Note: unlike Android's Play install referrer, the Microsoft Store does NOT pass this
        /// value through to the freshly installed app, so it only drives Store-side campaign
        /// reporting - BoostOps install attribution on Windows still relies on the click event
        /// (TrackClick) plus fingerprint matching, as on iOS.
        /// </summary>
        public static string AppendMicrosoftCampaignId(string url, string campaignId)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(campaignId))
                return url;

            bool isMicrosoft = url.StartsWith("ms-windows-store:", System.StringComparison.OrdinalIgnoreCase)
                            || url.IndexOf("microsoft.com", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isMicrosoft)
                return url;

            // Don't double-append if a campaign id is already present.
            if (System.Text.RegularExpressions.Regex.IsMatch(url, @"[?&]cid=", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return url;

            // Microsoft Store custom campaign IDs are limited to 100 characters.
            string cid = System.Uri.EscapeDataString(campaignId);
            if (cid.Length > 100)
                cid = cid.Substring(0, 100);

            char sep = url.IndexOf('?') >= 0 ? '&' : '?';
            return url + sep + "cid=" + cid;
        }

        /// <summary>
        /// Resolve the Microsoft Store destination for a campaign as a Store-app protocol link.
        /// Prefers store_ids.microsoft (canonical), converts a web microsoft URL when only that is
        /// present, and returns null when neither is available so callers never fall back to a
        /// non-Windows store link.
        /// </summary>
        public static string GetWindowsStoreUrl(Campaign campaign)
        {
            var urls = campaign?.target_project?.store_urls;
            var ids = campaign?.target_project?.store_ids;

            // Diagnostic: surface exactly what the BoostOps server returned for this campaign so
            // we can tell whether the Microsoft URL/ID is present or the server config needs fixing.
            Debug.Log($"[BoostOpsStoreDetector] 🪟 Windows resolution for '{campaign?.name}' (target_project_id='{campaign?.target_project?.project_id ?? "null"}'):\n" +
                      $"    store_urls.microsoft = '{urls?.microsoft ?? "null"}'\n" +
                      $"    store_ids.microsoft  = '{ids?.microsoft ?? "null"}'\n" +
                      $"    (other urls) google='{urls?.google ?? "null"}', apple='{urls?.apple ?? "null"}', amazon='{urls?.amazon ?? "null"}', samsung='{urls?.samsung ?? "null"}'");

            string resolved = ResolveMicrosoftStoreUrl(urls?.microsoft, ids?.microsoft);
            if (!string.IsNullOrEmpty(resolved))
            {
                // Tag with the campaign id for Microsoft Store acquisition reporting.
                resolved = AppendMicrosoftCampaignId(resolved, campaign?.campaign_id ?? campaign?.name);
                Debug.Log($"[BoostOpsStoreDetector] 🪟 Resolved Microsoft Store destination (Store-app protocol): {resolved}");
                return resolved;
            }

            Debug.LogWarning($"[BoostOpsStoreDetector] ⚠️ No Microsoft Store URL or Store ID for campaign '{campaign?.name}'. " +
                             "Server config likely missing the Microsoft store mapping for this target project.");
            return null;
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
            var _ids = campaign?.target_project?.store_ids;
            Debug.Log($"[BoostOpsStoreDetector] 🏪 Available URLs: Google='{storeUrls.google ?? "null"}', Apple='{storeUrls.apple ?? "null"}', Amazon='{storeUrls.amazon ?? "null"}', Microsoft='{storeUrls.microsoft ?? "null"}' | store_ids.microsoft='{_ids?.microsoft ?? "null"}'");
            
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
                {
                    var winUrl = GetWindowsStoreUrl(campaign);
                    if (!string.IsNullOrEmpty(winUrl))
                        return winUrl;
                    break;
                }
                    
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
#elif UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_WINRT
            string result = GetWindowsStoreUrl(campaign);
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
#elif (UNITY_STANDALONE_WIN || UNITY_WSA) && !UNITY_EDITOR
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

#if (UNITY_STANDALONE_WIN || UNITY_WSA) && !UNITY_EDITOR
        private static AppStore DetectWindowsStore()
        {
#if UNITY_WSA
            // UWP packages are distributed through the Microsoft Store.
            return AppStore.WindowsStore;
#else
            // Standalone Windows builds are distributed directly or via MS Store
            // UWP API (Windows.ApplicationModel) is not available in StandaloneWindows64
            return AppStore.Sideloaded;
#endif
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