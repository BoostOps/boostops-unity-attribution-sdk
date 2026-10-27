using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Single path for opening a promo destination after a click has been logged.
    /// The same click_id must go into the click event and the store URL, or the
    /// install can't be matched back to the click.
    /// </summary>
    internal static class BoostOpsStoreLauncher
    {
        internal static string NewClickId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Add Play Install Referrer parameters to a Google Play URL. Other stores
        /// can't carry a click-level id and are returned unchanged.
        /// </summary>
        internal static string WithAttribution(string storeUrl, string clickId, string campaignSlug,
            string placement, string channel, string targetStoreId, string targetProjectId)
        {
            if (string.IsNullOrEmpty(storeUrl) || string.IsNullOrEmpty(clickId))
                return storeUrl;

            bool isPlay = storeUrl.IndexOf("play.google.com/store/apps/details", StringComparison.OrdinalIgnoreCase) >= 0
                || storeUrl.StartsWith("market://details", StringComparison.OrdinalIgnoreCase);
            if (!isPlay || storeUrl.IndexOf("referrer=", StringComparison.OrdinalIgnoreCase) >= 0)
                return storeUrl;

            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("utm_source", "boostops"),
                new KeyValuePair<string, string>("utm_medium", string.IsNullOrEmpty(channel) ? "xpromo" : channel),
                new KeyValuePair<string, string>("utm_campaign", campaignSlug ?? ""),
                new KeyValuePair<string, string>("utm_content", placement ?? ""),
                new KeyValuePair<string, string>("click_id", clickId),
                new KeyValuePair<string, string>("click_ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()),
                new KeyValuePair<string, string>("source_store_id", BoostOpsAnalyticsContract.GetSourceStoreId() ?? ""),
                new KeyValuePair<string, string>("target_store_id", targetStoreId ?? ""),
                new KeyValuePair<string, string>("target_project_id", targetProjectId ?? "")
            };

            var parts = new List<string>(pairs.Count);
            foreach (var p in pairs)
                parts.Add(Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value));

            // The whole referrer must be one encoded value; raw '&' would split it
            // into separate Play URL params and Play would keep only the first pair.
            string referrer = Uri.EscapeDataString(string.Join("&", parts.ToArray()));
            string separator = storeUrl.Contains("?") ? "&" : "?";
            return storeUrl + separator + "referrer=" + referrer;
        }

        /// <summary>
        /// Open a store or link URL: Microsoft Store protocol on Windows, the
        /// in-app App Store sheet on iOS when possible, otherwise OpenURL.
        /// </summary>
        internal static void Open(string url, string appleAppStoreId = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning("[BoostOps] No store URL to open");
                return;
            }

            // Leaving for the store can suspend the app before the click flush finishes.
            BoostOps.Analytics.BoostOpsAnalyticsClient.ExistingInstance?.PersistQueueSnapshot();

#if (UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_WINRT) && !UNITY_EDITOR
            url = BoostOpsStoreDetector.ResolveMicrosoftStoreUrl(url, null) ?? url;
#endif

#if UNITY_IOS && !UNITY_EDITOR
            string appStoreId = !string.IsNullOrEmpty(appleAppStoreId)
                ? appleAppStoreId
                : (url.Contains("apps.apple.com") ? BoostOpsAppStoreSheet.ExtractAppStoreId(url) : null);
            if (!string.IsNullOrEmpty(appStoreId) && BoostOpsAppStoreSheet.IsAvailable()
                && BoostOpsAppStoreSheet.ShowAppStoreSheet(appStoreId))
            {
                return;
            }
#endif

            try
            {
                Application.OpenURL(url);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to open store URL: {e.Message}");
            }
        }
    }
}
