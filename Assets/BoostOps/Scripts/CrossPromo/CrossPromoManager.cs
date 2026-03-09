using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace BoostOps.CrossPromo
{
    /// <summary>
    /// Runtime manager for cross-promotion functionality.
    /// Automatically loads configuration from StreamingAssets and provides
    /// easy-to-use methods for selecting and displaying target games.
    /// </summary>
    public class CrossPromoManager : MonoBehaviour
    {
        public bool loadOnStart = true;
        public bool enableDebugLogs = true;
        public bool enableFrequencyCapping = true;
        public bool resetFrequencyCapsOnStart = false;
        
        // Events
        public System.Action<CrossPromoSettings> OnSettingsLoaded;
        public System.Action<CrossPromoTarget> OnTargetSelected;
        public System.Action<CrossPromoTarget> OnTargetOpened;
        
        // Private fields
        private CrossPromoSettings settings;
        private Dictionary<string, int> frequencyTracker = new Dictionary<string, int>();
        private string currentDate = "";
        private bool isInitialized = false;
        
        // Singleton pattern (optional)
        private static CrossPromoManager instance;
        public static CrossPromoManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<CrossPromoManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("CrossPromoManager");
                        instance = go.AddComponent<CrossPromoManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        
        // Properties
        public bool IsInitialized => isInitialized;
        public CrossPromoSettings Settings => settings;
        public int TargetGameCount => settings?.sources?[0]?.targets?.Length ?? 0;
        
        void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        void Start()
        {
            if (loadOnStart)
            {
                LoadSettings();
            }
        }
        
        /// <summary>
        /// Load cross-promotion settings from StreamingAssets
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                string filePath = Path.Combine(Application.streamingAssetsPath, "cross_promo_local.json");
                
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    settings = JsonUtility.FromJson<CrossPromoSettings>(json);
                    
                    if (settings != null && settings.sources != null && settings.sources.Length > 0)
                    {
                        isInitialized = true;
                        LoadFrequencyData();
                        if (resetFrequencyCapsOnStart)
                        {
                            ResetAllFrequencyCaps();
                        }
                        DebugLog($"✅ Loaded cross-promo settings: {TargetGameCount} target games");
                        OnSettingsLoaded?.Invoke(settings);
                    }
                    else
                    {
                        Debug.LogWarning("[CrossPromoManager] Invalid settings structure in JSON file");
                    }
                }
                else
                {
                    DebugLog($"❌ Cross-promo settings file not found: {filePath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CrossPromoManager] Failed to load settings: {e.Message}");
            }
        }
        
        /// <summary>
        /// Get all target games that are valid for the current platform
        /// </summary>
        public List<CrossPromoTarget> GetValidTargets()
        {
            if (!isInitialized || settings.sources.Length == 0) return new List<CrossPromoTarget>();
            
            var validTargets = new List<CrossPromoTarget>();
            foreach (var target in settings.sources[0].targets)
            {
                if (target.IsValidForCurrentPlatform())
                {
                    validTargets.Add(target);
                }
            }
            
            return validTargets;
        }
        
        /// <summary>
        /// Get valid targets that haven't exceeded their frequency cap
        /// </summary>
        public List<CrossPromoTarget> GetAvailableTargets()
        {
            var validTargets = GetValidTargets();
            
            if (!enableFrequencyCapping)
                return validTargets;
            
            var availableTargets = new List<CrossPromoTarget>();
            foreach (var target in validTargets)
            {
                int currentShows = GetTargetShows(target.id);
                // 0 = unlimited, otherwise check if below frequency cap
                if (target.freqCap == 0 || currentShows < target.freqCap)
                {
                    availableTargets.Add(target);
                }
            }
            
            return availableTargets;
        }
        
        /// <summary>
        /// Select a random target game based on weights and frequency caps
        /// </summary>
        public CrossPromoTarget SelectRandomTarget()
        {
            var availableTargets = GetAvailableTargets();
            
            if (availableTargets.Count == 0)
            {
                DebugLog("No available targets (all may have reached frequency caps)");
                return null;
            }
            
            CrossPromoTarget selectedTarget = null;
            
            // Select based on rotation algorithm
            switch (settings.rotation?.ToLower())
            {
                case "weighted_random":
                    selectedTarget = SelectWeightedRandom(availableTargets);
                    break;
                case "waterfall":
                    selectedTarget = SelectWaterfall(availableTargets);
                    break;
                default:
                    selectedTarget = SelectWeightedRandom(availableTargets);
                    break;
            }
            
            if (selectedTarget != null)
            {
                OnTargetSelected?.Invoke(selectedTarget);
                DebugLog($"Selected target: {selectedTarget.headline} (ID: {selectedTarget.id})");
            }
            
            return selectedTarget;
        }
        
        /// <summary>
        /// Show a target game (increment frequency counter)
        /// </summary>
        public void ShowTarget(CrossPromoTarget target)
        {
            if (target == null) return;
            
            if (enableFrequencyCapping)
            {
                IncrementTargetShows(target.id);
            }
            
            string capDisplay = target.freqCap == 0 ? "unlimited" : target.freqCap.ToString();
            DebugLog($"Showed target: {target.headline} (shows today: {GetTargetShows(target.id)}/{capDisplay})");
        }
        
        /// <summary>
        /// Open the store page for a target game
        /// </summary>
        public void OpenTarget(CrossPromoTarget target)
        {
            if (target == null) return;
            
            string storeUrl = target.GetStoreUrl();
            if (!string.IsNullOrEmpty(storeUrl))
            {
                OpenStoreUrl(storeUrl);
                OnTargetOpened?.Invoke(target);
                DebugLog($"Opened store page: {target.headline} -> {storeUrl}");
            }
            else
            {
                Debug.LogWarning($"[CrossPromoManager] No store URL available for {target.headline} on current platform");
            }
        }
        
        /// <summary>
        /// Get a target game by its ID
        /// </summary>
        public CrossPromoTarget GetTargetById(string id)
        {
            if (!isInitialized || string.IsNullOrEmpty(id)) return null;
            
            return settings.sources[0].targets.FirstOrDefault(t => t.id == id);
        }
        
        /// <summary>
        /// Reset frequency caps for all targets (for today)
        /// </summary>
        public void ResetAllFrequencyCaps()
        {
            frequencyTracker.Clear();
            
            // Delete frequency data (single key)
            PlayerPrefs.DeleteKey("CrossPromo_FrequencyData");
            
            DebugLog($"Reset all frequency caps for {currentDate}");
        }
        
        /// <summary>
        /// Reset frequency cap for a specific target
        /// </summary>
        public void ResetTargetFrequencyCap(string targetId)
        {
            if (frequencyTracker.ContainsKey(targetId))
            {
                frequencyTracker[targetId] = 0;
                SaveFrequencyData();
                DebugLog($"Reset frequency cap for target: {targetId}");
            }
        }
        
        /// <summary>
        /// Get the number of times a target has been shown (today)
        /// </summary>
        public int GetTargetShows(string targetId)
        {
            // Check if date has changed (app running across midnight)
            CheckAndHandleDateChange();
            
            return frequencyTracker.ContainsKey(targetId) ? frequencyTracker[targetId] : 0;
        }
        
        /// <summary>
        /// Check if the date has changed since initialization and reset frequency data if needed
        /// </summary>
        private void CheckAndHandleDateChange()
        {
            string todayDate = System.DateTime.Now.ToString("yyyy-MM-dd");
            if (currentDate != todayDate)
            {
                DebugLog($"Date changed from {currentDate} to {todayDate} - resetting frequency caps");
                
                // Save current data for the old date (if any)
                SaveFrequencyData();
                
                // Reset for new date
                currentDate = todayDate;
                frequencyTracker.Clear();
                
                // Load any existing data for the new date
                LoadFrequencyData();
            }
        }
        
        // Private helper methods
        private CrossPromoTarget SelectWeightedRandom(List<CrossPromoTarget> targets)
        {
            if (targets.Count == 0) return null;
            if (targets.Count == 1) return targets[0];
            
            int totalWeight = targets.Sum(t => t.weight);
            if (totalWeight <= 0) return targets[Random.Range(0, targets.Count)];
            
            int randomValue = Random.Range(0, totalWeight);
            int currentWeight = 0;
            
            foreach (var target in targets)
            {
                currentWeight += target.weight;
                if (randomValue < currentWeight)
                {
                    return target;
                }
            }
            
            return targets[targets.Count - 1]; // Fallback
        }
        
        private CrossPromoTarget SelectWaterfall(List<CrossPromoTarget> targets)
        {
            if (targets.Count == 0) return null;
            
            // Waterfall: Find first game in priority order that hasn't hit frequency cap
            // targets list maintains the priority order from the UI (first = highest priority)
            foreach (var target in targets)
            {
                int currentShows = GetTargetShows(target.id);
                // 0 = unlimited, otherwise check if below frequency cap
                if (target.freqCap == 0 || currentShows < target.freqCap)
                {
                    return target;
                }
            }
            
            // All games have hit their caps - return null
            return null;
        }
        
        private void IncrementTargetShows(string targetId)
        {
            if (!frequencyTracker.ContainsKey(targetId))
            {
                frequencyTracker[targetId] = 0;
            }
            
            frequencyTracker[targetId]++;
            SaveFrequencyData();
        }
        
        private void LoadFrequencyData()
        {
            if (!enableFrequencyCapping) return;
            
            // Get today's date in YYYY-MM-DD format
            currentDate = System.DateTime.Now.ToString("yyyy-MM-dd");
            string data = PlayerPrefs.GetString("CrossPromo_FrequencyData", "");
            
            if (!string.IsNullOrEmpty(data))
            {
                try
                {
                    string[] parts = data.Split('|');
                    if (parts.Length >= 2)
                    {
                        string storedDate = parts[0];
                        
                        // Only load data if it's from today
                        if (storedDate == currentDate)
                        {
                            for (int i = 1; i < parts.Length; i++)
                            {
                                if (string.IsNullOrEmpty(parts[i])) continue;
                                
                                string[] entryParts = parts[i].Split(':');
                                if (entryParts.Length == 2 && int.TryParse(entryParts[1], out int count))
                                {
                                    frequencyTracker[entryParts[0]] = count;
                                }
                            }
                            DebugLog($"Loaded frequency data for {currentDate}: {frequencyTracker.Count} entries");
                        }
                        else
                        {
                            DebugLog($"Ignoring old frequency data from {storedDate} (today is {currentDate})");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CrossPromoManager] Failed to load frequency data: {e.Message}");
                }
            }
            else
            {
                DebugLog($"No frequency data found for {currentDate} - starting fresh");
            }
        }
        
        private void SaveFrequencyData()
        {
            if (!enableFrequencyCapping) return;
            
            try
            {
                var entries = new List<string>();
                foreach (var kvp in frequencyTracker)
                {
                    entries.Add($"{kvp.Key}:{kvp.Value}");
                }
                
                // Format: DATE|target:count|target:count...
                string data = currentDate + "|" + string.Join("|", entries);
                PlayerPrefs.SetString("CrossPromo_FrequencyData", data);
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CrossPromoManager] Failed to save frequency data: {e.Message}");
            }
        }
        

        
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CrossPromoManager] {message}");
            }
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
                Debug.LogError("[CrossPromoManager] Cannot open empty store URL");
                return;
            }

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
                        DebugLog($"Opened App Store sheet for app ID: {appStoreId}");
                        return;
                    }
                    else
                    {
                        DebugLog("Failed to show App Store sheet, falling back to browser");
                    }
                }
            }
