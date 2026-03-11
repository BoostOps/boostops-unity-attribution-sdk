using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoostOps
{
    /// <summary>
    /// BoostOps SDK initialization modes for demo
    /// </summary>
    public enum InitializationMode
    {
        /// <summary>Demo mode with canned test campaigns (no external dependencies)</summary>
        DemoMode,
        /// <summary>Client only mode - uses local configuration without server connection</summary>
        ClientOnlyMode,
        /// <summary>Server config mode - connects to BoostOps service with SDK key from settings</summary>
        ServerConfigMode
    }

            /// <summary>
        /// BoostOps Demo that showcases core cross-promotion features
        /// Drop this prefab in your scene to see BoostOps cross-promotion in action
        /// </summary>
        public class BoostOpsDemo : MonoBehaviour
    {
        [Header("Demo Settings")]
        [SerializeField] private InitializationMode initMode = InitializationMode.DemoMode;

        [Header("UI References")]
        [SerializeField] private Canvas mainCanvas; // Demo UI canvas only - BoostOps will create its own overlay
        [SerializeField] private GameObject demoPanel;
        [SerializeField] private Image logoImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text statsText;
        [SerializeField] private ScrollRect logScrollRect;
        [SerializeField] private Text logText;

        [Header("Feature Buttons")]
        [SerializeField] private Button initializeButton;
        [SerializeField] private Button showBannerButton;
        [SerializeField] private Button showRichInterstitialButton;
        [SerializeField] private Button showIconInterstitialButton;
        [SerializeField] private Button showNativeButton;
        // Dynamic Links removed - internal functionality only
        [SerializeField] private Button refreshCampaignsButton;
        [SerializeField] private Button getStatsButton;
        [SerializeField] private Button clearLogButton;
        // Frequency caps button removed - internal functionality only
        [SerializeField] private Button preloadAssetsButton;
        [SerializeField] private Button dllProtectionDemoButton; // Optional: Test DLL protection
        
        // Internal state
        private List<string> logMessages = new List<string>();
        // Note: Stats now read from BoostOpsSDK.GetTotalImpressions() and BoostOpsSDK.GetTotalClicks()
        
        void Start()
        {
            BoostOpsLogger.LogDebug("Demo", "Demo starting - setting up UI");
            SubscribeToEvents();
            
            // Note: DLL protection demo moved to separate button
            // DemonstrateDLLProtection(); // ← Removed from Start() - use button instead for development
            
            // Note: Auto-initialization removed - use "Initialize SDK" button instead
            // This allows testing different initialization modes via the UI
            
            BoostOpsLogger.LogDebug("Demo", "Demo ready - BoostOps initialization started");

            if (showRichInterstitialButton) showRichInterstitialButton.interactable = false;
            if (showIconInterstitialButton) showIconInterstitialButton.interactable = false;
            // Frequency caps button removed - internal functionality only
            
        }
        
        // Note: InitializeBoostOpsAsync() removed - demo now uses manual initialization via button
        // This ensures proper testing of different initialization modes and prevents conflicts
        
        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        #region UI Setup
        
        void UpdateUI()
        {
            bool sdkInitialized = BoostOpsSDK.IsInitialized;
            
            // Update status text
            if (statusText)
            {
                if (sdkInitialized)
                {
                    int campaignCount = BoostOpsSDK.GetCampaignCount();
                    statusText.text = $"Status: ✅ Ready ({campaignCount} campaigns)";
                }
                else
                {
                    statusText.text = "Please click 'Init' to initialize BoostOps™ and enable cross-promotion features";
                }
            }
            
            // Update stats with impression counts
            if (statsText)
            {
                // Read real stats from BoostOpsSDK instead of local variables
                int totalImpressions = BoostOpsSDK.GetTotalImpressions();
                int totalClicks = BoostOpsSDK.GetTotalClicks();
                var basicStats = $"Impressions: {totalImpressions} | Clicks: {totalClicks}";
                
                // Frequency cap info removed - internal functionality only
                
                statsText.text = basicStats;
            }
            
            // Simple button state: enabled only if SDK is initialized
            bool buttonsEnabled = sdkInitialized;
            
            // Enable/disable buttons
            if (showBannerButton) showBannerButton.interactable = buttonsEnabled;
            if (showRichInterstitialButton) showRichInterstitialButton.interactable = buttonsEnabled;
            if (showIconInterstitialButton) showIconInterstitialButton.interactable = buttonsEnabled;
            if (showNativeButton) showNativeButton.interactable = buttonsEnabled;
            // Dynamic Links button removed - internal functionality only
            if (refreshCampaignsButton) refreshCampaignsButton.interactable = buttonsEnabled;
            if (getStatsButton) getStatsButton.interactable = buttonsEnabled;
            if (preloadAssetsButton) preloadAssetsButton.interactable = buttonsEnabled;
            
            // Frequency caps button removed - internal functionality only
            
            // Initialize button is only enabled when SDK is NOT initialized
            if (initializeButton) initializeButton.interactable = !sdkInitialized;
        }
        
        #endregion
        
        #region Event Handling
        
        void SubscribeToEvents()
        {
            BoostOpsSDK.OnInitSuccess += OnInitSuccess;
            BoostOpsSDK.OnInitFailed  += OnInitFailed;
            BoostOpsSDK.OnCampaignImpression += OnCampaignImpression;
            BoostOpsSDK.OnCampaignClick += OnCampaignClick;
            
            // Dynamic Links events removed - internal functionality only
        }
        
        void UnsubscribeFromEvents()
        {
            BoostOpsSDK.OnInitSuccess -= OnInitSuccess;
            BoostOpsSDK.OnInitFailed  -= OnInitFailed;
            BoostOpsSDK.OnCampaignImpression -= OnCampaignImpression;
            BoostOpsSDK.OnCampaignClick -= OnCampaignClick;
            
            // Dynamic Links events removed - internal functionality only
        }
        
        void OnInitSuccess()
        {
            AddLog("✅ SDK Initialized Successfully!");
            UpdateUI();
        }

        void OnInitFailed(InitError error)
        {
            AddLog($"❌ SDK Initialized Failed! {error.Message}");
            UpdateUI();
        }
        
        // OnCampaignsLoaded method removed - event no longer exists in BoostOpsManager
        
        void OnCampaignImpression(CampaignInfo campaign)
        {
            // Note: Real impression tracking handled by BoostOpsCore
            AddLog($"👁️ Impression: {campaign.Name}");
            UpdateUI();
        }
        
        void OnCampaignClick(CampaignInfo campaign)
        {
            // Note: Real click tracking handled by BoostOpsCore  
            AddLog($"🖱️ Click: {campaign.Name}");
            UpdateUI();
        }
        
        void OnSDKError(string error)
        {
            AddLog($"❌ SDK Error: {error}");
            UpdateUI();
        }
        
        // Dynamic Links event handlers removed - internal functionality only
        
        #endregion
        
        #region Button Handlers
        
        public async void OnInitializeClicked()
        {
            Debug.Log("[BoostOpsDemo] Initialize SDK button clicked");
            
            // Guard against double initialization
            if (BoostOpsSDK.IsInitialized)
            {
                AddLog("⚠️ SDK is already initialized!");
                return;
            }
            
            AddLog("🔧 Initializing Unity Services...");
            try
            {
                await InitUnityServicesViaReflection();
                AddLog("✅ Unity Services initialized successfully");
            }
            catch (System.Exception ex)
            {
                AddLog($"⚠️ Unity Services initialization skipped: {ex.Message}");
            }
            
            // Set up custom prefabs before SDK initialization
            SetupCustomPrefabs();
            
            // Configure initialization based on selected mode
            if (initMode == InitializationMode.DemoMode)
            {
                Debug.Log("[BoostOpsDemo] Setting demo data file path");
                BoostOpsSDK.SetDemoDataFile("CrossPromo/demo_campaigns.json");
                AddLog("🎮 Demo Mode: Using static demo campaigns file");
            }
            else if (initMode == InitializationMode.ClientOnlyMode)
            {
                Debug.Log("[BoostOpsDemo] Using client only mode with local configuration");
                AddLog("📱 Client Only Mode: Using local configuration without server connection");
            }
            else if (initMode == InitializationMode.ServerConfigMode)
            {
                Debug.Log("[BoostOpsDemo] Using server config mode - project key automatically configured");
                AddLog("🌐 Server Config Mode: Using project key from BoostOps Project Settings");
            }
            
            if (initMode == InitializationMode.ServerConfigMode)
            {
                AddLog("🌐 Fetching remote config...");
                
                try
                {
                    bool fetched = await FetchRemoteConfigViaReflection();
                    if (fetched)
                    {
                        AddLog("✅ Remote config fetched successfully");
                    }
                    else
                    {
                        AddLog("⚠️ Remote config not available (no supported provider found)");
                    }
                }
                catch (System.Exception ex)
                {
                    AddLog($"❌ Failed to fetch remote config: {ex.Message}");
                }
            }
            
            // Initialize SDK
            BoostOpsLogger.LogDebug("Demo", "Starting SDK initialization");
            AddLog("🔄 Initializing SDK...");
            
            BoostOpsSDK.Init(result => {
                if (result.Success)
                {
                    AddLog($"✅ SDK Initialized! Mode: {result.Mode}, Campaigns: {result.CampaignCount}");
                    BoostOpsLogger.LogDebug("Demo", "SDK initialization successful");
                }
                else
                {
                    AddLog($"❌ SDK Init failed: {result.ErrorMessage}");
                    Debug.Log($"[BoostOpsDemo] SDK initialization failed: {result.ErrorMessage}");
                }
                UpdateUI();
            });
        }
          
        public void OnShowIconInterstitialClicked()
        {
            AddLog("🎯 Showing interstitial cross-promotion...");
            
            if (BoostOpsSDK.IsInitialized)
            {
                // Only show campaigns if SDK is properly initialized
                bool success = BoostOpsSDK.ShowCrossPromo("demo_interstitial", PromoFormat.Icon);
                if (success)
                {
                    AddLog("✅ Icon Interstitial cross-promotion displayed successfully");
                    // Removed manual impressionCount++ - handled by OnCampaignImpression event
                    UpdateUI();
                }
                else
                {
                    AddLog("❌ Failed to show interstitial - no campaigns available");
                }
            }
            else
            {
                AddLog("❌ SDK not initialized - please initialize the SDK first");
            }
        }
        
        // Frequency cap management removed - internal functionality only
        
        void OnRefreshCampaignsClicked()
        {
            AddLog("🔄 Refreshing campaigns...");
            
            if (BoostOpsSDK.IsInitialized)
            {
                // Re-initialize to refresh campaigns
                BoostOpsSDK.Init(result => {
                    if (result.Success)
                    {
                        AddLog($"✅ Campaigns refreshed! Found {result.CampaignCount} campaigns");
                        UpdateUI();
                    }
                    else
                    {
                        AddLog($"❌ Failed to refresh campaigns: {result.ErrorMessage}");
                    }
                });
            }
            else
            {
                AddLog("❌ SDK not initialized! Click Initialize SDK first.");
            }
        }
        
        void OnClearLogClicked()
        {
            logMessages.Clear();
            UpdateLogDisplay();
            AddLog("🗑️ Log cleared");
        }
        
        #endregion
        
        
        #region Helper Methods
        
        void AddLog(string message)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            logMessages.Add($"[{timestamp}] {message}");
            
            // Keep only last 50 messages
            if (logMessages.Count > 50)
            {
                logMessages.RemoveAt(0);
            }
            
            UpdateLogDisplay();
        }
        
        void UpdateLogDisplay()
        {
            if (logText)
            {
                logText.text = string.Join("\n", logMessages);
                
                // Auto-scroll to bottom if we have a scroll rect
                var scrollRect = logText.GetComponentInParent<ScrollRect>();
                if (scrollRect)
                {
                    StartCoroutine(ScrollToBottom(scrollRect));
                }
            }
        }
        
        System.Collections.IEnumerator ScrollToBottom(ScrollRect scrollRect)
        {
            yield return new WaitForEndOfFrame();
            scrollRect.verticalNormalizedPosition = 0f;
        }
        
        void ShowShareDialog(string link, string text)
        {
            AddLog($"📤 Share: {text}");
            AddLog($"   Link: {link}");
            
            // On mobile, this would open the native share dialog
            // For demo purposes, we'll just log the share info
        }
        
        System.Collections.IEnumerator DestroyAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        
        void CreateText(string name, GameObject parent, Vector2 anchorMin, Vector2 anchorMax, string text, int fontSize, Color color, TextAnchor alignment, out Text textComponent)
        {
            var textGO = new GameObject(name);
            textGO.transform.SetParent(parent.transform, false);
            
            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            textComponent = textGO.AddComponent<Text>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.color = color;
            textComponent.alignment = alignment;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        
        void CreateLogArea(GameObject parent)
        {
            var logAreaGO = new GameObject("Log Area");
            logAreaGO.transform.SetParent(parent.transform, false);
            
            var rect = logAreaGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.1f);
            rect.anchorMax = new Vector2(0.95f, 0.3f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            var scrollRect = logAreaGO.AddComponent<ScrollRect>();
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            
            var logBG = logAreaGO.AddComponent<UnityEngine.UI.Image>();
            logBG.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(logAreaGO.transform, false);
            
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            
            scrollRect.content = contentRect;
            
            logText = contentGO.AddComponent<Text>();
            logText.text = "📋 BoostOps Demo Log:\n";
            logText.fontSize = 12;
            logText.color = Color.white;
            logText.alignment = TextAnchor.UpperLeft;
            logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        
        /// <summary>
        /// Set up custom prefabs for BoostOps campaign display
        /// Loads the default campaign display prefabs and assigns them via BoostOpsSDK
        /// </summary>
        void SetupCustomPrefabs()
        {
            Debug.Log("[BoostOpsDemo] Setting up custom campaign display prefabs");
            
            // Try to load and assign the default campaign prefabs
            try
            {
                // Load the default campaign prefabs from their known locations
                var bannerPrefab = Resources.Load<GameObject>("Prefabs/DefaultBannerPrefab");
                var iconPrefab = Resources.Load<GameObject>("Prefabs/DefaultIconInterstitialPrefab");
                var richPrefab = Resources.Load<GameObject>("Prefabs/DefaultRichInterstitialPrefab");
                var nativePrefab = Resources.Load<GameObject>("Prefabs/DefaultNativePrefab");
                
                // Try alternative loading from AssetDatabase (if in editor)
                #if UNITY_EDITOR
                Debug.Log("[BoostOpsDemo] Resources.Load failed, trying AssetDatabase.LoadAssetAtPath...");
                // Load each prefab individually if Resources.Load failed
                if (bannerPrefab == null)
                {
                    bannerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoostOps/Prefabs/DefaultBannerPrefab.prefab");
                    Debug.Log($"[BoostOpsDemo] Banner prefab loaded via AssetDatabase: {(bannerPrefab != null ? "✅" : "❌")}");
                }
                if (iconPrefab == null)
                {
                    iconPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoostOps/Prefabs/DefaultIconInterstitialPrefab.prefab");
                    Debug.Log($"[BoostOpsDemo] Icon prefab loaded via AssetDatabase: {(iconPrefab != null ? "✅" : "❌")}");
                }
                if (richPrefab == null)
                {
                    richPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoostOps/Prefabs/DefaultRichInterstitialPrefab.prefab");
                    Debug.Log($"[BoostOpsDemo] Rich prefab loaded via AssetDatabase: {(richPrefab != null ? "✅" : "❌")}");
                }
                if (nativePrefab == null)
                {
                    nativePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BoostOps/Prefabs/DefaultNativePrefab.prefab");
                    Debug.Log($"[BoostOpsDemo] Native prefab loaded via AssetDatabase: {(nativePrefab != null ? "✅" : "❌")}");
                }
                #endif
                
                // Count how many prefabs we successfully loaded
                int loadedPrefabs = 0;
                if (bannerPrefab != null) loadedPrefabs++;
                if (iconPrefab != null) loadedPrefabs++;
                if (richPrefab != null) loadedPrefabs++;
                if (nativePrefab != null) loadedPrefabs++;
                
                if (loadedPrefabs > 0)
                {
                    // Assign prefabs via BoostOpsSDK (this will auto-create manager if needed)
                    BoostOpsSDK.SetCustomPrefabs(bannerPrefab, iconPrefab, richPrefab, nativePrefab);
                    Debug.Log($"[BoostOpsDemo] ✅ Successfully loaded {loadedPrefabs}/4 campaign display prefabs");
                    AddLog($"✅ Loaded {loadedPrefabs}/4 campaign display prefabs");
                }
                else
                {
                    Debug.LogWarning("[BoostOpsDemo] No campaign prefabs found - will use programmatic UI");
                    AddLog("⚠️ No campaign prefabs found - using programmatic UI");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsDemo] Failed to load campaign prefabs: {ex.Message}");
                AddLog($"❌ Failed to load campaign prefabs: {ex.Message}");
            }
            
            // Even without prefabs, BoostOps will fall back to programmatic UI creation
            Debug.Log("[BoostOpsDemo] Custom prefabs setup complete");
            AddLog("🔧 Custom prefabs setup complete");
        }
        
        #endregion
             
        private bool IsUnityPurchasingAvailable()
        {
#if UNITY_PURCHASING
            return true;
#else
            return false;
#endif
        }
        

        

        
        
        private bool IsIAPAvailable()
        {
            // Basic check - in a real implementation you'd check StoreKit availability
            return Application.platform == RuntimePlatform.IPhonePlayer;
        }
        
        // === DLL Protection Demo ===
        
        /// <summary>
        /// Button handler for DLL Protection demonstration
        /// NOTE: This is for distribution testing only - in development, use source code
        /// </summary>
        public void OnDLLProtectionDemoClicked()
        {
            AddLog("🔒 Testing SDK Functionality...");
            AddLog("NOTE: This works with both source code (development) and DLL (distribution) builds");
            AddLog("In production packages, all implementation is protected in BoostOps.Internal.dll");
            DemonstrateDLLProtection();
        }
        
        /// <summary>
        /// Demonstrate SDK functionality (works with both source and DLL builds)
        /// </summary>
        private void DemonstrateDLLProtection()
        {
            LogDebug("=== BoostOps SDK Functionality Demo ===");
            
            // Check if BoostOps is initialized
            bool isInitialized = BoostOpsSDK.IsInitialized;
            LogDebug($"SDK Initialization Status: {(isInitialized ? "✅ Initialized" : "❌ Not Initialized")}");
            
            if (!isInitialized)
            {
                LogDebug("Initializing SDK...");
                BoostOpsSDK.Initialize();
                isInitialized = BoostOpsSDK.IsInitialized;
                LogDebug($"SDK Initialization: {(isInitialized ? "✅ Success" : "❌ Failed")}");
            }
            
            if (isInitialized)
            {
                // Show campaign count
                int campaignCount = BoostOpsSDK.GetCampaignCount();
                LogDebug($"Available Campaigns: {campaignCount}");
                                
            }
            
            LogDebug("=== SDK Functionality Demo Complete ===");
            LogDebug("NOTE: In production, all implementation is protected in BoostOps.Internal.dll");
        }

        private void LogDebug(string message)
        {
            BoostOpsLogger.LogDebug("Demo", message);
        }
        
        /// <summary>
        /// Initialize Unity Services via reflection to avoid hard compile-time dependency.
        /// Works whether or not Unity.Services.Core is installed.
        /// </summary>
        private async Task InitUnityServicesViaReflection()
        {
            var unityServicesType = System.Type.GetType("Unity.Services.Core.UnityServices, Unity.Services.Core");
            if (unityServicesType == null)
            {
                Debug.Log("[BoostOpsDemo] Unity.Services.Core not found — skipping Unity Services init");
                return;
            }
            
            var stateProperty = unityServicesType.GetProperty("State",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (stateProperty?.GetValue(null)?.ToString() == "Initialized")
                return;
            
            var initMethod = unityServicesType.GetMethod("InitializeAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, System.Type.EmptyTypes, null);
            if (initMethod != null)
            {
                var task = initMethod.Invoke(null, null) as Task;
                if (task != null) await task;
            }
        }
        
        /// <summary>
        /// Fetch remote config via reflection — tries Unity Remote Config first, then Firebase.
        /// No hard compile-time dependency on either package.
        /// </summary>
        private async Task<bool> FetchRemoteConfigViaReflection()
        {
            var projectSettings = BoostOpsProjectSettings.GetInstance();
            string configKey = "boostops_config";
            
            // Try Unity Remote Config
            var rcType = System.Type.GetType("Unity.Services.RemoteConfig.RemoteConfigService, Unity.Services.RemoteConfig");
            if (rcType != null)
            {
                var instanceProp = rcType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance != null)
                {
                    var fetchMethod = rcType.GetMethod("FetchConfigsAsync",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (fetchMethod != null)
                    {
                        var userAttrs = new Dictionary<string, object>();
                        var appAttrs = new Dictionary<string, object>
                        {
                            { "environment", "production" },
                            { "project_key", projectSettings?.projectKey ?? "unknown" }
                        };
                        var task = fetchMethod.Invoke(instance, new object[] { userAttrs, appAttrs }) as Task;
                        if (task != null) await task;
                    }
                    
                    var appConfigProp = instance.GetType().GetProperty("appConfig");
                    var appConfig = appConfigProp?.GetValue(instance);
                    var getJson = appConfig?.GetType().GetMethod("GetJson",
                        new System.Type[] { typeof(string), typeof(string) });
                    if (getJson != null)
                    {
                        var json = getJson.Invoke(appConfig, new object[] { configKey, "{}" }) as string;
                        if (!string.IsNullOrEmpty(json) && json != "{}")
                        {
                            AddLog($"✅ Unity Remote Config fetched ({json.Length} chars)");
                            return true;
                        }
                    }
                }
            }
            
            // Try Firebase Remote Config
            var fbType = System.Type.GetType("Firebase.RemoteConfig.FirebaseRemoteConfig, Firebase.RemoteConfig");
            if (fbType != null)
            {
                AddLog("🔥 Using Firebase Remote Config...");
                var defaultProp = fbType.GetProperty("DefaultInstance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var fbInstance = defaultProp?.GetValue(null);
                if (fbInstance != null)
                {
                    var fetchMethod = fbType.GetMethod("FetchAsync",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null, System.Type.EmptyTypes, null);
                    if (fetchMethod != null)
                    {
                        var task = fetchMethod.Invoke(fbInstance, null) as Task;
                        if (task != null) await task;
                    }
                    
                    var activateMethod = fbType.GetMethod("ActivateAsync",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (activateMethod != null)
                    {
                        var task = activateMethod.Invoke(fbInstance, null) as Task;
                        if (task != null) await task;
                    }
                    
                    AddLog("✅ Firebase Remote Config fetched and activated");
                    return true;
                }
            }
            
            return false;
        }
    }
} 