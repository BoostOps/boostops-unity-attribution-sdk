using System.Collections.Generic;
using BoostOps.Internal;
using UnityEngine;
using System.Collections;

namespace BoostOps
{
    /// <summary>
    /// Firebase Analytics provider implementation
    /// Handles Firebase Analytics integration with conditional compilation
    /// Safe to include in projects without Firebase Analytics package
    /// </summary>
    public class FirebaseAnalyticsProvider : IAnalyticsProvider
    {
        public string ProviderName => "Firebase Analytics";
        
        private bool _isFirebaseInitialized = false;
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
                    if (settings?.FirebaseAnalytics != true)
                    {
                        _isAvailableCached = false;
                        return false;
                    }
                    
                    var firebaseAnalyticsType = System.Type.GetType("Firebase.Analytics.FirebaseAnalytics, Firebase.Analytics");
                    _isAvailableCached = firebaseAnalyticsType != null;
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
        /// Called when settings change.
        /// </summary>
        internal void InvalidateAvailabilityCache()
        {
            _isAvailableCached = null;
        }

        public void Initialize()
        {
            if (IsAvailable)
            {
                // BoostOpsLogger.LogDebug("Analytics", "Firebase Analytics provider initializing...");
                
                // Start checking Firebase initialization status
                CoroutineRunner.StartCoroutine(CheckFirebaseInitialization());
            }
            else
            {
                // BoostOpsLogger.LogDebug("Analytics", "Firebase Analytics not available (package not installed or disabled)");
            }
        }

        /// <summary>
        /// Check if Firebase is fully initialized and ready to receive events
        /// </summary>
        private IEnumerator CheckFirebaseInitialization()
        {
            // BoostOpsLogger.LogDebug("Analytics", "🔍 Starting Firebase Analytics initialization check...");
            
            // Wait a frame to ensure Firebase has time to initialize
            yield return null;
            
            bool firebaseReady = false;
            
            try
            {
                // Use reflection to check if Firebase is initialized
                var firebaseAppType = System.Type.GetType("Firebase.FirebaseApp, Firebase.App");
                if (firebaseAppType != null)
                {
                    // Check DefaultInstance property
                    var defaultInstanceProperty = firebaseAppType.GetProperty("DefaultInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (defaultInstanceProperty != null)
                    {
                        var defaultInstance = defaultInstanceProperty.GetValue(null);
                        if (defaultInstance != null)
                        {
                            // Firebase is initialized
                            firebaseReady = true;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Firebase initialization check failed: {ex.Message}");
            }
            
            if (firebaseReady)
            {
                _isFirebaseInitialized = true;
                // BoostOpsLogger.LogDebug("Analytics", "✅ Firebase Analytics ready on first check - processing cached events");
                ProcessCachedEvents();
                yield break;
            }
            
            // If we get here, Firebase might still be initializing
            // Retry after a delay
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                // Try again with more checks
                var firebaseAnalyticsType = System.Type.GetType("Firebase.Analytics.FirebaseAnalytics, Firebase.Analytics");
                if (firebaseAnalyticsType != null)
                {
                    // Firebase Analytics type exists, assume it's ready
                    _isFirebaseInitialized = true;
                    BoostOpsLogger.LogDebug("Analytics", "✅ Firebase Analytics assumed ready - processing cached events");
                    ProcessCachedEvents();
                }
                else
                {
                    BoostOpsLogger.LogWarning("Analytics", "⚠️ Firebase Analytics initialization timeout - some events may be lost");
                    // Assume ready to avoid blocking events indefinitely
                    _isFirebaseInitialized = true;
                    ProcessCachedEvents();
                }
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Firebase Analytics type check failed: {ex.Message}");
                
                // Assume ready to avoid blocking events indefinitely
                _isFirebaseInitialized = true;
                ProcessCachedEvents();
            }
        }

        /// <summary>
        /// Process cached events once Firebase is ready
        /// </summary>
        private void ProcessCachedEvents()
        {
            if (_cachedEvents.Count == 0) return;
            
            BoostOpsLogger.LogDebug("Analytics", $"Processing {_cachedEvents.Count} cached Firebase Analytics events");
            
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
                    BoostOpsLogger.LogError("Analytics", $"Failed to process cached event {cachedEvent.EventName}: {ex.Message}");
                }
            }
            
            _cachedEvents.Clear();
            BoostOpsLogger.LogDebug("Analytics", "✅ All cached Firebase Analytics events processed");
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
                    var go = new GameObject("FirebaseAnalyticsCoroutineRunner");
                    Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunnerBehaviour>();
                }
                _instance.StartCoroutine(routine);
            }
            
