using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Best-in-class cross-promotion system using direct store links.
    /// Provides client-side URL generation with full attribution tracking.
    /// </summary>
    public static class BoostOpsCrossPromotion
    {
        /// <summary>
        /// Generates a direct store URL for cross-promotion with full attribution parameters.
        /// Android: Play Store link with Install Referrer params
        /// iOS: App Store link with campaign token
        /// Internal use only - developers should use TrackClickAndOpenStore() instead.
        /// </summary>
        internal static string GenerateStoreUrl(
            string targetStoreId,
            string targetProjectId,
            string campaignSlug,
            string placement,
            string clickId = null,
            string sourceStoreId = null,
            string sourceProjectId = null,
            Dictionary<string, string> customParams = null)
        {
            // Auto-generate click ID if not provided
            if (string.IsNullOrEmpty(clickId))
            {
                clickId = GenerateClickId();
            }

            // Auto-detect source IDs if not provided
            if (string.IsNullOrEmpty(sourceStoreId))
            {
                sourceStoreId = GetStoreIdInternal();
            }

            if (string.IsNullOrEmpty(sourceProjectId))
            {
                sourceProjectId = BoostOpsAnalyticsContract.GetSourceProjectId();
            }

            // Validate required parameters
            if (string.IsNullOrEmpty(targetStoreId))
            {
                Debug.LogError("[BoostOps CrossPromo] Target store ID is required");
                return null;
            }

#if UNITY_ANDROID
            return GenerateAndroidStoreUrl(
                targetStoreId,
                targetProjectId,
                campaignSlug,
                placement,
                clickId,
                sourceStoreId,
                sourceProjectId,
                customParams
            );
#elif UNITY_IOS
            return GenerateIOSStoreUrl(
                targetStoreId,
                targetProjectId,
                campaignSlug,
                placement,
                clickId,
                sourceStoreId,
                sourceProjectId,
                customParams
            );
#elif UNITY_WSA || UNITY_STANDALONE_WIN
            return GenerateWindowsStoreUrl(
                targetStoreId,
                targetProjectId,
                campaignSlug,
                placement,
                clickId,
                sourceStoreId,
                sourceProjectId,
                customParams
            );
#else
            Debug.LogWarning("[BoostOps CrossPromo] Platform not supported for cross-promotion");
            return null;
#endif
        }

        /// <summary>
        /// Tracks a cross-promotion click and opens the store.
        /// This is the recommended all-in-one method for cross-promotion.
        /// </summary>
        /// <param name="storeUrl">The app store URL to open (e.g., "https://apps.apple.com/app/id1234567890" or "https://play.google.com/store/apps/details?id=com.example.app")</param>
        /// <param name="campaignSlug">Campaign identifier (e.g., "summer_promo", "holiday_campaign")</param>
        /// <param name="placement">Where the ad is shown (e.g., "main_menu", "level_complete")</param>
        /// <param name="format">Ad format (e.g., "banner", "interstitial", "rewarded")</param>
        public static void TrackClickAndOpenStore(
            string storeUrl,
            string campaignSlug,
            string placement,
            string format = "")
        {
            if (string.IsNullOrEmpty(storeUrl))
            {
                Debug.LogError("[BoostOps CrossPromo] Store URL is required");
                return;
            }

            // Extract target store ID from URL
            string targetStoreId = ExtractStoreIdFromUrl(storeUrl);
            if (string.IsNullOrEmpty(targetStoreId))
            {
                Debug.LogError($"[BoostOps CrossPromo] Could not extract store ID from URL: {storeUrl}");
                return;
            }

            // Generate unique click ID
            string clickId = GenerateClickId();

            // Auto-detect source IDs
            string sourceStoreId = GetStoreIdInternal();
            // Note: sourceProjectId removed - server derives from project_key

            // Track click event (for attribution matching)
            // Server can look up the project_id from the store_id
            BoostOpsAnalyticsContract.TrackClick(
                campaignSlug: campaignSlug,
                placement: placement,
                format: format,
                // Note: source_store_id is in context.store_id (universal) - not passed here
                // Note: source_project_id is derived server-side from project_key
                targetStoreId: targetStoreId,
                targetProjectId: "",  // Server derives this from targetStoreId
                networkCampaignId: clickId,
                channel: "xpromo"
            );

            // Open the store URL directly (no need to regenerate it)
            Debug.Log($"[BoostOps CrossPromo] Opening store: {storeUrl}");
            Application.OpenURL(storeUrl);
        }

        /// <summary>
        /// Extracts the store ID from an App Store or Play Store URL
        /// </summary>
        private static string ExtractStoreIdFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            try
            {
                // iOS App Store: https://apps.apple.com/app/id1234567890 or https://apps.apple.com/app/apple-store/id1234567890
                if (url.Contains("apps.apple.com"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"id(\d+)");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
                // Android Play Store: https://play.google.com/store/apps/details?id=com.example.app
                else if (url.Contains("play.google.com"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"[?&]id=([^&]+)");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
                // Microsoft Store: https://apps.microsoft.com/store/detail/9XXXXXXX or ms-windows-store://pdp/?productid=9XXXXXXX
                else if (url.Contains("microsoft.com") || url.Contains("ms-windows-store"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"(?:detail/|productid=)([A-Z0-9]{10,14})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                        return match.Groups[1].Value;
                }

                Debug.LogWarning($"[BoostOps CrossPromo] Unrecognized store URL format: {url}");
                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BoostOps CrossPromo] Error parsing store URL: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generates a unique click identifier using GUID.
        /// Format: lowercase alphanumeric without dashes for better URL compatibility.
        /// </summary>
        private static string GenerateClickId()
        {
            return Guid.NewGuid().ToString("N"); // "N" format = 32 hex digits without dashes
        }

        #region Android Implementation

        private static string GenerateAndroidStoreUrl(
            string packageName,
            string targetProjectId,
            string campaignSlug,
            string placement,
            string clickId,
            string sourceStoreId,
            string sourceProjectId,
            Dictionary<string, string> customParams)
        {
            // Build referrer parameters
            var referrerParams = new List<string>
            {
                "utm_source=boostops",
                $"utm_campaign={Uri.EscapeDataString(campaignSlug ?? "")}",
                $"utm_medium={Uri.EscapeDataString(placement ?? "")}",
                $"click_id={Uri.EscapeDataString(clickId ?? "")}",
                $"source_store_id={Uri.EscapeDataString(sourceStoreId ?? "")}",
                $"source_project_id={Uri.EscapeDataString(sourceProjectId ?? "")}",
                $"target_store_id={Uri.EscapeDataString(packageName ?? "")}",
                $"target_project_id={Uri.EscapeDataString(targetProjectId ?? "")}"
            };

            // Add custom parameters
            if (customParams != null)
            {
                foreach (var kvp in customParams)
                {
                    referrerParams.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? "")}");
                }
            }

            // Join referrer parameters
            string referrer = string.Join("&", referrerParams.ToArray());

            // Build final URL
            return $"https://play.google.com/store/apps/details?id={packageName}&referrer={referrer}";
        }

        #endregion

        #region iOS Implementation

        private static string GenerateIOSStoreUrl(
            string appStoreId,
            string targetProjectId,
            string campaignSlug,
            string placement,
            string clickId,
            string sourceStoreId,
            string sourceProjectId,
            Dictionary<string, string> customParams)
        {
            // iOS App Store links support limited parameters
            // ct (campaign token): campaign identifier for App Store Connect reporting
            // pt (provider token): affiliate/provider ID (optional)
            // mt (media type): always 8 for apps

            // Build campaign token: include campaign name for Apple reporting
            // Note: ct is for campaign-level reporting, not click-level tracking
            // Click-level tracking is done via TrackClick() + attribution matching
            string campaignToken = $"boostops_{campaignSlug}";
            
            // For click-level granularity, we could append click_id to ct, but:
            // - Apple has 40 char limit on ct
            // - Better to use attribution matching (already implemented)
            // - ct should be campaign-level for App Store Connect reporting

            // Build base URL
            string url = $"https://apps.apple.com/app/id{appStoreId}?ct={Uri.EscapeDataString(campaignToken)}&mt=8";

            // Note: We cannot pass source_store_id, target_project_id, click_id via App Store URL
            // These are tracked server-side via TrackClick() event + attribution matching
            // This is why iOS attribution uses IDFV + probabilistic matching (95-98% match rate)

            return url;
        }

        #endregion

        #region Windows Implementation

        private static string GenerateWindowsStoreUrl(
            string storeId,
            string targetProjectId,
            string campaignSlug,
            string placement,
            string clickId,
            string sourceStoreId,
            string sourceProjectId,
            Dictionary<string, string> customParams)
        {
            // Microsoft Store URLs support cid (campaign ID) parameter for attribution
            string campaignToken = $"boostops_{Uri.EscapeDataString(campaignSlug ?? "")}_{Uri.EscapeDataString(clickId ?? "")}";
            return $"https://apps.microsoft.com/store/detail/{storeId}?cid={campaignToken}";
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Internal method to get the current app's store ID
        /// </summary>
        private static string GetStoreIdInternal()
        {
            try
            {
                var settings = BoostOps.Internal.InternalSettingsCache.GetProjectSettings();
                if (settings == null) return "";

#if UNITY_IOS
                return settings.AppleAppStoreId ?? "";
#elif UNITY_ANDROID
                return settings.AndroidPackageName ?? "";
#elif UNITY_WSA || UNITY_WINRT || UNITY_STANDALONE_WIN
                return settings.MicrosoftStoreId ?? "";
#else
                return "";
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BoostOps CrossPromo] Failed to get store ID: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// Validates a store ID format (internal use only)
        /// </summary>
        private static bool IsValidStoreId(string storeId)
        {
            if (string.IsNullOrEmpty(storeId))
                return false;

#if UNITY_ANDROID
            // Android package name format: com.company.app
            return storeId.Contains(".");
#elif UNITY_IOS
            // iOS App Store ID format: numeric string
            return System.Text.RegularExpressions.Regex.IsMatch(storeId, @"^\d+$");
#else
            return false;
#endif
        }

        #endregion
    }
}
