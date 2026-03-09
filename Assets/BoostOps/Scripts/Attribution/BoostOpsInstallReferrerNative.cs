using UnityEngine;
using System;
using System.Collections.Generic;

namespace BoostOps
{
    /// <summary>
    /// Unity wrapper for Android Install Referrer API integration
    /// Provides automatic install referrer tracking for accurate attribution
    /// Critical component missing from Unity's native deep link handling
    /// </summary>
    public class BoostOpsInstallReferrerNative : MonoBehaviour
    {
        [Header("Install Referrer Configuration")]
        [SerializeField] private bool enableDebugLogs = true;
#pragma warning disable 0414 // Field assigned but never used (used in platform-specific code)
        [SerializeField] private bool enableInstallReferrerTracking = true;
#pragma warning restore 0414
        
        // Singleton access
        public static BoostOpsInstallReferrerNative Instance { get; private set; }
        
        // --- Events ---
        
        /// <summary>
        /// Fired when install referrer data is successfully retrieved
        /// </summary>
        public static event Action<InstallReferrerData> OnInstallReferrerReceived;
        
        /// <summary>
        /// Fired when an error occurs retrieving install referrer data
        /// </summary>
        public static event Action<string> OnInstallReferrerError;
        
        // Internal state
        private bool isInitialized = false;
        private string apiKey;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject androidInstallReferrer;
#endif
        
        // Properties
        public bool IsInitialized => isInitialized;
        public bool IsAndroidPlatform => Application.platform == RuntimePlatform.Android;
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // Set GameObject name for native callbacks
                gameObject.name = "BoostOpsInstallReferrerNative";
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Initialize install referrer tracking
        /// Should be called early in app startup
        /// </summary>
        public void Initialize(string apiKey)
        {
            if (isInitialized)
            {
                LogDebug("Install referrer tracking already initialized");
                return;
            }
            
            if (string.IsNullOrEmpty(apiKey))
            {
                LogError("API key cannot be null or empty for install referrer tracking");
                return;
            }
            
            this.apiKey = apiKey;
            
            try
            {
                LogDebug("Initializing install referrer tracking...");
                
#if UNITY_ANDROID && !UNITY_EDITOR
                InitializeAndroid();
#elif UNITY_IOS && !UNITY_EDITOR
                // iOS doesn't have install referrer API - attribution comes via deep links
                LogDebug("iOS platform detected - install referrer API not available, using deep links only");
                isInitialized = true;
#else
                // Editor or other platforms
                LogDebug("Editor/unsupported platform - install referrer tracking disabled");
                isInitialized = true;
#endif
            }
            catch (Exception e)
            {
                LogError($"Failed to initialize install referrer tracking: {e.Message}");
                OnInstallReferrerError?.Invoke(e.Message);
            }
        }
        
#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Initialize Android install referrer tracking
        /// </summary>
        private void InitializeAndroid()
        {
            try
            {
                if (!enableInstallReferrerTracking)
                {
                    LogDebug("Install referrer tracking disabled in configuration");
                    return;
                }
                
                // Get the native install referrer class
                using (AndroidJavaClass referrerClass = new AndroidJavaClass("com.boostops.unity.referrer.BoostOpsInstallReferrerNative"))
                {
                    if (referrerClass == null)
                    {
                        LogError("Native install referrer class not found - ensure BoostOpsInstallReferrerNative.java is included");
                        return;
                    }
                    
                    // Get singleton instance
                    androidInstallReferrer = referrerClass.CallStatic<AndroidJavaObject>("getInstance");
                    if (androidInstallReferrer == null)
                    {
                        LogError("Failed to get install referrer native instance");
                        return;
                    }
                    
                    // Initialize with API key
                    androidInstallReferrer.Call("initialize", apiKey);
                    
                    isInitialized = true;
                    LogDebug("Android install referrer tracking initialized successfully");
                }
            }
            catch (AndroidJavaException e)
            {
                LogError($"Android Java exception during install referrer initialization: {e.Message}");
                OnInstallReferrerError?.Invoke($"Android error: {e.Message}");
            }
            catch (Exception e)
            {
                LogError($"Error initializing Android install referrer: {e.Message}");
                OnInstallReferrerError?.Invoke(e.Message);
            }
        }
#endif
        
        /// <summary>
        /// Check if install referrer has been processed
        /// </summary>
        public bool HasProcessedInstallReferrer()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (androidInstallReferrer != null)
                {
                    return androidInstallReferrer.Call<bool>("hasProcessedInstallReferrer");
                }
            }
            catch (Exception e)
            {
                LogError($"Error checking install referrer status: {e.Message}");
            }
#endif
            return false;
        }
        
        /// <summary>
        /// Manually trigger install referrer query (for testing)
        /// </summary>
        public void QueryInstallReferrerManually()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (androidInstallReferrer != null)
                {
                    androidInstallReferrer.Call("queryInstallReferrerManually");
                    LogDebug("Manual install referrer query triggered");
                }
            }
            catch (Exception e)
            {
                LogError($"Error querying install referrer manually: {e.Message}");
            }
