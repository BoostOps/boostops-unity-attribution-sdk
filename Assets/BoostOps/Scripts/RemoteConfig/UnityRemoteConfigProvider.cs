using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if UNITY_REMOTE_CONFIG
using Unity.Services.RemoteConfig;
#endif

namespace BoostOps
{
#if UNITY_REMOTE_CONFIG
    /// <summary>
    /// Unity Remote Config provider implementation
    /// </summary>
    public class UnityRemoteConfigProvider : IRemoteConfigProvider
    {
        public string ProviderName => "Unity Remote Config";

        public bool IsAvailable => true;

        public async Task<bool> InitializeAsync()
        {
            try
            {
                BoostOpsLogger.LogDebug("UnityRemoteConfig", "Unity Remote Config is available");
                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("UnityRemoteConfig", $"Failed to initialize Unity Remote Config: {ex.Message}");
                return false;
            }
        }

        public async Task<RemoteConfigResult> FetchAndLoadConfigAsync(string configKey)
        {
            try
            {
                var requestTask = RemoteConfigService.Instance.FetchConfigsAsync<object, object>(new object(), new object());
                await requestTask;
                
                BoostOpsLogger.LogDebug("UnityRemoteConfig", "Unity Remote Config fetched successfully");

                string configJson = RemoteConfigService.Instance.appConfig.GetJson(configKey, "{}");
                
                BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Config Key: {configKey}");
                BoostOpsLogger.LogDebug("UnityRemoteConfig", $"JSON Length: {configJson?.Length ?? 0} characters");
                
                UnityEngine.Debug.Log($"[BoostOps Runtime] Unity Remote Config Key: {configKey}");
                
                SaveRuntimeConfigToEditorPrefs(configKey, configJson);

                return ParseConfigurationJson(configJson);
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("UnityRemoteConfig", $"Failed to fetch Unity Remote Config: {ex.Message}");
                return RemoteConfigResult.CreateFailure($"Failed to fetch Unity Remote Config: {ex.Message}");
            }
        }

        private RemoteConfigResult ParseConfigurationJson(string configJson)
        {
            try
            {
                if (string.IsNullOrEmpty(configJson) || configJson == "{}")
                {
                    BoostOpsLogger.LogDebug("UnityRemoteConfig", "No campaigns found in Unity Remote Config");
                    return RemoteConfigResult.CreateSuccess("{}", new List<Campaign>(), new BoostOpsConfig());
                }

                BoostOpsLogger.LogDebug("UnityRemoteConfig", "Parsing JSON using shared RemoteCampaignConfig model (same as editor)");
                
                var sharedConfig = UnityEngine.JsonUtility.FromJson<BoostOps.Core.RemoteCampaignConfig>(configJson);
                
                if (sharedConfig?.campaigns != null && sharedConfig.campaigns.Count > 0)
                {
                    BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Successfully parsed {sharedConfig.campaigns.Count} campaigns from shared model");
                    
                    var runtimeCampaigns = sharedConfig.campaigns.Select(ConvertCoreToRuntimeCampaign).ToList();
                    
                    var config = BoostOpsConfig.ParseFromJson(configJson);
                    
                    BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Converted to {runtimeCampaigns.Count} runtime campaigns");
                    
                    int validCampaigns = 0;
                    foreach (var campaign in runtimeCampaigns)
                    {
                        if (CampaignParser.IsValidCampaign(campaign))
                        {
                            validCampaigns++;
                            BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Valid campaign: {campaign.name} (ID: {campaign.campaign_id})");
                        }
                        else
                        {
                            BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Invalid campaign: {campaign?.name ?? "Unknown"} - missing required data");
                        }
                    }
                    BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Campaign validation complete: {validCampaigns}/{runtimeCampaigns.Count} campaigns are valid");

                    return RemoteConfigResult.CreateSuccess(configJson, runtimeCampaigns, config);
                }
                else
                {
                    BoostOpsLogger.LogWarning("UnityRemoteConfig", "Shared model parsing succeeded but no campaigns found");
                    return RemoteConfigResult.CreateSuccess(configJson, new List<Campaign>(), new BoostOpsConfig());
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("UnityRemoteConfig", $"Failed to parse configuration using shared model: {ex.Message}");
                
                try
                {
                    BoostOpsLogger.LogDebug("UnityRemoteConfig", "Attempting fallback to original parser");
                    var campaigns = CampaignParser.ParseCampaignsFromJson(configJson);
                    var config = BoostOpsConfig.ParseFromJson(configJson);
                    return RemoteConfigResult.CreateSuccess(configJson, campaigns, config);
                }
                catch (Exception fallbackEx)
                {
                    BoostOpsLogger.LogError("UnityRemoteConfig", $"Fallback parser also failed: {fallbackEx.Message}");
                    return RemoteConfigResult.CreateFailure($"Both shared model and fallback parsers failed: {ex.Message}");
                }
            }
        }
        
        private List<Campaign> ConvertToRuntimeCampaigns(List<BoostOps.Campaign> sharedCampaigns)
        {
            var runtimeCampaigns = new List<Campaign>();
            
            foreach (var sharedCampaign in sharedCampaigns)
            {
                try
                {
                    BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Converting shared campaign: {sharedCampaign.campaign_id}");
                    
                    var runtimeCampaign = new Campaign
                    {
                        campaign_id = sharedCampaign.campaign_id,
                        name = sharedCampaign.name,
                        status = sharedCampaign.status,
                        created_at = sharedCampaign.created_at,
                        updated_at = sharedCampaign.updated_at
                    };
                    
                    if (sharedCampaign.schedule != null)
                    {
                        runtimeCampaign.schedule = new CampaignSchedule
                        {
                            start_date = sharedCampaign.schedule.start_date,
                            end_date = sharedCampaign.schedule.end_date,
                            days = sharedCampaign.schedule.days,
                            start_hour = sharedCampaign.schedule.start_hour,
                            end_hour = sharedCampaign.schedule.end_hour
                        };
                        
                        BoostOpsLogger.LogDebug("UnityRemoteConfig", $"  Schedule - Start: {runtimeCampaign.schedule.start_date}, Days: [{string.Join(",", runtimeCampaign.schedule.days)}]");
                    }
                    
                    if (sharedCampaign.target_project != null)
                    {
                        runtimeCampaign.target_project = new TargetProject
                        {
                            project_id = sharedCampaign.target_project.project_id
                        };
                        
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
                            
                            BoostOpsLogger.LogDebug("UnityRemoteConfig", $"  Store URLs - Apple: {runtimeCampaign.target_project.store_urls.apple}, Google: {runtimeCampaign.target_project.store_urls.google}");
                        }
                        
                        if (sharedCampaign.target_project.creatives != null && sharedCampaign.target_project.creatives.Length > 0)
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
                                };
                                
                                if (sharedCreative.variants != null && sharedCreative.variants.Length > 0)
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
                                    runtimeCreative.variants = runtimeVariants.ToArray();
                                }
                                
                                runtimeCreatives.Add(runtimeCreative);
                            }
                            
                            runtimeCampaign.target_project.creatives = runtimeCreatives.ToArray();
                            BoostOpsLogger.LogDebug("UnityRemoteConfig", $"  Converted {runtimeCreatives.Count} creatives");
                        }
                    }
                    
