using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Networking;
using BoostOps.Analytics;

namespace BoostOps
{
    /// <summary>
    /// BoostOps Dynamic Links - Firebase Dynamic Links replacement for cross-promotion
    /// Provides smart app store routing, deferred deep linking, and attribution tracking
    /// Compatible with BoostOps campaigns and analytics
    /// </summary>
    public class BoostOpsDynamicLinks : MonoBehaviour
    {
        [Header("Dynamic Links Configuration")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // Singleton access
        public static BoostOpsDynamicLinks Instance { get; private set; }
        
        // --- Events ---
        
        /// <summary>
        /// Fired when a dynamic link is received and parsed
        /// </summary>
        public static event Action<DynamicLinkInfo> OnDynamicLinkReceived;
        
        /// <summary>
        /// Fired when an error occurs processing dynamic links
        /// </summary>
        public static event Action<string> OnDynamicLinkError;
        
        /// <summary>
        /// Fired when a dynamic link is clicked
        /// </summary>
        public static event Action<DynamicLinkClickData> OnLinkClicked;
        
        /// <summary>
        /// Fired when install attribution data is received from dynamic links
        /// </summary>
        public static event Action<DynamicLinkAttributionData> OnInstallAttribution;
        
        // Internal state
        private bool isInitialized = false;
        private Dictionary<string, DynamicLinkInfo> linkCache = new Dictionary<string, DynamicLinkInfo>();
        private BoostOpsProjectSettings dynamicLinksConfig = null;
        private string[] configuredDomains = null;
        
        // Properties
        public bool IsInitialized => isInitialized;
        
        /// <summary>
        /// Get all configured domains for this project
        /// </summary>
        public string[] ConfiguredDomains 
        { 
            get 
            { 
                LoadConfigurationIfNeeded();
                return configuredDomains ?? new string[0]; 
            } 
        }
        
        /// <summary>
        /// Get the primary configured domain
        /// </summary>
        public string PrimaryDomain 
        { 
            get 
            { 
                var domains = ConfiguredDomains;
                return domains.Length > 0 ? domains[0] : "";
            } 
        }
        
        void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                _ = InitializeAsync();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {
            // Check for incoming dynamic links when app starts
            CheckForIncomingLinks();
            
            // Also do a delayed check for Application.absoluteURL
            // On Android, this might be set after Awake() completes
            StartCoroutine(DelayedDeepLinkCheck());
        }
        
        private IEnumerator DelayedDeepLinkCheck()
        {
            // Wait a few frames to ensure Unity has fully initialized
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            
            // Check again if absoluteURL was set
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                LogDebug($"[Delayed Check] Found deep link in Application.absoluteURL: {Application.absoluteURL}");
                HandleIncomingLink(Application.absoluteURL);
            }
        }
        
        /// <summary>
        /// Initialize the Dynamic Links system
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // LogDebug("BoostOps Dynamic Links initializing...");
                
                // Load configuration
                LoadConfigurationIfNeeded();
                
                // Set up deep link handling for different platforms
                SetupPlatformSpecificHandling();
                
                // Check for deferred deep link attribution
                CheckDeferredAttribution();
                
                isInitialized = true;
                
                // if (configuredDomains != null && configuredDomains.Length > 0)
                // {
                //     LogDebug($"BoostOps Dynamic Links initialized successfully for {configuredDomains.Length} domain(s): {string.Join(", ", configuredDomains)}");
                // }
                // else
                // {
                //     LogDebug("BoostOps Dynamic Links initialized successfully (no domains configured)");
                // }
                
