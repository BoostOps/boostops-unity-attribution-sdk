using System.Collections.Generic;

namespace BoostOps
{
    /// <summary>
    /// Public campaign information for external developers
    /// Clean, simple data structure without internal implementation details
    /// </summary>
    public class CampaignInfo
    {
        /// <summary>
        /// Unique campaign identifier
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Campaign display name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// App name being promoted
        /// </summary>
        public string AppName { get; set; }
        
        /// <summary>
        /// Short description of the promoted app
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// App icon URL
        /// </summary>
        public string IconUrl { get; set; }
        
        /// <summary>
        /// Hero/screenshot image URL
        /// </summary>
        public string HeroImageUrl { get; set; }
        
        /// <summary>
        /// App Store URL for iOS
        /// </summary>
        public string IosUrl { get; set; }
        
        /// <summary>
        /// Google Play Store URL for Android
        /// </summary>
        public string AndroidUrl { get; set; }
        
        /// <summary>
        /// Campaign priority (higher = more likely to show)
        /// </summary>
        public int Priority { get; set; }
        
        /// <summary>
        /// Whether this campaign is currently active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Campaign type (e.g., "cross_promo", "house_ad")
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Custom metadata for targeting or analytics
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Create CampaignInfo from internal Campaign object
        /// </summary>
        public static CampaignInfo FromInternalCampaign(object internalCampaign)
        {
            if (internalCampaign == null) return null;
            
            // Use reflection to safely extract data from internal Campaign
            var campaignType = internalCampaign.GetType();
            
            try
            {
                var campaignInfo = new CampaignInfo();
                
                // Extract basic campaign data
                campaignInfo.Id = GetFieldValue<string>(internalCampaign, "campaign_id") ?? "";
                campaignInfo.Name = GetFieldValue<string>(internalCampaign, "name") ?? "";
                campaignInfo.IsActive = GetFieldValue<string>(internalCampaign, "status") == "active";
                campaignInfo.Type = "cross_promo";
                
                // Extract target project data for app info
                var targetProject = GetFieldValue<object>(internalCampaign, "target_project");
                if (targetProject != null)
                {
                    campaignInfo.AppName = GetFieldValue<string>(targetProject, "name") ?? "";
                    campaignInfo.Description = GetFieldValue<string>(targetProject, "description") ?? "";
                    campaignInfo.IconUrl = GetFieldValue<string>(targetProject, "icon_url") ?? "";
                    campaignInfo.HeroImageUrl = GetFieldValue<string>(targetProject, "hero_image_url") ?? "";
                    campaignInfo.IosUrl = GetFieldValue<string>(targetProject, "ios_url") ?? "";
                    campaignInfo.AndroidUrl = GetFieldValue<string>(targetProject, "android_url") ?? "";
                    
                    // Priority from target project
                    campaignInfo.Priority = GetFieldValue<int>(targetProject, "priority");
                }
                
                return campaignInfo;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CampaignInfo] Failed to convert internal campaign: {ex.Message}");
                return new CampaignInfo
                {
                    Id = "unknown",
                    Name = "Unknown Campaign",
                    AppName = "Unknown App",
                    IsActive = false,
                    Type = "cross_promo"
                };
            }
        }
        
        /// <summary>
        /// Helper method to safely extract field values using reflection
        /// </summary>
        private static T GetFieldValue<T>(object obj, string fieldName)
        {
            if (obj == null) return default(T);
            
            var field = obj.GetType().GetField(fieldName);
            if (field != null)
            {
                var value = field.GetValue(obj);
                if (value is T) return (T)value;
                if (value != null && typeof(T) == typeof(string)) return (T)(object)value.ToString();
            }
            
            return default(T);
        }
    }
}
