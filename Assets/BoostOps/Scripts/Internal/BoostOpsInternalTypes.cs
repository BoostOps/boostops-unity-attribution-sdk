using System;
using System.Collections.Generic;

namespace BoostOps.Internal
{
    /// <summary>
    /// Internal version of PromoFormat for DLL usage
    /// </summary>
    public enum PromoFormat 
    {
        Auto,             // Smart selection (server or client logic chooses best)
        Banner,           // Small banner overlay
        Native,           // Custom integrated display  
        Icon,             // Simple popup with app icon
        Rich              // Full-screen rich interstitial (default for Auto)
    }
    
    /// <summary>
    /// Internal version of PromoOptions for DLL usage
    /// </summary>
    public class PromoOptions
    {
        public int MaxRetries { get; set; } = 1;         // Retry failed requests
        public bool AllowCaching { get; set; } = true;   // Use cached campaigns
        public Dictionary<string, string> CustomData { get; set; }  // Extra targeting data
    }
    
    /// <summary>
    /// Internal version of InitResult for DLL usage
    /// </summary>
    public class InitResult
    {
        public bool Success { get; set; }                    // True if initialization completed successfully
        public string Mode { get; set; }                     // "Online", "LocalOnly", or "Offline"
        public int CampaignCount { get; set; }               // Number of campaigns loaded
        public string ErrorMessage { get; set; }             // Error details if Success = false
    }
    
    /// <summary>
    /// Internal version of InitError for DLL usage
    /// </summary>
    public class InitError
    {
        public string Message { get; set; }                  // Human-readable error description
        public string Code { get; set; }                     // Error code for programmatic handling
        public Exception InnerException { get; set; }        // Original exception if available
    }
    
    /// <summary>
    /// Internal interface for SourceProject functionality
    /// </summary>
    public interface ISourceProject
    {
        string DefaultIconInterstitialDescription { get; }
        string DefaultIconInterstitialButtonText { get; }
        string DefaultRichInterstitialDescription { get; }
        string DefaultRichInterstitialButtonText { get; }
        string ProjectName { get; }
        string ProjectId { get; }
    }
    
    /// <summary>
    /// Internal project settings data for DLL usage
    /// Provides access to project settings without direct dependency on BoostOpsProjectSettings
    /// </summary>
    public class InternalProjectSettings
    {
        public string ProjectId { get; set; } = "";  // The actual project ID from BoostOps backend
        public string ProjectKey { get; set; } = "";
        public bool UseRemoteManagement { get; set; } = false;
        // BoostOps Analytics derived from UseRemoteManagement (enabled when using remote management)
        public bool BoostOpsAnalytics => UseRemoteManagement;
        public string IngestUrl { get; set; } = "https://analytics.boostops.io/v1";
        public bool FirebaseAnalytics { get; set; } = false;
        public bool UnityAnalytics { get; set; } = false;
        public string AppleAppStoreId { get; set; } = "";
        public string AndroidPackageName { get; set; } = "";
        public string AmazonStoreId { get; set; } = "";
        public string MicrosoftStoreId { get; set; } = "";
        public string SamsungStoreId { get; set; } = "";
        