            private class CoroutineRunnerBehaviour : MonoBehaviour { }
        }

        public void TrackImpression(string eventName, Dictionary<string, string> parameters)
        {
            if (!IsAvailable)
            {
                BoostOpsLogger.LogDebug("Analytics", "Firebase Analytics not enabled for impression");
                return;
            }

            // Firebase specific logic for impressions: Send dual events (GA4 standard + BoostOps)
            if (eventName == BoostOpsAnalyticsContract.EventNames.IMPRESSION)
            {
                // Send GA4 ad_impression event
                TrackEvent("ad_impression", parameters);
                
                // Send original BoostOps event  
                TrackEvent(eventName, parameters);
            }
            else
            {
                TrackEvent(eventName, parameters);
            }
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
            if (!IsAvailable)
            {
                BoostOpsLogger.LogDebug("Analytics", "Firebase Analytics not enabled for purchase");
                return;
            }

            // Send only the custom BoostOps event (no duplicates)
            TrackEventWithMixedParameters(eventName, parameters);
        }

        /// <summary>
        /// Track event with mixed parameter types (for purchase events)
        /// </summary>
        private void TrackEventWithMixedParameters(string eventName, Dictionary<string, object> parameters)
        {
            if (!IsAvailable)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Firebase Analytics not enabled for event: {eventName}");
                return;
            }

            // Cache event if Firebase isn't ready yet
            if (!_isFirebaseInitialized)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Firebase not ready - caching mixed event: {eventName}");
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

