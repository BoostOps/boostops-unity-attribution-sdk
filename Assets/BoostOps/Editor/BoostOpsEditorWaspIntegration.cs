using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using BoostOps;

namespace BoostOps.Editor
{
    /// <summary>
    /// Integration layer between new Wasp operations and existing Editor Window
    /// Provides compatibility layer for migrating from HTTP endpoints to Wasp operations
    /// </summary>
    public class BoostOpsEditorWaspIntegration
    {
        private UnityWaspClient waspClient;
        private bool isInitialized = false;
        
        /// <summary>
        /// Initialize the integration with JWT token and server mode
        /// </summary>
        /// <param name="jwtToken">JWT token from OAuth flow</param>
        /// <param name="useLocalServer">True for local development, false for production</param>
        public void Initialize(string jwtToken, bool useLocalServer = false)
        {
#if !UNITY_EDITOR
            Debug.LogError("[BoostOpsEditorWaspIntegration] This integration is EDITOR-ONLY and should not be used in runtime builds!");
            return;
#endif
            waspClient = new UnityWaspClient();
            waspClient.Initialize(jwtToken);
            isInitialized = true;
            
            Debug.Log($"[BoostOpsEditorWaspIntegration] Initialized with production server");
        }
        
        /// <summary>
        /// Initialize the integration with JWT token and user info
        /// </summary>
        /// <param name="jwtToken">JWT token from OAuth flow</param>
        /// <param name="userDisplayName">User's display name</param>
        /// <param name="userEmail">User's email</param>
        /// <param name="useLocalServer">True for local development, false for production</param>
        public bool Initialize(string jwtToken, string userDisplayName, string userEmail, bool useLocalServer = false)
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogError("[BoostOpsEditorWaspIntegration] JWT token is required for initialization");
                return false;
            }
            
            waspClient = new UnityWaspClient();
            waspClient.Initialize(jwtToken);
            isInitialized = true;
            