        /// <summary>
        /// Create from external project settings (called by public API)
        /// </summary>
        public static InternalProjectSettings FromExternal(object externalSettings)
        {
            if (externalSettings == null) return new InternalProjectSettings();
            
            try
            {
                var settingsType = externalSettings.GetType();
                var result = new InternalProjectSettings();
                
                UnityEngine.Debug.Log($"[InternalProjectSettings] Extracting from type: {settingsType.Name}");
                
                // Use reflection to safely extract data
                var projectIdField = settingsType.GetField("projectId");
                if (projectIdField != null)
                {
                    var projectIdValue = projectIdField.GetValue(externalSettings) as string ?? "";
                    result.ProjectId = projectIdValue;
                    UnityEngine.Debug.Log($"[InternalProjectSettings] Found projectId field, value: '{projectIdValue}'");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[InternalProjectSettings] projectId field NOT FOUND on {settingsType.Name}");
                }
                
                var projectKeyField = settingsType.GetField("projectKey");
                if (projectKeyField != null)
                {
                    var projectKeyValue = projectKeyField.GetValue(externalSettings) as string ?? "";
                    result.ProjectKey = projectKeyValue;
                    UnityEngine.Debug.Log($"[InternalProjectSettings] Found projectKey field, value length: {projectKeyValue.Length}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[InternalProjectSettings] projectKey field NOT FOUND on {settingsType.Name}");
                }
                
                var useRemoteManagementField = settingsType.GetField("useRemoteManagement");
                if (useRemoteManagementField != null)
                    result.UseRemoteManagement = (bool)(useRemoteManagementField.GetValue(externalSettings) ?? false);
                
                // BoostOps Analytics is now derived from UseRemoteManagement - no separate field needed
                
                var ingestUrlField = settingsType.GetField("ingestUrl");
                if (ingestUrlField != null)
                    result.IngestUrl = ingestUrlField.GetValue(externalSettings) as string ?? "https://analytics.boostops.io/v1";
                
                var firebaseAnalyticsField = settingsType.GetField("firebaseAnalytics");
                if (firebaseAnalyticsField != null)
                    result.FirebaseAnalytics = (bool)(firebaseAnalyticsField.GetValue(externalSettings) ?? false);
                
                var unityAnalyticsField = settingsType.GetField("unityAnalytics");
                if (unityAnalyticsField != null)
                    result.UnityAnalytics = (bool)(unityAnalyticsField.GetValue(externalSettings) ?? false);
                
                var appleAppStoreIdField = settingsType.GetField("appleAppStoreId");
                if (appleAppStoreIdField != null)
                    result.AppleAppStoreId = appleAppStoreIdField.GetValue(externalSettings) as string ?? "";
                
                var androidPackageNameField = settingsType.GetField("androidPackageName");
                if (androidPackageNameField != null)
                    result.AndroidPackageName = androidPackageNameField.GetValue(externalSettings) as string ?? "";
                
                var amazonStoreIdField = settingsType.GetField("amazonStoreId");
                if (amazonStoreIdField != null)
                    result.AmazonStoreId = amazonStoreIdField.GetValue(externalSettings) as string ?? "";
                
                var microsoftStoreIdField = settingsType.GetField("microsoftStoreId");
                if (microsoftStoreIdField != null)
                    result.MicrosoftStoreId = microsoftStoreIdField.GetValue(externalSettings) as string ?? "";
                
                var samsungStoreIdField = settingsType.GetField("samsungStoreId");
                if (samsungStoreIdField != null)
                    result.SamsungStoreId = samsungStoreIdField.GetValue(externalSettings) as string ?? "";
                
                return result;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[InternalProjectSettings] Failed to extract settings: {ex.Message}");
                return new InternalProjectSettings();
            }
        }
    }
    
    /// <summary>
    /// Static cache for project settings provided by the public SDK layer
    /// </summary>
    public static class InternalSettingsCache
    {
        private static InternalProjectSettings _cachedSettings = null;
        
        /// <summary>
        /// Set project settings from the public SDK layer (called during initialization)
        /// </summary>
        public static void SetProjectSettings(InternalProjectSettings settings)
        {
            _cachedSettings = settings;
            UnityEngine.Debug.Log($"[InternalSettingsCache] ✅ Project settings cached - ProjectKey: '{settings?.ProjectKey}' (length: {settings?.ProjectKey?.Length ?? 0})");
        }
        
        /// <summary>
        /// Check if project settings have been cached (without triggering warnings)
        /// </summary>
        public static bool HasCachedSettings()
        {
            return _cachedSettings != null;
        }
        
        /// <summary>
        /// Get cached project settings for internal use
        /// </summary>
        public static InternalProjectSettings GetProjectSettings()
        {
            if (_cachedSettings == null)
            {
                UnityEngine.Debug.LogWarning("[InternalSettingsCache] ⚠️ Project settings not yet cached - returning default values");
                return new InternalProjectSettings();
            }
            
            return _cachedSettings;
        }
    }
}
