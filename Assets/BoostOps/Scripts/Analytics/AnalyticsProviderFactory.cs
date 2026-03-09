using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Factory class for creating and managing analytics providers
    /// Handles initialization and provides access to all available analytics providers
    /// </summary>
    public static class AnalyticsProviderFactory
    {
        private static List<IAnalyticsProvider> _providers;
        private static List<IAnalyticsProvider> _availableProviders;
        private static bool _initialized = false;

        /// <summary>
        /// Get all initialized analytics providers
        /// </summary>
        public static List<IAnalyticsProvider> GetProviders()
        {
            if (!_initialized)
            {
                InitializeProviders();
            }
            
            return _providers ?? new List<IAnalyticsProvider>();
        }

        /// <summary>
        /// Get all available (enabled) analytics providers.
        /// Returns a cached list that is rebuilt only during initialization or
        /// when RefreshAvailableProviders() is called.
        /// </summary>
        public static List<IAnalyticsProvider> GetAvailableProviders()
        {
            if (!_initialized)
            {
                InitializeProviders();
            }
            
            return _availableProviders ?? new List<IAnalyticsProvider>();
        }
        
        /// <summary>
        /// Re-evaluates which providers are available and rebuilds the cached list.
        /// Call after settings change or consent flow completes.
        /// </summary>
        public static void RefreshAvailableProviders()
        {
            if (_providers == null) return;
            
            foreach (var provider in _providers)
            {
                if (provider is UnityAnalyticsProvider uap)
                    uap.InvalidateAvailabilityCache();
                else if (provider is FirebaseAnalyticsProvider fap)
                    fap.InvalidateAvailabilityCache();
            }
            
            _availableProviders = _providers.Where(p => p.IsAvailable).ToList();
        }

        /// <summary>
        /// Get a specific provider by type
        /// </summary>
        public static T GetProvider<T>() where T : class, IAnalyticsProvider
        {
            return GetProviders().OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Get a provider by name
        /// </summary>
        public static IAnalyticsProvider GetProvider(string providerName)
        {
            return GetProviders().FirstOrDefault(p => p.ProviderName == providerName);
        }

        /// <summary>
        /// Initialize all analytics providers
        /// </summary>
        private static void InitializeProviders()
        {
            _providers = new List<IAnalyticsProvider>();

            // Create all provider instances
            _providers.Add(new BoostOpsAnalyticsProvider());    // Always available
            _providers.Add(new FirebaseAnalyticsProvider());    // Conditional on Firebase package + settings
            _providers.Add(new UnityAnalyticsProvider());       // Conditional on Unity Analytics package + settings

            // Initialize each provider
            foreach (var provider in _providers)
            {
                try
                {
                    provider.Initialize();
                    // BoostOpsLogger.LogDebug("Analytics", $"Initialized provider: {provider.ProviderName} (Available: {provider.IsAvailable})");
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError("Analytics", $"Failed to initialize provider {provider.ProviderName}: {ex.Message}");
                }
            }

            _availableProviders = _providers.Where(p => p.IsAvailable).ToList();
            _initialized = true;
        }

        /// <summary>
        /// Reset providers (useful for testing or reinitialization)
        /// </summary>
        public static void Reset()
        {
            _providers?.Clear();
            _providers = null;
            _availableProviders?.Clear();
            _availableProviders = null;
            _initialized = false;
        }

        /// <summary>
        /// Send event to all available providers
        /// </summary>
        public static void SendToAllProviders(System.Action<IAnalyticsProvider> action)
        {
            var availableProviders = GetAvailableProviders();
            
            foreach (var provider in availableProviders)
            {
                try
                {
                    action(provider);
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError("Analytics", $"Error in provider {provider.ProviderName}: {ex.Message}");
                }
            }
        }
    }
} 