using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BoostOps.Analytics;

namespace BoostOps
{
    /// <summary>
    /// BoostOps SDK - Unified Cross-Promotion & Deep Linking Interface
    /// Clean, simple API following MAX-style initialization pattern
    /// </summary>
    public static class BoostOpsSDK
    {
        // --- Internal Implementation (DLL Protected) ---
        private static BoostOps.Internal.BoostOpsSDKInternal _internal;
        
        // --- Cached Identifiers ---
        private static string _cachedInstallId;
        
        // --- Events (Forwarded from Internal Implementation) ---
        
        /// <summary>
        /// Fired when SDK initialization fails with fatal errors
        /// </summary>
        public static event Action<InitError> OnInitFailed
        {
            add { EnsureInternal(); _internal.OnInitFailed += (internalError) => value?.Invoke(ConvertFromInternal(internalError)); }
            remove { /* Event removal not supported for converted events */ }
        }
        
        /// <summary>
        /// Fired when SDK initialization succeeds
        /// </summary>
        public static event Action OnInitSuccess
        {
            add { EnsureInternal(); _internal.OnInitSuccess += value; }
            remove { if (_internal != null) _internal.OnInitSuccess -= value; }
        }
        
        /// <summary>
        /// Fired when a cross-promotion campaign is displayed to the user
        /// </summary>
        public static event Action<CampaignInfo> OnCampaignImpression
        {
            add { EnsureInternal(); _internal.OnCampaignImpression += (internalCampaign) => value?.Invoke(CampaignInfo.FromInternalCampaign(internalCampaign)); }
            remove { /* Event removal not supported for converted events */ }
        }
        
        /// <summary>
        /// Fired when a user clicks on a cross-promotion campaign
        /// </summary>
        public static event Action<CampaignInfo> OnCampaignClick
        {
            add { EnsureInternal(); _internal.OnCampaignClick += (internalCampaign) => value?.Invoke(CampaignInfo.FromInternalCampaign(internalCampaign)); }
            remove { /* Event removal not supported for converted events */ }
        }
        
        /// <summary>
        /// Fired when campaigns become available (remote config loaded)
        /// Useful for first-launch scenarios where you want to update UI as soon as campaigns are ready
        /// </summary>
        public static event Action OnCampaignsReady
        {
            add { EnsureInternal(); _internal.OnCampaignsReady += value; }
            remove { /* Event removal not supported for converted events */ }
        }
        
        // --- Properties ---
        public static bool IsInitialized => _internal?.IsInitialized ?? false;
        
        /// <summary>
        /// Check if SDK is currently running in local-only mode (no server communication)
        /// </summary>
        public static bool IsLocalMode
        {
            get
            {
                var manager = GetOrCreateManager();
                return manager?.InternalManager?.LocalOnlyMode ?? false;
            }
        }
        
        /// <summary>
        /// Ensure internal implementation is created
        /// </summary>
        private static void EnsureInternal()
        {
            if (_internal == null)
            {
                _internal = new BoostOps.Internal.BoostOpsSDKInternal();
                
                // Inject the internal manager from BoostOpsManager
                var manager = GetOrCreateManager();
                if (manager?.InternalManager != null)
                {
                    // Don't configure prefabs yet - wait until Init() is called
                    _internal.SetInternalManager(manager.InternalManager);
                }
            }
        }
        
        // --- Core SDK ---
        
        /// <summary>
        /// Set SDK key once (MAX-style) - optional for local-only mode
        /// </summary>
        /// <param name="sdkKey">Your BoostOps SDK key</param>
        public static void SetSdkKey(string sdkKey)
        {
            EnsureInternal();
            _internal.SetSdkKey(sdkKey);
        }
        
        /// <summary>
        /// Set demo data file path for static JSON data loading
        /// Path should be relative to StreamingAssets folder
        /// </summary>
        /// <param name="filePath">Path to JSON file relative to StreamingAssets (e.g., "BoostOps/demo_campaigns.json")</param>
        public static void SetDemoDataFile(string filePath)
        {
            EnsureInternal();
            _internal.SetDemoDataFile(filePath);
        }
        
        /// <summary>
        /// Get the current demo data file path
        /// </summary>
        /// <returns>Demo data file path or null if not set</returns>
        public static string GetDemoDataFile()
        {
            return _internal?.DemoDataFile;
        }
        
