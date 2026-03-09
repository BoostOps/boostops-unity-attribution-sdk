using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoostOps
{
    /// <summary>
    /// Interface for remote config providers (Firebase, Unity Remote Config, Local)
    /// </summary>
    public interface IRemoteConfigProvider
    {
        /// <summary>
        /// Initialize the remote config provider
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Fetch and load configuration from the remote provider
        /// </summary>
        /// <param name="configKey">The key to fetch configuration data</param>
        /// <returns>Result containing campaigns and configuration data</returns>
        Task<RemoteConfigResult> FetchAndLoadConfigAsync(string configKey);

        /// <summary>
        /// Get the provider name for logging purposes
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Check if the provider is available/supported in the current environment
        /// </summary>
        bool IsAvailable { get; }
    }

    /// <summary>
    /// Result returned by remote config operations
    /// </summary>
    public class RemoteConfigResult
    {
        public bool Success { get; set; }
        public string ConfigJson { get; set; }
        public string ErrorMessage { get; set; }
        public List<Campaign> Campaigns { get; set; }
        public BoostOpsConfig Config { get; set; }

        public RemoteConfigResult()
        {
            Success = false;
            ConfigJson = "{}";
            Campaigns = new List<Campaign>();
            Config = new BoostOpsConfig();
        }

        public static RemoteConfigResult CreateSuccess(string configJson, List<Campaign> campaigns, BoostOpsConfig config)
        {
            return new RemoteConfigResult
            {
                Success = true,
                ConfigJson = configJson,
                Campaigns = campaigns,
                Config = config
            };
        }

        public static RemoteConfigResult CreateFailure(string errorMessage)
        {
            return new RemoteConfigResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ConfigJson = "{}",
                Campaigns = new List<Campaign>(),
                Config = new BoostOpsConfig()
            };
        }
    }
} 