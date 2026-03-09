using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

namespace BoostOps.CrossPromo
{
    /// <summary>
    /// Individual app tile in the app wall grid
    /// Displays app icon, name, and install button
    /// </summary>
    public class BoostOpsAppWallTile : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("App icon image")]
        public Image appIcon;
        
        [Tooltip("App name text")]
        public Text appNameText;
        
        [Tooltip("Install/Download button")]
        public Button installButton;
        
        [Tooltip("Install button text")]
        public Text installButtonText;
        
        [Tooltip("Loading spinner (optional)")]
        public GameObject loadingSpinner;
        
        [Tooltip("Tile button (makes entire card clickable)")]
        public Button tileButton;
        
        // Private state
        private BoostOps.Core.AppWallApp currentApp;
        private string currentPlacement;
        private int currentPosition;
        private bool isSetup = false;
        private string impressionId; // Link this click back to the item's impression
        private long impressionTimestamp; // For time_to_click calculation
        private string containerImpressionId; // Link this click back to the container (app wall)
        
        /// <summary>
        /// Setup the tile with app data and impression tracking
        /// </summary>
        public void Setup(BoostOps.Core.AppWallApp app, string placement, int position = 0, string itemImpressionId = null, long itemImpressionTimestamp = 0, string wallContainerImpressionId = null)
        {
            if (app == null)
            {
                Debug.LogWarning("[BoostOpsAppWallTile] Attempted to setup with null app");
                return;
            }
            
            currentApp = app;
            currentPlacement = placement;
            currentPosition = position;
            impressionId = itemImpressionId;
            impressionTimestamp = itemImpressionTimestamp;
            containerImpressionId = wallContainerImpressionId;
            isSetup = true;
            
            // Set app name
            if (appNameText != null)
            {
                appNameText.text = app.target_project_name ?? "Unknown App";
            }
            
            // Set install button text
            if (installButtonText != null)
            {
                installButtonText.text = "Install";
            }
            
            // Wire up install button
            if (installButton != null)
            {
                installButton.onClick.RemoveAllListeners();
                installButton.onClick.AddListener(OnInstallClicked);
            }
            
            // Wire up tile button (entire card clickable)
            if (tileButton != null)
            {
                tileButton.onClick.RemoveAllListeners();
                tileButton.onClick.AddListener(OnInstallClicked);
            }
            
            // Load app icon
            LoadAppIcon();
            
            // Debug.Log($"[BoostOpsAppWallTile] Setup complete for {app.target_project_name}");
        }
        
        private void LoadAppIcon()
        {
            if (currentApp == null || appIcon == null)
                return;
            
            // Show loading spinner if available
            if (loadingSpinner != null)
            {
                loadingSpinner.SetActive(true);
            }
            
            // Get icon URL or local key
            string iconUrl = currentApp.GetIconUrl();
            string localKey = currentApp.GetIconLocalKey();
            
            if (!string.IsNullOrEmpty(localKey))
            {
                // Try to load from Resources first (if prefetched)
                StartCoroutine(LoadIconFromResources(localKey));
            }
            else if (!string.IsNullOrEmpty(iconUrl))
            {
                // Fallback to loading from URL
                StartCoroutine(LoadIconFromUrl(iconUrl));
            }
            else
            {
                Debug.LogWarning($"[BoostOpsAppWallTile] No icon URL or local key for {currentApp.target_project_name}");
                HideLoadingSpinner();
            }
        }
        
        private IEnumerator LoadIconFromResources(string localKey)
        {
            // Convert local key to Resources path
            // Remote config uses "Icons/1144343820_icon"
            // Unity Resources path is "BoostOps/Icons/1144343820_icon"
            string resourcePath = "BoostOps/" + localKey;
            
            // Try loading as Sprite first
            Sprite iconSprite = Resources.Load<Sprite>(resourcePath);
            
            if (iconSprite != null)
            {
                appIcon.sprite = iconSprite;
                appIcon.enabled = true;
                // Icon loaded successfully - no log needed
            }
            else
            {
                // Try loading as Texture2D and convert to Sprite
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                
                if (texture != null)
                {
                    iconSprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    
                    appIcon.sprite = iconSprite;
                    appIcon.enabled = true;
                    // Icon loaded successfully - no log needed
                }
                else
                {
                    Debug.LogWarning($"[BoostOpsAppWallTile] ❌ Could not load icon from Resources: {resourcePath} (original key: {localKey})");
                    
                    // Fallback to URL if Resources failed
                    string iconUrl = currentApp.GetIconUrl();
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        Debug.LogWarning($"[BoostOpsAppWallTile] ⚠️ Falling back to URL loading: {iconUrl}");
                        yield return StartCoroutine(LoadIconFromUrl(iconUrl));
                        yield break;
                    }
                }
            }
            
            HideLoadingSpinner();
            yield return null;
        }
        
        private IEnumerator LoadIconFromUrl(string url)
        {
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();
                
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(www);
                    if (texture != null)
                    {
                        Sprite iconSprite = Sprite.Create(
                            texture,
                            new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f)
                        );
                        
                        appIcon.sprite = iconSprite;
                        appIcon.enabled = true;
                        // Icon loaded successfully from URL fallback - no log needed
                    }
                }
                else
                {
                    Debug.LogWarning($"[BoostOpsAppWallTile] ❌ Failed to load icon from URL: {url} - {www.error}");
                }
            }
            
            HideLoadingSpinner();
        }
        
        private void HideLoadingSpinner()
        {
            if (loadingSpinner != null)
            {
                loadingSpinner.SetActive(false);
            }
        }
        
        private void OnInstallClicked()
        {
            if (!isSetup || currentApp == null)
            {
                Debug.LogWarning("[BoostOpsAppWallTile] Install clicked before setup or app is null");
                return;
            }
            
            Debug.Log($"[BoostOpsAppWallTile] Install clicked for {currentApp.target_project_name}");
            
            // Track click event
            TrackClick();
            
            // Open store page
            OpenStorePage();
        }
        
        private void TrackClick()
        {
            try
            {
                string sourceStoreId = BoostOpsAnalyticsContract.GetSourceStoreId();
                string targetStoreId = currentApp.GetStoreId();
                
                // Use deterministic campaign ID from backend, with fallback
                string campaignId = currentApp.campaign_id;
                string campaignSlug = currentApp.campaign_slug;
                
                // Fallback: Use simple slug if not provided by backend (server will derive full ID)
                if (string.IsNullOrEmpty(campaignSlug))
                {
                    campaignSlug = $"app_wall_item_{currentApp.target_project_id}";
                }
                
                BoostOpsAnalyticsContract.TrackClick(
                    campaignSlug: campaignSlug,
                    placement: currentPlacement,
                    // Note: source_store_id is in context.store_id (universal) - not passed here
                    // Note: source_project_id is derived server-side from project_key
                    targetStoreId: targetStoreId,
                    targetProjectId: currentApp.target_project_id,
                    format: "app_wall",
                    channel: "xpromo",
                    position: currentPosition,
                    impressionId: impressionId, // ✨ Link to item's impression_id
                    impressionTimestamp: impressionTimestamp,
                    containerImpressionId: containerImpressionId // ✨ Link to app wall container
                );
                
                Debug.Log($"[BoostOpsAppWallTile] Tracked click - {currentApp.target_project_name} (campaign_slug: {campaignSlug}, source: {sourceStoreId} -> target: {targetStoreId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsAppWallTile] Failed to track click: {ex.Message}");
            }
        }
        
        private void OpenStorePage()
        {
            string storeUrl = currentApp.GetStoreUrl();
            
            if (string.IsNullOrEmpty(storeUrl))
            {
                Debug.LogWarning($"[BoostOpsAppWallTile] No store URL for {currentApp.target_project_name}");
                return;
            }
            
#if UNITY_IOS && !UNITY_EDITOR
            // Try native iOS store sheet first
            string storeId = currentApp.GetStoreId();
            if (!string.IsNullOrEmpty(storeId) && BoostOps.BoostOpsAppStoreSheet.IsAvailable())
            {
                Debug.Log($"[BoostOpsAppWallTile] Opening iOS store sheet: {storeId}");
                BoostOps.BoostOpsAppStoreSheet.ShowAppStoreSheet(storeId);
                return;
            }
#endif
            
            // Fallback to opening URL
            Debug.Log($"[BoostOpsAppWallTile] Opening store URL: {storeUrl}");
            Application.OpenURL(storeUrl);
        }
        
        private void OnDestroy()
        {
            // Clean up button listener
            if (installButton != null)
            {
                installButton.onClick.RemoveAllListeners();
            }
        }
    }
}

