using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if FIREBASE_REMOTE_CONFIG
using Firebase.RemoteConfig;
#endif

namespace BoostOps
{
#if FIREBASE_REMOTE_CONFIG
    /// <summary>
    /// Firebase Remote Config provider implementation
    /// </summary>
    public class FirebaseRemoteConfigProvider : IRemoteConfigProvider
    {
        public string ProviderName => "Firebase Remote Config";

        public bool IsAvailable => true;

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Wait for Firebase to initialize
                var dependencyStatus = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
                if (dependencyStatus != Firebase.DependencyStatus.Available)
                {
                    BoostOpsLogger.LogError("FirebaseRemoteConfig", $"Firebase not available: {dependencyStatus}");
                    return false;
                }
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", "Firebase initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("FirebaseRemoteConfig", $"Failed to initialize Firebase: {ex.Message}");
                return false;
            }
        }

        public async Task<RemoteConfigResult> FetchAndLoadConfigAsync(string configKey)
        {
            try
            {
                // Set config defaults
                var defaults = new Dictionary<string, object>
                {
                    { configKey, "{}" }
                };
                await FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults);

                // Fetch and activate remote config
                await FirebaseRemoteConfig.DefaultInstance.FetchAsync();
                await FirebaseRemoteConfig.DefaultInstance.ActivateAsync();
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", "Firebase Remote Config fetched and activated");

                // Get the configuration JSON
                string configJson = FirebaseRemoteConfig.DefaultInstance.GetValue(configKey).StringValue;
                
                // Enhanced debug logging
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"=== FIREBASE REMOTE CONFIG DEBUG ===");
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Config Key: {configKey}");
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Raw JSON Retrieved: {configJson}");
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"JSON Length: {configJson?.Length ?? 0} characters");
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Is Empty/Default: {string.IsNullOrEmpty(configJson) || configJson == "{}"}");
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"=== END DEBUG ===");
                
                // Also log to Unity Console for visibility
                UnityEngine.Debug.Log($"[BoostOps Runtime] Firebase Remote Config Key: {configKey}");
                UnityEngine.Debug.Log($"[BoostOps Runtime] Raw JSON Retrieved: {configJson}");
                
                // Save the runtime-retrieved config to EditorPrefs for editor window access
                SaveRuntimeConfigToEditorPrefs(configKey, configJson);

                return ParseConfigurationJson(configJson);
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("FirebaseRemoteConfig", $"Failed to fetch Firebase Remote Config: {ex.Message}");
                return RemoteConfigResult.CreateFailure($"Failed to fetch Firebase Remote Config: {ex.Message}");
            }
        }

        private RemoteConfigResult ParseConfigurationJson(string configJson)
        {
            try
            {
                if (string.IsNullOrEmpty(configJson) || configJson == "{}")
                {
                    BoostOpsLogger.LogDebug("FirebaseRemoteConfig", "No campaigns found in Firebase Remote Config");
                    return RemoteConfigResult.CreateSuccess("{}", new List<Campaign>(), new BoostOpsConfig());
                }

                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", "Parsing JSON using shared RemoteCampaignConfig model (same as editor and Unity Remote Config)");
                
                // Use the same parsing logic as the editor and Unity Remote Config for consistency
                var sharedConfig = UnityEngine.JsonUtility.FromJson<BoostOps.Core.RemoteCampaignConfig>(configJson);
                
                if (sharedConfig?.campaigns != null && sharedConfig.campaigns.Count > 0)
                {
                    BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Successfully parsed {sharedConfig.campaigns.Count} campaigns from shared model");
                    
                    // Convert from BoostOps.Core.Campaign to BoostOps.Campaign (runtime type)
                    var runtimeCampaigns = sharedConfig.campaigns.Select(ConvertCoreToRuntimeCampaign).ToList();
                    
                    var config = BoostOpsConfig.ParseFromJson(configJson);
                    
                    BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Converted to {runtimeCampaigns.Count} runtime campaigns");
                    
                    int validCampaigns = 0;
                    foreach (var campaign in runtimeCampaigns)
                    {
                        if (CampaignParser.IsValidCampaign(campaign))
                        {
                            validCampaigns++;
                            BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Valid campaign: {campaign.name} (ID: {campaign.campaign_id})");
                        }
                        else
                        {
                            BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Invalid campaign: {campaign?.name ?? "Unknown"} - missing required data");
                        }
                    }
                    BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"{validCampaigns} out of {runtimeCampaigns.Count} campaigns are valid");

                    return RemoteConfigResult.CreateSuccess(configJson, runtimeCampaigns, config);
                }
                else
                {
                    BoostOpsLogger.LogWarning("FirebaseRemoteConfig", "Shared model parsing succeeded but no campaigns found");
                    return RemoteConfigResult.CreateSuccess(configJson, new List<Campaign>(), new BoostOpsConfig());
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("FirebaseRemoteConfig", $"Failed to parse configuration using shared model: {ex.Message}");
                
                // Fallback to original parser as last resort
                try
                {
                    BoostOpsLogger.LogDebug("FirebaseRemoteConfig", "Attempting fallback to original parser");
                    var campaigns = CampaignParser.ParseCampaignsFromJson(configJson);
                    var config = BoostOpsConfig.ParseFromJson(configJson);
                    return RemoteConfigResult.CreateSuccess(configJson, campaigns, config);
                }
                catch (Exception fallbackEx)
                {
                    BoostOpsLogger.LogError("FirebaseRemoteConfig", $"Fallback parser also failed: {fallbackEx.Message}");
                    return RemoteConfigResult.CreateFailure($"Both shared model and fallback parsers failed: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Convert from BoostOps.Core.Campaign (JSON deserialization model) to BoostOps.Campaign (runtime model)
        /// </summary>
        private Campaign ConvertCoreToRuntimeCampaign(BoostOps.Core.Campaign coreCampaign)
        {
            var campaign = new Campaign();
            
            campaign.campaign_id = coreCampaign.campaign_id;
            campaign.name = coreCampaign.name;
            campaign.status = coreCampaign.status;
            campaign.min_sessions = coreCampaign.min_sessions;
            campaign.min_player_days = coreCampaign.min_player_days;
            campaign.created_at = coreCampaign.created_at;
            campaign.updated_at = coreCampaign.updated_at;
            
            if (coreCampaign.frequency_cap != null)
            {
                campaign.frequency_cap = new BoostOps.Core.FrequencyCapJson
                {
                    time_unit = coreCampaign.frequency_cap.time_unit,
                    impressions = coreCampaign.frequency_cap.impressions
                };
            }
            
            if (coreCampaign.target_project != null)
            {
                campaign.target_project = new TargetProject();
                campaign.target_project.project_id = coreCampaign.target_project.project_id;
                
                if (coreCampaign.target_project.store_urls != null)
                {
                    campaign.target_project.store_urls = new StoreUrls
                    {
                        apple = coreCampaign.target_project.store_urls.apple,
                        google = coreCampaign.target_project.store_urls.google,
                        amazon = coreCampaign.target_project.store_urls.amazon,
                        microsoft = coreCampaign.target_project.store_urls.microsoft,
                        samsung = coreCampaign.target_project.store_urls.samsung,
                        web = coreCampaign.target_project.store_urls.web
                    };
                }
                
                if (coreCampaign.target_project.store_ids != null)
                {
                    campaign.target_project.store_ids = new StoreIds
                    {
                        apple = coreCampaign.target_project.store_ids.apple,
                        google = coreCampaign.target_project.store_ids.google,
                        amazon = coreCampaign.target_project.store_ids.amazon,
                        microsoft = coreCampaign.target_project.store_ids.microsoft,
                        samsung = coreCampaign.target_project.store_ids.samsung
                    };
                }
                
                if (coreCampaign.target_project.platform_ids != null)
                {
                    campaign.target_project.platform_ids = new PlatformIds
                    {
                        ios_bundle_id = coreCampaign.target_project.platform_ids.ios_bundle_id,
                        android_package_name = coreCampaign.target_project.platform_ids.android_package_name
                    };
                }
                
                if (coreCampaign.target_project.creatives != null && coreCampaign.target_project.creatives.Length > 0)
                {
                    campaign.target_project.creatives = coreCampaign.target_project.creatives.Select(coreCreative =>
                    {
                        var creative = new Creative();
                        creative.format = coreCreative.format;
                        creative.creative_id = coreCreative.creative_id;
                        
                        if (coreCreative.variants != null && coreCreative.variants.Length > 0)
                        {
                            creative.variants = coreCreative.variants.Select(v => new CreativeVariant
                            {
                                url = v.url,
                                local_key = v.local_key,
                                resolution = v.resolution,
                                sha256 = v.sha256
                            }).ToArray();
                        }
                        
                        return creative;
                    }).ToArray();
                }
            }
            
            return campaign;
        }
        
        /// <summary>
        /// Legacy conversion method (kept for backward compatibility)
        /// </summary>
        private List<Campaign> ConvertToRuntimeCampaigns(List<BoostOps.Campaign> sharedCampaigns)
        {
            var runtimeCampaigns = new List<Campaign>();
            
            foreach (var sharedCampaign in sharedCampaigns)
            {
                try
                {
                    BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Converting shared campaign: {sharedCampaign.campaign_id}");
                    
                    // Create runtime campaign with data from shared model
                    var runtimeCampaign = new Campaign
                    {
                        campaign_id = sharedCampaign.campaign_id,
                        name = sharedCampaign.name,
                        status = sharedCampaign.status,
                        created_at = sharedCampaign.created_at,
                        updated_at = sharedCampaign.updated_at
                    };
                    
                    // Convert schedule if available
                    if (sharedCampaign.schedule != null)
                    {
                        runtimeCampaign.schedule = new CampaignSchedule
                        {
                            start_date = sharedCampaign.schedule.start_date,
                            end_date = sharedCampaign.schedule.end_date,
                            days = ConvertStringDaysToIntArray(sharedCampaign.schedule.days),
                            start_hour = sharedCampaign.schedule.start_hour,
                            end_hour = sharedCampaign.schedule.end_hour
                        };
                        
                        BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"  Schedule - Start: {runtimeCampaign.schedule.start_date}, Days: [{string.Join(",", runtimeCampaign.schedule.days)}]");
                    }
                    
                    // Convert target project
                    if (sharedCampaign.target_project != null)
                    {
                        runtimeCampaign.target_project = new TargetProject
                        {
                            project_id = sharedCampaign.target_project.project_id
                        };
                        
                        // Convert store URLs (using StoreLinks which is the runtime class name)
                        if (sharedCampaign.target_project.store_urls != null)
                        {
                            runtimeCampaign.target_project.store_urls = new StoreUrls
                            {
                                apple = sharedCampaign.target_project.store_urls.apple,
                                google = sharedCampaign.target_project.store_urls.google,
                                web = sharedCampaign.target_project.store_urls.web,
                                amazon = sharedCampaign.target_project.store_urls.amazon,
                                microsoft = sharedCampaign.target_project.store_urls.microsoft
                            };
                            
                            BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"  Store URLs - Apple: {runtimeCampaign.target_project.store_urls.apple}, Google: {runtimeCampaign.target_project.store_urls.google}");
                        }
                        
                        // Convert creatives if available (runtime uses Creative[] arrays, not Lists)
                        if (sharedCampaign.target_project.creatives != null && sharedCampaign.target_project.creatives.Count > 0)
                        {
                            var runtimeCreatives = new List<Creative>();
                            foreach (var sharedCreative in sharedCampaign.target_project.creatives)
                            {
                                var runtimeCreative = new Creative
                                {
                                    creative_id = sharedCreative.creative_id,
                                    format = sharedCreative.format,
                                    orientation = sharedCreative.orientation,
                                    prefetch = sharedCreative.prefetch,
                                    ttl_hours = sharedCreative.ttl_hours
                                    // Note: runtime Creative doesn't have 'required' or 'hosted_by' properties
                                };
                                
                                if (sharedCreative.variants != null && sharedCreative.variants.Count > 0)
                                {
                                    var runtimeVariants = new List<CreativeVariant>();
                                    foreach (var sharedVariant in sharedCreative.variants)
                                    {
                                        runtimeVariants.Add(new CreativeVariant
                                        {
                                            resolution = sharedVariant.resolution,
                                            url = sharedVariant.url,
                                            sha256 = sharedVariant.sha256,
                                            local_key = sharedVariant.local_key
                                        });
                                    }
                                    runtimeCreative.variants = runtimeVariants.ToArray(); // Convert List to array
                                }
                                
                                runtimeCreatives.Add(runtimeCreative);
                            }
                            
                            runtimeCampaign.target_project.creatives = runtimeCreatives.ToArray(); // Convert List to array
                            BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"  Converted {runtimeCreatives.Count} creatives");
                        }
                    }
                    
                    // Convert frequency cap
                    if (sharedCampaign.frequency_cap != null)
                    {
                        if (sharedCampaign.frequency_cap.time_unit?.ToUpper() == "UNLIMITED")
                        {
                            runtimeCampaign.frequency_cap = BoostOps.Core.FrequencyCap.Unlimited();
                        }
                        else
                        {
                            runtimeCampaign.frequency_cap = BoostOps.Core.FrequencyCap.Daily(sharedCampaign.frequency_cap.impressions);
                        }
                    }
                    
                    runtimeCampaigns.Add(runtimeCampaign);
                    BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Successfully converted campaign: {runtimeCampaign.campaign_id}");
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError("FirebaseRemoteConfig", $"Failed to convert campaign {sharedCampaign?.campaign_id}: {ex.Message}");
                }
            }
            
            return runtimeCampaigns;
        }
        
        /// <summary>
        /// Save runtime-retrieved config to EditorPrefs for editor window access
        /// </summary>
        private void SaveRuntimeConfigToEditorPrefs(string configKey, string configJson)
        {
            try
            {
#if UNITY_EDITOR
                // Save the config JSON and metadata to EditorPrefs
                string pk = $"BoostOps_{UnityEngine.Application.dataPath}_";
                UnityEditor.EditorPrefs.SetString(pk + "RuntimeConfig_JSON", configJson ?? "{}");
                UnityEditor.EditorPrefs.SetString(pk + "RuntimeConfig_Key", configKey ?? "");
                UnityEditor.EditorPrefs.SetString(pk + "RuntimeConfig_Timestamp", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                UnityEditor.EditorPrefs.SetString(pk + "RuntimeConfig_Provider", "Firebase Remote Config");
                
                BoostOpsLogger.LogDebug("FirebaseRemoteConfig", $"Saved runtime config to EditorPrefs for editor access");
#endif
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogWarning("FirebaseRemoteConfig", $"Failed to save runtime config to EditorPrefs: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Convert string days list to int array for runtime model
        /// </summary>
        private int[] ConvertStringDaysToIntArray(List<string> stringDays)
        {
            if (stringDays == null || stringDays.Count == 0)
                return new int[0];
                
            var intDays = new List<int>();
            foreach (string dayStr in stringDays)
            {
                if (int.TryParse(dayStr, out int dayInt))
                {
                    intDays.Add(dayInt);
                }
            }
            return intDays.ToArray();
        }
    }
#else
    /// <summary>
    /// Firebase Remote Config provider stub (when package is not installed)
    /// </summary>
    public class FirebaseRemoteConfigProvider : IRemoteConfigProvider
    {
        public string ProviderName => "Firebase Remote Config (Not Available)";
        public bool IsAvailable => false;

        public async Task<bool> InitializeAsync()
        {
            await Task.CompletedTask;
            BoostOpsLogger.LogWarning("FirebaseRemoteConfig", "Firebase Remote Config package not installed");
            return false;
        }

        public async Task<RemoteConfigResult> FetchAndLoadConfigAsync(string configKey)
        {
            await Task.CompletedTask;
            return RemoteConfigResult.CreateFailure("Firebase Remote Config package not installed");
        }
    }
#endif
} 