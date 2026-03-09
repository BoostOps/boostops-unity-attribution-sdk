using UnityEngine;
using UnityEngine.UI;

namespace BoostOps
{
    /// <summary>
    /// Component for individual app wall item (game tile in grid)
    /// Attach to app wall item prefab root object
    /// </summary>
    public class BoostOpsAppWallItem : MonoBehaviour
    {
        [Header("UI References")]
        public Image iconImage;
        public Text gameNameText;
        public Button button;
        
        private Campaign campaign;
        private string placement;
        private BoostOpsCampaignDisplay parentDisplay;
        private bool impressionTracked = false;
        
        /// <summary>
        /// Initialize the item with campaign data
        /// </summary>
        public void Initialize(Campaign campaign, string placement, BoostOpsCampaignDisplay parentDisplay)
        {
            this.campaign = campaign;
            this.placement = placement;
            this.parentDisplay = parentDisplay;
            
            // Populate UI
            if (iconImage != null)
            {
                LoadIcon();
            }
            
            if (gameNameText != null)
            {
                gameNameText.text = campaign.name ?? "Game";
            }
            
            // Wire up button
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }
            
            // Track impression
            TrackImpression();
        }
        
        private void LoadIcon()
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
                        iconImage.sprite = sprite;
                        iconImage.color = Color.white;
                        return;
                    }
                }
            }
            
            // Fallback to placeholder
            iconImage.color = new Color(0.7f, 0.7f, 0.7f);
        }
        
        private void OnClick()
        {
            Debug.Log($"[BoostOpsAppWallItem] Clicked: {campaign.name}");
            
            // Track click
            TrackClick();
            
            // Open store
            OpenStore();
        }
        
        private void TrackImpression()
        {
            if (impressionTracked) return;
            
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
            
            impressionTracked = true;
        }
        
        private void TrackClick()
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
        
        private void OpenStore()
        {
            if (campaign?.target_project?.store_urls == null)
            {
                Debug.LogWarning($"[BoostOpsAppWallItem] No store URLs for campaign: {campaign?.name}");
                return;
            }
            
            string storeUrl = GetPlatformStoreUrl(campaign.target_project.store_urls);
            
            if (!string.IsNullOrEmpty(storeUrl))
            {
                Application.OpenURL(storeUrl);
                Debug.Log($"[BoostOpsAppWallItem] Opened store: {storeUrl}");
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

