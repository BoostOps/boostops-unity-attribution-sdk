using UnityEngine;
using UnityEngine.Serialization;
using System;
using BoostOps.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoostOps.CrossPromo
{
    /// <summary>
    /// ScriptableObject table containing all cross-promotion target games configuration.
    /// Create via Assets > Create > BoostOps > Cross-Promo Table
    /// </summary>
    [CreateAssetMenu(fileName = "CrossPromoTable", menuName = "BoostOps/Cross-Promo Table", order = 100)]
    public class CrossPromoTable : ScriptableObject
    {
        // NOTE: Source game store IDs are now managed in BoostOpsProjectSettings
        // This eliminates duplication and uses the authoritative source
        
        public TargetGame[] targets = new TargetGame[0];
        public string defaultDomain = "boostlink.me";
        public string campaignSlug = "cp";
        public RotationType rotation = RotationType.Waterfall;
        public FrequencyCap globalFrequencyCap; // Global default frequency cap (new unified object)
        

        
        [Header("Default Text Settings")]
        [Tooltip("Default button text for icon interstitials (can be overridden per campaign)")]
        public string defaultIconInterstitialButtonText = "Play Now!";
        [Tooltip("Default description text for icon interstitials (can be overridden per campaign)")]
        public string defaultIconInterstitialDescription = "Try this awesome game!";
        [Tooltip("Default button text for rich interstitials (can be overridden per campaign)")]
        public string defaultRichInterstitialButtonText = "Play Now!";
        [Tooltip("Default description text for rich interstitials (can be overridden per campaign)")]
        public string defaultRichInterstitialDescription = "Join millions of players in this amazing adventure!";
        
        [Header("User Eligibility Requirements")]
        public int minPlayerSession = 3; // Minimum session count before showing cross-promo (best practice: 3-5)
        public int minPlayerDay = 1; // Minimum days since install before showing cross-promo (best practice: 1-3)
        
        /// <summary>
        /// Ensure frequency cap is properly initialized (called automatically)
        /// </summary>
        private void OnEnable()
        {
            if (globalFrequencyCap == null)
            {
                globalFrequencyCap = FrequencyCap.Unlimited(); // Default to unlimited
            }
        }
        
        /// <summary>
        /// Get the current CrossPromoTable instance from the project
        /// </summary>
        public static CrossPromoTable GetInstance()
        {
            // Try to find CrossPromoTable in Resources/BoostOps
            var table = Resources.Load<CrossPromoTable>("BoostOps/CrossPromoTable");
            if (table != null)
            {
                return table;
            }
            
#if UNITY_EDITOR
            // In editor, try direct path loading
            const string editorPath = "Assets/Resources/BoostOps/CrossPromoTable.asset";
            var editorTable = UnityEditor.AssetDatabase.LoadAssetAtPath<CrossPromoTable>(editorPath);
            if (editorTable != null)
            {
                return editorTable;
            }
            
            // Fallback: search for any CrossPromoTable assets in the project
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CrossPromoTable");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<CrossPromoTable>(path);
            }
#endif
            
            // Return null if not found - fallback values will be used
            return null;
        }
    }

    /// <summary>
    /// Configuration for a single target game in cross-promotion
    /// </summary>
    [Serializable]
    public class TargetGame
    {
        public string id;
        public string boostLinkSlug;
        public string androidPackageId;
        public string iosAppStoreId;
        public string iosBundleId;
        [FormerlySerializedAs("windowsStoreId")]
        public string microsoftStoreId;
        public string amazonStoreId;
        public string samsungStoreId;
        public int weight = 100;
        public FrequencyCap frequencyCap; // New unified frequency cap object
        public bool useCustomFreqCap = true; // Whether to use custom frequency cap instead of global
        public string headline;
        

        public Sprite icon;
        
        [Header("Text Overrides (optional)")]
        [Tooltip("Override icon interstitial button text (leave empty to use global default)")]
        public string customIconInterstitialButtonText;
        [Tooltip("Override icon interstitial description (leave empty to use global default)")]
        public string customIconInterstitialDescription;
        [Tooltip("Override rich interstitial button text (leave empty to use global default)")]
        public string customRichInterstitialButtonText;
        [Tooltip("Override rich interstitial description (leave empty to use global default)")]
        public string customRichInterstitialDescription;

        /// <summary>
        /// Validates that this target game has required fields
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(id) && 
                   !string.IsNullOrEmpty(boostLinkSlug) &&
                   !string.IsNullOrEmpty(headline) &&
                   (HasAndroidConfig() || HasIOSConfig() || HasMicrosoftConfig() || HasAmazonConfig() || HasSamsungConfig());
        }

        public bool HasAndroidConfig() => !string.IsNullOrEmpty(androidPackageId);
        public bool HasIOSConfig() => !string.IsNullOrEmpty(iosAppStoreId) && !string.IsNullOrEmpty(iosBundleId);
        public bool HasMicrosoftConfig() => !string.IsNullOrEmpty(microsoftStoreId);
        public bool HasAmazonConfig() => !string.IsNullOrEmpty(amazonStoreId);
        public bool HasSamsungConfig() => !string.IsNullOrEmpty(samsungStoreId);
        
        /// <summary>
        /// Gets the effective frequency cap object (either custom or global)
        /// </summary>
        public FrequencyCap GetEffectiveFrequencyCap(CrossPromoTable table)
        {
            if (useCustomFreqCap && frequencyCap != null)
                return frequencyCap;
            
            if (table?.globalFrequencyCap != null)
                return table.globalFrequencyCap;
                
            // Fallback to daily cap of 2
            return FrequencyCap.Daily(2);
        }
        

        
        /// <summary>
        /// Gets the effective icon interstitial button text (custom override or global default)
        /// </summary>
        public string GetEffectiveIconInterstitialButtonText(CrossPromoTable table)
        {
            return !string.IsNullOrEmpty(customIconInterstitialButtonText) ? 
                customIconInterstitialButtonText : 
                (table?.defaultIconInterstitialButtonText ?? "Play Now!");
        }
        
        /// <summary>
        /// Gets the effective icon interstitial description (custom override or global default)
        /// </summary>
        public string GetEffectiveIconInterstitialDescription(CrossPromoTable table)
        {
            return !string.IsNullOrEmpty(customIconInterstitialDescription) ? 
                customIconInterstitialDescription : 
                (table?.defaultIconInterstitialDescription ?? "Try this awesome game!");
        }
        
        /// <summary>
        /// Gets the effective rich interstitial button text (custom override or global default)
        /// </summary>
        public string GetEffectiveRichInterstitialButtonText(CrossPromoTable table)
        {
            return !string.IsNullOrEmpty(customRichInterstitialButtonText) ? 
                customRichInterstitialButtonText : 
                (table?.defaultRichInterstitialButtonText ?? "Play Now!");
        }
        
        /// <summary>
        /// Gets the effective rich interstitial description (custom override or global default)
        /// </summary>
        public string GetEffectiveRichInterstitialDescription(CrossPromoTable table)
        {
            return !string.IsNullOrEmpty(customRichInterstitialDescription) ? 
                customRichInterstitialDescription : 
                (table?.defaultRichInterstitialDescription ?? "Join millions of players in this amazing adventure!");
        }
    }

    /// <summary>
    /// Algorithm for selecting which game to promote
    /// </summary>
    public enum RotationType
    {
        Waterfall,
        WeightedRandom
    }
} 