        /// <summary>
        /// Initialize BoostOps SDK with callback
        /// Safe to call multiple times - will return immediately if already initialized
        /// </summary>
        /// <param name="callback">Initialization result callback</param>
        public static void Init(System.Action<InitResult> callback = null)
        {
            // Read project settings and pass to internal classes FIRST
            var projectSettings = BoostOpsProjectSettings.GetInstance();
            
            // Initialize debug logging from project settings BEFORE any other logs
            if (projectSettings != null)
            {
                BoostOpsLogger.IsDebugLoggingEnabled = projectSettings.debugLogging;
            }
            
            // Migrate legacy PlayerPrefs to Unity-style lowercase_snake_case format
            // This must happen before any other PlayerPrefs operations
            BoostOpsPlayerPrefsKeys.MigrateLegacyPlayerPrefs();
            
            // Generate/load install_id early so it's available for third-party SDKs
            if (string.IsNullOrEmpty(_cachedInstallId))
            {
                _cachedInstallId = BoostOpsIdentifierManager.GetInstallId();
            }
            
            // Regenerate session ID for new app launch (cold start)
            // This ensures each app launch gets a unique session ID
            BoostOpsEventBuilder.RegenerateSessionId();
            // BoostOpsLogger.LogDebug("SDK", "🆔 Generated new session ID for app launch");
            
            // BoostOpsLogger.LogDebug("SDK", "🔍 Reading project settings...");
            
            if (projectSettings != null)
            {
                // BoostOpsLogger.LogInfo("SDK", $"✅ Project settings loaded - ProjectKey: '{projectSettings.projectKey}' (length: {projectSettings.projectKey?.Length ?? 0})");
                // BoostOpsLogger.LogInfo("SDK", $"✅ Project ID: '{projectSettings.projectId}' (will be used as source_project_id in analytics)");
                
                // Convert to internal settings format
                var internalSettings = new BoostOps.Internal.InternalProjectSettings
                {
                    ProjectId = projectSettings.projectId ?? "",  // ⭐ CRITICAL: Source project ID for analytics
                    ProjectKey = projectSettings.projectKey ?? "",
                    UseRemoteManagement = projectSettings.useRemoteManagement,
                    // BoostOpsAnalytics is now derived from UseRemoteManagement
                    IngestUrl = projectSettings.ingestUrl ?? "https://analytics.boostops.io/v1",
                    FirebaseAnalytics = projectSettings.firebaseAnalytics,
                    UnityAnalytics = projectSettings.unityAnalytics,
                    AppleAppStoreId = projectSettings.appleAppStoreId ?? "",
                    AndroidPackageName = projectSettings.androidPackageName ?? "",
                    AmazonStoreId = projectSettings.amazonStoreId ?? "",
                    MicrosoftStoreId = projectSettings.microsoftStoreId ?? "",
                    SamsungStoreId = projectSettings.samsungStoreId ?? ""
                };
                
                // Cache settings for internal classes
                BoostOps.Internal.InternalSettingsCache.SetProjectSettings(internalSettings);
            }
            else
            {
                Debug.LogWarning("[BoostOps SDK] ⚠️ Project settings not found - using default values");
                
                // Set default settings
                var defaultSettings = new BoostOps.Internal.InternalProjectSettings();
                BoostOps.Internal.InternalSettingsCache.SetProjectSettings(defaultSettings);
            }
            
            // Delegate all complex initialization logic to internal implementation
            EnsureInternal();
            
            // Configure prefabs now that we're actually initializing
            var manager = GetOrCreateManager();
            if (manager != null)
            {
                manager.EnsureConfigured();
            }
            
            _internal.Init(callback != null ? (internalResult) => callback(ConvertFromInternal(internalResult)) : null);
        }
        
        /// <summary>
        /// Initialize the BoostOps SDK (alias for Init)
        /// Generates/loads the install_id which can be retrieved via GetInstallId()
        /// </summary>
        public static void Initialize()
        {
            // Generate/load install_id immediately on initialization
            // This ensures it's available synchronously for third-party SDKs
            _cachedInstallId = BoostOpsIdentifierManager.GetInstallId();
            
            Init();
        }
        
        /// <summary>
        /// Initialize BoostOps SDK asynchronously with full control over timing
        /// Safe to call multiple times - will return immediately if already initialized
        /// Note: This assumes settings have already been loaded via Init() first
        /// Generates/loads the install_id which can be retrieved via GetInstallId()
        /// </summary>
        /// <returns>True if initialization succeeded, false otherwise</returns>
        public static async System.Threading.Tasks.Task<bool> InitializeAsync()
        {
            // Generate/load install_id immediately on initialization
            _cachedInstallId = BoostOpsIdentifierManager.GetInstallId();
            
            var manager = GetOrCreateManager();
            
            // Configure prefabs now that we're actually initializing
            if (manager != null)
            {
                manager.EnsureConfigured();
            }
            
            return await manager.InitializeAsync();
        }
        
        /// <summary>
        /// Initialize for local mode only (no server connection)
        /// </summary>
        public static void InitializeLocalOnly()
        {
            // Generate/load install_id immediately on initialization
            _cachedInstallId = BoostOpsIdentifierManager.GetInstallId();
            
            var manager = GetOrCreateManager();
            
            // Configure prefabs now that we're actually initializing
            if (manager != null)
            {
                manager.EnsureConfigured();
            }
            
            manager.InitializeLocalOnly();
        }
        
        // --- Identifier API ---
        
        /// <summary>
        /// Get the install ID for this app installation.
        /// This is a per-app identifier that persists until app uninstall.
        /// Use this for third-party SDK integration (e.g., IronSource, AdMob).
        /// 
        /// IMPORTANT: Must call Initialize() first. Returns null if not initialized.
        /// 
        /// Format: 32 hexadecimal characters (no dashes)
        /// Example: "550e8400e29b41d4a716446655440000"
        /// </summary>
        /// <returns>Install ID string, or null if SDK not initialized</returns>
        public static string GetInstallId()
        {
            // Return cached value if available
            if (!string.IsNullOrEmpty(_cachedInstallId))
            {
                return _cachedInstallId;
            }
            
            // If not cached yet, try to get it directly
            // (This handles cases where GetInstallId is called before Initialize)
            _cachedInstallId = BoostOpsIdentifierManager.GetInstallId();
            return _cachedInstallId;
        }
        
        /// <summary>
        /// Get the number of available campaigns
        /// </summary>
        /// <returns>Campaign count</returns>
        public static int GetCampaignCount()
        {
            var manager = BoostOpsManager.Instance;
            return manager?.GetCampaignCount() ?? 0;
        }
              
        // --- Cross-Promotion API ---
        