            try
            {
                // Use reflection to call Firebase Analytics safely
                var firebaseAnalyticsType = System.Type.GetType("Firebase.Analytics.FirebaseAnalytics, Firebase.Analytics");
                if (firebaseAnalyticsType == null)
                {
                    BoostOpsLogger.LogError("Analytics", "Firebase Analytics type not found");
                    return;
                }

                // Get the Parameter type first
                var parameterType = System.Type.GetType("Firebase.Analytics.Parameter, Firebase.Analytics");
                if (parameterType == null)
                {
                    BoostOpsLogger.LogError("Analytics", "Firebase Analytics Parameter type not found");
                    return;
                }

                // Get the LogEvent method with Parameter[] signature
                var logEventMethod = firebaseAnalyticsType.GetMethod("LogEvent", 
                    new System.Type[] { typeof(string), parameterType.MakeArrayType() });
                
                if (logEventMethod == null)
                {
                    BoostOpsLogger.LogError("Analytics", "Firebase Analytics LogEvent(String, Parameter[]) method not found");
                    return;
                }

                // Convert parameters to Firebase Parameter array with proper types
                var paramArray = System.Array.CreateInstance(parameterType, parameters.Count);
                int index = 0;
                
                foreach (var param in parameters)
                {
                    object paramInstance = null;
                    
                    // Handle different parameter types for Firebase Analytics
                    if (param.Value is string stringValue)
                    {
                        var stringConstructor = parameterType.GetConstructor(new System.Type[] { typeof(string), typeof(string) });
                        paramInstance = stringConstructor?.Invoke(new object[] { param.Key, stringValue });
                    }
                    else if (param.Value is int intValue)
                    {
                        var intConstructor = parameterType.GetConstructor(new System.Type[] { typeof(string), typeof(int) });
                        paramInstance = intConstructor?.Invoke(new object[] { param.Key, intValue });
                    }
                    else if (param.Value is long longValue)
                    {
                        var longConstructor = parameterType.GetConstructor(new System.Type[] { typeof(string), typeof(long) });
                        paramInstance = longConstructor?.Invoke(new object[] { param.Key, longValue });
                    }
                    else if (param.Value is float floatValue)
                    {
                        var doubleConstructor = parameterType.GetConstructor(new System.Type[] { typeof(string), typeof(double) });
                        paramInstance = doubleConstructor?.Invoke(new object[] { param.Key, (double)floatValue });
                    }
                    else if (param.Value is double doubleValue)
                    {
                        var doubleConstructor = parameterType.GetConstructor(new System.Type[] { typeof(string), typeof(double) });
                        paramInstance = doubleConstructor?.Invoke(new object[] { param.Key, doubleValue });
                    }
                    else
                    {
                        // Fallback to string for other types
                        var stringConstructor = parameterType.GetConstructor(new System.Type[] { typeof(string), typeof(string) });
                        paramInstance = stringConstructor?.Invoke(new object[] { param.Key, param.Value?.ToString() ?? "" });
                    }
                    
                    if (paramInstance != null)
                    {
                        paramArray.SetValue(paramInstance, index++);
                    }
                }

                // Send event to Firebase Analytics using reflection
                logEventMethod.Invoke(null, new object[] { eventName, paramArray });
                // BoostOpsLogger.LogInfo("Analytics", $"🔥 Firebase Analytics -> {eventName} (mixed parameters)");
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Firebase Analytics error for {eventName}: {ex.Message}");
            }
        }

        public void TrackEvent(string eventName, Dictionary<string, string> parameters)
        {
            if (!IsAvailable)
            {
                BoostOpsLogger.LogDebug("Analytics", $"Firebase Analytics not enabled for event: {eventName}");
                return;
            }

            // Cache event if Firebase isn't ready yet
            if (!_isFirebaseInitialized)
            {
                BoostOpsLogger.LogInfo("Analytics", $"🔥 Firebase not ready - caching event: {eventName}");
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

            try
            {
                // Use reflection to call Firebase Analytics safely
                var firebaseAnalyticsType = System.Type.GetType("Firebase.Analytics.FirebaseAnalytics, Firebase.Analytics");
                if (firebaseAnalyticsType == null)
                {
                    BoostOpsLogger.LogError("Analytics", "Firebase Analytics type not found");
                    return;
                }

                // Get the Parameter type first
                var parameterType = System.Type.GetType("Firebase.Analytics.Parameter, Firebase.Analytics");
                if (parameterType == null)
                {
                    BoostOpsLogger.LogError("Analytics", "Firebase Analytics Parameter type not found");
                    return;
                }

                // Get the LogEvent method with Parameter[] signature
                var logEventMethod = firebaseAnalyticsType.GetMethod("LogEvent", 
                    new System.Type[] { typeof(string), parameterType.MakeArrayType() });
                
                if (logEventMethod == null)
                {
                    BoostOpsLogger.LogError("Analytics", "Firebase Analytics LogEvent(String, Parameter[]) method not found");
                    return;
                }

                // Get the Parameter constructor for string values
                var paramConstructor = parameterType.GetConstructor(new System.Type[] { typeof(string), typeof(string) });
                
                if (paramConstructor == null)
                {
                    BoostOpsLogger.LogError("Analytics", "Firebase Analytics Parameter(String, String) constructor not found");
                    return;
                }

                // Convert parameters to Firebase Parameter array
                var paramArray = System.Array.CreateInstance(parameterType, parameters.Count);
                int index = 0;
                foreach (var param in parameters)
                {
                    var paramInstance = paramConstructor.Invoke(new object[] { param.Key, param.Value });
                    paramArray.SetValue(paramInstance, index++);
                }

                // Send event to Firebase Analytics using reflection
                logEventMethod.Invoke(null, new object[] { eventName, paramArray });
                // BoostOpsLogger.LogInfo("Analytics", $"🔥 Firebase Analytics -> {eventName}");
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Firebase Analytics error for {eventName}: {ex.Message}");
            }
        }
    }
} 