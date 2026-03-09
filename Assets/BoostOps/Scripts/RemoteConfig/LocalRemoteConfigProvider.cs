using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Local fallback remote config provider - loads from local files
    /// </summary>
    public class LocalRemoteConfigProvider : IRemoteConfigProvider
    {
        public string ProviderName => "Local Config (Fallback)";

        public bool IsAvailable => true; // Local provider is always available

        public async Task<bool> InitializeAsync()
        {
            // Local provider doesn't need initialization
            BoostOpsLogger.LogDebug("LocalRemoteConfig", "Local Remote Config provider is ready");
            await Task.CompletedTask;
            return true;
        }

        public Task<RemoteConfigResult> FetchAndLoadConfigAsync(string configKey)
        {
            try
            {
                BoostOpsLogger.LogDebug("LocalRemoteConfig", "Loading campaigns from local sources (Resources)");
                
                // Try to load from local sources using synchronous Resources loading
                var (campaigns, config) = LoadLocalCampaigns();
                
                if (campaigns.Count > 0)
                {
                    BoostOpsLogger.LogDebug("LocalRemoteConfig", $"Loaded {campaigns.Count} campaigns from local sources");
                    
                    // Create a synthetic JSON representation for consistency
                    string configJson = CreateConfigJson(campaigns, config);
                    
                    return Task.FromResult(RemoteConfigResult.CreateSuccess(configJson, campaigns, config));
                }
                else
                {
                    BoostOpsLogger.LogWarning("LocalRemoteConfig", "No campaigns found in local sources");
                    return Task.FromResult(RemoteConfigResult.CreateSuccess("{}", new List<Campaign>(), new BoostOpsConfig()));
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("LocalRemoteConfig", $"Failed to load local campaigns: {ex.Message}");
                return Task.FromResult(RemoteConfigResult.CreateFailure($"Failed to load local campaigns: {ex.Message}"));
            }
        }

        private (List<Campaign> campaigns, BoostOpsConfig config) LoadLocalCampaigns()
        {
            var campaigns = new List<Campaign>();
            var config = new BoostOpsConfig();
            
            try
            {
                // Load synchronously from Resources
                campaigns = LoadFromResources();
                
                // Also parse the config from the same JSON file if we have campaigns
                if (campaigns.Count > 0)
                {
                    try
                    {
                        // Load config from Resources
                        var textAsset = Resources.Load<TextAsset>("BoostOps/cross_promo_local");
                        if (textAsset != null && !string.IsNullOrEmpty(textAsset.text))
                        {
                            config = BoostOpsConfig.ParseFromJson(textAsset.text, CampaignParsingMode.LocalOnly);
                        }
                    }
                    catch (Exception ex)
                    {
                        BoostOpsLogger.LogError("LocalRemoteConfig", $"Failed to parse config: {ex.Message}");
                        config = new BoostOpsConfig();
                    }
                }
                else
                {
                    // Set default config values for local mode
                    config = new BoostOpsConfig();
                }
                
                return (campaigns, config);
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("LocalRemoteConfig", $"Error loading local campaigns: {ex.Message}");
                return (new List<Campaign>(), new BoostOpsConfig());
            }
        }

        private List<Campaign> LoadFromResources()
        {
            try
            {
                // Use synchronous Resources loading - works on all platforms
                var campaigns = CampaignParser.LoadCampaignsFromResources("BoostOps/cross_promo_local");
                
                if (campaigns != null && campaigns.Count > 0)
                {
                    BoostOpsLogger.LogDebug("LocalRemoteConfig", $"Found {campaigns.Count} campaigns in Resources");
                    return campaigns;
                }
                
                BoostOpsLogger.LogDebug("LocalRemoteConfig", "No campaigns found in Resources/BoostOps/cross_promo_local.json");
                return new List<Campaign>();
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("LocalRemoteConfig", $"Failed to load from Resources: {ex.Message}");
                return new List<Campaign>();
            }
        }



        private string CreateConfigJson(List<Campaign> campaigns, BoostOpsConfig config)
        {
            try
            {
                // Create a simple JSON structure that matches what the remote providers would return
                var configData = new
                {
                    campaigns = campaigns,
                    config = config,
                    source = "local"
                };
                
                return UnityEngine.JsonUtility.ToJson(configData, true);
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("LocalRemoteConfig", $"Failed to create config JSON: {ex.Message}");
                return "{}";
            }
        }
    }
} 