using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BoostOps.Analytics;

namespace BoostOps.Internal
{
    /// <summary>
    /// BoostOpsSDKInternal - Contains all the sophisticated SDK initialization and configuration logic
    /// This class will be compiled into the DLL to protect IP
    /// </summary>
    public class BoostOpsSDKInternal
    {
        #region Private Fields
        
        private string sdkKey;
        private string demoDataFile;
        private bool isInitialized = false;
        private BoostOpsManagerInternal managerInternal;
        
        // Track if app has been opened before in this memory session (for cold vs warm detection)
        private static bool hasOpenedInCurrentSession = false;
        
        // Static state accessible from other DLL classes without referencing Public types
        private static MonoBehaviour _coroutineRunner;
        private static bool _sdkInitializedStatic = false;
        private static bool _sdkLocalModeStatic = false;
        
        // Events that get forwarded to public SDK events
        public event Action<BoostOps.Internal.InitError> OnInitFailed;
        public event Action OnInitSuccess;
        public event Action OnCampaignsReady; // Fired when campaigns are loaded from remote config
        
        #pragma warning disable CS0067 // Event is never used - part of internal API, will be implemented
        public event Action<Campaign> OnCampaignImpression;
        public event Action<Campaign> OnCampaignClick;
        #pragma warning restore CS0067
        
        #endregion
        
        #region Public Properties
        
        public bool IsInitialized => isInitialized && managerInternal?.IsInitialized == true;
        public string SdkKey => sdkKey;
        public string DemoDataFile => demoDataFile;
        
        /// <summary>
        /// Static check for SDK initialization state (used by DLL-internal classes)
        /// </summary>
        public static bool IsSDKInitialized => _sdkInitializedStatic;
        
        /// <summary>
        /// Static check for local mode state (used by DLL-internal classes)
        /// </summary>
        public static bool IsSDKLocalMode => _sdkLocalModeStatic;
        
        /// <summary>
        /// Register a MonoBehaviour for running coroutines from non-MonoBehaviour DLL code.
        /// Called by BoostOpsManager on Awake().
        /// </summary>
        public static void SetCoroutineRunner(MonoBehaviour runner)
        {
            _coroutineRunner = runner;
        }
        
        #endregion
        
        #region Constructor
        
        public BoostOpsSDKInternal()
        {
            // Internal manager will be set by BoostOpsManager when it creates this instance
            BoostOpsLogger.LogDebug("SDKInternal", $"🔗 Created SDK internal instance - waiting for manager injection");
        }
        
        /// <summary>
        /// Set the internal manager instance (called by BoostOpsManager)
        /// </summary>
        public void SetInternalManager(BoostOps.Internal.BoostOpsManagerInternal manager)
        {
            managerInternal = manager;
            Debug.Log($"[BoostOpsSDKInternal] 🔗 Internal manager injected: {managerInternal?.GetHashCode() ?? 0}");
        }
        
        #endregion
        
        #region Configuration
        
        /// <summary>
        /// Set SDK key for server mode
        /// </summary>
        public void SetSdkKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[BoostOpsSDKInternal] SDK key is null or empty");
                return;
            }
            
