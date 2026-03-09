using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BoostOps;
using BoostOps.Analytics;
#if !BOOSOPS_DLL_BUILD
using Unity.Services.RemoteConfig;
#endif

namespace BoostOps.Internal
{
    /// <summary>
    /// BoostOpsManagerInternal - Contains all the sophisticated campaign logic
    /// This class will be compiled into the DLL to protect IP
    /// </summary>
    public class BoostOpsManagerInternal
    {
        #region Constructors
        
        public BoostOpsManagerInternal()
        {
            _instance = this;
        }
        
        #endregion
        
        #region Private Fields
        
        private List<Campaign> campaigns = new List<Campaign>();
        private BoostOps.Core.AppWallsConfig appWalls = null; // App walls configuration from remote config
        
        // Track impression data for click linking (campaign_id -> impression data)
        private Dictionary<string, ImpressionData> impressionTracker = new Dictionary<string, ImpressionData>();
        
        private class ImpressionData
        {
            public string ImpressionId;
            public long ImpressionTimestamp;
        }
        private bool usingCachedAppWalls = false; // Flag to track if using cached fallback
        private bool localOnlyMode = false;
        private bool isInitialized = false;
        
        #pragma warning disable CS0414 // Field is assigned but never used - reserved for future implementation
        private bool isAttemptingLazyLoad = false;
        private bool remoteCampaignsLoading = false;
        private int playerDay = 1;
        private int sessionCount = 1;
        #pragma warning restore CS0414
        
        // Event fired when campaigns are loaded from remote config
        public event Action OnCampaignsLoaded;
        
        private bool remoteCampaignsLoaded = false;
        private Dictionary<string, int> dailyImpressionCounts = new Dictionary<string, int>();
        private string environment = "production";
        private BoostOpsConfig currentConfig;
        
        // Static reference for analytics access
        private static BoostOpsManagerInternal _instance;
        
        // Configuration from public facade
        private GameObject customBannerPrefab;
        private GameObject customIconInterstitialPrefab;
        private GameObject customRichInterstitialPrefab;
        private GameObject customNativePrefab;
        private GameObject customAppWallPrefab;
        private GameObject customAppWallItemPrefab;
        private int overlaySortingOrder = 32767;
        private string amazonAssociatesTag = "";
        
        #endregion
        
        #region Public Properties
        
        public bool IsInitialized => isInitialized;
        public bool LocalOnlyMode => localOnlyMode;
        public int CampaignCount => campaigns?.Count ?? 0;
        
        /// <summary>
        /// Static instance access for mode checking and configuration
        /// </summary>
        public static BoostOpsManagerInternal Instance => _instance;
        
        /// <summary>
        /// Get the analytics config from the currently loaded campaign config
        /// </summary>
        public static AnalyticsConfig GetAnalyticsConfig()
        {
            return _instance?.currentConfig?.analytics_config;
        }
        
        #endregion
        
        #region Configuration
        
        /// <summary>
        /// Configure the internal manager with settings from the public facade
        /// </summary>
        public void ConfigureSettings(GameObject bannerPrefab, GameObject iconInterstitialPrefab, 
            GameObject richInterstitialPrefab, GameObject nativePrefab, GameObject appWallPrefab, 
            GameObject appWallItemPrefab, int sortingOrder, string associatesTag)
        {
            customBannerPrefab = bannerPrefab;
            customIconInterstitialPrefab = iconInterstitialPrefab;
            customRichInterstitialPrefab = richInterstitialPrefab;
            customNativePrefab = nativePrefab;
            customAppWallPrefab = appWallPrefab;
            customAppWallItemPrefab = appWallItemPrefab;
            overlaySortingOrder = sortingOrder;
            amazonAssociatesTag = associatesTag;
            
            
            if (customIconInterstitialPrefab != null)
            {
            }
            else
            {
                Debug.LogError($"[BoostOpsManagerInternal] ❌ CRITICAL: Icon interstitial prefab is NULL! Will fall back to CreateDefaultIconInterstitial()");
            }
        }
        
        #endregion
        
        #region Initialization
        
        public void SetLocalOnlyMode(bool localOnly)
        {
            localOnlyMode = localOnly;
        }
        
        /// <summary>
        /// Start monitoring remote config for runtime updates
        /// </summary>
        private void StartRemoteConfigMonitoring()
        {
            if (localOnlyMode)
            {
                return;
            }

            // Debug.Log("[BoostOpsManagerInternal] 🔄 Starting remote config monitoring for runtime updates...");
            
            // Monitor Firebase Remote Config (real-time listener)
            StartFirebaseConfigMonitoring();
            
            // Monitor Unity Remote Config (polling)
            StartUnityConfigMonitoring();
        }
        
        /// <summary>
        /// Handle remote config updates - reload analytics and process queued events
        /// </summary>
        private void OnRemoteConfigUpdated(string source, List<string> updatedKeys = null)
        {
            Debug.Log($"[BoostOpsManagerInternal] 📡 Remote config updated from {source}");
            
            try
            {
                bool campaignsReloaded = TryParseCachedRemoteConfig();
                
                if (campaignsReloaded)
                {
                    Debug.Log("[BoostOpsManagerInternal] ✅ Remote campaigns reloaded - analytics config updated");
                    
                    // Process any queued analytics events with new config
                    var analyticsProvider = AnalyticsProviderFactory.GetProvider("BoostOps Analytics") as BoostOpsAnalyticsProvider;
                    analyticsProvider?.Initialize();
                    
                    // Process queued analytics events now that remote config is available
                    BoostOpsSDKInternal.ProcessQueuedAnalyticsEvents();
                    
                    OnCampaignsLoaded?.Invoke();
                }
                else
                {
                    Debug.LogWarning("[BoostOpsManagerInternal] ⚠️ Failed to reload campaigns after config update");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] ❌ Error handling remote config update: {ex.Message}");
            }
        }
        
