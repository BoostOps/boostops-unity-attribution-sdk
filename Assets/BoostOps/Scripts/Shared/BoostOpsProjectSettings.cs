using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoostOps
{
    /// <summary>
    /// Project-wide BoostOps settings stored as a ScriptableObject in Resources/BoostOps
    /// </summary>
    [CreateAssetMenu(fileName = "BoostOpsProjectSettings", menuName = "BoostOps/Project Settings")]
    public class BoostOpsProjectSettings : ScriptableObject
    {
        [Header("App Identity")]
        public string appleAppStoreId = "";
        public string androidPackageName = "";
        public string androidCertFingerprint = "";
        
        [Header("Additional Store IDs")]
        public string amazonStoreId = "";
        [FormerlySerializedAs("windowsStoreId")]
        public string microsoftStoreId = "";
        public string samsungStoreId = "";
        
        [Header("BoostOps Project Authentication")]
        [Tooltip("BoostOps Project ID (fetched from server during registration/login)")]
        public string projectId = "";  // The actual project ID from the BoostOps backend
        
        [Tooltip("BoostOps Project Key (safe to commit; used only for event ingest).\nFormat: bo_{env}_{publicProjectId}_{randomSuffix}\n")]
        public string projectKey = "";  // (project-level key)
        
        [Tooltip("BoostOps event ingest endpoint URL (always production)")]
        public string ingestUrl = "https://analytics.boostops.io/v1";
        
        [Header("Analytics Event Logging")]
        // NOTE: BoostOps Analytics is automatically enabled/disabled based on useRemoteManagement
        public bool firebaseAnalytics = false;
        public bool unityAnalytics = true;
        
        [Header("Dynamic Links Configuration")]
        public string projectSlug = "";
        public string customDomain = "";
        public string fallbackUrl = "";
        
        [Header("BoostLinks™ Domain Configuration")]
        [SerializeField] 
        [Tooltip("Your domains for BoostLinks™ (e.g., game.example.com or yourslug.boostlink.me)")]
        private List<string> domains = new List<string>();
        
        [SerializeField] 
        [Tooltip("Validate hosts and generate AASA/assetlinks files for all domains")]
        private bool validateAllHosts = true;
        
        [Header("Cross-Promotion Runtime Mode")]
        [Tooltip("Enable remote management via BoostOps servers (requires valid project key). When disabled, uses local cross-promo files only.")]
        public bool useRemoteManagement = false;
        
        [Header("Developer Settings")]
        [Tooltip("Enable detailed debug logging in console (useful for troubleshooting)")]
        public bool debugLogging = false;
        
        [Header("Cached Configuration (Managed by Editor)")]
        [Tooltip("Cached app wall configuration from BoostOps API (auto-updated by editor)")]
        [TextArea(3, 10)]
        public string cachedAppWallsJson = "";
        
        [Tooltip("When app walls cache was last updated")]
        public string appWallsLastUpdated = "";
        
        [Tooltip("Source of the cached app walls data")]
        public string appWallsSource = "";
        
        private static BoostOpsProjectSettings _instance;
        
        /// <summary>
        /// Clear the cached instance and force reload from disk (useful for development/debugging)
        /// </summary>
        public static void ClearCache()
        {
            Debug.Log("[BoostOpsProjectSettings] 🔄 Clearing cached instance - will reload from disk on next GetInstance() call");
            _instance = null;
        }
        
        /// <summary>
        /// Get the settings instance from Resources/BoostOps
        /// </summary>
        public static BoostOpsProjectSettings GetInstance()
        {
            if (_instance == null)
            {
                BoostOpsLogger.LogDebug("ProjectSettings", "🔍 GetInstance() called - attempting to load from Resources...");
                
                _instance = Resources.Load<BoostOpsProjectSettings>("BoostOps/BoostOpsProjectSettings");
                
                if (_instance == null)
                {
                    Debug.LogWarning("[BoostOpsProjectSettings] ⚠️ Project settings not found in Resources/BoostOps. Using default values.");
                    Debug.LogWarning("[BoostOpsProjectSettings] ⚠️ Expected path: Resources/BoostOps/BoostOpsProjectSettings.asset");
                    Debug.LogWarning("[BoostOpsProjectSettings] ⚠️ This usually means the asset file is not included in the build or not in the correct Resources folder.");
                    
                    _instance = CreateInstance<BoostOpsProjectSettings>();
                    Debug.Log("[BoostOpsProjectSettings] 🔧 Created default instance with empty values");
                }
                // else
                // {
                //     BoostOpsLogger.LogInfo("ProjectSettings", $"✅ Successfully loaded settings from Resources");
                //     BoostOpsLogger.LogDebug("ProjectSettings", $"🔍 Loaded projectKey: '{_instance.projectKey}' (length: {_instance.projectKey?.Length ?? 0})");
                //     BoostOpsLogger.LogDebug("ProjectSettings", $"🔍 Loaded appleAppStoreId: '{_instance.appleAppStoreId}'");
                //     BoostOpsLogger.LogDebug("ProjectSettings", $"🔍 Loaded useRemoteManagement: {_instance.useRemoteManagement}");
                // }
            }
            // else: Cached instance - no need to log every access
            
            return _instance;
        }
        
        /// <summary>
        /// Get or create settings (for editor use only) - Always ensures asset file exists
        /// </summary>
        public static BoostOpsProjectSettings GetOrCreateSettings()
        {
#if UNITY_EDITOR
            string assetPath = "Assets/Resources/BoostOps/BoostOpsProjectSettings.asset";
            
            // Check if asset file exists on disk
            bool assetFileExists = System.IO.File.Exists(assetPath);
            
            // Try to load existing asset first
            BoostOpsProjectSettings existingAsset = null;
            if (assetFileExists)
            {
                existingAsset = AssetDatabase.LoadAssetAtPath<BoostOpsProjectSettings>(assetPath);
            }
            
            // If we have a valid asset file, use it
            if (existingAsset != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(existingAsset)))
            {
                _instance = existingAsset;
                return _instance;
            }
            
            // Need to create the asset file
            var currentSettings = GetInstance();
            
            try
            {
                EnsureDirectoryStructure();
                
                var newSettings = CreateInstance<BoostOpsProjectSettings>();
                
                // Copy any existing data from current instance
                if (currentSettings != null)
                {
                    newSettings.projectKey = currentSettings.projectKey;
                    newSettings.projectSlug = currentSettings.projectSlug;
                    newSettings.appleAppStoreId = currentSettings.appleAppStoreId;
                    newSettings.androidCertFingerprint = currentSettings.androidCertFingerprint;
                    newSettings.fallbackUrl = currentSettings.fallbackUrl;
                    newSettings.ingestUrl = currentSettings.ingestUrl;
                }
                
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(newSettings, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                string createdAssetPath = AssetDatabase.GetAssetPath(newSettings);
                if (!string.IsNullOrEmpty(createdAssetPath))
                {
                    _instance = newSettings;
                    return _instance;
                }
                else
                {
                    Debug.LogWarning("[BoostOps] Asset creation returned empty path — Editor code will handle saving");
                    _instance = newSettings;
                    return _instance;
                }
#else
                // DLL build path: return in-memory instance; Editor code saves to disk
                _instance = newSettings;
                return _instance;
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Exception creating project settings: {ex.Message}");
                var fallbackSettings = CreateInstance<BoostOpsProjectSettings>();
                if (currentSettings != null)
                {
                    fallbackSettings.projectKey = currentSettings.projectKey;
                    fallbackSettings.projectSlug = currentSettings.projectSlug;
                    fallbackSettings.appleAppStoreId = currentSettings.appleAppStoreId;
                    fallbackSettings.androidCertFingerprint = currentSettings.androidCertFingerprint;
                    fallbackSettings.fallbackUrl = currentSettings.fallbackUrl;
                    fallbackSettings.ingestUrl = currentSettings.ingestUrl;
                }
                _instance = fallbackSettings;
                return fallbackSettings;
            }
#else
            // In builds, just return the instance from Resources
            return GetInstance();
#endif
        }
        
        /// <summary>
        /// Ensure directory structure exists for project settings
        /// </summary>
        private static void EnsureDirectoryStructure()
        {
#if UNITY_EDITOR
            // Create directories using System.IO
            string fullResourcesPath = System.IO.Path.Combine(Application.dataPath, "Resources");
            string fullBoostOpsPath = System.IO.Path.Combine(fullResourcesPath, "BoostOps");
            
            if (!System.IO.Directory.Exists(fullResourcesPath))
                System.IO.Directory.CreateDirectory(fullResourcesPath);
            if (!System.IO.Directory.Exists(fullBoostOpsPath))
                System.IO.Directory.CreateDirectory(fullBoostOpsPath);
            
            AssetDatabase.Refresh();
            
            // Ensure AssetDatabase recognises the folders
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/BoostOps"))
                AssetDatabase.CreateFolder("Assets/Resources", "BoostOps");
#endif
        }
        
        /// <summary>
        /// Refresh android package name from current project settings
        /// </summary>
        public void RefreshAndroidPackageName()
        {
            androidPackageName = Application.identifier;
        }
        
        /// <summary>
        /// Log current settings for debugging
        /// </summary>
        public void LogCurrentSettings()
        {
            Debug.Log($"[BoostOps] Project Settings Configuration:");
            Debug.Log($"[BoostOps] - Apple App Store ID: {appleAppStoreId}");
            Debug.Log($"[BoostOps] - Android Package: {androidPackageName}");
            Debug.Log($"[BoostOps] - Android Cert Fingerprint: {(!string.IsNullOrEmpty(androidCertFingerprint) ? "***SET***" : "NOT SET")}");
            Debug.Log($"[BoostOps] - Amazon Store ID: {amazonStoreId}");
            Debug.Log($"[BoostOps] - Microsoft Store ID: {microsoftStoreId}");
            Debug.Log($"[BoostOps] - Samsung Store ID: {samsungStoreId}");
            Debug.Log($"[BoostOps] - Project Slug: {projectSlug}");
            Debug.Log($"[BoostOps] - Custom Domain: {customDomain}");
            Debug.Log($"[BoostOps] - BoostOps Analytics: {(useRemoteManagement ? "Enabled (remote mode)" : "Disabled (local mode)")}");
            Debug.Log($"[BoostOps] - Firebase Analytics: {firebaseAnalytics}");
            Debug.Log($"[BoostOps] - Unity Analytics: {unityAnalytics}");
        }
        
        /// <summary>
        /// Validate current settings
        /// </summary>
        public bool ValidateSettings()
        {
            bool isValid = true;
            
            if (string.IsNullOrEmpty(appleAppStoreId))
            {
                Debug.LogWarning("[BoostOps] Apple App Store ID is not set");
                isValid = false;
            }
            
            // Note: Additional store IDs are optional, so we don't validate them as required
            
            if (string.IsNullOrEmpty(androidPackageName))
            {
                Debug.LogWarning("[BoostOps] Android package name is not set");
                isValid = false;
            }
            
            if (string.IsNullOrEmpty(projectSlug))
            {
                Debug.LogWarning("[BoostOps] Project slug is not set");
                isValid = false;
            }
            
            return isValid;
        }
        
        // Constants for domain management
        public const int MAX_DOMAINS = 5;
        
        // Properties for domain management
        public List<string> Domains => new List<string>(domains ?? new List<string>());
        public bool ValidateAllHosts => validateAllHosts;
        
        /// <summary>
        /// Get all configured domains
        /// </summary>
        public List<string> GetAllHosts()
        {
            return domains?.Where(d => !string.IsNullOrEmpty(d)).Distinct().ToList() ?? new List<string>();
        }
        
        /// <summary>
        /// Get the total number of configured domains
        /// </summary>
        public int GetHostCount()
        {
            return GetAllHosts().Count;
        }
        
        /// <summary>
        /// Get the first domain (for backward compatibility with "primary" concept)
        /// </summary>
        public string PrimaryHost => GetAllHosts().FirstOrDefault() ?? "";
        
        /// <summary>
        /// Add a domain with validation
        /// </summary>
        public bool AddDomain(string domain)
        {
            if (domains == null)
                domains = new List<string>();
                
            if (domains.Count >= MAX_DOMAINS)
            {
                Debug.LogWarning($"Maximum domains ({MAX_DOMAINS}) reached.");
                return false;
            }
            
            var cleanDomain = CleanHost(domain);
            if (!ValidateHostFormat(cleanDomain))
            {
                Debug.LogWarning($"Domain '{domain}' is not a valid format.");
                return false;
            }
            
            if (domains.Contains(cleanDomain))
            {
                Debug.LogWarning($"Domain '{cleanDomain}' already exists.");
                return false;
            }
            
            domains.Add(cleanDomain);
            return true;
        }
        
        /// <summary>
        /// Remove a domain
        /// </summary>
        public bool RemoveDomain(string domain)
        {
            if (domains == null)
                return false;
                
            var cleanDomain = CleanHost(domain);
            return domains.Remove(cleanDomain);
        }
        
        /// <summary>
        /// Remove a domain by index
        /// </summary>
        public bool RemoveDomainAt(int index)
        {
            if (domains == null || index < 0 || index >= domains.Count)
                return false;
                
            domains.RemoveAt(index);
            return true;
        }
        
        /// <summary>
        /// Clear all domains
        /// </summary>
        public void ClearDomains()
        {
            if (domains == null)
                domains = new List<string>();
            else
                domains.Clear();
        }
        
        /// <summary>
        /// Set domains from a list
        /// </summary>
        public void SetDomains(List<string> newDomains)
        {
            domains = newDomains?.Where(d => !string.IsNullOrEmpty(d))
                                 .Select(CleanHost)
                                 .Where(ValidateHostFormat)
                                 .Distinct()
                                 .ToList() ?? new List<string>();
        }
        
        /// <summary>
        /// Check if a domain exists
        /// </summary>
        public bool ContainsDomain(string domain)
        {
            if (domains == null)
                return false;
                
            var cleanDomain = CleanHost(domain);
            return domains.Contains(cleanDomain);
        }
        
        /// <summary>
        /// Validate a host format
        /// </summary>
        public static bool ValidateHostFormat(string host)
        {
            if (string.IsNullOrEmpty(host))
                return false;
            
            // Remove protocol if present
            host = host.Replace("https://", "").Replace("http://", "");
            
            // Remove trailing slash
            host = host.TrimEnd('/');
            
            // Basic domain validation
            if (host.Contains(" ") || host.Contains("..") || host.StartsWith(".") || host.EndsWith("."))
                return false;
            
            // Must contain at least one dot (domain.com)
            if (!host.Contains("."))
                return false;
            
            // Check for valid characters (simplified)
            var validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-";
            if (host.Any(c => !validChars.Contains(c)))
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Clean and normalize a host (remove protocol, trailing slash, etc.)
        /// </summary>
        public static string CleanHost(string host)
        {
            if (string.IsNullOrEmpty(host))
                return "";
            
            // Remove protocol
            host = host.Replace("https://", "").Replace("http://", "");
            
            // Remove trailing slash
            host = host.TrimEnd('/');
            
            // Convert to lowercase
            host = host.ToLower();
            
            return host;
        }
        
        /// <summary>
        /// Validation result for configuration
        /// </summary>
        [System.Serializable]
        public class ValidationResult
        {
            public bool IsValid => errors.Count == 0;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
            
            public void AddError(string error)
            {
                errors.Add(error);
            }
            
            public void AddWarning(string warning)
            {
                warnings.Add(warning);
            }
            
            public string GetErrorsString()
            {
                return string.Join("\n", errors);
            }
            
            public string GetWarningsString()
            {
                return string.Join("\n", warnings);
            }
        }
        
        /// <summary>
        /// Validate the domain configuration
        /// </summary>
        public ValidationResult ValidateDomainConfiguration()
        {
            var result = new ValidationResult();
            var allDomains = GetAllHosts();
            
            if (allDomains.Count == 0)
            {
                result.AddError("At least one domain is required");
            }
            
            if (allDomains.Count > MAX_DOMAINS)
            {
                result.AddError($"Too many domains. Maximum {MAX_DOMAINS} allowed.");
            }
            
            foreach (var domain in allDomains)
            {
                if (!ValidateHostFormat(domain))
                {
                    result.AddError($"Domain '{domain}' is not a valid domain format");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate configuration (backward compatibility alias for ValidateDomainConfiguration)
        /// </summary>
        public ValidationResult ValidateConfiguration()
        {
            return ValidateDomainConfiguration();
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Diagnostic method to check directory structure and asset status
        /// </summary>
        public static void DiagnoseDirectoryIssues()
        {
            Debug.Log("[BoostOps] 🔍 Diagnosing BoostOps directory structure...");
            
            // Check physical directories
            string assetsPath = Application.dataPath;
            string boostOpsPath = System.IO.Path.Combine(assetsPath, "BoostOps");
            string resourcesPath = System.IO.Path.Combine(boostOpsPath, "Resources");
            string boostOpsResourcesPath = System.IO.Path.Combine(resourcesPath, "BoostOps");
            
            Debug.Log($"[BoostOps] 📁 Assets path: {assetsPath}");
            Debug.Log($"[BoostOps] 📁 BoostOps folder exists: {System.IO.Directory.Exists(boostOpsPath)}");
            Debug.Log($"[BoostOps] 📁 Resources folder exists: {System.IO.Directory.Exists(resourcesPath)}");
            Debug.Log($"[BoostOps] 📁 BoostOps/Resources/BoostOps folder exists: {System.IO.Directory.Exists(boostOpsResourcesPath)}");
            
            // Check AssetDatabase recognition
            Debug.Log($"[BoostOps] 🗃️ AssetDatabase recognizes Assets/BoostOps: {AssetDatabase.IsValidFolder("Assets/BoostOps")}");
            Debug.Log($"[BoostOps] 🗃️ AssetDatabase recognizes Assets/Resources: {AssetDatabase.IsValidFolder("Assets/Resources")}");
            Debug.Log($"[BoostOps] 🗃️ AssetDatabase recognizes Assets/Resources/BoostOps: {AssetDatabase.IsValidFolder("Assets/Resources/BoostOps")}");
            
            // Check asset file
            string assetPath = "Assets/Resources/BoostOps/BoostOpsProjectSettings.asset";
            bool assetExists = System.IO.File.Exists(assetPath);
            Debug.Log($"[BoostOps] 📄 Settings asset file exists: {assetExists}");
            
            if (assetExists)
            {
                var loadedAsset = AssetDatabase.LoadAssetAtPath<BoostOpsProjectSettings>(assetPath);
                Debug.Log($"[BoostOps] 📄 Asset loads via AssetDatabase: {loadedAsset != null}");
                
                var resourcesAsset = Resources.Load<BoostOpsProjectSettings>("BoostOps/BoostOpsProjectSettings");
                Debug.Log($"[BoostOps] 📄 Asset loads via Resources: {resourcesAsset != null}");
            }
            
            // Check current instance
            var currentInstance = GetInstance();
            string instancePath = currentInstance != null ? AssetDatabase.GetAssetPath(currentInstance) : "NULL";
            Debug.Log($"[BoostOps] 📄 Current instance path: {instancePath}");
            Debug.Log($"[BoostOps] 📄 Current instance is temporary: {instancePath == ""}");
        }
#endif
    }
}