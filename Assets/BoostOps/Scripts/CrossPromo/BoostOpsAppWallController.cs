using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

namespace BoostOps.CrossPromo
{
    /// <summary>
    /// Controller for the App Wall display
    /// Manages the grid of app tiles and handles user interactions
    /// </summary>
    public class BoostOpsAppWallController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Title text at the top of the app wall")]
        public Text titleText;
        
        [Tooltip("Container where app tiles will be instantiated")]
        public Transform appGridContainer;
        
        [Tooltip("Close button for dismissing the app wall")]
        public Button closeButton;
        
        [Tooltip("Background dimmer (optional - will be wired to close if present)")]
        public Button backgroundButton;
        
        [Tooltip("Grid layout for app tiles")]
        public GridLayoutGroup gridLayout;
        
        [Tooltip("Content panel RectTransform (for portrait/landscape resizing)")]
        public RectTransform contentPanel;
        
        [Header("Prefab References")]
        [Tooltip("Prefab for individual app tiles")]
        public GameObject appTilePrefab;
        
        [Header("Configuration")]
        [Tooltip("Placement identifier for analytics")]
        public string placement = "app_wall";
        
        [Tooltip("Default title if not specified")]
        public string defaultTitle = "More Games You'll Love";
        
        [Tooltip("Number of columns in portrait mode")]
        public int portraitColumns = 2;
        
        [Tooltip("Number of columns in landscape mode")]
        public int landscapeColumns = 3;
        
        // Private state
        private List<GameObject> instantiatedTiles = new List<GameObject>();
        private List<BoostOps.Core.AppWallApp> currentApps = new List<BoostOps.Core.AppWallApp>();
        private bool isShowing = false;
        private string containerImpressionId; // Store container_impression_id for the entire wall (passed to all clicks)
        private Dictionary<string, string> itemImpressionIds = new Dictionary<string, string>(); // impression_id per item
        private Dictionary<string, long> itemImpressionTimestamps = new Dictionary<string, long>(); // timestamp per item
        
        // Events
        public event Action OnAppWallClosed;
        
        private void Awake()
        {
            // Wire up close button
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
            
            // Wire up background to close (optional)
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(Close);
            }
            
            // Adjust layout for current orientation
            UpdateLayoutForOrientation();
            
            // Start hidden
            gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Show the app wall with the provided apps
        /// </summary>
        public void Show(List<BoostOps.Core.AppWallApp> apps, string customTitle = null)
        {
            if (apps == null || apps.Count == 0)
            {
                Debug.LogWarning("[BoostOpsAppWallController] No apps provided to show");
                return;
            }
            
            currentApps = apps;
            
            // Show the UI first (must be active before creating tiles so coroutines work)
            gameObject.SetActive(true);
            isShowing = true;
            
            // Update layout for current orientation
            UpdateLayoutForOrientation();
            
            // Set title
            if (titleText != null)
            {
                titleText.text = customTitle ?? defaultTitle;
            }
            
            // Clear existing tiles
            ClearTiles();
            
            // Store apps for tracking
            currentApps = new List<BoostOps.Core.AppWallApp>(apps);
            
            // Track app wall impression FIRST (so we have impression_id for tiles)
            TrackAppWallImpression();
            
            // Create tiles for each app (now they can access currentImpressionId)
            for (int i = 0; i < apps.Count; i++)
            {
                CreateTile(apps[i], i);
            }
            
            Debug.Log($"[BoostOpsAppWallController] Showing app wall with {apps.Count} apps");
        }
        
        /// <summary>
        /// Close the app wall
        /// </summary>
        public void Close()
        {
            if (!isShowing)
                return;
            
            isShowing = false;
            
            // Resume game if it was paused
            if (Time.timeScale == 0f)
            {
                Time.timeScale = 1f;
            }
            
            OnAppWallClosed?.Invoke();
            
            Debug.Log("[BoostOpsAppWallController] App wall closed - destroying instance");
            
            // Destroy the app wall instance to prevent UI blocking issues
            Destroy(gameObject);
        }
        
