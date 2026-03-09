using UnityEngine;
using System;
using System.Collections.Generic;

namespace BoostOps
{
    /// <summary>
    /// Robust deep link protection system for BoostOps
    /// Captures deep link data immediately to prevent conflicts with other plugins
    /// Uses multiple capture strategies and persistent storage
    /// </summary>
    [DefaultExecutionOrder(-1000)] // Execute very early
    public class BoostOpsDeepLinkProtection : MonoBehaviour
    {
        [Header("Protection Configuration")]
        [SerializeField] private bool enablePersistentStorage = true;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float captureTimeout = 2f; // Max time to wait for deep link
        
        // Singleton access
        public static BoostOpsDeepLinkProtection Instance { get; private set; }
        
        // Protected deep link data
        private static string capturedDeepLink = null;
        private static List<Action<string>> pendingHandlers = new List<Action<string>>();
        
        // --- Events ---
        
        /// <summary>
        /// Fired when a deep link is captured and protected for safe handling
        /// </summary>
        public static event Action<string> OnDeepLinkCaptured;
        
        // Properties
        public static string CapturedDeepLink => capturedDeepLink;
        public static bool HasDeepLink => !string.IsNullOrEmpty(capturedDeepLink);
        
        // Debouncing to prevent duplicate OnApplicationFocus calls during startup
        private float lastFocusCheckTime = 0f;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            // Check if protection instance already exists to prevent duplicates
            var existing = GameObject.Find("BoostOps_DeepLink_Protection");
            if (existing == null)
            {
                // Create protection instance immediately when Unity starts
                // This runs before any scene loads or other scripts initialize
                GameObject protectionObject = new GameObject("BoostOps_DeepLink_Protection");
                protectionObject.AddComponent<BoostOpsDeepLinkProtection>();
                DontDestroyOnLoad(protectionObject);
                BoostOpsLogger.LogInfo("🛡️ Deep link protection initialized");
            }
            else
            {
                BoostOpsLogger.LogDebug("⚠️ Deep link protection already exists, skipping creation");
            }
            
            // Create BoostOpsDynamicLinks early to ensure it catches cold start deep links
            var dynamicLinksExisting = GameObject.Find("BoostOpsDynamicLinks");
            if (dynamicLinksExisting == null)
            {
                GameObject dynamicLinksObject = new GameObject("BoostOpsDynamicLinks");
                dynamicLinksObject.AddComponent<BoostOpsDynamicLinks>();
                DontDestroyOnLoad(dynamicLinksObject);
                BoostOpsLogger.LogInfo("🔗 BoostOpsDynamicLinks initialized early for cold start deep links");
            }
        }
        
