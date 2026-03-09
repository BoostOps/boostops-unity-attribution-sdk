using UnityEngine;
using System;
using System.Collections.Generic;

namespace BoostOps
{
    /// <summary>
    /// Internal manager for BoostOps SDK - auto-created by BoostOpsSDK when needed.
    /// This class is internal-only. Use BoostOpsSDK static class for all public APIs.
    /// Actual business logic is protected in BoostOps.Internal.dll for IP protection.
    /// </summary>
    internal class BoostOpsManager : MonoBehaviour
    {
        #region Singleton Pattern
        
        private static BoostOpsManager _instance;
        
        internal static BoostOpsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<BoostOpsManager>();
                    
                    if (_instance == null)
                    {
                        // Try to load from Resources first (works in builds and third-party apps)
                        var boostOpsPrefab = Resources.Load<GameObject>("BoostOps/BoostOpsManager");
                        if (boostOpsPrefab != null)
                        {
                            BoostOpsLogger.LogDebug("Manager", "🎯 Loading BoostOpsManager from Resources/BoostOps/BoostOpsManager.prefab...");
                            var go = Instantiate(boostOpsPrefab);
                            go.name = "BoostOpsManager"; // Remove "(Clone)" suffix
                            _instance = go.GetComponent<BoostOpsManager>();
                            
                            if (_instance != null)
                            {
                                DontDestroyOnLoad(go);
                                BoostOpsLogger.LogInfo("Manager", "✅ Successfully instantiated BoostOpsManager from prefab");
                            }
                            else
                            {
                                Debug.LogError("[BoostOps Manager] ❌ BoostOpsManager prefab doesn't have BoostOpsManager component!");
                                Destroy(go);
                            }
                        }
                        
                        // Fallback: Create programmatically if prefab not found
                        if (_instance == null)
                        {
                            Debug.LogWarning("[BoostOps Manager] ⚠️ BoostOps prefab not found in Resources - creating programmatically");
                            var go = new GameObject("BoostOpsManager");
                            _instance = go.AddComponent<BoostOpsManager>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Public Events (Customer-Facing)
        
        public static event System.Action OnSDKInitialized;
        public static event System.Action<string> OnSDKError;
        
        #pragma warning disable CS0067 // Event is never used - part of public API, will be implemented
        public static event System.Action<Campaign> OnCampaignImpression;
        public static event System.Action<Campaign> OnCampaignClick;
        #pragma warning restore CS0067
        
        #endregion
        
        #region Public Properties (Customer-Facing)
        
        [Header("Initialization Settings")]
        [Tooltip("Should BoostOps initialize automatically on Start()?")]
        public bool autoInitializeOnStart = true;
        
        [Header("Custom Campaign Prefabs")]
        [Tooltip("Custom banner prefab (optional - uses built-in if null)")]
        public GameObject customBannerPrefab;
        
        [Tooltip("Custom icon interstitial prefab (optional - uses built-in if null)")]
        public GameObject customIconInterstitialPrefab;
        
        [Tooltip("Custom rich interstitial prefab (optional - uses built-in if null)")]
        public GameObject customRichInterstitialPrefab;
        
        [Tooltip("Custom native ad prefab (optional - uses built-in if null)")]
        public GameObject customNativePrefab;
        
        [Tooltip("Custom app wall prefab - grid container (optional - uses built-in if null)")]
        public GameObject customAppWallPrefab;
        
        [Tooltip("Custom app wall item prefab - individual game tile (optional - uses built-in if null)")]
        public GameObject customAppWallItemPrefab;
        
        [Tooltip("Use built-in default prefabs instead of custom ones (can be set via BoostOpsSDK.SetUseDefaultPrefabs)")]
        public bool useDefaultPrefabs = false;
        
        [Header("Display Settings")]
        [Tooltip("Default sorting order for BoostOps overlay canvas")]
        public int overlaySortingOrder = 32767;
        
        [Tooltip("Amazon Associates tag for monetization")]
        public string amazonAssociatesTag = "";
        
        internal bool IsInitialized { get; private set; }
        
        // Internal manager contains all the sophisticated logic
        private BoostOps.Internal.BoostOpsManagerInternal internalManager;
        private bool isInternalManagerConfigured = false;
        
        // Internal accessor for BoostOpsSDK to get the configured internal manager
        internal BoostOps.Internal.BoostOpsManagerInternal InternalManager => internalManager;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            BoostOpsLogger.LogDebug("Manager", $"🚀 Awake() called on instance {GetHashCode()}");
            
            if (_instance != null && _instance != this)
            {
                BoostOpsLogger.LogWarning("Manager", $"⚠️ Duplicate BoostOpsManager detected! Destroying instance {GetHashCode()}, keeping {_instance.GetHashCode()}");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Register this MonoBehaviour as the coroutine runner for DLL-internal code
            BoostOps.Internal.BoostOpsSDKInternal.SetCoroutineRunner(this);
            
            // Initialize internal manager that contains all the sophisticated logic
            internalManager = new BoostOps.Internal.BoostOpsManagerInternal();
            BoostOpsLogger.LogDebug("Manager", $"🏭 Created new BoostOpsManagerInternal instance {internalManager.GetHashCode()}");
            
            // Auto-load prefab references if this instance was created programmatically (no prefabs set)
            LoadPrefabReferencesIfNeeded();
            
            // Don't configure yet - wait until SDK Init() is called
            BoostOpsLogger.LogDebug("Manager", "⏳ Manager created but not configured yet (waiting for SDK Init() call)");
        }
        
        void Start()
        {
            // ✅ Auto-initialization removed - now controlled via BoostOpsSDK.Init()
            Debug.Log("[BoostOps Manager] 🔄 Start() - Manager ready, waiting for SDK Init() call");
        }
        
        #endregion
        
        #region Native Callbacks (Called from iOS/Android via UnitySendMessage)
        
        /// <summary>
        /// iOS native callback when StoreKit captures a purchase receipt
        /// Called via UnitySendMessage from BoostOpsReceiptCapture.mm
        /// </summary>
        public void OnReceiptCaptured(string jsonPayload)
        {
            try
            {
                #if BOOSTOPS_DEBUG_LOGGING
                BoostOpsLogger.LogDebug("Manager", $"📱 Native receipt callback: {jsonPayload?.Substring(0, Math.Min(100, jsonPayload?.Length ?? 0))}...");
                #endif
                
                if (string.IsNullOrEmpty(jsonPayload))
                {
                    Debug.LogWarning("[BoostOps Manager] Received empty receipt payload from native");
                    return;
                }
                
                // Parse JSON payload
                var data = JsonUtility.FromJson<ReceiptPayload>(jsonPayload);
                
                // Forward to receipt cache
                BoostOpsReceiptCache.CachePurchase(
                    productId: data.productId,
                    transactionId: data.transactionId,
                    receipt: data.receipt,
                    productName: !string.IsNullOrEmpty(data.productName) ? data.productName : null,
                    productType: !string.IsNullOrEmpty(data.productType) ? data.productType : null,
                    subscriptionGroupId: !string.IsNullOrEmpty(data.subscriptionGroupId) ? data.subscriptionGroupId : null,
                    originalTransactionId: !string.IsNullOrEmpty(data.originalTransactionId) ? data.originalTransactionId : null,
                    isIntroductoryPricePeriod: data.isIntroductoryPricePeriod,
                    isTrialPeriod: data.isTrialPeriod
                );
                
                #if BOOSTOPS_DEBUG_LOGGING
                BoostOpsLogger.LogDebug("Manager", $"✅ Receipt cached: {data.productId} (txn: {data.transactionId?.Substring(0, Math.Min(8, data.transactionId?.Length ?? 0))}...)");
                #endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps Manager] Failed to process native receipt callback: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Data structure for receipt payload from native (matches iOS JSON)
        /// </summary>
        [System.Serializable]
        private class ReceiptPayload
        {
            public string productId;
            public string transactionId;
            public string receipt;
            public string productName;
            public string productType;
            public string subscriptionGroupId;
            public string originalTransactionId;
            public bool isIntroductoryPricePeriod;
            public bool isTrialPeriod;
        }
        
        #endregion
        
        #region Internal API (Used by BoostOpsSDK)
        
        /// <summary>
        /// Initialize SDK with project configuration (Internal - use BoostOpsSDK.InitializeAsync())
        /// </summary>
        internal async System.Threading.Tasks.Task<bool> InitializeAsync()
        {
            try
            {
                Debug.Log("[BoostOps Manager] InitializeAsync called - configuring and delegating to internal manager");
                
                // Ensure prefabs and settings are transferred before initialization
                ConfigureInternalManager();
                
                // Delegate to internal implementation for managed mode
                bool success = internalManager != null ? await internalManager.InitializeManagedAsync() : false;
                
                IsInitialized = success;
                
                if (success)
                {
                    OnSDKInitialized?.Invoke();
                    Debug.Log("[BoostOps Manager] ✅ Managed initialization complete via internal manager");
                }
                else
                {
                    Debug.LogError("[BoostOps Manager] ❌ Managed initialization failed");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps Manager] ❌ Managed initialization failed: {ex.Message}");
                OnSDKError?.Invoke(ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Configure the internal manager with current prefab settings (Internal - called by BoostOpsSDK)
        /// </summary>
        internal void EnsureConfigured()
        {
            if (internalManager != null)
            {
                Debug.Log("[BoostOps Manager] 🔧 EnsureConfigured() - Configuring internal manager with current settings");
                ConfigureInternalManager();
            }
        }
        
        /// <summary>
        /// Initialize SDK with project key (overload for compatibility) (Internal - use BoostOpsSDK.InitializeAsync())
        /// </summary>
        internal async System.Threading.Tasks.Task<bool> InitializeAsync(string projectKey)
        {
            // Delegate to internal implementation
            return await InitializeAsync();
        }
        

        

        
        /// <summary>
        /// Initialize for local mode only (no server) (Internal - use BoostOpsSDK.InitializeLocalOnly())
        /// </summary>
        internal void InitializeLocalOnly()
        {
            try
            {
                Debug.Log("[BoostOps Manager] InitializeLocalOnly called - configuring prefabs and delegating to internal manager");
                
                // ✅ CRITICAL: Ensure prefabs are transferred from inspector to internal manager
                ConfigureInternalManager();
                
                // Delegate to internal manager that contains all the real logic
                internalManager?.InitializeLocalOnly();
                
                IsInitialized = internalManager?.IsInitialized ?? false;
                
                if (IsInitialized)
                {
                    OnSDKInitialized?.Invoke();
                    Debug.Log("[BoostOps Manager] ✅ Local initialization complete via internal manager");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps Manager] ❌ Local initialization failed: {ex.Message}");
                OnSDKError?.Invoke(ex.Message);
            }
        }
        
        /// <summary>
        /// Get the next campaign using the configured selection algorithm
        /// Delegates to internal manager for sophisticated logic
        /// </summary>
        public Campaign GetNextCampaign(string placement = "default")
        {
            // Delegate to internal manager that contains all the sophisticated waterfall/weighted logic
            return internalManager?.GetNextCampaign(placement);
        }
        
        /// <summary>
        /// Get a random eligible campaign for display (legacy method)
        /// </summary>
        public Campaign GetRandomCampaign()
        {
            return GetNextCampaign("random");
        }
        
        /// <summary>
        /// Track impression for analytics
        /// </summary>
        public void TrackImpression(Campaign campaign, string placement)
        {
            internalManager?.TrackImpression(campaign, placement);
        }
        
        /// <summary>
        /// Track click for analytics
        /// </summary>
        public void TrackClick(Campaign campaign, string placement)
        {
            internalManager?.TrackClick(campaign, placement);
        }
        
        /// <summary>
        /// Get total impressions (for stats display) (Internal - use BoostOpsSDK.GetTotalImpressions())
        /// </summary>
        internal int GetTotalImpressions()
        {
            return internalManager?.GetTotalImpressions() ?? 0;
        }
        
        /// <summary>
        /// Get total clicks (for stats display) (Internal - use BoostOpsSDK.GetTotalClicks())
        /// </summary>
        internal int GetTotalClicks()
        {
            return internalManager?.GetTotalClicks() ?? 0;
        }
        
        // Additional methods for compatibility with existing code (Internal - use BoostOpsSDK methods)
        internal int GetCampaignCount() => internalManager?.CampaignCount ?? 0;
        internal bool IsReady => IsInitialized;
        internal void SetLocalOnlyMode(bool localOnly) { internalManager?.SetLocalOnlyMode(localOnly); }
        internal void LoadCampaignsFromLocalFile(string filePath) { /* Delegated to internal implementation */ }
        /// <summary>
        /// Show cross-promotion campaign with specific display mode (Internal - use BoostOpsSDK.ShowCrossPromo())
        /// </summary>
        /// <param name="placement">Placement identifier for analytics</param>
        /// <param name="displayMode">How to display the campaign</param>
        /// <returns>True if campaign was successfully displayed, false otherwise</returns>
        internal bool ShowCrossPromo(string placement, BoostOpsCampaignDisplay.CampaignDisplayMode displayMode)
        {
            // Delegate to internal implementation that contains all the sophisticated logic
            return internalManager?.ShowCrossPromo(placement, displayMode) ?? false;
        }
        
        /// <summary>
        /// Overload for backward compatibility with generic object parameter (Internal - use BoostOpsSDK.ShowCrossPromo())
        /// </summary>
        internal bool ShowCrossPromo(string placement, object format = null) 
        {
            // Convert object to display mode if possible, otherwise use default
            var displayMode = BoostOpsCampaignDisplay.CampaignDisplayMode.RichInterstitial;
            
            if (format is BoostOpsCampaignDisplay.CampaignDisplayMode mode)
            {
                displayMode = mode;
            }
            
            return ShowCrossPromo(placement, displayMode);
        }
        public void HideAllPromos() 
        { 
            internalManager?.HideAllPromos();
        }
        
        public void HidePromo(string placement) 
        { 
            internalManager?.HidePromo(placement);
        }
        
        public Campaign GetCampaignById(string id) 
        { 
            return internalManager?.GetCampaignById(id);
        }
        
        // Fixed method signatures to match development code expectations
        public void LogPurchase(string transactionId, decimal localizedPrice, string isoCurrencyCode, string productId, object properties) 
        { 
            Debug.Log($"[BoostOps Manager] LogPurchase: {transactionId}, {localizedPrice}, {isoCurrencyCode}, {productId}");
            /* TODO: Move to DLL */ 
        }
        
        public void TrackClickAndOpenStore(Campaign campaign, string placement, object displayMode) 
        { 
            Debug.Log($"[BoostOps Manager] TrackClickAndOpenStore: {campaign?.CampaignId}, {placement}, {displayMode}");
            internalManager?.TrackClickAndOpenStore(campaign, placement, displayMode);
        }
        
        // Provide access to SourceProject for compatibility (delegates to internal implementation)
        public ISourceProject SourceProject => new SourceProjectProxy();
        
        /// <summary>
        /// Set custom prefabs for campaign display (can be called at runtime)
        /// </summary>
        public void SetCustomPrefabs(GameObject bannerPrefab = null, GameObject iconInterstitialPrefab = null, 
            GameObject richInterstitialPrefab = null, GameObject nativePrefab = null)
        {
            customBannerPrefab = bannerPrefab;
            customIconInterstitialPrefab = iconInterstitialPrefab;
            customRichInterstitialPrefab = richInterstitialPrefab;
            customNativePrefab = nativePrefab;
            
            // Force reconfiguration when prefabs change at runtime
            isInternalManagerConfigured = false;
            ConfigureInternalManager();
            
            Debug.Log("[BoostOps Manager] Custom prefabs updated");
        }
        
        /// <summary>
        /// Set overlay sorting order (Internal - use BoostOpsSDK.SetOverlayPriority())
        /// </summary>
        internal void SetOverlaySortingOrder(int sortingOrder)
        {
            overlaySortingOrder = sortingOrder;
            isInternalManagerConfigured = false;
            ConfigureInternalManager();
            Debug.Log($"[BoostOps Manager] Overlay sorting order set to: {sortingOrder}");
        }
        
        /// <summary>
        /// Set Amazon Associates tag for monetization (Internal - use BoostOpsSDK.SetAmazonAssociatesTag())
        /// </summary>
        internal void SetAmazonAssociatesTag(string tag)
        {
            amazonAssociatesTag = tag;
            isInternalManagerConfigured = false;
            ConfigureInternalManager();
            Debug.Log($"[BoostOps Manager] Amazon Associates tag set to: '{tag}'");
        }
        
        /// <summary>
        /// Auto-load default prefab references if current instance has no prefabs configured
        /// This only applies to programmatically created instances (fallback case)
        /// </summary>
        private void LoadPrefabReferencesIfNeeded()
        {
            // Only auto-load if we have no prefabs configured (indicating programmatic creation fallback)
            bool hasPrefabs = customBannerPrefab != null || customIconInterstitialPrefab != null || 
                             customRichInterstitialPrefab != null || customNativePrefab != null;
            
            if (hasPrefabs)
            {
                Debug.Log("[BoostOps Manager] 🎯 Prefabs already configured - skipping auto-load");
                return;
            }
            
            Debug.Log("[BoostOps Manager] 🔍 No prefabs configured - loading default prefabs from Resources...");
            LoadDefaultPrefabReferences();
        }
        
        /// <summary>
        /// Load default prefab references (tries Resources first, falls back to internal defaults)
        /// </summary>
        private void LoadDefaultPrefabReferences()
        {
            Debug.Log("[BoostOps Manager] 🎨 Loading default prefab references...");
            
            // Try to load default prefabs from Resources first (for runtime compatibility)
            customBannerPrefab = Resources.Load<GameObject>("BoostOps/DefaultBannerPrefab");
            customIconInterstitialPrefab = Resources.Load<GameObject>("BoostOps/DefaultIconInterstitialPrefab");
            customRichInterstitialPrefab = Resources.Load<GameObject>("BoostOps/DefaultRichInterstitialPrefab");
            customNativePrefab = Resources.Load<GameObject>("BoostOps/DefaultNativePrefab");
            
            // Count successful loads
            int loadedCount = 0;
            if (customBannerPrefab != null) loadedCount++;
            if (customIconInterstitialPrefab != null) loadedCount++;
            if (customRichInterstitialPrefab != null) loadedCount++;
            if (customNativePrefab != null) loadedCount++;
            
            if (loadedCount > 0)
            {
                Debug.Log($"[BoostOps Manager] ✅ Loaded {loadedCount}/4 default prefabs from Resources:");
                Debug.Log($"[BoostOps Manager]   📦 Banner: {(customBannerPrefab != null ? customBannerPrefab.name : "NULL")}");
                Debug.Log($"[BoostOps Manager]   📦 Icon Interstitial: {(customIconInterstitialPrefab != null ? customIconInterstitialPrefab.name : "NULL")}");
                Debug.Log($"[BoostOps Manager]   📦 Rich Interstitial: {(customRichInterstitialPrefab != null ? customRichInterstitialPrefab.name : "NULL")}");
                Debug.Log($"[BoostOps Manager]   📦 Native: {(customNativePrefab != null ? customNativePrefab.name : "NULL")}");
            }
            
            // If no prefabs loaded from Resources, fall back to internal defaults
            if (loadedCount == 0)
            {
                Debug.Log("[BoostOps Manager] 🎨 No default prefabs found in Resources - using internal defaults");
                useDefaultPrefabs = true;
            }
            else if (loadedCount < 4)
            {
                Debug.LogWarning($"[BoostOps Manager] ⚠️ Only {loadedCount}/4 default prefabs loaded from Resources - missing prefabs will use internal defaults");
            }
        }
        
        #endregion
        
        #region Internal Configuration
        
        /// <summary>
        /// Configure the internal manager with current settings
        /// </summary>
        private void ConfigureInternalManager()
        {
            if (isInternalManagerConfigured)
            {
                Debug.Log($"[BoostOps Manager] ✅ ConfigureInternalManager() skipped - already configured on instance {GetHashCode()}");
                return;
            }
            
            // Debug.Log($"[BoostOps Manager] 🔍 ConfigureInternalManager() called on instance {GetHashCode()} - internalManager={(internalManager != null ? $"EXISTS({internalManager.GetHashCode()})" : "NULL")}");
            
            if (internalManager != null)
            {
                // Debug.Log($"[BoostOps Manager] 🔧 ConfigureInternalManager - Transferring prefabs from public to internal:");
                // Debug.Log($"[BoostOps Manager]   🎨 Use Default Prefabs: {useDefaultPrefabs}");
                // Debug.Log($"[BoostOps Manager]   📦 Banner Prefab: {(customBannerPrefab != null ? customBannerPrefab.name : "NULL")}");
                // Debug.Log($"[BoostOps Manager]   📦 Icon Interstitial Prefab: {(customIconInterstitialPrefab != null ? customIconInterstitialPrefab.name : "NULL")}");
                // Debug.Log($"[BoostOps Manager]   📦 Rich Interstitial Prefab: {(customRichInterstitialPrefab != null ? customRichInterstitialPrefab.name : "NULL")}");
                // Debug.Log($"[BoostOps Manager]   📦 Native Prefab: {(customNativePrefab != null ? customNativePrefab.name : "NULL")}");
                // Debug.Log($"[BoostOps Manager]   🎛️ Overlay Sort Order: {overlaySortingOrder}");
                // Debug.Log($"[BoostOps Manager]   🏷️ Amazon Tag: '{amazonAssociatesTag}'");
                
                // Only show prefab warnings if we're not using default prefabs
                if (!useDefaultPrefabs)
                {
                    if (customIconInterstitialPrefab != null)
                    {
                        // Debug.Log($"[BoostOps Manager] 🎯 ICON PREFAB FOUND! Name: '{customIconInterstitialPrefab.name}', InstanceID: {customIconInterstitialPrefab.GetInstanceID()}");
                    }
                    else
                    {
                        Debug.LogError($"[BoostOps Manager] ❌ ICON PREFAB IS NULL! This will cause CreateDefaultIconInterstitial to run instead of using your prefab!");
                    }
                }
                // else
                // {
                //     Debug.Log($"[BoostOps Manager] 🎨 Using default prefabs - custom prefab validation skipped");
                // }
                
                // Debug.Log($"[BoostOps Manager] 🚀 About to call internalManager.ConfigureSettings()...");
                internalManager.ConfigureSettings(
                    customBannerPrefab,
                    customIconInterstitialPrefab,
                    customRichInterstitialPrefab,
                    customNativePrefab,
                    customAppWallPrefab,
                    customAppWallItemPrefab,
                    overlaySortingOrder,
                    amazonAssociatesTag
                );
                
                // Debug.Log($"[BoostOps Manager] ✅ Prefab configuration transfer complete");
                isInternalManagerConfigured = true;
            }
            else
            {
                Debug.LogError("[BoostOps Manager] ❌ Cannot configure - internal manager is null!");
            }
        }
        
        #endregion
        
        #region Internal Implementation Notes
        
        // 🔒 IP PROTECTION NOTICE:
        // This is a THIN PUBLIC FACADE over the internal implementation.
        // 
        // Actual business logic is implemented in BoostOps.Internal.dll:
        // - Campaign filtering and eligibility logic
        // - Frequency capping and impression tracking  
        // - Remote config fetching and parsing
        // - Analytics data collection and transmission
        // - Attribution and deep link handling
        // - UI rendering and user interaction
        // 
        // This design provides:
        // ✅ Clean public API for customers
        // ✅ Maximum IP protection for business logic
        // ✅ Easier maintenance and updates
        // ✅ Professional SDK distribution
        
        #endregion
    }
    
    /// <summary>
    /// Proxy class that provides SourceProject interface without exposing internal implementation (Internal)
    /// Note: ISourceProject interface is now defined in BoostOpsTypes.cs
    /// </summary>
    internal class SourceProjectProxy : ISourceProject
    {
        public string DefaultIconInterstitialDescription => "Try our exciting new game!";
        public string DefaultIconInterstitialButtonText => "Download Now";
        public string DefaultRichInterstitialDescription => "Experience amazing gameplay in our latest game!";
        public string DefaultRichInterstitialButtonText => "Play Now";
        public string ProjectName => "BoostOps Project";
        public string ProjectId => "boostops-project";
    }
    
    /// <summary>
    /// Interface for SourceProject functionality (Internal)
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
}