        /// <summary>
        /// Attempts to show a cross-promotion. Returns true if a creative was actually
        /// displayed, false if nothing was ready or the request was rejected
        /// by rules/filters.
        /// </summary>
        /// <param name="placement">Placement identifier (e.g., "level_complete", "main_menu")</param>
        /// <param name="format">Promo format (Auto, Banner, Native, Icon, Rich)</param>
        /// <param name="opts">Optional configuration</param>
        /// <returns>True if cross-promo was displayed, false if rejected/unavailable</returns>
        public static bool ShowCrossPromo(string placement, PromoFormat format = PromoFormat.Auto, PromoOptions opts = null)
        {
            // Delegate to internal implementation that contains format conversion and display logic
            EnsureInternal();
            return _internal.ShowCrossPromo(placement, ConvertToInternal(format), ConvertToInternal(opts));
        }
        
        /// <summary>
        /// Get available campaigns for external developers (simplified view)
        /// </summary>
        /// <returns>List of campaign information</returns>
        public static System.Collections.Generic.List<CampaignInfo> GetAvailableCampaigns()
        {
            var manager = GetOrCreateManager();
            if (manager?.InternalManager != null)
            {
                // Convert internal campaigns to public CampaignInfo objects
                var internalCampaigns = manager.InternalManager.GetAllCampaigns();
                var campaignInfos = new System.Collections.Generic.List<CampaignInfo>();
                
                foreach (var campaign in internalCampaigns)
                {
                    campaignInfos.Add(CampaignInfo.FromInternalCampaign(campaign));
                }
                
                return campaignInfos;
            }
            
            return new System.Collections.Generic.List<CampaignInfo>();
        }
        
        /// <summary>
        /// Get eligible campaigns filtered by format
        /// Use this to get campaigns for custom UI implementation (e.g., app wall grid)
        /// </summary>
        /// <param name="format">Filter by format: "interstitial", "app_wall", "banner", "rewarded" (null = all formats)</param>
        /// <param name="placement">Placement identifier for analytics tracking</param>
        /// <param name="maxCount">Maximum number of campaigns to return (default: 10)</param>
        /// <returns>List of eligible campaigns</returns>
        public static System.Collections.Generic.List<Campaign> GetEligibleCampaigns(string format = null, string placement = null, int maxCount = 10)
        {
            var manager = GetOrCreateManager();
            if (manager?.InternalManager != null)
            {
                var campaigns = manager.InternalManager.GetAllCampaigns();
                
                // Lazy load: If no campaigns available, try to parse remote config cache
                if (campaigns.Count == 0)
                {
                    // Debug.Log("[BoostOps SDK] No campaigns available - attempting lazy load from remote config cache...");
                    manager.InternalManager.TryLazyLoadCampaigns();
                    campaigns = manager.InternalManager.GetAllCampaigns();
                    
                    if (campaigns.Count > 0)
                    {
                        // Debug.Log($"[BoostOps SDK] Successfully lazy loaded {campaigns.Count} campaigns");
                    }
                }
                
                var eligible = new System.Collections.Generic.List<Campaign>();
                
                foreach (var campaign in campaigns)
                {
                    // Check if campaign is active
                    if (!campaign.IsActive)
                        continue;
                    
                    // Check format filter
                    if (!string.IsNullOrEmpty(format) && !campaign.SupportsFormat(format))
                        continue;
                    
                    eligible.Add(campaign);
                    
                    if (eligible.Count >= maxCount)
                        break;
                }
                
                return eligible;
            }
            
            return new System.Collections.Generic.List<Campaign>();
        }
        
        /// <summary>
        /// Show app wall with apps from remote config app_walls section
        /// Displays portfolio of games for user to browse
        /// Uses the new app_walls configuration from remote config
        /// </summary>
        /// <param name="placement">Placement identifier for analytics</param>
        /// <returns>True if app wall was shown, false if no apps available</returns>
        public static bool ShowAppWall(string placement)
        {
            EnsureInternal();
            return _internal.ShowAppWall(placement);
        }
        
        /// <summary>
        /// Show app wall with specific campaigns (legacy approach)
        /// For custom campaign selection, use this method
        /// </summary>
        /// <param name="placement">Placement identifier for analytics</param>
        /// <param name="maxCampaigns">Maximum number of campaigns to show (default: 12)</param>
        /// <returns>True if app wall was shown, false if no campaigns available</returns>
        [System.Obsolete("Use ShowAppWall(placement) to use remote config app_walls, or ShowAppWallWithCampaigns() for campaign-based approach")]
        public static bool ShowAppWall(string placement, int maxCampaigns)
        {
            EnsureInternal();
            // Call internal SDK which filters campaigns and shows app wall
            return _internal.ShowAppWall(placement, maxCampaigns);
        }
        
        /// <summary>
        /// Show app wall with specific campaigns (explicit method name)
        /// For custom campaign selection
        /// </summary>
        public static bool ShowAppWallWithCampaigns(string placement, int maxCampaigns = 12)
        {
            EnsureInternal();
            // Call internal SDK which filters campaigns and shows app wall
            return _internal.ShowAppWall(placement, maxCampaigns);
        }
        
        // --- Native Promo API (Simplified) ---
        
        /// <summary>
        /// Get a native promo for custom UI implementation
        /// Returns a BoostOpsPromo with campaign data + tracking context
        /// Uses Null Object pattern - always returns a valid object (check IsAvailable instead of null)
        /// </summary>
        /// <param name="placement">Placement identifier (e.g., "lobby_icon", "main_menu")</param>
        /// <param name="format">Promo format (default: "native")</param>
        /// <returns>BoostOpsPromo instance (check IsAvailable property - false if no campaigns available)</returns>
        public static BoostOpsPromo GetNativePromo(string placement, string format = "native")
        {
            var campaigns = GetEligibleCampaigns(format, placement, maxCount: 1);
            
            if (campaigns == null || campaigns.Count == 0)
                return BoostOpsPromo.Unavailable(placement, format);
            
            return new BoostOpsPromo(campaigns[0], placement, format);
        }
        
