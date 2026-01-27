using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace BoostOps.Editor
{
    /// <summary>
    /// Modal window for selecting which registered apps to pre-cache icons for
    /// </summary>
    public class RegisteredAppsImportWindow : EditorWindow
    {
        private RegisteredApp[] apps;
        private bool[] selectedApps;
        private System.Action<RegisteredApp[]> onSelectionComplete;
        private Vector2 scrollPosition;
        private Texture2D appleLogoTexture;
        private Texture2D androidLogoTexture;
        
        /// <summary>
        /// Initialize the window with app data and callback
        /// </summary>
        /// <param name="apps">Array of registered apps from API</param>
        /// <param name="callback">Callback when user confirms selection</param>
        public void Initialize(RegisteredApp[] apps, System.Action<RegisteredApp[]> callback)
        {
            this.apps = apps;
            this.selectedApps = new bool[apps.Length];
            this.onSelectionComplete = callback;
            
            // Default to selecting all apps with at least one platform that has an icon
            for (int i = 0; i < apps.Length; i++)
            {
                var primaryPlatform = GetPriorityIconPlatform(apps[i].allPlatforms);
                selectedApps[i] = primaryPlatform != null;
            }
            
            // Set window properties
            this.minSize = new Vector2(550, 300);
            this.maxSize = new Vector2(650, 500);
            
            // Load platform logos
            LoadPlatformLogos();
        }
        
        private void LoadPlatformLogos()
        {
            // Load Apple logo
            appleLogoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BoostOps/Editor/Resources/apple-logo-white.png");
            
            // Load Google Play logo
            androidLogoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BoostOps/Editor/Resources/google-play-logo.png");
        }
        
        private void OnGUI()
        {
            if (apps == null || apps.Length == 0)
            {
                EditorGUILayout.LabelField("No registered apps found.", EditorStyles.centeredGreyMiniLabel);
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
                return;
            }
            
            // Header
            EditorGUILayout.LabelField("Cache App Icons", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Select cross promotion apps to pre-cache icons for:", EditorStyles.label);
            
            // Description
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💡 Why pre-cache icons?", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Pre-cache icons for apps you might want to run cross-promotion campaigns for later using the BoostOps dashboard. This ensures icons load instantly in your app without needing to be downloaded at runtime, providing a better user experience.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // App selection list
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            for (int i = 0; i < apps.Length; i++)
            {
                var app = apps[i];
                var iconPlatform = GetPriorityIconPlatform(app.allPlatforms);
                
                EditorGUILayout.BeginHorizontal();
                
                // Checkbox
                selectedApps[i] = EditorGUILayout.Toggle(selectedApps[i], GUILayout.Width(20));
                
                // App name
                EditorGUILayout.LabelField(app.name, EditorStyles.boldLabel);
                
                GUILayout.FlexibleSpace();
                
                // Platform indicator with fallback count and cache status
                if (iconPlatform != null)
                {
                    int fallbackCount = app.allPlatforms.Count(p => !p.isPriority && !string.IsNullOrEmpty(p.iconUrl));
                    string fallbackText = fallbackCount > 0 ? $" (+{fallbackCount} fallback)" : "";
                    
                    // Check if icon is already cached
                    bool isCached = IsIconCached(app, iconPlatform);
                    string cacheStatus = isCached ? "✅ Cached" : "⬇️ Not cached";
                    
                    // Use logo + text for platform display
                    EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
                    
                    if ((iconPlatform.type == "APPLE_STORE" || iconPlatform.type == "IOS_APP_STORE") && appleLogoTexture != null)
                    {
                        GUILayout.Label(appleLogoTexture, GUILayout.Width(16), GUILayout.Height(16));
                        EditorGUILayout.LabelField($"iOS{fallbackText}", EditorStyles.miniLabel, GUILayout.Width(80));
                    }
                    else if ((iconPlatform.type == "GOOGLE_STORE" || iconPlatform.type == "GOOGLE_PLAY") && androidLogoTexture != null)
                    {
                        GUILayout.Label(androidLogoTexture, GUILayout.Width(16), GUILayout.Height(16));
                        EditorGUILayout.LabelField($"Android{fallbackText}", EditorStyles.miniLabel, GUILayout.Width(80));
                    }
                    else
                    {
                        // Fallback for other platforms or missing textures
                        string platformDisplay = (iconPlatform.type == "APPLE_STORE" || iconPlatform.type == "IOS_APP_STORE") ? "📱 iOS" : 
                                               (iconPlatform.type == "GOOGLE_STORE" || iconPlatform.type == "GOOGLE_PLAY") ? "🤖 Android" : 
                                               "🌐 " + iconPlatform.type;
                        EditorGUILayout.LabelField($"[{platformDisplay}]{fallbackText}", EditorStyles.miniLabel, GUILayout.Width(80));
                    }
                    
                    // Show cache status
                    var statusStyle = isCached ? EditorStyles.miniLabel : EditorStyles.centeredGreyMiniLabel;
                    EditorGUILayout.LabelField(cacheStatus, statusStyle, GUILayout.Width(100));
                    
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("[❌ No icon]", EditorStyles.miniLabel, GUILayout.Width(140));
                    GUI.enabled = false; // Disable checkbox for apps without icons
                    selectedApps[i] = false;
                    GUI.enabled = true;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            // Summary with cache statistics
            int selectedCount = selectedApps.Count(s => s);
            int cachedCount = 0;
            int uncachedCount = 0;
            
            for (int i = 0; i < apps.Length; i++)
            {
                var iconPlatform = GetPriorityIconPlatform(apps[i].allPlatforms);
                if (iconPlatform != null)
                {
                    if (IsIconCached(apps[i], iconPlatform))
                        cachedCount++;
                    else
                        uncachedCount++;
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Cache Status: {cachedCount} cached, {uncachedCount} not cached", EditorStyles.miniLabel);
            
            if (selectedCount > 0)
            {
                EditorGUILayout.LabelField($"Selected: {selectedCount} app{(selectedCount == 1 ? "" : "s")}", 
                    EditorStyles.boldLabel);
            }
            
            // Buttons
            EditorGUILayout.Space();
            
            // Selection buttons row
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Select All"))
            {
                for (int i = 0; i < selectedApps.Length; i++)
                {
                    var iconPlatform = GetPriorityIconPlatform(apps[i].allPlatforms);
                    selectedApps[i] = iconPlatform != null; // Only select apps that have icons
                }
            }
            
            if (GUILayout.Button("Select Uncached"))
            {
                for (int i = 0; i < selectedApps.Length; i++)
                {
                    var iconPlatform = GetPriorityIconPlatform(apps[i].allPlatforms);
                    if (iconPlatform != null)
                    {
                        // Only select apps that have icons but are not cached
                        selectedApps[i] = !IsIconCached(apps[i], iconPlatform);
                    }
                    else
                    {
                        selectedApps[i] = false;
                    }
                }
            }
            
            if (GUILayout.Button("Select None"))
            {
                for (int i = 0; i < selectedApps.Length; i++)
                    selectedApps[i] = false;
            }
            
            GUILayout.FlexibleSpace();
            
            // Delete cached icons button
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Light red tint
            if (GUILayout.Button("🗑️ Delete Cached Icons", GUILayout.Width(140)))
            {
                DeleteCachedIcons();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            // Main action buttons row
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            
            GUILayout.FlexibleSpace();
            
            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button("Cache Icons"))
            {
                var selected = apps.Where((app, index) => selectedApps[index]).ToArray();
                onSelectionComplete?.Invoke(selected);
                Close();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Delete all cached icons and show confirmation dialog
        /// </summary>
        private void DeleteCachedIcons()
        {
            string iconsPath = "Assets/Resources/BoostOps/Icons";
            
            // Check if icons directory exists
            if (!AssetDatabase.IsValidFolder(iconsPath))
            {
                EditorUtility.DisplayDialog("No Cached Icons", 
                    "No cached icons found. The icons directory doesn't exist yet.", 
                    "OK");
                return;
            }
            
            // Find all icon files in the directory
            string[] iconFiles = AssetDatabase.FindAssets("t:Texture2D", new[] { iconsPath });
            
            if (iconFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("No Cached Icons", 
                    "No cached icons found in the directory.", 
                    "OK");
                return;
            }
            
            // Show confirmation dialog
            bool confirmed = EditorUtility.DisplayDialog("Delete Cached Icons", 
                $"Are you sure you want to delete all {iconFiles.Length} cached app icons?\n\n" +
                "This action cannot be undone. Icons will need to be re-downloaded for future cross-promotion campaigns.", 
                "Delete All", "Cancel");
                
            if (!confirmed) return;
            
            // Delete all icon files
            int deletedCount = 0;
            try
            {
                foreach (string guid in iconFiles)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.DeleteAsset(assetPath))
                    {
                        deletedCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[BoostOps] Failed to delete cached icon: {assetPath}");
                    }
                }
                
                // Refresh AssetDatabase
                AssetDatabase.Refresh();
                
                // Show success message
                EditorUtility.DisplayDialog("Icons Deleted", 
                    $"Successfully deleted {deletedCount} cached app icons.\n\n" +
                    "Icons will be re-downloaded automatically when needed for cross-promotion campaigns.", 
                    "OK");
                    
                Debug.Log($"[BoostOps] Deleted {deletedCount} cached app icons from {iconsPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Error deleting cached icons: {ex.Message}");
                EditorUtility.DisplayDialog("Delete Failed", 
                    $"An error occurred while deleting cached icons:\n\n{ex.Message}", 
                    "OK");
            }
        }
        
        /// <summary>
        /// Check if an icon is already cached for the given app and platform
        /// </summary>
        private bool IsIconCached(RegisteredApp app, AppPlatform platform)
        {
            if (app == null || platform == null || string.IsNullOrEmpty(platform.storeId))
                return false;
            
            // Generate the expected icon filename based on store ID
            string iconFileName = $"{platform.storeId}_icon.png";
            string iconPath = $"Assets/Resources/BoostOps/Icons/{iconFileName}";
            
            // Check if the icon file exists in the Resources folder
            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath) != null;
        }
        
        /// <summary>
        /// Get the priority platform for icon download (iOS > Android > Others)
        /// </summary>
        private AppPlatform GetPriorityIconPlatform(AppPlatform[] platforms)
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
            
            // Priority 2: Any Apple platform with valid icon URL
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
    }
}