        void Awake()
        {
            // Singleton pattern with early capture
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // IMMEDIATELY capture any existing deep link data
                CaptureDeepLinkData();
                
                // Set up ongoing protection
                SetupDeepLinkProtection();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Immediately capture deep link data before other plugins can interfere
        /// </summary>
        private void CaptureDeepLinkData()
        {
            try
            {
                // Strategy 1: Capture Application.absoluteURL immediately
                string absoluteURL = Application.absoluteURL;
                if (!string.IsNullOrEmpty(absoluteURL))
                {
                    capturedDeepLink = absoluteURL;
                    LogDebug($"Captured deep link from Application.absoluteURL: {absoluteURL}");
                    
                    // Store persistently in case other plugins clear it
                    if (enablePersistentStorage)
                    {
                        PlayerPrefs.SetString("boostops_captured_deep_link", absoluteURL);
                        PlayerPrefs.SetString("boostops_deep_link_timestamp", DateTime.UtcNow.ToBinary().ToString());
                        PlayerPrefs.Save();
                    }
                    
                    // Notify any pending handlers
                    NotifyHandlers(absoluteURL);
                }
                
                // Strategy 2: Check for previously stored deep link (app crash recovery)
                else if (enablePersistentStorage)
                {
                    string storedLink = PlayerPrefs.GetString("boostops_captured_deep_link", "");
                    string timestampStr = PlayerPrefs.GetString("boostops_deep_link_timestamp", "");
                    
                    if (!string.IsNullOrEmpty(storedLink) && !string.IsNullOrEmpty(timestampStr))
                    {
                        // Check if the stored link is recent (within last 5 minutes)
                        if (long.TryParse(timestampStr, out long timestampBinary))
                        {
                            DateTime timestamp = DateTime.FromBinary(timestampBinary);
                            if (DateTime.UtcNow - timestamp < TimeSpan.FromMinutes(5))
                            {
                                capturedDeepLink = storedLink;
                                LogDebug($"Recovered deep link from storage: {storedLink}");
                                NotifyHandlers(storedLink);
                            }
                        }
                    }
                }
                
                // Strategy 3: Platform-specific immediate capture
                CaptureplatformSpecificDeepLink();
            }
            catch (Exception ex)
            {
                LogError($"Failed to capture deep link data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set up ongoing deep link protection and monitoring
        /// </summary>
        private void SetupDeepLinkProtection()
        {
            // Subscribe to Unity's deep link event with highest priority
            Application.deepLinkActivated += OnDeepLinkActivated;
            
            // Set up a timeout to clear old stored links
            Invoke(nameof(ClearOldStoredLinks), captureTimeout);
        }
        
        /// <summary>
        /// Poll for intent changes when app gains focus (warm start detection)
        /// This is how AppsFlyer/Branch handle warm starts WITHOUT custom activity
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                // DEBOUNCE: Unity fires OnApplicationFocus multiple times during startup
                // Only check once per second to prevent duplicate events
                float currentTime = Time.realtimeSinceStartup;
                if (currentTime - lastFocusCheckTime < 1.0f)
                {
                    LogDebug("OnApplicationFocus fired too soon - debouncing (preventing duplicate)");
                    return;
                }
                lastFocusCheckTime = currentTime;
                
                LogDebug("App gained focus - checking for intent changes (warm start detection)");
                
                // Capture the current deep link (only notifies if it's new/different)
                CaptureplatformSpecificDeepLink();
            }
        }
        
        /// <summary>
        /// Handle deep link activation while app is running
        /// </summary>
        private void OnDeepLinkActivated(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            
            LogDebug($"Deep link activated: {url}");
            
            // Update our captured deep link
            capturedDeepLink = url;
            
            // Store persistently
            if (enablePersistentStorage)
            {
                PlayerPrefs.SetString("boostops_captured_deep_link", url);
                PlayerPrefs.SetString("boostops_deep_link_timestamp", DateTime.UtcNow.ToBinary().ToString());
                PlayerPrefs.Save();
            }
            
            // Notify handlers
            NotifyHandlers(url);
        }
        
        /// <summary>
        /// Platform-specific deep link capture for additional protection
        /// </summary>
        private void CaptureplatformSpecificDeepLink()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: Try to get intent data directly
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = currentActivity.Call<AndroidJavaObject>("getIntent"))
                {
                    string action = intent.Call<string>("getAction");
                    if (action == "android.intent.action.VIEW")
                    {
                        using (AndroidJavaObject uri = intent.Call<AndroidJavaObject>("getData"))
                        {
                            if (uri != null)
                            {
                                string deepLink = uri.Call<string>("toString");
                                
                                // CHANGED: Allow re-capturing even if we already have a link (for warm starts)
                                // But only notify handlers if it's actually different (prevents duplicates)
                                if (!string.IsNullOrEmpty(deepLink))
                                {
                                    bool isNewLink = deepLink != capturedDeepLink;
                                    
                                    if (isNewLink)
                                    {
                                        capturedDeepLink = deepLink;
                                        LogDebug($"Captured NEW Android intent deep link: {deepLink}");
                                        
                                        if (enablePersistentStorage)
                                        {
                                            PlayerPrefs.SetString("boostops_captured_deep_link", deepLink);
                                            PlayerPrefs.SetString("boostops_deep_link_timestamp", DateTime.UtcNow.ToBinary().ToString());
                                            PlayerPrefs.Save();
                                        }
                                        
                                        NotifyHandlers(deepLink);
                                    }
                                    else
                                    {
                                        LogDebug($"Intent still has same deep link: {deepLink} - skipping duplicate notification");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Android intent capture failed (this is normal): {ex.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS: Additional protection could be added here if needed
            // Unity's Application.absoluteURL should be sufficient for iOS
#endif
        }
        
        /// <summary>
        /// Register a handler for deep link events (safe from conflicts)
        /// </summary>
        public static void RegisterDeepLinkHandler(Action<string> handler)
        {
            if (handler == null) return;
            
            // If we already have a captured deep link, call the handler immediately
            if (HasDeepLink)
            {
                handler(capturedDeepLink);
            }
            else
            {
                // Store the handler for when we do capture a deep link
                pendingHandlers.Add(handler);
            }
            
            // Also subscribe to future events
            OnDeepLinkCaptured += handler;
        }
        
        /// <summary>
        /// Unregister a deep link handler
        /// </summary>
        public static void UnregisterDeepLinkHandler(Action<string> handler)
        {
            if (handler == null) return;
            
            pendingHandlers.Remove(handler);
            OnDeepLinkCaptured -= handler;
        }
        
        /// <summary>
        /// Force refresh deep link data (useful for testing)
        /// </summary>
        public static void RefreshDeepLinkData()
        {
            if (Instance != null)
            {
                Instance.CaptureDeepLinkData();
            }
        }
        
        /// <summary>
        /// Clear any stored deep link data
        /// </summary>
        public static void ClearDeepLinkData()
        {
            capturedDeepLink = null;
            
            if (PlayerPrefs.HasKey("boostops_captured_deep_link"))
            {
                PlayerPrefs.DeleteKey("boostops_captured_deep_link");
                PlayerPrefs.DeleteKey("boostops_deep_link_timestamp");
                PlayerPrefs.Save();
            }
        }
        
        /// <summary>
        /// Check if we're in a conflict situation with other plugins
        /// </summary>
        public static bool DetectDeepLinkConflicts()
        {
            // Check if Application.absoluteURL is empty but we have a stored link
            // This could indicate another plugin cleared it
            bool hasStoredLink = PlayerPrefs.HasKey("boostops_captured_deep_link");
            bool applicationUrlEmpty = string.IsNullOrEmpty(Application.absoluteURL);
            
            if (hasStoredLink && applicationUrlEmpty && !HasDeepLink)
            {
                return true; // Potential conflict detected
            }
            
            return false;
        }
        
        /// <summary>
        /// Get diagnostic information about deep link capture
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"Has Captured Deep Link: {HasDeepLink}");
            info.AppendLine($"Captured Deep Link: {capturedDeepLink ?? "None"}");
            info.AppendLine($"Application.absoluteURL: {Application.absoluteURL ?? "None"}");
            info.AppendLine($"Stored Deep Link: {PlayerPrefs.GetString("boostops_captured_deep_link", "None")}");
            info.AppendLine($"Conflicts Detected: {DetectDeepLinkConflicts()}");
            info.AppendLine($"Pending Handlers: {pendingHandlers.Count}");
            
            return info.ToString();
        }
        
        private void NotifyHandlers(string deepLink)
        {
            // Notify the main event
            OnDeepLinkCaptured?.Invoke(deepLink);
            
            // Notify any pending handlers
            foreach (var handler in pendingHandlers)
            {
                try
                {
                    handler(deepLink);
                }
                catch (Exception ex)
                {
                    LogError($"Deep link handler failed: {ex.Message}");
                }
            }
            
            // Clear pending handlers since they've been notified
            pendingHandlers.Clear();
        }
        
        private void ClearOldStoredLinks()
        {
            // Clear stored links after timeout to prevent stale data
            if (enablePersistentStorage)
            {
                string timestampStr = PlayerPrefs.GetString("boostops_deep_link_timestamp", "");
                if (!string.IsNullOrEmpty(timestampStr))
                {
                    if (long.TryParse(timestampStr, out long timestampBinary))
                    {
                        DateTime timestamp = DateTime.FromBinary(timestampBinary);
                        if (DateTime.UtcNow - timestamp > TimeSpan.FromMinutes(5))
                        {
                            PlayerPrefs.DeleteKey("boostops_captured_deep_link");
                            PlayerPrefs.DeleteKey("boostops_deep_link_timestamp");
                            PlayerPrefs.Save();
                            LogDebug("Cleared old stored deep link data");
                        }
                    }
                }
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[BoostOps Deep Link Protection] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[BoostOps Deep Link Protection] {message}");
        }
        
        void OnDestroy()
        {
            // Clean up event subscriptions
            Application.deepLinkActivated -= OnDeepLinkActivated;
        }
    }
} 