        /// <summary>
        /// Track impression for native promo
        /// Automatically generates and assigns UnitInstanceId on first call
        /// Attempts to lazy load campaign if promo was previously unavailable
        /// Safe to call with unavailable promos (will log warning and return)
        /// </summary>
        /// <param name="promo">The promo instance to track</param>
        public static void TrackImpression(BoostOpsPromo promo)
        {
            if (promo == null)
            {
                Debug.LogWarning("[BoostOps] TrackImpression: Promo is null");
                return;
            }
            
            // Try to refresh if unavailable (lazy load campaigns)
            if (!promo.IsAvailable)
            {
                // Debug.Log("[BoostOps] TrackImpression: Promo unavailable, attempting lazy load...");
                if (!promo.TryRefresh())
                {
                    Debug.LogWarning("[BoostOps] TrackImpression: Promo is not available (no campaigns found after lazy load)");
                    return;
                }
            }
            
            // Generate unit instance ID if not already set (first impression)
            if (string.IsNullOrEmpty(promo.UnitInstanceId))
            {
                promo.UnitInstanceId = System.Guid.NewGuid().ToString();
            }
            
            // Store timestamp for click linking
            promo.ImpressionTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Track impression and get the generated impression_id
            promo.ImpressionId = BoostOpsAnalyticsContract.TrackImpression(
                campaignSlug: promo.CampaignId ?? promo.Name,
                placement: promo.Placement,
                format: promo.Format,
                // Note: source_store_id is in context.store_id (universal) - not passed here
                // Note: source_project_id is derived server-side from project_key
                targetStoreId: BoostOpsAnalyticsContract.GetTargetStoreId(promo.Campaign),
                targetProjectId: BoostOpsAnalyticsContract.GetTargetProjectId(promo.Campaign),
                channel: "xpromo"
            );
            
            // Debug.Log($"[BoostOps] Tracked impression: {promo.Name} (instance: {promo.UnitInstanceId}, impression: {promo.ImpressionId})");
        }
        
        /// <summary>
        /// Handle click on native promo
        /// Tracks click event (reusing UnitInstanceId) and opens app store
        /// Attempts to lazy load campaign if promo was previously unavailable
        /// Safe to call with unavailable promos (will log warning and return)
        /// </summary>
        /// <param name="promo">The promo instance that was clicked</param>
        public static void Click(BoostOpsPromo promo)
        {
            if (promo == null)
            {
                Debug.LogWarning("[BoostOps] Click: Promo is null");
                return;
            }
            
            // Try to refresh if unavailable (lazy load campaigns)
            if (!promo.IsAvailable)
            {
                // Debug.Log("[BoostOps] Click: Promo unavailable, attempting lazy load...");
                if (!promo.TryRefresh())
                {
                    Debug.LogWarning($"[BoostOps] Click: Promo is not available after lazy load - IsAvailable={promo.IsAvailable}, Campaign={(promo.Campaign != null ? "exists" : "null")}, Placement={promo.Placement}");
                    return;
                }
                // Debug.Log("[BoostOps] Click: Successfully lazy loaded campaign data");
            }
            
            // Ensure unit instance ID exists (in case Click is called before TrackImpression)
            if (string.IsNullOrEmpty(promo.UnitInstanceId))
            {
                promo.UnitInstanceId = System.Guid.NewGuid().ToString();
                Debug.LogWarning($"[BoostOps] Click called before TrackImpression - generated instance ID: {promo.UnitInstanceId}");
            }
            
            // Track click with impression linking
            BoostOpsAnalyticsContract.TrackClick(
                campaignSlug: promo.CampaignId ?? promo.Name,
                placement: promo.Placement,
                format: promo.Format,
                // Note: source_store_id is in context.store_id (universal) - not passed here
                // Note: source_project_id is derived server-side from project_key
                targetStoreId: BoostOpsAnalyticsContract.GetTargetStoreId(promo.Campaign),
                targetProjectId: BoostOpsAnalyticsContract.GetTargetProjectId(promo.Campaign),
                channel: "xpromo",
                impressionId: promo.ImpressionId,
                impressionTimestamp: promo.ImpressionTimestamp
            );
            
            // Debug.Log($"[BoostOps] Tracked click: {promo.Name} (instance: {promo.UnitInstanceId}, impression: {promo.ImpressionId})");
            
            // Open app store
            string storeUrl = promo.GetStoreUrl();
            if (!string.IsNullOrEmpty(storeUrl))
            {
#if UNITY_IOS && !UNITY_EDITOR
                // iOS: Try native app sheet first (keeps user in-app)
                if (storeUrl.Contains("apps.apple.com") && BoostOps.BoostOpsAppStoreSheet.IsAvailable())
                {
                    string appStoreId = BoostOps.BoostOpsAppStoreSheet.ExtractAppStoreId(storeUrl);
                    if (!string.IsNullOrEmpty(appStoreId))
                    {
                        bool success = BoostOps.BoostOpsAppStoreSheet.ShowAppStoreSheet(appStoreId);
                        if (success)
                        {
                            // Debug.Log($"[BoostOps] Opened iOS app sheet for: {appStoreId}");
                            return;
                        }
                        else
                        {
                            Debug.LogWarning("[BoostOps] Failed to show app sheet, falling back to browser");
                        }
                    }
                }
#endif
                
                // Add attribution parameters to store URL
                string finalUrl = AppendCrossPromoAttributionParameters(
                    storeUrl, 
                    promo.CampaignId ?? promo.Name,
                    promo.Placement ?? "unknown"
                );
                
                // Fallback: Open in external browser/store
                Application.OpenURL(finalUrl);
                // Debug.Log($"[BoostOps] Opening store: {finalUrl}");
            }
            else
            {
                Debug.LogWarning($"[BoostOps] No store URL available for {promo.Name}");
            }
        }
        
