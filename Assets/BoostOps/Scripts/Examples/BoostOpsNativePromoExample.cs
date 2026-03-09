using UnityEngine;
using UnityEngine.UI;

namespace BoostOps.Examples
{
    /// <summary>
    /// Example: Simple native cross-promo implementation
    /// Shows how to use the simplified BoostOpsPromo API
    /// </summary>
    public class BoostOpsNativePromoExample : MonoBehaviour
    {
        [Header("UI References")]
        public Image iconImage;
        public Text appNameText;
        public Text descriptionText;
        public Button installButton;
        
        [Header("Settings")]
        public string placement = "lobby_icon";
        
        private BoostOpsPromo promo;
        
        void Start()
        {
            LoadPromo();
        }
        
        void LoadPromo()
        {
            // 1. Fetch render data + tracking context (never returns null)
            promo = BoostOpsSDK.GetNativePromo(placement);
            
            // 2. Check if a campaign is available (Null Object pattern)
            if (!promo.IsAvailable)
            {
                Debug.Log($"[NativePromo] No promo available for placement: {placement}");
                gameObject.SetActive(false);
                return;
            }
            
            // 3. Render your custom UI
            RenderPromo();
            
            // 4. Track impression (sets UnitInstanceId automatically)
            BoostOpsSDK.TrackImpression(promo);
            
            // 5. Wire up click handler
            installButton.onClick.AddListener(OnInstallClick);
        }
        
        void RenderPromo()
        {
            // Set app name
            appNameText.text = promo.Name;
            
            // Load icon from campaign creatives
            var iconVariant = promo.GetBestVariant(CreativeFormat.Icon);
            if (iconVariant != null)
            {
                LoadIcon(iconVariant.local_key);
            }
            
            // Optional: Set description if you have it
            if (descriptionText != null)
            {
                descriptionText.text = $"Try {promo.Name}!";
            }
            
            Debug.Log($"[NativePromo] Rendered: {promo.Name} (instance: {promo.UnitInstanceId})");
        }
        
        void LoadIcon(string localKey)
        {
            string resourcePath = localKey.StartsWith("BoostOps/") 
                ? localKey 
                : $"BoostOps/{localKey}";
            
            var sprite = Resources.Load<Sprite>(resourcePath);
            
            if (sprite != null)
            {
                iconImage.sprite = sprite;
            }
            else
            {
                Debug.LogWarning($"[NativePromo] Icon not found: {resourcePath}");
            }
        }
        
        void OnInstallClick()
        {
            // Track click + open store (all in one!)
            // Automatically reuses promo.UnitInstanceId for attribution
            BoostOpsSDK.Click(promo);
        }
    }
}

