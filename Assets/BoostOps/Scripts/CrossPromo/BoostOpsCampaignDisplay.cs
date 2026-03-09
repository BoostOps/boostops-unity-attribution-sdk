using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BoostOps.CrossPromo;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoostOps
{
    /// <summary>
    /// Component for displaying cross-promotion campaigns in Unity UI
    /// Supports multiple display modes: Banner, Interstitial, Native
    /// </summary>
    public class BoostOpsCampaignDisplay : MonoBehaviour
    {
        [Header("Display Configuration")]
        public CampaignDisplayMode displayMode = CampaignDisplayMode.Banner;
        public string placementId = "default";
        public bool autoShow = false;
        public float autoShowDelay = 2f;
        
        [Header("UI References")]
        public Canvas targetCanvas;
        
        [Header("Campaign Prefabs")]
        [Tooltip("Banner prefab (optional override - uses default if null)")]
        public GameObject bannerPrefab;
        [Tooltip("Icon interstitial prefab - simple popup with app icon (optional override)")]
        public GameObject iconInterstitialPrefab;
        [Tooltip("Rich interstitial prefab - full-screen with hero image (optional override)")]
        public GameObject richInterstitialPrefab;
        [Tooltip("Native prefab (optional override - uses default if null)")]
        public GameObject nativePrefab;
        [Tooltip("App wall prefab - grid of multiple apps (optional override)")]
        public GameObject appWallPrefab;
        [Tooltip("App wall item prefab - individual game tile in grid")]
        public GameObject appWallItemPrefab;
        
        [Header("Banner Settings")]
        public BannerPosition bannerPosition = BannerPosition.Bottom;
        public Vector2 bannerSize = new Vector2(320, 50);
        
        [Header("Interstitial Settings")]
        public bool pauseGameOnInterstitial = true;
        public float interstitialDuration = -1f; // -1 = disabled, positive = auto-hide after X seconds
        
        // Current campaign being displayed
        private Campaign currentCampaign;
        private GameObject currentDisplayObject;
        private bool isShowing = false;
        
        public enum CampaignDisplayMode
        {
            Banner,           // Small banner at top/bottom
            IconInterstitial, // Simple popup with app icon focus
            RichInterstitial, // Full-screen popup with hero image
            Native,           // Custom integrated display
            AppWall           // Grid of multiple apps (portfolio showcase)
        }
        
        public enum BannerPosition
        {
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
        
        void Start()
        {
            // Auto-find canvas if not set
            if (targetCanvas == null)
            {
                targetCanvas = FindFirstObjectByType<Canvas>();
            }
            
            if (autoShow)
            {
                StartCoroutine(AutoShowCampaign());
            }
        }
        
        /// <summary>
        /// Show a random eligible campaign
        /// </summary>
        public void ShowRandomCampaign()
        {
            Debug.LogWarning("[BoostOps] ShowRandomCampaign called but SDK integration required for campaign data.");
            
            // For DLL usage, create a dummy campaign for testing
            var dummyCampaign = CreateDummyCampaign();
            ShowCampaign(dummyCampaign);
        }
        
        /// <summary>
        /// Create a dummy campaign for DLL compatibility
        /// </summary>
        private Campaign CreateDummyCampaign()
        {
            return new Campaign
            {
                campaign_id = "dummy_campaign",
                name = "Sample Campaign",
                status = "active"
            };
        }
        
        /// <summary>
        /// Show a specific campaign
        /// </summary>
        public void ShowCampaign(Campaign campaign)
        {
            if (campaign == null || isShowing) return;
            
            currentCampaign = campaign;
            isShowing = true;
            
            // Note: TrackImpression is handled by BoostOpsManager.ShowCrossPromo to avoid double-counting
            
            // Create display based on mode
            switch (displayMode)
            {
                case CampaignDisplayMode.Banner:
#if UNITY_IOS && !UNITY_EDITOR
                    Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Calling ShowBanner");
#endif
                    ShowBanner(campaign);
                    break;
                case CampaignDisplayMode.IconInterstitial:
#if UNITY_IOS && !UNITY_EDITOR
                    Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Calling ShowIconInterstitial");
#endif
                    ShowIconInterstitial(campaign);
                    break;
                case CampaignDisplayMode.RichInterstitial:
#if UNITY_IOS && !UNITY_EDITOR
                    Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Calling ShowRichInterstitial");
#endif
                    ShowRichInterstitial(campaign);
                    break;
                case CampaignDisplayMode.Native:
#if UNITY_IOS && !UNITY_EDITOR
                    Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Calling ShowNative");
#endif
                    ShowNative(campaign);
                    break;
                case CampaignDisplayMode.AppWall:
                    Debug.LogWarning("[BoostOpsCampaignDisplay] AppWall mode requires ShowAppWall(campaigns) method with multiple campaigns");
                    break;
            }
            
#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log($"[BoostOpsCampaignDisplay] [iOS] ShowCampaign method completed");
#endif
        }
        
        /// <summary>
        /// Show multiple campaigns in an app wall grid
        /// </summary>
        public void ShowAppWall(List<Campaign> campaigns)
        {
            if (campaigns == null || campaigns.Count == 0)
            {
                Debug.LogWarning("[BoostOpsCampaignDisplay] ShowAppWall: No campaigns provided");
                return;
            }
            
            if (isShowing)
            {
                Debug.LogWarning("[BoostOpsCampaignDisplay] ShowAppWall: Display already showing");
                return;
            }
            
            isShowing = true;
            
            Debug.Log($"[BoostOpsCampaignDisplay] Showing app wall with {campaigns.Count} campaigns");
            
            if (appWallPrefab == null)
            {
                CreateDefaultAppWall(campaigns);
            }
            else
            {
                ShowAppWallPrefab(campaigns);
            }
        }
        
        /// <summary>
        /// Hide current campaign display
        /// </summary>
        public void HideCampaign()
        {
            if (currentDisplayObject != null)
            {
                Destroy(currentDisplayObject);
                currentDisplayObject = null;
            }
            
            isShowing = false;
            currentCampaign = null;
            
            // Resume game if paused
            if (pauseGameOnInterstitial && Time.timeScale == 0)
            {
                Time.timeScale = 1f;
            }
        }
        
        /// <summary>
        /// Handle campaign click
        /// </summary>
        public void OnCampaignClicked()
        {
            if (currentCampaign != null)
            {
                Debug.Log($"[BoostOps] Campaign clicked: {currentCampaign?.name}");
                
                // Track click analytics - route to Firebase/Unity Analytics in local mode, BoostOps in server mode
                TrackCampaignClick(currentCampaign);
                
                // Navigate to store URL
                OpenCampaignStoreUrl(currentCampaign);
                
                // Hide display after click
                if (displayMode == CampaignDisplayMode.IconInterstitial || displayMode == CampaignDisplayMode.RichInterstitial)
                {
                    HideCampaign();
                }
            }
        }
        
        /// <summary>
        /// Track campaign click analytics
        /// Delegates to BoostOpsManagerInternal which has impression tracking data
        /// </summary>
        private void TrackCampaignClick(Campaign campaign)
        {
            try
            {
                // Determine format based on display mode
                string format = GetFormatFromDisplayMode();
                
                // Delegate to BoostOpsManagerInternal which tracks impressions and has impression_id
                var manager = BoostOps.Internal.BoostOpsManagerInternal.Instance;
                if (manager != null)
                {
                    // Manager has impression tracking and will link click to impression
                    manager.TrackClick(campaign, placementId, format);
                    // Debug.Log($"[BoostOpsCampaignDisplay] ✅ Tracked click via manager (format: {format})");
                }
                else
                {
                    // Fallback: Track without impression linking (shouldn't happen in normal operation)
                    Debug.LogWarning($"[BoostOpsCampaignDisplay] ⚠️ Manager not available - tracking click without impression linking");
                    
                    string campaignSlug = campaign?.campaign_id ?? campaign?.name ?? "";
                    string targetStoreId = BoostOpsAnalyticsContract.GetTargetStoreId(campaign);
                    string targetProjectId = BoostOpsAnalyticsContract.GetTargetProjectId(campaign);
                    string placement = placementId ?? "unknown";
                    
                    BoostOpsAnalyticsContract.TrackClick(
                        campaignSlug: campaignSlug,
                        placement: placement,
                        targetStoreId: targetStoreId,
                        targetProjectId: targetProjectId,
                        format: format,
                        channel: "xpromo"
                        // ⚠️ Missing impression_id - manager not available
                    );
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsCampaignDisplay] ❌ Failed to track click analytics: {ex.Message}");
                // Don't crash on analytics failures
            }
        }
        
        /// <summary>
        /// Get analytics format string from display mode
        /// </summary>
        private string GetFormatFromDisplayMode()
        {
            switch (displayMode)
            {
                case CampaignDisplayMode.Banner:
                    return "banner";
                case CampaignDisplayMode.IconInterstitial:
                    return "icon";
                case CampaignDisplayMode.RichInterstitial:
                    return "rich_interstitial";
                case CampaignDisplayMode.Native:
                    return "native";
                case CampaignDisplayMode.AppWall:
                    return "app_wall";
                default:
                    return "unknown";
            }
        }
        
        /// <summary>
        /// Open store URL for the current platform
        /// </summary>
        private void OpenCampaignStoreUrl(Campaign campaign)
        {
            if (campaign?.target_project?.store_urls == null)
            {
                Debug.LogWarning($"[BoostOpsCampaignDisplay] No store URLs available for campaign '{campaign?.name}'");
                return;
            }
            
            string storeUrl = GetPlatformStoreUrl(campaign.target_project.store_urls);
            
            if (!string.IsNullOrEmpty(storeUrl))
            {
                Debug.Log($"[BoostOpsCampaignDisplay] Opening store URL: {storeUrl}");
                
#if UNITY_IOS && !UNITY_EDITOR
                // Check if this is an iOS App Store URL and try native sheet first
                if (storeUrl.Contains("apps.apple.com"))
                {
                    var appStoreIdMatch = System.Text.RegularExpressions.Regex.Match(storeUrl, @"id(\d+)");
                    if (appStoreIdMatch.Success)
                    {
                        string appStoreId = appStoreIdMatch.Groups[1].Value;
                        // Try native iOS App Store sheet if available
                        if (BoostOps.BoostOpsAppStoreSheet.IsAvailable())
                        {
                            bool success = BoostOps.BoostOpsAppStoreSheet.ShowAppStoreSheet(appStoreId);
                            if (success)
                            {
                                Debug.Log($"[BoostOpsCampaignDisplay] Opened native App Store sheet for ID: {appStoreId}");
                                return;
                            }
                        }
                    }
                }
#endif
                
                // Fallback to standard URL opening
                try
                {
                    Application.OpenURL(storeUrl);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BoostOpsCampaignDisplay] Failed to open store URL: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[BoostOpsCampaignDisplay] No store URL available for current platform ({Application.platform})");
            }
        }
        
        /// <summary>
        /// Get the appropriate store URL for the current platform
        /// </summary>
        private string GetPlatformStoreUrl(StoreUrls storeUrls)
        {
            if (storeUrls == null) return null;
            
#if UNITY_IOS && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(storeUrls.apple))
                return storeUrls.apple;
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(storeUrls.google))
                return storeUrls.google;
            if (!string.IsNullOrEmpty(storeUrls.amazon))
                return storeUrls.amazon;
            if (!string.IsNullOrEmpty(storeUrls.samsung))
                return storeUrls.samsung;
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(storeUrls.microsoft))
                return storeUrls.microsoft;
#elif UNITY_EDITOR
            // In Unity Editor, respect the current build target
            var buildTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
            
            if (buildTarget == UnityEditor.BuildTarget.iOS)
            {
                if (!string.IsNullOrEmpty(storeUrls.apple))
                    return storeUrls.apple;
            }
            else if (buildTarget == UnityEditor.BuildTarget.Android)
            {
                if (!string.IsNullOrEmpty(storeUrls.google))
                    return storeUrls.google;
                if (!string.IsNullOrEmpty(storeUrls.amazon))
                    return storeUrls.amazon;
                if (!string.IsNullOrEmpty(storeUrls.samsung))
                    return storeUrls.samsung;
            }
            else if (buildTarget == UnityEditor.BuildTarget.StandaloneWindows || buildTarget == UnityEditor.BuildTarget.StandaloneWindows64)
            {
                if (!string.IsNullOrEmpty(storeUrls.microsoft))
                    return storeUrls.microsoft;
            }
            
            if (!string.IsNullOrEmpty(storeUrls.google))
                return storeUrls.google;
            if (!string.IsNullOrEmpty(storeUrls.apple))
                return storeUrls.apple;
            if (!string.IsNullOrEmpty(storeUrls.amazon))
                return storeUrls.amazon;
            if (!string.IsNullOrEmpty(storeUrls.microsoft))
                return storeUrls.microsoft;
            if (!string.IsNullOrEmpty(storeUrls.samsung))
                return storeUrls.samsung;
#endif
            
            // Final fallback for unsupported platforms (shouldn't reach here in normal cases)
            if (!string.IsNullOrEmpty(storeUrls.google))
                return storeUrls.google;
            if (!string.IsNullOrEmpty(storeUrls.apple))
                return storeUrls.apple;
            if (!string.IsNullOrEmpty(storeUrls.amazon))
                return storeUrls.amazon;
            if (!string.IsNullOrEmpty(storeUrls.microsoft))
                return storeUrls.microsoft;
            if (!string.IsNullOrEmpty(storeUrls.samsung))
                return storeUrls.samsung;
                
            return null;
        }
        
        #region Display Mode Implementations
        
        private void ShowBanner(Campaign campaign)
        {
            if (bannerPrefab == null)
            {
                CreateDefaultBanner(campaign);
                return;
            }
            
            currentDisplayObject = Instantiate(bannerPrefab, targetCanvas.transform);
            PositionBanner(currentDisplayObject);
            PopulateCampaignData(currentDisplayObject, campaign);
        }
        
        private void ShowIconInterstitial(Campaign campaign)
        {
            if (iconInterstitialPrefab == null)
            {
                CreateDefaultIconInterstitial(campaign);
                return;
            }
            
            // Pause game if configured
            if (pauseGameOnInterstitial)
            {
                Time.timeScale = 0f;
            }
            
            currentDisplayObject = Instantiate(iconInterstitialPrefab, targetCanvas.transform);
            
            // iOS diagnostic logging removed to reduce verbosity
            
            PopulateCampaignData(currentDisplayObject, campaign);
            
            // Wire up background to close on click (safety fallback if close button breaks)
            Button backgroundButton = currentDisplayObject.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(HideCampaign);
            }
            
            // Find content panel and add click-eating button if it doesn't have one
            Transform panelTransform = currentDisplayObject.transform.Find("ContentPanel");
            if (panelTransform == null)
                panelTransform = currentDisplayObject.transform.Find("Panel");
            
            if (panelTransform != null)
            {
                Button panelButton = panelTransform.GetComponent<Button>();
                if (panelButton == null)
                {
                    // Add button to eat clicks on content panel
                    panelButton = panelTransform.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                    panelButton.onClick.AddListener(() => { /* Eat click - do nothing */ });
                }
            }
            
            // iOS component diagnostic logging removed to reduce verbosity
            
            // Start fade-in animation if CanvasGroup is present
            if (currentDisplayObject != null)
            {
                CanvasGroup canvasGroup = currentDisplayObject.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
#if UNITY_IOS && !UNITY_EDITOR
                    Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Starting fade-in animation (CanvasGroup alpha: {canvasGroup.alpha})");
#endif
                    StartCoroutine(FadeInInterstitial(canvasGroup, 0.25f));
                }
            }
