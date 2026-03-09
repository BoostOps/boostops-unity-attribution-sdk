using System.Collections.Generic;
using BoostOps.Internal;
using UnityEngine;
using BoostOps.Analytics;
using System.Collections;

namespace BoostOps
{
    /// <summary>
    /// Unity Analytics provider implementation
    /// Handles Unity Analytics integration with conditional compilation
    /// Safe to include in projects without Unity Analytics package
    /// </summary>
    public class UnityAnalyticsProvider : IAnalyticsProvider
    {
        public string ProviderName => "Unity Analytics";
        
        private bool _isUnityAnalyticsInitialized = false;
        private bool? _isAvailableCached = null;
        private List<CachedEvent> _cachedEvents = new List<CachedEvent>();
        
        private struct CachedEvent
        {
            public string EventName;
            public Dictionary<string, string> StringParameters;
            public Dictionary<string, object> MixedParameters;
            public bool IsMixedType;
        }

        public bool IsAvailable 
        { 
            get 
            {
                if (_isAvailableCached.HasValue)
                    return _isAvailableCached.Value;
                
                try
                {
                    var settings = InternalSettingsCache.GetProjectSettings();
                    if (settings?.UnityAnalytics != true)
                    {
                        _isAvailableCached = false;
                        return false;
                    }
                    
                    var newAnalyticsType = System.Type.GetType("Unity.Services.Analytics.AnalyticsService, Unity.Services.Analytics");
                    var oldAnalyticsType = System.Type.GetType("UnityEngine.Analytics.Analytics, UnityEngine.UnityAnalyticsModule");
                    
                    _isAvailableCached = newAnalyticsType != null || oldAnalyticsType != null;
                    return _isAvailableCached.Value;
                }
                catch
                {
                    _isAvailableCached = false;
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Invalidates the cached availability so the next access re-evaluates.
        /// Called when settings change or after consent flow completes.
        /// </summary>
        internal void InvalidateAvailabilityCache()
        {
            _isAvailableCached = null;
        }

        public void Initialize()
        {
            if (IsAvailable)
            {
                // BoostOpsLogger.LogDebug("Analytics", "Unity Analytics provider initializing...");
                
                // Start checking Unity Analytics initialization status
                CoroutineRunner.StartCoroutine(CheckUnityAnalyticsInitialization());
            }
            else
            {
                // BoostOpsLogger.LogDebug("Analytics", "Unity Analytics not available (package not installed or disabled)");
            }
        }

        /// <summary>
        /// Check if Unity Analytics is fully initialized and ready to receive events
        /// </summary>
        private IEnumerator CheckUnityAnalyticsInitialization()
        {
            // BoostOpsLogger.LogDebug("Analytics", "🔍 Starting Unity Analytics initialization check...");
            
            // Wait a frame to ensure Unity Analytics has time to initialize
            yield return null;
            
            bool unityAnalyticsReady = false;
            
            try
            {
                // Use UnityAnalyticsGuard to check if Unity Analytics is ready
                if (UnityAnalyticsGuard.EnsureReady())
                {
                    unityAnalyticsReady = true;
                }
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics first check: services not ready ({ex.InnerException?.Message ?? ex.Message})");
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics first check failed: {ex.Message}");
            }
            
            if (unityAnalyticsReady)
            {
                _isUnityAnalyticsInitialized = true;
                BoostOpsLogger.LogDebug("Analytics", "✅ Unity Analytics ready - processing cached events");
                ProcessCachedEvents();
                yield break;
            }
            
            // If not ready, wait and try again with longer delay for Unity Services initialization
            yield return new WaitForSeconds(2.0f); // Increased from 0.5f to give Unity Services more time
            
            // Second attempt - try UnityAnalyticsGuard again
            try
            {
                if (UnityAnalyticsGuard.EnsureReady())
                {
                    _isUnityAnalyticsInitialized = true;
                    BoostOpsLogger.LogDebug("Analytics", "✅ Unity Analytics ready on second check - processing cached events");
                    ProcessCachedEvents();
                    yield break;
                }
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics second check failed: {ex.Message}");
            }
            
            // Third attempt after more delay
            yield return new WaitForSeconds(3.0f);
            
            bool isReady = false;
            try
            {
                isReady = UnityAnalyticsGuard.EnsureReady();
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics third check: services not ready ({ex.InnerException?.Message ?? ex.Message})");
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics third check failed: {ex.Message}");
            }
            
            if (isReady)
            {
                _isUnityAnalyticsInitialized = true;
                // BoostOpsLogger.LogDebug("Analytics", "✅ Unity Analytics ready via reflection - processing cached events");
                ProcessCachedEvents();
            }
            else
            {
                BoostOpsLogger.LogDebug("Analytics", "Unity Analytics reflection checks failed - assuming ready to avoid event loss");
                // After multiple attempts with delays, assume Unity Analytics will be ready soon
                // Better to send events than lose them due to initialization race conditions
                _isUnityAnalyticsInitialized = true;
                ProcessCachedEvents();
                
                // Start a background check to see when Unity Analytics actually becomes ready
                CoroutineRunner.StartCoroutine(LogWhenUnityAnalyticsReady());
            }
        }

        /// <summary>
        /// Background coroutine to log when Unity Analytics actually becomes ready (for debugging race conditions)
        /// </summary>
        private IEnumerator LogWhenUnityAnalyticsReady()
        {
            for (int i = 0; i < 20; i++) // Check for up to 20 seconds
            {
                yield return new WaitForSeconds(1.0f);
                
                try
                {
                    if (UnityAnalyticsGuard.EnsureReady())
                    {
                        BoostOpsLogger.LogDebug("Analytics", $"🎯 Unity Analytics actually ready after {i + 1} seconds");
                        yield break;
                    }
                }
                catch (System.Exception ex)
                {
                    // Ignore exceptions during background checks
                    if (i % 5 == 0) // Log every 5 seconds
                    {
                        BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics still not ready after {i + 1}s: {ex.Message}");
                    }
                }
            }
            
            BoostOpsLogger.LogDebug("Analytics", "Unity Analytics readiness check timeout after 20 seconds");
        }

        /// <summary>
        /// Process cached events once Unity Analytics is ready
        /// </summary>
        private void ProcessCachedEvents()
        {
            if (_cachedEvents.Count == 0) return;
            
            // BoostOpsLogger.LogDebug("Analytics", $"Processing {_cachedEvents.Count} cached Unity Analytics events");
            
            foreach (var cachedEvent in _cachedEvents)
            {
                try
                {
                    if (cachedEvent.IsMixedType)
                    {
                        TrackEventWithMixedParametersInternal(cachedEvent.EventName, cachedEvent.MixedParameters);
                    }
                    else
                    {
                        TrackEventInternal(cachedEvent.EventName, cachedEvent.StringParameters);
                    }
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError("Analytics", $"Failed to process cached Unity Analytics event {cachedEvent.EventName}: {ex.Message}");
                }
            }
            
            _cachedEvents.Clear();
            // BoostOpsLogger.LogDebug("Analytics", "✅ All cached Unity Analytics events processed");
        }

        /// <summary>
        /// Simple coroutine runner for providers that don't inherit from MonoBehaviour
        /// </summary>
        private static class CoroutineRunner
        {
            private static CoroutineRunnerBehaviour _instance;
            
            public static void StartCoroutine(IEnumerator routine)
            {
                if (_instance == null)
                {
                    var go = new GameObject("UnityAnalyticsCoroutineRunner");
                    Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunnerBehaviour>();
                }
                _instance.StartCoroutine(routine);
            }
            
            private class CoroutineRunnerBehaviour : MonoBehaviour { }
        }

        public void TrackImpression(string eventName, Dictionary<string, string> parameters)
        {
            TrackEvent(eventName, parameters);
        }

        public void TrackClick(string eventName, Dictionary<string, string> parameters)
        {
            TrackEvent(eventName, parameters);
        }

        public void TrackInstall(string eventName, Dictionary<string, string> parameters)
        {
            TrackEvent(eventName, parameters);
        }

        public void TrackPurchase(string eventName, Dictionary<string, object> parameters)
        {
            BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics TrackPurchase called: {eventName} with {parameters?.Count ?? 0} parameters");
            TrackEventWithMixedParameters(eventName, parameters);
        }

        /// <summary>
        /// Track event with mixed parameter types (for purchase events)
        /// </summary>
        private void TrackEventWithMixedParameters(string eventName, Dictionary<string, object> parameters)
        {
            if (!IsAvailable)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics not enabled for event: {eventName}");
                return;
            }

            // Cache event if Unity Analytics isn't ready yet
            if (!_isUnityAnalyticsInitialized)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics not ready - caching mixed event: {eventName}");
                _cachedEvents.Add(new CachedEvent
                {
                    EventName = eventName,
                    MixedParameters = parameters,
                    IsMixedType = true
                });
                return;
            }

            TrackEventWithMixedParametersInternal(eventName, parameters);
        }

        private void TrackEventWithMixedParametersInternal(string eventName, Dictionary<string, object> parameters)
        {
            // Check if Unity Analytics is properly initialized and ready to receive events
            if (!UnityAnalyticsGuard.EnsureReady())
            {
                // BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics not ready - skipping event: {eventName}");
                return;
            }

            try
            {
                // Try the newer Unity Services Analytics API first
                var newAnalyticsType = System.Type.GetType("Unity.Services.Analytics.AnalyticsService, Unity.Services.Analytics");
                if (newAnalyticsType != null)
                {
                    var instanceProperty = newAnalyticsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        try
                        {
                            var instance = instanceProperty.GetValue(null);
                            if (instance != null)
                            {
                                var instanceType = instance.GetType();
                                var customDataMethod = instanceType.GetMethod("CustomData", new System.Type[] { typeof(string), typeof(Dictionary<string, object>) });
                                if (customDataMethod != null)
                                {
                                    // Parameters are already in the correct format for Unity Analytics
                                    customDataMethod.Invoke(instance, new object[] { eventName, parameters });
                                    BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics (New API) -> {eventName} (mixed parameters) SUCCESS");
                                    return;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            BoostOpsLogger.LogError("Analytics", $"Unity Analytics (New API) failed for {eventName}: {ex.Message}");
                            // Fall through to old API
                        }
                    }
                }

                // Fall back to older Unity Analytics API
                var oldAnalyticsType = System.Type.GetType("UnityEngine.Analytics.Analytics, UnityEngine.UnityAnalyticsModule");
                if (oldAnalyticsType != null)
                {
                    // Look for CustomEvent method with IDictionary parameter
                    var customEventMethod = oldAnalyticsType.GetMethod("CustomEvent", new System.Type[] { typeof(string), typeof(System.Collections.IDictionary) });
                    if (customEventMethod != null)
                    {
                        // Convert parameters to IDictionary (parameters already have correct types)
                        var eventData = new Dictionary<string, object>(parameters);

                        // Send event using static method
                        var result = customEventMethod.Invoke(null, new object[] { eventName, eventData });
                        BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics (Legacy API) -> {eventName} (mixed parameters) [Result: {result}]");
                        return;
                    }
                }

                BoostOpsLogger.LogError("Analytics", "No compatible Unity Analytics API found");
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Unity Analytics error for {eventName}: {ex.Message}");
            }
        }

        public void TrackEvent(string eventName, Dictionary<string, string> parameters)
        {
            if (!IsAvailable)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics not enabled for event: {eventName}");
                return;
            }

            // Cache event if Unity Analytics isn't ready yet
            if (!_isUnityAnalyticsInitialized)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics not ready - caching event: {eventName}");
                _cachedEvents.Add(new CachedEvent
                {
                    EventName = eventName,
                    StringParameters = parameters,
                    IsMixedType = false
                });
                return;
            }

            TrackEventInternal(eventName, parameters);
        }