        /// <summary>
        /// Hide a specific placement or all cross-promos when placement == null
        /// </summary>
        /// <param name="placement">Placement to hide, or null to hide all</param>
        public static void HideCrossPromo(string placement = null)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[BoostOps] SDK not initialized.");
                return;
            }
            
            var manager = BoostOpsManager.Instance;
            
            if (string.IsNullOrEmpty(placement))
            {
                // Hide all active cross-promos
                manager?.HideAllPromos();
            }
            else
            {
                // Hide specific placement
                manager?.HidePromo(placement);
            }
        }
        
        // --- Deep Links ---
        
        /// <summary>
        /// Create BoostOps deep link with campaign tracking parameters
        /// </summary>
        /// <param name="targetUrl">Target URL to encode</param>
        /// <param name="campaignId">Campaign ID for tracking</param>
        /// <param name="additionalParams">Additional parameters to include</param>
        /// <returns>Generated deep link URL</returns>
        public static string CreateDeepLink(string targetUrl, string campaignId, Dictionary<string, string> additionalParams = null)
        {
            var manager = BoostOpsManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[BoostOps] Manager not available for deep link creation");
                return targetUrl;
            }

            // Basic deep link creation logic
            var uriBuilder = new UriBuilder(targetUrl);
            var queryString = uriBuilder.Query;
            
            // Simple query parameter handling (Unity-compatible)
            if (!string.IsNullOrEmpty(queryString) && queryString.StartsWith("?"))
                queryString = queryString.Substring(1);
            
            var parameters = new List<string>();
            if (!string.IsNullOrEmpty(queryString))
            {
                parameters.AddRange(queryString.Split('&'));
            }

            // Add BoostOps parameters
            parameters.Add($"boostops_campaign={Uri.EscapeDataString(campaignId)}");
            parameters.Add($"boostops_timestamp={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            if (additionalParams != null)
            {
                foreach (var param in additionalParams)
                {
                    parameters.Add($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}");
                }
            }

            uriBuilder.Query = string.Join("&", parameters);
            return uriBuilder.ToString();
        }

        /// <summary>
        /// Get deep link information from the last opened deep link
        /// </summary>
        /// <returns>Deep link information or null if none available</returns>
        public static DeepLinkInfo GetDeepLinkInfo()
        {
            var manager = BoostOpsManager.Instance;
            if (manager == null)
            {
                return new DeepLinkInfo
                {
                    IsValid = false,
                    Error = "Manager not initialized"
                };
            }

            // Create deep link info from manager state
            var deepLinkInfo = new DeepLinkInfo
            {
                IsValid = true,
                CampaignId = "",
                SourceApp = "",
                Parameters = new Dictionary<string, string>()
            };

            return deepLinkInfo;
        }
        
        /// <summary>
        /// Get the user's referral code for display in UI.
        /// Code is generated once and persisted locally (Base32, 8 characters).
        /// 
        /// Example: "R7XK2PQM"
        /// 
        /// Use this to show the code in your UI:
        /// - Profile screen: "Your code: R7XK2PQM"
        /// - Share screen: "Share code R7XK2PQM with friends"
        /// - Leaderboard: "Player #R7XK2PQM"
        /// </summary>
        /// <returns>User's persistent referral code (e.g., "R7XK2PQM")</returns>
        public static string GetReferralCode()
        {
            return Utils.BoostOpsReferralCodeGenerator.GetOrCreateCode();
        }
        
        /// <summary>
        /// Get a referral URL for social sharing and invites.
        /// Automatically includes the user's referral code.
        /// 
        /// Returns: "https://yourslug.boostlink.me/r/R7XK2PQM"
        /// 
        /// The referral code is generated once per device and persisted.
        /// Use GetReferralCode() to display the code in your UI.
        /// </summary>
        /// <returns>Shareable referral URL with user's code, or null if project not configured</returns>
        public static string GetReferralUrl()
        {
            var settings = BoostOpsProjectSettings.GetOrCreateSettings();
            if (settings == null)
            {
                Debug.LogWarning("[BoostOps] GetReferralUrl: Project settings not available");
                return null;
            }
            
            // Get project slug from settings
            string projectSlug = settings.projectSlug;
            if (string.IsNullOrEmpty(projectSlug))
            {
                Debug.LogWarning("[BoostOps] GetReferralUrl: Project slug not configured. Please configure in BoostOps settings.");
                return null;
            }
            
            // Get or create user's referral code
            string referralCode = GetReferralCode();
            
            // Build URL with referral code in path
            // Format: https://kenocasino.boostlink.me/r/ABC123
            string url = $"https://{projectSlug}.boostlink.me/r/{Uri.EscapeDataString(referralCode)}";
            
            return url;
        }
        
        // --- Advanced Configuration ---
        
        /// <summary>
        /// Set Amazon Associates affiliate tag to earn referral revenue
        /// </summary>
        /// <param name="associatesTag">Your Amazon Associates tag (e.g., "yourboostops-20")</param>
        public static void SetAmazonAssociatesTag(string associatesTag)
        {
            var manager = GetOrCreateManager();
            manager.SetAmazonAssociatesTag(associatesTag);
        }
        
        /// <summary>
        /// Set the overlay priority for cross-promotion campaigns
        /// </summary>
        /// <param name="sortingOrder">Canvas sorting order (default: 32767)</param>
        public static void SetOverlayPriority(int sortingOrder)
        {
            var manager = GetOrCreateManager();
            manager.SetOverlaySortingOrder(sortingOrder);
        }
        
        /// <summary>
        /// Set custom prefabs for campaign display modes
        /// </summary>
        public static void SetCustomPrefabs(GameObject bannerPrefab = null, GameObject iconInterstitialPrefab = null, GameObject richInterstitialPrefab = null, GameObject nativePrefab = null)
        {
            var manager = GetOrCreateManager();
            manager.SetCustomPrefabs(bannerPrefab, iconInterstitialPrefab, richInterstitialPrefab, nativePrefab);
        }
        
        /// <summary>
        /// Configure SDK to use built-in default prefabs instead of custom ones
        /// Call this before Init() if you want to use the SDK's default UI
        /// </summary>
        /// <param name="useDefaults">True to use default prefabs, false to require custom prefabs</param>
        public static void SetUseDefaultPrefabs(bool useDefaults = true)
        {
            var manager = GetOrCreateManager();
            if (manager != null)
            {
                manager.useDefaultPrefabs = useDefaults;
                Debug.Log($"[BoostOps SDK] 🎨 Use default prefabs set to: {useDefaults}");
            }
        }
        
        /// <summary>
        /// Set custom server endpoint (for testing/enterprise)
        /// </summary>
        /// <param name="endpoint">Custom endpoint URL</param>
        public static void SetCustomEndpoint(string endpoint)
        {
            // Delegate to internal implementation
            EnsureInternal();
            // Internal implementation will handle custom endpoint logic
        }
        
        /// <summary>
        /// Set a custom user ID that will be included in all analytics events
        /// This allows you to correlate BoostOps events with your own user tracking
        /// </summary>
        /// <param name="customUserId">Your custom user identifier (pass null or empty to clear)</param>
        public static void SetCustomUserId(string customUserId)
        {
            BoostOps.Analytics.BoostOpsIdentifierManager.SetCustomUserId(customUserId);
        }
        
        // --- Revenue Tracking API ---
        
        /// <summary>
        /// Track an in-app purchase (EXPLICIT TRACKING - INDUSTRY STANDARD)
        /// 
        /// This is the RECOMMENDED way to track purchases in BoostOps.
        /// Call this method in your IAP purchase callback for guaranteed tracking.
        /// Works reliably alongside other SDKs (Facebook, AppsFlyer, Branch, Firebase, etc.)
        /// 
        /// <code>
        /// BoostOpsSDK.TrackPurchase(
        ///     amount,           // decimal: purchase amount in local currency
        ///     currency,         // string: ISO currency code (USD, EUR, etc.)
        ///     productId,        // string: your product identifier
        ///     transactionId,    // string: store's order/transaction ID (strongly recommended)
        ///     receipt           // string: store receipt/purchase token for server-side validation (optional)
        /// );
        /// </code>
        /// 
        /// If using Unity IAP, prefer the Product overload which extracts all fields automatically:
        /// <code>
        /// BoostOpsSDK.TrackPurchase(args.purchasedProduct);
        /// </code>
        /// </summary>
        /// <param name="amount">Purchase amount in local currency (REQUIRED)</param>
        /// <param name="currency">ISO 4217 currency code: USD, EUR, GBP, JPY, etc. (REQUIRED)</param>
        /// <param name="productId">Product identifier from app store (REQUIRED)</param>
        /// <param name="transactionId">Store transaction ID (STRONGLY RECOMMENDED) - The unique identifier from the app store. Required for deduplication.</param>
        /// <param name="receipt">Store receipt or purchase token (OPTIONAL) - Raw receipt data for server-side validation. On Android this is the purchase token; on iOS this is the app receipt.</param>
        public static void TrackPurchase(
            decimal amount,
            string currency,
            string productId,
            string transactionId = null,
            string receipt = null)
        {
            BoostOpsAnalyticsContract.TrackPurchase(
                amount: amount,
                currency: currency,
                productId: productId,
                transactionId: transactionId,
                receipt: receipt
            );
        }
        
