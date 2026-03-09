using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BoostOps.Attribution
{
    /// <summary>
    /// Manages SKAdNetwork (SKAN) conversion value updates for iOS attribution
    /// Supports SKAN 2.x (iOS 14.0+), SKAN 3.0 (iOS 15.4+), and SKAN 4.0 (iOS 16.1+)
    /// </summary>
    public class BoostOpsSKANManager : MonoBehaviour
    {
        #region Native iOS Bridge
        
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int _BoostOps_GetSKANVersion();
        
        [DllImport("__Internal")]
        private static extern void _BoostOps_UpdateConversionValue(int conversionValue, string callbackObjectName, string callbackMethodName);
        
        [DllImport("__Internal")]
        private static extern void _BoostOps_UpdateConversionValueCoarse(int fineValue, string coarseValue, string callbackObjectName, string callbackMethodName);
        
        [DllImport("__Internal")]
        private static extern void _BoostOps_UpdateConversionValueCoarseLocked(int fineValue, string coarseValue, bool lockWindow, string callbackObjectName, string callbackMethodName);
        
        [DllImport("__Internal")]
        private static extern void _BoostOps_RegisterForAdNetworkAttribution();
#else
        private static int _BoostOps_GetSKANVersion() => 0;
        private static void _BoostOps_UpdateConversionValue(int conversionValue, string callbackObjectName, string callbackMethodName) { }
        private static void _BoostOps_UpdateConversionValueCoarse(int fineValue, string coarseValue, string callbackObjectName, string callbackMethodName) { }
        private static void _BoostOps_UpdateConversionValueCoarseLocked(int fineValue, string coarseValue, bool lockWindow, string callbackObjectName, string callbackMethodName) { }
        private static void _BoostOps_RegisterForAdNetworkAttribution() { }
#endif
        
        #endregion
        
        #region Singleton & Initialization
        
        private static BoostOpsSKANManager _instance;
        public static BoostOpsSKANManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("BoostOpsSKANManager");
                    _instance = go.AddComponent<BoostOpsSKANManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            Initialize();
        }
        
        #endregion
        
        #region Properties & State
        
        private int _skanVersion;
        private int _currentConversionValue = -1;
        private string _currentCoarseValue = "low";
        private bool _isWindowLocked = false;
        private bool _isInitialized = false;
        private BoostOpsSKANMapping _mapping;  // NEW: Server-format schema
        private decimal _cumulativeRevenue = 0m;
        private bool _isFirstPurchase = true;
        
        /// <summary>SKAN version: 0 (unavailable), 2 (iOS 14.0), 3 (iOS 15.4), 4 (iOS 16.1+)</summary>
        public int SKANVersion => _skanVersion;
        
        /// <summary>Is SKAN available on this device</summary>
        public bool IsSKANAvailable => _skanVersion >= 2;
        
        /// <summary>Current conversion value (0-63), or -1 if not set</summary>
        public int CurrentConversionValue => _currentConversionValue;
        
        /// <summary>Current coarse value (SKAN 4.0 only)</summary>
        public string CurrentCoarseValue => _currentCoarseValue;
        
        /// <summary>Is measurement window locked (SKAN 4.0 only)</summary>
        public bool IsWindowLocked => _isWindowLocked;
        
        /// <summary>Active SKAN mapping (matches server schema)</summary>
        public BoostOpsSKANMapping Mapping => _mapping;
        
        /// <summary>Cumulative revenue for lifetime value (LTV) tracking</summary>
        public decimal CumulativeRevenue => _cumulativeRevenue;
        
        [Obsolete("Use Mapping instead of Schema (schema format changed to match server)")]
        public BoostOpsSKANConversionSchema Schema => null;
        
        /// <summary>Disable automatic SKAN updates if another SDK is managing SKAN</summary>
        public static bool DisableAutomaticSKAN { get; set; } = false;
        
        /// <summary>Event fired when conversion value is updated successfully</summary>
        public event Action<int, string> OnConversionValueUpdated;
        
        /// <summary>Event fired when conversion value update fails</summary>
        public event Action<string> OnConversionValueUpdateFailed;
        
        #endregion
        
        #region Initialization
        
        private void Initialize()
        {
            if (_isInitialized) return;
            
            // Get SKAN version
            _skanVersion = _BoostOps_GetSKANVersion();
            
            // Debug.Log($"[BoostOps SKAN] Initializing... Version: {_skanVersion}");
            
            // Check for conflicting attribution SDKs
            DetectSKANConflicts();
            
            // Register for attribution (iOS 14.0+)
            if (IsSKANAvailable)
            {
                _BoostOps_RegisterForAdNetworkAttribution();
            }
            
            // Load default mapping (matches server format)
            _mapping = BoostOpsSKANMapping.CreateDefault();
            
            // Load persisted state
            LoadPersistedState();
            
            _isInitialized = true;
            
            // Debug.Log($"[BoostOps SKAN] Initialized | SKAN Version: {_skanVersion} | Available: {IsSKANAvailable}");
        }
        
        /// <summary>
        /// Set SKAN mapping (can be loaded from server config)
        /// </summary>
        public void SetMapping(BoostOpsSKANMapping mapping)
        {
            if (mapping == null)
            {
                Debug.LogWarning("[BoostOps SKAN] Cannot set null mapping");
                return;
            }
            
            _mapping = mapping;
            
            // Log mapping metadata
            Debug.Log($"[BoostOps SKAN] ✅ SKAN mapping loaded:");
            Debug.Log($"  Mapping ID: {mapping.mapping_id}");
            Debug.Log($"  Effective From: {mapping.effective_from}");
            Debug.Log($"  SKAN Version: {mapping.skan_version}");
            Debug.Log($"  Mode: {mapping.mode}");
            Debug.Log($"  Max Fine Value: {mapping.window1?.max_fine_value ?? 63}");
            Debug.Log($"  Downgrade Behavior: {mapping.downgrade_behavior}");
            Debug.Log($"  Revenue Buckets: {mapping.window1?.revenue_buckets?.Count ?? 0}");
            Debug.Log($"  Milestones: {mapping.window1?.milestones?.Count ?? 0}");
        }
        
        /// <summary>
        /// [OBSOLETE] Use SetMapping() instead
        /// </summary>
        [Obsolete("Use SetMapping(BoostOpsSKANMapping) instead")]
        public void SetConversionSchema(BoostOpsSKANConversionSchema schema)
        {
            Debug.LogWarning("[BoostOps SKAN] SetConversionSchema is obsolete. Use SetMapping() with server format instead.");
        }
        
        /// <summary>
        /// Load SKAN mapping from server config JSON
        /// Server provides full mapping in config.skan.mapping
        /// </summary>
        public void LoadMappingFromJson(string mappingJson)
        {
            if (string.IsNullOrEmpty(mappingJson))
            {
                Debug.Log("[BoostOps SKAN] No mapping JSON provided - using defaults");
                return;
            }
            
            Debug.Log($"[BoostOps SKAN] Loading mapping from JSON ({mappingJson.Length} chars)");
            
            try
            {
                var mapping = BoostOpsSKANMapping.FromJson(mappingJson);
                
                if (mapping == null)
                {
                    Debug.LogWarning("[BoostOps SKAN] Failed to parse mapping JSON - using defaults");
                    return;
                }
                
                SetMapping(mapping);
                
                Debug.Log($"[BoostOps SKAN] Mapping loaded successfully from server");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps SKAN] Error loading mapping from JSON: {ex.Message}");
                Debug.Log("[BoostOps SKAN] Using default mapping");
            }
        }
        
        /// <summary>
        /// [OBSOLETE] Use LoadMappingFromJson() instead
        /// </summary>
        [Obsolete("Use LoadMappingFromJson(string) instead")]
        public void LoadSchemaFromRemoteConfig(SKANSchemaConfig serverConfig)
        {
            Debug.LogWarning("[BoostOps SKAN] LoadSchemaFromRemoteConfig is obsolete. Use LoadMappingFromJson() instead.");
        }
        
        /// <summary>
        /// [OBSOLETE] No longer needed with new mapping format
        /// </summary>
        [Obsolete("ApplyCriticalDefaults is obsolete - not used with new mapping format")]
        private void ApplyCriticalDefaults(BoostOpsSKANConversionSchema schema)
        {
            // Check if we have critical events
            bool hasAppLaunch = schema.Rules.Any(r => r.EventType == "app_launch");
            bool hasFirstPurchase = schema.Rules.Any(r => r.EventType == "purchase");
            
            // Add critical defaults if missing
            if (!hasAppLaunch)
            {
                schema.Rules.Insert(0, new BoostOps.Attribution.ConversionRule
                {
                    EventType = "app_launch",
                    FineValue = 1,
                    CoarseValue = "low",
                    Description = "[Default] First app launch",
                    Condition = (data) => {
                        var launchCount = data != null && data.ContainsKey("launch_count") ? (int)data["launch_count"] : 0;
                        return launchCount == 1;
                    }
                });
                
                Debug.Log("[BoostOps SKAN] Added critical default: app_launch → Value 1");
            }
            
            if (!hasFirstPurchase)
            {
                schema.Rules.Add(new BoostOps.Attribution.ConversionRule
                {
                    EventType = "purchase",
                    FineValue = 16,
                    CoarseValue = "medium",
                    Description = "[Default] First purchase (any amount)",
                    Condition = (data) => {
                        return data != null && data.ContainsKey("is_first_purchase") && (bool)data["is_first_purchase"];
                    }
                });
                
                Debug.Log("[BoostOps SKAN] Added critical default: first purchase → Value 16");
            }
        }
        
        #endregion
        
        #region Conversion Value Updates
        
        /// <summary>
        /// Update conversion value based on an event
        /// The schema will determine the appropriate conversion value
        /// </summary>
        public void UpdateConversionValueForEvent(string eventType, Dictionary<string, object> eventData = null)
        {
            // Check if automatic SKAN is disabled (another SDK is managing it)
            if (DisableAutomaticSKAN)
            {
                Debug.Log("[BoostOps SKAN] Automatic SKAN disabled - another SDK is managing SKAN");
                return;
            }
            
            if (!IsSKANAvailable)
            {
                // Debug.Log("[BoostOps SKAN] SKAN not available on this device");
                return;
            }
            
            if (_isWindowLocked)
            {
                Debug.Log("[BoostOps SKAN] Measurement window is locked, cannot update conversion value");
                return;
            }
            
            // Track cumulative revenue and convert to USD for consistent bucketing
            decimal amountUsd = 0m;
            if (eventType == "purchase" && eventData != null && eventData.ContainsKey("amount"))
            {
                try
                {
                    var amount = Convert.ToDecimal(eventData["amount"]);
                    var currency = eventData.ContainsKey("currency") ? eventData["currency"].ToString() : "USD";
                    
                    // Convert to USD using hardcoded rates
                    amountUsd = BoostOps.Utilities.BoostOpsCurrencyHelper.ConvertToUsd(amount, currency);
                    
                    // Track cumulative revenue in USD for LTV bucketing
                    _cumulativeRevenue += amountUsd;
                    
                    // Add USD amount to event data
                    eventData["amount_usd"] = amountUsd;
                    eventData["cumulative_revenue"] = _cumulativeRevenue;
                    
                    #if BOOSTOPS_DEBUG_LOGGING
                    if (currency != "USD")
                    {
                        BoostOpsLogger.LogDebug("SKAN", 
                            $"Revenue: {amount:F2} {currency} → ${amountUsd:F2} USD (LTV: ${_cumulativeRevenue:F2})");
                    }
                    #endif
                    
                    // Persist cumulative revenue across sessions
                    PlayerPrefs.SetFloat("BoostOps_SKAN_CumulativeRevenue", (float)_cumulativeRevenue);
                    PlayerPrefs.SetInt("BoostOps_SKAN_IsFirstPurchase", 0);  // No longer first purchase
                    PlayerPrefs.Save();
                    
                    // Track first purchase status
                    if (_isFirstPurchase)
                    {
                        eventData["is_first_purchase"] = true;
                        _isFirstPurchase = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BoostOps SKAN] Failed to track revenue: {ex.Message}");
                }
            }
            
            // Add install detection for app_open events
            if (eventType == "app_open" || eventType == "app_launch")
            {
                if (eventData == null) eventData = new Dictionary<string, object>();
                eventData["is_first_install"] = IsFirstEverInstall();
            }
            
            // Calculate conversion value based on event type and mapping
            int newValue = -1;
            string coarseValue = "low";
            bool shouldLock = false;
            
            if (eventType == "purchase" && amountUsd > 0)
            {
                // Purchase event: Use revenue buckets
                bool isFirstPurchase = eventData != null && eventData.ContainsKey("is_first_purchase") 
                    && (bool)eventData["is_first_purchase"];
                
                newValue = _mapping.GetConversionValueForPurchase(amountUsd, isFirstPurchase);
                coarseValue = _mapping.GetCoarseValueForRevenue(amountUsd, 1);  // Window 1 for now
                shouldLock = _mapping.ShouldLockWindow(1, newValue, coarseValue);
                
                #if BOOSTOPS_DEBUG_LOGGING
                BoostOpsLogger.LogDebug("SKAN", 
                    $"Purchase: ${amountUsd:F2} USD → CV={newValue}, Coarse={coarseValue}, Lock={shouldLock}");
                #endif
            }
            else
            {
                // Non-purchase event: Check milestones
                newValue = _mapping.GetConversionValueForMilestone(eventType);
                
                if (newValue < 0)
                {
                    Debug.Log($"[BoostOps SKAN] Event not in milestones: {eventType}");
                    return;
                }
                
                // Milestone events don't lock windows by default
                coarseValue = "low";
                shouldLock = false;
                
                #if BOOSTOPS_DEBUG_LOGGING
                BoostOpsLogger.LogDebug("SKAN", 
                    $"Milestone: {eventType} → CV={newValue}");
                #endif
            }
            
            // Validate conversion value
            if (!_mapping.IsValidConversionValue(newValue))
            {
                Debug.LogWarning($"[BoostOps SKAN] ❌ Conversion value ({newValue}) exceeds max ({_mapping.window1?.max_fine_value ?? 63}). Event: {eventType}");
                return;
            }
            
            // Check downgrade policy
            if (!_mapping.CanUpdateValue(_currentConversionValue, newValue))
            {
                var reason = _mapping.downgrade_behavior == "allow_equal" 
                    ? "new value < current (allow_equal mode)" 
                    : "new value <= current (reject mode)";
                Debug.Log($"[BoostOps SKAN] Conversion value not updated: {reason}. Current: {_currentConversionValue}, New: {newValue}");
                return;
            }
            
            // Perform update based on SKAN version
            if (_skanVersion >= 4 && !string.IsNullOrEmpty(coarseValue))
            {
                // SKAN 4.0: Use coarse value with optional lock
                UpdateConversionValueCoarse(newValue, coarseValue, shouldLock);
            }
            else
            {
                // SKAN 2.x/3.x: Use fine value only
                UpdateConversionValue(newValue);
            }
        }
        
        /// <summary>
        /// Directly update conversion value (0-63) without schema
        /// </summary>
        public void UpdateConversionValue(int conversionValue)
        {
            if (!IsSKANAvailable)
            {
                Debug.LogWarning("[BoostOps SKAN] SKAN not available on this device");
                return;
            }
            
            if (conversionValue < 0 || conversionValue > 63)
            {
                Debug.LogError($"[BoostOps SKAN] Invalid conversion value: {conversionValue} (must be 0-63)");
                return;
            }
            
            if (conversionValue <= _currentConversionValue)
            {
                Debug.LogWarning($"[BoostOps SKAN] Conversion value not updated: {conversionValue} <= {_currentConversionValue}");
                return;
            }
            
            Debug.Log($"[BoostOps SKAN] Updating conversion value: {_currentConversionValue} → {conversionValue}");
            
            _BoostOps_UpdateConversionValue(conversionValue, gameObject.name, nameof(OnNativeConversionValueCallback));
        }
        
        /// <summary>
        /// Update conversion value with coarse value (SKAN 4.0, iOS 16.1+)
        /// </summary>
        public void UpdateConversionValueCoarse(int fineValue, string coarseValue, bool lockWindow = false)
        {
            if (!IsSKANAvailable)
            {
                Debug.LogWarning("[BoostOps SKAN] SKAN not available on this device");
                return;
            }
            
            if (fineValue < 0 || fineValue > 63)
            {
                Debug.LogError($"[BoostOps SKAN] Invalid fine value: {fineValue} (must be 0-63)");
                return;
            }
            
            if (fineValue <= _currentConversionValue)
            {
                Debug.LogWarning($"[BoostOps SKAN] Conversion value not updated: {fineValue} <= {_currentConversionValue}");
                return;
            }
            
            Debug.Log($"[BoostOps SKAN] Updating conversion value (SKAN 4.0): fine={fineValue}, coarse={coarseValue}, lock={lockWindow}");
            
            if (lockWindow)
            {
                _BoostOps_UpdateConversionValueCoarseLocked(fineValue, coarseValue, lockWindow, gameObject.name, nameof(OnNativeConversionValueCallback));
            }
            else
            {
                _BoostOps_UpdateConversionValueCoarse(fineValue, coarseValue, gameObject.name, nameof(OnNativeConversionValueCallback));
            }
        }
        
        #endregion
        
        #region Native Callbacks
        
        /// <summary>
        /// Called by native iOS code when conversion value update completes
        /// </summary>
        private void OnNativeConversionValueCallback(string jsonResponse)
        {
            try
            {
                var response = JsonUtility.FromJson<SKANUpdateResponse>(jsonResponse);
                
                if (response.success)
                {
                    // Update state
                    if (response.fineValue > 0)
                    {
                        _currentConversionValue = response.fineValue;
                    }
                    else if (response.value > 0)
                    {
                        _currentConversionValue = response.value;
                    }
                    
                    if (!string.IsNullOrEmpty(response.coarseValue))
                    {
                        _currentCoarseValue = response.coarseValue;
                    }
                    
                    if (response.lockWindow)
                    {
                        _isWindowLocked = true;
                    }
                    
                    // Persist state
                    PersistState();
                    
                    // Fire event
                    OnConversionValueUpdated?.Invoke(_currentConversionValue, _currentCoarseValue);
                    
                    Debug.Log($"[BoostOps SKAN] ✅ Conversion value updated: {_currentConversionValue} (coarse: {_currentCoarseValue})");
                }
                else
                {
                    Debug.LogWarning($"[BoostOps SKAN] ❌ Conversion value update failed: {response.error}");
                    OnConversionValueUpdateFailed?.Invoke(response.error);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps SKAN] Failed to parse native callback: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Persistence
        
        private const string PREF_KEY_CONVERSION_VALUE = "BoostOps_SKAN_ConversionValue";
        private const string PREF_KEY_COARSE_VALUE = "BoostOps_SKAN_CoarseValue";
        private const string PREF_KEY_WINDOW_LOCKED = "BoostOps_SKAN_WindowLocked";
        
        private void LoadPersistedState()
        {
            _currentConversionValue = PlayerPrefs.GetInt(PREF_KEY_CONVERSION_VALUE, -1);
            _currentCoarseValue = PlayerPrefs.GetString(PREF_KEY_COARSE_VALUE, "low");
            _isWindowLocked = PlayerPrefs.GetInt(PREF_KEY_WINDOW_LOCKED, 0) == 1;
            _cumulativeRevenue = (decimal)PlayerPrefs.GetFloat("BoostOps_SKAN_CumulativeRevenue", 0f);
            _isFirstPurchase = PlayerPrefs.GetInt("BoostOps_SKAN_IsFirstPurchase", 1) == 1;  // Default: true
            
            if (_currentConversionValue >= 0)
            {
                Debug.Log($"[BoostOps SKAN] Loaded persisted state: value={_currentConversionValue}, coarse={_currentCoarseValue}, locked={_isWindowLocked}, LTV=${_cumulativeRevenue}, first={_isFirstPurchase}");
            }
        }
        
        private void PersistState()
        {
            PlayerPrefs.SetInt(PREF_KEY_CONVERSION_VALUE, _currentConversionValue);
            PlayerPrefs.SetString(PREF_KEY_COARSE_VALUE, _currentCoarseValue);
            PlayerPrefs.SetInt(PREF_KEY_WINDOW_LOCKED, _isWindowLocked ? 1 : 0);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Reset SKAN state (for testing only)
        /// </summary>
        public void ResetState()
        {
            _currentConversionValue = -1;
            _currentCoarseValue = "low";
            _isWindowLocked = false;
            
            PlayerPrefs.DeleteKey(PREF_KEY_CONVERSION_VALUE);
            PlayerPrefs.DeleteKey(PREF_KEY_COARSE_VALUE);
            PlayerPrefs.DeleteKey(PREF_KEY_WINDOW_LOCKED);
            PlayerPrefs.Save();
            
            Debug.Log("[BoostOps SKAN] State reset");
        }
        
        #endregion
        
        #region Helper Classes
        
        [Serializable]
        private class SKANUpdateResponse
        {
            public bool success;
            public string error;
            public int value;
            public int fineValue;
            public string coarseValue;
            public bool lockWindow;
            public bool legacy;
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Detect if other attribution SDKs are present that might conflict with SKAN
        /// </summary>
        private void DetectSKANConflicts()
        {
            var conflictingSdks = new List<string>();
            
            // Check for common mobile measurement partner (MMP) SDKs
            if (System.Type.GetType("AppsFlyerSDK.AppsFlyer, Assembly-CSharp") != null) conflictingSdks.Add("AppsFlyer");
            if (System.Type.GetType("BranchIO.Branch, Branch") != null) conflictingSdks.Add("Branch");
            if (System.Type.GetType("com.adjust.sdk.Adjust, Adjust") != null) conflictingSdks.Add("Adjust");
            if (System.Type.GetType("SingularSDK.Singular, Singular") != null) conflictingSdks.Add("Singular");
            if (System.Type.GetType("Kochava.Tracker, Kochava") != null) conflictingSdks.Add("Kochava");
            
            if (conflictingSdks.Count > 0)
            {
                Debug.LogWarning($"[BoostOps SKAN] ⚠️ Detected other attribution SDKs: {string.Join(", ", conflictingSdks)}");
                Debug.LogWarning("[BoostOps SKAN] ⚠️ Multiple SDKs managing SKAN can cause conflicts.");
                Debug.LogWarning("[BoostOps SKAN] ⚠️ Set BoostOpsSKANManager.DisableAutomaticSKAN = true if another SDK is managing SKAN.");
            }
        }
        
        /// <summary>
        /// Detect if this is the first ever install (not a reinstall)
        /// </summary>
        private bool IsFirstEverInstall()
        {
            const string FIRST_INSTALL_KEY = "BoostOps_IsFirstEverInstall";
            
            if (PlayerPrefs.HasKey(FIRST_INSTALL_KEY))
            {
                return false;  // Not first install (reinstall)
            }
            
            // Mark as installed (this persists even if app is deleted and reinstalled)
            PlayerPrefs.SetInt(FIRST_INSTALL_KEY, 1);
            PlayerPrefs.Save();
            return true;  // First install
        }
        
        #endregion
    }
}

