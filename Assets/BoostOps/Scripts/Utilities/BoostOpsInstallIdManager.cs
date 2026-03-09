using System;
using UnityEngine;

namespace BoostOps.Utilities
{
    /// <summary>
    /// Manages the generation, persistence, and retrieval of install_id for BoostOps analytics.
    /// 
    /// Spec:
    /// - Format: boi_<Base64URL UUIDv4>
    /// - Lifecycle: Per-install (resets on uninstall/reinstall)
    /// - Storage: Unity PlayerPrefs for cross-session persistence
    /// - Privacy: No device fingerprinting, purely random per-install identifier
    /// </summary>
    public static class BoostOpsInstallIdManager
    {
        private const string INSTALL_ID_PREF_KEY = "BoostOps_InstallId";
        private const string INSTALL_ID_PREFIX = "boi_";
        
        private static string _cachedInstallId;
        
        /// <summary>
        /// Get or generate the install_id for this app installation.
        /// Thread-safe and cached after first call.
        /// </summary>
        /// <returns>Install ID in format: boi_<Base64URL_UUIDv4></returns>
        public static string GetInstallId()
        {
            // Return cached value if already loaded
            if (!string.IsNullOrEmpty(_cachedInstallId))
            {
                return _cachedInstallId;
            }
            
            // Try to load from PlayerPrefs
            string storedId = PlayerPrefs.GetString(INSTALL_ID_PREF_KEY, null);
            
            if (!string.IsNullOrEmpty(storedId) && IsValidInstallId(storedId))
            {
                _cachedInstallId = storedId;
                BoostOpsLogger.LogDebug("InstallId", $"📱 Loaded existing install_id: {_cachedInstallId}");
                return _cachedInstallId;
            }
            
            // Generate new install_id
            _cachedInstallId = GenerateNewInstallId();
            
            // Persist to PlayerPrefs
            PlayerPrefs.SetString(INSTALL_ID_PREF_KEY, _cachedInstallId);
            PlayerPrefs.Save(); // Ensure immediate persistence
            
            BoostOpsLogger.LogInfo("InstallId", $"🆕 Generated new install_id: {_cachedInstallId}");
            return _cachedInstallId;
        }
        
        /// <summary>
        /// Generate a new install_id using UUIDv4 + Base64URL encoding.
        /// Format: boi_<Base64URL_UUIDv4_no_padding>
        /// </summary>
        /// <returns>New install ID</returns>
        private static string GenerateNewInstallId()
        {
            // Generate 128-bit UUIDv4
            Guid uuid = Guid.NewGuid();
            byte[] uuidBytes = uuid.ToByteArray();
            
            // Convert to Base64URL (RFC 4648 Section 5) - no padding
            string base64Url = Convert.ToBase64String(uuidBytes)
                .Replace('+', '-')    // Replace + with -
                .Replace('/', '_')    // Replace / with _
                .TrimEnd('=');        // Remove padding
            
            return INSTALL_ID_PREFIX + base64Url;
        }
        
        /// <summary>
        /// Validate that an install_id has the correct format
        /// </summary>
        /// <param name="installId">Install ID to validate</param>
        /// <returns>True if valid format</returns>
        private static bool IsValidInstallId(string installId)
        {
            if (string.IsNullOrEmpty(installId))
                return false;
                
            if (!installId.StartsWith(INSTALL_ID_PREFIX))
                return false;
                
            // Extract the Base64URL part
            string base64UrlPart = installId.Substring(INSTALL_ID_PREFIX.Length);
            
            if (string.IsNullOrEmpty(base64UrlPart))
                return false;
                
            // Basic length check for UUIDv4 Base64URL (should be 22 chars without padding)
            if (base64UrlPart.Length != 22)
                return false;
                
            // Check for valid Base64URL characters only
            foreach (char c in base64UrlPart)
            {
                if (!IsBase64UrlChar(c))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if character is valid in Base64URL encoding
        /// </summary>
        private static bool IsBase64UrlChar(char c)
        {
            return (c >= 'A' && c <= 'Z') ||    // A-Z
                   (c >= 'a' && c <= 'z') ||    // a-z  
                   (c >= '0' && c <= '9') ||    // 0-9
                   c == '-' || c == '_';        // Base64URL specific chars
        }
        
        /// <summary>
        /// Reset the install_id (for testing purposes or manual reset)
        /// This will generate a new ID on next GetInstallId() call
        /// </summary>
        public static void ResetInstallId()
        {
            _cachedInstallId = null;
            PlayerPrefs.DeleteKey(INSTALL_ID_PREF_KEY);
            PlayerPrefs.Save();
            BoostOpsLogger.LogInfo("InstallId", "🔄 Install ID reset - new ID will be generated on next access");
        }
        
        /// <summary>
        /// Check if install_id is currently cached in memory
        /// </summary>
        public static bool HasCachedInstallId => !string.IsNullOrEmpty(_cachedInstallId);
        
        /// <summary>
        /// Check if install_id exists in persistent storage
        /// </summary>
        public static bool HasPersistedInstallId => !string.IsNullOrEmpty(PlayerPrefs.GetString(INSTALL_ID_PREF_KEY, null));
    }
}

