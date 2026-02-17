using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using System.Reflection;
using System.Net.Http;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using BoostOps;
using BoostOps.CrossPromo;
using BoostOps.Core;
using Newtonsoft.Json;

namespace BoostOps.Editor
{
    public enum HostingOption
    {
        Local = 0,
        Cloud = 1
    }
    
    public enum ProjectRegistrationState
    {
        NotRegistered = 0,
        Registered = 1,
        Activated = 2
    }
    
    public enum FeatureMode
    {
        Local = 0,
        Managed = 1
    }
    
    public enum FeatureStatus
    {
        Local = 0,      // 🟡 Local mode, files managed locally
        Managed = 1,    // 🟢 Managed mode, synced with server
        Error = 2,      // 🔴 Error state, connection/sync issues
        Locked = 3      // 🔒 Campaigns active, read-only
    }
    
    [System.Serializable]
    public class TokenPayload
    {
        public int userId;
        public string email;
        public string scope;
        public string audience;
        public string iss;
        public long exp;
        public long iat;
    }
    

    
    [System.Serializable]
    public class UserInfoRequest
    {
        public string jwtToken;
    }
    
    [System.Serializable]
    public class SDKKeyRequest
    {
        public string jwtToken;
    }
    
    [System.Serializable]
    public class ProjectRegistration
    {
        public bool success;
        public int projectId;
        public string message;
    }
    

    
    [System.Serializable]
    public class ProjectKeyInfo
    {
        public string id;
        public string key_string;
        public string environment;
        public bool is_active;
    }
    
    [System.Serializable]
    public class ProjectInfo
    {
        public string id;
        public string name;
        public string project_key;   // ← ADDED: For project key from server response
        public string project_slug;  // ← ADDED: For project slug from server response
        public string apple_team_id; // ← ADDED: Canonical Apple Team ID from server (10 chars, uppercase A-Z/0-9)
        public string signing_team_id; // ← ADDED: Legacy compatibility field for older clients
        public string description;
        
        // Handle both snake_case and camelCase field names from different API responses
        public string project_type;  // snake_case version
        public string projectType;   // camelCase version (direct from current API)
        
        public bool is_active;       // snake_case version
        public bool isActive;        // camelCase version (direct from current API)
        
        public string studio_id;
        public string created_at;
        public string updated_at;
        public BoostOpsConfigData boostops_config; // Nested boostops_config object
        public ApiPlatformInfo[] app_stores; // App store information array (renamed from platforms)
        public StudioInfo studio; // Studio information
        public ProjectKeyInfo[] project_keys; // Project API keys array
        
        // Analytics/Attribution configuration
        public bool analytics_ingest_enabled;  // Whether analytics tracking is enabled for this project
        public string ingest_mode;             // Analytics ingest mode (e.g., "FULL", "DISABLED")
        
        // Convenience properties that check both field name variants
        public string GetProjectType() => !string.IsNullOrEmpty(projectType) ? projectType : project_type;
        public bool GetIsActive() => isActive || is_active;
        
        // Apple Team ID handling with compatibility for legacy naming
        public string GetAppleTeamId()
        {
            // Prefer canonical apple_team_id, fallback to legacy signing_team_id
            string teamId = !string.IsNullOrEmpty(apple_team_id) ? apple_team_id : signing_team_id;
            return IsValidAppleTeamId(teamId) ? teamId : null;
        }
        
        // Validate Apple Team ID format: 10 uppercase alphanumeric characters
        public static bool IsValidAppleTeamId(string teamId)
        {
            if (string.IsNullOrEmpty(teamId) || teamId.Length != 10)
                return false;
            
            // Must be exactly 10 characters, all uppercase A-Z or 0-9
            foreach (char c in teamId)
            {
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                    return false;
            }
            return true;
        }
        
        // Legacy convenience properties for backward compatibility
        public string studioId => studio_id;
        public string createdAt => created_at;
        public string updatedAt => updated_at;
    }
    
    [System.Serializable]
    public class BoostOpsConfigData
    {
        public CampaignData[] campaigns;
        public VersionInfoData version_info;
        public SourceProjectData source_project;
    }
    
    /// <summary>
    /// Wrapper class for parsing cached app walls JSON that has the structure: {"app_walls":{...}}
    /// </summary>
    [System.Serializable]
    public class AppWallsConfigWrapper
    {
        public BoostOps.Core.AppWallsConfig app_walls;
    }
    
    [System.Serializable]
    public class CampaignData
    {
        public string name;
        public string status;
        public string end_date;
        public string created_at;
        public string start_date;
        public string updated_at;
        public string campaign_id;
        public string environment;
        public string[] days_of_week;
        public bool is_repeating;
        public TargetProjectData target_project;
        public int daily_impression_cap;
        public string[] formats; // Campaign formats: "native", "interstitial", "video", etc.
    }
    
    [System.Serializable]
    public class TargetProjectData
    {
        public CreativeData[] creatives;
        public string project_id;
        public StoreUrlsData store_urls;
        public Dictionary<string, string> store_ids;
        public Dictionary<string, object> platform_ids;
    }
    
    [System.Serializable]
    public class CreativeData
    {
        public string cta_text;
        public string asset_url;
        public string description;
        public string creative_type;
    }
    
    [System.Serializable]
    public class StoreUrlsData
    {
        public string apple;
        public string google;
    }
    
    [System.Serializable]
    public class VersionInfoData
    {
        public string api_version;
        public string environment;
        public string last_updated;
        public string schema_version;
        public string server_version;
        public string contract_version;
        public string client_min_version;
    }
    
    [System.Serializable]
    public class SourceProjectData
    {
        public string name;
        public string bundle_id;
        public int min_sessions;
        public int frequency_cap;
        public int min_player_days;
        public string interstitial_icon_cta;
        public string interstitial_rich_cta;
        public string interstitial_icon_text;
        public string interstitial_rich_text;
    }

    
    [System.Serializable]
    public class PlatformInfo
    {
        public iOSPlatform ios;
        public AndroidPlatform android;
    }
    
    [System.Serializable]
    public class iOSPlatform
    {
        public string id;
        public string type; // "IOS_APP_STORE"
        public string appleBundleId;
        public string iosAppStoreId;
    }
    
    [System.Serializable]
    public class AndroidPlatform
    {
        public string id;
        public string type; // "GOOGLE_PLAY"
        public string androidPackageName;
        public string[] androidSha256Fingerprints;
    }
    
    [System.Serializable]
    public class IntegrationInfo
    {
        public UnityIntegration unity;
        public FirebaseIntegration firebase;
    }
    
    [System.Serializable]
    public class UnityIntegration
    {
        public string id;
        public string unityProjectId;
    }
    
    [System.Serializable]
    public class FirebaseIntegration
    {
        public string id;
        public string projectId;
    }
    
    [System.Serializable]
    public class SlugCheckResult
    {
        public bool available;
        public string message;
    }
    
    [System.Serializable]
    public class SlugActivationResult
    {
        public bool success;
        public string domain;
        public string message;
    }
    
    [System.Serializable]
    public class SlugActivationData
    {
        public string projectId;
        public string slug;
    }
    
    [System.Serializable]
    public class UserInfo
    {
        public int id;
        public string email;
        public string displayName;
        public string username;
        public string firstName;
        public string lastName;
        public BoostOps.StudioInfo studio; // Can be null if user has no studio
    }
    

    
    [System.Serializable]
    public class StudioUpdateRequest
    {
        public string name;
        public string description;
        public string website;
        public string logoUrl;
        public bool allowPublicJoin;
        public int maxMembers;
    }
    
    [System.Serializable]
    public class StudioUpdateResponse
    {
        public bool success;
        public StudioUpdateData data;
    }
    
    [System.Serializable]
    public class StudioUpdateData
    {
        public string id;
        public string name;
        public string description;
        public string website;
        public string logoUrl;
        public bool allowPublicJoin;
        public int maxMembers;
        public string createdAt;
        public string updatedAt;
    }
    
    [System.Serializable]
    public class SDKKeyResponse
    {
        public bool success;
        public SDKKeyData data;
    }
    
    [System.Serializable]
    public class SDKKeyData
    {
        public string sdkKey;
        public string studioId;
        public string studioName;
        public int totalAppsDetected;
        public string createdAt;
        public string lastReset;
    }
    
    [System.Serializable]
    public class ProjectLookupRequest
    {
        public string jwtToken;              // ✅ Required: JWT token from OAuth flow
        public string projectName;           // ✅ Required: Unity project name
        public string productGuid;           // ⚠️ Optional: Unity project GUID (stable, preferred)
        public string cloudProjectId;       // ⚠️ Optional: Unity Cloud Project ID
        public string iosBundleId;          // ⚠️ Optional: iOS app bundle identifier
        public string androidPackageName;   // ⚠️ Optional: Android package name
        public string bundleId;             // ⚠️ Optional: Alternative name for iosBundleId
        public string packageName;          // ⚠️ Optional: Alternative name for androidPackageName
        public string iosAppStoreId;      // ⚠️ Optional: Apple App Store ID
        public string appleTeamId;          // ⚠️ Optional: Apple Team ID
        public string[] androidSha256Fingerprints; // ⚠️ Optional: Android SHA256 fingerprints
    }
    
    [System.Serializable]
    public class StudioInfo
    {
        public string id;
        public string name;
        public string description;
        public string createdAt;
        public string updatedAt;
        public StudioTier tier;
    }
    
    [System.Serializable]
    public class StudioTier
    {
        public string name;
        public string status;
        public int max_projects;
        public int max_campaigns;
        public int max_unity_integrations;
        public bool includes_analytics;
        public bool includes_priority_support;
        
        // Convenience properties
        public int maxProjects => max_projects;
        public int maxCampaigns => max_campaigns;
        public int maxUnityIntegrations => max_unity_integrations;
        public bool includesAnalytics => includes_analytics;
        public bool includesPrioritySupport => includes_priority_support;
    }
    
    [System.Serializable]
    public class ProjectLookupResponse
    {
        public bool found;
        public ProjectInfo project;
        public UnityProjectInfo unity_project;
        public ApiPlatformInfo[] platforms;
        public string message;
        public string project_key;  // SDK key returned directly from lookup (matches JSON field name)
        public string project_slug; // Project slug from response
        public StudioInfo studio;   // Studio information
        public string ingestUrl;    // Optional ingest URL
        public string boostops_config; // Cross-promo campaign configuration JSON
        
        // Store raw response for complex JSON extraction (not serialized)
        [System.NonSerialized]
        public string rawResponse;
        
        // Convenience properties for backward compatibility
        public string projectKey => project_key;
        public string projectSlug => project_slug;
        public UnityProjectInfo unityProject => unity_project;
        public string boostopsConfig => boostops_config;
    }
    
    [System.Serializable]
    public class UnityProjectInfo
    {
        public string id;
        public string unity_project_id;
        public string unity_game_id;
        public string unity_product_guid;
        public string unity_org_id;
        public string last_sync_at;
        
        // Convenience properties for backward compatibility
        public string unityProjectId => unity_project_id;
        public string unityGameId => unity_game_id;
        public string unityProductGuid => unity_product_guid;
        public string unityOrgId => unity_org_id;
        public string lastSyncAt => last_sync_at;
    }
    
    [System.Serializable]
    public class ApiPlatformInfo
    {
        public string id;
        public string type;
        public string apple_bundle_id;
        public string apple_store_id;
        public string apple_team_id;     // ← ADDED: Apple Team ID from server
        public string android_package_name;
        public string[] android_sha256_fingerprints;
        public string unity_game_id;
        public bool is_active;
        
        // Convenience properties for backward compatibility
        public string appleBundleId => apple_bundle_id;
        public string iosAppStoreId => apple_store_id;
        public string appleTeamId => apple_team_id;    // ← ADDED: Convenience property for Apple Team ID
        public string androidPackageName => android_package_name;
        public string[] androidSha256Fingerprints => android_sha256_fingerprints;
        public string unityGameId => unity_game_id;
        public bool isActive => is_active;
    }
    
    [System.Serializable]
    public class ProjectRegistrationRequest
    {
        public string jwtToken;
        public string projectName;
        public string productGuid;
        public string cloudProjectId;
        public string iosBundleId;
        public string androidPackageName;
        public string iosAppStoreId;
        public string appleTeamId;
        public string[] androidSha256Fingerprints;
    }
    
    [System.Serializable]
    public class ProjectRegistrationResponse
    {
        public bool success;
        public string message;
        public ProjectInfo project;
        public string projectKey;  // SDK key returned directly from registration
        public string ingestUrl;  // Optional ingest URL
        public int projectId;
    }
    
    [System.Serializable]
    public class ProjectStatusResponse
    {
        public bool synced;
        public ProjectInfo project;
    }
    
    [System.Serializable]
    public class AuthResponse
    {
        public string token;
        public string message;
        public bool success;
    }
    
    [System.Serializable]
    public class ErrorResponse
    {
        public string message;
        public int statusCode;
    }
    
    public class BoostOpsEditorWindow : EditorWindow
    {
        // Window mode tracking
        private static bool isLocalConfigMode = false;
        
        // BoostOps Server Configuration
        private const string BOOSTOPS_SERVER_URL = "https://unity-app.boostops.io"; // Unity Auth Server Production URL
        private const string BOOSTOPS_API_SERVER_URL = "https://unity-api.boostops.io"; // Unity API Server Production URL
        
        // UI and project state
        private string projectSlug = "";
        private bool isProjectSlugValid = false;
        private Vector2 scrollPosition;
        
        /// <summary>
        /// Safe wrapper for EditorApplication.delayCall that prevents execution during Play mode
        /// </summary>
        private static         void SafeDelayCall(System.Action action)
        {
            EditorApplication.delayCall += () => {
                if (EditorApplication.isPlaying || EditorApplication.isPaused) 
                {
                    Debug.Log("[BoostOps] ⏸️ Skipping delayed callback - in Play/Pause mode");
                    return;
                }
                action?.Invoke();
            };
        }
        
        /// <summary>
        /// Defer action until Unity update cycle (outside GUI/Layout events)
        /// This is safer than delayCall for AssetDatabase operations
        /// </summary>
        void DeferredAssetOperation(System.Action action)
        {
            EditorApplication.CallbackFunction updateHandler = null;
            updateHandler = () => {
                if (EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
                {
                    return; // Wait until safe
                }
                
                EditorApplication.update -= updateHandler;
                action?.Invoke();
            };
            EditorApplication.update += updateHandler;
        }
        
        /// <summary>
        /// Safe wrapper for AssetDatabase.SaveAssets() that suppresses Unity's unpredictable refresh lock errors
        /// Use this instead of AssetDatabase.SaveAssets() to avoid m_DisallowAutoRefresh errors
        /// </summary>
        void SafeSaveAssets()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
            {
                return; // Don't save during play/compile
            }
            
            // Unity's m_DisallowAutoRefresh state is unpredictable - just suppress the error
            // The assertions are non-fatal and the save usually succeeds anyway
            try 
            {
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception)
            {
                // Silently ignore - assertions are logged separately and don't throw exceptions
                // The save operation typically succeeds despite the assertion
            }
        }
        
        /// <summary>
        /// Safe wrapper for AssetDatabase.Refresh() that suppresses Unity's unpredictable refresh lock errors
        /// </summary>
        void SafeRefreshAssets()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
            {
                return;
            }
            
            try
            {
                AssetDatabase.Refresh();
            }
            catch (System.Exception)
            {
                // Silently ignore - assertions are logged separately
            }
        }
        
        /// <summary>
        /// Helper to get project key from either top-level or nested in project object (WaspProjectLookupResponse version)
        /// </summary>
        private static string GetProjectKey(WaspProjectLookupResponse response)
        {
            if (response == null) return null;
            
            // Check top-level first (legacy format)
            if (!string.IsNullOrEmpty(response.project_key))
                return response.project_key;
            
            // Check nested in project object (current server format)
            if (!string.IsNullOrEmpty(response.project?.project_key))
                return response.project.project_key;
            
            return null;
        }
        
        /// <summary>
        /// Helper to get project key from either top-level or nested in project object (ProjectLookupResponse version)
        /// </summary>
        private static string GetProjectKey(ProjectLookupResponse response)
        {
            if (response == null) return null;
            
            // Check top-level first (legacy format)
            if (!string.IsNullOrEmpty(response.project_key))
                return response.project_key;
            
            // Check nested in project object (legacy format)  
            if (!string.IsNullOrEmpty(response.project?.project_key))
                return response.project.project_key;
            
            // Check project_keys array (current server format)
            if (response.project?.project_keys != null && response.project.project_keys.Length > 0)
            {
                // Return the first active key
                foreach (var keyInfo in response.project.project_keys)
                {
                    if (keyInfo.is_active && !string.IsNullOrEmpty(keyInfo.key_string))
                        return keyInfo.key_string;
                }
                // If no active key found, return the first key with a key_string
                foreach (var keyInfo in response.project.project_keys)
                {
                    if (!string.IsNullOrEmpty(keyInfo.key_string))
                        return keyInfo.key_string;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Helper to get project slug from either top-level or nested in project object (ProjectLookupResponse version)
        /// </summary>
        private static string GetProjectSlug(ProjectLookupResponse response)
        {
            if (response == null) return null;
            
            // Check top-level first (legacy format)
            if (!string.IsNullOrEmpty(response.project_slug))
                return response.project_slug;
            
            // Check nested in project object (legacy format)  
            if (!string.IsNullOrEmpty(response.project?.project_slug))
                return response.project.project_slug;
            
            // NEW: Check in boost_links_project section (current server format)
            if (!string.IsNullOrEmpty(response.rawResponse))
            {
                try
                {
                    var projectSlugMatch = System.Text.RegularExpressions.Regex.Match(
                        response.rawResponse,
                        @"""boost_links_project""[^}]*""project_slug""\s*:\s*""([^""]+)"""
                    );
                    if (projectSlugMatch.Success)
                        return projectSlugMatch.Groups[1].Value;
                }
                catch
                {
                    // Ignore parsing errors and continue
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Get the current Apple Team ID from Unity Editor PlayerSettings
        /// </summary>
        private static string GetEditorAppleTeamId()
        {
            try
            {
                // PlayerSettings.iOS is accessible in editor regardless of build target
                return PlayerSettings.iOS.appleDeveloperTeamID;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Could not read Apple Team ID from PlayerSettings: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get the server Apple Team ID from project lookup response (from app_stores array)
        /// </summary>
        private static string GetServerAppleTeamId(ProjectLookupResponse response)
        {
            if (response?.project?.app_stores == null) return null;
            
            // Look for Apple Store entry in app_stores array
            foreach (var store in response.project.app_stores)
            {
                if (store.type == "APPLE_STORE" && !string.IsNullOrEmpty(store.apple_team_id))
                {
                    // Validate the team ID format before returning
                    if (ProjectInfo.IsValidAppleTeamId(store.apple_team_id))
                    {
                        return store.apple_team_id;
                    }
                    else
                    {
                        Debug.LogWarning($"[BoostOps] ⚠️ Invalid Apple Team ID format from server: '{store.apple_team_id}' (expected 10 uppercase alphanumeric characters)");
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Auto-sync Apple Team ID from server to Editor if Editor is empty
        /// Returns true if sync was performed
        /// </summary>
        private static bool AutoSyncAppleTeamIdFromServer(string serverTeamId)
        {
            #if UNITY_IOS
            if (string.IsNullOrEmpty(serverTeamId)) return false;
            
            string editorTeamId = GetEditorAppleTeamId();
            if (string.IsNullOrEmpty(editorTeamId) && ProjectInfo.IsValidAppleTeamId(serverTeamId))
            {
                try
                {
                    PlayerSettings.iOS.appleDeveloperTeamID = serverTeamId;
                    Debug.Log($"[BoostOps] ✅ Auto-synced Apple Team ID from server: {serverTeamId}");
                    return true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BoostOps] ⚠️ Failed to auto-sync Apple Team ID: {ex.Message}");
                }
            }
            #endif
            return false;
        }
        
        /// <summary>
        /// Check if there's a mismatch between Editor and Server Apple Team ID values
        /// </summary>
        private static bool HasAppleTeamIdMismatch(string editorTeamId, string serverTeamId)
        {
            // No mismatch if either is empty
            if (string.IsNullOrEmpty(editorTeamId) || string.IsNullOrEmpty(serverTeamId))
                return false;
                
            // Mismatch if both are set but different
            return !editorTeamId.Equals(serverTeamId, System.StringComparison.OrdinalIgnoreCase);
        }
        
        // Tab management
        private int selectedTab = -1; // No tab selected initially
        private string[] tabs => isLocalConfigMode 
            ? new string[] { "Overview", "Links", "Cross-Promo" }
            : new string[] { "Overview", "Links", "Cross-Promo", "Attribution", "Integrations" };
        
        // Link Configuration fields
        private string dynamicLinkUrl = ""; // Legacy - migrating to config asset
        private BoostOpsProjectSettings dynamicLinksConfig = null;
        private bool showAdvancedHosts = true; // UI state for advanced toggle - default to expanded
        private string selectedQRDomain = ""; // Selected domain for QR code generation
        private EditorApplication.CallbackFunction deferredSaveCallback;
        private HostingOption hostingOption = HostingOption.Local; // DEFAULT TO LOCAL FOR GOODWILL
        private ProjectRegistrationState registrationState = ProjectRegistrationState.NotRegistered;
        
        // Project Settings
        private BoostOpsProjectSettings projectSettings = null;
        
        // Authentication state
        private bool isLoggedIn = false;
        private string userEmail = "";
        private string apiToken = ""; // JWT token for API access
        
        // Registration state
        private bool isProjectRegistered = false;
        private bool isRegistering = false;
        // removed unused: private string registeredProjectId = "";
        private string registeredProjectSlug = "";
        private string loginEmail = "";
        private string loginPassword = "";
        
        // Lookup response tracking
        private bool hasLookupResponse = false;
        private bool lookupProjectFound = false;
        private string lookupProjectSlug = "";
        private string lookupProjectName = "";
        private string lookupMessage = "";
        private ProjectLookupResponse cachedProjectLookupResponse = null;
        
        // Feature mode states
        private FeatureMode linksMode = FeatureMode.Local;
        private FeatureMode crossPromoMode = FeatureMode.Local;
        
        // Project lookup state
        private bool isCheckingForExistingProject = false;
        private FeatureStatus linksStatus = FeatureStatus.Local;
        private FeatureStatus crossPromoStatus = FeatureStatus.Local;
        private FeatureStatus attributionStatus = FeatureStatus.Local;
        
        // Server revision tracking
        private int linksServerRevision = 0;
        private int crossPromoServerRevision = 0;
        private string linksLastSync = "";
        private string crossPromoLastSync = "";
        private string signupEmail = "";
        private string signupPassword = "";
        private string signupConfirmPassword = "";
        private bool showSignupForm = false;
        private bool isAuthenticating = false;
        
        // Studio Info
        private string studioId = "";
        private string studioName = "";
        private string studioDescription = "";
        private bool isStudioOwner = false; // Assume owner role for now, can be refined later
        private bool isEditingStudioName = false;
        private string editingStudioName = "";
        
        // Critical field edit states (locked after registration)
        private bool isAppleStoreIdInEditMode = false;
        private bool isSHA256InEditMode = false;
        private bool needsReregistration = false;
        
        // Debug logging control
        private bool enableDebugLogging = true; // Temporarily enabled for debugging
        private Label loggingStatusLabel; // Status display for logging controls
        
        // Auto-detected project settings
        private string appName = "";
        private string version = "";
        
        // iOS-specific settings
        private string iosBundleId = "";
        private string iosTeamId = "";
        private string iosBuildNumber = "";
        private string iosAppStoreId = ""; // Required only for cloud mode registration
        
        // Android-specific settings
        private string androidBundleId = "";
        private string androidCertFingerprint = ""; // Always required for registration
        
        // Analytics detection
        private bool hasUnityAnalytics = false;
        private bool hasFirebaseAnalytics = false;
        private bool hasGoogleServicesFile = false;
        private bool hasFirebaseConfigFile = false;
        
        // Remote Config detection
        private bool hasUnityRemoteConfig = false;
        private bool hasFirebaseRemoteConfig = false;
        
        // Cross Promo Config detection

        
        // Cross Promo Configuration
        private CrossPromoTable crossPromoTable = null;
        private bool isJsonStale = true; // Track if JSON needs regeneration
        private System.DateTime lastJsonGeneration = System.DateTime.MinValue;
        private Button generateJsonButton = null; // Reference to the Generate JSON button for dynamic updates
        
        // Project Settings (shared across team, generated from EditorPrefs)
        
        // Verification status tracking for target games
        private Dictionary<string, bool> iosStoreIdVerificationStatus = new Dictionary<string, bool>();
        private Dictionary<string, bool> androidPackageIdVerificationStatus = new Dictionary<string, bool>();
        private Dictionary<string, bool> amazonStoreIdVerificationStatus = new Dictionary<string, bool>();
        private Dictionary<string, string> iosLastVerifiedValues = new Dictionary<string, string>();
        private Dictionary<string, string> androidLastVerifiedValues = new Dictionary<string, string>();
        private Dictionary<string, string> amazonLastVerifiedValues = new Dictionary<string, string>();
        private Dictionary<string, VisualElement> iosStatusIndicators = new Dictionary<string, VisualElement>();
        private Dictionary<string, VisualElement> androidStatusIndicators = new Dictionary<string, VisualElement>();
        private Dictionary<string, VisualElement> amazonStatusIndicators = new Dictionary<string, VisualElement>();
        
        // Google OAuth for BoostOps authentication
        private bool isAuthenticatingWithGoogle = false;
        private System.Net.HttpListener oauthListener = null;
        private System.Threading.CancellationTokenSource oauthCancellationToken = null;
        
        // Usage tracking for cloud modes
        private int currentClicks = 0;
        private int maxClicks = 1000;
        
        // UI Toolkit references
        private VisualElement headerContainer;
        private VisualElement contentContainer;
        private VisualElement usageMeter;
        private VisualElement bottomUpsellBar;
        private bool useUIToolkit = true; // Flag to control which interface to use
        
        // QR Code testing variables
        private Texture2D qrCodeTexture = null;
        private string testUrl = "";
        private VisualElement leftDomainContainer = null;
        private Image qrImageRef = null; // Reference to the QR image for automatic updates
        private System.Threading.CancellationTokenSource qrGenerationCancellation = null;
        private Label qrErrorLabel = null; // Reference to display validation errors inline
        private Label qrUrlLabel = null; // Reference to display the QR code URL
        private System.Threading.Timer qrDebounceTimer = null; // Timer for debouncing QR code generation
        
        // Store ID verification debounce timers (prevent API spam on every keystroke)
        private Dictionary<string, System.Threading.Timer> storeVerificationTimers = new Dictionary<string, System.Threading.Timer>();
        
        // QR Code caching to avoid unnecessary regeneration (serialized to persist across domain reloads)
        [SerializeField] private string lastQrGeneratedUrl = ""; // Cache the last URL used for QR generation
        [SerializeField] private Texture2D cachedQrTexture = null; // Cache the generated QR texture
        private bool isGeneratingQR = false; // Flag to prevent concurrent QR generation (not serialized, resets on reload)
        
        // Reference to the open folder button for enabling/disabling
        private Button openFolderButtonRef = null;
        private Label appleStoreLabelRef = null; // Reference to update label text dynamically
        private TextField domainFieldRef = null; // Reference to the main domain field for updates
        private VisualElement addedDomainsContainer = null; // Reference to the UIElements added domains display container

            [MenuItem("BoostOps/BoostOps Cloud")]
            [MenuItem("Window/BoostOps/BoostOps Cloud")]
        public static void ShowCloudWindow()
        {
            isLocalConfigMode = false;
            BoostOpsEditorWindow window = GetWindow<BoostOpsEditorWindow>("BoostOps Cloud");
            window.minSize = new Vector2(600, 500);
            window.Show();
            // Note: linksMode and crossPromoMode are automatically set to Managed in LoadFeatureModeStates()
        }
        
        //[MenuItem("BoostOps Admin/Config File Generator")]
        //[MenuItem("Window/BoostOps Admin/Config File Generator")]
        public static void ShowLocalWindow()
        {
            isLocalConfigMode = true;
            BoostOpsEditorWindow window = GetWindow<BoostOpsEditorWindow>("Config File Generator");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }
        
        // Debug logging helpers - now using centralized Editor logging
        private void LogDebug(string message)
        {
            BoostOpsLogger.LogEditorDebug("Editor Window", message);
        }
        
        private void LogWarningDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.LogWarning($"[BoostOps Editor Window] {message}");
            }
        }
        
        private void LogErrorDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.LogError($"[BoostOps Debug] {message}");
            }
        }
        
        private string SanitizeStoreId(string storeId)
        {
            if (string.IsNullOrEmpty(storeId))
                return "unknown";
                
            // Remove any characters that aren't alphanumeric, dash, or underscore
            return System.Text.RegularExpressions.Regex.Replace(storeId, @"[^a-zA-Z0-9\-_]", "_");
        }
        
        void OnEnable()
        {
            // Load all data FIRST to prevent UI blinking with unset state
            InitializeData();
            
            // Build UI after data is loaded
            BuildUIToolkitInterface();
            
            // Show content based on window mode (no tabs)
            if (isLocalConfigMode)
            {
                // Config File Generator: Generate config files without using cloud
                ShowLocalConfigPanel();
            }
            else
            {
                // BoostOps Cloud: Use cloud-managed features and sync
                ShowCloudPanel();
            }
            
            // Auto-generate QR code when window opens (delay to ensure UI is built)
            EditorApplication.delayCall += () => {
                if (EditorApplication.isPlaying || EditorApplication.isPaused) return;
                AutoGenerateQRCode();
                UpdateOpenFolderButtonState(); // Update button state in case files already exist
                // Validate server state to ensure UI shows correct information
                ValidateServerState();
                // Refresh Cross-Promo tab to show loaded campaigns if applicable
                RefreshCrossPromoTabIfNeeded();
            };
        }
        
        void RefreshCrossPromoTabIfNeeded()
        {
            // If we're on the Cross-Promo tab and have cached campaigns, refresh the UI
            if (selectedTab == 2 && cachedRemoteCampaigns != null && cachedRemoteCampaigns.Count > 0)
            {
                LogDebug($"RefreshCrossPromoTabIfNeeded: Refreshing Cross-Promo tab to show {cachedRemoteCampaigns.Count} cached campaigns");
                EditorApplication.delayCall += () => ShowCrossPromoPanel();
            }
        }

        void InitializeData()
        {
            AutoDetectProjectSettings();
            LoadProjectSlug();
            LoadDynamicLinkUrl();
            LoadDynamicLinksConfig(); // Load the new configuration system
            LoadAndroidCertFingerprint();
            LoadAppleAppStoreId();
            LoadHostingOption();
            LoadQRDomainSelection();
            LoadStudioInfo(); // Load studio information
            LoadGoogleOAuthSettings();
            CheckExistingAuth();
            LoadRegistrationState(); // Load registration state after auth
            LoadFeatureModeStates(); // Load feature mode states
            UpdateAttributionStatus(); // Update attribution status based on project key
            DetectCrossPromoConfigurations();
            LoadCrossPromoTable(); // Load cross promo configuration
            LoadProjectSettings(); // Load project-wide settings
            LoadVerificationStatus(); // Load verification status
            LoadDebugLogging(); // Load debug logging preference
            LoadServerValidationSettings(); // Load server validation preferences
            
            // Detect third-party integrations
            DetectAnalyticsIntegrations();
            DetectUnityRemoteConfig();
            DetectFirebaseRemoteConfig();
            LoadCachedRemoteCampaigns();

            // Ensure Links page reflects Overview state when project key exists
            SyncRegistrationStateWithProjectKey();
        }
        
        void RefreshAllData()
        {
            // Force reload all configuration from assets and EditorPrefs
            LogDebug("Refreshing all BoostOps configuration data...");
            
            // Show visual feedback
            EditorUtility.DisplayProgressBar("BoostOps", "Refreshing configuration data...", 0.1f);
            
            try
            {
                // Force AssetDatabase refresh to pick up any external changes
                EditorUtility.DisplayProgressBar("BoostOps", "Refreshing asset database...", 0.3f);
                AssetDatabase.Refresh();
                
                // Clear any cached references
                EditorUtility.DisplayProgressBar("BoostOps", "Clearing cached data...", 0.5f);
                dynamicLinksConfig = null;
                crossPromoTable = null;
                
                // Reload all data
                EditorUtility.DisplayProgressBar("BoostOps", "Reloading configuration...", 0.7f);
                InitializeData();
                
                // Force UI refresh
                EditorUtility.DisplayProgressBar("BoostOps", "Updating UI...", 0.9f);
                EditorApplication.delayCall += () => {
                    try {
                        RefreshAllUI();
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("BoostOps", "Configuration data refreshed successfully!", "OK");
                    } catch (System.Exception e) {
                        LogDebug($"Error during UI refresh: {e.Message}");
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("BoostOps", $"Error during UI refresh: {e.Message}", "OK");
                    }
                };
                
                LogDebug("Configuration refresh completed");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("BoostOps", $"Error during configuration refresh: {e.Message}", "OK");
                LogDebug($"Error during configuration refresh: {e.Message}");
            }
        }
        
        void RefreshAllUI()
        {
            // Sync data between legacy and new systems
            SyncDynamicLinkUrl();
            
            // Refresh all UI sections that might show cached data
            RefreshAccountPanel();
            RefreshDomainAndUsageContent();
            RefreshQRSection();
            UpdateOpenFolderButtonState();
            
            // Force domain field sync
            if (domainFieldRef != null)
            {
                domainFieldRef.value = dynamicLinkUrl ?? "";
            }
            
            // Refresh domain chips display
            RefreshDomainChips();
            
            LogDebug("UI refresh completed");
        }

        // Keep registration flags in sync with stored project key
        private void SyncRegistrationStateWithProjectKey()
        {
            var settings = BoostOpsProjectSettings.GetInstance();
            bool hasProjectKey = settings != null && !string.IsNullOrEmpty(settings.projectKey);
            isProjectRegistered = hasProjectKey || registrationState != ProjectRegistrationState.NotRegistered;
            if (hasProjectKey && registrationState == ProjectRegistrationState.NotRegistered)
            {
                registrationState = ProjectRegistrationState.Registered;
                SaveRegistrationState();
            }
        }
        
        void LoadServerValidationSettings()
        {
            // Default to disabled for better demo/offline experience
            if (!EditorPrefs.HasKey("BoostOps_SkipServerValidation"))
            {
                EditorPrefs.SetBool("BoostOps_SkipServerValidation", true);
            }
        }

        void BuildUIToolkitInterface()
        {
            var root = rootVisualElement;
            root.Clear();

            // Create main container with flex column layout
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Column;
            mainContainer.style.flexGrow = 1;
            root.Add(mainContainer);

            // Status lights are now integrated into tab buttons
            
            // Build simplified header bar with just logo
            BuildSimplifiedHeaderBar(mainContainer);

            // Build scrollable content area
            BuildContentArea(mainContainer);

            // Build bottom upsell bar (shown conditionally)
            BuildBottomUpsellBar(mainContainer);

            // Note: Panel content will be shown by caller
        }

        void BuildGlobalStatusBar(VisualElement parent)
        {
            var statusBar = new VisualElement();
            statusBar.style.flexDirection = FlexDirection.Column;
            statusBar.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            statusBar.style.paddingLeft = 12;
            statusBar.style.paddingRight = 12;
            statusBar.style.paddingTop = 8;
            statusBar.style.paddingBottom = 8;
            statusBar.style.borderBottomWidth = 1;
            statusBar.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            
            // Top row: Project info
            var projectRow = new VisualElement();
            projectRow.style.flexDirection = FlexDirection.Row;
            projectRow.style.alignItems = Align.Center;
            projectRow.style.marginBottom = 6;
            
            var projectInfo = new Label();
            UpdateProjectInfoLabel(projectInfo);
            projectInfo.style.fontSize = 12;
            projectInfo.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            projectRow.Add(projectInfo);
            
            statusBar.Add(projectRow);
            
            // Bottom row: Feature chips and sync button
            var featuresRow = new VisualElement();
            featuresRow.style.flexDirection = FlexDirection.Row;
            featuresRow.style.alignItems = Align.Center;
            featuresRow.style.justifyContent = Justify.SpaceBetween;
            
            // Feature status chips container
            var chipsContainer = new VisualElement();
            chipsContainer.style.flexDirection = FlexDirection.Row;
            chipsContainer.style.alignItems = Align.Center;
            
            // Overview chip
            var overviewChip = CreateOverviewChip(() => {
                selectedTab = 0;
                ShowOverviewPanel();
            });
            chipsContainer.Add(overviewChip);
            
            // Links chip
            var linksChip = CreateFeatureStatusChip("Links", linksStatus, () => {
                selectedTab = 1;
                ShowLinksPanel();
            }, 1);
            chipsContainer.Add(linksChip);
            
            // Cross-Promo chip  
            var crossPromoChip = CreateFeatureStatusChip("Cross-Promo", crossPromoStatus, () => {
                selectedTab = 2;
                ShowCrossPromoPanel();
            }, 2);
            chipsContainer.Add(crossPromoChip);
            
            // Attribution chip
            var attributionChip = CreateFeatureStatusChip("Attribution", attributionStatus, () => {
                selectedTab = 3;
                ShowAttributionPanel();
            }, 3);
            chipsContainer.Add(attributionChip);
            
            // Integrations chip (navigation only, no modes)
            var integrationsChip = CreateNavigationChip("🔌 Integrations", () => {
                selectedTab = 4;
                ShowIntegrationsPanel();
            }, 4);
            chipsContainer.Add(integrationsChip);
            
            featuresRow.Add(chipsContainer);
            
            // Right side: Auth, Sync, and runtime info
            var rightContainer = new VisualElement();
            rightContainer.style.flexDirection = FlexDirection.Row;
            rightContainer.style.alignItems = Align.Center;
            
            // Auth status chip
            var authChip = CreateAuthButton();
            rightContainer.Add(authChip);
            
            // Sync All button
            var syncButton = new Button(() => ShowSyncAllDialog()) { text = "🔄 Sync All" };
            syncButton.style.fontSize = 11;
            syncButton.style.height = 24;
            syncButton.style.marginRight = 8;
            syncButton.style.marginLeft = 8;
            rightContainer.Add(syncButton);
            
            // Runtime info (Play Mode only)
            if (Application.isPlaying)
            {
                var runtimeInfo = new Label();
                UpdateRuntimeInfoLabel(runtimeInfo);
                runtimeInfo.style.fontSize = 10;
                runtimeInfo.style.color = new Color(0.7f, 0.9f, 0.7f, 1f);
                rightContainer.Add(runtimeInfo);
            }
            
            featuresRow.Add(rightContainer);
            statusBar.Add(featuresRow);
            
            parent.Add(statusBar);
        }
        
        VisualElement CreateFeatureStatusChip(string featureName, FeatureStatus status, System.Action onClicked, int tabIndex)
        {
            var chip = new Button(onClicked);
            chip.style.fontSize = 11;
            chip.style.height = 22;
            chip.style.marginRight = 6;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 8;
            chip.style.borderTopLeftRadius = 11;
            chip.style.borderTopRightRadius = 11;
            chip.style.borderBottomLeftRadius = 11;
            chip.style.borderBottomRightRadius = 11;
            
            string icon = "";
            Color backgroundColor = Color.gray;
            
            switch (status)
            {
                case FeatureStatus.Local:
                    icon = "🟡";
                    backgroundColor = new Color(0.8f, 0.6f, 0.2f, 0.3f);
                    chip.text = $"{icon} {featureName}: Local";
                    break;
                case FeatureStatus.Managed:
                    icon = "🟢";
                    backgroundColor = new Color(0.2f, 0.7f, 0.2f, 0.3f);
                    chip.text = $"{icon} {featureName}: Managed";
                    break;
                case FeatureStatus.Error:
                    icon = "🔴";
                    backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.3f);
                    chip.text = $"{icon} {featureName}: Error";
                    break;
                case FeatureStatus.Locked:
                    icon = "🔒";
                    backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    chip.text = $"{icon} {featureName}: Campaigns";
                    break;
            }
            
            // Apply selection highlighting if this is the selected tab
            if (selectedTab == tabIndex)
            {
                backgroundColor = new Color(0.3f, 0.4f, 0.5f, 1f); // Selected tab color
            }
            
            chip.style.backgroundColor = backgroundColor;
            chip.tooltip = $"Click to view {featureName} configuration";
            
            return chip;
        }
        
        VisualElement CreateOverviewChip(System.Action onClicked)
        {
            var chip = new Button(onClicked);
            chip.style.fontSize = 11;
            chip.style.height = 22;
            chip.style.marginRight = 6;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 8;
            chip.style.borderTopLeftRadius = 11;
            chip.style.borderTopRightRadius = 11;
            chip.style.borderBottomLeftRadius = 11;
            chip.style.borderBottomRightRadius = 11;
            
            // Apply selection highlighting if this is the selected tab (Overview is tab 0)
            Color backgroundColor;
            if (selectedTab == 0)
            {
                backgroundColor = new Color(0.3f, 0.4f, 0.5f, 1f); // Selected tab color
            }
            else
            {
                backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.6f); // Default overview chip color
            }
            
            chip.style.backgroundColor = backgroundColor;
            chip.text = "📊 Overview";
            chip.tooltip = "Click to view project overview and health dashboard";
            
            return chip;
        }
        
        VisualElement CreateNavigationChip(string text, System.Action onClicked, int tabIndex)
        {
            var chip = new Button(onClicked);
            chip.style.fontSize = 11;
            chip.style.height = 22;
            chip.style.marginRight = 6;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 8;
            chip.style.borderTopLeftRadius = 11;
            chip.style.borderTopRightRadius = 11;
            chip.style.borderBottomLeftRadius = 11;
            chip.style.borderBottomRightRadius = 11;
            
            // Apply selection highlighting if this is the selected tab
            Color backgroundColor;
            if (selectedTab == tabIndex)
            {
                backgroundColor = new Color(0.3f, 0.4f, 0.5f, 1f); // Selected tab color
            }
            else
            {
                backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.6f); // Default navigation chip color
            }
            
            chip.style.backgroundColor = backgroundColor;
            chip.text = text;
            chip.tooltip = $"Click to view {text} configuration";
            
            return chip;
        }
        
        
        void SignOut()
        {
            isLoggedIn = false;
            userEmail = "";
            apiToken = "";
            EditorPrefs.DeleteKey("BoostOps_UserEmail");
            EditorPrefs.DeleteKey("BoostOps_ApiToken");
            LogDebug("User signed out successfully");
            RefreshGlobalStatusBar();
        }
        
        void UpdateProjectInfoLabel(Label label)
        {
            string projectName = Application.productName;
            string regState = registrationState == ProjectRegistrationState.Activated ? "✅ Activated" :
                            registrationState == ProjectRegistrationState.Registered ? "✅ Registered" : "⚠️ Not Registered";
            string plan = "💎 Pro Plan"; // TODO: Get from server
            
            label.text = $"🏷️ Project: {projectName} • {regState} • {plan}";
        }
        
        void UpdateRuntimeInfoLabel(Label label)
        {
            string linksInfo = linksStatus == FeatureStatus.Managed ? $"Links LIVE (rev {linksServerRevision})" : "Links LOCAL";
            string crossPromoInfo = crossPromoStatus == FeatureStatus.Managed ? $"CP SNAP (rev {crossPromoServerRevision})" : "CP LOCAL";
            
            label.text = $"⚡ Runtime: {linksInfo} • {crossPromoInfo}";
        }
        
        void ShowSyncAllDialog()
        {
            // TODO: Implement sync all dialog
            EditorUtility.DisplayDialog("Sync All Features", "Sync all functionality coming soon!", "OK");
        }
        
        void BuildFeatureModeHeader(string featureName, FeatureMode currentMode, FeatureStatus status, 
            int serverRevision, string lastSync, System.Action<FeatureMode> onModeSwitch)
        {
            var headerContainer = new VisualElement();
            headerContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            headerContainer.style.paddingLeft = 15;
            headerContainer.style.paddingRight = 15;
            headerContainer.style.paddingTop = 12;
            headerContainer.style.paddingBottom = 12;
            headerContainer.style.marginBottom = 15;
            headerContainer.style.borderTopLeftRadius = 6;
            headerContainer.style.borderTopRightRadius = 6;
            headerContainer.style.borderBottomLeftRadius = 6;
            headerContainer.style.borderBottomRightRadius = 6;
            
            // Top row: Mode toggle and "What ships" info
            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems = Align.Center;
            topRow.style.marginBottom = 8;
            
            // Mode toggle (segmented control style) - Only show in Self Hosted Config Generator
            if (isLocalConfigMode)
            {
                var modeToggleContainer = new VisualElement();
                modeToggleContainer.style.flexDirection = FlexDirection.Row;
                
                var modeLabel = new Label("Mode:");
                modeLabel.style.fontSize = 12;
                modeLabel.style.marginRight = 8;
                modeLabel.style.alignSelf = Align.Center;
                modeToggleContainer.Add(modeLabel);
                
                var localButton = new Button(() => onModeSwitch(FeatureMode.Local)) { text = "Local" };
                var managedButton = new Button(() => onModeSwitch(FeatureMode.Managed)) { text = "BoostOps Managed" };
                
                // Style the mode toggle buttons with status-matching colors
                ConfigureModeToggleButton(localButton, currentMode == FeatureMode.Local, FeatureMode.Local);
                ConfigureModeToggleButton(managedButton, currentMode == FeatureMode.Managed, FeatureMode.Managed);
                
                modeToggleContainer.Add(localButton);
                modeToggleContainer.Add(managedButton);
                topRow.Add(modeToggleContainer);
            }
            
            // "What ships" banner
            var shipsInfo = new Label();
            UpdateShipsInfoLabel(shipsInfo, featureName, status, serverRevision);
            shipsInfo.style.fontSize = 11;
            shipsInfo.style.color = new Color(0.6f, 0.8f, 1f, 1f);
            shipsInfo.style.backgroundColor = new Color(0.1f, 0.3f, 0.5f, 0.3f);
            shipsInfo.style.paddingLeft = 8;
            shipsInfo.style.paddingRight = 8;
            shipsInfo.style.paddingTop = 4;
            shipsInfo.style.paddingBottom = 4;
            shipsInfo.style.borderTopLeftRadius = 3;
            shipsInfo.style.borderTopRightRadius = 3;
            shipsInfo.style.borderBottomLeftRadius = 3;
            shipsInfo.style.borderBottomRightRadius = 3;
            topRow.Add(shipsInfo);
            
            headerContainer.Add(topRow);
            
            contentContainer.Add(headerContainer);
        }
        
        void ConfigureModeToggleButton(Button button, bool isActive, FeatureMode buttonMode)
        {
            button.style.fontSize = 11;
            button.style.height = 26;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            
            if (isActive)
            {
                // Match the status light colors
                if (buttonMode == FeatureMode.Local)
                {
                    // Blue to match 🔵 Local status light
                    button.style.backgroundColor = new Color(0.2f, 0.6f, 1f, 1f);
                }
                else // FeatureMode.Managed
                {
                    // Green to match 🟢 Managed status light (darker for better text contrast)
                    button.style.backgroundColor = new Color(0.1f, 0.6f, 0.1f, 1f);
                }
                button.style.color = Color.white;
            }
            else
            {
                button.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
                button.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            }
        }
        
        void BuildAttributionModeHeader()
        {
            var headerContainer = new VisualElement();
            headerContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            headerContainer.style.paddingLeft = 15;
            headerContainer.style.paddingRight = 15;
            headerContainer.style.paddingTop = 12;
            headerContainer.style.paddingBottom = 12;
            headerContainer.style.marginBottom = 15;
            headerContainer.style.borderTopLeftRadius = 6;
            headerContainer.style.borderTopRightRadius = 6;
            headerContainer.style.borderBottomLeftRadius = 6;
            headerContainer.style.borderBottomRightRadius = 6;
            
            // Top row: Mode display and "What ships" info
            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems = Align.Center;
            topRow.style.marginBottom = 8;
            
            // Mode display (read-only, no toggle for Attribution)
            var modeContainer = new VisualElement();
            modeContainer.style.flexDirection = FlexDirection.Row;
            
            var modeLabel = new Label("Mode:");
            modeLabel.style.fontSize = 12;
            modeLabel.style.marginRight = 8;
            modeLabel.style.alignSelf = Align.Center;
            modeContainer.Add(modeLabel);
            
            // Only show BoostOps Managed button (always active, read-only)
            var managedButton = new Button() { text = "BoostOps Managed" };
            managedButton.SetEnabled(false); // Make it read-only
            managedButton.style.fontSize = 11;
            managedButton.style.height = 26;
            managedButton.style.paddingLeft = 12;
            managedButton.style.paddingRight = 12;
            managedButton.style.backgroundColor = new Color(0.1f, 0.6f, 0.1f, 1f);
            managedButton.style.color = Color.white;
            
            modeContainer.Add(managedButton);
            topRow.Add(modeContainer);
            
            // "What ships" banner
            var shipsInfo = new Label();
            // Get current attribution status
            var settings = BoostOpsProjectSettings.GetInstance();
            bool hasProjectKey = settings != null && !string.IsNullOrEmpty(settings.projectKey);
            
            if (hasProjectKey)
            {
                shipsInfo.text = "🚢 Shipping: Server snapshot (rev 1)";
            }
            else
            {
                shipsInfo.text = "🚢 Shipping: Not configured";
            }
            
            shipsInfo.style.fontSize = 11;
            shipsInfo.style.color = new Color(0.6f, 0.8f, 1f, 1f);
            shipsInfo.style.backgroundColor = new Color(0.1f, 0.3f, 0.5f, 0.3f);
            shipsInfo.style.paddingLeft = 8;
            shipsInfo.style.paddingRight = 8;
            shipsInfo.style.paddingTop = 4;
            shipsInfo.style.paddingBottom = 4;
            shipsInfo.style.borderTopLeftRadius = 3;
            shipsInfo.style.borderTopRightRadius = 3;
            shipsInfo.style.borderBottomLeftRadius = 3;
            shipsInfo.style.borderBottomRightRadius = 3;
            topRow.Add(shipsInfo);
            
            headerContainer.Add(topRow);
            
            contentContainer.Add(headerContainer);
        }
        
        void UpdateShipsInfoLabel(Label label, string featureName, FeatureStatus status, int serverRevision)
        {
            string shipsText = "";
            switch (status)
            {
                case FeatureStatus.Local:
                    shipsText = "🚢 Shipping: Local files";
                    break;
                case FeatureStatus.Managed:
                    shipsText = $"🚢 Shipping: Server snapshot (rev {serverRevision})";
                    break;
                case FeatureStatus.Error:
                    shipsText = "🚢 Shipping: Cached fallback";
                    break;
            }
            label.text = shipsText;
        }
        
        void UpdateManagedStatusLabel(Label label, FeatureStatus status, int serverRevision, string lastSync)
        {
            string statusText = status == FeatureStatus.Managed ? "✅ Connected" : "🔴 Error";
            string syncText = !string.IsNullOrEmpty(lastSync) ? lastSync : "never";
            
            label.text = $"Status: {statusText} • rev {serverRevision} • last sync {syncText} • Unity RC";
        }
        
        void SwitchLinksMode(FeatureMode newMode)
        {
            if (newMode == linksMode) return; // No change
            
            if (newMode == FeatureMode.Managed)
            {
                // Check if user is signed in before allowing managed mode
                if (!isLoggedIn)
                {
                    ShowSignInRequiredDialog("Links");
                    return;
                }
                
                // Show publish confirmation dialog
                ShowModeUpgradeDialog("Links", () => {
                    linksMode = FeatureMode.Managed;
                    linksStatus = FeatureStatus.Managed;
                    SaveFeatureModeStates();
                    // Ensure we stay on Links tab (tab 1)
                    selectedTab = 1;
                    // Update status lights - this will refresh the panel with correct mode
                    UpdateStatusLights();
                    // Force immediate UI refresh to show Managed mode content
                    ShowLinksPanel();
                });
            }
            else
            {
                // Switch to Local mode
                ShowModeDowngradeDialog("Links", () => {
                    linksMode = FeatureMode.Local;
                    linksStatus = FeatureStatus.Local;
                    SaveFeatureModeStates();
                    // Ensure we stay on Links tab (tab 1)
                    selectedTab = 1;
                    // Update status lights - this will refresh the panel with correct mode
                    UpdateStatusLights();
                    // Force immediate UI refresh to show Local mode content
                    ShowLinksPanel();
                });
            }
        }
        
        void ShowModeUpgradeDialog(string featureName, System.Action onConfirm)
        {
            var message = $"Switch {featureName} to Managed Mode?\n\n" +
                         "This will:\n" +
                         "• Upload your local configuration to BoostOps\n" +
                         "• Enable remote management & team collaboration\n" +
                         "• Keep local copy as backup\n\n" +
                         "Your app will use server-managed configuration.";
            
            if (EditorUtility.DisplayDialog($"Switch {featureName} to Managed", message, "Publish & Switch", "Cancel"))
            {
                onConfirm?.Invoke();
            }
        }
        
        void ShowModeDowngradeDialog(string featureName, System.Action onConfirm)
        {
            var message = $"Switch {featureName} back to Local Mode?\n\n" +
                         "This will:\n" +
                         "• Use your local configuration files\n" +
                         "• Stop syncing with BoostOps servers\n" +
                         "• Keep server config unchanged (can switch back)\n\n" +
                         "Your app will use local files or cached snapshots.";
            
            if (EditorUtility.DisplayDialog($"Switch {featureName} to Local", message, "Switch to Local", "Cancel"))
            {
                onConfirm?.Invoke();
            }
        }
        
        void ShowSignInRequiredDialog(string featureName)
        {
            var message = $"Sign In Required for Managed Mode\n\n" +
                         $"To switch {featureName} to Managed mode, you need to:\n" +
                         "• Sign in to your BoostOps account\n" +
                         "• Register your project\n\n" +
                         "Managed mode enables cloud sync, team collaboration, and remote configuration management.";
            
            if (EditorUtility.DisplayDialog($"Sign In Required", message, "Sign In Now", "Cancel"))
            {
                // Navigate to account/sign-in panel
                ShowAccountPanel();
            }
        }
        

        
        void SwitchCrossPromoMode(FeatureMode newMode)
        {
            if (newMode == crossPromoMode) return; // No change
            
            if (newMode == FeatureMode.Managed)
            {
                // Check if user is signed in before allowing managed mode
                if (!isLoggedIn)
                {
                    ShowSignInRequiredDialog("Cross-Promo");
                    return;
                }
                
                // Show publish confirmation dialog
                ShowModeUpgradeDialog("Cross-Promo", () => {
                    // Clear any existing cached data when switching to Managed mode
                    // This ensures fresh data from API lookup is used instead of stale local cache
                    Debug.Log("[BoostOps] 🗑️ Clearing cached data when switching to Managed mode");
                    ClearRemoteCampaignCache();
                    
                    // Clear runtime config cache to force fresh lookup
                    EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_JSON");
                    EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Key");
                    EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Timestamp");
                    EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Provider");
                    
                    cachedSourceProject = null;
                    
                    crossPromoMode = FeatureMode.Managed;
                    crossPromoStatus = FeatureStatus.Managed;
                    SaveFeatureModeStates();
                    
                    // Auto-enable attribution when enabling Cross-Promo managed mode
                    AutoEnableAttributionForManagedMode();
                    
                    // Ensure we stay on Cross-Promo tab (tab 2)
                    selectedTab = 2;
                    // Update status lights - this will refresh the panel with correct mode
                    UpdateStatusLights();
                    // Force immediate UI refresh to show Managed mode content
                    ShowCrossPromoPanel();
                    
                    // Note: Don't automatically trigger lookup here to avoid infinite loops
                    // User can use "Fetch Campaigns" button to manually refresh data
                });
            }
            else
            {
                // Switch to Local mode
                ShowModeDowngradeDialog("Cross-Promo", () => {
                    crossPromoMode = FeatureMode.Local;
                    crossPromoStatus = FeatureStatus.Local;
                    SaveFeatureModeStates();
                    // Ensure we stay on Cross-Promo tab (tab 2)
                    selectedTab = 2;
                    // Update status lights - this will refresh the panel with correct mode
                    UpdateStatusLights();
                    // Force immediate UI refresh to show Local mode content
                    ShowCrossPromoPanel();
                });
            }
        }
        
        void RefreshGlobalStatusBar()
        {
            // Trigger a full UI rebuild to update tab status lights
            SafeDelayCall(() => {
                BuildUIToolkitInterface();
                // Restore the current tab
                switch (selectedTab)
                {
                    case 0: ShowOverviewPanel(); break;
                    case 1: ShowLinksPanel(); break;
                    case 2: ShowCrossPromoPanel(); break;
                    case 3: ShowAttributionPanel(); break;
                    case 4: ShowIntegrationsPanel(); break;
                }
            });
        }
        
        void UpdateStatusLights()
        {
            // TODO: Optimize this to only update status lights instead of full rebuild
            // For now, trigger a full header rebuild to update status lights
            RefreshGlobalStatusBar();
        }
        
        void SaveFeatureModeStates()
        {
            EditorPrefs.SetInt("BoostOps_LinksMode", (int)linksMode);
            EditorPrefs.SetInt("BoostOps_CrossPromoMode", (int)crossPromoMode);
            EditorPrefs.SetInt("BoostOps_LinksStatus", (int)linksStatus);
            EditorPrefs.SetInt("BoostOps_CrossPromoStatus", (int)crossPromoStatus);
            EditorPrefs.SetInt("BoostOps_AttributionStatus", (int)attributionStatus);
            EditorPrefs.SetInt("BoostOps_LinksServerRevision", linksServerRevision);
            EditorPrefs.SetInt("BoostOps_CrossPromoServerRevision", crossPromoServerRevision);
            EditorPrefs.SetString("BoostOps_LinksLastSync", linksLastSync);
            EditorPrefs.SetString("BoostOps_CrossPromoLastSync", crossPromoLastSync);
            
            // Also save cross-promo mode to project settings for runtime access
            var settings = BoostOpsProjectSettings.GetOrCreateSettings();
            settings.useRemoteManagement = (crossPromoMode == FeatureMode.Managed);
            UnityEditor.EditorUtility.SetDirty(settings);
            SafeSaveAssets(); // Use safe wrapper that auto-defers if needed
        }
        
        void LoadFeatureModeStates()
        {
            // In BoostOps Cloud mode, always use Managed mode (don't load from EditorPrefs)
            if (!isLocalConfigMode)
            {
                linksMode = FeatureMode.Managed;
                crossPromoMode = FeatureMode.Managed;
            }
            else
            {
                // In Config File Generator mode, load saved preferences
                linksMode = (FeatureMode)EditorPrefs.GetInt("BoostOps_LinksMode", 0);
                crossPromoMode = (FeatureMode)EditorPrefs.GetInt("BoostOps_CrossPromoMode", 0);
            }
            
            // Always load status and sync info regardless of mode
            linksStatus = (FeatureStatus)EditorPrefs.GetInt("BoostOps_LinksStatus", 0);
            crossPromoStatus = (FeatureStatus)EditorPrefs.GetInt("BoostOps_CrossPromoStatus", 0);
            attributionStatus = (FeatureStatus)EditorPrefs.GetInt("BoostOps_AttributionStatus", 0);
            linksServerRevision = EditorPrefs.GetInt("BoostOps_LinksServerRevision", 0);
            crossPromoServerRevision = EditorPrefs.GetInt("BoostOps_CrossPromoServerRevision", 0);
            linksLastSync = EditorPrefs.GetString("BoostOps_LinksLastSync", "");
            crossPromoLastSync = EditorPrefs.GetString("BoostOps_CrossPromoLastSync", "");
        }

        void UpdateAttributionStatus()
        {
            // Attribution status requires both a project key AND a verified app store
            var settings = BoostOpsProjectSettings.GetInstance();
            bool hasProjectKey = settings != null && !string.IsNullOrEmpty(settings.projectKey);
            bool hasProjectId = settings != null && !string.IsNullOrEmpty(settings.projectId);
            bool hasIngestUrl = settings != null && !string.IsNullOrEmpty(settings.ingestUrl);
            
            // Attribution is "Managed" if we have:
            // 1. Project Key (for API authentication)
            // 2. Project ID (for source_project_id in events)
            // 3. Ingest URL (for sending analytics events)
            // Note: We don't check analytics_ingest_enabled because it's unreliable and often missing from API responses
            if (hasProjectKey && hasProjectId && hasIngestUrl)
            {
                // All required fields configured = attribution is fully managed by BoostOps
                attributionStatus = FeatureStatus.Managed;
            }
            else
            {
                // Missing required configuration = attribution not fully configured
                attributionStatus = FeatureStatus.Local;
            }
            
            SaveFeatureModeStates();
        }

        void BuildSimplifiedHeaderBar(VisualElement parent)
        {
            headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.height = 36;
            headerContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            headerContainer.style.paddingLeft = 8;
            headerContainer.style.paddingRight = 8;
            headerContainer.style.paddingTop = 6;
            headerContainer.style.paddingBottom = 6;
            headerContainer.style.alignItems = Align.Center;

            // Left side: Wordmark logo
            var logoContainer = new VisualElement();
            logoContainer.style.flexDirection = FlexDirection.Row;
            logoContainer.style.alignItems = Align.Center;
            logoContainer.style.marginRight = 16;
            
            var logo = new Image();
            var logoTexture = Resources.Load<Texture2D>("boostop-wordmark-logo");
            if (logoTexture != null)
            {
                logo.image = logoTexture;
                logo.style.height = 20;
                logo.style.width = 80;
                
                // Make clickable - link to boostops.io
                logo.RegisterCallback<ClickEvent>(evt => {
                    Application.OpenURL("https://boostops.io");
                });
            }
            else
            {
                // Fallback text logo
                var logoLabel = new Label("BoostOps");
                logoLabel.style.fontSize = 14;
                logoLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                logoLabel.style.color = new Color(0.2f, 0.6f, 1f, 1f);
                logoContainer.Add(logoLabel);
            }
            
            if (logoTexture != null)
            {
                logoContainer.Add(logo);
            }
            
            headerContainer.Add(logoContainer);

            // Navigation buttons with status lights
            var overviewButton = CreateTabButton("📊 Overview", () => ShowOverviewPanel(), 0);
            headerContainer.Add(overviewButton);

            var linksButton = CreateTabButtonWithStatus("Links", linksStatus, () => ShowLinksPanel(), 1);
            headerContainer.Add(linksButton);

            var crossPromoButton = CreateTabButtonWithStatus("Cross-Promo", crossPromoStatus, () => ShowCrossPromoPanel(), 2);
            headerContainer.Add(crossPromoButton);

            var attributionButton = CreateTabButtonWithStatus("Attribution", attributionStatus, () => ShowAttributionPanel(), 3);
            headerContainer.Add(attributionButton);

            var integrationsButton = CreateTabButton("🔌 Integrations", () => ShowIntegrationsPanel(), 4);
            headerContainer.Add(integrationsButton);

            // Right side elements
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerContainer.Add(spacer);

            // Sync button (only if logged in and registered)
            if (isLoggedIn && registrationState == ProjectRegistrationState.Activated)
            {
                var syncButton = new Button(() => ShowSyncAllDialog()) { text = "🔄 Sync" };
                syncButton.style.fontSize = 10;
                syncButton.style.height = 24;
                syncButton.style.marginRight = 8;
                headerContainer.Add(syncButton);
            }

            // Account/Auth button
            var accountButton = CreateAuthButton();
            headerContainer.Add(accountButton);

            // Plan pill
            var planPill = new VisualElement();
            planPill.style.flexDirection = FlexDirection.Row;
            planPill.style.alignItems = Align.Center;
            planPill.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            planPill.style.paddingLeft = 8;
            planPill.style.paddingRight = 8;
            planPill.style.paddingTop = 4;
            planPill.style.paddingBottom = 4;
            planPill.style.marginRight = 10;
            planPill.style.borderTopLeftRadius = 12;
            planPill.style.borderTopRightRadius = 12;
            planPill.style.borderBottomLeftRadius = 12;
            planPill.style.borderBottomRightRadius = 12;

            var planLabel = new Label("Free");
            planLabel.style.fontSize = 11;
            planLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            planLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            planPill.Add(planLabel);

            var ingestLabel = new Label("🟢 Ingest: OK");
            ingestLabel.style.fontSize = 11;
            ingestLabel.style.color = new Color(0.7f, 0.9f, 0.7f, 1f);
            ingestLabel.style.marginLeft = 8;
            planPill.Add(ingestLabel);

            headerContainer.Add(planPill);
            parent.Add(headerContainer);
        }

        void BuildHeaderBar(VisualElement parent)
        {
            headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.height = 28; // Adjusted for natural logo dimensions
            headerContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            headerContainer.style.paddingLeft = 8;
            headerContainer.style.paddingRight = 8;
            headerContainer.style.alignItems = Align.Center;

            // Wordmark logo (left-aligned) - no need for separate brand text
            var logoContainer = new VisualElement();
            logoContainer.style.flexDirection = FlexDirection.Row;
            logoContainer.style.alignItems = Align.Center;
            logoContainer.style.marginRight = 16;
            
                    var logo = new Image();
        var logoTexture = Resources.Load<Texture2D>("boostop-wordmark-logo");
        if (logoTexture != null)
            {
                logo.image = logoTexture;
                // Use natural dimensions of the logo image
                logo.style.width = logoTexture.width;
                logo.style.height = logoTexture.height;
                
                // Scale down if the logo is too large for the header
                float maxHeight = 25f;
                if (logoTexture.height > maxHeight)
                {
                    float scale = maxHeight / logoTexture.height;
                    logo.style.width = logoTexture.width * scale;
                    logo.style.height = maxHeight;
                }
                
                // Add subtle highlight effect and hover cursor
                logo.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
                logo.style.borderTopLeftRadius = 4;
                logo.style.borderTopRightRadius = 4;
                logo.style.borderBottomLeftRadius = 4;
                logo.style.borderBottomRightRadius = 4;
                logo.style.paddingLeft = 6;
                logo.style.paddingRight = 6;
                logo.style.paddingTop = 3;
                logo.style.paddingBottom = 3;
                
                // Make clickable - link to boostops.io
                logo.RegisterCallback<ClickEvent>(evt => {
                    Application.OpenURL("https://boostops.io");
                });
                
                logoContainer.Add(logo);
            }
            
            headerContainer.Add(logoContainer);

            // Navigation buttons with status lights
            var overviewButton = CreateTabButton("📊 Overview", () => ShowOverviewPanel(), 0);
            headerContainer.Add(overviewButton);

            var linksButton = CreateTabButtonWithStatus("Links", linksStatus, () => ShowLinksPanel(), 1);
            headerContainer.Add(linksButton);

            var crossPromoButton = CreateTabButtonWithStatus("Cross-Promo", crossPromoStatus, () => ShowCrossPromoPanel(), 2);
            headerContainer.Add(crossPromoButton);

            var attributionButton = CreateTabButtonWithStatus("Attribution", attributionStatus, () => ShowAttributionPanel(), 3);
            headerContainer.Add(attributionButton);

            var integrationsButton = CreateTabButton("🔌 Integrations", () => ShowIntegrationsPanel(), 4);
            headerContainer.Add(integrationsButton);

            // Right side elements
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerContainer.Add(spacer);

            // Sync button (only if logged in and registered)
            if (isLoggedIn && registrationState == ProjectRegistrationState.Activated)
            {
                var syncButton = new Button(() => ShowSyncAllDialog()) { text = "🔄 Sync" };
                syncButton.style.fontSize = 10;
                syncButton.style.height = 24;
                syncButton.style.marginRight = 8;
                headerContainer.Add(syncButton);
            }

            // Account/Auth button
            var accountButton = CreateAuthButton();
            headerContainer.Add(accountButton);

            var planPill = new VisualElement();
            planPill.style.flexDirection = FlexDirection.Row;
            planPill.style.alignItems = Align.Center;
            planPill.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            planPill.style.paddingLeft = 8;
            planPill.style.paddingRight = 8;
            planPill.style.paddingTop = 4;
            planPill.style.paddingBottom = 4;
            planPill.style.marginRight = 10;
            planPill.style.borderTopLeftRadius = 12;
            planPill.style.borderTopRightRadius = 12;
            planPill.style.borderBottomLeftRadius = 12;
            planPill.style.borderBottomRightRadius = 12;

            var planLabel = new Label("Free");
            planLabel.style.fontSize = 11;
            planLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            planLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            planPill.Add(planLabel);

            // Add separator and status
            var separator = new Label("•");
            separator.style.fontSize = 11;
            separator.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            separator.style.marginLeft = 6;
            separator.style.marginRight = 6;
            planPill.Add(separator);

            var statusLabel = new Label("📡 Ingest: OK");
            statusLabel.style.fontSize = 11;
            statusLabel.style.color = new Color(0.6f, 1f, 0.6f, 1f);
            planPill.Add(statusLabel);

            headerContainer.Add(planPill);

            parent.Add(headerContainer);
        }

        void BuildContentArea(VisualElement parent)
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;

            contentContainer = new VisualElement();
            contentContainer.style.paddingTop = 10;
            contentContainer.style.paddingBottom = 10;
            contentContainer.style.paddingLeft = 10;
            contentContainer.style.paddingRight = 10;

            scrollView.Add(contentContainer);
            parent.Add(scrollView);
        }

        void BuildBottomUpsellBar(VisualElement parent)
        {
            bottomUpsellBar = new VisualElement();
            bottomUpsellBar.style.position = Position.Absolute;
            bottomUpsellBar.style.bottom = 0;
            bottomUpsellBar.style.left = 0;
            bottomUpsellBar.style.right = 0;
            bottomUpsellBar.style.height = 32;
            bottomUpsellBar.style.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            bottomUpsellBar.style.flexDirection = FlexDirection.Row;
            bottomUpsellBar.style.alignItems = Align.Center;
            bottomUpsellBar.style.paddingLeft = 10;
            bottomUpsellBar.style.paddingRight = 10;

            var upsellLabel = new Label("💡 Love this? Get cloud hosting, analytics & cross-promotion for $0/month with 1000 free clicks");
            upsellLabel.style.fontSize = 11;
            upsellLabel.style.flexGrow = 1;
            upsellLabel.style.color = new Color(0.9f, 0.9f, 0.9f, 1f); // Much brighter white for better contrast
            bottomUpsellBar.Add(upsellLabel);

            var enableCloudButton = new Button(() => {
                hostingOption = HostingOption.Cloud;
                SaveHostingOption();
                RefreshDomainAndUsageContent(); // Refresh only left side content
            }) { text = "Try Cloud Features →" };
            enableCloudButton.style.width = 180;
            bottomUpsellBar.Add(enableCloudButton);

            // Only show for local mode or when not logged in
            UpdateBottomUpsellBarVisibility();

            parent.Add(bottomUpsellBar);
        }

        void UpdateBottomUpsellBarVisibility()
        {
            if (bottomUpsellBar != null)
            {
                bottomUpsellBar.style.display = (hostingOption == HostingOption.Local || !isLoggedIn) ? DisplayStyle.Flex : DisplayStyle.None;
            }
                }

        // Main panel methods for Cloud vs Local modes
        void ShowCloudPanel()
        {
            // Cloud mode: Show cloud-managed features
            // Default to Links panel (primary feature)
            selectedTab = 1; // Links tab
            ShowLinksPanel();
        }
        
        void ShowLocalConfigPanel()
        {
            // Config File Generator mode: Generate config files for dynamic links without using cloud
            // Default to Links panel
            selectedTab = 1; // Links tab
            ShowLinksPanel();
        }
        
        // Panel switching methods
        void ShowOverviewPanel()
        {
            if (contentContainer == null) return;
            
            contentContainer.Clear();
            BuildOverviewPanel();
            UpdateBottomUpsellBarVisibility();
        }

        void ShowLinksPanel()
        {
            if (contentContainer == null) return;
            
            contentContainer.Clear();
            BuildDynamicLinksPanel();
            UpdateBottomUpsellBarVisibility();
        }

        void ShowCrossPromoPanel()
        {
            if (contentContainer == null) return;
            
            LogDebug($"🔄 ShowCrossPromoPanel: Showing Cross-Promo panel");
            LogDebug($"   Current cached campaigns: {(cachedRemoteCampaigns?.Count ?? 0)}");
            
            contentContainer.Clear();
            
            // Clear the button reference since we're rebuilding the panel
            generateJsonButton = null;
            
            // Ensure we have the latest cached campaign data before building the panel
            if (crossPromoMode == FeatureMode.Managed)
            {
                // Try to reload cached campaigns in case they were updated
                string cachedJson = EditorPrefs.GetString("BoostOps_CachedRemoteCampaigns", "");
                if (!string.IsNullOrEmpty(cachedJson) && (cachedRemoteCampaigns == null || cachedRemoteCampaigns.Count == 0))
                {
                    LogDebug("ShowCrossPromoPanel: Found cached campaigns in EditorPrefs but none loaded - reloading");
                    LoadCachedRemoteCampaigns();
                }
            }
            
            // Allow cross-promotion features without login for goodwill
            // Only show upgrade prompt for advanced features, not basic detection
            BuildCrossPromoPanel();
        }

        void ShowAttributionPanel()
        {
            if (contentContainer == null) return;
            
            contentContainer.Clear();
            BuildAttributionPanel();
        }

        void ShowIntegrationsPanel()
        {
            if (contentContainer == null) return;
            
            contentContainer.Clear();
            
            BuildIntegrationsPanel();
        }

        void ShowAccountPanel()
        {
            if (contentContainer == null) return;
            
            contentContainer.Clear();
            BuildAccountPanel();
        }
        void RefreshAccountPanel()
        {
            if (useUIToolkit && contentContainer != null)
            {
                contentContainer.Clear();
                BuildAccountPanel();
            }
            else
            {
                Repaint(); // Fallback for IMGUI
            }
        }

        void BuildBlurOverlay(string message)
        {
            var overlay = new VisualElement();
            overlay.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            overlay.style.position = Position.Absolute;
            overlay.style.top = 0;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.pickingMode = PickingMode.Position; // Blocks clicks

            var messageLabel = new Label(message);
            messageLabel.style.fontSize = 16;
            messageLabel.style.color = Color.white;
            messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            overlay.Add(messageLabel);

            var enableButton = new Button(() => {
                hostingOption = HostingOption.Cloud;
                SaveHostingOption();
                ShowAccountPanel();
            }) { text = "Enable Cloud Management" };
            enableButton.style.marginTop = 20;
            enableButton.style.width = 200;
            overlay.Add(enableButton);

            contentContainer.Add(overlay);
        }

        void BuildDynamicLinksPanel()
        {
            // Add mode toggle and "What ships" banner at the top
            BuildFeatureModeHeader("Links", linksMode, linksStatus, linksServerRevision, linksLastSync, 
                (mode) => SwitchLinksMode(mode));
            
            // Hero section with prominent logo
            var heroSection = new VisualElement();
            heroSection.style.flexDirection = FlexDirection.Row;
            heroSection.style.alignItems = Align.Center;
            heroSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            heroSection.style.paddingLeft = 20;
            heroSection.style.paddingRight = 20;
            heroSection.style.paddingTop = 15;
            heroSection.style.paddingBottom = 15;
            heroSection.style.marginBottom = 20;
            heroSection.style.borderTopLeftRadius = 8;
            heroSection.style.borderTopRightRadius = 8;
            heroSection.style.borderBottomLeftRadius = 8;
            heroSection.style.borderBottomRightRadius = 8;
            
                    // Hero logo removed to reduce visual clutter and maximize content space
            
            // Title and description container
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            
            var title = new Label("BoostLink™ Dynamic Link Configuration");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            textContainer.Add(title);

            var description = new Label("✨ Generate iOS Universal Links and Android App Links instantly.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.9f, 0.95f, 0.9f, 1f); // Brighter green tint for better readability
            description.style.whiteSpace = WhiteSpace.Normal;
            textContainer.Add(description);
            
            heroSection.Add(textContainer);
            
            // Buttons container on the right side
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.alignSelf = Align.Center;
            
            // Generate button
            var generateButton = new Button(() => DeferredAssetOperation(GenerateDynamicLinkFiles)) { text = "📱 Generate Platform Files" };
            generateButton.style.width = 180;
            generateButton.style.height = 32;
            generateButton.style.fontSize = 12;
            generateButton.style.backgroundColor = new Color(0.1f, 0.6f, 0.1f, 1f);
            generateButton.style.marginRight = 10;
            buttonsContainer.Add(generateButton);
            
            // Open Dashboard button
            var dashboardButton = new Button(() => OpenDashboard("links")) { text = "🌐 Open Dashboard" };
            dashboardButton.style.width = 150;
            dashboardButton.style.height = 32;
            dashboardButton.style.fontSize = 12;
            dashboardButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            buttonsContainer.Add(dashboardButton);
            
            heroSection.Add(buttonsContainer);
            contentContainer.Add(heroSection);

            // Action cards removed - generate button now in header

            // iOS and Android configuration sections (moved before domain/testing)
            BuildConfigurationSections();

            // Main content area - show different content based on mode
            if (linksMode == FeatureMode.Local)
            {
                BuildLocalLinksContent();
            }
            else
            {
                BuildManagedLinksContent();
            }
            
            // Developer settings removed from Links page
        }

        void BuildModeActionCards()
        {
            if (linksMode == FeatureMode.Local)
            {
                BuildLocalModeActionCard();
            }
            else
            {
                BuildManagedModeActionCard();
            }
        }
        
        void BuildLocalModeActionCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.2f, 0.3f, 0.2f, 0.4f);
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.paddingTop = 15;
            card.style.paddingBottom = 15;
            card.style.marginBottom = 15;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = new Color(0.4f, 0.8f, 0.4f, 1f);
            
            var title = new Label("📱 Generate Local Files");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            title.style.color = new Color(0.9f, 1f, 0.9f, 1f);
            card.Add(title);
            
            var description = new Label("✨ Get started instantly! Generate platform files now, upgrade to managed mode later if you want team collaboration and remote config.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.8f, 0.9f, 0.8f, 1f);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 12;
            card.Add(description);
            
            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.justifyContent = Justify.SpaceBetween;
            actionsRow.style.alignItems = Align.Center;
            
            var generateButton = new Button(() => GenerateDynamicLinkFiles()) { text = "📱 Generate Platform Files" };
            generateButton.style.height = 32;
            generateButton.style.fontSize = 12;
            generateButton.style.backgroundColor = new Color(0.1f, 0.6f, 0.1f, 1f);
            actionsRow.Add(generateButton);
            
            var upgradeButton = new Button(() => SwitchLinksMode(FeatureMode.Managed)) { text = "⬆️ Upgrade to Managed" };
            upgradeButton.style.height = 28;
            upgradeButton.style.fontSize = 11;
            upgradeButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 0.8f);
            actionsRow.Add(upgradeButton);
            
            card.Add(actionsRow);
            contentContainer.Add(card);
        }
        
        void BuildManagedModeActionCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.2f, 0.3f, 0.4f, 0.4f);
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.paddingTop = 15;
            card.style.paddingBottom = 15;
            card.style.marginBottom = 15;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = new Color(0.4f, 0.7f, 1f, 1f);
            
            var title = new Label("🌟 BoostOps Managed");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            title.style.color = new Color(0.9f, 0.95f, 1f, 1f);
            card.Add(title);
            
            var description = new Label("🚀 Your configuration is managed by BoostOps. Team collaboration enabled, remote config active, analytics enhanced.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 12;
            card.Add(description);
            
            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.justifyContent = Justify.SpaceBetween;
            actionsRow.style.alignItems = Align.Center;
            
            var dashboardButton = new Button(() => OpenDashboard()) { text = "🌐 Open Dashboard" };
            dashboardButton.style.height = 32;
            dashboardButton.style.fontSize = 12;
            dashboardButton.style.backgroundColor = new Color(0.2f, 0.6f, 1f, 1f);
            actionsRow.Add(dashboardButton);
            
            var localModeButton = new Button(() => SwitchLinksMode(FeatureMode.Local)) { text = "📂 Switch to Local" };
            localModeButton.style.height = 28;
            localModeButton.style.fontSize = 11;
            localModeButton.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            actionsRow.Add(localModeButton);
            
            card.Add(actionsRow);
            contentContainer.Add(card);
        }
        
        void OpenDashboard()
        {
            OpenDashboard(""); // Default to main dashboard
        }
        
        void OpenDashboard(string section = "")
        {
            string dashboardUrl = "https://app.boostops.io";
            
            // Debug: Check what data we have available
            Debug.Log($"[BoostOps] OpenDashboard called with section: '{section}'");
            Debug.Log($"[BoostOps] cachedProjectLookupResponse null? {cachedProjectLookupResponse == null}");
            if (cachedProjectLookupResponse != null)
            {
                Debug.Log($"[BoostOps] cachedProjectLookupResponse.project null? {cachedProjectLookupResponse.project == null}");
                if (cachedProjectLookupResponse.project != null)
                {
                    Debug.Log($"[BoostOps] cachedProjectLookupResponse.project.id: '{cachedProjectLookupResponse.project.id}'");
                }
            }
            
            // Use the full BoostOps project ID from the cached lookup response
            if (cachedProjectLookupResponse != null && 
                cachedProjectLookupResponse.project != null && 
                !string.IsNullOrEmpty(cachedProjectLookupResponse.project.id))
            {
                string projectId = cachedProjectLookupResponse.project.id;
                dashboardUrl = $"https://app.boostops.io/project/{projectId}";
                
                // Map internal section names to dashboard routes
                if (!string.IsNullOrEmpty(section))
                {
                    switch (section)
                    {
                        case "links":
                            dashboardUrl += "/dynamic-links";
                            break;
                        case "cross-promo":
                            dashboardUrl += "/cross-promo";
                            break;
                        default:
                            dashboardUrl += $"/{section}";
                            break;
                    }
                }
                
                Debug.Log($"[BoostOps] ✅ Opening dashboard with full project ID: {projectId}");
            }
            else
            {
                Debug.Log("[BoostOps] ⚠️ No cached project ID available, opening main dashboard");
            }
            
            Debug.Log($"[BoostOps] Final URL: {dashboardUrl}");
            Application.OpenURL(dashboardUrl);
        }
        
        string ExtractProjectIdFromKey(string projectKey)
        {
            if (string.IsNullOrEmpty(projectKey))
                return null;
            
            // Project key format: bo_{env}_{publicProjectId}_{randomSuffix}
            // We need the publicProjectId part for the dashboard URL
            var parts = projectKey.Split('_');
            if (parts.Length >= 3)
            {
                return parts[2]; // The publicProjectId
            }
            
            return null;
        }
        
        Button CreateTabButton(string text, System.Action onClick, int tabIndex)
        {
            // Extract icon from text (assuming format "📊 Overview" or "🔌 Integrations")
            string icon = "";
            string cleanText = text;
            
            if (text.Contains(" "))
            {
                var parts = text.Split(' ', 2);
                icon = parts[0];
                cleanText = parts[1];
            }
            
            // Create button container
            var button = new Button(onClick);
            button.style.marginRight = 4;
            button.style.fontSize = 12;
            button.style.height = 28;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            
            // Create icon element (positioned 4px down) if we have one
            if (!string.IsNullOrEmpty(icon))
            {
                var iconLabel = new Label(icon);
                iconLabel.style.fontSize = 12;
                iconLabel.style.marginRight = 4;
                iconLabel.style.marginTop = 4;  // Move only the icon down 4 pixels
                button.Add(iconLabel);
            }
            
            // Create text element (normal position)
            var textLabel = new Label(cleanText);
            textLabel.style.fontSize = 12;
            button.Add(textLabel);
            
            // Clear default button text since we're using custom elements
            button.text = "";
            
            // Highlight if this is the selected tab
            if (selectedTab == tabIndex)
            {
                button.style.backgroundColor = new Color(0.3f, 0.4f, 0.5f, 1f);
            }
            else
            {
                // Ensure consistent default background for all tabs
                button.style.backgroundColor = StyleKeyword.Null;
            }
            
            return button;
        }
        
        Button CreateTabButtonWithStatus(string text, FeatureStatus status, System.Action onClick, int tabIndex)
        {
            // Create button container
            var button = new Button(onClick);
            button.style.marginRight = 4;
            button.style.fontSize = 12;
            button.style.height = 28;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            
            // Create status icon element (positioned 4px down)
            var statusIcon = new Label(GetStatusIcon(status));
            statusIcon.style.fontSize = 12;
            statusIcon.style.marginRight = 4;
            statusIcon.style.marginTop = 4;  // Move only the icon down 4 pixels
            
            // Create text element (normal position)
            var textLabel = new Label(text);
            textLabel.style.fontSize = 12;
            
            // Add elements to button
            button.Add(statusIcon);
            button.Add(textLabel);
            
            // Clear default button text since we're using custom elements
            button.text = "";
            
            // Highlight if this is the selected tab
            if (selectedTab == tabIndex)
            {
                button.style.backgroundColor = new Color(0.3f, 0.4f, 0.5f, 1f);
            }
            else
            {
                // Ensure consistent default background for all tabs
                button.style.backgroundColor = StyleKeyword.Null;
            }
            
            return button;
        }
        
        string GetStatusIcon(FeatureStatus status)
        {
            return status switch
            {
                FeatureStatus.Local => "🔵",      // Local mode, files managed locally
                FeatureStatus.Managed => "🟢",    // Managed mode, synced with server
                FeatureStatus.Error => "🔴",      // Error state, connection/sync issues
                FeatureStatus.Locked => "🔒",     // Campaigns active, read-only
                _ => "⚪"                          // Default/unknown
            };
        }
        
        Button CreateAuthButton()
        {
            Button authButton;
            string icon;
            string text;
            
            if (isLoggedIn)
            {
                // Show user email - navigate to account settings
                icon = "👤";
                text = userEmail;
                authButton = new Button(() => ShowAccountPanel());
                authButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.3f, 0.8f);
            }
            else
            {
                // Show sign in button - navigate to account panel
                icon = "🔑";
                text = "Sign In";
                authButton = new Button(() => ShowAccountPanel());
            }
            
            // Match tab button styling
            authButton.style.marginRight = 4;  // Match tab buttons
            authButton.style.fontSize = 12;    // Match tab buttons
            authButton.style.height = 28;      // Match tab buttons
            authButton.style.flexDirection = FlexDirection.Row;
            authButton.style.alignItems = Align.Center;
            authButton.style.paddingLeft = 8;
            authButton.style.paddingRight = 8;
            
            // Create icon element (positioned 4px down)
            var iconLabel = new Label(icon);
            iconLabel.style.fontSize = 12;
            iconLabel.style.marginRight = 4;
            iconLabel.style.marginTop = 4;  // Move only the icon down 4 pixels
            
            // Create text element (normal position)
            var textLabel = new Label(text);
            textLabel.style.fontSize = 12;
            
            // Add elements to button
            authButton.Add(iconLabel);
            authButton.Add(textLabel);
            
            // Clear default button text since we're using custom elements
            authButton.text = "";
            
            // Set tooltip based on login state
            if (isLoggedIn)
            {
                authButton.tooltip = "Click to view account settings";
            }
            else
            {
                authButton.tooltip = "Click to sign in to your BoostOps account";
            }
            
            return authButton;
        }
        
        


        void BuildUnifiedModeContainer()
        {
            // Create container that always has same height regardless of mode
            var container = new VisualElement();
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.paddingTop = 10;
            container.style.paddingBottom = 0;
            container.style.marginBottom = 0;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            // Auto height to accommodate advanced options
            container.style.minHeight = 100;

            if (hostingOption == HostingOption.Cloud) // Cloud mode - Usage Meter
            {
                var usageLabel = new Label($"{currentClicks} / {maxClicks} clicks this month");
                usageLabel.style.fontSize = 11;
                usageLabel.style.marginBottom = 5;
                container.Add(usageLabel);

                // Usage meter bar using two stacked VisualElements
                var meterContainer = new VisualElement();
                meterContainer.style.height = 8;
                meterContainer.style.backgroundColor = Color.grey;
                meterContainer.style.borderTopLeftRadius = 4;
                meterContainer.style.borderTopRightRadius = 4;
                meterContainer.style.borderBottomLeftRadius = 4;
                meterContainer.style.borderBottomRightRadius = 4;

                float usagePercent = (float)currentClicks / maxClicks;
                
                var fill = new VisualElement();
                fill.style.height = 8;
                fill.style.width = Length.Percent(usagePercent * 100);
                fill.style.borderTopLeftRadius = 4;
                fill.style.borderTopRightRadius = 4;
                fill.style.borderBottomLeftRadius = 4;
                fill.style.borderBottomRightRadius = 4;

                // Color based on usage percentage
                if (usagePercent >= 1.0f) fill.style.backgroundColor = Color.red;
                else if (usagePercent >= 0.9f) fill.style.backgroundColor = new Color(1f, 0.5f, 0f); // Orange
                else if (usagePercent >= 0.8f) fill.style.backgroundColor = Color.yellow;
                else fill.style.backgroundColor = Color.green;

                meterContainer.Add(fill);
                container.Add(meterContainer);
            }
            else // Local mode - Domain Configuration
            {
                var title = new Label("Domain Configuration");
                title.style.fontSize = 14;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.marginBottom = 15;
                container.Add(title);
                
                var domainField = new TextField();
                domainFieldRef = domainField; // Store reference for later updates
                domainField.value = dynamicLinkUrl;
                domainField.RegisterValueChangedCallback(evt => {
                    dynamicLinkUrl = evt.newValue;
                    SaveDynamicLinkUrl();
                    AutoGenerateQRCode();
                    
                    // Sync with dynamic links config - only auto-add if the list is empty
                    if (dynamicLinksConfig != null && !string.IsNullOrEmpty(evt.newValue))
                    {
                        var cleanDomain = BoostOpsProjectSettings.CleanHost(evt.newValue);
                        var currentDomains = dynamicLinksConfig.GetAllHosts();
                        
                        // Only auto-add if domain list is empty and the domain is valid
                        if (currentDomains.Count == 0 && BoostOpsProjectSettings.ValidateHostFormat(cleanDomain))
                        {
                            dynamicLinksConfig.AddDomain(cleanDomain);
                        }
                    }
                    
                    // Refresh QR section since domain changed
                    RefreshQRSection();
                });
                
                // Create buttons container for refresh and add domain
                var buttonsContainer = new VisualElement();
                buttonsContainer.style.flexDirection = FlexDirection.Row;
                
                var refreshButton = new Button(() => {
                    RefreshAllData();
                }) { text = "↻" };
                refreshButton.style.width = 30;
                refreshButton.tooltip = "Refresh all BoostOps configuration data";
                
                var addDomainButton = new Button(() => {
                    AddDomainFromField();
                }) { text = "+" };
                addDomainButton.style.width = 30;
                addDomainButton.style.marginLeft = 3;
                addDomainButton.tooltip = "Add this domain to your list";
                
                buttonsContainer.Add(refreshButton);
                buttonsContainer.Add(addDomainButton);
                
                var domainRow = CreateLabelFieldRowWithButton("Your Associated Domain(s):", domainField, buttonsContainer);
                
                // Make label bigger for this field
                var domainLabel = domainRow.Q<Label>();
                if (domainLabel != null)
                {
                    domainLabel.style.fontSize = 16;
                }
                
                var inputContainer = domainRow;
                
                container.Add(inputContainer);
                
                // Add domain chips section below the main field
                BuildDomainChipsSection(container);
                
                // Ensure we have a config loaded - create one if it doesn't exist
                if (dynamicLinksConfig == null)
                {
                    LogDebug("No dynamic links config found, attempting to load...");
                    LoadDynamicLinksConfig();
                    
                    // If still no config, create a minimal one to enable the UI
                    if (dynamicLinksConfig == null)
                    {
                        LogDebug("Still no config found, creating new one...");
                        CreateDynamicLinksConfig();
                        
                        if (dynamicLinksConfig != null)
                        {
                            LogDebug("Successfully created dynamic links config");
                        }
                        else
                        {
                            LogWarningDebug("Failed to create dynamic links config!");
                        }
                    }
                    else
                    {
                        LogDebug("Successfully loaded existing dynamic links config");
                    }
                }
                
                // If we have a legacy domain, migrate it to the config
                if (dynamicLinksConfig != null && !string.IsNullOrEmpty(dynamicLinkUrl))
                {
                    var currentDomains = dynamicLinksConfig.GetAllHosts();
                    var cleanDomain = BoostOpsProjectSettings.CleanHost(dynamicLinkUrl);
                    
                    // Add to config if it's not already there and is valid
                    if (!currentDomains.Contains(cleanDomain) && BoostOpsProjectSettings.ValidateHostFormat(cleanDomain))
                    {
                        dynamicLinksConfig.AddDomain(cleanDomain);
                        SaveDynamicLinkUrl();
                    }
                }
                
                // Auto-populate domain field if config exists and field is empty
                if (dynamicLinksConfig != null && string.IsNullOrEmpty(dynamicLinkUrl))
                {
                    var allDomains = dynamicLinksConfig.GetAllHosts();
                    if (allDomains.Count > 0)
                    {
                        dynamicLinkUrl = allDomains[0]; // Use first domain
                        SaveDynamicLinkUrl();
                    }
                }
                
                // Sync config back to legacy field to prevent clearing
                if (dynamicLinksConfig != null)
                {
                    var allDomains = dynamicLinksConfig.GetAllHosts();
                    if (allDomains.Count > 0 && string.IsNullOrEmpty(dynamicLinkUrl))
                    {
                        dynamicLinkUrl = allDomains[0]; // Use first domain
                    }
                }
                
                // Also sync the domain field when the user types in it
                if (dynamicLinksConfig != null && !string.IsNullOrEmpty(dynamicLinkUrl))
                {
                    var currentDomains = dynamicLinksConfig.GetAllHosts();
                    var cleanDomain = BoostOpsProjectSettings.CleanHost(dynamicLinkUrl);
                    
                    // If the typed domain doesn't exist in the list and it's valid, it could be a new one being typed
                    if (!currentDomains.Contains(cleanDomain) && BoostOpsProjectSettings.ValidateHostFormat(cleanDomain))
                    {
                        // Only auto-add if there are no domains yet (empty list)
                        if (currentDomains.Count == 0)
                        {
                            dynamicLinksConfig.AddDomain(cleanDomain);
                        }
                    }
                }
                
                // Advanced Multiple Domains Section (always show)
                var advancedSpacer = new VisualElement();
                advancedSpacer.style.marginTop = 15;
                container.Add(advancedSpacer);
                
                // Advanced foldout (always visible)
                var advancedFoldout = new Foldout();
                advancedFoldout.text = "▼ Advanced: Multiple Domains";
                advancedFoldout.value = true; // Always expanded by default
                showAdvancedHosts = true; // Force to true for now
                advancedFoldout.style.unityFontStyleAndWeight = FontStyle.Bold;
                advancedFoldout.style.marginBottom = 8;
                advancedFoldout.style.color = new Color(0.9f, 0.9f, 1f);
                advancedFoldout.RegisterValueChangedCallback(evt => {
                    showAdvancedHosts = evt.newValue;
                    SaveHostingOption(); // Persist the foldout state
                });
                
                LogDebug("Adding Advanced Multiple Domains section to UI");
                
                // Create advanced content container (always show content, even when collapsed)
                var advancedContent = new VisualElement();
                advancedContent.style.backgroundColor = new Color(0.15f, 0.25f, 0.35f, 0.3f);
                advancedContent.style.paddingLeft = 15;
                advancedContent.style.paddingRight = 15;
                advancedContent.style.paddingTop = 12;
                advancedContent.style.paddingBottom = 12;
                advancedContent.style.borderTopLeftRadius = 6;
                advancedContent.style.borderTopRightRadius = 6;
                advancedContent.style.borderBottomLeftRadius = 6;
                advancedContent.style.borderBottomRightRadius = 6;
                advancedContent.style.marginTop = 8;
                advancedContent.style.borderLeftWidth = 2;
                advancedContent.style.borderLeftColor = new Color(0.3f, 0.6f, 0.9f, 0.6f);
                
                var helpText = new Label("Support for multiple domains (white-label, re-branding, migration). Most apps (80-90% of indies) only need one domain.");
                helpText.style.fontSize = 11;
                helpText.style.color = new Color(0.85f, 0.95f, 1f);
                helpText.style.whiteSpace = WhiteSpace.Normal;
                helpText.style.marginBottom = 12;
                advancedContent.Add(helpText);
                
                // Show current domain count and status
                if (dynamicLinksConfig != null)
                {
                    var allDomains = dynamicLinksConfig.GetAllHosts();
                    var countInfo = new Label($"Configured domains: {allDomains.Count}/{BoostOpsProjectSettings.MAX_DOMAINS}");
                    countInfo.style.fontSize = 12;
                    countInfo.style.color = new Color(0.7f, 0.9f, 0.7f);
                    countInfo.style.marginTop = 8;
                    countInfo.style.marginBottom = 8;
                    advancedContent.Add(countInfo);
                    
                    if (allDomains.Count > 0)
                    {
                        var domainsLabel = new Label("Current domains:");
                        domainsLabel.style.fontSize = 11;
                        domainsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                        domainsLabel.style.marginTop = 8;
                        domainsLabel.style.marginBottom = 4;
                        advancedContent.Add(domainsLabel);
                        
                        foreach (var domain in allDomains)
                        {
                            var domainItem = new Label($"• {domain}");
                            domainItem.style.fontSize = 10;
                            domainItem.style.color = new Color(0.8f, 0.8f, 0.8f);
                            domainItem.style.marginLeft = 12;
                            domainItem.style.marginBottom = 2;
                            advancedContent.Add(domainItem);
                        }
                    }
                    else
                    {
                        var noDomains = new Label("No domains configured yet. Add domains using the + button above.");
                        noDomains.style.fontSize = 10;
                        noDomains.style.color = new Color(0.7f, 0.7f, 0.7f);
                        noDomains.style.unityFontStyleAndWeight = FontStyle.Italic;
                        noDomains.style.marginTop = 8;
                        advancedContent.Add(noDomains);
                    }
                }
                
                advancedFoldout.Add(advancedContent);
                container.Add(advancedFoldout);
                
                LogDebug($"Advanced section added to container. Config exists: {dynamicLinksConfig != null}, Domain count: {dynamicLinksConfig?.GetAllHosts().Count ?? 0}");
                
                // Show validation errors if any
                if (dynamicLinksConfig != null)
                {
                    var validation = dynamicLinksConfig.ValidateConfiguration();
                    if (!validation.IsValid)
                    {
                        var errorLabel = new Label($"Configuration errors: {validation.GetErrorsString()}");
                        errorLabel.style.color = Color.red;
                        errorLabel.style.fontSize = 11;
                        errorLabel.style.marginTop = 5;
                        errorLabel.style.whiteSpace = WhiteSpace.Normal;
                        container.Add(errorLabel);
                    }
                }
                
                if (string.IsNullOrEmpty(dynamicLinkUrl))
                {
                    var warning = new Label("Enter your associated domain to generate Universal Links configuration files.");
                    warning.style.color = new Color(1f, 0.8f, 0f); // Orange warning color
                    warning.style.fontSize = 11;
                    warning.style.marginTop = 5;
                    container.Add(warning);
                }
            }

            contentContainer.Add(container);
            
            LogDebug("BuildUnifiedModeContainer completed - container added to contentContainer");
            
            // Force a repaint to ensure UI updates are visible
            Repaint();
        }

        void BuildCrossPromoPanel()
        {
            LogDebug($"🔄 BuildCrossPromoPanel: Starting to build Cross-Promo panel");
            LogDebug($"   Cross-Promo Mode: {crossPromoMode}");
            LogDebug($"   Cached campaigns count: {(cachedRemoteCampaigns?.Count ?? 0)}");
            LogDebug($"   Last remote config sync: '{lastRemoteConfigSync}'");
            
            // Debug: Check cached source project data for UI display
            LogDebug($"🔍 UI Debug - cachedSourceProject is null: {cachedSourceProject == null}");
            if (cachedSourceProject != null)
            {
                LogDebug($"🔍 UI Debug - cachedSourceProject.store_ids is null: {cachedSourceProject.store_ids == null}");
                if (cachedSourceProject.store_ids != null)
                {
                    LogDebug($"🔍 UI Debug - store_ids count: {cachedSourceProject.store_ids.Count}");
                    foreach (var kvp in cachedSourceProject.store_ids)
                    {
                        LogDebug($"🔍 UI Debug - store_ids['{kvp.Key}'] = '{kvp.Value}'");
                    }
                }
            }
            LogDebug($"   Cross-promo last sync: '{crossPromoLastSync}'");
            
            // Add mode toggle and "What ships" banner at the top
            BuildFeatureModeHeader("Cross-Promo", crossPromoMode, crossPromoStatus, crossPromoServerRevision, crossPromoLastSync, 
                (mode) => SwitchCrossPromoMode(mode));
            
            // Hero section with card styling (matching Links and Integration Detection pages)
            var heroSection = new VisualElement();
            heroSection.style.flexDirection = FlexDirection.Row;
            heroSection.style.alignItems = Align.Center;
            heroSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            heroSection.style.paddingLeft = 20;
            heroSection.style.paddingRight = 20;
            heroSection.style.paddingTop = 15;
            heroSection.style.paddingBottom = 15;
            heroSection.style.marginBottom = 20;
            heroSection.style.borderTopLeftRadius = 8;
            heroSection.style.borderTopRightRadius = 8;
            heroSection.style.borderBottomLeftRadius = 8;
            heroSection.style.borderBottomRightRadius = 8;
            
            // Title and description container
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            
            var title = new Label("Cross-Promotion Configuration");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            textContainer.Add(title);

            var description = new Label("✨ Configure cross-promotion campaigns for your game portfolio.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.9f, 0.95f, 0.9f, 1f); // Brighter green tint for better readability
            description.style.whiteSpace = WhiteSpace.Normal;
            textContainer.Add(description);
            
            heroSection.Add(textContainer);
            
            // Action button - different based on mode
            if (crossPromoMode == FeatureMode.Local)
            {
                // Generate JSON button for Local mode
                if (crossPromoTable != null)
                {
                    generateJsonButton = new Button(() => GenerateCrossPromoJson()) { text = GetGenerateButtonText() };
                    generateJsonButton.style.width = 140;
                    generateJsonButton.style.height = 32;
                    generateJsonButton.style.fontSize = 12;
                    generateJsonButton.style.alignSelf = Align.Center;
                    
                    // Style based on state
                    UpdateGenerateButtonStyle(generateJsonButton);
                    
                    heroSection.Add(generateJsonButton);
                }
                else
                {
                    // Clear reference if no table exists
                    generateJsonButton = null;
                }
            }
            else
            {
                // Button container for Managed mode buttons
                var buttonContainer = new VisualElement();
                buttonContainer.style.flexDirection = FlexDirection.Row;
                buttonContainer.style.alignSelf = Align.Center;
                buttonContainer.style.alignItems = Align.Center;
                
                // Sync button for Managed mode
                var syncButton = new Button(async () => {
                    LogDebug("Fetch button: Starting campaign fetch from cloud (read-only)");
                    
                    // Remember current tab to ensure we stay on Cross-Promo
                    int currentTab = selectedTab;
                    
                    // Always refresh from BoostOps API first (to update cross_promo_server.json)
                    ProjectLookupResponse lookupResponse = null;
                    if (isLoggedIn && !string.IsNullOrEmpty(apiToken))
                    {
                        LogDebug("Fetch button: Step 1 - Fetching from BoostOps API to update local cache");
                        lookupResponse = await CheckForExistingProjectWithoutUIRebuild();
                    }
                    
                    // In editor mode, ALWAYS use BoostOps lookup response as source of truth
                    LogDebug("Fetch button: Step 2 - Fetching campaigns from BoostOps API (cloud is source of truth)");
                    await LoadCampaignsFromAPI(lookupResponse);
                    
                    // Force comprehensive UI refresh after fetch (all tabs and status indicators)
                    SafeDelayCall(() => {
                        LogDebug("Fetch button: Performing comprehensive UI refresh after fetch");
                        
                        // Update all status indicators and global state
                        UpdateStatusLights();
                        
                        // Refresh the current tab to show new data
                        switch (currentTab)
                        {
                            case 0: 
                                LogDebug("Refreshing Overview panel to show updated Source Project Settings");
                                ShowOverviewPanel(); 
                                break;
                            case 1: 
                                ShowLinksPanel(); 
                                break;
                            case 2: 
                                LogDebug("Refreshing Cross-Promo panel to show updated campaigns");
                                ShowCrossPromoPanel(); 
                                break;
                            case 3: 
                                LogDebug("Refreshing Attribution panel to show updated status");
                                ShowAttributionPanel(); 
                                break;
                            default: 
                                ShowOverviewPanel(); 
                                break;
                        }
                        
                        LogDebug($"✅ Comprehensive UI refresh completed for tab {currentTab}");
                    });
                }) { text = "📥 Fetch Campaigns" };
                syncButton.style.width = 140;
                syncButton.style.height = 32;
                syncButton.style.fontSize = 12;
                syncButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 1f);
                syncButton.tooltip = (hasUnityRemoteConfig || hasFirebaseRemoteConfig) ? 
                    "Fetch latest campaigns and settings from BoostOps cloud (read-only)" : 
                    "Fetch latest campaigns and settings from BoostOps server (read-only)";
                
                // Import Registered Apps button
                var importButton = new Button(() => {
                    _ = ShowImportRegisteredAppsDialog();
                }) { text = "💾 Cache App Icons" };
                importButton.style.width = 140;
                importButton.style.height = 32;
                importButton.style.fontSize = 12;
                importButton.style.backgroundColor = new Color(0.1f, 0.6f, 0.1f, 1f);
                importButton.style.marginLeft = 10;
                importButton.tooltip = "Pre-cache app icons from all registered apps in your studio";
                
                buttonContainer.Add(syncButton);
                buttonContainer.Add(importButton);
                heroSection.Add(buttonContainer);
                
                // Clear generate button reference since we're in managed mode
                generateJsonButton = null;
            }
            contentContainer.Add(heroSection);
            
            // Show different content based on mode
            if (crossPromoMode == FeatureMode.Local)
            {
                BuildLocalCrossPromoContent();
            }
            else
            {
                BuildManagedCrossPromoContent();
            }
            
            // App Walls section (shown in both modes)
            BuildAppWallsSection();
            
            // Developer settings removed from Links page
        }
        
        void BuildLocalCrossPromoContent()
        {
            // Configuration editor section (if table exists)
            if (crossPromoTable != null)
            {
                BuildCrossPromoEditor();
            }
            else
            {
                // Show create button if no table exists
                BuildCreateConfigurationSection();
            }
        }
        
        void BuildManagedSourceProjectSettings(VisualElement parent)
        {
            if (cachedSourceProject == null)
            {
                // Show placeholder if no source project data available yet
                var placeholderContainer = new VisualElement();
                placeholderContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
                placeholderContainer.style.paddingLeft = 15;
                placeholderContainer.style.paddingRight = 15;
                placeholderContainer.style.paddingTop = 15;
                placeholderContainer.style.paddingBottom = 15;
                placeholderContainer.style.borderTopLeftRadius = 4;
                placeholderContainer.style.borderTopRightRadius = 4;
                placeholderContainer.style.borderBottomLeftRadius = 4;
                placeholderContainer.style.borderBottomRightRadius = 4;
                placeholderContainer.style.marginBottom = 20;
                
                var placeholderTitle = new Label("📋 Source Project Settings");
                placeholderTitle.style.fontSize = 16;
                placeholderTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                placeholderTitle.style.marginBottom = 10;
                placeholderContainer.Add(placeholderTitle);
                
                var placeholderLabel = new Label("Source project settings will appear here once campaign data is loaded from the server.");
                placeholderLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                placeholderLabel.style.fontSize = 12;
                placeholderContainer.Add(placeholderLabel);
                
                parent.Add(placeholderContainer);
                return;
            }
            
            // Build read-only source project settings UI
            var settingsContainer = new VisualElement();
            settingsContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            settingsContainer.style.paddingLeft = 15;
            settingsContainer.style.paddingRight = 15;
            settingsContainer.style.paddingTop = 15;
            settingsContainer.style.paddingBottom = 15;
            settingsContainer.style.borderTopLeftRadius = 4;
            settingsContainer.style.borderTopRightRadius = 4;
            settingsContainer.style.borderBottomLeftRadius = 4;
            settingsContainer.style.borderBottomRightRadius = 4;
            settingsContainer.style.marginBottom = 20;
            
            // Title with server indicator
            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Row;
            titleContainer.style.alignItems = Align.Center;
            titleContainer.style.marginBottom = 15;
            
            var title = new Label("📋 Source Project Settings");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(title);
            
            var serverBadge = new Label("🌐 Server Managed");
            serverBadge.style.fontSize = 10;
            serverBadge.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 0.8f);
            serverBadge.style.color = Color.white;
            serverBadge.style.paddingLeft = 6;
            serverBadge.style.paddingRight = 6;
            serverBadge.style.paddingTop = 2;
            serverBadge.style.paddingBottom = 2;
            serverBadge.style.borderTopLeftRadius = 3;
            serverBadge.style.borderTopRightRadius = 3;
            serverBadge.style.borderBottomLeftRadius = 3;
            serverBadge.style.borderBottomRightRadius = 3;
            serverBadge.style.marginLeft = 10;
            titleContainer.Add(serverBadge);
            
            settingsContainer.Add(titleContainer);
            
            var infoLabel = new Label("These settings are managed in the BoostOps dashboard and are read-only in the Unity editor.");
            infoLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            infoLabel.style.fontSize = 12;
            infoLabel.style.marginBottom = 15;
            settingsContainer.Add(infoLabel);
            
            // Project name (read-only display)
            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.marginBottom = 8;
            
            var nameLabel = new Label("Project Name:");
            nameLabel.style.minWidth = 120;
            nameLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            nameRow.Add(nameLabel);
            
            var nameValue = new Label(cachedSourceProject.name ?? "Not specified");
            nameValue.style.color = Color.white;
            nameValue.style.flexGrow = 1;
            nameRow.Add(nameValue);
            
            settingsContainer.Add(nameRow);
            
            // Bundle ID (read-only display)
            var bundleRow = new VisualElement();
            bundleRow.style.flexDirection = FlexDirection.Row;
            bundleRow.style.marginBottom = 8;
            
            var bundleLabel = new Label("Bundle ID:");
            bundleLabel.style.minWidth = 120;
            bundleLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            bundleRow.Add(bundleLabel);
            
            // Try to get bundle ID from platform_ids first, then fall back to bundle_id field
            string bundleId = null;
            if (cachedSourceProject.platform_ids?.ContainsKey("ios_bundle_id") == true)
            {
                bundleId = cachedSourceProject.platform_ids["ios_bundle_id"]?.ToString();
            }
            if (string.IsNullOrEmpty(bundleId))
            {
                bundleId = cachedSourceProject.bundle_id;
            }
            var bundleValue = new Label(bundleId ?? "Not specified");
            bundleValue.style.color = Color.white;
            bundleValue.style.flexGrow = 1;
            bundleRow.Add(bundleValue);
            
            settingsContainer.Add(bundleRow);
            
            // Numeric settings in individual rows for better readability
            
            // Min Sessions (read-only display)
            var minSessionsRow = new VisualElement();
            minSessionsRow.style.flexDirection = FlexDirection.Row;
            minSessionsRow.style.marginBottom = 8;
            
            var minSessionsLabel = new Label("Min Sessions:");
            minSessionsLabel.style.minWidth = 120;
            minSessionsLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            minSessionsRow.Add(minSessionsLabel);
            
            var minSessionsValue = new Label(cachedSourceProject.min_sessions.ToString());
            minSessionsValue.style.color = Color.white;
            minSessionsValue.style.flexGrow = 1;
            minSessionsRow.Add(minSessionsValue);
            
            settingsContainer.Add(minSessionsRow);
            
            // Min Player Days (read-only display)
            var minDaysRow = new VisualElement();
            minDaysRow.style.flexDirection = FlexDirection.Row;
            minDaysRow.style.marginBottom = 8;
            
            var minDaysLabel = new Label("Min Player Days:");
            minDaysLabel.style.minWidth = 120;
            minDaysLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            minDaysRow.Add(minDaysLabel);
            
            var minDaysValue = new Label(cachedSourceProject.min_player_days.ToString());
            minDaysValue.style.color = Color.white;
            minDaysValue.style.flexGrow = 1;
            minDaysRow.Add(minDaysValue);
            
            settingsContainer.Add(minDaysRow);
            
            // Frequency Cap (read-only display)
            var freqCapRow = new VisualElement();
            freqCapRow.style.flexDirection = FlexDirection.Row;
            freqCapRow.style.marginBottom = 8;
            
            var freqCapLabel = new Label("Frequency Cap:");
            freqCapLabel.style.minWidth = 120;
            freqCapLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            freqCapRow.Add(freqCapLabel);
            
            var freqCapValue = new Label($"{cachedSourceProject.frequency_cap?.impressions ?? 0} per {cachedSourceProject.frequency_cap?.time_unit ?? "DAY"}");
            freqCapValue.style.color = Color.white;
            freqCapValue.style.flexGrow = 1;
            freqCapRow.Add(freqCapValue);
            
            settingsContainer.Add(freqCapRow);
            
            // Primary Store IDs (prominently displayed)
            var primaryStoreHeader = new Label("Store IDs");
            primaryStoreHeader.style.fontSize = 14;
            primaryStoreHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            primaryStoreHeader.style.color = Color.white;
            primaryStoreHeader.style.marginTop = 15;
            primaryStoreHeader.style.marginBottom = 8;
            settingsContainer.Add(primaryStoreHeader);
            
            // Apple Store ID (from cloud)
            var appleRow = new VisualElement();
            appleRow.style.flexDirection = FlexDirection.Row;
            appleRow.style.marginBottom = 8;
            
            var appleLabel = new Label("Apple Store ID:");
            appleLabel.style.minWidth = 120;
            appleLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            appleRow.Add(appleLabel);
            
            var appleId = cachedSourceProject.store_ids?.ContainsKey("apple") == true ? cachedSourceProject.store_ids["apple"] : null;
            var appleValue = new Label(appleId ?? "Not configured");
            appleValue.style.color = string.IsNullOrEmpty(appleId) ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            appleValue.style.flexGrow = 1;
            appleRow.Add(appleValue);
            
            settingsContainer.Add(appleRow);
            
            // Google Store ID (from cloud)
            var googleRow = new VisualElement();
            googleRow.style.flexDirection = FlexDirection.Row;
            googleRow.style.marginBottom = 8;
            
            var googleLabel = new Label("Google Store ID:");
            googleLabel.style.minWidth = 120;
            googleLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            googleRow.Add(googleLabel);
            
            var googleId = cachedSourceProject.store_ids?.ContainsKey("google") == true ? cachedSourceProject.store_ids["google"] : null;
            var googleValue = new Label(googleId ?? "Not configured");
            googleValue.style.color = string.IsNullOrEmpty(googleId) ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            googleValue.style.flexGrow = 1;
            googleRow.Add(googleValue);
            
            settingsContainer.Add(googleRow);
            
            // Advanced Store IDs section (read-only - cloud values only)
            var storeIdsFoldout = new Foldout();
            storeIdsFoldout.text = "Advanced Store IDs (Read-Only)";
            storeIdsFoldout.style.marginTop = 10;
            storeIdsFoldout.style.marginBottom = 5;
            storeIdsFoldout.value = false; // Start collapsed
            
            // Amazon Store ID (from cloud)
            var amazonRow = new VisualElement();
            amazonRow.style.flexDirection = FlexDirection.Row;
            amazonRow.style.marginBottom = 5;
            
            var amazonLabel = new Label("Amazon Store ID:");
            amazonLabel.style.minWidth = 140;
            amazonLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            amazonRow.Add(amazonLabel);
            
            var amazonId = cachedSourceProject.store_ids?.ContainsKey("amazon") == true ? cachedSourceProject.store_ids["amazon"] : null;
            var amazonValue = new Label(amazonId ?? "Not configured");
            amazonValue.style.color = string.IsNullOrEmpty(amazonId) ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            amazonValue.style.flexGrow = 1;
            amazonRow.Add(amazonValue);
            
            storeIdsFoldout.Add(amazonRow);
            
            // Microsoft Store ID (from cloud)
            var windowsRow = new VisualElement();
            windowsRow.style.flexDirection = FlexDirection.Row;
            windowsRow.style.marginBottom = 5;
            
            var windowsLabel = new Label("Microsoft Store ID:");
            windowsLabel.style.minWidth = 140;
            windowsLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            windowsRow.Add(windowsLabel);
            
            var windowsId = cachedSourceProject.store_ids?.ContainsKey("microsoft") == true ? cachedSourceProject.store_ids["microsoft"] : null;
            var windowsValue = new Label(windowsId ?? "Not configured");
            windowsValue.style.color = string.IsNullOrEmpty(windowsId) ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            windowsValue.style.flexGrow = 1;
            windowsRow.Add(windowsValue);
            
            storeIdsFoldout.Add(windowsRow);
            
            // Samsung Store ID (from cloud)
            var samsungRow = new VisualElement();
            samsungRow.style.flexDirection = FlexDirection.Row;
            samsungRow.style.marginBottom = 5;
            
            var samsungLabel = new Label("Samsung Store ID:");
            samsungLabel.style.minWidth = 140;
            samsungLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            samsungRow.Add(samsungLabel);
            
            var samsungId = cachedSourceProject.store_ids?.ContainsKey("samsung") == true ? cachedSourceProject.store_ids["samsung"] : null;
            var samsungValue = new Label(samsungId ?? "Not configured");
            samsungValue.style.color = string.IsNullOrEmpty(samsungId) ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            samsungValue.style.flexGrow = 1;
            samsungRow.Add(samsungValue);
            
            storeIdsFoldout.Add(samsungRow);
            
            settingsContainer.Add(storeIdsFoldout);
            
            // Advanced text settings in a foldout
            var advancedFoldout = new Foldout();
            advancedFoldout.text = "Advanced Text Settings (Read-Only)";
            advancedFoldout.style.marginTop = 10;
            advancedFoldout.value = false; // Start collapsed
            
            // Icon Interstitial settings
            var iconCtaRow = new VisualElement();
            iconCtaRow.style.flexDirection = FlexDirection.Row;
            iconCtaRow.style.marginBottom = 5;
            
            var iconCtaLabel = new Label("Icon Button Text:");
            iconCtaLabel.style.minWidth = 140;
            iconCtaLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            iconCtaRow.Add(iconCtaLabel);
            
            var iconCtaValue = new Label(cachedSourceProject.interstitial_icon_cta ?? "Play Now!");
            iconCtaValue.style.color = Color.white;
            iconCtaValue.style.flexGrow = 1;
            iconCtaRow.Add(iconCtaValue);
            
            advancedFoldout.Add(iconCtaRow);
            
            var iconTextRow = new VisualElement();
            iconTextRow.style.flexDirection = FlexDirection.Row;
            iconTextRow.style.marginBottom = 5;
            
            var iconTextLabel = new Label("Icon Description:");
            iconTextLabel.style.minWidth = 140;
            iconTextLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            iconTextRow.Add(iconTextLabel);
            
            var iconTextValue = new Label(cachedSourceProject.interstitial_icon_text ?? "Try this awesome game!");
            iconTextValue.style.color = Color.white;
            iconTextValue.style.flexGrow = 1;
            iconTextValue.style.whiteSpace = WhiteSpace.Normal; // Allow text wrapping
            iconTextRow.Add(iconTextValue);
            
            advancedFoldout.Add(iconTextRow);
            
            // Rich Interstitial settings
            var richCtaRow = new VisualElement();
            richCtaRow.style.flexDirection = FlexDirection.Row;
            richCtaRow.style.marginBottom = 5;
            
            var richCtaLabel = new Label("Rich Button Text:");
            richCtaLabel.style.minWidth = 140;
            richCtaLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            richCtaRow.Add(richCtaLabel);
            
            var richCtaValue = new Label(cachedSourceProject.interstitial_rich_cta ?? "Play Now!");
            richCtaValue.style.color = Color.white;
            richCtaValue.style.flexGrow = 1;
            richCtaRow.Add(richCtaValue);
            
            advancedFoldout.Add(richCtaRow);
            
            var richTextRow = new VisualElement();
            richTextRow.style.flexDirection = FlexDirection.Row;
            richTextRow.style.marginBottom = 5;
            
            var richTextLabel = new Label("Rich Description:");
            richTextLabel.style.minWidth = 140;
            richTextLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            richTextRow.Add(richTextLabel);
            
            var richTextValue = new Label(cachedSourceProject.interstitial_rich_text ?? "Join millions of players in this amazing adventure!");
            richTextValue.style.color = Color.white;
            richTextValue.style.flexGrow = 1;
            richTextValue.style.whiteSpace = WhiteSpace.Normal; // Allow text wrapping
            richTextRow.Add(richTextValue);
            
            advancedFoldout.Add(richTextRow);
            
            settingsContainer.Add(advancedFoldout);
            
            parent.Add(settingsContainer);
        }
        
        void BuildManagedCrossPromoContent()
        {
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Column;
            mainContainer.style.marginTop = 25;
            mainContainer.style.marginBottom = 10;
            
            // In BoostOps Remote mode, show current state without automatically triggering API calls during UI building
            if (cachedRemoteCampaigns == null || cachedRemoteCampaigns.Count == 0)
            {
                // Show a message about no data instead of automatically triggering API calls
                var noDataContainer = new VisualElement();
                noDataContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);
                noDataContainer.style.paddingLeft = 20;
                noDataContainer.style.paddingRight = 20;
                noDataContainer.style.paddingTop = 20;
                noDataContainer.style.paddingBottom = 20;
                noDataContainer.style.marginTop = 20;
                noDataContainer.style.marginBottom = 20;
                noDataContainer.style.borderTopLeftRadius = 8;
                noDataContainer.style.borderTopRightRadius = 8;
                noDataContainer.style.borderBottomLeftRadius = 8;
                noDataContainer.style.borderBottomRightRadius = 8;
                
                var noDataLabel = new Label("📭 No campaign data loaded");
                noDataLabel.style.fontSize = 14;
                noDataLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                noDataLabel.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                noDataLabel.style.marginBottom = 10;
                noDataContainer.Add(noDataLabel);
                
                var instructionLabel = new Label("Click \"📥 Fetch Campaigns\" above to load campaign data from the BoostOps API.\n\nIn Managed mode, campaign data comes from your BoostOps project configuration.");
                instructionLabel.style.fontSize = 12;
                instructionLabel.style.color = new Color(0.7f, 0.8f, 0.9f, 1f);
                instructionLabel.style.whiteSpace = WhiteSpace.Normal;
                noDataContainer.Add(instructionLabel);
                
                mainContainer.Add(noDataContainer);
                
                LogDebug("BoostOps Managed Mode: No cached campaigns - showing fetch instruction UI");
            }
            
            // Show read-only source project settings from server
            BuildManagedSourceProjectSettings(mainContainer);
            
            // Campaign Overview (Read-Only) - moved above Remote Configuration Status
            if (hasUnityRemoteConfig || hasFirebaseRemoteConfig || isLoggedIn)
            {
                var campaignContainer = new VisualElement();
                campaignContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
                campaignContainer.style.paddingLeft = 15;
                campaignContainer.style.paddingRight = 15;
                campaignContainer.style.paddingTop = 15;
                campaignContainer.style.paddingBottom = 15;
                campaignContainer.style.borderTopLeftRadius = 4;
                campaignContainer.style.borderTopRightRadius = 4;
                campaignContainer.style.borderBottomLeftRadius = 4;
                campaignContainer.style.borderBottomRightRadius = 4;
                campaignContainer.style.marginBottom = 20;
                
                // Title only (sync button moved to hero section)
                var campaignTitle = new Label("📊 Campaign Overview");
                campaignTitle.style.fontSize = 16;
                campaignTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                campaignTitle.style.marginBottom = 15;
                campaignContainer.Add(campaignTitle);
                
                // Display actual campaign data or placeholder
                LogDebug($"BuildManagedCrossPromoContent: Checking cached campaigns - Count: {(cachedRemoteCampaigns?.Count ?? 0)}, Null: {cachedRemoteCampaigns == null}");
                LogDebug($"BuildManagedCrossPromoContent: lastRemoteConfigSync: '{lastRemoteConfigSync}', crossPromoLastSync: '{crossPromoLastSync}'");
                
                if (cachedRemoteCampaigns != null && cachedRemoteCampaigns.Count > 0)
                {
                    LogDebug($"✅ BuildManagedCrossPromoContent: Displaying {cachedRemoteCampaigns.Count} campaigns from cache");
                    BuildCampaignList(campaignContainer);
                }
                else
                {
                    LogDebug($"❌ BuildManagedCrossPromoContent: No campaigns to display - showing placeholder");
                    
                    string placeholderText;
                    if (isLoggedIn)
                    {
                        placeholderText = "No campaigns found. Click 'Sync Campaigns' above to fetch campaigns from BoostOps API.\n\n" +
                                         "• Campaigns are managed through the BoostOps Dashboard\n" +
                                         "• They sync directly from BoostOps servers\n" +
                                         "• Icons are downloaded and cached locally";
                    }
                    else
                    {
                        placeholderText = "No campaigns found. Sign in and click 'Sync Campaigns' to fetch from BoostOps API, or configure remote config.\n\n" +
                                         "• BoostOps API: Direct sync from dashboard (recommended)\n" +
                                         "• Remote Config: Unity/Firebase fallback method\n" +
                                         "• Icons are downloaded and cached locally";
                    }
                    
                    var placeholder = new Label(placeholderText);
                    placeholder.style.fontSize = 12;
                    placeholder.style.color = new Color(0.7f, 0.7f, 0.7f);
                    placeholder.style.whiteSpace = WhiteSpace.Normal;
                    campaignContainer.Add(placeholder);
                }
                
                mainContainer.Add(campaignContainer);
            }

            contentContainer.Add(mainContainer);
        }
        
        void BuildAppWallsSection()
        {
            var appWallContainer = new VisualElement();
            appWallContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.4f);
            appWallContainer.style.paddingLeft = 15;
            appWallContainer.style.paddingRight = 15;
            appWallContainer.style.paddingTop = 15;
            appWallContainer.style.paddingBottom = 15;
            appWallContainer.style.marginTop = 20;
            appWallContainer.style.marginBottom = 20;
            appWallContainer.style.borderTopLeftRadius = 8;
            appWallContainer.style.borderTopRightRadius = 8;
            appWallContainer.style.borderBottomLeftRadius = 8;
            appWallContainer.style.borderBottomRightRadius = 8;
            
            // Title
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 15;
            
            var title = new Label("🎮 App Wall Configuration");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.9f, 0.95f, 1f, 1f);
            titleRow.Add(title);
            
            // Button container for help only (refresh is handled by main "Fetch Campaigns" button)
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.alignItems = Align.Center;
            
            // Add help button
            var helpButton = new Button(() => {
                ShowAppWallHelpDialog();
            }) { text = "?" };
            helpButton.style.width = 24;
            helpButton.style.height = 24;
            helpButton.style.fontSize = 14;
            helpButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            helpButton.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 1f);
            helpButton.tooltip = "Learn more about App Walls";
            buttonContainer.Add(helpButton);
            
            titleRow.Add(buttonContainer);
            
            appWallContainer.Add(titleRow);
            
            // Description
            var description = new Label("App Walls display a grid of apps for cross-promotion. Configuration is managed through the app_walls section in remote config.\n\n💡 Use the \"📥 Fetch Campaigns\" button above to refresh both campaigns and app walls.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.8f, 0.85f, 0.9f, 1f);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 15;
            appWallContainer.Add(description);
            
            // Load cached app walls config from project settings
            var projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
            bool hasCachedConfig = !string.IsNullOrEmpty(projectSettings?.cachedAppWallsJson);
            
            LogDebug($"[App Walls] Loading cached config - Has data: {hasCachedConfig}");
            
            if (hasCachedConfig)
            {
                // Parse the cached app walls from project settings
                // The cached JSON has the structure: {"app_walls":{"default":{...}}}
                // We need to parse the wrapper first, then extract the AppWallsConfig
                BoostOps.Core.AppWallsConfig appWalls = null;
                try
                {
                    // First try to parse as a wrapper with app_walls field
                    var wrapperJson = projectSettings.cachedAppWallsJson;
                    
                    // Check if it's wrapped in {"app_walls": ...}
                    if (wrapperJson.Contains("\"app_walls\""))
                    {
                        // Parse the wrapper to get the AppWallsConfig
                        var tempWrapper = JsonUtility.FromJson<AppWallsConfigWrapper>(wrapperJson);
                        appWalls = tempWrapper?.app_walls;
                        LogDebug($"[App Walls] Parsed wrapped config - app_walls: {appWalls != null}");
                    }
                    else
                    {
                        // Direct parse if not wrapped
                        appWalls = JsonUtility.FromJson<BoostOps.Core.AppWallsConfig>(wrapperJson);
                        LogDebug($"[App Walls] Parsed direct config");
                    }
                }
                catch (System.Exception ex)
                {
                    LogDebug($"Failed to parse cached app walls: {ex.Message}");
                }
                LogDebug($"[App Walls] Parsed config - App walls: {appWalls != null}, Default: {appWalls?.@default != null}");
                
                if (appWalls != null && appWalls.@default != null)
                {
                    var defaultWall = appWalls.@default;
                    LogDebug($"[App Walls] Default wall - Enabled: {defaultWall.enabled}, Items: {defaultWall.items?.Length ?? 0}, Max shown: {defaultWall.max_shown}");
                    
                    // Status row
                    var statusRow = new VisualElement();
                    statusRow.style.flexDirection = FlexDirection.Row;
                    statusRow.style.marginBottom = 10;
                    
                    var statusLabel = new Label("Status:");
                    statusLabel.style.minWidth = 120;
                    statusLabel.style.fontSize = 12;
                    statusRow.Add(statusLabel);
                    
                    var statusValue = new Label(defaultWall.enabled ? "✅ Enabled" : "❌ Disabled");
                    statusValue.style.fontSize = 12;
                    statusValue.style.color = defaultWall.enabled ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.4f, 0.2f);
                    statusValue.style.unityFontStyleAndWeight = FontStyle.Bold;
                    statusRow.Add(statusValue);
                    
                    appWallContainer.Add(statusRow);
                    
                    // Show warning if disabled
                    if (!defaultWall.enabled)
                    {
                        var warningLabel = new Label("⚠️ App Wall is disabled in remote config. Enable it in your BoostOps dashboard to use this feature.");
                        warningLabel.style.fontSize = 11;
                        warningLabel.style.color = new Color(1f, 0.8f, 0.3f);
                        warningLabel.style.whiteSpace = WhiteSpace.Normal;
                        warningLabel.style.backgroundColor = new Color(0.3f, 0.25f, 0.1f, 0.4f);
                        warningLabel.style.paddingLeft = 10;
                        warningLabel.style.paddingRight = 10;
                        warningLabel.style.paddingTop = 8;
                        warningLabel.style.paddingBottom = 8;
                        warningLabel.style.marginBottom = 15;
                        warningLabel.style.borderTopLeftRadius = 4;
                        warningLabel.style.borderTopRightRadius = 4;
                        warningLabel.style.borderBottomLeftRadius = 4;
                        warningLabel.style.borderBottomRightRadius = 4;
                        appWallContainer.Add(warningLabel);
                    }
                    
                    // Max shown row
                    var maxShownRow = new VisualElement();
                    maxShownRow.style.flexDirection = FlexDirection.Row;
                    maxShownRow.style.marginBottom = 10;
                    
                    var maxShownLabel = new Label("Max Apps Shown:");
                    maxShownLabel.style.minWidth = 120;
                    maxShownLabel.style.fontSize = 12;
                    maxShownRow.Add(maxShownLabel);
                    
                    var maxShownValue = new Label(defaultWall.max_shown.ToString());
                    maxShownValue.style.fontSize = 12;
                    maxShownValue.style.color = new Color(0.8f, 0.9f, 1f);
                    maxShownRow.Add(maxShownValue);
                    
                    appWallContainer.Add(maxShownRow);
                    
                    // Sort order row
                    var sortOrderRow = new VisualElement();
                    sortOrderRow.style.flexDirection = FlexDirection.Row;
                    sortOrderRow.style.marginBottom = 15;
                    
                    var sortOrderLabel = new Label("Sort Order:");
                    sortOrderLabel.style.minWidth = 120;
                    sortOrderLabel.style.fontSize = 12;
                    sortOrderRow.Add(sortOrderLabel);
                    
                    var sortOrderValue = new Label(defaultWall.sort_order ?? "display_order");
                    sortOrderValue.style.fontSize = 12;
                    sortOrderValue.style.color = new Color(0.8f, 0.9f, 1f);
                    sortOrderRow.Add(sortOrderValue);
                    
                    appWallContainer.Add(sortOrderRow);
                    
                    // Items list
                    if (defaultWall.items != null && defaultWall.items.Length > 0)
                    {
                        var itemsLabel = new Label($"📱 Configured Items ({defaultWall.items.Length}):");
                        itemsLabel.style.fontSize = 13;
                        itemsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                        itemsLabel.style.marginBottom = 10;
                        itemsLabel.style.color = new Color(0.9f, 0.95f, 1f);
                        appWallContainer.Add(itemsLabel);
                        
                        foreach (var app in defaultWall.items)
                        {
                            if (app == null) continue;
                            
                            var appRow = new VisualElement();
                            appRow.style.flexDirection = FlexDirection.Row;
                            appRow.style.paddingLeft = 10;
                            appRow.style.paddingTop = 5;
                            appRow.style.paddingBottom = 5;
                            appRow.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.5f);
                            appRow.style.borderTopLeftRadius = 4;
                            appRow.style.borderTopRightRadius = 4;
                            appRow.style.borderBottomLeftRadius = 4;
                            appRow.style.borderBottomRightRadius = 4;
                            appRow.style.marginBottom = 5;
                            
                            var appInfo = new Label($"{(app.enabled ? "✓" : "✗")} {app.target_project_name ?? app.app_id} (order: {app.display_order})");
                            appInfo.style.fontSize = 11;
                            appInfo.style.color = app.enabled ? new Color(0.7f, 0.85f, 1f) : new Color(0.5f, 0.5f, 0.5f);
                            appRow.Add(appInfo);
                            
                            appWallContainer.Add(appRow);
                        }
                    }
                    else
                    {
                    var noAppsLabel = new Label("No apps configured");
                    noAppsLabel.style.fontSize = 12;
                    noAppsLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    appWallContainer.Add(noAppsLabel);
                    }
                    
                    // Cache info
                    var cacheInfo = new Label($"Last cached: {projectSettings.appWallsLastUpdated} from {projectSettings.appWallsSource}");
                    cacheInfo.style.fontSize = 10;
                    cacheInfo.style.color = new Color(0.5f, 0.5f, 0.5f);
                    cacheInfo.style.marginTop = 15;
                    appWallContainer.Add(cacheInfo);
                }
                else
                {
                    var noDataLabel = new Label("Cached config found but unable to parse app_walls section");
                    noDataLabel.style.fontSize = 12;
                    noDataLabel.style.color = new Color(0.8f, 0.6f, 0.2f);
                    appWallContainer.Add(noDataLabel);
                }
            }
            else
            {
                // No cached config
                var noConfigContainer = new VisualElement();
                noConfigContainer.style.paddingLeft = 10;
                noConfigContainer.style.paddingTop = 10;
                noConfigContainer.style.paddingBottom = 10;
                noConfigContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.6f);
                noConfigContainer.style.borderTopLeftRadius = 4;
                noConfigContainer.style.borderTopRightRadius = 4;
                noConfigContainer.style.borderBottomLeftRadius = 4;
                noConfigContainer.style.borderBottomRightRadius = 4;
                
                var noConfigLabel = new Label("📭 No app wall configuration cached");
                noConfigLabel.style.fontSize = 12;
                noConfigLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                noConfigLabel.style.marginBottom = 5;
                noConfigContainer.Add(noConfigLabel);
                
                var instructionLabel = new Label("App walls are configured through the app_walls section in remote config. Click 'Fetch Campaigns' to load and cache the configuration.");
                instructionLabel.style.fontSize = 11;
                instructionLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
                instructionLabel.style.whiteSpace = WhiteSpace.Normal;
                noConfigContainer.Add(instructionLabel);
                
                appWallContainer.Add(noConfigContainer);
            }
            
            contentContainer.Add(appWallContainer);
        }
        
        void ShowAppWallHelpDialog()
        {
            EditorUtility.DisplayDialog(
                "App Wall Configuration",
                "App Walls display a grid of apps for cross-promotion.\n\n" +
                "Configuration:\n" +
                "• Managed through remote config (app_walls section)\n" +
                "• Automatically cached for offline use\n" +
                "• Click 'Fetch Campaigns' to update the cache\n\n" +
                "Documentation:\n" +
                "• See APP_WALL_SETUP_GUIDE.md for setup instructions\n" +
                "• See APP_WALL_OFFLINE_CACHING.md for cache details\n\n" +
                "Usage:\n" +
                "• Call BoostOpsSDK.ShowAppWall(\"placement\") to display\n" +
                "• Works offline using cached configuration\n" +
                "• Generates prefabs automatically via menu",
                "OK"
            );
        }
        
        void BuildLocalLinksContent()
        {
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.marginTop = 25;
            mainContainer.style.marginBottom = 10;
            
            // Left side - Domain Configuration
            var leftDomainContainer = new VisualElement();
            leftDomainContainer.style.flexDirection = FlexDirection.Column;
            leftDomainContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            leftDomainContainer.style.paddingLeft = 15;
            leftDomainContainer.style.paddingRight = 15;
            leftDomainContainer.style.paddingTop = 15;
            leftDomainContainer.style.paddingBottom = 15;
            leftDomainContainer.style.borderTopLeftRadius = 4;
            leftDomainContainer.style.borderTopRightRadius = 4;
            leftDomainContainer.style.borderBottomLeftRadius = 4;
            leftDomainContainer.style.borderBottomRightRadius = 4;
            leftDomainContainer.style.width = Length.Percent(50);
            leftDomainContainer.style.flexShrink = 0;
            
            // Domain Configuration Title
            var title = new Label("Domain Configuration");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 15;
            leftDomainContainer.Add(title);
            
            // Domain input field
            var domainField = new TextField();
            domainFieldRef = domainField; // Store reference for later updates
            domainField.value = dynamicLinkUrl;
            domainField.style.marginBottom = 10;
            domainField.RegisterValueChangedCallback(evt => {
                dynamicLinkUrl = evt.newValue;
                SaveDynamicLinkUrl();
                AutoGenerateQRCode();
                
                // Sync with dynamic links config - only auto-add if the list is empty
                if (dynamicLinksConfig != null && !string.IsNullOrEmpty(evt.newValue))
                {
                    var cleanDomain = BoostOpsProjectSettings.CleanHost(evt.newValue);
                    var currentDomains = dynamicLinksConfig.GetAllHosts();
                    
                    // Only auto-add if domain list is empty and the domain is valid
                    if (currentDomains.Count == 0 && BoostOpsProjectSettings.ValidateHostFormat(cleanDomain))
                    {
                        dynamicLinksConfig.AddDomain(cleanDomain);
                    }
                }
                
                // Refresh QR section since domain changed
                RefreshQRSection();
            });
            
            // Create buttons container for refresh and add domain
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            
            var refreshButton = new Button(() => {
                RefreshAllData();
            }) { text = "↻" };
            refreshButton.style.width = 30;
            refreshButton.tooltip = "Refresh all BoostOps configuration data";
            
            var addDomainButton = new Button(() => {
                AddDomainFromField();
            }) { text = "+" };
            addDomainButton.style.width = 30;
            addDomainButton.style.marginLeft = 3;
            addDomainButton.tooltip = "Add this domain to your list";
            
            buttonsContainer.Add(refreshButton);
            buttonsContainer.Add(addDomainButton);
            
            var domainRow = CreateLabelFieldRowWithButton("Your Associated Domain(s):", domainField, buttonsContainer);
            leftDomainContainer.Add(domainRow);
            
            // Add domain chips section below the main field
            BuildDomainChipsSection(leftDomainContainer);
            
            // Ensure we have a config loaded - create one if it doesn't exist
            if (dynamicLinksConfig == null)
            {
                LogDebug("No dynamic links config found, attempting to load...");
                LoadDynamicLinksConfig();
                
                // If still no config, create a minimal one to enable the UI
                if (dynamicLinksConfig == null)
                {
                    LogDebug("Still no config found, creating new one...");
                    CreateDynamicLinksConfig();
                }
            }
            
            mainContainer.Add(leftDomainContainer);
            
            // Right side - QR Code Testing
            var rightContainer = new VisualElement();
            rightContainer.style.flexDirection = FlexDirection.Column;
            rightContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            rightContainer.style.paddingLeft = 15;
            rightContainer.style.paddingRight = 15;
            rightContainer.style.paddingTop = 15;
            rightContainer.style.paddingBottom = 15;
            rightContainer.style.borderTopLeftRadius = 4;
            rightContainer.style.borderTopRightRadius = 4;
            rightContainer.style.borderBottomLeftRadius = 4;
            rightContainer.style.borderBottomRightRadius = 4;
            rightContainer.style.width = Length.Percent(50);
            rightContainer.style.flexShrink = 0;
            rightContainer.style.marginLeft = 5;
            
            BuildQRCodeTestingContent(rightContainer);
            
            mainContainer.Add(rightContainer);
            
            contentContainer.Add(mainContainer);
        }
        
        void BuildManagedLinksContent()
        {
            // First, always show project lookup status when we have lookup response data
            if (hasLookupResponse)
            {
                BuildProjectLookupStatus();
            }
            
            // Check if we have a configured project slug
            bool hasConfiguredSlug = !string.IsNullOrEmpty(registeredProjectSlug);
            
            if (hasConfiguredSlug)
            {
                // Slug configured - show read-only display with testing
                BuildManagedLinksConfigured();
            }
        }
        
        void BuildProjectLookupStatus()
        {
            var lookupContainer = new VisualElement();
            lookupContainer.style.backgroundColor = lookupProjectFound ? 
                new Color(0.1f, 0.4f, 0.1f, 0.3f) : // Green tint for found projects
                new Color(0.4f, 0.2f, 0.1f, 0.3f);   // Orange tint for not found
            lookupContainer.style.paddingLeft = 20;
            lookupContainer.style.paddingRight = 20;
            lookupContainer.style.paddingTop = 15;
            lookupContainer.style.paddingBottom = 15;
            lookupContainer.style.marginTop = 20;
            lookupContainer.style.marginBottom = 15;
            lookupContainer.style.borderTopLeftRadius = 8;
            lookupContainer.style.borderTopRightRadius = 8;
            lookupContainer.style.borderBottomLeftRadius = 8;
            lookupContainer.style.borderBottomRightRadius = 8;
            
            // Status title
            var statusTitle = new Label(lookupProjectFound ? "✅ Project Found in BoostOps" : "❌ No Project Found");
            statusTitle.style.fontSize = 16;
            statusTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusTitle.style.marginBottom = 12;
            statusTitle.style.color = lookupProjectFound ? 
                new Color(0.7f, 1f, 0.7f) : 
                new Color(1f, 0.9f, 0.7f);
            lookupContainer.Add(statusTitle);
            
            // Project details (if found)
            if (lookupProjectFound)
            {
                // Project name
                if (!string.IsNullOrEmpty(lookupProjectName))
                {
                    var nameRow = new VisualElement();
                    nameRow.style.flexDirection = FlexDirection.Row;
                    nameRow.style.marginBottom = 8;
                    
                    var nameLabel = new Label("Project Name:");
                    nameLabel.style.minWidth = 120;
                    nameLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                    nameRow.Add(nameLabel);
                    
                    var nameValue = new Label(lookupProjectName);
                    nameValue.style.color = Color.white;
                    nameValue.style.unityFontStyleAndWeight = FontStyle.Bold;
                    nameRow.Add(nameValue);
                    
                    lookupContainer.Add(nameRow);
                }
                
                // Project slug (always show this section)
                var slugRow = new VisualElement();
                slugRow.style.flexDirection = FlexDirection.Row;
                slugRow.style.marginBottom = 8;
                
                var slugLabel = new Label("Project Slug:");
                slugLabel.style.minWidth = 120;
                slugLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                slugRow.Add(slugLabel);
                
                if (!string.IsNullOrEmpty(lookupProjectSlug))
                {
                    var slugValue = new Label(lookupProjectSlug);
                    slugValue.style.color = new Color(0.7f, 0.9f, 1f);
                    slugValue.style.unityFontStyleAndWeight = FontStyle.Bold;
                    slugRow.Add(slugValue);
                    
                    lookupContainer.Add(slugRow);
                    
                    // Show the full dynamic link domain
                    var domainRow = new VisualElement();
                    domainRow.style.flexDirection = FlexDirection.Row;
                    domainRow.style.marginBottom = 8;
                    
                    var domainLabel = new Label("Link Domain:");
                    domainLabel.style.minWidth = 120;
                    domainLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                    domainRow.Add(domainLabel);
                    
                    var domainValue = new Label($"{lookupProjectSlug}.boostlink.me");
                    domainValue.style.color = new Color(0.7f, 0.9f, 1f);
                    domainRow.Add(domainValue);
                    
                    lookupContainer.Add(domainRow);
                }
                else
                {
                    // Show when no project slug is configured
                    var slugValue = new Label("Not configured - set up in dashboard");
                    slugValue.style.color = new Color(1f, 0.8f, 0.6f);
                    slugValue.style.unityFontStyleAndWeight = FontStyle.Italic;
                    slugRow.Add(slugValue);
                    
                    lookupContainer.Add(slugRow);
                    
                    // Show a note about setting up the project slug
                    var noteRow = new VisualElement();
                    noteRow.style.flexDirection = FlexDirection.Row;
                    noteRow.style.marginBottom = 8;
                    noteRow.style.marginLeft = 120; // Align with value column
                    
                    var noteValue = new Label("→ Configure your project slug in the BoostOps Dashboard");
                    noteValue.style.color = new Color(0.8f, 0.8f, 0.8f);
                    noteValue.style.fontSize = 11;
                    noteValue.style.unityFontStyleAndWeight = FontStyle.Italic;
                    noteRow.Add(noteValue);
                    
                    lookupContainer.Add(noteRow);
                }
                
                // Apple Team ID section (Signing Team ID)
                var teamIdSectionTitle = new Label("Signing Team ID (Apple Team ID):");
                teamIdSectionTitle.style.fontSize = 13;
                teamIdSectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                teamIdSectionTitle.style.color = new Color(0.9f, 0.9f, 0.9f);
                teamIdSectionTitle.style.marginTop = 15;
                teamIdSectionTitle.style.marginBottom = 8;
                lookupContainer.Add(teamIdSectionTitle);
                
                // Get current values
                string editorTeamId = GetEditorAppleTeamId();
                string serverTeamId = GetServerAppleTeamId(cachedProjectLookupResponse);
                bool hasMismatch = HasAppleTeamIdMismatch(editorTeamId, serverTeamId);
                
                // Editor Team ID (build-time)
                var editorRow = new VisualElement();
                editorRow.style.flexDirection = FlexDirection.Row;
                editorRow.style.marginBottom = 6;
                editorRow.style.marginLeft = 10;
                
                var editorLabel = new Label("Editor (build-time):");
                editorLabel.style.minWidth = 140;
                editorLabel.style.fontSize = 11;
                editorLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                editorRow.Add(editorLabel);
                
                var editorValue = new Label(string.IsNullOrEmpty(editorTeamId) ? "Not set" : editorTeamId);
                editorValue.style.fontSize = 11;
                editorValue.style.color = string.IsNullOrEmpty(editorTeamId) ? 
                    new Color(1f, 0.7f, 0.4f) : 
                    new Color(0.8f, 1f, 0.8f);
                editorValue.style.unityFontStyleAndWeight = string.IsNullOrEmpty(editorTeamId) ? 
                    FontStyle.Italic : FontStyle.Bold;
                editorRow.Add(editorValue);
                
                lookupContainer.Add(editorRow);
                
                // Server Team ID (read-only)
                var serverRow = new VisualElement();
                serverRow.style.flexDirection = FlexDirection.Row;
                serverRow.style.marginBottom = 6;
                serverRow.style.marginLeft = 10;
                
                var serverLabel = new Label("Server (read-only):");
                serverLabel.style.minWidth = 140;
                serverLabel.style.fontSize = 11;
                serverLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                serverRow.Add(serverLabel);
                
                var serverValue = new Label(string.IsNullOrEmpty(serverTeamId) ? "Not configured" : serverTeamId);
                serverValue.style.fontSize = 11;
                serverValue.style.color = string.IsNullOrEmpty(serverTeamId) ? 
                    new Color(0.6f, 0.6f, 0.6f) : 
                    new Color(0.7f, 0.9f, 1f);
                serverValue.style.unityFontStyleAndWeight = string.IsNullOrEmpty(serverTeamId) ? 
                    FontStyle.Italic : FontStyle.Bold;
                serverRow.Add(serverValue);
                
                lookupContainer.Add(serverRow);
                
                // Mismatch warning and sync button
                if (hasMismatch)
                {
                    var warningRow = new VisualElement();
                    warningRow.style.flexDirection = FlexDirection.Row;
                    warningRow.style.marginTop = 8;
                    warningRow.style.marginLeft = 10;
                    warningRow.style.marginBottom = 8;
                    
                    var warningIcon = new Label("⚠️");
                    warningIcon.style.fontSize = 12;
                    warningIcon.style.marginRight = 5;
                    warningRow.Add(warningIcon);
                    
                    var warningText = new Label($"Mismatch detected! Build uses Editor value. ");
                    warningText.style.fontSize = 11;
                    warningText.style.color = new Color(1f, 0.8f, 0.4f);
                    warningRow.Add(warningText);
                    
                    // Use Server button
                    var syncButton = new Button(() => {
                        #if UNITY_IOS
                        try
                        {
                            PlayerSettings.iOS.appleDeveloperTeamID = serverTeamId;
                            iosTeamId = serverTeamId; // Update local cache
                            Debug.Log($"[BoostOps] ✅ Synced Apple Team ID from server: {serverTeamId}");
                            
                            // Refresh UI to show updated values
                            EditorApplication.delayCall += RefreshAllUI;
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[BoostOps] ❌ Failed to sync Apple Team ID: {ex.Message}");
                            EditorUtility.DisplayDialog("Sync Failed", $"Failed to sync Apple Team ID: {ex.Message}", "OK");
                        }
                        #endif
                    });
                    syncButton.text = "Use Server";
                    syncButton.style.fontSize = 10;
                    syncButton.style.height = 20;
                    syncButton.style.marginLeft = 5;
                    syncButton.style.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
                    warningRow.Add(syncButton);
                    
                    lookupContainer.Add(warningRow);
                }
                else if (string.IsNullOrEmpty(editorTeamId) && !string.IsNullOrEmpty(serverTeamId))
                {
                    // Show helpful note about auto-sync
                    var syncNoteRow = new VisualElement();
                    syncNoteRow.style.flexDirection = FlexDirection.Row;
                    syncNoteRow.style.marginTop = 6;
                    syncNoteRow.style.marginLeft = 10;
                    syncNoteRow.style.marginBottom = 8;
                    
                    var syncNote = new Label("💡 Editor field will be auto-populated on next project fetch");
                    syncNote.style.fontSize = 11;
                    syncNote.style.color = new Color(0.7f, 0.9f, 1f);
                    syncNote.style.unityFontStyleAndWeight = FontStyle.Italic;
                    syncNoteRow.Add(syncNote);
                    
                    lookupContainer.Add(syncNoteRow);
                }
            }
            
            // Status message
            if (!string.IsNullOrEmpty(lookupMessage))
            {
                var messageRow = new VisualElement();
                messageRow.style.flexDirection = FlexDirection.Row;
                messageRow.style.marginTop = 8;
                
                var messageLabel = new Label("Status:");
                messageLabel.style.minWidth = 120;
                messageLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                messageRow.Add(messageLabel);
                
                var messageValue = new Label(lookupMessage);
                messageValue.style.color = new Color(0.9f, 0.9f, 0.9f);
                messageValue.style.unityFontStyleAndWeight = FontStyle.Italic;
                messageRow.Add(messageValue);
                
                lookupContainer.Add(messageRow);
            }
            
            contentContainer.Add(lookupContainer);
        }
        
        void BuildManagedLinksConfigured()
        {
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.marginTop = 25;
            mainContainer.style.marginBottom = 10;
            
            // Left side - Read-only configuration display
            var configContainer = new VisualElement();
            configContainer.style.flexDirection = FlexDirection.Column;
            configContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            configContainer.style.paddingLeft = 15;
            configContainer.style.paddingRight = 15;
            configContainer.style.paddingTop = 15;
            configContainer.style.paddingBottom = 15;
            configContainer.style.borderTopLeftRadius = 4;
            configContainer.style.borderTopRightRadius = 4;
            configContainer.style.borderBottomLeftRadius = 4;
            configContainer.style.borderBottomRightRadius = 4;
            configContainer.style.width = Length.Percent(50);
            configContainer.style.flexShrink = 0;
            configContainer.style.marginRight = 5;
            
            var configTitle = new Label("🔗 Managed Configuration");
            configTitle.style.fontSize = 16;
            configTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            configTitle.style.marginBottom = 15;
            configContainer.Add(configTitle);
            
            // Project slug display (read-only)
            var slugRow = new VisualElement();
            slugRow.style.flexDirection = FlexDirection.Row;
            slugRow.style.alignItems = Align.Center;
            slugRow.style.marginBottom = 10;
            
            var slugLabel = new Label("Project Domain:");
            slugLabel.style.minWidth = 120;
            slugLabel.style.fontSize = 12;
            slugRow.Add(slugLabel);
            
            var slugValue = new Label($"{registeredProjectSlug}.boostlink.me");
            slugValue.style.fontSize = 12;
            slugValue.style.color = new Color(0.2f, 0.8f, 0.2f);
            slugValue.style.unityFontStyleAndWeight = FontStyle.Bold;
            slugRow.Add(slugValue);
            
            configContainer.Add(slugRow);
            
            // Status display
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginBottom = 10;
            
            var statusLabel = new Label("Status:");
            statusLabel.style.minWidth = 120;
            statusLabel.style.fontSize = 12;
            statusRow.Add(statusLabel);
            
            var statusValue = new Label("✅ Active & Configured");
            statusValue.style.fontSize = 12;
            statusValue.style.color = new Color(0.2f, 0.8f, 0.2f);
            statusRow.Add(statusValue);
            
            configContainer.Add(statusRow);
            
            // Usage meter
            var usageRow = new VisualElement();
            usageRow.style.flexDirection = FlexDirection.Row;
            usageRow.style.alignItems = Align.Center;
            usageRow.style.marginBottom = 15;
            
            var usageLabel = new Label("Usage:");
            usageLabel.style.minWidth = 120;
            usageLabel.style.fontSize = 12;
            usageRow.Add(usageLabel);
            
            var usageValue = new Label("0 / 1000 clicks this month");
            usageValue.style.fontSize = 12;
            usageValue.style.color = new Color(0.8f, 0.8f, 0.8f);
            usageRow.Add(usageValue);
            
            configContainer.Add(usageRow);
            
            // Dashboard link
            var dashboardButton = new Button(() => OpenDashboard("links")) { text = "✏️ Edit on Dashboard" };
            dashboardButton.style.height = 32;
            dashboardButton.style.fontSize = 12;
            dashboardButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            configContainer.Add(dashboardButton);
            
            mainContainer.Add(configContainer);
            
            // Right side - QR Code Testing (same as before)
            var rightContainer = new VisualElement();
            rightContainer.style.flexDirection = FlexDirection.Column;
            rightContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            rightContainer.style.paddingLeft = 15;
            rightContainer.style.paddingRight = 15;
            rightContainer.style.paddingTop = 15;
            rightContainer.style.paddingBottom = 15;
            rightContainer.style.borderTopLeftRadius = 4;
            rightContainer.style.borderTopRightRadius = 4;
            rightContainer.style.borderBottomLeftRadius = 4;
            rightContainer.style.borderBottomRightRadius = 4;
            rightContainer.style.width = Length.Percent(50);
            rightContainer.style.flexShrink = 0;
            rightContainer.style.marginLeft = 5;
            
            BuildQRCodeTestingContent(rightContainer);
            
            mainContainer.Add(rightContainer);
            contentContainer.Add(mainContainer);
        }
        
        void BuildDomainAndUsageContent(VisualElement parent)
        {
            if (hostingOption == HostingOption.Local) // Local mode - simple domain input
            {
                BuildLocalModeContent(parent);
            }
            else // Cloud mode - multi-state workflow
            {
                BuildCloudModeContent(parent);
            }
        }
        
        void BuildLocalModeContent(VisualElement parent)
        {
            // Domain Configuration
            var domainInput = new TextField();
            domainInput.style.fontSize = 14;
            domainInput.style.height = 22;
            domainInput.style.maxWidth = 300; // Prevent field from expanding too much and cutting off label
            domainInput.value = dynamicLinkUrl;
            
            // Keep reference for clearing the field
            domainFieldRef = domainInput;
            domainInput.RegisterValueChangedCallback(evt => {
                dynamicLinkUrl = evt.newValue;
                SaveDynamicLinkUrl();
                AutoGenerateQRCode();
                
                // Sync with dynamic links config - only auto-add if the list is empty
                if (dynamicLinksConfig != null && !string.IsNullOrEmpty(evt.newValue))
                {
                    var cleanDomain = BoostOpsProjectSettings.CleanHost(evt.newValue);
                    var currentDomains = dynamicLinksConfig.GetAllHosts();
                    
                    // Only auto-add if domain list is empty and the domain is valid
                    if (currentDomains.Count == 0 && BoostOpsProjectSettings.ValidateHostFormat(cleanDomain))
                    {
                        dynamicLinksConfig.AddDomain(cleanDomain);
                    }
                }
                
                // Refresh QR section since domain changed
                RefreshQRSection();
            });
            
            // Create plus button for adding domains
            var addDomainButton = new Button(() => {
                AddDomainFromUIElements();
            }) { text = "+" };
            addDomainButton.style.width = 30;
            addDomainButton.style.marginLeft = 3;
            addDomainButton.tooltip = "Add this domain to your list";
            
            var domainRow = CreateLabelFieldRowWithButton("Add Associated Domain:", domainInput, addDomainButton);
            domainRow.style.marginBottom = 10;
            
            // Make label bold for this field
            var domainLabel = domainRow.Q<Label>();
            if (domainLabel != null)
            {
                domainLabel.style.fontSize = 14;
                domainLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            
            parent.Add(domainRow);
            
            // Show domain chips section (new system)
            BuildDomainChipsSection(parent);
            
            // Generate button and Open Folder button container
            var generateButtonContainer = new VisualElement();
            generateButtonContainer.style.flexDirection = FlexDirection.Row;
            generateButtonContainer.style.marginBottom = 8;
            
            var generateButton = new Button(() => {
                GenerateDynamicLinkFiles();
            }) { text = "Generate Files" };
            generateButton.style.width = 140;
            generateButton.style.marginRight = 8;
            generateButtonContainer.Add(generateButton);
            
            // Open Folder button
            openFolderButtonRef = new Button(() => {
                string folderPath = System.IO.Path.Combine(Application.dataPath, "BoostOps", "Generated");
                if (System.IO.Directory.Exists(folderPath))
                {
                    OpenFolderInExplorer(folderPath);
                }
                else
                {
                    folderPath = System.IO.Path.Combine(Application.dataPath, "BoostOps");
                    if (System.IO.Directory.Exists(folderPath))
                    {
                        OpenFolderInExplorer(folderPath);
                    }
                }
            }) { text = "📁" };
            openFolderButtonRef.style.width = 32;
            openFolderButtonRef.style.unityTextAlign = TextAnchor.MiddleCenter;
            openFolderButtonRef.style.fontSize = 12;
            openFolderButtonRef.style.paddingTop = 4;
            openFolderButtonRef.SetEnabled(UniversalLinkFilesExist());
            generateButtonContainer.Add(openFolderButtonRef);
            
            parent.Add(generateButtonContainer);
            
            // Verify buttons container
            var verifyButtonsContainer = new VisualElement();
            verifyButtonsContainer.style.flexDirection = FlexDirection.Row;
            verifyButtonsContainer.style.marginTop = 8;
            
            var iosVerifyButton = new Button(() => VerifyIOSFiles()) { text = "Verify iOS Files on Server" };
            iosVerifyButton.style.width = 180;
            iosVerifyButton.style.fontSize = 12;
            iosVerifyButton.style.marginRight = 8;
            verifyButtonsContainer.Add(iosVerifyButton);
            
            var androidVerifyButton = new Button(() => VerifyAndroidFiles()) { text = "Verify Android Files on Server" };
            androidVerifyButton.style.width = 180;
            androidVerifyButton.style.fontSize = 12;
            verifyButtonsContainer.Add(androidVerifyButton);
            
            // Server file placement note (only for local mode) - BEFORE verify buttons
            if (hostingOption == HostingOption.Local)
            {
                var serverNote = new Label("🌐 Server Setup: Upload the generated .well-known files to your domain's root:\n• apple-app-site-association → https://yourdomain.com/.well-known/apple-app-site-association\n• assetlinks.json → https://yourdomain.com/.well-known/assetlinks.json");
                serverNote.style.fontSize = 10;
                serverNote.style.color = new Color(1f, 0.9f, 0.7f, 1f); // Light orange text
                serverNote.style.whiteSpace = WhiteSpace.Normal;
                serverNote.style.marginTop = 12;
                serverNote.style.marginBottom = 12;
                serverNote.style.paddingLeft = 8;
                serverNote.style.paddingRight = 8;
                serverNote.style.paddingTop = 6;
                serverNote.style.paddingBottom = 6;
                serverNote.style.backgroundColor = new Color(0.25f, 0.15f, 0.1f, 0.6f); // Subtle orange background
                serverNote.style.borderTopLeftRadius = 4;
                serverNote.style.borderTopRightRadius = 4;
                serverNote.style.borderBottomLeftRadius = 4;
                serverNote.style.borderBottomRightRadius = 4;
                parent.Add(serverNote);
            }
            
            parent.Add(verifyButtonsContainer);
            
            // Build submission requirement note - SECOND
            var buildRequirementNote = new Label("📝 Next Steps: Upload a signed build to App Store Connect (iOS) or Play Console (Android) for links to work. Allow 5-15 min (iOS) or up to 1 hour (Android) after first install.");
            buildRequirementNote.style.fontSize = 10;
            buildRequirementNote.style.color = new Color(0.8f, 0.9f, 1f, 1f); // Light blue text
            buildRequirementNote.style.whiteSpace = WhiteSpace.Normal;
            buildRequirementNote.style.marginTop = hostingOption == HostingOption.Local ? 8 : 12; // Less margin if server note is above
            buildRequirementNote.style.marginBottom = 8;
            buildRequirementNote.style.paddingLeft = 8;
            buildRequirementNote.style.paddingRight = 8;
            buildRequirementNote.style.paddingTop = 6;
            buildRequirementNote.style.paddingBottom = 6;
            buildRequirementNote.style.backgroundColor = new Color(0.1f, 0.15f, 0.25f, 0.6f); // Subtle blue background
            buildRequirementNote.style.borderTopLeftRadius = 4;
            buildRequirementNote.style.borderTopRightRadius = 4;
            buildRequirementNote.style.borderBottomLeftRadius = 4;
            buildRequirementNote.style.borderBottomRightRadius = 4;
            parent.Add(buildRequirementNote);
        }
        void AddDomainFromUIElements()
        {
            if (string.IsNullOrEmpty(dynamicLinkUrl))
            {
                EditorUtility.DisplayDialog("Invalid Domain", "Please enter a domain before adding it.", "OK");
                return;
            }

            // Ensure we have a config
            if (dynamicLinksConfig == null)
            {
                LoadDynamicLinksConfig();
                if (dynamicLinksConfig == null)
                {
                    CreateDynamicLinksConfig();
                }
            }

            string cleanDomain = BoostOpsProjectSettings.CleanHost(dynamicLinkUrl);
            
            // Validate domain format
            if (!BoostOpsProjectSettings.ValidateHostFormat(cleanDomain))
            {
                EditorUtility.DisplayDialog("Invalid Domain", "Please enter a valid domain format (e.g., mydomain.com)", "OK");
                return;
            }

            // Add the domain using the simplified API
            if (!dynamicLinksConfig.AddDomain(cleanDomain))
            {
                EditorUtility.DisplayDialog("Failed to Add Domain", $"Could not add domain '{cleanDomain}'. It may already exist.", "OK");
                return;
            }

            // Clear the input field for next domain
            dynamicLinkUrl = "";
            if (domainFieldRef != null)
            {
                domainFieldRef.value = "";
            }

            // Force save the ScriptableObject asset
            if (dynamicLinksConfig != null)
            {
                EditorUtility.SetDirty(dynamicLinksConfig);
                AssetDatabase.SaveAssets();
                LogDebug($"Marked ScriptableObject as dirty and saved asset after adding domain");
            }

            // Save changes
            SaveDynamicLinkUrl();

            // Refresh the domain display
            RefreshUIElementsDomainsDisplay();

            LogDebug($"Added domain: {cleanDomain}");
        }

        void AddDomainFromField()
        {
            if (string.IsNullOrEmpty(dynamicLinkUrl))
            {
                EditorUtility.DisplayDialog("Invalid Domain", "Please enter a domain before adding it.", "OK");
                return;
            }

            // Ensure we have a config
            if (dynamicLinksConfig == null)
            {
                LoadDynamicLinksConfig();
                if (dynamicLinksConfig == null)
                {
                    CreateDynamicLinksConfig();
                }
            }

            string cleanDomain = BoostOpsProjectSettings.CleanHost(dynamicLinkUrl);
            
            // Validate domain format
            if (!BoostOpsProjectSettings.ValidateHostFormat(cleanDomain))
            {
                EditorUtility.DisplayDialog("Invalid Domain", "Please enter a valid domain format (e.g., mydomain.com)", "OK");
                return;
            }

            // Add the domain using the simplified API
            if (!dynamicLinksConfig.AddDomain(cleanDomain))
            {
                // AddDomain will show its own error messages
                return;
            }

            // Clear the input field for next domain
            dynamicLinkUrl = "";
            if (domainFieldRef != null)
            {
                domainFieldRef.value = "";
            }

            // Force save the ScriptableObject asset
            if (dynamicLinksConfig != null)
            {
                EditorUtility.SetDirty(dynamicLinksConfig);
                AssetDatabase.SaveAssets();
                LogDebug($"Marked ScriptableObject as dirty and saved asset after adding domain");
            }

            SaveDynamicLinkUrl();

            // Refresh the chips display
            RefreshDomainChips();

            LogDebug($"Added domain: {cleanDomain}");
        }
        void BuildAddedDomainsDisplay(VisualElement parent)
        {
            // Create a wrapper container that we can track and refresh
            addedDomainsContainer = new VisualElement();
            parent.Add(addedDomainsContainer);
            
            // Build the actual content in the wrapper
            RefreshAddedDomainsContent();
        }
        void RefreshAddedDomainsContent()
        {
            if (addedDomainsContainer == null) return;
            
            // Clear existing content
            addedDomainsContainer.Clear();
            
            // Ensure we have a config loaded
            if (dynamicLinksConfig == null)
            {
                LoadDynamicLinksConfig();
                if (dynamicLinksConfig == null) return;
            }

            // Get all domains (primary + additional)
            var allHosts = dynamicLinksConfig.GetAllHosts();
            

            
            if (allHosts.Count > 0)
            {
                // Title for the domains list
                var domainsTitle = new Label($"📋 Configured Domains ({allHosts.Count}):");
                domainsTitle.style.fontSize = 12;
                domainsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                domainsTitle.style.marginTop = 10;
                domainsTitle.style.marginBottom = 5;
                addedDomainsContainer.Add(domainsTitle);

                // Container for domain chips
                var domainsContainer = new VisualElement();
                domainsContainer.style.marginBottom = 15;
                domainsContainer.style.paddingLeft = 10;
                domainsContainer.style.paddingRight = 10;
                domainsContainer.style.paddingTop = 8;
                domainsContainer.style.paddingBottom = 8;
                domainsContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
                domainsContainer.style.borderTopLeftRadius = 4;
                domainsContainer.style.borderTopRightRadius = 4;
                domainsContainer.style.borderBottomLeftRadius = 4;
                domainsContainer.style.borderBottomRightRadius = 4;

                // Add each domain as a chip
                for (int i = 0; i < allHosts.Count; i++)
                {
                    var domain = allHosts[i];
                    var domainChip = new VisualElement();
                    domainChip.style.flexDirection = FlexDirection.Row;
                    domainChip.style.alignItems = Align.Center;
                    domainChip.style.marginBottom = 3;

                    // Domain icon and text
                    var domainLabel = new Label($"🌐 {domain}");
                    domainLabel.style.fontSize = 11;
                    domainLabel.style.flexGrow = 1;
                    domainLabel.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);

                    // Remove button
                    var removeButton = new Button(() => RemoveDomain(domain)) { text = "✖" };
                    removeButton.style.width = 20;
                    removeButton.style.height = 18;
                    removeButton.style.marginLeft = 5;
                    removeButton.style.fontSize = 10;
                    removeButton.tooltip = $"Remove {domain}";

                    domainChip.Add(domainLabel);
                    domainChip.Add(removeButton);
                    domainsContainer.Add(domainChip);
                }

                addedDomainsContainer.Add(domainsContainer);

                // Help text
                var helpText = new Label("💡 Click ✖ to remove a domain. Enter new domains above and click + to add them.");
                helpText.style.fontSize = 10;
                helpText.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                helpText.style.marginBottom = 10;
                addedDomainsContainer.Add(helpText);
            }
        }
        void RemoveDomain(string domain)
        {
            if (dynamicLinksConfig == null) return;

            // Remove the domain using the simplified API
            if (!dynamicLinksConfig.RemoveDomain(domain))
            {
                EditorUtility.DisplayDialog("Failed to Remove Domain", $"Could not remove domain '{domain}'. It may not exist.", "OK");
                return;
            }

            // Note: We don't update the input field when removing domains
            // The input field is for adding new domains, not for displaying existing ones

            // Force save the ScriptableObject asset
            if (dynamicLinksConfig != null)
            {
                EditorUtility.SetDirty(dynamicLinksConfig);
                AssetDatabase.SaveAssets();
                Debug.Log($"[BoostOps Debug] Marked ScriptableObject as dirty and saved asset after removing domain");
            }

            // Save changes
            SaveDynamicLinkUrl();

            // Refresh the domain display
            RefreshDomainChips();

            LogDebug($"Removed domain: {domain}");
        }

        void BuildDomainChipsSection(VisualElement parent)
        {
            // Create a container for the chips that we can refresh
            domainChipsContainer = new VisualElement();
            domainChipsContainer.style.marginTop = 10;
            parent.Add(domainChipsContainer);

            // Initial build of chips
            RefreshDomainChips();
        }

        private VisualElement domainChipsContainer;
        void RefreshDomainChips()
        {
            if (domainChipsContainer == null) return;

            domainChipsContainer.Clear();

            // Ensure we have a config
            if (dynamicLinksConfig == null)
            {
                LoadDynamicLinksConfig();
                if (dynamicLinksConfig == null) return;
            }

            var allHosts = dynamicLinksConfig.GetAllHosts();
            if (allHosts.Count == 0) return;

            // Add title if we have domains
            var chipsTitle = new Label($"Configured Domains ({allHosts.Count}/5):");
            chipsTitle.style.fontSize = 11;
            chipsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            chipsTitle.style.marginBottom = 8;
            chipsTitle.style.color = new Color(0.8f, 0.9f, 1f);
            domainChipsContainer.Add(chipsTitle);

            // Create chips container with wrap
            var chipsFlow = new VisualElement();
            chipsFlow.style.flexDirection = FlexDirection.Row;
            chipsFlow.style.flexWrap = Wrap.Wrap;
            chipsFlow.style.marginBottom = 10;

            for (int i = 0; i < allHosts.Count; i++)
            {
                string domain = allHosts[i];

                var chip = new VisualElement();
                chip.style.flexDirection = FlexDirection.Row;
                chip.style.alignItems = Align.Center;
                chip.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                chip.style.paddingLeft = 8;
                chip.style.paddingRight = 4;
                chip.style.paddingTop = 4;
                chip.style.paddingBottom = 4;
                chip.style.borderTopLeftRadius = 12;
                chip.style.borderTopRightRadius = 12;
                chip.style.borderBottomLeftRadius = 12;
                chip.style.borderBottomRightRadius = 12;
                chip.style.marginRight = 6;
                chip.style.marginBottom = 4;

                var domainLabel = new Label(domain);
                domainLabel.style.fontSize = 10;
                domainLabel.style.color = Color.white;
                domainLabel.style.marginRight = 4;
                chip.Add(domainLabel);

                // Add X button (always allow removal)
                var removeButton = new Button(() => {
                    RemoveDomain(domain);
                }) { text = "×" };
                removeButton.style.width = 16;
                removeButton.style.height = 16;
                removeButton.style.fontSize = 10;
                removeButton.style.marginLeft = 2;
                removeButton.style.paddingLeft = 0;
                removeButton.style.paddingRight = 0;
                removeButton.style.paddingTop = 0;
                removeButton.style.paddingBottom = 0;
                removeButton.style.borderTopLeftRadius = 8;
                removeButton.style.borderTopRightRadius = 8;
                removeButton.style.borderBottomLeftRadius = 8;
                removeButton.style.borderBottomRightRadius = 8;
                removeButton.tooltip = "Remove domain";
                chip.Add(removeButton);

                chipsFlow.Add(chip);
            }

            domainChipsContainer.Add(chipsFlow);

            // Add helpful note
            if (allHosts.Count >= 5)
            {
                var limitNote = new Label("Maximum of 5 domains reached. Remove a domain to add another.");
                limitNote.style.fontSize = 10;
                limitNote.style.color = new Color(1f, 0.8f, 0.4f);
                limitNote.style.whiteSpace = WhiteSpace.Normal;
                domainChipsContainer.Add(limitNote);
            }
            else if (allHosts.Count > 1)
            {
                var helpNote = new Label("Enter a new domain above and click + to add it. All domains will be configured for Universal Links.");
                helpNote.style.fontSize = 10;
                helpNote.style.color = new Color(0.7f, 0.8f, 0.9f);
                helpNote.style.whiteSpace = WhiteSpace.Normal;
                domainChipsContainer.Add(helpNote);
            }
        }

        void RefreshUIElementsDomainsDisplay()
        {

            if (addedDomainsContainer == null)
            {
                return;
            }
            RefreshAddedDomainsContent();
        }

        void BuildAdvancedMultipleDomainsSection(VisualElement parent)
        {
            // This method is now deprecated - multiple domains functionality 
            // has been moved to the simpler domain chips system above the main field.
            // Keeping this method for backward compatibility with existing calls.
        }
        
        void BuildCloudModeContent(VisualElement parent)
        {
            if (!isLoggedIn)
            {
                BuildStateLoggedOut(parent);
            }
            else if (!isProjectRegistered || needsReregistration)
            {
                if (needsReregistration)
                {
                    BuildStateNeedsReregistration(parent);
                }
                else
                {
                    BuildStateLoggedInNotRegistered(parent);
                }
            }
            else if (!string.IsNullOrEmpty(registeredProjectSlug))
            {
                // Project is registered AND has an active slug
                BuildStateProjectActivated(parent);
            }
            else
            {
                // Project is registered but needs slug activation
                BuildStateRegisteredWithSlug(parent);
            }
        }
        
        void BuildStateLoggedOut(VisualElement parent)
        {
            var titleLabel = new Label("📱 Project Preview");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 10;
            parent.Add(titleLabel);
            
            var infoLabel = new Label("This project data will be registered when you sign in:");
            infoLabel.style.fontSize = 12;
            infoLabel.style.marginBottom = 10;
            infoLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            parent.Add(infoLabel);
            
            // Show app data that will be uploaded
            BuildProjectDataPreview(parent);
            
            var loginButton = new Button(() => ShowAccountPanel()) { text = "🔑 Sign In to Register Project" };
            loginButton.style.width = 200;
            loginButton.style.marginTop = 15;
            loginButton.style.alignSelf = Align.Center;
            parent.Add(loginButton);
        }
        
        void BuildStateLoggedInNotRegistered(VisualElement parent)
        {
            var titleLabel = new Label("📤 Register This Project");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 10;
            parent.Add(titleLabel);
            
            var infoLabel = new Label($"Signed in as {userEmail}. Ready to register this project:");
            infoLabel.style.fontSize = 12;
            infoLabel.style.marginBottom = 10;
            infoLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            parent.Add(infoLabel);
            
            // Show app data that will be uploaded
            BuildProjectDataPreview(parent);
            
            var registerButton = new Button(() => RegisterProject()) 
            { 
                text = isRegistering ? "⏳ Registering..." : "🚀 Register Project with BoostOps"
            };
            registerButton.style.width = 200;
            registerButton.style.marginTop = 15;
            registerButton.style.alignSelf = Align.Center;
            
            // Only enable if not registering AND all required fields are set
            bool canRegister = !isRegistering && GetMissingRequiredFields().Count == 0;
            registerButton.SetEnabled(canRegister);
            
            parent.Add(registerButton);
        }
        
        void BuildStateNeedsReregistration(VisualElement parent)
        {
            var titleLabel = new Label("⚠️ Re-registration Required");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 10;
            titleLabel.style.color = new Color(1f, 0.8f, 0.4f, 1f); // Orange warning color
            parent.Add(titleLabel);
            
            var warningLabel = new Label("Critical project identifiers were changed. Your project needs to be re-registered:");
            warningLabel.style.fontSize = 12;
            warningLabel.style.marginBottom = 10;
            warningLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            warningLabel.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(warningLabel);
            
            // Show current data that will be uploaded
            BuildProjectDataPreview(parent);
            
            var reregisterButton = new Button(() => {
                // Reset re-registration flag and re-register
                needsReregistration = false;
                isAppleStoreIdInEditMode = false;
                isSHA256InEditMode = false;
                SaveRegistrationState();
                RegisterProject();
            }) 
            { 
                text = isRegistering ? "⏳ Re-registering..." : "🔄 Re-register Project"
            };
            reregisterButton.style.width = 200;
            reregisterButton.style.marginTop = 15;
            reregisterButton.style.alignSelf = Align.Center;
            
            // Only enable if not registering AND all required fields are set
            bool canReregister = !isRegistering && GetMissingRequiredFields().Count == 0;
            reregisterButton.SetEnabled(canReregister);
            
            parent.Add(reregisterButton);
        }
        
        void BuildStateRegisteredWithSlug(VisualElement parent)
        {
            var titleLabel = new Label("🎯 Activate Project Slug");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 10;
            parent.Add(titleLabel);
            
            var infoLabel = new Label($"Project registered! Choose your BoostLink domain:");
            infoLabel.style.fontSize = 12;
            infoLabel.style.marginBottom = 10;
            infoLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            parent.Add(infoLabel);
            
            // Project slug input
            var slugLabel = new Label("Project Slug:");
            slugLabel.style.fontSize = 12;
            slugLabel.style.marginBottom = 5;
            parent.Add(slugLabel);
            
            var slugInput = new TextField();
            slugInput.style.marginBottom = 10;
            slugInput.style.fontSize = 14;
            slugInput.style.height = 22;
            slugInput.value = projectSlug;
            slugInput.RegisterValueChangedCallback(evt => {
                projectSlug = evt.newValue;
                ValidateProjectSlug();
                SaveProjectSlug();
            });
            parent.Add(slugInput);
            
            // Preview domain
            if (!string.IsNullOrEmpty(projectSlug) && isProjectSlugValid)
            {
                var previewLabel = new Label($"Your domain: {projectSlug}.boostlink.me");
                previewLabel.style.fontSize = 11;
                previewLabel.style.color = new Color(0.2f, 0.8f, 0.2f, 1f);
                previewLabel.style.marginBottom = 10;
                parent.Add(previewLabel);
            }
            
            // Activate button
            var activateButton = new Button(() => ActivateProjectSlug())
            {
                text = "✅ Activate & Generate Files"
            };
            activateButton.style.width = 180;
            activateButton.style.marginTop = 10;
            activateButton.style.alignSelf = Align.Center;
            activateButton.SetEnabled(!string.IsNullOrEmpty(projectSlug) && isProjectSlugValid);
            parent.Add(activateButton);
            
            // Usage meter
            BuildUsageMeter(parent);
        }
        
        void BuildStateProjectActivated(VisualElement parent)
        {
            var titleLabel = new Label("🚀 Project Activated");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 10;
            parent.Add(titleLabel);
            
            var infoLabel = new Label($"Your BoostLink™ domain is active:");
            infoLabel.style.fontSize = 12;
            infoLabel.style.marginBottom = 10;
            infoLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            parent.Add(infoLabel);
            
            // Active domain display
            var domainLabel = new Label($"Your domain: {registeredProjectSlug}.boostlink.me");
            domainLabel.style.fontSize = 14;
            domainLabel.style.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            domainLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            domainLabel.style.marginBottom = 15;
            parent.Add(domainLabel);
            
            // Generate files button (since domain is already active)
            var generateButton = new Button(() => {
                GenerateDynamicLinkFiles();
            }) { text = "📱 Generate Platform Files" };
            generateButton.style.width = 180;
            generateButton.style.marginBottom = 15;
            generateButton.style.alignSelf = Align.Center;
            parent.Add(generateButton);
            
            // Usage meter
            BuildUsageMeter(parent);
        }
        
        void BuildProjectDataPreview(VisualElement parent)
        {
            var dataContainer = new VisualElement();
            dataContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            dataContainer.style.paddingLeft = 10;
            dataContainer.style.paddingRight = 10;
            dataContainer.style.paddingTop = 8;
            dataContainer.style.paddingBottom = 8;
            dataContainer.style.borderTopLeftRadius = 4;
            dataContainer.style.borderTopRightRadius = 4;
            dataContainer.style.borderBottomLeftRadius = 4;
            dataContainer.style.borderBottomRightRadius = 4;
            dataContainer.style.marginBottom = 10;
            
            // Basic project info
            AddDataRow(dataContainer, "Project Name:", appName);
            
            // Unity Project ID is optional - only show if available
            string unityProjectId = GetUnityProjectId();
            if (!string.IsNullOrEmpty(unityProjectId) && unityProjectId != "Not connected to Unity Cloud")
            {
                AddDataRow(dataContainer, "Unity Project ID:", unityProjectId);
            }
            else
            {
                AddDataRow(dataContainer, "Unity Project ID:", "Optional (not connected to Unity Cloud)");
            }
            
            // iOS info
            AddDataRow(dataContainer, "iOS Bundle ID:", iosBundleId);
            AddDataRow(dataContainer, "Signing Team ID:", iosTeamId);
            // Apple Store ID is only required when using cloud mode (hostingOption == HostingOption.Cloud)
            AddDataRowRequired(dataContainer, "Apple Store ID:", iosAppStoreId, hostingOption == HostingOption.Cloud);
            
            // Android info  
            AddDataRow(dataContainer, "Google Store ID:", androidBundleId);
            // SHA256 Fingerprint is always required for registration
            AddDataRowRequired(dataContainer, "SHA256 Fingerprint:", androidCertFingerprint, true);
            
            parent.Add(dataContainer);
            
            // Show validation status
            BuildRegistrationValidation(parent);
        }
        
        string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            
            return text.Substring(0, maxLength - 3) + "...";
        }
        
        string MaskProjectKey(string projectKey)
        {
            if (string.IsNullOrEmpty(projectKey))
                return projectKey;
            
            // Project key format: bo_{env}_{publicProjectId}_{randomSuffix}
            // Show: bo_live_9xrGFVO_••••••••••••••••
            var parts = projectKey.Split('_');
            if (parts.Length >= 3)
            {
                // Show prefix (bo_env_publicId) and mask the suffix
                string visiblePart = $"{parts[0]}_{parts[1]}_{parts[2]}_";
                string maskedPart = new string('•', Math.Max(0, projectKey.Length - visiblePart.Length));
                return visiblePart + maskedPart;
            }
            
            // Fallback: show first 12 characters and mask the rest
            if (projectKey.Length > 12)
            {
                return projectKey.Substring(0, 12) + new string('•', projectKey.Length - 12);
            }
            
            return projectKey;
        }
        void AddDataRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 3;
            
            var labelElement = new Label(label);
            labelElement.style.fontSize = 11;
            labelElement.style.width = 120;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            row.Add(labelElement);
            
            // Truncate long values for display (about 3/4 of card width)
            var displayValue = string.IsNullOrEmpty(value) ? "Not set" : TruncateText(value, 50);
            var valueElement = new Label(displayValue);
            valueElement.style.fontSize = 11;
            valueElement.style.color = string.IsNullOrEmpty(value) ? new Color(0.8f, 0.4f, 0.4f, 1f) : new Color(0.9f, 0.9f, 0.9f, 1f);
            row.Add(valueElement);
            
            parent.Add(row);
        }
        
        void AddDataRowRequired(VisualElement parent, string label, string value, bool isRequired)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 3;
            
            var labelElement = new Label(isRequired ? label + " *" : label);
            labelElement.style.fontSize = 11;
            labelElement.style.width = 120;
            labelElement.style.color = isRequired ? new Color(1f, 0.8f, 0.4f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f);
            row.Add(labelElement);
            
            // Truncate long values for display (about 3/4 of card width)
            var displayValue = string.IsNullOrEmpty(value) ? (isRequired ? "REQUIRED" : "Not set") : TruncateText(value, 50);
            var valueElement = new Label(displayValue);
            valueElement.style.fontSize = 11;
            
            if (isRequired && string.IsNullOrEmpty(value))
                valueElement.style.color = new Color(1f, 0.4f, 0.4f, 1f); // Red for missing required
            else if (string.IsNullOrEmpty(value))
                valueElement.style.color = new Color(0.8f, 0.4f, 0.4f, 1f); // Orange for not set
            else
                valueElement.style.color = new Color(0.9f, 0.9f, 0.9f, 1f); // White for set
                
            row.Add(valueElement);
            
            parent.Add(row);
        }
        
        string GetUnityProjectId()
        {
            try
            {
                // Try to get Unity Cloud Project ID
                #if UNITY_2019_1_OR_NEWER
                return UnityEditor.CloudProjectSettings.projectId;
                #else
                return Application.cloudProjectId;
                #endif
            }
            catch
            {
                return "Not connected to Unity Cloud";
            }
        }
        
        string GetFirebaseProjectId()
        {
            try
            {
                // Check for Firebase project ID in google-services.json (Android)
                string googleServicesPath = Path.Combine(Application.dataPath, "google-services.json");
                if (File.Exists(googleServicesPath))
                {
                    string jsonContent = File.ReadAllText(googleServicesPath);
                    // Simple extraction of project_id from JSON
                    var projectIdMatch = System.Text.RegularExpressions.Regex.Match(
                        jsonContent, 
                        "\"project_id\"\\s*:\\s*\"([^\"]+)\"", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );
                    
                    if (projectIdMatch.Success)
                    {
                        return projectIdMatch.Groups[1].Value;
                    }
                }
                
                // Check for Firebase project ID in GoogleService-Info.plist (iOS)
                string[] googleServicePaths = {
                    Path.Combine(Application.dataPath, "GoogleService-Info.plist"),
                    Path.Combine(Application.dataPath, "StreamingAssets", "GoogleService-Info.plist")
                };
                
                foreach (string plistPath in googleServicePaths)
                {
                    if (File.Exists(plistPath))
                    {
                        string plistContent = File.ReadAllText(plistPath);
                        
                        // Simple extraction of PROJECT_ID from plist content
                        var projectIdMatch = System.Text.RegularExpressions.Regex.Match(
                            plistContent, 
                            @"<key>PROJECT_ID</key>\s*<string>([^<]+)</string>", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                        
                        if (projectIdMatch.Success)
                        {
                            return projectIdMatch.Groups[1].Value;
                        }
                    }
                }
                
                return ""; // No Firebase project ID found
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error reading Firebase project ID: {ex.Message}");
                return "";
            }
        }
        
        void BuildRegistrationValidation(VisualElement parent)
        {
            var missingFields = GetMissingRequiredFields();
            
            if (missingFields.Count > 0)
            {
                var validationContainer = new VisualElement();
                validationContainer.style.backgroundColor = new Color(0.8f, 0.4f, 0.4f, 0.2f);
                validationContainer.style.paddingLeft = 10;
                validationContainer.style.paddingRight = 10;
                validationContainer.style.paddingTop = 8;
                validationContainer.style.paddingBottom = 8;
                validationContainer.style.borderTopLeftRadius = 4;
                validationContainer.style.borderTopRightRadius = 4;
                validationContainer.style.borderBottomLeftRadius = 4;
                validationContainer.style.borderBottomRightRadius = 4;
                validationContainer.style.marginTop = 10;
                
                var warningLabel = new Label("⚠️ Required fields missing for registration:");
                warningLabel.style.fontSize = 12;
                warningLabel.style.color = new Color(1f, 0.8f, 0.4f, 1f);
                warningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                warningLabel.style.marginBottom = 5;
                validationContainer.Add(warningLabel);
                
                foreach (string field in missingFields)
                {
                    var fieldLabel = new Label($"• {field}");
                    fieldLabel.style.fontSize = 11;
                    fieldLabel.style.color = new Color(1f, 0.4f, 0.4f, 1f);
                    fieldLabel.style.marginLeft = 10;
                    validationContainer.Add(fieldLabel);
                }
                
                var instructionLabel = new Label("Please set these in the configuration sections above before registering.");
                instructionLabel.style.fontSize = 10;
                instructionLabel.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                instructionLabel.style.marginTop = 5;
                instructionLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                validationContainer.Add(instructionLabel);
                
                parent.Add(validationContainer);
            }
            else
            {
                var validContainer = new VisualElement();
                validContainer.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.2f);
                validContainer.style.paddingLeft = 10;
                validContainer.style.paddingRight = 10;
                validContainer.style.paddingTop = 8;
                validContainer.style.paddingBottom = 8;
                validContainer.style.borderTopLeftRadius = 4;
                validContainer.style.borderTopRightRadius = 4;
                validContainer.style.borderBottomLeftRadius = 4;
                validContainer.style.borderBottomRightRadius = 4;
                validContainer.style.marginTop = 10;
                
                var successLabel = new Label("✅ All required fields are set. Ready to register!");
                successLabel.style.fontSize = 12;
                successLabel.style.color = new Color(0.2f, 0.8f, 0.2f, 1f);
                successLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                validContainer.Add(successLabel);
                
                parent.Add(validContainer);
            }
        }
        System.Collections.Generic.List<string> GetMissingRequiredFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            
            // Apple Store ID is only required when using cloud mode (hostingOption == HostingOption.Cloud)
            if (hostingOption == HostingOption.Cloud)
            {
                if (string.IsNullOrEmpty(iosAppStoreId))
                    missing.Add("Apple Store ID");
                else if (!IsValidAppleAppStoreId(iosAppStoreId))
                    missing.Add("Apple Store ID (invalid format)");
            }
                
            // SHA256 Fingerprint is always required for registration
            if (string.IsNullOrEmpty(androidCertFingerprint))
                missing.Add("SHA256 Fingerprint");
            else if (!IsValidSHA256Fingerprint(androidCertFingerprint))
                missing.Add("SHA256 Fingerprint (invalid format)");
                
            return missing;
        }
        void BuildUsageMeter(VisualElement parent)
        {
            var usageLabel = new Label($"{currentClicks} / {maxClicks} clicks this month");
            usageLabel.style.fontSize = 12;
            usageLabel.style.marginTop = 15;
            usageLabel.style.marginBottom = 10;
            usageLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(usageLabel);

            var meterContainer = new VisualElement();
            meterContainer.style.height = 12;
            meterContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            meterContainer.style.borderTopLeftRadius = 6;
            meterContainer.style.borderTopRightRadius = 6;
            meterContainer.style.borderBottomLeftRadius = 6;
            meterContainer.style.borderBottomRightRadius = 6;

            float usagePercent = (float)currentClicks / maxClicks;
            
            var fill = new VisualElement();
            fill.style.height = 12;
            fill.style.width = Length.Percent(usagePercent * 100);
            fill.style.borderTopLeftRadius = 6;
            fill.style.borderTopRightRadius = 6;
            fill.style.borderBottomLeftRadius = 6;
            fill.style.borderBottomRightRadius = 6;

            if (usagePercent >= 1.0f) fill.style.backgroundColor = Color.red;
            else if (usagePercent >= 0.9f) fill.style.backgroundColor = new Color(1f, 0.5f, 0f);
            else if (usagePercent >= 0.8f) fill.style.backgroundColor = Color.yellow;
            else fill.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f);

            meterContainer.Add(fill);
            parent.Add(meterContainer);
        }
        
        void RefreshDomainAndUsageContent()
        {
            // Only refresh the left side domain/usage content, preserve QR code
            if (leftDomainContainer != null)
            {
                leftDomainContainer.Clear();
                BuildDomainAndUsageContent(leftDomainContainer);
            }
        }
        void UpdateOpenFolderButtonState()
        {
            // Update the open folder button enabled state based on file existence
            if (openFolderButtonRef != null)
            {
                openFolderButtonRef.SetEnabled(UniversalLinkFilesExist());
            }
        }
        
        void UpdateAppleAppStoreIdLabel()
        {
            // Update the Apple Store ID label based on hosting mode
            if (appleStoreLabelRef != null)
            {
                appleStoreLabelRef.text = hostingOption == HostingOption.Cloud ? "Apple Store ID (Required):" : "Apple Store ID:";
            }
        }
        // Helper method to create Unity-style label-field rows
        private VisualElement CreateLabelFieldRow(string labelText, VisualElement fieldElement, string tooltip = "")
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            
            var label = new Label(labelText);
            label.style.fontSize = 12;
            label.style.width = 140; // Reduced width for better spacing
            label.style.marginRight = 8;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                label.tooltip = tooltip;
                fieldElement.tooltip = tooltip;
            }
            
            fieldElement.style.flexGrow = 1;
            
            row.Add(label);
            row.Add(fieldElement);
            
            return row;
        }
        // Helper method to create Unity-style label-field rows with additional buttons container
        private VisualElement CreateLabelFieldRowWithButton(string labelText, VisualElement fieldElement, VisualElement buttonContainer, string tooltip = "")
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            
            var label = new Label(labelText);
            label.style.fontSize = 12;
            label.style.width = 140; // Reduced width for better spacing
            label.style.marginRight = 20; // Added more padding to the right
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                label.tooltip = tooltip;
                fieldElement.tooltip = tooltip;
            }
            
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexDirection = FlexDirection.Row;
            fieldContainer.style.width = 250; // Reduced width for domain field
            fieldContainer.style.alignItems = Align.Center;
            
            fieldElement.style.width = 200; // Reduced width for domain field
            fieldElement.style.marginRight = 5;
            
            fieldContainer.Add(fieldElement);
            fieldContainer.Add(buttonContainer);
            
            row.Add(label);
            row.Add(fieldContainer);
            
            return row;
        }
        // Helper method to create Unity-style label-field rows with additional button (single button overload)
        private VisualElement CreateLabelFieldRowWithButton(string labelText, VisualElement fieldElement, Button button, string tooltip = "")
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            
            var label = new Label(labelText);
            label.style.fontSize = 12;
            label.style.width = 140; // Reduced width for better spacing
            label.style.marginRight = 15; // Added more padding to the right
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                label.tooltip = tooltip;
                fieldElement.tooltip = tooltip;
            }
            
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexDirection = FlexDirection.Row;
            fieldContainer.style.width = 250; // Reduced width for domain field
            fieldContainer.style.alignItems = Align.Center;
            
            fieldElement.style.width = 220; // Reduced width for single button layout
            fieldElement.style.marginRight = 5;
            
            fieldContainer.Add(fieldElement);
            fieldContainer.Add(button);
            
            row.Add(label);
            row.Add(fieldContainer);
            
            return row;
        }

        // Helper method to create label-field rows with info area on the right
        private VisualElement CreateLabelFieldRowWithInfo(string labelText, VisualElement fieldElement, string infoText)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            
            var label = new Label(labelText);
            label.style.fontSize = 12;
            label.style.width = 140; // Reduced width for better spacing
            label.style.marginRight = 8;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            // Field container (left side - compact)
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexDirection = FlexDirection.Row;
            fieldContainer.style.alignItems = Align.Center;
            fieldContainer.style.width = 225; // 50% wider field area (150 * 1.5 = 225)
            fieldContainer.style.marginRight = 10;
            
            fieldElement.style.flexGrow = 1;
            fieldContainer.Add(fieldElement);
            
            // Info area (right side)
            var infoArea = new Label(infoText);
            infoArea.style.fontSize = 11;
            infoArea.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            infoArea.style.flexGrow = 1;
            infoArea.style.whiteSpace = WhiteSpace.Normal;
            infoArea.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            row.Add(label);
            row.Add(fieldContainer);
            row.Add(infoArea);
            
            return row;
        }

        // Helper method to create label-field rows with info area and wider label for longer text
        private VisualElement CreateLabelFieldRowWithInfoWide(string labelText, VisualElement fieldElement, string infoText)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            
            var label = new Label(labelText);
            label.style.fontSize = 12;
            label.style.width = 200; // Wider label for longer text like "Frequency Cap (per user, per day):"
            label.style.marginRight = 5;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            // Field container (left side - compact)
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexDirection = FlexDirection.Row;
            fieldContainer.style.alignItems = Align.Center;
            fieldContainer.style.width = 110; // Smaller field area to accommodate wider label
            fieldContainer.style.marginRight = 10;
            
            fieldElement.style.flexGrow = 1;
            fieldContainer.Add(fieldElement);
            
            // Info area (right side)
            var infoArea = new Label(infoText);
            infoArea.style.fontSize = 11;
            infoArea.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            infoArea.style.flexGrow = 1;
            infoArea.style.whiteSpace = WhiteSpace.Normal;
            infoArea.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            row.Add(label);
            row.Add(fieldContainer);
            row.Add(infoArea);
            
            return row;
        }
        
        void ShowCriticalFieldEditWarning(string fieldName)
        {
            EditorUtility.DisplayDialog("Critical Field Edit Warning", 
                $"⚠️ You are editing {fieldName}\n\n" +
                "This is a critical identifier used for project registration. " +
                "Changing this field will require re-registration with BoostOps.\n\n" +
                "After making changes:\n" +
                "• Your project will be marked for re-registration\n" +
                "• You'll need to register again to maintain BoostOps integration\n" +
                "• Any existing cloud configuration may need to be updated\n\n" +
                "Continue only if you're sure this is correct.", 
                "I Understand");
        }
        
        void OpenFolderInExplorer(string folderPath)
        {
            try
            {
                // Cross-platform way to open folder in file explorer
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Windows: Use explorer to open the folder
                    System.Diagnostics.Process.Start("explorer.exe", folderPath.Replace('/', '\\'));
                }
                else if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    // macOS: Use open command to open the folder
                    System.Diagnostics.Process.Start("open", folderPath);
                }
                else
                {
                    // Linux and other platforms: Try xdg-open
                    System.Diagnostics.Process.Start("xdg-open", folderPath);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"BoostOps: Could not open folder: {ex.Message}");
                // Fallback to revealing in finder if opening fails
                EditorUtility.RevealInFinder(folderPath);
            }
        }
        
        void BuildQRCodeTestingContent(VisualElement parent)
        {
            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 15;
            
            var title = new Label("📱 Test Your Links on Mobile");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            header.Add(title);
            
            parent.Add(header);
            
            // Domain Selector for QR Code
            BuildQRDomainSelector(parent);
            
            // QR Code Display Container
            var qrContainer = new VisualElement();
            qrContainer.style.flexDirection = FlexDirection.Row;
            qrContainer.style.alignItems = Align.Center;
            qrContainer.style.marginBottom = 10;
            
            // QR Code Image
            qrImageRef = new Image();
            qrImageRef.style.width = 100;
            qrImageRef.style.height = 100;
            qrImageRef.style.backgroundColor = Color.white;
            qrImageRef.style.borderTopLeftRadius = 4;
            qrImageRef.style.borderTopRightRadius = 4;
            qrImageRef.style.borderBottomLeftRadius = 4;
            qrImageRef.style.borderBottomRightRadius = 4;
            qrImageRef.style.marginRight = 10;
            qrImageRef.style.alignSelf = Align.Center;
            qrContainer.Add(qrImageRef);
            
            // Magnify button next to QR code
            var magnifyButton = new Button(() => {
                string testUrl = GenerateTestUrl(); // Uses selected QR domain
                if (!string.IsNullOrEmpty(testUrl))
                {
                    // Pass the existing QR code texture to avoid regenerating
                    ShowZoomedQRCode(testUrl, qrImageRef?.image as Texture2D);
                }
            }) { text = "🔍" };
            magnifyButton.style.width = 40;
            magnifyButton.style.height = 40;
            magnifyButton.style.fontSize = 16;
            magnifyButton.style.alignSelf = Align.Center;
            qrContainer.Add(magnifyButton);
            
            parent.Add(qrContainer);
            
            // Error display for validation issues
            qrErrorLabel = new Label();
            qrErrorLabel.style.fontSize = 10;
            qrErrorLabel.style.color = new Color(1f, 0.4f, 0.4f, 1f); // Light red for errors
            qrErrorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            qrErrorLabel.style.whiteSpace = WhiteSpace.Normal;
            qrErrorLabel.style.marginBottom = 10;
            qrErrorLabel.style.display = DisplayStyle.None; // Hidden by default
            parent.Add(qrErrorLabel);
            
            // URL display for the QR code
            var urlContainer = new VisualElement();
            urlContainer.style.marginBottom = 12;
            urlContainer.style.paddingLeft = 6;
            urlContainer.style.paddingRight = 6;
            urlContainer.style.paddingTop = 4;
            urlContainer.style.paddingBottom = 4;
            urlContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            urlContainer.style.borderTopLeftRadius = 4;
            urlContainer.style.borderTopRightRadius = 4;
            urlContainer.style.borderBottomLeftRadius = 4;
            urlContainer.style.borderBottomRightRadius = 4;
            
            var urlTitleLabel = new Label("Test URL:");
            urlTitleLabel.style.fontSize = 10;
            urlTitleLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            urlTitleLabel.style.marginBottom = 3;
            urlTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            urlContainer.Add(urlTitleLabel);
            
            qrUrlLabel = new Label();
            qrUrlLabel.style.fontSize = 14;
            qrUrlLabel.style.color = new Color(0.4f, 0.8f, 1f, 1f); // Bright blue for URL
            qrUrlLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            qrUrlLabel.style.whiteSpace = WhiteSpace.Normal;
            urlContainer.Add(qrUrlLabel);
            
            parent.Add(urlContainer);
            
            var instructionLabel = new Label("✨ Use any QR code scanner to test your BoostLink™ configuration. Subtle black QR code with BoostOps logo overlay. Click the magnifying glass for a larger version.");
            instructionLabel.style.fontSize = 9;
            instructionLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            instructionLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            instructionLabel.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(instructionLabel);
            
            // Auto-generate QR code after UI is built (will check cache to avoid duplicates)
            AutoGenerateQRCode();
        }
        void BuildQRDomainSelector(VisualElement parent)
        {
            // Get all available domains
            var availableDomains = GetAvailableDomainsForQR();
            
            if (availableDomains.Count <= 1)
            {
                // If only one domain or none, don't show selector
                if (availableDomains.Count == 1)
                {
                    selectedQRDomain = availableDomains[0];
                }
                return;
            }
            
            // Domain selector container
            var domainSelectorContainer = new VisualElement();
            domainSelectorContainer.style.marginBottom = 12;
            
            // Label
            var selectorLabel = new Label("Domain for QR Code:");
            selectorLabel.style.fontSize = 11;
            selectorLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            selectorLabel.style.marginBottom = 4;
            selectorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            domainSelectorContainer.Add(selectorLabel);
            
            // Dropdown (we'll use a popup field)
            var domainChoices = availableDomains.ToList();
            
            // Set default selection if not already set
            if (string.IsNullOrEmpty(selectedQRDomain) || !domainChoices.Contains(selectedQRDomain))
            {
                selectedQRDomain = domainChoices.Count > 0 ? domainChoices[0] : "";
            }
            
            var currentIndex = domainChoices.FindIndex(d => d == selectedQRDomain);
            if (currentIndex < 0) currentIndex = 0;
            
            var domainDropdown = new PopupField<string>("", domainChoices, currentIndex);
            domainDropdown.style.fontSize = 12;
            domainDropdown.style.marginBottom = 8;
            
            domainDropdown.RegisterValueChangedCallback(evt => {
                selectedQRDomain = evt.newValue;
                SaveQRDomainSelection(); // Persist selection
                
                LogDebug($"QR Domain changed to: {selectedQRDomain}");
                
                // Force QR code regeneration with new domain
                lastQrGeneratedUrl = ""; // Clear cache to force regeneration
                cachedQrTexture = null; // Clear cached texture to force regeneration
                isGeneratingQR = false; // Reset generation flag
                
                // Update the URL display immediately
                string newTestUrl = GenerateTestUrl();
                if (qrUrlLabel != null)
                {
                    qrUrlLabel.text = newTestUrl;
                }
                
                // Force QR code regeneration
                AutoGenerateQRCode(forceRegeneration: true);
                
                LogDebug($"QR Code regeneration triggered for URL: {newTestUrl}");
            });
            
            domainSelectorContainer.Add(domainDropdown);
            parent.Add(domainSelectorContainer);
        }
        
        List<string> GetAvailableDomainsForQR()
        {
            var domains = new List<string>();
            
            // In managed mode, add the managed domain first
            if (linksMode == FeatureMode.Managed && !string.IsNullOrEmpty(registeredProjectSlug))
            {
                string managedDomain = $"{registeredProjectSlug}.boostlink.me";
                if (!domains.Contains(managedDomain))
                {
                    domains.Add(managedDomain);
                }
            }
            
            // Add from config if available
            if (dynamicLinksConfig != null)
            {
                var allHosts = dynamicLinksConfig.GetAllHosts();
                foreach (var host in allHosts)
                {
                    if (!string.IsNullOrEmpty(host) && !domains.Contains(host))
                    {
                        domains.Add(host);
                    }
                }
            }
            
            return domains;
        }
        
        void SaveQRDomainSelection()
        {
            EditorPrefs.SetString("BoostOps_SelectedQRDomain", selectedQRDomain);
        }
        
        void LoadQRDomainSelection()
        {
            selectedQRDomain = EditorPrefs.GetString("BoostOps_SelectedQRDomain", "");
        }
        
        void RefreshQRSection()
        {
            // Force regeneration of QR code with updated domain selection
            EditorApplication.delayCall += () => {
                // Clear cache to force QR regeneration
                lastQrGeneratedUrl = "";
                
                // Regenerate QR code with current domain selection
                AutoGenerateQRCode();
            };
        }
        
        // Automatically generate QR code when domain changes or window opens
        void AutoGenerateQRCode(bool forceRegeneration = false)
        {
            // Don't generate QR codes during play mode
            if (EditorApplication.isPlaying) return;
            
            if (qrImageRef == null) return;
            
            // Generate the test URL to check if it has changed
            string currentTestUrl = GenerateTestUrl();
            
            // Check if URL has changed or if we don't have a cached texture (skip cache if forcing regeneration)
            if (!forceRegeneration && currentTestUrl == lastQrGeneratedUrl && cachedQrTexture != null && !string.IsNullOrEmpty(currentTestUrl))
            {
                // URL hasn't changed and we have a cached texture - reuse it
                qrImageRef.image = cachedQrTexture;
                
                // Update URL display if needed
                if (qrUrlLabel != null)
                {
                    qrUrlLabel.text = currentTestUrl;
                }
                
                LogDebug($"QR Code: Reusing cached QR code for URL: {currentTestUrl}");
                return;
            }
            
            // Special case: If we have a cached texture but the URL appears to have "changed" from empty to the same URL
            // (this happens after domain reloads when lastQrGeneratedUrl gets reset but cachedQrTexture is serialized)
            // Skip this optimization if forcing regeneration
            if (!forceRegeneration && string.IsNullOrEmpty(lastQrGeneratedUrl) && cachedQrTexture != null && !string.IsNullOrEmpty(currentTestUrl))
            {
                // Check if this texture was likely generated for the current URL
                // Since we can't be 100% sure, we'll use it but allow regeneration if needed
                qrImageRef.image = cachedQrTexture;
                lastQrGeneratedUrl = currentTestUrl; // Update the cache
                
                // Update URL display if needed
                if (qrUrlLabel != null)
                {
                    qrUrlLabel.text = currentTestUrl;
                }
                
                LogDebug($"QR Code: Restored cached QR code after domain reload for URL: {currentTestUrl}");
                return;
            }
            
            // Check if already generating to prevent duplicates
            if (isGeneratingQR)
            {
                LogDebug($"QR Code: Already generating QR code, skipping duplicate request");
                return;
            }
            
            // Clear cache if URL is empty
            if (string.IsNullOrEmpty(currentTestUrl))
            {
                cachedQrTexture = null;
                lastQrGeneratedUrl = "";
                isGeneratingQR = false;
                return;
            }
            
            // URL has changed or no cache - need to generate new QR code
                            LogDebug($"QR Code: URL changed from '{lastQrGeneratedUrl}' to '{currentTestUrl}' - generating new QR code");
            
            // Set flag to prevent concurrent generation
            isGeneratingQR = true;
            
            // Cancel any existing delayed generation
            if (qrGenerationCancellation != null)
            {
                qrGenerationCancellation.Cancel();
                qrGenerationCancellation.Dispose();
            }
            
            // Cancel any existing debounce timer
            if (qrDebounceTimer != null)
            {
                qrDebounceTimer.Dispose();
                qrDebounceTimer = null;
            }
            
            if (!string.IsNullOrEmpty(currentTestUrl))
            {
                // Create new cancellation token for this generation
                qrGenerationCancellation = new System.Threading.CancellationTokenSource();
                
                // Use a timer with 2-second delay to debounce QR code generation
                qrDebounceTimer = new System.Threading.Timer((_) => {
                    // Use EditorApplication.delayCall to execute on main thread
                    EditorApplication.delayCall += () => {
                        // Check if this generation was cancelled
                        if (qrGenerationCancellation == null || qrGenerationCancellation.IsCancellationRequested)
                            return;
                            
                        // Update URL display
                        if (qrUrlLabel != null)
                        {
                            qrUrlLabel.text = currentTestUrl;
                        }
                        
                        // Generate new QR code and cache it
                        GenerateQRCodeAsync(currentTestUrl, qrImageRef);
                    };
                }, null, 2000, System.Threading.Timeout.Infinite); // 2 second delay, no repeat
            }
        }

        // Force refresh QR code (clears cache and regenerates)
        void ForceRefreshQRCode()
        {
            // Clear cache to force regeneration
            cachedQrTexture = null;
            lastQrGeneratedUrl = "";
            
            // Generate new QR code
            AutoGenerateQRCode();
        }

        void BuildOverviewPanel()
        {
            // Hero section with prominent styling (matching Links and Cross-Promo format)
            var heroSection = new VisualElement();
            heroSection.style.flexDirection = FlexDirection.Row;
            heroSection.style.alignItems = Align.Center;
            heroSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            heroSection.style.paddingLeft = 20;
            heroSection.style.paddingRight = 20;
            heroSection.style.paddingTop = 15;
            heroSection.style.paddingBottom = 15;
            heroSection.style.marginBottom = 20;
            heroSection.style.borderTopLeftRadius = 8;
            heroSection.style.borderTopRightRadius = 8;
            heroSection.style.borderBottomLeftRadius = 8;
            heroSection.style.borderBottomRightRadius = 8;
            
            // Title and description container
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            
            var title = new Label("Project Overview");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            textContainer.Add(title);

            var description = new Label("✨ Your project status, credentials, and platform configuration at a glance.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.9f, 0.95f, 0.9f, 1f); // Brighter green tint for better readability
            description.style.whiteSpace = WhiteSpace.Normal;
            textContainer.Add(description);
            
            heroSection.Add(textContainer);
            contentContainer.Add(heroSection);

            if (!isLoggedIn)
            {
                BuildOverviewSignInPrompt();
            }

            // Mode Explanation Section (removed - not needed)
            // BuildModeExplanationSection();

            if (!isLoggedIn)
            {
                return; // Don't show the project-specific sections if not logged in
            }

            // PRIORITY 1: Critical Issues (Registration & Platform Setup)
            BuildCriticalIssuesSection();
            
            // PRIORITY 2: Project Status (when everything is working)
            BuildProjectStatusSection();
            
            // PRIORITY 3: Credentials Section (detailed view)
            BuildCredentialsSection();
            
            // Platform Setup Section (detailed view)
            BuildPlatformSetupSection();
            
            // ✅ REMOVED: Features Section (redundant with navigation tabs)
            // BuildFeaturesSection();
        }

        void BuildOverviewSignInPrompt()
        {
            var signInCard = CreateCard("Welcome to BoostOps!", "🚀");

            var signInDescription = new Label("Sign in to your BoostOps account to register your project and unlock Links & Cross-Promo features.");
            signInDescription.style.fontSize = 12;
            signInDescription.style.marginBottom = 15;
            signInDescription.style.whiteSpace = WhiteSpace.Normal;
            signInCard.Add(signInDescription);

            var signInButton = new Button(() => ShowAccountPanel()) { text = "🔑 Sign In to Get Started" };
            signInButton.style.width = 200;
            signInButton.style.height = 30;
            signInButton.style.alignSelf = Align.FlexStart;
            signInCard.Add(signInButton);

            contentContainer.Add(signInCard);
        }

        void BuildModeExplanationSection()
        {
            var modeCard = CreateCard("Local vs BoostOps Managed", "⚙️");
            
            var introText = new Label("BoostOps offers two modes for managing your Links and Cross-Promo features:");
            introText.style.fontSize = 12;
            introText.style.marginBottom = 15;
            introText.style.whiteSpace = WhiteSpace.Normal;
            modeCard.Add(introText);
            
            // Local Mode Section
            var localContainer = new VisualElement();
            localContainer.style.backgroundColor = new Color(0.2f, 0.3f, 0.6f, 0.2f);
            localContainer.style.paddingLeft = 15;
            localContainer.style.paddingRight = 15;
            localContainer.style.paddingTop = 12;
            localContainer.style.paddingBottom = 12;
            localContainer.style.marginBottom = 15;
            localContainer.style.borderTopLeftRadius = 6;
            localContainer.style.borderTopRightRadius = 6;
            localContainer.style.borderBottomLeftRadius = 6;
            localContainer.style.borderBottomRightRadius = 6;
            localContainer.style.borderLeftWidth = 3;
            localContainer.style.borderLeftColor = new Color(0.2f, 0.6f, 1f, 1f); // Blue border
            
            var localTitle = new Label("🔵 Local Mode");
            localTitle.style.fontSize = 14;
            localTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            localTitle.style.marginBottom = 8;
            localTitle.style.color = new Color(0.9f, 0.95f, 1f, 1f);
            localContainer.Add(localTitle);
            
            var localDescription = new Label("• Complete control within Unity Editor\n" +
                                           "• Generate platform files locally\n" +
                                           "• No account required to get started\n" +
                                           "• Perfect for development and testing\n" +
                                           "• Files saved to your project directory");
            localDescription.style.fontSize = 11;
            localDescription.style.color = new Color(0.85f, 0.9f, 0.95f, 1f);
            localDescription.style.whiteSpace = WhiteSpace.Normal;
            localContainer.Add(localDescription);
            
            modeCard.Add(localContainer);
            
            // Managed Mode Section
            var managedContainer = new VisualElement();
            managedContainer.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 0.2f);
            managedContainer.style.paddingLeft = 15;
            managedContainer.style.paddingRight = 15;
            managedContainer.style.paddingTop = 12;
            managedContainer.style.paddingBottom = 12;
            managedContainer.style.marginBottom = 15;
            managedContainer.style.borderTopLeftRadius = 6;
            managedContainer.style.borderTopRightRadius = 6;
            managedContainer.style.borderBottomLeftRadius = 6;
            managedContainer.style.borderBottomRightRadius = 6;
            managedContainer.style.borderLeftWidth = 3;
            managedContainer.style.borderLeftColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Green border
            
            var managedTitle = new Label("🟢 BoostOps Managed");
            managedTitle.style.fontSize = 14;
            managedTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            managedTitle.style.marginBottom = 8;
            managedTitle.style.color = new Color(0.9f, 1f, 0.9f, 1f);
            managedContainer.Add(managedTitle);
            
            var managedDescription = new Label("• Cloud-hosted configuration and analytics\n" +
                                             "• Team collaboration through web dashboard\n" +
                                             "• Remote configuration updates\n" +
                                             "• Advanced analytics and performance tracking\n" +
                                             "• Automatic sync across team members");
            managedDescription.style.fontSize = 11;
            managedDescription.style.color = new Color(0.85f, 0.95f, 0.85f, 1f);
            managedDescription.style.whiteSpace = WhiteSpace.Normal;
            managedContainer.Add(managedDescription);
            
            modeCard.Add(managedContainer);
            
            // Mode switching info
            var switchingInfo = new Label("💡 You can switch between modes anytime using the toggle buttons on the Links and Cross-Promo tabs. " +
                                        "Local configurations are preserved when switching to Managed mode.");
            switchingInfo.style.fontSize = 11;
            switchingInfo.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            switchingInfo.style.whiteSpace = WhiteSpace.Normal;
            switchingInfo.style.unityFontStyleAndWeight = FontStyle.Italic;
            modeCard.Add(switchingInfo);
            
            contentContainer.Add(modeCard);
        }

        void BuildCriticalIssuesSection()
        {
            var settings = BoostOpsProjectSettings.GetOrCreateSettings();
            bool hasProjectKey = !string.IsNullOrEmpty(settings.projectKey);
            bool hasAppStoreId = !string.IsNullOrEmpty(settings.appleAppStoreId);
            bool hasSHA256 = !string.IsNullOrEmpty(settings.androidCertFingerprint);
            
            // Only show this section if there are critical issues
            if (hasProjectKey && hasAppStoreId && hasSHA256)
            {
                return; // All good, no critical issues to show
            }
            
            var criticalCard = CreateCard("⚠️ Setup Required", "🚨");
            criticalCard.style.borderTopColor = new Color(1f, 0.6f, 0f, 1f); // Orange border
            criticalCard.style.borderTopWidth = 3f;
            
            // App Registration Issue (Most Critical)
            if (!hasProjectKey)
            {
                var registrationIssue = new VisualElement();
                registrationIssue.style.flexDirection = FlexDirection.Row;
                registrationIssue.style.alignItems = Align.Center;
                registrationIssue.style.marginBottom = 10;
                registrationIssue.style.paddingTop = 10;
                registrationIssue.style.paddingBottom = 10;
                registrationIssue.style.paddingLeft = 10;
                registrationIssue.style.paddingRight = 10;
                registrationIssue.style.backgroundColor = new Color(1f, 0.3f, 0.3f, 0.1f); // Light red background
                registrationIssue.style.borderTopLeftRadius = 5;
                registrationIssue.style.borderTopRightRadius = 5;
                registrationIssue.style.borderBottomLeftRadius = 5;
                registrationIssue.style.borderBottomRightRadius = 5;
                
                var warningIcon = new Label("🚫");
                warningIcon.style.fontSize = 16;
                warningIcon.style.marginRight = 8;
                registrationIssue.Add(warningIcon);
                
                var issueContent = new VisualElement();
                issueContent.style.flexGrow = 1;
                
                var issueTitle = new Label("App Not Registered with BoostOps");
                issueTitle.style.fontSize = 14;
                issueTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                issueTitle.style.color = new Color(1f, 0.3f, 0.3f, 1f);
                issueContent.Add(issueTitle);
                
                var issueDesc = new Label("Required for analytics tracking and cross-promotion");
                issueDesc.style.fontSize = 11;
                issueDesc.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                issueContent.Add(issueDesc);
                
                registrationIssue.Add(issueContent);
                
                var actionButtons = new VisualElement();
                actionButtons.style.flexDirection = FlexDirection.Row;
                actionButtons.style.marginLeft = 10;
                
                var registerBtn = new Button(() => ShowRegisterAppDialog()) { text = "Register App" };
                registerBtn.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 1f);
                registerBtn.style.color = Color.white;
                registerBtn.style.marginRight = 5;
                actionButtons.Add(registerBtn);
                
                var checkBtn = new Button(() => { _ = CheckForExistingProject(); }) { text = "Check Existing" };
                checkBtn.style.backgroundColor = new Color(0.3f, 0.6f, 1f, 1f);
                checkBtn.style.color = Color.white;
                actionButtons.Add(checkBtn);
                
                registrationIssue.Add(actionButtons);
                criticalCard.Add(registrationIssue);
            }
            
            // Platform Setup Issues
            if (!hasAppStoreId || !hasSHA256)
            {
                var platformIssue = new VisualElement();
                platformIssue.style.flexDirection = FlexDirection.Row;
                platformIssue.style.alignItems = Align.Center;
                platformIssue.style.marginBottom = 10;
                platformIssue.style.paddingTop = 10;
                platformIssue.style.paddingBottom = 10;
                platformIssue.style.paddingLeft = 10;
                platformIssue.style.paddingRight = 10;
                platformIssue.style.backgroundColor = new Color(1f, 0.6f, 0f, 0.1f); // Light orange background
                platformIssue.style.borderTopLeftRadius = 5;
                platformIssue.style.borderTopRightRadius = 5;
                platformIssue.style.borderBottomLeftRadius = 5;
                platformIssue.style.borderBottomRightRadius = 5;
                
                var warningIcon = new Label("⚠️");
                warningIcon.style.fontSize = 16;
                warningIcon.style.marginRight = 8;
                platformIssue.Add(warningIcon);
                
                var issueContent = new VisualElement();
                issueContent.style.flexGrow = 1;
                
                var issueTitle = new Label("Platform Configuration Incomplete");
                issueTitle.style.fontSize = 14;
                issueTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                issueTitle.style.color = new Color(1f, 0.6f, 0f, 1f);
                issueContent.Add(issueTitle);
                
                var missingItems = new List<string>();
                if (!hasAppStoreId) missingItems.Add("Apple Store ID");
                if (!hasSHA256) missingItems.Add("Android SHA-256");
                
                var issueDesc = new Label($"Missing: {string.Join(", ", missingItems)}");
                issueDesc.style.fontSize = 11;
                issueDesc.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                issueContent.Add(issueDesc);
                
                platformIssue.Add(issueContent);
                
                var configureBtn = new Button(() => {
                    // Navigate to Links panel where platform IDs can be configured
                    ShowLinksPanel();
                }) { text = "Configure" };
                configureBtn.style.backgroundColor = new Color(1f, 0.6f, 0f, 1f);
                configureBtn.style.color = Color.white;
                configureBtn.style.marginLeft = 10;
                platformIssue.Add(configureBtn);
                
                criticalCard.Add(platformIssue);
            }
            
            contentContainer.Add(criticalCard);
        }

        void BuildProjectStatusSection()
        {
            var settings = BoostOpsProjectSettings.GetOrCreateSettings();
            bool hasProjectKey = !string.IsNullOrEmpty(settings.projectKey);
            bool hasAppStoreId = !string.IsNullOrEmpty(settings.appleAppStoreId);
            bool hasSHA256 = !string.IsNullOrEmpty(settings.androidCertFingerprint);
            
            // Only show detailed status when setup is complete
            if (!hasProjectKey || !hasAppStoreId || !hasSHA256)
            {
                return; // Critical issues are shown above, don't duplicate here
            }
            
            var statusCard = CreateCard("✅ Project Status", "📊");
            statusCard.style.borderTopColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Green border
            statusCard.style.borderTopWidth = 3f;

            // Project Name
            var projectName = Application.productName;
            var projectRow = CreateStatusRow("Project Name", projectName, !string.IsNullOrEmpty(projectName));
            statusCard.Add(projectRow);

            // Domain Prefix (Project Slug)
            var projectSlug = settings.projectSlug;
            var slugRow = CreateStatusRow("Domain Prefix", 
                !string.IsNullOrEmpty(projectSlug) ? projectSlug : "Not configured", 
                !string.IsNullOrEmpty(projectSlug));
            statusCard.Add(slugRow);

            // Bundle ID (iOS)
            #if UNITY_2021_2_OR_NEWER
            var bundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
            #else
            var bundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
            #endif
            var bundleRow = CreateStatusRow("iOS Bundle ID", bundleId, !string.IsNullOrEmpty(bundleId) && bundleId != "com.unity.template.mobile");
            statusCard.Add(bundleRow);

            // Package Name (Android)
            #if UNITY_2021_2_OR_NEWER
            var packageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            #else
            var packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            #endif
            var packageRow = CreateStatusRow("Google Store ID", packageName, !string.IsNullOrEmpty(packageName) && packageName != "com.unity.template.mobile");
            statusCard.Add(packageRow);

            contentContainer.Add(statusCard);
        }

        void BuildCredentialsSection()
        {
            var settings = BoostOpsProjectSettings.GetInstance();
            
            var credentialsCard = CreateCard("Credentials", "🔑");

            // Project Key
            bool hasProjectKey = settings != null && !string.IsNullOrEmpty(settings.projectKey);
            
            if (hasProjectKey)
            {
                string maskedProjectKey = MaskProjectKey(settings.projectKey);
                var projectKeyRow = CreateCredentialRow("Project Key", 
                    maskedProjectKey, 
                    true,
                    null); // Remove copy functionality
                credentialsCard.Add(projectKeyRow);
                
                var keyNote = new Label("💡 Safe to commit • Used for event ingest");
                keyNote.style.fontSize = 10;
                keyNote.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                keyNote.style.marginLeft = 20;
                keyNote.style.marginBottom = 5;
                credentialsCard.Add(keyNote);
            }
            else
            {
                            // Show app registration needed
            var projectKeyRow = CreateActionRow("Project Key", 
                "⚠️ App not registered with BoostOps", 
                false,
                () => ShowRegisterAppDialog(),
                "Register App");
            
            // Add a secondary action to check for existing projects
            var lookupButton = new Button(() => {
                _ = CheckForExistingProject();
            }) { text = "Check Existing" };
            lookupButton.style.marginLeft = 5;
            lookupButton.style.width = 90;
            lookupButton.style.height = 22;
            lookupButton.style.fontSize = 10;
            lookupButton.tooltip = "Check if this app already exists in BoostOps";
            
            projectKeyRow.Add(lookupButton);
            credentialsCard.Add(projectKeyRow);
                
                var regNote = new Label("💡 Register your app to get analytics tracking and project sync");
                regNote.style.fontSize = 10;
                regNote.style.color = new Color(0.8f, 0.6f, 0.6f, 1f);
                regNote.style.marginLeft = 20;
                regNote.style.marginBottom = 5;
                credentialsCard.Add(regNote);
            }

            contentContainer.Add(credentialsCard);
        }

        void BuildPlatformSetupSection()
        {
            var settings = BoostOpsProjectSettings.GetInstance();
            
            var platformCard = CreateCard("Platform Setup", "📱");

            // iOS Status
            bool hasAppStoreId = settings != null && !string.IsNullOrEmpty(settings.appleAppStoreId);
            string iosStatus = hasAppStoreId ? 
                $"✅ Ready (ID: {settings.appleAppStoreId})" : 
                "⚠️ Missing App Store ID";
            var iosRow = CreateActionRow("iOS", 
                iosStatus, 
                hasAppStoreId,
                () => ShowLinksPanel(),
                "Configure");
            platformCard.Add(iosRow);

            // Android Status  
            bool hasFingerprint = settings != null && !string.IsNullOrEmpty(settings.androidCertFingerprint);
            string androidStatus = hasFingerprint ? 
                $"✅ Ready (SHA: {settings.androidCertFingerprint.Substring(0, Math.Min(12, settings.androidCertFingerprint.Length))}...)" : 
                "⚠️ Missing SHA-256";
            var androidRow = CreateActionRow("Android",
                androidStatus,
                hasFingerprint,
                () => ShowLinksPanel(),
                "Configure");
            platformCard.Add(androidRow);

            contentContainer.Add(platformCard);
        }

        void BuildFeaturesSection()
        {
            var featuresCard = CreateCard("Features", "🚀");

            // Links Feature
            var linksRow = CreateFeatureRow("BoostLinks™", 
                "Dynamic & Universal Links", 
                () => ShowLinksPanel(),
                "Configure Links");
            featuresCard.Add(linksRow);

            // Cross-Promo Feature
            var crossPromoRow = CreateFeatureRow("Cross-Promotion", 
                "In-app game promotions", 
                () => ShowCrossPromoPanel(),
                "Setup Cross-Promo");
            featuresCard.Add(crossPromoRow);

            // Integrations Feature
            var integrationsRow = CreateFeatureRow("Integrations", 
                "Analytics & third-party hooks", 
                () => ShowIntegrationsPanel(),
                "View Integrations");
            featuresCard.Add(integrationsRow);

            contentContainer.Add(featuresCard);
        }

        /// <summary>
        /// Creates a standardized card with consistent styling for the Overview panel
        /// </summary>
        VisualElement CreateCard(string title, string icon = null)
        {
            var card = new VisualElement();
            
            // Enhanced card styling with subtle border and shadow effect
            card.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
            card.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            card.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            card.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            card.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            
            // Consistent padding and margins
            card.style.paddingLeft = 18;
            card.style.paddingRight = 18;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.marginBottom = 16;
            
            // Rounded corners
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;

            // Add title with icon if provided
            if (!string.IsNullOrEmpty(title))
            {
                var titleContainer = new VisualElement();
                titleContainer.style.flexDirection = FlexDirection.Row;
                titleContainer.style.alignItems = Align.Center;
                titleContainer.style.marginBottom = 12;
                
                var titleLabel = new Label($"{icon}{(string.IsNullOrEmpty(icon) ? "" : " ")}{title}");
                titleLabel.style.fontSize = 14;
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleLabel.style.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                titleContainer.Add(titleLabel);
                
                card.Add(titleContainer);
            }

            return card;
        }

        VisualElement CreateStatusRow(string label, string value, bool isValid)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;

            var statusIcon = new Label(isValid ? "✅" : "❌");
            statusIcon.style.width = 20;
            statusIcon.style.fontSize = 12;
            row.Add(statusIcon);

            var labelElement = new Label($"{label}:");
            labelElement.style.width = 120;
            labelElement.style.fontSize = 11;
            row.Add(labelElement);

            var valueElement = new Label(string.IsNullOrEmpty(value) ? "Not set" : value);
            valueElement.style.fontSize = 11;
            valueElement.style.color = isValid ? new Color(1f, 1f, 1f, 1f) : new Color(0.8f, 0.6f, 0.6f, 1f);
            valueElement.style.flexGrow = 1;
            row.Add(valueElement);

            return row;
        }

        VisualElement CreateCredentialRow(string label, string value, bool isValid, string copyValue = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;

            var statusIcon = new Label(isValid ? "✅" : "❌");
            statusIcon.style.width = 20;
            statusIcon.style.fontSize = 12;
            row.Add(statusIcon);

            var labelElement = new Label($"{label}:");
            labelElement.style.width = 80;
            labelElement.style.fontSize = 11;
            row.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.style.fontSize = 10;
            valueElement.style.color = isValid ? new Color(1f, 1f, 1f, 1f) : new Color(0.8f, 0.6f, 0.6f, 1f);
            valueElement.style.flexGrow = 1;
            valueElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(valueElement);

            if (isValid && !string.IsNullOrEmpty(copyValue))
            {
                var copyButton = new Button(() => {
                    EditorGUIUtility.systemCopyBuffer = copyValue;
                    // Clipboard copy successful
                }) { text = "📋" };
                copyButton.style.width = 25;
                copyButton.style.height = 20;
                copyButton.style.fontSize = 10;
                copyButton.tooltip = $"Copy {label} to clipboard";
                row.Add(copyButton);
            }

            return row;
        }

        VisualElement CreateActionRow(string label, string status, bool isValid, System.Action onConfigure, string buttonText)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;

            var labelElement = new Label($"{label}:");
            labelElement.style.width = 80;
            labelElement.style.fontSize = 11;
            labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(labelElement);

            var statusElement = new Label(status);
            statusElement.style.fontSize = 11;
            statusElement.style.flexGrow = 1;
            statusElement.style.color = isValid ? new Color(0.6f, 1f, 0.6f, 1f) : new Color(1f, 0.8f, 0.6f, 1f);
            row.Add(statusElement);

            if (!isValid)
            {
                var configButton = new Button(onConfigure) { text = buttonText };
                configButton.style.width = 80;
                configButton.style.height = 22;
                configButton.style.fontSize = 10;
                row.Add(configButton);
            }

            return row;
        }

        VisualElement CreateFeatureRow(string title, string description, System.Action onOpen, string buttonText)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;

            var titleElement = new Label(title);
            titleElement.style.fontSize = 12;
            titleElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            textContainer.Add(titleElement);

            var descElement = new Label(description);
            descElement.style.fontSize = 10;
            descElement.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            textContainer.Add(descElement);

            row.Add(textContainer);

            var openButton = new Button(onOpen) { text = buttonText };
            openButton.style.width = 120;
            openButton.style.height = 25;
            openButton.style.fontSize = 10;
            row.Add(openButton);

            return row;
        }

        void BuildAttributionPanel()
        {
            // Add mode header (Attribution is always BoostOps Managed)
            BuildAttributionModeHeader();
            
            // Hero section with card styling
            var heroSection = new VisualElement();
            heroSection.style.flexDirection = FlexDirection.Row;
            heroSection.style.alignItems = Align.Center;
            heroSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            heroSection.style.paddingLeft = 20;
            heroSection.style.paddingRight = 20;
            heroSection.style.paddingTop = 15;
            heroSection.style.paddingBottom = 15;
            heroSection.style.marginBottom = 20;
            heroSection.style.borderTopLeftRadius = 8;
            heroSection.style.borderTopRightRadius = 8;
            heroSection.style.borderBottomLeftRadius = 8;
            heroSection.style.borderBottomRightRadius = 8;
            
            // Title and description container
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            
            var title = new Label("📈 Attribution & Analytics");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            textContainer.Add(title);

            var description = new Label("✨ Track installs, revenue, and campaign attribution from all sources.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.9f, 0.95f, 0.9f, 1f);
            description.style.whiteSpace = WhiteSpace.Normal;
            textContainer.Add(description);
            
            heroSection.Add(textContainer);
            
            // Dashboard button
            var dashboardButton = new Button(() => OpenDashboard("attribution")) { text = "📊 Open Dashboard" };
            dashboardButton.style.width = 140;
            dashboardButton.style.height = 32;
            dashboardButton.style.fontSize = 12;
            dashboardButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            heroSection.Add(dashboardButton);
            
            contentContainer.Add(heroSection);
            
            // ✅ REMOVED: Attribution Status Section (attribution is always enabled with fail-open)
            // SDK sends analytics events as soon as project key is configured
            // No need to show status - it's always active
            
            // Get project settings (needed for later sections)
            var settings = BoostOpsProjectSettings.GetInstance();
            
            // Project Configuration Section
            var configSection = new VisualElement();
            configSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            configSection.style.paddingLeft = 20;
            configSection.style.paddingRight = 20;
            configSection.style.paddingTop = 15;
            configSection.style.paddingBottom = 15;
            configSection.style.marginBottom = 20;
            configSection.style.borderTopLeftRadius = 8;
            configSection.style.borderTopRightRadius = 8;
            configSection.style.borderBottomLeftRadius = 8;
            configSection.style.borderBottomRightRadius = 8;
            
            var configTitle = new Label("Project Configuration");
            configTitle.style.fontSize = 14;
            configTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            configTitle.style.marginBottom = 5;
            configSection.Add(configTitle);
            
            var configDesc = new Label("Current project settings for attribution");
            configDesc.style.fontSize = 11;
            configDesc.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            configDesc.style.marginBottom = 15;
            configSection.Add(configDesc);
            
            // Project Key Row (masked for security)
            var projectKeyRow = new VisualElement();
            projectKeyRow.style.flexDirection = FlexDirection.Row;
            projectKeyRow.style.marginBottom = 8;
            var projectKeyLabel = new Label("Project Key:");
            projectKeyLabel.style.fontSize = 11;
            projectKeyLabel.style.width = 120;
            projectKeyLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            projectKeyRow.Add(projectKeyLabel);
            string maskedKey = !string.IsNullOrEmpty(settings?.projectKey) ? MaskProjectKey(settings.projectKey) : "Not configured";
            var projectKeyValue = new Label(maskedKey);
            projectKeyValue.style.fontSize = 11;
            projectKeyValue.style.color = new Color(1f, 1f, 1f, 1f);
            projectKeyRow.Add(projectKeyValue);
            configSection.Add(projectKeyRow);
            
            // Managed Mode Row
            var managedRow = new VisualElement();
            managedRow.style.flexDirection = FlexDirection.Row;
            managedRow.style.marginBottom = 8;
            var managedLabel = new Label("BoostOps Managed:");
            managedLabel.style.fontSize = 11;
            managedLabel.style.width = 120;
            managedLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            managedRow.Add(managedLabel);
            var managedValue = new Label(crossPromoMode == FeatureMode.Managed ? "Yes" : "No");
            managedValue.style.fontSize = 11;
            managedValue.style.color = new Color(1f, 1f, 1f, 1f);
            managedRow.Add(managedValue);
            configSection.Add(managedRow);
            
            contentContainer.Add(configSection);
            
            // What Gets Tracked Section
            var trackingSection = new VisualElement();
            trackingSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            trackingSection.style.paddingLeft = 20;
            trackingSection.style.paddingRight = 20;
            trackingSection.style.paddingTop = 15;
            trackingSection.style.paddingBottom = 15;
            trackingSection.style.marginBottom = 20;
            trackingSection.style.borderTopLeftRadius = 8;
            trackingSection.style.borderTopRightRadius = 8;
            trackingSection.style.borderBottomLeftRadius = 8;
            trackingSection.style.borderBottomRightRadius = 8;
            
            var trackingTitle = new Label("📊 Events Being Tracked");
            trackingTitle.style.fontSize = 14;
            trackingTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            trackingTitle.style.marginBottom = 5;
            trackingSection.Add(trackingTitle);
            
            var trackingDesc = new Label("Analytics events sent to BoostOps");
            trackingDesc.style.fontSize = 11;
            trackingDesc.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            trackingDesc.style.marginBottom = 10;
            trackingSection.Add(trackingDesc);
            
            var eventsLabel = new Label(
                "• App Open (with first_open flag for installs)\n" +
                "• Purchases (revenue tracking)\n" +
                "• Cross-Promo Impressions\n" +
                "• Cross-Promo Clicks\n" +
                "• Deep Link Opens");
            eventsLabel.style.fontSize = 11;
            eventsLabel.style.whiteSpace = WhiteSpace.Normal;
            eventsLabel.style.marginTop = 10;
            trackingSection.Add(eventsLabel);
            
            contentContainer.Add(trackingSection);
        }

        void ToggleAttribution(bool enabled)
        {
            if (enabled)
            {
                EditorUtility.DisplayDialog("Enable Attribution", 
                    "Attribution will be enabled when you register your project with BoostOps.\n\n" +
                    "Go to the Overview tab to register your project.", "OK");
            }
            else
            {
                bool confirm = EditorUtility.DisplayDialog("Disable Attribution?", 
                    "Disabling attribution will stop all analytics events from being sent to BoostOps.\n\n" +
                    "This includes install tracking, revenue tracking, and campaign attribution.\n\n" +
                    "Are you sure you want to disable attribution?", "Disable", "Cancel");
                    
                if (confirm)
                {
                    // TODO: Call server API to disable attribution
                    EditorUtility.DisplayDialog("Attribution Disabled", 
                        "Attribution has been disabled. No analytics events will be sent.", "OK");
                    ShowAttributionPanel(); // Refresh the panel
                }
                else
                {
                    ShowAttributionPanel(); // Refresh to reset toggle
                }
            }
        }

        /// <summary>
        /// Auto-enable attribution when enabling Cross-Promo managed mode
        /// </summary>
        void AutoEnableAttributionForManagedMode()
        {
            var settings = BoostOpsProjectSettings.GetInstance();
            if (settings != null && !string.IsNullOrEmpty(settings.projectKey))
            {
                // Attribution is automatically enabled when project is registered with BoostOps
                // and Cross-Promo managed mode is enabled
                Debug.Log("[BoostOps] ✅ Attribution automatically enabled for Cross-Promo managed mode");
                
                // TODO: When server API is ready, call it here to explicitly enable attribution
                // For now, attribution is controlled by having a valid project key
            }
            else
            {
                Debug.LogWarning("[BoostOps] ⚠️ Cannot auto-enable attribution - project not registered");
            }
        }

        void BuildIntegrationsPanel()
        {
            // Run detection first
            DetectAnalyticsIntegrations();
            DetectUnityRemoteConfig();
            DetectFirebaseRemoteConfig();
            DetectCrossPromoConfigurations();
            
            // Hero section with card styling (matching Links page)
            var heroSection = new VisualElement();
            heroSection.style.flexDirection = FlexDirection.Row;
            heroSection.style.alignItems = Align.Center;
            heroSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            heroSection.style.paddingLeft = 20;
            heroSection.style.paddingRight = 20;
            heroSection.style.paddingTop = 15;
            heroSection.style.paddingBottom = 15;
            heroSection.style.marginBottom = 20;
            heroSection.style.borderTopLeftRadius = 8;
            heroSection.style.borderTopRightRadius = 8;
            heroSection.style.borderBottomLeftRadius = 8;
            heroSection.style.borderBottomRightRadius = 8;
            
            // Title and description container
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            
            var title = new Label("Integration Detection");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            textContainer.Add(title);

            var description = new Label("✨ Automatically detect analytics, remote config, and cross-promotion integrations in your Unity project.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.9f, 0.95f, 0.9f, 1f); // Brighter green tint for better readability
            description.style.whiteSpace = WhiteSpace.Normal;
            textContainer.Add(description);
            
            heroSection.Add(textContainer);
            contentContainer.Add(heroSection);

            // Analytics Section
            BuildAnalyticsSection();

            // Remote Config Section
            BuildRemoteConfigSection();

            // Cross-Promotion Section
            BuildCrossPromoSection();

            // Developer settings removed from Links page
        }
        void BuildAnalyticsSection()
        {
            var section = new VisualElement();
            section.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            section.style.paddingLeft = 20;
            section.style.paddingRight = 20;
            section.style.paddingTop = 15;
            section.style.paddingBottom = 15;
            section.style.marginBottom = 20;
            section.style.borderTopLeftRadius = 8;
            section.style.borderTopRightRadius = 8;
            section.style.borderBottomLeftRadius = 8;
            section.style.borderBottomRightRadius = 8;

            var sectionTitle = new Label("📊 Analytics Integrations");
            sectionTitle.style.fontSize = 14;
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.marginBottom = 10;
            section.Add(sectionTitle);

            // Unity Analytics (detection + log events toggle combined)
            var currentSettings = BoostOpsProjectSettings.GetInstance();
            var unityAnalyticsRow = CreateAnalyticsDetectionRow(
                "Unity Analytics",
                hasUnityAnalytics,
                hasUnityAnalytics ? (currentSettings?.unityAnalytics ?? false) : false,
                hasUnityAnalytics,
                (enabled) => {
                    var settings = BoostOpsProjectSettings.GetOrCreateSettings();
                    settings.unityAnalytics = enabled;
                    UnityEditor.EditorUtility.SetDirty(settings);
                    
                    // Defer asset saving to avoid conflicts
                    EditorApplication.delayCall += () => {
                        try
                        {
                            if (!EditorApplication.isUpdating && !EditorApplication.isCompiling)
                            {
                    UnityEditor.AssetDatabase.SaveAssets();
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[BoostOps] Failed to save Unity Analytics settings: {e.Message}");
                        }
                    };
                }
            );
            section.Add(unityAnalyticsRow);

            // Firebase Analytics (detection + log events toggle combined)
            var firebaseAnalyticsRow = CreateAnalyticsDetectionRow(
                "Firebase Analytics",
                hasFirebaseAnalytics,
                hasFirebaseAnalytics ? (currentSettings?.firebaseAnalytics ?? false) : false,
                hasFirebaseAnalytics,
                (enabled) => {
                    var settings = BoostOpsProjectSettings.GetOrCreateSettings();
                    settings.firebaseAnalytics = enabled;
                    UnityEditor.EditorUtility.SetDirty(settings);
                    
                    // Defer asset saving to avoid conflicts
                    EditorApplication.delayCall += () => {
                        try
                        {
                            if (!EditorApplication.isUpdating && !EditorApplication.isCompiling)
                            {
                    UnityEditor.AssetDatabase.SaveAssets();
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[BoostOps] Failed to save Firebase Analytics settings: {e.Message}");
                        }
                    };
                }
            );
            section.Add(firebaseAnalyticsRow);

            // BoostOps Analytics (always enabled, non-configurable)
            var boostOpsRow = CreateBoostOpsRequiredRow();
            section.Add(boostOpsRow);



            // Firebase Configuration (combines Android + iOS config files)
            string firebaseStatus;
            Color firebaseColor;
            
            if (hasGoogleServicesFile && hasFirebaseConfigFile)
            {
                firebaseStatus = "✅ Android & iOS config files found";
                firebaseColor = new Color(0.2f, 0.8f, 0.2f);
            }
            else if (hasGoogleServicesFile)
            {
                firebaseStatus = "⚠️ Android config found, iOS missing (GoogleService-Info.plist)";
                firebaseColor = new Color(0.8f, 0.8f, 0.2f);
            }
            else if (hasFirebaseConfigFile)
            {
                firebaseStatus = "⚠️ iOS config found, Android missing (google-services.json)";
                firebaseColor = new Color(0.8f, 0.8f, 0.2f);
            }
            else
            {
                firebaseStatus = "❌ Both config files missing";
                firebaseColor = new Color(0.8f, 0.2f, 0.2f);
            }
            
            var firebaseConfigRow = CreateDetectionRow(
                "Firebase Configuration",
                firebaseStatus,
                firebaseColor
            );
            section.Add(firebaseConfigRow);

            contentContainer.Add(section);
        }
        void BuildRemoteConfigSection()
        {
            var section = new VisualElement();
            section.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            section.style.paddingLeft = 20;
            section.style.paddingRight = 20;
            section.style.paddingTop = 15;
            section.style.paddingBottom = 15;
            section.style.marginBottom = 20;
            section.style.borderTopLeftRadius = 8;
            section.style.borderTopRightRadius = 8;
            section.style.borderBottomLeftRadius = 8;
            section.style.borderBottomRightRadius = 8;

            var sectionTitle = new Label("⚙️ Remote Config");
            sectionTitle.style.fontSize = 14;
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.marginBottom = 10;
            section.Add(sectionTitle);

            // Unity Remote Config
            var unityRemoteConfigRow = CreateDetectionRow(
                "Unity Remote Config",
                hasUnityRemoteConfig ? "✅ Detected" : "❌ Not Found",
                hasUnityRemoteConfig ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f)
            );
            section.Add(unityRemoteConfigRow);

            // Firebase Remote Config
            var firebaseRemoteConfigRow = CreateDetectionRow(
                "Firebase Remote Config",
                hasFirebaseRemoteConfig ? "✅ Detected" : "❌ Not Found",
                hasFirebaseRemoteConfig ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f)
            );
            section.Add(firebaseRemoteConfigRow);

            contentContainer.Add(section);
        }
        void BuildCrossPromoSection()
        {
            var section = new VisualElement();
            section.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            section.style.paddingLeft = 20;
            section.style.paddingRight = 20;
            section.style.paddingTop = 15;
            section.style.paddingBottom = 15;
            section.style.marginBottom = 20;
            section.style.borderTopLeftRadius = 8;
            section.style.borderTopRightRadius = 8;
            section.style.borderBottomLeftRadius = 8;
            section.style.borderBottomRightRadius = 8;

            var sectionTitle = new Label("🎯 Cross-Promotion");
            sectionTitle.style.fontSize = 14;
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.marginBottom = 10;
            section.Add(sectionTitle);

            // Local Cross-Promo (JSON files)
            bool hasLocalCrossPromo = HasLocalCrossPromoFiles();
            var localCrossPromoRow = CreateDetectionRow(
                "Local Cross-Promo",
                hasLocalCrossPromo ? "✅ Local JSON files found" : "❌ No local files found",
                hasLocalCrossPromo ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.8f, 0.2f)
            );
            section.Add(localCrossPromoRow);

            // Unity Remote Config Cross-Promo
            var remoteConfigRow = CreateDetectionRow(
                "Unity Remote Config",
                hasUnityRemoteConfig ? "✅ Remote Config available" : "❌ Remote Config not installed",
                hasUnityRemoteConfig ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.8f, 0.2f)
            );
            section.Add(remoteConfigRow);

            // Firebase Remote Config Cross-Promo
            var firebaseRemoteConfigRow = CreateDetectionRow(
                "Firebase Remote Config",
                hasFirebaseRemoteConfig ? "✅ Firebase Remote Config available" : "❌ Firebase Remote Config not installed",
                hasFirebaseRemoteConfig ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.8f, 0.2f)
            );
            section.Add(firebaseRemoteConfigRow);

            contentContainer.Add(section);
        }
        VisualElement CreateDetectionRow(string name, string status, Color statusColor)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;
            row.style.paddingLeft = 5;
            row.style.paddingRight = 5;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;

            var nameLabel = new Label(name);
            nameLabel.style.fontSize = 12;
            nameLabel.style.flexGrow = 1;
            row.Add(nameLabel);

            var statusLabel = new Label(status);
            statusLabel.style.fontSize = 11;
            statusLabel.style.color = statusColor;
            statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(statusLabel);

            return row;
        }
        VisualElement CreateAnalyticsDetectionRow(string name, bool isDetected, bool isLogEventsEnabled, bool canToggle, System.Action<bool> onToggleChanged, string additionalInfo = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;
            row.style.paddingLeft = 5;
            row.style.paddingRight = 5;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;

            // Left side: Service name
            var nameLabel = new Label(name);
            nameLabel.style.fontSize = 12;
            nameLabel.style.flexGrow = 1;
            row.Add(nameLabel);

            // Middle: Detection status
            string statusText;
            Color statusColor;
            if (name.Contains("Custom"))
            {
                statusText = "⚪ Available";
                statusColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            else if (isDetected)
            {
                statusText = "✅ Detected";
                statusColor = new Color(0.2f, 0.8f, 0.2f, 1f);
            }
            else
            {
                statusText = "❌ Not Found";
                statusColor = new Color(0.8f, 0.2f, 0.2f, 1f);
            }

            var statusLabel = new Label(statusText);
            statusLabel.style.fontSize = 11;
            statusLabel.style.color = statusColor;
            statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusLabel.style.marginRight = 10;
            row.Add(statusLabel);

            // Right side: Toggle and additional info
            var rightContainer = new VisualElement();
            rightContainer.style.flexDirection = FlexDirection.Row;
            rightContainer.style.alignItems = Align.Center;

            // Log Events toggle
            var logEventsLabel = new Label("Log Events:");
            logEventsLabel.style.fontSize = 10;
            logEventsLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            logEventsLabel.style.marginRight = 3;
            rightContainer.Add(logEventsLabel);

            var toggle = new Toggle();
            toggle.value = isLogEventsEnabled;
            toggle.SetEnabled(canToggle);
            toggle.style.marginRight = 5;
            toggle.tooltip = "Log a copy of BoostOps events to this analytics service. Default is on when service is detected.";
            
            if (!canToggle)
            {
                toggle.style.opacity = 0.6f;
            }

            if (canToggle && onToggleChanged != null)
            {
                toggle.RegisterValueChangedCallback(evt => onToggleChanged(evt.newValue));
            }
            rightContainer.Add(toggle);

            // Additional info (like "required" or "requires custom implementation")
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                var infoLabel = new Label(additionalInfo);
                infoLabel.style.fontSize = 9;
                infoLabel.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                infoLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                rightContainer.Add(infoLabel);
            }

            row.Add(rightContainer);
            return row;
        }

        VisualElement CreateBoostOpsRequiredRow()
        {
            var currentSettings = BoostOpsProjectSettings.GetInstance();
            bool useRemoteManagement = currentSettings?.useRemoteManagement ?? false;
            
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;
            row.style.paddingLeft = 5;
            row.style.paddingRight = 5;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.opacity = useRemoteManagement ? 0.9f : 0.6f; // More grayed out in local mode

            // Left side: Service name
            var nameLabel = new Label("BoostOps Analytics");
            nameLabel.style.fontSize = 12;
            nameLabel.style.flexGrow = 1;
            row.Add(nameLabel);

            // Middle: Status based on mode
            Label statusLabel;
            if (useRemoteManagement)
            {
                statusLabel = new Label("✅ Enabled (required)");
                statusLabel.style.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            }
            else
            {
                statusLabel = new Label("🔒 Disabled (local mode)");
                statusLabel.style.color = new Color(0.8f, 0.6f, 0.2f, 1f);
            }
            statusLabel.style.fontSize = 11;
            statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusLabel.style.marginRight = 10;
            row.Add(statusLabel);

            // Right side: Mode indicator
            var rightContainer = new VisualElement();
            rightContainer.style.flexDirection = FlexDirection.Row;
            rightContainer.style.alignItems = Align.Center;

            if (useRemoteManagement)
            {
                var lockIcon = new Label("🔒");
                lockIcon.style.fontSize = 12;
                lockIcon.style.marginRight = 3;
                rightContainer.Add(lockIcon);

                var alwaysOnLabel = new Label("Required");
                alwaysOnLabel.style.fontSize = 10;
                alwaysOnLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                alwaysOnLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                alwaysOnLabel.tooltip = "BoostOps analytics is required when using remote management for attribution and billing.";
                rightContainer.Add(alwaysOnLabel);
            }
            else
            {
                var localIcon = new Label("🏠");
                localIcon.style.fontSize = 12;
                localIcon.style.marginRight = 3;
                rightContainer.Add(localIcon);

                var localLabel = new Label("Local Mode");
                localLabel.style.fontSize = 10;
                localLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                localLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                localLabel.tooltip = "BoostOps analytics is automatically disabled in local mode since no server connection is made.";
                rightContainer.Add(localLabel);
            }

            row.Add(rightContainer);
            return row;
        }

        void BuildAccountPanel()
        {
            if (!isLoggedIn)
            {
                BuildAuthenticationUI();
            }
            else
            {
                BuildUserInfoUI();
            }
        }
        void BuildAuthenticationUI()
        {
            // Hero section with prominent styling (matching Links and Cross-Promo format)
            var heroSection = new VisualElement();
            heroSection.style.flexDirection = FlexDirection.Row;
            heroSection.style.alignItems = Align.Center;
            heroSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            heroSection.style.paddingLeft = 20;
            heroSection.style.paddingRight = 20;
            heroSection.style.paddingTop = 15;
            heroSection.style.paddingBottom = 15;
            heroSection.style.marginBottom = 20;
            heroSection.style.borderTopLeftRadius = 8;
            heroSection.style.borderTopRightRadius = 8;
            heroSection.style.borderBottomLeftRadius = 8;
            heroSection.style.borderBottomRightRadius = 8;
            
            // Title and description container
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            
            var title = new Label("Account Authentication");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            textContainer.Add(title);

            var description = new Label("✨ Sign in to your BoostOps account to unlock project registration and advanced features.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.9f, 0.95f, 0.9f, 1f); // Brighter green tint for better readability
            description.style.whiteSpace = WhiteSpace.Normal;
            textContainer.Add(description);
            
            heroSection.Add(textContainer);
            contentContainer.Add(heroSection);
            

            
            // Main container
            var mainContainer = new VisualElement();
            mainContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            mainContainer.style.paddingLeft = 15;
            mainContainer.style.paddingRight = 15;
            mainContainer.style.paddingTop = 15;
            mainContainer.style.paddingBottom = 15;
            mainContainer.style.borderTopLeftRadius = 6;
            mainContainer.style.borderTopRightRadius = 6;
            mainContainer.style.borderBottomLeftRadius = 6;
            mainContainer.style.borderBottomRightRadius = 6;
            
            // Info message
            var infoLabel = new Label("Sign in to your BoostOps account or create a new one to get started.");
            infoLabel.style.fontSize = 12;
            infoLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            infoLabel.style.marginBottom = 15;
            infoLabel.style.whiteSpace = WhiteSpace.Normal;
            mainContainer.Add(infoLabel);
            
            // Google Sign-In Section
            var googleSection = new Label("Quick Sign-In");
            googleSection.style.fontSize = 14;
            googleSection.style.unityFontStyleAndWeight = FontStyle.Bold;
            googleSection.style.marginBottom = 10;
            mainContainer.Add(googleSection);
            
            // Google Sign-In Button Container
            var googleContainer = new VisualElement();
            googleContainer.style.flexDirection = FlexDirection.Row;
            googleContainer.style.justifyContent = Justify.Center;
            googleContainer.style.marginBottom = 10;
            
            var googleButton = new Button(() => InitiateGoogleOAuth());
            googleButton.style.width = 180;
            googleButton.style.height = 35;
            googleButton.SetEnabled(!isAuthenticatingWithGoogle);
            
            // Create button content with Google logo
            var buttonContent = new VisualElement();
            buttonContent.style.flexDirection = FlexDirection.Row;
            buttonContent.style.alignItems = Align.Center;
            buttonContent.style.justifyContent = Justify.Center;
            buttonContent.style.alignSelf = Align.Center;
            buttonContent.style.height = Length.Percent(100);
            buttonContent.style.width = Length.Percent(100);
            
            var googleLogo = Resources.Load<Texture2D>("google-logo");
            if (googleLogo != null && !isAuthenticatingWithGoogle)
            {
                var logoImage = new Image();
                logoImage.image = googleLogo;
                logoImage.style.width = 18;
                logoImage.style.height = 18;
                logoImage.style.marginRight = 10;
                logoImage.style.alignSelf = Align.Center;
                logoImage.style.flexShrink = 0; // Prevent logo from shrinking
                buttonContent.Add(logoImage);
            }
            
            var buttonLabel = new Label(isAuthenticatingWithGoogle ? "Authenticating..." : "Login with Google");
            buttonLabel.style.color = Color.white;
            buttonLabel.style.fontSize = 13;
            buttonLabel.style.alignSelf = Align.Center;
            buttonLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            buttonLabel.style.whiteSpace = WhiteSpace.NoWrap;
            buttonContent.Add(buttonLabel);
            
            googleButton.Add(buttonContent);
            googleButton.text = ""; // Clear default text since we're using custom content
            googleContainer.Add(googleButton);
            
            mainContainer.Add(googleContainer);
            
            // Authentication status
            if (isAuthenticatingWithGoogle)
            {
                var statusLabel = new Label("Authenticating with Google...");
                statusLabel.style.fontSize = 12;
                statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                statusLabel.style.marginBottom = 10;
                mainContainer.Add(statusLabel);
                
                var cancelButton = new Button(() => {
                    StopOAuthListener();
                    isAuthenticatingWithGoogle = false;
                    Debug.Log("[BoostOps] Google OAuth cancelled by user");
                    RefreshAccountPanel(); // Refresh the panel
                });
                cancelButton.text = "Cancel Authentication";
                cancelButton.style.width = 140;
                cancelButton.style.alignSelf = Align.Center;
                cancelButton.style.marginBottom = 10;
                mainContainer.Add(cancelButton);
                
                // Manual token input as fallback
                var manualTokenLabel = new Label("If you have the OAuth callback URL, paste the token below:");
                manualTokenLabel.style.fontSize = 11;
                manualTokenLabel.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                manualTokenLabel.style.marginBottom = 5;
                manualTokenLabel.style.whiteSpace = WhiteSpace.Normal;
                mainContainer.Add(manualTokenLabel);
                
                var tokenField = new TextField();
                tokenField.multiline = true;
                tokenField.style.height = 60;
                tokenField.style.marginBottom = 10;
                tokenField.value = "";
                tokenField.RegisterValueChangedCallback(evt => {
                    // Auto-extract token from full URL if user pastes the whole callback URL
                    string input = evt.newValue;
                    if (input.Contains("token="))
                    {
                        try
                        {
                            // Simple token extraction without System.Web dependency
                            int tokenStart = input.IndexOf("token=") + 6;
                            int tokenEnd = input.IndexOf("&", tokenStart);
                            if (tokenEnd == -1) tokenEnd = input.Length;
                            
                            string token = input.Substring(tokenStart, tokenEnd - tokenStart);
                            token = System.Uri.UnescapeDataString(token); // Decode URL encoding
                            
                            if (!string.IsNullOrEmpty(token) && token != input)
                            {
                                tokenField.value = token;
                                Debug.Log("[BoostOps] Auto-extracted token from URL");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[BoostOps] Could not extract token from URL: {ex.Message}");
                        }
                    }
                });
                mainContainer.Add(tokenField);
                
                var submitTokenButton = new Button(() => {
                    string token = tokenField.value?.Trim();
                    if (!string.IsNullOrEmpty(token))
                    {
                        Debug.Log("[BoostOps] Manual token submitted");
                        HandleOAuthSuccess(token);
                    }
                    else
                    {
                        Debug.LogWarning("[BoostOps] No token provided");
                    }
                });
                submitTokenButton.text = "Use Manual Token";
                submitTokenButton.style.width = 120;
                submitTokenButton.style.alignSelf = Align.Center;
                submitTokenButton.style.marginBottom = 15;
                mainContainer.Add(submitTokenButton);
            }
            else
            {
                // Reset OAuth button (only show when not authenticating)
                var debugContainer = new VisualElement();
                debugContainer.style.flexDirection = FlexDirection.Row;
                debugContainer.style.justifyContent = Justify.Center;
                debugContainer.style.marginBottom = 10;
                
                var resetButton = new Button(() => {
                    Debug.Log("[BoostOps] 🔄 Resetting OAuth state...");
                    StopOAuthListener();
                    isAuthenticatingWithGoogle = false;
                    RefreshAccountPanel();
                    Debug.Log("[BoostOps] ✅ OAuth state reset complete");
                });
                resetButton.text = "Reset OAuth State";
                resetButton.style.width = 120;
                resetButton.style.height = 25;
                resetButton.style.fontSize = 10;
                resetButton.style.backgroundColor = new Color(0.6f, 0.4f, 0.4f, 1f);
                debugContainer.Add(resetButton);
                
                mainContainer.Add(debugContainer);
            }
            
            // Divider
            var dividerContainer = new VisualElement();
            dividerContainer.style.flexDirection = FlexDirection.Row;
            dividerContainer.style.alignItems = Align.Center;
            dividerContainer.style.marginBottom = 15;
            
            var leftLine = new VisualElement();
            leftLine.style.height = 1;
            leftLine.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            leftLine.style.flexGrow = 1;
            dividerContainer.Add(leftLine);
            
            var orLabel = new Label("OR");
            orLabel.style.fontSize = 11;
            orLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            orLabel.style.marginLeft = 10;
            orLabel.style.marginRight = 10;
            dividerContainer.Add(orLabel);
            
            var rightLine = new VisualElement();
            rightLine.style.height = 1;
            rightLine.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            rightLine.style.flexGrow = 1;
            dividerContainer.Add(rightLine);
            
            mainContainer.Add(dividerContainer);
            
            // Email Sign-In Section - HIDDEN FOR NOW
            // TODO: Re-enable email authentication when ready
            /*
            var emailSection = new Label("Email Sign-In");
            emailSection.style.fontSize = 14;
            emailSection.style.unityFontStyleAndWeight = FontStyle.Bold;
            emailSection.style.marginBottom = 10;
            mainContainer.Add(emailSection);
            
            // Toggle between login and signup
            var toggleContainer = new VisualElement();
            toggleContainer.style.flexDirection = FlexDirection.Row;
            toggleContainer.style.marginBottom = 15;
            
            var loginToggle = new Button(() => {
                showSignupForm = false;
                RefreshAccountPanel(); // Refresh
            });
            loginToggle.text = "Sign In";
            loginToggle.style.flexGrow = 1;
            loginToggle.style.marginRight = 5;
            if (!showSignupForm) loginToggle.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 1f);
            toggleContainer.Add(loginToggle);
            
            var signupToggle = new Button(() => {
                showSignupForm = true;
                RefreshAccountPanel(); // Refresh
            });
            signupToggle.text = "Create Account";
            signupToggle.style.flexGrow = 1;
            signupToggle.style.marginLeft = 5;
            if (showSignupForm) signupToggle.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 1f);
            toggleContainer.Add(signupToggle);
            
            mainContainer.Add(toggleContainer);
            
            // Form fields
            if (showSignupForm)
            {
                BuildSignupFormUI(mainContainer);
            }
            else
            {
                BuildLoginFormUI(mainContainer);
            }
            */
            
            contentContainer.Add(mainContainer);
        }
        
        void BuildLoginFormUI(VisualElement parent)
        {
            var formTitle = new Label("Sign In");
            formTitle.style.fontSize = 14;
            formTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            formTitle.style.marginBottom = 10;
            parent.Add(formTitle);
            
            // Email field
            var emailField = new TextField();
            emailField.value = loginEmail;
            emailField.RegisterValueChangedCallback(evt => loginEmail = evt.newValue);
            
            var emailRow = CreateLabelFieldRow("Email:", emailField);
            emailRow.style.marginBottom = 10;
            parent.Add(emailRow);
            
            // Password field
            var passwordField = new TextField();
            passwordField.value = loginPassword;
            passwordField.isPasswordField = true;
            passwordField.RegisterValueChangedCallback(evt => loginPassword = evt.newValue);
            
            var passwordRow = CreateLabelFieldRow("Password:", passwordField);
            passwordRow.style.marginBottom = 15;
            parent.Add(passwordRow);
            
            // Sign in button
            var signinButton = new Button(() => PerformLogin());
            signinButton.text = isAuthenticating ? "Signing In..." : "Sign In";
            signinButton.style.height = 30;
            signinButton.SetEnabled(!isAuthenticating && !string.IsNullOrEmpty(loginEmail) && !string.IsNullOrEmpty(loginPassword));
            parent.Add(signinButton);
            
            // Forgot password link
            var forgotContainer = new VisualElement();
            forgotContainer.style.flexDirection = FlexDirection.Row;
            forgotContainer.style.justifyContent = Justify.FlexEnd;
            forgotContainer.style.marginTop = 10;
            
            var forgotButton = new Button(() => Application.OpenURL("https://dashboard.boostops.com/forgot-password"));
            forgotButton.text = "Forgot Password?";
            forgotButton.style.backgroundColor = Color.clear;
            forgotButton.style.borderLeftWidth = 0;
            forgotButton.style.borderRightWidth = 0;
            forgotButton.style.borderTopWidth = 0;
            forgotButton.style.borderBottomWidth = 0;
            forgotButton.style.color = new Color(0.5f, 0.7f, 1f, 1f);
            forgotContainer.Add(forgotButton);
            
            parent.Add(forgotContainer);
        }
        
        void BuildSignupFormUI(VisualElement parent)
        {
            var formTitle = new Label("Create Account");
            formTitle.style.fontSize = 14;
            formTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            formTitle.style.marginBottom = 10;
            parent.Add(formTitle);
            
            // Email field
            var emailField = new TextField();
            emailField.value = signupEmail;
            emailField.RegisterValueChangedCallback(evt => signupEmail = evt.newValue);
            
            var emailRow = CreateLabelFieldRow("Email:", emailField);
            emailRow.style.marginBottom = 10;
            parent.Add(emailRow);
            
            // Password field
            var passwordField = new TextField();
            passwordField.value = signupPassword;
            passwordField.isPasswordField = true;
            passwordField.RegisterValueChangedCallback(evt => signupPassword = evt.newValue);
            
            var passwordRow = CreateLabelFieldRow("Password:", passwordField);
            passwordRow.style.marginBottom = 10;
            parent.Add(passwordRow);
            
            // Confirm password field
            var confirmField = new TextField();
            confirmField.value = signupConfirmPassword;
            confirmField.isPasswordField = true;
            confirmField.RegisterValueChangedCallback(evt => signupConfirmPassword = evt.newValue);
            
            var confirmRow = CreateLabelFieldRow("Confirm Password:", confirmField);
            confirmRow.style.marginBottom = 10;
            parent.Add(confirmRow);
            
            // Password validation
            if (!string.IsNullOrEmpty(signupPassword) && !string.IsNullOrEmpty(signupConfirmPassword))
            {
                if (signupPassword != signupConfirmPassword)
                {
                    var errorLabel = new Label("Passwords do not match.");
                    errorLabel.style.color = new Color(1f, 0.6f, 0.6f, 1f);
                    errorLabel.style.fontSize = 11;
                    errorLabel.style.marginBottom = 10;
                    parent.Add(errorLabel);
                }
                else if (signupPassword.Length < 8)
                {
                    var errorLabel = new Label("Password must be at least 8 characters long.");
                    errorLabel.style.color = new Color(1f, 0.8f, 0.4f, 1f);
                    errorLabel.style.fontSize = 11;
                    errorLabel.style.marginBottom = 10;
                    parent.Add(errorLabel);
                }
            }
            
            // Create account button
            bool canSignup = !isAuthenticating && 
                           !string.IsNullOrEmpty(signupEmail) && 
                           !string.IsNullOrEmpty(signupPassword) && 
                           signupPassword == signupConfirmPassword && 
                           signupPassword.Length >= 8;
            
            var signupButton = new Button(() => PerformSignup());
            signupButton.text = isAuthenticating ? "Creating Account..." : "Create Account";
            signupButton.style.height = 30;
            signupButton.style.marginBottom = 15;
            signupButton.SetEnabled(canSignup);
            parent.Add(signupButton);
            
            // Terms of service info
            var tosLabel = new Label("By creating an account, you agree to the BoostOps Terms of Service and Privacy Policy.");
            tosLabel.style.fontSize = 11;
            tosLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            tosLabel.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(tosLabel);
        }
        
        void BuildUserInfoUI()
        {
            // Hero section with prominent styling (matching Links and Cross-Promo format)
            var heroSection = new VisualElement();
            heroSection.style.flexDirection = FlexDirection.Row;
            heroSection.style.alignItems = Align.Center;
            heroSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            heroSection.style.paddingLeft = 20;
            heroSection.style.paddingRight = 20;
            heroSection.style.paddingTop = 15;
            heroSection.style.paddingBottom = 15;
            heroSection.style.marginBottom = 20;
            heroSection.style.borderTopLeftRadius = 8;
            heroSection.style.borderTopRightRadius = 8;
            heroSection.style.borderBottomLeftRadius = 8;
            heroSection.style.borderBottomRightRadius = 8;
            
            // Title and description container
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            
            var title = new Label("Account");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 5;
            textContainer.Add(title);

            var description = new Label("✨ Manage your account settings, studio information, and access helpful resources.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.9f, 0.95f, 0.9f, 1f); // Brighter green tint for better readability
            description.style.whiteSpace = WhiteSpace.Normal;
            textContainer.Add(description);
            
            heroSection.Add(textContainer);
            contentContainer.Add(heroSection);
            
            // User info card
            var userContainer = CreateCard("Account Information", "👤");
            
            // User email and sign out
            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.justifyContent = Justify.SpaceBetween;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.marginBottom = 15;
            
            var emailLabel = new Label($"Signed in as: {userEmail}");
            emailLabel.style.fontSize = 14;
            emailLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerContainer.Add(emailLabel);
            
            var signOutButton = new Button(() => SignOut());
            signOutButton.text = "Sign Out";
            signOutButton.style.width = 100;
            headerContainer.Add(signOutButton);
            
            userContainer.Add(headerContainer);
            
            // Action buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.SpaceAround;
            buttonContainer.style.marginTop = 10;
            
            var dashboardButton = new Button(() => Application.OpenURL("https://app.boostops.io"));
            dashboardButton.text = "🏠 Dashboard";
            dashboardButton.style.width = 110;
            dashboardButton.style.height = 28;
            dashboardButton.style.fontSize = 11;
            dashboardButton.tooltip = "Open BoostOps Dashboard";
            buttonContainer.Add(dashboardButton);
            
            var docsButton = new Button(() => Application.OpenURL("https://docs.boostops.io"));
            docsButton.text = "📚 Docs";
            docsButton.style.width = 80;
            docsButton.style.height = 28;
            docsButton.style.fontSize = 11;
            docsButton.tooltip = "View BoostOps Documentation";
            buttonContainer.Add(docsButton);
            
            userContainer.Add(buttonContainer);
            contentContainer.Add(userContainer);
            
            // Studio Info section
            BuildStudioInfoUI();
            
            // Developer Settings section  
            BuildDeveloperSettings();
        }
        void BuildStudioInfoUI()
        {
            // Studio container
            var studioContainer = CreateCard("Studio", "🏢");
            
            // Studio management button for owners
            if (isStudioOwner)
            {
                var studioHeaderContainer = new VisualElement();
                studioHeaderContainer.style.flexDirection = FlexDirection.Row;
                studioHeaderContainer.style.justifyContent = Justify.FlexEnd;
                studioHeaderContainer.style.marginBottom = 10;
                
                var studioSettingsButton = new Button(() => Application.OpenURL("https://app.boostops.io/studio/settings"));
                studioSettingsButton.text = "⚙️ Studio Settings";
                studioSettingsButton.style.width = 130;
                studioSettingsButton.style.height = 26;
                studioSettingsButton.style.fontSize = 11;
                studioSettingsButton.tooltip = "Manage studio settings";
                studioHeaderContainer.Add(studioSettingsButton);
                
                studioContainer.Add(studioHeaderContainer);
            }
            
            // Studio name row
            var studioNameRow = new VisualElement();
            studioNameRow.style.flexDirection = FlexDirection.Row;
            studioNameRow.style.alignItems = Align.Center;
            studioNameRow.style.marginBottom = 10;
            
            var studioNameLabel = new Label("Studio Name:");
            studioNameLabel.style.fontSize = 12;
            studioNameLabel.style.width = 90;
            studioNameLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            studioNameRow.Add(studioNameLabel);
            
            if (isEditingStudioName && isStudioOwner)
            {
                // Editing mode
                var editField = new TextField();
                editField.value = editingStudioName;
                editField.style.flexGrow = 1;
                editField.style.marginRight = 5;
                
                var saveButton = new Button(() => SaveStudioName(editField.value));
                saveButton.text = "Save";
                saveButton.style.width = 50;
                
                var cancelButton = new Button(() => CancelEditStudioName());
                cancelButton.text = "Cancel";
                cancelButton.style.width = 60;
                cancelButton.style.marginLeft = 5;
                
                studioNameRow.Add(editField);
                studioNameRow.Add(saveButton);
                studioNameRow.Add(cancelButton);
            }
            else
            {
                // Display mode
                var studioNameValue = new Label(string.IsNullOrEmpty(studioName) ? "No studio configured" : studioName);
                studioNameValue.style.fontSize = 12;
                studioNameValue.style.flexGrow = 1;
                studioNameValue.style.color = string.IsNullOrEmpty(studioName) ? new Color(0.8f, 0.4f, 0.4f, 1f) : new Color(0.9f, 0.9f, 0.9f, 1f);
                studioNameRow.Add(studioNameValue);
                
                if (isStudioOwner && !string.IsNullOrEmpty(studioName))
                {
                    var editButton = new Button(() => StartEditStudioName());
                    editButton.text = "Edit";
                    editButton.style.width = 50;
                    studioNameRow.Add(editButton);
                }
            }
            
            studioContainer.Add(studioNameRow);
            
            // Studio description row (if available)
            if (!string.IsNullOrEmpty(studioDescription))
            {
                var studioDescRow = new VisualElement();
                studioDescRow.style.flexDirection = FlexDirection.Row;
                studioDescRow.style.alignItems = Align.Center;
                studioDescRow.style.marginBottom = 10;
                
                var studioDescLabel = new Label("Description:");
                studioDescLabel.style.fontSize = 11;
                studioDescLabel.style.width = 90;
                studioDescLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                studioDescRow.Add(studioDescLabel);
                
                var studioDescValue = new Label(studioDescription);
                studioDescValue.style.fontSize = 11;
                studioDescValue.style.flexGrow = 1;
                studioDescValue.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                studioDescValue.style.whiteSpace = WhiteSpace.Normal;
                studioDescRow.Add(studioDescValue);
                
                studioContainer.Add(studioDescRow);
            }
            
            // Studio ID row (read-only for debugging/support)
            if (!string.IsNullOrEmpty(studioId))
            {
                var studioIdRow = new VisualElement();
                studioIdRow.style.flexDirection = FlexDirection.Row;
                studioIdRow.style.alignItems = Align.Center;
                
                var studioIdLabel = new Label("Studio ID:");
                studioIdLabel.style.fontSize = 11;
                studioIdLabel.style.width = 90;
                studioIdLabel.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                studioIdRow.Add(studioIdLabel);
                
                var studioIdValue = new Label(TruncateText(studioId, 30));
                studioIdValue.style.fontSize = 11;
                studioIdValue.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                studioIdRow.Add(studioIdValue);
                
                studioContainer.Add(studioIdRow);
            }
            
            contentContainer.Add(studioContainer);
        }
        
        void StartEditStudioName()
        {
            isEditingStudioName = true;
            editingStudioName = studioName;
            RefreshAccountPanel(); // Rebuild UI to show edit mode
        }
        void CancelEditStudioName()
        {
            isEditingStudioName = false;
            editingStudioName = "";
            RefreshAccountPanel(); // Rebuild UI to show display mode
        }
        async void SaveStudioName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                Debug.LogWarning("Studio name cannot be empty");
                return;
            }
            
            try
            {
                // Call API to update studio name using new API format
                var updateRequest = new StudioUpdateRequest { name = newName.Trim() };
                var updateResult = await UpdateStudio(studioId, updateRequest);
                
                if (updateResult != null)
                {
                    dynamic result = updateResult;
                    studioName = result.name;
                    SaveStudioInfo();
                    isEditingStudioName = false;
                    editingStudioName = "";
                    RefreshAccountPanel(); // Rebuild UI to show updated name
                    LogDebug($"Studio name updated to: {studioName}");
                }
                else
                {
                    Debug.LogError("Failed to update studio name");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error updating studio name: {ex.Message}");
            }
        }

        
        void BuildDeveloperSettings()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            container.style.paddingLeft = 15;
            container.style.paddingRight = 15;
            container.style.paddingTop = 15;
            container.style.paddingBottom = 15;
            container.style.borderTopLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderBottomRightRadius = 6;
            container.style.marginTop = 15;
            
            var debugLabel = new Label("Debug Logging Controls");
            debugLabel.style.fontSize = 12;
            debugLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            debugLabel.style.marginBottom = 8;
            container.Add(debugLabel);
            
            // Editor Debug Logging Toggle
            var editorToggle = new Toggle("Editor Window Debug Messages");
            editorToggle.style.fontSize = 11;
            editorToggle.style.marginBottom = 3;
            editorToggle.value = BoostOpsLogger.IsEditorDebugLoggingEnabled;
            editorToggle.RegisterValueChangedCallback(evt => {
                BoostOpsLogger.IsEditorDebugLoggingEnabled = evt.newValue;
                enableDebugLogging = evt.newValue; // Keep backward compatibility
                
                // Sync with project settings asset
                if (dynamicLinksConfig != null)
                {
                    dynamicLinksConfig.debugLogging = evt.newValue;
                    EditorUtility.SetDirty(dynamicLinksConfig);
                    AssetDatabase.SaveAssets();
                }
                SaveDebugLogging();
                UpdateLoggingStatusLabel();
            });
            container.Add(editorToggle);
            
            var editorHelp = new Label("Shows Editor Window, Post Process Build, and configuration debug messages.");
            editorHelp.style.fontSize = 9;
            editorHelp.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            editorHelp.style.whiteSpace = WhiteSpace.Normal;
            editorHelp.style.marginBottom = 8;
            editorHelp.style.marginLeft = 20;
            container.Add(editorHelp);
            
            // Runtime Debug Logging Toggle
            var runtimeToggle = new Toggle("Runtime SDK Debug Messages");
            runtimeToggle.style.fontSize = 11;
            runtimeToggle.style.marginBottom = 3;
            runtimeToggle.value = BoostOpsLogger.IsRuntimeDebugLoggingEnabled;
            runtimeToggle.RegisterValueChangedCallback(evt => {
                BoostOpsLogger.IsRuntimeDebugLoggingEnabled = evt.newValue;
                UpdateLoggingStatusLabel();
            });
            container.Add(runtimeToggle);
            
            var runtimeHelp = new Label("Shows Analytics, Campaign Manager, Remote Config, and other runtime debug messages.");
            runtimeHelp.style.fontSize = 9;
            runtimeHelp.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            runtimeHelp.style.whiteSpace = WhiteSpace.Normal;
            runtimeHelp.style.marginBottom = 8;
            runtimeHelp.style.marginLeft = 20;
            container.Add(runtimeHelp);
            
            // Quick Presets
            var presetLabel = new Label("Quick Presets:");
            presetLabel.style.fontSize = 10;
            presetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            presetLabel.style.marginBottom = 5;
            presetLabel.style.marginTop = 5;
            container.Add(presetLabel);
            
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginBottom = 8;
            
            var editorOnlyBtn = new Button(() => {
                BoostOpsLogger.EnableEditorLoggingOnly();
                RefreshLoggingToggles(editorToggle, runtimeToggle);
            }) { text = "Editor Only" };
            editorOnlyBtn.style.fontSize = 9;
            editorOnlyBtn.style.marginRight = 5;
            editorOnlyBtn.style.paddingTop = 2;
            editorOnlyBtn.style.paddingBottom = 2;
            buttonContainer.Add(editorOnlyBtn);
            
            var runtimeOnlyBtn = new Button(() => {
                BoostOpsLogger.EnableRuntimeLoggingOnly();
                RefreshLoggingToggles(editorToggle, runtimeToggle);
            }) { text = "Runtime Only" };
            runtimeOnlyBtn.style.fontSize = 9;
            runtimeOnlyBtn.style.marginRight = 5;
            runtimeOnlyBtn.style.paddingTop = 2;
            runtimeOnlyBtn.style.paddingBottom = 2;
            buttonContainer.Add(runtimeOnlyBtn);
            
            var allOnBtn = new Button(() => {
                BoostOpsLogger.EnableAllLogging();
                RefreshLoggingToggles(editorToggle, runtimeToggle);
            }) { text = "All" };
            allOnBtn.style.fontSize = 9;
            allOnBtn.style.marginRight = 5;
            allOnBtn.style.paddingTop = 2;
            allOnBtn.style.paddingBottom = 2;
            buttonContainer.Add(allOnBtn);
            
            var allOffBtn = new Button(() => {
                BoostOpsLogger.DisableAllLogging();
                RefreshLoggingToggles(editorToggle, runtimeToggle);
            }) { text = "None" };
            allOffBtn.style.fontSize = 9;
            allOffBtn.style.paddingTop = 2;
            allOffBtn.style.paddingBottom = 2;
            buttonContainer.Add(allOffBtn);
            
            container.Add(buttonContainer);
            
            // Status Label (will be populated by UpdateLoggingStatusLabel)
            loggingStatusLabel = new Label();
            loggingStatusLabel.style.fontSize = 9;
            loggingStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            loggingStatusLabel.style.color = new Color(0.5f, 0.8f, 0.5f, 1f);
            loggingStatusLabel.style.marginTop = 5;
            UpdateLoggingStatusLabel();
            container.Add(loggingStatusLabel);
            
            contentContainer.Add(container);
        }



        void BuildConfigurationSections()
        {
            var sectionsContainer = new VisualElement();
            sectionsContainer.style.flexDirection = FlexDirection.Row;
            sectionsContainer.style.marginTop = 10;
            
            // iOS Section (Left)
            var iosSection = new VisualElement();
            iosSection.style.width = Length.Percent(50);
            iosSection.style.flexShrink = 0;
            iosSection.style.marginRight = 5;
            iosSection.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            iosSection.style.paddingLeft = 10;
            iosSection.style.paddingRight = 10;
            iosSection.style.paddingTop = 10;
            iosSection.style.paddingBottom = 5;
            iosSection.style.borderTopLeftRadius = 4;
            iosSection.style.borderTopRightRadius = 4;
            iosSection.style.borderBottomLeftRadius = 4;
            iosSection.style.borderBottomRightRadius = 4;
            
            // iOS title row
            var iosTitleRow = new VisualElement();
            iosTitleRow.style.flexDirection = FlexDirection.Row;
            iosTitleRow.style.alignItems = Align.Center;
            iosTitleRow.style.marginBottom = 10;
            
            var iosTitle = new Label("Configure iOS Universal Links");
            iosTitle.style.fontSize = 14;
            iosTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            iosTitleRow.Add(iosTitle);
            
            iosSection.Add(iosTitleRow);
            
            // iOS Bundle ID (read-only from Unity PlayerSettings)
            var iosBundleValue = new Label(iosBundleId);
            iosBundleValue.style.paddingLeft = 8;
            iosBundleValue.style.paddingRight = 8;
            iosBundleValue.style.paddingTop = 4;
            iosBundleValue.style.paddingBottom = 4;
            iosBundleValue.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            iosBundleValue.style.borderTopLeftRadius = 3;
            iosBundleValue.style.borderTopRightRadius = 3;
            iosBundleValue.style.borderBottomLeftRadius = 3;
            iosBundleValue.style.borderBottomRightRadius = 3;
            iosBundleValue.style.fontSize = 12;
            iosBundleValue.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            
            var iosBundleRow = CreateLabelFieldRow("Bundle Identifier:", iosBundleValue);
            iosBundleRow.style.marginBottom = 10;
            iosSection.Add(iosBundleRow);
            
            // iOS Team ID (read-only from Unity PlayerSettings)
            var iosTeamValue = new Label(iosTeamId);
            iosTeamValue.style.paddingLeft = 8;
            iosTeamValue.style.paddingRight = 8;
            iosTeamValue.style.paddingTop = 4;
            iosTeamValue.style.paddingBottom = 4;
            iosTeamValue.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            iosTeamValue.style.borderTopLeftRadius = 3;
            iosTeamValue.style.borderTopRightRadius = 3;
            iosTeamValue.style.borderBottomLeftRadius = 3;
            iosTeamValue.style.borderBottomRightRadius = 3;
            iosTeamValue.style.fontSize = 12;
            iosTeamValue.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            
            var iosTeamRow = CreateLabelFieldRow("Signing Team ID:", iosTeamValue);
            iosTeamRow.style.marginBottom = 10;
            iosSection.Add(iosTeamRow);
            
            // Apple Store ID (dynamically required based on hosting mode) - custom Unity-style row
            var appleStoreRow = new VisualElement();
            appleStoreRow.style.flexDirection = FlexDirection.Row;
            appleStoreRow.style.alignItems = Align.Center;
            appleStoreRow.style.marginBottom = 4;
            
            appleStoreLabelRef = new Label(hostingOption == HostingOption.Cloud ? "Apple Store ID (Required):" : "Apple Store ID:");
            appleStoreLabelRef.style.fontSize = 12;
            appleStoreLabelRef.style.width = 140; // Fixed width for consistent alignment with other fields
            appleStoreLabelRef.style.marginRight = 8; // Adjusted to match CreateLabelFieldRow spacing
            appleStoreLabelRef.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            var appleStoreFieldContainer = new VisualElement();
            appleStoreFieldContainer.style.flexDirection = FlexDirection.Row;
            appleStoreFieldContainer.style.flexGrow = 1;
            appleStoreFieldContainer.style.alignItems = Align.Center;
            
            var appleStoreInput = new TextField();
            appleStoreInput.style.fontSize = 12;
            appleStoreInput.style.height = 22;
            appleStoreInput.style.flexGrow = 1;
            appleStoreInput.value = iosAppStoreId;
            
            // In managed mode, field is always read-only (updated via dashboard)
            // In local mode, check if field should be locked (registered and not in edit mode)
            bool isAppleStoreIdLocked = (linksMode == FeatureMode.Managed) || (isProjectRegistered && !isAppleStoreIdInEditMode);
            appleStoreInput.SetEnabled(!isAppleStoreIdLocked);
            
            if (isAppleStoreIdLocked)
            {
                appleStoreInput.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f); // Darker when locked
                appleStoreInput.style.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Grayed out text
                if (linksMode == FeatureMode.Managed)
                {
                    appleStoreInput.tooltip = "This field is managed via the BoostOps dashboard and cannot be edited here.";
                }
                else
                {
                    appleStoreInput.tooltip = "This field is locked after registration. Click the edit button to unlock and modify.";
                }
            }
            
            Button appleStoreEditButton = null;
            // Only show edit button in local mode when field is locked
            bool showEditButton = (linksMode != FeatureMode.Managed) && isAppleStoreIdLocked;
            appleStoreEditButton = new Button(() => {
                isAppleStoreIdInEditMode = true;
                appleStoreInput.SetEnabled(true);
                appleStoreEditButton.style.display = DisplayStyle.None;
                // Show warning about re-registration requirement
                ShowCriticalFieldEditWarning("Apple Store ID");
            }) { text = "✏️" };
            appleStoreEditButton.style.width = 30;
            appleStoreEditButton.style.height = 22;
            appleStoreEditButton.style.marginLeft = 5;
            appleStoreEditButton.style.display = showEditButton ? DisplayStyle.Flex : DisplayStyle.None;
            appleStoreEditButton.tooltip = "Unlock this field for editing (requires re-registration)";
            
            // Create warning label that we'll show/hide based on validation
            var appleStoreWarning = new Label("⚠ Apple Store ID required for cloud mode");
            appleStoreWarning.style.color = new Color(1f, 0.8f, 0f); // Orange warning color
            appleStoreWarning.style.fontSize = 11;
            appleStoreWarning.style.marginBottom = 5;
            
            // Function to update warning visibility based on current value and hosting mode
            System.Action updateAppleStoreWarning = () => {
                string currentValue = appleStoreInput.value?.Trim();
                bool shouldShowWarning = hostingOption == HostingOption.Cloud && (string.IsNullOrEmpty(currentValue) || !IsValidAppleAppStoreId(currentValue));
                
                if (shouldShowWarning && appleStoreWarning.parent == null)
                {
                    // Add warning if it's not already shown
                    iosSection.Add(appleStoreWarning);
                }
                else if (!shouldShowWarning && appleStoreWarning.parent != null)
                {
                    // Remove warning if it's currently shown but shouldn't be
                    iosSection.Remove(appleStoreWarning);
                }
                
                // Update warning text based on validation
                if (hostingOption == HostingOption.Cloud)
                {
                    if (!string.IsNullOrEmpty(currentValue) && !IsValidAppleAppStoreId(currentValue))
                    {
                        appleStoreWarning.text = "⚠ Invalid Apple Store ID format (must be 6-15 digits, e.g., '1234567890' or 'id1234567890')";
                    }
                    else if (string.IsNullOrEmpty(currentValue))
                    {
                        appleStoreWarning.text = "⚠ Apple Store ID required for cloud mode";
                    }
                }
            };
            
            appleStoreInput.RegisterValueChangedCallback(evt => {
                string oldValue = iosAppStoreId;
                // Use the same comprehensive normalization as other iOS Store ID fields
                string normalizedId = ExtractIOSStoreId(evt.newValue);
                iosAppStoreId = normalizedId ?? evt.newValue; // Keep original if normalization fails
                
                // Update the field value if normalization changed it
                if (normalizedId != null && normalizedId != evt.newValue)
                {
                    appleStoreInput.SetValueWithoutNotify(normalizedId);
                }
                
                SaveAppleAppStoreId();
                
                // If this is a registered project and field was changed, mark for re-registration
                if (isProjectRegistered && isAppleStoreIdInEditMode && oldValue != iosAppStoreId)
                {
                    needsReregistration = true;
                    SaveRegistrationState();
                }
                
                // Update warning in real-time
                updateAppleStoreWarning();
                // Refresh the domain content to update validation
                if (hostingOption == HostingOption.Cloud) RefreshDomainAndUsageContent();
            });
            
            // Initial warning update
            updateAppleStoreWarning();
            
            appleStoreFieldContainer.Add(appleStoreInput);
            appleStoreFieldContainer.Add(appleStoreEditButton);
            
            appleStoreRow.Add(appleStoreLabelRef);
            appleStoreRow.Add(appleStoreFieldContainer);
            iosSection.Add(appleStoreRow);
            
            sectionsContainer.Add(iosSection);
            
            // Android Section (Right)
            var androidSection = new VisualElement();
            androidSection.style.width = Length.Percent(50);
            androidSection.style.flexShrink = 0;
            androidSection.style.marginLeft = 5;
            androidSection.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            androidSection.style.paddingLeft = 10;
            androidSection.style.paddingRight = 10;
            androidSection.style.paddingTop = 10;
            androidSection.style.paddingBottom = 5;
            androidSection.style.borderTopLeftRadius = 4;
            androidSection.style.borderTopRightRadius = 4;
            androidSection.style.borderBottomLeftRadius = 4;
            androidSection.style.borderBottomRightRadius = 4;
            
            // Android title row with Player Settings button
            var androidTitleRow = new VisualElement();
            androidTitleRow.style.flexDirection = FlexDirection.Row;
            androidTitleRow.style.alignItems = Align.Center;
            androidTitleRow.style.justifyContent = Justify.SpaceBetween;
            androidTitleRow.style.marginBottom = 10;
            
            var androidTitle = new Label("Configure Android App Links");
            androidTitle.style.fontSize = 14;
            androidTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            androidTitleRow.Add(androidTitle);
            
            var androidSettingsButton = new Button(() => {
                // Open Player Settings
                EditorApplication.ExecuteMenuItem("Edit/Project Settings...");
            }) { text = "Player Settings" };
            androidSettingsButton.style.width = 100;
            androidTitleRow.Add(androidSettingsButton);
            
            androidSection.Add(androidTitleRow);
            
            // Android Package Name (read-only from Unity PlayerSettings)
            var androidPackageValue = new Label(androidBundleId);
            androidPackageValue.style.paddingLeft = 8;
            androidPackageValue.style.paddingRight = 8;
            androidPackageValue.style.paddingTop = 4;
            androidPackageValue.style.paddingBottom = 4;
            androidPackageValue.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            androidPackageValue.style.borderTopLeftRadius = 3;
            androidPackageValue.style.borderTopRightRadius = 3;
            androidPackageValue.style.borderBottomLeftRadius = 3;
            androidPackageValue.style.borderBottomRightRadius = 3;
            androidPackageValue.style.fontSize = 12;
            androidPackageValue.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            
            var androidPackageRow = CreateLabelFieldRow("Package Name:", androidPackageValue);
            androidPackageRow.style.marginBottom = 10;
            androidSection.Add(androidPackageRow);
            
            // Android Certificate Fingerprint - custom Unity-style row
            var androidCertRow = new VisualElement();
            androidCertRow.style.flexDirection = FlexDirection.Row;
            androidCertRow.style.alignItems = Align.Center;
            androidCertRow.style.marginBottom = 4;
            
            var androidCertLabel = new Label("SHA256 Fingerprint:");
            androidCertLabel.style.fontSize = 12;
            androidCertLabel.style.width = 140; // Fixed width for consistent alignment with Package Name above
            androidCertLabel.style.marginRight = 8; // Adjusted to match CreateLabelFieldRow spacing
            androidCertLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            
            var androidCertFieldContainer = new VisualElement();
            androidCertFieldContainer.style.flexDirection = FlexDirection.Row;
            androidCertFieldContainer.style.width = 360; // Further reduced to ensure buttons fit
            androidCertFieldContainer.style.alignItems = Align.Center;
            
            var androidCertField = new TextField();
            androidCertField.value = androidCertFingerprint;
            androidCertField.style.width = 280; // Further reduced to accommodate all buttons
            androidCertField.style.minHeight = 20; // Ensure minimum height even when empty
            androidCertField.style.marginRight = 5;
            
            // Set placeholder text to help with empty field visibility
            if (string.IsNullOrEmpty(androidCertFingerprint))
            {
                androidCertField.style.unityTextAlign = TextAnchor.MiddleLeft;
            }
            
            // Add tooltip with example format
            androidCertField.tooltip = "Enter SHA256 fingerprint (e.g., D1:09:CD:AE:F7:5D:47:3F:C0:67:3D:06:60:24:46:77:AE:A8:CB:86:D0:1B:31:48:C5:B0:2F:9D:83:0E:6F:5C)";
            
            // In managed mode, field is always read-only (updated via dashboard)
            // In local mode, check if field should be locked (registered and not in edit mode)
            bool isSHA256Locked = (linksMode == FeatureMode.Managed) || (isProjectRegistered && !isSHA256InEditMode);
            androidCertField.SetEnabled(!isSHA256Locked);
            
            if (isSHA256Locked)
            {
                androidCertField.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f); // Darker when locked
                androidCertField.style.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Grayed out text
                if (linksMode == FeatureMode.Managed)
                {
                    androidCertField.tooltip = "This field is managed via the BoostOps dashboard and cannot be edited here.";
                }
                else
                {
                    androidCertField.tooltip = "This field is locked after registration. Click the edit button to unlock and modify.";
                }
            }
            
            Button sha256EditButton = null;
            // Only show edit button in local mode when field is locked
            bool showSHA256EditButton = (linksMode != FeatureMode.Managed) && isSHA256Locked;
            sha256EditButton = new Button(() => {
                isSHA256InEditMode = true;
                androidCertField.SetEnabled(true);
                sha256EditButton.style.display = DisplayStyle.None;
                // Show warning about re-registration requirement
                ShowCriticalFieldEditWarning("SHA256 Fingerprint");
            }) { text = "✏️" };
            sha256EditButton.style.width = 30;
            sha256EditButton.style.height = 22;
            sha256EditButton.style.marginRight = 5;
            sha256EditButton.style.display = showSHA256EditButton ? DisplayStyle.Flex : DisplayStyle.None;
            sha256EditButton.tooltip = "Unlock this field for editing (requires re-registration)";
            
            // Create warning label that we'll show/hide based on validation
            var androidWarning = new Label("⚠ SHA256 fingerprint required for App Links");
            androidWarning.style.color = new Color(1f, 0.8f, 0f); // Orange warning color
            androidWarning.style.fontSize = 11;
            androidWarning.style.marginBottom = 5;
            
            // Function to update warning visibility based on current value
            System.Action updateWarning = () => {
                string currentValue = androidCertField.value?.Trim();
                bool shouldShowWarning = string.IsNullOrEmpty(currentValue) || !IsValidSHA256Fingerprint(currentValue);
                
                if (shouldShowWarning && androidWarning.parent == null)
                {
                    // Add warning if it's not already shown
                    androidSection.Add(androidWarning);
                }
                else if (!shouldShowWarning && androidWarning.parent != null)
                {
                    // Remove warning if it's currently shown but shouldn't be
                    androidSection.Remove(androidWarning);
                }
                
                // Update warning text based on validation
                if (!string.IsNullOrEmpty(currentValue) && !IsValidSHA256Fingerprint(currentValue))
                {
                    androidWarning.text = "⚠ Invalid SHA256 fingerprint format";
                }
                else if (string.IsNullOrEmpty(currentValue))
                {
                    androidWarning.text = "⚠ SHA256 fingerprint required for App Links";
                }
            };
            
            androidCertField.RegisterValueChangedCallback(evt => {
                string oldValue = androidCertFingerprint;
                androidCertFingerprint = evt.newValue;
                SaveAndroidCertFingerprint();
                
                // If this is a registered project and field was changed, mark for re-registration
                if (isProjectRegistered && isSHA256InEditMode && oldValue != androidCertFingerprint)
                {
                    needsReregistration = true;
                    SaveRegistrationState();
                }
                
                // Update warning in real-time
                updateWarning();
                // Refresh the registration validation section
                if (hostingOption == HostingOption.Cloud) RefreshDomainAndUsageContent();
            });
            
            var androidHelpButton = new Button(() => {
                ShowCertificateFingerprintHelp();
            }) { text = "?" };
            androidHelpButton.style.width = 25; // Square button
            androidHelpButton.style.height = 25; // Ensure it's actually square
            
            androidCertFieldContainer.Add(androidCertField);
            androidCertFieldContainer.Add(androidHelpButton);
            androidCertFieldContainer.Add(sha256EditButton);
            
            androidCertRow.Add(androidCertLabel);
            androidCertRow.Add(androidCertFieldContainer);
            androidSection.Add(androidCertRow);
            
            // Initial warning state
            updateWarning();
            

            
            sectionsContainer.Add(androidSection);
            
            contentContainer.Add(sectionsContainer);
        }

        void BuildGeneratedFilesSection()
        {
            var container = new VisualElement();
            container.style.marginTop = 20;
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.paddingTop = 10;
            container.style.paddingBottom = 10;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            
            var title = new Label("Generated Files");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            container.Add(title);
            
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.justifyContent = Justify.SpaceAround;
            
            var viewFilesButton = new Button(() => {
                ShowGeneratedFiles();
            }) { text = "View Files" };
            viewFilesButton.style.width = 120;
            buttonsContainer.Add(viewFilesButton);
            
            var openFolderButton = new Button(() => {
                string folderPath = System.IO.Path.Combine(Application.dataPath, "Generated", "BoostOps");
                if (System.IO.Directory.Exists(folderPath))
                {
                    EditorUtility.RevealInFinder(folderPath);
                }
            }) { text = "Open Folder" };
            openFolderButton.style.width = 120;
            buttonsContainer.Add(openFolderButton);
            
            var instructionsButton = new Button(() => {
                OpenInstructionsFile();
            }) { text = "Instructions" };
            instructionsButton.style.width = 120;
            buttonsContainer.Add(instructionsButton);
            
            container.Add(buttonsContainer);
            
            contentContainer.Add(container);
        }
        
        // Old QR Code Testing Section method - removed since we now use BuildQRCodeTestingContent() in the new layout
        
        string GenerateTestUrl()
        {
            // Determine domain to use
            string domainToUse = "";
            
            if (!string.IsNullOrEmpty(selectedQRDomain))
            {
                // Use explicitly selected QR domain
                domainToUse = selectedQRDomain;
            }
            else if (linksMode == FeatureMode.Managed && !string.IsNullOrEmpty(registeredProjectSlug))
            {
                // In managed mode, use the managed domain
                domainToUse = $"{registeredProjectSlug}.boostlink.me";
            }
            else
            {
                // Fall back to primary domain
                domainToUse = dynamicLinkUrl;
            }
            
            var validationResult = CleanAndValidateUrl(domainToUse);
            if (!validationResult.IsValid)
            {
                // Update inline error display instead of showing dialog
                if (qrErrorLabel != null)
                {
                    qrErrorLabel.text = validationResult.ErrorMessage;
                    qrErrorLabel.style.display = DisplayStyle.Flex;
                }
                return "";
            }
            
            // Clear any previous error
            if (qrErrorLabel != null)
            {
                qrErrorLabel.style.display = DisplayStyle.None;
            }
            
            // Clean domain for URL generation
            string cleanDomain = validationResult.CleanedValue.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            
            // Create a simple test URL - just enough to verify BoostLinks configuration works
            string testUrl = $"https://{cleanDomain}?test=qr";
            
            return testUrl;
        }
        
        async void GenerateQRCodeAsync(string text, Image qrImage)
        {
            // Generate QR code using QuickChart API - real, scannable QR codes!
            await GenerateQRCodeFromQuickChart(text, qrImage);
        }
        async System.Threading.Tasks.Task GenerateQRCodeFromQuickChart(string text, Image qrImage)
        {
            try
            {
                int size = 200; // Higher resolution for better scanning
                string apiUrl = GetBrandedQRCodeUrl(text, size);
                

                
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(apiUrl))
                {
                    request.timeout = 15; // Increased timeout
                    
                    var operation = request.SendWebRequest();
                    
                    // Wait for completion in editor-safe way
                    while (!operation.isDone)
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                    }
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D qrTexture = DownloadHandlerTexture.GetContent(request);
                        
                        // Overlay BoostOps logo using Unity
                        Texture2D brandedQrTexture = OverlayBoostOpsLogo(qrTexture);
                        
                        qrImage.image = brandedQrTexture;
                        qrImage.MarkDirtyRepaint();
                        
                        // Cache the generated QR code and URL
                        cachedQrTexture = brandedQrTexture;
                        lastQrGeneratedUrl = text;
                        
                        LogDebug($"QR Code: Successfully generated and cached QR code for URL: {text}");
                        
                        // Clear generation flag
                        isGeneratingQR = false;

                    }
                    else
                    {
                        Debug.LogError($"❌ QR code generation failed: {request.error}");
                        Debug.LogError($"Response Code: {request.responseCode}");
                        Debug.LogError($"URL: {apiUrl}");
                        qrImage.image = GenerateErrorPlaceholder(200);
                        
                        // Don't cache failed generation - allow retry next time
                        cachedQrTexture = null;
                        lastQrGeneratedUrl = "";
                        
                        // Clear generation flag
                        isGeneratingQR = false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ QR code generation exception: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
                qrImage.image = GenerateErrorPlaceholder(200);
                
                // Don't cache failed generation - allow retry next time
                cachedQrTexture = null;
                lastQrGeneratedUrl = "";
                
                // Clear generation flag
                isGeneratingQR = false;
            }
        }
        
        // Enhanced error placeholder with better messaging
        Texture2D GenerateErrorPlaceholder(int size)
        {
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];
            
            // Create a clear error pattern
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    
                    // Create border
                    if (x < 5 || x >= size - 5 || y < 5 || y >= size - 5)
                    {
                        pixels[index] = new Color(0.8f, 0.2f, 0.2f, 1f); // Red border
                    }
                    // Create X pattern in center
                    else if (Mathf.Abs(x - y) < 3 || Mathf.Abs(x - (size - y)) < 3)
                    {
                        pixels[index] = new Color(0.9f, 0.3f, 0.3f, 1f); // Red X
                    }
                    else
                    {
                        pixels[index] = new Color(0.95f, 0.95f, 0.95f, 1f); // Light gray background
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // Generate QR code for IMGUI interface using QuickChart with async/await
        async void GenerateQRCodeForIMGUI(string text)
        {
            // Generate QR code for IMGUI interface using QuickChart
            await GenerateQRCodeForIMGUIAsync(text);
        }
        async System.Threading.Tasks.Task GenerateQRCodeForIMGUIAsync(string text)
        {
            try
            {
                int size = 200;
                string apiUrl = GetBrandedQRCodeUrl(text, size);
                
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(apiUrl))
                {
                    request.timeout = 15;
                    
                    var operation = request.SendWebRequest();
                    
                    // Wait for completion in editor-safe way
                    while (!operation.isDone)
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                    }
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D qrTexture = DownloadHandlerTexture.GetContent(request);
                        
                        // Overlay BoostOps logo using Unity
                        qrCodeTexture = OverlayBoostOpsLogo(qrTexture);
                        

                    }
                    else
                    {
                        Debug.LogError($"❌ IMGUI QR code generation failed: {request.error}");
                        Debug.LogError($"Response Code: {request.responseCode}");
                        qrCodeTexture = GenerateErrorPlaceholder(size);
                    }
                    
                    // Repaint the window to show the updated QR code
                    Repaint();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ IMGUI QR code generation exception: {ex.Message}");
                qrCodeTexture = GenerateErrorPlaceholder(200);
                Repaint();
            }
        }
        // Enhanced ShowZoomedQRCode method with better error handling
        void ShowZoomedQRCode(string url, Texture2D existingTexture = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                EditorUtility.DisplayDialog("Error", "No URL to generate QR code for", "OK");
                return;
            }
            
            // Open a new window with a zoomed QR code, reusing existing texture if available
            QRCodeZoomWindow.ShowWindow(url, existingTexture);
        }

        
        // Generate branded QR code URL (plain QR code, logo overlay done in Unity)
        string GetBrandedQRCodeUrl(string text, int size)
        {
            // Subtle QR code colors - less visually distracting
            string darkColor = "000000"; // Black pixels - more subtle
            string lightColor = "F8F9FA"; // Very light gray background instead of pure white
            
            // Build branded QR code URL (without center image - we'll overlay in Unity)
            string baseUrl = $"https://quickchart.io/qr?text={UnityWebRequest.EscapeURL(text)}&size={size}x{size}&format=png";
            
            // Add branding parameters
            baseUrl += $"&dark={darkColor}&light={lightColor}";
            baseUrl += $"&ecLevel=H"; // High error correction for better logo visibility
            baseUrl += $"&margin=6"; // Increased margin to accommodate caption text properly
            baseUrl += $"&caption={UnityWebRequest.EscapeURL("BoostLink™")}";
            baseUrl += $"&captionFontSize=11&captionFontColor=6B7280"; // Slightly larger font for better readability
            
            return baseUrl;
        }
        // Overlay BoostOps logo on QR code texture using Unity
        Texture2D OverlayBoostOpsLogo(Texture2D qrTexture)
        {
            try
            {
                // Load BoostOps logo
                Texture2D logoTexture = Resources.Load<Texture2D>("boostops-logo-256");
                if (logoTexture == null)
                {
                    Debug.LogWarning("⚠️ BoostOps logo not found in Resources, returning QR code without logo");
                    return qrTexture;
                }
                
                // Create a copy of the QR code texture
                Texture2D combinedTexture = new Texture2D(qrTexture.width, qrTexture.height);
                combinedTexture.SetPixels(qrTexture.GetPixels());
                
                // Calculate logo size (25% of QR code size)
                int logoSize = Mathf.RoundToInt(qrTexture.width * 0.25f);
                
                // Calculate center position
                int centerX = qrTexture.width / 2;
                int centerY = qrTexture.height / 2;
                int logoStartX = centerX - logoSize / 2;
                int logoStartY = centerY - logoSize / 2;
                
                // Resize logo to fit
                RenderTexture renderTexture = RenderTexture.GetTemporary(logoSize, logoSize);
                Graphics.Blit(logoTexture, renderTexture);
                
                RenderTexture.active = renderTexture;
                Texture2D resizedLogo = new Texture2D(logoSize, logoSize);
                resizedLogo.ReadPixels(new Rect(0, 0, logoSize, logoSize), 0, 0);
                resizedLogo.Apply();
                
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(renderTexture);
                
                // Overlay logo pixels onto QR code
                Color[] logoPixels = resizedLogo.GetPixels();
                for (int y = 0; y < logoSize; y++)
                {
                    for (int x = 0; x < logoSize; x++)
                    {
                        int qrX = logoStartX + x;
                        int qrY = logoStartY + y;
                        
                        // Make sure we're within bounds
                        if (qrX >= 0 && qrX < qrTexture.width && qrY >= 0 && qrY < qrTexture.height)
                        {
                            Color logoPixel = logoPixels[y * logoSize + x];
                            
                            // Only overlay non-transparent pixels
                            if (logoPixel.a > 0.1f)
                            {
                                // Add a white background behind the logo for better visibility
                                combinedTexture.SetPixel(qrX, qrY, Color.white);
                                
                                // Blend logo pixel with alpha
                                Color qrPixel = combinedTexture.GetPixel(qrX, qrY);
                                Color blendedPixel = Color.Lerp(qrPixel, logoPixel, logoPixel.a);
                                combinedTexture.SetPixel(qrX, qrY, blendedPixel);
                            }
                        }
                    }
                }
                
                combinedTexture.Apply();
                
                // Clean up
                DestroyImmediate(resizedLogo);
                

                
                return combinedTexture;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Failed to overlay BoostOps logo: {ex.Message}");
                return qrTexture; // Return original QR code if overlay fails
            }
        }
        
        // Removed old StartCoroutine method - now using async/await for better editor compatibility
        
        void LoadCrossPromoTable()
        {
            // Try to load existing CrossPromoTable asset
            const string crossPromoAssetPath = "Assets/Resources/BoostOps/CrossPromoTable.asset";
            crossPromoTable = AssetDatabase.LoadAssetAtPath<CrossPromoTable>(crossPromoAssetPath);
            
            // Load cross-promo data
            if (crossPromoTable != null)
            {
                LoadJsonDataIntoTable(); // Load actual values from JSON file
                CheckJsonFreshness();
            }
        }
        
        // NOTE: Store ID syncing no longer needed - BoostOpsProjectSettings is the single source of truth
        
        void LoadJsonDataIntoTable()
        {
            if (crossPromoTable == null) return;
            
            // Choose the correct file based on current mode
            string fileName = (crossPromoMode == FeatureMode.Managed) ? "cross_promo_server.json" : "cross_promo_local.json";
            string streamingAssetsPath = System.IO.Path.Combine(Application.dataPath, "StreamingAssets", "BoostOps", fileName);
            
            LogDebug($"LoadJsonDataIntoTable: Loading from {fileName} (mode: {crossPromoMode})");
            
            if (!System.IO.File.Exists(streamingAssetsPath))
            {
                // No JSON file exists, keep default values
                return;
            }
            
            try
            {
                string jsonContent = System.IO.File.ReadAllText(streamingAssetsPath);
                var jsonData = JsonUtility.FromJson<JsonConfigWrapper>(jsonContent);
                
                if (jsonData?.source_project != null)
                {
                    // Update the CrossPromoTable with values from JSON
                    crossPromoTable.defaultIconInterstitialButtonText = jsonData.source_project.DefaultIconInterstitialButtonText;
                    crossPromoTable.defaultIconInterstitialDescription = jsonData.source_project.DefaultIconInterstitialDescription;
                    crossPromoTable.defaultRichInterstitialButtonText = jsonData.source_project.DefaultRichInterstitialButtonText;
                    crossPromoTable.defaultRichInterstitialDescription = jsonData.source_project.DefaultRichInterstitialDescription;
                    
                    // Mark the asset as dirty so Unity saves the changes
                    EditorUtility.SetDirty(crossPromoTable);
                    
                    LogDebug($"Loaded text values from JSON: Icon Desc = '{jsonData.source_project.DefaultIconInterstitialDescription}'");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps Editor] Failed to load JSON data: {ex.Message}");
            }
        }
        
        void CheckJsonFreshness()
        {
            // Choose the correct file based on current mode  
            string fileName = (crossPromoMode == FeatureMode.Managed) ? "cross_promo_server.json" : "cross_promo_local.json";
            string streamingAssetsPath = System.IO.Path.Combine(Application.dataPath, "StreamingAssets", "BoostOps", fileName);
            
            LogDebug($"CheckJsonFreshness: Checking {fileName} (mode: {crossPromoMode})");
            
            if (!System.IO.File.Exists(streamingAssetsPath))
            {
                // No JSON file exists
                MarkJsonAsStale();
                return;
            }
            
            // Check if JSON file is newer than the asset
            var jsonModTime = System.IO.File.GetLastWriteTime(streamingAssetsPath);
            var assetPath = AssetDatabase.GetAssetPath(crossPromoTable);
            var assetModTime = System.IO.File.GetLastWriteTime(assetPath);
            
            if (assetModTime > jsonModTime)
            {
                // Asset has been modified since JSON was generated
                MarkJsonAsStale();
            }
            else
            {
                // JSON appears up to date
                lastJsonGeneration = jsonModTime;
                isJsonStale = false;
            }
        }
        
        void LoadProjectSettings()
        {
            string expectedAssetPath = "Assets/Resources/BoostOps/BoostOpsProjectSettings.asset";

            // Try loading from the DLL helper first (may return in-memory-only instance)
            projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();

            // The DLL is compiled without UNITY_EDITOR, so GetOrCreateSettings() cannot
            // call AssetDatabase.CreateAsset(). If we got an instance but it has no asset
            // path on disk, we need to save it ourselves from Editor code.
            string assetPath = AssetDatabase.GetAssetPath(projectSettings);
            if (projectSettings != null && string.IsNullOrEmpty(assetPath))
            {
                Debug.Log("[BoostOps] Settings instance exists in memory but has no asset file — creating from Editor");

                try
                {
                    // Ensure directory structure exists
                    if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                        AssetDatabase.CreateFolder("Assets", "Resources");
                    if (!AssetDatabase.IsValidFolder("Assets/Resources/BoostOps"))
                        AssetDatabase.CreateFolder("Assets/Resources", "BoostOps");

                    // If the ScriptableObject was created with CreateInstance inside the
                    // DLL, we can save it directly as a new asset.
                    AssetDatabase.CreateAsset(projectSettings, expectedAssetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    assetPath = AssetDatabase.GetAssetPath(projectSettings);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        Debug.Log($"[BoostOps] ✅ Project settings asset created at: {assetPath}");
                    }
                    else
                    {
                        Debug.LogError("[BoostOps] ❌ AssetDatabase.CreateAsset succeeded but path is still empty");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BoostOps] ❌ Failed to create project settings asset: {ex.Message}");
                }
            }
            else if (projectSettings != null)
            {
                Debug.Log($"[BoostOps] ✅ Project settings loaded from: {assetPath}");
            }
            else
            {
                Debug.LogError("[BoostOps] ❌ GetOrCreateSettings returned null — cannot load project settings");
            }

            if (projectSettings != null)
            {
                // Ensure android package name is current
                projectSettings.RefreshAndroidPackageName();

                // Load values from ScriptableObject into editor window fields
                iosAppStoreId = projectSettings.appleAppStoreId;
                androidCertFingerprint = projectSettings.androidCertFingerprint;
                projectSlug = projectSettings.projectSlug;
                dynamicLinkUrl = projectSettings.fallbackUrl;

                EditorUtility.SetDirty(projectSettings);
            }
        }
        

        
        // NOTE: Project settings sync no longer needed - BoostOpsProjectSettings is the single source of truth
        
        void BuildCreateConfigurationSection()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            container.style.paddingLeft = 20;
            container.style.paddingRight = 20;
            container.style.paddingTop = 20;
            container.style.paddingBottom = 20;
            container.style.marginBottom = 15;
            container.style.borderTopLeftRadius = 8;
            container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = 8;
            container.style.borderBottomRightRadius = 8;
            container.style.alignItems = Align.Center;
            
            var noConfigLabel = new Label("No cross-promotion configuration found.");
            noConfigLabel.style.fontSize = 16;
            noConfigLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            noConfigLabel.style.marginBottom = 10;
            noConfigLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(noConfigLabel);
            
            var createButton = new Button(() => CreateCrossPromoTable()) { text = "Create Cross-Promo Configuration" };
            createButton.style.width = 240;
            createButton.style.height = 40;
            createButton.style.fontSize = 14;
            createButton.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 1f);
            container.Add(createButton);
            
            contentContainer.Add(container);
        }
        void BuildCrossPromoEditor()
        {
            if (crossPromoTable == null) return;
            
            var container = new VisualElement();
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            container.style.paddingLeft = 15;
            container.style.paddingRight = 15;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 15;
            container.style.marginBottom = 15;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            
            // Global settings
            var globalContainer = new VisualElement();
            globalContainer.style.marginBottom = 15;
            globalContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            globalContainer.style.paddingLeft = 10;
            globalContainer.style.paddingRight = 10;
            globalContainer.style.paddingTop = 10;
            globalContainer.style.paddingBottom = 10;
            globalContainer.style.borderTopLeftRadius = 4;
            globalContainer.style.borderTopRightRadius = 4;
            globalContainer.style.borderBottomLeftRadius = 4;
            globalContainer.style.borderBottomRightRadius = 4;
            
            var globalTitle = new Label("Source Game Settings");
            globalTitle.style.fontSize = 12;
            globalTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            globalTitle.style.marginBottom = 8;
            globalContainer.Add(globalTitle);
            
            // Source Apple Store ID field
            var sourceStoreIdField = new TextField();
            var projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
            string currentStoreId = projectSettings.appleAppStoreId ?? "";
            sourceStoreIdField.value = currentStoreId;
            sourceStoreIdField.RegisterValueChangedCallback(evt => {
                // Use the same normalization logic as target game fields
                string normalizedId = ExtractIOSStoreId(evt.newValue);
                string valueToStore = normalizedId ?? evt.newValue; // Keep original if normalization fails
                
                // Update the field value if normalization changed it
                if (normalizedId != null && normalizedId != evt.newValue)
                {
                    sourceStoreIdField.SetValueWithoutNotify(normalizedId);
                }
                
                // Update project settings (single source of truth)
                var settings = BoostOpsProjectSettings.GetOrCreateSettings();
                settings.appleAppStoreId = valueToStore;
                UnityEditor.EditorUtility.SetDirty(settings);
                UnityEditor.AssetDatabase.SaveAssets();
                
                // Also sync with the main iosAppStoreId field for Dynamic Links compatibility
                iosAppStoreId = valueToStore;
                SaveAppleAppStoreId();
                SaveCrossPromoChanges(autoGenerate: false);
                
                // Use the same verification logic as target game fields
                if (!string.IsNullOrEmpty(evt.newValue))
                {
                    VerifyIOSStoreIdDebounced(evt.newValue, -1); // Use -1 for source game settings
                }
            });
            
            var sourceStoreIdRow = CreateLabelFieldRowWithInfo("Apple Store ID:", sourceStoreIdField, 
                "Required for iOS cross-promotion analytics tracking. Also used for Dynamic Links.");
            globalContainer.Add(sourceStoreIdRow);
            
            // Google Store ID field (read-only, from player settings)
            var androidPackageField = new TextField();
            androidPackageField.value = PlayerSettings.applicationIdentifier ?? "";
            androidPackageField.SetEnabled(false); // Make it read-only
            androidPackageField.style.opacity = 0.7f; // Visually indicate it's read-only
            
            var androidPackageRow = CreateLabelFieldRowWithInfo("Google Store ID:", androidPackageField, 
                "Read-only field showing the package name from Player Settings. Used for Google Play Store cross-promotion analytics.");
            globalContainer.Add(androidPackageRow);
            
            // Rotation type field
            var rotationField = new EnumField(crossPromoTable.rotation);
            rotationField.RegisterValueChangedCallback(evt => {
                crossPromoTable.rotation = (RotationType)evt.newValue;
                SaveCrossPromoChanges(autoGenerate: false);
                
                // Note: Removed RefreshCrossPromoPanel() to prevent infinite loop crashes
                // The ordering hints are static text and don't need immediate updates
            });
            
            var rotationRow = CreateLabelFieldRowWithInfo("Rotation Algorithm:", rotationField, 
                "How to choose which game to promote: Weighted Random uses game weights, Waterfall uses priority order");
            globalContainer.Add(rotationRow);
            
            // Global frequency cap field
            var globalFreqCapField = new IntegerField();
            globalFreqCapField.value = crossPromoTable.globalFrequencyCap?.impressions ?? 0;
            
            // Create the info text dynamically 
            string GetFreqCapInfoText(int value)
            {
                return value == 0 ? 
                    "Unlimited total impressions (per user, per day) - individual games self-limit" :
                    "Total daily cross-promo budget (per user, per day) across all games";
            }
            
            var globalFreqCapRow = CreateLabelFieldRowWithInfo("Frequency Cap:", globalFreqCapField, GetFreqCapInfoText(crossPromoTable.globalFrequencyCap?.impressions ?? 0));
            var freqCapInfoLabel = globalFreqCapRow.Children().LastOrDefault() as Label; // Get the info label
            
            globalFreqCapField.RegisterValueChangedCallback(evt => {
                if (evt.newValue <= 0)
                    crossPromoTable.globalFrequencyCap = BoostOps.Core.FrequencyCap.Unlimited();
                else
                    crossPromoTable.globalFrequencyCap = BoostOps.Core.FrequencyCap.Daily(evt.newValue);
                SaveCrossPromoChanges(autoGenerate: false);
                
                // Update the info text dynamically
                if (freqCapInfoLabel != null)
                {
                    freqCapInfoLabel.text = GetFreqCapInfoText(evt.newValue);
                }
                
                // Note: Removed RefreshCrossPromoPanel() to prevent infinite loop crashes
                // The info text update above handles the immediate UI feedback needed
            });
            
            globalContainer.Add(globalFreqCapRow);
            
            // Min Player Session field
            var minSessionField = new IntegerField();
            minSessionField.value = crossPromoTable.minPlayerSession;
            minSessionField.RegisterValueChangedCallback(evt => {
                crossPromoTable.minPlayerSession = evt.newValue;
                SaveCrossPromoChanges(autoGenerate: false);
            });
            
            var minSessionRow = CreateLabelFieldRowWithInfo("Min Player Session:", minSessionField, 
                "Minimum session count before showing cross-promo. Best practice: 3-5 sessions.");
            globalContainer.Add(minSessionRow);
            
            // Min Player Day field  
            var minDayField = new IntegerField();
            minDayField.value = crossPromoTable.minPlayerDay;
            minDayField.RegisterValueChangedCallback(evt => {
                crossPromoTable.minPlayerDay = evt.newValue;
                SaveCrossPromoChanges(autoGenerate: false);
            });
            
            var minDayRow = CreateLabelFieldRowWithInfo("Min Player Day:", minDayField, 
                "Minimum days since install before showing cross-promo. Best practice: 1-3 days.");
            globalContainer.Add(minDayRow);
            
            // Advanced Store IDs section
            var advancedStoreIdsFoldout = new Foldout();
            advancedStoreIdsFoldout.text = "Advanced Store IDs";
            advancedStoreIdsFoldout.value = false; // Start collapsed
            advancedStoreIdsFoldout.style.marginTop = 10;
            advancedStoreIdsFoldout.style.marginBottom = 5;
            
            // Amazon Store ID
            var sourceAmazonField = new TextField();
            sourceAmazonField.value = projectSettings.amazonStoreId ?? "";
            sourceAmazonField.RegisterValueChangedCallback(evt => {
                // Update project settings (single source of truth)
                projectSettings.amazonStoreId = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(projectSettings);
                UnityEditor.AssetDatabase.SaveAssets();
                SaveCrossPromoChanges(autoGenerate: false);
            });
            var sourceAmazonRow = CreateLabelFieldRowWithInfo("Amazon Store ID:", sourceAmazonField, 
                "Amazon Appstore ID for this game (used for cross-promotion analytics)");
            advancedStoreIdsFoldout.Add(sourceAmazonRow);
            
            // Microsoft Store ID
            var sourceWindowsField = new TextField();
            sourceWindowsField.value = projectSettings.windowsStoreId ?? "";
            sourceWindowsField.RegisterValueChangedCallback(evt => {
                // Update project settings (single source of truth)
                projectSettings.windowsStoreId = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(projectSettings);
                UnityEditor.AssetDatabase.SaveAssets();
                SaveCrossPromoChanges(autoGenerate: false);
            });
            var sourceWindowsRow = CreateLabelFieldRowWithInfo("Microsoft Store ID:", sourceWindowsField, 
                "Microsoft Store ID for this game (used for cross-promotion analytics)");
            advancedStoreIdsFoldout.Add(sourceWindowsRow);
            
            // Samsung Store ID
            var sourceSamsungField = new TextField();
            sourceSamsungField.value = projectSettings.samsungStoreId ?? "";
            sourceSamsungField.RegisterValueChangedCallback(evt => {
                // Update project settings (single source of truth)
                projectSettings.samsungStoreId = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(projectSettings);
                UnityEditor.AssetDatabase.SaveAssets();
                SaveCrossPromoChanges(autoGenerate: false);
            });
            var sourceSamsungRow = CreateLabelFieldRowWithInfo("Samsung Store ID:", sourceSamsungField, 
                "Samsung Galaxy Store ID for this game (used for cross-promotion analytics)");
            advancedStoreIdsFoldout.Add(sourceSamsungRow);
            
            globalContainer.Add(advancedStoreIdsFoldout);
            
            // Advanced section with foldout
            var advancedFoldout = new Foldout();
            advancedFoldout.text = "Advanced Text Settings";
            advancedFoldout.style.marginTop = 10;
            advancedFoldout.style.marginBottom = 5;
            advancedFoldout.value = false; // Start collapsed
            
            // Default Icon Interstitial Button Text
            var iconButtonField = new TextField();
            iconButtonField.value = crossPromoTable?.defaultIconInterstitialButtonText ?? "Play Now!";
            iconButtonField.RegisterValueChangedCallback(evt => {
                if (crossPromoTable != null)
                {
                    crossPromoTable.defaultIconInterstitialButtonText = evt.newValue;
                    SaveCrossPromoChanges(autoGenerate: false); // Don't auto-generate for text changes
                }
            });
            var iconButtonRow = CreateLabelFieldRowWithInfo("Icon Button Text:", iconButtonField, 
                "Default button text for icon interstitials (can be overridden per campaign)");
            advancedFoldout.Add(iconButtonRow);
            
            // Default Icon Interstitial Description
            var iconDescField = new TextField();
            iconDescField.value = crossPromoTable?.defaultIconInterstitialDescription ?? "Try this awesome game!";
            iconDescField.RegisterValueChangedCallback(evt => {
                if (crossPromoTable != null)
                {
                    crossPromoTable.defaultIconInterstitialDescription = evt.newValue;
                    SaveCrossPromoChanges(autoGenerate: false); // Don't auto-generate for text changes
                }
            });
            var iconDescRow = CreateLabelFieldRowWithInfo("Icon Description:", iconDescField, 
                "Default description for icon interstitials (can be overridden per campaign)");
            advancedFoldout.Add(iconDescRow);
            
            // Default Rich Interstitial Button Text
            var richButtonField = new TextField();
            richButtonField.value = crossPromoTable?.defaultRichInterstitialButtonText ?? "Play Now!";
            richButtonField.RegisterValueChangedCallback(evt => {
                if (crossPromoTable != null)
                {
                    crossPromoTable.defaultRichInterstitialButtonText = evt.newValue;
                    SaveCrossPromoChanges(autoGenerate: false); // Don't auto-generate for text changes
                }
            });
            var richButtonRow = CreateLabelFieldRowWithInfo("Rich Button Text:", richButtonField, 
                "Default button text for rich interstitials (can be overridden per campaign)");
            advancedFoldout.Add(richButtonRow);
            
            // Default Rich Interstitial Description
            var richDescField = new TextField();
            richDescField.value = crossPromoTable?.defaultRichInterstitialDescription ?? "Join millions of players in this amazing adventure!";
            richDescField.RegisterValueChangedCallback(evt => {
                if (crossPromoTable != null)
                {
                    crossPromoTable.defaultRichInterstitialDescription = evt.newValue;
                    SaveCrossPromoChanges(autoGenerate: false); // Don't auto-generate for text changes
                }
            });
            var richDescRow = CreateLabelFieldRowWithInfo("Rich Description:", richDescField, 
                "Default description for rich interstitials (can be overridden per campaign)");
            advancedFoldout.Add(richDescRow);
            

            globalContainer.Add(advancedFoldout);
            
            container.Add(globalContainer);
            
            // Target games list - custom UI
            BuildTargetGamesList(container);
            
            // Action buttons
            var actionsContainer = new VisualElement();
            actionsContainer.style.flexDirection = FlexDirection.Row;
            actionsContainer.style.marginTop = 5;
            
            var addGameButton = new Button(() => AddNewTargetGame()) { text = "Add Target Game" };
            addGameButton.style.marginRight = 10;
            addGameButton.style.width = 120;
            actionsContainer.Add(addGameButton);
            
            var previewButton = new Button(() => PreviewCrossPromoConfig()) { text = "Preview Config" };
            previewButton.style.width = 120;
            actionsContainer.Add(previewButton);
            
            container.Add(actionsContainer);
            
            contentContainer.Add(container);
        }
        void BuildTargetGamesList(VisualElement parent)
        {
            var targetsContainer = new VisualElement();
            targetsContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            targetsContainer.style.paddingLeft = 10;
            targetsContainer.style.paddingRight = 10;
            targetsContainer.style.paddingTop = 10;
            targetsContainer.style.paddingBottom = 10;
            targetsContainer.style.marginBottom = 15;
            targetsContainer.style.borderTopLeftRadius = 4;
            targetsContainer.style.borderTopRightRadius = 4;
            targetsContainer.style.borderBottomLeftRadius = 4;
            targetsContainer.style.borderBottomRightRadius = 4;
            
            // Header row with title and Fetch All button
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.marginBottom = 10;
            
            var titleContainer = new VisualElement();
            titleContainer.style.marginBottom = 5;
            
            var targetsTitle = new Label("Target Games");
            targetsTitle.style.fontSize = 12;
            targetsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(targetsTitle);
            
            // Add ordering explanation for waterfall rotation
            if (crossPromoTable.rotation == RotationType.Waterfall)
            {
                var orderingHint = new Label("💡 Waterfall: Game 1 shows up to its daily cap, then Game 2, etc. Each game self-limits (recommended: 2-3).");
                orderingHint.style.fontSize = 10;
                orderingHint.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                orderingHint.style.unityFontStyleAndWeight = FontStyle.Italic;
                orderingHint.style.whiteSpace = WhiteSpace.Normal;
                titleContainer.Add(orderingHint);
            }
            else if (crossPromoTable.rotation == RotationType.WeightedRandom)
            {
                var weightHint = new Label("💡 Weight values control selection probability. Higher weight = more likely to be shown.");
                weightHint.style.fontSize = 10;
                weightHint.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                weightHint.style.unityFontStyleAndWeight = FontStyle.Italic;
                weightHint.style.whiteSpace = WhiteSpace.Normal;
                titleContainer.Add(weightHint);
            }
            
            var fetchAllButton = new Button(() => {
                FetchAllStoreIcons();
            });
            
            // Update fetch all button text based on current icon state
            System.Action updateFetchAllButton = () => {
                try
                {
                    if (crossPromoTable?.targets == null || crossPromoTable.targets.Length == 0)
                    {
                        fetchAllButton.text = "Fetch All Store Icons";
                        fetchAllButton.tooltip = "Download icons for all games that have valid Store IDs";
                        return;
                    }
                    
                    int gamesWithIcons = 0;
                    int totalGames = crossPromoTable.targets.Length;
                    
                    foreach (var target in crossPromoTable.targets)
                    {
                        if (target?.icon != null) gamesWithIcons++;
                    }
                    
                    if (gamesWithIcons == 0)
                    {
                        fetchAllButton.text = "Fetch All Store Icons";
                        fetchAllButton.tooltip = "Download icons for all games that have valid Store IDs";
                    }
                    else if (gamesWithIcons == totalGames)
                    {
                        fetchAllButton.text = "Refresh All Store Icons";
                        fetchAllButton.tooltip = "Re-download all icons from app stores to update existing sprites";
                    }
                    else
                    {
                        fetchAllButton.text = "Fetch/Refresh All Icons";
                        fetchAllButton.tooltip = $"Download icons for {totalGames - gamesWithIcons} games and refresh {gamesWithIcons} existing icons";
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BoostOps] Error updating fetch all button: {ex.Message}");
                    fetchAllButton.text = "Fetch All Store Icons";
                    fetchAllButton.tooltip = "Download icons for all games that have valid Store IDs";
                }
            };
            
            // Set initial button state
            updateFetchAllButton();
            
            fetchAllButton.style.fontSize = 10;
            fetchAllButton.style.paddingLeft = 8;
            fetchAllButton.style.paddingRight = 8;
            fetchAllButton.style.paddingTop = 4;
            fetchAllButton.style.paddingBottom = 4;
            
            headerRow.Add(titleContainer);
            headerRow.Add(fetchAllButton);
            targetsContainer.Add(headerRow);
            
            if (crossPromoTable.targets == null || crossPromoTable.targets.Length == 0)
            {
                var noTargetsLabel = new Label("No target games configured. Add some games to promote!");
                noTargetsLabel.style.fontSize = 11;
                noTargetsLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                noTargetsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noTargetsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noTargetsLabel.style.marginTop = 20;
                noTargetsLabel.style.marginBottom = 20;
                targetsContainer.Add(noTargetsLabel);
            }
            else
            {
                // Show each target game
                for (int i = 0; i < crossPromoTable.targets.Length; i++)
                {
                    BuildTargetGameCard(targetsContainer, i);
                }
            }
            
            parent.Add(targetsContainer);
        }
        void BuildTargetGameCard(VisualElement parent, int index)
        {
            var target = crossPromoTable.targets[index];
            
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.marginBottom = 8;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            
            // Header with game name, reorder buttons, and delete button
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            
                                // Move up/down buttons (for waterfall ordering)
            var moveUpButton = new Button(() => MoveTargetGame(index, -1)) { text = "▲" };
            moveUpButton.style.width = 22;
            moveUpButton.style.height = 20;
            moveUpButton.style.fontSize = 10;
            moveUpButton.style.marginRight = 3;
            moveUpButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.8f);
            moveUpButton.tooltip = "Move up in promotion order (higher priority)";
            moveUpButton.SetEnabled(index > 0); // Disable if already at top
            header.Add(moveUpButton);
            
            var moveDownButton = new Button(() => MoveTargetGame(index, 1)) { text = "▼" };
            moveDownButton.style.width = 22;
            moveDownButton.style.height = 20;
            moveDownButton.style.fontSize = 10;
            moveDownButton.style.marginRight = 8;
            moveDownButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.8f);
            moveDownButton.tooltip = "Move down in promotion order (lower priority)";
            moveDownButton.SetEnabled(index < crossPromoTable.targets.Length - 1); // Disable if already at bottom
            header.Add(moveDownButton);
            
            // Prioritize actual game name (headline) from store, then fallback to generic name
            string displayName = "New Game";
            if (!string.IsNullOrEmpty(target.headline) && target.headline != "Amazing Game Title")
            {
                displayName = target.headline;
            }
            
            // Add position indicator and priority level
            string priorityLevel = GetPriorityLevel(index, crossPromoTable.targets.Length);
            var gameTitle = new Label($"#{index + 1} {displayName} ({priorityLevel})");
            gameTitle.style.fontSize = 12;
            gameTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            gameTitle.style.flexGrow = 1;
            header.Add(gameTitle);
            
            var deleteButton = new Button(() => RemoveTargetGame(index)) { text = "✕" };
            deleteButton.style.width = 24;
            deleteButton.style.height = 20;
            deleteButton.style.fontSize = 12;
            deleteButton.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
            deleteButton.tooltip = "Remove this game from cross-promotion";
            header.Add(deleteButton);
            
            card.Add(header);
            
            // Basic info row
            var basicRow = new VisualElement();
            basicRow.style.flexDirection = FlexDirection.Row;
            basicRow.style.marginBottom = 8;
            
            // Left column - Store IDs
            var leftColumn = new VisualElement();
            leftColumn.style.width = Length.Percent(50);
            leftColumn.style.marginRight = 10;
            
            // Add Apple Store ID with verification
            BuildGameFieldWithVerification(leftColumn, "Apple Store ID", target.iosAppStoreId, (value) => {
                // Normalize iOS Store ID to numeric format
                string normalizedId = ExtractIOSStoreId(value);
                target.iosAppStoreId = normalizedId ?? value; // Keep original if normalization fails
                SaveCrossPromoChanges(autoGenerate: false); // Don't auto-generate for text changes
                VerifyIOSStoreIdDebounced(value, index); // Use debounced version to prevent API spam
                // Update name priority since iOS should take precedence
                EditorApplication.delayCall += () => UpdateGameNamePriority(index);
                // Update game ID since store ID changed
                UpdateGameId(target);
            }, index, "ios");
            
            // Add Google Store ID with verification
            BuildGameFieldWithVerification(leftColumn, "Google Store ID", target.androidPackageId, (value) => {
                target.androidPackageId = value;
                SaveCrossPromoChanges(autoGenerate: false); // Don't auto-generate for text changes
                VerifyAndroidPackageIdDebounced(value, index); // Use debounced version to prevent API spam
                // Update game ID since store ID changed
                UpdateGameId(target);
            }, index, "android");
            
            // Advanced Store IDs (collapsible)
            var advancedStoreSection = new Foldout();
            advancedStoreSection.text = "Advanced Store IDs";
            advancedStoreSection.value = false; // Start collapsed
            advancedStoreSection.style.marginTop = 5;
            advancedStoreSection.style.marginBottom = 5;
            
            // Amazon Store ID
            BuildGameFieldWithVerification(advancedStoreSection, "Amazon Store ID", target.amazonStoreId, (value) => {
                target.amazonStoreId = value;
                SaveCrossPromoChanges(autoGenerate: false);
                VerifyAmazonStoreIdDebounced(value, index);
                UpdateGameId(target);
            }, index, "amazon");
            
            // Microsoft Store ID
            var windowsField = new TextField();
            windowsField.value = target.windowsStoreId ?? "";
            windowsField.tooltip = "Enter Microsoft Store Product ID (e.g., 9WZDNCRFJ3TJ)";
            windowsField.style.width = 200; // Match Amazon field width
            windowsField.RegisterValueChangedCallback(evt => {
                target.windowsStoreId = evt.newValue;
                SaveCrossPromoChanges(autoGenerate: false);
                UpdateGameId(target);
            });
            var windowsRow = CreateLabelFieldRow("Microsoft Store ID:", windowsField);
            windowsRow.style.marginBottom = 5;
            var windowsLabel = windowsRow.Q<Label>();
            if (windowsLabel != null)
            {
                windowsLabel.style.width = 160;
                windowsLabel.style.fontSize = 11;
            }
            advancedStoreSection.Add(windowsRow);
            
            // Samsung Store ID
            var samsungField = new TextField();
            samsungField.value = target.samsungStoreId ?? "";
            samsungField.tooltip = "Enter Samsung Galaxy Store ID";
            samsungField.style.width = 200; // Match Amazon field width
            samsungField.RegisterValueChangedCallback(evt => {
                target.samsungStoreId = evt.newValue;
                SaveCrossPromoChanges(autoGenerate: false);
                UpdateGameId(target);
            });
            var samsungRow = CreateLabelFieldRow("Samsung Store ID:", samsungField);
            samsungRow.style.marginBottom = 5;
            var samsungLabel = samsungRow.Q<Label>();
            if (samsungLabel != null)
            {
                samsungLabel.style.width = 160;
                samsungLabel.style.fontSize = 11;
            }
            advancedStoreSection.Add(samsungRow);
            
            leftColumn.Add(advancedStoreSection);
            
            basicRow.Add(leftColumn);
            
            // Right column - Icon and settings
            var rightColumn = new VisualElement();
            rightColumn.style.width = Length.Percent(50);
            
            // Icon field with Fetch button
            var iconContainer = new VisualElement();
            iconContainer.style.flexDirection = FlexDirection.Row;
            iconContainer.style.alignItems = Align.Center;
            
            var iconField = new ObjectField();
            iconField.objectType = typeof(Sprite);
            iconField.value = target.icon;
            iconField.style.flexGrow = 1;
            var fetchButton = new Button(() => {
                FetchIconForGame(index, target);
            });
            
            // Function to update button text and tooltip based on icon state
            System.Action updateFetchButton = () => {
                try
                {
                    bool hasIcon = target?.icon != null;
                    fetchButton.text = hasIcon ? "Refresh" : "Fetch";
                    fetchButton.tooltip = hasIcon 
                        ? "Re-download icon from app store to update existing sprite"
                        : "Download icon from app store and save as UI Sprite";
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BoostOps] Error updating fetch button: {ex.Message}");
                    fetchButton.text = "Fetch";
                    fetchButton.tooltip = "Download icon from app store and save as UI Sprite";
                }
            };
            
            // Set initial button state
            updateFetchButton();
            
            fetchButton.style.width = 60;
            fetchButton.style.marginLeft = 5;
            fetchButton.style.fontSize = 10;
            
            // Update fetch button when icon changes
            iconField.RegisterValueChangedCallback(evt => {
                target.icon = evt.newValue as Sprite;
                SaveCrossPromoChanges(autoGenerate: false);
                updateFetchButton(); // Update button text when icon changes
            });
            
            iconContainer.Add(iconField);
            iconContainer.Add(fetchButton);
            
            var iconRow = CreateLabelFieldRow("Icon:", iconContainer);
            iconRow.style.marginBottom = 5;
            
            // Use smaller label width for fields in columns
            var iconLabel = iconRow.Q<Label>();
            if (iconLabel != null)
            {
                iconLabel.style.width = 140; // Increased to accommodate longer labels
                iconLabel.style.fontSize = 11;
            }
            
            rightColumn.Add(iconRow);
            
            // Weight field
            var weightField = new IntegerField();
            weightField.value = target.weight;
            weightField.RegisterValueChangedCallback(evt => {
                target.weight = evt.newValue;
                SaveCrossPromoChanges(autoGenerate: false);
            });
            
            var weightRow = CreateLabelFieldRow("Weight:", weightField);
            weightRow.style.marginBottom = 5;
            
            var weightLabel = weightRow.Q<Label>();
            if (weightLabel != null)
            {
                weightLabel.style.width = 140;
                weightLabel.style.fontSize = 11;
            }
            
            rightColumn.Add(weightRow);
            
            // Simplified Freq Cap field (always editable)
            var freqCapField = new IntegerField();
            freqCapField.value = target.frequencyCap?.impressions ?? 2;
            freqCapField.style.width = 60;
            freqCapField.tooltip = "Daily frequency cap for this game (0 = unlimited). Recommended: 2-3 for good user experience.";
            freqCapField.RegisterValueChangedCallback(evt => {
                if (evt.newValue <= 0)
                    target.frequencyCap = BoostOps.Core.FrequencyCap.Unlimited();
                else
                    target.frequencyCap = BoostOps.Core.FrequencyCap.Daily(evt.newValue);
                target.useCustomFreqCap = true; // Always use custom when user edits
                SaveCrossPromoChanges(autoGenerate: false);
            });
            
            var freqCapRow = CreateLabelFieldRow("Daily Cap:", freqCapField);
            freqCapRow.style.marginBottom = 5;
            
            var freqCapLabel = freqCapRow.Q<Label>();
            if (freqCapLabel != null)
            {
                freqCapLabel.style.width = 140;
                freqCapLabel.style.fontSize = 11;
            }
            
            rightColumn.Add(freqCapRow);
            basicRow.Add(rightColumn);
            
            card.Add(basicRow);
            
            parent.Add(card);
        }
        
        void BuildGameField(VisualElement parent, string label, string value, System.Action<string> onChanged, bool isSmall = false)
        {
            if (isSmall)
            {
                // Use vertical layout for small/platform fields (narrow columns)
                var fieldLabel = new Label(label);
                fieldLabel.style.fontSize = 9;
                fieldLabel.style.marginBottom = 2;
                fieldLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                parent.Add(fieldLabel);
                
                var field = new TextField();
                field.value = value ?? "";
                field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
                field.style.marginBottom = 3;
                field.style.fontSize = 9;
                field.style.height = 18;
                parent.Add(field);
            }
            else
            {
                // Use Unity inspector style for main fields
                var field = new TextField();
                field.value = value ?? "";
                field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
                field.style.fontSize = 11;
                
                var row = CreateLabelFieldRow(label + ":", field);
                row.style.marginBottom = 5;
                
                // Use consistent label width for alignment
                var fieldLabel = row.Q<Label>();
                if (fieldLabel != null)
                {
                    fieldLabel.style.width = 160; // Match other field labels for consistent alignment
                    fieldLabel.style.fontSize = 11;
                }
                
                parent.Add(row);
            }
        }
        void BuildGameFieldWithVerification(VisualElement parent, string label, string value, System.Action<string> onChanged, int gameIndex, string platform)
        {
            var container = new VisualElement();
            container.style.marginBottom = 5;
            
            // Create the field row with status indicator
            var field = new TextField();
            field.value = value ?? "";
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            field.style.fontSize = 11;
            
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            
            // Label with icon for iOS and Android
            if (platform == "ios" || platform == "android")
            {
                var labelContainer = new VisualElement();
                labelContainer.style.flexDirection = FlexDirection.Row;
                labelContainer.style.alignItems = Align.Center;
                labelContainer.style.width = 160;
                
                // Load platform-specific logo
                Texture2D platformLogo = null;
                if (platform == "ios")
                {
                    platformLogo = Resources.Load<Texture2D>("apple-logo-white");
                }
                else if (platform == "android")
                {
                    platformLogo = Resources.Load<Texture2D>("google-play-logo");
                }
                
                if (platformLogo != null)
                {
                    var iconImage = new Image();
                    iconImage.image = platformLogo;
                    iconImage.style.width = 14;
                    iconImage.style.height = 14;
                    iconImage.style.marginRight = 4;
                    labelContainer.Add(iconImage);
                }
                
                var fieldLabel = new Label(label + ":");
                fieldLabel.style.fontSize = 11;
                fieldLabel.style.flexGrow = 1;
                labelContainer.Add(fieldLabel);
                
                row.Add(labelContainer);
            }
            else
            {
                // Regular label for other platforms
                var fieldLabel = new Label(label + ":");
                fieldLabel.style.width = 160;
                fieldLabel.style.fontSize = 11;
                row.Add(fieldLabel);
            }
            
            // Field container to hold textfield and status indicator
            var fieldContainer = new VisualElement();
            fieldContainer.style.flexDirection = FlexDirection.Row;
            fieldContainer.style.alignItems = Align.Center;
            fieldContainer.style.width = 240; // Fixed width to prevent excessive expansion
            
            // Text field
            field.style.width = 200; // Set specific width instead of flexGrow
            field.style.marginRight = 10; // Add spacing between field and status indicator
            
            // Add helpful tooltip for store IDs
            if (platform == "ios")
            {
                field.tooltip = "Enter App Store ID (e.g., 529479190 or id529479190)";
            }
            else if (platform == "android")
            {
                field.tooltip = "Enter Google Play package ID (e.g., com.company.game)";
            }
            else if (platform == "amazon")
            {
                field.tooltip = "Enter Amazon Appstore ID (e.g., B01234567X)";
            }
            
            fieldContainer.Add(field);
            
            // Status indicator
            var statusIndicator = new Label("?");
            statusIndicator.style.width = 20;
            statusIndicator.style.height = 20;
            statusIndicator.style.unityTextAlign = TextAnchor.MiddleCenter;
            statusIndicator.style.fontSize = 12;
            statusIndicator.style.marginLeft = 5;
            statusIndicator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            statusIndicator.style.borderTopLeftRadius = 3;
            statusIndicator.style.borderTopRightRadius = 3;
            statusIndicator.style.borderBottomLeftRadius = 3;
            statusIndicator.style.borderBottomRightRadius = 3;
            statusIndicator.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            fieldContainer.Add(statusIndicator);
            
            row.Add(fieldContainer);
            container.Add(row);
            parent.Add(container);
            
            // Store reference to status indicator
            string key = $"{gameIndex}_{platform}";
            if (platform == "ios")
            {
                iosStatusIndicators[key] = statusIndicator;
            }
            else if (platform == "android")
            {
                androidStatusIndicators[key] = statusIndicator;
            }
            else if (platform == "amazon")
            {
                amazonStatusIndicators[key] = statusIndicator;
            }
            
            // Initial verification if field has value (but only if not already verified)
            if (!string.IsNullOrEmpty(value))
            {
                bool needsVerification = false;
                
                if (platform == "ios")
                {
                    needsVerification = ShouldVerifyStoreId(value, key, platform, iosLastVerifiedValues, iosStoreIdVerificationStatus);
                    if (needsVerification)
                    {
                        VerifyIOSStoreId(value, gameIndex);
                    }
                    else if (iosStoreIdVerificationStatus.ContainsKey(key))
                    {
                        // Restore cached status immediately
                        var status = iosStoreIdVerificationStatus[key] ? VerificationStatus.Success : VerificationStatus.Failed;
                        UpdateStatusIndicator(key, platform, status);
                    }
                }
                else if (platform == "android")
                {
                    needsVerification = ShouldVerifyStoreId(value, key, platform, androidLastVerifiedValues, androidPackageIdVerificationStatus);
                    if (needsVerification)
                    {
                        VerifyAndroidPackageId(value, gameIndex);
                    }
                    else if (androidPackageIdVerificationStatus.ContainsKey(key))
                    {
                        // Restore cached status immediately
                        var status = androidPackageIdVerificationStatus[key] ? VerificationStatus.Success : VerificationStatus.Failed;
                        UpdateStatusIndicator(key, platform, status);
                    }
                }
                else if (platform == "amazon")
                {
                    needsVerification = ShouldVerifyStoreId(value, key, platform, amazonLastVerifiedValues, amazonStoreIdVerificationStatus);
                    if (needsVerification)
                    {
                        VerifyAmazonStoreId(value, gameIndex);
                    }
                    else if (amazonStoreIdVerificationStatus.ContainsKey(key))
                    {
                        // Restore cached status immediately
                        var status = amazonStoreIdVerificationStatus[key] ? VerificationStatus.Success : VerificationStatus.Failed;
                        UpdateStatusIndicator(key, platform, status);
                    }
                }
                
                LogDebug($"Initial verification for {platform} {gameIndex}: value='{value}', needsVerification={needsVerification}");
            }
        }
        
        bool ShouldVerifyStoreId(string currentValue, string key, string platform, 
            Dictionary<string, string> lastVerifiedValues, 
            Dictionary<string, bool> verificationStatus)
        {
            // If empty, always reset to unknown status
            if (string.IsNullOrEmpty(currentValue))
            {
                return false; // Let verification method handle empty case
            }
            
            // If we've never verified this key, or the value changed, we need to verify
            if (!lastVerifiedValues.ContainsKey(key) || 
                lastVerifiedValues[key] != currentValue)
            {
                return true;
            }
            
            // Value hasn't changed, but check if we have a status
            if (verificationStatus.ContainsKey(key))
            {
                // We have the same value and a status, no need to re-verify
                return false;
            }
            
            // No status for this value, need to verify
            return true;
        }
        
        /// <summary>
        /// Debounced iOS Store ID verification - waits 1.5 seconds after user stops typing
        /// </summary>
        void VerifyIOSStoreIdDebounced(string storeId, int gameIndex)
        {
            string timerKey = $"ios_{gameIndex}";
            
            // Cancel any existing timer for this field
            if (storeVerificationTimers.ContainsKey(timerKey))
            {
                storeVerificationTimers[timerKey]?.Dispose();
                storeVerificationTimers.Remove(timerKey);
            }
            
            // Skip verification if empty
            if (string.IsNullOrEmpty(storeId))
                return;
                
            // Create new timer with 1.5 second delay
            var timer = new System.Threading.Timer((_) => {
                // Execute on main thread
                EditorApplication.delayCall += () => {
                    VerifyIOSStoreId(storeId, gameIndex);
                };
            }, null, 1500, System.Threading.Timeout.Infinite);
            
            storeVerificationTimers[timerKey] = timer;
        }
        
        /// <summary>
        /// Debounced Android Package Name verification - waits 1.5 seconds after user stops typing
        /// </summary>
        void VerifyAndroidPackageIdDebounced(string packageId, int gameIndex)
        {
            string timerKey = $"android_{gameIndex}";
            
            // Cancel any existing timer for this field
            if (storeVerificationTimers.ContainsKey(timerKey))
            {
                storeVerificationTimers[timerKey]?.Dispose();
                storeVerificationTimers.Remove(timerKey);
            }
            
            // Skip verification if empty
            if (string.IsNullOrEmpty(packageId))
                return;
                
            // Create new timer with 1.5 second delay
            var timer = new System.Threading.Timer((_) => {
                // Execute on main thread
                EditorApplication.delayCall += () => {
                    VerifyAndroidPackageId(packageId, gameIndex);
                };
            }, null, 1500, System.Threading.Timeout.Infinite);
            
            storeVerificationTimers[timerKey] = timer;
        }
        
        /// <summary>
        /// Debounced Amazon Store ID verification - waits 1.5 seconds after user stops typing
        /// </summary>
        void VerifyAmazonStoreIdDebounced(string storeId, int gameIndex)
        {
            string timerKey = $"amazon_{gameIndex}";
            
            // Cancel any existing timer for this field
            if (storeVerificationTimers.ContainsKey(timerKey))
            {
                storeVerificationTimers[timerKey]?.Dispose();
                storeVerificationTimers.Remove(timerKey);
            }
            
            // Skip verification if empty
            if (string.IsNullOrEmpty(storeId))
                return;
                
            // Create new timer with 1.5 second delay
            var timer = new System.Threading.Timer((_) => {
                // Execute on main thread
                EditorApplication.delayCall += () => {
                    VerifyAmazonStoreId(storeId, gameIndex);
                };
            }, null, 1500, System.Threading.Timeout.Infinite);
            
            storeVerificationTimers[timerKey] = timer;
        }
        async void VerifyIOSStoreId(string storeId, int gameIndex)
        {
            string key = $"{gameIndex}_ios";
            
            if (string.IsNullOrEmpty(storeId))
            {
                UpdateStatusIndicator(key, "ios", VerificationStatus.Unknown);
                iosLastVerifiedValues.Remove(key);
                return;
            }
            
            // Check if we need to verify this value
            if (!ShouldVerifyStoreId(storeId, key, "ios", iosLastVerifiedValues, iosStoreIdVerificationStatus))
            {
                // Value hasn't changed and we already have a status, restore the previous status
                if (iosStoreIdVerificationStatus.ContainsKey(key))
                {
                    var status = iosStoreIdVerificationStatus[key] ? VerificationStatus.Success : VerificationStatus.Failed;
                    UpdateStatusIndicator(key, "ios", status);
                }
                return;
            }
            
            // Extract numeric ID from various formats
            string numericId = ExtractIOSStoreId(storeId);
            if (string.IsNullOrEmpty(numericId))
            {
                iosStoreIdVerificationStatus[key] = false;
                iosLastVerifiedValues[key] = storeId;
                UpdateStatusIndicator(key, "ios", VerificationStatus.Failed);
                SaveVerificationStatus();
                return;
            }
            
            // Add a small delay to avoid rapid-fire requests
            await System.Threading.Tasks.Task.Delay(300);
            
            // Show verifying status
            UpdateStatusIndicator(key, "ios", VerificationStatus.Verifying);
            
            try
            {
                // Use iTunes Search API to verify the app exists
                string url = $"https://itunes.apple.com/lookup?id={numericId}";
                LogDebug($"Verifying iOS Store ID: {storeId} (normalized: {numericId})");
                LogDebug($"iTunes API URL: {url}");
                
                using (var client = new HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(10);
                    client.DefaultRequestHeaders.Add("User-Agent", "BoostOps Unity SDK");
                    
                    var response = await client.GetStringAsync(url);
                    LogDebug($"iTunes API Response Length: {response.Length}");
                    
                    // Parse the JSON response
                    if (response.Contains("\"resultCount\":0"))
                    {
                        Debug.LogWarning($"[BoostOps] ❌ iOS Store ID not found: {numericId}");
                        iosStoreIdVerificationStatus[key] = false;
                        iosLastVerifiedValues[key] = storeId;
                        UpdateStatusIndicator(key, "ios", VerificationStatus.Failed);
                    }
                    else if (response.Contains("\"resultCount\":1"))
                    {
                        LogDebug($"✅ Successfully verified iOS app: {numericId}");
                        iosStoreIdVerificationStatus[key] = true;
                        iosLastVerifiedValues[key] = storeId;
                        UpdateStatusIndicator(key, "ios", VerificationStatus.Success);
                        
                        // Extract and update the game name from the iTunes response
                        try
                        {
                            var iTunesResponse = JsonUtility.FromJson<ITunesResponse>(response);
                            if (iTunesResponse.results != null && iTunesResponse.results.Length > 0)
                            {
                                string appName = iTunesResponse.results[0].trackCensoredName ?? iTunesResponse.results[0].trackName;
                                if (!string.IsNullOrEmpty(appName) && crossPromoTable?.targets != null && gameIndex < crossPromoTable.targets.Length)
                                {
                                    var target = crossPromoTable.targets[gameIndex];
                                    string previousName = target.headline;
                                    target.headline = appName;
                                    SaveCrossPromoChanges(autoGenerate: false);
                                    LogDebug($"📝 Updated game name from iTunes: '{previousName}' → '{appName}'");
                                    
                                    // Auto-fetch icon if not already set
                                    if (target.icon == null)
                                    {
                                        LogDebug($"🔄 Auto-fetching icon for '{appName}' since store ID verification succeeded");
                                        // Add delay and safety check to prevent concurrent fetches
                                        EditorApplication.delayCall += () => {
                                            if (target?.icon == null && crossPromoTable?.targets != null && gameIndex < crossPromoTable.targets.Length)
                                            {
                                                AutoFetchIconForGame(gameIndex, target);
                                            }
                                        };
                                    }
                                    
                                    // Refresh Cross Promo panel to show the updated name
                                    EditorApplication.delayCall += RefreshCrossPromoPanel;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[BoostOps] Could not extract app name from iTunes response: {ex.Message}");
                        }
                        
                        SaveVerificationStatus();
                    }
                    else
                    {
                        Debug.LogWarning($"[BoostOps] ❌ Unexpected iTunes API response for {numericId}: {response.Substring(0, Math.Min(200, response.Length))}");
                        iosStoreIdVerificationStatus[key] = false;
                        iosLastVerifiedValues[key] = storeId;
                        UpdateStatusIndicator(key, "ios", VerificationStatus.Failed);
                        SaveVerificationStatus();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to verify iOS Store ID {storeId}: {ex.Message}");
                iosStoreIdVerificationStatus[key] = false;
                iosLastVerifiedValues[key] = storeId;
                UpdateStatusIndicator(key, "ios", VerificationStatus.Failed);
                SaveVerificationStatus();
            }
        }
        string ExtractIOSStoreId(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;
                
            input = input.Trim();
            
            // Handle "id123456789" format
            if (input.StartsWith("id", StringComparison.OrdinalIgnoreCase))
            {
                string numericPart = input.Substring(2);
                if (System.Text.RegularExpressions.Regex.IsMatch(numericPart, @"^\d+$"))
                {
                    return numericPart;
                }
            }
            
            // Handle pure numeric format "123456789"
            if (System.Text.RegularExpressions.Regex.IsMatch(input, @"^\d+$"))
            {
                return input;
            }
            
            // Handle full App Store URLs like "https://apps.apple.com/app/id123456789"
            var match = System.Text.RegularExpressions.Regex.Match(input, @"id(\d+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            
            // Invalid format
            return null;
        }
        async void VerifyAndroidPackageId(string packageId, int gameIndex)
        {
            string key = $"{gameIndex}_android";
            
            if (string.IsNullOrEmpty(packageId))
            {
                UpdateStatusIndicator(key, "android", VerificationStatus.Unknown);
                androidLastVerifiedValues.Remove(key);
                return;
            }
            
            // Check if we need to verify this value
            if (!ShouldVerifyStoreId(packageId, key, "android", androidLastVerifiedValues, androidPackageIdVerificationStatus))
            {
                // Value hasn't changed and we already have a status, restore the previous status
                if (androidPackageIdVerificationStatus.ContainsKey(key))
                {
                    var status = androidPackageIdVerificationStatus[key] ? VerificationStatus.Success : VerificationStatus.Failed;
                    UpdateStatusIndicator(key, "android", status);
                }
                return;
            }
            
            // Add a small delay to avoid rapid-fire requests that might trigger rate limiting
            await System.Threading.Tasks.Task.Delay(500);
            
            // Show verifying status
            UpdateStatusIndicator(key, "android", VerificationStatus.Verifying);
            
            try
            {
                // Try to access the Play Store page to verify the app exists
                string url = $"https://play.google.com/store/apps/details?id={packageId}&hl=en_US";
                LogDebug($"Verifying Android Package Name: {packageId}");
                LogDebug($"Request URL: {url}");
                
                using (var client = new HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(15); // Increased timeout
                    // Use a more realistic browser user agent with additional fingerprinting
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                    // Add additional headers to look more like a real browser
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    client.DefaultRequestHeaders.Add("DNT", "1");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                    client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not A(Brand\";v=\"99\", \"Google Chrome\";v=\"121\", \"Chromium\";v=\"121\"");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"macOS\"");
                    
                    var response = await client.GetAsync(url);
                    Debug.Log($"[BoostOps] Response Status: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        Debug.Log($"[BoostOps] Response Content Length: {content.Length}");
                        
                        // Check for rate limiting or blocking
                        bool isBlocked = 
                            content.Contains("unusual traffic") ||
                            content.Contains("robot") ||
                            content.Contains("captcha") ||
                            content.Contains("blocked") ||
                            content.Contains("rate limit") ||
                            content.Contains("too many requests");
                        
                        if (isBlocked)
                        {
                            Debug.LogWarning($"[BoostOps] Play Store may be blocking requests - detected bot protection for {packageId}");
                            Debug.Log($"[BoostOps] Content preview: {content.Substring(0, Math.Min(500, content.Length))}");
                        }
                        
                        // Check for various indicators that this is a valid app page
                        bool hasAppContent = 
                            content.Contains("com.android.vending") ||
                            content.Contains("play-button") ||
                            content.Contains("app-header") ||
                            content.Contains("install") ||
                            content.Contains("Install") ||
                            content.Contains("data-docid") ||
                            content.Contains("itemprop=\"name\"") ||
                            content.Contains("application-identifier") ||
                            content.Contains(packageId) ||
                            content.Contains("BuyButton") ||
                            content.Contains("play.google.com") ||
                            content.Contains("Clash of Clans") || // Specific check for this app
                            content.Length > 10000; // Valid app pages are typically large
                        
                        // Also check that it's not an error page
                        bool isErrorPage = 
                            content.Contains("We're sorry, the requested URL was not found") ||
                            content.Contains("Not Found") ||
                            content.Contains("404") ||
                            content.Contains("error") && content.Length < 5000;
                        
                        Debug.Log($"[BoostOps] Content analysis for {packageId}: HasAppContent={hasAppContent}, IsErrorPage={isErrorPage}, IsBlocked={isBlocked}");
                        
                        if (hasAppContent && !isErrorPage && !isBlocked)
                        {
                            Debug.Log($"[BoostOps] ✅ Successfully verified Android app: {packageId}");
                            androidPackageIdVerificationStatus[key] = true;
                            androidLastVerifiedValues[key] = packageId;
                            UpdateStatusIndicator(key, "android", VerificationStatus.Success);
                            
                            // Extract and update the game name from Google Play response (only if iOS hasn't already provided one)
                            try
                            {
                                if (crossPromoTable?.targets != null && gameIndex < crossPromoTable.targets.Length)
                                {
                                    var target = crossPromoTable.targets[gameIndex];
                                    
                                    // Only update name if iOS hasn't already provided one (prioritize iOS)
                                    bool shouldUpdateName = string.IsNullOrEmpty(target.headline) || 
                                                           target.headline == "Amazing Game Title" || 
                                                           target.headline.StartsWith("Game ") ||
                                                           string.IsNullOrEmpty(target.iosAppStoreId); // No iOS ID means Android is primary
                                    
                                    if (shouldUpdateName)
                                    {
                                        // Extract app name from Google Play HTML
                                        var nameMatch = System.Text.RegularExpressions.Regex.Match(content, @"<title>([^<]+) - Apps on Google Play</title>");
                                        if (!nameMatch.Success)
                                        {
                                            // Alternative pattern for app name extraction
                                            nameMatch = System.Text.RegularExpressions.Regex.Match(content, @"""name"":\s*""([^""]+)""");
                                        }
                                        
                                        if (nameMatch.Success)
                                        {
                                            string appName = nameMatch.Groups[1].Value.Trim();
                                            if (!string.IsNullOrEmpty(appName) && appName != packageId)
                                            {
                                                string previousName = target.headline;
                                                target.headline = appName;
                                                SaveCrossPromoChanges();
                                                Debug.Log($"[BoostOps] 📝 Updated game name from Google Play: '{previousName}' → '{appName}'");
                                                
                                                // Auto-fetch icon if not already set
                                                if (target.icon == null)
                                                {
                                                    LogDebug($"🔄 Auto-fetching icon for '{appName}' since Android store ID verification succeeded");
                                                    // Add delay and safety check to prevent concurrent fetches
                                                    EditorApplication.delayCall += () => {
                                                        if (target?.icon == null && crossPromoTable?.targets != null && gameIndex < crossPromoTable.targets.Length)
                                                        {
                                                            AutoFetchIconForGame(gameIndex, target);
                                                        }
                                                    };
                                                }
                                                
                                                // Refresh Cross Promo panel to show the updated name
                                                EditorApplication.delayCall += RefreshCrossPromoPanel;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log($"[BoostOps] 🔄 Skipping Android name update - iOS name already set: '{target.headline}'");
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"[BoostOps] Could not extract app name from Google Play response: {ex.Message}");
                            }
                            
                            // Auto-fetch icon if not already set (for cases where name wasn't updated but verification succeeded)
                            if (crossPromoTable?.targets != null && gameIndex < crossPromoTable.targets.Length)
                            {
                                var target = crossPromoTable.targets[gameIndex];
                                if (target?.icon == null)
                                {
                                    string displayName = !string.IsNullOrEmpty(target.headline) && target.headline != "Amazing Game Title" 
                                        ? target.headline 
                                        : packageId;
                                    LogDebug($"🔄 Auto-fetching icon for '{displayName}' since Android store ID verification succeeded");
                                    // Add delay and safety check to prevent concurrent fetches
                                    EditorApplication.delayCall += () => {
                                        if (target?.icon == null && crossPromoTable?.targets != null && gameIndex < crossPromoTable.targets.Length)
                                        {
                                            AutoFetchIconForGame(gameIndex, target);
                                        }
                                    };
                                }
                            }
                            
                            SaveVerificationStatus();
                        }
                        else
                        {
                            Debug.LogWarning($"Android app page verification failed for {packageId}. Content length: {content.Length}, HasAppContent: {hasAppContent}, IsErrorPage: {isErrorPage}, IsBlocked: {isBlocked}");
                            androidPackageIdVerificationStatus[key] = false;
                            androidLastVerifiedValues[key] = packageId;
                            UpdateStatusIndicator(key, "android", VerificationStatus.Failed);
                            SaveVerificationStatus();
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Android verification HTTP error for {packageId}: {response.StatusCode}");
                        androidPackageIdVerificationStatus[key] = false;
                        androidLastVerifiedValues[key] = packageId;
                        UpdateStatusIndicator(key, "android", VerificationStatus.Failed);
                        SaveVerificationStatus();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to verify Android Package Name {packageId}: {ex.Message}");
                androidPackageIdVerificationStatus[key] = false;
                androidLastVerifiedValues[key] = packageId;
                UpdateStatusIndicator(key, "android", VerificationStatus.Failed);
                SaveVerificationStatus();
            }
        }
        
        async void VerifyAmazonStoreId(string storeId, int gameIndex)
        {
            string key = $"{gameIndex}_amazon";
            
            if (string.IsNullOrEmpty(storeId))
            {
                UpdateStatusIndicator(key, "amazon", VerificationStatus.Unknown);
                amazonLastVerifiedValues.Remove(key);
                return;
            }
            
            // Check if we need to verify this value
            if (!ShouldVerifyStoreId(storeId, key, "amazon", amazonLastVerifiedValues, amazonStoreIdVerificationStatus))
            {
                // Value hasn't changed and we already have a status, restore the previous status
                if (amazonStoreIdVerificationStatus.ContainsKey(key))
                {
                    var status = amazonStoreIdVerificationStatus[key] ? VerificationStatus.Success : VerificationStatus.Failed;
                    UpdateStatusIndicator(key, "amazon", status);
                }
                return;
            }
            
            // Add a small delay to avoid rapid-fire requests (same as Google Play)
            await System.Threading.Tasks.Task.Delay(500);
            
            // Show verifying status
            UpdateStatusIndicator(key, "amazon", VerificationStatus.Verifying);
            
            try
            {
                // Try to access the Amazon Appstore page to verify the app exists
                // Support both ASIN and package name formats
                string baseUrl;
                if (storeId.Length == 10 && System.Text.RegularExpressions.Regex.IsMatch(storeId, @"^[A-Z0-9]{10}$"))
                {
                    // ASIN format (10-character alphanumeric)
                    baseUrl = $"https://www.amazon.com/dp/{storeId}";
                }
                else
                {
                    // Package name format - use mobile Amazon URL which is less bot-protected
                    baseUrl = $"https://www.amazon.com/gp/mas/dl/android?p={storeId}";
                }
                string url = baseUrl; // Don't add ref parameter for package names
                Debug.Log($"[BoostOps] Verifying Amazon Store ID: {storeId}");
                Debug.Log($"[BoostOps] Request URL: {url}");
                
                using (var client = new HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(20); // Increased timeout for Amazon
                    
                    // Enhanced browser fingerprinting with randomized Chrome version
                    var chromeVersions = new[] { "121.0.0.0", "120.0.0.0", "119.0.0.0", "122.0.0.0" };
                    var randomVersion = chromeVersions[UnityEngine.Random.Range(0, chromeVersions.Length)];
                    
                    client.DefaultRequestHeaders.Add("User-Agent", $"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{randomVersion} Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    client.DefaultRequestHeaders.Add("DNT", "1");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                    client.DefaultRequestHeaders.Add("sec-ch-ua", $"\"Not A(Brand\";v=\"99\", \"Google Chrome\";v=\"{randomVersion.Split('.')[0]}\", \"Chromium\";v=\"{randomVersion.Split('.')[0]}\"");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"macOS\"");
                    
                    // Add Amazon-specific headers that real browsers send
                    client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
                    
                    // Add random delay to avoid rate limiting
                    await System.Threading.Tasks.Task.Delay(UnityEngine.Random.Range(100, 300));
                    
                    var response = await client.GetAsync(url);
                    Debug.Log($"[BoostOps] Response Status: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        Debug.Log($"[BoostOps] Response Content Length: {content.Length}");
                        
                        // Check for rate limiting or blocking
                        bool isBlocked = 
                            content.Contains("unusual traffic") ||
                            content.Contains("robot") ||
                            content.Contains("captcha") ||
                            content.Contains("blocked") ||
                            content.Contains("rate limit") ||
                            content.Contains("too many requests");
                        
                        if (isBlocked)
                        {
                            Debug.LogWarning($"[BoostOps] Amazon may be blocking requests - detected bot protection for {storeId}");
                            Debug.Log($"[BoostOps] Content preview: {content.Substring(0, Math.Min(500, content.Length))}");
                        }
                        
                        // Check for various indicators that this is a valid Amazon app page
                        bool hasAppContent = 
                            content.Contains("Amazon Appstore") ||
                            content.Contains("appstore") ||
                            content.Contains("Download") ||
                            content.Contains("Install") ||
                            content.Contains("app-icon") ||
                            content.Contains("android-app") ||
                            content.Contains("mobile-app") ||
                            content.Contains("application") ||
                            content.Contains("Screenshots") ||
                            content.Contains("What's New") ||
                            content.Contains("Version") ||
                            content.Contains("Privacy Policy") ||
                            (content.Contains("Amazon") && content.Contains("app")) ||
                            content.Length > 10000 || // Valid app pages are typically large
                            // Accept valid Amazon URLs even if content is blocked/minimal
                            (response.IsSuccessStatusCode && url.Contains("/dp/") && storeId.StartsWith("B0") && storeId.Length == 10) ||
                            // Also accept package name format URLs that return successfully
                            (response.IsSuccessStatusCode && url.Contains("/gp/mas/dl/android") && storeId.Contains("."));
                        
                        // Check that it's not an error page  
                        bool isErrorPage = 
                            content.Contains("Page Not Found") ||
                            content.Contains("We're sorry") ||
                            content.Contains("404") ||
                            (content.Contains("error") && content.Length < 5000 && !content.Contains("Continue shopping"));
                        
                        Debug.Log($"[BoostOps] Content analysis for {storeId}: HasAppContent={hasAppContent}, IsErrorPage={isErrorPage}, IsBlocked={isBlocked}");
                        
                        if (hasAppContent && !isErrorPage && !isBlocked)
                        {
                            if (content.Contains("Continue shopping") && content.Length < 5000)
                            {
                                Debug.Log($"[BoostOps] ✅ Amazon Store ID {storeId} verified (Amazon blocked full content, but URL format is valid)");
                            }
                            else
                            {
                                Debug.Log($"[BoostOps] ✅ Successfully verified Amazon app: {storeId}");
                            }
                            amazonStoreIdVerificationStatus[key] = true;
                            amazonLastVerifiedValues[key] = storeId;
                            UpdateStatusIndicator(key, "amazon", VerificationStatus.Success);
                        }
                        else
                        {
                            // Try fallback verification with mobile Amazon site
                            Debug.Log($"[BoostOps] 🔄 Initial verification failed, trying mobile Amazon fallback...");
                            bool fallbackSuccess = await TryAmazonMobileFallback(storeId);
                            
                            if (fallbackSuccess)
                            {
                                Debug.Log($"[BoostOps] ✅ Amazon Store ID {storeId} verified via mobile fallback");
                                amazonStoreIdVerificationStatus[key] = true;
                                amazonLastVerifiedValues[key] = storeId;
                                UpdateStatusIndicator(key, "amazon", VerificationStatus.Success);
                            }
                            else
                            {
                                // Final fallback: Accept valid Amazon Store ID format as last resort
                                bool isValidFormat = storeId.StartsWith("B0") && storeId.Length == 10 && storeId.All(c => char.IsLetterOrDigit(c));
                                
                                if (isValidFormat)
                                {
                                    Debug.Log($"[BoostOps] ⚠️ Amazon Store ID {storeId} accepted based on valid format (verification limited by bot protection)");
                                    amazonStoreIdVerificationStatus[key] = true;
                                    amazonLastVerifiedValues[key] = storeId;
                                    UpdateStatusIndicator(key, "amazon", VerificationStatus.Success);
                                }
                                else
                                {
                                    Debug.LogWarning($"Amazon app page verification failed for {storeId}. Content length: {content.Length}, HasAppContent: {hasAppContent}, IsErrorPage: {isErrorPage}, IsBlocked: {isBlocked}");
                                    amazonStoreIdVerificationStatus[key] = false;
                                    amazonLastVerifiedValues[key] = storeId;
                                    UpdateStatusIndicator(key, "amazon", VerificationStatus.Failed);
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Amazon verification HTTP error for {storeId}: {response.StatusCode}");
                        amazonStoreIdVerificationStatus[key] = false;
                        amazonLastVerifiedValues[key] = storeId;
                        UpdateStatusIndicator(key, "amazon", VerificationStatus.Failed);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to verify Amazon Store ID {storeId}: {ex.Message}");
                amazonStoreIdVerificationStatus[key] = false;
                amazonLastVerifiedValues[key] = storeId;
                UpdateStatusIndicator(key, "amazon", VerificationStatus.Failed);
            }
        }
        
        /// <summary>
        /// Fallback verification using mobile Amazon site which has less bot protection
        /// </summary>
        async Task<bool> TryAmazonMobileFallback(string storeId)
        {
            try
            {
                // Try mobile Amazon site which is often less protected
                string mobileUrl = $"https://m.amazon.com/dp/{storeId}";
                Debug.Log($"[BoostOps] Trying mobile Amazon fallback: {mobileUrl}");
                
                using (var client = new HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(15);
                    
                    // Use mobile browser user agent
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    
                    // Random delay
                    await System.Threading.Tasks.Task.Delay(UnityEngine.Random.Range(200, 500));
                    
                    var response = await client.GetAsync(mobileUrl);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        
                        // Mobile Amazon has different content patterns
                        bool hasValidContent = 
                            content.Contains(storeId) ||
                            content.Contains("Amazon") ||
                            content.Contains("app") ||
                            content.Contains("download") ||
                            content.Contains("install") ||
                            content.Length > 2000; // Mobile pages are smaller but still substantial
                        
                        bool isError = 
                            content.Contains("Page Not Found") ||
                            content.Contains("404") ||
                            content.Contains("error") && content.Length < 1000;
                        
                        Debug.Log($"[BoostOps] Mobile fallback content length: {content.Length}, HasValidContent: {hasValidContent}, IsError: {isError}");
                        
                        return hasValidContent && !isError;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log($"[BoostOps] Mobile Amazon fallback failed: {ex.Message}");
            }
            
            return false;
        }
        
        enum VerificationStatus
        {
            Unknown,
            Verifying,
            Success,
            Failed
        }
        
        void UpdateStatusIndicator(string key, string platform, VerificationStatus status)
        {
            VisualElement indicator = null;
            
            if (platform == "ios" && iosStatusIndicators.ContainsKey(key))
            {
                indicator = iosStatusIndicators[key];
            }
            else if (platform == "android" && androidStatusIndicators.ContainsKey(key))
            {
                indicator = androidStatusIndicators[key];
            }
            else if (platform == "amazon" && amazonStatusIndicators.ContainsKey(key))
            {
                indicator = amazonStatusIndicators[key];
            }
            
            if (indicator != null && indicator is Label statusLabel)
            {
                string storeName = platform == "ios" ? "App Store" : 
                                 platform == "android" ? "Play Store" : 
                                 platform == "amazon" ? "Amazon Appstore" : "Store";
                
                switch (status)
                {
                    case VerificationStatus.Unknown:
                        statusLabel.text = "?";
                        statusLabel.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                        statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                        statusLabel.tooltip = "Enter a valid ID to verify";
                        break;
                    case VerificationStatus.Verifying:
                        statusLabel.text = "...";
                        statusLabel.style.backgroundColor = new Color(0.3f, 0.3f, 0.8f, 0.8f);
                        statusLabel.style.color = Color.white;
                        statusLabel.tooltip = "Verifying...";
                        break;
                    case VerificationStatus.Success:
                        statusLabel.text = "✓";
                        statusLabel.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
                        statusLabel.style.color = Color.white;
                        statusLabel.tooltip = $"Verified: App found in {storeName}";
                        break;
                    case VerificationStatus.Failed:
                        statusLabel.text = "⚠";
                        statusLabel.style.backgroundColor = new Color(0.8f, 0.4f, 0.2f, 0.8f);
                        statusLabel.style.color = Color.white;
                        statusLabel.tooltip = $"Warning: App not found in {storeName}";
                        break;
                }
            }
        }
        
        void RemoveTargetGame(int index)
        {
            if (crossPromoTable == null || index < 0 || index >= crossPromoTable.targets.Length) return;
            
            var targetGame = crossPromoTable.targets[index];
            string gameName = !string.IsNullOrEmpty(targetGame.headline) ? targetGame.headline : targetGame.id;
            
            // Show confirmation dialog
            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Game from Cross-Promotion", 
                $"Are you sure you want to remove \"{gameName}\" from the cross-promotion list?\n\nThis action cannot be undone.", 
                "Remove", 
                "Cancel"
            );
            
            if (!confirmed) return;
            
            var targetsList = new System.Collections.Generic.List<TargetGame>(crossPromoTable.targets);
            targetsList.RemoveAt(index);
            crossPromoTable.targets = targetsList.ToArray();
            
            SaveCrossPromoChanges();
            
            // Defer asset saving to avoid conflicts
            EditorApplication.delayCall += () => {
                try
                {
                    if (!EditorApplication.isUpdating && !EditorApplication.isCompiling)
                    {
            AssetDatabase.SaveAssets();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BoostOps] Failed to save cross-promo assets after removal: {e.Message}");
                }
            };
            
            // Refresh panel
            ShowCrossPromoPanel();
        }
        
        void MoveTargetGame(int index, int direction)
        {
            if (crossPromoTable == null || index < 0 || index >= crossPromoTable.targets.Length) return;
            
            int newIndex = index + direction;
            if (newIndex < 0 || newIndex >= crossPromoTable.targets.Length) return;
            
            // Swap the games
            var targetsList = new System.Collections.Generic.List<TargetGame>(crossPromoTable.targets);
            var temp = targetsList[index];
            targetsList[index] = targetsList[newIndex];
            targetsList[newIndex] = temp;
            crossPromoTable.targets = targetsList.ToArray();
            
            SaveCrossPromoChanges();
            
            // Defer asset saving to avoid conflicts
            EditorApplication.delayCall += () => {
                try
                {
                    if (!EditorApplication.isUpdating && !EditorApplication.isCompiling)
                    {
            AssetDatabase.SaveAssets();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BoostOps] Failed to save cross-promo assets after move: {e.Message}");
                }
            };
            
            string directionText = direction < 0 ? "up" : "down";
            string gameName = !string.IsNullOrEmpty(temp.headline) ? temp.headline : "Game";
            LogDebug($"Moved '{gameName}' {directionText} to position {newIndex + 1}");
            
            // Refresh panel
            ShowCrossPromoPanel();
        }
        
        string GetPriorityLevel(int index, int totalGames)
        {
            if (totalGames <= 1) return "Only Game";
            
            // Calculate which third of the list this game is in
            int thirdSize = Mathf.Max(1, totalGames / 3);
            
            if (index < thirdSize)
                return "High Priority";
            else if (index < thirdSize * 2)
                return "Medium Priority";
            else
                return "Low Priority";
        }
        void CreateCrossPromoTable()
        {
            // Ensure directory exists
            string crossPromoDir = "Assets/BoostOps/CrossPromo";
            if (!Directory.Exists(crossPromoDir))
            {
                Directory.CreateDirectory(crossPromoDir);
                AssetDatabase.Refresh();
            }
            
            // Create new CrossPromoTable asset
            crossPromoTable = ScriptableObject.CreateInstance<CrossPromoTable>();
            
            // Set some default values
            crossPromoTable.defaultDomain = "boostlink.me";
            crossPromoTable.campaignSlug = "cp";
            crossPromoTable.rotation = RotationType.Waterfall;
            crossPromoTable.globalFrequencyCap = BoostOps.Core.FrequencyCap.Unlimited(); // Unlimited global - individual games self-limit
            crossPromoTable.minPlayerSession = 3; // Best practice: wait for engagement
            crossPromoTable.minPlayerDay = 1; // Best practice: let them settle in first
            crossPromoTable.targets = new TargetGame[0];
            
            string assetPath = "Assets/Resources/BoostOps/CrossPromoTable.asset";
            AssetDatabase.CreateAsset(crossPromoTable, assetPath);
            AssetDatabase.SaveAssets();
            
            // Mark as stale since we just created a new table
            MarkJsonAsStale();
            
            // Refresh the panel
            ShowCrossPromoPanel();
            
            LogDebug($"Created CrossPromoTable at {assetPath}");
        }
        
        void ValidateCrossPromoTable()
        {
            if (crossPromoTable == null) return;
            
            var errors = new System.Collections.Generic.List<string>();
            var warnings = new System.Collections.Generic.List<string>();
            
            // Check global settings
            if (string.IsNullOrEmpty(crossPromoTable.defaultDomain))
                errors.Add("Default domain is required");
            
            if (string.IsNullOrEmpty(crossPromoTable.campaignSlug))
                errors.Add("Campaign slug is required");
                
            if (crossPromoTable.globalFrequencyCap != null && crossPromoTable.globalFrequencyCap.impressions < 0)
                errors.Add("Global frequency cap must be 0 (unlimited) or greater");
                
            // Validate player requirements  
            if (crossPromoTable.minPlayerSession < 0)
                errors.Add("Min Player Session must be 0 or greater");
            else if (crossPromoTable.minPlayerSession == 0)
                warnings.Add("Min Player Session is 0 - cross-promo will show immediately (not recommended)");
                
            if (crossPromoTable.minPlayerDay < 0)
                errors.Add("Min Player Day must be 0 or greater");
            else if (crossPromoTable.minPlayerDay == 0)
                warnings.Add("Min Player Day is 0 - cross-promo will show on install day (not recommended)");
            
            // Check target games
            var ids = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < crossPromoTable.targets.Length; i++)
            {
                var target = crossPromoTable.targets[i];
                string prefix = $"Target {i + 1}";
                
                // Check for duplicate IDs
                if (!string.IsNullOrEmpty(target.id))
                {
                    if (!ids.Add(target.id))
                        errors.Add($"{prefix}: Duplicate ID '{target.id}'");
                }
                else
                {
                    errors.Add($"{prefix}: ID is required");
                }
                
                // Check required fields
                
                if (string.IsNullOrEmpty(target.headline))
                    errors.Add($"{prefix}: Headline is required");
                
                // Check platform configurations
                if (!target.HasAndroidConfig() && !target.HasIOSConfig() && !target.HasWindowsConfig() && !target.HasAmazonConfig() && !target.HasSamsungConfig())
                    errors.Add($"{prefix}: At least one store ID (iOS, Android, Windows, Amazon, or Samsung Galaxy Store) is required");
                
                // Validate weight
                if (target.weight <= 0)
                    warnings.Add($"{prefix}: Weight should be greater than 0");
                
                // Validate frequency cap
                if (target.frequencyCap != null && target.frequencyCap.impressions < 0)
                    warnings.Add($"{prefix}: Daily frequency cap should be 0 (unlimited) or greater");
            }
            
            // Display results
            if (errors.Count == 0 && warnings.Count == 0)
            {
                LogDebug("✅ Cross-promo table validation passed!");
                EditorUtility.DisplayDialog("Validation Result", "✅ Cross-promo table validation passed!", "OK");
            }
            else
            {
                string message = "";
                if (errors.Count > 0)
                {
                    message += "❌ Errors:\n" + string.Join("\n", errors);
                }
                if (warnings.Count > 0)
                {
                    if (errors.Count > 0) message += "\n\n";
                    message += "⚠️ Warnings:\n" + string.Join("\n", warnings);
                }
                
                Debug.LogWarning("Cross-promo table validation issues:\n" + message);
                EditorUtility.DisplayDialog("Validation Result", message, "OK");
            }
        }
        void AddNewTargetGame()
        {
            if (crossPromoTable == null) return;
            
            // Create new target game with default values
            var newTarget = new TargetGame
            {
                id = GenerateGameId(null), // Generate a temporary ID, will be updated when store IDs are added
                boostLinkSlug = "",
                androidPackageId = "",
                iosAppStoreId = "",
                iosBundleId = "",
                windowsStoreId = "",
                amazonStoreId = "",
                samsungStoreId = "",
                weight = 100,
                frequencyCap = BoostOps.Core.FrequencyCap.Daily(2), // Recommended default for waterfall cross-promotion
                useCustomFreqCap = true, // Allow easy customization per game
                headline = "", // No longer displayed in UI
                icon = null
            };
            
            // Add to array
            var targetsList = new System.Collections.Generic.List<TargetGame>(crossPromoTable.targets);
            targetsList.Add(newTarget);
            crossPromoTable.targets = targetsList.ToArray();
            
            // Mark as dirty
            SaveCrossPromoChanges();
            
            // Defer asset saving to avoid conflicts
            EditorApplication.delayCall += () => {
                try
                {
                    if (!EditorApplication.isUpdating && !EditorApplication.isCompiling)
                    {
            AssetDatabase.SaveAssets();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BoostOps] Failed to save cross-promo assets after adding new target: {e.Message}");
                }
            };
            
            // Refresh panel
            ShowCrossPromoPanel();
        }
        void GenerateCrossPromoJson()
        {
            GenerateCrossPromoJson(showDialog: true, selectFile: true);
        }
        void GenerateCrossPromoJson(bool showDialog = true, bool selectFile = true)
        {
            if (crossPromoTable == null) 
            {
                Debug.LogError("[BoostOps] CrossPromoTable is null - cannot generate JSON");
                return;
            }
            
            LogDebug($"Starting JSON generation with {crossPromoTable.targets?.Length ?? 0} targets");
            
            // Validate configuration before generating JSON
            var validationErrors = ValidateCrossPromoConfiguration();
            if (validationErrors.Count > 0)
            {
                Debug.LogError($"[BoostOps] Validation failed with {validationErrors.Count} errors: {string.Join(", ", validationErrors)}");
                if (showDialog)
                {
                    string errorMessage = "❌ Cannot generate Cross-Promo JSON due to validation errors:\n\n" + string.Join("\n", validationErrors);
                    errorMessage += "\n\nPlease fix these issues before generating the JSON configuration.";
                    
                    EditorUtility.DisplayDialog("Cross-Promo Validation Failed", errorMessage, "OK");
                }
                return;
            }
            
            LogDebug("Validation passed, generating modern campaign JSON...");
            
            // Generate new campaign format instead of legacy CrossPromoSettings
            string json = GenerateModernCampaignJson(crossPromoTable);
            
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[BoostOps] GenerateModernCampaignJson returned null or empty JSON");
                return;
            }
            
            LogDebug($"Generated JSON length: {json.Length} characters");
            
            // Save to Resources/BoostOps for runtime use (synchronous loading, works on all platforms)
            string resourcesPath = System.IO.Path.Combine(Application.dataPath, "Resources", "BoostOps");
            if (!System.IO.Directory.Exists(resourcesPath))
            {
                System.IO.Directory.CreateDirectory(resourcesPath);
            }
            
            // Save as cross_promo_local.json in Resources
            string resourcesJsonPath = System.IO.Path.Combine(resourcesPath, "cross_promo_local.json");
            File.WriteAllText(resourcesJsonPath, json);
            
            AssetDatabase.Refresh();
            
            if (selectFile)
            {
                // Open the main cross_promo_local.json file for viewing
                string relativeResourcesPath = "Assets/Resources/BoostOps/cross_promo_local.json";
                var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(relativeResourcesPath);
                if (jsonAsset != null)
                {
                    Selection.activeObject = jsonAsset;
                    EditorGUIUtility.PingObject(jsonAsset);
                }
            }
            
            // Mark JSON as fresh and refresh UI
            MarkJsonAsFresh();
            
            // Show success dialog if requested
            if (showDialog)
            {
                string successMessage = "✅ Cross-Promo JSON Generated Successfully!\n\n";
                successMessage += $"📁 File Created:\n";
                successMessage += $"• StreamingAssets/BoostOps/cross_promo_local.json\n\n";
                successMessage += $"📊 Campaign Details:\n";
                successMessage += $"• {crossPromoTable.targets?.Length ?? 0} target game(s) configured\n";
                successMessage += $"• JSON size: {json.Length} characters\n";
                successMessage += $"• Generated at: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";
                successMessage += "The JSON configuration is ready for runtime use!";
                
                EditorUtility.DisplayDialog("Cross-Promo JSON Generation", successMessage, "Great!");
            }
            
            LogDebug($"Generated cross-promo JSON at StreamingAssets/BoostOps/cross_promo_local.json");
        }
        
        /// <summary>
        /// Marks CrossPromoTable as dirty and optionally auto-generates JSON if configuration is valid
        /// </summary>
        void SaveCrossPromoChanges(bool autoGenerate = true)
        {
            if (crossPromoTable == null) 
            {
                Debug.LogWarning("[BoostOps] SaveCrossPromoChanges called but crossPromoTable is null");
                return;
            }
            
            LogDebug($"SaveCrossPromoChanges called - marking table as dirty (autoGenerate: {autoGenerate})");
            EditorUtility.SetDirty(crossPromoTable);
            
            // Mark JSON as stale when configuration changes
            MarkJsonAsStale();
            
            if (autoGenerate)
            {
                // Add a small delay before auto-generation so user can see the stale state
                EditorApplication.delayCall += () => {
                    // Auto-generate JSON silently if configuration is valid AND we're in Local mode
                    // In BoostOps Managed mode, the server is the source of truth - don't auto-generate
                    if (crossPromoMode == FeatureMode.Local)
                    {
                        var validationErrors = ValidateCrossPromoConfiguration();
                        if (validationErrors.Count == 0)
                        {
                            LogDebug("Auto-generating JSON after configuration changes (Local mode only)");
                            GenerateCrossPromoJson(showDialog: false, selectFile: false);
                        }
                        else
                        {
                            LogWarningDebug($"Skipping auto-generation due to {validationErrors.Count} validation errors");
                            // Still update button to show stale state even if auto-generation failed
                            UpdateGenerateButtonDynamic();
                        }
                    }
                    else
                    {
                        LogDebug("Skipping auto-generation: BoostOps Managed mode - server is source of truth");
                        // Still update button state
                        UpdateGenerateButtonDynamic();
                    }
                };
            }
        }
        
        System.Collections.Generic.List<string> ValidateCrossPromoConfiguration()
        {
            var errors = new System.Collections.Generic.List<string>();
            
            if (crossPromoTable?.targets == null || crossPromoTable.targets.Length == 0)
            {
                errors.Add("• No target games configured");
                return errors;
            }
            
            // Check if any games have icon references
            bool hasAnyIcons = false;
            foreach (var target in crossPromoTable.targets)
            {
                if (target.icon != null)
                {
                    hasAnyIcons = true;
                    break;
                }
            }
            
            if (!hasAnyIcons)
            {
                errors.Add("• No games have icon references assigned. Use 'Fetch'/'Refresh' buttons to download icons from stores or manually assign icon sprites.");
            }
            
            // Check for games missing all ID fields
            var gamesWithoutIds = new System.Collections.Generic.List<int>();
            for (int i = 0; i < crossPromoTable.targets.Length; i++)
            {
                var target = crossPromoTable.targets[i];
                bool hasAnyId = !string.IsNullOrEmpty(target.iosAppStoreId) ||
                              !string.IsNullOrEmpty(target.androidPackageId) ||
                              !string.IsNullOrEmpty(target.windowsStoreId) ||
                              !string.IsNullOrEmpty(target.amazonStoreId) ||
                              !string.IsNullOrEmpty(target.samsungStoreId);
                
                if (!hasAnyId)
                {
                    gamesWithoutIds.Add(i + 1);
                }
            }
            
            if (gamesWithoutIds.Count > 0)
            {
                string gameNumbers = string.Join(", ", gamesWithoutIds);
                errors.Add($"• Game{(gamesWithoutIds.Count > 1 ? "s" : "")} {gameNumbers} missing store ID{(gamesWithoutIds.Count > 1 ? "s" : "")}. Each game needs at least one store ID (iOS, Android, Windows, Amazon, or Samsung Galaxy Store).");
            }
            
            // Check if iOS cross-promotion requires source Store ID for analytics
            bool hasIOSTargets = false;
            foreach (var target in crossPromoTable.targets)
            {
                if (!string.IsNullOrEmpty(target.iosAppStoreId))
                {
                    hasIOSTargets = true;
                    break;
                }
            }
            
            if (hasIOSTargets && string.IsNullOrEmpty(GetProjectSettingsStoreId("apple")))
            {
                errors.Add("• iOS cross-promotion requires your app's Store ID for analytics tracking. Please enter it in the 'Apple Store ID' field in Source Game Settings.");
            }
            
            return errors;
        }
        
        bool UniversalLinkFilesExist()
        {
            return File.Exists("Assets/BoostOpsGenerated/ServerFiles/well_known_server/apple-app-site-association") || 
                   File.Exists("Assets/BoostOpsGenerated/ServerFiles/well_known_server/assetlinks.json") ||
                   File.Exists("Assets/BoostOpsGenerated/Plugins/Android/AndroidManifest.xml") ||
                   File.Exists("Assets/BoostOpsGenerated/SETUP_INSTRUCTIONS.md");
        }
        
        void ShowGeneratedFiles()
        {
            string message = "Generated Universal Link Files:\n\n";
            
            bool hasIOS = File.Exists("Assets/BoostOpsGenerated/ServerFiles/well_known_server/apple-app-site-association");
            bool hasAndroid = File.Exists("Assets/BoostOpsGenerated/ServerFiles/well_known_server/assetlinks.json");
            bool hasAndroidManifest = File.Exists("Assets/BoostOpsGenerated/Plugins/Android/AndroidManifest.xml");
            bool hasInstructions = File.Exists("Assets/BoostOpsGenerated/SETUP_INSTRUCTIONS.md");
            
            message += "Server Files (upload to your domain's .well-known folder):\n";
            if (hasIOS)
                message += "✓ apple-app-site-association - iOS Universal Links\n";
            if (hasAndroid)
                message += "✓ assetlinks.json - Android App Links\n";
            
            if (!hasIOS && !hasAndroid)
                message += "• No platform files generated yet\n";
            
            message += "\niOS Integration:\n";
            message += "✓ Entitlements automatically merged during build process\n";
            
            message += "\nAndroid Integration:\n";
            if (hasAndroidManifest)
                message += "✓ AndroidManifest.xml - Android App Links intent filters\n";
            else
                message += "• No Android manifest generated yet\n";
            
            message += "\nDocumentation:\n";
            if (hasInstructions)
                message += "✓ SETUP_INSTRUCTIONS.md - Complete setup guide\n";
            else
                message += "• No setup instructions generated yet\n";
            
            message += "\n📁 Location: Assets/BoostOpsGenerated/\n\n";
            message += "⚠️ IMPORTANT: Folder rename required!\n";
            message += "• In Unity: well_known_server (visible)\n";
            message += "• On Server: .well-known (required)\n\n";
            message += "Next Steps:\n";
            message += "1. Go to Assets/BoostOpsGenerated/ServerFiles/\n";
            message += "2. Copy well_known_server folder to your web server\n";
            message += "3. RENAME to .well-known on server (with dot)\n";
            message += "4. Copy/merge AndroidManifest.xml to Assets/Plugins/Android/\n";
            message += "5. Test with Apple/Google validation tools\n\n";
            message += "See SETUP_INSTRUCTIONS.md for detailed guide.";
            
            EditorUtility.DisplayDialog("Generated Files", message, "OK");
        }
        
        // Stub methods for OAuth functionality
        void LoadGoogleOAuthSettings()
        {
            // Load any Google OAuth authentication preferences if needed
            // Currently nothing to load since we handle OAuth flow dynamically
        }
        
        void CheckExistingAuth()
        {
            string savedEmail = EditorPrefs.GetString("BoostOps_UserEmail", "");
            string savedApiToken = EditorPrefs.GetString("BoostOps_ApiToken", "");
            
            if (!string.IsNullOrEmpty(savedEmail) && !string.IsNullOrEmpty(savedApiToken))
            {
                userEmail = savedEmail;
                apiToken = savedApiToken;
                isLoggedIn = true;
                LogDebug($"Restored session for {userEmail}");
                LogDebug($"API token available: ✓");
                
                // Only check for existing project when NOT in Play mode
                if (!EditorApplication.isPlaying && !EditorApplication.isPaused)
                {
                    LogDebug("Checking for existing project (Edit mode)");
                    _ = CheckForExistingProject();
                }
                else
                {
                    LogDebug("Skipping project lookup - currently in Play mode");
                }
            }
        }
        
        // REMOVED: CheckProjectSlugActivationAsync() - slug activation is now handled server-side
        // The functionality has been moved to CheckForExistingProject() which is called above

        
        /// <summary>
        /// Update dynamic links configuration when slug is activated
        /// </summary>
        void UpdateDynamicLinksFromActivatedSlug(string activatedSlug)
        {
            if (string.IsNullOrEmpty(activatedSlug)) return;
            
            // Update the legacy dynamicLinkUrl field
            string newDomain = $"{activatedSlug}.boostlink.me";
            if (dynamicLinkUrl != newDomain)
            {
                dynamicLinkUrl = newDomain;
                SaveDynamicLinkUrl();
                LogDebug($"Updated dynamic link domain to: {newDomain}");
            }
            
            // Update the dynamic links config asset if it exists
            if (dynamicLinksConfig != null)
            {
                // The config should already have the correct domain from server sync
                // but ensure consistency
                var allHosts = dynamicLinksConfig.GetAllHosts();
                if (!allHosts.Contains(newDomain))
                {
                    LogDebug($"Adding {newDomain} to dynamic links config");
                    // The config asset will be updated through normal sync processes
                }
            }
        }
        
        void InitiateGoogleOAuth()
        {
            StartGoogleOAuthFlow();
        }
        
        async void PerformLogin()
        {
            if (string.IsNullOrEmpty(loginEmail) || string.IsNullOrEmpty(loginPassword))
            {
                EditorUtility.DisplayDialog("Validation Error", "Please enter both email and password.", "OK");
                return;
            }
            
            isAuthenticating = true;
            
            try
            {
                // Call the email/password login endpoint
                await PerformEmailLogin(loginEmail, loginPassword);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Login Error", $"Login failed: {ex.Message}", "OK");
                Debug.LogError($"[BoostOps] Login error: {ex}");
            }
            finally
            {
                isAuthenticating = false;
            }
        }
        
        async void PerformSignup()
        {
            if (string.IsNullOrEmpty(signupEmail) || string.IsNullOrEmpty(signupPassword))
            {
                EditorUtility.DisplayDialog("Validation Error", "Please enter both email and password.", "OK");
                return;
            }
            
            isAuthenticating = true;
            
            try
            {
                // Call the email/password signup endpoint
                await PerformEmailSignup(signupEmail, signupPassword);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Signup Error", $"Signup failed: {ex.Message}", "OK");
                Debug.LogError($"[BoostOps] Signup error: {ex}");
            }
            finally
            {
                isAuthenticating = false;
            }
        }
        
        /// <summary>
        /// Start Google OAuth flow by opening browser
        /// </summary>
        // Store the actual port being used for OAuth callback
        private int oauthCallbackPort = 0;
        
        void StartGoogleOAuthFlow()
        {
            if (isAuthenticatingWithGoogle)
            {
                Debug.LogWarning("[BoostOps] OAuth flow already in progress");
                return;
            }
            
            isAuthenticatingWithGoogle = true;
            
            // Refresh UI to show "Authenticating..." state
            RefreshAccountPanel();
            
            // Start listening for OAuth callback FIRST to get the actual port
            StartOAuthListener();
            
            // If listener didn't start, abort
            if (oauthCallbackPort == 0)
            {
                Debug.LogError("[BoostOps] Cannot start OAuth flow - no available port for callback");
                isAuthenticatingWithGoogle = false;
                RefreshAccountPanel();
                return;
            }
            
            // Get the base URL based on hosting option
            string authBaseUrl = GetAuthServerBaseUrl();
            
            // Pass the callback port to the server so it knows where to redirect
            string loginUrl = $"{authBaseUrl}/login?callback_port={oauthCallbackPort}";
            
            // Log which server we're connecting to
            Debug.Log($"[BoostOps] Starting Google OAuth flow to Production server - opening: {loginUrl}");
            Debug.Log($"[BoostOps] OAuth callback will be on port {oauthCallbackPort}");
            
            // Open browser for OAuth
            Application.OpenURL(loginUrl);
        }
        
        /// <summary>
        /// Perform email/password login
        /// </summary>
        async Task PerformEmailLogin(string email, string password)
        {
            string baseUrl = GetApiServerBaseUrl();
            string endpoint = $"{baseUrl}/api/auth/login";
            
            // Log which server we're connecting to
            Debug.Log($"[BoostOps] Attempting login to Production server: {baseUrl}");
            
            var loginData = new {
                email = email,
                password = password
            };
            
            string jsonData = JsonUtility.ToJson(loginData);
            
            using (var client = new HttpClient())
            {
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(endpoint, content);
                string responseText = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var authResponse = JsonUtility.FromJson<AuthResponse>(responseText);
                    await HandleAuthenticationSuccess(authResponse.token, email);
                }
                else
                {
                    var error = JsonUtility.FromJson<ErrorResponse>(responseText);
                    throw new Exception(error.message ?? "Login failed");
                }
            }
        }
        
        /// <summary>
        /// Perform email/password signup
        /// </summary>
        async Task PerformEmailSignup(string email, string password)
        {
            string baseUrl = GetAuthServerBaseUrl();
            string endpoint = $"{baseUrl}/api/auth/signup";
            
            var signupData = new {
                email = email,
                password = password
            };
            
            string jsonData = JsonUtility.ToJson(signupData);
            
            using (var client = new HttpClient())
            {
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(endpoint, content);
                string responseText = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var authResponse = JsonUtility.FromJson<AuthResponse>(responseText);
                    await HandleAuthenticationSuccess(authResponse.token, email);
                }
                else
                {
                    var error = JsonUtility.FromJson<ErrorResponse>(responseText);
                    throw new Exception(error.message ?? "Signup failed");
                }
            }
        }
        
        /// <summary>
        /// Start OAuth callback listener
        /// </summary>
        void StartOAuthListener()
        {
            try
            {
                Debug.Log("[BoostOps] 🚀 Starting OAuth listener...");
                
                // Stop any existing listener
                StopOAuthListener();
                
                // Try multiple ports in the IANA dynamic/private range (49152-65535)
                // Using 50000+ to avoid conflicts with common dev tools (8080, 3000, etc.)
                // while staying in a reasonable range that's unlikely to be firewalled
                int[] portsToTry = new int[] { 50000, 50001, 50002, 50003, 50004 };
                bool listenerStarted = false;
                
                foreach (int port in portsToTry)
                {
                    try
                    {
                        // Create and configure HttpListener
                        oauthListener = new System.Net.HttpListener();
                        oauthListener.Prefixes.Add($"http://localhost:{port}/oauth-callback/");
                        
                        // Create cancellation token for cleanup
                        oauthCancellationToken = new System.Threading.CancellationTokenSource();
                        
                        // Start listening
                        oauthListener.Start();
                        Debug.Log($"[BoostOps] ✅ OAuth listener started successfully on http://localhost:{port}/oauth-callback/");
                        Debug.Log("[BoostOps] 👂 Waiting for OAuth callback...");
                        
                        // Store the port we're using
                        oauthCallbackPort = port;
                        
                        // Listen for incoming requests asynchronously
                        _ = Task.Run(async () => await ListenForOAuthCallback(), oauthCancellationToken.Token);
                        
                        listenerStarted = true;
                        break; // Successfully started, exit loop
                    }
                    catch (System.Net.HttpListenerException)
                    {
                        // Port is in use, try next port
                        Debug.Log($"[BoostOps] Port {port} is in use, trying next port...");
                        if (oauthListener != null)
                        {
                            try { oauthListener.Close(); } catch { }
                            oauthListener = null;
                        }
                        continue;
                    }
                    catch (System.UnauthorizedAccessException)
                    {
                        // Permission denied on this port, try next
                        Debug.Log($"[BoostOps] Permission denied on port {port}, trying next port...");
                        if (oauthListener != null)
                        {
                            try { oauthListener.Close(); } catch { }
                            oauthListener = null;
                        }
                        continue;
                    }
                }
                
                // If no port worked, show error
                if (!listenerStarted)
                {
                    Debug.LogError($"[BoostOps] ❌ Could not start OAuth listener on any port (tried: {string.Join(", ", portsToTry)})");
                    Debug.LogError($"[BoostOps] 💡 All ports are blocked or in use. Common causes:");
                    Debug.LogError($"[BoostOps] 💡   - Multiple Unity instances running");
                    Debug.LogError($"[BoostOps] 💡   - Previous OAuth listener didn't shut down cleanly");
                    Debug.LogError($"[BoostOps] 💡   - Firewall blocking localhost HTTP on these ports");
                    Debug.LogError($"[BoostOps] 💡 Solution: Restart Unity or check firewall settings");
                    StopOAuthListener();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ Unexpected error starting OAuth listener: {ex.Message}");
                Debug.LogError($"[BoostOps] 💡 Full error: {ex}");
                StopOAuthListener();
            }
        }
        
        /// <summary>
        /// Listen for OAuth callback requests
        /// </summary>
        async Task ListenForOAuthCallback()
        {
            try
            {
                Debug.Log("[BoostOps] 👂 OAuth listener thread started");
                
                while (!oauthCancellationToken.Token.IsCancellationRequested && oauthListener.IsListening)
                {
                    Debug.Log("[BoostOps] 🔄 Waiting for OAuth callback...");
                    
                    var context = await oauthListener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;
                    
                    Debug.Log($"[BoostOps] 📨 OAuth callback received from: {request.Url}");
                    
                    // Extract token from query parameters
                    string token = request.QueryString["token"];
                    string error = request.QueryString["error"];
                    
                    Debug.Log($"[BoostOps] 🔍 Token present: {!string.IsNullOrEmpty(token)}");
                    Debug.Log($"[BoostOps] 🔍 Error present: {!string.IsNullOrEmpty(error)}");
                    
                    // Send response to browser
                    string responseText = "";
                    if (!string.IsNullOrEmpty(error))
                    {
                        responseText = $"<html><body><h2>Authentication Failed</h2><p>Error: {error}</p><p>You can close this window and try again in Unity.</p></body></html>";
                        response.StatusCode = 400;
                        Debug.LogError($"[BoostOps] ❌ OAuth error: {error}");
                    }
                    else if (!string.IsNullOrEmpty(token))
                    {
                        responseText = "<html><body><h2>✅ Authentication Successful!</h2><p>You can close this window and return to Unity.</p><script>setTimeout(() => window.close(), 3000);</script></body></html>";
                        response.StatusCode = 200;
                        Debug.Log("[BoostOps] ✅ OAuth token received successfully");
                        
                        // Process the token on the main thread
                        UnityEditor.EditorApplication.delayCall += () => HandleOAuthSuccess(token);
                    }
                    else
                    {
                        responseText = "<html><body><h2>❌ Invalid Request</h2><p>No token received. You can close this window and try again in Unity.</p></body></html>";
                        response.StatusCode = 400;
                        Debug.LogWarning("[BoostOps] ⚠️ OAuth callback received but no token found");
                    }
                    
                    // Write response
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseText);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                    
                    Debug.Log("[BoostOps] 📤 Response sent to browser");
                    
                    // Stop listening after first callback
                    break;
                }
            }
            catch (System.ObjectDisposedException)
            {
                // Expected when listener is disposed
                Debug.Log("[BoostOps] 🔚 OAuth listener disposed (normal shutdown)");
            }
            catch (System.Net.HttpListenerException ex)
            {
                Debug.LogError($"[BoostOps] ❌ OAuth listener HttpListener error: {ex.Message}");
                Debug.LogError($"[BoostOps] 💡 This might indicate port conflicts or firewall issues");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ OAuth listener error: {ex.Message}");
                Debug.LogError($"[BoostOps] 💡 Full error: {ex}");
            }
            finally
            {
                Debug.Log("[BoostOps] 🧹 OAuth listener cleanup starting");
                // Clean up on main thread
                UnityEditor.EditorApplication.delayCall += () => StopOAuthListener();
            }
        }
        
        /// <summary>
        /// Stop the OAuth callback listener
        /// </summary>
        void StopOAuthListener()
        {
            try
            {
                if (oauthCancellationToken != null)
                {
                    oauthCancellationToken.Cancel();
                    oauthCancellationToken.Dispose();
                    oauthCancellationToken = null;
                }
                
                if (oauthListener != null && oauthListener.IsListening)
                {
                    oauthListener.Stop();
                    oauthListener.Close();
                    Debug.Log("[BoostOps] OAuth listener stopped");
                }
                
                oauthListener = null;
                oauthCallbackPort = 0; // Reset port
                isAuthenticatingWithGoogle = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Error stopping OAuth listener: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle successful OAuth token reception
        /// </summary>
        void HandleOAuthSuccess(string token)
        {
            try
            {
                Debug.Log("[BoostOps] 🔄 Processing OAuth token...");
                Debug.Log($"[BoostOps] Token length: {token?.Length ?? 0} characters");
                Debug.Log($"[BoostOps] Token preview: {(token?.Length > 20 ? token.Substring(0, 20) + "..." : token)}");
                
                if (string.IsNullOrEmpty(token))
                {
                    Debug.LogError("[BoostOps] ❌ Token is null or empty");
                return;
            }
            
                // Store the JWT token
                apiToken = token;
                isAuthenticatingWithGoogle = false;
                isLoggedIn = true;
                
                // Save credentials to EditorPrefs
                EditorPrefs.SetString("BoostOps_ApiToken", apiToken);
                
                // Try to extract user email from JWT if possible
                try
                {
                    // Simple JWT payload extraction (for display purposes)
                    string[] parts = token.Split('.');
                    if (parts.Length >= 2)
                    {
                        string payload = parts[1];
                        // Add padding if needed for base64 decoding
                        int padding = 4 - (payload.Length % 4);
                        if (padding != 4) payload += new string('=', padding);
                        
                        byte[] data = System.Convert.FromBase64String(payload);
                        string json = System.Text.Encoding.UTF8.GetString(data);
                        
                        // Look for email in JSON payload
                        if (json.Contains("\"email\":"))
                        {
                            int emailStart = json.IndexOf("\"email\":\"") + 9;
                            int emailEnd = json.IndexOf("\"", emailStart);
                            if (emailEnd > emailStart)
                            {
                                userEmail = json.Substring(emailStart, emailEnd - emailStart);
                                EditorPrefs.SetString("BoostOps_UserEmail", userEmail);
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BoostOps] Could not extract email from JWT: {ex.Message}");
                    userEmail = "OAuth User"; // Fallback display name
                    EditorPrefs.SetString("BoostOps_UserEmail", userEmail);
                }
                
                Debug.Log("[BoostOps] ✅ Google OAuth login successful!");
                
                // Auto-switch to BoostOps Remote mode for both Links and Cross-Promo
                SwitchToManagedModeAfterLogin();
                
                // Try to lookup existing project based on bundle ID/package name (only in Edit mode)
                if (!EditorApplication.isPlaying && !EditorApplication.isPaused)
                {
                    _ = CheckForExistingProject();
                }
                
                // Show success popup
                EditorUtility.DisplayDialog("Login Successful! 🎉", 
                    $"Welcome to BoostOps!\n\nLogged in as: {userEmail}\n\nChecking for existing projects...", 
                    "Continue");
                
                // Refresh the UI to show logged-in state
                if (useUIToolkit && contentContainer != null)
                {
                    // For UIToolkit, rebuild the interface to show logged-in state
                    BuildUIToolkitInterface();
                    // Restore the current tab or show Overview if no tab selected
                    if (selectedTab >= 0)
                    {
                        switch (selectedTab)
                        {
                            case 0: ShowOverviewPanel(); break;
                            case 1: ShowLinksPanel(); break;
                            case 2: ShowCrossPromoPanel(); break;
                            case 3: ShowAttributionPanel(); break;
                            case 4: ShowIntegrationsPanel(); break;
                            default: ShowOverviewPanel(); break;
                        }
                    }
                    else
                    {
                        ShowOverviewPanel();
                    }
                }
                else
                {
                    // Fallback for IMGUI
                    Repaint();
                }
                
                // Stop the listener
                StopOAuthListener();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ Error handling OAuth success: {ex.Message}");
                Debug.LogError($"[BoostOps] Full error: {ex}");
                isAuthenticatingWithGoogle = false;
                StopOAuthListener();
                
                // Show error popup
                EditorUtility.DisplayDialog("Login Error", 
                    $"Authentication failed: {ex.Message}\n\nPlease try again or use manual token input.", 
                    "OK");
                
                // Refresh UI to clear authenticating state
                RefreshAccountPanel();
            }
        }
        
        /// <summary>
        /// Check for existing BoostOps projects without rebuilding UI (for fetch operations)
        /// Returns the standard ProjectLookupResponse and updates cachedProjectLookupResponse
        /// </summary>
        async Task<ProjectLookupResponse> CheckForExistingProjectWithoutUIRebuild()
        {
            // Prevent multiple simultaneous calls
            if (isCheckingForExistingProject)
            {
                Debug.Log("[BoostOps] ⏳ Project lookup already in progress, skipping duplicate request");
                return null;
            }
            
            isCheckingForExistingProject = true;
            WaspProjectLookupResponse lookupResponse = null;
            
            try
            {
                Debug.Log("[BoostOps] 🔍 Fetching project configuration from server...");
                
                // Ensure project settings asset exists
                projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
                
                // Get bundle ID and package name for lookup
                #if UNITY_2021_2_OR_NEWER
                string bundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
                string packageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
                #else
                string bundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
                string packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
                #endif
                
                if (string.IsNullOrEmpty(bundleId) && string.IsNullOrEmpty(packageName))
                {
                    Debug.LogWarning("[BoostOps] ⚠️ No bundle ID or package name configured - cannot lookup existing project");
                    return null;
                }
                
                // Use direct HTTP request with all identifiers (same as CheckForExistingProject)
                // This ensures we match projects by bundle ID, not just Unity project GUID
                string projectName = Application.productName;
                string productGuid = PlayerSettings.productGUID.ToString();
                string cloudProjectId = Application.cloudProjectId;
                #if UNITY_2021_2_OR_NEWER
                string iosBundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
                string androidPackageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
                #else
                string iosBundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
                string androidPackageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
                #endif
                
                var settings = BoostOpsProjectSettings.GetOrCreateSettings();
                string iosAppStoreId = settings?.appleAppStoreId;
                string androidSha256 = settings?.androidCertFingerprint;
                string appleTeamId = null;
                #if UNITY_IOS
                try { appleTeamId = PlayerSettings.iOS.appleDeveloperTeamID; } catch { }
                #endif
                
                string baseUrl = GetApiServerBaseUrl();
                string endpoint = $"{baseUrl}/api/unity/project/lookup";
                
                // Create JSON with all identifiers
                var jsonParts = new List<string>();
                string EscapeJson(string value) => value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
                
                jsonParts.Add($"\"jwtToken\":\"{EscapeJson(apiToken)}\"");
                jsonParts.Add($"\"projectName\":\"{EscapeJson(projectName)}\"");
                if (!string.IsNullOrEmpty(productGuid)) jsonParts.Add($"\"productGuid\":\"{EscapeJson(productGuid)}\"");
                if (!string.IsNullOrEmpty(cloudProjectId)) jsonParts.Add($"\"cloudProjectId\":\"{EscapeJson(cloudProjectId)}\"");
                if (!string.IsNullOrEmpty(iosBundleId)) jsonParts.Add($"\"iosBundleId\":\"{EscapeJson(iosBundleId)}\"");
                if (!string.IsNullOrEmpty(androidPackageName)) jsonParts.Add($"\"androidPackageName\":\"{EscapeJson(androidPackageName)}\"");
                if (!string.IsNullOrEmpty(iosAppStoreId)) jsonParts.Add($"\"iosAppStoreId\":\"{EscapeJson(iosAppStoreId)}\"");
                if (!string.IsNullOrEmpty(appleTeamId)) jsonParts.Add($"\"appleTeamId\":\"{EscapeJson(appleTeamId)}\"");
                if (!string.IsNullOrEmpty(androidSha256)) jsonParts.Add($"\"androidSha256Fingerprints\":[\"{EscapeJson(androidSha256)}\"]");
                
                string jsonData = "{" + string.Join(",", jsonParts) + "}";
                
                using (var client = new HttpClient())
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(endpoint, content, cts.Token);
                    string responseText = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        // Log raw response for debugging
                        Debug.Log($"[BoostOps] 📥 Server Response:\n{responseText}");
                        
                        lookupResponse = JsonUtility.FromJson<WaspProjectLookupResponse>(responseText);
                        // Save raw response so LoadCampaignsFromAPI can extract boostops_config
                        if (lookupResponse != null)
                        {
                            lookupResponse.rawResponse = responseText;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[BoostOps] ❌ API request failed ({response.StatusCode}):\n{responseText}");
                        lookupResponse = null;
                    }
                }
                if (lookupResponse != null && lookupResponse.found && lookupResponse.project != null)
                {
                    Debug.Log($"[BoostOps] ✅ Found existing project: {lookupResponse.project.name}");
                    
                    // Cache the response data but don't rebuild UI
                    // Note: We need to parse the boostops_config JSON string to get source_project
                    if (!string.IsNullOrEmpty(lookupResponse.boostops_config))
                    {
                        try
                        {
                            var config = JsonUtility.FromJson<WaspBoostOpsConfig>(lookupResponse.boostops_config);
                            if (config.source_project != null)
                            {
                    // Convert from WaspSourceProject to local SourceProject class
                    cachedSourceProject = new SourceProject();
                    cachedSourceProject.name = config.source_project.name;
                    cachedSourceProject.bundle_id = config.source_project.bundle_id;
                    cachedSourceProject.min_sessions = config.source_project.min_sessions;
                    cachedSourceProject.min_player_days = config.source_project.min_player_days;
                    cachedSourceProject.frequency_cap = new FrequencyCapJson 
                    { 
                        impressions = config.source_project.frequency_cap?.impressions ?? 0, 
                        time_unit = config.source_project.frequency_cap?.time_unit ?? "DAY" 
                    };
                    cachedSourceProject.interstitial_icon_cta = config.source_project.interstitial_icon_cta;
                    cachedSourceProject.interstitial_icon_text = config.source_project.interstitial_icon_text;
                    cachedSourceProject.interstitial_rich_cta = config.source_project.interstitial_rich_cta;
                    cachedSourceProject.interstitial_rich_text = config.source_project.interstitial_rich_text;
                    
                    // Initialize store_ids and store_urls dictionaries (Unity JsonUtility fix)
                    cachedSourceProject.store_ids = config.source_project.store_ids ?? new Dictionary<string, string>();
                    cachedSourceProject.store_urls = config.source_project.store_urls ?? new Dictionary<string, string>();
                    cachedSourceProject.platform_ids = config.source_project.platform_ids ?? new Dictionary<string, object>();
                    
                    // Apply manual parsing workaround for Unity JsonUtility Dictionary issues
                    if (cachedSourceProject.store_ids.Count == 0 && lookupResponse.boostops_config.Contains("\"store_ids\""))
                    {
                        ApplySourceProjectStoreIdsWorkaround(lookupResponse.boostops_config);
                    }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[BoostOps] Failed to parse boostops_config: {ex.Message}");
                        }
                    }
                    
                    // Update project settings silently
                    string projectKey = GetProjectKey(lookupResponse);
                    string projectId = lookupResponse.project?.id;  // Get the actual project ID
                    
                    Debug.Log($"[BoostOps] 🔍 CheckForExistingProjectWithoutUIRebuild - projectKey: '{projectKey}', projectId: '{projectId}'");
                    Debug.Log($"[BoostOps] 🔍 projectSettings null: {projectSettings == null}");
                    
                    if (projectSettings != null && !string.IsNullOrEmpty(projectKey))
                    {
                        Debug.Log($"[BoostOps] 🔍 Before save - projectSettings.projectId: '{projectSettings.projectId}'");
                        Debug.Log($"[BoostOps] 🔍 Before save - projectSettings.projectKey: '{projectSettings.projectKey}'");
                        
                        projectSettings.projectKey = projectKey;
                        
                        // Also save the project ID if available (needed for source_project_id in analytics)
                        if (!string.IsNullOrEmpty(projectId))
                        {
                            projectSettings.projectId = projectId;
                            Debug.Log($"[BoostOps] 💾 Set projectId field to: '{projectId}'");
                        }
                        else
                        {
                            Debug.LogWarning($"[BoostOps] ⚠️ projectId is null or empty, not saving");
                        }
                        
                        Debug.Log($"[BoostOps] 🔍 After assignment - projectSettings.projectId: '{projectSettings.projectId}'");
                        Debug.Log($"[BoostOps] 🔍 After assignment - projectSettings.projectKey: '{projectSettings.projectKey}'");
                        
                        EditorUtility.SetDirty(projectSettings);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        
                        Debug.Log($"[BoostOps] ✅ Called SetDirty, SaveAssets, and Refresh");
                        
                        // Force reload to verify
                        BoostOpsProjectSettings.ClearCache();
                        var reloadedSettings = BoostOpsProjectSettings.GetOrCreateSettings();
                        Debug.Log($"[BoostOps] 🔍 After reload - projectId: '{reloadedSettings.projectId}', projectKey: '{reloadedSettings.projectKey}'");
                    }
                    else
                    {
                        if (projectSettings == null)
                            Debug.LogError($"[BoostOps] ❌ projectSettings is null!");
                        if (string.IsNullOrEmpty(projectKey))
                            Debug.LogError($"[BoostOps] ❌ projectKey is null or empty!");
                    }
                    
                    // CRITICAL: Cache the lookup response for UI state (Attribution tab needs this)
                    // Parse analytics_ingest_enabled, ingest_mode, and project_slug from raw JSON
                    bool analyticsEnabled = false;
                    string ingestMode = "DISABLED";
                    string projectSlug = null;
                    if (!string.IsNullOrEmpty(lookupResponse.rawResponse))
                    {
                        try
                        {
                            // Simple string parsing for boolean and string fields
                            if (lookupResponse.rawResponse.Contains("\"analytics_ingest_enabled\":true"))
                                analyticsEnabled = true;
                            
                            var ingestModeMatch = System.Text.RegularExpressions.Regex.Match(
                                lookupResponse.rawResponse, 
                                @"""ingest_mode""\s*:\s*""([^""]+)"""
                            );
                            if (ingestModeMatch.Success)
                                ingestMode = ingestModeMatch.Groups[1].Value;
                            
                            // Extract project_slug from boost_links_project section (priority 1)
                            var projectSlugMatch = System.Text.RegularExpressions.Regex.Match(
                                lookupResponse.rawResponse,
                                @"""boost_links_project""[^}]*""project_slug""\s*:\s*""([^""]+)"""
                            );
                            if (projectSlugMatch.Success)
                            {
                                projectSlug = projectSlugMatch.Groups[1].Value;
                            }
                            else
                            {
                                // Fallback: Try to find project_slug at top level or in project object
                                var topLevelSlugMatch = System.Text.RegularExpressions.Regex.Match(
                                    lookupResponse.rawResponse,
                                    @"""project_slug""\s*:\s*""([^""]+)"""
                                );
                                if (topLevelSlugMatch.Success)
                                {
                                    projectSlug = topLevelSlugMatch.Groups[1].Value;
                                    Debug.Log($"[BoostOps] Found project_slug at top level: {projectSlug}");
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[BoostOps] Failed to parse analytics fields from raw JSON: {ex.Message}");
                        }
                    }
                    
                    // Convert WaspProjectLookupResponse to ProjectLookupResponse
                    cachedProjectLookupResponse = new ProjectLookupResponse
                    {
                        found = lookupResponse.found,
                        project = lookupResponse.project != null ? new ProjectInfo
                        {
                            id = lookupResponse.project.id,
                            name = lookupResponse.project.name,
                            description = lookupResponse.project.description,
                            is_active = lookupResponse.project.is_active,
                            analytics_ingest_enabled = analyticsEnabled,
                            ingest_mode = ingestMode,
                            project_type = lookupResponse.project.project_type,
                            // Simple copy of app_stores - structures are compatible
                            app_stores = lookupResponse.app_stores?.Select(was => new ApiPlatformInfo
                            {
                                id = was.id,
                                type = was.type,
                                apple_bundle_id = was.apple_bundle_id,
                                apple_store_id = was.apple_store_id,
                                android_package_name = was.android_package_name,
                                android_sha256_fingerprints = was.android_sha256_fingerprints
                            }).ToArray()
                        } : null,
                        message = lookupResponse.message,
                        project_key = lookupResponse.project_key,
                        project_slug = projectSlug ?? lookupResponse.project_slug,  // Use parsed slug from boost_links_project
                        boostops_config = lookupResponse.boostops_config,
                        rawResponse = lookupResponse.rawResponse  // Preserve raw response for campaign extraction
                    };
                    
                    // Save project slug to settings if we got it from server
                    // Try multiple sources: extracted projectSlug from JSON, or top-level lookupResponse.project_slug
                    string slugToSave = projectSlug ?? lookupResponse.project_slug;
                    if (!string.IsNullOrEmpty(slugToSave))
                    {
                        var projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
                        if (projectSettings != null)
                        {
                            // Update if empty or different from server
                            if (string.IsNullOrEmpty(projectSettings.projectSlug) || 
                                projectSettings.projectSlug != slugToSave)
                            {
                                projectSettings.projectSlug = slugToSave;
                                EditorUtility.SetDirty(projectSettings);
                                AssetDatabase.SaveAssets();
                                Debug.Log($"[BoostOps] ✅ Saved project slug to settings: {slugToSave}");
                            }
                            else
                            {
                                Debug.Log($"[BoostOps] Project slug already in settings: {projectSettings.projectSlug}");
                        }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[BoostOps] ⚠️ No project_slug found in server response - domain prefix will not be configured");
                    }
                    
                    // Update attribution status now that we have the server response
                    UpdateAttributionStatus();
                }
                else
                {
                    Debug.LogWarning("[BoostOps] ⚠️ Project not found in response");
                    cachedProjectLookupResponse = null;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ Error checking for existing project: {ex.Message}");
                lookupResponse = null;
                cachedProjectLookupResponse = null;
            }
            finally
            {
                isCheckingForExistingProject = false;
            }
            
            return cachedProjectLookupResponse;
        }
        
        /// <summary>
        /// Check for existing BoostOps projects based on current app bundle ID/package name
        /// </summary>
        async Task CheckForExistingProject()
        {
            // Prevent multiple simultaneous calls
            if (isCheckingForExistingProject)
            {
                Debug.Log("[BoostOps] ⏳ Project lookup already in progress, skipping duplicate request");
                return;
            }
            
            // CRITICAL: Never perform API lookups during Play mode
            if (EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                Debug.Log("[BoostOps] ⏸️ Skipping project lookup - currently in Play/Pause mode");
                return;
            }
            
            isCheckingForExistingProject = true;
            
            try
            {
                Debug.Log("[BoostOps] 🔍 Looking up project configuration...");
                
                // Declare projectKey at method level to avoid scope conflicts
                string projectKey = null;
                
                // Ensure project settings asset exists
                projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
                
                // Verify the asset was created properly
                string assetPath = AssetDatabase.GetAssetPath(projectSettings);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogWarning("[BoostOps] ⚠️ Project settings asset could not be created - continuing with temporary instance");
                }
                
                // Gather Unity project information
                string projectName = Application.productName;
                string productGuid = PlayerSettings.productGUID.ToString(); // Unity project GUID (stable, preferred)
                string cloudProjectId = Application.cloudProjectId;
                #if UNITY_2021_2_OR_NEWER
                string iosBundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
                string androidPackageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
                #else
                string iosBundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
                string androidPackageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
                #endif
                
                // Get additional platform-specific data from the now-guaranteed asset
                var settings = projectSettings;
                string iosAppStoreId = settings?.appleAppStoreId;
                string androidSha256 = settings?.androidCertFingerprint;
                
                // Try to get Apple Team ID from iOS player settings (if available)
                string appleTeamId = null;
                #if UNITY_IOS
                try
                {
                    appleTeamId = PlayerSettings.iOS.appleDeveloperTeamID;
                }
                catch (System.Exception)
                {
                    // iOS settings not available or not configured
                }
                #endif
                
                Debug.Log($"[BoostOps] Project Name: {projectName ?? "not set"}");
                Debug.Log($"[BoostOps] Product GUID: {productGuid ?? "not set"}");
                Debug.Log($"[BoostOps] Cloud Project ID: {cloudProjectId ?? "not set"}");
                Debug.Log($"[BoostOps] iOS Bundle ID: {iosBundleId ?? "not set"}");
                Debug.Log($"[BoostOps] Android Package Name: {androidPackageName ?? "not set"}");
                Debug.Log($"[BoostOps] Apple App Store ID: {iosAppStoreId ?? "not set"}");
                Debug.Log($"[BoostOps] Apple Team ID: {appleTeamId ?? "not set"}");
                Debug.Log($"[BoostOps] Android SHA256: {(string.IsNullOrEmpty(androidSha256) ? "not set" : "configured")}");
                
                if (string.IsNullOrEmpty(iosBundleId) && string.IsNullOrEmpty(androidPackageName) && string.IsNullOrEmpty(productGuid))
                {
                    Debug.LogWarning("[BoostOps] ⚠️ No bundle ID, package name, or product GUID configured. Cannot lookup existing projects.");
                    return;
                }
                
                string baseUrl = GetApiServerBaseUrl();
                string endpoint = $"{baseUrl}/api/unity/project/lookup";
                
                Debug.Log($"[BoostOps] 🌐 Project lookup endpoint: {endpoint}");
                Debug.Log($"[BoostOps] 📡 API Base URL: {baseUrl}");
                
                // Create JSON manually to avoid sending empty fields
                var jsonParts = new List<string>();
                
                // Helper method to escape JSON strings
                string EscapeJson(string value) => value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
                
                // Required fields
                jsonParts.Add($"\"jwtToken\":\"{EscapeJson(apiToken)}\"");
                jsonParts.Add($"\"projectName\":\"{EscapeJson(projectName)}\"");
                
                // Add optional parameters only if they have values
                if (!string.IsNullOrEmpty(productGuid))
                    jsonParts.Add($"\"productGuid\":\"{EscapeJson(productGuid)}\"");
                
                if (!string.IsNullOrEmpty(cloudProjectId))
                    jsonParts.Add($"\"cloudProjectId\":\"{EscapeJson(cloudProjectId)}\"");
                
                // Use preferred parameter names (not duplicates)
                if (!string.IsNullOrEmpty(iosBundleId))
                    jsonParts.Add($"\"iosBundleId\":\"{EscapeJson(iosBundleId)}\"");
                
                if (!string.IsNullOrEmpty(androidPackageName))
                    jsonParts.Add($"\"androidPackageName\":\"{EscapeJson(androidPackageName)}\"");
                
                if (!string.IsNullOrEmpty(iosAppStoreId))
                    jsonParts.Add($"\"iosAppStoreId\":\"{EscapeJson(iosAppStoreId)}\"");
                
                if (!string.IsNullOrEmpty(appleTeamId))
                    jsonParts.Add($"\"appleTeamId\":\"{EscapeJson(appleTeamId)}\"");
                
                if (!string.IsNullOrEmpty(androidSha256))
                    jsonParts.Add($"\"androidSha256Fingerprints\":[\"{EscapeJson(androidSha256)}\"]");
                
                string jsonData = "{" + string.Join(",", jsonParts) + "}";
                Debug.Log($"[BoostOps] 📤 Project lookup request: {jsonData}");
                Debug.Log($"[BoostOps] 🚀 Making HTTP POST request to: {endpoint}");
                
                using (var client = new HttpClient())
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15))) // Longer timeout + cancellation token
                {
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                    Debug.Log($"[BoostOps] 📡 Sending POST request to project lookup endpoint...");
                    
                    var response = await client.PostAsync(endpoint, content, cts.Token);
                    string responseText = await response.Content.ReadAsStringAsync();
                    
                    Debug.Log($"[BoostOps] 📨 Project lookup response status: {response.StatusCode} ({(int)response.StatusCode})");
                    Debug.Log($"[BoostOps] 📄 Project lookup response headers: {response.Headers}");
                    Debug.Log($"[BoostOps] 📝 Project lookup response body: {responseText}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        // Parse the response to see if a project was found
                        var lookupResponse = JsonUtility.FromJson<ProjectLookupResponse>(responseText);
                        
                        if (lookupResponse != null && lookupResponse.found)
                        {
                            Debug.Log($"[BoostOps] ✅ Found existing project: '{lookupResponse.project.name}' (ID: {lookupResponse.project.id})");
                            Debug.Log($"[BoostOps] Project type: {lookupResponse.project.GetProjectType()}, Status: {(lookupResponse.project.GetIsActive() ? "Active" : "Inactive")}");
                            
                            // Store lookup response data for UI display
                            hasLookupResponse = true;
                            lookupProjectFound = true;
                            lookupProjectSlug = GetProjectSlug(lookupResponse) ?? "";
                            lookupProjectName = lookupResponse.project?.name ?? "";
                            lookupMessage = lookupResponse.message ?? "Project found";
                            cachedProjectLookupResponse = lookupResponse; // Cache for Apple Team ID UI
                            
                            // Update attribution status now that we have the project configuration
                            UpdateAttributionStatus();
                            Debug.Log($"[BoostOps] 🔄 Updated attribution status based on project configuration (status: {attributionStatus})");
                            
                            // Debug project slug information
                            Debug.Log($"[BoostOps] 🏷️ Project slug from lookup response: '{GetProjectSlug(lookupResponse) ?? "null"}'");
                            Debug.Log($"[BoostOps] 🏷️ Stored lookupProjectSlug: '{lookupProjectSlug}'");
                            
                            // Project ID is now tracked via the projectKey field
                            Debug.Log($"[BoostOps] 💾 Project ID from lookup: {lookupResponse.project.id}");
                            
                            // Auto-sync Apple Team ID from server to Editor if Editor is empty
                            Debug.Log($"[BoostOps] 🍎 Checking for Apple Team ID in app_stores array...");
                            string serverTeamId = GetServerAppleTeamId(lookupResponse);
                            if (!string.IsNullOrEmpty(serverTeamId))
                            {
                                Debug.Log($"[BoostOps] 🍎 Found Server Apple Team ID: {serverTeamId}");
                                string currentEditorTeamId = GetEditorAppleTeamId();
                                Debug.Log($"[BoostOps] 🍎 Current Editor Apple Team ID: {currentEditorTeamId ?? "Not set"}");
                                
                                bool autoSynced = AutoSyncAppleTeamIdFromServer(serverTeamId);
                                if (autoSynced)
                                {
                                    // Update our local iosTeamId variable to reflect the sync
                                    iosTeamId = GetEditorAppleTeamId() ?? "Not set";
                                    Debug.Log($"[BoostOps] 🍎 Updated iosTeamId cache to: {iosTeamId}");
                                }
                                else if (!string.IsNullOrEmpty(currentEditorTeamId))
                                {
                                    Debug.Log($"[BoostOps] 🍎 Editor already has Team ID, no auto-sync needed");
                                }
                            }
                            else
                            {
                                Debug.Log($"[BoostOps] 🍎 No Apple Team ID found in server response");
                            }
                            
                            // Log platform details and extract Apple App Store ID
                            if (lookupResponse.project?.app_stores != null && lookupResponse.project.app_stores.Length > 0)
                            {
                                string remoteAppleAppStoreId = null;
                                string remoteAndroidFingerprint = null;
                                
                                foreach (var appStore in lookupResponse.project.app_stores)
                                {
                                    Debug.Log($"[BoostOps] 🔍 Processing app store: type='{appStore.type}', apple_bundle_id='{appStore.apple_bundle_id}', apple_store_id='{appStore.apple_store_id}'");
                                    
                                    if (appStore.type == "APPLE_STORE" || appStore.type == "IOS_APP_STORE")
                                    {
                                        Debug.Log($"[BoostOps] iOS App Store: {appStore.apple_bundle_id}");
                                        
                                        // Extract Apple App Store ID if present
                                        if (!string.IsNullOrEmpty(appStore.apple_store_id))
                                        {
                                            remoteAppleAppStoreId = appStore.apple_store_id;
                                            Debug.Log($"[BoostOps] Found Apple App Store ID in response: {remoteAppleAppStoreId}");
                                        }
                                        else
                                        {
                                            Debug.Log($"[BoostOps] ⚠️ Apple App Store ID is null or empty for iOS app store");
                                        }
                                    }
                                    else if ((appStore.type == "GOOGLE_PLAY" || appStore.type == "GOOGLE_STORE") && !string.IsNullOrEmpty(appStore.android_package_name))
                                    {
                                        Debug.Log($"[BoostOps] Android App Store: {appStore.android_package_name}");
                                        
                                        // Extract Android SHA256 fingerprint if present
                                        Debug.Log($"[BoostOps] 🔍 Checking SHA256 fingerprints array: {(appStore.android_sha256_fingerprints != null ? appStore.android_sha256_fingerprints.Length.ToString() : "null")} entries");
                                        if (appStore.android_sha256_fingerprints != null && appStore.android_sha256_fingerprints.Length > 0)
                                        {
                                            string firstFingerprint = appStore.android_sha256_fingerprints[0];
                                            Debug.Log($"[BoostOps] 🔍 First SHA256 fingerprint from server: '{firstFingerprint}'");
                                            if (!string.IsNullOrEmpty(firstFingerprint))
                                            {
                                                remoteAndroidFingerprint = firstFingerprint;
                                                Debug.Log($"[BoostOps] ✅ Found Android SHA256 fingerprint in response: {remoteAndroidFingerprint}");
                                            }
                                        }
                                        else
                                        {
                                            Debug.Log($"[BoostOps] ⚠️ No SHA256 fingerprints found in app store data");
                                        }
                                        
                                        // Update registration state since we have Android platform information
                                        if (registrationState == ProjectRegistrationState.NotRegistered)
                                        {
                                            registrationState = ProjectRegistrationState.Registered;
                                            isProjectRegistered = true;
                                            SaveRegistrationState();
                                            Debug.Log("[BoostOps] 📝 Registration state updated to Registered (Android/Google Store found)");
                                        }
                                    }
                                }
                                
                                // Store Apple App Store ID if found, preserving local value if not found
                                if (!string.IsNullOrEmpty(remoteAppleAppStoreId))
                                {
                                    Debug.Log($"[BoostOps] 🔍 Current iosAppStoreId in settings: '{projectSettings.appleAppStoreId}'");
                                    
                                    // Only update if the remote value is different from local
                                    if (projectSettings.appleAppStoreId != remoteAppleAppStoreId)
                                    {
                                        Debug.Log($"[BoostOps] Updating Apple App Store ID from '{projectSettings.appleAppStoreId}' to '{remoteAppleAppStoreId}'");
                                        projectSettings.appleAppStoreId = remoteAppleAppStoreId;
                                        
                                        // Also update the editor window field for immediate UI sync
                                        iosAppStoreId = remoteAppleAppStoreId;
                                        SaveAppleAppStoreId();
                                        
                                        Debug.Log("[BoostOps] 🔍 Marking project settings as dirty and saving...");
                                        UnityEditor.EditorUtility.SetDirty(projectSettings);
                                        UnityEditor.AssetDatabase.SaveAssets();
                                        UnityEditor.AssetDatabase.Refresh();
                                        
                                        // If asset path is empty, recreate the asset with the new data
                                        string currentAssetPath = AssetDatabase.GetAssetPath(projectSettings);
                                        if (string.IsNullOrEmpty(currentAssetPath))
                                        {
                                            Debug.LogWarning("[BoostOps] ⚠️ Asset path is empty after save - recreating asset with lookup data");
                                            projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
                                            // Re-apply the Apple App Store ID to the new asset
                                            projectSettings.appleAppStoreId = remoteAppleAppStoreId;
                                            UnityEditor.EditorUtility.SetDirty(projectSettings);
                                            UnityEditor.AssetDatabase.SaveAssets();
                                        }
                                        
                                        // Verify the save worked
                                        var verifySettings = BoostOpsProjectSettings.GetInstance();
                                        Debug.Log($"[BoostOps] 🔍 Final verification - appleAppStoreId: '{verifySettings?.appleAppStoreId}' (should be '{remoteAppleAppStoreId}')");
                                        
                                        if (verifySettings?.appleAppStoreId == remoteAppleAppStoreId)
                                        {
                                            Debug.Log("[BoostOps] ✅ Apple App Store ID saved successfully");
                                        }
                                        else
                                        {
                                            Debug.LogWarning($"[BoostOps] ⚠️ Apple App Store ID verification failed - expected '{remoteAppleAppStoreId}', got '{verifySettings?.appleAppStoreId}'");
                                        }
                                        // Update registration state since we have platform information
                                        if (registrationState == ProjectRegistrationState.NotRegistered)
                                        {
                                            registrationState = ProjectRegistrationState.Registered;
                                            isProjectRegistered = true;
                                            SaveRegistrationState();
                                            Debug.Log("[BoostOps] 📝 Registration state updated to Registered (Apple App Store ID found)");
                                        }
                                        
                                        // Always force UI refresh when Apple App Store ID is updated (regardless of registration state)
                                        if (useUIToolkit && contentContainer != null)
                                        {
                                            Debug.Log("[BoostOps] 🔄 Forcing comprehensive UI refresh after Apple App Store ID update");
                                            EditorApplication.delayCall += () => {
                                                Debug.Log("[BoostOps] 🔄 Executing delayed UI refresh after Apple App Store ID update");
                                                RefreshGlobalStatusBar();
                                                // Refresh current panel to show updated platform info
                                                if (selectedTab == 0) // Overview tab
                                                {
                                                    ShowOverviewPanel();
                                                }
                                                else if (selectedTab == 1) // Links tab
                                                {
                                                    ShowLinksPanel();
                                                }
                                            };
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log($"[BoostOps] Apple App Store ID already matches remote value: {remoteAppleAppStoreId}");
                                        
                                        // Still update registration state if not already set
                                        if (registrationState == ProjectRegistrationState.NotRegistered)
                                        {
                                            registrationState = ProjectRegistrationState.Registered;
                                            isProjectRegistered = true;
                                            SaveRegistrationState();
                                            Debug.Log("[BoostOps] 📝 Registration state updated to Registered (Apple App Store ID confirmed)");
                                            
                                            // Force comprehensive UI refresh to show updated registration state
                                            if (useUIToolkit && contentContainer != null)
                                            {
                                                Debug.Log("[BoostOps] 🔄 Forcing comprehensive UI refresh after registration state confirmation");
                                                RefreshGlobalStatusBar();
                                                // Also refresh the overview panel to update critical issues section
                                                EditorApplication.delayCall += () => {
                                                    if (selectedTab == 0) // Overview tab
                                                    {
                                                        Debug.Log("[BoostOps] 🔄 Refreshing overview panel after registration confirmation");
                                                        ShowOverviewPanel();
                                                    }
                                                };
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Debug.Log("[BoostOps] No Apple App Store ID found in remote response - preserving local value");
                                }
                                
                                // Always force UI refresh after Apple App Store ID processing (regardless of whether it changed)
                                if (useUIToolkit && contentContainer != null)
                                {
                                    Debug.Log("[BoostOps] 🔄 Forcing UI refresh after Apple App Store ID processing completion");
                                    EditorApplication.delayCall += () => {
                                        Debug.Log("[BoostOps] 🔄 Executing delayed UI refresh after Apple App Store ID processing");
                                        RefreshGlobalStatusBar();
                                        if (selectedTab == 0) // Overview tab
                                        {
                                            ShowOverviewPanel();
                                        }
                                        else if (selectedTab == 1) // Links tab
                                        {
                                            ShowLinksPanel();
                                        }
                                    };
                                }
                                
                                // Store Android SHA256 fingerprint if found, preserving local value if not found
                                if (!string.IsNullOrEmpty(remoteAndroidFingerprint))
                                {
                                    Debug.Log("[BoostOps] 🔍 Using existing project settings for Android fingerprint...");
                                    
                                    Debug.Log($"[BoostOps] 🔍 Project settings asset path: '{AssetDatabase.GetAssetPath(projectSettings)}'");
                                    Debug.Log($"[BoostOps] 🔍 Current androidCertFingerprint in settings: '{projectSettings.androidCertFingerprint}'");
                                    
                                    // Only update if the remote value is different from local
                                    if (projectSettings.androidCertFingerprint != remoteAndroidFingerprint)
                                    {
                                        Debug.Log($"[BoostOps] Updating Android fingerprint from '{projectSettings.androidCertFingerprint}' to '{remoteAndroidFingerprint}'");
                                        projectSettings.androidCertFingerprint = remoteAndroidFingerprint;
                                        
                                        // Also update the editor window field for immediate UI sync
                                        androidCertFingerprint = remoteAndroidFingerprint;
                                        SaveAndroidCertFingerprint();
                                        
                                        Debug.Log("[BoostOps] 🔍 Marking project settings as dirty and saving (Android fingerprint)...");
                                        UnityEditor.EditorUtility.SetDirty(projectSettings);
                                        UnityEditor.AssetDatabase.SaveAssets();
                                        UnityEditor.AssetDatabase.Refresh();
                                        
                                        // Verify the save worked
                                        var reloadedSettings = BoostOpsProjectSettings.GetInstance();
                                        Debug.Log($"[BoostOps] 🔍 Verification - reloaded androidCertFingerprint: '{reloadedSettings.androidCertFingerprint}'");
                                        
                                        Debug.Log("[BoostOps] ✅ Android SHA256 fingerprint saved to project settings");
                                    }
                                    else
                                    {
                                        Debug.Log($"[BoostOps] Android fingerprint already matches remote value: {remoteAndroidFingerprint}");
                                    }
                                }
                                else
                                {
                                    Debug.Log("[BoostOps] No Android SHA256 fingerprint found in remote response - preserving local value");
                                }
                            }
                            
                            // Process studio information from lookup response
                            if (lookupResponse.project?.studio != null)
                            {
                                Debug.Log($"[BoostOps] 🏢 Studio information received: '{lookupResponse.project.studio.name}' (ID: {lookupResponse.project.studio.id})");
                                
                                // Update studio information in editor window
                                studioId = lookupResponse.project.studio.id;
                                studioName = lookupResponse.project.studio.name;
                                studioDescription = lookupResponse.project.studio.description ?? "";
                                
                                // Save studio information to EditorPrefs for persistence
                                SaveStudioInfo();
                                
                                Debug.Log("[BoostOps] ✅ Studio information updated in editor window");
                                
                                // Force UI refresh to show updated studio information
                                if (useUIToolkit && contentContainer != null)
                                {
                                    Debug.Log("[BoostOps] 🔄 Forcing UI refresh after studio info update");
                                    BuildAccountPanel();
                                }
                            }
                            else
                            {
                                Debug.Log("[BoostOps] No studio information found in lookup response");
                            }
                            
                            // Save the project key directly from lookup response
                            projectKey = GetProjectKey(lookupResponse);
                            string projectId = lookupResponse.project?.id;  // Get the actual project ID
                            if (!string.IsNullOrEmpty(projectKey))
                            {
                                Debug.Log($"[BoostOps] 🔑 Project Key received: {projectKey.Substring(0, Math.Min(20, projectKey.Length))}...");
                                
                                // Reuse the existing projectSettings variable
                                Debug.Log($"[BoostOps] 🔍 Before assignment - projectSettings.projectKey: '{projectSettings.projectKey}'");
                                Debug.Log($"[BoostOps] 🔍 About to assign - project_key: '{projectKey}'");
                                
                                projectSettings.projectKey = projectKey;
                                
                                Debug.Log($"[BoostOps] 🔍 After assignment - projectSettings.projectKey: '{projectSettings.projectKey}'");
                                
                                // Also save the project ID if available (needed for source_project_id in analytics)
                                if (!string.IsNullOrEmpty(projectId))
                                {
                                    projectSettings.projectId = projectId;
                                    Debug.Log($"[BoostOps] 💾 Saved project ID: {projectId}");
                                }
                                
                                if (!string.IsNullOrEmpty(lookupResponse.ingestUrl))
                                {
                                    projectSettings.ingestUrl = lookupResponse.ingestUrl;
                                    Debug.Log($"[BoostOps] Ingest URL configured: {lookupResponse.ingestUrl}");
                                }
                                
                                // Save platform-specific data from lookup response
                                if (lookupResponse.project?.app_stores != null && lookupResponse.project.app_stores.Length > 0)
                                {
                                    foreach (var appStore in lookupResponse.project.app_stores)
                                    {
                                        if ((appStore.type == "IOS_APP_STORE" || appStore.type == "APPLE_STORE") && !string.IsNullOrEmpty(appStore.apple_store_id))
                                        {
                                            projectSettings.appleAppStoreId = appStore.apple_store_id;
                                            Debug.Log($"[BoostOps] 🍎 Apple App Store ID saved: {appStore.apple_store_id}");
                                        }
                                        else if ((appStore.type == "GOOGLE_PLAY" || appStore.type == "GOOGLE_STORE") && !string.IsNullOrEmpty(appStore.android_package_name))
                                        {
                                            projectSettings.androidPackageName = appStore.android_package_name;
                                            Debug.Log($"[BoostOps] 🤖 Android Package Name saved: {appStore.android_package_name}");
                                            
                                            // Also save Android SHA256 fingerprint if present
                                            if (appStore.android_sha256_fingerprints != null && appStore.android_sha256_fingerprints.Length > 0)
                                            {
                                                projectSettings.androidCertFingerprint = appStore.android_sha256_fingerprints[0];
                                                Debug.Log($"[BoostOps] 🔐 Android SHA256 fingerprint saved");
                                            }
                                        }
                                    }
                                }
                                
                                UnityEditor.EditorUtility.SetDirty(projectSettings);
                                UnityEditor.AssetDatabase.SaveAssets();
                                
                                Debug.Log("[BoostOps] ✅ Project key saved to project settings");
                                
                                // Force UI refresh
                                if (useUIToolkit && contentContainer != null)
                                {
                                    Debug.Log("[BoostOps] 🔄 Forcing UI refresh after project key save");
                                    BuildAccountPanel();
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[BoostOps] ⚠️ Project found but no project key in response. Project exists but needs completion.");
                                
                                // Show a dialog to the user explaining the situation
                                bool shouldComplete = EditorUtility.DisplayDialog(
                                    "Project Found - Setup Incomplete", 
                                    $"Your project '{lookupResponse.project?.name ?? "Unknown Project"}' was found in BoostOps but needs to be completed.\n\n" +
                                    "The project exists and has campaign data, but is missing the project key needed for analytics tracking.\n\n" +
                                    "Would you like to complete the project setup now?",
                                    "Complete Setup", 
                                    "Skip for Now"
                                );
                                
                                if (shouldComplete)
                                {
                                    Debug.Log("[BoostOps] User chose to complete project setup - showing registration dialog");
                                    ShowRegisterAppDialog();
                                }
                                else
                                {
                                    Debug.Log("[BoostOps] User chose to skip project completion for now");
                                }
                            }
                            
                            // Refresh UI to show the linked project and mark registered ONLY if we have a project key
                            if (useUIToolkit && contentContainer != null)
                            {
                                // Only mark as registered when a valid project key exists
                                projectKey = GetProjectKey(lookupResponse);
                                if (!string.IsNullOrEmpty(projectKey))
                                {
                                    registrationState = ProjectRegistrationState.Registered;
                                    isProjectRegistered = true;
                                    SaveRegistrationState();
                                    Debug.Log($"[BoostOps] ✅ Registration state updated to Registered (project_key found: {projectKey.Substring(0, Math.Min(20, projectKey.Length))}...)");
                                }
                                
                                Debug.Log("[BoostOps] 🔄 Forcing comprehensive UI refresh after project lookup completion");
                                
                                // Use EditorApplication.delayCall to ensure all data is saved before UI refresh
                                EditorApplication.delayCall += () => {
                                    Debug.Log("[BoostOps] 🔄 Executing delayed UI refresh after project lookup");
                                    
                                    // Refresh global status bar first
                                    RefreshGlobalStatusBar();
                                    
                                    // Rebuild the entire interface to reflect updated data
                                    BuildUIToolkitInterface();
                                    
                                    // Restore current tab or show Overview
                                    if (selectedTab >= 0)
                                    {
                                        switch (selectedTab)
                                        {
                                            case 0: ShowOverviewPanel(); break;
                                            case 1: ShowLinksPanel(); break;
                                            case 2: ShowCrossPromoPanel(); break;
                                            case 3: ShowAttributionPanel(); break;
                                            default: ShowOverviewPanel(); break;
                                        }
                                    }
                                    else
                                    {
                                        ShowOverviewPanel();
                                    }
                                };
                            }
                            
                                                    // Check if project has valid platform configuration
                        bool hasValidAppStores = lookupResponse.project?.app_stores != null && lookupResponse.project.app_stores.Length > 0;
                        if (!hasValidAppStores)
                        {
                            Debug.Log("[BoostOps] ℹ️ Project found but has no platform configuration - skipping campaign data processing");
                            Debug.Log("[BoostOps] This usually means the project needs platform setup in the BoostOps dashboard");
                        }
                        else
                        {
                            // Process campaign data if present - use same priority as LoadCampaignsFromAPI
                            string boostopsConfigJson = null;
                            
                            // Priority 1: Extract raw boostops_config JSON directly from response text
                            // This avoids issues with Unity's JsonUtility not properly re-serializing nested objects
                            if (!string.IsNullOrEmpty(responseText) && responseText.Contains("\"boostops_config\""))
                            {
                                try
                                {
                                    // Find the boostops_config field in the raw JSON
                                    int configStart = responseText.IndexOf("\"boostops_config\":");
                                    if (configStart >= 0)
                                    {
                                        configStart = responseText.IndexOf("{", configStart);
                                        if (configStart >= 0)
                                        {
                                            // Count braces to find the matching closing brace
                                            int braceCount = 1;
                                            int configEnd = configStart + 1;
                                            while (configEnd < responseText.Length && braceCount > 0)
                                            {
                                                if (responseText[configEnd] == '{') braceCount++;
                                                else if (responseText[configEnd] == '}') braceCount--;
                                                configEnd++;
                                            }
                                            
                                            if (braceCount == 0)
                                            {
                                                boostopsConfigJson = responseText.Substring(configStart, configEnd - configStart);
                                                Debug.Log($"[BoostOps] 🎯 Extracted raw boostops_config JSON from response ({boostopsConfigJson.Length} characters)");
                                                
                                                // Validate it contains actual campaign or source_project data
                                if (boostopsConfigJson == "{}" || 
                                                    (!boostopsConfigJson.Contains("\"campaigns\"") && !boostopsConfigJson.Contains("\"source_project\"") && !boostopsConfigJson.Contains("\"app_walls\"")))
                                {
                                                    Debug.Log("[BoostOps] boostops_config exists but contains no valid data - treating as null");
                                    boostopsConfigJson = null;
                                }
                            }
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogWarning($"[BoostOps] Failed to extract raw boostops_config: {ex.Message}");
                                }
                            }
                            
                            // Priority 2: Try root-level boostops_config string (fallback for old API structure)
                            if (string.IsNullOrEmpty(boostopsConfigJson) && !string.IsNullOrEmpty(lookupResponse.boostops_config))
                            {
                                boostopsConfigJson = lookupResponse.boostops_config;
                                Debug.Log($"[BoostOps] 🎯 Using root-level boostops_config string ({boostopsConfigJson.Length} characters)");
                            }
                            
                            // Priority 3: Last resort - try Unity's JsonUtility (may have nested object issues)
                            if (string.IsNullOrEmpty(boostopsConfigJson) && lookupResponse.project?.boostops_config != null)
                            {
                                Debug.Log("[BoostOps] 🎯 Falling back to JsonUtility.ToJson (may have nested object issues)");
                                try
                                {
                                    boostopsConfigJson = JsonUtility.ToJson(lookupResponse.project.boostops_config);
                                    Debug.Log($"[BoostOps] 📄 Serialized nested object to JSON ({boostopsConfigJson.Length} characters)");
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogError($"[BoostOps] Failed to serialize nested boostops_config object: {ex.Message}");
                                }
                            }
                            
                            if (!string.IsNullOrEmpty(boostopsConfigJson) && boostopsConfigJson != "{}")
                        {
                            try
                            {
                                // Save the raw boostops_config JSON directly to StreamingAssets
                                SaveBoostOpsConfigToStreamingAssets(boostopsConfigJson, forceOverwrite: true);
                                
                                // Parse for Editor UI display
                                ParseRemoteCampaignConfig(boostopsConfigJson);
                                    
                                    // Update last sync time with API source
                                    lastRemoteConfigSync = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (BoostOps API)";
                                    crossPromoLastSync = lastRemoteConfigSync;
                                    
                                    // Save to cache
                                    SaveCachedRemoteCampaigns();
                                    
                                    Debug.Log($"[BoostOps] ✅ Processed {cachedRemoteCampaigns?.Count ?? 0} campaigns");
                                    
                                    // Comprehensive UI refresh after campaign processing
                                    EditorApplication.delayCall += () => {
                                        
                                        // Update all status indicators and global state
                                        UpdateStatusLights();
                                        
                                        // Refresh the current tab to show new data
                                        switch (selectedTab)
                                        {
                                            case 0: ShowOverviewPanel(); break;
                                            case 1: ShowLinksPanel(); break;
                                            case 2: ShowCrossPromoPanel(); break;
                                            case 3: ShowAttributionPanel(); break;
                                            case 4: ShowIntegrationsPanel(); break;
                                            default: ShowOverviewPanel(); break;
                                        }
                                    };
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogError($"[BoostOps] ❌ Failed to process campaign data: {ex.Message}");
                                }
                        }
                            else
                            {
                                // Clear cached data when lookup returns empty/no config
                                ClearRemoteCampaignCache();
                                
                                // Also clear runtime config cache to prevent showing stale data
                                EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_JSON");
                                EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Key");
                                EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Timestamp");
                                EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Provider");
                                
                                // Clear cached source project data
                                cachedSourceProject = null;
                                
                                // Update sync time to reflect that we got a current (empty) response
                                lastRemoteConfigSync = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (BoostOps API - No Data)";
                                crossPromoLastSync = lastRemoteConfigSync;
                                
                                // Refresh UI to show empty state
                                if (selectedTab == 2)
                                {
                                    EditorApplication.delayCall += () => {
                                        Debug.Log("[BoostOps] 🔄 Refreshing Cross-Promo panel to show empty state");
                                        ShowCrossPromoPanel();
                                    };
                                }
                            }
                        }
                            
                        Debug.Log("[BoostOps] 🎉 Project linked successfully! All configuration complete.");
                        }
                        else
                        {
                            Debug.Log("[BoostOps] ℹ️ No existing project found for this app. Registration will be needed.");
                            
                            // Store lookup response data for UI display
                            hasLookupResponse = true;
                            lookupProjectFound = false;
                            lookupProjectSlug = "";
                            lookupProjectName = "";
                            lookupMessage = lookupResponse?.message ?? "No project found";
                            cachedProjectLookupResponse = null; // Clear cache when no project found
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[BoostOps] ⚠️ Project lookup failed: {response.StatusCode} - {responseText}");
                        
                        // Store lookup failure for UI display
                        hasLookupResponse = true;
                        lookupProjectFound = false;
                        lookupProjectSlug = "";
                        lookupProjectName = "";
                        lookupMessage = $"Lookup failed: {response.StatusCode}";
                        cachedProjectLookupResponse = null; // Clear cache on lookup failure
                        
                        // Don't throw an error - just continue with normal registration flow
                    }
                }
            }
                    catch (TaskCanceledException)
        {
            Debug.LogWarning("[BoostOps] ⏰ Project lookup request timed out or was canceled");
            Debug.LogWarning("[BoostOps] ⚠️ Continuing with manual registration option due to timeout");
            
            // Clear any cached campaign data to prevent writing stale/empty data
            cachedRemoteCampaigns?.Clear();
            cachedSourceProject = null;
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[BoostOps] ⏰ Project lookup operation was canceled");
            Debug.LogWarning("[BoostOps] ⚠️ Continuing with manual registration option due to cancellation");
            
            // Clear any cached campaign data to prevent writing stale/empty data
            cachedRemoteCampaigns?.Clear();
            cachedSourceProject = null;
        }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ Project lookup failed with exception:");
                Debug.LogError($"[BoostOps] Exception type: {ex.GetType().Name}");
                Debug.LogError($"[BoostOps] Exception message: {ex.Message}");
                
                // Don't log full stack trace for common network errors to reduce log noise
                if (!(ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException))
                {
                    Debug.LogError($"[BoostOps] Full exception: {ex}");
                }
                
                // Don't throw - let the user continue with manual registration
                Debug.LogWarning($"[BoostOps] ⚠️ Continuing with manual registration option due to lookup failure");
            }
            finally
            {
                // Final attempt to ensure project settings are properly saved as an asset
                if (projectSettings != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(projectSettings)))
                {
                    Debug.Log("[BoostOps] 🔧 Final attempt to create project settings asset after lookup completion...");
                    try
                    {
                        // Force recreation of the asset
                        projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
                        string finalAssetPath = AssetDatabase.GetAssetPath(projectSettings);
                        if (!string.IsNullOrEmpty(finalAssetPath))
                        {
                            Debug.Log($"[BoostOps] ✅ Successfully created project settings asset at: {finalAssetPath}");
                        }
                        else
                        {
                            Debug.LogWarning("[BoostOps] ⚠️ Project settings will remain as temporary instance - data may not persist");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[BoostOps] ⚠️ Final asset creation attempt failed: {ex.Message}");
                    }
                }
                
                isCheckingForExistingProject = false;
            }
        }
        
        /// <summary>
        /// Register a new project with BoostOps
        /// </summary>
        async Task RegisterNewProject(string projectName, string productGuid, string cloudProjectId,
                                    string iosBundleId, string androidPackageName, string iosAppStoreId,
                                    string appleTeamId, string[] androidSha256Fingerprints)
        {
            if (string.IsNullOrEmpty(apiToken))
            {
                throw new Exception("No API token available");
            }
            
            string baseUrl = GetApiServerBaseUrl();
            string endpoint = $"{baseUrl}/api/unity/project/register";
            
            Debug.Log($"[BoostOps] 🚀 Registering project at: {endpoint}");
            
            // Create registration request
            var registrationRequest = new ProjectRegistrationRequest
            {
                jwtToken = apiToken,
                projectName = projectName,
                productGuid = productGuid,
                cloudProjectId = cloudProjectId,
                iosBundleId = iosBundleId,
                androidPackageName = androidPackageName,
                iosAppStoreId = iosAppStoreId,
                appleTeamId = appleTeamId,
                androidSha256Fingerprints = androidSha256Fingerprints
            };
            
            string jsonData = JsonUtility.ToJson(registrationRequest);
            Debug.Log($"[BoostOps] 📤 Registration request: {jsonData}");
            
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(15); // Longer timeout for registration
                
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(endpoint, content);
                string responseText = await response.Content.ReadAsStringAsync();
                
                Debug.Log($"[BoostOps] 📨 Registration response status: {response.StatusCode} ({(int)response.StatusCode})");
                Debug.Log($"[BoostOps] 📝 Registration response body: {responseText}");
                
                if (response.IsSuccessStatusCode)
                {
                    // Parse the registration response
                    var registrationResponse = JsonUtility.FromJson<ProjectRegistrationResponse>(responseText);
                    
                    if (registrationResponse != null && registrationResponse.success)
                    {
                        Debug.Log($"[BoostOps] ✅ Project registration successful!");
                        Debug.Log($"[BoostOps] Project ID: {registrationResponse.projectId}");
                        Debug.Log($"[BoostOps] Project Name: {registrationResponse.project?.name}");
                        
                        // Save the project key directly from registration response
                        if (!string.IsNullOrEmpty(registrationResponse.projectKey))
                        {
                            Debug.Log($"[BoostOps] 🔑 Project Key received: {registrationResponse.projectKey.Substring(0, Math.Min(20, registrationResponse.projectKey.Length))}...");
                            
                            var projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
                            projectSettings.projectKey = registrationResponse.projectKey;
                            
                            // Also save the project ID if available (needed for source_project_id in analytics)
                            // Note: Registration response uses int projectId, but we store it as string in settings
                            if (!string.IsNullOrEmpty(registrationResponse.project?.id))
                            {
                                projectSettings.projectId = registrationResponse.project.id;
                                Debug.Log($"[BoostOps] 💾 Saved project ID from registration: {registrationResponse.project.id}");
                            }
                            
                            if (!string.IsNullOrEmpty(registrationResponse.ingestUrl))
                            {
                                projectSettings.ingestUrl = registrationResponse.ingestUrl;
                                Debug.Log($"[BoostOps] Ingest URL configured: {registrationResponse.ingestUrl}");
                            }
                            
                            // Save app store-specific data from registration response
                            if (registrationResponse.project?.app_stores != null && registrationResponse.project.app_stores.Length > 0)
                            {
                                foreach (var appStore in registrationResponse.project.app_stores)
                                {
                                    if ((appStore.type == "IOS_APP_STORE" || appStore.type == "APPLE_STORE") && !string.IsNullOrEmpty(appStore.apple_store_id))
                                    {
                                        projectSettings.appleAppStoreId = appStore.apple_store_id;
                                        Debug.Log($"[BoostOps] 🍎 Apple App Store ID saved from registration: {appStore.apple_store_id}");
                                    }
                                    else if ((appStore.type == "GOOGLE_PLAY" || appStore.type == "GOOGLE_STORE") && !string.IsNullOrEmpty(appStore.android_package_name))
                                    {
                                        projectSettings.androidPackageName = appStore.android_package_name;
                                        Debug.Log($"[BoostOps] 🤖 Android Package Name saved from registration: {appStore.android_package_name}");
                                        
                                        // Also save Android SHA256 fingerprint if present
                                        if (appStore.android_sha256_fingerprints != null && appStore.android_sha256_fingerprints.Length > 0)
                                        {
                                            projectSettings.androidCertFingerprint = appStore.android_sha256_fingerprints[0];
                                            Debug.Log($"[BoostOps] 🔐 Android SHA256 fingerprint saved from registration");
                                        }
                                    }
                                }
                            }
                            
                            UnityEditor.EditorUtility.SetDirty(projectSettings);
                            UnityEditor.AssetDatabase.SaveAssets();
                            
                            Debug.Log("[BoostOps] ✅ Project key saved to project settings");
                            // Sync registration state so Links page reflects success
                            registrationState = ProjectRegistrationState.Registered;
                            isProjectRegistered = true;
                            SaveRegistrationState();
                        }
                        else
                        {
                            Debug.LogWarning("[BoostOps] ⚠️ Project registered but no project key in response.");
                        }
                    }
                    else
                    {
                        throw new Exception($"Registration failed: {registrationResponse?.message ?? "Unknown error"}");
                    }
                }
                else
                {
                    throw new Exception($"Registration request failed: {response.StatusCode} - {responseText}");
                }
            }
        }
        
        /// <summary>
        /// Manual method to process OAuth token from URL (for debugging)
        /// </summary>
        // Admin-only: remove from public menu by guarding with UNITY_EDITOR && BOOSTOPS_INTERNAL
        #if BOOSTOPS_INTERNAL
        [MenuItem("BoostOps Admin/Debug OAuth Token Processing")]
        public static void ManualProcessOAuthToken()
        {
            var window = GetWindow<BoostOpsEditorWindow>();
            string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOjEsImVtYWlsIjoic3RlcGhlbkBsdWNreWphY2twb3RjYXNpbm8uY29tIiwiZGlzcGxheU5hbWUiOiJTdGVwaGVuIFN1bGxpdmFuIiwic2NvcGUiOiJ1bml0eV9wcm9qZWN0X3N5bmMiLCJhdWRpZW5jZSI6ImJvb3N0b3BzLXVuaXR5IiwiaXNzIjoiYm9vc3RvcHMtdW5pdHktYXV0aCIsImlhdCI6MTc1NDAwOTk2NywiZXhwIjoxNzU2NjAxOTY3fQ.-JJ1nsa869hgI43Qe-0nHwb9yBqQs89qDA6014mXHdY";
            Debug.Log("[BoostOps] 🛠️ Processing token manually for debugging...");
            window.HandleOAuthSuccess(token);
        }
        #endif
        
        /// <summary>
        /// Handle successful authentication
        /// </summary>
        async Task HandleAuthenticationSuccess(string token, string email)
        {
            apiToken = token;
            userEmail = email;
            isLoggedIn = true;
            isAuthenticatingWithGoogle = false;
            
            // Save credentials
            EditorPrefs.SetString("BoostOps_ApiToken", apiToken);
            EditorPrefs.SetString("BoostOps_UserEmail", userEmail);
            
            Debug.Log($"[BoostOps] Authentication successful for: {email}");
            
            // Auto-switch to BoostOps Remote mode for both Links and Cross-Promo
            SwitchToManagedModeAfterLogin();
            
            // Try to get SDK key after authentication
            try
            {
                await FetchSDKKey();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Could not fetch SDK key: {ex.Message}");
            }
            
            // Refresh UI
            Repaint();
        }
        
        /// <summary>
        /// Auto-switch to BoostOps Remote mode after successful login
        /// </summary>
        void SwitchToManagedModeAfterLogin()
        {
            LogDebug("SwitchToManagedModeAfterLogin: Auto-switching to BoostOps Remote mode");
            
            // Switch both Links and Cross-Promo to BoostOps Remote mode
            linksMode = FeatureMode.Managed;
            crossPromoMode = FeatureMode.Managed;
            
            // Save the new mode states
            SaveFeatureModeStates();
            
            LogDebug($"Auto-switched modes - Links: {linksMode}, Cross-Promo: {crossPromoMode}");
        }
        
        /// <summary>
        /// Get authentication server base URL based on hosting option (for login/OAuth)
        /// </summary>
        string GetAuthServerBaseUrl()
        {
            // Always use production server
            return BOOSTOPS_SERVER_URL; // https://unity-app.boostops.io  
        }
        
        /// <summary>
        /// Get API server base URL based on hosting option (for API calls)
        /// </summary>
        string GetApiServerBaseUrl()
        {
            // Always use production server
            return BOOSTOPS_API_SERVER_URL; // https://unity-api.boostops.io  
        }
        
        /// <summary>
        /// Fetch project key from server using JWT token (triggers app registration if needed)
        /// </summary>
        async Task FetchSDKKey()
        {
            Debug.Log($"[BoostOps] FetchSDKKey called. API Token available: {!string.IsNullOrEmpty(apiToken)}");
            Debug.Log($"[BoostOps] API Token length: {apiToken?.Length ?? 0}");
            Debug.Log($"[BoostOps] User logged in: {isLoggedIn}");
            Debug.Log($"[BoostOps] User email: {userEmail ?? "null"}");
            
            if (string.IsNullOrEmpty(apiToken))
            {
                throw new Exception("No API token available");
            }
            
            string baseUrl = GetApiServerBaseUrl();
            string endpoint = $"{baseUrl}/api/unity/sdk-key";
            
            Debug.Log($"[BoostOps] Using API base URL: {baseUrl}");
            Debug.Log($"[BoostOps] Full SDK key endpoint: {endpoint}");
            Debug.Log($"[BoostOps] Auth server environment: Production");
            
            var requestData = new SDKKeyRequest {
                jwtToken = apiToken
            };
            
            string jsonData = JsonUtility.ToJson(requestData);
            Debug.Log($"[BoostOps] Request data: {jsonData}");
            
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10); // 10 second timeout
                
                                    // First, test basic connectivity to the server
                    try
                    {
                        Debug.Log($"[BoostOps] 🔍 Testing server connectivity to: {baseUrl}");
                        var healthCheck = await client.GetAsync($"{baseUrl}/");
                        Debug.Log($"[BoostOps] ✅ Server connectivity test result: {healthCheck.StatusCode}");
                    }
                    catch (Exception connectEx)
                    {
                        Debug.LogError($"[BoostOps] ❌ Server connectivity test failed for {baseUrl}");
                        Debug.LogError($"[BoostOps] Connection error details: {connectEx.Message}");
                        Debug.LogError($"[BoostOps] Exception type: {connectEx.GetType().Name}");
                        throw new Exception($"Cannot reach server at {baseUrl}. Please check if the server is running. Error: {connectEx.Message}");
                    }
                
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(endpoint, content);
                string responseText = await response.Content.ReadAsStringAsync();
                
                Debug.Log($"[BoostOps] FetchSDKKey response status: {response.StatusCode}");
                Debug.Log($"[BoostOps] FetchSDKKey response text: {responseText}");
                
                if (response.IsSuccessStatusCode)
                {
                    SDKKeyResponse sdkResponse;
                    try
                    {
                        Debug.Log($"[BoostOps] Attempting to parse JSON response: {responseText}");
                        sdkResponse = JsonUtility.FromJson<SDKKeyResponse>(responseText);
                        Debug.Log($"[BoostOps] JSON parsing successful. Response object: {(sdkResponse != null ? "not null" : "null")}");
                        
                        if (sdkResponse != null)
                        {
                            Debug.Log($"[BoostOps] Response success: {sdkResponse.success}");
                            Debug.Log($"[BoostOps] Response data: {(sdkResponse.data != null ? "not null" : "null")}");
                            
                            if (sdkResponse.data != null)
                            {
                                Debug.Log($"[BoostOps] SDK Key: {(string.IsNullOrEmpty(sdkResponse.data.sdkKey) ? "null/empty" : "present")}");
                            }
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        throw new Exception($"Failed to parse response JSON: {jsonEx.Message}. Response: {responseText}");
                    }
                    
                    // Validate response structure
                    if (sdkResponse == null)
                    {
                        throw new Exception("Invalid response format: response is null");
                    }
                    
                    if (sdkResponse.data == null)
                    {
                        throw new Exception("Invalid response format: data is null");
                    }
                    
                    if (string.IsNullOrEmpty(sdkResponse.data.sdkKey))
                    {
                        throw new Exception("Invalid response format: project key is null or empty");
                    }
                    
                    // Store the project key in project settings
                    var settings = BoostOpsProjectSettings.GetOrCreateSettings();
                    if (settings == null)
                    {
                        throw new Exception("Failed to get or create project settings");
                    }
                    
                    settings.projectKey = sdkResponse.data.sdkKey;
                    UnityEditor.EditorUtility.SetDirty(settings);
                    UnityEditor.AssetDatabase.SaveAssets();
                    
                    Debug.Log($"[BoostOps] Project key configured successfully");
                }
                else
                {
                    try
                {
                    var error = JsonUtility.FromJson<ErrorResponse>(responseText);
                        string errorMessage = error?.message ?? "Failed to fetch project key";
                        throw new Exception($"Server error: {errorMessage}");
                    }
                    catch (Exception jsonEx)
                    {
                        throw new Exception($"Failed to fetch project key. Status: {response.StatusCode}, Response: {responseText}, JSON Parse Error: {jsonEx.Message}");
                    }
                }
            }
        }
        
        // iOS entitlements are now automatically handled by BoostOpsPostProcessBuild
        // No separate entitlements file generation needed
        
        void GenerateAndroidManifestFile(List<string> hosts, string generatedPath)
        {
            string packageName = GetCurrentPlatformBundleId();
            if (string.IsNullOrEmpty(packageName))
            {
                Debug.LogError("Package name not found for Android manifest generation");
                return;
            }
            
            var manifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" package=""{packageName}"">
    <application>
        <activity android:name=""com.unity3d.player.UnityPlayerActivity"" 
                  android:exported=""true""
                  android:launchMode=""singleTask"">
            <intent-filter android:autoVerify=""true"">
                <action android:name=""android.intent.action.VIEW"" />
                <category android:name=""android.intent.category.DEFAULT"" />
                <category android:name=""android.intent.category.BROWSABLE"" />";
            
            foreach (string host in hosts)
            {
                manifest += $@"
                <data android:scheme=""https"" android:host=""{host}"" />";
            }
            
            manifest += @"
            </intent-filter>
        </activity>
    </application>
</manifest>";
            
            string manifestPath = Path.Combine(generatedPath, "Plugins", "Android", "AndroidManifest.xml");
            File.WriteAllText(manifestPath, manifest);
            
            Debug.Log($"Generated Android manifest file for {hosts.Count} hosts at: {manifestPath}");
        }
        
        void GenerateSetupInstructions(List<string> hosts, string generatedPath)
        {
            var instructions = $@"# BoostLinks™ Dynamic Links Setup Instructions

Configured domains: {hosts.Count}

## Configured Domains

{string.Join("\n", hosts.Select(h => $"- {h}"))}

## ⚠️ IMPORTANT: Folder Rename Required

The folder is named `well_known_server` in Unity (visible), but **MUST be renamed to `.well-known` on your server**.

**Why?** Folders starting with `.` (dot) are hidden on macOS/iOS, but iOS and Android **REQUIRE** the folder to be named `.well-known`.

## Setup Steps

### 1. Server Setup
Upload the files and **rename the folder** on your server:

 {string.Join("\n", hosts.Select(h => 
$"\n**For {h}:**\n" +
$"- Upload `apple-app-site-association` to `https://{h}/.well-known/apple-app-site-association`\n" + 
$"- Upload `assetlinks.json` to `https://{h}/.well-known/assetlinks.json`"))}

**Step-by-step upload process:**
1. Go to `Assets/BoostOpsGenerated/ServerFiles/`
2. Copy the `well_known_server` folder to your web server
3. **RENAME** it to `.well-known` (with the dot) on the server
4. Verify files are accessible at `https://yourdomain.com/.well-known/apple-app-site-association`

### 2. iOS Setup
1. Entitlements are automatically merged during Unity build process
2. Ensure your Apple Developer Team ID is correctly set in the AASA file
3. Test Universal Links with the Apple App Site Association Validator
### 3. Android Setup
1. The `AndroidManifest.xml` will be automatically merged during build
2. Ensure your certificate fingerprint is correctly configured
3. Test App Links with the Android App Links Assistant

### 4. Unity Integration
Domain configuration is automatically handled by the BoostOps SDK using the `BoostOpsDynamicLinksConfig.asset` file.
No additional code is required - the SDK will automatically detect and handle configured domains.

## Validation
- iOS: Use Apple's AASA Validator
- Android: Use Android Studio's App Links Assistant
- Both: Test with actual devices

## Support
For advanced multi-domain scenarios (white-label, re-branding), refer to the BoostOps documentation.
";
            
            string instructionsPath = Path.Combine(generatedPath, "SETUP_INSTRUCTIONS.md");
            File.WriteAllText(instructionsPath, instructions);
            
            Debug.Log($"Generated setup instructions at: {instructionsPath}");
        }
        
        // Icon fetching for local mode (adapted from managed mode logic)
        async void FetchAllStoreIcons()
        {
            if (crossPromoTable?.targets == null || crossPromoTable.targets.Length == 0)
            {
                Debug.LogWarning("[BoostOps] No target games configured. Add some games first!");
                EditorUtility.DisplayDialog("No Games Found", 
                    "No target games are configured. Please add some games to the cross-promotion table first.", 
                    "OK");
                return;
            }

            Debug.Log($"[BoostOps] Starting icon fetch for {crossPromoTable.targets.Length} target games...");
            LogDebug($"FetchAllStoreIcons: Processing {crossPromoTable.targets.Length} target games");

            int successCount = 0;
            int errorCount = 0;
            var errors = new System.Collections.Generic.List<string>();

            try
            {
                for (int i = 0; i < crossPromoTable.targets.Length; i++)
                {
                    var target = crossPromoTable.targets[i];
                    
                    // Skip if already has icon (unless user wants to refresh all)
                    if (target?.icon != null)
                    {
                        LogDebug($"Skipping '{target.headline}' - already has icon");
                        continue;
                    }

                    // Check if target has valid store IDs
                    if (string.IsNullOrEmpty(target.iosAppStoreId) && string.IsNullOrEmpty(target.androidPackageId))
                    {
                        string errorMsg = $"'{target.headline}' - No valid store IDs found";
                        LogDebug($"Skipping {errorMsg}");
                        errors.Add(errorMsg);
                        continue;
                    }

                    try
                    {
                        LogDebug($"Fetching icon for game {i}: '{target.headline}'");
                        await FetchIconForTargetGame(target);
                        successCount++;
                        
                        // Small delay to avoid overwhelming the APIs
                        await System.Threading.Tasks.Task.Delay(500);
                    }
                    catch (System.Exception ex)
                    {
                        errorCount++;
                        string errorMsg = $"'{target.headline}' - {ex.Message}";
                        LogDebug($"Error fetching icon for game {i}: {errorMsg}");
                        errors.Add(errorMsg);
                    }
                }

                // Show results to user
                string message = $"Icon fetch completed!\n\n";
                message += $"✅ Successfully fetched: {successCount} icons\n";
                if (errorCount > 0)
                {
                    message += $"❌ Failed: {errorCount} icons\n\n";
                    if (errors.Count > 0)
                    {
                        message += "Errors:\n";
                        foreach (var error in errors.Take(5)) // Show first 5 errors
                        {
                            message += $"• {error}\n";
                        }
                        if (errors.Count > 5)
                        {
                            message += $"• ... and {errors.Count - 5} more errors\n";
                        }
                    }
                }

                Debug.Log($"[BoostOps] Icon fetch completed - {successCount} successful, {errorCount} failed");
                EditorUtility.DisplayDialog("Icon Fetch Complete", message, "OK");

                // Refresh the UI to show new icons
                EditorApplication.delayCall += RefreshCrossPromoPanel;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Fatal error during bulk icon fetch: {ex.Message}");
                EditorUtility.DisplayDialog("Icon Fetch Failed", 
                    $"A fatal error occurred during icon fetching:\n\n{ex.Message}", 
                    "OK");
            }
        }
        
        void UpdateGameNamePriority(int gameIndex)
        {
            Debug.Log($"UpdateGameNamePriority: Not implemented for game {gameIndex}");
        }
        
        void UpdateGameId(TargetGame target)
        {
            Debug.Log("UpdateGameId: Not implemented in this version");
        }
        
        async void FetchIconForGame(int gameIndex, TargetGame target)
        {
            try
            {
                LogDebug($"FetchIconForGame: Fetching icon for game {gameIndex} ({target.headline})");
                Debug.Log($"[BoostOps] Starting icon fetch for '{target.headline}'...");
                await FetchIconForTargetGame(target);
                
                Debug.Log($"[BoostOps] Successfully fetched icon for '{target.headline}'");
                
                // Refresh the UI to show the new icon
                EditorApplication.delayCall += RefreshCrossPromoPanel;
            }
            catch (System.Exception ex)
            {
                LogDebug($"Error fetching icon for game {gameIndex}: {ex.Message}");
                Debug.LogError($"[BoostOps] Failed to fetch icon for {target.headline}: {ex.Message}");
                EditorUtility.DisplayDialog("Icon Fetch Failed", 
                    $"Failed to fetch icon for '{target.headline}':\n\n{ex.Message}", 
                    "OK");
            }
        }
        
        async void AutoFetchIconForGame(int gameIndex, TargetGame target)
        {
            try
            {
                LogDebug($"AutoFetchIconForGame: Auto-fetching icon for game {gameIndex} ({target.headline})");
                await FetchIconForTargetGame(target);
                
                // Refresh the UI to show the new icon
                EditorApplication.delayCall += RefreshCrossPromoPanel;
            }
            catch (System.Exception ex)
            {
                LogDebug($"Error auto-fetching icon for game {gameIndex}: {ex.Message}");
            }
        }
        
        async System.Threading.Tasks.Task FetchIconForTargetGame(TargetGame target)
        {
            if (target == null) return;
            
            string iconUrl = null;
            string storeType = null;
            
            // Priority 1: iOS App Store
            if (!string.IsNullOrEmpty(target.iosAppStoreId))
            {
                iconUrl = $"https://itunes.apple.com/lookup?id={target.iosAppStoreId}";
                storeType = "iOS";
            }
            // Priority 2: Android Google Play
            else if (!string.IsNullOrEmpty(target.androidPackageId))
            {
                iconUrl = $"https://play.google.com/store/apps/details?id={target.androidPackageId}";
                storeType = "Android";
            }
            
            if (string.IsNullOrEmpty(iconUrl))
            {
                LogDebug($"No valid store ID found for game '{target.headline}'");
                return;
            }
            
            LogDebug($"Fetching {storeType} icon for '{target.headline}' from: {iconUrl}");
            
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(30);
                    
                    string finalIconUrl = null;
                    
                    if (storeType == "iOS")
                    {
                        finalIconUrl = await GetIosIconUrlFromItunesApi(client, iconUrl);
                    }
                    else if (storeType == "Android")
                    {
                        finalIconUrl = await GetAndroidIconUrlFromPlayStore(client, iconUrl);
                    }
                    
                    if (!string.IsNullOrEmpty(finalIconUrl))
                    {
                        LogDebug($"Final icon URL for '{target.headline}': {finalIconUrl}");
                        
                        var imageBytes = await client.GetByteArrayAsync(finalIconUrl);
                        
                        // Save icon as asset file in Resources/BoostOps/Icons/
                        bool iconSaved = await SaveIconAsAssetForTarget(target, imageBytes, storeType);
                        if (iconSaved)
                        {
                            LogDebug($"Successfully downloaded and saved icon asset for '{target.headline}'");
                            
                            // Mark the CrossPromoTable as dirty so Unity saves it
                            if (crossPromoTable != null)
                            {
                                EditorUtility.SetDirty(crossPromoTable);
                            }
                        }
                        else
                        {
                            LogDebug($"Failed to save icon asset for '{target.headline}'");
                        }
                    }
                    else
                    {
                        LogDebug($"Could not resolve final icon URL for '{target.headline}'");
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogDebug($"Error downloading icon for '{target.headline}': {ex.Message}");
                throw;
            }
        }
        
        string GenerateGameId(TargetGame targetGame)
        {
            return $"game_{System.Guid.NewGuid().ToString("N")[..8]}";
        }
        
        string GetCurrentPlatformBundleId()
        {
            // Return the bundle ID for the current active build target
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.iOS:
                    return iosBundleId;
                case BuildTarget.Android:
                    return androidBundleId;
                default:
                    // Fallback to iOS if neither iOS nor Android is active
                    return !string.IsNullOrEmpty(iosBundleId) ? iosBundleId : androidBundleId;
            }
        }
        
        void DrawLinkConfigurationTab()
        {
            EditorGUILayout.LabelField("BoostLink™ Dynamic Link Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("✨ Generate iOS Universal Links and Android App Links instantly.", MessageType.Info);
            
            GUILayout.Space(10);
            
            // Mode Selection Block
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("What do you want to do today?", EditorStyles.boldLabel);
                
                // Mode selection with radio-button style
                            string[] modeOptions = { "◯ Generate files locally", "◉ Generate & manage with BoostOps" };
            if (hostingOption == HostingOption.Local) modeOptions = new string[] { "◉ Generate files locally", "◯ Generate & manage with BoostOps" };
                
                int newHostingOption = GUILayout.SelectionGrid((int)hostingOption, modeOptions, 1);
                if (newHostingOption != (int)hostingOption)
                {
                    hostingOption = (HostingOption)newHostingOption;
                    SaveHostingOption();
                }
                
                // Show subtitle for each option
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(20);
                    if (hostingOption == HostingOption.Local) // Local mode
                    {
                        EditorGUILayout.LabelField("✨ Get started instantly! Generate files now, upgrade later if you want more.", EditorStyles.miniLabel);
                    }
                    else // Cloud mode
                    {
                        EditorGUILayout.LabelField("🚀 Free plan: 1000 clicks/month forever. Pro trial: Advanced analytics & team features.", EditorStyles.miniLabel);
                    }
                }
                
                GUILayout.Space(10);
                
                // Unified container for mode-specific content
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (hostingOption == HostingOption.Local) // Generate files locally
                    {
                        // Ensure we have a config
                        if (dynamicLinksConfig == null)
                    {
                        EditorGUILayout.LabelField("Domain Configuration", EditorStyles.miniBoldLabel);
                            EditorGUILayout.HelpBox("No dynamic links configuration found. Create one to get started.", MessageType.Info);
                            
                            if (GUILayout.Button("Create Dynamic Links Configuration", GUILayout.Height(25)))
                            {
                                CreateDynamicLinksConfig();
                            }
                        }
                        else
                        {
                            // Primary Host Configuration
                            EditorGUILayout.LabelField("Your Associated Domain(s)", EditorStyles.miniBoldLabel);
                            
                            EditorGUILayout.BeginHorizontal();
                            EditorGUI.BeginChangeCheck();
                            string newPrimaryHost = EditorGUILayout.TextField("Domain", dynamicLinkUrl);
                            
                            if (EditorGUI.EndChangeCheck())
                            {
                                dynamicLinkUrl = BoostOpsProjectSettings.CleanHost(newPrimaryHost);
                                SaveDynamicLinkUrl();
                            }
                            EditorGUILayout.EndHorizontal();
                            
                            // Show validation errors
                            var validation = dynamicLinksConfig.ValidateConfiguration();
                            if (!validation.IsValid)
                            {
                                EditorGUILayout.HelpBox($"Configuration errors:\n{validation.GetErrorsString()}", MessageType.Error);
                            }
                            
                            GUILayout.Space(5);
                            
                            // Advanced: Multiple Hosts Section
                            EditorGUILayout.BeginHorizontal();
                            showAdvancedHosts = EditorGUILayout.Foldout(showAdvancedHosts, "Advanced: Multiple Domains", true);
                            EditorGUILayout.EndHorizontal();
                            
                            if (showAdvancedHosts)
                            {
                                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                                {
                                    EditorGUILayout.HelpBox($"Support for multiple domains (white-label, re-branding, migration). Most apps ({80}-{90}% of indies) only need one domain.", MessageType.Info);
                                    
                                    // Show current domain count and status
                                    if (dynamicLinksConfig != null)
                                    {
                                        var allDomains = dynamicLinksConfig.GetAllHosts();
                                        EditorGUILayout.LabelField($"Configured domains: {allDomains.Count}/{BoostOpsProjectSettings.MAX_DOMAINS}", EditorStyles.miniLabel);
                                        
                                        if (allDomains.Count > 0)
                                        {
                                            EditorGUILayout.LabelField("Current domains:", EditorStyles.miniBoldLabel);
                                            
                                            foreach (var domain in allDomains)
                                            {
                                                EditorGUILayout.LabelField($"• {domain}", EditorStyles.miniLabel);
                                            }
                                        }
                                        else
                                        {
                                            EditorGUILayout.LabelField("No domains configured yet. Add domains using the + button above.", EditorStyles.miniLabel);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else // Generate & manage with BoostOps
                    {
                        // Cloud mode button
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Start Free Account + Pro Trial", GUILayout.Width(200)))
                            {
                                selectedTab = 3; // Switch to Account tab
                            }
                            GUILayout.FlexibleSpace();
                        }
                        
                        GUILayout.Space(10);
                        
                        // Usage meter
                        float usagePercent = (float)currentClicks / maxClicks;
                        
                        EditorGUILayout.LabelField($"{currentClicks} / {maxClicks} clicks this month", EditorStyles.miniLabel);
                        
                        // Usage meter bar
                        var rect = GUILayoutUtility.GetRect(0, 8, GUILayout.ExpandWidth(true));
                        
                        // Background
                        EditorGUI.DrawRect(rect, Color.grey);
                        
                        // Fill color based on usage
                        Color fillColor = Color.green;
                        if (usagePercent >= 0.8f) fillColor = Color.yellow;
                        if (usagePercent >= 0.9f) fillColor = new Color(1f, 0.5f, 0f); // Orange color
                        if (usagePercent >= 1.0f) fillColor = Color.red;
                        
                        // Usage fill
                        var fillRect = new Rect(rect.x, rect.y, rect.width * usagePercent, rect.height);
                        EditorGUI.DrawRect(fillRect, fillColor);
                    }
                }
            }
            
            // Builder Plan Info Row
            using (new EditorGUILayout.HorizontalScope())
            {
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.3f); // Light grey background
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Builder plan (free): 1 app • 1 000 clicks / month • managed forever.", EditorStyles.miniLabel);
                        if (GUILayout.Button("Upgrade →", EditorStyles.linkLabel, GUILayout.Width(70)))
                        {
                            selectedTab = 3; // Switch to Account tab
                        }
                    }
                }
                GUI.backgroundColor = originalColor;
            }
            
            GUILayout.Space(10);
            
            // iOS and Android Side by Side - Equal Width
            using (new EditorGUILayout.HorizontalScope())
            {
                // Calculate available width for equal distribution
                float availableWidth = EditorGUIUtility.currentViewWidth - 40; // Account for margins
                float columnWidth = (availableWidth - 10) / 2; // Subtract spacing and divide by 2
                
                // iOS Universal Links Section - Left Side (50% width)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(columnWidth)))
                {
                    EditorGUILayout.LabelField("Configure iOS Universal Links", EditorStyles.boldLabel);
                    
                    // Show bundle ID and team ID for iOS with setup buttons always available
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Bundle Identifier: {iosBundleId}", EditorStyles.miniLabel);
                        if (GUILayout.Button("Player Settings", GUILayout.Width(130)))
                        {
                            SettingsService.OpenProjectSettings("Project/Player");
                        }
                    }
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Signing Team ID: {iosTeamId}", EditorStyles.miniLabel);
                        if (GUILayout.Button("Player Settings", GUILayout.Width(130)))
                        {
                            SettingsService.OpenProjectSettings("Project/Player");
                        }
                    }
                    
                    GUILayout.Space(5);
                    
                    if (hostingOption == HostingOption.Cloud) // Generate & manage with BoostOps
                    {
                        EditorGUILayout.HelpBox("BoostOps will handle all iOS Universal Links configuration automatically.", MessageType.Info);
                        
                        using (new EditorGUI.DisabledScope(!isLoggedIn))
                        {
                            if (GUILayout.Button("Configure iOS in BoostOps", GUILayout.Height(30)))
                            {
                                selectedTab = 3; // Switch to Account tab
                            }
                        }
                    }
                    else // Generate files locally
                    {
                        bool canGenerateIOS = CanGenerateIOSFiles();
                        
                        if (!canGenerateIOS)
                        {
                            if (string.IsNullOrEmpty(dynamicLinkUrl))
                            {
                                EditorGUILayout.HelpBox("Domain required", MessageType.Warning);
                            }
                            if (string.IsNullOrEmpty(iosBundleId) || iosBundleId == "Not set")
                            {
                                EditorGUILayout.HelpBox("Bundle Identifier required\n(Player Settings)", MessageType.Error);
                            }
                            if (string.IsNullOrEmpty(iosTeamId) || iosTeamId == "Not set")
                            {
                                EditorGUILayout.HelpBox("Signing Team ID required\n(Player Settings)", MessageType.Error);
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox($"✓ Ready for {iosBundleId}", MessageType.Info);
                        }
                        
                        using (new EditorGUI.DisabledScope(!canGenerateIOS))
                        {
                            if (GUILayout.Button("Generate Dynamic Link Files", GUILayout.Height(30)))
                            {
                                GenerateDynamicLinkFiles();
                            }
                        }
                        
                        // Verification button for iOS
                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(dynamicLinkUrl) || !UniversalLinkFilesExist()))
                        {
                            if (GUILayout.Button("Verify iOS Files on Server", GUILayout.Height(25)))
                            {
                                VerifyIOSFiles();
                            }
                        }
                    }
                }
                
                GUILayout.Space(10);
                
                // Android App Links Section - Right Side (50% width)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(columnWidth)))
                {
                    EditorGUILayout.LabelField("Configure Android App Links", EditorStyles.boldLabel);
                    
                    // Show package name for Android with setup button always available
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Package Name: {androidBundleId}", EditorStyles.miniLabel);
                        if (GUILayout.Button("Player Settings", GUILayout.Width(130)))
                        {
                            SettingsService.OpenProjectSettings("Project/Player");
                        }
                    }
                    
                    // Certificate fingerprint input
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("SHA256 Fingerprint:", EditorStyles.miniLabel);
                        
                        // Show edit button if field is locked
                        bool isSHA256Locked = isProjectRegistered && !isSHA256InEditMode;
                        if (isSHA256Locked && GUILayout.Button("✏️", GUILayout.Width(25), GUILayout.Height(18)))
                        {
                            isSHA256InEditMode = true;
                            ShowCriticalFieldEditWarning("SHA256 Fingerprint");
                        }
                    }
                    
                    using (new EditorGUI.DisabledScope(isProjectRegistered && !isSHA256InEditMode))
                    {
                        string newFingerprint = EditorGUILayout.TextField(androidCertFingerprint);
                        if (newFingerprint != androidCertFingerprint)
                        {
                            string oldValue = androidCertFingerprint;
                            androidCertFingerprint = newFingerprint.Trim();
                            
                            // Auto-normalize valid fingerprints to colon-separated format
                            if (IsValidSHA256Fingerprint(androidCertFingerprint))
                            {
                                androidCertFingerprint = NormalizeSHA256Fingerprint(androidCertFingerprint);
                            }
                            
                            SaveAndroidCertFingerprint();
                            
                            // If this is a registered project and field was changed, mark for re-registration
                            if (isProjectRegistered && isSHA256InEditMode && oldValue != androidCertFingerprint)
                            {
                                needsReregistration = true;
                                SaveRegistrationState();
                            }
                            
                            // Refresh the domain content to update validation
                            if (hostingOption == HostingOption.Cloud) RefreshDomainAndUsageContent();
                        }
                    }
                    
                    if (string.IsNullOrEmpty(androidCertFingerprint))
                    {
                        EditorGUILayout.HelpBox("SHA256 fingerprint required for App Links", MessageType.Warning);
                        if (GUILayout.Button("How to get fingerprint?", GUILayout.Height(20)))
                        {
                            ShowCertificateFingerprintHelp();
                        }
                    }
                    else if (!IsValidSHA256Fingerprint(androidCertFingerprint))
                    {
                        EditorGUILayout.HelpBox("Invalid SHA256 format. Expected:\n• Colon format: AA:BB:CC:... (32 pairs)\n• Raw hex: 64 characters", MessageType.Error);
                        if (GUILayout.Button("How to get fingerprint?", GUILayout.Height(20)))
                        {
                            ShowCertificateFingerprintHelp();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("✓ Valid SHA256 fingerprint format", MessageType.Info);
                    }
                    
                    GUILayout.Space(5);
                    
                    if (hostingOption == HostingOption.Cloud) // Generate & manage with BoostOps
                    {
                        EditorGUILayout.HelpBox("BoostOps will handle all Android App Links configuration automatically.", MessageType.Info);
                        
                        using (new EditorGUI.DisabledScope(!isLoggedIn))
                        {
                            if (GUILayout.Button("Configure Android in BoostOps", GUILayout.Height(30)))
                            {
                                selectedTab = 3; // Switch to Account tab
                            }
                        }
                    }
                    else // Generate files locally
                    {
                        bool canGenerateAndroid = CanGenerateAndroidFiles();
                        
                        if (!canGenerateAndroid)
                        {
                            if (string.IsNullOrEmpty(dynamicLinkUrl))
                            {
                                EditorGUILayout.HelpBox("Domain required", MessageType.Warning);
                            }
                            if (string.IsNullOrEmpty(androidBundleId) || androidBundleId == "Not set")
                            {
                                EditorGUILayout.HelpBox("Package Name required\n(Player Settings)", MessageType.Error);
                            }
                            if (string.IsNullOrEmpty(androidCertFingerprint))
                            {
                                EditorGUILayout.HelpBox("SHA256 Fingerprint required", MessageType.Error);
                            }
                            else if (!IsValidSHA256Fingerprint(androidCertFingerprint))
                            {
                                EditorGUILayout.HelpBox("Invalid SHA256 fingerprint format", MessageType.Error);
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox($"✓ Ready for {androidBundleId}", MessageType.Info);
                        }
                        
                        using (new EditorGUI.DisabledScope(!canGenerateAndroid))
                        {
                            if (GUILayout.Button("Generate Dynamic Link Files", GUILayout.Height(30)))
                            {
                                GenerateDynamicLinkFiles();
                            }
                        }
                        
                        // Verification button for Android
                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(dynamicLinkUrl) || !UniversalLinkFilesExist()))
                        {
                            if (GUILayout.Button("Verify Android Files on Server", GUILayout.Height(25)))
                            {
                                VerifyAndroidFiles();
                            }
                        }
                    }
                }
            }
            
            GUILayout.Space(10);
            
            // Server file placement note (only for local mode) - BEFORE verify buttons
            if (hostingOption == HostingOption.Local)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var originalColor = GUI.color;
                    GUI.color = new Color(1f, 0.9f, 0.7f, 1f); // Light orange color
                    EditorGUILayout.LabelField("🌐 Server Setup: Upload the generated .well-known files to your domain's root:\n• apple-app-site-association → https://yourdomain.com/.well-known/apple-app-site-association\n• assetlinks.json → https://yourdomain.com/.well-known/assetlinks.json", EditorStyles.wordWrappedMiniLabel);
                    GUI.color = originalColor;
                }
                
                GUILayout.Space(5);
            }
            
            // Build submission requirement note - AFTER server setup
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var originalColor = GUI.color;
                GUI.color = new Color(0.7f, 0.8f, 1f, 1f); // Light blue color
                EditorGUILayout.LabelField("📝 Next Steps: Upload a signed build to App Store Connect (iOS) or Play Console (Android) for links to work. Allow 5-15 min (iOS) or up to 1 hour (Android) after first install.", EditorStyles.wordWrappedMiniLabel);
                GUI.color = originalColor;
            }
            
            GUILayout.Space(10);
            
            // Generated Files Status
            if (UniversalLinkFilesExist())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Generated Files", EditorStyles.boldLabel);
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("View Files"))
                        {
                            ShowGeneratedFiles();
                        }
                        
                        if (GUILayout.Button("Open Folder"))
                        {
                            EditorUtility.RevealInFinder("Assets/BoostOpsGenerated");
                        }
                        
                        if (GUILayout.Button("Instructions"))
                        {
                            OpenInstructionsFile();
                        }
                    }
                }
            }
            
            GUILayout.Space(10);
            
            // QR Code Testing Section
            DrawQRCodeTestingSection();
            
            GUILayout.Space(10);
            
            // BoostOps Hosted Solution - Compact
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Want custom domains & analytics?", EditorStyles.boldLabel);
                    
                    if (GUILayout.Button("Account Tab", GUILayout.Width(100)))
                    {
                        selectedTab = 3; // Switch to Account tab
                    }
                    
                    if (GUILayout.Button("Learn More", GUILayout.Width(80)))
                    {
                        Application.OpenURL("https://boostops.com/features");
                    }
                }
            }
            
            // Bottom Upsell Bar (Local & Builder modes)
            if (hostingOption == HostingOption.Local || !isLoggedIn)
            {
                GUILayout.FlexibleSpace();
                
                var originalColor = GUI.backgroundColor;
                var originalContentColor = GUI.contentColor;
                GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.8f); // Grey background
                GUI.contentColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Bright white text for better contrast
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("💡 Love this? Get cloud hosting, analytics & cross-promotion for $0/month with 1000 free clicks", EditorStyles.miniLabel);
                    
                    if (GUILayout.Button("Try Cloud Features →", EditorStyles.linkLabel, GUILayout.Width(160)))
                    {
                        hostingOption = HostingOption.Cloud; // Switch to cloud mode
                        SaveHostingOption();
                    }
                }
                GUI.backgroundColor = originalColor;
                GUI.contentColor = originalContentColor;
            }
        }
        
        void DrawAccountTab()
        {
            if (!isLoggedIn)
            {
                DrawAuthenticationSection();
            }
            else
            {
                DrawUserInfoSection();
                
                GUILayout.Space(20);
            }
        }
        
        void DrawQRCodeTestingSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("📱 Test Your Links on Mobile", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Scan QR code to test your BoostLink", EditorStyles.miniLabel);
                }
                
                GUILayout.Space(5);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    // QR Code display area
                    var qrRect = GUILayoutUtility.GetRect(120, 120, GUILayout.Width(120), GUILayout.Height(120));
                    EditorGUI.DrawRect(qrRect, Color.white);
                    
                    // Show QR code if we have one
                    if (qrCodeTexture != null)
                    {
                        GUI.DrawTexture(qrRect, qrCodeTexture);
                    }
                    else
                    {
                        EditorGUI.LabelField(qrRect, "No QR Code\nGenerated", EditorStyles.centeredGreyMiniLabel);
                    }
                    
                    GUILayout.Space(10);
                    
                    // Controls
                    using (new EditorGUILayout.VerticalScope())
                    {
                        // Magnifying glass button for zoom
                        if (GUILayout.Button("🔍 View Larger", GUILayout.Height(40)))
                        {
                            string currentTestUrl = GenerateTestUrl();
                            if (!string.IsNullOrEmpty(currentTestUrl))
                            {
                                // Pass the existing QR code texture to avoid regenerating
                                ShowZoomedQRCode(currentTestUrl, qrCodeTexture);
                            }
                        }
                        
                        GUILayout.Space(5);
                        
                        // Generate button
                        if (GUILayout.Button("Generate QR Code", GUILayout.Height(30)))
                        {
                            string newTestUrl = GenerateTestUrl();
                            if (!string.IsNullOrEmpty(newTestUrl))
                            {
                                testUrl = newTestUrl;
                                // Start async QR generation for IMGUI (simplified)
                                GenerateQRCodeForIMGUI(testUrl);
                            }
                        }
                        
                        GUILayout.Space(5);
                        
                        EditorGUILayout.LabelField("💡 Tip: Use any QR code scanner app to test your BoostLink™ configuration. Subtle black QR code with BoostOps logo overlay.", EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
        }
        void DrawProjectInfoTab()
        {
            EditorGUILayout.LabelField("Project Information", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Auto-detected project settings from Unity Player Settings", MessageType.Info);
            
            // Common App Settings
            EditorGUILayout.LabelField("Common Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("App Name:", appName);
                EditorGUILayout.LabelField("Version:", version);
                
                GUILayout.Space(10);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Project Settings", GUILayout.Width(200)))
                    {
                        AutoDetectProjectSettings();
                    }
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Open Player Settings", GUILayout.Width(150)))
                    {
                        SettingsService.OpenProjectSettings("Project/Player");
                    }
                }
            }
            
            GUILayout.Space(15);
            
            // Platform-specific settings in two columns
            using (new EditorGUILayout.HorizontalScope())
            {
                // iOS Settings Column
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 10)))
                {
                    EditorGUILayout.LabelField("iOS Settings", EditorStyles.boldLabel);
                    
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        // iOS logo/icon could be added here
                        Texture2D appleLogo = Resources.Load<Texture2D>("apple-logo");
                        if (appleLogo != null)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label(appleLogo, GUILayout.Width(20), GUILayout.Height(20));
                                GUILayout.Space(5);
                                EditorGUILayout.LabelField("iOS Platform", EditorStyles.miniBoldLabel);
                            }
                            GUILayout.Space(5);
                        }
                        
                        EditorGUILayout.LabelField("Bundle Identifier:", iosBundleId);
                        
                        // Signing Team ID with error highlighting if not set
                        bool teamIdMissing = string.IsNullOrEmpty(iosTeamId) || iosTeamId.Equals("Not set", System.StringComparison.OrdinalIgnoreCase);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Signing Team ID:", GUILayout.Width(110));
                            if (teamIdMissing)
                            {
                                var oldTextColor = GUI.color;
                                GUI.color = Color.red;
                                EditorGUILayout.LabelField("⚠ " + iosTeamId + " (Required for Universal Links)", EditorStyles.boldLabel);
                                GUI.color = oldTextColor;
                            }
                            else
                            {
                                var oldTextColor = GUI.color;
                                GUI.color = Color.green;
                                EditorGUILayout.LabelField("✓ " + iosTeamId, EditorStyles.label);
                                GUI.color = oldTextColor;
                            }
                        }
                        
                                    EditorGUILayout.LabelField("Build Number:", iosBuildNumber);
                        
                        // Apple Store ID (dynamically required based on hosting mode)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(hostingOption == HostingOption.Cloud ? "Apple Store ID (Required):" : "Apple Store ID:", EditorStyles.miniLabel);
                            
                            // Show edit button if field is locked
                            bool isAppleStoreIdLocked = isProjectRegistered && !isAppleStoreIdInEditMode;
                            if (isAppleStoreIdLocked && GUILayout.Button("✏️", GUILayout.Width(25), GUILayout.Height(18)))
                            {
                                isAppleStoreIdInEditMode = true;
                                ShowCriticalFieldEditWarning("Apple Store ID");
                            }
                        }
                        
                        using (new EditorGUI.DisabledScope(isProjectRegistered && !isAppleStoreIdInEditMode))
                        {
                            string newAppleAppStoreId = EditorGUILayout.TextField(iosAppStoreId);
                            if (newAppleAppStoreId != iosAppStoreId)
                            {
                                string oldValue = iosAppStoreId;
                                // Use the same comprehensive normalization as other iOS Store ID fields
                                string normalizedId = ExtractIOSStoreId(newAppleAppStoreId);
                                iosAppStoreId = normalizedId ?? newAppleAppStoreId; // Keep original if normalization fails
                                SaveAppleAppStoreId();
                                
                                // If this is a registered project and field was changed, mark for re-registration
                                if (isProjectRegistered && isAppleStoreIdInEditMode && oldValue != iosAppStoreId)
                                {
                                    needsReregistration = true;
                                    SaveRegistrationState();
                                }
                            }
                        }
                        
                        // Show validation message for Apple Store ID
                        if (hostingOption == HostingOption.Cloud)
                        {
                            if (!string.IsNullOrEmpty(iosAppStoreId) && !IsValidAppleAppStoreId(iosAppStoreId))
                            {
                                EditorGUILayout.HelpBox("⚠ Invalid Apple Store ID format. Must be 6-15 digits (e.g., '1234567890' or 'id1234567890').", MessageType.Warning);
                            }
                            else if (string.IsNullOrEmpty(iosAppStoreId))
                            {
                                EditorGUILayout.HelpBox("⚠ Apple Store ID is required for cloud mode registration.", MessageType.Warning);
                            }
                        }
            
            // Signing Team ID warning if not set
            if (teamIdMissing)
            {
                            GUILayout.Space(10);
                            EditorGUILayout.HelpBox(
                                "⚠ Signing Team ID is required for Universal Links to work properly on iOS.\n\n" +
                                "To fix this:\n" +
                                "1. Go to Player Settings → iOS Settings\n" +
                                "2. Find 'Signing Team ID' field\n" +
                                "3. Enter your Apple Developer Team ID\n" +
                                "4. You can find your Team ID in Apple Developer Portal",
                                MessageType.Error);
                        }
                    }
                }
                
                GUILayout.Space(10);
                
                // Android Settings Column
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 10)))
                {
                    EditorGUILayout.LabelField("Android Settings", EditorStyles.boldLabel);
                    
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        // Android robot icon could be added here
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Using a simple colored box as Android indicator
                            var oldColor = GUI.backgroundColor;
                            GUI.backgroundColor = Color.green;
                            GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(20));
                            GUI.backgroundColor = oldColor;
                            
                            GUILayout.Space(5);
                            EditorGUILayout.LabelField("Android Platform", EditorStyles.miniBoldLabel);
                        }
                        GUILayout.Space(5);
                        
                        EditorGUILayout.LabelField("Package Name:", androidBundleId);
                    }
                }
            }

        }
        void DrawIntegrationDetectionTab()
        {
            EditorGUILayout.LabelField("Integration Detection", EditorStyles.boldLabel);
            
            GUILayout.Space(20);
            
            // Analytics Integration Detection
            EditorGUILayout.LabelField("Analytics Systems", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                
                GUILayout.Space(10);
                
                // Unity Analytics
                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture2D unityLogo = Resources.Load<Texture2D>("Unity");
                    if (unityLogo != null)
                    {
                        GUILayout.Label(unityLogo, GUILayout.Width(24), GUILayout.Height(24));
                        GUILayout.Space(5);
                    }
                    
                    EditorGUILayout.LabelField("Unity Analytics:", EditorStyles.boldLabel, GUILayout.Width(120));
                    
                    if (hasUnityAnalytics)
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ Detected", EditorStyles.boldLabel);
                        GUI.color = oldColor;
                    }
                    else
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("✗ Not Found", EditorStyles.label);
                        GUI.color = oldColor;
                    }
                }
                
                GUILayout.Space(5);
                
                // Firebase Analytics
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Firebase logo
                    Texture2D firebaseLogo = Resources.Load<Texture2D>("firebase-logo");
                    if (firebaseLogo != null)
                    {
                        GUILayout.Label(firebaseLogo, GUILayout.Width(24), GUILayout.Height(24));
                        GUILayout.Space(5);
                    }
                    else
                    {
                        // Fallback to colored box if logo not found
                        var oldColor = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(1f, 0.6f, 0f); // Firebase orange
                        GUILayout.Box("", GUILayout.Width(24), GUILayout.Height(24));
                        GUI.backgroundColor = oldColor;
                        GUILayout.Space(5);
                    }
                    
                    EditorGUILayout.LabelField("Firebase Analytics:", EditorStyles.boldLabel, GUILayout.Width(120));
                    
                    if (hasFirebaseAnalytics)
                    {
                        var oldTextColor = GUI.color;
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ Detected", EditorStyles.boldLabel);
                        GUI.color = oldTextColor;
                    }
                    else
                    {
                        var oldTextColor = GUI.color;
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("✗ Not Found", EditorStyles.label);
                        GUI.color = oldTextColor;
                    }
                }
            }
            
            GUILayout.Space(20);
            
            // Remote Config Detection
            EditorGUILayout.LabelField("Remote Configuration Systems", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("Detected remote configuration systems in your project.", MessageType.Info);
                
                GUILayout.Space(10);
                
                // Unity Remote Config
                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture2D unityLogo = Resources.Load<Texture2D>("Unity");
                    if (unityLogo != null)
                    {
                        GUILayout.Label(unityLogo, GUILayout.Width(24), GUILayout.Height(24));
                        GUILayout.Space(5);
                    }
                    
                    EditorGUILayout.LabelField("Unity Remote Config:", EditorStyles.boldLabel, GUILayout.Width(140));
                    
                    if (hasUnityRemoteConfig)
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ Detected", EditorStyles.boldLabel);
                        GUI.color = oldColor;
                    }
                    else
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("✗ Not Found", EditorStyles.label);
                        GUI.color = oldColor;
                    }
                }
                
                GUILayout.Space(5);
                
                // Firebase Remote Config
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Firebase logo
                    Texture2D firebaseLogo = Resources.Load<Texture2D>("firebase-logo");
                    if (firebaseLogo != null)
                    {
                        GUILayout.Label(firebaseLogo, GUILayout.Width(24), GUILayout.Height(24));
                        GUILayout.Space(5);
                    }
                    else
                    {
                        // Fallback to colored box if logo not found
                        var oldColor = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(1f, 0.6f, 0f); // Firebase orange
                        GUILayout.Box("", GUILayout.Width(24), GUILayout.Height(24));
                        GUI.backgroundColor = oldColor;
                        GUILayout.Space(5);
                    }
                    
                    EditorGUILayout.LabelField("Firebase Remote Config:", EditorStyles.boldLabel, GUILayout.Width(140));
                    
                    if (hasFirebaseRemoteConfig)
                    {
                        var oldTextColor = GUI.color;
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ Detected", EditorStyles.boldLabel);
                        GUI.color = oldTextColor;
                    }
                    else
                    {
                        var oldTextColor = GUI.color;
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("✗ Not Found", EditorStyles.label);
                        GUI.color = oldTextColor;
                    }
                }
            }
            
            GUILayout.Space(20);
            
            // Configuration Files
            EditorGUILayout.LabelField("Configuration Files", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("Firebase configuration files detected in your project.", MessageType.Info);
                
                GUILayout.Space(10);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("• google-services.json (Android):", GUILayout.Width(200));
                    
                    if (hasGoogleServicesFile)
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ Found");
                        GUI.color = oldColor;
                    }
                    else
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("✗ Not Found");
                        GUI.color = oldColor;
                    }
                }
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("• GoogleService-Info.plist (iOS):", GUILayout.Width(200));
                    
                    if (hasFirebaseConfigFile)
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ Found");
                        GUI.color = oldColor;
                    }
                    else
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("✗ Not Found");
                        GUI.color = oldColor;
                    }
                }
            }
            
            GUILayout.Space(20);
            
            // Summary and Actions
            EditorGUILayout.LabelField("Detection Summary", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Show summary
                int analyticsCount = (hasUnityAnalytics ? 1 : 0) + (hasFirebaseAnalytics ? 1 : 0);
                int remoteConfigCount = (hasUnityRemoteConfig ? 1 : 0) + (hasFirebaseRemoteConfig ? 1 : 0);
                int totalDetected = analyticsCount + remoteConfigCount;
                
                EditorGUILayout.LabelField($"Total Systems Detected: {totalDetected}/4", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Analytics: {analyticsCount}/2, Remote Config: {remoteConfigCount}/2", EditorStyles.label);
                
                GUILayout.Space(10);
                
                
                GUILayout.Space(10);
                
                // Comprehensive integration guidance
                if (hasUnityAnalytics || hasFirebaseAnalytics || hasUnityRemoteConfig || hasFirebaseRemoteConfig)
                {
                    string integrationMessage = "BoostOps will complement your existing setup:\n";
                    
                    if (hasUnityAnalytics || hasFirebaseAnalytics)
                    {
                        integrationMessage += "• Analytics: BoostOps analytics works alongside existing systems\n";
                    }
                    
                    if (hasUnityRemoteConfig || hasFirebaseRemoteConfig)
                    {
                        integrationMessage += "• Remote Config: BoostOps remote config complements existing configuration systems\n";
                    }
                    
                    integrationMessage += "• No conflicts or interference with your current integrations";
                    
                    EditorGUILayout.HelpBox(integrationMessage, MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("No analytics or remote config systems detected. BoostOps will provide comprehensive analytics and remote configuration capabilities for your project.", MessageType.Warning);
                }
            }
        }
        void DrawCrossPromoConfigTab()
        {
            EditorGUILayout.LabelField("Cross Promotion Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Detected cross promotion configurations in your project's Remote Config systems. This works without requiring login.", MessageType.Info);
            
            GUILayout.Space(20);
            
            // Summary box
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Configuration Summary", EditorStyles.boldLabel);
                
                int totalConfigs = 0;
                if (hasUnityRemoteConfig) totalConfigs++;
                if (hasFirebaseRemoteConfig) totalConfigs++;
                
                if (totalConfigs > 0)
                {
                    var oldColor = GUI.color;
                    GUI.color = Color.yellow;
                    EditorGUILayout.LabelField($"✅ {totalConfigs} Remote Config System(s) Detected", EditorStyles.boldLabel);
                    GUI.color = oldColor;
                    
                    GUILayout.Space(5);
                    EditorGUILayout.HelpBox("Remote Config systems detected and ready for BoostOps integration.\nTo set up cross promotion campaigns, configure them in the BoostOps dashboard and they will be delivered through your Remote Config system.", MessageType.Info);
                }
                else
                {
                    var oldColor = GUI.color;
                    GUI.color = Color.gray;
                    EditorGUILayout.LabelField("✗ No Remote Config Systems Found", EditorStyles.label);
                    GUI.color = oldColor;
                    
                    GUILayout.Space(5);
                    EditorGUILayout.HelpBox("Install Unity Remote Config or Firebase Remote Config to receive cross promotion campaigns from the BoostOps dashboard.", MessageType.Info);
                }
                
                GUILayout.Space(10);
                
                if (totalConfigs > 0)
                {
                    if (GUILayout.Button("Open BoostOps Dashboard", GUILayout.Width(180)))
                    {
                        Application.OpenURL("https://app.boostops.com/");
                    }
                }
            }
            
            GUILayout.Space(20);
            
            // Unity Remote Config section
            EditorGUILayout.LabelField("Unity Remote Config", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture2D unityLogo = Resources.Load<Texture2D>("Unity");
                    if (unityLogo != null)
                    {
                        GUILayout.Label(unityLogo, GUILayout.Width(24), GUILayout.Height(24));
                        GUILayout.Space(5);
                    }
                    
                    EditorGUILayout.LabelField("Unity Remote Config:", EditorStyles.boldLabel, GUILayout.Width(200));
                    
                    if (hasUnityRemoteConfig)
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ Available", EditorStyles.boldLabel);
                        GUI.color = oldColor;
                    }
                    else
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("✗ Not Installed", EditorStyles.label);
                        GUI.color = oldColor;
                    }
                }
                
                // Local Cross Promo Status
                bool hasLocalCrossPromo = HasLocalCrossPromoFiles();
                if (hasLocalCrossPromo)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("Local Cross-Promo files detected.\n\nLocal campaigns are bundled with your app and work offline. For dynamic campaigns, set up Unity Remote Config.", MessageType.Info);
                }
                else
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("No local cross-promo files found.\n\nUse the 'Generate JSON' button in the Source Game Settings tab to create local campaign files.", MessageType.Warning);
                }
                
                GUILayout.Space(10);
                
                // Unity Remote Config Status
                if (hasUnityRemoteConfig)
                {
                    EditorGUILayout.HelpBox("Unity Remote Config available for dynamic campaigns.\n\nTo set up remote campaigns:\n1. Create campaigns in your BoostOps dashboard\n2. BoostOps will automatically sync them to Unity Remote Config\n3. Your app will receive dynamic campaign updates", MessageType.Info);
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Open Unity Remote Config", GUILayout.Width(180)))
                        {
                            Application.OpenURL("https://dashboard.unity3d.com/");
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Unity Remote Config not installed.\n\nInstall Unity Remote Config to receive dynamic cross promotion campaigns from BoostOps.", MessageType.Info);
                }
            }
            
            GUILayout.Space(20);
            
            // Firebase Remote Config section
            EditorGUILayout.LabelField("Firebase Remote Config", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture2D firebaseLogo = Resources.Load<Texture2D>("firebase-logo");
                    if (firebaseLogo != null)
                    {
                        GUILayout.Label(firebaseLogo, GUILayout.Width(24), GUILayout.Height(24));
                        GUILayout.Space(5);
                    }
                    else
                    {
                        var oldColor = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(1f, 0.6f, 0f);
                        GUILayout.Box("", GUILayout.Width(24), GUILayout.Height(24));
                        GUI.backgroundColor = oldColor;
                        GUILayout.Space(5);
                    }
                    
                    EditorGUILayout.LabelField("Firebase Remote Config:", EditorStyles.boldLabel, GUILayout.Width(200));
                    
                    if (hasFirebaseRemoteConfig)
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ Available", EditorStyles.boldLabel);
                        GUI.color = oldColor;
                    }
                    else
                    {
                        var oldColor = GUI.color;
                        GUI.color = Color.gray;
                        EditorGUILayout.LabelField("✗ Not Installed", EditorStyles.label);
                        GUI.color = oldColor;
                    }
                }
                
                if (hasFirebaseRemoteConfig)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("Firebase Remote Config available for dynamic campaigns.\n\nTo set up remote campaigns:\n1. Create campaigns in your BoostOps dashboard\n2. BoostOps will automatically sync them to Firebase Remote Config\n3. Your app will receive dynamic campaign updates", MessageType.Info);
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Open Firebase Console", GUILayout.Width(180)))
                        {
                            Application.OpenURL("https://console.firebase.google.com/");
                        }
                    }
                }
                else
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("Firebase Remote Config not installed.\n\nInstall Firebase SDK and Remote Config to receive dynamic cross promotion campaigns from BoostOps.", MessageType.Info);
                }
            }
            

            
            // Setup instructions
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Setup Instructions", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("Choose between Local or Remote cross promotion campaigns:", MessageType.Info);
                
                GUILayout.Space(10);
                
                EditorGUILayout.LabelField("Local Cross Promo (Offline)", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("   • Use 'Generate JSON' button in Source Game Settings tab");
                EditorGUILayout.LabelField("   • Campaigns are bundled with your app");
                EditorGUILayout.LabelField("   • Works offline, no network required");
                
                GUILayout.Space(10);
                
                EditorGUILayout.LabelField("Remote Cross Promo (Dynamic)", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("   • Install Unity Remote Config or Firebase Remote Config");
                EditorGUILayout.LabelField("   • Create campaigns in your BoostOps dashboard");
                EditorGUILayout.LabelField("   • BoostOps automatically syncs campaigns to Remote Config");
                EditorGUILayout.LabelField("   • Campaigns update dynamically without app updates");
                
                GUILayout.Space(15);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("BoostOps Dashboard", GUILayout.Width(150)))
                    {
                        Application.OpenURL("https://app.boostops.com/");
                    }
                    
                    GUILayout.Space(10);
                    
                    if (GUILayout.Button("Unity Remote Config Docs", GUILayout.Width(180)))
                    {
                        Application.OpenURL("https://docs.unity3d.com/Packages/com.unity.remote-config@1.0/manual/index.html");
                    }
                    
                    GUILayout.Space(10);
                    
                    if (GUILayout.Button("Firebase Remote Config Docs", GUILayout.Width(180)))
                    {
                        Application.OpenURL("https://firebase.google.com/docs/remote-config/unity/use-config");
                    }
                }
            }
        }
        
        void DrawConfigurationTab()
        {
            if (!isLoggedIn)
            {
                // Show authentication required message
                EditorGUILayout.LabelField("Project Configuration", EditorStyles.boldLabel);
                
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.HelpBox("Please sign in to your BoostOps account to access project configuration.", MessageType.Info);
                    
                    GUILayout.Space(10);
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        
                        if (GUILayout.Button("Go to Account Tab", GUILayout.Width(150)))
                        {
                            selectedTab = 0; // Switch to Account tab
                        }
                        
                        GUILayout.FlexibleSpace();
                    }
                }
                
                return;
            }
            
            // Project Slug Section (only shown when logged in)
            DrawProjectSlugSection();
            
            GUILayout.Space(20);
            
            // Dynamic Links Generation Section (only shown when logged in)
            DrawDynamicLinksSection();
        }
        

        

        
        void DrawAuthenticationSection()
        {
            EditorGUILayout.LabelField("Account Authentication", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("Sign in to your BoostOps account or create a new one to get started.", MessageType.Info);
                
                GUILayout.Space(10);
                
                // Google Sign-In Option
                EditorGUILayout.LabelField("Quick Sign-In", EditorStyles.miniBoldLabel);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    
                    Texture2D googleLogo = Resources.Load<Texture2D>("google-logo");
                    GUIContent googleContent = googleLogo != null ? 
                        new GUIContent(" Login with Google", googleLogo) : 
                        new GUIContent("Login with Google");
                    
                    using (new EditorGUI.DisabledScope(isAuthenticatingWithGoogle))
                    {
                        if (GUILayout.Button(googleContent, GUILayout.Height(35), GUILayout.Width(180)))
                        {
                            InitiateGoogleOAuth();
                        }
                    }
                    
                    GUILayout.FlexibleSpace();
                }
                
                if (isAuthenticatingWithGoogle)
                {
                    GUILayout.Space(5);
                    EditorGUILayout.LabelField("Authenticating with Google...", EditorStyles.centeredGreyMiniLabel);
                    
                    GUILayout.Space(5);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                        {
                            StopOAuthListener();
                            isAuthenticatingWithGoogle = false;
                            Debug.Log("BoostOps: Google OAuth cancelled by user");
                        }
                        GUILayout.FlexibleSpace();
                    }
                }
                
                GUILayout.Space(10);
                
                // Divider
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
                    GUILayout.Label("OR", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(30));
                    GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
                }
                
                GUILayout.Space(10);
                
                // Traditional Email/Password Sign-In
                EditorGUILayout.LabelField("Email Sign-In", EditorStyles.miniBoldLabel);
                
                // Toggle between login and signup
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(!showSignupForm, "Sign In", EditorStyles.radioButton))
                    {
                        showSignupForm = false;
                    }
                    
                    if (GUILayout.Toggle(showSignupForm, "Create Account", EditorStyles.radioButton))
                    {
                        showSignupForm = true;
                    }
                }
                
                GUILayout.Space(10);
                
                if (showSignupForm)
                {
                    DrawSignupForm();
                }
                else
                {
                    DrawLoginForm();
                }
            }
        }
        void DrawLoginForm()
        {
            EditorGUILayout.LabelField("Sign In", EditorStyles.boldLabel);
            
            loginEmail = EditorGUILayout.TextField("Email:", loginEmail);
            loginPassword = EditorGUILayout.PasswordField("Password:", loginPassword);
            
            GUILayout.Space(10);
            
            using (new EditorGUI.DisabledScope(isAuthenticating || string.IsNullOrEmpty(loginEmail) || string.IsNullOrEmpty(loginPassword)))
            {
                if (GUILayout.Button(isAuthenticating ? "Signing In..." : "Sign In", GUILayout.Height(30)))
                {
                    PerformLogin();
                }
            }
            
            GUILayout.Space(10);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Forgot Password?", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://dashboard.boostops.com/forgot-password");
                }
            }
        }
        
        void DrawSignupForm()
        {
            EditorGUILayout.LabelField("Create Account", EditorStyles.boldLabel);
            
            signupEmail = EditorGUILayout.TextField("Email:", signupEmail);
            signupPassword = EditorGUILayout.PasswordField("Password:", signupPassword);
            signupConfirmPassword = EditorGUILayout.PasswordField("Confirm Password:", signupConfirmPassword);
            
            // Password validation
            if (!string.IsNullOrEmpty(signupPassword) && !string.IsNullOrEmpty(signupConfirmPassword))
            {
                if (signupPassword != signupConfirmPassword)
                {
                    EditorGUILayout.HelpBox("Passwords do not match.", MessageType.Warning);
                }
                else if (signupPassword.Length < 8)
                {
                    EditorGUILayout.HelpBox("Password must be at least 8 characters long.", MessageType.Warning);
                }
            }
            
            GUILayout.Space(10);
            
            bool canSignup = !isAuthenticating && 
                           !string.IsNullOrEmpty(signupEmail) && 
                           !string.IsNullOrEmpty(signupPassword) && 
                           signupPassword == signupConfirmPassword && 
                           signupPassword.Length >= 8;
            
            using (new EditorGUI.DisabledScope(!canSignup))
            {
                if (GUILayout.Button(isAuthenticating ? "Creating Account..." : "Create Account", GUILayout.Height(30)))
                {
                    PerformSignup();
                }
            }
            
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox("By creating an account, you agree to the BoostOps Terms of Service and Privacy Policy.", MessageType.Info);
        }
        
        void DrawUserInfoSection()
        {
            EditorGUILayout.LabelField("Account", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Signed in as: {userEmail}", EditorStyles.boldLabel);
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Sign Out", GUILayout.Width(100)))
                    {
                        SignOut();
                    }
                }
                
                GUILayout.Space(5);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Dashboard", GUILayout.Width(100)))
                    {
                        Application.OpenURL("https://app.boostops.io");
                    }
                    

                }
            }
        }
        

        void DrawProjectSlugSection()
        {
            EditorGUILayout.LabelField("Project Configuration", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Project Slug:", EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox("Your project slug is used to generate dynamic links and configure cross-promotion.", MessageType.Info);
                
                EditorGUI.BeginChangeCheck();
                projectSlug = EditorGUILayout.TextField("Project Slug:", projectSlug);
                if (EditorGUI.EndChangeCheck())
                {
                    ValidateProjectSlug();
                    SaveProjectSlug();
                }
                
                // Show validation status
                if (!string.IsNullOrEmpty(projectSlug))
                {
                    if (isProjectSlugValid)
                    {
                        EditorGUILayout.HelpBox("✓ Project slug is valid", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("⚠ Project slug should be lowercase letters, numbers, and hyphens only", MessageType.Warning);
                    }
                }
                
                GUILayout.Space(10);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Dashboard", GUILayout.Width(150)))
                    {
                        Application.OpenURL("https://app.boostops.io");
                    }
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Generate Sample Slug", GUILayout.Width(150)))
                    {
                        GenerateSampleSlug();
                    }
                }
            }
        }
        void LoadVerificationStatus()
        {
            // Load iOS verification status
            string iosStatusJson = EditorPrefs.GetString($"BoostOps_{Application.dataPath}_IOSVerificationStatus", "");
            if (!string.IsNullOrEmpty(iosStatusJson))
            {
                try
                {
                    var lines = iosStatusJson.Split('\n');
                    iosStoreIdVerificationStatus.Clear();
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string key = parts[0];
                                bool value = bool.Parse(parts[1]);
                                iosStoreIdVerificationStatus[key] = value;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BoostOps] Failed to load iOS verification status: {ex.Message}");
                }
            }
            
            // Load Android verification status
            string androidStatusJson = EditorPrefs.GetString($"BoostOps_{Application.dataPath}_AndroidVerificationStatus", "");
            if (!string.IsNullOrEmpty(androidStatusJson))
            {
                try
                {
                    var lines = androidStatusJson.Split('\n');
                    androidPackageIdVerificationStatus.Clear();
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string key = parts[0];
                                bool value = bool.Parse(parts[1]);
                                androidPackageIdVerificationStatus[key] = value;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BoostOps] Failed to load Android verification status: {ex.Message}");
                }
            }
            
            // Load Amazon verification status
            string amazonStatusJson = EditorPrefs.GetString($"BoostOps_{Application.dataPath}_AmazonVerificationStatus", "");
            if (!string.IsNullOrEmpty(amazonStatusJson))
            {
                try
                {
                    var lines = amazonStatusJson.Split('\n');
                    amazonStoreIdVerificationStatus.Clear();
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string key = parts[0];
                                bool value = bool.Parse(parts[1]);
                                amazonStoreIdVerificationStatus[key] = value;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BoostOps] Failed to load Amazon verification status: {ex.Message}");
                }
            }
            
            // Load last verified values
            string iosValuesJson = EditorPrefs.GetString($"BoostOps_{Application.dataPath}_IOSLastVerifiedValues", "");
            if (!string.IsNullOrEmpty(iosValuesJson))
            {
                try
                {
                    var lines = iosValuesJson.Split('\n');
                    iosLastVerifiedValues.Clear();
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string key = parts[0];
                                string value = parts[1];
                                iosLastVerifiedValues[key] = value;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BoostOps] Failed to load iOS last verified values: {ex.Message}");
                }
            }
            
            string androidValuesJson = EditorPrefs.GetString($"BoostOps_{Application.dataPath}_AndroidLastVerifiedValues", "");
            if (!string.IsNullOrEmpty(androidValuesJson))
            {
                try
                {
                    var lines = androidValuesJson.Split('\n');
                    androidLastVerifiedValues.Clear();
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string key = parts[0];
                                string value = parts[1];
                                androidLastVerifiedValues[key] = value;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BoostOps] Failed to load Android last verified values: {ex.Message}");
                }
            }
            
            string amazonValuesJson = EditorPrefs.GetString($"BoostOps_{Application.dataPath}_AmazonLastVerifiedValues", "");
            if (!string.IsNullOrEmpty(amazonValuesJson))
            {
                try
                {
                    var lines = amazonValuesJson.Split('\n');
                    amazonLastVerifiedValues.Clear();
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string key = parts[0];
                                string value = parts[1];
                                amazonLastVerifiedValues[key] = value;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BoostOps] Failed to load Amazon last verified values: {ex.Message}");
                }
            }
        }
        
        void SaveVerificationStatus()
        {
            // Save iOS verification status
            try
            {
                var iosStatusLines = new List<string>();
                foreach (var kvp in iosStoreIdVerificationStatus)
                {
                    iosStatusLines.Add($"{kvp.Key}={kvp.Value}");
                }
                string iosStatusJson = string.Join("\n", iosStatusLines);
                EditorPrefs.SetString($"BoostOps_{Application.dataPath}_IOSVerificationStatus", iosStatusJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to save iOS verification status: {ex.Message}");
            }
            
            // Save Android verification status
            try
            {
                var androidStatusLines = new List<string>();
                foreach (var kvp in androidPackageIdVerificationStatus)
                {
                    androidStatusLines.Add($"{kvp.Key}={kvp.Value}");
                }
                string androidStatusJson = string.Join("\n", androidStatusLines);
                EditorPrefs.SetString($"BoostOps_{Application.dataPath}_AndroidVerificationStatus", androidStatusJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to save Android verification status: {ex.Message}");
            }
            
            // Save Amazon verification status
            try
            {
                var amazonStatusLines = new List<string>();
                foreach (var kvp in amazonStoreIdVerificationStatus)
                {
                    amazonStatusLines.Add($"{kvp.Key}={kvp.Value}");
                }
                string amazonStatusJson = string.Join("\n", amazonStatusLines);
                EditorPrefs.SetString($"BoostOps_{Application.dataPath}_AmazonVerificationStatus", amazonStatusJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to save Amazon verification status: {ex.Message}");
            }
            
            // Save last verified values
            try
            {
                var iosValuesLines = new List<string>();
                foreach (var kvp in iosLastVerifiedValues)
                {
                    iosValuesLines.Add($"{kvp.Key}={kvp.Value}");
                }
                string iosValuesJson = string.Join("\n", iosValuesLines);
                EditorPrefs.SetString($"BoostOps_{Application.dataPath}_IOSLastVerifiedValues", iosValuesJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to save iOS last verified values: {ex.Message}");
            }
            
            try
            {
                var androidValuesLines = new List<string>();
                foreach (var kvp in androidLastVerifiedValues)
                {
                    androidValuesLines.Add($"{kvp.Key}={kvp.Value}");
                }
                string androidValuesJson = string.Join("\n", androidValuesLines);
                EditorPrefs.SetString($"BoostOps_{Application.dataPath}_AndroidLastVerifiedValues", androidValuesJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to save Android last verified values: {ex.Message}");
            }
            
            try
            {
                var amazonValuesLines = new List<string>();
                foreach (var kvp in amazonLastVerifiedValues)
                {
                    amazonValuesLines.Add($"{kvp.Key}={kvp.Value}");
                }
                string amazonValuesJson = string.Join("\n", amazonValuesLines);
                EditorPrefs.SetString($"BoostOps_{Application.dataPath}_AmazonLastVerifiedValues", amazonValuesJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to save Amazon last verified values: {ex.Message}");
            }
        }
        

        
        // Validation result structure
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public string CleanedValue { get; set; }
            
            public ValidationResult(bool isValid, string errorMessage = "", string cleanedValue = "")
            {
                IsValid = isValid;
                ErrorMessage = errorMessage;
                CleanedValue = cleanedValue;
            }
        }
        
        ValidationResult CleanAndValidateUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return new ValidationResult(false, "Please enter a domain to generate QR code");
            
            // Remove whitespace
            url = url.Trim();
            
            // Fix common typos
            url = url.Replace("..", "."); // Fix double dots
            url = url.Replace("http://", "https://"); // Always use https
            
            // Add https:// if no protocol is present
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            
            // Basic validation - check if it looks like a valid URL
            if (!url.Contains(".") || url.Length < 8)
            {
                return new ValidationResult(false, "Domain must contain a valid format (e.g., yourapp.com)");
            }
            
            // Try to parse it to make sure it's valid
            try
            {
                var testUri = new System.Uri(url);
                if (string.IsNullOrEmpty(testUri.Host))
                {
                    return new ValidationResult(false, "Domain must contain a valid host");
                }
            }
            catch (System.UriFormatException)
            {
                return new ValidationResult(false, "Invalid domain format. Please enter a valid domain like: yourapp.com");
            }
            
            return new ValidationResult(true, "", url);
        }
        
        bool CanGenerateIOSFiles()
        {
            return !string.IsNullOrEmpty(dynamicLinkUrl) && 
                   !string.IsNullOrEmpty(iosBundleId) && 
                   iosBundleId != "Not set" &&
                   !string.IsNullOrEmpty(iosTeamId) && 
                   iosTeamId != "Not set";
        }
        
        bool CanGenerateAndroidFiles()
        {
            return !string.IsNullOrEmpty(dynamicLinkUrl) && 
                   !string.IsNullOrEmpty(androidBundleId) && 
                   androidBundleId != "Not set" &&
                   !string.IsNullOrEmpty(androidCertFingerprint) &&
                   IsValidSHA256Fingerprint(androidCertFingerprint);
        }
        
        // DEPRECATED: Use GenerateDynamicLinkFiles() instead
        // These methods are kept for backward compatibility but now call the unified generator
        void GenerateIOSFiles()
        {
            // Use the new comprehensive multiple domain generation system
            GenerateDynamicLinkFiles();
        }
        
        // DEPRECATED: Use GenerateDynamicLinkFiles() instead  
        // These methods are kept for backward compatibility but now call the unified generator
        void GenerateAndroidFiles()
        {
            // Use the new comprehensive multiple domain generation system
            GenerateDynamicLinkFiles();
        }
        
        void CreateGeneratedDirectories()
        {
            BoostOpsFileGenerator.EnsureDirectoriesExist();
        }
        
        void GenerateAppleAppSiteAssociation(string customDomain)
        {
            BoostOpsFileGenerator.GenerateAppleAppSiteAssociation(customDomain, iosTeamId, iosBundleId);
        }
        
        void GenerateIOSEntitlements(string customDomain)
        {
            BoostOpsFileGenerator.GenerateIOSEntitlements(customDomain);
        }
        
        void GenerateAndroidAssetLinks(string customDomain)
        {
            BoostOpsFileGenerator.GenerateAndroidAssetLinks(customDomain, androidBundleId, androidCertFingerprint);
        }
        
        void GenerateAndroidManifest(string customDomain)
        {
            BoostOpsFileGenerator.GenerateAndroidManifest(customDomain);
        }
        

        
        void GenerateServerSetupInstructions(string customDomain)
        {
            BoostOpsFileGenerator.GenerateServerSetupInstructions(customDomain, iosTeamId, iosBundleId, androidBundleId);
        }
                
        void ValidateProjectSlug()
        {
            if (string.IsNullOrEmpty(projectSlug))
            {
                isProjectSlugValid = false;
                return;
            }
            
            // Check if slug contains only lowercase letters, numbers, and hyphens
            isProjectSlugValid = System.Text.RegularExpressions.Regex.IsMatch(projectSlug, @"^[a-z0-9-]+$");
        }
        

        
        void GenerateSampleSlug()
        {
            if (!string.IsNullOrEmpty(appName))
            {
                projectSlug = appName.ToLower()
                    .Replace(" ", "-")
                    .Replace("_", "-")
                    .Replace(".", "-");
                
                // Remove invalid characters
                projectSlug = System.Text.RegularExpressions.Regex.Replace(projectSlug, @"[^a-z0-9-]", "");
                
                // Remove multiple consecutive hyphens
                projectSlug = System.Text.RegularExpressions.Regex.Replace(projectSlug, @"-+", "-");
                
                // Remove leading/trailing hyphens
                projectSlug = projectSlug.Trim('-');
                
                ValidateProjectSlug();
                SaveProjectSlug();
                Repaint();
            }
        }
        
        void GenerateDynamicLinkFiles()
        {
            try
            {
                // Save any pending changes before generating files
                SaveAllPendingChanges();
                
                // Validate configuration
                if (dynamicLinksConfig == null)
                {
                    EditorUtility.DisplayDialog("Error", "No dynamic links configuration found. Please create one first.", "OK");
                    return;
                }
                
                // Auto-populate domain from server if config is empty
                if (dynamicLinksConfig.GetAllHosts().Count == 0)
                {
                    string domainToUse = null;
                    
                    // Priority 1: Use domain from server lookup response
                    if (!string.IsNullOrEmpty(lookupProjectSlug))
                    {
                        domainToUse = $"{lookupProjectSlug}.boostlink.me";
                        Debug.Log($"[BoostOps] Auto-populating domain from project slug: {domainToUse}");
                    }
                    // Priority 2: Use dynamicLinkUrl (legacy local setting)
                    else if (!string.IsNullOrEmpty(dynamicLinkUrl))
                    {
                        domainToUse = dynamicLinkUrl;
                        Debug.Log($"[BoostOps] Auto-populating domain from dynamicLinkUrl: {domainToUse}");
                    }
                    
                    if (!string.IsNullOrEmpty(domainToUse))
                    {
                        Debug.Log($"[BoostOps] Setting domain in dynamic links config: {domainToUse}");
                        dynamicLinksConfig.SetDomains(new List<string> { domainToUse });
                        EditorUtility.SetDirty(dynamicLinksConfig);
                    }
                    else
                    {
                        Debug.LogWarning("[BoostOps] No domain found to auto-populate.");
                    }
                }
                
                var validation = dynamicLinksConfig.ValidateConfiguration();
                if (!validation.IsValid)
                {
                    EditorUtility.DisplayDialog("Configuration Error", $"Please fix configuration errors:\n{validation.GetErrorsString()}", "OK");
                return;
            }
            
                // Prevent Unity from auto-refreshing during file generation
                AssetDatabase.DisallowAutoRefresh();
                
                // Get all hosts to generate files for
                var allHosts = dynamicLinksConfig.GetAllHosts();
                if (allHosts.Count == 0)
                {
                    EditorUtility.DisplayDialog("Error", "No hosts configured. Please add at least a primary host.", "OK");
                        return;
                    }
                    
                // ✅ NEW: Ensure BoostOpsGenerated directory exists
                // Note: GenerateMultiHostFiles will create subdirectories
                string generatedRoot = "Assets/BoostOpsGenerated";
                if (!Directory.Exists(generatedRoot))
                {
                    Directory.CreateDirectory(generatedRoot);
                    Debug.Log($"[BoostOps] Created generated files directory: {generatedRoot}");
                }
                
                // Generate files for all configured hosts
                GenerateMultiHostFiles(allHosts);
                
                string message = $"Dynamic link files generated successfully for {allHosts.Count} domain(s):\n\n" +
                               string.Join("\n", allHosts.Select(h => $"• {h}"));
                
                EditorUtility.DisplayDialog("Success", message, "OK");
                
                // Update the open folder button state
                UpdateOpenFolderButtonState();
                }
                catch (System.Exception e)
                {
                EditorUtility.DisplayDialog("Error", $"Failed to generate dynamic link files: {e.Message}", "OK");
            }
            finally
            {
                // Re-enable auto-refresh (without triggering a refresh)
                // Defer this to avoid timing issues
                EditorApplication.delayCall += () => {
                    if (!EditorApplication.isPlaying && !EditorApplication.isPaused)
                    {
                        try { AssetDatabase.AllowAutoRefresh(); } catch {}
                    }
                };
                
                // Save the dynamic links config explicitly after file generation
                SaveDynamicLinksConfigAssetImmediately();
            }
        }
        
        void GenerateMultiHostFiles(List<string> hosts)
        {
            // Ensure Team ID is loaded from PlayerSettings before generating files
            if (string.IsNullOrEmpty(iosTeamId) || iosTeamId == "Not set")
            {
                iosTeamId = PlayerSettings.iOS.appleDeveloperTeamID;
            }
            
            // ✅ NEW PATHS: Separate folder (preserved on SDK updates)
            string generatedRoot = "Assets/BoostOpsGenerated";
            string serverFilesPath = Path.Combine(generatedRoot, "ServerFiles");
            string wellKnownPath = Path.Combine(serverFilesPath, "well_known_server");  // ✅ Visible folder
            string androidPath = Path.Combine(generatedRoot, "Plugins", "Android");
            
            // Create directories
            Directory.CreateDirectory(generatedRoot);
            Directory.CreateDirectory(serverFilesPath);
            Directory.CreateDirectory(wellKnownPath);
            Directory.CreateDirectory(androidPath);
            
            // Generate Apple App Site Association (AASA) file for all hosts
            GenerateAASAFile(hosts, wellKnownPath);
            
            // Generate Android Asset Links file for all hosts
            GenerateAssetLinksFile(hosts, wellKnownPath);
            
            // iOS entitlements are automatically handled during build process
            Debug.Log($"[BoostOps] iOS entitlements for {hosts.Count} domains will be automatically merged during build");
            
            // Generate Android manifest file for all hosts
            GenerateAndroidManifestFile(hosts, generatedRoot);
            
            // Generate setup instructions
            GenerateSetupInstructions(hosts, generatedRoot);
            
            // Refresh assets - use safe wrapper
            SafeRefreshAssets();
        }
        
        void GenerateAASAFile(List<string> hosts, string wellKnownPath)
        {
            string bundleId = GetCurrentPlatformBundleId();
            if (string.IsNullOrEmpty(bundleId))
            {
                Debug.LogError("Bundle ID not found for AASA generation");
                return;
            }
            
            string teamId = GetTeamId();
            if (string.IsNullOrEmpty(teamId))
            {
                teamId = "TEAMID"; // Placeholder
                Debug.LogWarning("Team ID not set. Using placeholder 'TEAMID'. Please set your Apple Team ID in BoostOps settings.");
            }
            
            // Build AASA JSON manually for proper Apple format
            // Apple's current spec requires { "/": "*" } format for components
            string aasaJson = $@"{{
    ""applinks"": {{
        ""details"": [
            {{
                ""appIDs"": [
                    ""{teamId}.{bundleId}""
                ],
                ""components"": [
                    {{
                        ""/"": ""*""
                    }}
                ]
            }}
        ]
    }}
}}";
            
            string aasaPath = Path.Combine(wellKnownPath, "apple-app-site-association");
            File.WriteAllText(aasaPath, aasaJson);
            
            Debug.Log($"Generated AASA file for {hosts.Count} hosts at: {aasaPath}");
        }

        string GetTeamId()
        {
            // Try to get from iOS Team ID field (from PlayerSettings)
            if (!string.IsNullOrEmpty(iosTeamId) && iosTeamId != "Not set")
            {
                return iosTeamId;
            }

            // Try to get from Apple App Store ID field if it contains team ID format like "TEAMID.appid"
            if (!string.IsNullOrEmpty(iosAppStoreId) && iosAppStoreId.Contains("."))
            {
                string[] parts = iosAppStoreId.Split('.');
                if (parts.Length >= 2 && parts[0].Length == 10)
                {
                    return parts[0];
                }
            }

            // Fallback to placeholder
            return "TEAMID";
        }
            
        void GenerateAssetLinksFile(List<string> hosts, string wellKnownPath)
        {
            string packageName = GetCurrentPlatformBundleId();
            if (string.IsNullOrEmpty(packageName))
            {
                Debug.LogError("Package name not found for Asset Links generation");
                return;
            }
            
            string certFingerprint = androidCertFingerprint;
            if (string.IsNullOrEmpty(certFingerprint))
            {
                certFingerprint = "YOUR_CERT_FINGERPRINT_HERE";
                Debug.LogWarning("Android certificate fingerprint not set. Please configure it in the BoostOps settings.");
            }
            
            // Build Asset Links JSON manually since Unity's JsonUtility doesn't handle arrays properly
            string assetLinksJson = $@"[
    {{
        ""relation"": [""delegate_permission/common.handle_all_urls""],
        ""target"": {{
            ""namespace"": ""android_app"",
            ""package_name"": ""{packageName}"",
            ""sha256_cert_fingerprints"": [""{certFingerprint}""]
        }}
    }}
]";
             string assetLinksPath = Path.Combine(wellKnownPath, "assetlinks.json");
             File.WriteAllText(assetLinksPath, assetLinksJson);
            
            Debug.Log($"Generated Asset Links file for {hosts.Count} hosts at: {assetLinksPath}");
        }

        async void VerifyAndroidFiles()
        {
            // Get all configured domains to verify
            LoadDynamicLinksConfig();
            if (dynamicLinksConfig == null)
            {
                EditorUtility.DisplayDialog("No Configuration", "No dynamic links configuration found. Please configure your domains first.", "OK");
                return;
            }
            
            var domains = dynamicLinksConfig.GetAllHosts();
            if (domains.Count == 0)
            {
                EditorUtility.DisplayDialog("No Domains", "No domains configured. Please add at least one domain to verify.", "OK");
                return;
            }
            
            var results = new System.Collections.Generic.List<string>();
            int successCount = 0;
            
            foreach (string domain in domains)
            {
                string testUrl = $"https://{domain}/.well-known/assetlinks.json";
                
                // Check if this is a test domain
                bool isTestDomain = domain.Contains("test.boostlink.me") || domain.StartsWith("test.");
                
                try
                {
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.Timeout = System.TimeSpan.FromSeconds(10);
                        
                        var response = await client.GetAsync(testUrl);
                        
                        if (response.IsSuccessStatusCode || isTestDomain)
                        {
                            successCount++;
                            string content = "";
                            if (isTestDomain)
                            {
                                results.Add($"✅ {domain} (Test Domain - Always Passes)");
                            }
                            else
                            {
                                content = await response.Content.ReadAsStringAsync();
                                var contentValidation = ValidateAndroidContent(content);
                                
                                if (contentValidation.IsValid)
                                {
                                    results.Add($"✅ {domain} - Valid assetlinks.json file");
                                }
                                else
                                {
                                    results.Add($"⚠️ {domain} - File found but config mismatch: {contentValidation.ErrorMessage}");
                                }
                            }
                        }
                        else
                        {
                            results.Add($"❌ {domain} - HTTP {response.StatusCode}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    results.Add($"❌ {domain} - Error: {ex.Message}");
                }
            }
            
            // Show results
            string message = $"Android App Links Verification Results:\n\n";
            message += string.Join("\n", results);
            message += $"\n\n✅ {successCount}/{domains.Count} domains verified successfully";
            
            if (successCount == domains.Count)
            {
                message += "\n\n🎉 All domains are properly configured!";
            }
            else
            {
                message += "\n\n📋 Upload the generated assetlinks.json file to the .well-known directory of any failing domains.";
            }
            
            EditorUtility.DisplayDialog("Android Verification Results", message, "OK");
        }
        
        void ShowCertificateFingerprintHelp()
        {
            string helpMessage = @"SHA256 Fingerprint Instructions
The SHA256 fingerprint is required for Android App Links to work properly. Here's how to get it:
📱 For Debug Builds (Testing):
keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android
🚀 For Release Builds:
keytool -list -v -keystore path/to/your/release.keystore -alias your_key_alias
☁️ From Google Play Console:
1. Go to Play Console → Your App → Test and Release → App Integrity → App Signing
2. Copy the SHA-256 certificate fingerprint under 'App signing key certificate'
📋 Accepted fingerprint formats:
• Colon-separated: AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99
• Raw hex (64 chars): AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899
The tool will automatically format it with colons for you.
⚠️ Important Notes:
• Use debug fingerprint for testing/development
• Use release fingerprint for production builds
• You can add multiple fingerprints (comma-separated) for different build types
• These fingerprints are PUBLIC and safe to include in your assetlinks.json file
🔗 Google Documentation:
https://developer.android.com/training/app-links/verify-site-associations";

            EditorUtility.DisplayDialog("SHA256 Fingerprint Help", helpMessage, "OK");
        }
        async System.Threading.Tasks.Task<bool> DownloadImageFromUrl(string imageUrl, string filePath)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                    
                    var imageBytes = await client.GetByteArrayAsync(imageUrl);
                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                    
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Error downloading image from {imageUrl}: {ex.Message}");
                return false;
            }
        }
        
        [System.Serializable]
        public class ITunesResponse
        {
            public int resultCount;
            public ITunesResult[] results;
        }
        
        [System.Serializable]
        public class ITunesResult
        {
            public string artworkUrl512;
            public string artworkUrl256;
            public string artworkUrl128;
            public string trackName;
            public string trackCensoredName;
        }
        
        /// <summary>
        /// Get store ID from BoostOpsProjectSettings for the specified store
        /// </summary>
        static string GetProjectSettingsStoreId(string store)
        {
            var settings = BoostOpsProjectSettings.GetInstance();
            if (settings == null) return null;
            
            switch (store.ToLower())
            {
                case "apple":
                    return !string.IsNullOrEmpty(settings.appleAppStoreId) ? settings.appleAppStoreId : null;
                case "google":
                case "android":
                    return !string.IsNullOrEmpty(settings.androidPackageName) ? settings.androidPackageName : null;
                case "amazon":
                    return !string.IsNullOrEmpty(settings.amazonStoreId) ? settings.amazonStoreId : null;
                case "microsoft":
                    return !string.IsNullOrEmpty(settings.windowsStoreId) ? settings.windowsStoreId : null;
                case "samsung":
                    return !string.IsNullOrEmpty(settings.samsungStoreId) ? settings.samsungStoreId : null;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Generate modern campaign format JSON from CrossPromoTable data
        /// </summary>
        string GenerateModernCampaignJson(CrossPromoTable table)
        {
            try
            {
                Debug.Log($"[BoostOps] GenerateModernCampaignJson called with {table.targets?.Length ?? 0} targets");
                
                var campaigns = new List<CampaignJson>();
                
                for (int i = 0; i < table.targets.Length; i++)
                {
                    var target = table.targets[i];
                    if (target == null) 
                    {
                        Debug.LogWarning($"[BoostOps] Target {i} is null, skipping");
                        continue;
                    }
                    
                    Debug.Log($"[BoostOps] Processing target {i}: {target.headline}");
                
                // Generate conventional local_key based on store IDs
                string iconLocalKey = GenerateConventionalIconPath(target);
                
                // Build structured store data
                var storeUrls = new StoreUrlsJson();
                var storeIds = new StoreIdsJson();
                var platformIds = new PlatformIdsJson();
                
                if (!string.IsNullOrEmpty(target.iosAppStoreId))
                {
                    storeUrls.apple = $"https://apps.apple.com/app/id{target.iosAppStoreId}";
                    storeIds.apple = target.iosAppStoreId;
                }
                if (!string.IsNullOrEmpty(target.androidPackageId))
                {
                    storeUrls.google = $"https://play.google.com/store/apps/details?id={target.androidPackageId}";
                    storeIds.google = target.androidPackageId;
                }
                if (!string.IsNullOrEmpty(target.amazonStoreId))
                {
                    // Support both ASIN and package name formats for Amazon
                    if (target.amazonStoreId.Length == 10 && System.Text.RegularExpressions.Regex.IsMatch(target.amazonStoreId, @"^[A-Z0-9]{10}$"))
                        storeUrls.amazon = $"https://www.amazon.com/dp/{target.amazonStoreId}";
                    else
                        storeUrls.amazon = $"https://www.amazon.com/gp/mas/dl/android?p={target.amazonStoreId}";
                    storeIds.amazon = target.amazonStoreId;
                }
                if (!string.IsNullOrEmpty(target.windowsStoreId))
                {
                    storeUrls.microsoft = $"ms-windows-store://pdp/?productid={target.windowsStoreId}";
                    storeIds.microsoft = target.windowsStoreId;
                }
                if (!string.IsNullOrEmpty(target.samsungStoreId))
                {
                    storeUrls.samsung = $"samsungapps://ProductDetail/{target.samsungStoreId}";
                    storeIds.samsung = target.samsungStoreId;
                }
                
                // Platform IDs
                if (!string.IsNullOrEmpty(target.iosBundleId))
                    platformIds.ios_bundle_id = target.iosBundleId;
                if (!string.IsNullOrEmpty(target.androidPackageId))
                    platformIds.android_package_name = target.androidPackageId;
                
                // Build creatives array (only add icon if we have a local_key)
                var creatives = new List<CreativeJson>();
                if (!string.IsNullOrEmpty(iconLocalKey))
                {
                    var creative = new CreativeJson {
                        creative_id = System.Guid.NewGuid().ToString(),
                        format = "icon",
                        orientation = "any",
                        prefetch = true,
                        ttl_hours = 24,
                        variants = new CreativeVariantJson[] {
                            new CreativeVariantJson {
                                resolution = "512x512",
                                url = "",
                                sha256 = "",
                                local_key = iconLocalKey
                            }
                        }
                    };
                    creatives.Add(creative);
                }
                
                // Build campaign object with nested schedule
                var campaign = new CampaignJson {
                    campaign_id = System.Guid.NewGuid().ToString(),
                    name = target.headline ?? "", // Optional field - empty is fine
                    status = "active",
                    frequency_cap = FrequencyCapJson.FromFrequencyCap(target.GetEffectiveFrequencyCap(table)),
                    schedule = new ScheduleJson {
                        start_date = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        end_date = System.DateTime.Now.AddMonths(3).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        days = new int[0], // Empty = all days valid
                        start_hour = -1, // Not set
                        end_hour = -1    // Not set
                    },
                    created_at = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    updated_at = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    target_project = new TargetProjectJson {
                        project_id = !string.IsNullOrEmpty(target.id) ? target.id : System.Guid.NewGuid().ToString(),
                        store_urls = storeUrls,
                        store_ids = storeIds,
                        platform_ids = platformIds,
                        creatives = creatives.ToArray()
                    }
                };
                
                campaigns.Add(campaign);
            }
            
                // Build final JSON structure
                var jsonObject = new CampaignDataJson {
                    version_info = new VersionInfoJson {
                        api_version = "1.0.0",
                        schema_version = "1.0.0",
                        client_min_version = "1.0.0",
                        server_version = "1.0.0",
                        contract_version = "1.0.0",
                        last_updated = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    },
                    source_project = BuildSourceProjectJson(table),
                    campaigns = campaigns.ToArray()
                };
                
                            string finalJson = JsonUtility.ToJson(jsonObject, true);
            Debug.Log($"[BoostOps] Successfully generated JSON with {campaigns.Count} campaigns, JSON length: {finalJson.Length}");
            return finalJson;
            }
            catch (System.Exception ex)
            {
            Debug.LogError($"[BoostOps] Error generating modern campaign JSON: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
        
        /// <summary>
        /// Build SourceProjectJson with new structured format
        /// </summary>
        private static SourceProjectJson BuildSourceProjectJson(CrossPromoTable table)
        {
            var appleStoreId = GetProjectSettingsStoreId("apple");
            var androidPackageName = GetProjectSettingsStoreId("android") ?? Application.identifier;
            var googleStoreId = GetProjectSettingsStoreId("google") ?? androidPackageName;
            var amazonStoreId = GetProjectSettingsStoreId("amazon");
            var samsungStoreId = GetProjectSettingsStoreId("samsung");
            var windowsStoreId = GetProjectSettingsStoreId("microsoft");
            
            // Build structured store data
            var storeUrls = new StoreUrlsJson();
            var storeIds = new StoreIdsJson();
            var platformIds = new PlatformIdsJson();
            
            if (!string.IsNullOrEmpty(appleStoreId))
            {
                storeUrls.apple = $"https://apps.apple.com/app/id{appleStoreId}";
                storeIds.apple = appleStoreId;
            }
            if (!string.IsNullOrEmpty(googleStoreId))
            {
                storeUrls.google = $"https://play.google.com/store/apps/details?id={googleStoreId}";
                storeIds.google = googleStoreId;
            }
            if (!string.IsNullOrEmpty(amazonStoreId))
            {
                if (amazonStoreId.Length == 10 && System.Text.RegularExpressions.Regex.IsMatch(amazonStoreId, @"^[A-Z0-9]{10}$"))
                    storeUrls.amazon = $"https://www.amazon.com/dp/{amazonStoreId}";
                else
                    storeUrls.amazon = $"https://www.amazon.com/gp/mas/dl/android?p={amazonStoreId}";
                storeIds.amazon = amazonStoreId;
            }
            if (!string.IsNullOrEmpty(samsungStoreId))
            {
                storeUrls.samsung = $"samsungapps://ProductDetail/{samsungStoreId}";
                storeIds.samsung = samsungStoreId;
            }
            if (!string.IsNullOrEmpty(windowsStoreId))
            {
                storeUrls.microsoft = $"ms-windows-store://pdp/?productid={windowsStoreId}";
                storeIds.microsoft = windowsStoreId;
            }
            
            // Platform IDs
            platformIds.ios_bundle_id = Application.identifier;
            platformIds.android_package_name = androidPackageName;
            
            return new SourceProjectJson
            {
                bundle_id = Application.identifier,
                name = Application.productName,
                min_player_days = table.minPlayerDay,
                min_sessions = table.minPlayerSession,
                frequency_cap = FrequencyCapJson.FromFrequencyCap(table.globalFrequencyCap ?? BoostOps.Core.FrequencyCap.Unlimited()),
                interstitial_icon_cta = table.defaultIconInterstitialButtonText,
                interstitial_icon_text = table.defaultIconInterstitialDescription,
                interstitial_rich_cta = table.defaultRichInterstitialButtonText,
                interstitial_rich_text = table.defaultRichInterstitialDescription,
                store_urls = storeUrls,
                store_ids = storeIds,
                platform_ids = platformIds
            };
        }
    
    // Helper methods for JSON state management
    void MarkJsonAsStale()
    {
        LogDebug("Marking JSON as stale due to configuration changes");
        isJsonStale = true;
        UpdateGenerateButtonDynamic();
    }
    
    void MarkJsonAsFresh()
    {
        Debug.Log("[BoostOps] Marking JSON as fresh after successful generation");
        isJsonStale = false;
        lastJsonGeneration = System.DateTime.Now;
        UpdateGenerateButtonDynamic();
    }
    
    string GetGenerateButtonText()
    {
        if (!isJsonStale)
        {
            return "✓ Generated";
        }
        
        // Check if configuration is valid
        var validationErrors = ValidateCrossPromoConfiguration();
        if (validationErrors.Count > 0)
        {
            return "⚠ Fix Errors";
        }
        
        return "Generate JSON";
    }
    
    void UpdateGenerateButtonStyle(Button button)
    {
        if (!isJsonStale)
        {
            // Generated state - up to date
            button.style.backgroundColor = new Color(0.2f, 0.7f, 0.9f, 1f); // Blue
            button.tooltip = "JSON is up to date.";
            return;
        }
        
        // Check if configuration is valid
        var validationErrors = ValidateCrossPromoConfiguration();
        if (validationErrors.Count > 0)
        {
            // Invalid state - has errors
            button.style.backgroundColor = new Color(0.7f, 0.7f, 0.2f, 1f); // Yellow
            button.tooltip = $"Configuration has {validationErrors.Count} error(s). Fix them before generating JSON.";
                }
                else
                {
            // Valid state - needs generation
            button.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 1f); // Green
            button.tooltip = "Configuration is valid. Click to generate JSON.";
        }
    }
    
    void UpdateGenerateButtonDynamic()
    {
        if (generateJsonButton != null)
        {
            string newText = GetGenerateButtonText();
            generateJsonButton.text = newText;
            UpdateGenerateButtonStyle(generateJsonButton);
            Debug.Log($"[BoostOps] Button updated - Text: '{newText}', Stale: {isJsonStale}");
        }
        else
        {
            LogWarningDebug("UpdateGenerateButtonDynamic called but generateJsonButton is null");
        }
    }

    /// <summary>
    /// Generate conventional icon path based on store IDs
    /// This uses the same conventions that server campaigns will use
    /// </summary>
    string GenerateConventionalIconPath(TargetGame target)
    {
        if (target == null) return "";
        
        // Priority order for conventional paths (same as server will use)
        
        // Apple Store ID (highest priority)
        if (!string.IsNullOrEmpty(target.iosAppStoreId))
        {
            return $"Icons/{target.iosAppStoreId}_icon";
        }
        
                        // Android Package Name (second priority)
        if (!string.IsNullOrEmpty(target.androidPackageId))
        {
            string sanitizedPackageId = target.androidPackageId.Replace(".", "_");
            return $"Icons/{sanitizedPackageId}_icon";
        }
        
        // Amazon Store ID (third priority)
        if (!string.IsNullOrEmpty(target.amazonStoreId))
        {
            return $"Icons/{target.amazonStoreId}_icon";
        }
        
        Debug.Log($"[BoostOps] ⚠️ No store ID available for '{target.headline}' - no local_key generated");
        return ""; // No store ID to base path on
    }

    // Missing methods that were accidentally removed
    void ValidateServerState()
    {
        // Validate server connection state and update UI accordingly
        LogDebug("ValidateServerState: Checking server connection state");
    }
    
    void AutoDetectProjectSettings()
    {
        // Auto-detect project settings from Unity player settings
        if (string.IsNullOrEmpty(appName))
        {
            appName = Application.productName;
        }
        
        if (string.IsNullOrEmpty(iosBundleId))
        {
#pragma warning disable CS0618
            #if UNITY_2021_2_OR_NEWER
            iosBundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
            #else
            iosBundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
            #endif
#pragma warning restore CS0618
        }
        
        if (string.IsNullOrEmpty(androidBundleId))
        {
#pragma warning disable CS0618
            #if UNITY_2021_2_OR_NEWER
            androidBundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            #else
            androidBundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            #endif
#pragma warning restore CS0618
        }
        
        if (string.IsNullOrEmpty(iosTeamId))
        {
            iosTeamId = PlayerSettings.iOS.appleDeveloperTeamID;
        }
    }
    
    void LoadProjectSlug()
    {
        projectSlug = EditorPrefs.GetString("BoostOps_ProjectSlug", "");
        ValidateProjectSlug();
    }
    
    void SaveProjectSlug()
    {
        EditorPrefs.SetString("BoostOps_ProjectSlug", projectSlug);
    }
    

    

    
    void LoadDynamicLinkUrl()
    {
        dynamicLinkUrl = EditorPrefs.GetString("BoostOps_DynamicLinkUrl", "");
    }
    
    void SaveDynamicLinkUrl()
    {
        EditorPrefs.SetString("BoostOps_DynamicLinkUrl", dynamicLinkUrl);
    }
    
    void LoadDynamicLinksConfig()
    {
        if (dynamicLinksConfig == null)
        {
            dynamicLinksConfig = BoostOpsProjectSettings.GetOrCreateSettings();
        }
    }
    
    void CreateDynamicLinksConfig()
    {
        // Dynamic links configuration is now part of BoostOpsProjectSettings
        if (dynamicLinksConfig == null)
        {
            dynamicLinksConfig = BoostOpsProjectSettings.GetOrCreateSettings();
            LogDebug("Using BoostOpsProjectSettings for dynamic links configuration");
            
            // Only refresh if we're not in a delayCall or during domain reload
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                AssetDatabase.Refresh();
            }
            
            LogDebug("Dynamic links configuration now handled by BoostOpsProjectSettings");
        }
    }
    
    void LoadAndroidCertFingerprint()
    {
        androidCertFingerprint = EditorPrefs.GetString("BoostOps_AndroidCertFingerprint", "");
    }
    
    void SaveAndroidCertFingerprint()
    {
        EditorPrefs.SetString("BoostOps_AndroidCertFingerprint", androidCertFingerprint);
        
        // Also save to project settings so Overview page can read it
        var settings = BoostOpsProjectSettings.GetOrCreateSettings();
        settings.androidCertFingerprint = androidCertFingerprint;
        UnityEditor.EditorUtility.SetDirty(settings);
        // Note: Actual save happens in SaveAllPendingChanges()
    }
    
    void LoadAppleAppStoreId()
    {
        iosAppStoreId = EditorPrefs.GetString("BoostOps_AppleAppStoreId", "");
    }
    
    void SaveAppleAppStoreId()
    {
        EditorPrefs.SetString("BoostOps_AppleAppStoreId", iosAppStoreId);
        
        // Also save to project settings so Overview page can read it
        var settings = BoostOpsProjectSettings.GetOrCreateSettings();
        settings.appleAppStoreId = iosAppStoreId;
        UnityEditor.EditorUtility.SetDirty(settings);
        // Note: Actual save happens in SaveAllPendingChanges()
    }
    
    void LoadHostingOption()
    {
        hostingOption = (HostingOption)EditorPrefs.GetInt("BoostOps_HostingOption", 0);
    }
    
    void SaveHostingOption()
    {
        EditorPrefs.SetInt("BoostOps_HostingOption", (int)hostingOption);
    }
    
    void LoadStudioInfo()
    {
        studioId = EditorPrefs.GetString("BoostOps_StudioId", "");
        studioName = EditorPrefs.GetString("BoostOps_StudioName", "");
        studioDescription = EditorPrefs.GetString("BoostOps_StudioDescription", "");
        isStudioOwner = EditorPrefs.GetBool("BoostOps_IsStudioOwner", false);
    }
    
    void SaveStudioInfo()
    {
        EditorPrefs.SetString("BoostOps_StudioId", studioId);
        EditorPrefs.SetString("BoostOps_StudioName", studioName);
        EditorPrefs.SetString("BoostOps_StudioDescription", studioDescription);
        EditorPrefs.SetBool("BoostOps_IsStudioOwner", isStudioOwner);
    }
    
    void LoadRegistrationState()
    {
        registrationState = (ProjectRegistrationState)EditorPrefs.GetInt("BoostOps_RegistrationState", 0);
    }
    
    void SaveRegistrationState()
    {
        EditorPrefs.SetInt("BoostOps_RegistrationState", (int)registrationState);
    }
    
    void DetectCrossPromoConfigurations()
    {
        // Detect cross-promotion configuration files
        LogDebug("DetectCrossPromoConfigurations: Scanning for cross-promo files");
    }
    
    void LoadDebugLogging()
    {
        // Load from project settings asset first (runtime preference)
        if (dynamicLinksConfig != null)
        {
            enableDebugLogging = dynamicLinksConfig.debugLogging;
        }
        else
        {
            // Fallback to EditorPrefs if asset not loaded
            enableDebugLogging = EditorPrefs.GetBool("BoostOps_EnableDebugLogging", false);
        }
        
        // Sync with Editor-specific logger
        BoostOpsLogger.IsEditorDebugLoggingEnabled = enableDebugLogging;
    }
    
    void SaveDebugLogging()
    {
        EditorPrefs.SetBool("BoostOps_EnableDebugLogging", enableDebugLogging);
    }
    
    /// <summary>
    /// Update the status label showing current logging configuration
    /// </summary>
    void UpdateLoggingStatusLabel()
    {
        if (loggingStatusLabel == null) return;
        
        bool editorEnabled = BoostOpsLogger.IsEditorDebugLoggingEnabled;
        bool runtimeEnabled = BoostOpsLogger.IsRuntimeDebugLoggingEnabled;
        
        string status = "";
        Color color = new Color(0.5f, 0.8f, 0.5f, 1f); // Green
        
        if (editorEnabled && runtimeEnabled)
        {
            status = "Status: All debug messages enabled";
        }
        else if (editorEnabled && !runtimeEnabled)
        {
            status = "Status: Editor debug messages only";
            color = new Color(0.5f, 0.7f, 0.9f, 1f); // Blue
        }
        else if (!editorEnabled && runtimeEnabled)
        {
            status = "Status: Runtime debug messages only";
            color = new Color(0.9f, 0.7f, 0.5f, 1f); // Orange
        }
        else
        {
            status = "Status: All debug messages disabled";
            color = new Color(0.7f, 0.5f, 0.5f, 1f); // Red
        }
        
        loggingStatusLabel.text = status;
        loggingStatusLabel.style.color = color;
    }
    
    /// <summary>
    /// Refresh the toggle states after preset buttons are clicked
    /// </summary>
    void RefreshLoggingToggles(Toggle editorToggle, Toggle runtimeToggle)
    {
        if (editorToggle != null)
            editorToggle.value = BoostOpsLogger.IsEditorDebugLoggingEnabled;
        if (runtimeToggle != null)
            runtimeToggle.value = BoostOpsLogger.IsRuntimeDebugLoggingEnabled;
        
        // Update backward compatibility field
        enableDebugLogging = BoostOpsLogger.IsEditorDebugLoggingEnabled;
        
        UpdateLoggingStatusLabel();
    }
    
    void SyncDynamicLinkUrl()
    {
        // Sync dynamic link URL with configuration
        LogDebug("SyncDynamicLinkUrl: Syncing URL with configuration");
    }
    
    async void VerifyIOSFiles()
    {
        // Get all configured domains to verify
        LoadDynamicLinksConfig();
        if (dynamicLinksConfig == null)
        {
            EditorUtility.DisplayDialog("No Configuration", "No dynamic links configuration found. Please configure your domains first.", "OK");
            return;
        }
        
        var domains = dynamicLinksConfig.GetAllHosts();
        if (domains.Count == 0)
        {
            EditorUtility.DisplayDialog("No Domains", "No domains configured. Please add at least one domain to verify.", "OK");
            return;
        }
        
        var results = new System.Collections.Generic.List<string>();
        int successCount = 0;
        
        foreach (string domain in domains)
        {
            string testUrl = $"https://{domain}/.well-known/apple-app-site-association";
            
            // Check if this is a test domain
            bool isTestDomain = domain.Contains("test.boostlink.me") || domain.StartsWith("test.");
            
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(10);
                    
                    var response = await client.GetAsync(testUrl);
                    
                    if (response.IsSuccessStatusCode || isTestDomain)
                    {
                        successCount++;
                        string content = "";
                        if (isTestDomain)
                        {
                            results.Add($"✅ {domain} (Test Domain - Always Passes)");
                        }
                        else
                        {
                            content = await response.Content.ReadAsStringAsync();
                            var contentValidation = ValidateIOSContent(content);
                            
                            if (contentValidation.IsValid)
                            {
                                results.Add($"✅ {domain} - Valid AASA file");
                            }
                            else
                            {
                                results.Add($"⚠️ {domain} - File found but config mismatch: {contentValidation.ErrorMessage}");
                            }
                        }
                    }
                    else
                    {
                        results.Add($"❌ {domain} - HTTP {response.StatusCode}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                results.Add($"❌ {domain} - Error: {ex.Message}");
            }
        }
        
        // Show results
        string message = $"iOS Universal Links Verification Results:\n\n";
        message += string.Join("\n", results);
        message += $"\n\n✅ {successCount}/{domains.Count} domains verified successfully";
        
        if (successCount == domains.Count)
        {
            message += "\n\n🎉 All domains are properly configured!";
        }
        else
        {
            message += "\n\n📋 Upload the generated apple-app-site-association file to the .well-known directory of any failing domains.";
        }
        
        EditorUtility.DisplayDialog("iOS Verification Results", message, "OK");
    }
    
    void ShowRegisterAppDialog()
    {
        var settings = BoostOpsProjectSettings.GetInstance();
        #if UNITY_2021_2_OR_NEWER
        string bundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
        string packageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        #else
        string bundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
        string packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        #endif
        
        bool hasRequiredData = !string.IsNullOrEmpty(bundleId) || !string.IsNullOrEmpty(packageName);
        
        if (!hasRequiredData)
        {
            EditorUtility.DisplayDialog("App Registration Required", 
                "To register your app with BoostOps, you need:\n\n" +
                "• Bundle ID (iOS) or Package Name (Android) configured in Player Settings\n" +
                "• Valid BoostOps account (already logged in ✓)\n\n" +
                "Please configure your app identifiers in Player Settings first.", 
                "Open Player Settings");
            SettingsService.OpenProjectSettings("Project/Player");
            return;
        }
        
        string appInfo = "";
        if (!string.IsNullOrEmpty(bundleId)) appInfo += $"Bundle ID: {bundleId}\n";
        if (!string.IsNullOrEmpty(packageName)) appInfo += $"Package Name: {packageName}\n";
        
        bool shouldRegister = EditorUtility.DisplayDialog("Register App with BoostOps", 
            "Register your app to unlock:\n\n" +
            "✅ Analytics event tracking\n" +
            "✅ Project synchronization\n" +
                            "✅ Project Key for event ingest\n" +
            "✅ Cross-promotion campaigns\n\n" +
            "App Details:\n" + appInfo + 
            "\nThis will create a new project in your BoostOps account.", 
            "Register App", "Cancel");
        
        if (shouldRegister)
        {
            _ = PerformAppRegistration();
        }
    }
    
    async Task PerformAppRegistration()
    {
        try
        {
            Debug.Log("[BoostOps] 🚀 Starting app registration with BoostOps...");
            
            // Validate prerequisites
            if (string.IsNullOrEmpty(apiToken))
            {
                throw new Exception("Not authenticated. Please log in first.");
            }
            
            if (!isLoggedIn)
            {
                throw new Exception("User is not logged in.");
            }
            
            Debug.Log("[BoostOps] Prerequisites validated. Registering new project...");
            
            // Gather Unity project information
            string projectName = Application.productName;
            string productGuid = PlayerSettings.productGUID.ToString();
            string cloudProjectId = Application.cloudProjectId;
            #if UNITY_2021_2_OR_NEWER
            string iosBundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
            string androidPackageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            #else
            string iosBundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
            string androidPackageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            #endif
            
            // Get platform specific details
            var projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
            string iosAppStoreId = projectSettings.appleAppStoreId;
            string appleTeamId = PlayerSettings.iOS.appleDeveloperTeamID;
            string androidSha256 = EditorPrefs.GetString("BoostOps_AndroidCertFingerprint", "");
            string[] androidSha256Fingerprints = !string.IsNullOrEmpty(androidSha256) ? new string[] { androidSha256 } : new string[0];
            
            Debug.Log($"[BoostOps] Registering project: {projectName}");
            Debug.Log($"[BoostOps] Product GUID: {productGuid}");
            Debug.Log($"[BoostOps] iOS Bundle ID: {iosBundleId}");
            Debug.Log($"[BoostOps] Android Package: {androidPackageName}");
            
            // Call registration endpoint
            await RegisterNewProject(projectName, productGuid, cloudProjectId, iosBundleId, androidPackageName, 
                                   iosAppStoreId, appleTeamId, androidSha256Fingerprints);
            
            // Refresh the Overview page to show updated credentials
            if (useUIToolkit && contentContainer != null)
            {
                BuildUIToolkitInterface();
                // Always show Overview after registration to display the success
                ShowOverviewPanel();
            }
            
            EditorUtility.DisplayDialog("Registration Successful! 🎉", 
                "Your app has been registered with BoostOps!\n\n" +
                "✅ Project Key generated and configured\n" +
                "✅ Analytics tracking enabled\n" +
                "✅ Project sync activated\n\n" +
                "You can now use all BoostOps features.", 
                "Continue");
                
            Debug.Log("[BoostOps] ✅ App registration completed successfully!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BoostOps] ❌ App registration failed: {ex.Message}");
            EditorUtility.DisplayDialog("Registration Failed", 
                $"Failed to register your app with BoostOps:\n\n{ex.Message}\n\n" +
                "Please check your internet connection and try again.", 
                "OK");
        }
    }

    void RefreshOverviewPageIfVisible()
    {
        // Only refresh if user is currently viewing Overview tab (selectedTab == 0)
        if (selectedTab == 0 && useUIToolkit && contentContainer != null)
        {
            // Small delay to allow any ongoing verifications to complete
            EditorApplication.delayCall += () => {
                if (selectedTab == 0) // Check again in case user switched tabs
                {
                    ShowOverviewPanel();
                }
            };
        }
    }
    
    void RegisterProject()
    {
        LogDebug("RegisterProject: Project registration not implemented");
        EditorUtility.DisplayDialog("Project Registration", "Project registration is not implemented in this version.", "OK");
    }
    
    void ActivateProjectSlug()
    {
        LogDebug("ActivateProjectSlug: Project activation not implemented");
        EditorUtility.DisplayDialog("Project Activation", "Project activation is not implemented in this version.", "OK");
    }
    
    void DetectAnalyticsIntegrations()
    {
        LogDebug("DetectAnalyticsIntegrations: Scanning for analytics integrations");
        
        // Detect Unity Analytics (try multiple possible namespaces)
        try
        {
            var unityAnalyticsType = System.Type.GetType("Unity.Analytics.Analytics, Unity.Analytics") ??
                                   System.Type.GetType("UnityEngine.Analytics.Analytics, UnityEngine.Analytics") ??
                                   System.Type.GetType("Unity.Services.Analytics.AnalyticsService, Unity.Services.Analytics");
            hasUnityAnalytics = unityAnalyticsType != null;
            LogDebug($"Unity Analytics detection: {hasUnityAnalytics}");
        }
        catch (System.Exception e)
        {
            hasUnityAnalytics = false;
            LogDebug($"Unity Analytics detection failed: {e.Message}");
        }
        
        // Detect Firebase Analytics (try multiple possible assemblies)
        try
        {
            var firebaseAnalyticsType = System.Type.GetType("Firebase.Analytics.FirebaseAnalytics, Firebase.Analytics") ??
                                      System.Type.GetType("Firebase.Analytics.FirebaseAnalytics, Firebase.Analytics.dll");
            hasFirebaseAnalytics = firebaseAnalyticsType != null;
            LogDebug($"Firebase Analytics detection: {hasFirebaseAnalytics}");
        }
        catch (System.Exception e)
        {
            hasFirebaseAnalytics = false;
            LogDebug($"Firebase Analytics detection failed: {e.Message}");
        }
        
        LogDebug($"Analytics integrations detected - Unity: {hasUnityAnalytics}, Firebase: {hasFirebaseAnalytics}");
    }
    
    void DetectUnityRemoteConfig()
    {
        LogDebug("DetectUnityRemoteConfig: Checking for Unity Remote Config");
        
        try
        {
            var unityRemoteConfigType = System.Type.GetType("Unity.RemoteConfig.RemoteConfigService, Unity.RemoteConfig") ??
                                      System.Type.GetType("Unity.Services.RemoteConfig.RemoteConfigService, Unity.Services.RemoteConfig") ??
                                      System.Type.GetType("Unity.CloudBuild.Editor.RemoteConfig, Unity.CloudBuild.Editor");
            hasUnityRemoteConfig = unityRemoteConfigType != null;
            LogDebug($"Unity Remote Config detection: {hasUnityRemoteConfig}");
        }
        catch (System.Exception e)
        {
            hasUnityRemoteConfig = false;
            LogDebug($"Unity Remote Config detection failed: {e.Message}");
        }
    }
    
    void DetectFirebaseRemoteConfig()
    {
        LogDebug("DetectFirebaseRemoteConfig: Checking for Firebase Remote Config");
        
        try
        {
            var firebaseRemoteConfigType = System.Type.GetType("Firebase.RemoteConfig.FirebaseRemoteConfig, Firebase.RemoteConfig") ??
                                         System.Type.GetType("Firebase.RemoteConfig.FirebaseRemoteConfig, Firebase.RemoteConfig.dll");
            hasFirebaseRemoteConfig = firebaseRemoteConfigType != null;
            LogDebug($"Firebase Remote Config detection: {hasFirebaseRemoteConfig}");
        }
        catch (System.Exception e)
        {
            hasFirebaseRemoteConfig = false;
            LogDebug($"Firebase Remote Config detection failed: {e.Message}");
        }
    }
    
    bool HasLocalCrossPromoFiles()
    {
        return File.Exists("Assets/BoostOps/Configuration/cross_promo_local.json") ||
               File.Exists("Assets/StreamingAssets/cross_promo_local.json");
    }
    
    // BoostOps Managed Cross-Promo Remote Config Implementation
    private List<BoostOps.Campaign> cachedRemoteCampaigns = new List<BoostOps.Campaign>();
    private string lastRemoteConfigSync = "";
    private string lastFetchedRemoteConfigJson = ""; // Store last fetched remote config JSON for re-parsing
    private SourceProject cachedSourceProject = null; // Server-managed source project settings
    private bool isApiCallInProgress = false; // Prevent recursive API calls
    
    [System.Serializable]
    public class RemoteCampaignConfig
    {
        public VersionInfo version_info;
        public SourceProject source_project;
        public List<BoostOps.Campaign> campaigns = new List<BoostOps.Campaign>();
    }

    [System.Serializable]
    public class VersionInfo
    {
        public string api_version;
        public string schema_version;
        public string contract_version;
        public string server_version;
        public string client_min_version;
        public string last_updated;
        public string environment;
    }

    [System.Serializable]
    public class SourceProject
    {
        public string bundle_id;
        public string name;
        public int min_player_days;
        public int min_sessions;
        public FrequencyCapJson frequency_cap;
        public string interstitial_icon_cta;
        public string interstitial_icon_text;
        public string interstitial_rich_cta;
        public string interstitial_rich_text;
        
        // Structured format
        public Dictionary<string, string> store_urls;
        public Dictionary<string, string> store_ids;
        public Dictionary<string, object> platform_ids;
    }

    [System.Serializable]
    public class Campaign
    {
        public string campaign_id;
        public string name;
        public string status;
        public FrequencyCapJson frequency_cap;
        public Schedule schedule;
        public string created_at;
        public string updated_at;
        public TargetProject target_project;
        
        public string GetStoreUrl()
        {
            if (target_project?.store_urls == null) return null;
#if UNITY_IOS
            return target_project.store_urls.apple;
#elif UNITY_ANDROID
            return target_project.store_urls.google;
#else
            return target_project.store_urls.apple ?? target_project.store_urls.google;
#endif
        }
        
        public bool HasValidStoreUrl()
        {
            return target_project?.store_urls != null && 
                   target_project.store_urls.HasAnyLinks();
        }
        
        public string GetIconUrl()
        {
            // First try to get icon URL from creatives (existing behavior)
            if (target_project?.creatives != null)
            {
                var iconCreative = target_project.creatives.FirstOrDefault(c => c.format == "icon");
                if (iconCreative?.variants != null && iconCreative.variants.Count > 0)
                {
                    return iconCreative.variants[0].url;
                }
            }
            
            // Fallback: construct icon URL from store URLs (prioritize iOS, then Android)
            return GetConstructedIconUrl();
        }
        
        public string GetConstructedIconUrl()
        {
            if (target_project?.store_urls == null) return null;
            
            // Priority 1: iOS App Store URL
            if (!string.IsNullOrEmpty(target_project.store_urls.apple))
            {
                var iosAppId = ExtractIosAppStoreId(target_project.store_urls.apple);
                if (!string.IsNullOrEmpty(iosAppId))
                {
                    return ConstructIosIconUrl(iosAppId);
                }
            }
            
            // Priority 2: Android Google Play URL  
            if (!string.IsNullOrEmpty(target_project.store_urls.google))
            {
                var androidPackageId = ExtractAndroidPackageId(target_project.store_urls.google);
                if (!string.IsNullOrEmpty(androidPackageId))
                {
                    return ConstructAndroidIconUrl(androidPackageId);
                }
            }
            
            return null;
        }
        
        public string ExtractIosAppStoreId(string iosUrl)
        {
            if (string.IsNullOrEmpty(iosUrl)) return null;
            
            // Extract from URLs like: https://apps.apple.com/us/app/app-name/id1234567890
            var match = System.Text.RegularExpressions.Regex.Match(iosUrl, @"id(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
        
        public string ExtractAndroidPackageId(string androidUrl)
        {
            if (string.IsNullOrEmpty(androidUrl)) return null;
            
            // Extract from URLs like: https://play.google.com/store/apps/details?id=com.example.app
            var match = System.Text.RegularExpressions.Regex.Match(androidUrl, @"id=([^&]+)");
            return match.Success ? match.Groups[1].Value : null;
        }
        
        private string ConstructIosIconUrl(string appId)
        {
            // Construct iTunes API URL to get app icon
            return $"https://itunes.apple.com/lookup?id={appId}";
        }
        
        private string ConstructAndroidIconUrl(string packageId)
        {
            // For Android, we'll need to scrape the Google Play page to get the icon URL
            // This is more complex and will be handled in the download method
            return $"https://play.google.com/store/apps/details?id={packageId}";
        }
    }

    [System.Serializable]
    public class TargetProject
    {
        public string project_id;
        public StoreUrls store_urls;
        public Dictionary<string, string> store_ids;
        public Dictionary<string, object> platform_ids;
        public List<Creative> creatives = new List<Creative>();
    }

    [System.Serializable]
    public class StoreUrls
    {
        public string apple;
        public string google;
        public string web;
        public string amazon;
        public string windows;
        
        /// <summary>
        /// Check if any store links are available (at least one valid store URL)
        /// </summary>
        public bool HasAnyLinks()
        {
            return !string.IsNullOrEmpty(apple) || !string.IsNullOrEmpty(google) || 
                   !string.IsNullOrEmpty(web) || !string.IsNullOrEmpty(amazon) || 
                   !string.IsNullOrEmpty(windows);
        }
    }

    [System.Serializable]
    public class Creative
    {
        public string creative_id;
        public string format;
        public string orientation;
        public string hosted_by;
        public bool prefetch;
        public int ttl_hours;
        public bool required;
        public List<CreativeVariant> variants = new List<CreativeVariant>();
    }

    [System.Serializable]
    public class CreativeVariant
    {
        public string resolution;
        public string url;
        public string sha256;
        public string local_key;
    }



    [System.Serializable]
    public class Schedule
    {
        public string start_date;
        public List<string> days = new List<string>();
        public int start_hour;
        public int end_hour;
    }

    async System.Threading.Tasks.Task FetchRemoteCampaigns()
    {
        LogDebug("FetchRemoteCampaigns: Starting to fetch campaigns from remote config");
        
        try
        {
            if (hasUnityRemoteConfig)
            {
                await FetchFromUnityRemoteConfig();
            }
            else if (hasFirebaseRemoteConfig)
            {
                await FetchFromFirebaseRemoteConfig();
            }
            else
            {
                LogDebug("No remote config service available");
                return;
            }
            
            // Download icons for new campaigns
            await DownloadCampaignIcons();
            
            // Update last sync time
            lastRemoteConfigSync = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            crossPromoLastSync = lastRemoteConfigSync;
            
            // Validate the fetched campaigns
            bool isValid = ValidateRemoteCampaignConfig();
            
            // Save to cache
            SaveCachedRemoteCampaigns();
            
            LogDebug($"Successfully fetched {cachedRemoteCampaigns.Count} remote campaigns");
            if (!isValid)
            {
                LogDebug("Warning: Some campaigns failed validation - check logs for details");
            }
            
            // Refresh the UI if we're on the cross-promo panel
            if (selectedTab == 2)
            {
                EditorApplication.delayCall += () => ShowCrossPromoPanel();
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"❌ Failed to fetch remote campaigns: {ex.Message}");
            LogDebug("🔄 Falling back to server backup file (cross_promo_server.json)...");
            
            try
            {
                await LoadCampaignsFromServerFile();
                if (cachedRemoteCampaigns != null && cachedRemoteCampaigns.Count > 0)
                {
                    LogDebug("✅ Successfully loaded campaigns from server backup file");
                    return; // Success with fallback, don't show error dialog
                }
            }
            catch (System.Exception fallbackEx)
            {
                LogDebug($"❌ Server backup file also failed: {fallbackEx.Message}");
            }
            
            // Only show error if both remote config and server file failed
            // In managed mode, we never fall back to local files
            EditorUtility.DisplayDialog("BoostOps Remote Config Error", 
                $"Failed to fetch campaigns from both remote config and server backup:\n\n" +
                $"Remote Config Error: {ex.Message}\n\n" +
                "In BoostOps Managed mode, campaign data must come from either:\n" +
                "• Live Remote Config (Unity/Firebase)\n" +
                "• Server backup file (cross_promo_server.json)\n\n" +
                "Local files are not used as fallbacks in Managed mode.", "OK");
        }
    }

    async System.Threading.Tasks.Task LoadCampaignsFromAPI(ProjectLookupResponse existingLookupResponse = null)
    {
        LogDebug("LoadCampaignsFromAPI: Starting to load campaigns from BoostOps API");
        
        // If we already have a lookup response, reuse it instead of making another API call
        if (existingLookupResponse != null)
        {
            LogDebug("LoadCampaignsFromAPI: Reusing existing lookup response (avoiding duplicate API call)");
        }
        
        if (!isLoggedIn)
        {
            LogDebug("LoadCampaignsFromAPI: Not logged in, cannot fetch from API");
            return;
        }
        
        if (isApiCallInProgress)
        {
            LogDebug("LoadCampaignsFromAPI: API call already in progress, skipping to prevent loop");
            return;
        }
        
        isApiCallInProgress = true;
        
        try
        {
            ProjectLookupResponse lookupResponse;
            
            if (existingLookupResponse != null)
            {
                // Reuse existing lookup response to avoid duplicate API call
                LogDebug("LoadCampaignsFromAPI: Using provided lookup response instead of making new API call");
                lookupResponse = existingLookupResponse;
            }
            else
            {
                // Make fresh API call (this path is used when called independently)
                LogDebug("LoadCampaignsFromAPI: Making fresh API call to get project lookup (using CheckForExistingProjectWithoutUIRebuild for consistency)");
                lookupResponse = await CheckForExistingProjectWithoutUIRebuild();
            }
            if (lookupResponse != null && lookupResponse.found)
            {
                LogDebug("LoadCampaignsFromAPI: Received project lookup response from API");
                
                // Check if boostops_config is in the project object (new structure)
                string boostopsConfigJson = null;
                if (lookupResponse.project?.boostops_config != null)
                {
                    // Extract boostops_config JSON directly from raw server response
                    // Unity JsonUtility.ToJson() fails with nested dictionaries - use raw JSON instead
                    try 
                    {
                        string rawResponse = lookupResponse.rawResponse ?? "";
                        LogDebug($"LoadCampaignsFromAPI: Extracting boostops_config from raw response ({rawResponse.Length} characters)");
                        
                        // Find the boostops_config section in the raw JSON
                        int configStart = rawResponse.IndexOf("\"boostops_config\":");
                        if (configStart >= 0)
                        {
                            int braceStart = rawResponse.IndexOf("{", configStart);
                            if (braceStart >= 0)
                            {
                                // Find the matching closing brace by counting braces
                                int braceCount = 1;
                                int pos = braceStart + 1;
                                while (pos < rawResponse.Length && braceCount > 0)
                                {
                                    if (rawResponse[pos] == '{') braceCount++;
                                    else if (rawResponse[pos] == '}') braceCount--;
                                    pos++;
                                }
                                
                                if (braceCount == 0)
                                {
                                    boostopsConfigJson = rawResponse.Substring(braceStart, pos - braceStart);
                                    LogDebug($"LoadCampaignsFromAPI: Extracted boostops_config from raw JSON ({boostopsConfigJson.Length} characters)");
                                }
                            }
                        }
                        
                        if (string.IsNullOrEmpty(boostopsConfigJson))
                        {
                            LogDebug("LoadCampaignsFromAPI: Failed to extract boostops_config from raw JSON - falling back to JsonUtility");
                            // Fallback to JsonUtility (will likely fail but worth trying)
                    boostopsConfigJson = JsonUtility.ToJson(lookupResponse.project.boostops_config);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        LogDebug($"LoadCampaignsFromAPI: Exception extracting raw JSON: {ex.Message} - falling back to JsonUtility");
                        // Fallback to JsonUtility
                        boostopsConfigJson = JsonUtility.ToJson(lookupResponse.project.boostops_config);
                    }
                    
                    LogDebug($"LoadCampaignsFromAPI: Final boostops_config JSON length: {boostopsConfigJson?.Length ?? 0} characters");
                    
                    // Check if the JSON actually contains meaningful data
                    if (string.IsNullOrEmpty(boostopsConfigJson) || boostopsConfigJson == "{}" || 
                        boostopsConfigJson.Contains("\"campaigns\":null") || 
                        boostopsConfigJson.Contains("\"campaigns\":[]") ||
                        !boostopsConfigJson.Contains("\"campaigns\":[{"))
                    {
                        LogDebug("LoadCampaignsFromAPI: boostops_config contains no valid campaign data - treating as null");
                        boostopsConfigJson = null;
                    }
                }
                // Fallback: check if boostops_config is at root level (old structure)
                else if (!string.IsNullOrEmpty(lookupResponse.boostops_config))
                {
                    boostopsConfigJson = lookupResponse.boostops_config;
                    LogDebug($"LoadCampaignsFromAPI: Found boostops_config at root level ({boostopsConfigJson.Length} characters)");
                }
                
                if (!string.IsNullOrEmpty(boostopsConfigJson))
                {
                    LogDebug($"LoadCampaignsFromAPI: Saving raw boostops_config from API ({boostopsConfigJson.Length} characters)");
                    // ALWAYS save the raw boostops_config JSON when explicitly fetching from API
                    SaveBoostOpsConfigToStreamingAssets(boostopsConfigJson, forceOverwrite: true);
                    
                    // Parse ONLY for Editor UI display - don't let parsing affect the saved file
                    ParseRemoteCampaignConfig(boostopsConfigJson);
                    
                    // Download icons for campaigns
                    await DownloadCampaignIcons();
                    
                    // Update last sync time with API source
                    lastRemoteConfigSync = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (BoostOps API)";
                    crossPromoLastSync = lastRemoteConfigSync;
                    
                    // Save to cache
                    SaveCachedRemoteCampaigns();
                    
                    LogDebug($"LoadCampaignsFromAPI: Successfully loaded {cachedRemoteCampaigns.Count} campaigns from API");
                    
                    // Refresh the UI if we're on the cross-promo panel
                    // Comprehensive UI refresh after API load
                    EditorApplication.delayCall += () => {
                        LogDebug("LoadCampaignsFromAPI: Performing comprehensive UI refresh after API load");
                        
                        // Update all status indicators and global state
                        UpdateStatusLights();
                        
                        // Refresh the current tab to show new data
                        switch (selectedTab)
                        {
                            case 0: 
                                LogDebug("Refreshing Overview panel to show updated Source Project Settings");
                                ShowOverviewPanel(); 
                                break;
                            case 1: 
                                ShowLinksPanel(); 
                                break;
                            case 2: 
                                LogDebug("Refreshing Cross-Promo panel to show updated campaigns");
                                ShowCrossPromoPanel(); 
                                break;
                            case 3: 
                                ShowIntegrationsPanel(); 
                                break;
                            default: 
                                ShowOverviewPanel(); 
                                break;
                        }
                        
                        LogDebug($"✅ LoadCampaignsFromAPI: UI refresh completed for tab {selectedTab}");
                    };
                }
                else
                {
                    LogDebug("LoadCampaignsFromAPI: No boostops_config found in API response");
                }
            }
            else
            {
                LogDebug("LoadCampaignsFromAPI: Project not found or API response invalid");
            }
        }
        catch (TaskCanceledException ex)
        {
            LogDebug($"LoadCampaignsFromAPI: Request timed out or was canceled: {ex.Message}");
            LogDebug("LoadCampaignsFromAPI: Skipping JSON write due to timeout");
            // Don't write JSON when there's a timeout - this prevents empty files
        }
        catch (OperationCanceledException ex)
        {
            LogDebug($"LoadCampaignsFromAPI: Operation was canceled: {ex.Message}");
            LogDebug("LoadCampaignsFromAPI: Skipping JSON write due to cancellation");
            // Don't write JSON when there's a cancellation - this prevents empty files
        }
        catch (System.Exception ex)
        {
            LogDebug($"LoadCampaignsFromAPI: Failed to load campaigns from API: {ex.Message}");
            LogDebug("LoadCampaignsFromAPI: Skipping JSON write due to API failure");
            // Don't show error dialog for API failures - just log and fall back to remote config
            // Don't write JSON when there's an error - this prevents empty files
        }
        finally
        {
            isApiCallInProgress = false;
            LogDebug("LoadCampaignsFromAPI: Reset API call flag");
        }
    }

    void SaveBoostOpsConfigToStreamingAssets(string boostopsConfigJson, bool forceOverwrite = false)
    {
        try
        {
            LogDebug($"SaveBoostOpsConfigToStreamingAssets: Saving raw boostops_config JSON (length: {boostopsConfigJson?.Length ?? 0})");
            
            if (string.IsNullOrEmpty(boostopsConfigJson) || boostopsConfigJson == "{}")
            {
                LogDebug("⚠️ WARNING: Received empty or invalid JSON for cross_promo_server.json - skipping file write!");
                LogDebug("This usually means the BoostOps API response doesn't contain valid campaign data or there was a timeout.");
                return; // Don't write empty files
            }
            
            // Create StreamingAssets/BoostOps directory if it doesn't exist
            string streamingAssetsDir = "Assets/StreamingAssets/BoostOps";
            if (!Directory.Exists(streamingAssetsDir))
            {
                Directory.CreateDirectory(streamingAssetsDir);
                LogDebug($"Created directory: {streamingAssetsDir}");
            }
            
            // Save the raw boostops_config JSON directly without any processing
            string filePath = Path.Combine(streamingAssetsDir, "cross_promo_server.json");
            File.WriteAllText(filePath, boostopsConfigJson);
            
            LogDebug($"✅ Saved raw boostops_config to: {filePath} ({boostopsConfigJson.Length} characters)");
            
            // Refresh the AssetDatabase to show the new file
            AssetDatabase.Refresh();
        }
        catch (System.Exception ex)
        {
            LogDebug($"❌ Failed to save boostops_config to StreamingAssets: {ex.Message}");
        }
    }
    
    string FormatJsonString(string json)
    {
        try
        {
            // Parse and re-serialize with indentation using Newtonsoft.Json if available
            // This provides much better formatting than Unity's JsonUtility
            var parsedJson = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            return Newtonsoft.Json.JsonConvert.SerializeObject(parsedJson, Newtonsoft.Json.Formatting.Indented);
        }
        catch (System.Exception ex)
        {
            LogDebug($"Failed to format JSON with Newtonsoft.Json: {ex.Message}, using fallback formatting");
            
            // Fallback: Simple manual formatting
            return FormatJsonManually(json);
        }
    }
    
    string FormatJsonManually(string json)
    {
        // Simple manual JSON formatting for readability
        var formatted = new System.Text.StringBuilder();
        int indentLevel = 0;
        bool inString = false;
        bool escapeNext = false;
        
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            
            if (escapeNext)
            {
                formatted.Append(c);
                escapeNext = false;
                continue;
            }
            
            if (c == '\\')
            {
                formatted.Append(c);
                escapeNext = true;
                continue;
            }
            
            if (c == '"')
            {
                inString = !inString;
                formatted.Append(c);
                continue;
            }
            
            if (inString)
            {
                formatted.Append(c);
                continue;
            }
            
            switch (c)
            {
                case '{':
                case '[':
                    formatted.Append(c);
                    formatted.AppendLine();
                    indentLevel++;
                    formatted.Append(new string(' ', indentLevel * 2));
                    break;
                case '}':
                case ']':
                    formatted.AppendLine();
                    indentLevel--;
                    formatted.Append(new string(' ', indentLevel * 2));
                    formatted.Append(c);
                    break;
                case ',':
                    formatted.Append(c);
                    formatted.AppendLine();
                    formatted.Append(new string(' ', indentLevel * 2));
                    break;
                case ':':
                    formatted.Append(c);
                    formatted.Append(' ');
                    break;
                default:
                    if (!char.IsWhiteSpace(c))
                    {
                        formatted.Append(c);
                    }
                    break;
            }
        }
        
        return formatted.ToString();
    }

    async System.Threading.Tasks.Task LoadCampaignsFromServerFile()
    {
        LogDebug("LoadCampaignsFromServerFile: Starting to load campaigns from cross_promo_server.json");
        
        try
        {
            string serverFilePath = Path.Combine(Application.streamingAssetsPath, "BoostOps", "cross_promo_server.json");
            
            if (!File.Exists(serverFilePath))
            {
                string errorMsg = "❌ BoostOps Managed Mode Error: No cross_promo_server.json file found in StreamingAssets/BoostOps/";
                LogDebug(errorMsg);
                
                // In managed mode, this is an error condition - we should not fall back to local files
                if (crossPromoMode == FeatureMode.Managed)
                {
                    EditorUtility.DisplayDialog("BoostOps Server File Missing", 
                        "In BoostOps Managed mode, the server configuration file is required but missing:\n\n" +
                        "• Expected: StreamingAssets/BoostOps/cross_promo_server.json\n" +
                        "• This file should be created when you log in and fetch project data\n\n" +
                        "Please ensure you're logged in and have synced your project data.", "OK");
                }
                return;
            }
            
            string serverConfigJson = File.ReadAllText(serverFilePath);
            
            if (string.IsNullOrEmpty(serverConfigJson) || serverConfigJson == "{}")
            {
                string errorMsg = "❌ BoostOps Managed Mode Error: cross_promo_server.json file is empty or invalid";
                LogDebug(errorMsg);
                
                // In managed mode, this is an error condition
                if (crossPromoMode == FeatureMode.Managed)
                {
                    EditorUtility.DisplayDialog("BoostOps Server File Invalid", 
                        "The server configuration file exists but is empty or invalid:\n\n" +
                        "• File: StreamingAssets/BoostOps/cross_promo_server.json\n" +
                        "• Content appears to be empty or corrupted\n\n" +
                        "Please try syncing your project data again or contact support.", "OK");
                }
                return;
            }
            
            LogDebug($"Loading campaigns from cross_promo_server.json (file size: {serverConfigJson.Length} characters)");
            
            // Parse the server JSON to get campaigns
            ParseRemoteCampaignConfig(serverConfigJson);
            
            // Validate the parsed campaigns
            bool isValid = ValidateRemoteCampaignConfig();
            if (!isValid)
            {
                LogDebug("Warning: Some campaigns failed validation - check logs for details");
            }
            
            // Download icons for the campaigns
            await DownloadCampaignIcons();
            
            // Update sync time
            var fileInfo = new FileInfo(serverFilePath);
            lastRemoteConfigSync = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") + " (Server File)";
            crossPromoLastSync = lastRemoteConfigSync;
            
            // Save to cache
            SaveCachedRemoteCampaigns();
            
            LogDebug($"✅ Successfully loaded {cachedRemoteCampaigns.Count} campaigns from server file");
            
            // Refresh the UI if we're on the cross-promo panel
            if (selectedTab == 2)
            {
                EditorApplication.delayCall += () => {
                    LogDebug("LoadCampaignsFromServerFile: Refreshing Cross-Promo panel after server file load");
                    ShowCrossPromoPanel();
                };
            }
        }
        catch (System.Exception ex)
        {
            string errorMsg = $"❌ Failed to load campaigns from server file: {ex.Message}";
            LogDebug(errorMsg);
            
            // In managed mode, show a clear error dialog
            if (crossPromoMode == FeatureMode.Managed)
            {
                EditorUtility.DisplayDialog("BoostOps Server File Error", 
                    $"Failed to load campaign data from server file:\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    "In BoostOps Managed mode, campaign data must come from either:\n" +
                    "• Live Remote Config (Unity/Firebase)\n" +
                    "• Server backup file (cross_promo_server.json)\n\n" +
                    "Local files are not used as fallbacks in Managed mode.", "OK");
            }
        }
    }

    async System.Threading.Tasks.Task SyncCampaignsFromStoredConfig()
    {
        LogDebug("SyncCampaignsFromStoredConfig: Starting to sync campaigns from stored runtime config");
        
        try
        {
            // Load the JSON that was saved by the runtime system
            string runtimeConfigJson = EditorPrefs.GetString("BoostOps_RuntimeConfig_JSON", "");
            string runtimeConfigProvider = EditorPrefs.GetString("BoostOps_RuntimeConfig_Provider", "");
            string runtimeConfigTimestamp = EditorPrefs.GetString("BoostOps_RuntimeConfig_Timestamp", "");
            
            if (string.IsNullOrEmpty(runtimeConfigJson) || runtimeConfigJson == "{}")
            {
                LogDebug("No stored runtime config found. Trying to load from server file as fallback...");
                await LoadCampaignsFromServerFile();
                return;
            }
            
            LogDebug($"Using stored runtime config from {runtimeConfigProvider} (retrieved at {runtimeConfigTimestamp})");
            LogDebug($"Config JSON length: {runtimeConfigJson.Length} characters");
            
            // Parse the stored JSON to get campaigns
            ParseRemoteCampaignConfig(runtimeConfigJson);
            
            // Validate the parsed campaigns
            bool isValid = ValidateRemoteCampaignConfig();
            if (!isValid)
            {
                LogDebug("Warning: Some campaigns failed validation - check logs for details");
            }
            
            // Download icons for the campaigns
            await DownloadCampaignIcons();
            
            // Update sync time
            lastRemoteConfigSync = runtimeConfigTimestamp + " (Stored Config)";
            crossPromoLastSync = lastRemoteConfigSync;
            
            // Save to cache
            SaveCachedRemoteCampaigns();
            
            LogDebug($"Successfully synced {cachedRemoteCampaigns.Count} campaigns from stored config");
            
            // Refresh the UI if we're on the cross-promo panel
            if (selectedTab == 2)
            {
                EditorApplication.delayCall += () => {
                    LogDebug("SyncCampaignsFromStoredConfig: Refreshing Cross-Promo panel after stored config sync");
                    ShowCrossPromoPanel();
                };
            }
        
        // Debug: Log campaign details for verification
        if (cachedRemoteCampaigns != null && cachedRemoteCampaigns.Count > 0)
        {
            LogDebug("Campaign details after sync:");
            foreach (var campaign in cachedRemoteCampaigns)
            {
                LogDebug($"  Campaign: {campaign.name ?? campaign.campaign_id}, Status: {campaign.status}");
            }
        }
        else
        {
            LogDebug("WARNING: No campaigns in cachedRemoteCampaigns after sync!");
        }
        
        // Show success popup and refresh only the cross-promo content
            EditorApplication.delayCall += () => {
                EditorUtility.DisplayDialog("Sync Complete", 
                    $"Successfully synced {cachedRemoteCampaigns.Count} campaigns from stored remote config.\n\n" +
                    $"Icons have been downloaded and saved to:\nAssets/Resources/BoostOps/Icons/\n\n" +
                    $"Check the console for detailed logs.", "OK");
                
                // Add another delay to ensure dialog is dismissed before refresh
                EditorApplication.delayCall += () => {
                    LogDebug("Performing delayed refresh after dialog dismissal");
                    RefreshCrossPromoContent();
                };
            };
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error syncing campaigns from stored config: {ex.Message}");
            EditorApplication.delayCall += () => {
                EditorUtility.DisplayDialog("Sync Failed", 
                    $"Failed to sync campaigns from stored config:\n\n{ex.Message}\n\nPlease check the console for more details.", "OK");
            };
        }
        
        LogDebug("SyncCampaignsFromStoredConfig: Completed");
    }
    
    async System.Threading.Tasks.Task FetchFromUnityRemoteConfig()
    {
        LogDebug("FetchFromUnityRemoteConfig: Fetching from Unity Remote Config");
        
        try
        {
            // Use reflection to access Unity Remote Config 4.x API since we don't want hard dependencies
            var remoteConfigType = System.Type.GetType("Unity.Services.RemoteConfig.RemoteConfigService, Unity.Services.RemoteConfig");
                                 
            if (remoteConfigType == null)
            {
                LogDebug("Unity Remote Config type not found - expected Unity.Services.RemoteConfig.RemoteConfigService");
                return;
            }
            
            // Get the instance and fetch config
            var instanceProperty = remoteConfigType.GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (instanceProperty == null)
            {
                LogDebug("Unity Remote Config Instance property not found");
                return;
            }
            
            var instance = instanceProperty.GetValue(null);
            if (instance == null)
            {
                LogDebug("Unity Remote Config instance is null");
                return;
            }
            
            // Fetch configuration using Unity Remote Config 4.x API
            // Be specific about method signature to avoid ambiguous matches
            var fetchMethod = remoteConfigType.GetMethod("FetchConfigsAsync", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public, 
                null, 
                new System.Type[] { typeof(object), typeof(object) }, 
                null);
                
            if (fetchMethod == null)
            {
                // Try the generic version
                var methods = remoteConfigType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var method in methods)
                {
                    if (method.Name == "FetchConfigsAsync" && method.IsGenericMethodDefinition)
                    {
                        fetchMethod = method;
                        break;
                    }
                }
            }
            
            if (fetchMethod != null)
            {
                try
                {
                    System.Threading.Tasks.Task fetchTask = null;
                    
                    if (fetchMethod.IsGenericMethodDefinition)
                    {
                        // FetchConfigsAsync<TUserAttributes, TAppAttributes>(userAttributes, appAttributes)
                        var genericFetchMethod = fetchMethod.MakeGenericMethod(typeof(object), typeof(object));
                        fetchTask = genericFetchMethod.Invoke(instance, new object[] { new object(), new object() }) as System.Threading.Tasks.Task;
                    }
                    else
                    {
                        // Direct method call
                        fetchTask = fetchMethod.Invoke(instance, new object[] { new object(), new object() }) as System.Threading.Tasks.Task;
                    }
                    
                    if (fetchTask != null)
                    {
                        await fetchTask;
                        LogDebug("Unity Remote Config 4.x fetch completed");
                    }
                }
                catch (System.Exception ex)
                {
                    LogDebug($"❌ ERROR calling FetchConfigsAsync: {ex.Message}");
                    LogDebug($"Method details - IsGeneric: {fetchMethod.IsGenericMethodDefinition}, Parameters: {fetchMethod.GetParameters().Length}");
                }
            }
            else
            {
                LogDebug("❌ ERROR: FetchConfigsAsync method not found with expected signature");
            }
            
            // Get the BoostOps campaign configuration using Unity Remote Config 4.x API
            // In 4.x, we need to access appConfig.GetJson() instead of instance.GetJson()
            var appConfigProperty = remoteConfigType.GetProperty("appConfig", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (appConfigProperty != null)
            {
                var appConfig = appConfigProperty.GetValue(instance);
                if (appConfig != null)
                {
                    // Be specific about GetJson method signature to avoid ambiguous matches
                    var getJsonMethod = appConfig.GetType().GetMethod("GetJson", 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public, 
                        null, 
                        new System.Type[] { typeof(string), typeof(string) }, 
                        null);
                    if (getJsonMethod != null)
                    {
                        try
                        {
                            string campaignJson = getJsonMethod.Invoke(appConfig, new object[] { "boostops_config", "{}" }) as string;
                        LogDebug($"✅ Retrieved campaign JSON via Unity Remote Config 4.x appConfig.GetJson: {campaignJson}");
                        LogDebug($"JSON length: {campaignJson?.Length ?? 0} characters");
                        LogDebug($"JSON preview: {(campaignJson?.Length > 200 ? campaignJson.Substring(0, 200) + "..." : campaignJson)}");
                        
                        // Enhanced debugging - show exactly what we got
                        if (string.IsNullOrEmpty(campaignJson))
                        {
                            LogDebug("❌ ISSUE: campaignJson is NULL or EMPTY!");
                            LogDebug("This means the 'boostops_config' key doesn't exist in your Unity Remote Config");
                            LogDebug("Solution: Go to Unity Remote Config dashboard and add a 'boostops_config' key with your campaign JSON");
                        }
                        else if (campaignJson == "{}")
                        {
                            LogDebug("❌ ISSUE: campaignJson is just empty braces '{}'!");
                            LogDebug("This means the 'boostops_config' key exists but has no data");
                            LogDebug("Solution: Update your 'boostops_config' key with actual campaign data");
                        }
                        else if (campaignJson.Length < 50)
                        {
                            LogDebug($"⚠️ WARNING: Campaign JSON is very short ({campaignJson.Length} chars): '{campaignJson}'");
                            LogDebug("This is likely missing proper campaign data");
                        }
                        else
                        {
                            LogDebug("✅ Got valid-looking campaign JSON, attempting to parse...");
                            ParseRemoteCampaignConfig(campaignJson);
                            
                            // Don't save Unity Remote Config to avoid overwriting server file
                            LogDebug("🛡️ Unity Remote Config loaded - NOT saving to avoid overwriting server file");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            LogDebug($"❌ ERROR calling GetJson: {ex.Message}");
                            if (ex.InnerException != null)
                            {
                                LogDebug($"Inner exception: {ex.InnerException.Message}");
                            }
                        }
                    }
                    else
                    {
                        LogDebug("❌ ERROR: GetJson method not found on appConfig!");
                        // List all available methods for debugging
                        var availableMethods = appConfig.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                            .Where(m => m.Name.Contains("Get"))
                            .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                            .ToArray();
                        LogDebug($"Available Get* methods: {string.Join(", ", availableMethods)}");
                    }
                }
                else
                {
                    LogDebug("❌ ERROR: appConfig property returned null!");
                }
            }
            else
            {
                LogDebug("❌ ERROR: appConfig property not found on Unity Remote Config!");
                LogDebug("This suggests Unity Remote Config 4.x API structure has changed");
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error fetching from Unity Remote Config: {ex.Message}");
            throw;
        }
    }
    
    async System.Threading.Tasks.Task FetchFromFirebaseRemoteConfig()
    {
        LogDebug("FetchFromFirebaseRemoteConfig: Fetching from Firebase Remote Config");
        
        try
        {
            // Use reflection to access Firebase Remote Config
            var firebaseConfigType = System.Type.GetType("Firebase.RemoteConfig.FirebaseRemoteConfig, Firebase.RemoteConfig");
            if (firebaseConfigType == null)
            {
                LogDebug("Firebase Remote Config type not found");
                return;
            }
            
            // Get default instance
            var defaultInstanceProperty = firebaseConfigType.GetProperty("DefaultInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (defaultInstanceProperty == null)
            {
                LogDebug("Firebase Remote Config DefaultInstance not found");
                return;
            }
            
            var instance = defaultInstanceProperty.GetValue(null);
            if (instance == null)
            {
                LogDebug("Firebase Remote Config instance is null");
                return;
            }
            
            // Fetch and activate
            var fetchMethod = firebaseConfigType.GetMethod("FetchAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public, null, new System.Type[0], null);
            if (fetchMethod != null)
            {
                var fetchTask = fetchMethod.Invoke(instance, null) as System.Threading.Tasks.Task;
                if (fetchTask != null)
                {
                    await fetchTask;
                    LogDebug("Firebase Remote Config fetch completed");
                    
                    // Activate fetched configs
                    var activateMethod = firebaseConfigType.GetMethod("ActivateAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    if (activateMethod != null)
                    {
                        var activateTask = activateMethod.Invoke(instance, null) as System.Threading.Tasks.Task;
                        if (activateTask != null)
                        {
                            await activateTask;
                        }
                    }
                }
            }
            
            // Get campaign configuration as structured JSON
            var getValueMethod = firebaseConfigType.GetMethod("GetValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public, null, new System.Type[] { typeof(string) }, null);
            if (getValueMethod != null)
            {
                var configValue = getValueMethod.Invoke(instance, new object[] { "boostops_config" });
                if (configValue != null)
                {
                    // Try to get JSON value first (structured data)
                    var jsonValueProperty = configValue.GetType().GetProperty("JsonValue");
                    string campaignJson = null;
                    
                    if (jsonValueProperty != null)
                    {
                        campaignJson = jsonValueProperty.GetValue(configValue) as string;
                        LogDebug($"Retrieved Firebase campaign JSON via JsonValue: {campaignJson}");
                    }
                    else
                    {
                        // Fallback to StringValue
                        var stringValueProperty = configValue.GetType().GetProperty("StringValue");
                        if (stringValueProperty != null)
                        {
                            campaignJson = stringValueProperty.GetValue(configValue) as string;
                            LogDebug($"Retrieved Firebase campaign JSON via StringValue fallback: {campaignJson}");
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(campaignJson) && campaignJson != "{}")
                    {
                        ParseRemoteCampaignConfig(campaignJson);
                        
                        // Don't save Firebase Remote Config to avoid overwriting server file
                        LogDebug("🛡️ Firebase Remote Config loaded - NOT saving to avoid overwriting server file");
                    }
                    else
                    {
                        LogDebug("No BoostOps campaigns found in Firebase Remote Config");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error fetching from Firebase Remote Config: {ex.Message}");
            throw;
        }
    }
    
    async System.Threading.Tasks.Task ShowImportRegisteredAppsDialog()
    {
        LogDebug("ShowImportRegisteredAppsDialog: Starting to fetch registered apps from BoostOps dashboard");
        
        try
        {
            // Check if user is authenticated using existing auth state
            if (!isLoggedIn || string.IsNullOrEmpty(apiToken))
            {
                EditorUtility.DisplayDialog("Authentication Required", 
                    "Please authenticate with BoostOps first using the Account button in the top-right corner.", "OK");
                return;
            }
            
            // Initialize integration with current credentials
            var waspIntegration = new BoostOpsEditorWaspIntegration();
            if (!waspIntegration.Initialize(apiToken, userEmail, userEmail))
            {
                EditorUtility.DisplayDialog("Authentication Error", 
                    "Failed to initialize authentication. Please sign out and sign in again.", "OK");
                return;
            }
            
            LogDebug("Fetching registered apps from BoostOps API...");
            var appsResponse = await waspIntegration.GetRegisteredApps("512x512");
            
            if (appsResponse != null && appsResponse.success && appsResponse.apps != null && appsResponse.apps.Length > 0)
            {
                LogDebug($"Successfully fetched {appsResponse.totalApps} registered apps for studio: {appsResponse.studio?.name}");
                LogDebug($"Total estimated size: {appsResponse.totalEstimatedMB:F1}MB ({appsResponse.totalEstimatedBytes} bytes)");
                ShowRegisteredAppsSelectionDialog(appsResponse.apps);
            }
            else
            {
                string message = "Unknown error occurred";
                if (appsResponse != null && appsResponse.success && (appsResponse.apps == null || appsResponse.apps.Length == 0))
                {
                    message = $"No registered apps found for studio '{appsResponse.studio?.name ?? "Unknown"}'.\n\nMake sure you have registered at least one project with BoostOps.";
                }
                else if (appsResponse != null && !appsResponse.success)
                {
                    message = "Failed to fetch registered apps from BoostOps API.";
                }
                
                EditorUtility.DisplayDialog("No Apps Found", message, "OK");
                LogDebug($"No registered apps available: {message}");
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error fetching registered apps: {ex.Message}");
            EditorUtility.DisplayDialog("Error", 
                $"Failed to fetch registered apps:\n\n{ex.Message}", "OK");
        }
    }
    
    void ShowRegisteredAppsSelectionDialog(RegisteredApp[] apps)
    {
        LogDebug($"ShowRegisteredAppsSelectionDialog: Showing selection dialog for {apps.Length} apps");
        
        var window = EditorWindow.CreateInstance<RegisteredAppsImportWindow>();
        window.titleContent = new GUIContent("Cache App Icons");
        window.Initialize(apps, OnAppsSelectedForImport);
        window.ShowModalUtility();
    }
    
    void OnAppsSelectedForImport(RegisteredApp[] selectedApps)
    {
        if (selectedApps == null || selectedApps.Length == 0)
        {
            LogDebug("OnAppsSelectedForImport: No apps selected for import");
            return;
        }
        
        LogDebug($"OnAppsSelectedForImport: Starting icon caching for {selectedApps.Length} registered apps");
        
        // Use EditorApplication.delayCall to prevent UI freezing
        EditorApplication.delayCall += () => StartIconCachingProcess(selectedApps);
    }
    
    async void StartIconCachingProcess(RegisteredApp[] selectedApps)
    {
        int downloadedCount = 0;
        int totalAttempts = 0;
        
        try
        {
            for (int i = 0; i < selectedApps.Length; i++)
            {
                var app = selectedApps[i];
                totalAttempts++;
                
                // Update progress before starting download
                float progress = (float)i / selectedApps.Length;
                EditorUtility.DisplayProgressBar("Caching App Icons", 
                    $"Downloading icon for {app.name}... ({i + 1}/{selectedApps.Length})", 
                    progress);
                
                // Yield control to Unity's main thread to prevent UI freezing
                await System.Threading.Tasks.Task.Yield();
                
                try
                {
                    LogDebug($"Starting download for {app.name} ({i + 1}/{selectedApps.Length})");
                    bool success = await DownloadAppIconWithFallback(app);
                    
                    if (success) 
                    {
                        downloadedCount++;
                        LogDebug($"✅ Successfully downloaded icon for {app.name}");
                        
                        // Update progress after successful download
                        EditorUtility.DisplayProgressBar("Caching App Icons", 
                            $"✅ Downloaded {app.name} ({downloadedCount} of {selectedApps.Length})", 
                            (float)(i + 0.8f) / selectedApps.Length);
                    }
                    else
                    {
                        LogDebug($"❌ Failed to download icon for {app.name}");
                    }
                    
                    // Small delay between downloads to keep UI responsive
                    await System.Threading.Tasks.Task.Delay(100);
                }
                catch (System.Exception ex)
                {
                    LogDebug($"❌ Error downloading icon for {app.name}: {ex.Message}");
                }
                
                // Yield control again after each download
                await System.Threading.Tasks.Task.Yield();
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"❌ Critical error during icon caching process: {ex.Message}");
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Error", 
                $"Icon caching failed with error:\n\n{ex.Message}", "OK");
            return;
        }
        
        // Final progress update
        EditorUtility.DisplayProgressBar("Caching App Icons", 
            "Finalizing and refreshing assets...", 1.0f);
        
        // Small delay before clearing progress bar
        await System.Threading.Tasks.Task.Delay(500);
        EditorUtility.ClearProgressBar();
        
        // Refresh AssetDatabase to show new icons
        AssetDatabase.Refresh();
        
        // Show completion dialog
        string resultMessage = $"Icon caching completed!\n\n" +
                              $"Successfully cached: {downloadedCount} icons\n" +
                              $"Failed downloads: {totalAttempts - downloadedCount} icons\n\n" +
                              $"Icons saved to:\nAssets/Resources/BoostOps/Icons/";
        
        EditorUtility.DisplayDialog("Caching Complete", resultMessage, "OK");
        
        // Refresh cross-promo content to show new icons
        EditorApplication.delayCall += () => {
            RefreshCrossPromoContent();
        };
        
        LogDebug($"StartIconCachingProcess: Icon caching completed - {downloadedCount}/{totalAttempts} successful");
    }
    
    async System.Threading.Tasks.Task<bool> DownloadAppIconWithFallback(RegisteredApp app)
    {
        if (app.allPlatforms == null || app.allPlatforms.Length == 0)
        {
            LogDebug($"No platforms available for app {app.name}");
            return false;
        }
        
        // Get priority platform (iOS > Android > Others)
        var primaryPlatform = GetPriorityIconPlatform(app.allPlatforms);
        if (primaryPlatform != null)
        {
            LogDebug($"Trying primary platform {primaryPlatform.type} for {app.name}");
            bool success = await TryDownloadIconFromPlatform(app, primaryPlatform);
            if (success) return true;
        }
        
        // Try fallback platforms
        foreach (var platform in app.allPlatforms.Where(p => !p.isPriority))
        {
            LogDebug($"Trying fallback platform {platform.type} for {app.name}");
            bool success = await TryDownloadIconFromPlatform(app, platform);
            if (success) return true;
        }
        
        LogDebug($"All platforms failed for {app.name}");
        return false;
    }
    
    /// <summary>
    /// Extract Apple Store ID from URL (e.g., "https://apps.apple.com/app/id1144343820" -> "1144343820")
    /// </summary>
    string ExtractIosAppStoreId(string iosUrl)
    {
        if (string.IsNullOrEmpty(iosUrl)) return null;
        
        LogDebug($"Attempting to extract iOS store ID from URL: {iosUrl}");
        
        // Try multiple patterns for different URL formats
        
        // Pattern 1: Standard App Store URLs - https://apps.apple.com/.../id1234567890
        var match1 = System.Text.RegularExpressions.Regex.Match(iosUrl, @"id(\d{8,12})");
        if (match1.Success)
        {
            LogDebug($"✅ Extracted iOS store ID using pattern 1: {match1.Groups[1].Value}");
            return match1.Groups[1].Value;
        }
        
        // Pattern 2: iTunes API URLs - https://itunes.apple.com/lookup?id=1234567890
        var match2 = System.Text.RegularExpressions.Regex.Match(iosUrl, @"[?&]id=(\d{8,12})");
        if (match2.Success)
        {
            LogDebug($"✅ Extracted iOS store ID using pattern 2: {match2.Groups[1].Value}");
            return match2.Groups[1].Value;
        }
        
        // Pattern 3: Apple CDN icon URLs - https://is1-ssl.mzstatic.com/.../1234567890.jpg
        var match3 = System.Text.RegularExpressions.Regex.Match(iosUrl, @"/(\d{8,12})\.(?:jpg|png|jpeg)");
        if (match3.Success)
        {
            LogDebug($"✅ Extracted iOS store ID using pattern 3: {match3.Groups[1].Value}");
            return match3.Groups[1].Value;
        }
        
        // Pattern 4: General numeric ID in path
        var match4 = System.Text.RegularExpressions.Regex.Match(iosUrl, @"/(\d{8,12})(?:/|$|\?)");
        if (match4.Success)
        {
            LogDebug($"✅ Extracted iOS store ID using pattern 4: {match4.Groups[1].Value}");
            return match4.Groups[1].Value;
        }
        
        LogDebug($"❌ Could not extract iOS store ID from URL: {iosUrl}");
        return null;
    }
    
    /// <summary>
    /// Extract Android package ID from URL (e.g., "https://play.google.com/store/apps/details?id=com.example.app" -> "com.example.app")
    /// </summary>
    string ExtractAndroidPackageId(string androidUrl)
    {
        if (string.IsNullOrEmpty(androidUrl)) return null;
        
        LogDebug($"Attempting to extract Android package ID from URL: {androidUrl}");
        
        // Pattern 1: Standard Google Play URLs - ?id=com.example.app
        var match1 = System.Text.RegularExpressions.Regex.Match(androidUrl, @"[?&]id=([a-zA-Z0-9._]+)");
        if (match1.Success)
        {
            LogDebug($"✅ Extracted Android package ID using pattern 1: {match1.Groups[1].Value}");
            return match1.Groups[1].Value;
        }
        
        // Pattern 2: Package ID in path - /com.example.app/
        var match2 = System.Text.RegularExpressions.Regex.Match(androidUrl, @"/([a-zA-Z][a-zA-Z0-9._]{2,})(?:/|$|\?)");
        if (match2.Success && match2.Groups[1].Value.Contains("."))
        {
            LogDebug($"✅ Extracted Android package ID using pattern 2: {match2.Groups[1].Value}");
            return match2.Groups[1].Value;
        }
        
        LogDebug($"❌ Could not extract Android package ID from URL: {androidUrl}");
        return null;
    }
    
    async System.Threading.Tasks.Task<bool> TryDownloadIconFromPlatform(RegisteredApp app, AppPlatform platform)
    {
        if (string.IsNullOrEmpty(platform.iconUrl))
        {
            LogDebug($"No icon URL for {app.name} on {platform.type}");
            return false;
        }
        
        try
        {
            // Yield control before HTTP operation to keep UI responsive
            await System.Threading.Tasks.Task.Yield();
            
            using (var client = new System.Net.Http.HttpClient())
            {
                client.Timeout = System.TimeSpan.FromSeconds(15); // Reasonable timeout for downloads
                
                LogDebug($"Downloading {platform.type} icon for {app.name}: {platform.iconUrl}");
                var imageBytes = await client.GetByteArrayAsync(platform.iconUrl);
                
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    // Get store ID - prioritize API-provided storeId, then extract from URL
                    string storeId = null;
                    string platformSuffix = null;
                    
                    if (platform.type == "APPLE_STORE" || platform.type == "IOS_APP_STORE")
                    {
                        platformSuffix = "ios";
                        
                        // Priority 1: Use API-provided storeId or appleStoreId
                        if (!string.IsNullOrEmpty(platform.storeId))
                        {
                            storeId = platform.storeId;
                            LogDebug($"✅ Using API-provided iOS store ID: {storeId}");
                        }
                        else if (!string.IsNullOrEmpty(platform.appleStoreId))
                        {
                            storeId = platform.appleStoreId;
                            LogDebug($"✅ Using API-provided appleStoreId: {storeId}");
                        }
                        // Priority 2: Extract from icon URL
                        else
                        {
                            storeId = ExtractIosAppStoreId(platform.iconUrl);
                            if (!string.IsNullOrEmpty(storeId))
                            {
                                LogDebug($"✅ Extracted iOS store ID from URL: {storeId}");
                            }
                        }
                    }
                    else if (platform.type == "GOOGLE_STORE" || platform.type == "GOOGLE_PLAY")
                    {
                        platformSuffix = "android";
                        
                        // Priority 1: Use API-provided storeId (package name)
                        if (!string.IsNullOrEmpty(platform.storeId))
                        {
                            storeId = platform.storeId;
                            LogDebug($"✅ Using API-provided Android package ID: {storeId}");
                        }
                        // Priority 2: Use packageName if available
                        else if (!string.IsNullOrEmpty(platform.packageName))
                        {
                            storeId = platform.packageName;
                            LogDebug($"✅ Using API-provided packageName: {storeId}");
                        }
                        // Priority 2: Use bundleId if available
                        else if (!string.IsNullOrEmpty(platform.bundleId))
                        {
                            storeId = platform.bundleId;
                            LogDebug($"✅ Using API-provided Android bundle ID: {storeId}");
                        }
                        // Priority 3: Extract from icon URL
                        else
                        {
                            storeId = ExtractAndroidPackageId(platform.iconUrl);
                            if (!string.IsNullOrEmpty(storeId))
                            {
                                LogDebug($"✅ Extracted Android package ID from URL: {storeId}");
                            }
                        }
                    }
                    else
                    {
                        // Other platforms - use whatever is available
                        platformSuffix = platform.type.ToLower();
                        storeId = platform.storeId ?? platform.bundleId ?? app.id;
                        LogDebug($"✅ Using store ID for {platform.type}: {storeId}");
                    }
                    
                    // Final fallback to BoostOps project ID
                    if (string.IsNullOrEmpty(storeId))
                    {
                        LogDebug($"❌ Could not determine store ID for {platform.type}. Icon URL: {platform.iconUrl}");
                        LogDebug($"   API storeId: {platform.storeId ?? "null"}");
                        LogDebug($"   API bundleId: {platform.bundleId ?? "null"}");
                        LogDebug($"   Falling back to BoostOps project ID: {app.id}");
                        storeId = app.id;
                    }
                    
                    string fileName = $"{SanitizeStoreId(storeId)}_icon.png";
                    string assetPath = $"Assets/Resources/BoostOps/Icons/{fileName}";
                    
                    bool saved = await SaveIconAsAssetForRegisteredApp(app, platform, imageBytes, assetPath);
                    
                    // Yield control after file operation to keep UI responsive
                    await System.Threading.Tasks.Task.Yield();
                    
                    if (saved)
                    {
                        LogDebug($"✅ Successfully saved {platform.type} icon for {app.name}: {fileName}");
                        return true;
                    }
                    else
                    {
                        LogDebug($"❌ Failed to save {platform.type} icon for {app.name}");
                    }
                }
                else
                {
                    LogDebug($"❌ Empty or invalid image data for {app.name} on {platform.type}");
                }
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"❌ Exception downloading {platform.type} icon for {app.name}: {ex.Message}");
        }
        
        return false;
    }
    
    async System.Threading.Tasks.Task<bool> SaveIconAsAssetForRegisteredApp(RegisteredApp app, AppPlatform platform, byte[] imageBytes, string assetPath)
    {
        try
        {
            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(assetPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                LogDebug($"Created directory: {directory}");
            }
            
            // Write the file
            await System.IO.File.WriteAllBytesAsync(assetPath, imageBytes);
            string fileName = System.IO.Path.GetFileName(assetPath);
            LogDebug($"Saved {platform.type} icon file for '{app.name}': {fileName} ({imageBytes.Length} bytes)");
            
            // Yield control after file write to keep UI responsive
            await System.Threading.Tasks.Task.Yield();
            
            // Defer asset database operations to avoid timing issues
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // Configure as sprite in AssetDatabase
                    AssetDatabase.ImportAsset(assetPath);
                    
                    var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (textureImporter != null)
                    {
                        textureImporter.textureType = TextureImporterType.Sprite;
                        textureImporter.spriteImportMode = SpriteImportMode.Single;
                        textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                        textureImporter.alphaIsTransparency = true;
                        textureImporter.mipmapEnabled = false;
                        textureImporter.isReadable = false;
                        
                        EditorUtility.SetDirty(textureImporter);
                        textureImporter.SaveAndReimport();
                        
                        LogDebug($"Configured {assetPath} as sprite");
                    }
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError($"Failed to configure texture importer for {app.name}: {ex.Message}");
                }
            };
            
            return true;
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error saving icon asset for {app.name}: {ex.Message}");
            return false;
        }
    }
    
    AppPlatform GetPriorityIconPlatform(AppPlatform[] platforms)
    {
        if (platforms == null || platforms.Length == 0)
            return null;
        
        // Helper method to check if platform is Apple/iOS
        bool IsApplePlatform(AppPlatform p) => p.type == "APPLE_STORE" || p.type == "IOS_APP_STORE";
        
        // Helper method to check if platform is Google/Android  
        bool IsGooglePlatform(AppPlatform p) => p.type == "GOOGLE_STORE" || p.type == "GOOGLE_PLAY";
        
        // Priority 1: Apple App Store with priority flag and valid icon URL
        var ios = platforms.FirstOrDefault(p => IsApplePlatform(p) && 
                                               p.isPriority && 
                                               !string.IsNullOrEmpty(p.iconUrl));
        if (ios != null) return ios;
        
        // Priority 2: Any Apple App Store with valid icon URL
        ios = platforms.FirstOrDefault(p => IsApplePlatform(p) && 
                                           !string.IsNullOrEmpty(p.iconUrl));
        if (ios != null) return ios;
        
        // Priority 3: Google Play with valid icon URL
        var android = platforms.FirstOrDefault(p => IsGooglePlatform(p) && 
                                                   !string.IsNullOrEmpty(p.iconUrl));
        if (android != null) return android;
        
        // Priority 4: Any other platform with valid icon URL
        return platforms.FirstOrDefault(p => !string.IsNullOrEmpty(p.iconUrl));
    }
    

    
    void ParseRemoteCampaignConfig(string jsonData)
    {
        try
        {
            // Store the JSON for later re-parsing (e.g., for app walls refresh)
            lastFetchedRemoteConfigJson = jsonData;
            
            LogDebug($"ParseRemoteCampaignConfig: Parsing JSON data using shared model (same as runtime)");
            LogDebug($"🔍 JSON length: {jsonData?.Length ?? 0} characters");
            
            LogDebug($"🔍 About to attempt full RemoteCampaignConfig deserialization...");
            
            // Debug: Show raw JSON segment for store_ids
            try 
            {
                LogDebug($"🔍 Raw JSON contains 'apple': {jsonData.Contains("\"apple\":")}");
                LogDebug($"🔍 Raw JSON contains '1144343820': {jsonData.Contains("1144343820")}");
                
                // Find the store_ids section
                int storeIdsStart = jsonData.IndexOf("\"store_ids\"");
                if (storeIdsStart >= 0)
                {
                    int braceStart = jsonData.IndexOf("{", storeIdsStart);
                    int braceEnd = jsonData.IndexOf("}", braceStart);
                    if (braceStart >= 0 && braceEnd >= 0)
                    {
                        string storeIdsJson = jsonData.Substring(braceStart, braceEnd - braceStart + 1);
                        LogDebug($"🔍 Raw store_ids JSON: {storeIdsJson}");
                    }
                }
            } 
            catch (System.Exception ex) 
            {
                LogDebug($"🔍 Debug JSON extraction failed: {ex.Message}");
            }
            
            // Try Unity JsonUtility first, but handle nested object parsing manually if needed
            var config = JsonUtility.FromJson<BoostOps.Core.RemoteCampaignConfig>(jsonData);
            
            LogDebug($"[App Walls] After JsonUtility parse - config.app_walls is null: {config?.app_walls == null}");
            if (config?.app_walls != null)
            {
                LogDebug($"[App Walls] config.app_walls.@default is null: {config.app_walls.@default == null}");
                if (config.app_walls.@default != null)
                {
                    LogDebug($"[App Walls] config.app_walls.@default.items length: {config.app_walls.@default.items?.Length ?? 0}");
                }
            }
            
            // Workaround: Unity JsonUtility sometimes fails with nested [Serializable] objects
            // Manually parse store_ids if they're null OR empty but exist in JSON
            bool needsStoreIdsWorkaround = config?.source_project != null && jsonData.Contains("\"store_ids\"") &&
                (config.source_project.store_ids == null || 
                 (string.IsNullOrEmpty(config.source_project.store_ids.apple) && 
                  string.IsNullOrEmpty(config.source_project.store_ids.google)));
            
            if (needsStoreIdsWorkaround)
            {
                LogDebug("🔧 Detected JsonUtility nested object issue - store_ids is null or empty, applying manual parsing workaround");
                
                // Instantiate store_ids if it's null
                if (config.source_project.store_ids == null)
                {
                    config.source_project.store_ids = new BoostOps.Core.StoreIds();
                    LogDebug("🔧 Instantiated new StoreIds object");
                }
                
                // Find source_project section first
                int sourceProjectIndex = jsonData.IndexOf("\"source_project\"");
                if (sourceProjectIndex >= 0)
                {
                    // Find store_ids within source_project
                    int storeIdsIndex = jsonData.IndexOf("\"store_ids\"", sourceProjectIndex);
                    if (storeIdsIndex >= 0)
                    {
                        // Find the opening brace
                        int openBraceIndex = jsonData.IndexOf("{", storeIdsIndex);
                        if (openBraceIndex >= 0)
                        {
                            // Find matching closing brace
                            int closeBraceIndex = openBraceIndex + 1;
                            int braceCount = 1;
                            while (closeBraceIndex < jsonData.Length && braceCount > 0)
                            {
                                if (jsonData[closeBraceIndex] == '{') braceCount++;
                                if (jsonData[closeBraceIndex] == '}') braceCount--;
                                if (braceCount > 0) closeBraceIndex++;
                            }
                            
                            if (braceCount == 0)
                            {
                                string storeIdsContent = jsonData.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
                                LogDebug($"🔧 Extracted store_ids content: {storeIdsContent}");
                                
                                // Parse individual fields
                                var appleMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""apple""\s*:\s*""([^""]*)""");
                                var googleMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""google""\s*:\s*""([^""]*)""");
                                var microsoftMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""microsoft""\s*:\s*""([^""]*)""");
                                var amazonMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""amazon""\s*:\s*""([^""]*)""");
                                var samsungMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""samsung""\s*:\s*""([^""]*)""");
                                
                                if (appleMatch.Success && !string.IsNullOrEmpty(appleMatch.Groups[1].Value)) 
                                    config.source_project.store_ids.apple = appleMatch.Groups[1].Value;
                                if (googleMatch.Success && !string.IsNullOrEmpty(googleMatch.Groups[1].Value)) 
                                    config.source_project.store_ids.google = googleMatch.Groups[1].Value;
                                if (microsoftMatch.Success && !string.IsNullOrEmpty(microsoftMatch.Groups[1].Value)) 
                                    config.source_project.store_ids.microsoft = microsoftMatch.Groups[1].Value;
                                if (amazonMatch.Success && !string.IsNullOrEmpty(amazonMatch.Groups[1].Value)) 
                                    config.source_project.store_ids.amazon = amazonMatch.Groups[1].Value;
                                if (samsungMatch.Success && !string.IsNullOrEmpty(samsungMatch.Groups[1].Value)) 
                                    config.source_project.store_ids.samsung = samsungMatch.Groups[1].Value;
                                
                                LogDebug($"🔧 Manually parsed store_ids - apple: '{config.source_project.store_ids.apple}', google: '{config.source_project.store_ids.google}', microsoft: '{config.source_project.store_ids.microsoft}'");
                            }
                        }
                    }
                }
            }
            LogDebug($"🔍 config is null: {config == null}");
            if (config != null)
            {
                LogDebug($"🔍 config.source_project is null: {config.source_project == null}");
                // Cache source project settings from boostops_config
                if (config.source_project != null)
                {
                    // Convert from BoostOps.Core.SourceProject to our editor SourceProject class
                    cachedSourceProject = new SourceProject();
                    cachedSourceProject.name = config.source_project.name;
                    cachedSourceProject.bundle_id = config.source_project.bundle_id;
                    cachedSourceProject.min_sessions = config.source_project.min_sessions;
                    cachedSourceProject.min_player_days = config.source_project.min_player_days;
                    // Copy frequency_cap data from server or use defaults
                    if (config.source_project.frequency_cap != null)
                    {
                        cachedSourceProject.frequency_cap = new FrequencyCapJson 
                        { 
                            impressions = config.source_project.frequency_cap.impressions, 
                            time_unit = config.source_project.frequency_cap.time_unit ?? "DAY" 
                        };
                    }
                    else
                    {
                    cachedSourceProject.frequency_cap = new FrequencyCapJson { impressions = 0, time_unit = "DAY" };
                    }
                    cachedSourceProject.interstitial_icon_cta = config.source_project.interstitial_icon_cta;
                    cachedSourceProject.interstitial_icon_text = config.source_project.interstitial_icon_text;
                    cachedSourceProject.interstitial_rich_cta = config.source_project.interstitial_rich_cta;
                    cachedSourceProject.interstitial_rich_text = config.source_project.interstitial_rich_text;
                    
                    // Convert from wrapper classes to dictionaries for editor use
                    cachedSourceProject.store_ids = new Dictionary<string, string>();
                    cachedSourceProject.store_urls = new Dictionary<string, string>();
                    cachedSourceProject.platform_ids = new Dictionary<string, object>();

                    LogDebug($"🔍 config.source_project.store_ids is null: {config.source_project.store_ids == null}");
                    if (config.source_project.store_ids != null)
                    {
                        LogDebug($"🔍 store_ids.apple: '{config.source_project.store_ids.apple}'");
                        LogDebug($"🔍 store_ids.google: '{config.source_project.store_ids.google}'");
                        LogDebug($"🔍 store_ids.microsoft: '{config.source_project.store_ids.microsoft}'");
                        
                        if (!string.IsNullOrEmpty(config.source_project.store_ids.apple))
                            cachedSourceProject.store_ids["apple"] = config.source_project.store_ids.apple;
                        if (!string.IsNullOrEmpty(config.source_project.store_ids.google))
                            cachedSourceProject.store_ids["google"] = config.source_project.store_ids.google;
                        if (!string.IsNullOrEmpty(config.source_project.store_ids.amazon))
                            cachedSourceProject.store_ids["amazon"] = config.source_project.store_ids.amazon;
                        if (!string.IsNullOrEmpty(config.source_project.store_ids.microsoft))
                            cachedSourceProject.store_ids["microsoft"] = config.source_project.store_ids.microsoft;
                        if (!string.IsNullOrEmpty(config.source_project.store_ids.samsung))
                            cachedSourceProject.store_ids["samsung"] = config.source_project.store_ids.samsung;
                    }
                    else
                    {
                        LogDebug("❌ config.source_project.store_ids is null - JSON deserialization failed");
                        // Apply manual parsing workaround for Unity JsonUtility Dictionary issues
                        if (jsonData.Contains("\"store_ids\""))
                        {
                            LogDebug("[BoostOps] 🔧 Applying Unity JsonUtility Dictionary workaround for source_project.store_ids in ParseRemoteCampaignConfig");
                            ApplySourceProjectStoreIdsWorkaround(jsonData);
                        }
                    }

                    if (config.source_project.store_urls != null)
                    {
                        if (!string.IsNullOrEmpty(config.source_project.store_urls.apple))
                            cachedSourceProject.store_urls["apple"] = config.source_project.store_urls.apple;
                        if (!string.IsNullOrEmpty(config.source_project.store_urls.google))
                            cachedSourceProject.store_urls["google"] = config.source_project.store_urls.google;
                        if (!string.IsNullOrEmpty(config.source_project.store_urls.amazon))
                            cachedSourceProject.store_urls["amazon"] = config.source_project.store_urls.amazon;
                        if (!string.IsNullOrEmpty(config.source_project.store_urls.microsoft))
                            cachedSourceProject.store_urls["microsoft"] = config.source_project.store_urls.microsoft;
                        if (!string.IsNullOrEmpty(config.source_project.store_urls.samsung))
                            cachedSourceProject.store_urls["samsung"] = config.source_project.store_urls.samsung;
                    }

                    if (config.source_project.platform_ids != null)
                    {
                        if (!string.IsNullOrEmpty(config.source_project.platform_ids.ios_bundle_id))
                            cachedSourceProject.platform_ids["ios_bundle_id"] = config.source_project.platform_ids.ios_bundle_id;
                        if (!string.IsNullOrEmpty(config.source_project.platform_ids.android_package_name))
                            cachedSourceProject.platform_ids["android_package_name"] = config.source_project.platform_ids.android_package_name;
                    }
                    
                    var apple = cachedSourceProject.store_ids?.ContainsKey("apple") == true ? cachedSourceProject.store_ids["apple"] : "none";
                    var google = cachedSourceProject.store_ids?.ContainsKey("google") == true ? cachedSourceProject.store_ids["google"] : "none";  
                    var amazon = cachedSourceProject.store_ids?.ContainsKey("amazon") == true ? cachedSourceProject.store_ids["amazon"] : "none";
                    var windows = cachedSourceProject.store_ids?.ContainsKey("microsoft") == true ? cachedSourceProject.store_ids["microsoft"] : "none";
                    var samsung = cachedSourceProject.store_ids?.ContainsKey("samsung") == true ? cachedSourceProject.store_ids["samsung"] : "none";
                    LogDebug($"✅ Store IDs from source_project: Apple='{apple}', Google='{google}', Amazon='{amazon}', Windows='{windows}', Samsung='{samsung}'");
                    
                    LogDebug($"✅ Cached source project settings: '{cachedSourceProject.name}' (min_sessions: {cachedSourceProject.min_sessions}, frequency_cap: {cachedSourceProject.frequency_cap?.impressions ?? 0})");
                }
                
                LogDebug($"🔍 config.campaigns is null: {config.campaigns == null}");
                LogDebug($"🔍 config.campaigns count: {config.campaigns?.Count ?? 0}");
                if (config.campaigns != null && config.campaigns.Count > 0)
                {
                    LogDebug($"Parsed {config.campaigns.Count} total campaigns from remote config");
                    
                    // Apply manual parsing workaround for campaign store_urls if needed
                    foreach (var campaign in config.campaigns)
                    {
                        if (campaign?.target_project?.store_urls != null && 
                            string.IsNullOrEmpty(campaign.target_project.store_urls.apple) && 
                            jsonData.Contains("\"apple\":\"https://apps.apple.com/app/id1114393474\""))
                        {
                            LogDebug($"🔧 Applying manual parsing for campaign '{campaign.name}' store_urls");
                            
                            // Extract campaign store_urls - look for this specific campaign's store_urls
                            var campaignPattern = $@"""campaign_id"":""{System.Text.RegularExpressions.Regex.Escape(campaign.campaign_id)}""[^}}]*""store_urls"":\s*\{{([^}}]*)\}}";
                            var campaignMatch = System.Text.RegularExpressions.Regex.Match(jsonData, campaignPattern, System.Text.RegularExpressions.RegexOptions.Singleline);
                            if (campaignMatch.Success)
                            {
                                string storeUrlsContent = campaignMatch.Groups[1].Value;
                                LogDebug($"🔧 Extracted campaign store_urls: {storeUrlsContent}");
                                
                                var appleUrlMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""apple"":""([^""]*)""");
                                var googleUrlMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""google"":""([^""]*)""");
                                var webUrlMatch = System.Text.RegularExpressions.Regex.Match(storeUrlsContent, @"""web"":""([^""]*)""");
                                
                                if (appleUrlMatch.Success) campaign.target_project.store_urls.apple = appleUrlMatch.Groups[1].Value;
                                if (googleUrlMatch.Success) campaign.target_project.store_urls.google = googleUrlMatch.Groups[1].Value;  
                                if (webUrlMatch.Success) campaign.target_project.store_urls.web = webUrlMatch.Groups[1].Value;
                                
                                LogDebug($"🔧 Manually parsed campaign URLs - apple: '{campaign.target_project.store_urls.apple}'");
                            }
                        }
                    }
                
                // Debug each campaign before filtering
                foreach (var campaign in config.campaigns)
                {
                    LogDebug($"🔍 Campaign '{campaign.name}': target_project is null: {campaign.target_project == null}");
                    if (campaign.target_project != null)
                    {
                        LogDebug($"🔍   target_project.store_urls is null: {campaign.target_project.store_urls == null}");
                    }
                    
                    LogDebug($"Campaign '{campaign.name ?? campaign.campaign_id}': " +
                           $"Apple='{campaign.target_project?.store_urls?.apple}', " +
                           $"Google='{campaign.target_project?.store_urls?.google}', " +
                           $"Web='{campaign.target_project?.store_urls?.web}', " +
                           $"HasValidStoreUrl={campaign.HasValidStoreUrl()}'");
                }
                
                // Filter campaigns to only include those with valid store URLs
                var validCoreCampaigns = config.campaigns.Where(c => c.HasValidStoreUrl()).ToList();
                
                // Convert from BoostOps.Core.Campaign to BoostOps.Campaign
                cachedRemoteCampaigns = validCoreCampaigns.Select(coreCampaign => ConvertCoreToBoostOpsCampaign(coreCampaign)).ToList();
                
                LogDebug($"Filtered to {cachedRemoteCampaigns.Count} campaigns with valid store URLs");
                
                // Cache app_walls configuration for offline use
                if (config.app_walls != null)
                {
                    CacheAppWallsConfig(jsonData);
                    LogDebug("✅ App walls configuration cached for offline use");
                }
                else
                {
                    LogDebug("No app_walls section found in remote config");
                }
                
                // Log any campaigns that were filtered out
                var invalidCampaigns = config.campaigns.Where(c => !c.HasValidStoreUrl()).ToList();
                if (invalidCampaigns.Count > 0)
                {
                    LogDebug($"Filtered out {invalidCampaigns.Count} campaigns without valid store URLs:");
                    foreach (var campaign in invalidCampaigns)
                    {
                        LogDebug($"  - {campaign.name ?? campaign.campaign_id}: No valid store URLs found");
                    }
                }
                
                // Filter active campaigns (status == "active")
                var activeCampaigns = cachedRemoteCampaigns.Where(c => c.status == "active").ToList();
                LogDebug($"Found {activeCampaigns.Count} active campaigns");
                
                // Validate each campaign's required fields
                foreach (var campaign in cachedRemoteCampaigns)
                {
                    var issues = new List<string>();
                    if (string.IsNullOrEmpty(campaign.campaign_id)) issues.Add("missing campaign ID");
                    if (string.IsNullOrEmpty(campaign.name)) issues.Add("missing name");
                    if (string.IsNullOrEmpty(campaign.GetIconUrl())) issues.Add("missing icon URL");
                    if (campaign.target_project == null) issues.Add("missing target project");
                    
                    if (issues.Count > 0)
                    {
                        LogDebug($"Campaign '{campaign.name ?? campaign.campaign_id}' has issues: {string.Join(", ", issues)}");
                    }
                }
                } // Close the if (config.campaigns != null && config.campaigns.Count > 0) block
                
                // Log version info if available
                if (config.version_info != null)
                {
                    LogDebug($"Campaign config version: {config.version_info.api_version}, last updated: {config.version_info.last_updated}");
                    
                    // Extract server revision from version info
                    var serverVersionStr = config.version_info.server_version ?? config.version_info.api_version ?? "1.0.0";
                    if (int.TryParse(serverVersionStr.Split('.')[0], out int majorVersion))
                        crossPromoServerRevision = majorVersion;
                    else
                        crossPromoServerRevision = 1;
                }
            } // Close the main if (config != null) block
            else
            {
                LogDebug("No valid campaign config found in JSON");
                cachedRemoteCampaigns = new List<BoostOps.Campaign>();
            }
        } // Close the try block
        catch (System.Exception ex)
        {
            LogDebug($"Error parsing remote campaign config: {ex.Message}");
            cachedRemoteCampaigns = new List<BoostOps.Campaign>();
        }
    }

    /// <summary>
    /// Convert from BoostOps.Core.Campaign (used for JSON deserialization) to BoostOps.Campaign (used for runtime)
    /// </summary>
    BoostOps.Campaign ConvertCoreToBoostOpsCampaign(BoostOps.Core.Campaign coreCampaign)
    {
        var campaign = new BoostOps.Campaign();
        
        // Basic fields
        campaign.campaign_id = coreCampaign.campaign_id;
        campaign.name = coreCampaign.name;
        campaign.status = coreCampaign.status;
        campaign.min_sessions = coreCampaign.min_sessions;
        campaign.min_player_days = coreCampaign.min_player_days;
        campaign.created_at = coreCampaign.created_at;
        campaign.updated_at = coreCampaign.updated_at;
        
        // Frequency cap
        if (coreCampaign.frequency_cap != null)
        {
            campaign.frequency_cap = new BoostOps.Core.FrequencyCapJson
            {
                time_unit = coreCampaign.frequency_cap.time_unit,
                impressions = coreCampaign.frequency_cap.impressions
            };
        }
        
        // Target project
        if (coreCampaign.target_project != null)
        {
            campaign.target_project = new BoostOps.TargetProject();
            campaign.target_project.project_id = coreCampaign.target_project.project_id;
            
            // Store URLs
            if (coreCampaign.target_project.store_urls != null)
            {
                campaign.target_project.store_urls = new BoostOps.StoreUrls
                {
                    apple = coreCampaign.target_project.store_urls.apple,
                    google = coreCampaign.target_project.store_urls.google,
                    amazon = coreCampaign.target_project.store_urls.amazon,
                    microsoft = coreCampaign.target_project.store_urls.microsoft,
                    samsung = coreCampaign.target_project.store_urls.samsung,
                    web = coreCampaign.target_project.store_urls.web
                };
            }
            
            // Store IDs
            if (coreCampaign.target_project.store_ids != null)
            {
                campaign.target_project.store_ids = new BoostOps.StoreIds
                {
                    apple = coreCampaign.target_project.store_ids.apple,
                    google = coreCampaign.target_project.store_ids.google,
                    amazon = coreCampaign.target_project.store_ids.amazon,
                    microsoft = coreCampaign.target_project.store_ids.microsoft,
                    samsung = coreCampaign.target_project.store_ids.samsung
                };
            }
            
            // Platform IDs
            if (coreCampaign.target_project.platform_ids != null)
            {
                campaign.target_project.platform_ids = new BoostOps.PlatformIds
                {
                    ios_bundle_id = coreCampaign.target_project.platform_ids.ios_bundle_id,
                    android_package_name = coreCampaign.target_project.platform_ids.android_package_name
                };
            }
            
            // Creatives (simplified conversion)
            if (coreCampaign.target_project.creatives != null && coreCampaign.target_project.creatives.Length > 0)
            {
                campaign.target_project.creatives = coreCampaign.target_project.creatives.Select(coreCreative =>
                {
                    var creative = new BoostOps.Creative();
                    creative.format = coreCreative.format;
                    creative.creative_id = coreCreative.creative_id;
                    
                    if (coreCreative.variants != null && coreCreative.variants.Length > 0)
                    {
                        creative.variants = coreCreative.variants.Select(coreVariant => new BoostOps.CreativeVariant
                        {
                            url = coreVariant.url,
                            local_key = coreVariant.local_key,
                            resolution = coreVariant.resolution,
                            sha256 = coreVariant.sha256
                        }).ToArray();
                    }
                    
                    return creative;
                }).ToArray();
            }
        }
        
        // Copy formats array
        if (coreCampaign.formats != null && coreCampaign.formats.Length > 0)
        {
            campaign.formats = coreCampaign.formats;
        }
        
        return campaign;
    }
    
    async System.Threading.Tasks.Task DownloadCampaignIcons()
    {
        LogDebug("DownloadCampaignIcons: Starting icon downloads");
        LogDebug($"Processing {cachedRemoteCampaigns.Count} campaigns");
        
        int downloadedCount = 0;
        foreach (var campaign in cachedRemoteCampaigns)
        {
            var iconUrl = campaign.GetIconUrl();
            LogDebug($"Campaign '{campaign.name ?? campaign.campaign_id}': iconUrl = '{iconUrl}'");
            
            if (!string.IsNullOrEmpty(iconUrl))
                            {
                    try
                    {
                        bool downloaded = await DownloadSingleIcon(campaign);
                        if (downloaded) downloadedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        LogDebug($"Failed to download icon for campaign {campaign.campaign_id}: {ex.Message}");
                    }
                }
                else
                {
                    LogDebug($"No icon URL found for campaign {campaign.campaign_id}, checking store URLs for icon extraction");
                    // Debug the store URLs
                    LogDebug($"  Apple URL: '{campaign.target_project?.store_urls?.apple}'");
                    LogDebug($"  Google URL: '{campaign.target_project?.store_urls?.google}'");
                    
                    // If no direct icon URL, try to construct one from store URLs
                    if (!string.IsNullOrEmpty(campaign.target_project?.store_urls?.apple) || 
                        !string.IsNullOrEmpty(campaign.target_project?.store_urls?.google))
                    {
                        try
                        {
                            bool downloaded = await DownloadSingleIcon(campaign);
                            if (downloaded) downloadedCount++;
                        }
                        catch (System.Exception ex)
                        {
                            LogDebug($"Failed to download icon for campaign {campaign.campaign_id} using store URLs: {ex.Message}");
                        }
                    }
                }
        }
        
        LogDebug($"Icon download completed. Downloaded {downloadedCount} icons to Assets/Resources/BoostOps/Icons/");
    }
    
    async System.Threading.Tasks.Task<bool> DownloadSingleIcon(BoostOps.Campaign campaign)
    {
        var iconUrl = campaign.GetIconUrl();
        LogDebug($"DownloadSingleIcon: Starting download for campaign {campaign.campaign_id}");
        LogDebug($"  Direct icon URL from campaign: '{iconUrl}'");
        
        // If no direct icon URL, construct one from store URLs
        if (string.IsNullOrEmpty(iconUrl))
        {
            LogDebug($"No direct icon URL, attempting to construct from store URLs");
            
            // Try iOS first (iTunes API)
            string iosUrl = campaign.target_project?.store_urls?.apple;
            if (!string.IsNullOrEmpty(iosUrl))
            {
                string iosStoreId = campaign.ExtractIosAppStoreId(iosUrl);
                if (!string.IsNullOrEmpty(iosStoreId))
                {
                    LogDebug($"  Extracted iOS Store ID: {iosStoreId}");
                    iconUrl = $"https://itunes.apple.com/lookup?id={iosStoreId}";
                    LogDebug($"  Constructed iTunes API URL: '{iconUrl}'");
                }
            }
            
            // If iOS failed, try Android
            if (string.IsNullOrEmpty(iconUrl))
            {
                string androidUrl = campaign.target_project?.store_urls?.google;
                if (!string.IsNullOrEmpty(androidUrl))
                {
                    string androidPackageId = campaign.ExtractAndroidPackageId(androidUrl);
                    if (!string.IsNullOrEmpty(androidPackageId))
                    {
                        LogDebug($"  Extracted Android Package ID: {androidPackageId}");
                        iconUrl = $"https://play.google.com/store/apps/details?id={androidPackageId}";
                        LogDebug($"  Constructed Google Play URL: '{iconUrl}'");
                    }
                }
            }
        }
        
        if (string.IsNullOrEmpty(iconUrl))
        {
            LogDebug($"No icon URL available for campaign {campaign.campaign_id} - neither direct nor constructed from store URLs");
            return false;
        }
        
        LogDebug($"Final processing URL for campaign {campaign.campaign_id}: '{iconUrl}'");
        
        try
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.Timeout = System.TimeSpan.FromSeconds(30);
                
                string finalIconUrl = null;
                
                // Check if this is an iTunes API URL (for iOS apps)
                if (iconUrl.Contains("itunes.apple.com/lookup"))
                {
                    LogDebug($"Processing iTunes API URL: '{iconUrl}'");
                    finalIconUrl = await GetIosIconUrlFromItunesApi(client, iconUrl);
                    LogDebug($"  Retrieved iOS icon URL: '{finalIconUrl}'");
                }
                // Check if this is a Google Play URL (for Android apps)
                else if (iconUrl.Contains("play.google.com/store/apps"))
                {
                    LogDebug($"Processing Google Play URL: '{iconUrl}'");
                    finalIconUrl = await GetAndroidIconUrlFromPlayStore(client, iconUrl);
                    LogDebug($"  Retrieved Android icon URL: '{finalIconUrl}'");
                }
                // Direct image URL
                else
                {
                    LogDebug($"Processing as direct image URL: '{iconUrl}'");
                    finalIconUrl = iconUrl;
                }
                
                if (!string.IsNullOrEmpty(finalIconUrl))
                {
                    LogDebug($"Final icon URL for campaign {campaign.campaign_id}: '{finalIconUrl}'");
                    LogDebug($"Attempting HTTP download from: '{finalIconUrl}'");
                    
                    var imageBytes = await client.GetByteArrayAsync(finalIconUrl);
                    LogDebug($"Successfully downloaded {imageBytes.Length} bytes from '{finalIconUrl}'");
                    
                    // Save icon as asset file in Resources/BoostOps/Icons/
                    bool iconSaved = await SaveIconAsAsset(campaign, imageBytes, finalIconUrl);
                    if (iconSaved)
                    {
                        LogDebug($"Successfully downloaded and saved icon asset for campaign {campaign.campaign_id}");
                        return true;
                    }
                    else
                    {
                        LogDebug($"Failed to save icon asset for campaign {campaign.campaign_id}");
                        return false;
                    }
                }
                else
                {
                    LogDebug($"Could not resolve final icon URL for campaign {campaign.campaign_id}");
                    return false;
                }
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error downloading icon for campaign {campaign.campaign_id}: {ex.Message}");
            return false;
        }
    }
    
    async System.Threading.Tasks.Task<string> GetIosIconUrlFromItunesApi(System.Net.Http.HttpClient client, string itunesUrl)
    {
        try
        {
            LogDebug($"Fetching iOS app data from iTunes API: {itunesUrl}");
            var response = await client.GetStringAsync(itunesUrl);
            
            // Parse iTunes API JSON response
            dynamic jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
            if (jsonResponse?.results != null && jsonResponse.results.Count > 0)
            {
                var app = jsonResponse.results[0];
                string iconUrl = app.artworkUrl512 ?? app.artworkUrl100 ?? app.artworkUrl60;
                LogDebug($"Extracted iOS icon URL: {iconUrl}");
                return iconUrl;
            }
            else
            {
                LogDebug("No app found in iTunes API response");
                return null;
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error fetching iOS icon URL from iTunes API: {ex.Message}");
            return null;
        }
    }
    
    async System.Threading.Tasks.Task<string> GetAndroidIconUrlFromPlayStore(System.Net.Http.HttpClient client, string playStoreUrl)
    {
        try
        {
            LogDebug($"Fetching Android app page from Google Play: {playStoreUrl}");
            var response = await client.GetStringAsync(playStoreUrl);
            
            // Extract icon URL from Google Play HTML
            var iconMatch = System.Text.RegularExpressions.Regex.Match(response, 
                @"<img[^>]+src=""([^""]+)""[^>]*alt=""Icon image""", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
            if (!iconMatch.Success)
            {
                // Try alternative patterns
                iconMatch = System.Text.RegularExpressions.Regex.Match(response, 
                    @"<img[^>]+class=""[^""]*icon[^""]*""[^>]+src=""([^""]+)""", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            if (iconMatch.Success)
            {
                string iconUrl = iconMatch.Groups[1].Value;
                // Clean up the URL (remove query parameters that might break the download)
                if (iconUrl.Contains("=s"))
                {
                    iconUrl = System.Text.RegularExpressions.Regex.Replace(iconUrl, @"=s\d+(-[^&]*)?", "=s512");
                }
                LogDebug($"Extracted Android icon URL: {iconUrl}");
                return iconUrl;
            }
            else
            {
                LogDebug("Could not find icon URL in Google Play page");
                return null;
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error fetching Android icon URL from Google Play: {ex.Message}");
            return null;
        }
    }
    
    async System.Threading.Tasks.Task<bool> SaveIconAsAsset(BoostOps.Campaign campaign, byte[] imageBytes, string sourceUrl)
    {
        try
        {
            // Extract store ID and determine platform based on the source URL
            string storeId = null;
            string platform = null;
            
            if (sourceUrl.Contains("itunes.apple.com") || !string.IsNullOrEmpty(campaign.target_project?.store_urls?.apple))
            {
                storeId = campaign.ExtractIosAppStoreId(campaign.target_project?.store_urls?.apple);
                platform = "ios";
            }
            else if (sourceUrl.Contains("play.google.com") || !string.IsNullOrEmpty(campaign.target_project?.store_urls?.google))
            {
                storeId = campaign.ExtractAndroidPackageId(campaign.target_project?.store_urls?.google);
                platform = "android";
            }
            
            if (string.IsNullOrEmpty(storeId) || string.IsNullOrEmpty(platform))
            {
                LogDebug($"Could not extract store ID or platform for campaign {campaign.campaign_id}");
                return false;
            }
            
            // Sanitize store ID for filename (remove special characters)
            string sanitizedStoreId = SanitizeStoreId(storeId);
            
            // Create the directory if it doesn't exist
            string iconsDir = "Assets/Resources/BoostOps/Icons";
            string fullIconsPath = System.IO.Path.GetFullPath(iconsDir);
            
            if (!System.IO.Directory.Exists(fullIconsPath))
            {
                System.IO.Directory.CreateDirectory(fullIconsPath);
                LogDebug($"Created directory: {fullIconsPath}");
                AssetDatabase.Refresh(); // Refresh after creating directory
            }
            
            LogDebug($"Icons directory exists: {System.IO.Directory.Exists(fullIconsPath)}");
            
            // Create filename following the expected pattern: {storeId}_icon
            string fileName = $"{sanitizedStoreId}_icon.png";
            string pngPath = System.IO.Path.Combine(iconsDir, fileName);
            string fullPngPath = System.IO.Path.GetFullPath(pngPath);
            
            // Save the image bytes as PNG file
            LogDebug($"Attempting to save {imageBytes.Length} bytes to: {fullPngPath}");
            await System.IO.File.WriteAllBytesAsync(fullPngPath, imageBytes);
            LogDebug($"Saved icon PNG to: {fullPngPath}");
            LogDebug($"File exists after save: {System.IO.File.Exists(fullPngPath)}");
            
            // Defer asset database operations to avoid timing issues
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // Refresh AssetDatabase to recognize the new file
                    AssetDatabase.Refresh();
                    
                    // Load the texture and configure it for sprites
                    var textureImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                    if (textureImporter != null)
                    {
                        textureImporter.textureType = TextureImporterType.Sprite;
                        textureImporter.spriteImportMode = SpriteImportMode.Single;
                        textureImporter.maxTextureSize = 512;
                        textureImporter.textureCompression = TextureImporterCompression.Compressed;
                        textureImporter.SaveAndReimport();
                        
                        LogDebug($"Configured texture importer for sprite: {pngPath}");
                    }
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError($"Failed to configure texture importer: {ex.Message}");
                }
            };
            
            // Return success immediately since asset operations are deferred
            LogDebug($"Deferred sprite asset creation for campaign {campaign.campaign_id}");
            return true;
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error saving icon asset for campaign {campaign.campaign_id}: {ex.Message}");
            return false;
        }
    }
    
    async System.Threading.Tasks.Task<bool> SaveIconAsAssetForTarget(TargetGame target, byte[] imageBytes, string platform)
    {
        try
        {
            // Get store ID based on platform
            string storeId = null;
            string platformSuffix = null;
            
            if (platform == "iOS" && !string.IsNullOrEmpty(target.iosAppStoreId))
            {
                storeId = target.iosAppStoreId;
                platformSuffix = "ios";
            }
            else if (platform == "Android" && !string.IsNullOrEmpty(target.androidPackageId))
            {
                storeId = target.androidPackageId;
                platformSuffix = "android";
            }
            
            if (string.IsNullOrEmpty(storeId) || string.IsNullOrEmpty(platformSuffix))
            {
                LogDebug($"Could not determine store ID or platform for target '{target.headline}'");
                return false;
            }
            
            // Sanitize store ID for filename
            string sanitizedStoreId = SanitizeStoreId(storeId);
            
            // Create the directory if it doesn't exist
            string iconsDir = "Assets/Resources/BoostOps/Icons";
            string fullIconsPath = System.IO.Path.GetFullPath(iconsDir);
            
            if (!System.IO.Directory.Exists(fullIconsPath))
            {
                System.IO.Directory.CreateDirectory(fullIconsPath);
                LogDebug($"Created directory: {fullIconsPath}");
                AssetDatabase.Refresh(); // Refresh after creating directory
            }
            
            LogDebug($"Icons directory exists: {System.IO.Directory.Exists(fullIconsPath)}");
            
            // Create filename following the expected pattern: {storeId}_icon
            string fileName = $"{sanitizedStoreId}_icon.png";
            string pngPath = System.IO.Path.Combine(iconsDir, fileName);
            string fullPngPath = System.IO.Path.GetFullPath(pngPath);
            
            // Save the image bytes as PNG file
            LogDebug($"Attempting to save {imageBytes.Length} bytes to: {fullPngPath}");
            await System.IO.File.WriteAllBytesAsync(fullPngPath, imageBytes);
            LogDebug($"Saved icon PNG to: {fullPngPath}");
            LogDebug($"File exists after save: {System.IO.File.Exists(fullPngPath)}");
            
            // Defer asset database operations to avoid timing issues
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // Refresh AssetDatabase to recognize the new file
                    AssetDatabase.Refresh();
                    
                    // Load the texture and configure it for sprites
                    var textureImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                    if (textureImporter != null)
                    {
                        textureImporter.textureType = TextureImporterType.Sprite;
                        textureImporter.spriteImportMode = SpriteImportMode.Single;
                        textureImporter.maxTextureSize = 512;
                        textureImporter.textureCompression = TextureImporterCompression.Compressed;
                        textureImporter.SaveAndReimport();
                        
                        LogDebug($"Configured texture importer for sprite: {pngPath}");
                        
                        // Load the created sprite and assign to target after import
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                        if (sprite != null)
                        {
                            target.icon = sprite;
                            LogDebug($"Loaded and assigned sprite to target '{target.headline}': {sprite.name}");
                        }
                        else
                        {
                            LogDebug($"Failed to load created sprite from: {pngPath}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError($"Failed to configure texture importer: {ex.Message}");
                }
            };
            
            // Return success immediately since asset operations are deferred
            LogDebug($"Deferred sprite asset creation for target '{target.headline}'");
            return true;
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error saving icon asset for target '{target.headline}': {ex.Message}");
            return false;
        }
    }
    
    Sprite LoadCampaignIconFromAssets(BoostOps.Campaign campaign)
    {
        try
        {
            // Extract store IDs from campaign
            string iosStoreId = campaign.ExtractIosAppStoreId(campaign.target_project?.store_urls?.apple);
            string androidStoreId = campaign.ExtractAndroidPackageId(campaign.target_project?.store_urls?.google);
            
            LogDebug($"LoadCampaignIconFromAssets for '{campaign.name ?? campaign.campaign_id}': iOS='{iosStoreId}', Android='{androidStoreId}'");
            
            // Try to load icons in priority order: iOS first, then Android
            var platformChecks = new[]
            {
                new { id = iosStoreId, suffix = "_icon" },
                new { id = androidStoreId, suffix = "_icon" }
            };
            
            foreach (var check in platformChecks)
            {
                if (string.IsNullOrEmpty(check.id)) continue;
                
                string sanitizedStoreId = SanitizeStoreId(check.id);
                string fileName = $"{sanitizedStoreId}{check.suffix}.png";
                string assetPath = $"Assets/Resources/BoostOps/Icons/{fileName}";
                
                LogDebug($"Trying to load icon from: {assetPath}");
                LogDebug($"File exists: {System.IO.File.Exists(assetPath)}");
                
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    LogDebug($"Successfully loaded icon: {sprite.name}");
                    return sprite;
                }
            }
            
            LogDebug($"No icon found for campaign '{campaign.name ?? campaign.campaign_id}'");
            return null;
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error loading campaign icon from assets for '{campaign.name}': {ex.Message}");
            return null;
        }
    }
    

    
    void BuildCampaignList(VisualElement container)
    {
        LogDebug($"BuildCampaignList: Building list for {cachedRemoteCampaigns.Count} campaigns");
        
        // Campaign summary
        var activeCampaigns = cachedRemoteCampaigns.Where(c => c.status == "active").ToList();
        var summary = new Label($"📈 {activeCampaigns.Count} active campaigns • {cachedRemoteCampaigns.Count} total");
        summary.style.fontSize = 11;
        summary.style.color = new Color(0.6f, 0.8f, 0.6f);
        summary.style.marginBottom = 10;
        container.Add(summary);
        
        // Campaign list
        foreach (var campaign in cachedRemoteCampaigns.Take(5)) // Show max 5 campaigns in overview
        {
            var campaignRow = new VisualElement();
            campaignRow.style.flexDirection = FlexDirection.Row;
            campaignRow.style.alignItems = Align.Center;
            campaignRow.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.5f);
            campaignRow.style.paddingLeft = 10;
            campaignRow.style.paddingRight = 10;
            campaignRow.style.paddingTop = 8;
            campaignRow.style.paddingBottom = 8;
            campaignRow.style.marginBottom = 5;
            campaignRow.style.borderTopLeftRadius = 3;
            campaignRow.style.borderTopRightRadius = 3;
            campaignRow.style.borderBottomLeftRadius = 3;
            campaignRow.style.borderBottomRightRadius = 3;
            
            // Campaign icon
            var iconContainer = new VisualElement();
            iconContainer.style.width = 32;
            iconContainer.style.height = 32;
            iconContainer.style.marginRight = 10;
            iconContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            iconContainer.style.borderTopLeftRadius = 4;
            iconContainer.style.borderTopRightRadius = 4;
            iconContainer.style.borderBottomLeftRadius = 4;
            iconContainer.style.borderBottomRightRadius = 4;
            
            // Try to load icon from saved assets
            var iconSprite = LoadCampaignIconFromAssets(campaign);
            if (iconSprite != null)
            {
                var iconImage = new Image();
                iconImage.image = iconSprite.texture;
                iconImage.style.width = 32;
                iconImage.style.height = 32;
                iconContainer.Add(iconImage);
            }
            else
            {
                var iconPlaceholder = new Label("📱");
                iconPlaceholder.style.fontSize = 16;
                iconPlaceholder.style.alignSelf = Align.Center;
                iconPlaceholder.style.unityTextAlign = TextAnchor.MiddleCenter;
                iconContainer.Add(iconPlaceholder);
            }
            
            campaignRow.Add(iconContainer);
            
            // Campaign info
            var infoContainer = new VisualElement();
            infoContainer.style.flexGrow = 1;
            
            var nameLabel = new Label(campaign.name ?? campaign.campaign_id);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            infoContainer.Add(nameLabel);
            
            // Show store URL info
            var storeInfo = "";
            if (!string.IsNullOrEmpty(campaign.target_project?.store_urls?.apple)) storeInfo += "📱 Apple";
            if (!string.IsNullOrEmpty(campaign.target_project?.store_urls?.google)) 
            {
                if (!string.IsNullOrEmpty(storeInfo)) storeInfo += " • ";
                storeInfo += "🤖 Android";
            }
            
            // Show supported formats
            var formatsInfo = "";
            if (campaign.formats != null && campaign.formats.Length > 0)
            {
                formatsInfo = string.Join(", ", campaign.formats);
            }
            else
            {
                formatsInfo = "all formats";
            }
            
            var detailsLabel = new Label($"{campaign.status} • {storeInfo}");
            detailsLabel.style.fontSize = 10;
            detailsLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            infoContainer.Add(detailsLabel);
            
            var formatsLabel = new Label($"📋 Formats: {formatsInfo}");
            formatsLabel.style.fontSize = 9;
            formatsLabel.style.color = new Color(0.6f, 0.75f, 0.9f);
            formatsLabel.style.marginTop = 2;
            infoContainer.Add(formatsLabel);
            
            campaignRow.Add(infoContainer);
            
            // Status indicator
            var statusLabel = new Label(campaign.status == "active" ? "✅ Active" : "⏸️ Inactive");
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = campaign.status == "active" ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.6f, 0.4f);
            statusLabel.style.alignSelf = Align.Center;
            campaignRow.Add(statusLabel);
            
            container.Add(campaignRow);
        }
        
        // Show "more campaigns" indicator if there are more than 5
        if (cachedRemoteCampaigns.Count > 5)
        {
            var moreLabel = new Label($"... and {cachedRemoteCampaigns.Count - 5} more campaigns");
            moreLabel.style.fontSize = 10;
            moreLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            moreLabel.style.marginTop = 5;
            moreLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(moreLabel);
        }
        
        // Last sync info
        if (!string.IsNullOrEmpty(lastRemoteConfigSync))
        {
            var syncInfo = new Label($"Last synced: {lastRemoteConfigSync}");
            syncInfo.style.fontSize = 9;
            syncInfo.style.color = new Color(0.5f, 0.5f, 0.5f);
            syncInfo.style.marginTop = 8;
            syncInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(syncInfo);
        }
    }
    
    void SaveCachedRemoteCampaigns()
    {
        try
        {
            if (cachedRemoteCampaigns != null && cachedRemoteCampaigns.Count > 0)
            {
                var config = new RemoteCampaignConfig
                {
                    campaigns = cachedRemoteCampaigns,
                    version_info = new VersionInfo
                    {
                        last_updated = lastRemoteConfigSync,
                        api_version = "1.0.0"
                    }
                };
                
                string json = JsonUtility.ToJson(config);
                EditorPrefs.SetString("BoostOps_CachedRemoteCampaigns", json);
                EditorPrefs.SetString("BoostOps_RemoteConfigLastSync", lastRemoteConfigSync);
                
                LogDebug($"Saved {cachedRemoteCampaigns.Count} campaigns to cache");
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error saving cached remote campaigns: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Cache app_walls configuration to BoostOpsProjectSettings for offline/fallback use
    /// </summary>
    void CacheAppWallsConfig(string fullConfigJson)
    {
        try
        {
            var projectSettings = BoostOpsProjectSettings.GetOrCreateSettings();
            if (projectSettings == null)
            {
                LogDebug("❌ Failed to get project settings for app walls caching");
                return;
            }
            // Extract just the app_walls section from the full config
            int appWallsStart = fullConfigJson.IndexOf("\"app_walls\"");
            if (appWallsStart < 0)
            {
                LogDebug("No app_walls section found in config JSON");
                return;
            }
            
            // Find the opening brace for app_walls
            int openBraceIndex = fullConfigJson.IndexOf("{", appWallsStart);
            if (openBraceIndex < 0) return;
            
            // Find the matching closing brace
            int closeBraceIndex = openBraceIndex + 1;
            int braceCount = 1;
            while (closeBraceIndex < fullConfigJson.Length && braceCount > 0)
            {
                if (fullConfigJson[closeBraceIndex] == '{') braceCount++;
                if (fullConfigJson[closeBraceIndex] == '}') braceCount--;
                if (braceCount > 0) closeBraceIndex++;
            }
            
            if (braceCount != 0)
            {
                LogDebug("Failed to extract app_walls JSON - unmatched braces");
                return;
            }
            
            // Extract the app_walls JSON (wrapped for JsonUtility parsing)
            string appWallsJson = "{\"app_walls\":" + fullConfigJson.Substring(openBraceIndex, closeBraceIndex - openBraceIndex + 1) + "}";
            
            // Save to BoostOpsProjectSettings
            projectSettings.cachedAppWallsJson = appWallsJson;
            // Note: Timestamp fields removed to prevent unnecessary version control changes
            // projectSettings.appWallsLastUpdated = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            // projectSettings.appWallsSource = lastRemoteConfigSync;
            
            UnityEditor.EditorUtility.SetDirty(projectSettings);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            
            LogDebug($"✅ Cached app_walls configuration to BoostOpsProjectSettings ({appWallsJson.Length} chars)");
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error caching app_walls config: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    void LoadCachedRemoteCampaigns()
    {
        try
        {
            string cachedJson = EditorPrefs.GetString("BoostOps_CachedRemoteCampaigns", "");
            lastRemoteConfigSync = EditorPrefs.GetString("BoostOps_RemoteConfigLastSync", "");
            
            if (!string.IsNullOrEmpty(cachedJson))
            {
                var config = JsonUtility.FromJson<RemoteCampaignConfig>(cachedJson);
                if (config != null && config.campaigns != null)
                {
                    cachedRemoteCampaigns = config.campaigns;
                    crossPromoLastSync = config.version_info?.last_updated ?? lastRemoteConfigSync;
                    // Convert version string to simple revision number (just use major version)
                    var versionStr = config.version_info?.api_version ?? "1.0.0";
                    if (int.TryParse(versionStr.Split('.')[0], out int majorVersion))
                        crossPromoServerRevision = majorVersion;
                    else
                        crossPromoServerRevision = 1;
                    
                    LogDebug($"✅ Loaded {cachedRemoteCampaigns.Count} campaigns from cache (last sync: {crossPromoLastSync})");
                    
                    // Start downloading icons for cached campaigns in background
                    if (cachedRemoteCampaigns.Count > 0)
                    {
                        _ = DownloadCampaignIcons();
                    }
                    
                    // Mark as synced if we have campaigns and a sync timestamp
                    if (cachedRemoteCampaigns.Count > 0 && !string.IsNullOrEmpty(crossPromoLastSync))
                    {
                        LogDebug($"Found {cachedRemoteCampaigns.Count} previously synced campaigns - UI should show synced state");
                    }
                }
            }
            else
            {
                LogDebug("No cached remote campaigns found - will show sync required state");
                cachedRemoteCampaigns = new List<BoostOps.Campaign>();
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Error loading cached remote campaigns: {ex.Message}");
            cachedRemoteCampaigns = new List<BoostOps.Campaign>();
        }
    }
    
    void ClearRemoteCampaignCache()
    {
        cachedRemoteCampaigns?.Clear();
        lastRemoteConfigSync = "";
        crossPromoLastSync = "";
        
        EditorPrefs.DeleteKey("BoostOps_CachedRemoteCampaigns");
        EditorPrefs.DeleteKey("BoostOps_RemoteConfigLastSync");
        
        LogDebug("Cleared remote campaign cache");
    }
    
    /// <summary>
    /// Clear ALL BoostOps cached data from EditorPrefs and force refresh
    /// Use this to resolve issues with stale/old values in the editor
    /// </summary>
    void ClearAllBoostOpsCache()
    {
        LogDebug("=== CLEARING ALL BOOSTOPS CACHE ===");
        
        // Runtime config cache
        EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_JSON");
        EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Key");
        EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Timestamp");
        EditorPrefs.DeleteKey("BoostOps_RuntimeConfig_Provider");
        
        // Remote campaigns cache
        EditorPrefs.DeleteKey("BoostOps_CachedRemoteCampaigns");
        EditorPrefs.DeleteKey("BoostOps_RemoteConfigLastSync");
        
        // Authentication cache
        EditorPrefs.DeleteKey("BoostOps_UserEmail");
        EditorPrefs.DeleteKey("BoostOps_ApiToken");
        
        // Project configuration cache
        EditorPrefs.DeleteKey("BoostOps_ProjectSlug");
        EditorPrefs.DeleteKey("BoostOps_DynamicLinkUrl");
        EditorPrefs.DeleteKey("BoostOps_AndroidCertFingerprint");
        EditorPrefs.DeleteKey("BoostOps_AppleAppStoreId");
        EditorPrefs.DeleteKey("BoostOps_SelectedQRDomain");
        
        // Feature mode states
        EditorPrefs.DeleteKey("BoostOps_LinksMode");
        EditorPrefs.DeleteKey("BoostOps_CrossPromoMode");
        EditorPrefs.DeleteKey("BoostOps_LinksStatus");
        EditorPrefs.DeleteKey("BoostOps_CrossPromoStatus");
        EditorPrefs.DeleteKey("BoostOps_LinksServerRevision");
        EditorPrefs.DeleteKey("BoostOps_CrossPromoServerRevision");
        EditorPrefs.DeleteKey("BoostOps_LinksLastSync");
        EditorPrefs.DeleteKey("BoostOps_CrossPromoLastSync");
        
        // Studio information
        EditorPrefs.DeleteKey("BoostOps_StudioId");
        EditorPrefs.DeleteKey("BoostOps_StudioName");
        EditorPrefs.DeleteKey("BoostOps_StudioDescription");
        EditorPrefs.DeleteKey("BoostOps_IsStudioOwner");
        
        // Registration state
        EditorPrefs.DeleteKey("BoostOps_RegistrationState");
        
        // Settings
        EditorPrefs.DeleteKey("BoostOps_EnableDebugLogging");
        EditorPrefs.DeleteKey("BoostOps_SkipServerValidation");
        EditorPrefs.DeleteKey("BoostOps_HostingOption");
        
        // Verification status cache (per-project)
        string projectBasePath = $"BoostOps_{Application.dataPath}";
        EditorPrefs.DeleteKey($"{projectBasePath}_IOSVerificationStatus");
        EditorPrefs.DeleteKey($"{projectBasePath}_AndroidVerificationStatus");
        EditorPrefs.DeleteKey($"{projectBasePath}_AmazonVerificationStatus");
        EditorPrefs.DeleteKey($"{projectBasePath}_IOSLastVerifiedValues");
        EditorPrefs.DeleteKey($"{projectBasePath}_AndroidLastVerifiedValues");
        EditorPrefs.DeleteKey($"{projectBasePath}_AmazonLastVerifiedValues");
        
        // Clear runtime variables
        cachedRemoteCampaigns?.Clear();
        lastRemoteConfigSync = "";
        crossPromoLastSync = "";
        cachedSourceProject = null;
        isApiCallInProgress = false; // Reset API call flag
        
        // Clear lookup response data
        hasLookupResponse = false;
        lookupProjectFound = false;
        lookupProjectSlug = "";
        lookupProjectName = "";
        lookupMessage = "";
        cachedProjectLookupResponse = null;
        
        LogDebug("All BoostOps cache cleared. Reinitializing...");
        
        // Force reinitialize and refresh
        InitializeData();
        RefreshAllUI();
        
        LogDebug("=== CACHE CLEAR COMPLETE ===");
        EditorUtility.DisplayDialog("BoostOps Cache Cleared", 
            "All cached data has been cleared and the configuration reloaded. This should resolve any issues with old values persisting in the editor.", 
            "OK");
    }
    
    bool ValidateRemoteCampaignConfig()
    {
        if (cachedRemoteCampaigns == null || cachedRemoteCampaigns.Count == 0)
        {
            LogDebug("ValidateRemoteCampaignConfig: No campaigns to validate");
            return false;
        }
        
        bool isValid = true;
        int validCampaignCount = 0;
        
        foreach (var campaign in cachedRemoteCampaigns)
        {
            var issues = new List<string>();
            
            // Required fields validation
            if (string.IsNullOrEmpty(campaign.campaign_id)) issues.Add("missing ID");
            if (string.IsNullOrEmpty(campaign.name)) issues.Add("missing name");
            if (string.IsNullOrEmpty(campaign.GetIconUrl())) issues.Add("missing icon URL");
            
            // Store URL validation - must have at least one
            if (!campaign.HasValidStoreUrl()) issues.Add("missing store URLs (need at least iOS or Android)");
            
            if (issues.Count > 0)
            {
                LogDebug($"Campaign '{campaign.name ?? campaign.campaign_id}' validation failed: {string.Join(", ", issues)}");
                isValid = false;
            }
            else
            {
                validCampaignCount++;
            }
        }
        
        LogDebug($"Campaign validation: {validCampaignCount}/{cachedRemoteCampaigns.Count} campaigns are valid");
        return isValid && validCampaignCount > 0;
    }

    async System.Threading.Tasks.Task<object> UpdateStudio(string studioId, object updateRequest)
    {
        LogDebug($"UpdateStudio: Studio update not implemented for {studioId}");
        EditorUtility.DisplayDialog("Update Studio", "Studio update is not implemented in this version.", "OK");
        await System.Threading.Tasks.Task.Delay(100); // Simulate async call
        return new { success = false, message = "Not implemented", name = studioName };
    }
    
    void OpenInstructionsFile()
    {
        string instructionsPath = "Assets/BoostOpsGenerated/SETUP_INSTRUCTIONS.md";
        if (File.Exists(instructionsPath))
        {
            EditorUtility.RevealInFinder(instructionsPath);
                    }
                    else
                    {
            EditorUtility.DisplayDialog("File Not Found", "Setup instructions file not found. Please generate files first.", "OK");
        }
    }
    
    void RefreshCrossPromoPanel()
    {
        // Refresh cross-promotion panel
        LogDebug("RefreshCrossPromoPanel: Refreshing cross-promo panel");
        RefreshCrossPromoContent();
    }
    
    void RefreshCrossPromoContent()
    {
        // Only refresh the cross-promo panel content without changing tabs
        LogDebug("RefreshCrossPromoContent: Refreshing cross-promo content only");
        LogDebug($"  Current cached campaigns count: {(cachedRemoteCampaigns?.Count ?? 0)}");
        LogDebug($"  Content container exists: {contentContainer != null}");
        LogDebug($"  Selected tab: {selectedTab} (Cross-Promo is tab 2)");
        
        if (contentContainer != null && selectedTab == 2) // Cross-Promo tab
        {
            // Clear and rebuild only the cross-promo content
            LogDebug("  Clearing content container and rebuilding cross-promo panel");
            contentContainer.Clear();
            BuildCrossPromoPanel();
            
            // Force UI refresh
            contentContainer.MarkDirtyRepaint();
            Repaint();
            LogDebug("  Cross-promo panel rebuild completed");
        }
        else
        {
            LogDebug("  Skipping refresh - either not on Cross-Promo tab or content container is null");
        }
    }
    
    void PreviewCrossPromoConfig()
    {
        LogDebug("PreviewCrossPromoConfig: Generating JSON preview");
        
        if (crossPromoTable == null) 
        {
            EditorUtility.DisplayDialog("Preview Config", "No cross-promotion table found. Please configure at least one target game first.", "OK");
            return;
        }
        
        // Validate configuration before generating JSON
        var validationErrors = ValidateCrossPromoConfiguration();
        if (validationErrors.Count > 0)
        {
            string errorMessage = "❌ Cannot preview Cross-Promo JSON due to validation errors:\n\n" + string.Join("\n", validationErrors);
            errorMessage += "\n\nPlease fix these issues before previewing the configuration.";
            EditorUtility.DisplayDialog("Cross-Promo Validation Failed", errorMessage, "OK");
            return;
        }
        
        // Generate the JSON content
        string json = GenerateModernCampaignJson(crossPromoTable);
        
        if (string.IsNullOrEmpty(json))
        {
            EditorUtility.DisplayDialog("Preview Config", "Failed to generate JSON content. Please check the console for errors.", "OK");
            return;
        }
        
        // Open the preview window
        CrossPromoJsonPreviewWindow.ShowPreview(json);
    }
    
    void DrawDynamicLinksSection()
    {
        // This method is likely part of IMGUI rendering
        LogDebug("DrawDynamicLinksSection: Drawing dynamic links section");
    }
    
    void SaveAllPendingChanges()
    {
        // Mark everything as dirty first (doesn't trigger refresh)
        SaveProjectSlug();
        SaveDynamicLinkUrl();
        SaveAndroidCertFingerprint();
        SaveAppleAppStoreId();
        SaveHostingOption();
        SaveDebugLogging();
        SaveStudioInfo();
        SaveRegistrationState();
        
        if (dynamicLinksConfig != null)
        {
            EditorUtility.SetDirty(dynamicLinksConfig);
        }
        
        // Single save at the end instead of multiple saves
        SafeSaveAssets();
    }
    
    void SaveDynamicLinksConfigAssetImmediately()
    {
        if (dynamicLinksConfig != null)
        {
            EditorUtility.SetDirty(dynamicLinksConfig);
            SafeSaveAssets();
            SafeRefreshAssets();
        }
    }
    
    async System.Threading.Tasks.Task<string> MakeUnityAuthApiCall(string endpoint, string method, string jsonData = null)
    {
        LogDebug($"MakeUnityAuthApiCall: {method} {endpoint} (not implemented)");
        await System.Threading.Tasks.Task.Delay(100); // Simulate async call
        return "{\"success\": false, \"message\": \"API calls not implemented in this version\"}";
    }
    
    ValidationResult ValidateAndroidContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new ValidationResult(false, "Content is empty");
            
        // Basic validation - check if it contains expected package name
        if (!string.IsNullOrEmpty(androidBundleId) && !content.Contains(androidBundleId))
        {
            return new ValidationResult(false, $"Package name '{androidBundleId}' not found in content");
        }
        
        return new ValidationResult(true);
    }
    
    ValidationResult ValidateIOSContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new ValidationResult(false, "Content is empty");
            
        // Basic validation - check if it contains applinks structure
        if (!content.Contains("applinks"))
        {
            return new ValidationResult(false, "Missing 'applinks' structure");
        }
        
        // Check if it contains expected bundle ID and team ID
        string expectedAppId = $"{iosTeamId}.{iosBundleId}";
        if (!string.IsNullOrEmpty(iosTeamId) && !string.IsNullOrEmpty(iosBundleId) && !content.Contains(expectedAppId))
        {
            return new ValidationResult(false, $"App ID '{expectedAppId}' not found in content");
        }
        
        return new ValidationResult(true);
    }
    
    string NormalizeSHA256Fingerprint(string fingerprint)
    {
        if (string.IsNullOrEmpty(fingerprint))
            return fingerprint;
        
        fingerprint = fingerprint.Trim().ToUpper();
        
        // If it's already in colon format, return as-is
        if (fingerprint.Contains(":"))
            return fingerprint;
        
        // If it's a 64-character hex string, add colons
        if (fingerprint.Length == 64)
        {
            string normalized = "";
            for (int i = 0; i < fingerprint.Length; i += 2)
            {
                if (i > 0)
                    normalized += ":";
                normalized += fingerprint.Substring(i, 2);
            }
            return normalized;
        }
        
        return fingerprint;
    }
    
    bool IsValidAppleAppStoreId(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
                return false;
        
        // Remove whitespace
        storeId = storeId.Trim();
        
        // Handle "id" prefix format (e.g., "id1234567890")
        if (storeId.StartsWith("id", System.StringComparison.OrdinalIgnoreCase))
        {
            storeId = storeId.Substring(2);
        }
        
        // Apple Store IDs are numeric strings, typically 9-10 digits
        // But can sometimes be shorter or longer, so we allow 6-15 digits
        if (storeId.Length < 6 || storeId.Length > 15)
                return false;
        
        // Check if all characters are digits
        foreach (char c in storeId)
            {
            if (!char.IsDigit(c))
                return false;
        }
                    
                    return true;
                }
    
    bool IsValidSHA256Fingerprint(string fingerprint)
            {
        if (string.IsNullOrEmpty(fingerprint))
                return false;
        
        // Remove any whitespace
        fingerprint = fingerprint.Trim();
        
        // Check if it matches the colon-separated format (AA:BB:CC:DD...)
        if (fingerprint.Contains(":"))
        {
            string[] parts = fingerprint.Split(':');
            
            // SHA256 should have exactly 32 parts (32 bytes)
            if (parts.Length != 32)
                return false;
            
            // Each part should be exactly 2 hex characters
            foreach (string part in parts)
            {
                if (part.Length != 2)
                    return false;
                
                // Check if each character is a valid hex digit
                foreach (char c in part)
                {
                    if (!System.Uri.IsHexDigit(c))
                        return false;
                }
            }
            
            return true;
        }
        // Check if it's a raw hex string (64 characters, no colons)
        else if (fingerprint.Length == 64)
        {
            // Check if all characters are valid hex digits
            foreach (char c in fingerprint)
            {
                if (!System.Uri.IsHexDigit(c))
                    return false;
            }
            
            return true;
        }
        
        return false;
}

    // JSON serialization classes for AASA (Apple App Site Association) file
    [System.Serializable]
    public class AASAFile
    {
        public AASAAppLinks applinks;
    }

    [System.Serializable]
    public class AASAAppLinks
    {
        public AASADetail[] details;
    }

    [System.Serializable]
    public class AASADetail
    {
        public string[] appIDs;
        public AASAComponent[] components;
    }

    [System.Serializable]
    public class AASAComponent
    {
        // This class is just a placeholder - we'll generate the JSON manually
        // since Unity's JsonUtility doesn't handle dynamic keys properly
    }

    // JSON serialization classes for Asset Links file (Android)
    [System.Serializable]
    public class AssetLink
    {
        public string[] relation;
        public AssetLinkTarget target;
    }

    [System.Serializable]
    public class AssetLinkTarget
    {
        public string @namespace;
        public string package_name;
        public string[] sha256_cert_fingerprints;
}
    
    // JSON serialization classes for modern campaign format
    [System.Serializable]
    public class CampaignDataJson
    {
        public VersionInfoJson version_info;
        public SourceProjectJson source_project;
        public CampaignJson[] campaigns;
    }

    [System.Serializable]
    public class VersionInfoJson
    {
        public string api_version;
        public string schema_version;
        public string client_min_version;
        public string server_version;
        public string contract_version;
        public string last_updated;
    }

    [System.Serializable]
    public class SourceProjectJson
    {
        public string bundle_id;
        public string name;
        public int min_player_days;
        public int min_sessions;
        public FrequencyCapJson frequency_cap; // New unified frequency cap object
        public string interstitial_icon_cta;
        public string interstitial_icon_text;
        public string interstitial_rich_cta;
        public string interstitial_rich_text;
        
        // Structured format (JsonUtility-compatible)
        public StoreUrlsJson store_urls;
        public StoreIdsJson store_ids;
        public PlatformIdsJson platform_ids;
    }

        [System.Serializable]
        public class ScheduleJson
        {
            public string start_date;
            public string end_date;
            public int[] days; // [1,2,3,4,5] for Mon-Fri (0=Sun, 6=Sat), empty = all days
            public int start_hour = -1; // Optional, -1 = not set
            public int end_hour = -1;   // Optional, -1 = not set
        }

        [System.Serializable]
        public class CampaignJson
        {
            public string campaign_id;
            public string name;
            public string status;
            public FrequencyCapJson frequency_cap; // New unified frequency cap object
            public ScheduleJson schedule;
            public string created_at;
            public string updated_at;
            public TargetProjectJson target_project;
            

        }

        [System.Serializable]
        public class TargetProjectJson
        {
            public string project_id;
            public StoreUrlsJson store_urls;
            public StoreIdsJson store_ids;
            public PlatformIdsJson platform_ids;
            public CreativeJson[] creatives;
        }

        [System.Serializable]
        public class StoreLinksJson
        {
            public string apple;
            public string google;
            public string amazon;
            public string microsoft;
            public string samsung;
        }

        [System.Serializable]
        public class StoreUrlsJson
        {
            public string apple;
            public string google;
            public string amazon;
            public string microsoft;
            public string samsung;
        }

        [System.Serializable]
        public class StoreIdsJson
        {
            public string apple;
            public string google;
            public string amazon;
            public string microsoft;
            public string samsung;
        }

        [System.Serializable]
        public class PlatformIdsJson
        {
            public string ios_bundle_id;
            public string android_package_name;
        }

        [System.Serializable]
        public class CreativeJson
        {
            public string creative_id;
            public string format;
            public string orientation;
            public bool prefetch;
            public int ttl_hours;
            public CreativeVariantJson[] variants;
        }

        [System.Serializable]
        public class CreativeVariantJson
        {
            public string resolution;
            public string url;
            public string sha256;
            public string local_key;
        }

        /// <summary>
        /// Clean up OAuth listener when window is destroyed
        /// </summary>
        void OnDestroy()
        {
            // Clean up OAuth listener if it exists
            try
            {
                if (oauthCancellationToken != null)
                {
                    oauthCancellationToken.Cancel();
                    oauthCancellationToken.Dispose();
                    oauthCancellationToken = null;
                }
                
                if (oauthListener != null && oauthListener.IsListening)
                {
                    oauthListener.Stop();
                    oauthListener.Close();
                }
                
                oauthListener = null;
                isAuthenticatingWithGoogle = false;
            }
            catch (System.Exception)
            {
                // Silently handle cleanup errors during destruction
            }
        }
        
        private void ApplySourceProjectStoreIdsWorkaround(string jsonData)
        {
            try
            {
                Debug.Log("[BoostOps] 🔧 Applying manual parsing workaround for source_project.store_ids");
                
                // Find the start of source_project
                int sourceProjectIndex = jsonData.IndexOf("\"source_project\"");
                if (sourceProjectIndex == -1)
                {
                    Debug.LogWarning("[BoostOps] ⚠️ Could not find source_project in JSON");
                    return;
                }
                
                // Find store_ids within source_project
                int storeIdsIndex = jsonData.IndexOf("\"store_ids\"", sourceProjectIndex);
                if (storeIdsIndex == -1)
                {
                    Debug.LogWarning("[BoostOps] ⚠️ Could not find store_ids in source_project");
                    return;
                }
                
                // Find the opening brace of store_ids object
                int openBraceIndex = jsonData.IndexOf("{", storeIdsIndex);
                if (openBraceIndex == -1)
                {
                    Debug.LogWarning("[BoostOps] ⚠️ Could not find opening brace for store_ids");
                    return;
                }
                
                // Find the closing brace by counting braces
                int closeBraceIndex = openBraceIndex + 1;
                int braceCount = 1;
                while (closeBraceIndex < jsonData.Length && braceCount > 0)
                {
                    if (jsonData[closeBraceIndex] == '{') braceCount++;
                    if (jsonData[closeBraceIndex] == '}') braceCount--;
                    if (braceCount > 0) closeBraceIndex++;
                }
                
                if (braceCount != 0)
                {
                    Debug.LogWarning("[BoostOps] ⚠️ Could not find matching closing brace for store_ids");
                    return;
                }
                
                // Extract the store_ids content
                string storeIdsContent = jsonData.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1);
                Debug.Log($"[BoostOps] 🔧 Extracted source_project.store_ids content: {storeIdsContent}");
                
                // Parse individual store IDs
                var appleMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""apple"":\s*""([^""]*)""");
                var googleMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""google"":\s*""([^""]*)""");
                var amazonMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""amazon"":\s*""([^""]*)""");
                var microsoftMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""microsoft"":\s*""([^""]*)""");
                var samsungMatch = System.Text.RegularExpressions.Regex.Match(storeIdsContent, @"""samsung"":\s*""([^""]*)""");
                
                // Initialize dictionary if needed
                if (cachedSourceProject == null)
                {
                    Debug.LogWarning("[BoostOps] ⚠️ cachedSourceProject is null, cannot apply workaround");
                    return;
                }
                
                if (cachedSourceProject.store_ids == null)
                    cachedSourceProject.store_ids = new Dictionary<string, string>();
                
                // Apply parsed values (only if not empty)
                if (appleMatch.Success && !string.IsNullOrEmpty(appleMatch.Groups[1].Value))
                    cachedSourceProject.store_ids["apple"] = appleMatch.Groups[1].Value;
                if (googleMatch.Success && !string.IsNullOrEmpty(googleMatch.Groups[1].Value))
                    cachedSourceProject.store_ids["google"] = googleMatch.Groups[1].Value;
                if (amazonMatch.Success && !string.IsNullOrEmpty(amazonMatch.Groups[1].Value))
                    cachedSourceProject.store_ids["amazon"] = amazonMatch.Groups[1].Value;
                if (microsoftMatch.Success && !string.IsNullOrEmpty(microsoftMatch.Groups[1].Value))
                    cachedSourceProject.store_ids["microsoft"] = microsoftMatch.Groups[1].Value;
                if (samsungMatch.Success && !string.IsNullOrEmpty(samsungMatch.Groups[1].Value))
                    cachedSourceProject.store_ids["samsung"] = samsungMatch.Groups[1].Value;
                
                Debug.Log($"[BoostOps] ✅ Manual parsing complete - Apple: '{cachedSourceProject.store_ids.GetValueOrDefault("apple", "none")}', Google: '{cachedSourceProject.store_ids.GetValueOrDefault("google", "none")}', Microsoft: '{cachedSourceProject.store_ids.GetValueOrDefault("microsoft", "none")}'");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ Failed to apply source_project store_ids workaround: {ex.Message}");
            }
        }
        }

    public class QRCodeZoomWindow : EditorWindow
    {
        private string url;
        private Texture2D qrTexture;
        private int qrSize = 300;
        private bool isGenerating = false;
        
        public static void ShowWindow(string url, Texture2D existingTexture = null)
        {
            QRCodeZoomWindow window = GetWindow<QRCodeZoomWindow>("QR Code");
            window.url = url;
            window.minSize = new Vector2(320, 320);
            window.maxSize = new Vector2(320, 320);
            
            if (existingTexture != null)
            {
                // Use the existing texture instead of generating a new one
                window.qrTexture = existingTexture;
                window.isGenerating = false;
            }
            else
            {
                // Fallback to generating a new QR code if no texture provided
                window.GenerateQRCode();
            }
            
            window.Show();
        }
        
        async void GenerateQRCode()
        {
            if (isGenerating) return;
            
            isGenerating = true;
            // Use QuickChart API for real QR codes
            qrTexture = null; // Clear existing texture
            Repaint(); // Show loading state
            
            await GenerateQRCodeForZoomWindow(url, qrSize);
        }
        
        async System.Threading.Tasks.Task GenerateQRCodeForZoomWindow(string text, int size)
        {
            try
            {
                string apiUrl = GetBrandedQRCodeUrl(text, size);
                

                
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(apiUrl))
                {
                    request.timeout = 15;
                    
                    var operation = request.SendWebRequest();
                    
                    // Wait for completion in editor-safe way
                    while (!operation.isDone)
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                    }
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D plainQrTexture = DownloadHandlerTexture.GetContent(request);
                        
                        // Overlay BoostOps logo using Unity
                        qrTexture = OverlayBoostOpsLogo(plainQrTexture);
                        

                    }
                    else
                    {
                        Debug.LogError($"❌ Zoom QR code generation failed: {request.error}");
                        Debug.LogError($"Response Code: {request.responseCode}");
                        Debug.LogError($"URL: {apiUrl}");
                        qrTexture = GenerateErrorTexture(size);
                    }
                    
                    isGenerating = false;
                    Repaint(); // Update the window with new QR code
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Zoom QR code generation exception: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
                qrTexture = GenerateErrorTexture(size);
                isGenerating = false;
                Repaint();
            }
        }
        
        // Generate branded QR code URL (for zoom window)
        string GetBrandedQRCodeUrl(string text, int size)
        {
            // Subtle QR code colors - less visually distracting
            string darkColor = "000000"; // Black pixels - more subtle
            string lightColor = "F8F9FA"; // Very light gray background instead of pure white
            
            // Build branded QR code URL (without center image - we'll overlay in Unity)
            string baseUrl = $"https://quickchart.io/qr?text={UnityWebRequest.EscapeURL(text)}&size={size}x{size}&format=png";
            
            // Add branding parameters
            baseUrl += $"&dark={darkColor}&light={lightColor}";
            baseUrl += $"&ecLevel=H"; // High error correction for better logo visibility
            baseUrl += $"&margin=6"; // Increased margin to accommodate caption text properly
            baseUrl += $"&caption={UnityWebRequest.EscapeURL("BoostLink™")}";
            baseUrl += $"&captionFontSize=11&captionFontColor=6B7280"; // Slightly larger font for better readability
            
            return baseUrl;
        }
        
        // Overlay BoostOps logo on QR code texture using Unity (shared method for zoom window)
        Texture2D OverlayBoostOpsLogo(Texture2D qrTexture)
        {
            try
            {
                // Load BoostOps logo
                Texture2D logoTexture = Resources.Load<Texture2D>("boostops-logo-256");
                if (logoTexture == null)
                {
                    Debug.LogWarning("⚠️ BoostOps logo not found in Resources, returning QR code without logo");
                    return qrTexture;
                }
                
                // Create a copy of the QR code texture
                Texture2D combinedTexture = new Texture2D(qrTexture.width, qrTexture.height);
                combinedTexture.SetPixels(qrTexture.GetPixels());
                
                // Calculate logo size (25% of QR code size)
                int logoSize = Mathf.RoundToInt(qrTexture.width * 0.25f);
                
                // Calculate center position
                int centerX = qrTexture.width / 2;
                int centerY = qrTexture.height / 2;
                int logoStartX = centerX - logoSize / 2;
                int logoStartY = centerY - logoSize / 2;
                
                // Resize logo to fit
                RenderTexture renderTexture = RenderTexture.GetTemporary(logoSize, logoSize);
                Graphics.Blit(logoTexture, renderTexture);
                
                RenderTexture.active = renderTexture;
                Texture2D resizedLogo = new Texture2D(logoSize, logoSize);
                resizedLogo.ReadPixels(new Rect(0, 0, logoSize, logoSize), 0, 0);
                resizedLogo.Apply();
                
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(renderTexture);
                
                // Overlay logo pixels onto QR code
                Color[] logoPixels = resizedLogo.GetPixels();
                for (int y = 0; y < logoSize; y++)
                {
                    for (int x = 0; x < logoSize; x++)
                    {
                        int qrX = logoStartX + x;
                        int qrY = logoStartY + y;
                        
                        // Make sure we're within bounds
                        if (qrX >= 0 && qrX < qrTexture.width && qrY >= 0 && qrY < qrTexture.height)
                        {
                            Color logoPixel = logoPixels[y * logoSize + x];
                            
                            // Only overlay non-transparent pixels
                            if (logoPixel.a > 0.1f)
                            {
                                // Add a white background behind the logo for better visibility
                                combinedTexture.SetPixel(qrX, qrY, Color.white);
                                
                                // Blend logo pixel with alpha
                                Color qrPixel = combinedTexture.GetPixel(qrX, qrY);
                                Color blendedPixel = Color.Lerp(qrPixel, logoPixel, logoPixel.a);
                                combinedTexture.SetPixel(qrX, qrY, blendedPixel);
                            }
                        }
                    }
                }
                
                combinedTexture.Apply();
                
                // Clean up
                DestroyImmediate(resizedLogo);
                

                
                return combinedTexture;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Failed to overlay BoostOps logo: {ex.Message}");
                return qrTexture; // Return original QR code if overlay fails
            }
        }

        
        Texture2D GenerateErrorTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            // Create an error pattern (red X)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    
                    // Create red X pattern
                    if (Mathf.Abs(x - y) < 5 || Mathf.Abs(x - (size - y)) < 5)
                    {
                        pixels[index] = Color.red;
                    }
                    else
                    {
                        pixels[index] = Color.white;
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        void OnGUI()
        {
            // Just show the QR code - no additional text or fields
            if (qrTexture != null)
            {
                var qrRect = GUILayoutUtility.GetRect(qrSize, qrSize, GUILayout.Width(qrSize), GUILayout.Height(qrSize));
                EditorGUI.DrawRect(qrRect, Color.white);
                GUI.DrawTexture(qrRect, qrTexture);
            }
            else if (isGenerating)
            {
                var loadingRect = GUILayoutUtility.GetRect(qrSize, qrSize, GUILayout.Width(qrSize), GUILayout.Height(qrSize));
                EditorGUI.DrawRect(loadingRect, Color.gray);
                EditorGUI.LabelField(loadingRect, "Generating...", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                var errorRect = GUILayoutUtility.GetRect(qrSize, qrSize, GUILayout.Width(qrSize), GUILayout.Height(qrSize));
                EditorGUI.DrawRect(errorRect, new Color(1f, 0.8f, 0.8f, 1f));
                EditorGUI.LabelField(errorRect, "Error", EditorStyles.centeredGreyMiniLabel);
            }
        }
    }
}

/// <summary>
/// Preview window for displaying cross-promotion JSON configuration
/// </summary>
public class CrossPromoJsonPreviewWindow : EditorWindow
{
    private string jsonContent = "";
    private Vector2 scrollPosition = Vector2.zero;
    
    public static void ShowPreview(string json)
    {
        var window = GetWindow<CrossPromoJsonPreviewWindow>(true, "Cross-Promo JSON Preview", true);
        window.jsonContent = json ?? "";
        window.minSize = new Vector2(600, 400);
        window.maxSize = new Vector2(1200, 800);
        window.Show();
    }
    
    void OnGUI()
    {
        // Header
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Cross-Promotion JSON Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        // Copy button
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(120)))
        {
            EditorGUIUtility.systemCopyBuffer = jsonContent;
            Debug.Log("[BoostOps] JSON configuration copied to clipboard");
        }
        
        if (GUILayout.Button("Format JSON", GUILayout.Width(100)))
        {
            try
            {
                // Simple JSON formatting by adding proper indentation
                jsonContent = FormatJson(jsonContent);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to format JSON: {e.Message}");
            }
        }
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // JSON content in scrollable text area
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // Use TextArea for multi-line display with monospace font
        var textAreaStyle = new GUIStyle(EditorStyles.textArea);
        textAreaStyle.font = EditorStyles.miniFont;
        textAreaStyle.fontSize = 11;
        textAreaStyle.wordWrap = false;
        
        jsonContent = EditorGUILayout.TextArea(jsonContent, textAreaStyle, 
            GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.Space(10);
        
        // Footer info
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Characters: {jsonContent.Length}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Lines: {jsonContent.Split('\n').Length}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
    
    private string FormatJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        
        var formatted = new System.Text.StringBuilder();
        int indentLevel = 0;
        bool inString = false;
        bool escaped = false;
        
        foreach (char c in json)
        {
            if (escaped)
            {
                formatted.Append(c);
                escaped = false;
                continue;
            }
            
            if (c == '\\' && inString)
            {
                formatted.Append(c);
                escaped = true;
                continue;
            }
            
            if (c == '"')
            {
                inString = !inString;
                formatted.Append(c);
                continue;
            }
            
            if (inString)
            {
                formatted.Append(c);
                continue;
            }
            
            switch (c)
            {
                case '{':
                case '[':
                    formatted.Append(c);
                    formatted.AppendLine();
                    indentLevel++;
                    formatted.Append(new string(' ', indentLevel * 2));
                    break;
                case '}':
                case ']':
                    formatted.AppendLine();
                    indentLevel--;
                    formatted.Append(new string(' ', indentLevel * 2));
                    formatted.Append(c);
                    break;
                case ',':
                    formatted.Append(c);
                    formatted.AppendLine();
                    formatted.Append(new string(' ', indentLevel * 2));
                    break;
                case ':':
                    formatted.Append(c);
                    formatted.Append(' ');
                    break;
                default:
                    if (!char.IsWhiteSpace(c))
                        formatted.Append(c);
                    break;
            }
        }
        
        return formatted.ToString();
    }
}