#if UNITY_PURCHASING
        /// <summary>
        /// Track a purchase directly from a Unity IAP Product object.
        /// This is the simplest and most reliable integration — all purchase data
        /// (amount, currency, product ID, transaction ID, receipt) is extracted automatically.
        /// 
        /// <code>
        /// public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args) {
        ///     BoostOpsSDK.TrackPurchase(args.purchasedProduct);
        ///     return PurchaseProcessingResult.Complete;
        /// }
        /// </code>
        /// </summary>
        /// <param name="product">The Unity IAP Product from PurchaseEventArgs.purchasedProduct</param>
        public static void TrackPurchase(UnityEngine.Purchasing.Product product)
        {
            if (product == null)
            {
                BoostOpsLogger.LogError("SDK", "TrackPurchase called with null product");
                return;
            }
            
            TrackPurchase(
                amount: product.metadata.localizedPrice,
                currency: product.metadata.isoCurrencyCode,
                productId: product.definition.id,
                transactionId: product.transactionID,
                receipt: product.receipt
            );
        }
#endif
        
        /// <summary>
        /// Track a conversion event for iOS SKAN optimization (LOCAL ONLY - NO SERVER COST).
        /// 
        /// This updates the iOS SKAN conversion value based on your schema.
        /// Events are NOT sent to the BoostOps server to keep costs minimal.
        /// 
        /// iOS: Updates SKAN conversion value locally (free)
        /// Android: No-op (Android doesn't have SKAN)
        /// 
        /// Common use cases:
        /// - Tutorial completion: "tutorial_complete"
        /// - Registration: "registration_complete"
        /// - Level milestones: "level_10_complete", "level_25_complete"
        /// - Key achievements: "first_win", "high_roller"
        /// 
        /// IMPORTANT: This is for SKAN optimization only.
        /// For engagement analytics, use Firebase or Unity Analytics directly.
        /// BoostOps only tracks install (app_open) and purchase events on the server.
        /// </summary>
        /// <param name="eventName">Event name matching your SKAN schema (e.g., "tutorial_complete")</param>
        /// <param name="parameters">Optional event parameters for SKAN rules (e.g., level number, value)</param>
        public static void TrackConversionEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                Debug.LogWarning("[BoostOps SDK] TrackConversionEvent called with empty eventName");
                return;
            }
            
            #if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("SDK", $"TrackConversionEvent (SKAN only): {eventName}");
            #endif
            
            // Update SKAN conversion value (iOS only, local - no server cost)
            #if UNITY_IOS && !UNITY_EDITOR
            try
            {
                BoostOpsSKAN.UpdateConversionValueForEvent(eventName, parameters);
                
                #if BOOSTOPS_DEBUG_LOGGING
                BoostOpsLogger.LogDebug("SDK", $"✅ SKAN updated for: {eventName} (local only, no server)");
                #endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps SDK] SKAN update failed for {eventName}: {ex.Message}");
            }
            #else
            #if BOOSTOPS_DEBUG_LOGGING
            BoostOpsLogger.LogDebug("SDK", $"⏭️ SKAN skipped (not iOS): {eventName}");
            #endif
            #endif
        }
        
        // --- Internal Methods ---
        
        private static BoostOpsManager GetOrCreateManager()
        {
            if (BoostOpsManager.Instance != null)
                return BoostOpsManager.Instance;
                
            // Create BoostOpsManager GameObject at scene root when needed
            Debug.Log("[BoostOpsSDK] Auto-creating BoostOpsManager GameObject at scene root");
            var managerObject = new GameObject("BoostOpsManager");
            var manager = managerObject.AddComponent<BoostOpsManager>();
            UnityEngine.Object.DontDestroyOnLoad(managerObject);
            return manager;
        }
        
        // Event subscription is now handled automatically through custom add/remove accessors
        // that delegate to the internal implementation - no manual subscription needed

        // --- Asset Loading ---
        // Note: Asset loading and preloading is now handled automatically by the SDK
        // and is not exposed as a public API for better encapsulation and simplicity
        
        // =============================================================================
        // DEMO & ANALYTICS METHODS
        // =============================================================================
        // The following methods are primarily intended for demo apps, testing, and 
        // advanced analytics scenarios. Most production apps won't need these.
        
        /// <summary>
        /// Get total impressions across all campaigns today
        /// NOTE: Primarily for demo/analytics use - most apps don't need this
        /// </summary>
        /// <returns>Total impressions for today</returns>
        public static int GetTotalImpressions()
        {
            var manager = BoostOpsManager.Instance;
            if (manager != null)
            {
                return manager.GetTotalImpressions();
            }
            return 0;
        }
        
        /// <summary>
        /// Get total clicks across all campaigns today
        /// NOTE: Primarily for demo/analytics use - most apps don't need this
        /// </summary>
        /// <returns>Total clicks for today</returns>
        public static int GetTotalClicks()
        {
            var manager = BoostOpsManager.Instance;
            if (manager != null)
            {
                return manager.GetTotalClicks();
            }
            return 0;
        }
        
        // Frequency cap methods removed - internal campaign management only

        #region Type Conversion Methods

        /// <summary>
        /// Convert public InitError to internal InitError
        /// </summary>
        private static BoostOps.Internal.InitError ConvertToInternal(InitError publicError)
        {
            if (publicError == null) return null;
            return new BoostOps.Internal.InitError
            {
                Code = publicError.Code,
                Message = publicError.Message,
                InnerException = publicError.InnerException
            };
        }

        /// <summary>
        /// Convert internal InitError to public InitError
        /// </summary>
        private static InitError ConvertFromInternal(BoostOps.Internal.InitError internalError)
        {
            if (internalError == null) return null;
            return new InitError
            {
                Code = internalError.Code,
                Message = internalError.Message,
                InnerException = internalError.InnerException
            };
        }

        /// <summary>
        /// Convert internal InitResult to public InitResult
        /// </summary>
        private static InitResult ConvertFromInternal(BoostOps.Internal.InitResult internalResult)
        {
            if (internalResult == null) return null;
            return new InitResult
            {
                Success = internalResult.Success,
                Mode = internalResult.Mode,
                CampaignCount = internalResult.CampaignCount,
                ErrorMessage = internalResult.ErrorMessage
            };
        }

        /// <summary>
        /// Convert public PromoFormat to internal PromoFormat
        /// </summary>
        private static BoostOps.Internal.PromoFormat ConvertToInternal(PromoFormat publicFormat)
        {
            return (BoostOps.Internal.PromoFormat)(int)publicFormat;
        }

        /// <summary>
        /// Convert public PromoOptions to internal PromoOptions
        /// </summary>
        private static BoostOps.Internal.PromoOptions ConvertToInternal(PromoOptions publicOptions)
        {
            if (publicOptions == null) return null;
            return new BoostOps.Internal.PromoOptions
            {
                MaxRetries = publicOptions.MaxRetries,
                AllowCaching = publicOptions.AllowCaching,
                CustomData = publicOptions.CustomData
            };
        }
        
        /// <summary>
        /// Append cross-promotion attribution parameters to store URL (Android only)
        /// </summary>
        private static string AppendCrossPromoAttributionParameters(string baseUrl, string campaignId, string placement)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: Add Play Store referrer parameters
            string sourcePackageName = BoostOpsAnalyticsContract.GetSourceStoreId();
            string clickId = System.Guid.NewGuid().ToString("N"); // Generate unique click ID
            long clickTimestamp = (long)(System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1))).TotalMilliseconds;
            
            // Build referrer parameters
            var referrerParams = new System.Collections.Generic.Dictionary<string, string>
            {
                { "utm_source", sourcePackageName },
                { "utm_medium", "xpromo" },
                { "utm_campaign", campaignId },
                { "utm_content", placement },
                { "xp_click_id", clickId },
                { "click_ts", clickTimestamp.ToString() }
            };
            
            // URL encode referrer parameters
            var encodedParams = new System.Collections.Generic.List<string>();
            foreach (var param in referrerParams)
            {
                string encodedKey = System.Uri.EscapeDataString(param.Key);
                string encodedValue = System.Uri.EscapeDataString(param.Value);
                encodedParams.Add($"{encodedKey}={encodedValue}");
            }
            
            string referrerString = string.Join("&", encodedParams.ToArray());
            
            // Append referrer to Play Store URL
            if (baseUrl.Contains("?"))
            {
                return $"{baseUrl}&referrer={referrerString}";
            }
            else
            {
                return $"{baseUrl}?referrer={referrerString}";
            }