                    runtimeCampaigns.Add(runtimeCampaign);
                    BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Successfully converted campaign: {runtimeCampaign.campaign_id}");
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError("UnityRemoteConfig", $"Failed to convert campaign {sharedCampaign?.campaign_id}: {ex.Message}");
                }
            }
            
            return runtimeCampaigns;
        }
        
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
        
        private void SaveRuntimeConfigToEditorPrefs(string configKey, string configJson)
        {
            try
            {
#if UNITY_EDITOR
                string pk = $"BoostOps_{UnityEngine.Application.dataPath}_";
                UnityEditor.EditorPrefs.SetString(pk + "RuntimeConfig_JSON", configJson ?? "{}");
                UnityEditor.EditorPrefs.SetString(pk + "RuntimeConfig_Key", configKey ?? "");
                UnityEditor.EditorPrefs.SetString(pk + "RuntimeConfig_Timestamp", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                UnityEditor.EditorPrefs.SetString(pk + "RuntimeConfig_Provider", "Unity Remote Config");
                
                BoostOpsLogger.LogDebug("UnityRemoteConfig", $"Saved runtime config to EditorPrefs for editor access");
#endif
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogWarning("UnityRemoteConfig", $"Failed to save runtime config to EditorPrefs: {ex.Message}");
            }
        }

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
                            creative.variants = coreCreative.variants.Select(coreVariant => new CreativeVariant
                            {
                                url = coreVariant.url,
                                local_key = coreVariant.local_key,
                                resolution = coreVariant.resolution,
                                sha256 = coreVariant.sha256
                            }).ToArray();
                        }
                        
                        return creative;
                    }).ToArray();
                }
            }
            
            return campaign;
        }
    }
#else
    /// <summary>
    /// Unity Remote Config provider stub (when package is not installed)
    /// </summary>
    public class UnityRemoteConfigProvider : IRemoteConfigProvider
    {
        public string ProviderName => "Unity Remote Config (Not Available)";
        public bool IsAvailable => false;

        public async Task<bool> InitializeAsync()
        {
            await Task.CompletedTask;
            BoostOpsLogger.LogWarning("UnityRemoteConfig", "Unity Remote Config package not installed");
            return false;
        }

        public async Task<RemoteConfigResult> FetchAndLoadConfigAsync(string configKey)
        {
            await Task.CompletedTask;
            return RemoteConfigResult.CreateFailure("Unity Remote Config package not installed");
        }
    }
#endif
}