            Debug.Log($"[BoostOpsEditorWaspIntegration] Initialized with production server for user: {userDisplayName}");
            return true;
        }
        
        /// <summary>
        /// Initialize with stored credentials from EditorPrefs
        /// </summary>
        /// <returns>True if initialization was successful</returns>
        public bool InitializeWithStoredCredentials()
        {
            string jwtToken = EditorPrefs.GetString("BoostOps_ApiToken", "");
            bool useLocalServer = EditorPrefs.GetBool("BoostOps_UseLocalServer", true);
            
            if (string.IsNullOrEmpty(jwtToken))
            {
                Debug.LogWarning("[BoostOpsEditorWaspIntegration] No stored JWT token found");
                return false;
            }
            
            Initialize(jwtToken, useLocalServer);
            return true;
        }
        
        /// <summary>
        /// Get user information using Wasp operations
        /// </summary>
        /// <returns>UserInfo compatible with existing Editor Window</returns>
        public async Task<UserInfo> GetUserInfo()
        {
            if (!isInitialized)
            {
                Debug.LogError("[BoostOpsEditorWaspIntegration] Not initialized. Call Initialize() first.");
                return null;
            }
            
            try
            {
                var waspUserInfo = await waspClient.GetUserInfo();
                if (waspUserInfo == null) return null;
                
                // Convert from Wasp type to Editor type
                return new UserInfo
                {
                    id = waspUserInfo.id,
                    email = waspUserInfo.email,
                    displayName = waspUserInfo.displayName,
                    username = waspUserInfo.displayName ?? waspUserInfo.email, // Use displayName or email as fallback
                    firstName = GetFirstName(waspUserInfo.displayName),
                    lastName = GetLastName(waspUserInfo.displayName),
                    studio = waspUserInfo.studio != null ? new BoostOps.StudioInfo
                    {
                        id = null, // Wasp API doesn't provide studio ID yet
                        name = waspUserInfo.studio.name,
                        description = waspUserInfo.studio.description
                    } : null
                };
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsEditorWaspIntegration] Error getting user info: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get SDK key using Wasp operations
        /// </summary>
        /// <returns>SDKKeyData compatible with existing Editor Window</returns>
        public async Task<SDKKeyData> GetSDKKey()
        {
            if (!isInitialized)
            {
                Debug.LogError("[BoostOpsEditorWaspIntegration] Not initialized. Call Initialize() first.");
                return null;
            }
            
            try
            {
                var response = await waspClient.GetSDKKey();
                if (response != null && response.success)
                {
                    // Convert from Wasp type to Editor type
                    return new SDKKeyData
                    {
                        sdkKey = response.sdkKey,
                        studioId = response.studioId.ToString(),
                        studioName = "Studio", // Default value since not provided by API
                        totalAppsDetected = 0, // Default value since not provided by API
                        createdAt = System.DateTime.Now.ToString("yyyy-MM-dd"), // Default value
                        lastReset = System.DateTime.Now.ToString("yyyy-MM-dd") // Default value
                    };
                }
                else
                {
                    Debug.LogWarning($"[BoostOpsEditorWaspIntegration] SDK key request failed");
                    return null;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsEditorWaspIntegration] Error getting SDK key: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Register Unity project using Wasp operations
        /// </summary>
        /// <param name="projectName">Project name</param>
        /// <param name="unityProjectId">Unity project ID (will be ignored, kept for compatibility)</param>
        /// <param name="iosBundleId">iOS bundle ID</param>
        /// <param name="androidPackage">Android package name</param>
        /// <returns>ProjectRegistrationResult compatible with existing Editor Window</returns>
        public async Task<ProjectRegistrationResult> RegisterProject(
            string projectName, 
            string unityProjectId = null, 
            string iosBundleId = null, 
            string androidPackage = null)
        {
            if (!isInitialized)
            {
                Debug.LogError("[BoostOpsEditorWaspIntegration] Not initialized. Call Initialize() first.");
                return null;
            }
            
            try
            {
                // Note: unityProjectId is ignored in the new API, kept for compatibility
                var result = await waspClient.RegisterProject(
                    projectName: projectName,
                    bundleId: iosBundleId,
                    androidPackageName: androidPackage
                );
                
                if (result != null)
                {
                    return new ProjectRegistrationResult
                    {
                        success = result.success,
                        projectId = result.projectId.ToString(),
                        message = result.message
                    };
                }
                else
                {
                    return new ProjectRegistrationResult
                    {
                        success = false,
                        projectId = null,
                        message = "Registration failed"
                    };
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsEditorWaspIntegration] Error registering project: {ex.Message}");
                return new ProjectRegistrationResult
                {
                    success = false,
                    projectId = null,
                    message = $"Error: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// Lookup project by Unity project identifiers to check if already registered
        /// </summary>
        /// <returns>Project info if found, null if not found</returns>
        public async Task<WaspProjectInfo> LookupProject()
        {
            if (!isInitialized)
            {
                Debug.LogError("[BoostOpsEditorWaspIntegration] Not initialized. Call Initialize() first.");
                return null;
            }
            
            try
            {
                var result = await waspClient.LookupProject();
                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsEditorWaspIntegration] Error looking up project: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Test the connection to the Wasp server
        /// </summary>
        /// <returns>True if connection is working</returns>
        public async Task<bool> TestConnection()
        {
            if (!isInitialized)
            {
                Debug.LogError("[BoostOpsEditorWaspIntegration] Not initialized. Call Initialize() first.");
                return false;
            }
            
            try
            {
                var userInfo = await waspClient.GetUserInfo();
                return userInfo != null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsEditorWaspIntegration] Connection test failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get all registered apps for the studio with multi-platform icon URLs
        /// </summary>
        /// <param name="iconSize">Icon size preference: "256x256", "512x512", or "1024x1024"</param>
        /// <returns>RegisteredAppsResponse with all studio apps and platform info</returns>
        public async Task<RegisteredAppsResponse> GetRegisteredApps(string iconSize = "512x512")
        {
            if (!isInitialized)
            {
                Debug.LogError("[BoostOpsEditorWaspIntegration] Not initialized. Call Initialize() first.");
                return null;
            }
            
            try
            {
                Debug.Log($"[BoostOpsEditorWaspIntegration] Fetching registered apps with icon size: {iconSize}");
                var response = await waspClient.GetRegisteredApps(iconSize);
                
                if (response != null && response.success)
                {
                    Debug.Log($"[BoostOpsEditorWaspIntegration] Successfully fetched {response.totalApps} registered apps for studio: {response.studio?.name}");
                    return response;
                }
                else
                {
                    Debug.LogWarning($"[BoostOpsEditorWaspIntegration] Failed to fetch registered apps");
                    return response;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsEditorWaspIntegration] Error fetching registered apps: {ex.Message}");
                return new RegisteredAppsResponse
                {
                    success = false,
                    apps = new RegisteredApp[0],
                    totalApps = 0,
                    iconSize = iconSize,
                    totalEstimatedBytes = 0,
                    totalEstimatedMB = 0f,
                    studio = new BoostOps.StudioInfo { id = "", name = "" }
                };
            }
        }
        
        /// <summary>
        /// Check if integration is properly authenticated
        /// </summary>
        /// <returns>True if authenticated and ready to use</returns>
        public async Task<bool> IsAuthenticated()
        {
            if (!isInitialized)
                return false;
                
            try
            {
                var userInfo = await waspClient.GetUserInfo();
                return userInfo != null && !string.IsNullOrEmpty(userInfo.email);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Helper method to extract first name from display name
        /// </summary>
        private string GetFirstName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "";
            
            var parts = displayName.Split(' ');
            return parts.Length > 0 ? parts[0] : "";
        }
        
        /// <summary>
        /// Helper method to extract last name from display name
        /// </summary>
        private string GetLastName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "";
            
            var parts = displayName.Split(' ');
            return parts.Length > 1 ? parts[parts.Length - 1] : "";
        }
    }
    
    /// <summary>
    /// Result structure for project registration
    /// </summary>
    [System.Serializable]
    public class ProjectRegistrationResult
    {
        public bool success;
        public string projectId;
        public string message;
    }
    

    
    /// <summary>
    /// Extension methods for easy integration
    /// </summary>
    public static class BoostOpsEditorWaspExtensions
    {
        /// <summary>
        /// Create a new Wasp integration instance
        /// </summary>
        /// <returns>New BoostOpsEditorWaspIntegration instance</returns>
        public static BoostOpsEditorWaspIntegration CreateWaspIntegration()
        {
            return new BoostOpsEditorWaspIntegration();
        }
        
        /// <summary>
        /// Create and initialize Wasp integration with stored credentials
        /// </summary>
        /// <param name="integration">Output integration instance</param>
        /// <returns>True if initialization was successful</returns>
        public static bool CreateAndInitializeWaspIntegration(out BoostOpsEditorWaspIntegration integration)
        {
            integration = new BoostOpsEditorWaspIntegration();
            return integration.InitializeWithStoredCredentials();
        }
        
        /// <summary>
        /// Initialize Wasp integration with explicit credentials
        /// </summary>
        /// <param name="jwtToken">JWT token from OAuth</param>
        /// <param name="useLocalServer">True for local development</param>
        /// <returns>Initialized integration instance</returns>
        public static BoostOpsEditorWaspIntegration InitializeWaspIntegration(string jwtToken, bool useLocalServer = true)
        {
            var integration = new BoostOpsEditorWaspIntegration();
            integration.Initialize(jwtToken, useLocalServer);
            return integration;
        }
    }
}

// Example usage in BoostOpsEditorWindow:
/*
public class BoostOpsEditorWindow : EditorWindow
{
    private BoostOpsEditorWaspIntegration waspIntegration;
    
    void OnEnable()
    {
        // Initialize Wasp integration
        waspIntegration = BoostOpsEditorWaspExtensions.CreateWaspIntegration();
        
        // Try to initialize with stored credentials
        if (waspIntegration.InitializeWithStoredCredentials())
        {
            Debug.Log("Wasp integration initialized successfully");
        }
    }
    
    // Replace old GetUserInfo method
    private async System.Threading.Tasks.Task<UserInfo> GetUserInfo()
    {
        return await waspIntegration.GetUserInfo();
    }
    
    // Replace old GetSDKKey method
    private async System.Threading.Tasks.Task<SDKKeyData> GetSDKKey()
    {
        return await waspIntegration.GetSDKKey();
    }
    
    // Replace old project registration method
    private async void RegisterProject()
    {
        var result = await waspIntegration.RegisterProject(
            projectName: appName,
            unityProjectId: GetUnityProjectId(), // This parameter is ignored but kept for compatibility
            iosBundleId: iosBundleId,
            androidPackage: androidBundleId
        );
        
        if (result != null && result.success)
        {
            Debug.Log($"Project registered successfully: {result.projectId}");
        }
        else
        {
            Debug.LogError($"Project registration failed: {result?.message}");
        }
    }
}
*/ 