#else
            LogDebug("Manual install referrer query only available on Android");
#endif
        }
        
        /// <summary>
        /// Native callback when install referrer is received (called from Java)
        /// This method name must match UNITY_CALLBACK_METHOD in Java
        /// </summary>
        public void OnInstallReferrerReceivedCallback(string jsonData)
        {
            try
            {
                LogDebug($"Install referrer received: {jsonData}");
                
                if (string.IsNullOrEmpty(jsonData))
                {
                    LogError("Install referrer data is empty");
                    return;
                }
                
                // Parse the JSON data
                var referrerData = ParseInstallReferrerData(jsonData);
                if (referrerData != null)
                {
                    // Fire event
                    OnInstallReferrerReceived?.Invoke(referrerData);
                    
                    // Integrate with existing attribution system
                    IntegrateWithAttributionSystem(referrerData);
                    
                    LogDebug($"Install referrer processed: Campaign={referrerData.CampaignId}, Source={referrerData.UtmSource}");
                }
            }
            catch (Exception e)
            {
                LogError($"Error processing install referrer callback: {e.Message}");
                OnInstallReferrerError?.Invoke(e.Message);
            }
        }
        
        /// <summary>
        /// Parse install referrer JSON data
        /// </summary>
        private InstallReferrerData ParseInstallReferrerData(string jsonData)
        {
            try
            {
                // Using Unity's JsonUtility for simple parsing
                var jsonObj = JsonUtility.FromJson<InstallReferrerJsonData>(jsonData);
                
                return new InstallReferrerData
                {
                    RawReferrer = jsonObj.raw_referrer,
                    ClickTimestamp = DateTimeOffset.FromUnixTimeSeconds(jsonObj.click_timestamp).DateTime,
                    InstallTimestamp = DateTimeOffset.FromUnixTimeSeconds(jsonObj.install_timestamp).DateTime,
                    InstantExperience = jsonObj.instant_experience,
                    
                    // UTM parameters
                    UtmSource = jsonObj.utm_source,
                    UtmMedium = jsonObj.utm_medium,
                    UtmCampaign = jsonObj.utm_campaign,
                    UtmTerm = jsonObj.utm_term,
                    UtmContent = jsonObj.utm_content,
                    
                    // BoostOps-specific parameters
                    CampaignId = jsonObj.campaign_id,
                    SourceAppId = jsonObj.source_app_id,
                    BoostReferrer = jsonObj.boost_referrer,
                    
                    // Metadata
                    AttributionSource = "install_referrer",
                    SdkVersion = jsonObj.sdk_version,
                    ProcessedTimestamp = DateTime.UtcNow
                };
            }
            catch (Exception e)
            {
                LogError($"Error parsing install referrer data: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Integrate with existing BoostOps attribution system
        /// </summary>
        private void IntegrateWithAttributionSystem(InstallReferrerData referrerData)
        {
            try
            {
                // Attribution integration requires SDK setup
                LogDebug("Install referrer attribution integration requires SDK setup");
                
                // Send analytics event about successful attribution
                LogDebug($"Install referrer attribution successful: Source={referrerData.UtmSource}, Campaign={referrerData.UtmCampaign}");
            }
            catch (Exception e)
            {
                LogError($"Error integrating with attribution system: {e.Message}");
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[BoostOps Install Referrer] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[BoostOps Install Referrer] {message}");
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[BoostOps Install Referrer] {message}");
        }
        
        void OnDestroy()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                androidInstallReferrer?.Dispose();
#endif
            }
            catch (Exception e)
            {
                LogError($"Error during cleanup: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Data structure for install referrer information
    /// </summary>
    [Serializable]
    public class InstallReferrerData
    {
        // Raw data
        public string RawReferrer;
        public DateTime ClickTimestamp;
        public DateTime InstallTimestamp;
        public bool InstantExperience;
        
        // UTM parameters
        public string UtmSource;
        public string UtmMedium;
        public string UtmCampaign;
        public string UtmTerm;
        public string UtmContent;
        
        // BoostOps-specific parameters
        public string CampaignId;
        public string SourceAppId;
        public string BoostReferrer;
        
        // Metadata
        public string AttributionSource;
        public string SdkVersion;
        public DateTime ProcessedTimestamp;
    }
    
    /// <summary>
    /// Internal JSON data structure for parsing native responses
    /// </summary>
    [Serializable]
    internal class InstallReferrerJsonData
    {
        public string raw_referrer;
        public long click_timestamp;
        public long install_timestamp;
        public bool instant_experience;
        
        public string utm_source;
        public string utm_medium;
        public string utm_campaign;
        public string utm_term;
        public string utm_content;
        
        public string campaign_id;
        public string source_app_id;
        public string boost_referrer;
        
        public string attribution_source;
        public string sdk_version;
        public long timestamp;
    }
} 