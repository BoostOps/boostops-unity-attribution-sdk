using System.Collections.Generic;
using UnityEngine;
using BoostOps.Attribution;

namespace BoostOps
{
    /// <summary>
    /// Public API for SKAdNetwork (SKAN) conversion value management
    /// Automatically integrated with BoostOps Analytics - manual calls usually not needed
    /// </summary>
    public static class BoostOpsSKAN
    {
        /// <summary>
        /// Get SKAN version supported on this device (0 = unavailable, 2-4 = version)
        /// </summary>
        public static int GetSKANVersion()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            return BoostOpsSKANManager.Instance?.SKANVersion ?? 0;
            #else
            return 0;
            #endif
        }
        
        /// <summary>
        /// Check if SKAN is available on this device
        /// </summary>
        public static bool IsSKANAvailable()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            return BoostOpsSKANManager.Instance?.IsSKANAvailable ?? false;
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// Get current conversion value (0-63), or -1 if not set
        /// </summary>
        public static int GetCurrentConversionValue()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            return BoostOpsSKANManager.Instance?.CurrentConversionValue ?? -1;
            #else
            return -1;
            #endif
        }
        
        /// <summary>
        /// Manually update SKAN conversion value for a custom event
        /// Note: BoostOps Analytics automatically handles common events (purchases, app opens, etc.)
        /// </summary>
        /// <param name="eventType">Event type matching your schema (e.g., "level_complete", "tutorial_complete")</param>
        /// <param name="eventData">Optional event data for schema rule matching (e.g., level number, amount)</param>
        public static void UpdateConversionValueForEvent(string eventType, Dictionary<string, object> eventData = null)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            if (BoostOpsSKANManager.Instance != null)
            {
                BoostOpsSKANManager.Instance.UpdateConversionValueForEvent(eventType, eventData);
            }
            else
            {
                Debug.LogWarning("[BoostOps SKAN] SKAN Manager not initialized");
            }
            #endif
        }
        
        /// <summary>
        /// Manually set conversion value (0-63) without using the schema
        /// Use UpdateConversionValueForEvent() instead for schema-based management
        /// </summary>
        /// <param name="conversionValue">Value between 0-63</param>
        public static void SetConversionValue(int conversionValue)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            if (BoostOpsSKANManager.Instance != null)
            {
                BoostOpsSKANManager.Instance.UpdateConversionValue(conversionValue);
            }
            else
            {
                Debug.LogWarning("[BoostOps SKAN] SKAN Manager not initialized");
            }
            #endif
        }
        
        /// <summary>
        /// Set SKAN mapping (matches server format)
        /// Can be loaded from server config or defined in code
        /// </summary>
        /// <param name="mapping">SKAN mapping configuration</param>
        public static void SetMapping(BoostOpsSKANMapping mapping)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            if (BoostOpsSKANManager.Instance != null)
            {
                BoostOpsSKANManager.Instance.SetMapping(mapping);
            }
            else
            {
                Debug.LogWarning("[BoostOps SKAN] SKAN Manager not initialized");
            }
            #endif
        }
        
        /// <summary>
        /// Load SKAN mapping from JSON (matches server format)
        /// Server provides full mapping in config.skan.mapping
        /// </summary>
        /// <param name="json">JSON mapping definition</param>
        public static void LoadMappingFromJson(string json)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            if (BoostOpsSKANManager.Instance != null)
            {
                BoostOpsSKANManager.Instance.LoadMappingFromJson(json);
            }
            else
            {
                Debug.LogWarning("[BoostOps SKAN] SKAN Manager not initialized");
            }
            #endif
        }
        
        // === OBSOLETE METHODS (Old Schema Format) ===
        
        /// <summary>
        /// [OBSOLETE] Use SetMapping() instead
        /// </summary>
        [System.Obsolete("Use SetMapping(BoostOpsSKANMapping) instead")]
        public static void SetConversionSchema(BoostOpsSKANConversionSchema schema)
        {
            Debug.LogWarning("[BoostOps SKAN] SetConversionSchema is obsolete. Use SetMapping() with server format instead.");
        }
        
        /// <summary>
        /// [OBSOLETE] Use LoadMappingFromJson() instead
        /// </summary>
        [System.Obsolete("Use LoadMappingFromJson(string) instead")]
        public static void LoadSchemaFromJson(string json)
        {
            Debug.LogWarning("[BoostOps SKAN] LoadSchemaFromJson is obsolete. Use LoadMappingFromJson() instead.");
        }
        
        /// <summary>
        /// [OBSOLETE] Use LoadMappingFromJson() instead
        /// </summary>
        [System.Obsolete("Use LoadMappingFromJson() instead")]
        public static void LoadSchemaFromRemoteConfig(BoostOpsConfig config)
        {
            Debug.LogWarning("[BoostOps SKAN] LoadSchemaFromRemoteConfig is obsolete. Use LoadMappingFromJson() instead.");
        }
        
        /// <summary>
        /// Subscribe to conversion value update events
        /// </summary>
        /// <param name="callback">Callback receives (conversionValue, coarseValue)</param>
        public static void OnConversionValueUpdated(System.Action<int, string> callback)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            if (BoostOpsSKANManager.Instance != null)
            {
                BoostOpsSKANManager.Instance.OnConversionValueUpdated += callback;
            }
            #endif
        }
        
        /// <summary>
        /// Subscribe to conversion value update errors
        /// </summary>
        /// <param name="callback">Callback receives error message</param>
        public static void OnConversionValueUpdateFailed(System.Action<string> callback)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            if (BoostOpsSKANManager.Instance != null)
            {
                BoostOpsSKANManager.Instance.OnConversionValueUpdateFailed += callback;
            }
            #endif
        }
    }
}

