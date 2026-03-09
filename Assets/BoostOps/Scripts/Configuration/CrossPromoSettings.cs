using UnityEngine;
using System;

namespace BoostOps.CrossPromo
{
    /// <summary>
    /// Runtime settings for cross-promotion loaded from JSON.
    /// This is the exact structure that gets serialized to StreamingAssets/cross_promo_local.json
    /// </summary>
    [Serializable]
    public class CrossPromoSettings
    {
        public int version = 1;
        public string defaultDomain = "boostlink.me";
        public string campaignSlug = "cp";
        public string rotation = "weighted_random";
        public CrossPromoSource[] sources;
    }

    /// <summary>
    /// A source game and its list of promotion targets
    /// </summary>
    [Serializable]
    public class CrossPromoSource
    {
        public string sourceBundleId;
        public CrossPromoTarget[] targets;
    }

    /// <summary>
    /// Serializable version of TargetGame for JSON export/import
    /// </summary>
    [Serializable]
    public class CrossPromoTarget
    {
        public string id;
        public string boostLinkSlug;
        public string androidPackageId;
        public string iosAppStoreId;
        public string iosBundleId;
        public string microsoftStoreId;
        public string amazonStoreId;
        public string samsungStoreId;
        public int weight = 100;
        public int freqCap = 2;
        public string headline;
        // Note: Sprite icon is not serialized to JSON; handle separately for runtime

        /// <summary>
        /// Creates a CrossPromoTarget from a TargetGame
        /// </summary>
        public static CrossPromoTarget FromTargetGame(TargetGame targetGame, CrossPromoTable table = null)
        {
            return new CrossPromoTarget
            {
                id = targetGame.id,
                boostLinkSlug = targetGame.boostLinkSlug,
                androidPackageId = targetGame.androidPackageId,
                iosAppStoreId = targetGame.iosAppStoreId,
                iosBundleId = targetGame.iosBundleId,
                microsoftStoreId = targetGame.microsoftStoreId,
                amazonStoreId = targetGame.amazonStoreId,
                samsungStoreId = targetGame.samsungStoreId,
                weight = targetGame.weight,
                freqCap = targetGame.GetEffectiveFrequencyCap(table).impressions,
                headline = targetGame.headline
            };
        }

        /// <summary>
        /// Gets the appropriate store URL for the current platform
        /// </summary>
        public string GetStoreUrl()
        {
#if UNITY_ANDROID
            // Check Samsung Galaxy Store first for Android
            if (!string.IsNullOrEmpty(samsungStoreId))
                return $"samsungapps://ProductDetail/{samsungStoreId}";
            // Check Amazon App Store for Android devices with Amazon store
            if (!string.IsNullOrEmpty(amazonStoreId))
            {
                // Support both ASIN and package name formats for Amazon
                if (amazonStoreId.Length == 10 && System.Text.RegularExpressions.Regex.IsMatch(amazonStoreId, @"^[A-Z0-9]{10}$"))
                {
                    // ASIN format (10-character alphanumeric)
                    return $"https://www.amazon.com/dp/{amazonStoreId}";
                }
                else if (amazonStoreId.Contains("."))
                {
                    // Package name format
                    return $"https://www.amazon.com/gp/mas/dl/android?p={amazonStoreId}";
                }
                else
                {
                    // Assume it's a package name if it doesn't match ASIN pattern
                    return $"https://www.amazon.com/gp/mas/dl/android?p={amazonStoreId}";
                }
            }
            // Fallback to Google Play Store
            if (!string.IsNullOrEmpty(androidPackageId))
                return $"https://play.google.com/store/apps/details?id={androidPackageId}";
#elif UNITY_IOS
            if (!string.IsNullOrEmpty(iosAppStoreId))
                return $"https://apps.apple.com/app/id{iosAppStoreId}";
#elif UNITY_WSA || UNITY_STANDALONE_WIN
            if (!string.IsNullOrEmpty(microsoftStoreId))
                return $"https://apps.microsoft.com/store/detail/{microsoftStoreId}";
#endif
            return null;
        }

        /// <summary>
        /// Checks if this target is valid for the current platform
        /// </summary>
        public bool IsValidForCurrentPlatform()
        {
#if UNITY_ANDROID
            return !string.IsNullOrEmpty(androidPackageId) || 
                   !string.IsNullOrEmpty(samsungStoreId) || 
                   !string.IsNullOrEmpty(amazonStoreId);
#elif UNITY_IOS
            return !string.IsNullOrEmpty(iosAppStoreId) && !string.IsNullOrEmpty(iosBundleId);
#elif UNITY_WSA || UNITY_STANDALONE_WIN
            return !string.IsNullOrEmpty(microsoftStoreId);
#else
            return false;
#endif
        }
    }
} 