        #if FIREBASE_REMOTE_CONFIG
        /// <summary>
        /// Start Firebase Remote Config real-time monitoring
        /// </summary>
        private void StartFirebaseConfigMonitoring()
        {
            try
            {
                Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.AddOnConfigUpdateListener((configUpdate) => {
                    try 
                    {
                        if (configUpdate?.UpdatedKeys?.Count > 0)
                        {
                            var updatedKeys = new List<string>(configUpdate.UpdatedKeys);
                            
                            // Check if our config key was updated
                            if (updatedKeys.Contains("boostops_config"))
                            {
                                Debug.Log("[BoostOpsManagerInternal] 🔥 Firebase: boostops_config updated - activating...");
                                
                                // Activate the updated config
                                Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(task => {
                                    if (task.IsCompletedSuccessfully)
                                    {
                                        OnRemoteConfigUpdated("Firebase Remote Config", updatedKeys);
                                    }
                                    else
                                    {
                                        Debug.LogError($"[BoostOpsManagerInternal] Failed to activate Firebase config: {task.Exception?.Message}");
                                    }
                                });
                            }
                            else
                            {
                                // Debug.Log($"[BoostOpsManagerInternal] 🔥 Firebase config updated but boostops_config not affected. Updated keys: {string.Join(", ", updatedKeys)}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[BoostOpsManagerInternal] Error in Firebase config update listener: {ex.Message}");
                    }
                });
                
                // Debug.Log("[BoostOpsManagerInternal] ✅ Firebase Remote Config listener registered");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] Failed to setup Firebase Remote Config monitoring: {ex.Message}");
            }
        }
        #else
        private void StartFirebaseConfigMonitoring()
        {
            // Debug.Log("[BoostOpsManagerInternal] ⚠️ Firebase Remote Config monitoring unavailable - package not installed");
        }
        #endif
        
        private void StartUnityConfigMonitoring()
        {
            try
            {
                string lastConfigJson = "";
                
                System.Action pollUnityConfig = () => {
                    try
                    {
                        var configKey = "boostops_config";
                        var currentConfigJson = GetRemoteConfigValueViaReflection(configKey);
                        
                        if (!string.IsNullOrEmpty(currentConfigJson) && currentConfigJson != "{}" && currentConfigJson != lastConfigJson)
                        {
                            Debug.Log($"[BoostOpsManagerInternal] 🎯 Unity Remote Config change detected (length: {currentConfigJson.Length})");
                            lastConfigJson = currentConfigJson;
                            OnRemoteConfigUpdated("Unity Remote Config", new List<string> { configKey });
                        }
                        else if (string.IsNullOrEmpty(lastConfigJson))
                        {
                            lastConfigJson = currentConfigJson ?? "{}";
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[BoostOpsManagerInternal] Error polling Unity Remote Config: {ex.Message}");
                    }
                };
                
                var gameObject = new UnityEngine.GameObject("BoostOps_RemoteConfigPoller");
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                var poller = gameObject.AddComponent<UnityRemoteConfigPoller>();
                poller.StartPolling(pollUnityConfig, 5f, 60f);
                
                // Debug.Log("[BoostOpsManagerInternal] ✅ Unity Remote Config polling started");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] Failed to setup Unity Remote Config monitoring: {ex.Message}");
            }
        }
        
        public void InitializeLocalOnly()
        {
            try
            {
                BoostOpsLogger.LogInfo("ManagerInternal", "=== InitializeLocalOnly() ENTRY POINT ===");
                
                if (isInitialized && localOnlyMode && campaigns.Count > 0)
                {
                    BoostOpsLogger.LogDebug("ManagerInternal", "Already initialized in local mode with campaigns loaded");
                    return;
                }
                
                // Force local mode FIRST, before analytics initialization
                localOnlyMode = true;
                BoostOpsLogger.LogInfo("ManagerInternal", "✅ Set localOnlyMode = true BEFORE analytics initialization");
                
                // Initialize analytics client (BoostOps provider will self-disable in local mode, but keep Google/Unity active)
                try
                {
                    BoostOpsLogger.LogDebug("ManagerInternal", "Initializing analytics client for local mode (BoostOps provider disabled, Google/Unity active)...");
                    BoostOpsAnalyticsContract.InitializeAnalyticsFromSettings(isDevelopmentMode: false);
                    BoostOpsLogger.LogInfo("ManagerInternal", "✅ Analytics client initialized successfully - BoostOps provider auto-disabled in local mode");
                }
                catch (Exception analyticsEx)
                {
                    BoostOpsLogger.LogError("ManagerInternal", $"⚠️ Analytics initialization failed (non-critical): {analyticsEx.Message}");
                    // Continue with SDK initialization even if analytics fails
                }
                
                // Load campaigns synchronously from Resources
                bool campaignsLoaded = LoadLocalCampaigns();
                
                // Load cached app walls configuration (fallback for offline use)
                LoadCachedAppWalls();
                
                isInitialized = true;
                BoostOpsLogger.LogInfo("ManagerInternal", $"✅ Local initialization complete. Campaigns loaded: {campaigns.Count}, App walls: {(appWalls != null ? "cached" : "none")}");
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("ManagerInternal", $"❌ Local initialization failed: {ex.Message}");
                throw;
            }
        }
        
        public async System.Threading.Tasks.Task<bool> InitializeManagedAsync()
        {
            try
            {
                if (isInitialized && !localOnlyMode)
                {
                    return true;
                }
                
                // Initialize analytics client (settings should already be cached by BoostOpsSDK.Init())
                try
                {
                    // Debug.Log("[BoostOpsManagerInternal] Initializing analytics client...");
                    BoostOpsAnalyticsContract.InitializeAnalyticsFromSettings(isDevelopmentMode: false);
                    // Debug.Log("[BoostOpsManagerInternal] ✅ Analytics client initialized successfully");
                }
                catch (Exception analyticsEx)
                {
                    Debug.LogError($"[BoostOpsManagerInternal] ⚠️ Analytics initialization failed (non-critical): {analyticsEx.Message}");
                    // Continue with SDK initialization even if analytics fails
                }
                
                // Set managed mode and automatically load remote campaigns
                localOnlyMode = false;
                isInitialized = true;
                remoteCampaignsLoaded = false;
                remoteCampaignsLoading = false;
                
                // Don't fetch remote config - the app handles fetching
                // We just read from the cache when needed (lazy load)
                // Remote config will be parsed on-demand when ShowCrossPromo or ShowAppWall is called
                    
                    // Start monitoring remote config for runtime updates
                    StartRemoteConfigMonitoring();
                    
                    // Debug.Log("[BoostOpsManagerInternal] ✅ Managed mode initialized with automatic campaign loading and config monitoring");
                    return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] ❌ Managed initialization failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Public method to trigger lazy loading of campaigns from remote config cache.
        /// Called by BoostOpsSDK when GetEligibleCampaigns() finds no campaigns available.
        /// </summary>
        public bool TryLazyLoadCampaigns()
        {
            return TryParseCachedRemoteConfig();
        }
        
        /// <summary>
        /// Try to parse campaigns from Unity Remote Config's cached data.
        /// This reads from the cache without fetching - the app is responsible for fetching.
        /// 
        /// BoostOps SDK does NOT call FetchConfigsAsync() - the app handles all remote config fetching.
        /// This method is called on-demand when campaigns are needed:
        /// - When ShowCrossPromo or ShowAppWall is first called (lazy load)
        /// - When remote config monitoring detects an update
        /// - As a retry mechanism if config wasn't available earlier
        /// 
        /// Returns true if config was successfully parsed, false if cache is empty or invalid.
        /// </summary>
        private bool TryParseCachedRemoteConfig()
        {
            if (remoteCampaignsLoaded)
            {
                // Debug.Log("[BoostOpsManagerInternal] Remote campaigns already parsed");
                return true;
            }
            
            try
            {
                string configKey = "boostops_config";
                
                // Read from Unity Remote Config cache via reflection (no fetch - assumes already fetched by Unity Services)
                var configJson = GetRemoteConfigValueViaReflection(configKey);
                
                if (string.IsNullOrEmpty(configJson) || configJson == "{}")
                {
                    return false;
                }
                
                bool success = ParseRemoteConfigJson(configJson);
                remoteCampaignsLoaded = success;
                
                return success;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] Failed to read from Unity Remote Config cache: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Read a value from Unity Remote Config cache via reflection.
        /// Avoids hard dependency on Unity.Services.RemoteConfig assembly for DLL builds.
        /// </summary>
        private string GetRemoteConfigValueViaReflection(string key)
        {
            try
            {
                var remoteConfigType = System.Type.GetType("Unity.Services.RemoteConfig.RemoteConfigService, Unity.Services.RemoteConfig");
                if (remoteConfigType == null) return "{}";
                
                var instanceProp = remoteConfigType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null) return "{}";
                
                var appConfigProp = instance.GetType().GetProperty("appConfig");
                var appConfig = appConfigProp?.GetValue(instance);
                if (appConfig == null) return "{}";
                
                var getJsonMethod = appConfig.GetType().GetMethod("GetJson", new System.Type[] { typeof(string), typeof(string) });
                if (getJsonMethod == null) return "{}";
                
                return getJsonMethod.Invoke(appConfig, new object[] { key, "{}" }) as string ?? "{}";
            }
            catch (System.Exception)
            {
                return "{}";
            }
        }
        
        private async System.Threading.Tasks.Task<bool> LoadRemoteConfigCampaignsOld()
        {
            try
            {
                Debug.Log("[BoostOpsManagerInternal] Loading campaigns from Unity Remote Config...");
                
                // Ensure Unity Services are initialized first
                try 
                {
                    var unityServicesType = System.Type.GetType("Unity.Services.Core.UnityServices, Unity.Services.Core");
                    if (unityServicesType != null)
                    {
                        var stateProperty = unityServicesType.GetProperty("State", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var currentState = stateProperty?.GetValue(null);
                        
                        if (currentState?.ToString() != "Initialized")
                        {
                            var initMethod = unityServicesType.GetMethod("InitializeAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (initMethod != null)
                            {
                                var initTask = initMethod.Invoke(null, new object[0]) as System.Threading.Tasks.Task;
                                await initTask;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BoostOpsManagerInternal] Could not check/initialize Unity Services: {ex.Message}");
                }
                
                // Get project settings to determine the project key
                var projectSettings = InternalSettingsCache.GetProjectSettings();
                if (projectSettings == null || string.IsNullOrEmpty(projectSettings.ProjectKey))
                {
                    Debug.LogError("[BoostOpsManagerInternal] No project key found - cannot fetch remote config");
                    return false;
                }
                
                // Debug.Log($"[BoostOpsManagerInternal] Using project key: {projectSettings.ProjectKey}, Environment: {environment}");
                
                // Use reflection to access Unity Remote Config 4.x API
                var remoteConfigType = System.Type.GetType("Unity.Services.RemoteConfig.RemoteConfigService, Unity.Services.RemoteConfig");
                if (remoteConfigType == null)
                {
                    Debug.LogError("[BoostOpsManagerInternal] Unity Remote Config service not available");
                    return false;
                }
                
                var instanceProperty = remoteConfigType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var remoteConfigInstance = instanceProperty?.GetValue(null);
                
                if (remoteConfigInstance == null)
                {
                    Debug.LogError("[BoostOpsManagerInternal] Could not get Unity Remote Config instance");
                    return false;
                }
                
                // Fetch configs
                var fetchMethod = remoteConfigType.GetMethod("FetchConfigsAsync", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null, 
                    new System.Type[] { typeof(object), typeof(object) }, 
                    null);
                
                if (fetchMethod != null)
                {
                    // Create proper user attributes and app attributes for the fetch
                    var userAttributes = new System.Collections.Generic.Dictionary<string, object>();
                    var appAttributes = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "environment", environment },
                        { "project_key", projectSettings.ProjectKey }
                    };
                    
                    var fetchTask = fetchMethod.Invoke(remoteConfigInstance, new object[] { userAttributes, appAttributes }) as System.Threading.Tasks.Task;
                    await fetchTask;
                }
                else
                {
                    Debug.LogWarning("[BoostOpsManagerInternal] FetchConfigsAsync method not found - using cached config");
                }
                
                // Get JSON using the correct config key
                string configKey = "boostops_config";
                
                // Use the correct Unity Remote Config 4.0 API: RemoteConfigService.Instance.appConfig.GetJson(key)
                var appConfigProperty = remoteConfigType.GetProperty("appConfig", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var appConfig = appConfigProperty?.GetValue(remoteConfigInstance);
                
                if (appConfig == null)
                {
                    Debug.LogError("[BoostOpsManagerInternal] Could not get appConfig from RemoteConfigService.Instance");
                    Debug.LogWarning("[BoostOpsManagerInternal] Falling back to local campaign loading...");
                    return LoadLocalCampaigns();
                }
                
                // Get the GetJson method from appConfig - try different overloads
                var getJsonMethod = appConfig.GetType().GetMethod("GetJson", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (getJsonMethod == null)
                {
                    Debug.LogError($"[BoostOpsManagerInternal] Could not find GetJson method on appConfig type: {appConfig.GetType().FullName}");
                    // Debug: List all available methods with their signatures
                    var methods = appConfig.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    // Verbose method signature logging removed
                    Debug.LogWarning("[BoostOpsManagerInternal] Falling back to local campaign loading...");
                    return LoadLocalCampaigns();
                }
                
                // Check the method signature and call with correct parameters
                var parameters = getJsonMethod.GetParameters();
                var configJson = null as object;
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    // Standard GetJson(string key) signature
                    configJson = getJsonMethod.Invoke(appConfig, new object[] { configKey });
                }
                else if (parameters.Length == 0)
                {
                    // GetJson() with no parameters - might return all config as JSON
                    configJson = getJsonMethod.Invoke(appConfig, new object[0]);
                }
                else if (parameters.Length == 2)
                {
                    // GetJson(string key, defaultValue) signature
                    configJson = getJsonMethod.Invoke(appConfig, new object[] { configKey, "{}" });
                }
                else
                {
                    Debug.LogError($"[BoostOpsManagerInternal] Unsupported GetJson method signature with {parameters.Length} parameters");
                    Debug.LogWarning("[BoostOpsManagerInternal] Falling back to local campaign loading...");
                    return LoadLocalCampaigns();
                }
                
                if (configJson == null)
                {
                    Debug.LogWarning($"[BoostOpsManagerInternal] No remote config found for key: {configKey}");
                    Debug.LogWarning("[BoostOpsManagerInternal] Falling back to local campaign loading...");
                    return LoadLocalCampaigns();
                }
                
                // Check if we got the default empty value
                var configJsonString = configJson?.ToString();
                if (configJsonString == "{}")
                {
                    Debug.LogWarning($"[BoostOpsManagerInternal] Remote config returned default empty value for key: {configKey}");
                    Debug.LogWarning("[BoostOpsManagerInternal] This indicates the key doesn't exist in Unity Remote Config Dashboard");
                    Debug.LogWarning("[BoostOpsManagerInternal] Falling back to local campaign loading...");
                    return LoadLocalCampaigns();
                }
                
                // Parse the campaigns from the remote config JSON object
                return ParseRemoteConfigJson(configJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] Failed to load remote config campaigns: {ex.Message}");
                Debug.LogWarning("[BoostOpsManagerInternal] Falling back to local campaign loading...");
                return LoadLocalCampaigns();
            }
        }
        
        /// <summary>
        /// Load campaigns from local sources (StreamingAssets, Resources) as fallback
        /// </summary>
        private bool LoadLocalCampaigns()
        {
            try
            {
                BoostOpsLogger.LogDebug("ManagerInternal", "Loading campaigns from Resources...");
                
                // Load full config with analytics settings from Resources
                var configResult = BoostOps.CampaignParser.LoadConfigFromResources("BoostOps/cross_promo_local");
                
                if (configResult != null)
                {
                    // Store the full config for analytics access
                    currentConfig = configResult;
                    
                    // Trigger processing of any queued analytics events now that config is loaded
                    var analyticsProvider = AnalyticsProviderFactory.GetProvider("BoostOps Analytics") as BoostOpsAnalyticsProvider;
                    analyticsProvider?.Initialize();
                    
                    // Get campaigns from the config
                    var localCampaigns = configResult.GetAllCampaigns();
                    
                    if (localCampaigns != null && localCampaigns.Count > 0)
                    {
                        campaigns.Clear();
                        campaigns.AddRange(localCampaigns);
                        BoostOpsLogger.LogInfo("ManagerInternal", $"✅ Loaded {campaigns.Count} campaigns from Resources");
                        BoostOpsLogger.LogInfo("ManagerInternal", $"✅ Analytics config enabled: {currentConfig.analytics_config?.enabled ?? false}");
                        return true;
                    }
                }
                
                BoostOpsLogger.LogWarning("ManagerInternal", "No campaigns found in Resources/BoostOps/cross_promo_local.json");
                return false;
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("ManagerInternal", $"Failed to load local campaigns: {ex.Message}");
                return false;
            }
        }
        
        private bool ParseRemoteConfigJson(object configJson)
        {
            try
            {
                // Convert object to JSON string for parsing
                var configJsonString = configJson?.ToString();
                if (string.IsNullOrEmpty(configJsonString))
                {
                    Debug.LogWarning("[BoostOpsManagerInternal] Config JSON object is null or empty");
                    return false;
                }
                
                // Parse campaigns using the ORIGINAL working JsonConfigWrapper approach
                JsonConfigWrapper structuredConfig = null;
                try
                {
                    structuredConfig = JsonUtility.FromJson<JsonConfigWrapper>(configJsonString);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BoostOpsManagerInternal] JsonUtility parsing failed: {ex.Message}");
                }

                bool foundCampaigns = false;
                if (structuredConfig != null && structuredConfig.Campaigns != null && structuredConfig.Campaigns.Length > 0)
                {
                    campaigns.Clear();
                    campaigns.AddRange(structuredConfig.Campaigns);
                    foundCampaigns = true;
                    
                    // Debug.Log($"[BoostOpsManagerInternal] ✅ Loaded {campaigns.Count} campaigns using JsonConfigWrapper");
                }
                
                // Parse app walls configuration
                if (structuredConfig != null && structuredConfig.app_walls != null)
                {
                    appWalls = structuredConfig.app_walls;
                    usingCachedAppWalls = false; // Using remote config, not cached
                    var defaultWall = appWalls.@default;
                    
                    if (defaultWall != null && defaultWall.enabled)
                    {
                        int appCount = defaultWall.items?.Length ?? 0;
                        // Debug.Log($"[BoostOpsManagerInternal] ✅ Loaded app wall with {appCount} items from remote config (max_shown: {defaultWall.max_shown})");
                    }
                }
                else
                {
                    // Load cached app walls as fallback
                    if (appWalls == null)
                    {
                        LoadCachedAppWalls();
                    }
                }
                
                // SEPARATELY parse analytics config from the same JSON
                currentConfig = ParseAnalyticsConfigFromJson(configJsonString);
                
                // Store version info if available
                if (structuredConfig?.version_info != null)
                {
                    currentConfig.version_info = structuredConfig.version_info;
                }
                
                if (currentConfig?.analytics_config != null)
                {
                    // Debug.Log($"[BoostOpsManagerInternal] ✅ Analytics config loaded: enabled={currentConfig.analytics_config.enabled}, endpoint={currentConfig.analytics_config.endpoint}");
                    
                    // Trigger processing of any queued analytics events now that remote config is loaded
                    var analyticsProvider = AnalyticsProviderFactory.GetProvider("BoostOps Analytics") as BoostOpsAnalyticsProvider;
                    analyticsProvider?.Initialize();
                }
                
                return foundCampaigns;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] Failed to parse remote config JSON: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Parse analytics config specifically from JSON string
        /// </summary>
        private BoostOpsConfig ParseAnalyticsConfigFromJson(string jsonString)
        {
            var config = new BoostOpsConfig();
            
            try
            {
                // Extract the ingest section using regex
                var ingestMatch = System.Text.RegularExpressions.Regex.Match(
                    jsonString, 
                    @"""ingest"":\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}",
                    System.Text.RegularExpressions.RegexOptions.Singleline
                );
                
                if (ingestMatch.Success)
                {
                    var ingestJsonContent = ingestMatch.Groups[1].Value;
                    var ingestJson = "{" + ingestJsonContent + "}";
                    
                    try
                    {
                        var ingestConfig = JsonUtility.FromJson<AnalyticsIngestConfig>(ingestJson);
                        if (ingestConfig != null)
                        {
                            config.analytics_config = ingestConfig.ToAnalyticsConfig();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[BoostOpsManagerInternal] Failed to parse extracted ingest JSON: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] Exception parsing analytics config: {ex.Message}");
            }
            
            return config;
        }
        
        #endregion
        
        #region Campaign Selection Algorithms
        
        /// <summary>
        /// Get the next campaign using the configured selection algorithm
        /// Supports waterfall (for demo/local mode) and weighted random selection (for server mode)
        /// </summary>
        public Campaign GetNextCampaign(string placement = "default")
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] GetNextCampaign: Not initialized");
                return null;
            }
            
            // Get all available campaigns with eligibility filtering
            List<Campaign> eligibleCampaigns = FilterEligibleCampaigns(campaigns, placement);
            
            if (eligibleCampaigns.Count == 0)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] GetNextCampaign: No eligible campaigns available");
                return null;
            }
            
            // For demo/local mode, use waterfall selection (prioritize by order)
            if (localOnlyMode)
            {
                return GetWaterfallCampaign(eligibleCampaigns, placement);
            }
            
            // For server mode, use weighted random (or server-configured algorithm)
            return GetWeightedRandomCampaign(eligibleCampaigns);
        }
        
        /// <summary>
        /// Filter campaigns for eligibility (schedule, targeting, etc.)
        /// </summary>
        private List<Campaign> FilterEligibleCampaigns(List<Campaign> allCampaigns, string placement)
        {
            var eligible = new List<Campaign>();
            
            foreach (var campaign in allCampaigns)
            {
                if (campaign == null) continue;
                
                // Basic eligibility checks
                bool isEligible = true;
                
                // Check if campaign is active
                if (campaign.schedule != null && !campaign.schedule.IsActive(DateTime.Now))
                {
                    string endDateStr = string.IsNullOrEmpty(campaign.schedule.end_date) ? "No end date" : campaign.schedule.end_date;
                    string daysStr = (campaign.schedule.days == null || campaign.schedule.days.Length == 0) ? "All days" : string.Join(",", campaign.schedule.days);
                    string scheduleInfo = $"Start: {campaign.schedule.start_date}, End: {endDateStr}, Days: {daysStr}";
                    if (campaign.schedule.HasHourRestrictions)
                    {
                        scheduleInfo += $", Hours: {campaign.schedule.start_hour}:00-{campaign.schedule.end_hour}:00";
                    }
                    // Debug.Log($"[BoostOpsManagerInternal] Campaign '{campaign.name}' filtered out - schedule not active ({scheduleInfo})");
                    isEligible = false;
                }
                
                if (isEligible)
                {
                    eligible.Add(campaign);
                }
            }
            return eligible;
        }
        
        /// <summary>
        /// Waterfall selection: Show campaigns in order until each hits daily cap, then move to next
        /// Like a waterfall - fills up one "bucket" completely before moving to the next level
        /// </summary>
        private Campaign GetWaterfallCampaign(List<Campaign> campaigns, string placement)
        {
            if (campaigns.Count == 0) return null;
            if (campaigns.Count == 1) return campaigns[0];
                        
            // In demo/local mode, ignore frequency caps for easier testing
            bool ignoreCapsForDemo = localOnlyMode;
            
            if (ignoreCapsForDemo)
            {
                // Demo mode: cycle through campaigns without cap restrictions for easy testing
                try
                {
                    // Use a time-based index calculation for cycling every 10 seconds
                    long ticks = System.DateTime.Now.Ticks;
                    long seconds = ticks / System.TimeSpan.TicksPerSecond;
                    int campaignIndex = (int)(Math.Abs(seconds / 10) % campaigns.Count);
                    
                    if (campaignIndex >= 0 && campaignIndex < campaigns.Count)
                    {
                        var selectedCampaign = campaigns[campaignIndex];
                        return selectedCampaign;
                    }
                    else
                    {
                        Debug.LogWarning($"[BoostOpsManagerInternal] Demo waterfall: Invalid index {campaignIndex}, using first campaign");
                        return campaigns[0];
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BoostOpsManagerInternal] Exception in demo waterfall selection: {ex.Message}");
                    return campaigns.Count > 0 ? campaigns[0] : null;
                }
            }
            
            // Normal waterfall: Go through campaigns in order, find first one that hasn't hit daily cap
            // For now, simplified without frequency caps
            return campaigns[0];
        }
        
        /// <summary>
        /// Weighted random selection (future: could use campaign weights from server)
        /// For now, treats all campaigns equally
        /// </summary>
        private Campaign GetWeightedRandomCampaign(List<Campaign> campaigns)
        {
            if (campaigns.Count == 0) return null;
            
            try
            {
                // TODO: Implement actual weighted selection when server provides campaign weights
                // For now, use equal weights (random selection)
                int randomIndex = UnityEngine.Random.Range(0, campaigns.Count);
                
                if (randomIndex >= 0 && randomIndex < campaigns.Count)
                {
                    var selectedCampaign = campaigns[randomIndex];
                    return selectedCampaign;
                }
                else
                {
                    Debug.LogWarning($"[BoostOpsManagerInternal] Random index {randomIndex} out of bounds, using first campaign");
                    return campaigns[0];
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] Exception in weighted random selection: {ex.Message}");
                return campaigns.Count > 0 ? campaigns[0] : null;
            }
        }
        
        #endregion
        
        #region Campaign Display
        
        /// <summary>
        /// Show cross-promotion campaign with specific display mode
        /// </summary>
        public bool ShowCrossPromo(string placement, BoostOpsCampaignDisplay.CampaignDisplayMode displayMode)
        {
            // Debug.Log($"[BoostOpsManagerInternal] ShowCrossPromo called on instance {GetHashCode()} - placement: {placement}, displayMode: {displayMode}");
            // Debug.Log($"[BoostOpsManagerInternal] 🔍 Prefab state at ShowCrossPromo: Icon={customIconInterstitialPrefab?.name ?? "NULL"}, Banner={customBannerPrefab?.name ?? "NULL"}");
            
            if (!isInitialized)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] ShowCrossPromo: SDK not initialized!");
                return false;
            }
            
            // For remote mode, lazy load campaigns if not already loaded
            if (!localOnlyMode && !remoteCampaignsLoaded)
            {
                TryParseCachedRemoteConfig();
                
                // If still not loaded, show warning
                if (!remoteCampaignsLoaded)
                {
                    Debug.LogWarning("[BoostOpsManagerInternal] Remote config not available yet. Ensure remote config has been fetched.");
                }
            }
            
            // Check if campaigns are available
            if (campaigns.Count == 0)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] ShowCrossPromo: No campaigns available!");
                return false;
            }
            
            // Use proper campaign selection algorithm
            Campaign campaign = GetNextCampaign(placement);
            if (campaign == null)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] No eligible campaigns available");
                return false;
            }
            
            Debug.Log($"[BoostOpsManagerInternal] Selected campaign: '{campaign.name}' for display");
            
            // Debug campaign details
            // Debug.Log($"[BoostOpsManagerInternal] Campaign Status: '{campaign.status}'");
            // Debug.Log($"[BoostOpsManagerInternal] Campaign IsActive: {campaign.IsActive}");
            // Debug.Log($"[BoostOpsManagerInternal] Schedule IsActive: {campaign.Schedule.IsActive(DateTime.Now)}");
            // Debug.Log($"[BoostOpsManagerInternal] Current Time: {DateTime.Now}");
            
            // Find existing display component
            BoostOpsCampaignDisplay display = UnityEngine.Object.FindFirstObjectByType<BoostOpsCampaignDisplay>();
            
            if (display == null)
            {
                // Auto-create display component for better user experience
                // Debug.Log("[BoostOpsManagerInternal] Auto-creating BoostOpsCampaignDisplay component...");
                display = CreateAutoDisplay(displayMode, placement);
                
                if (display == null)
                {
                    Debug.LogError("[BoostOpsManagerInternal] Failed to auto-create BoostOpsCampaignDisplay component!");
                    return false;
                }
                
                // Debug.Log($"[BoostOpsManagerInternal] ✅ Auto-created BoostOpsCampaignDisplay in {displayMode} mode");
            }
            
            // Configure and show the campaign
            display.displayMode = displayMode;
            display.placementId = placement;
            display.ShowCampaign(campaign);
            
            // Track impression
            TrackImpression(campaign, placement);
            
            Debug.Log($"[BoostOpsManagerInternal] ✅ Successfully displayed campaign '{campaign.name}' in {displayMode} mode");
            return true;
        }
        
        public void HideAllPromos()
        {
            BoostOpsCampaignDisplay[] displays = UnityEngine.Object.FindObjectsByType<BoostOpsCampaignDisplay>(UnityEngine.FindObjectsSortMode.None);
            foreach (var display in displays)
            {
                if (display != null && display.gameObject.activeInHierarchy)
                {
                    display.gameObject.SetActive(false);
                }
            }
        }
        
        public void HidePromo(string placement)
        {
            BoostOpsCampaignDisplay[] displays = UnityEngine.Object.FindObjectsByType<BoostOpsCampaignDisplay>(UnityEngine.FindObjectsSortMode.None);
            foreach (var display in displays)
            {
                if (display != null && display.placementId == placement && display.gameObject.activeInHierarchy)
                {
                    display.gameObject.SetActive(false);
                    break;
                }
            }
        }
        
        /// <summary>
        /// Show app wall with multiple campaigns (legacy campaign-based approach)
        /// </summary>
        public bool ShowAppWallWithCampaigns(List<Campaign> campaigns, string placement)
        {
            if (campaigns == null || campaigns.Count == 0)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] ShowAppWall: No campaigns provided");
                return false;
            }
            
            // Debug.Log($"[BoostOpsManagerInternal] ShowAppWall called with {campaigns.Count} campaigns at placement: {placement}");
            
            // Find or create canvas
            Canvas canvas = GetOrCreateOverlayCanvas();
            if (canvas == null)
            {
                Debug.LogError("[BoostOpsManagerInternal] ShowAppWall: Failed to find or create canvas");
                return false;
            }
            
            // Create campaign display component
            GameObject displayObject = new GameObject($"BoostOpsAppWallDisplay_{placement}");
            displayObject.transform.SetParent(canvas.transform, false);
            
            BoostOpsCampaignDisplay display = displayObject.AddComponent<BoostOpsCampaignDisplay>();
            display.targetCanvas = canvas;
            display.placementId = placement;
            
            // Set prefabs if available
            if (customAppWallPrefab != null)
            {
                display.appWallPrefab = customAppWallPrefab;
            }
            if (customAppWallItemPrefab != null)
            {
                display.appWallItemPrefab = customAppWallItemPrefab;
            }
            
            // Show the app wall with multiple campaigns
            display.ShowAppWall(campaigns);
            
            Debug.Log($"[BoostOpsManagerInternal] App wall displayed with {campaigns.Count} campaigns");
            return true;
        }
        
        /// <summary>
        /// Show app wall using remote config app_walls section
        /// This is the new approach that uses dedicated app wall configuration
        /// </summary>
        public bool ShowAppWall(string placement)
        {
            // For remote mode, lazy load campaigns if not already loaded
            if (!localOnlyMode && !remoteCampaignsLoaded)
            {
                TryParseCachedRemoteConfig();
                
                // If still not loaded, show warning
                if (!remoteCampaignsLoaded)
                {
                    Debug.LogWarning("[BoostOpsManagerInternal] Remote config not available yet. Ensure remote config has been fetched.");
                }
            }
            
            // Get eligible apps from remote config
            var eligibleApps = GetEligibleAppWallApps();
            
            if (eligibleApps == null || eligibleApps.Count == 0)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] ShowAppWall: No eligible apps in app_walls configuration");
                return false;
            }
            
            // Debug.Log($"[BoostOpsManagerInternal] ShowAppWall called with {eligibleApps.Count} apps from remote config at placement: {placement}");
            
            // Find or create canvas
            Canvas canvas = GetOrCreateOverlayCanvas();
            if (canvas == null)
            {
                Debug.LogError("[BoostOpsManagerInternal] ShowAppWall: Failed to find or create canvas");
                return false;
            }
            
            // Find or load the app wall prefab
            GameObject appWallPrefab = customAppWallPrefab;
            if (appWallPrefab == null)
            {
                // Try to load default prefab from Resources
                appWallPrefab = Resources.Load<GameObject>("BoostOps/Prefabs/DefaultAppWallPrefab");
                
                if (appWallPrefab == null)
                {
                    Debug.LogError("[BoostOpsManagerInternal] ShowAppWall: No app wall prefab available. Assign customAppWallPrefab or ensure DefaultAppWallPrefab exists in Resources.");
                    return false;
                }
            }
            
            // Instantiate the app wall
            GameObject appWallInstance = UnityEngine.Object.Instantiate(appWallPrefab, canvas.transform);
            appWallInstance.name = $"BoostOpsAppWall_{placement}";
            
            // Get the controller component
            var controller = appWallInstance.GetComponent<BoostOps.CrossPromo.BoostOpsAppWallController>();
            if (controller == null)
            {
                Debug.LogError("[BoostOpsManagerInternal] ShowAppWall: App wall prefab is missing BoostOpsAppWallController component!");
                UnityEngine.Object.Destroy(appWallInstance);
                return false;
            }
            
            // Set the app tile prefab if available
            if (customAppWallItemPrefab != null && controller.appTilePrefab == null)
            {
                controller.appTilePrefab = customAppWallItemPrefab;
            }
            
            // Set placement
            controller.placement = placement;
            
            // Show the app wall
            controller.Show(eligibleApps);
            
            // Debug.Log($"[BoostOpsManagerInternal] App wall displayed with {eligibleApps.Count} apps");
            return true;
        }
        
        #endregion
        
        #region Analytics & Tracking
        
        public void TrackImpression(Campaign campaign, string placement)
        {
            if (campaign == null) return;
            
            // Debug.Log($"[BoostOpsManagerInternal] Tracking impression: {campaign.name} at {placement}");
            
            // In local mode, still track to Firebase/Unity Analytics but skip BoostOps server analytics
            if (localOnlyMode)
            {
                // Debug.Log($"[BoostOpsManagerInternal] 📊 LOCAL IMPRESSION: {campaign.name} at {placement} (local mode - BoostOps analytics disabled, Firebase/Unity active)");
                
                // Get platform-appropriate store IDs
                string sourceStoreId = BoostOpsAnalyticsContract.GetSourceStoreId();
                string targetStoreId = BoostOpsAnalyticsContract.GetTargetStoreId(campaign);
                
                // Debug.Log($"[BoostOpsManagerInternal] Local impression data - Source: '{sourceStoreId}', Target: '{targetStoreId}'");
                
                // Track to Firebase/Unity Analytics (BoostOps provider will auto-disable itself in local mode)
                string localImpressionId = BoostOpsAnalyticsContract.TrackImpression(
                    campaignSlug: campaign.campaign_id ?? campaign.name,
                    placement: placement,
                    format: "interstitial",
                    durationMs: null,
                    // Note: source_store_id is in context.store_id (universal) - not passed here
                    // Note: source_project_id is derived server-side from project_key
                    targetStoreId: targetStoreId,
                    targetProjectId: BoostOpsAnalyticsContract.GetTargetProjectId(campaign),
                    networkCampaignId: campaign.campaign_id,
                    revenueShareRate: null,
                    channel: "xpromo"
                );
                
                // Store impression_id for potential click linking
                string localCampaignKey = campaign.campaign_id ?? campaign.name;
                impressionTracker[localCampaignKey] = new ImpressionData
                {
                    ImpressionId = localImpressionId,
                    ImpressionTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                return;
            }
            
            // Managed mode - send analytics to BoostOps servers
            // Debug.Log($"[BoostOpsManagerInternal] Sending impression to BoostOps analytics...");
            
            // Get platform-appropriate store IDs
            string sourceStoreId2 = BoostOpsAnalyticsContract.GetSourceStoreId();
            string targetStoreId2 = BoostOpsAnalyticsContract.GetTargetStoreId(campaign);
            
            // Debug: Log campaign data before tracking
            string campaignSlug = campaign.campaign_id ?? campaign.name;
            // Debug.Log($"[BoostOpsManagerInternal] 🔍 Impression Debug - Campaign ID: '{campaign.campaign_id}', Name: '{campaign.name}', Final Slug: '{campaignSlug}'");
            // Debug.Log($"[BoostOpsManagerInternal] 🔍 Impression Debug - Placement: '{placement}', Source: '{sourceStoreId2}', Target: '{targetStoreId2}'");
            
            // Track impression with proper cross-promotion attribution
            string impressionId = BoostOpsAnalyticsContract.TrackImpression(
                campaignSlug: campaignSlug,
                placement: placement,
                format: "interstitial", // Default format, could be made configurable
                durationMs: null,
                // Note: source_store_id is in context.store_id (universal) - not passed here
                // Note: source_project_id is derived server-side from project_key
                targetStoreId: targetStoreId2,
                targetProjectId: BoostOpsAnalyticsContract.GetTargetProjectId(campaign),
                networkCampaignId: campaign.campaign_id,
                revenueShareRate: null, // Could be added to campaign data if needed
                channel: "xpromo"
            );
            
            // Store impression_id for potential click linking
            string campaignKey = campaign.campaign_id ?? campaign.name;
            impressionTracker[campaignKey] = new ImpressionData
            {
                ImpressionId = impressionId,
                ImpressionTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            // Debug.Log($"[BoostOpsManagerInternal] ✅ Impression tracked - Source: '{sourceStoreId2}', Target: '{targetStoreId2}'");
        }
        
        public string TrackClick(Campaign campaign, string placement, string format = "interstitial")
        {
            if (campaign == null) return null;
            
            // In local mode, only log click locally - do not send any analytics to BoostOps servers
            if (localOnlyMode)
            {
                return null; // No click_id in local mode
            }
            
            // Managed mode - send analytics to BoostOps servers
            // Generate click_id for deterministic attribution (especially for Android Play referrer)
            string clickId = System.Guid.NewGuid().ToString("N");
            
            // Get platform-appropriate store IDs
            string sourceStoreId2 = BoostOpsAnalyticsContract.GetSourceStoreId();
            string targetStoreId2 = BoostOpsAnalyticsContract.GetTargetStoreId(campaign);
            
            // Get the store URL for deep linking
            string storeUrl2 = GetStoreUrlForPlatform(campaign);
            
            // Get impression data for click linking
            string impressionId = null;
            long? impressionTimestamp = null;
            string campaignKey = campaign.campaign_id ?? campaign.name;
            if (impressionTracker.TryGetValue(campaignKey, out ImpressionData impressionData))
            {
                impressionId = impressionData.ImpressionId;
                impressionTimestamp = impressionData.ImpressionTimestamp;
            }
            
            // Track click with proper cross-promotion attribution (click_id is auto-generated in CreateClickEvent if not provided)
            BoostOpsAnalyticsContract.TrackClick(
                campaignSlug: campaign.campaign_id ?? campaign.name,
                placement: placement,
                clickX: null, // Could be added if click coordinates are tracked
                clickY: null,
                timeToClickMs: null, // Could be added if timing is tracked
                // Note: source_store_id is in context.store_id (universal) - not passed here
                // Note: source_project_id is derived server-side from project_key
                targetStoreId: targetStoreId2,
                targetProjectId: BoostOpsAnalyticsContract.GetTargetProjectId(campaign),
                networkCampaignId: campaign.campaign_id,
                deepLinkUrl: storeUrl2,
                format: format, // Passed from display mode (icon, rich_interstitial, etc.)
                channel: "xpromo",
                impressionId: impressionId,
                impressionTimestamp: impressionTimestamp,
                clickId: clickId // For Android Play referrer attribution
            );
            
            Debug.Log($"[BoostOpsManagerInternal] ✅ Click tracked - Source: '{sourceStoreId2}', Target: '{targetStoreId2}', click_id: '{clickId}'");
            
            // Return click_id for use in store URL (Android Play referrer)
            return clickId;
        }
        
        public int GetTotalImpressions()
        {
            // TODO: Implement analytics aggregation
            return 0;
        }
        
        public int GetTotalClicks()
        {
            // TODO: Implement analytics aggregation
            return 0;
        }
        
        /// <summary>
        /// Track a campaign click and open the store URL
        /// </summary>
        public void TrackClickAndOpenStore(Campaign campaign, string placement, object displayMode)
        {
            if (campaign == null) return;
            
            // Track the click using the centralized tracking method
            TrackClick(campaign, placement);
            
            // Open appropriate store link
            string storeUrl = GetStoreUrlForPlatform(campaign);
            if (!string.IsNullOrEmpty(storeUrl))
            {
                OpenStoreUrl(storeUrl);
            }
            else
            {
                Debug.LogError($"[BoostOps Manager Internal] No store URL available for campaign: {campaign.name}");
            }
        }
        
        /// <summary>
        /// Get the best store URL for the current platform
        /// </summary>
        private string GetStoreUrlForPlatform(Campaign campaign)
        {
            if (campaign?.target_project?.store_urls == null)
            {
                // Fallback to universal link if available
                if (campaign?.target_project?.universal_link != null)
                {
                    return campaign.target_project.universal_link.url;
                }
                return null;
            }
            
            // Use smart store detection to get the best URL for current store
            string bestUrl = BoostOps.BoostOpsStoreDetector.GetBestStoreUrl(campaign);
            if (!string.IsNullOrEmpty(bestUrl))
            {
                return bestUrl;
            }
            
            // Fallback to original platform-based selection
            return campaign.target_project.store_urls.GetUrlForCurrentPlatform();
        }
        
        /// <summary>
        /// Opens a store URL using the best available method for the platform.
        /// On iOS, uses native App Store sheet if available and URL is an App Store link.
        /// Otherwise falls back to opening in browser.
        /// </summary>
        private void OpenStoreUrl(string storeUrl)
        {
            if (string.IsNullOrEmpty(storeUrl))
            {
                Debug.LogError("[BoostOps Manager Internal] Cannot open empty store URL");
                return;
            }

            Debug.Log($"[BoostOps Manager Internal] Opening store URL: {storeUrl}");

#if UNITY_IOS && !UNITY_EDITOR
            // Check if this is an iOS App Store URL and native sheet is available
            if (storeUrl.Contains("apps.apple.com") && BoostOps.BoostOpsAppStoreSheet.IsAvailable())
            {
                var appStoreId = BoostOps.BoostOpsAppStoreSheet.ExtractAppStoreId(storeUrl);
                if (!string.IsNullOrEmpty(appStoreId))
                {
                    bool success = BoostOps.BoostOpsAppStoreSheet.ShowAppStoreSheet(appStoreId);
                    if (success)
                    {
                        Debug.Log($"[BoostOps Manager Internal] Opened App Store sheet for app ID: {appStoreId}");
                        return;
                    }
                    else
                    {
                        Debug.Log("[BoostOps Manager Internal] Failed to show App Store sheet, falling back to browser");
                    }
                }
            }
#endif
            
            // Fallback to standard URL opening
            try
            {
                Application.OpenURL(storeUrl);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BoostOps Manager Internal] Failed to open store URL: {e.Message}");
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        public List<Campaign> GetAllCampaigns()
        {
            return new List<Campaign>(campaigns);
        }
        
        /// <summary>
        /// Load cached app walls configuration as fallback
        /// Called during initialization or when remote config doesn't have app_walls
        /// </summary>
        private void LoadCachedAppWalls()
        {
            try
            {
                // Try to load cached config from BoostOpsProjectSettings
                var projectSettings = BoostOpsProjectSettings.GetInstance();
                
                if (projectSettings == null)
                {
                    return;
                }
                
                if (string.IsNullOrEmpty(projectSettings.cachedAppWallsJson))
                {
                    return;
                }
                
                // Parse cached app walls from JSON
                var cachedAppWalls = JsonUtility.FromJson<BoostOps.Core.AppWallsConfig>(projectSettings.cachedAppWallsJson);
                if (cachedAppWalls != null)
                {
                    appWalls = cachedAppWalls;
                    usingCachedAppWalls = true;
                    
                    var defaultWall = appWalls.@default;
                    if (defaultWall != null && defaultWall.enabled)
                    {
                        int appCount = defaultWall.items?.Length ?? 0;
                        Debug.Log($"[BoostOpsManagerInternal] ✅ Loaded {appCount} items from cached fallback (last updated: {projectSettings.appWallsLastUpdated})");
                    }
                    // else: cached app wall is disabled
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] Failed to load cached app walls: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get the app walls configuration from remote config (or cached fallback)
        /// </summary>
        public BoostOps.Core.AppWallsConfig GetAppWalls()
        {
            return appWalls;
        }
        
        /// <summary>
        /// Get the default app wall configuration
        /// </summary>
        public BoostOps.Core.AppWallDefault GetDefaultAppWall()
        {
            return appWalls?.@default;
        }
        
        /// <summary>
        /// Get eligible apps for the app wall (respecting enabled flags and max_shown)
        /// </summary>
        public List<BoostOps.Core.AppWallApp> GetEligibleAppWallApps()
        {
            var defaultWall = GetDefaultAppWall();
            if (defaultWall == null)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] GetEligibleAppWallApps: No default app wall found in config");
                return new List<BoostOps.Core.AppWallApp>();
            }
            
            if (!defaultWall.enabled)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] GetEligibleAppWallApps: App wall is disabled in remote config (enabled: false). Enable it in your BoostOps dashboard.");
                return new List<BoostOps.Core.AppWallApp>();
            }
            
            if (defaultWall.items == null)
            {
                Debug.LogWarning("[BoostOpsManagerInternal] GetEligibleAppWallApps: No items configured in app_walls.default.items");
                return new List<BoostOps.Core.AppWallApp>();
            }
            
            var eligible = new List<BoostOps.Core.AppWallApp>();
            foreach (var app in defaultWall.items)
            {
                if (app != null && app.enabled)
                {
                    eligible.Add(app);
                    
                    if (eligible.Count >= defaultWall.max_shown)
                        break;
                }
            }
            
            // Apply sort order
            if (defaultWall.sort_order == "random")
            {
                // Shuffle the list
                for (int i = eligible.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    var temp = eligible[i];
                    eligible[i] = eligible[j];
                    eligible[j] = temp;
                }
            }
            
            return eligible;
        }
        
        public Campaign GetCampaignById(string id)
        {
            return campaigns.FirstOrDefault(c => c.campaign_id == id);
        }
        
        /// <summary>
        /// Create an automatic BoostOpsCampaignDisplay component with high-priority overlay canvas
        /// Ensures cross-promotion campaigns always appear above game UI
        /// </summary>
        private BoostOpsCampaignDisplay CreateAutoDisplay(BoostOpsCampaignDisplay.CampaignDisplayMode displayMode, string placement)
        {
            try
            {
                // Debug.Log($"[BoostOpsManagerInternal] CreateAutoDisplay starting on instance {GetHashCode()} - Mode: {displayMode}, Placement: {placement}");
                // Debug.Log($"[BoostOpsManagerInternal] 🔍 Prefab state at CreateAutoDisplay: Icon={customIconInterstitialPrefab?.name ?? "NULL"}, Banner={customBannerPrefab?.name ?? "NULL"}");
                
                // Create a new GameObject for the display
                GameObject displayObject = new GameObject($"BoostOps_AutoDisplay_{placement}");
                
                // Always create or find the dedicated BoostOps overlay canvas
                Canvas overlayCanvas = GetOrCreateOverlayCanvas();
                
                // Set display as child of overlay canvas
                displayObject.transform.SetParent(overlayCanvas.transform, false);
                
                // Add the BoostOpsCampaignDisplay component
                BoostOpsCampaignDisplay display = displayObject.AddComponent<BoostOpsCampaignDisplay>();
                
                // Configure the display
                display.displayMode = displayMode;
                display.placementId = placement;
                display.targetCanvas = overlayCanvas;
                display.autoShow = false; // We're manually controlling it
                
                // Assign custom prefabs if available
                // Debug.Log($"[BoostOpsManagerInternal] Assigning prefabs - Icon: {(customIconInterstitialPrefab != null ? customIconInterstitialPrefab.name : "NULL")}, Banner: {(customBannerPrefab != null ? customBannerPrefab.name : "NULL")}");
                
                if (customBannerPrefab != null) display.bannerPrefab = customBannerPrefab;
                if (customIconInterstitialPrefab != null) display.iconInterstitialPrefab = customIconInterstitialPrefab;
                if (customRichInterstitialPrefab != null) display.richInterstitialPrefab = customRichInterstitialPrefab;
                if (customNativePrefab != null) display.nativePrefab = customNativePrefab;
                
                // Debug.Log($"[BoostOpsManagerInternal] ✅ Auto-display component created - Final prefab check: iconInterstitialPrefab={(display.iconInterstitialPrefab != null ? display.iconInterstitialPrefab.name : "NULL")}");
                return display;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsManagerInternal] ❌ Failed to create auto-display: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get or create the dedicated BoostOps overlay canvas with high sorting order
        /// </summary>
        private Canvas GetOrCreateOverlayCanvas()
        {
            // Look for existing BoostOps overlay canvas
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(UnityEngine.FindObjectsSortMode.None);
            Canvas existingCanvas = System.Array.Find(canvases, c => c.name.Contains("BoostOps_Overlay"));
            
            if (existingCanvas != null)
            {
                // Debug.Log($"[BoostOpsManagerInternal] Found existing BoostOps overlay canvas: {existingCanvas.name}");
                return existingCanvas;
            }
            
            // Create new overlay canvas
            GameObject canvasObject = new GameObject("BoostOps_OverlayCanvas");
            Canvas overlayCanvas = canvasObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = overlaySortingOrder; // Use configured sorting order
            
            // Add CanvasScaler for proper scaling
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster for UI interaction
            canvasObject.AddComponent<GraphicRaycaster>();
            
            // Make it persistent across scene loads
            UnityEngine.Object.DontDestroyOnLoad(canvasObject);
            
            // Debug.Log($"[BoostOpsManagerInternal] ✅ Created new BoostOps overlay canvas with sorting order: {overlayCanvas.sortingOrder}");
            return overlayCanvas;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Wrapper class for loading local campaign JSON
    /// </summary>
    [System.Serializable]
    public class LocalCampaignWrapper
    {
        public List<Campaign> campaigns;
    }
    
    /// <summary>
    /// Mock SourceProject for development compatibility
    /// This is internal implementation that customers don't see
    /// </summary>
    internal class SourceProjectMock : BoostOps.Internal.ISourceProject
    {
        public string DefaultIconInterstitialDescription => "Try our exciting new game!";
        public string DefaultIconInterstitialButtonText => "Download Now";
        public string DefaultRichInterstitialDescription => "Experience amazing gameplay in our latest game!";
        public string DefaultRichInterstitialButtonText => "Play Now";
        
        // Add other properties as needed for development
        public string ProjectName => "BoostOps Development";
        public string ProjectId => "dev-project";
    }
    
    public class UnityRemoteConfigPoller : UnityEngine.MonoBehaviour
    {
        public void StartPolling(System.Action pollAction, float initialDelay, float interval)
        {
            InvokeRepeating(nameof(DoPoll), initialDelay, interval);
            _pollAction = pollAction;
        }
        
        private System.Action _pollAction;
        
        private void DoPoll()
        {
            _pollAction?.Invoke();
        }
        
        private void OnDestroy()
        {
            CancelInvoke();
        }
    }
}