                await Task.CompletedTask; // Satisfy compiler warning for async method
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize BoostOps Dynamic Links: {ex.Message}");
                OnDynamicLinkError?.Invoke(ex.Message);
                return false;
            }
        }
        
        // Note: Dynamic link creation removed - links should be created server-side
        // This client only handles incoming links for attribution and deep linking
        
        /// <summary>
        /// Handle incoming dynamic link
        /// </summary>
        public void HandleIncomingLink(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            
            LogDebug($"Handling incoming dynamic link: {url}");
            
            // Validate that this URL is from a configured domain
            if (!IsConfiguredDomain(url))
            {
                LogDebug($"Dynamic link from unconfigured domain, ignoring: {url}");
                return;
            }
            
            // Track the click
            TrackLinkClick(url);
            
            // Parse the link and extract campaign information
            var linkInfo = ParseDynamicLink(url);
            if (linkInfo != null)
            {
                // NOTE: Don't send a separate app_open event here!
                // The SDK initialization already sends app_open with the deep link URL.
                // Sending another one creates duplicate events with incorrect launch_type.
                
                // Fire the event for app-level handling
                OnDynamicLinkReceived?.Invoke(linkInfo);
                
                // Handle campaign-specific logic
                HandleCampaignDeepLink(linkInfo);
            }
        }
        
        /// <summary>
        /// Track dynamic link click
        /// </summary>
        public void TrackLinkClick(string dynamicLink)
        {
            if (string.IsNullOrEmpty(dynamicLink))
                return;
            
            var clickData = new DynamicLinkClickData
            {
                DynamicLink = dynamicLink,
                Timestamp = DateTime.UtcNow,
                Platform = GetCurrentPlatform(),
                DeviceInfo = GetDeviceInfo()
            };
            
            // Note: Link click tracking handled by Analytics Contract backend integration
            
            OnLinkClicked?.Invoke(clickData);
            LogDebug($"Tracked dynamic link click: {dynamicLink}");
        }
        
        /// <summary>
        /// Track install attribution from dynamic link
        /// </summary>
        public void TrackInstallAttribution(string campaignId, string sourceAppId)
        {
            if (string.IsNullOrEmpty(campaignId))
                return;
            
            var attributionData = new DynamicLinkAttributionData
            {
                CampaignId = campaignId,
                SourceAppId = sourceAppId,
                TargetAppId = Application.identifier,
                InstallTimestamp = DateTime.UtcNow,
                AttributionSource = "dynamic_link"
            };
            
            // Note: Install attribution is handled by BoostOpsInstallAttribution system
            // The main attribution system will track this install via TrackInstallEvent(),
            // which sends app_open events with first_open=true (industry standard approach).
            
            // Note: User properties handled by Analytics Contract backend integration
            
            OnInstallAttribution?.Invoke(attributionData);
            LogDebug($"Tracked install attribution: Campaign={campaignId}, Source={sourceAppId}");
        }
        
        #region Platform-Specific Handling
        
        private void SetupPlatformSpecificHandling()
        {
            // Unity 2018.3+ provides built-in deep linking APIs for all platforms
            // No platform-specific code needed!
            
            // Subscribe to deep link events (fires when app is opened while running)
            Application.deepLinkActivated += OnDeepLinkActivated;
            
            // Subscribe to BoostOpsDeepLinkProtection events (for Android warm starts)
            // Application.deepLinkActivated doesn't fire on Android warm starts, so we use our custom polling
            BoostOps.BoostOpsDeepLinkProtection.OnDeepLinkCaptured += OnDeepLinkCapturedByProtection;
            
            // Check if app was launched with a deep link (first launch)
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                // LogDebug($"App launched with deep link: {Application.absoluteURL}");
                HandleIncomingLink(Application.absoluteURL);
            }
            
            // LogDebug("Unity native deep link handling initialized");
        }
        
        private string lastProcessedLinkUrl = null;  // Deduplicate across both event sources
        private float lastLinkProcessTime = 0f;
        private string lastProcessedWarmStartUrl = null;  // Prevent duplicate warm start events
        private bool hasProcessedInitialDeepLink = false;  // Track if we've processed the cold start deep link
        
        private void OnDeepLinkActivated(string url)
        {
            LogDebug($"Deep link activated while app running (Unity API): {url}");
            
            // Deduplicate: iOS might fire both Unity API and our protection system
            if (IsDuplicateLink(url))
            {
                LogDebug($"Skipping duplicate link processing (already handled by protection system)");
                return;
            }
            
            HandleIncomingLink(url);
        }
        
        private void OnDeepLinkCapturedByProtection(string url)
        {
            LogDebug($"Deep link captured by protection system: {url}");
            
            // Deduplicate: iOS might fire both Unity API and our protection system
            if (IsDuplicateLink(url))
            {
                LogDebug($"Skipping duplicate link processing (already handled)");
                // Still need to check if we should send app_open event
            }
            else
            {
                HandleIncomingLink(url);
            }
            
            // CRITICAL: The FIRST deep link captured is for the cold start
            // SDK initialization will include it in the cold start event
            // Only SUBSEQUENT deep links (warm starts) need their own app_open event
            if (!hasProcessedInitialDeepLink)
            {
                hasProcessedInitialDeepLink = true;
                LogDebug($"This is the initial cold start deep link - SDK will handle app_open event");
                return;
            }
            
            // This is a subsequent deep link (genuine warm start)
            LogDebug($"This is a warm start deep link - sending app_open event");
            StartCoroutine(SendAppOpenIfWarmStart(url));
        }
        
        private bool IsDuplicateLink(string url)
        {
            float currentTime = Time.realtimeSinceStartup;
            
            // Consider it a duplicate if same URL within 1 second
            if (url == lastProcessedLinkUrl && (currentTime - lastLinkProcessTime) < 1f)
            {
                return true;
            }
            
            // Update tracking
            lastProcessedLinkUrl = url;
            lastLinkProcessTime = currentTime;
            return false;
        }
        
        private System.Collections.IEnumerator SendAppOpenIfWarmStart(string url)
        {
            // CRITICAL FIX: On cold starts, deep links are captured BEFORE SDK initializes
            // We need to wait for SDK to complete cold start tracking, THEN check if we need to send warm start
            
            // Wait for SDK initialization to complete (with timeout)
            float timeout = 2f;
            float elapsed = 0f;
            while (!BoostOps.Internal.BoostOpsSDKInternal.IsSDKInitialized && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (!BoostOps.Internal.BoostOpsSDKInternal.IsSDKInitialized)
            {
                LogDebug($"SDK still not initialized after {timeout}s - this is likely a cold start, skipping event");
                yield break;
            }
            
            // Now that SDK is initialized, it's safe to check launch type
            // On cold starts: SDK already called GetLaunchType() and sent the event
            // On warm starts: We need to send the event now
            string launchType = BoostOps.Internal.BoostOpsSDKInternal.GetLaunchType();
            
            LogDebug($"SDK initialized, checked launch type: {launchType}");
            
            // If it's STILL returning "warm" after SDK init, it's a genuine warm start
            if (launchType == "warm")
            {
                // Prevent duplicate events for the same URL
                if (url == lastProcessedWarmStartUrl)
                {
                    LogDebug($"Skipping duplicate warm start event for same URL: {url}");
                    yield break;
                }
                lastProcessedWarmStartUrl = url;
                
                LogDebug($"Sending warm start app_open event with deep link");
                BoostOpsAnalyticsContract.TrackAppOpen(
                    launchType: "warm",
                    deeplinkUrl: url
                );
                
                // Record that app_open was sent to prevent duplicate from lifecycle handlers
                BoostOps.Analytics.BoostOpsAnalyticsClient.RecordAppOpenSent();
            }
            else
            {
                // This shouldn't happen - if SDK is initialized, flag should be set
                // But if it does, it means SDK just handled cold start, so skip
                LogDebug($"Launch type is '{launchType}' after SDK init - not sending event (SDK already handled it)");
            }
        }
        
        #endregion
        
        #region Link Parsing and Processing
        
        private DynamicLinkInfo ParseDynamicLink(string url)
        {
            try
            {
                // Check cache first
                if (linkCache.ContainsKey(url))
                {
                    return linkCache[url];
                }
                
                // Parse URL parameters
                var uri = new Uri(url);
                var parameters = ParseQueryString(uri.Query);
                
                var linkInfo = new DynamicLinkInfo
                {
                    OriginalUrl = url,
                    CampaignId = GetParameter(parameters, "campaign_id"),
                    CampaignName = GetParameter(parameters, "campaign_name"),
                    SourceAppId = GetParameter(parameters, "source_app"),
                    TargetAppId = GetParameter(parameters, "target_app"),
                    CustomParameters = parameters
                };
                
                return linkInfo;
            }
            catch (Exception ex)
            {
                LogError($"Failed to parse dynamic link: {ex.Message}");
                return null;
            }
        }
        
        private void HandleCampaignDeepLink(DynamicLinkInfo linkInfo)
        {
            if (string.IsNullOrEmpty(linkInfo.CampaignId))
                return;
            
            // Campaign lookup requires SDK integration
            LogDebug($"Deep link campaign processing requires SDK integration. Campaign ID: {linkInfo.CampaignId}");
            
            // Track attribution if this is a first launch
            if (IsFirstLaunch())
            {
                TrackInstallAttribution(linkInfo.CampaignId, linkInfo.SourceAppId);
            }
        }
        
        #endregion
        
        // Note: Server communication for link creation removed - links created server-side
        
        private async Task SendWebRequest(UnityWebRequest request)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }
        
        #region Helper Methods
        
        private void CheckForIncomingLinks()
        {
            // Check for any incoming links on app start
            string incomingUrl = GetIncomingUrl();
            if (!string.IsNullOrEmpty(incomingUrl))
            {
                HandleIncomingLink(incomingUrl);
            }
        }
        
        private string GetIncomingUrl()
        {
            // Unity 2018.3+ handles this automatically via Application.absoluteURL
            // No platform-specific code needed!
            return Application.absoluteURL;
        }
        
        private void CheckDeferredAttribution()
        {
            // Check if this is a first launch after install from a dynamic link
            if (IsFirstLaunch())
            {
                // LogDebug("Checking for deferred attribution...");
                
                // Check if there's stored attribution data
                string storedCampaignId = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.PENDING_ATTRIBUTION_CAMPAIGN, "");
                string storedSourceApp = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.PENDING_ATTRIBUTION_SOURCE, "");
                
                if (!string.IsNullOrEmpty(storedCampaignId))
                {
                    TrackInstallAttribution(storedCampaignId, storedSourceApp);
                    
                    // Clear the stored data
                    PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.PENDING_ATTRIBUTION_CAMPAIGN);
                    PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.PENDING_ATTRIBUTION_SOURCE);
                }
            }
        }
        
        private bool IsFirstLaunch()
        {
            return PlayerPrefs.GetInt(BoostOpsPlayerPrefsKeys.HAS_LAUNCHED_BEFORE, 0) == 0;
        }
        
        private void MarkAsLaunched()
        {
            PlayerPrefs.SetInt(BoostOpsPlayerPrefsKeys.HAS_LAUNCHED_BEFORE, 1);
        }
        
        // Note: BuildLongDynamicLink removed - not needed for link reception
        
        private Dictionary<string, string> ParseQueryString(string query)
        {
            var parameters = new Dictionary<string, string>();
            
            if (string.IsNullOrEmpty(query))
                return parameters;
            
            // Remove leading '?' if present
            if (query.StartsWith("?"))
                query = query.Substring(1);
            
            string[] pairs = query.Split('&');
            foreach (string pair in pairs)
            {
                string[] keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    string key = Uri.UnescapeDataString(keyValue[0]);
                    string value = Uri.UnescapeDataString(keyValue[1]);
                    parameters[key] = value;
                }
            }
            
            return parameters;
        }
        
        private string GetParameter(Dictionary<string, string> parameters, string key)
        {
            return parameters.ContainsKey(key) ? parameters[key] : null;
        }
        
        /// <summary>
        /// Extract link_id parameter from deep link URL
        /// </summary>
        private string ExtractLinkId(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;
            
            try
            {
                var uri = new Uri(url);
                var parameters = ParseQueryString(uri.Query);
                return GetParameter(parameters, "link_id");
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        // Note: Short code generation removed - not needed for link reception
        
        private string GetCurrentPlatform()
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_WEBGL
            return "WebGL";
#elif UNITY_STANDALONE_WIN
            return "Windows";
#elif UNITY_STANDALONE_OSX
            return "macOS";
#elif UNITY_STANDALONE_LINUX
            return "Linux";
#else
            return "Unknown";
#endif
        }
        
        private DeviceInfo GetDeviceInfo()
        {
            return new DeviceInfo
            {
                Platform = GetCurrentPlatform(),
                DeviceModel = SystemInfo.deviceModel,
                OperatingSystem = SystemInfo.operatingSystem,
                ScreenResolution = $"{Screen.width}x{Screen.height}",
                Language = Application.systemLanguage.ToString(),
                DeviceId = BoostOpsIdentifierManager.GetBoostOpsId()
            };
        }
        
        #endregion
        
        #region Configuration and Domain Management
        
        /// <summary>
        /// Load configuration if not already loaded
        /// </summary>
        private void LoadConfigurationIfNeeded()
        {
            if (configuredDomains != null)
                return; // Already loaded
            
            LoadConfiguration();
        }
        
        /// <summary>
        /// Load dynamic links configuration
        /// </summary>
        private void LoadConfiguration()
        {
            // Load from ScriptableObject configuration
            LoadFromScriptableObjectConfig();
            
            // Log configuration status
            // if (configuredDomains != null && configuredDomains.Length > 0)
            // {
            //     LogDebug($"Loaded configuration for {configuredDomains.Length} domain(s): {string.Join(", ", configuredDomains)}");
            // }
            // else
            // {
            //     LogDebug("No domains configured - dynamic links will accept any domain");
            // }
        }
        
        /// <summary>
        /// Load configuration from ScriptableObject asset
        /// </summary>
        private void LoadFromScriptableObjectConfig()
        {
            try
            {
                // Load configuration from BoostOpsProjectSettings
                var config = BoostOpsProjectSettings.GetInstance();
                
                if (config != null)
                {
                    dynamicLinksConfig = config;
                    var allHosts = config.GetAllHosts();
                    configuredDomains = allHosts.ToArray();
                    // LogDebug($"Loaded ScriptableObject configuration with {configuredDomains.Length} domains");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load ScriptableObject configuration: {ex.Message}");
            }
        }
        

        
        /// <summary>
        /// Check if a URL is from a configured domain
        /// </summary>
        public bool IsConfiguredDomain(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
            
            // If no domains configured, accept any domain (backward compatibility)
            if (configuredDomains == null || configuredDomains.Length == 0)
                return true;
            
            try
            {
                var uri = new Uri(url);
                string urlHost = uri.Host.ToLower();
                
                foreach (string configuredDomain in configuredDomains)
                {
                    if (string.IsNullOrEmpty(configuredDomain))
                        continue;
                    
                    string cleanDomain = BoostOpsProjectSettings.CleanHost(configuredDomain);
                    if (string.Equals(urlHost, cleanDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Error validating domain for URL '{url}': {ex.Message}");
                return true; // If we can't validate, allow it (fail open)
            }
        }
        
        /// <summary>
        /// Get domain validation info for debugging
        /// </summary>
        public string GetDomainValidationInfo(string url)
        {
            if (configuredDomains == null || configuredDomains.Length == 0)
                return "No domains configured - accepting all";
            
            try
            {
                var uri = new Uri(url);
                string urlHost = uri.Host.ToLower();
                return $"URL host '{urlHost}' checked against configured domains: [{string.Join(", ", configuredDomains)}]";
            }
            catch (Exception ex)
            {
                return $"Error parsing URL: {ex.Message}";
            }
        }
        
        #endregion
        
        #region Debug Logging
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[BoostOps Dynamic Links] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[BoostOps Dynamic Links] {message}");
        }
        
        #endregion
    }
    
    #region Data Models
    
    // Note: DynamicLinkBuilder removed - dynamic links should be created server-side
    
    [Serializable]
    public class DynamicLinkInfo
    {
        public string OriginalUrl;
        public string CampaignId;
        public string CampaignName;
        public string SourceAppId;
        public string TargetAppId;
        public string IOSUrl;
        public string AndroidUrl;
        public string WebUrl;
        public string SocialTitle;
        public string SocialDescription;
        public string SocialImageUrl;
        public Dictionary<string, string> CustomParameters;
    }
    
    [Serializable]
    public class DynamicLinkClickData
    {
        public string DynamicLink;
        public DateTime Timestamp;
        public string Platform;
        public DeviceInfo DeviceInfo;
    }
    
    [Serializable]
    public class DynamicLinkAttributionData
    {
        public string CampaignId;
        public string SourceAppId;
        public string TargetAppId;
        public DateTime InstallTimestamp;
        public string AttributionSource;
    }
    
    [Serializable]
    public class DeviceInfo
    {
        public string Platform;
        public string DeviceModel;
        public string OperatingSystem;
        public string ScreenResolution;
        public string Language;
        public string DeviceId; // BoostOps ID (persistent user identifier)
    }
    
    // Note: ShortLinkResponse removed - not needed for link reception
    
    #endregion
} 