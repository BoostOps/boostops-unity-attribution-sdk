using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BoostOps
{
    /// <summary>
    /// Component for displaying app wall (portfolio grid) with multiple campaigns
    /// Attach to app wall prefab root object
    /// </summary>
    public class BoostOpsAppWallDisplay : MonoBehaviour
    {
        [Header("Required References")]
        [Tooltip("Container where app wall items will be instantiated")]
        public Transform itemContainer;
        
        [Tooltip("Prefab for individual app wall items")]
        public GameObject itemPrefab;
        
        [Header("UI References (Optional)")]
        public Text titleText;
        public Button closeButton;
        public GridLayoutGroup gridLayout;
        
        [Header("Settings")]
        public int portraitColumns = 2;
        public int landscapeColumns = 3;
        public int maxItems = 12;
        
        [Header("Panel Size")]
        public RectTransform contentPanel;
        
        private List<Campaign> campaigns;
        private string placement;
        private BoostOpsCampaignDisplay parentDisplay;
        
        /// <summary>
        /// Initialize the app wall with campaigns
        /// Called by BoostOpsCampaignDisplay when showing app wall
        /// </summary>
        public void Initialize(List<Campaign> campaigns, string placement, BoostOpsCampaignDisplay parentDisplay)
        {
            this.campaigns = campaigns;
            this.placement = placement;
            this.parentDisplay = parentDisplay;
            
            Debug.Log($"[BoostOpsAppWallDisplay] Initializing with {campaigns.Count} campaigns");
            
            // Setup UI
            SetupUI();
            
            // Adapt layout to orientation (one-time setup)
            UpdateLayoutForOrientation();
            
            // Populate grid with campaign items
            PopulateItems();
        }
        
        private void SetupUI()
        {
            // Set title if available
            if (titleText != null)
            {
                titleText.text = "Our Games";
            }
            
            // Wire up close button
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => {
                    if (parentDisplay != null)
                    {
                        parentDisplay.HideCampaign();
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                });
            }
        }
        
        private void UpdateLayoutForOrientation()
        {
            if (gridLayout == null) return;
            
            bool isLandscape = Screen.width > Screen.height;
            int columns = isLandscape ? landscapeColumns : portraitColumns;
            
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;
            
            // Match prefab cell sizes: 300x400 with 36px spacing
            gridLayout.cellSize = new Vector2(300, 400);
            gridLayout.spacing = new Vector2(36, 36);
            
            // Adjust content panel size for portrait mode
            if (contentPanel != null)
            {
                if (isLandscape)
                {
                    // Landscape: 1080 x 720 (matches prefab)
                    contentPanel.sizeDelta = new Vector2(1080, 720);
                }
                else
                {
                    // Portrait: narrower panel to fit 2 columns
                    contentPanel.sizeDelta = new Vector2(720, 900);
                }
            }
            
            Debug.Log($"[BoostOpsAppWallDisplay] Orientation: {(isLandscape ? "Landscape" : "Portrait")}, Columns: {columns}, Cell Size: {gridLayout.cellSize}, Panel Size: {contentPanel?.sizeDelta}");
        }
        
        private void PopulateItems()
        {
            if (itemContainer == null)
            {
                Debug.LogError("[BoostOpsAppWallDisplay] Item container not assigned!");
                return;
            }
            
            // Clear existing items
            foreach (Transform child in itemContainer)
            {
                Destroy(child.gameObject);
            }
            
            // Limit number of items
            int itemCount = Mathf.Min(campaigns.Count, maxItems);
            
            for (int i = 0; i < itemCount; i++)
            {
                var campaign = campaigns[i];
                CreateItem(campaign);
            }
            
            Debug.Log($"[BoostOpsAppWallDisplay] Created {itemCount} app wall items");
        }
        
        private void CreateItem(Campaign campaign)
        {
            GameObject item;
            
            if (itemPrefab != null)
            {
                // Use custom prefab
                item = Instantiate(itemPrefab, itemContainer);
                
                // Find BoostOpsAppWallItem component and initialize
                var itemComponent = item.GetComponent<BoostOpsAppWallItem>();
                if (itemComponent != null)
                {
                    itemComponent.Initialize(campaign, placement, parentDisplay);
                }
                else
                {
                    Debug.LogWarning($"[BoostOpsAppWallDisplay] Item prefab missing BoostOpsAppWallItem component, using fallback population");
                    PopulateItemFallback(item, campaign);
                }
            }
            else
            {
                // Create default item
                item = CreateDefaultItem(campaign);
            }
        }
        
        private GameObject CreateDefaultItem(Campaign campaign)
        {
            GameObject item = new GameObject($"AppWallItem_{campaign.campaign_id}");
            item.transform.SetParent(itemContainer, false);
            
            // Background
            Image itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(0.95f, 0.95f, 0.95f);
            
            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(item.transform, false);
            Image icon = iconObj.AddComponent<Image>();
            
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(120, 120);
            iconRect.anchoredPosition = new Vector2(0, 30);
            
            // Load campaign icon using same logic as BoostOpsCampaignDisplay
            LoadCampaignIcon(icon, campaign);
            
            // Name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(item.transform, false);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.text = campaign.name ?? "Game";
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = new Color(0.2f, 0.2f, 0.2f);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            
            RectTransform nameRect = nameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 0);
            nameRect.sizeDelta = new Vector2(-20, 40);
            nameRect.anchoredPosition = new Vector2(0, 30);
            
            // Button (entire item is clickable)
            Button button = item.AddComponent<Button>();
            button.targetGraphic = itemBg;
            button.onClick.AddListener(() => {
                OnItemClicked(campaign);
            });
            
            // Track impression
            TrackImpression(campaign);
            
            return item;
        }
        
        private void PopulateItemFallback(GameObject item, Campaign campaign)
        {
            // Try to find and populate standard UI elements
            var icon = item.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                LoadCampaignIcon(icon, campaign);
            }
            
            var nameText = item.transform.Find("Name")?.GetComponent<Text>();
            if (nameText != null)
            {
                nameText.text = campaign.name ?? "Game";
            }
            
            // Wire up button if it exists
            var button = item.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnItemClicked(campaign));
            }
            
            TrackImpression(campaign);
        }
        
        private void LoadCampaignIcon(Image icon, Campaign campaign)
        {
            // Try to load icon from campaign creatives
            var creative = campaign?.target_project?.creatives != null 
                ? System.Array.Find(campaign.target_project.creatives, c => c.format == "icon")
                : null;
            if (creative?.variants != null && creative.variants.Length > 0)
            {
                string localKey = creative.variants[0].local_key;
                if (!string.IsNullOrEmpty(localKey))
                {
                    string resourcePath = localKey.StartsWith("BoostOps/") ? localKey : $"BoostOps/{localKey}";
                    var sprite = Resources.Load<Sprite>(resourcePath);
                    
                    if (sprite == null)
                    {
                        var texture = Resources.Load<Texture2D>(resourcePath);
                        if (texture != null)
                        {
                            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        }
                    }
                    
                    if (sprite != null)
                    {
                        icon.sprite = sprite;
                        icon.color = Color.white;
                        return;
                    }
                }
            }
            
            // Fallback to placeholder color
            icon.color = new Color(0.7f, 0.7f, 0.7f);
        }
        
        private void OnItemClicked(Campaign campaign)
        {
            Debug.Log($"[BoostOpsAppWallDisplay] Item clicked: {campaign.name}");
            
            // Track click
            TrackClick(campaign);
            
            // Open store
            OpenStore(campaign);
        }
        
        private void TrackImpression(Campaign campaign)
        {
            BoostOpsAnalyticsContract.TrackImpression(
                campaignSlug: campaign.campaign_id ?? campaign.name,
                placement: placement,
                // Note: source_store_id is in context.store_id (universal) - not passed here
                // Note: source_project_id is derived server-side from project_key
                targetStoreId: BoostOpsAnalyticsContract.GetTargetStoreId(campaign),
                targetProjectId: BoostOpsAnalyticsContract.GetTargetProjectId(campaign),
                format: "app_wall",
                channel: "xpromo"
            );
        }
        
        private void TrackClick(Campaign campaign)
        {
            BoostOpsAnalyticsContract.TrackClick(
                campaignSlug: campaign.campaign_id ?? campaign.name,
                placement: placement,
                // Note: source_store_id is in context.store_id (universal) - not passed here
                // Note: source_project_id is derived server-side from project_key
                targetStoreId: BoostOpsAnalyticsContract.GetTargetStoreId(campaign),
                targetProjectId: BoostOpsAnalyticsContract.GetTargetProjectId(campaign),
                format: "app_wall",
                channel: "xpromo"
            );
        }
        
        private void OpenStore(Campaign campaign)
        {
            if (campaign?.target_project?.store_urls == null)
            {
                Debug.LogWarning($"[BoostOpsAppWallDisplay] No store URLs for campaign: {campaign?.name}");
                return;
            }
            
            string storeUrl = GetPlatformStoreUrl(campaign.target_project.store_urls);
            
            if (!string.IsNullOrEmpty(storeUrl))
            {
                Application.OpenURL(storeUrl);
                Debug.Log($"[BoostOpsAppWallDisplay] Opened store: {storeUrl}");
            }
        }
        
        private string GetPlatformStoreUrl(StoreUrls storeUrls)
        {
#if UNITY_IOS
            return storeUrls.apple;
#elif UNITY_ANDROID
            return storeUrls.google ?? storeUrls.amazon ?? storeUrls.samsung;
#else
            return storeUrls.google ?? storeUrls.apple ?? storeUrls.web;
#endif
        }
    }
}

