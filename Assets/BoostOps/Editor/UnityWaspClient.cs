using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Text.RegularExpressions;

namespace BoostOps
{
    /// <summary>
    /// Unity client for BoostOps API endpoints
    /// </summary>
    public class UnityWaspClient
    {
        private string productionUrl = "https://unity-api.boostops.io"; // Production URL
        private string jwtToken;
        
        /// <summary>
        /// Initialize the client with JWT token
        /// </summary>
        /// <param name="token">JWT token from OAuth flow</param>
        public void Initialize(string token)
        {
            jwtToken = token;
            Debug.Log($"[UnityWaspClient] Initialized with token: {token?.Substring(0, Math.Min(20, token?.Length ?? 0))}...");
        }
        

        
        /// <summary>
        /// Get user information using API endpoint
        /// Includes studio information for security and reduced information leakage:
        /// - Studio name and description for UI display
        /// - No internal database IDs exposed
        /// - Studio will be null if user has no studio
        /// </summary>
        /// <returns>User information with studio context or null if failed</returns>
        public async Task<WaspUserInfo> GetUserInfo()
        {
            Debug.Log($"[UnityWaspClient] GetUserInfo called with token: {(string.IsNullOrEmpty(jwtToken) ? "NULL/EMPTY" : jwtToken.Substring(0, Math.Min(20, jwtToken.Length)) + "...")}");
            
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[UnityWaspClient] JWT token is null or empty. Please call Initialize() first.");
                return null;
            }
            
            var requestData = new UserInfoRequest
            {
                jwt_token = this.jwtToken
            };
            
            var response = await CallAPI("POST", "/api/unity/user-info", requestData);
            if (response == null) return null;
            
            return JsonUtility.FromJson<WaspUserInfo>(response);
        }
        