        private void CreateTile(BoostOps.Core.AppWallApp app, int position)
        {
            if (appTilePrefab == null)
            {
                Debug.LogError("[BoostOpsAppWallController] App tile prefab is not assigned!");
                return;
            }
            
            if (appGridContainer == null)
            {
                Debug.LogError("[BoostOpsAppWallController] App grid container is not assigned!");
                return;
            }
            
            // Instantiate the tile (parent is active, so tile will be active in hierarchy)
            GameObject tile = Instantiate(appTilePrefab, appGridContainer);
            instantiatedTiles.Add(tile);
            
            // Ensure tile is active (needed for coroutines to start)
            tile.SetActive(true);
            
            // Get the tile controller
            var tileController = tile.GetComponent<BoostOpsAppWallTile>();
            if (tileController != null)
            {
                // Get campaign slug for impression lookup
                string campaignSlug = app.campaign_slug ?? $"app_wall_item_{app.target_project_id}";
                
                // Get item-specific impression data
                string itemImpressionId = itemImpressionIds.ContainsKey(campaignSlug) ? itemImpressionIds[campaignSlug] : null;
                long itemImpressionTimestamp = itemImpressionTimestamps.ContainsKey(campaignSlug) ? itemImpressionTimestamps[campaignSlug] : 0;
                
                // Set up the tile with app data, position, and impression data (both item and container IDs)
                tileController.Setup(app, placement, position, itemImpressionId, itemImpressionTimestamp, containerImpressionId);
            }
            else
            {
                Debug.LogWarning("[BoostOpsAppWallController] Tile prefab is missing BoostOpsAppWallTile component");
                
                // Fallback: Try to wire up manually
                SetupTileManually(tile, app, position);
            }
        }
        
        private void SetupTileManually(GameObject tile, BoostOps.Core.AppWallApp app, int position)
        {
            // Find common UI elements by name
            var nameText = tile.transform.Find("AppName")?.GetComponent<Text>();
            if (nameText != null)
            {
                nameText.text = app.target_project_name;
            }
            
            var iconImage = tile.transform.Find("AppIcon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                // Try to load icon (would need asset loading implementation)
                Debug.Log($"[BoostOpsAppWallController] Would load icon for {app.target_project_name}");
            }
            
            var installButton = tile.transform.Find("InstallButton")?.GetComponent<Button>();
            if (installButton != null)
            {
                installButton.onClick.RemoveAllListeners();
                installButton.onClick.AddListener(() => OnAppClicked(app, position));
            }
        }
        
        private void ClearTiles()
        {
            foreach (var tile in instantiatedTiles)
            {
                if (tile != null)
                {
                    Destroy(tile);
                }
            }
            
            instantiatedTiles.Clear();
        }
        
        private void OnAppClicked(BoostOps.Core.AppWallApp app, int position)
        {
            Debug.Log($"[BoostOpsAppWallController] App clicked: {app.target_project_name} at position {position}");
            
            // Track click
            TrackAppWallClick(app, position);
            
            // Open store page
            OpenStorePage(app);
        }
        
        private void TrackAppWallImpression()
        {
            try
            {
                string sourceStoreId = BoostOpsAnalyticsContract.GetSourceStoreId();
                
                // Generate container impression ID for the entire wall
                containerImpressionId = System.Guid.NewGuid().ToString("N"); // Raw hex without dashes
                
                // Build array of item data with individual impression IDs
                var itemsData = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                
                for (int i = 0; i < currentApps.Count; i++)
                {
                    var app = currentApps[i];
                    
                    // Use deterministic campaign ID from backend, with fallback
                    string campaignId = app.campaign_id;
                    string campaignSlug = app.campaign_slug;
                    
                    // Fallback: Use simple slug if not provided by backend (server will derive full ID)
                    if (string.IsNullOrEmpty(campaignSlug))
                    {
                        campaignSlug = $"app_wall_item_{app.target_project_id}";
                    }
                    
                    // Generate unique impression_id for this item (for click linking)
                    string itemImpressionId = System.Guid.NewGuid().ToString("N");
                    long itemImpressionTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    
                    // Store for click linking
                    itemImpressionIds[campaignSlug] = itemImpressionId;
                    itemImpressionTimestamps[campaignSlug] = itemImpressionTimestamp;
                    
                    var itemData = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "campaign_id", campaignId },
                        { "campaign_slug", campaignSlug },
                        { "target_project_id", app.target_project_id },
                        { "target_store_id", app.GetStoreId() },
                        { "position", i },
                        { "impression_id", itemImpressionId } // ✨ Individual item impression ID
                        // Note: source_type (organic/sponsored) is determined by the server, not sent from SDK
                    };
                    
                    itemsData.Add(itemData);
                }
                
