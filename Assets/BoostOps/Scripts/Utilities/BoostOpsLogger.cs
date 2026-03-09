using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Centralized logging system for BoostOps SDK
    /// Supports separate controls for Editor Window vs Runtime debug logging
    /// </summary>
    public static class BoostOpsLogger
    {
        /// <summary>
        /// Global debug logging enabled state (backward compatibility)
        /// When set, enables both Editor and Runtime debug logging
        /// </summary>
        public static bool IsDebugLoggingEnabled 
        { 
            get => IsEditorDebugLoggingEnabled || IsRuntimeDebugLoggingEnabled;
            set 
            {
                IsEditorDebugLoggingEnabled = value;
                IsRuntimeDebugLoggingEnabled = value;
            }
        }
        
        /// <summary>
        /// Editor Window debug logging enabled state
        /// Controls debug messages from Editor Window, Post Process Build, etc.
        /// </summary>
        public static bool IsEditorDebugLoggingEnabled { get; set; } = false;
        
        /// <summary>
        /// Runtime debug logging enabled state  
        /// Controls debug messages from runtime SDK components (Analytics, Campaigns, etc.)
        /// </summary>
        public static bool IsRuntimeDebugLoggingEnabled { get; set; } = false;
        
        /// <summary>
        /// Log a runtime debug message (only if runtime debug logging is enabled)
        /// Used for runtime SDK components like Analytics, Campaigns, etc.
        /// </summary>
        public static void LogDebug(string message)
        {
            if (IsRuntimeDebugLoggingEnabled)
            {
                Debug.Log($"[BoostOps] {message}");
            }
        }
        
        /// <summary>
        /// Log a runtime debug message with component prefix (only if runtime debug logging is enabled)
        /// Used for runtime SDK components like Analytics, Campaigns, etc.
        /// </summary>
        public static void LogDebug(string component, string message)
        {
            if (IsRuntimeDebugLoggingEnabled)
            {
                Debug.Log($"[BoostOps {component}] {message}");
            }
        }
        
        /// <summary>
        /// Log an Editor Window debug message (only if editor debug logging is enabled)
        /// Used for Editor Window, Post Process Build, and other editor-only components
        /// </summary>
        public static void LogEditorDebug(string message)
        {
            if (IsEditorDebugLoggingEnabled)
            {
                Debug.Log($"[BoostOps Editor] {message}");
            }
        }
        
        /// <summary>
        /// Log an Editor Window debug message with component prefix (only if editor debug logging is enabled)
        /// Used for Editor Window, Post Process Build, and other editor-only components
        /// </summary>
        public static void LogEditorDebug(string component, string message)
        {
            if (IsEditorDebugLoggingEnabled)
            {
                Debug.Log($"[BoostOps Editor {component}] {message}");
            }
        }
        
        /// <summary>
        /// Log a warning (always shown, but with controlled prefix)
        /// </summary>
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"[BoostOps] {message}");
        }
        
        /// <summary>
        /// Log a warning with component prefix (always shown)
        /// </summary>
        public static void LogWarning(string component, string message)
        {
            Debug.LogWarning($"[BoostOps {component}] {message}");
        }
        
        /// <summary>
        /// Log an error (always shown)
        /// </summary>
        public static void LogError(string message)
        {
            Debug.LogError($"[BoostOps] {message}");
        }
        
        /// <summary>
        /// Log an error with component prefix (always shown)
        /// </summary>
        public static void LogError(string component, string message)
        {
            Debug.LogError($"[BoostOps {component}] {message}");
        }
        
        /// <summary>
        /// Log important information (always shown, but less verbose than debug)
        /// Use sparingly for critical state changes
        /// </summary>
        public static void LogInfo(string message)
        {
            Debug.Log($"[BoostOps] {message}");
        }
        
        /// <summary>
        /// Log important information with component prefix (always shown)
        /// </summary>
        public static void LogInfo(string component, string message)
        {
            Debug.Log($"[BoostOps {component}] {message}");
        }
        
        #region Logging Control Utilities
        
        /// <summary>
        /// Enable only Editor Window debug logging (silences runtime debug messages)
        /// Perfect for editor-only development and debugging UI issues
        /// </summary>
        public static void EnableEditorLoggingOnly()
        {
            IsEditorDebugLoggingEnabled = true;
            IsRuntimeDebugLoggingEnabled = false;
            Debug.Log("[BoostOps Logger] 🎯 Editor debug logging enabled, Runtime debug logging disabled");
        }
        
        /// <summary>
        /// Enable only Runtime debug logging (silences editor window debug messages)  
        /// Perfect for testing runtime behavior without editor noise
        /// </summary>
        public static void EnableRuntimeLoggingOnly()
        {
            IsEditorDebugLoggingEnabled = false;
            IsRuntimeDebugLoggingEnabled = true;
            Debug.Log("[BoostOps Logger] 🚀 Runtime debug logging enabled, Editor debug logging disabled");
        }
        
        /// <summary>
        /// Enable all debug logging (both Editor and Runtime)
        /// Same as setting IsDebugLoggingEnabled = true
        /// </summary>
        public static void EnableAllLogging()
        {
            IsEditorDebugLoggingEnabled = true;
            IsRuntimeDebugLoggingEnabled = true;
            Debug.Log("[BoostOps Logger] 📢 All debug logging enabled (Editor + Runtime)");
        }
        
        /// <summary>
        /// Disable all debug logging (both Editor and Runtime)
        /// Same as setting IsDebugLoggingEnabled = false
        /// </summary>
        public static void DisableAllLogging()
        {
            IsEditorDebugLoggingEnabled = false;
            IsRuntimeDebugLoggingEnabled = false;
            Debug.Log("[BoostOps Logger] 🔇 All debug logging disabled");
        }
        
        /// <summary>
        /// Print current logging status for debugging
        /// </summary>
        public static void PrintLoggingStatus()
        {
            Debug.Log($"[BoostOps Logger] Status - Editor: {(IsEditorDebugLoggingEnabled ? "✅" : "❌")}, Runtime: {(IsRuntimeDebugLoggingEnabled ? "✅" : "❌")}");
        }
        
        #endregion
    }
} 