#if UNITY_IOS && !UNITY_EDITOR
            else
            {
                Debug.Log($"[BoostOpsCampaignDisplay] [iOS] No CanvasGroup found - no fade animation");
            }
#endif
            
            // Auto-hide after duration (if enabled)
            if (interstitialDuration > 0)
            {
#if UNITY_IOS && !UNITY_EDITOR
                Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Starting auto-hide timer: {interstitialDuration} seconds");
#endif
                StartCoroutine(AutoHideInterstitial());
            }
        }
        
        private void ShowRichInterstitial(Campaign campaign)
        {
            if (richInterstitialPrefab == null)
            {
                CreateDefaultRichInterstitial(campaign);
                return;
            }
            
            // Pause game if configured
            if (pauseGameOnInterstitial)
            {
                Time.timeScale = 0f;
            }
            
            currentDisplayObject = Instantiate(richInterstitialPrefab, targetCanvas.transform);
            PopulateCampaignData(currentDisplayObject, campaign);
            
            // Wire up background to close on click (safety fallback if close button breaks)
            Button backgroundButton = currentDisplayObject.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(HideCampaign);
            }
            
            // Find content panel and add click-eating button if it doesn't have one
            Transform panelTransform = currentDisplayObject.transform.Find("ContentPanel");
            if (panelTransform == null)
                panelTransform = currentDisplayObject.transform.Find("Panel");
            
            if (panelTransform != null)
            {
                Button panelButton = panelTransform.GetComponent<Button>();
                if (panelButton == null)
                {
                    // Add button to eat clicks on content panel
                    panelButton = panelTransform.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                    panelButton.onClick.AddListener(() => { /* Eat click - do nothing */ });
                }
            }
            
            // Start fade-in animation if CanvasGroup is present
            if (currentDisplayObject != null)
            {
                CanvasGroup canvasGroup = currentDisplayObject.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    StartCoroutine(FadeInInterstitial(canvasGroup, 0.25f));
                }
            }
            
            // Auto-hide after duration (if enabled)
            if (interstitialDuration > 0)
            {
                StartCoroutine(AutoHideInterstitial());
            }
        }
        
        private void ShowNative(Campaign campaign)
        {
            if (nativePrefab == null)
            {
                Debug.LogWarning("[BoostOps] Native prefab not set for native display mode.");
                return;
            }
            
            currentDisplayObject = Instantiate(nativePrefab, targetCanvas.transform);
            PopulateCampaignData(currentDisplayObject, campaign);
        }
        
        private void ShowAppWallPrefab(List<Campaign> campaigns)
        {
            // Pause game if configured (app wall is full-screen like interstitials)
            if (pauseGameOnInterstitial)
            {
                Time.timeScale = 0f;
            }
            
            currentDisplayObject = Instantiate(appWallPrefab, targetCanvas.transform);
            
            // Find the BoostOpsAppWallDisplay component and initialize it
            var appWallDisplay = currentDisplayObject.GetComponent<BoostOpsAppWallDisplay>();
            if (appWallDisplay != null)
            {
                appWallDisplay.Initialize(campaigns, placementId, this);
            }
            else
            {
                Debug.LogError("[BoostOpsCampaignDisplay] AppWall prefab missing BoostOpsAppWallDisplay component");
                HideCampaign();
            }
        }
        
        private void CreateDefaultAppWall(List<Campaign> campaigns)
        {
            Debug.Log("[BoostOpsCampaignDisplay] Creating default app wall UI");
            
            // Pause game if configured
            if (pauseGameOnInterstitial)
            {
                Time.timeScale = 0f;
            }
            
            // Create full-screen app wall container
            GameObject appWall = new GameObject("BoostOpsAppWall");
            appWall.transform.SetParent(targetCanvas.transform, false);
            
            // Add CanvasGroup for fade animation
            CanvasGroup canvasGroup = appWall.AddComponent<CanvasGroup>();
            
            // Semi-transparent full-screen background
            Image bg = appWall.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            // Background closes app wall on click
            Button bgButton = appWall.AddComponent<Button>();
            bgButton.transition = Selectable.Transition.None;
            bgButton.onClick.AddListener(HideCampaign);
            
            // Content panel - match prefab structure with portrait support
            GameObject panel = new GameObject("ContentPanel");
            panel.transform.SetParent(appWall.transform, false);
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = Color.white;
            
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            
            // Adjust panel size based on orientation
            bool isLandscape = Screen.width > Screen.height;
            if (isLandscape)
            {
                // Landscape: match prefab size (1080 x 720)
                panelRect.sizeDelta = new Vector2(1080, 720);
            }
            else
            {
                // Portrait: narrower panel to fit better (720 x 900 - rotated dimensions)
                panelRect.sizeDelta = new Vector2(720, 900);
            }
            
            // Header
            GameObject header = new GameObject("Header");
            header.transform.SetParent(panel.transform, false);
            Text headerText = header.AddComponent<Text>();
            headerText.text = "Our Games";
            headerText.fontSize = 32;
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = new Color(0.2f, 0.2f, 0.2f);
            headerText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.sizeDelta = new Vector2(0, 60);
            headerRect.anchoredPosition = new Vector2(0, -30);
            
            // Close button
            GameObject closeButton = new GameObject("CloseButton");
            closeButton.transform.SetParent(panel.transform, false);
            Image closeBg = closeButton.AddComponent<Image>();
            closeBg.color = new Color(0.6f, 0.6f, 0.6f);
            
            Button close = closeButton.AddComponent<Button>();
            close.onClick.AddListener(HideCampaign);
            
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.sizeDelta = new Vector2(50, 50);
            closeRect.anchoredPosition = new Vector2(-25, -25);
            
            GameObject closeText = new GameObject("Text");
            closeText.transform.SetParent(closeButton.transform, false);
            Text closeTextComp = closeText.AddComponent<Text>();
            closeTextComp.text = "×";
            closeTextComp.fontSize = 36;
            closeTextComp.fontStyle = FontStyle.Bold;
            closeTextComp.color = Color.white;
            closeTextComp.alignment = TextAnchor.MiddleCenter;
            
            RectTransform closeTextRect = closeText.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            
            // ScrollView
            GameObject scrollView = new GameObject("ScrollView");
            scrollView.transform.SetParent(panel.transform, false);
            
            RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.offsetMin = new Vector2(20, 20);
            scrollRect.offsetMax = new Vector2(-20, -80);
            
            ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            
            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            
            Image viewportMask = viewport.AddComponent<Image>();
            viewportMask.color = Color.white;
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            
            scroll.viewport = viewportRect;
            
            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            
            scroll.content = contentRect;
            
            // Grid Layout - match prefab structure with portrait support
            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            
            // Adapt columns and sizing based on orientation to match prefab proportions
            if (isLandscape)
            {
                // Landscape: match prefab (3 columns, 300x400 cells, 36px spacing)
                grid.constraintCount = 3;
                grid.cellSize = new Vector2(300, 400);
                grid.spacing = new Vector2(36, 36);
            }
            else
            {
                // Portrait: 2 columns, proportionally smaller cells to fit
                grid.constraintCount = 2;
                grid.cellSize = new Vector2(300, 400);
                grid.spacing = new Vector2(36, 36);
            }
            
            grid.padding = new RectOffset(0, 0, 0, 0);
            
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // Create items for each campaign
            foreach (var campaign in campaigns)
            {
                CreateAppWallItem(content.transform, campaign);
            }
            
            // Fade in
            StartCoroutine(FadeInInterstitial(canvasGroup, 0.25f));
            
            currentDisplayObject = appWall;
        }
        
        private void CreateAppWallItem(Transform parent, Campaign campaign)
        {
            GameObject item = new GameObject($"AppWallItem_{campaign.campaign_id}");
            item.transform.SetParent(parent, false);
            
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
            
            // Load icon
            LoadCampaignImage(icon, campaign, "icon");
            
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
                // Store current campaign temporarily
                currentCampaign = campaign;
                OnCampaignClicked();
            });
            
            // Track impression
            BoostOpsAnalyticsContract.TrackImpression(
                campaignSlug: campaign.campaign_id ?? campaign.name,
                placement: placementId,
                // Note: source_store_id is in context.store_id (universal) - not passed here
                // Note: source_project_id is derived server-side from project_key
                targetStoreId: BoostOpsAnalyticsContract.GetTargetStoreId(campaign),
                targetProjectId: BoostOpsAnalyticsContract.GetTargetProjectId(campaign),
                format: "app_wall",
                channel: "xpromo"
            );
        }
        
        #endregion
        
        #region Default UI Creation
        
        private void CreateDefaultBanner(Campaign campaign)
        {
            // Create simple banner UI
            GameObject banner = new GameObject("BoostOps_Banner");
            banner.transform.SetParent(targetCanvas.transform, false);
            
            // Add background
            Image bg = banner.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            
            // Add button component
            Button button = banner.AddComponent<Button>();
            button.onClick.AddListener(OnCampaignClicked);
            
            // Add text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(banner.transform, false);
            Text text = textObj.AddComponent<Text>();
            text.text = $"Try {campaign.Name}!";
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            
            // Set size and position
            RectTransform rectTransform = banner.GetComponent<RectTransform>();
            rectTransform.sizeDelta = bannerSize;
            PositionBanner(banner);
            
            currentDisplayObject = banner;
        }
        
        private void CreateDefaultIconInterstitial(Campaign campaign)
        {
#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log($"[BoostOpsCampaignDisplay] [iOS] CreateDefaultIconInterstitial called - prefab was null");
            Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Target canvas for default creation: {targetCanvas?.name}");
#endif
            
            // Create full-screen interstitial overlay (reverse-engineered from DefaultIconInterstitialPrefab)
            GameObject interstitial = new GameObject("DefaultIconInterstitialPrefab");
            interstitial.transform.SetParent(targetCanvas.transform, false);
            
            // Semi-transparent full-screen background (dims game and blocks clicks)
            Image bg = interstitial.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.588f); // Match prefab alpha: 0.5882353
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            // Add button component to background (no transition to match prefab)
            // Clicking outside the popup (on the background) will close it
            Button bgButton = interstitial.AddComponent<Button>();
            bgButton.transition = Selectable.Transition.None;
            bgButton.onClick.AddListener(HideCampaign);
            
            // Add CanvasGroup for fade animations (matches prefab)
            CanvasGroup canvasGroup = interstitial.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            
            // Content panel (700×600px - matching prefab dimensions)
            GameObject panel = new GameObject("ContentPanel");
            panel.transform.SetParent(interstitial.transform, false);
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = Color.white; // Clean white background
            
            // Try to load the actual panel sprite from Resources (matches prefab)
            Sprite panelSprite = Resources.Load<Sprite>("BoostOps/UI/panel_rounded");
            if (panelSprite != null)
            {
                panelBg.sprite = panelSprite;
                panelBg.type = Image.Type.Sliced;
            }
            
            // Add button to panel to block/eat clicks (prevent closing when clicking on white area)
            Button panelButton = panel.AddComponent<Button>();
            panelButton.transition = Selectable.Transition.None;
            panelButton.onClick.AddListener(() => { /* Eat click - do nothing */ });
            
            // Add shadow effect (matches prefab)
            Shadow panelShadow = panel.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0, 0, 0, 0.314f); // Match prefab shadow
            panelShadow.effectDistance = new Vector2(0, -6);
            panelShadow.useGraphicAlpha = true;
            
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700, 600); // Match prefab size
            panelRect.anchoredPosition = Vector2.zero; // Centered
            
            // Panel border (subtle overlay - matches prefab)
            GameObject panelBorder = new GameObject("PanelBorder");
            panelBorder.transform.SetParent(panel.transform, false);
            Image borderBg = panelBorder.AddComponent<Image>();
            borderBg.color = new Color(0.8f, 0.8f, 0.8f, 0.3f);
            borderBg.enabled = false; // Disabled by default like in prefab
            
            // Try to load border sprite from Resources
            Sprite borderSprite = Resources.Load<Sprite>("BoostOps/UI/border_rounded");
            if (borderSprite != null)
            {
                borderBg.sprite = borderSprite;
                borderBg.type = Image.Type.Sliced;
            }
            
            RectTransform borderRect = borderBg.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = new Vector2(4, 4); // Match prefab border offset
            borderRect.anchoredPosition = Vector2.zero;
            
            // Icon mask container (rounded corners for icon - matches prefab)
            GameObject iconMask = new GameObject("IconMask");
            iconMask.transform.SetParent(panel.transform, false);
            Image maskBg = iconMask.AddComponent<Image>();
            maskBg.color = Color.white;
            
            // Try to load the mask sprite from Resources
            Sprite maskSprite = Resources.Load<Sprite>("BoostOps/UI/icon_mask_rounded");
            if (maskSprite != null)
            {
                maskBg.sprite = maskSprite;
                maskBg.type = Image.Type.Sliced;
            }
            
            // Add mask component for rounded icon appearance
            Mask iconMaskComponent = iconMask.AddComponent<Mask>();
            iconMaskComponent.showMaskGraphic = false; // Hide mask graphic like in prefab
            
            RectTransform iconMaskRect = iconMask.GetComponent<RectTransform>();
            iconMaskRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconMaskRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconMaskRect.sizeDelta = new Vector2(210, 210); // Match prefab icon container size
            iconMaskRect.anchoredPosition = new Vector2(0, 68); // Match prefab positioning
            
            // Campaign icon (inside mask for rounded appearance)
            GameObject iconObj = new GameObject("CampaignIcon");
            iconObj.transform.SetParent(iconMask.transform, false);
            Image icon = iconObj.AddComponent<Image>();
            icon.color = Color.white; // Placeholder - will be replaced by actual icon
            
            // Add explicit role identifier for asset loading (using GameObject name instead of component)
            iconObj.name = "CampaignIcon"; // Standard name for campaign icon images
            
            // Add shadow effect to icon (matches prefab)
            Shadow iconShadow = iconObj.AddComponent<Shadow>();
            iconShadow.effectColor = new Color(0, 0.58f, 1, 0.5f); // Blue shadow to match prefab
            iconShadow.effectDistance = new Vector2(2, -2);
            iconShadow.useGraphicAlpha = true;
            
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero; // Fill mask container
            iconRect.anchoredPosition = Vector2.zero;
            
            // ✅ LOAD THE ACTUAL CAMPAIGN ICON
            LoadCampaignImage(icon, campaign, "icon");
            // Debug.Log($"[BoostOpsCampaignDisplay] Loading campaign icon for '{campaign.name}'");
            
            // Description text (matches prefab)
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(panel.transform, false);
            Text desc = descObj.AddComponent<Text>();
            
            // Use default description (SourceProject requires SDK integration)
            BoostOps.Internal.ISourceProject sourceProject = null;
            
            string descriptionText = sourceProject?.DefaultIconInterstitialDescription ?? "Try our new game!";
            desc.text = descriptionText;
            
            desc.fontSize = 42;
            desc.color = new Color(0.3f, 0.3f, 0.3f, 1); // Match prefab text color
            desc.alignment = TextAnchor.MiddleCenter;
            desc.fontStyle = FontStyle.Normal;
            desc.resizeTextForBestFit = true;
            desc.resizeTextMinSize = 18; // Ensure minimum readable size
            desc.resizeTextMaxSize = 42;
            
            // Ensure we have a font (use Unity's built-in font as fallback)
            if (desc.font == null)
            {
                desc.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            
            RectTransform descRect = desc.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.5f, 0.5f);
            descRect.anchorMax = new Vector2(0.5f, 0.5f);
            descRect.sizeDelta = new Vector2(560, 50); // Match prefab description size
            descRect.anchoredPosition = new Vector2(0, -113); // Match prefab positioning
            
            // Play button (matches prefab styling)
            GameObject buttonObj = new GameObject("PlayButton");
            buttonObj.transform.SetParent(panel.transform, false);
            Image buttonBg = buttonObj.AddComponent<Image>();
            buttonBg.color = new Color(0, 0.58f, 1, 1); // Match prefab blue color
            
            // Try to load button sprite from Resources
            Sprite buttonSprite = Resources.Load<Sprite>("BoostOps/UI/button_rounded");
            if (buttonSprite != null)
            {
                buttonBg.sprite = buttonSprite;
                buttonBg.type = Image.Type.Sliced;
            }
            
            Button button = buttonObj.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint; // Match prefab transition
            button.onClick.AddListener(OnCampaignClicked);
            
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0, 0);
            buttonRect.anchorMax = new Vector2(1, 0);
            buttonRect.sizeDelta = new Vector2(-80, 80); // Match prefab button sizing
            buttonRect.anchoredPosition = new Vector2(0, 32); // Match prefab positioning
            
            // Button text
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            Text buttonText = buttonTextObj.AddComponent<Text>();
            
            // Get button text from SourceProject settings
            string buttonTextStr = sourceProject?.DefaultIconInterstitialButtonText ?? "Play Now!";
            buttonText.text = buttonTextStr;
            
            buttonText.fontSize = 36; // Match prefab font size
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            
            RectTransform buttonTextRect = buttonText.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
            buttonTextRect.anchoredPosition = Vector2.zero;
            
            // Close button (matches prefab - top-right corner)
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(panel.transform, false);
            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0.557f, 0.557f, 0.576f, 1); // Match prefab close button color
            
            // Try to load close button sprite from Resources
            Sprite closeSprite = Resources.Load<Sprite>("BoostOps/UI/close_button");
            if (closeSprite != null)
            {
                closeBg.sprite = closeSprite;
                closeBg.type = Image.Type.Simple;
            }
            
            Button closeButton = closeObj.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.ColorTint; // Match prefab
            closeButton.onClick.AddListener(HideCampaign);
            
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.sizeDelta = new Vector2(64, 63); // Match prefab close button size
            closeRect.anchoredPosition = new Vector2(-20, -20); // Match prefab positioning
            
            // Close button text (X symbol)
            GameObject closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeObj.transform, false);
            Text closeText = closeTextObj.AddComponent<Text>();
            closeText.text = "×";
            closeText.fontSize = 52; // Match prefab close text size
            closeText.color = Color.white;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.fontStyle = FontStyle.Bold;
            
            RectTransform closeTextRect = closeText.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            closeTextRect.anchoredPosition = Vector2.zero;
            
            // Add fade-in animation for smooth appearance
            StartCoroutine(FadeInInterstitial(canvasGroup, 0.25f)); // Quick 0.25s fade
            
            currentDisplayObject = interstitial;
        }
        
        private void CreateDefaultRichInterstitial(Campaign campaign)
        {
            // Create full-screen interstitial overlay
            GameObject interstitial = new GameObject("BoostOps_Interstitial");
            interstitial.transform.SetParent(targetCanvas.transform, false);
            
            // Semi-transparent full-screen background (dims game and blocks clicks)
            Image bg = interstitial.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f); // Semi-transparent black overlay
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            // Add button component to background - clicking outside closes the popup
            Button bgButton = interstitial.AddComponent<Button>();
            bgButton.transition = Selectable.Transition.None;
            bgButton.onClick.AddListener(HideCampaign);
            
            // Content panel - Large full-screen style (90% of screen)
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(interstitial.transform, false);
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = Color.white;
            
            // Add button to panel to block/eat clicks (prevent closing when clicking on white area)
            Button panelButton = panel.AddComponent<Button>();
            panelButton.transition = Selectable.Transition.None;
            panelButton.onClick.AddListener(() => { /* Eat click - do nothing */ });
            
            // Add subtle border effect (replaced Shadow for better compatibility)
            GameObject border = new GameObject("PanelBorder");
            border.transform.SetParent(panel.transform, false);
            Image borderBg = border.AddComponent<Image>();
            borderBg.color = new Color(0.8f, 0.8f, 0.8f, 0.3f);
            RectTransform borderRect = borderBg.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);
            border.transform.SetSiblingIndex(0); // Behind panel
            
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            // Use anchor-based sizing for responsive full-screen layout
            panelRect.anchorMin = new Vector2(0.05f, 0.05f); // 5% margin from edges
            panelRect.anchorMax = new Vector2(0.95f, 0.95f); // 5% margin from edges
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            
            // Campaign screenshot (large hero image for rich interstitial - fills most of upper panel)
            GameObject screenshotObj = new GameObject("CampaignHero");
            screenshotObj.transform.SetParent(panel.transform, false);
            Image screenshot = screenshotObj.AddComponent<Image>();
            screenshot.color = new Color(0.7f, 0.7f, 0.7f); // Will be replaced by actual screenshot
            
            // Add explicit role identifier to avoid string matching bugs (using GameObject name)
            screenshotObj.name = "CampaignHero"; // Standard name for campaign hero images
            
            RectTransform screenshotRect = screenshot.GetComponent<RectTransform>();
            // Use anchor-based sizing for responsive layout - takes up most of the upper half
            screenshotRect.anchorMin = new Vector2(0.05f, 0.5f);
            screenshotRect.anchorMax = new Vector2(0.95f, 0.85f);
            screenshotRect.offsetMin = Vector2.zero;
            screenshotRect.offsetMax = Vector2.zero;
            
            // Description text - positioned below screenshot using anchors, uses CrossPromoTable settings for rich interstitial
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(panel.transform, false);
            Text desc = descObj.AddComponent<Text>();
            
            // Use default description (SourceProject requires SDK integration)
            string descriptionText = "Join millions of players in this amazing adventure!";
            desc.text = descriptionText;
            
            desc.fontSize = 22; // Larger font for bigger display
            desc.color = new Color(0.3f, 0.3f, 0.3f);
            desc.alignment = TextAnchor.MiddleCenter;
            RectTransform descRect = desc.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.05f, 0.28f);
            descRect.anchorMax = new Vector2(0.95f, 0.42f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            
            // Play button - prominent call-to-action with anchor-based sizing
            GameObject buttonObj = new GameObject("PlayButton");
            buttonObj.transform.SetParent(panel.transform, false);
            Image buttonBg = buttonObj.AddComponent<Image>();
            buttonBg.color = new Color(0.2f, 0.7f, 1f); // Nice blue color
            Button button = buttonObj.AddComponent<Button>();
            button.onClick.AddListener(OnCampaignClicked);
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            // Center button with larger size for full-screen layout
            buttonRect.anchorMin = new Vector2(0.25f, 0.08f);
            buttonRect.anchorMax = new Vector2(0.75f, 0.18f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            
            // Button text - larger for full-screen layout, uses CrossPromoTable settings for rich interstitial
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            Text buttonText = buttonTextObj.AddComponent<Text>();
            
            // Use default button text (SourceProject requires SDK integration)
            string buttonTextStr = "Play Now!";
            buttonText.text = buttonTextStr;
            
            buttonText.fontSize = 28; // Larger font for bigger button
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            RectTransform buttonTextRect = buttonText.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
            buttonTextRect.anchoredPosition = Vector2.zero;
            
            // Close button - small X in top-right corner
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(panel.transform, false);
            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0.6f, 0.6f, 0.6f);
            Button closeButton = closeObj.AddComponent<Button>();
            closeButton.onClick.AddListener(HideCampaign);
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.sizeDelta = new Vector2(30, 30);
            closeRect.anchoredPosition = new Vector2(320, 250); // Top-right corner for 700×560 panel
            
            GameObject closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeObj.transform, false);
            Text closeText = closeTextObj.AddComponent<Text>();
            closeText.text = "×";
            closeText.fontSize = 20;
            closeText.color = Color.white;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.fontStyle = FontStyle.Bold;
            RectTransform closeTextRect = closeText.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.sizeDelta = Vector2.zero;
            closeTextRect.anchoredPosition = Vector2.zero;
            
            // Add fade-in animation for smooth appearance
            CanvasGroup canvasGroup = interstitial.AddComponent<CanvasGroup>();
            StartCoroutine(FadeInInterstitial(canvasGroup, 0.25f)); // Quick 0.25s fade
            
            currentDisplayObject = interstitial;
        }
        
        #endregion
        
        #region Helper Methods
        
        private void PositionBanner(GameObject banner)
        {
            RectTransform rectTransform = banner.GetComponent<RectTransform>();
            
            switch (bannerPosition)
            {
                case BannerPosition.Top:
                    rectTransform.anchorMin = new Vector2(0.5f, 1f);
                    rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    rectTransform.anchoredPosition = new Vector2(0, -bannerSize.y / 2);
                    break;
                case BannerPosition.Bottom:
                    rectTransform.anchorMin = new Vector2(0.5f, 0f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0f);
                    rectTransform.anchoredPosition = new Vector2(0, bannerSize.y / 2);
                    break;
                // Add other positions as needed
            }
        }
        
        /// <summary>
        /// Populate prefab with campaign data using naming conventions
        /// Supports Text, Image, Button components with specific names
        /// </summary>
        private void PopulateCampaignData(GameObject displayObject, Campaign campaign)
        {
            if (displayObject == null)
            {
                BoostOpsLogger.LogError("CrossPromo", "PopulateCampaignData: displayObject is null!");
                return;
            }
            
            if (campaign == null) 
            {
                BoostOpsLogger.LogError("CrossPromo", "PopulateCampaignData: Campaign is null!");
                return;
            }
            
            // Debug.Log($"[BoostOpsCampaignDisplay] PopulateCampaignData - MINIMAL VERSION - only setting icon image and button text");
            
            // 1. SET ICON IMAGE ONLY - Find icon images by GameObject name (DLL-safe approach)
            Transform[] allTransforms = displayObject.GetComponentsInChildren<Transform>(true);
            
            foreach (var transform in allTransforms)
            {
                if (transform.name == "CampaignIcon")
                {
                    Image image = transform.GetComponent<Image>();
                    if (image != null)
                    {
                        LoadCampaignImage(image, campaign, "icon");
                        // Debug.Log($"[BoostOpsCampaignDisplay] ✅ Set icon image via GameObject name");
                    }
                }
            }
            
            // Fallback: Handle images that don't have standard names - ICON ONLY
            Image[] imagesWithoutStandardNames = displayObject.GetComponentsInChildren<Image>(true)
                .Where(img => img.name != "CampaignIcon" && img.name != "CampaignHero")
                .ToArray();
                
            foreach (var image in imagesWithoutStandardNames)
            {
                string name = image.gameObject.name.ToLower();
                
                // Only set icon images, ignore all other images
                if (name == "campaignicon" || name == "icon")
                {
                    LoadCampaignImage(image, campaign, "icon");
                    // Debug.Log($"[BoostOpsCampaignDisplay] ✅ Set icon image via name matching: {name}");
                }
            }
            
            // 2. SET BUTTON TEXT ONLY - Find button text and set it
            Text[] texts = displayObject.GetComponentsInChildren<Text>(true);
            
            foreach (var text in texts)
            {
                string name = text.gameObject.name.ToLower();
                
                // Only set button/CTA text, ignore description text and everything else
                if (name.Contains("cta") || name.Contains("button") || name.Contains("action"))
                {
                    // Use default button text (SourceProject requires SDK integration)
                    string ctaText = "Play Now!";
                    
                    text.text = ctaText;
                    // Debug.Log($"[BoostOpsCampaignDisplay] ✅ Set button text to: '{ctaText}' - NO OTHER CHANGES");
                }
            }
            
            // 3. SETUP BUTTON CLICK HANDLERS - Find buttons and add click listeners if none exist
            Button[] buttons = displayObject.GetComponentsInChildren<Button>(true);
            
            foreach (var button in buttons)
            {
                string name = button.gameObject.name.ToLower();
                
                // Setup main campaign buttons (install/play/download/CTA buttons)
                if (name.Contains("campaign") || name.Contains("play") || name.Contains("cta") || 
                    name.Contains("main") || name.Contains("install") || name.Contains("download") || 
                    name.Contains("action"))
                {
                    // Only add listener if button doesn't already have persistent listeners
                    bool hasExistingListener = button.onClick.GetPersistentEventCount() > 0;
                    if (!hasExistingListener)
                    {
                        button.onClick.AddListener(() => OnCampaignClicked());
                        // Debug.Log($"[BoostOpsCampaignDisplay] ✅ Added campaign click listener to button: '{button.name}'");
                    }
                    else
                    {
                        Debug.Log($"[BoostOpsCampaignDisplay] ⚠️ Button '{button.name}' already has listeners, preserving existing setup");
                    }
                }
                // Setup close buttons
                else if (name.Contains("close") || name.Contains("x") || name.Contains("dismiss") || name.Contains("cancel"))
                {
                    bool hasExistingListener = button.onClick.GetPersistentEventCount() > 0;
                    if (!hasExistingListener)
                    {
                        button.onClick.AddListener(() => HideCampaign());
                        // Debug.Log($"[BoostOpsCampaignDisplay] ✅ Added close click listener to button: '{button.name}'");
                    }
                    else
                    {
                        Debug.Log($"[BoostOpsCampaignDisplay] ⚠️ Close button '{button.name}' already has listeners, preserving existing setup");
                    }
                }
            }
            
            // Debug.Log($"[BoostOpsCampaignDisplay] ✅ PopulateCampaignData complete - icon image, button text, and click handlers configured while preserving prefab styling");
        }
        
        private IEnumerator AutoShowCampaign()
        {
            yield return new WaitForSeconds(autoShowDelay);
            
            // Wait for SDK to be ready
            // Skip SDK readiness check (requires SDK integration)
            yield return new WaitForSeconds(0.1f);
            
            ShowRandomCampaign();
        }
        
        private IEnumerator AutoHideInterstitial()
        {
            yield return new WaitForSecondsRealtime(interstitialDuration);
            HideCampaign();
        }
        
        /// <summary>
        /// Load and assign campaign image to Image component
        /// </summary>
        /// <param name="imageComponent">Image component to populate</param>
        /// <param name="campaign">Campaign with creative data</param>
        /// <param name="format">Creative format to load ("icon", "hero", "banner")</param>
        private void LoadCampaignImage(Image imageComponent, Campaign campaign, string format)
        {
            if (imageComponent == null || campaign == null)
            {
                Debug.LogWarning($"[BoostOpsCampaignDisplay] Cannot load {format} image: missing components (imageComponent={imageComponent != null}, campaign={campaign != null})");
                return;
            }
            
            // BoostOpsLogger.LogDebug("CampaignDisplay", $"LoadCampaignImage: campaign='{campaign.name}', format='{format}', imageComponent='{imageComponent.gameObject.name}'");
            
            // Try to load PNG asset first
            var sprite = TryLoadPNGAsset(campaign, format);
            
            if (sprite != null)
            {
                imageComponent.sprite = sprite;
                imageComponent.color = Color.white; // Reset color when using actual assets
                // BoostOpsLogger.LogDebug("CampaignDisplay", $"Loaded PNG {format} for campaign '{campaign.name}'");
                return;
            }
            
            // Fall back to dynamic texture generation
            var campaignTheme = GetCampaignTheme(campaign.name);
            var texture = CreateThemedTexture(campaignTheme, format);
            
            if (texture != null)
            {
                var dynamicSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                imageComponent.sprite = dynamicSprite;
                imageComponent.color = Color.white; // Don't tint when using generated texture
                
                Debug.Log($"[BoostOpsCampaignDisplay] Created dynamic {format} for campaign '{campaign.name}'");
            }
            else
            {
                Debug.LogWarning($"[BoostOpsCampaignDisplay] Failed to load or create {format} for campaign '{campaign.name}'");
            }
        }
        
        /// <summary>
        /// Try to load asset using modern campaign creative data with local_key
        /// </summary>
        private Sprite TryLoadFromCreativeData(Campaign campaign, string format)
        {
            try
            {
                // Check if campaign has target_project with creatives
                if (campaign?.target_project?.creatives == null || campaign.target_project.creatives.Length == 0)
                    return null;
                
                var creative = campaign.target_project.creatives.FirstOrDefault(c => c.format == format);
                if (creative == null)
                    return null;
                
                if (creative.variants == null || creative.variants.Length == 0)
                    return null;
                
                string localKey = creative.variants[0].local_key;
                if (string.IsNullOrEmpty(localKey))
                    return null;
                
                // BoostOpsLogger.LogDebug("CampaignDisplay", $"Attempting to load asset using local_key: '{localKey}'");
                
                // iOS path diagnostic logging removed
                
                // Use the same logic as AssetResolver for consistent asset loading
                Sprite sprite = null;
                
                if (localKey.StartsWith("BoostOps/Downloads/DemoAssets/"))
                {
                    // Demo assets - only available in editor
#if UNITY_EDITOR
                    string assetPath = $"Assets/{localKey}.png";
                    sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite == null)
                    {
                        var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                        if (texture != null)
                        {
                            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        }
                    }
#endif
                }
                else if (localKey.StartsWith("BoostOps/"))
                {
                    // Legacy full path - load from Resources as-is
                    sprite = Resources.Load<Sprite>(localKey);
                    if (sprite == null)
                    {
                        var texture = Resources.Load<Texture2D>(localKey);
                        if (texture != null)
                        {
                            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        }
                    }
                }
                else
                {
                    // Modern resource-based format: prepend "BoostOps/" to create full path
                    string fullResourcePath = $"BoostOps/{localKey}";
                    
#if UNITY_IOS && !UNITY_EDITOR
                    Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Trying to load from Resources: '{fullResourcePath}'");
                    
                    // Check what Resources are actually available on iOS
                    try
                    {
                        var allSprites = Resources.LoadAll<Sprite>("BoostOps");
                        Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Found {allSprites.Length} sprites in BoostOps Resources folder");
                        foreach (var s in allSprites.Take(10)) // Limit to first 10 to avoid spam
                        {
                            Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Available sprite: '{s.name}'");
                        }
                        
                        var allTextures = Resources.LoadAll<Texture2D>("BoostOps");
                        Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Found {allTextures.Length} textures in BoostOps Resources folder");
                        foreach (var t in allTextures.Take(10)) // Limit to first 10 to avoid spam
                        {
                            Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Available texture: '{t.name}'");
                        }
                        
                        // Try case-insensitive search for iOS
                        string targetAssetName = localKey.Replace("Icons/", "").Replace("_ios_icon", "").Replace("_android_icon", "").Replace("_amazon_icon", "");
                        Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Looking for asset with base name: '{targetAssetName}'");
                        
                        var matchingSprite = allSprites.FirstOrDefault(s => s.name.Contains(targetAssetName));
                        if (matchingSprite != null)
                        {
                            Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Found potential match: '{matchingSprite.name}' for target '{targetAssetName}'");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[BoostOpsCampaignDisplay] [iOS] Error checking Resources: {ex.Message}");
                    }
#endif
                    
                    sprite = Resources.Load<Sprite>(fullResourcePath);
                    if (sprite == null)
                    {
                        var texture = Resources.Load<Texture2D>(fullResourcePath);
                        if (texture != null)
                        {
#if UNITY_IOS && !UNITY_EDITOR
                            Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Loaded texture '{texture.name}', creating sprite");
#endif
                            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        }
#if UNITY_IOS && !UNITY_EDITOR
                        else
                        {
                            Debug.LogWarning($"[BoostOpsCampaignDisplay] [iOS] Failed to load texture from Resources: '{fullResourcePath}'");
                        }
#endif
                    }
#if UNITY_IOS && !UNITY_EDITOR
                    else
                    {
                        Debug.Log($"[BoostOpsCampaignDisplay] [iOS] Successfully loaded sprite '{sprite.name}' from Resources");
                    }
#endif
                }
                
                if (sprite != null)
                {
                    // BoostOpsLogger.LogDebug("CampaignDisplay", $"Successfully loaded sprite using local_key: '{localKey}'");
                }
                else
                {
                    Debug.LogWarning($"[BoostOpsCampaignDisplay] Asset not found at local_key: '{localKey}'");
                }
                
                return sprite;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsCampaignDisplay] Error loading creative data for campaign '{campaign?.name}': {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Try to load a PNG asset for the campaign
        /// </summary>
        private Sprite TryLoadPNGAsset(Campaign campaign, string format)
        {
            // Modern approach: Try to load using creative data with local_key
            var sprite = TryLoadFromCreativeData(campaign, format);
            if (sprite != null)
            {
                // BoostOpsLogger.LogDebug("CampaignDisplay", $"Loaded {format} using modern creative data for campaign '{campaign.name}'");
                return sprite;
            }
            
            // Fallback: Legacy name-based approach
            Debug.Log($"[BoostOpsCampaignDisplay] Modern creative data not found, trying legacy name-based loading for campaign '{campaign.name}', format '{format}'");
            
            // Map campaign names to asset keys
            string assetKey = GetAssetKeyForCampaign(campaign.name);
            if (string.IsNullOrEmpty(assetKey))
                return null;
                
            // Map format to subfolder and suffix
            string subfolder = "";
            string suffix = "";
            
            switch (format.ToLower())
            {
                case "icon":
                    subfolder = "Icons";
                    suffix = "_icon";
                    break;
                case "hero":
                    subfolder = "Screenshots";
                    suffix = "_screenshot";
                    break;
                case "banner":
                    subfolder = "Banners";
                    suffix = "_banner";
                    break;
                default:
                    return null;
            }
            
            // Try to load from Resources first
            string resourcePath = $"BoostOps/{subfolder}/{assetKey}{suffix}";
            sprite = Resources.Load<Sprite>(resourcePath);
            
            if (sprite == null)
            {
                // Try loading as texture and converting to sprite
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }
            }
            
            // If not found in Resources, try Downloads folder for auto-downloaded icons
            if (sprite == null && format.ToLower() == "icon")
            {
                sprite = TryLoadDownloadedIconByStoreId(campaign);
            }
            
            return sprite;
        }
        
        /// <summary>
        /// Try to load an icon from the Downloads folder using store ID (more reliable than app name)
        /// </summary>
        private Sprite TryLoadDownloadedIconByStoreId(Campaign campaign)
        {
            try
            {
                Debug.Log($"[BoostOpsCampaignDisplay] TryLoadDownloadedIconByStoreId called for campaign: '{campaign.name}'");
                
                // Extract store IDs from campaign
                var storeIds = ExtractStoreIds(campaign);
                Debug.Log($"[BoostOpsCampaignDisplay] Extracted store IDs: iOS='{storeIds.iosId}', Android='{storeIds.androidId}', Amazon='{storeIds.amazonId}'");
                
                // Look for downloaded icons in the Resources folder (for runtime accessibility)
                string iconsPath = "Assets/Resources/BoostOps/Icons/";
                
                // Try each store ID with appropriate suffix
                var platformChecks = new[]
                {
                    new { id = storeIds.iosId, suffix = "_ios_icon" },
                    new { id = storeIds.androidId, suffix = "_android_icon" }, 
                    new { id = storeIds.amazonId, suffix = "_amazon_icon" }
                };
                
                foreach (var check in platformChecks)
                {
                    if (string.IsNullOrEmpty(check.id)) continue;
                    
                    string filename = SanitizeStoreId(check.id) + check.suffix + ".png";
                    string fullPath = iconsPath + filename;
                    
                    Debug.Log($"[BoostOpsCampaignDisplay] Checking for icon file: {filename}");
                    
#if UNITY_EDITOR
                    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
#else
                    // At runtime, load from Resources (BoostOps folder)
                    string resourcePath = $"BoostOps/Icons/{SanitizeStoreId(check.id)}{check.suffix}";
                    var sprite = Resources.Load<Sprite>(resourcePath);
#endif
                    if (sprite != null)
                    {
                        Debug.Log($"[BoostOpsCampaignDisplay] Found downloaded icon by store ID: {fullPath}");
                        return sprite;
                    }
                }
                
                Debug.Log($"[BoostOpsCampaignDisplay] No icon found in Resources/BoostOps/Icons/ for campaign using store IDs");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOpsCampaignDisplay] Error loading downloaded icon by store ID for '{campaign.name}': {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Extract store IDs from campaign object
        /// </summary>
        private (string iosId, string androidId, string amazonId) ExtractStoreIds(Campaign campaign)
        {
            string iosId = "";
            string androidId = "";
            string amazonId = "";
            
            // Try new format first (campaign.target_project.store_urls)
            if (campaign.target_project?.store_urls != null)
            {
                var links = campaign.target_project.store_urls;
                
                // Extract Apple App Store ID from URL
                if (!string.IsNullOrEmpty(links.apple))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(links.apple, @"id(\d+)");
                    if (match.Success)
                    {
                        iosId = match.Groups[1].Value;
                    }
                }
                
                // Extract Google package ID from URL  
                if (!string.IsNullOrEmpty(links.google))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(links.google, @"id=([a-zA-Z0-9._]+)");
                    if (match.Success)
                    {
                        androidId = match.Groups[1].Value;
                    }
                }
                
                // For Amazon, it's trickier since the URL format varies
                if (!string.IsNullOrEmpty(links.amazon))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(links.amazon, @"([A-Z0-9]{10})");
                    if (match.Success)
                    {
                        amazonId = match.Groups[1].Value;
                    }
                }
            }
            
            // Debug log if no store IDs found
            if (string.IsNullOrEmpty(iosId) && string.IsNullOrEmpty(androidId) && string.IsNullOrEmpty(amazonId))
            {
                Debug.LogWarning($"[BoostOpsCampaignDisplay] No store IDs found for campaign '{campaign.campaign_id}'. Check store_urls configuration.");
            }
            
            return (iosId, androidId, amazonId);
        }
        
        /// <summary>
        /// Sanitize store ID for use in filename (mainly for Android package IDs)
        /// </summary>
        private string SanitizeStoreId(string storeId)
        {
            if (string.IsNullOrEmpty(storeId)) return "";
            
            // Replace dots with underscores for Android package IDs
            return storeId.Replace(".", "_");
        }
        
        /// <summary>
        /// Get the asset key for a campaign name
        /// </summary>
        private string GetAssetKeyForCampaign(string campaignName)
        {
            if (string.IsNullOrEmpty(campaignName))
                return null;
                
            var name = campaignName.ToLower();
            
            if (name.Contains("puzzle") || name.Contains("quest"))
                return "puzzle_quest";
            else if (name.Contains("space") || name.Contains("shooter") || name.Contains("galaxy"))
                return "space_shooter";
            else if (name.Contains("racing") || name.Contains("thunder"))
                return "racing_thunder";
                
            return null;
        }
        
        /// <summary>
        /// Get theme colors and info for a campaign based on its name
        /// </summary>
        private (Color primaryColor, Color secondaryColor, string theme) GetCampaignTheme(string campaignName)
        {
            if (string.IsNullOrEmpty(campaignName))
                return (Color.gray, Color.white, "default");
                
            var name = campaignName.ToLower();
            
            if (name.Contains("puzzle") || name.Contains("quest"))
            {
                // Fantasy/Magic theme - Purple and Gold
                return (new Color(0.29f, 0.055f, 0.31f), new Color(1f, 0.84f, 0f), "fantasy");
            }
            else if (name.Contains("space") || name.Contains("shooter") || name.Contains("galaxy"))
            {
                // Sci-fi theme - Dark Blue and Cyan
                return (new Color(0.059f, 0.129f, 0.241f), new Color(0f, 0.807f, 0.819f), "scifi");
            }
            else if (name.Contains("racing") || name.Contains("thunder") || name.Contains("speed"))
            {
                // Racing theme - Red and Orange
                return (new Color(1f, 0.271f, 0f), new Color(1f, 0.647f, 0f), "racing");
            }
            
            return (Color.blue, Color.white, "default");
        }
        
        /// <summary>
        /// Create a themed texture for the campaign
        /// </summary>
        private Texture2D CreateThemedTexture((Color primaryColor, Color secondaryColor, string theme) campaignTheme, string format)
        {
            // Determine texture size based on format
            int width = 256, height = 256;
            if (format == "hero")
            {
                width = 512; height = 256; // 2:1 ratio for hero images
            }
            else if (format == "banner")
            {
                width = 320; height = 50; // Banner dimensions
            }
            
            var texture = new Texture2D(width, height);
            var pixels = new Color[width * height];
            
            // Create a gradient effect
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float gradientFactor = (float)y / height;
                    var color = Color.Lerp(campaignTheme.primaryColor, campaignTheme.secondaryColor, gradientFactor);
                    
                    // Add some visual interest with a pattern
                    if (format == "icon")
                    {
                        // Add a circular highlight for icons
                        float centerX = width * 0.5f;
                        float centerY = height * 0.5f;
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                        float maxDistance = Mathf.Min(width, height) * 0.4f;
                        
                        if (distance < maxDistance)
                        {
                            float highlight = 1f - (distance / maxDistance);
                            color = Color.Lerp(color, Color.white, highlight * 0.3f);
                        }
                    }
                    else if (format == "hero")
                    {
                        // Add diagonal stripes for hero images
                        if ((x + y) % 40 < 20)
                        {
                            color = Color.Lerp(color, campaignTheme.secondaryColor, 0.2f);
                        }
                    }
                    
                    pixels[y * width + x] = color;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
        
        /// <summary>
        /// Smooth fade-in animation for interstitials using CanvasGroup alpha
        /// Uses unscaled time so animation works even when game is paused
        /// </summary>
        /// <param name="canvasGroup">CanvasGroup to fade in</param>
        /// <param name="duration">Fade duration in seconds</param>
        private IEnumerator FadeInInterstitial(CanvasGroup canvasGroup, float duration = 0.3f)
        {
            // Defensive null check to prevent crashes in production
            if (canvasGroup == null)
            {
                BoostOpsLogger.LogWarning("CrossPromo", "FadeInInterstitial: CanvasGroup is null, skipping animation");
                yield break;
            }
            
            canvasGroup.alpha = 0f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                // Check if CanvasGroup was destroyed during animation
                if (canvasGroup == null)
                {
                    BoostOpsLogger.LogWarning("CrossPromo", "FadeInInterstitial: CanvasGroup became null during animation");
                    yield break;
                }
                
                elapsed += Time.unscaledDeltaTime; // Use unscaled time so animation works when game is paused
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            
            // Final null check before setting final alpha
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f; // Ensure we end at fully visible
            }
        }
        

        
        #endregion
    }
} 