#else
            // iOS and other platforms: return URL as-is (fingerprint matching only)
            return baseUrl;
#endif
        }
        
        #endregion
    }
    
    // --- Data Types ---
    // Note: Core data types (PromoFormat, InitResult, etc.) are now defined in BoostOpsTypes.cs
    
    /// <summary>
    /// Unity main thread dispatcher helper for async callbacks.
    /// Automatically handles execution on the main thread with proper MonoBehaviour integration.
    /// </summary>
    internal static class UnityMainThreadDispatcher
    {
        private static readonly Queue<System.Action> _executionQueue = new Queue<System.Action>();
        private static volatile bool initialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (!initialized)
            {
                // Check if dispatcher already exists to prevent duplicates
                var existing = GameObject.Find("BoostOpsMainThreadDispatcher");
                if (existing == null)
                {
                    var go = new GameObject("BoostOpsMainThreadDispatcher");
                    go.AddComponent<MainThreadDispatcherComponent>();
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    BoostOpsLogger.LogInfo("SDK", "🚀 Main thread dispatcher initialized");
                }
                else
                {
                    BoostOpsLogger.LogDebug("SDK", "⚠️ Main thread dispatcher already exists, skipping creation");
                }
                initialized = true;
            }
        }
        
        public static void Enqueue(System.Action action)
        {
            if (action == null) return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
        
        /// <summary>
        /// Process the execution queue - called by MainThreadDispatcherComponent
        /// </summary>
        internal static void ProcessQueue()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    try
                    {
                        _executionQueue.Dequeue()?.Invoke();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[BoostOps] Error executing main thread action: {e.Message}");
                    }
                }
            }
        }

        private class MainThreadDispatcherComponent : MonoBehaviour
        {
            private void Update()
            {
                UnityMainThreadDispatcher.ProcessQueue();
            }
        }
    }
    
    // --- Public API Types ---
    
    /// <summary>
    /// Promo format selection for ShowCrossPromo() method
    /// </summary>
    public enum PromoFormat 
    {
        Auto,             // Smart selection (server or client logic chooses best)
        Banner,           // Small banner overlay
        Native,           // Custom integrated display  
        Icon,             // Simple popup with app icon
        Rich              // Full-screen rich interstitial (default for Auto)
    }
    
    /// <summary>
    /// Optional configuration for ShowCrossPromo() calls
    /// </summary>
    public class PromoOptions
    {
        public int MaxRetries { get; set; } = 1;         // Retry failed requests
        public bool AllowCaching { get; set; } = true;   // Use cached campaigns
        public Dictionary<string, string> CustomData { get; set; }  // Extra targeting data
    }
    
    /// <summary>
    /// Initialization options (passed to Init method)
    /// </summary>
    public class InitOptions
    {
        public TimeSpan RemoteConfigTTL { get; set; } = TimeSpan.FromHours(6);  // Cache duration
        public bool ForceRemote { get; set; } = false;        // Force remote fetch
        public bool EnableDebugLogging { get; set; } = false; // Debug logs
        public string CustomEndpoint { get; set; } = null;    // Override server URL
    }
    
    /// <summary>
    /// Result of initialization (passed to Init callback)
    /// </summary>
    public class InitResult
    {
        public bool Success { get; set; }                    // True if initialization completed successfully
        public string Mode { get; set; }                     // "Online", "LocalOnly", or "Offline"
        public int CampaignCount { get; set; }               // Number of campaigns loaded
        public string ErrorMessage { get; set; }             // Error details if Success = false
    }
    
    /// <summary>
    /// Fatal initialization errors (OnInitFailed event)
    /// </summary>
    public class InitError
    {
        public string Message { get; set; }                  // Human-readable error description
        public string Code { get; set; }                     // Error code for programmatic handling
        public Exception InnerException { get; set; }        // Original exception if available
    }
    
    /// <summary>
    /// Strongly-typed deep link information
    /// </summary>
    public class DeepLinkInfo
    {
        public bool IsValid { get; set; }                           // True if deep link is valid
        public string Error { get; set; }                           // Error message if not valid
        public bool HasCampaign { get; set; }                      // True if deep link contains campaign data
        public string CampaignId { get; set; }                     // Campaign identifier
        public string SourceApp { get; set; }                      // Source application identifier
        public string Source { get; set; }                         // Traffic source (cross_promo, social, etc.)
        public string Medium { get; set; }                         // Campaign medium (banner, interstitial, etc.)
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>(); // Parameters from deep link
        public Dictionary<string, string> CustomParams { get; set; } = new Dictionary<string, string>(); // Additional custom parameters
        public bool IsFirstSession { get; set; }                   // True if this is user's first app launch
    }
} 