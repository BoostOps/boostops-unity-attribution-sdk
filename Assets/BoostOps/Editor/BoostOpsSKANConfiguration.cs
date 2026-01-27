using System.Collections.Generic;
using System.Linq;

namespace BoostOps.Editor
{
    /// <summary>
    /// Centralized configuration for SKAdNetwork (SKAN) identifiers
    /// This ensures consistent SKAN configuration across all BoostOps-enabled apps
    /// 
    /// Based on Google's official SKAdNetwork ID list for App Campaigns
    /// Source: https://developers.google.com/app-campaigns/ios/skadnetwork-ids
    /// 
    /// Why this works for Google + Meta:
    /// 1. Apple Ads registers itself automatically (iOS 14.5+)
    /// 2. Meta's IDs are included in Google's official list
    /// 3. Google requires ALL partner DSP IDs to prevent attribution loss
    /// </summary>
    public static class BoostOpsSKANConfiguration
    {
        /// <summary>
        /// BoostOps attribution endpoint for first-party SKAN postbacks
        /// Apple will send attribution data here for direct analysis
        /// </summary>
        public const string BOOSTOPS_ATTRIBUTION_ENDPOINT = "https://boostops-skan.com";

        /// <summary>
        /// Core Google SKAdNetwork ID - REQUIRED for Google App Campaigns
        /// </summary>
        public const string GOOGLE_CORE_ID = "cstr6suwn9.skadnetwork";

        /// <summary>
        /// Meta (Facebook) SKAdNetwork IDs - REQUIRED for Facebook campaigns
        /// These are also included in Google's official list
        /// </summary>
        public static readonly string[] META_FACEBOOK_IDS = {
            "v9wttpbfk9.skadnetwork",  // Meta primary ID
            "n38lu8286q.skadnetwork"   // Meta secondary ID
        };

        /// <summary>
        /// Complete SKAdNetwork ID list for maximum attribution coverage
        /// 
        /// This includes:
        /// - Google's core ID
        /// - All Google App Campaign partner DSPs
        /// - Meta/Facebook IDs (already in Google list, listed for clarity)
        /// - AppLovin ID (useful for MAX testing)
        /// 
        /// Updated from Google's official list: https://developers.google.com/app-campaigns/ios/skadnetwork-ids
        /// Last updated: Based on user requirements (Dec 2024)
        /// 
        /// Note: Apple Ads (Search Ads) registers automatically as of iOS 14.5+
        /// No need to include Apple's SKAdNetwork ID manually
        /// </summary>
        public static readonly string[] ALL_SKADNETWORK_IDS = {
            // Google core ID (REQUIRED)
            "cstr6suwn9.skadnetwork",

            // Third-party DSPs that can win Google App Campaign auctions
            // Missing any of these can cause attribution loss
            "4fzdc2evr5.skadnetwork",
            "2fnua5tdw4.skadnetwork", 
            "ydx93a7ass.skadnetwork",
            "p78axxw29g.skadnetwork",
            "v72qych5uu.skadnetwork",
            "cp8zw746q7.skadnetwork",
            
            // AppLovin (useful for MAX mediation testing)
            "ludvb6z3bs.skadnetwork",
            
            // Meta / Facebook Audience Network IDs (already in Google's list)
            "v9wttpbfk9.skadnetwork",  // Meta primary
            "n38lu8286q.skadnetwork",  // Meta secondary
            
            // Additional major DSPs from Google's official list
            "4468km3ulz.skadnetwork",
            "t38b2kh725.skadnetwork",
            "7ug5zh24hu.skadnetwork",
            "9rd848q2bz.skadnetwork",
            "n6fk4nfna4.skadnetwork",
            "7rz58n8ntl.skadnetwork",
            "ejvt5qm6ak.skadnetwork",
            "5lm9lj6jb7.skadnetwork",
            "44jx6755aq.skadnetwork",
            "tl55sbb4fm.skadnetwork",
            "2u9pt9hc89.skadnetwork",
            "8s468mfl3y.skadnetwork",
            "av6w8kgt66.skadnetwork",
            "klf5c3l5u5.skadnetwork",
            "ppxm28t8ap.skadnetwork",
            "424m5254lk.skadnetwork",
            "uw77j35x4d.skadnetwork",
            "578prtvx9j.skadnetwork",
            "4dzt52r2t5.skadnetwork",
            "e5fvkxwrpn.skadnetwork",
            "8c4e2ghe7u.skadnetwork",
            "zq492l623r.skadnetwork",
            "3rd42ekr43.skadnetwork",
            "3s53sq2bgm.skadnetwork",
            "f38h382jlk.skadnetwork",
            "hs6bdukanm.skadnetwork",
            "prcb7njmu6.skadnetwork",
            "vzv2zcsg8b.skadnetwork",
            "9nlqeag3gk.skadnetwork",
            "275upjj5gd.skadnetwork",
            "wg4vff78zm.skadnetwork",
            "g28c52eehv.skadnetwork",
            "cg4emx4h2s.skadnetwork",
            "294l99pt4k.skadnetwork",
            "mtkv5xtk9e.skadnetwork",
            "gvmwg8q7h5.skadnetwork",
            "n9x2a789qt.skadnetwork",
            "6g9af3uyq4.skadnetwork",
            "w9q455wk68.skadnetwork"
        };

        /// <summary>
        /// Validate that critical SKAdNetwork IDs are present
        /// Returns validation results for build-time checking
        /// </summary>
        public static SKANValidationResult ValidateConfiguration(string[] configuredIds)
        {
            var result = new SKANValidationResult();
            var configuredSet = new HashSet<string>(configuredIds ?? new string[0]);

            // Check Google core ID (critical)
            result.HasGoogleCoreId = configuredSet.Contains(GOOGLE_CORE_ID);
            
            // Check Meta IDs (critical for Facebook campaigns)
            result.HasMetaIds = META_FACEBOOK_IDS.All(id => configuredSet.Contains(id));
            
            // Calculate coverage percentage
            int presentCount = ALL_SKADNETWORK_IDS.Count(id => configuredSet.Contains(id));
            result.CoveragePercentage = (float)presentCount / ALL_SKADNETWORK_IDS.Length * 100f;
            
            // Find missing critical IDs
            result.MissingCriticalIds = new List<string>();
            if (!result.HasGoogleCoreId)
                result.MissingCriticalIds.Add(GOOGLE_CORE_ID);
            
            foreach (var metaId in META_FACEBOOK_IDS)
            {
                if (!configuredSet.Contains(metaId))
                    result.MissingCriticalIds.Add(metaId);
            }
            
            result.IsValid = result.HasGoogleCoreId && result.HasMetaIds;
            
            return result;
        }
    }

    /// <summary>
    /// Result of SKAdNetwork configuration validation
    /// </summary>
    public class SKANValidationResult
    {
        public bool IsValid { get; set; }
        public bool HasGoogleCoreId { get; set; }
        public bool HasMetaIds { get; set; }
        public float CoveragePercentage { get; set; }
        public List<string> MissingCriticalIds { get; set; } = new List<string>();

        public string GetSummary()
        {
            if (IsValid)
            {
                return $"✅ SKAN configuration valid - {CoveragePercentage:F1}% coverage";
            }
            else
            {
                return $"❌ SKAN configuration incomplete - Missing: {string.Join(", ", MissingCriticalIds)}";
            }
        }
    }
}