            sdkKey = key;
            Debug.Log($"[BoostOpsSDKInternal] SDK key configured: {key}");
        }
        
        /// <summary>
        /// Set demo data file for local testing
        /// </summary>
        public void SetDemoDataFile(string dataFile)
        {
            if (string.IsNullOrEmpty(dataFile))
            {
                Debug.LogWarning("[BoostOpsSDKInternal] Demo data file is null or empty");
                return;
            }
            
            demoDataFile = dataFile;
            Debug.Log($"[BoostOpsSDKInternal] Demo data file configured: {dataFile}");
        }
        
        #endregion
        
        #region Initialization Logic
        
        /// <summary>
        /// Initialize source project ID from project settings
        /// This runs once at SDK startup and caches the ID for the entire session
        /// </summary>
        private void InitializeSourceProjectId()
        {
            try
            {
                var projectSettings = InternalSettingsCache.GetProjectSettings();
                if (projectSettings == null)
                {
                    BoostOpsLogger.LogWarning("SDKInternal", "⚠️ Project settings not available - source_project_id will not be set");
                    return;
                }
                
                // Debug.Log($"[SDKInternal] Project settings retrieved - ProjectId: '{projectSettings.ProjectId}', ProjectKey: '{projectSettings.ProjectKey}' (length: {projectSettings.ProjectKey?.Length ?? 0})");
                
                string projectId = projectSettings.ProjectId;
                if (!string.IsNullOrEmpty(projectId))
                {
                    BoostOpsAnalyticsContract.SetSourceProjectId(projectId);
                    // BoostOpsLogger.LogInfo("SDKInternal", $"✅ Source project ID initialized from project settings: {projectId}");
                }
                else
                {
                    BoostOpsLogger.LogWarning("SDKInternal", "⚠️ Project ID not configured in project settings - source_project_id will not be set");
                    BoostOpsLogger.LogWarning("SDKInternal", "💡 Register your project in the BoostOps Editor Window to get a project ID");
                    BoostOpsLogger.LogWarning("SDKInternal", "💡 Check Assets/BoostOps/Resources/BoostOps/BoostOpsProjectSettings.asset in the Inspector");
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("SDKInternal", $"Failed to initialize source project ID: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Initialize SDK with sophisticated mode detection and Unity Services integration
        /// </summary>
        public async void Init(Action<BoostOps.Internal.InitResult> callback = null)
        {
            try
            {
                // BoostOpsLogger.LogInfo("SDKInternal", "Starting SDK initialization...");
                
                // Initialize source_project_id from project settings (compile-time value)
                InitializeSourceProjectId();
                
                // Determine initialization mode based on configuration
                string mode = DetermineInitializationMode();
                // BoostOpsLogger.LogDebug("SDKInternal", $"Initialization mode determined: {mode}");
                
                // Handle different initialization paths
                switch (mode)
                {
                    case "Demo":
                        InitializeDemoMode(callback);
                        break;
                        
                    case "LocalOnly":
                    case "LocalForced":
                        InitializeLocalMode(callback, mode);
                        break;
                        
                    case "Managed":
                        await InitializeManagedMode(callback);
                        break;
                        
                    default:
                        throw new Exception($"Unknown initialization mode: {mode}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] Initialization failed: {ex.Message}");
                OnInitFailed?.Invoke(new InitError { Message = ex.Message, Code = "INIT_EXCEPTION", InnerException = ex });
                callback?.Invoke(new BoostOps.Internal.InitResult { Success = false, ErrorMessage = ex.Message });
            }
        }
        
        /// <summary>
        /// Determine which initialization mode to use based on configuration
        /// </summary>
        private string DetermineInitializationMode()
        {
            BoostOpsLogger.LogDebug("SDKInternal", "Determining initialization mode...");
            
            // Check for demo data file first
            bool hasDemoDataFile = !string.IsNullOrEmpty(demoDataFile);
            if (hasDemoDataFile)
            {
                BoostOpsLogger.LogInfo("SDKInternal", "Demo data file detected - using Demo mode");
                return "Demo";
            }
            
            // Check project settings
            var projectSettings = InternalSettingsCache.GetProjectSettings();
            
            // Enhanced debugging for project settings
            BoostOpsLogger.LogDebug("SDKInternal", $"🔍 Project Settings Debug:");
            BoostOpsLogger.LogDebug("SDKInternal", $"  - projectSettings object: {(projectSettings != null ? "EXISTS" : "NULL")}");
            if (projectSettings != null)
            {
                BoostOpsLogger.LogDebug("SDKInternal", $"  - projectKey value: '{projectSettings.ProjectKey}'");
                BoostOpsLogger.LogDebug("SDKInternal", $"  - projectKey IsNullOrEmpty: {string.IsNullOrEmpty(projectSettings.ProjectKey)}");
                BoostOpsLogger.LogDebug("SDKInternal", $"  - projectKey Length: {projectSettings.ProjectKey?.Length ?? 0}");
                BoostOpsLogger.LogDebug("SDKInternal", $"  - useRemoteManagement: {projectSettings.UseRemoteManagement}");
            }
            
            bool hasProjectKey = !string.IsNullOrEmpty(projectSettings?.ProjectKey);
            bool forceLocalMode = !(projectSettings?.UseRemoteManagement == true);
            
            BoostOpsLogger.LogDebug("SDKInternal", $"Project key present: {hasProjectKey}");
            BoostOpsLogger.LogDebug("SDKInternal", $"Force local mode: {forceLocalMode}");
            
            if (!hasProjectKey || forceLocalMode)
            {
                return forceLocalMode ? "LocalForced" : "LocalOnly";
            }
            
            return "Managed";
        }
        
        /// <summary>
        /// Initialize in demo mode with local data file
        /// </summary>
        private void InitializeDemoMode(Action<BoostOps.Internal.InitResult> callback)
        {
            Debug.Log("[BoostOpsSDKInternal] Initializing in demo mode...");
            
            managerInternal.SetLocalOnlyMode(true);
            
            try
            {
                // Load demo data
                string filePath = System.IO.Path.Combine(Application.dataPath, "BoostOps", demoDataFile);
                Debug.Log($"[BoostOpsSDKInternal] Loading demo data from: {filePath}");
                
                // Use manager's local initialization
                managerInternal.InitializeLocalOnly();
                
                // Track app open event (industry standard with first session flag)
                TrackAppOpenWithFirstSessionDetection("demo_mode");
                
                isInitialized = true;
                _sdkInitializedStatic = true;
                OnInitSuccess?.Invoke();
                
                callback?.Invoke(new BoostOps.Internal.InitResult
                {
                    Success = true,
                    Mode = "Demo",
                    CampaignCount = managerInternal.CampaignCount,
                    ErrorMessage = null
                });
                
                Debug.Log("[BoostOpsSDKInternal] ✅ Demo mode initialization complete");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] Demo mode initialization failed: {ex.Message}");
                OnInitFailed?.Invoke(new InitError { Message = $"Failed to load demo data: {ex.Message}", Code = "DEMO_LOAD_ERROR" });
                callback?.Invoke(new BoostOps.Internal.InitResult { Success = false, ErrorMessage = ex.Message });
            }
        }
        
        /// <summary>
        /// Initialize in local-only mode (no server)
        /// </summary>
        private void InitializeLocalMode(Action<BoostOps.Internal.InitResult> callback, string mode)
        {
            BoostOpsLogger.LogInfo("SDKInternal", $"Initializing in {mode} mode...");
            
            managerInternal.SetLocalOnlyMode(true);
            
            try
            {
                // Subscribe to manager events
                SubscribeToManagerEvents();
                
                // Initialize manager synchronously using Resources
                managerInternal.InitializeLocalOnly();
                
                // Track app open event (industry standard with first session flag)
                TrackAppOpenWithFirstSessionDetection("local_mode");
                
                isInitialized = true;
                _sdkInitializedStatic = true;
                _sdkLocalModeStatic = true;
                OnInitSuccess?.Invoke();
                
                callback?.Invoke(new BoostOps.Internal.InitResult
                {
                    Success = true,
                    Mode = mode,
                    CampaignCount = managerInternal.CampaignCount,
                    ErrorMessage = null
                });
                
                Debug.Log($"[BoostOpsSDKInternal] ✅ {mode} initialization complete");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] {mode} initialization failed: {ex.Message}");
                OnInitFailed?.Invoke(new InitError { Message = ex.Message, Code = "LOCAL_INIT_ERROR" });
                callback?.Invoke(new BoostOps.Internal.InitResult { Success = false, ErrorMessage = ex.Message });
            }
        }
        
        /// <summary>
        /// Initialize in managed mode with server integration
        /// </summary>
        private async System.Threading.Tasks.Task InitializeManagedMode(Action<BoostOps.Internal.InitResult> callback)
        {
            // Debug.Log("[BoostOpsSDKInternal] Initializing in managed mode with server integration...");
            
            try
            {
                // Initialize Unity Services via reflection to avoid hard dependency
                var unityServicesType = System.Type.GetType("Unity.Services.Core.UnityServices, Unity.Services.Core");
                if (unityServicesType != null)
                {
                    var stateProperty = unityServicesType.GetProperty("State", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var currentState = stateProperty?.GetValue(null);
                    if (currentState?.ToString() != "Initialized")
                    {
                        var initMethod = unityServicesType.GetMethod("InitializeAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, System.Type.EmptyTypes, null);
                        if (initMethod != null)
                        {
                            var initTask = initMethod.Invoke(null, null) as System.Threading.Tasks.Task;
                            if (initTask != null) await initTask;
                        }
                    }
                }
                
                // Subscribe to manager events
                SubscribeToManagerEvents();
                
                // Initialize Install Attribution (creates native Install Referrer plugin on Android)
                InitializeInstallAttribution();
                
                // Send app open event immediately with first session detection (industry standard)
                // Uses hardcoded endpoint for first session, normal routing for subsequent opens
                TrackAppOpenWithFirstSessionDetection(GetLaunchType());
                
                // Initialize manager asynchronously via internal manager
                bool success = await managerInternal.InitializeManagedAsync();
                
                if (success)
                {
                    isInitialized = true;
                    _sdkInitializedStatic = true;
                    
                    OnInitSuccess?.Invoke();
                    
                    // ✅ DELAYED AUTO-INITIALIZE: Revenue tracker for automatic purchase detection
                    // NOTE: Delayed by 2 seconds to avoid iOS StoreKit startup crashes
                    // StoreKit observers MUST NOT be added during app startup - causes deadlocks/crashes
                    InitializeRevenueTrackerDelayed();
                    
                    callback?.Invoke(new BoostOps.Internal.InitResult
                    {
                        Success = true,
                        Mode = "Managed",
                        CampaignCount = managerInternal.CampaignCount,
                        ErrorMessage = null
                    });
                    
                    // Debug.Log("[BoostOpsSDKInternal] ✅ Managed mode initialization complete");
                }
                else
                {
                    throw new Exception("Manager initialization failed");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] Managed mode initialization failed: {ex.Message}");
                OnInitFailed?.Invoke(new InitError { Message = ex.Message, Code = "MANAGED_INIT_ERROR" });
                callback?.Invoke(new BoostOps.Internal.InitResult { Success = false, ErrorMessage = ex.Message });
            }
        }
        
        /// <summary>
        /// Subscribe to manager events and forward them to SDK events
        /// </summary>
        private void SubscribeToManagerEvents()
        {
            // Subscribe to manager events and forward them to public SDK events
            if (managerInternal != null)
            {
                managerInternal.OnCampaignsLoaded += () => {
                    Debug.Log("[BoostOpsSDKInternal] Campaigns loaded - firing OnCampaignsReady event");
                    OnCampaignsReady?.Invoke();
                };
            }
            // Debug.Log("[BoostOpsSDKInternal] Event subscription handled by public SDK layer");
        }
        
        /// <summary>
        /// Track install event (first launch only) and app open event
        /// DEPRECATED: Use TrackAppOpenWithFirstSessionDetection instead (industry standard)
        /// </summary>
        [System.Obsolete("Use TrackAppOpenWithFirstSessionDetection instead")]
        private static void TrackInstallAndAppOpenEvents(string launchType)
        {
            try
            {
                // Check if this is the first launch using consistent key
                int firstLaunchValue = PlayerPrefs.GetInt(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED, 0);
                bool isFirstLaunch = firstLaunchValue == 0;
                
                // CRITICAL DEBUG: Log first launch detection to diagnose if first_open is being set incorrectly
                Debug.Log($"[BoostOpsSDKInternal] 🔍 FIRST LAUNCH CHECK: PlayerPrefs['{BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED}'] = {firstLaunchValue}, isFirstLaunch = {isFirstLaunch}");
                
                if (isFirstLaunch)
                {
                    // Mark as no longer first launch
                    // Note: Install tracking now handled via TrackAppOpen with isFirstSession=true (industry standard)
                    PlayerPrefs.SetInt(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED, 1);
                    PlayerPrefs.Save();
                    Debug.Log($"[BoostOpsSDKInternal] 🎯 First launch detected - will track as first session in app open event");
                }
                
                // Industry standard: Single app open event with first session flag
                BoostOpsAnalyticsContract.TrackAppOpen(
                    launchType: launchType, 
                    isFirstSession: isFirstLaunch ? true : (bool?)null,  // Only set true for first session
                    organic: null,        // Let server determine organic vs attributed based on touch history
                    reinstall: null,      // Let server determine reinstall status
                    forceManagedMode: true  // Always use managed mode for immediate sending
                );
                
                if (isFirstLaunch)
                {
                    Debug.Log($"[BoostOpsSDKInternal] ✅ First session app open event sent immediately (includes install attribution)");
                }
                else
                {
                    Debug.Log($"[BoostOpsSDKInternal] ✅ Regular app open event sent for {launchType} session");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ⚠️ App open tracking failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Track app open event with first session detection (industry standard approach)
        /// Uses single event type with is_first_session flag instead of separate install/open events
        /// 
        /// ANDROID: For first launch, waits up to 2 seconds for Install Referrer data (AppsFlyer/Adjust/Branch approach)
        /// iOS: Sends immediately (no Install Referrer API - uses deep links/ASA instead)
        /// For REGULAR LAUNCHES: Sends immediately (no delay)
        /// </summary>
        private static void TrackAppOpenWithFirstSessionDetection(string launchType)
        {
            try
            {
                // Check if this is the first launch using consistent key
                bool isFirstLaunch = PlayerPrefs.GetInt(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED, 0) == 0;
                
#if UNITY_ANDROID && !UNITY_EDITOR
                // ANDROID ONLY: Wait for Install Referrer on first launch
                if (isFirstLaunch)
                {
                    // FIRST LAUNCH: Wait for install referrer data before sending event
                    Debug.Log($"[BoostOpsSDKInternal] 🎯 Android first launch - waiting for install referrer (up to 2s)...");
                    
                    // Start coroutine via registered coroutine runner (set by BoostOpsManager)
                    if (_coroutineRunner != null)
                    {
                        _coroutineRunner.StartCoroutine(WaitForInstallReferrerThenTrackAppOpen(launchType, isFirstLaunch));
                    }
                    else
                    {
                        Debug.LogWarning($"[BoostOpsSDKInternal] ⚠️ Coroutine runner not available - sending first launch event immediately");
                        SendAppOpenEventImmediate(launchType, isFirstLaunch);
                    }
                }
                else
                {
                    // REGULAR LAUNCH: Send immediately (no referrer needed)
                    SendAppOpenEventImmediate(launchType, isFirstLaunch);
                }
#else
                // iOS / Editor / Other platforms: Always send immediately
                // iOS attribution comes from deep links, Apple Search Ads, SKAdNetwork
                SendAppOpenEventImmediate(launchType, isFirstLaunch);
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ⚠️ App open tracking failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Coroutine: Wait for Install Referrer data before sending first app_open event
        /// Matches AppsFlyer/Adjust/Branch behavior - 2 second timeout
        /// </summary>
        private static System.Collections.IEnumerator WaitForInstallReferrerThenTrackAppOpen(string launchType, bool isFirstLaunch)
        {
            const float TIMEOUT = 2f;
            float elapsed = 0f;
            bool referrerReceived = false;
            
            Debug.Log($"[BoostOpsSDKInternal] ⏱️ Waiting for install referrer (timeout: {TIMEOUT}s)...");
            
            // Wait for install referrer (or timeout)
            while (elapsed < TIMEOUT)
            {
                string referrer = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_RAW, null);
                if (!string.IsNullOrEmpty(referrer))
                {
                    referrerReceived = true;
                    Debug.Log($"[BoostOpsSDKInternal] ✅ Install referrer received after {elapsed:F2}s: {referrer}");
                    break;
                }
                
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (!referrerReceived)
            {
                Debug.Log($"[BoostOpsSDKInternal] ⏱️ Install referrer timeout ({TIMEOUT}s) - sending event without referrer (organic install)");
            }
            
            // Now send app_open event (with or without referrer data)
            SendAppOpenEventImmediate(launchType, isFirstLaunch);
        }
        
        /// <summary>
        /// Send app_open event immediately (internal helper)
        /// </summary>
        private static void SendAppOpenEventImmediate(string launchType, bool isFirstLaunch)
        {
            try
            {
                // Check if a deep link was captured
                string deeplinkUrl = BoostOps.BoostOpsDeepLinkProtection.CapturedDeepLink;
                
                // DEBUG: Record this call for double-send detection
                AppOpenEventDebugger.RecordCall(launchType, isFirstLaunch, "SendAppOpenEventImmediate");
                
                // Debug.Log($"[BoostOpsSDKInternal] 🚀🚀🚀 SDK TRACKING APP OPEN - launchType: {launchType}, isFirstSession: {isFirstLaunch}, deeplink: {deeplinkUrl ?? "none"}, hasOpenedInCurrentSession: {hasOpenedInCurrentSession}");
                
                // Industry standard: Single app open event with first session flag
                BoostOpsAnalyticsContract.TrackAppOpen(
                    launchType: launchType, 
                    deeplinkUrl: deeplinkUrl,  // Include deep link if captured
                    isFirstSession: isFirstLaunch ? true : (bool?)null,  // Only set true for first session
                    organic: null,        // Let server determine organic vs attributed based on touch history
                    reinstall: null,      // Let server determine reinstall status
                    forceManagedMode: true  // Always use managed mode for immediate sending
                );
                
                // Record that app_open was sent to prevent duplicate from lifecycle handlers
                BoostOps.Analytics.BoostOpsAnalyticsClient.RecordAppOpenSent();
                
                if (isFirstLaunch)
                {
                    // Debug.Log($"[BoostOpsSDKInternal] ✅ First session app open event sent (includes install attribution)");
                    
                    // Mark as no longer first launch
                    PlayerPrefs.SetInt(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED, 1);
                    PlayerPrefs.Save();
                }
                // else
                // {
                //     Debug.Log($"[BoostOpsSDKInternal] ✅ Regular app open event sent with launch_type={launchType}");
                // }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ⚠️ App open event send failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Determine the launch type based on app state (industry standard)
        /// Returns "cold" for first open in memory, "warm" for subsequent opens
        /// Public so other internal classes (like dynamic links) can use the same logic
        /// </summary>
        internal static string GetLaunchType()
        {
            // Check if this is the first open in this memory session
            if (!hasOpenedInCurrentSession)
            {
                // First time app opened since loaded into memory = cold start
                // Debug.Log($"[BoostOpsSDKInternal] ❄️ GetLaunchType() → COLD (first call, setting flag)");
                hasOpenedInCurrentSession = true;
                return "cold";
            }
            
            // App was already opened before in this session = warm start (resume from background)
            // Debug.Log($"[BoostOpsSDKInternal] 🔥 GetLaunchType() → WARM (flag already set)");
            return "warm";
        }
        
        /// <summary>
        /// Initialize revenue tracker with delay to avoid iOS StoreKit startup crashes
        /// 
        /// CRITICAL: StoreKit observers MUST NOT be added during app startup - causes deadlocks/crashes
        /// Industry standard: 2-second delay (matches Branch, Singular, Facebook, AppsFlyer)
        /// 
        /// See: Documentation/IOS_STOREKIT_CRASH_FIX.md
        /// </summary>
        private static void InitializeRevenueTrackerDelayed()
        {
            #if UNITY_EDITOR
            // Editor: Initialize immediately (no native issues)
            InitializeRevenueTrackerImmediate();
            
            #elif UNITY_IOS
            // iOS Device: MUST delay by 2 seconds to ensure StoreKit is fully initialized
            // Attempting to add SKPaymentQueue observer too early causes app crashes/deadlocks
            try
            {
                if (_coroutineRunner != null)
                {
                    _coroutineRunner.StartCoroutine(InitializeRevenueTrackerCoroutine(2.0f));
                }
                else
                {
                    Debug.LogError("[BoostOpsSDKInternal] ❌ Coroutine runner not available - skipping receipt capture initialization");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ❌ Failed to start receipt capture coroutine: {ex.Message}");
            }
            
            #else
            // Android/Other: Initialize immediately (no StoreKit issues)
            InitializeRevenueTrackerImmediate();
            #endif
        }
        
        /// <summary>
        /// Initialize Install Attribution system (creates native Install Referrer + subscribes to save data)
        /// Critical for Android Install Referrer API to work
        /// </summary>
        private static void InitializeInstallAttribution()
        {
            try
            {
                // Check if native Install Referrer component already exists
                var existingNative = UnityEngine.Object.FindFirstObjectByType<BoostOps.BoostOpsInstallReferrerNative>();
                if (existingNative != null)
                {
                    Debug.Log("[BoostOpsSDKInternal] ✅ BoostOpsInstallReferrerNative already exists");
                    return;
                }
                
                // Get project settings for API key
                var settings = InternalSettingsCache.GetProjectSettings();
                string projectKey = settings?.ProjectKey ?? "boostops_install_referrer"; // Fallback key
                
                // Create the native Install Referrer component
                var go = new UnityEngine.GameObject("BoostOpsInstallReferrerNative");
                var nativeReferrer = go.AddComponent<BoostOps.BoostOpsInstallReferrerNative>();
                UnityEngine.Object.DontDestroyOnLoad(go);
                
                // Subscribe to event BEFORE initializing (so we catch the callback)
                BoostOps.BoostOpsInstallReferrerNative.OnInstallReferrerReceived += OnInstallReferrerReceived;
                
                // Initialize with API key (triggers connection to Play Store)
                nativeReferrer.Initialize(projectKey);
                
                // Debug.Log($"[BoostOpsSDKInternal] ✅ BoostOpsInstallReferrerNative created with event handler - Install Referrer API ready");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ⚠️ Failed to create Install Referrer component: {ex.Message}");
                // Non-critical - continue without it
            }
        }
        
        /// <summary>
        /// Handle Install Referrer data and save to PlayerPrefs (so wait loop can detect it)
        /// </summary>
        private static void OnInstallReferrerReceived(BoostOps.InstallReferrerData referrerData)
        {
            try
            {
                Debug.Log($"[BoostOpsSDKInternal] 📥 Install referrer data received - saving to PlayerPrefs");
                
                // Save to PlayerPrefs so our wait loop can detect it
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_RAW, referrerData.RawReferrer ?? "");
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_PROCESSED, DateTime.UtcNow.ToString("O"));
                
                // Save timestamps
                long clickTs = ((DateTimeOffset)referrerData.ClickTimestamp).ToUnixTimeSeconds();
                long installBeginTs = ((DateTimeOffset)referrerData.InstallTimestamp).ToUnixTimeSeconds();
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_CLICK_TS, clickTs.ToString());
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_REFERRER_INSTALL_BEGIN_TS, installBeginTs.ToString());
                
                PlayerPrefs.Save();
                
                Debug.Log($"[BoostOpsSDKInternal] ✅ Install referrer saved: {referrerData.RawReferrer}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ⚠️ Failed to save install referrer: {ex.Message}");
            }
        }
        
        private static System.Collections.IEnumerator InitializeRevenueTrackerCoroutine(float delaySeconds)
        {
            Debug.Log($"[BoostOpsSDKInternal] ⏳ Delaying receipt capture initialization by {delaySeconds}s (iOS StoreKit safety - prevents app crashes)");
            
            // Wait for StoreKit to fully initialize
            // Industry research shows 2 seconds is sufficient for all iOS versions
            yield return new UnityEngine.WaitForSeconds(delaySeconds);
            
            Debug.Log("[BoostOpsSDKInternal] ⏰ Delay complete - initializing receipt capture now");
            InitializeRevenueTrackerImmediate();
        }
        
        private static void InitializeRevenueTrackerImmediate()
        {
            try
            {
                // Initialize native receipt capture (iOS StoreKit, Android Google Play)
                BoostOpsReceiptCaptureNative.Initialize();
                // Debug.Log("[BoostOpsSDKInternal] ✅ Receipt capture initialized - automatic purchase enrichment active");
                
                // Initialize revenue tracker (attribution tracking only now)
                BoostOpsRevenueTracker.Initialize();
                // Debug.Log("[BoostOpsSDKInternal] ✅ Revenue tracker initialized - attribution tracking active");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOpsSDKInternal] ⚠️ Receipt capture initialization failed: {ex.Message}");
                // Non-critical - continue without receipt capture
            }
        }
        
        /// <summary>
        /// Send install event immediately (if first launch) and queue app open event for later sending
        /// DEPRECATED: Use TrackAppOpenWithFirstSessionDetection instead (industry standard)
        /// </summary>
        [System.Obsolete("Use TrackAppOpenWithFirstSessionDetection instead")]
        private void SendInstallAndQueueAppOpenEvent(string launchType)
        {
            try
            {
                Debug.Log($"[BoostOpsSDKInternal] 📝 Processing install and queuing app open event...");
                
                // Check if this is the first launch and send install event immediately
                bool isFirstLaunch = PlayerPrefs.GetInt(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED, 0) == 0;
                
                if (isFirstLaunch)
                {
                    // Mark as no longer first launch
                    // Note: Install tracking now handled via TrackAppOpen with isFirstSession=true (industry standard)
                    PlayerPrefs.SetInt(BoostOpsPlayerPrefsKeys.FIRST_LAUNCH_TRACKED, 1);
                    PlayerPrefs.Save();
                    Debug.Log($"[BoostOpsSDKInternal] 🎯 First launch detected - will be tracked as first session in app open event");
                }
                
                // Always queue the app open event for later sending when remote config is available
                QueueAppOpenEventInAnalyticsClient(launchType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ⚠️ Event processing failed: {ex.Message}");
                // Fallback: track immediately
                TrackInstallAndAppOpenEvents(launchType);
            }
        }
        
        /// <summary>
        /// Queue app open event in the BoostOps Analytics Client queue
        /// </summary>
        private static void QueueAppOpenEventInAnalyticsClient(string launchType)
        {
            try
            {
                Debug.Log($"[BoostOpsSDKInternal] 📝 Queuing app open event in analytics client for launch type: {launchType}");
                
                // Create parameters for the app open event
                var parameters = new Dictionary<string, string>
                {
                    ["launch_type"] = launchType ?? "cold"
                };
                
                // Queue the event in the analytics client - it will be sent when the queue is flushed
                BoostOpsAnalyticsContract.QueueAnalyticsEvent("boostops_open", parameters);
                
                Debug.Log($"[BoostOpsSDKInternal] ✅ App open event queued in analytics client");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ❌ Failed to queue app open event in analytics client: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process queued analytics events when remote config is loaded
        /// Either send events (if analytics enabled) or clear them (if disabled)
        /// </summary>
        public static void ProcessQueuedAnalyticsEvents()
        {
            try
            {
                // Check if BoostOps Analytics is enabled via remote config
                var analyticsProvider = AnalyticsProviderFactory.GetProvider("BoostOps Analytics") as BoostOpsAnalyticsProvider;
                if (analyticsProvider != null && analyticsProvider.IsAvailable)
                {
                    // Analytics is enabled - send all queued events
                    BoostOpsAnalyticsContract.FlushAnalyticsQueue((success) => {
                        if (success)
                        {
                            // Debug.Log("[BoostOpsSDKInternal] ✅ Analytics queue flushed successfully - all events sent");
                        }
                        else
                        {
                            Debug.LogWarning("[BoostOpsSDKInternal] ⚠️ Analytics queue flush failed");
                        }
                    });
                }
                else
                {
                    
                    // Analytics is disabled - clear all queued events without sending
                    BoostOpsAnalyticsClient.Instance.ClearQueue();
                    Debug.Log("[BoostOpsSDKInternal] 🗑️ Queued events cleared (analytics disabled)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ❌ Failed to process analytics queue: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Cross-Promotion API
        
        /// <summary>
        /// Show cross-promotion with format conversion
        /// </summary>
        public bool ShowCrossPromo(string placement, BoostOps.Internal.PromoFormat format = BoostOps.Internal.PromoFormat.Auto, BoostOps.Internal.PromoOptions opts = null)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[BoostOpsSDKInternal] SDK not initialized. Call Init() first.");
                return false;
            }
            
            if (managerInternal == null)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ❌ managerInternal is null! Cannot show cross promo. SDK Instance: {GetHashCode()}");
                return false;
            }
            
            // Debug.Log($"[BoostOpsSDKInternal] 🎯 ShowCrossPromo called - using managerInternal instance: {managerInternal.GetHashCode()}");
            
            try
            {
                // Convert BoostOps.Internal.PromoFormat to internal display mode
                BoostOpsCampaignDisplay.CampaignDisplayMode displayMode = ConvertPromoFormat(format);
                
                Debug.Log($"[BoostOpsSDKInternal] ShowCrossPromo: {placement}, {format} -> {displayMode}");
                
                // Delegate to internal manager
                return managerInternal.ShowCrossPromo(placement, displayMode);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ShowCrossPromo failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Show app wall using remote config app_walls section
        /// This is the new approach that uses dedicated app wall configuration
        /// </summary>
        public bool ShowAppWall(string placement)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[BoostOpsSDKInternal] SDK not initialized. Call Init() first.");
                return false;
            }
            
            if (managerInternal == null)
            {
                Debug.LogError($"[BoostOpsSDKInternal] managerInternal is null! Cannot show app wall.");
                return false;
            }
            
            Debug.Log($"[BoostOpsSDKInternal] ShowAppWall called with app_walls config - placement: {placement}");
            
            try
            {
                // Show app wall using remote config app_walls
                return managerInternal.ShowAppWall(placement);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ShowAppWall (app_walls) failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Show app wall with multiple campaigns (legacy campaign-based approach)
        /// </summary>
        public bool ShowAppWall(string placement, int maxCampaigns = 12)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[BoostOpsSDKInternal] SDK not initialized. Call Init() first.");
                return false;
            }
            
            if (managerInternal == null)
            {
                Debug.LogError($"[BoostOpsSDKInternal] managerInternal is null! Cannot show app wall.");
                return false;
            }
            
            Debug.Log($"[BoostOpsSDKInternal] ShowAppWall called - placement: {placement}, maxCampaigns: {maxCampaigns}");
            
            try
            {
                // Get eligible campaigns for app wall format
                var campaigns = managerInternal.GetAllCampaigns();
                var eligibleCampaigns = new System.Collections.Generic.List<Campaign>();
                
                foreach (var campaign in campaigns)
                {
                    if (campaign.IsActive && campaign.SupportsFormat("app_wall"))
                    {
                        eligibleCampaigns.Add(campaign);
                        if (eligibleCampaigns.Count >= maxCampaigns)
                            break;
                    }
                }
                
                if (eligibleCampaigns.Count == 0)
                {
                    Debug.LogWarning("[BoostOpsSDKInternal] ShowAppWall: No eligible campaigns for app_wall format");
                    return false;
                }
                
                Debug.Log($"[BoostOpsSDKInternal] Showing app wall with {eligibleCampaigns.Count} campaigns");
                
                // Show app wall via manager (legacy campaign-based approach)
                return managerInternal.ShowAppWallWithCampaigns(eligibleCampaigns, placement);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] ShowAppWall failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Convert public BoostOps.Internal.PromoFormat to internal CampaignDisplayMode
        /// </summary>
        private BoostOpsCampaignDisplay.CampaignDisplayMode ConvertPromoFormat(BoostOps.Internal.PromoFormat format)
        {
            switch (format)
            {
                case BoostOps.Internal.PromoFormat.Banner:
                    return BoostOpsCampaignDisplay.CampaignDisplayMode.Banner;
                case BoostOps.Internal.PromoFormat.Native:
                    return BoostOpsCampaignDisplay.CampaignDisplayMode.Native;
                case BoostOps.Internal.PromoFormat.Icon:
                    return BoostOpsCampaignDisplay.CampaignDisplayMode.IconInterstitial;
                case BoostOps.Internal.PromoFormat.Rich:
                    return BoostOpsCampaignDisplay.CampaignDisplayMode.RichInterstitial;
                case BoostOps.Internal.PromoFormat.Auto:
                default:
                    // Smart selection - default to Rich interstitial
                    return BoostOpsCampaignDisplay.CampaignDisplayMode.RichInterstitial;
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Hide cross-promotion display
        /// </summary>
        public void HideCrossPromo(string placement = null)
        {
            if (!IsInitialized) return;
            
            // TODO: Implement hide logic via manager
            Debug.Log($"[BoostOpsSDKInternal] HideCrossPromo: {placement ?? "all"}");
        }
        
        /// <summary>
        /// Get total impressions for stats
        /// </summary>
        public int GetTotalImpressions()
        {
            if (!IsInitialized) return 0;
            
            // TODO: Get from internal manager analytics
            return 0;
        }
        
        /// <summary>
        /// Get total clicks for stats
        /// </summary>
        public int GetTotalClicks()
        {
            if (!IsInitialized) return 0;
            
            // TODO: Get from internal manager analytics
            return 0;
        }
        
        /// <summary>
        /// Get deep link info (placeholder for removed functionality)
        /// </summary>
        public object GetDeepLinkInfo()
        {
            // Dynamic Links functionality removed - internal functionality only
            return null;
        }
        
        #endregion
        
        #region Asset Loading (Internal Implementation)
        
        /// <summary>
        /// Set the asset loading mode for all asset operations
        /// Internal implementation that delegates to AssetResolver
        /// </summary>
        /// <param name="onlineMode">True for online mode, false for offline mode</param>
        public void SetAssetLoadMode(bool onlineMode)
        {
            var mode = onlineMode ? AssetResolver.AssetLoadMode.Online : AssetResolver.AssetLoadMode.Offline;
            AssetResolver.SetAssetLoadMode(mode);
            Debug.Log($"[BoostOpsSDKInternal] Asset load mode set to: {mode}");
        }

        /// <summary>
        /// Preload all assets for a specific campaign to avoid jitter during first show
        /// Internal implementation that handles AssetResolver logic
        /// </summary>
        /// <param name="campaignId">Campaign ID to preload assets for</param>
        /// <returns>True if assets were preloaded successfully</returns>
        public async System.Threading.Tasks.Task<bool> PreloadCampaignAssetsAsync(string campaignId)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[BoostOpsSDKInternal] Cannot preload assets - SDK not initialized");
                return false;
            }

            if (managerInternal == null)
            {
                Debug.LogError("[BoostOpsSDKInternal] Internal manager not available");
                return false;
            }

            try
            {
                Debug.Log($"[BoostOpsSDKInternal] Preloading assets for campaign: {campaignId}");
                
                // Find the campaign using internal manager
                var allCampaigns = managerInternal.GetAllCampaigns();
                var targetCampaign = allCampaigns.FirstOrDefault(c => c.CampaignId == campaignId);
                
                if (targetCampaign == null)
                {
                    Debug.LogWarning($"[BoostOpsSDKInternal] Campaign not found for preloading: {campaignId}");
                    return false;
                }

                // Preload campaign assets using internal AssetResolver
                await AssetResolver.PreloadAssetsAsync(targetCampaign.Creatives);
                
                Debug.Log($"[BoostOpsSDKInternal] Successfully preloaded assets for campaign: {campaignId}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] Error preloading assets for campaign {campaignId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Preload assets for all available campaigns
        /// Internal implementation that handles AssetResolver logic
        /// </summary>
        /// <returns>Number of campaigns with successfully preloaded assets</returns>
        public async System.Threading.Tasks.Task<int> PreloadAllCampaignAssetsAsync()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[BoostOpsSDKInternal] Cannot preload assets - SDK not initialized");
                return 0;
            }

            if (managerInternal == null)
            {
                Debug.LogError("[BoostOpsSDKInternal] Internal manager not available");
                return 0;
            }

            try
            {
                Debug.Log("[BoostOpsSDKInternal] Preloading assets for all campaigns...");
                
                var allCampaigns = managerInternal.GetAllCampaigns();
                int successCount = 0;
                
                foreach (var campaign in allCampaigns)
                {
                    try
                    {
                        // Use internal AssetResolver for each campaign
                        await AssetResolver.PreloadAssetsAsync(campaign.Creatives);
                        successCount++;
                        Debug.Log($"[BoostOpsSDKInternal] Successfully preloaded assets for campaign: {campaign.CampaignId}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[BoostOpsSDKInternal] Error preloading assets for campaign {campaign.CampaignId}: {ex.Message}");
                    }
                }
                
                Debug.Log($"[BoostOpsSDKInternal] Preloaded assets for {successCount}/{allCampaigns.Count} campaigns");
                return successCount;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsSDKInternal] Error during preload all assets: {ex.Message}");
                return 0;
            }
        }
        
        #endregion
    }
}