        private void TrackEventInternal(string eventName, Dictionary<string, string> parameters)
        {
            // Check if Unity Analytics is properly initialized and ready to receive events
            if (!UnityAnalyticsGuard.EnsureReady())
            {
                // BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics not ready - skipping event: {eventName}");
                return;
            }

            try
            {
                // Try the newer Unity Services Analytics API first
                var newAnalyticsType = System.Type.GetType("Unity.Services.Analytics.AnalyticsService, Unity.Services.Analytics");
                if (newAnalyticsType != null)
                {
                    var instanceProperty = newAnalyticsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        try
                        {
                            var instance = instanceProperty.GetValue(null);
                            if (instance != null)
                            {
                                var instanceType = instance.GetType();
                                var customDataMethod = instanceType.GetMethod("CustomData", new System.Type[] { typeof(string), typeof(Dictionary<string, object>) });
                                if (customDataMethod != null)
                                {
                                    var unityParams = new Dictionary<string, object>();
                                    foreach (var param in parameters)
                                    {
                                        unityParams[param.Key] = param.Value;
                                    }
                                    
                                    customDataMethod.Invoke(instance, new object[] { eventName, unityParams });
                                    BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics (New API) -> {eventName}");
                                    return;
                                }
                            }
                        }
                        catch
                        {
                            // Fall through to old API
                        }
                    }
                }

                // Fall back to older Unity Analytics API
                var oldAnalyticsType = System.Type.GetType("UnityEngine.Analytics.Analytics, UnityEngine.UnityAnalyticsModule");
                if (oldAnalyticsType != null)
                {
                    // Look for CustomEvent method with IDictionary parameter
                    var customEventMethod = oldAnalyticsType.GetMethod("CustomEvent", new System.Type[] { typeof(string), typeof(System.Collections.IDictionary) });
                    if (customEventMethod != null)
                    {
                        // Convert parameters to IDictionary
                        var eventData = new Dictionary<string, object>();
                        foreach (var param in parameters)
                        {
                            eventData[param.Key] = param.Value;
                        }

                        // Send event using static method
                        var result = customEventMethod.Invoke(null, new object[] { eventName, eventData });
                        BoostOpsLogger.LogDebug("Analytics", $"Unity Analytics (Legacy API) -> {eventName} [Result: {result}]");
                        return;
                    }
                }

                BoostOpsLogger.LogError("Analytics", "No compatible Unity Analytics API found");
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Unity Analytics error for {eventName}: {ex.Message}");
            }
        }
    }
} 