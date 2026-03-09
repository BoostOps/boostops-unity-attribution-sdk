using UnityEngine;
using System.Security.Cryptography;
using System.Text;
using System;

#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace BoostOps
{
    /// <summary>
    /// Privacy-focused device identifier manager that hashes identifiers once and caches them
    /// Provides consistent, anonymized device identification for analytics and attribution
    /// </summary>
    public static class BoostOpsDeviceIdManager
    {
        private static string _hashedDeviceId;
#pragma warning disable 414 // Field assigned but never used (false positive - used in properties)
        private static string _hashedIdfv;
        private static string _hashedIdfa;
#pragma warning restore 414
        
        /// <summary>
        /// Get the primary hashed device identifier (stable across app launches)
        /// Uses IDFV on iOS, Android ID equivalent on Android, SystemInfo.deviceUniqueIdentifier as fallback
        /// </summary>
        public static string HashedDeviceId
        {
            get
            {
                if (_hashedDeviceId == null)
                {
                    _hashedDeviceId = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.HASHED_DEVICE_ID, "");
                    if (string.IsNullOrEmpty(_hashedDeviceId))
                    {
                        string rawId = GetRawDeviceId();
                        _hashedDeviceId = ComputeSha256Hash(rawId);
                        PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.HASHED_DEVICE_ID, _hashedDeviceId);
                        PlayerPrefs.Save();
                        
                        BoostOpsLogger.LogDebug("DeviceId", "Generated new hashed device ID");
                    }
                }
                return _hashedDeviceId;
            }
        }
        
        /// <summary>
        /// Get hashed IDFV (iOS only) - stable until all apps from developer are uninstalled
        /// Returns null on non-iOS platforms
        /// </summary>
        public static string HashedIdfv
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                if (_hashedIdfv == null)
                {
                    _hashedIdfv = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.HASHED_IDFV, "");
                    if (string.IsNullOrEmpty(_hashedIdfv))
                    {
                        string rawIdfv = Device.vendorIdentifier;
                        if (!string.IsNullOrEmpty(rawIdfv) && rawIdfv != "00000000-0000-0000-0000-000000000000")
                        {
                            _hashedIdfv = ComputeSha256Hash(rawIdfv);
                            PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.HASHED_IDFV, _hashedIdfv);
                            PlayerPrefs.Save();
                            
                            BoostOpsLogger.LogDebug("DeviceId", "Generated new hashed IDFV");
                        }
                    }
                }
                return _hashedIdfv;
#else
                return null;
#endif
            }
        }
        
        /// <summary>
        /// Get hashed IDFA (iOS only) if user has granted ATT permission
        /// Returns null if not authorized or on non-iOS platforms
        /// Checks for IDFA resets on each app launch
        /// </summary>
        public static string HashedIdfaIfAuthorized
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                // Use Unity's built-in iOS Device API to get IDFA
                // This respects the user's ATT (App Tracking Transparency) settings automatically
                string rawIdfa = Device.advertisingIdentifier;
                if (string.IsNullOrEmpty(rawIdfa) || rawIdfa == "00000000-0000-0000-0000-000000000000")
                {
                    // IDFA not available or user hasn't granted permission
                    return null;
                }
                
                // Check if IDFA has changed (user reset it)
                string cachedRawIdfa = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.RAW_IDFA_CACHE, "");
                string cachedHashedIdfa = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.HASHED_IDFA, "");
                
                if (rawIdfa != cachedRawIdfa)
                {
                    // IDFA changed or first time - recompute hash
                    _hashedIdfa = ComputeSha256Hash(rawIdfa);
                    PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.HASHED_IDFA, _hashedIdfa);
                    PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.RAW_IDFA_CACHE, rawIdfa);
                    PlayerPrefs.Save();
                    
                    BoostOpsLogger.LogDebug("DeviceId", "Updated hashed IDFA (raw IDFA changed)");
                }
                else
                {
                    // Use cached hash
                    _hashedIdfa = cachedHashedIdfa;
                }
                
                return _hashedIdfa;
#else
                return null;
#endif
            }
        }
        
        /// <summary>
        /// Get the raw device identifier for hashing
        /// Prioritizes platform-specific stable identifiers
        /// </summary>
        private static string GetRawDeviceId()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Use IDFV as primary identifier on iOS
            string idfv = Device.vendorIdentifier;
            if (!string.IsNullOrEmpty(idfv) && idfv != "00000000-0000-0000-0000-000000000000")
            {
                return idfv;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            // On Android, SystemInfo.deviceUniqueIdentifier should give us the Android ID
            // which is stable per app installation
            string androidId = SystemInfo.deviceUniqueIdentifier;
            if (!string.IsNullOrEmpty(androidId))
            {
                return androidId;
            }
#endif
            
            // Fallback to Unity's cross-platform identifier
            return SystemInfo.deviceUniqueIdentifier;
        }
        
        /// <summary>
        /// Compute SHA-256 hash of input string
        /// </summary>
        private static string ComputeSha256Hash(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
                
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
        
        /// <summary>
        /// Clear all cached identifiers (useful for testing or privacy reset)
        /// </summary>
        public static void ClearCachedIdentifiers()
        {
            PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.HASHED_DEVICE_ID);
            PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.HASHED_IDFV);
            PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.HASHED_IDFA);
            PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.RAW_IDFA_CACHE);
            PlayerPrefs.Save();
            
            _hashedDeviceId = null;
            _hashedIdfv = null;
            _hashedIdfa = null;
            
            BoostOpsLogger.LogDebug("DeviceId", "Cleared all cached identifiers");
        }
        
        /// <summary>
        /// Get debug information about available identifiers (for development only)
        /// </summary>
        public static string GetDebugInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BoostOps Device ID Debug Info ===");
            sb.AppendLine($"Platform: {Application.platform}");
            sb.AppendLine($"Hashed Device ID: {HashedDeviceId?.Substring(0, 8)}...");
            sb.AppendLine($"Hashed IDFV: {HashedIdfv?.Substring(0, 8) ?? "null"}...");
            sb.AppendLine($"Hashed IDFA: {HashedIdfaIfAuthorized?.Substring(0, 8) ?? "null"}...");
            
#if UNITY_EDITOR
            sb.AppendLine("\n=== RAW IDs (EDITOR ONLY - NEVER IN BUILDS) ===");
            sb.AppendLine($"Raw Device ID: {GetRawDeviceId()}");
#if UNITY_IOS
            sb.AppendLine($"Raw IDFV: {Device.vendorIdentifier ?? "null"}");
            sb.AppendLine($"Raw IDFA: {Device.advertisingIdentifier ?? "null"}");
#endif
#endif
            
            return sb.ToString();
        }
    }
} 