#endif
            
            // Fallback to standard URL opening
            try
            {
                Application.OpenURL(storeUrl);
                DebugLog($"Opened store URL in browser: {storeUrl}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CrossPromoManager] Failed to open store URL: {e.Message}");
            }
        }
        
        // Public utility methods for debugging
        public void LogCurrentStatus()
        {
            if (!isInitialized)
            {
                Debug.Log("[CrossPromoManager] Not initialized");
                return;
            }
            
            Debug.Log($"[CrossPromoManager] Status:\n" +
                     $"- Initialized: {isInitialized}\n" +
                     $"- Total targets: {TargetGameCount}\n" +
                     $"- Valid targets: {GetValidTargets().Count}\n" +
                     $"- Available targets: {GetAvailableTargets().Count}\n" +
                     $"- Rotation: {settings.rotation}\n" +
                     $"- Domain: {settings.defaultDomain}");
        }
        
        public void LogFrequencyStatus()
        {
            if (!enableFrequencyCapping)
            {
                Debug.Log("[CrossPromoManager] Frequency capping is disabled");
                return;
            }
            
            Debug.Log($"[CrossPromoManager] Frequency Status for {currentDate}:");
            var validTargets = GetValidTargets();
            foreach (var target in validTargets)
            {
                int shows = GetTargetShows(target.id);
                string capDisplay = target.freqCap == 0 ? "unlimited" : target.freqCap.ToString();
                Debug.Log($"[CrossPromoManager] {target.headline}: {shows}/{capDisplay} shows today");
            }
        }
    }
}