                // Track standard impression event with format="app_wall" and nested items array
                BoostOpsAnalyticsContract.TrackAppWallImpression(
                    placement: placement,
                    items: itemsData,
                    containerImpressionId: containerImpressionId // ✨ Container-level ID
                    // Note: source_store_id is in context.store_id (universal) - not passed here
                    // Note: source_project_id is derived server-side from project_key
                );
                
                Debug.Log($"[BoostOps] Tracked impression: app_wall ({currentApps.Count} items), container_impression_id={containerImpressionId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsAppWallController] Failed to track impression: {ex.Message}");
            }
        }
        
        private void TrackAppWallClick(BoostOps.Core.AppWallApp app, int position)
        {
            try
            {
                string sourceStoreId = BoostOpsAnalyticsContract.GetSourceStoreId();
                string targetStoreId = app.GetStoreId();
                
                // Use deterministic campaign ID from backend, with fallback
                string campaignId = app.campaign_id;
                string campaignSlug = app.campaign_slug;
                
                // Fallback: Use simple slug if not provided by backend (server will derive full ID)
                if (string.IsNullOrEmpty(campaignSlug))
                {
                    campaignSlug = $"app_wall_item_{app.target_project_id}";
                }
                
                BoostOpsAnalyticsContract.TrackClick(
                    campaignSlug: campaignSlug,
                    placement: placement,
                    // Note: source_store_id is in context.store_id (universal) - not passed here
                    // Note: source_project_id is derived server-side from project_key
                    targetStoreId: targetStoreId,
                    targetProjectId: app.target_project_id,
                    format: "app_wall",
                    channel: "xpromo",
                    position: position
                );
                
                Debug.Log($"[BoostOpsAppWallController] Tracked app_wall_click - {app.target_project_name} at position {position} (campaign_slug: {campaignSlug}, source: {sourceStoreId} -> target: {targetStoreId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOpsAppWallController] Failed to track click: {ex.Message}");
            }
        }
        
        private void UpdateLayoutForOrientation()
        {
            bool isLandscape = Screen.width > Screen.height;
            
            // Update grid layout if available
            if (gridLayout != null)
            {
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = isLandscape ? landscapeColumns : portraitColumns;
                
                // Match prefab cell sizes: 300x400 with 36px spacing
                gridLayout.cellSize = new Vector2(300, 400);
                gridLayout.spacing = new Vector2(36, 36);
            }
            
            // Adjust content panel size based on orientation
            if (contentPanel != null)
            {
                if (isLandscape)
                {
                    // Landscape: 1080 x 720 (matches prefab default)
                    contentPanel.sizeDelta = new Vector2(1080, 720);
                }
                else
                {
                    // Portrait: narrower panel to fit 2 columns
                    contentPanel.sizeDelta = new Vector2(720, 900);
                }
            }
            
            Debug.Log($"[BoostOpsAppWallController] Orientation: {(isLandscape ? "Landscape" : "Portrait")}, " +
                     $"Columns: {(isLandscape ? landscapeColumns : portraitColumns)}, " +
                     $"Panel Size: {contentPanel?.sizeDelta}");
        }
        
        private void OpenStorePage(BoostOps.Core.AppWallApp app)
        {
            string storeUrl = app.GetStoreUrl();
            
            if (string.IsNullOrEmpty(storeUrl))
            {
                Debug.LogWarning($"[BoostOpsAppWallController] No store URL available for {app.target_project_name}");
                return;
            }
            
#if UNITY_IOS && !UNITY_EDITOR
            // Try native iOS store sheet first
            string storeId = app.GetStoreId();
            if (!string.IsNullOrEmpty(storeId) && BoostOps.BoostOpsAppStoreSheet.IsAvailable())
            {
                Debug.Log($"[BoostOpsAppWallController] Opening iOS store sheet for: {storeId}");
                BoostOps.BoostOpsAppStoreSheet.ShowAppStoreSheet(storeId);
                return;
            }
#endif
            
            // Fallback to opening URL
            Debug.Log($"[BoostOpsAppWallController] Opening store URL: {storeUrl}");
            Application.OpenURL(storeUrl);
        }
        
        private void OnDestroy()
        {
            // Clean up listeners
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
            }
            
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
            }
            
            // Clean up tiles
            ClearTiles();
        }
    }
}

