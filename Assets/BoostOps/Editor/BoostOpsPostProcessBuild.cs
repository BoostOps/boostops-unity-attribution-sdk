using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine.Networking;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using BoostOps.CrossPromo;
using BoostOps.Core;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace BoostOps.Editor
{
    /// <summary>
    /// Automatically configures iOS entitlements for Universal Links during build
    /// Ensures com.apple.developer.associated-domains is properly set
    /// </summary>
    public class BoostOpsPostProcessBuild
    {
        [PostProcessBuild(1)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            // Generate cross-promotion JSON for all platforms during build
            GenerateCrossPromoJson(pathToBuiltProject);

#if UNITY_IOS
            if (buildTarget == BuildTarget.iOS)
            {
                ProcessIOSBuild(pathToBuiltProject);
            }
#endif
        }

        private static void GenerateCrossPromoJson(string pathToBuiltProject)
        {
            try
            {
                // Check if we're in Managed mode by loading project settings
                var settings = BoostOpsProjectSettings.GetOrCreateSettings();
                bool isManagedMode = settings.useRemoteManagement;

                Debug.Log($"[BoostOps] GenerateCrossPromoJson: Managed mode = {isManagedMode}");

                if (isManagedMode)
                {
                    // In Managed mode, ensure icons are cached from server data
                    CacheIconsForManagedMode();
                }
                else
                {
                    // In Local mode, use CrossPromoTable as before
                    GenerateLocalModeCrossPromoJson(pathToBuiltProject);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to generate cross-promo JSON: {e.Message}");
            }
        }

        private static void GenerateLocalModeCrossPromoJson(string pathToBuiltProject)
            {
                // Look for CrossPromoTable asset
                const string crossPromoAssetPath = "Assets/Resources/BoostOps/CrossPromoTable.asset";
                CrossPromoTable crossPromoTable = AssetDatabase.LoadAssetAtPath<CrossPromoTable>(crossPromoAssetPath);
                
                if (crossPromoTable == null)
                {
                    Debug.Log("[BoostOps] No CrossPromoTable found. Skipping cross-promo JSON generation.");
                    return;
                }

                // Generate modern campaign format instead of legacy CrossPromoSettings
                string json = GenerateModernCampaignJson(crossPromoTable);

                // Save to Resources/BoostOps for reliable cross-platform loading
                string resourcesPath = Path.Combine(Application.dataPath, "Resources", "BoostOps");
                if (!Directory.Exists(resourcesPath))
                {
                    Directory.CreateDirectory(resourcesPath);
                }
                
                // Save as cross_promo_local.json in Resources
                string resourcesJsonPath = Path.Combine(resourcesPath, "cross_promo_local.json");
                File.WriteAllText(resourcesJsonPath, json);

                Debug.Log($"[BoostOps] ✅ Generated cross-promo JSON in Resources: {resourcesJsonPath}");
                Debug.Log($"[BoostOps] Cross-promo config: {crossPromoTable.targets.Length} target game(s), rotation: {crossPromoTable.rotation}");
            }

        private static void CacheIconsForManagedMode()
        {
            Debug.Log("[BoostOps] 🔄 Managed mode detected - ensuring campaign icons are cached for build...");

            // Check if server file exists in StreamingAssets
            string serverFilePath = Path.Combine(Application.streamingAssetsPath, "BoostOps", "cross_promo_server.json");
            if (!File.Exists(serverFilePath))
            {
                Debug.LogWarning("[BoostOps] ⚠️ Managed mode enabled but no cross_promo_server.json found in StreamingAssets. Icons may not be cached properly.");
                return;
            }

            try
            {
                string serverConfigJson = File.ReadAllText(serverFilePath);
                if (string.IsNullOrEmpty(serverConfigJson) || serverConfigJson == "{}")
                {
                    Debug.LogWarning("[BoostOps] ⚠️ cross_promo_server.json is empty or invalid. Cannot cache icons.");
                    return;
                }

                // Parse server JSON to extract campaign data (simplified version)
                var campaigns = ParseServerConfigForIconCaching(serverConfigJson);
                
                Debug.Log($"[BoostOps] Found {campaigns.Count} campaigns in server config");

                // Cache icons synchronously during build
                int cachedCount = 0;
                foreach (var campaign in campaigns)
                {
                    if (CacheSingleCampaignIcon(campaign))
                    {
                        cachedCount++;
                    }
                }

                Debug.Log($"[BoostOps] ✅ Cached {cachedCount}/{campaigns.Count} campaign icons for Managed mode build");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ Failed to cache icons for Managed mode: {ex.Message}");
            }
        }

        private static List<CampaignIconInfo> ParseServerConfigForIconCaching(string serverConfigJson)
        {
            var campaigns = new List<CampaignIconInfo>();

            try
            {
                var config = JsonUtility.FromJson<ServerConfigData>(serverConfigJson);
                if (config?.campaigns != null)
                {
                    foreach (var campaign in config.campaigns)
                    {
                        if (campaign?.target_project != null)
                        {
                            var iconInfo = new CampaignIconInfo
                            {
                                campaignId = campaign.campaign_id,
                                name = campaign.name,
                                iconUrl = campaign.icon_url,
                                storeUrls = campaign.target_project.store_urls,
                                storeIds = campaign.target_project.store_ids
                            };
                            campaigns.Add(iconInfo);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BoostOps] Failed to parse server config for icon caching: {ex.Message}");
            }

            return campaigns;
        }

        private static bool CacheSingleCampaignIcon(CampaignIconInfo campaign)
        {
            try
            {
                // Ensure icons directory exists
                string iconsPath = Path.Combine(Application.dataPath, "Resources", "BoostOps", "Icons");
                if (!Directory.Exists(iconsPath))
                {
                    Directory.CreateDirectory(iconsPath);
                    Debug.Log($"[BoostOps] Created icons directory: {iconsPath}");
                }

                // Determine the expected icon filename based on store IDs
                string iconFilename = GetExpectedIconFilename(campaign);
                if (string.IsNullOrEmpty(iconFilename))
                {
                    Debug.LogWarning($"[BoostOps] Cannot determine icon filename for campaign {campaign.campaignId} - no valid store IDs");
                    return false;
                }

                string iconFilePath = Path.Combine(iconsPath, iconFilename + ".png");
                
                // Check if icon already exists
                if (File.Exists(iconFilePath))
                {
                    Debug.Log($"[BoostOps] Icon already cached for {campaign.name}: {iconFilename}.png");
                    return true; // Already cached
                }

                // Try to download the icon (simplified synchronous version for build-time)
                string iconUrl = GetIconDownloadUrl(campaign);
                if (string.IsNullOrEmpty(iconUrl))
                {
                    Debug.LogWarning($"[BoostOps] No icon URL available for campaign {campaign.name}");
                    return false;
                }

                Debug.Log($"[BoostOps] Downloading icon for {campaign.name} from: {iconUrl}");
                
                // Download icon synchronously (build-time only)
                bool downloadSuccess = DownloadIconSynchronously(iconUrl, iconFilePath);
                if (!downloadSuccess)
                {
                    Debug.LogWarning($"[BoostOps] Failed to download icon from {iconUrl}");
                    return false;
                }

                Debug.Log($"[BoostOps] ✅ Successfully cached icon: {iconFilename}.png");
                
                // Refresh AssetDatabase to recognize new icon
                AssetDatabase.ImportAsset($"Assets/Resources/BoostOps/Icons/{iconFilename}.png");
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Failed to cache icon for {campaign.name}: {ex.Message}");
                return false;
            }
        }

        private static string GetExpectedIconFilename(CampaignIconInfo campaign)
        {
            // Priority order: iOS -> Android -> Amazon (matches the Editor logic)
            if (campaign.storeIds != null)
            {
                if (!string.IsNullOrEmpty(campaign.storeIds.apple))
                    return $"{campaign.storeIds.apple}_ios_icon";
                    
                if (!string.IsNullOrEmpty(campaign.storeIds.google))
                    return $"{campaign.storeIds.google.Replace(".", "_")}_android_icon";
                    
                if (!string.IsNullOrEmpty(campaign.storeIds.amazon))
                    return $"{campaign.storeIds.amazon}_amazon_icon";
            }
            
            return null;
        }

        private static string GetIconDownloadUrl(CampaignIconInfo campaign)
        {
            // First try direct icon URL
            if (!string.IsNullOrEmpty(campaign.iconUrl))
                return campaign.iconUrl;
            
            // If no direct icon URL, log the issue but don't try complex fallbacks during build
            Debug.LogWarning($"[BoostOps] No direct icon URL for campaign {campaign.name}. " +
                           "Icon should be pre-cached in the Editor using the 'Cache App Icons' feature.");
            
            return null; // No viable icon URL found
        }

        private static bool DownloadIconSynchronously(string iconUrl, string savePath)
        {
            try
            {
                using (var request = UnityWebRequest.Get(iconUrl))
                {
                    // Synchronous download (Editor-only, build-time only)
                    request.SendWebRequest();
                    
                    // Wait for completion (synchronous)
                    while (!request.isDone)
                    {
                        System.Threading.Thread.Sleep(10); // Brief pause to avoid blocking completely
                    }
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        byte[] imageData = request.downloadHandler.data;
                        File.WriteAllBytes(savePath, imageData);
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"[BoostOps] Download failed: {request.error}");
                        return false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Exception downloading icon: {ex.Message}");
                return false;
            }
        }

        private static string GetStreamingAssetsPath(string pathToBuiltProject)
        {
            // Different platforms store StreamingAssets in different locations
#if UNITY_ANDROID
            return Path.Combine(pathToBuiltProject, "assets", "bin", "Data", "StreamingAssets");
#elif UNITY_IOS
            return Path.Combine(pathToBuiltProject, "Data", "Raw");
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            return Path.Combine(pathToBuiltProject, Application.productName + "_Data", "StreamingAssets");
#elif UNITY_WEBGL
            return Path.Combine(pathToBuiltProject, "StreamingAssets");
#else
            // Fallback for other platforms
            return Path.Combine(pathToBuiltProject, "StreamingAssets");
#endif
        }

#if UNITY_IOS
        private static void ProcessIOSBuild(string pathToBuiltProject)
        {
            try
            {
                // Get Xcode project and Info.plist paths
                string projectPath = pathToBuiltProject + "/Unity-iPhone.xcodeproj/project.pbxproj";
                string infoPlistPath = pathToBuiltProject + "/Info.plist";
                
                // Load Xcode project
                PBXProject project = new PBXProject();
                project.ReadFromFile(projectPath);

                // Get main target
                string targetGuid = project.GetUnityMainTargetGuid();
                
                // Add required iOS frameworks for BoostOps attribution
                AddRequiredFrameworks(project, targetGuid);
                
                // ALWAYS add StoreKit 2 configuration for enhanced purchase tracking
                AddStoreKit2Configuration(infoPlistPath);
                
                // ALWAYS add SKAN (SKAdNetwork) configuration for attribution
                AddSKANConfiguration(infoPlistPath);
                
                // Handle Universal Links configuration if available
                var configPath = FindUniversalLinksConfig();
                if (!string.IsNullOrEmpty(configPath))
                {
                    var domains = ExtractDomainsFromConfig(configPath);
                    if (domains.Count > 0)
                    {
                        // Merge associated domains into existing entitlements file
                        MergeAssociatedDomainsIntoEntitlements(project, targetGuid, pathToBuiltProject, domains);
                        Debug.Log($"[BoostOps] ✅ Automatically merged Universal Links entitlements for {domains.Count} domains: {string.Join(", ", domains)}");
                    }
                    else
                    {
                        Debug.LogWarning("[BoostOps] No domains found in Universal Links config. Skipping entitlements setup.");
                    }
                }
                else
                {
                    Debug.Log("[BoostOps] No Universal Links config found. Skipping entitlements setup.");
                }
                
                // Write changes back to project
                project.WriteToFile(projectPath);
                
                Debug.Log("[BoostOps] ✅ iOS build configuration completed successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to configure iOS build: {e.Message}");
            }
        }
        
        /// <summary>
        /// Add required iOS frameworks for BoostOps attribution and tracking
        /// </summary>
        private static void AddRequiredFrameworks(PBXProject project, string targetGuid)
        {
            try
            {
                // Required frameworks for BoostOps attribution and tracking
                string[] frameworks = {
                    "AdServices.framework",    // Modern attribution (AAAttribution) - iOS 14.3+
                    "StoreKit.framework"       // Purchase tracking - iOS 3.0+
                };
                
                // Note: iAd.framework was deprecated and removed by Apple
                // We only support modern attribution via AdServices framework

                foreach (string framework in frameworks)
                {
                    // Check if framework is already added to avoid duplicates
                    if (!project.ContainsFramework(targetGuid, framework))
                    {
                        project.AddFrameworkToProject(targetGuid, framework, false); // false = required (not weak)
                        Debug.Log($"[BoostOps] ✅ Added framework: {framework}");
                    }
                    else
                    {
                        Debug.Log($"[BoostOps] Framework already linked: {framework}");
                    }
                }
                
                // Make AdServices.framework weak linked since it's only available iOS 14.3+
                // This allows the app to run on older iOS versions without crashing
                if (project.ContainsFramework(targetGuid, "AdServices.framework"))
                {
                    // Remove the required framework and re-add as weak
                    project.RemoveFrameworkFromProject(targetGuid, "AdServices.framework");
                    project.AddFrameworkToProject(targetGuid, "AdServices.framework", true); // true = weak linked
                    Debug.Log("[BoostOps] ✅ AdServices.framework set to weak linking for iOS compatibility");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not add iOS frameworks: {e.Message}");
                // Don't fail the build - attribution will still work if frameworks are manually added
            }
        }
        
        /// <summary>
        /// Automatically adds StoreKit 2 configuration to Info.plist for enhanced purchase tracking
        /// This ensures both StoreKit 1 and StoreKit 2 purchases are captured properly
        /// </summary>
        private static void AddStoreKit2Configuration(string infoPlistPath)
        {
            try
            {
                // Load Info.plist
                PlistDocument plist = new PlistDocument();
                plist.ReadFromFile(infoPlistPath);
                
                // Add StoreKit 2 consumable purchase history tracking
                // This ensures StoreKit 2 automatically logs consumable purchases
                plist.root.SetBoolean("SKIncludeConsumableInAppPurchaseHistory", true);
                
                // Write changes back to Info.plist
                plist.WriteToFile(infoPlistPath);
                
                Debug.Log("[BoostOps] ✅ Added StoreKit 2 configuration - consumable purchases will be tracked automatically");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not add StoreKit 2 configuration: {e.Message}");
                // Don't fail the build for this - it's an enhancement, not critical
            }
        }

        /// <summary>
        /// Automatically adds SKAN (SKAdNetwork) configuration to Info.plist for attribution
        /// Includes Google Ads, Meta (Facebook), and partner DSP identifiers
        /// Also sets BoostOps attribution endpoint for first-party postbacks
        /// </summary>
        private static void AddSKANConfiguration(string infoPlistPath)
        {
            try
            {
                // Load Info.plist
                PlistDocument plist = new PlistDocument();
                plist.ReadFromFile(infoPlistPath);
                
                // Add BoostOps attribution endpoint for first-party SKAN postbacks
                // Apple will send attribution data here for direct analysis
                plist.root.SetString("NSAdvertisingAttributionReportEndpoint", BoostOpsSKANConfiguration.BOOSTOPS_ATTRIBUTION_ENDPOINT);
                
                // Get or create SKAdNetworkItems array
                PlistElementArray skAdNetworkItems;
                if (plist.root.values.ContainsKey("SKAdNetworkItems"))
                {
                    skAdNetworkItems = plist.root["SKAdNetworkItems"].AsArray();
                    Debug.Log($"[BoostOps] Found existing SKAdNetworkItems with {skAdNetworkItems.values.Count} entries");
                }
                else
                {
                    skAdNetworkItems = plist.root.CreateArray("SKAdNetworkItems");
                    Debug.Log("[BoostOps] Created new SKAdNetworkItems array");
                }
                
                // Track existing IDs to avoid duplicates
                var existingIds = new HashSet<string>();
                foreach (var item in skAdNetworkItems.values)
                {
                    if (item.AsDict().values.ContainsKey("SKAdNetworkIdentifier"))
                    {
                        string existingId = item.AsDict()["SKAdNetworkIdentifier"].AsString();
                        existingIds.Add(existingId);
                    }
                }
                
                // Add all BoostOps SKAN IDs (avoid duplicates)
                int addedCount = 0;
                foreach (string skAdNetworkId in BoostOpsSKANConfiguration.ALL_SKADNETWORK_IDS)
                {
                    if (!existingIds.Contains(skAdNetworkId))
                    {
                        var skAdNetworkDict = skAdNetworkItems.AddDict();
                        skAdNetworkDict.SetString("SKAdNetworkIdentifier", skAdNetworkId);
                        addedCount++;
                    }
                }
                
                // Write changes back to Info.plist
                plist.WriteToFile(infoPlistPath);
                
                // Validate configuration
                var allConfiguredIds = skAdNetworkItems.values
                    .Where(item => item.AsDict().values.ContainsKey("SKAdNetworkIdentifier"))
                    .Select(item => item.AsDict()["SKAdNetworkIdentifier"].AsString())
                    .ToArray();
                
                var validation = BoostOpsSKANConfiguration.ValidateConfiguration(allConfiguredIds);
                
                Debug.Log($"[BoostOps] ✅ SKAN configuration complete:");
                Debug.Log($"[BoostOps]   • Added {addedCount} new SKAdNetwork IDs");
                Debug.Log($"[BoostOps]   • Total IDs configured: {allConfiguredIds.Length}");
                Debug.Log($"[BoostOps]   • Attribution endpoint: {BoostOpsSKANConfiguration.BOOSTOPS_ATTRIBUTION_ENDPOINT}");
                Debug.Log($"[BoostOps]   • {validation.GetSummary()}");
                
                if (!validation.IsValid)
                {
                    Debug.LogWarning($"[BoostOps] ⚠️ SKAN validation failed: {string.Join(", ", validation.MissingCriticalIds)}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to add SKAN configuration: {e.Message}");
                // Don't fail the build - SKAN is important but not critical for basic functionality
            }
        }

        private static string FindUniversalLinksConfig()
        {
            // Look for BoostOps Project Settings asset
            string configAssetPath = "Assets/Resources/BoostOps/BoostOpsProjectSettings.asset";
            
            if (File.Exists(configAssetPath))
            {
                return configAssetPath;
            }

            return null;
        }

        private static List<string> ExtractDomainsFromConfig(string configPath)
        {
            try
            {
                // Load the BoostOpsProjectSettings asset directly
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<BoostOps.BoostOpsProjectSettings>(configPath);
                
                if (config != null)
                {
                    var domains = config.GetAllHosts();
                    if (domains != null && domains.Count > 0)
                    {
                        Debug.Log($"[BoostOps] Found {domains.Count} domains in project settings: {string.Join(", ", domains)}");
                        return domains;
                    }
                }

                Debug.LogWarning("[BoostOps] No domains found in BoostOpsProjectSettings asset");
                return new List<string>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BoostOps] Error reading project settings asset: {e.Message}");
                return new List<string>();
            }
        }

        private static void MergeAssociatedDomainsIntoEntitlements(PBXProject project, string targetGuid, string pathToBuiltProject, List<string> domains)
        {
            // Look for existing entitlements files that Unity might have created
            string[] possibleEntitlementsPaths = {
                pathToBuiltProject + "/Entitlements.entitlements",  // Most common Unity default
                pathToBuiltProject + "/Unity-iPhone.entitlements", // Alternative Unity naming
                pathToBuiltProject + "/" + PlayerSettings.productName + ".entitlements"  // Product-based naming
            };
            
            string existingEntitlementsPath = null;
            PlistDocument entitlements = null;
            
            // Try to find and load existing entitlements file
            foreach (string path in possibleEntitlementsPaths)
            {
                if (File.Exists(path))
                {
                    existingEntitlementsPath = path;
                    entitlements = new PlistDocument();
                    entitlements.ReadFromFile(path);
                    Debug.Log($"[BoostOps] Found existing entitlements file: {Path.GetFileName(path)}");
                    break;
                }
            }
            
            // If no existing entitlements file found, create a new one using Unity's standard naming
            if (entitlements == null)
            {
                existingEntitlementsPath = pathToBuiltProject + "/Entitlements.entitlements";
                entitlements = new PlistDocument();
                Debug.Log($"[BoostOps] No existing entitlements found, creating new file: Entitlements.entitlements");
            }
            
            PlistElementDict rootDict = entitlements.root;
            
            // Get or create associated domains array
            PlistElementArray associatedDomains;
            if (rootDict.values.ContainsKey("com.apple.developer.associated-domains"))
            {
                associatedDomains = rootDict["com.apple.developer.associated-domains"].AsArray();
                Debug.Log($"[BoostOps] Found existing associated domains: {associatedDomains.values.Count} entries");
            }
            else
            {
                associatedDomains = rootDict.CreateArray("com.apple.developer.associated-domains");
                Debug.Log("[BoostOps] Created new associated domains array");
            }
            
            // Add BoostOps domains (avoid duplicates)
            int addedCount = 0;
            foreach (string domain in domains)
            {
                string applinkEntry = $"applinks:{domain}";
                
                // Check if this domain already exists
                bool alreadyExists = false;
                foreach (var existingValue in associatedDomains.values)
                {
                    if (existingValue.AsString() == applinkEntry)
                    {
                        alreadyExists = true;
                        break;
                    }
                }
                
                if (!alreadyExists)
                {
                    associatedDomains.AddString(applinkEntry);
                    addedCount++;
                    Debug.Log($"[BoostOps] Added domain: {applinkEntry}");
                }
                else
                {
                    Debug.Log($"[BoostOps] Domain already exists: {applinkEntry}");
                }
            }
            
            // Write entitlements file
            entitlements.WriteToFile(existingEntitlementsPath);
            
            // Make sure Xcode project references the correct entitlements file
            string entitlementsFileName = Path.GetFileName(existingEntitlementsPath);
            
            // Only add to project if it's not already referenced
            string currentEntitlements = project.GetBuildPropertyForAnyConfig(targetGuid, "CODE_SIGN_ENTITLEMENTS");
            if (string.IsNullOrEmpty(currentEntitlements) || currentEntitlements != entitlementsFileName)
            {
                // Add entitlements file to Xcode project if not already added
                string entitlementsFileGuid = project.AddFile(entitlementsFileName, entitlementsFileName, PBXSourceTree.Source);
                
                // Set the entitlements file for the target
                project.SetBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsFileName);
                Debug.Log($"[BoostOps] Set entitlements file reference: {entitlementsFileName}");
            }
            
            Debug.Log($"[BoostOps] ✅ Successfully merged {addedCount} new BoostOps domains into entitlements file");
        }
#endif

        /// <summary>
        /// Get store ID from BoostOpsProjectSettings for the specified store
        /// </summary>
        private static string GetProjectSettingsStoreId(string store)
        {
            var settings = BoostOps.BoostOpsProjectSettings.GetInstance();
            if (settings == null) return null;
            
            switch (store.ToLower())
            {
                case "apple":
                    return !string.IsNullOrEmpty(settings.appleAppStoreId) ? settings.appleAppStoreId : null;
                case "google":
                case "android":
                    return !string.IsNullOrEmpty(settings.androidPackageName) ? settings.androidPackageName : null;
                case "amazon":
                    return !string.IsNullOrEmpty(settings.amazonStoreId) ? settings.amazonStoreId : null;
                case "windows":
                    return !string.IsNullOrEmpty(settings.windowsStoreId) ? settings.windowsStoreId : null;
                case "samsung":
                    return !string.IsNullOrEmpty(settings.samsungStoreId) ? settings.samsungStoreId : null;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Generate modern campaign format JSON from CrossPromoTable data
        /// </summary>
        private static string GenerateModernCampaignJson(CrossPromoTable table)
        {
            var campaigns = new List<object>();
            
            for (int i = 0; i < table.targets.Length; i++)
            {
                var target = table.targets[i];
                if (target == null) continue;
                
                // Generate conventional local_key based on store IDs
                string iconLocalKey = GenerateConventionalIconPath(target);
                
                // Build store URLs
                var storeUrls = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(target.iosAppStoreId))
                    storeUrls["apple"] = $"https://apps.apple.com/app/id{target.iosAppStoreId}";
                if (!string.IsNullOrEmpty(target.androidPackageId))
                    storeUrls["google"] = $"https://play.google.com/store/apps/details?id={target.androidPackageId}";
                if (!string.IsNullOrEmpty(target.amazonStoreId))
                {
                    // Support both ASIN format (10-char alphanumeric) and package name format
                    if (target.amazonStoreId.Length == 10 && System.Text.RegularExpressions.Regex.IsMatch(target.amazonStoreId, @"^[A-Z0-9]{10}$"))
                        storeUrls["amazon"] = $"https://www.amazon.com/dp/{target.amazonStoreId}";
                    else
                        storeUrls["amazon"] = $"https://www.amazon.com/gp/mas/dl/android?p={target.amazonStoreId}";
                }
                if (!string.IsNullOrEmpty(target.samsungStoreId))
                    storeUrls["samsung"] = $"samsungapps://ProductDetail/{target.samsungStoreId}";
                if (!string.IsNullOrEmpty(target.windowsStoreId))
                    storeUrls["microsoft"] = $"ms-windows-store://pdp/?productid={target.windowsStoreId}";
                
                // Build store IDs (extracted canonical identifiers)
                var storeIds = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(target.iosAppStoreId))
                    storeIds["apple"] = target.iosAppStoreId;
                if (!string.IsNullOrEmpty(target.androidPackageId))
                    storeIds["google"] = target.androidPackageId;
                if (!string.IsNullOrEmpty(target.amazonStoreId))
                    storeIds["amazon"] = target.amazonStoreId;
                if (!string.IsNullOrEmpty(target.samsungStoreId))
                    storeIds["samsung"] = target.samsungStoreId;
                if (!string.IsNullOrEmpty(target.windowsStoreId))
                    storeIds["microsoft"] = target.windowsStoreId;
                
                // Build platform IDs (bundle identifiers and platform-specific data)
                var platformIds = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(target.iosBundleId))
                    platformIds["ios_bundle_id"] = target.iosBundleId;
                if (!string.IsNullOrEmpty(target.androidPackageId))
                    platformIds["android_package_name"] = target.androidPackageId;
                
                // Build creatives array (only add icon if we have a local_key)
                var creatives = new List<object>();
                if (!string.IsNullOrEmpty(iconLocalKey))
                {
                    creatives.Add(new {
                        creative_id = $"{target.headline?.Replace(" ", "_").ToLower() ?? "campaign"}_icon",
                        format = "icon",
                        orientation = "any",
                        prefetch = true,
                        ttl_hours = 24,
                        variants = new[] {
                            new {
                                resolution = "512x512",
                                url = "",
                                sha256 = "",
                                local_key = iconLocalKey
                            }
                        }
                    });
                }
                
                // Build campaign object with nested schedule
                var effectiveFreqCap = target.GetEffectiveFrequencyCap(table);
                var campaign = new {
                    campaign_id = $"campaign_{i + 1}",
                    name = target.headline ?? "", // Optional field - empty is fine
                    status = "active",
                    frequency_cap = new {
                        impressions = effectiveFreqCap.impressions,
                        time_unit = effectiveFreqCap.time_unit.ToString()
                    },
                    schedule = new {
                        start_date = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        end_date = System.DateTime.Now.AddMonths(3).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        days = new int[0],  // Empty = all days valid (0=Sun, 1=Mon, ..., 6=Sat)
                        // start_hour and end_hour can be added later for time-of-day targeting
                    },
                    created_at = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    updated_at = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    target_project = new {
                        project_id = target.id ?? $"target_{i + 1}",
                        store_urls = storeUrls,
                        store_ids = storeIds,
                        platform_ids = platformIds,
                        creatives = creatives.ToArray()
                    }
                };
                
                campaigns.Add(campaign);
            }
            
            // Build final JSON structure
            var jsonObject = new {
                version_info = new {
                    api_version = "1.0.0",
                    schema_version = "1.0.0",
                    client_min_version = "1.0.0",
                    server_version = "1.0.0",
                    contract_version = "1.0.0",
                    last_updated = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ")
                },
                source_project = BuildSourceProjectData(table),
                campaigns = campaigns.ToArray()
            };
            
            return JsonUtility.ToJson(jsonObject, true);
        }
        
        /// <summary>
        /// Build source project data with new structured format
        /// </summary>
        private static object BuildSourceProjectData(CrossPromoTable table)
        {
            // Build source store URLs
            var sourceStoreUrls = new Dictionary<string, string>();
            var appleStoreId = GetProjectSettingsStoreId("apple");
            var androidPackageName = GetProjectSettingsStoreId("android") ?? Application.identifier;
            var googleStoreId = GetProjectSettingsStoreId("google") ?? androidPackageName;
            var amazonStoreId = GetProjectSettingsStoreId("amazon");
            var samsungStoreId = GetProjectSettingsStoreId("samsung");
            var windowsStoreId = GetProjectSettingsStoreId("windows");
            
            if (!string.IsNullOrEmpty(appleStoreId))
                sourceStoreUrls["apple"] = $"https://apps.apple.com/app/id{appleStoreId}";
            if (!string.IsNullOrEmpty(googleStoreId))
                sourceStoreUrls["google"] = $"https://play.google.com/store/apps/details?id={googleStoreId}";
            if (!string.IsNullOrEmpty(amazonStoreId))
            {
                // Support both ASIN format (10-char alphanumeric) and package name format
                if (amazonStoreId.Length == 10 && System.Text.RegularExpressions.Regex.IsMatch(amazonStoreId, @"^[A-Z0-9]{10}$"))
                    sourceStoreUrls["amazon"] = $"https://www.amazon.com/dp/{amazonStoreId}";
                else
                    sourceStoreUrls["amazon"] = $"https://www.amazon.com/gp/mas/dl/android?p={amazonStoreId}";
            }
            if (!string.IsNullOrEmpty(samsungStoreId))
                sourceStoreUrls["samsung"] = $"samsungapps://ProductDetail/{samsungStoreId}";
            if (!string.IsNullOrEmpty(windowsStoreId))
                sourceStoreUrls["microsoft"] = $"ms-windows-store://pdp/?productid={windowsStoreId}";
            
            // Build source store IDs
            var sourceStoreIds = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(appleStoreId))
                sourceStoreIds["apple"] = appleStoreId;
            if (!string.IsNullOrEmpty(googleStoreId))
                sourceStoreIds["google"] = googleStoreId;
            if (!string.IsNullOrEmpty(amazonStoreId))
                sourceStoreIds["amazon"] = amazonStoreId;
            if (!string.IsNullOrEmpty(samsungStoreId))
                sourceStoreIds["samsung"] = samsungStoreId;
            if (!string.IsNullOrEmpty(windowsStoreId))
                sourceStoreIds["microsoft"] = windowsStoreId;
                
            // Build source platform IDs
            var sourcePlatformIds = new Dictionary<string, object>();
            sourcePlatformIds["ios_bundle_id"] = Application.identifier;
            sourcePlatformIds["android_package_name"] = androidPackageName;
            
            // Add Apple Team ID if available
            var appleTeamId = GetProjectSettingsStoreId("apple_team_id");
            if (!string.IsNullOrEmpty(appleTeamId))
                sourcePlatformIds["apple_team_id"] = appleTeamId;
            
            // Get project ID from settings
            var settings = BoostOpsProjectSettings.GetInstance();
            var projectId = settings?.projectId ?? "";
            
            return new {
                project_id = projectId,  // ← ADDED: Include source project ID
                bundle_id = Application.identifier,
                name = Application.productName,
                min_player_days = table.minPlayerDay,
                min_sessions = table.minPlayerSession,
                frequency_cap = new {
                    impressions = (table.globalFrequencyCap ?? FrequencyCap.Unlimited()).impressions,
                    time_unit = (table.globalFrequencyCap ?? FrequencyCap.Unlimited()).time_unit.ToString()
                },
                interstitial_icon_cta = table.defaultIconInterstitialButtonText,
                interstitial_icon_text = table.defaultIconInterstitialDescription,
                interstitial_rich_cta = table.defaultRichInterstitialButtonText,
                interstitial_rich_text = table.defaultRichInterstitialDescription,
                store_urls = sourceStoreUrls,
                store_ids = sourceStoreIds,
                platform_ids = sourcePlatformIds
            };
        }
        
        /// <summary>
        /// Generate conventional icon path based on store IDs
        /// This creates paths that work with our local resource structure
        /// </summary>
        private static string GenerateConventionalIconPath(TargetGame target)
        {
            if (target == null) return "";
            
            // Priority order for conventional paths - these paths will have BoostOps/ prepended at runtime
            
            // iOS App Store ID (highest priority)
            if (!string.IsNullOrEmpty(target.iosAppStoreId))
            {
                return $"Icons/{target.iosAppStoreId}_ios_icon";
            }
            
            // Android Package ID (second priority)
            if (!string.IsNullOrEmpty(target.androidPackageId))
            {
                string sanitizedPackageId = target.androidPackageId.Replace(".", "_");
                return $"Icons/{sanitizedPackageId}_android_icon";
            }
            
            // Amazon Store ID (third priority)
            if (!string.IsNullOrEmpty(target.amazonStoreId))
            {
                return $"Icons/{target.amazonStoreId}_amazon_icon";
            }
            
            Debug.Log($"[BoostOps] ⚠️ No store ID available for '{target.headline}' - no local_key generated");
            return ""; // No store ID to base path on
        }

        // Data structures for Managed mode icon caching
        [System.Serializable]
        private class CampaignIconInfo
        {
            public string campaignId;
            public string name;
            public string iconUrl;
            public StoreUrls storeUrls;
            public StoreIds storeIds;
        }

        [System.Serializable]
        private class ServerConfigData
        {
            public ServerCampaign[] campaigns;
        }

        [System.Serializable]
        private class ServerCampaign
        {
            public string campaign_id;
            public string name;
            public string icon_url;
            public TargetProject target_project;
        }

        [System.Serializable]
        private class TargetProject
        {
            public StoreUrls store_urls;
            public StoreIds store_ids;
        }

        [System.Serializable]
        private class StoreUrls
        {
            public string apple;
            public string google;
            public string amazon;
        }

        [System.Serializable]
        private class StoreIds
        {
            public string apple;
            public string google;
            public string amazon;
        }
    }
} 