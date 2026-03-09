using System;

namespace BoostOps
{
    /// <summary>
    /// Factory for creating and managing remote config providers
    /// </summary>
    public static class RemoteConfigProviderFactory
    {
        /// <summary>
        /// Create the appropriate remote config provider based on available packages
        /// Priority: Firebase Remote Config > Unity Remote Config > Local Fallback
        /// </summary>
        /// <returns>The best available remote config provider</returns>
        public static IRemoteConfigProvider CreateProvider()
        {
            // Check Firebase Remote Config first (highest priority)
            var firebaseProvider = new FirebaseRemoteConfigProvider();
            if (firebaseProvider.IsAvailable)
            {
                BoostOpsLogger.LogDebug("RemoteConfigFactory", $"Using {firebaseProvider.ProviderName}");
                return firebaseProvider;
            }

            // Check Unity Remote Config second
            var unityProvider = new UnityRemoteConfigProvider();
            if (unityProvider.IsAvailable)
            {
                BoostOpsLogger.LogDebug("RemoteConfigFactory", $"Using {unityProvider.ProviderName}");
                return unityProvider;
            }

            // Fallback to local provider
            var localProvider = new LocalRemoteConfigProvider();
            BoostOpsLogger.LogDebug("RemoteConfigFactory", $"Using {localProvider.ProviderName} - no remote config packages available");
            return localProvider;
        }

        /// <summary>
        /// Create a specific provider type (for testing or explicit usage)
        /// </summary>
        /// <typeparam name="T">Type of provider to create</typeparam>
        /// <returns>Instance of the specified provider type</returns>
        public static T CreateProvider<T>() where T : IRemoteConfigProvider, new()
        {
            var provider = new T();
            BoostOpsLogger.LogDebug("RemoteConfigFactory", $"Created specific provider: {provider.ProviderName}");
            return provider;
        }

        /// <summary>
        /// Get information about all available providers
        /// </summary>
        /// <returns>String describing available providers</returns>
        public static string GetAvailableProvidersInfo()
        {
            var firebase = new FirebaseRemoteConfigProvider();
            var unity = new UnityRemoteConfigProvider();
            var local = new LocalRemoteConfigProvider();

            return $"Remote Config Providers - " +
                   $"Firebase: {(firebase.IsAvailable ? "Available" : "Not Available")}, " +
                   $"Unity: {(unity.IsAvailable ? "Available" : "Not Available")}, " +
                   $"Local: {(local.IsAvailable ? "Available" : "Not Available")}";
        }

        /// <summary>
        /// Force use of local provider (useful for testing or local-only mode)
        /// </summary>
        /// <returns>Local remote config provider</returns>
        public static IRemoteConfigProvider CreateLocalProvider()
        {
            var provider = new LocalRemoteConfigProvider();
            BoostOpsLogger.LogDebug("RemoteConfigFactory", $"Forced local provider: {provider.ProviderName}");
            return provider;
        }
    }
} 