        /// <summary>
        /// Check if project is already registered using lookup endpoint
        /// </summary>
        /// <returns>Project info if found, null if not registered</returns>
        public async Task<WaspProjectInfo> LookupProject()
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[UnityWaspClient] JWT token is null or empty. Please call Initialize() first.");
                return null;
            }
            
            var projectIds = GetUnityProjectIdentifiers();
            Debug.Log($"[UnityWaspClient] Looking up project with ProductGuid: {projectIds.productGuid}, CloudProjectId: {projectIds.cloudProjectId}");
            
            // Call the lookup API with both IDs (let server handle which one to use)
            var projectInfo = await LookupByUnityIds(projectIds.productGuid, projectIds.cloudProjectId);
            if (projectInfo != null)
            {
                Debug.Log($"[UnityWaspClient] ✅ Project found: {projectInfo.name}");
                return projectInfo;
            }
            
            Debug.Log("[UnityWaspClient] ❌ Project not found in lookup");
            return null;
        }
        
        /// <summary>
        /// Check if project is already registered (convenience method)
        /// </summary>
        /// <returns>True if project is registered, false if not</returns>
        public async Task<bool> IsProjectRegistered()
        {
            var projectInfo = await LookupProject();
            return projectInfo != null;
        }
        
        /// <summary>
        /// Lookup project by Unity IDs using correct POST API
        /// </summary>
        /// <param name="productGuid">Unity product GUID</param>
        /// <param name="cloudProjectId">Unity cloud project ID</param>
        /// <returns>Project info or null if not found</returns>
        private async Task<WaspProjectInfo> LookupByUnityIds(string productGuid, string cloudProjectId)
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[UnityWaspClient] JWT token is null or empty. Please call Initialize() first.");
                return null;
            }
            
            var requestData = new ProjectLookupRequest
            {
                jwt_token = this.jwtToken,
                product_guid = productGuid ?? "",
                cloud_project_id = cloudProjectId ?? ""
            };
            
            var response = await CallAPI("POST", "/api/unity/project/lookup", requestData);
            if (response == null) return null;
            
            try
            {
                var lookupResponse = JsonUtility.FromJson<WaspProjectLookupResponse>(response);
                return lookupResponse.found ? lookupResponse.project : null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UnityWaspClient] Failed to parse lookup response: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Register Unity project using API endpoint
        /// </summary>
        /// <param name="projectName">Name of the Unity project</param>
        /// <param name="bundleId">iOS bundle ID</param>
        /// <param name="androidPackageName">Android package name</param>
        /// <returns>Registration response or null if failed</returns>
        public async Task<WaspProjectRegistration> RegisterProject(string projectName, string bundleId = null, string androidPackageName = null)
        {
            return await RegisterProject(projectName, bundleId, null, null, androidPackageName, null, null);
        }

        /// <summary>
        /// Register Unity project with complete platform information
        /// </summary>
        /// <param name="projectName">Name of the Unity project</param>
        /// <param name="iosBundleId">iOS bundle ID</param>
        /// <param name="appleTeamId">Apple Team ID</param>
        /// <param name="appleStoreId">Apple App Store ID</param>
        /// <param name="androidPackageName">Android package name</param>
        /// <param name="androidSha256Fingerprints">Array of Android certificate SHA256 fingerprints</param>
        /// <param name="firebaseProjectId">Firebase project ID</param>
        /// <returns>Registration response or null if failed</returns>
        public async Task<WaspProjectRegistration> RegisterProject(
            string projectName, 
            string iosBundleId = null, 
            string appleTeamId = null, 
            string appleStoreId = null, 
            string androidPackageName = null, 
            string[] androidSha256Fingerprints = null, 
            string firebaseProjectId = null)
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[UnityWaspClient] JWT token is null or empty. Please call Initialize() first.");
                return null;
            }
            
            var projectIds = GetUnityProjectIdentifiers();
            
            var requestData = new ProjectRegistrationRequest
            {
                jwt_token = this.jwtToken,
                project_name = projectName,
                product_guid = projectIds.productGuid,
                cloud_project_id = projectIds.cloudProjectId,
                
                // iOS Platform
                ios_bundle_id = iosBundleId ?? "",
                apple_team_id = appleTeamId ?? "",
                apple_app_store_id = appleStoreId ?? "",
                
                // Android Platform  
                android_package_name = androidPackageName ?? "",
                android_sha256_fingerprints = androidSha256Fingerprints ?? new string[0],
                
                // Integrations
                firebase_project_id = firebaseProjectId ?? ""
            };
            
            var response = await CallAPI("POST", "/api/unity/project/register", requestData);
            if (response == null) return null;
            
            return JsonUtility.FromJson<WaspProjectRegistration>(response);
        }
        
        /// <summary>
        /// Get SDK key using API endpoint
        /// </summary>
        /// <returns>SDK key response or null if failed</returns>
        public async Task<WaspSDKKeyResponse> GetSDKKey()
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[UnityWaspClient] JWT token is null or empty. Please call Initialize() first.");
                return null;
            }
            
            var requestData = new SDKKeyRequest
            {
                jwt_token = this.jwtToken
            };
            
            var response = await CallAPI("POST", "/api/unity/sdk-key", requestData);
            if (response == null) return null;
            
            return JsonUtility.FromJson<WaspSDKKeyResponse>(response);
        }
        
        /// <summary>
        /// Get all registered apps for the studio with multi-platform icon URLs
        /// </summary>
        /// <param name="iconSize">Icon size preference: "256x256", "512x512", or "1024x1024"</param>
        /// <returns>RegisteredAppsResponse with all studio apps and platform info</returns>
        public async Task<RegisteredAppsResponse> GetRegisteredApps(string iconSize = "512x512")
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[UnityWaspClient] JWT token is null or empty. Please call Initialize() first.");
                return null;
            }
            
            var requestData = new RegisteredAppsRequest
            {
                jwt_token = this.jwtToken,
                icon_size = iconSize
            };
            
            Debug.Log($"[UnityWaspClient] Fetching registered apps with icon size: {iconSize}");
            var response = await CallAPI("POST", "/api/unity/registered-apps", requestData);
            if (response == null) return null;
            
            try
            {
                var result = JsonUtility.FromJson<RegisteredAppsResponse>(response);
                Debug.Log($"[UnityWaspClient] Successfully parsed {result.totalApps} registered apps");
                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UnityWaspClient] Failed to parse registered apps response: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Activate project slug to link existing project with BoostLink™
        /// </summary>
        /// <param name="projectSlug">The project slug to activate</param>
        /// <returns>Slug activation response or null if failed</returns>
        public async Task<WaspSlugActivationResponse> ActivateProjectSlug(string projectSlug)
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[UnityWaspClient] JWT token is null or empty. Please call Initialize() first.");
                return null;
            }
            
            if (string.IsNullOrEmpty(projectSlug))
            {
                Debug.LogError("[UnityWaspClient] Project slug cannot be null or empty.");
                return null;
            }
            
            var projectIds = GetUnityProjectIdentifiers();
            
            var requestData = new SlugActivationRequest
            {
                jwt_token = this.jwtToken,
                project_slug = projectSlug,
                product_guid = projectIds.productGuid,          // Unity's product GUID (preferred for projects not yet linked to Unity Cloud)
                cloud_project_id = projectIds.cloudProjectId    // Unity Cloud Project ID (if available)
            };
            
            Debug.Log($"[UnityWaspClient] Activating project slug '{projectSlug}' with ProductGuid: {projectIds.productGuid}, CloudProjectId: {projectIds.cloudProjectId ?? "Not connected"}");
            
            var response = await CallAPI("POST", "/api/unity/project/activate-slug", requestData);
            if (response == null) return null;
            
            return JsonUtility.FromJson<WaspSlugActivationResponse>(response);
        }
        
        /// <summary>
        /// Get the active slug for the current Unity project using the lookup endpoint
        /// </summary>
        /// <returns>Active slug response or null if failed</returns>
        public async Task<WaspActiveSlugResponse> GetActiveSlug()
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[UnityWaspClient] JWT token is null or empty. Please call Initialize() first.");
                return null;
            }
            
            var projectIds = GetUnityProjectIdentifiers();
            Debug.Log($"[UnityWaspClient] Getting active slug via lookup endpoint with ProductGuid: {projectIds.productGuid}, CloudProjectId: {projectIds.cloudProjectId ?? "Not connected"}");
            
            // Use the existing lookup endpoint to get project information
            var fullLookupResponse = await LookupProjectFull();
            if (fullLookupResponse == null || !fullLookupResponse.found)
            {
                Debug.Log($"[UnityWaspClient] ❌ No project found in lookup");
                return new WaspActiveSlugResponse
                {
                    success = true,
                    hasSlug = false,
                    projectSlug = null,
                    isDynamicLinksActive = false,
                    message = "Project not found"
                };
            }
            
            // Extract slug information from lookup response
            string projectSlug = fullLookupResponse.project_slug;
            bool isDynamicLinksActive = !string.IsNullOrEmpty(projectSlug);
            
            Debug.Log($"[UnityWaspClient] ✅ Project found via lookup: '{fullLookupResponse.project?.name}'");
            Debug.Log($"[UnityWaspClient] 🔍 Project slug from API: '{projectSlug ?? "null"}'");
            Debug.Log($"[UnityWaspClient] 🔗 Dynamic links active: {isDynamicLinksActive}");
            
            return new WaspActiveSlugResponse
            {
                success = true,
                hasSlug = !string.IsNullOrEmpty(projectSlug),
                projectSlug = projectSlug,
                isDynamicLinksActive = isDynamicLinksActive,
                message = fullLookupResponse.found ? "Project found" : "Project not found"
            };
        }
        
        /// <summary>
        /// Get full project lookup response
        /// </summary>
        /// <returns>Full lookup response or null if failed</returns>
        public async Task<WaspProjectLookupResponse> LookupProjectFull()
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                return null;
            }
            
            var projectIds = GetUnityProjectIdentifiers();
            var requestData = new ProjectLookupRequest
            {
                jwt_token = this.jwtToken,
                product_guid = projectIds.productGuid ?? "",
                cloud_project_id = projectIds.cloudProjectId ?? ""
            };
            
            var response = await CallAPI("POST", "/api/unity/project/lookup", requestData);
            if (response == null) return null;
            
            // Log the full response for debugging
            Debug.Log($"[UnityWaspClient] 📋 Full lookup response: {response}");
            
            try
            {
                var lookupResponse = JsonUtility.FromJson<WaspProjectLookupResponse>(response);
                // Store raw response for complex JSON extraction
                if (lookupResponse != null)
                {
                    lookupResponse.rawResponse = response;
                }
                return lookupResponse;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UnityWaspClient] Failed to parse full lookup response: {ex.Message}");
                Debug.LogError($"[UnityWaspClient] Raw response was: {response}");
                return null;
            }
        }
        
        /// <summary>
        /// Get Unity project identifiers (productGuid + cloudProjectId)
        /// </summary>
        /// <returns>Project identifiers</returns>
        private ProjectIdentifiers GetUnityProjectIdentifiers()
        {
            var identifiers = new ProjectIdentifiers();
            
            // Get productGuid from ProjectSettings.asset (always present)
            identifiers.productGuid = GetProjectGuidFromSettings();
            
            // Get cloudProjectId if connected to Unity Services (optional)
            identifiers.cloudProjectId = Application.cloudProjectId;
            
            Debug.Log($"[UnityWaspClient] Project identifiers - ProductGuid: {identifiers.productGuid}, CloudProjectId: {identifiers.cloudProjectId ?? "Not connected"}");
            
            return identifiers;
                        }
        
        /// <summary>
        /// Read productGuid from ProjectSettings.asset
        /// </summary>
        /// <returns>Product GUID or null if not found</returns>
        private string GetProjectGuidFromSettings()
        {
            try
            {
                string projectSettingsPath = Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset");
                if (File.Exists(projectSettingsPath))
                {
                    string content = File.ReadAllText(projectSettingsPath);
                    var match = Regex.Match(content, @"productGUID:\s*([a-f0-9]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            catch (System.Exception ex)
                        {
                Debug.LogWarning($"[UnityWaspClient] Failed to read project GUID: {ex.Message}");
                        }
                        
                        return null;
                    }
        
        /// <summary>
        /// Make HTTP API call to server
        /// </summary>
        /// <param name="method">HTTP method (GET, POST, etc.)</param>
        /// <param name="endpoint">API endpoint path</param>
        /// <param name="requestData">Data to send in request body</param>
        /// <returns>Response text or null if failed</returns>
        private async Task<string> CallAPI(string method, string endpoint, object requestData)
            {
            string url = $"{productionUrl}{endpoint}";
            
            Debug.Log($"[UnityWaspClient] URL: {url}");
            
            var request = new UnityWebRequest(url, method);
            request.SetRequestHeader("Content-Type", "application/json");
            
            if (requestData != null)
            {
                string jsonData = JsonUtility.ToJson(requestData);
                Debug.Log($"[UnityWaspClient] Request data: {jsonData}");
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }
            
            request.downloadHandler = new DownloadHandlerBuffer();
            
            await request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[UnityWaspClient] ❌ API call failed: {request.error}");
                Debug.LogError($"[UnityWaspClient] Response code: {request.responseCode}");
                Debug.LogError($"[UnityWaspClient] Response body: {request.downloadHandler.text}");
                return null;
            }
            
            Debug.Log($"[UnityWaspClient] ✅ API call successful");
            Debug.Log($"[UnityWaspClient] Response: {request.downloadHandler.text}");
            return request.downloadHandler.text;
        }
    }
    
    // Helper class for project identifiers
    [System.Serializable]
    public class ProjectIdentifiers
    {
        public string productGuid;
        public string cloudProjectId;
    }
    
    // Request Data Models
    [System.Serializable]
    public class UserInfoRequest
    {
        public string jwt_token;
    }
    
    [System.Serializable]
    public class ProjectRegistrationRequest
    {
        public string jwt_token;
        public string project_name;
        public string product_guid;        // Always present - Unity's internal project GUID
        public string cloud_project_id;     // Optional - only when connected to Unity Services
        
        // iOS Platform (all required if any provided)
        public string ios_bundle_id;
        public string apple_team_id;
        public string apple_app_store_id;
        
        // Android Platform (all required if any provided)
        public string android_package_name;
        public string[] android_sha256_fingerprints;
        
        // Integrations (Firebase project ID if available)
        public string firebase_project_id;
    }
    
    [System.Serializable]
    public class ProjectLookupRequest
    {
        public string jwt_token;
        public string product_guid;        // Unity's internal project GUID
        public string cloud_project_id;     // Unity Cloud Project ID (if available)
    }
    
    [System.Serializable]
    public class SDKKeyRequest
    {
        public string jwt_token;
    }
    
    [System.Serializable]
    public class SlugActivationRequest
    {
        public string jwt_token;
        public string project_slug;
        public string product_guid;      // Unity's product GUID (preferred for projects not yet linked to Unity Cloud)
        public string cloud_project_id;  // Unity Cloud Project ID (if available)
    }
    
    [System.Serializable]
    public class RegisteredAppsRequest
    {
        public string jwt_token;
        public string icon_size = "512x512";  // optional: "256x256", "512x512", "1024x1024"
    }
    
    [System.Serializable]
    public class RegisteredAppsResponse
    {
        public bool success;
        public RegisteredApp[] apps;
        public int totalApps;
        public string iconSize;
        public int totalEstimatedBytes;
        public float totalEstimatedMB;
        public StudioInfo studio;
    }
    
    [System.Serializable]
    public class RegisteredApp
    {
        public string id;               // Project ID
        public string name;             // Project name
        public string description;      // Project description
        public AppPlatform bestAppStore; // Highest priority app store with icon (renamed from bestPlatform)
        public AppPlatform[] allAppStores; // All app stores sorted by priority (renamed from allPlatforms)
        public int estimatedBytes;      // Estimated download size for icon
        
        // Backward compatibility properties
        public AppPlatform bestPlatform => bestAppStore;
        public AppPlatform[] allPlatforms => allAppStores;
    }
    
    [System.Serializable]
    public class AppPlatform
    {
        public string type;             // "APPLE_STORE", "GOOGLE_STORE", etc. (updated names)
        public string iconUrl;          // Direct download URL for icon
        public string cachedIconPath;   // Server-side cached path (may be null)
        public bool isPriority;         // true for Apple Store (preferred)
        public string storeId;          // The actual store ID (iOS: App Store ID, Android: package name)
        
        // New store-specific ID fields
        public string appleStoreId;         // Apple App Store ID
        public string bundleId;             // iOS bundle ID
        public string packageName;         // Android package name
        public string amazonAsin;          // Amazon ASIN
        public string microsoftProductId;  // Microsoft Store product ID
        public string samsungPackageName;  // Samsung Galaxy Store package name
    }
    
    [System.Serializable]
    public class StudioInfo
    {
        public string id;
        public string name;
        public string description;
    }

    // Response Data Models
    [System.Serializable]
    public class WaspUserInfo
    {
        public int id;
        public string email;
        public string displayName;
        public bool isAuthenticated;
        public WaspStudioInfo studio; // Can be null if user has no studio
    }

    [System.Serializable]
    public class WaspStudioInfo
    {
        public string name;
        public string description;
    }

    [System.Serializable]
    public class WaspProjectInfo
    {
        public string id;
        public string name;
        public string description;
        public string project_type;  // Note: snake_case in API
        public bool is_active;       // Note: snake_case in API
        public string studio_id;     // Note: snake_case in API
        public string created_at;    // Note: snake_case in API
        public string updated_at;    // Note: snake_case in API
        public string project_key;   // ← ADDED: For project key from server response
        public WaspBoostOpsConfig boostops_config; // Nested boostops_config object
    }
    
    [System.Serializable]
    public class WaspBoostOpsConfig
    {
        public WaspCampaign[] campaigns;
        public WaspVersionInfo version_info;
        public WaspSourceProject source_project;
    }
    
    [System.Serializable]
    public class WaspCampaign
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
        public WaspTargetProject target_project;
        public int daily_impression_cap;
    }
    
    [System.Serializable]
    public class WaspTargetProject
    {
        public WaspCreative[] creatives;
        public string project_id;
        public WaspStoreUrls store_urls;
        public Dictionary<string, string> store_ids;
        public Dictionary<string, object> platform_ids;
    }
    
    [System.Serializable]
    public class WaspCreative
    {
        public string cta_text;
        public string asset_url;
        public string description;
        public string creative_type;
    }
    
    [System.Serializable]
    public class WaspStoreUrls
    {
        public string ios;
        public string android;
    }
    
    [System.Serializable]
    public class WaspVersionInfo
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
    public class WaspSourceProject
    {
        public string name;
        public string bundle_id;
        public int min_sessions;
        public int min_player_days;
        public WaspFrequencyCap frequency_cap;
        public Dictionary<string, string> store_urls;
        public Dictionary<string, string> store_ids;
        public Dictionary<string, object> platform_ids;
        public string interstitial_icon_cta;
        public string interstitial_rich_cta;
        public string interstitial_icon_text;
        public string interstitial_rich_text;
    }
    
    [System.Serializable]
    public class WaspFrequencyCap
    {
        public string time_unit;
        public int impressions;
    }

    [System.Serializable]
    public class WaspProjectLookupResponse
    {
        public bool found;
        public WaspProjectInfo project;
        public WaspUnityProject unity_project;
        public string project_slug;  // The actual project slug - this is what we need!
        public WaspAppStore[] app_stores;
        public string project_key;
        public string ingest_url;
        public string boostops_config; // Cross-promo campaign configuration JSON
        public WaspStudio studio;
        
        // Store raw response for complex JSON extraction (not serialized)
        [System.NonSerialized]
        public string rawResponse;
        public string message;
    }
    
    [System.Serializable]
    public class WaspUnityProject
    {
        public string id;
        public string unity_project_id;
        public string unity_game_id;
        public string unity_product_guid;
        public string unity_org_id;
        public string last_sync_at;
    }
    
    [System.Serializable]
    public class WaspAppStore
    {
        public string id;
        public string type;
        public string apple_bundle_id;
        public string apple_store_id;
        public string android_package_name;
        public string[] android_sha256_fingerprints;
        public string amazon_asin;
        public string microsoft_product_id;
        public string samsung_package_name;
        public bool is_active;
    }
    
    [System.Serializable]
    public class WaspStudio
    {
        public string id;
        public string name;
        public string description;
        public string created_at;
        public string updated_at;
        public WaspTier tier;
    }
    
    [System.Serializable]
    public class WaspTier
    {
        public string name;
        public int max_projects;
        public bool includes_analytics;
    }
    
    [System.Serializable]
    public class WaspProjectRegistration
    {
        public bool success;
        public int projectId;
        public string message;
    }
    
    [System.Serializable]
    public class WaspSDKKeyResponse
    {
        public bool success;
        public string sdkKey;
        public int studioId;
    }
    
    [System.Serializable]
    public class WaspSlugActivationResponse
    {
        public bool success;
        public string projectId;
        public string projectSlug;
        public bool isDynamicLinksActive;
        public string message;
    }
    
    [System.Serializable]
    public class WaspActiveSlugResponse
    {
        public bool success;
        public bool hasSlug;
        public string projectSlug;
        public bool isDynamicLinksActive;
        public string message;
    }
} 