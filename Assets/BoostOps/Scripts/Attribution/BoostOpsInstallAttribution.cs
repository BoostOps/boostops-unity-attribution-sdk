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
    /// Enhanced install attribution system for BoostOps
    /// Tracks installs from multiple sources including dynamic links, campaigns, and referrals
    /// Provides deferred deep linking and comprehensive analytics
    /// </summary>
    public class BoostOpsInstallAttribution : MonoBehaviour
    {
        [Header("Attribution Configuration")]
        [SerializeField] private string attributionServerUrl = "https://your-attribution-server.com/api";
        [SerializeField] private string apiKey = ""; // Your API key
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool enableOfflineMode = false;
        [SerializeField] private int attributionWindowDays = 30;
        
        [Header("Deferred Deep Linking")]
        [SerializeField] private bool enableDeferredDeepLinking = true;
        [SerializeField] private float deferredLinkTimeout = 10f;
        
        // Singleton access
        public static BoostOpsInstallAttribution Instance { get; private set; }
        
        // --- Events ---
        
        /// <summary>
        /// Fired when install attribution data is successfully determined
        /// </summary>
        public static event Action<InstallAttributionData> OnInstallAttributed;
        
        /// <summary>
        /// Fired when an error occurs during attribution tracking
        /// </summary>
        public static event Action<string> OnAttributionError;
        
        /// <summary>
        /// Fired when a deferred deep link is received after app install
        /// </summary>
        public static event Action<DeferredDeepLinkData> OnDeferredDeepLinkReceived;
        
        /// <summary>
        /// Fired when a conversion event is tracked and attributed
        /// </summary>
        public static event Action<ConversionData> OnConversionTracked;
        
        // Internal state
        private bool isInitialized = false;
        private InstallAttributionData currentAttribution;
        private Dictionary<string, object> pendingEvents = new Dictionary<string, object>();
        private List<ConversionData> conversionHistory = new List<ConversionData>();
        
        // Properties
        public bool IsInitialized => isInitialized;
        public InstallAttributionData CurrentAttribution => currentAttribution;
        public bool IsAttributedInstall => currentAttribution != null && !string.IsNullOrEmpty(currentAttribution.CampaignId);
        public bool IsFirstLaunch => PlayerPrefs.GetInt(BoostOpsPlayerPrefsKeys.HAS_LAUNCHED_BEFORE, 0) == 0;
        
        void Awake()
        {
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
            // Use the protected deep link system instead of direct Unity APIs
            SetupProtectedDeepLinkHandling();
        }
        
        /// <summary>
        /// Initialize the attribution system with install referrer support
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                LogDebug("BoostOps Install Attribution initializing...");
                
                // Initialize install referrer tracking (critical for Android attribution)
                InitializeInstallReferrerTracking();
                
                // Load stored attribution data
                LoadStoredAttribution();
                
                // Check for first launch and deferred deep linking
                if (IsFirstLaunch)
                {
                    await ProcessFirstLaunchAttribution();
                }
                
                // Set up attribution tracking
                SetupAttributionTracking();
                
                // Process any pending events
                ProcessPendingEvents();
                
                isInitialized = true;
                LogDebug("BoostOps Install Attribution initialized successfully");
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize attribution system: {ex.Message}");
                OnAttributionError?.Invoke(ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Track an install event with attribution data
        /// </summary>
        public void TrackInstallEvent(string campaignId, string sourceAppId, string attributionSource = "unknown", Dictionary<string, object> additionalData = null)
        {
            if (string.IsNullOrEmpty(campaignId))
            {
                LogError("Cannot track install: campaign ID is required");
                return;
            }
            
            var installData = new InstallAttributionData
            {
                CampaignId = campaignId,
                SourceAppId = sourceAppId,
                TargetAppId = Application.identifier,
                AttributionSource = attributionSource,
                InstallTimestamp = DateTime.UtcNow,
                DeviceInfo = GetDeviceInfo(),
                AppVersion = Application.version,
                UnityVersion = Application.unityVersion,
                IsFirstLaunch = IsFirstLaunch,
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };
            
            // Store attribution locally
            currentAttribution = installData;
            SaveAttribution(installData);
            
            // Send to server
            SendInstallEventToServer(installData);
            
            // Track in analytics
            TrackAttributionInAnalytics(installData);
            
            // Fire event
            OnInstallAttributed?.Invoke(installData);
            
            LogDebug($"Install event tracked: Campaign={campaignId}, Source={sourceAppId}, Attribution={attributionSource}");
        }
        
        /// <summary>
        /// Track a conversion event (purchase, level completion, etc.)
        /// </summary>
        public void TrackConversion(string conversionType, double value = 0, string currency = "USD", Dictionary<string, object> additionalData = null)
        {
            var conversionData = new ConversionData
            {
                ConversionType = conversionType,
                Value = value,
                Currency = currency,
                Timestamp = DateTime.UtcNow,
                AttributionData = currentAttribution,
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };
            
            // Store conversion
            conversionHistory.Add(conversionData);
            SaveConversionHistory();
            
            // Send to server
            SendConversionEventToServer(conversionData);
            
            // Track in analytics
            TrackConversionInAnalytics(conversionData);
            
            // Fire event
            OnConversionTracked?.Invoke(conversionData);
            
            LogDebug($"Conversion tracked: Type={conversionType}, Value={value}, Currency={currency}");
        }
        
        /// <summary>
        /// Get attribution data for a specific campaign
        /// </summary>
        public InstallAttributionData GetAttributionForCampaign(string campaignId)
        {
            if (currentAttribution != null && currentAttribution.CampaignId == campaignId)
            {
                return currentAttribution;
            }
            return null;
        }
        
        /// <summary>
        /// Get conversion history for current attribution
        /// </summary>
        public List<ConversionData> GetConversionHistory()
        {
            return new List<ConversionData>(conversionHistory);
        }
        
        /// <summary>
        /// Check if current install is within attribution window
        /// </summary>
        public bool IsWithinAttributionWindow()
        {
            if (currentAttribution == null) return false;
            
            var timeSinceInstall = DateTime.UtcNow - currentAttribution.InstallTimestamp;
            return timeSinceInstall.TotalDays <= attributionWindowDays;
        }
        
        /// <summary>
        /// Force refresh attribution data from server
        /// </summary>
        public async Task<bool> RefreshAttributionFromServer()
        {
            if (!isInitialized) return false;
            
            try
            {
                var deviceId = BoostOpsIdentifierManager.GetInstallId();
                var response = await GetAttributionFromServer(deviceId);
                
                if (response != null)
                {
                    currentAttribution = response;
                    SaveAttribution(response);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to refresh attribution from server: {ex.Message}");
                OnAttributionError?.Invoke(ex.Message);
            }
            
            return false;
        }
        
        #region Private Methods
        
        private void LoadStoredAttribution()
        {
            string storedData = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.ATTRIBUTION_DATA, "");
            if (!string.IsNullOrEmpty(storedData))
            {
                try
                {
                    currentAttribution = JsonUtility.FromJson<InstallAttributionData>(storedData);
                    LogDebug($"Loaded stored attribution: {currentAttribution.CampaignId}");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to load stored attribution: {ex.Message}");
                }
            }
            
            // Load conversion history
            LoadConversionHistory();
        }
        
        private void SaveAttribution(InstallAttributionData attribution)
        {
            try
            {
                string json = JsonUtility.ToJson(attribution);
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.ATTRIBUTION_DATA, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                LogError($"Failed to save attribution: {ex.Message}");
            }
        }
        
        private void LoadConversionHistory()
        {
            string historyData = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.CONVERSION_HISTORY, "");
            if (!string.IsNullOrEmpty(historyData))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ConversionHistoryWrapper>(historyData);
                    conversionHistory = wrapper.conversions ?? new List<ConversionData>();
                }
                catch (Exception ex)
                {
                    LogError($"Failed to load conversion history: {ex.Message}");
                    conversionHistory = new List<ConversionData>();
                }
            }
        }
        
        private void SaveConversionHistory()
        {
            try
            {
                var wrapper = new ConversionHistoryWrapper { conversions = conversionHistory };
                string json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.CONVERSION_HISTORY, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                LogError($"Failed to save conversion history: {ex.Message}");
            }
        }
        
        private async Task ProcessFirstLaunchAttribution()
        {
            if (!enableDeferredDeepLinking) return;
            
            LogDebug("Processing first launch attribution...");
            
            // Check for deferred deep link data
            var deferredLinkData = await GetDeferredDeepLinkData();
            if (deferredLinkData != null)
            {
                LogDebug($"Received deferred deep link: {deferredLinkData.CampaignId}");
                
                // Track the install with deferred link data
                TrackInstallEvent(
                    deferredLinkData.CampaignId,
                    deferredLinkData.SourceAppId,
                    "deferred_deep_link",
                    deferredLinkData.AdditionalData
                );
                
                // Fire deferred deep link event
                OnDeferredDeepLinkReceived?.Invoke(deferredLinkData);
            }
            
            // Mark as no longer first launch
            PlayerPrefs.SetInt(BoostOpsPlayerPrefsKeys.HAS_LAUNCHED_BEFORE, 1);
            PlayerPrefs.Save();
        }
        
        private async Task<DeferredDeepLinkData> GetDeferredDeepLinkData()
        {
            if (enableOfflineMode)
            {
                // Check for locally stored deferred link data
                return GetLocalDeferredLinkData();
            }
            
            try
            {
                var boostopsId = BoostOpsIdentifierManager.GetBoostOpsId();
                var endpoint = $"{attributionServerUrl}/deferred-link";
                
                var requestData = new
                {
                    boostops_id = boostopsId,
                    app_id = Application.identifier,
                    platform = GetCurrentPlatform(),
                    timeout = deferredLinkTimeout
                };
                
                string jsonData = JsonUtility.ToJson(requestData);
                
                using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    }
                    
                    await SendWebRequest(request);
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonUtility.FromJson<DeferredDeepLinkResponse>(request.downloadHandler.text);
                        return response.data;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to get deferred deep link data: {ex.Message}");
            }
            
            return null;
        }
        
        private DeferredDeepLinkData GetLocalDeferredLinkData()
        {
            // Check for pending attribution data stored by dynamic links
            string pendingCampaign = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.PENDING_ATTRIBUTION_CAMPAIGN, "");
            string pendingSource = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.PENDING_ATTRIBUTION_SOURCE, "");
            
            if (!string.IsNullOrEmpty(pendingCampaign))
            {
                var deferredData = new DeferredDeepLinkData
                {
                    CampaignId = pendingCampaign,
                    SourceAppId = pendingSource,
                    Timestamp = DateTime.UtcNow,
                    AdditionalData = new Dictionary<string, object>()
                };
                
                // Clear pending data
                PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.PENDING_ATTRIBUTION_CAMPAIGN);
                PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.PENDING_ATTRIBUTION_SOURCE);
                
                return deferredData;
            }
            
            return null;
        }
        
        private void SetupAttributionTracking()
        {
            // Subscribe to dynamic links events
            if (BoostOpsDynamicLinks.Instance != null)
            {
                BoostOpsDynamicLinks.OnInstallAttribution += OnDynamicLinkAttribution;
                BoostOpsDynamicLinks.OnDynamicLinkReceived += OnDynamicLinkReceived;
            }
            
            // Note: Campaign event subscription handled by public SDK layer
            // Internal attribution system doesn't need direct SDK event access
            Debug.Log("[BoostOpsInstallAttribution] Attribution system initialized - events handled by public layer");
        }
        
        private void OnDynamicLinkAttribution(DynamicLinkAttributionData attributionData)
        {
            // Convert dynamic link attribution to install attribution
            TrackInstallEvent(
                attributionData.CampaignId,
                attributionData.SourceAppId,
                "dynamic_link",
                new Dictionary<string, object>
                {
                    { "target_app_id", attributionData.TargetAppId },
                    { "attribution_source", attributionData.AttributionSource }
                }
            );
        }
        
        private void OnDynamicLinkReceived(DynamicLinkInfo linkInfo)
        {
            // Track dynamic link open event
            if (!IsFirstLaunch && !string.IsNullOrEmpty(linkInfo.CampaignId))
            {
                TrackConversion("dynamic_link_open", 0, "USD", new Dictionary<string, object>
                {
                    { "campaign_id", linkInfo.CampaignId },
                    { "source_app_id", linkInfo.SourceAppId },
                    { "original_url", linkInfo.OriginalUrl }
                });
            }
        }
        
        private void OnCampaignClick(Campaign campaign)
        {
            // Track campaign click as conversion if user is attributed
            if (IsAttributedInstall)
            {
                TrackConversion("campaign_click", 0, "USD", new Dictionary<string, object>
                {
                    { "clicked_campaign_id", campaign.campaign_id },
                    { "clicked_campaign_name", campaign.name },
                    { "placement", "unknown" }
                });
            }
        }
        
        private void OnCampaignImpression(Campaign campaign)
        {
            // Track campaign impression as conversion if user is attributed
            if (IsAttributedInstall)
            {
                TrackConversion("campaign_impression", 0, "USD", new Dictionary<string, object>
                {
                    { "impression_campaign_id", campaign.campaign_id },
                    { "impression_campaign_name", campaign.name },
                    { "placement", "unknown" }
                });
            }
        }
        
        /// <summary>
        /// Initialize install referrer tracking for Android platform
        /// Critical for accurate attribution - Unity doesn't handle this automatically
        /// </summary>
        private void InitializeInstallReferrerTracking()
        {
            try
            {
                // Create install referrer component if it doesn't exist
                var existingReferrer = FindFirstObjectByType<BoostOpsInstallReferrerNative>();
                if (existingReferrer == null)
                {
                    var referrerObject = new GameObject("BoostOpsInstallReferrerNative");
                    var referrerComponent = referrerObject.AddComponent<BoostOpsInstallReferrerNative>();
                    
                    // Subscribe to install referrer events
                    BoostOpsInstallReferrerNative.OnInstallReferrerReceived += OnInstallReferrerReceived;
                    BoostOpsInstallReferrerNative.OnInstallReferrerError += OnInstallReferrerError;
                    
                    // Initialize with API key
                    referrerComponent.Initialize(apiKey);
                    
                    LogDebug("Install referrer tracking initialized");
                }
                else
                {
                    LogDebug("Install referrer tracking already exists");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize install referrer tracking: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle install referrer data when received
        /// </summary>
        private void OnInstallReferrerReceived(InstallReferrerData referrerData)
        {
            try
            {
                LogDebug($"Install referrer received in attribution system: {referrerData.CampaignId ?? referrerData.UtmCampaign}");
                
                // The BoostOpsInstallReferrerNative already integrates with our TrackInstallEvent method
                // This callback is for additional processing if needed
                
                // Store install referrer data for analytics
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_RAW, referrerData.RawReferrer ?? "");
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_PROCESSED, DateTime.UtcNow.ToString("O"));
                
                // Save timestamps from Google Play Install Referrer API (for attribution accuracy and fraud detection)
                // These are Unix seconds from Google's servers (trusted, server-side timestamps)
                long clickTs = ((DateTimeOffset)referrerData.ClickTimestamp).ToUnixTimeSeconds();
                long installBeginTs = ((DateTimeOffset)referrerData.InstallTimestamp).ToUnixTimeSeconds();
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_CLICK_TS, clickTs.ToString());
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_INSTALL_BEGIN_TS, installBeginTs.ToString());
                
                PlayerPrefs.Save();
                
            }
            catch (Exception ex)
            {
                LogError($"Error processing install referrer: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle install referrer errors
        /// </summary>
        private void OnInstallReferrerError(string error)
        {
            LogError($"Install referrer error: {error}");
        }
        
        /// <summary>
        /// Set up protected deep link handling to avoid conflicts with other plugins
        /// </summary>
        private void SetupProtectedDeepLinkHandling()
        {
            // Register with the protected deep link system
            BoostOpsDeepLinkProtection.RegisterDeepLinkHandler(HandleProtectedDeepLink);
            
            // Check for attribution data from app launch parameters (fallback)
            CheckForIncomingAttribution();
        }
        
        /// <summary>
        /// Handle deep link from the protected system
        /// </summary>
        private void HandleProtectedDeepLink(string deepLink)
        {
            if (string.IsNullOrEmpty(deepLink))
                return;
            
            LogDebug($"Handling protected deep link: {deepLink}");
            
            try
            {
                // Parse the deep link URL
                var uri = new Uri(deepLink);
                var parameters = ParseQueryString(uri.Query);
                
                // Extract attribution data
                string campaignId = GetParameter(parameters, "campaign_id");
                string sourceAppId = GetParameter(parameters, "source_app");
                string utmCampaign = GetParameter(parameters, "utm_campaign");
                string utmSource = GetParameter(parameters, "utm_source");
                
                // Use campaign_id or fallback to UTM parameters
                if (string.IsNullOrEmpty(campaignId))
                    campaignId = utmCampaign;
                
                if (string.IsNullOrEmpty(sourceAppId))
                    sourceAppId = utmSource;
                
                // Track the attribution if we have campaign data
                if (!string.IsNullOrEmpty(campaignId))
                {
                    var additionalData = new Dictionary<string, object>
                    {
                        { "deep_link_url", deepLink },
                        { "utm_campaign", utmCampaign ?? "" },
                        { "utm_source", utmSource ?? "" },
                        { "utm_medium", GetParameter(parameters, "utm_medium") ?? "" },
                        { "utm_content", GetParameter(parameters, "utm_content") ?? "" },
                        { "utm_term", GetParameter(parameters, "utm_term") ?? "" }
                    };
                    
                    TrackInstallEvent(campaignId, sourceAppId, "deep_link", additionalData);
                }
                else
                {
                    LogDebug("No campaign ID found in deep link, skipping attribution");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to handle protected deep link: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Parse query string parameters from URL
        /// </summary>
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
        
        /// <summary>
        /// Get parameter value from dictionary
        /// </summary>
        private string GetParameter(Dictionary<string, string> parameters, string key)
        {
            return parameters.ContainsKey(key) ? parameters[key] : null;
        }
        
        private void CheckForIncomingAttribution()
        {
            // Check for attribution data from app launch parameters (fallback)
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.StartsWith("--campaign-id="))
                {
                    string campaignId = arg.Substring("--campaign-id=".Length);
                    if (!string.IsNullOrEmpty(campaignId))
                    {
                        TrackInstallEvent(campaignId, "command_line", "launch_parameter");
                    }
                }
            }
        }
        
        private void ProcessPendingEvents()
        {
            // Process any events that were queued before initialization
            if (pendingEvents.Count > 0)
            {
                LogDebug($"Processing {pendingEvents.Count} pending events");
                
                foreach (var kvp in pendingEvents)
                {
                    // Process pending event based on type
                    // This is a placeholder for more sophisticated event processing
                    LogDebug($"Processing pending event: {kvp.Key}");
                }
                
                pendingEvents.Clear();
            }
        }
        
        private async void SendInstallEventToServer(InstallAttributionData installData)
        {
            if (enableOfflineMode) return;
            
            try
            {
                var endpoint = $"{attributionServerUrl}/install";
                var requestData = new
                {
                    boostops_id = BoostOpsIdentifierManager.GetBoostOpsId(),
                    campaign_id = installData.CampaignId,
                    source_app_id = installData.SourceAppId,
                    target_app_id = installData.TargetAppId,
                    attribution_source = installData.AttributionSource,
                    install_timestamp = installData.InstallTimestamp.ToString("o"),
                    app_version = installData.AppVersion,
                    unity_version = installData.UnityVersion,
                    device_info = installData.DeviceInfo,
                    is_first_launch = installData.IsFirstLaunch,
                    additional_data = installData.AdditionalData
                };
                
                string jsonData = JsonUtility.ToJson(requestData);
                
                using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    }
                    
                    await SendWebRequest(request);
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        LogDebug("Install event sent to server successfully");
                    }
                    else
                    {
                        LogError($"Failed to send install event to server: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error sending install event to server: {ex.Message}");
            }
        }
        
        private async void SendConversionEventToServer(ConversionData conversionData)
        {
            if (enableOfflineMode) return;
            
            try
            {
                var endpoint = $"{attributionServerUrl}/conversion";
                var requestData = new
                {
                    boostops_id = BoostOpsIdentifierManager.GetBoostOpsId(),
                    conversion_type = conversionData.ConversionType,
                    value = conversionData.Value,
                    currency = conversionData.Currency,
                    timestamp = conversionData.Timestamp.ToString("o"),
                    attribution_data = conversionData.AttributionData,
                    additional_data = conversionData.AdditionalData
                };
                
                string jsonData = JsonUtility.ToJson(requestData);
                
                using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    }
                    
                    await SendWebRequest(request);
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        LogDebug("Conversion event sent to server successfully");
                    }
                    else
                    {
                        LogError($"Failed to send conversion event to server: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error sending conversion event to server: {ex.Message}");
            }
        }
        
        private async Task<InstallAttributionData> GetAttributionFromServer(string deviceId)
        {
            try
            {
                var endpoint = $"{attributionServerUrl}/attribution/{deviceId}";
                
                using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
                {
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    }
                    
                    await SendWebRequest(request);
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonUtility.FromJson<AttributionResponse>(request.downloadHandler.text);
                        return response.data;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to get attribution from server: {ex.Message}");
            }
            
            return null;
        }
        
        private void TrackAttributionInAnalytics(InstallAttributionData attribution)
        {
            // Track install attribution using Analytics Contract v1
            var attributionMethod = attribution.AttributionSource switch
            {
                "deterministic" => BoostOpsAnalyticsContract.AttributionMethod.Deterministic,
                _ => BoostOpsAnalyticsContract.AttributionMethod.Probabilistic
            };
            
            // ❌ REMOVED: Duplicate app_open call
            // The SDK already tracks app_open during initialization with first_open:true
            // Attribution data is captured via attribution_click_id in the first_open event
            // This was causing double-send of app_open events (one with first_open:true, one with first_open:false)
            
            // Note: Attribution tracking is now handled automatically:
            // 1. SDK tracks app_open with first_open:true during initialization
            // 2. Install referrer data is captured as attribution_click_id in EventContext
            // 3. Server processes attribution based on click_id match
            // 4. No need for separate attribution event
        }
        
        private void TrackConversionInAnalytics(ConversionData conversion)
        {
            // Track purchase conversion from attribution data
            if (conversion.ConversionType.ToLower().Contains("purchase") || conversion.Value > 0)
            {
                // Generate synthetic transaction ID for attribution-derived conversions
                // Format: attr_{timestamp}_{hash} to allow deduplication while marking as attributed
                var syntheticTxnId = $"attr_{conversion.Timestamp.Ticks}_{conversion.ConversionType.GetHashCode():X8}";
                
                BoostOpsAnalyticsContract.TrackPurchase(
                    amount: (decimal)conversion.Value,
                    currency: conversion.Currency,
                    productId: conversion.ConversionType,
                    transactionId: syntheticTxnId
                );
            }
            
            // Note: Non-purchase conversions handled by Analytics Contract backend integration
            // Note: Attribution context automatically included in Analytics Contract events
        }
        
        private async Task SendWebRequest(UnityWebRequest request)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }
        

        
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
                DeviceId = BoostOpsIdentifierManager.GetInstallId() // Now uses hashed identifier
            };
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[BoostOps Attribution] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[BoostOps Attribution] {message}");
        }
        
        #endregion
        
        #region Cleanup
        
        void OnDestroy()
        {
            // Unsubscribe from events
            if (BoostOpsDynamicLinks.Instance != null)
            {
                BoostOpsDynamicLinks.OnInstallAttribution -= OnDynamicLinkAttribution;
                BoostOpsDynamicLinks.OnDynamicLinkReceived -= OnDynamicLinkReceived;
            }
            
            // Note: Campaign event unsubscription handled by public SDK layer
            // Internal attribution system doesn't need direct SDK event access
            Debug.Log("[BoostOpsInstallAttribution] Attribution system cleanup - events handled by public layer");
            
            // Unregister from protected deep link system
            BoostOpsDeepLinkProtection.UnregisterDeepLinkHandler(HandleProtectedDeepLink);
        }
        
        #endregion
    }
    
    #region Data Models
    
    [Serializable]
    public class InstallAttributionData
    {
        public string CampaignId;
        public string SourceAppId;
        public string TargetAppId;
        public string AttributionSource;
        public DateTime InstallTimestamp;
        public DeviceInfo DeviceInfo;
        public string AppVersion;
        public string UnityVersion;
        public bool IsFirstLaunch;
        public Dictionary<string, object> AdditionalData;
    }
    
    [Serializable]
    public class ConversionData
    {
        public string ConversionType;
        public double Value;
        public string Currency;
        public DateTime Timestamp;
        public InstallAttributionData AttributionData;
        public Dictionary<string, object> AdditionalData;
    }
    
    [Serializable]
    public class DeferredDeepLinkData
    {
        public string CampaignId;
        public string SourceAppId;
        public DateTime Timestamp;
        public Dictionary<string, object> AdditionalData;
    }
    
    [Serializable]
    public class ConversionHistoryWrapper
    {
        public List<ConversionData> conversions;
    }
    
    [Serializable]
    public class DeferredDeepLinkResponse
    {
        public bool success;
        public DeferredDeepLinkData data;
        public string error;
    }
    
    [Serializable]
    public class AttributionResponse
    {
        public bool success;
        public InstallAttributionData data;
        public string error;
    }
    
    #endregion
} 