using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using BoostOps.Core;

#if UNITY_IOS && !UNITY_EDITOR || UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace BoostOps.Analytics
{
    /// <summary>
    /// Comprehensive identifier collection and management for BoostOps analytics
    /// 
    /// Collects platform-specific identifiers according to BoostOps specification:
    /// - Cross-platform: boostops_id (ULID), session_id, storefront_country
    /// - iOS: IDFV, IDFA (if ATT), app_account_token, ASA attribution, SKAN source
    /// - Android: ASID hash (SHA-256), GAID, install referrer click_id
    /// - Optional: Firebase App Instance ID
    /// 
    /// Handles privacy compliance, caching, and cross-app persistence where applicable.
    /// </summary>
    public static class BoostOpsIdentifierManager
    {
        #region Platform-Agnostic Identifiers
        
        /// <summary>
        /// Cached BoostOps ID to avoid repeated storage lookups within the same session
        /// </summary>
        private static string _cachedBoostOpsId;
        
        /// <summary>
        /// Get the developer-provided custom user ID
        /// </summary>
        public static string GetCustomUserId()
        {
            try
            {
                return PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.CUSTOM_USER_ID, "");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to get custom user ID: {e.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Set the developer-provided custom user ID
        /// This will be included in all subsequent analytics events
        /// </summary>
        public static void SetCustomUserId(string customUserId)
        {
            try
            {
                if (string.IsNullOrEmpty(customUserId))
                {
                    // Clear the custom user ID
                    PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.CUSTOM_USER_ID);
                    Debug.Log("[BoostOps] Custom user ID cleared");
                }
                else
                {
                    PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.CUSTOM_USER_ID, customUserId);
                    Debug.Log($"[BoostOps] Custom user ID set: {customUserId}");
                }
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to set custom user ID: {e.Message}");
            }
        }
        
        /// <summary>
        /// Get or generate an Install ID (per-app installation, PlayerPrefs stored)
        /// Format: UUID v4 (per-install, standard format)
        /// Resets on app uninstall/reinstall
        /// </summary>
        public static string GetInstallId()
        {
            try
            {
                // Check PlayerPrefs for existing install ID
                string existingId = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.INSTALL_ID, "");
                
                Debug.Log($"[BoostOps] GetInstallId - existingId from PlayerPrefs: '{existingId}' (isEmpty: {string.IsNullOrEmpty(existingId)})");
                
                if (!string.IsNullOrEmpty(existingId))
                {
                    // Migrate old format (with dashes) to new format (no dashes)
                    if (existingId.Contains("-") && System.Guid.TryParse(existingId, out System.Guid guid))
                    {
                        existingId = guid.ToString("N"); // Convert to 32 hex chars, no dashes
                        PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_ID, existingId);
                        PlayerPrefs.Save();
                        Debug.Log($"[BoostOps] GetInstallId - Migrated format to: '{existingId}'");
                    }
                    return existingId;
                }
                
                // Generate new install ID
                string newInstallId = GenerateInstallId();
                Debug.Log($"[BoostOps] GetInstallId - Generated new install_id: '{newInstallId}'");
                
                // Store in PlayerPrefs (app-specific, resets on uninstall)
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_ID, newInstallId);
                PlayerPrefs.Save();
                
                return newInstallId;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to get/generate Install ID: {e.Message}");
                string fallbackId = GenerateInstallId();
                Debug.Log($"[BoostOps] GetInstallId - Using fallback install_id: '{fallbackId}'");
                return fallbackId; // Return a new one without storing
            }
        }
        
        /// <summary>
        /// Generate a new Install ID
        /// Format: 32 hex characters (no dashes, cleaner format)
        /// Example: 550e8400e29b41d4a716446655440000
        /// </summary>
        private static string GenerateInstallId()
        {
            try
            {
                return System.Guid.NewGuid().ToString("N"); // "N" format = 32 hex digits, no dashes
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to generate Install ID: {e.Message}");
                return $"ERROR{System.DateTime.UtcNow.Ticks:X}"; // Hex format for error too
            }
        }
        
        /// <summary>
        /// Get or generate the primary BoostOps ID (ULID format)
        /// 
        /// CRITICAL: This is the ONLY source of truth for boostops_id
        /// - ALWAYS client-generated (NEVER from server)
        /// - Generated ONCE on first app launch
        /// - Persisted across app sessions forever
        /// - NEVER regenerated after first creation
        /// 
        /// Cached in Keychain (iOS) or EncryptedSharedPrefs (Android) for cross-app persistence
        /// </summary>
        /// <returns>BoostOps ID in format: boid_XXXXXXXXXXXXXXXXXXXXXXXX</returns>
        public static string GetBoostOpsId()
        {
            // Return cached value if already loaded and valid
            if (!string.IsNullOrEmpty(_cachedBoostOpsId) && BoostOpsULIDGenerator.IsValidBoostOpsId(_cachedBoostOpsId))
            {
                // Debug.Log($"[BoostOpsID] ♻️ Returning cached boostops_id: {_cachedBoostOpsId}");
                return _cachedBoostOpsId;
            }
            
            // Try to load from persistent storage
            string existingId = GetStoredBoostOpsId();
            if (!string.IsNullOrEmpty(existingId) && BoostOpsULIDGenerator.IsValidBoostOpsId(existingId))
            {
                _cachedBoostOpsId = existingId;
                // Debug.Log($"[BoostOpsID] ✅ Loaded existing boostops_id from storage: {_cachedBoostOpsId}");
                return _cachedBoostOpsId;
            }
            
            // Generate new ULID-based BoostOps ID (ONLY happens on first launch)
            _cachedBoostOpsId = BoostOpsULIDGenerator.GenerateBoostOpsId();
            Debug.Log($"[BoostOpsID] 🆕 Generated NEW boostops_id: {_cachedBoostOpsId}");
            Debug.Log($"[BoostOpsID] ⚠️ This should ONLY happen on first app launch!");
            StoreBoostOpsId(_cachedBoostOpsId);
            
            return _cachedBoostOpsId;
        }
        
        /// <summary>
        /// Generate a new session ID for the current app session
        /// Should be regenerated on each app foreground/resume
        /// </summary>
        /// <returns>Session ID in format: sess_XXXXXXXX</returns>
        public static string GenerateSessionId()
        {
            return BoostOpsULIDGenerator.GenerateSessionId();
        }
        
        /// <summary>
        /// Get the App Store/Play Store region/country code
        /// Required by some SKAN/AAK coarse rules and regional UA reporting
        /// </summary>
        /// <returns>Two-letter country code (e.g., "US", "GB", "JP") or null if unavailable</returns>
        public static string GetStorefrontCountry()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                // Use Unity's built-in system language to infer country
                // This is an approximation - exact storefront country requires native implementation
                SystemLanguage language = Application.systemLanguage;
                string countryCode = GetCountryCodeFromSystemLanguage(language);
                if (!string.IsNullOrEmpty(countryCode))
                {
                    return countryCode;
                }
                
                // Fallback to region info if available
                try
                {
                    var culture = System.Globalization.CultureInfo.CurrentCulture;
                    if (culture != null && !string.IsNullOrEmpty(culture.TwoLetterISOLanguageName))
                    {
                        return culture.TwoLetterISOLanguageName.ToUpper();
                    }
                }
                catch
                {
                    // Ignore culture info errors
                }
                
                return "US"; // Default fallback
#elif UNITY_ANDROID && !UNITY_EDITOR
                return GetAndroidStorefrontCountry();
#else
                // Editor/other platforms - return a default for testing
                return "US";
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not determine storefront country: {e.Message}");
                return "US"; // Return default instead of null
            }
        }
        
        /// <summary>
        /// Convert Unity's SystemLanguage to approximate country code
        /// This is a best-effort mapping and may not be 100% accurate
        /// </summary>
        private static string GetCountryCodeFromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.English: return "US";
                case SystemLanguage.French: return "FR";
                case SystemLanguage.German: return "DE";
                case SystemLanguage.Spanish: return "ES";
                case SystemLanguage.Italian: return "IT";
                case SystemLanguage.Japanese: return "JP";
                case SystemLanguage.Korean: return "KR";
                case SystemLanguage.Chinese: return "CN";
                case SystemLanguage.ChineseSimplified: return "CN";
                case SystemLanguage.ChineseTraditional: return "TW";
                case SystemLanguage.Portuguese: return "PT";
                case SystemLanguage.Russian: return "RU";
                case SystemLanguage.Dutch: return "NL";
                case SystemLanguage.Polish: return "PL";
                case SystemLanguage.Swedish: return "SE";
                case SystemLanguage.Norwegian: return "NO";
                case SystemLanguage.Danish: return "DK";
                case SystemLanguage.Finnish: return "FI";
                case SystemLanguage.Turkish: return "TR";
                case SystemLanguage.Arabic: return "SA";
                case SystemLanguage.Hebrew: return "IL";
                case SystemLanguage.Thai: return "TH";
                case SystemLanguage.Vietnamese: return "VN";
                case SystemLanguage.Czech: return "CZ";
                case SystemLanguage.Hungarian: return "HU";
                case SystemLanguage.Greek: return "GR";
                case SystemLanguage.Ukrainian: return "UA";
                case SystemLanguage.Romanian: return "RO";
                case SystemLanguage.Bulgarian: return "BG";
                case SystemLanguage.Slovenian: return "SI";
                case SystemLanguage.Slovak: return "SK";
                case SystemLanguage.Latvian: return "LV";
                case SystemLanguage.Lithuanian: return "LT";
                case SystemLanguage.Estonian: return "EE";
                default: return "US"; // Default fallback
            }
        }
        
        #endregion
        
        #region iOS-Specific Identifiers
        
#if UNITY_IOS
        /// <summary>
        /// Get Identifier for Vendor (IDFV) - iOS only
        /// Backup join key inside your own iOS portfolio (cross-promo, churn loops)
        /// </summary>
        /// <returns>IDFV string or null if unavailable</returns>
        public static string GetIDFV()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                // Use Unity's built-in device identifier (returns IDFV on iOS)
                string deviceId = SystemInfo.deviceUniqueIdentifier;
                if (!string.IsNullOrEmpty(deviceId) && deviceId != "n/a")
                {
                    return deviceId;
                }
                return null;
#else
                // Editor simulation
                return "9C2E9F30-8C6A-4E7F-BD1D-7E1E1B1A4F61";
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get IDFV: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get Identifier for Advertising (IDFA) - iOS only, requires ATT consent
        /// Only used by ad-networks still accepting it (rare)
        /// </summary>
        /// <returns>IDFA string if available and authorized, null otherwise</returns>
        public static string GetIDFA()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                // Check ATT authorization status using Unity's iOS 14 Support package
                #if UNITY_ADS_IOS_SUPPORT
                var status = Unity.Advertisement.IosSupport.ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
                if (status != Unity.Advertisement.IosSupport.ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED)
                {
                    Debug.Log($"[BoostOps] IDFA not available - ATT not authorized (status: {status})");
                    return null; // Don't return IDFA if not authorized
                }
                #else
                Debug.LogWarning("[BoostOps] Unity iOS 14 Support package not installed - cannot check ATT status");
                return null;
                #endif
                
                // Use native implementation for IDFA
                string idfa = GetIOSIDFA();
                if (string.IsNullOrEmpty(idfa) || idfa == "00000000-0000-0000-0000-000000000000")
                {
                    return null; // Invalid/zero IDFA
                }
                return idfa;
#else
                // Editor simulation - return null to simulate no ATT consent
                return null;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get IDFA: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get or generate App Account Token - iOS only
        /// Deterministic join: ASA install ⇔ StoreKit receipts / ASN v2
        /// </summary>
        /// <returns>UUID string for App Account Token</returns>
        public static string GetAppAccountToken()
        {
            // Check if we already have one stored
            string existingToken = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.APP_ACCOUNT_TOKEN, "");
            if (!string.IsNullOrEmpty(existingToken))
            {
                return existingToken;
            }
            
            // Generate new UUID for App Account Token
            string newToken = BoostOpsULIDGenerator.GenerateAppAccountToken();
            PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.APP_ACCOUNT_TOKEN, newToken);
            PlayerPrefs.Save();
            
            return newToken;
        }
        
        /// <summary>
        /// Get Apple Search Ads attribution token - iOS installs only
        /// Apple Search Ads campaign / keyword details
        /// </summary>
        /// <returns>ASA attribution token string or null if unavailable</returns>
        public static string GetASAAttributionToken()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                // Use native implementation for ASA token
                return GetIOSASAToken();
#else
                // Editor simulation
                return null;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get ASA attribution token: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get SKAN source identifier - iOS installs only
        /// Optional: forward the 4-digit code for custom reporting
        /// </summary>
        /// <returns>4-digit SKAN source ID or null if unavailable</returns>
        public static string GetSKANSourceId()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                // Use native implementation for SKAN source ID
                return GetIOSSKANSourceId();
#else
                // Editor simulation
                return null;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get SKAN source ID: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Request App Tracking Transparency permission - iOS only
        /// This will show the ATT permission dialog to the user
        /// </summary>
        public static void RequestATTPermission()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                RequestIOSATTPermission();
#else
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not request ATT permission: {e.Message}");
            }
        }
        
        /// <summary>
        /// Store SKAN Source ID for later retrieval - iOS only
        /// This should be called during app launch if SKAN attribution data is available
        /// </summary>
        /// <param name="sourceId">4-digit SKAN source identifier</param>
        public static void StoreSKANSourceId(string sourceId)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceId))
                {
                    Debug.LogWarning("[BoostOps] Cannot store empty SKAN Source ID");
                    return;
                }
                
#if UNITY_IOS && !UNITY_EDITOR
                StoreIOSSKANSourceId(sourceId);
#else
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not store SKAN Source ID: {e.Message}");
            }
        }
        
#else
        // Stub implementations for non-iOS platforms
        public static string GetIDFV() => null;
        public static string GetIDFA() => null;
        public static string GetAppAccountToken() => null;
        public static string GetASAAttributionToken() => null;
        public static string GetSKANSourceId() => null;
        public static void RequestATTPermission() { }
        public static void StoreSKANSourceId(string sourceId) { }
#endif
        
        #endregion
        
        #region Android-Specific Identifiers
        
#if UNITY_ANDROID
        /// <summary>
        /// Get Android App Set ID hash (SHA-256) - Android only
        /// Developer-scoped ID that survives GAID deprecation; joins cross-app
        /// </summary>
        /// <returns>Base64-encoded SHA-256 hash of App Set ID or null if unavailable</returns>
        public static string GetASIDHash()
        {
            try
            {
#if !UNITY_EDITOR
                string appSetId = GetAndroidAppSetId();
                if (string.IsNullOrEmpty(appSetId))
                    return null;
                
                // Hash with SHA-256 and encode as Base64
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(appSetId));
                    return Convert.ToBase64String(hashBytes);
                }
#else
                // Editor simulation
                return "OCYiK4_FgrMRMqZ8T1iY9JWA_9_OzHGYGKTN3j3rEGI"; // Mock SHA-256 hash
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get ASID hash: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get Google Advertising ID (GAID) - Android only, until Google sunsets
        /// Deterministic ad-network joins today (Google Ads, Meta, etc.)
        /// </summary>
        /// <returns>GAID string or null if unavailable/opted out</returns>
        public static string GetGAID()
        {
            try
            {
#if !UNITY_EDITOR
                return GetAndroidGAID();
#else
                // Editor simulation
                return "e2c2d3f0-1234-5678-90ab-cdef12345678";
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get GAID: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get Install Referrer click ID - Android installs only
        /// Google Play Install Referrer → click-level attribution
        /// </summary>
        /// <returns>Install referrer click ID or null if unavailable</returns>
        public static string GetInstallReferrerClickId()
        {
            try
            {
#if !UNITY_EDITOR
                return GetAndroidInstallReferrerClickId();
#else
                // Editor simulation
                return null;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get install referrer click ID: {e.Message}");
                return null;
            }
        }
        
#endif
        
        #endregion
        
        #region Windows-Specific Identifiers
        
#if UNITY_STANDALONE_WIN
        /// <summary>
        /// Get the raw device identifier for Windows Standalone builds.
        /// Uses SystemInfo.deviceUniqueIdentifier (machine GUID).
        /// Stable across app sessions on the same machine.
        /// Server handles hashing/anonymization.
        /// </summary>
        public static string GetWindowsDeviceId()
        {
            try
            {
                string rawId = SystemInfo.deviceUniqueIdentifier;
                if (!string.IsNullOrEmpty(rawId) && rawId != "n/a")
                {
                    return rawId;
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get Windows device ID: {e.Message}");
                return null;
            }
        }
        
        private const int HKEY_CURRENT_USER = unchecked((int)0x80000001);
        private const int KEY_READ = 0x20019;
        private const int REG_SZ = 1;
        private const int REG_DWORD = 4;
        
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(int hKey, string subKey, int reserved, int desiredAccess, out int result);
        
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(int hKey, string valueName, IntPtr reserved, out int type, StringBuilder data, ref int dataSize);
        
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(int hKey, string valueName, IntPtr reserved, out int type, out int data, ref int dataSize);
        
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(int hKey);
        
        /// <summary>
        /// Get the Windows Advertising ID (AdvertisingId / msaid).
        /// Reads from the registry at HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo
        /// via P/Invoke to avoid dependency on Microsoft.Win32.Registry assembly.
        /// Returns null if the user has disabled advertising ID or it's not available.
        /// </summary>
        private const int HKEY_LOCAL_MACHINE = unchecked((int)0x80000002);
        
        /// <summary>
        /// Get the Windows Machine GUID from HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid.
        /// This is a stable per-installation GUID analogous to IDFV on iOS or Android ID.
        /// Same value is visible to all apps on the machine, enabling cross-app attribution.
        /// Only changes if Windows is reinstalled.
        /// </summary>
        public static string GetWindowsMachineGuid()
        {
            try
            {
                int hKey;
                int result = RegOpenKeyEx(HKEY_LOCAL_MACHINE,
                    @"SOFTWARE\Microsoft\Cryptography",
                    0, KEY_READ, out hKey);
                
                if (result != 0)
                {
                    Debug.LogWarning("[BoostOps] Windows Cryptography registry key not found");
                    return null;
                }
                
                try
                {
                    int idSize = 512;
                    int idType;
                    var idBuffer = new StringBuilder(idSize);
                    result = RegQueryValueEx(hKey, "MachineGuid", IntPtr.Zero, out idType, idBuffer, ref idSize);
                    if (result == 0 && idType == REG_SZ)
                    {
                        string machineGuid = idBuffer.ToString();
                        if (!string.IsNullOrEmpty(machineGuid))
                        {
                            return machineGuid;
                        }
                    }
                }
                finally
                {
                    RegCloseKey(hKey);
                }
                
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get Windows Machine GUID: {e.Message}");
                return null;
            }
        }
        
        public static string GetWindowsAdvertisingId()
        {
            try
            {
                int hKey;
                int result = RegOpenKeyEx(HKEY_CURRENT_USER, 
                    @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", 
                    0, KEY_READ, out hKey);
                
                if (result != 0)
                {
                    Debug.Log("[BoostOps] Windows AdvertisingInfo registry key not found");
                    return null;
                }
                
                try
                {
                    int enabledValue;
                    int enabledSize = 4;
                    int enabledType;
                    result = RegQueryValueEx(hKey, "Enabled", IntPtr.Zero, out enabledType, out enabledValue, ref enabledSize);
                    if (result == 0 && enabledType == REG_DWORD && enabledValue == 0)
                    {
                        Debug.Log("[BoostOps] Windows Advertising ID is disabled by user");
                        return null;
                    }
                    
                    int idSize = 512;
                    int idType;
                    var idBuffer = new StringBuilder(idSize);
                    result = RegQueryValueEx(hKey, "Id", IntPtr.Zero, out idType, idBuffer, ref idSize);
                    if (result == 0 && idType == REG_SZ)
                    {
                        string advertisingId = idBuffer.ToString();
                        if (!string.IsNullOrEmpty(advertisingId) && 
                            advertisingId != "00000000-0000-0000-0000-000000000000")
                        {
                            return advertisingId;
                        }
                    }
                }
                finally
                {
                    RegCloseKey(hKey);
                }
                
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get Windows Advertising ID: {e.Message}");
                return null;
            }
        }
#else
        public static string GetWindowsDeviceId() => null;
        public static string GetWindowsMachineGuid() => null;
        public static string GetWindowsAdvertisingId() => null;
#endif
        
        #endregion
        
        #region Optional Identifiers
        
        /// <summary>
        /// Get Firebase App Instance ID - if GA4 integration present
        /// Links GA4 events to Google Ads SKAN schema
        /// </summary>
        /// <returns>Firebase App Instance ID or null if Firebase not available</returns>
        public static string GetFirebaseAppInstanceId()
        {
            try
            {
                // Try to get Firebase App Instance ID via reflection to avoid hard dependency
#if UNITY_ANDROID || UNITY_IOS
                var firebaseAppType = Type.GetType("Firebase.FirebaseApp, Firebase.App");
                if (firebaseAppType != null)
                {
                    var defaultAppProperty = firebaseAppType.GetProperty("DefaultInstance");
                    var defaultApp = defaultAppProperty?.GetValue(null);
                    
                    if (defaultApp != null)
                    {
                        var optionsProperty = defaultApp.GetType().GetProperty("Options");
                        var options = optionsProperty?.GetValue(defaultApp);
                        
                        if (options != null)
                        {
                            var appIdProperty = options.GetType().GetProperty("AppId");
                            var appId = appIdProperty?.GetValue(options) as string;
                            return appId;
                        }
                    }
                }
#endif
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Could not get Firebase App Instance ID: {e.Message}");
                return null;
            }
        }
        
        #endregion
        
        #region Storage and Caching
        
        /// <summary>
        /// Get stored BoostOps ID from persistent cross-app storage
        /// Uses iOS Keychain or Android SharedPreferences with signature-based sharing
        /// </summary>
        private static string GetStoredBoostOpsId()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                Debug.Log($"[BoostOpsID] 🔍 Checking iOS Keychain for existing boostops_id...");
                string keychainId = GetBoostOpsIdFromKeychain();
                if (!string.IsNullOrEmpty(keychainId))
                {
                    Debug.Log($"[BoostOpsID] ✅ Found in Keychain: {keychainId}");
                    return keychainId;
                }
                Debug.Log($"[BoostOpsID] 🔍 Not found in Keychain, checking PlayerPrefs fallback...");
                string playerPrefsId = GetBoostOpsIdFromPlayerPrefs();
                if (!string.IsNullOrEmpty(playerPrefsId))
                {
                    Debug.Log($"[BoostOpsID] ✅ Found in PlayerPrefs fallback: {playerPrefsId}");
                }
                else
                {
                    Debug.Log($"[BoostOpsID] ❌ Not found in any storage (first launch)");
                }
                return playerPrefsId;
#elif UNITY_ANDROID && !UNITY_EDITOR
                Debug.Log($"[BoostOpsID] 🔍 Checking Android SharedPreferences for existing boostops_id...");
                string androidId = GetBoostOpsIdFromSharedStorage();
                if (!string.IsNullOrEmpty(androidId))
                {
                    Debug.Log($"[BoostOpsID] ✅ Found in SharedPreferences: {androidId}");
                    return androidId;
                }
                Debug.Log($"[BoostOpsID] 🔍 Not found in SharedPreferences, checking PlayerPrefs fallback...");
                string playerPrefsId = GetBoostOpsIdFromPlayerPrefs();
                if (!string.IsNullOrEmpty(playerPrefsId))
                {
                    Debug.Log($"[BoostOpsID] ✅ Found in PlayerPrefs fallback: {playerPrefsId}");
                }
                else
                {
                    Debug.Log($"[BoostOpsID] ❌ Not found in any storage (first launch)");
                }
                return playerPrefsId;
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
                Debug.Log($"[BoostOpsID] Checking Windows shared file for existing boostops_id...");
                string windowsId = GetBoostOpsIdFromWindowsSharedFile();
                if (!string.IsNullOrEmpty(windowsId))
                {
                    Debug.Log($"[BoostOpsID] Found in Windows shared file: {windowsId}");
                    return windowsId;
                }
                Debug.Log($"[BoostOpsID] Not found in Windows shared file, checking PlayerPrefs fallback...");
                string windowsPlayerPrefsId = GetBoostOpsIdFromPlayerPrefs();
                if (!string.IsNullOrEmpty(windowsPlayerPrefsId))
                {
                    Debug.Log($"[BoostOpsID] Found in PlayerPrefs fallback: {windowsPlayerPrefsId}");
                    StoreBoostOpsIdInWindowsSharedFile(windowsPlayerPrefsId);
                }
                else
                {
                    Debug.Log($"[BoostOpsID] Not found in any storage (first launch)");
                }
                return windowsPlayerPrefsId;
#else
                // Editor/other platforms: Use PlayerPrefs as fallback
                return PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.BOOSTOPS_ID, "");
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Error retrieving stored BoostOps ID: {e.Message}");
                
                // Fallback to PlayerPrefs on any platform if native storage fails
                return GetBoostOpsIdFromPlayerPrefs();
            }
        }
        
        /// <summary>
        /// Store BoostOps ID in persistent cross-app storage
        /// Uses iOS Keychain or Android SharedPreferences with signature-based sharing
        /// </summary>
        private static void StoreBoostOpsId(string boostopsId)
        {
            if (string.IsNullOrEmpty(boostopsId))
            {
                Debug.LogWarning("[BoostOps] Cannot store empty BoostOps ID");
                return;
            }
            
            Debug.Log($"[BoostOpsID] 💾 Storing boostops_id: {boostopsId}");
            bool nativeSuccess = false;
            
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                Debug.Log($"[BoostOpsID] 🔐 Attempting to store in iOS Keychain...");
                nativeSuccess = StoreBoostOpsIdInKeychain(boostopsId);
                Debug.Log($"[BoostOpsID] 🔐 iOS Keychain storage result: {nativeSuccess}");
#elif UNITY_ANDROID && !UNITY_EDITOR
                Debug.Log($"[BoostOpsID] 📱 Attempting to store in Android SharedPreferences...");
                nativeSuccess = StoreBoostOpsIdInSharedStorage(boostopsId);
                Debug.Log($"[BoostOpsID] 📱 Android SharedPreferences storage result: {nativeSuccess}");
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
                Debug.Log($"[BoostOpsID] Attempting to store in Windows shared file...");
                nativeSuccess = StoreBoostOpsIdInWindowsSharedFile(boostopsId);
                Debug.Log($"[BoostOpsID] Windows shared file storage result: {nativeSuccess}");
#else
                // Editor/other platforms: Use PlayerPrefs only
                nativeSuccess = false;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Error storing BoostOps ID in native storage: {e.Message}");
                nativeSuccess = false;
            }
            
            // ALWAYS store in PlayerPrefs as reliable fallback (even if native succeeds)
            // This ensures persistence even if native storage has silent failures
            Debug.Log($"[BoostOpsID] 💿 Storing in PlayerPrefs fallback (nativeSuccess={nativeSuccess})");
            bool playerPrefsSuccess = StoreBoostOpsIdInPlayerPrefs(boostopsId);
            
            if (!nativeSuccess && !playerPrefsSuccess)
            {
                Debug.LogError($"[BoostOpsID] ❌ CRITICAL: Failed to store boostops_id in ANY storage!");
            }
        }
        
        /// <summary>
        /// PlayerPrefs fallback storage (app-specific, no cross-app sharing)
        /// </summary>
        private static string GetBoostOpsIdFromPlayerPrefs()
        {
            return PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.BOOSTOPS_ID, "");
        }
        
        /// <summary>
        /// PlayerPrefs fallback storage (app-specific, no cross-app sharing)
        /// </summary>
        private static bool StoreBoostOpsIdInPlayerPrefs(string boostopsId)
        {
            try
            {
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.BOOSTOPS_ID, boostopsId);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to store BoostOps ID in PlayerPrefs: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Debug function to check all storage locations for BoostOps ID
        /// </summary>
        public static void DebugStorageStatus()
        {
            // Storage status debugging removed to reduce log verbosity
        }
        
        /// <summary>
        /// Clear BoostOps ID from all storage locations (for testing/reset ONLY)
        /// 
        /// ⚠️ WARNING: This will DELETE the user's permanent identity!
        /// - NEVER call this in production code
        /// - ONLY use for testing/debugging
        /// - Will cause user to be counted as a new install
        /// - Will break attribution and analytics continuity
        /// 
        /// </summary>
        public static void ClearStoredBoostOpsId()
        {
            Debug.LogWarning("[BoostOps] ⚠️ CLEARING BOOSTOPS_ID - User identity will be reset!");
            
            // Clear in-memory cache first
            _cachedBoostOpsId = null;
            
            // Clear PlayerPrefs
            PlayerPrefs.DeleteKey(BoostOpsPlayerPrefsKeys.BOOSTOPS_ID);
            PlayerPrefs.Save();
            
#if UNITY_IOS && !UNITY_EDITOR
            DeleteBoostOpsIdFromKeychain();
#endif
            
#if UNITY_ANDROID && !UNITY_EDITOR
            DeleteBoostOpsIdFromSharedStorage();
#endif
            
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DeleteBoostOpsIdFromWindowsSharedFile();
#endif
            
        }
        
        #endregion
        
        #region iOS Keychain Storage
        
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int storeBoostOpsIdInKeychain(string boostopsId);
        
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern System.IntPtr retrieveBoostOpsIdFromKeychain();
        
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int deleteBoostOpsIdFromKeychain();
        
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int boostOpsIdExistsInKeychain();
        
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void debugBoostOpsKeychainItems();
        
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void freeNativeString(System.IntPtr ptr);
        
        /// <summary>
        /// Store BoostOps ID in iOS Keychain with cross-app sharing
        /// </summary>
        private static bool StoreBoostOpsIdInKeychain(string boostopsId)
        {
            try
            {
                int result = storeBoostOpsIdInKeychain(boostopsId);
                if (result == 1)
                {
                    return true;
                }
                else
                {
                    Debug.LogWarning("[BoostOps] ❌ Failed to store BoostOps ID in iOS Keychain");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception storing BoostOps ID in Keychain: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Retrieve BoostOps ID from iOS Keychain
        /// </summary>
        private static string GetBoostOpsIdFromKeychain()
        {
            try
            {
                System.IntPtr ptr = retrieveBoostOpsIdFromKeychain();
                if (ptr != System.IntPtr.Zero)
                {
                    string result = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr);
                    // Free the memory allocated by native malloc() - use free() wrapper
                    freeNativeString(ptr);
                    
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                }
                
                return "";
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception retrieving BoostOps ID from Keychain: {e.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Delete BoostOps ID from iOS Keychain
        /// </summary>
        private static bool DeleteBoostOpsIdFromKeychain()
        {
            try
            {
                int result = deleteBoostOpsIdFromKeychain();
                return result == 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception deleting BoostOps ID from Keychain: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Debug iOS Keychain items
        /// </summary>
        private static void DebugKeychainItems()
        {
            try
            {
                debugBoostOpsKeychainItems();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception debugging Keychain items: {e.Message}");
            }
        }
#else
        // Stub implementations for non-iOS platforms
        private static bool StoreBoostOpsIdInKeychain(string boostopsId) => false;
        private static string GetBoostOpsIdFromKeychain() => "";
        private static bool DeleteBoostOpsIdFromKeychain() => false;
        private static void DebugKeychainItems() { }
#endif
        
        #endregion
        
        #region Android SharedPreferences Storage
        
#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Store BoostOps ID in Android SharedPreferences with cross-app sharing
        /// </summary>
        private static bool StoreBoostOpsIdInSharedStorage(string boostopsId)
        {
            try
            {
                using (var pluginClass = new AndroidJavaClass("com.boostops.unity.BoostOpsUnityPlugin"))
                {
                    // Initialize storage if not already done
                    pluginClass.CallStatic("initializeStorage");
                    
                    // Store the BoostOps ID
                    bool result = pluginClass.CallStatic<bool>("storeBoostOpsId", boostopsId);
                    
                    if (result)
                    {
                    }
                    else
                    {
                        Debug.LogWarning("[BoostOps] ❌ Failed to store BoostOps ID in Android SharedPreferences");
                    }
                    
                    return result;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception storing BoostOps ID in SharedStorage: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Retrieve BoostOps ID from Android SharedPreferences
        /// </summary>
        private static string GetBoostOpsIdFromSharedStorage()
        {
            try
            {
                using (var pluginClass = new AndroidJavaClass("com.boostops.unity.BoostOpsUnityPlugin"))
                {
                    // Initialize storage if not already done
                    pluginClass.CallStatic("initializeStorage");
                    
                    // Retrieve the BoostOps ID
                    string result = pluginClass.CallStatic<string>("retrieveBoostOpsId");
                    
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                    else
                    {
                        return "";
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception retrieving BoostOps ID from SharedStorage: {e.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Delete BoostOps ID from Android SharedPreferences
        /// </summary>
        private static bool DeleteBoostOpsIdFromSharedStorage()
        {
            try
            {
                using (var pluginClass = new AndroidJavaClass("com.boostops.unity.BoostOpsUnityPlugin"))
                {
                    pluginClass.CallStatic("initializeStorage");
                    return pluginClass.CallStatic<bool>("deleteBoostOpsId");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception deleting BoostOps ID from SharedStorage: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Debug Android SharedPreferences data
        /// </summary>
        private static void DebugSharedStorageData()
        {
            try
            {
                using (var pluginClass = new AndroidJavaClass("com.boostops.unity.BoostOpsUnityPlugin"))
                {
                    pluginClass.CallStatic("initializeStorage");
                    pluginClass.CallStatic("debugStoredData");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception debugging SharedStorage data: {e.Message}");
            }
        }
#else
        // Stub implementations for non-Android platforms
        private static bool StoreBoostOpsIdInSharedStorage(string boostopsId) => false;
        private static string GetBoostOpsIdFromSharedStorage() => "";
        private static bool DeleteBoostOpsIdFromSharedStorage() => false;
        private static void DebugSharedStorageData() { }
#endif
        
        #endregion
        
        #region Windows Shared File Storage
        
        private static readonly string WINDOWS_BOOSTOPS_DIR = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "BoostOps");
        private static readonly string WINDOWS_BOOSTOPS_ID_FILE = "boostops_id.dat";
        
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        /// <summary>
        /// Store BoostOps ID in a shared file at %LOCALAPPDATA%\BoostOps\boostops_id.dat
        /// All apps from the same publisher can read/write this file for cross-app identity.
        /// </summary>
        private static bool StoreBoostOpsIdInWindowsSharedFile(string boostopsId)
        {
            try
            {
                if (!System.IO.Directory.Exists(WINDOWS_BOOSTOPS_DIR))
                {
                    System.IO.Directory.CreateDirectory(WINDOWS_BOOSTOPS_DIR);
                }
                
                string filePath = System.IO.Path.Combine(WINDOWS_BOOSTOPS_DIR, WINDOWS_BOOSTOPS_ID_FILE);
                System.IO.File.WriteAllText(filePath, boostopsId);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to store BoostOps ID in Windows shared file: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Retrieve BoostOps ID from the shared Windows file.
        /// </summary>
        private static string GetBoostOpsIdFromWindowsSharedFile()
        {
            try
            {
                string filePath = System.IO.Path.Combine(WINDOWS_BOOSTOPS_DIR, WINDOWS_BOOSTOPS_ID_FILE);
                if (System.IO.File.Exists(filePath))
                {
                    string storedId = System.IO.File.ReadAllText(filePath).Trim();
                    if (!string.IsNullOrEmpty(storedId))
                    {
                        return storedId;
                    }
                }
                return "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to read BoostOps ID from Windows shared file: {e.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Delete BoostOps ID from Windows shared file.
        /// </summary>
        private static bool DeleteBoostOpsIdFromWindowsSharedFile()
        {
            try
            {
                string filePath = System.IO.Path.Combine(WINDOWS_BOOSTOPS_DIR, WINDOWS_BOOSTOPS_ID_FILE);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to delete BoostOps ID from Windows shared file: {e.Message}");
                return false;
            }
        }
#else
        private static bool StoreBoostOpsIdInWindowsSharedFile(string boostopsId) => false;
        private static string GetBoostOpsIdFromWindowsSharedFile() => "";
        private static bool DeleteBoostOpsIdFromWindowsSharedFile() => false;
#endif
        
        #endregion
        
        #region Native Platform Implementations
        
#if UNITY_IOS && !UNITY_EDITOR
        // Native implementations for identifiers Unity doesn't provide
        [DllImport("__Internal")]
        private static extern string GetIOSIDFV();
        
        [DllImport("__Internal")]
        private static extern string GetIOSIDFA();
        
        [DllImport("__Internal")]
        private static extern string GetIOSASAToken();
        
        [DllImport("__Internal")]
        private static extern string GetIOSSKANSourceId();
        
        [DllImport("__Internal")]
        private static extern void StoreIOSSKANSourceId(string sourceId);
        
        [DllImport("__Internal")]
        private static extern void RequestIOSATTPermission();
        
        [DllImport("__Internal")]
        private static extern string GetIOSLocale();
#endif
        
        #region Locale Detection
        
        /// <summary>
        /// Get device locale in industry-standard format (e.g., "en_US", "es_MX", "pt_BR")
        /// Uses native implementations on iOS/Android, .NET CultureInfo for other platforms
        /// </summary>
        /// <returns>Locale string in format "en_US" or null if unavailable</returns>
        public static string GetDeviceLocale()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                // iOS Device: Use native locale API for accurate iOS locale
                string locale = GetIOSLocale();
                if (!string.IsNullOrEmpty(locale))
                {
                    return locale;
                }
                
                Debug.LogWarning("[BoostOps] iOS native locale failed - returning null");
                return null;
                
#elif UNITY_ANDROID && !UNITY_EDITOR
                // Android Device: Use native locale API for accurate Android locale
                string locale = GetAndroidLocale();
                if (!string.IsNullOrEmpty(locale))
                {
                    return locale;
                }
                
                Debug.LogWarning("[BoostOps] Android native locale failed - returning null");
                return null;
                
#else
                // macOS, Windows, Linux, Unity Editor: Use .NET CultureInfo
                // This correctly detects the actual OS locale on all desktop platforms
                return GetDotNetLocale();
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to get device locale: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get locale using .NET CultureInfo (works on macOS, Windows, Linux, Editor)
        /// </summary>
        private static string GetDotNetLocale()
        {
            try
            {
                // Get current culture from .NET runtime
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                
                if (culture != null)
                {
                    // Get language and region
                    string language = culture.TwoLetterISOLanguageName;  // "en"
                    
                    // Try to get region info
                    var regionInfo = culture.IsNeutralCulture ? null : new System.Globalization.RegionInfo(culture.Name);
                    
                    if (regionInfo != null && !string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    {
                        // Full locale: language_COUNTRY (e.g., "en_US", "es_MX")
                        string locale = $"{language}_{regionInfo.TwoLetterISORegionName}";
                        // Debug.Log($"[BoostOps] .NET locale: {locale} (Culture: {culture.Name})");
                        return locale;
                    }
                    else
                    {
                        // Neutral culture (just language): "en", "es", "zh"
                        // Debug.Log($"[BoostOps] .NET locale (neutral): {language} (Culture: {culture.Name})");
                        return language;
                    }
                }
                else
                {
                    Debug.LogWarning("[BoostOps] .NET CultureInfo.CurrentCulture is null");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to get .NET locale: {e.Message}");
                return null;
            }
        }
        
        #endregion
        
#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Get Android locale via native Java implementation
        /// </summary>
        private static string GetAndroidLocale()
        {
            try
            {
                using (var pluginClass = new AndroidJavaClass("com.boostops.unity.IdentifierPlugin"))
                {
                    string locale = pluginClass.CallStatic<string>("getDeviceLocale");
                    if (!string.IsNullOrEmpty(locale))
                    {
                        Debug.Log($"[BoostOps] Android locale: {locale}");
                        return locale;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Android locale retrieval failed: {e.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// Get Android App Set ID via Java/Kotlin
        /// </summary>
        private static string GetAndroidAppSetId()
        {
            try
            {
                using (var pluginClass = new AndroidJavaClass("com.boostops.unity.IdentifierPlugin"))
                {
                    return pluginClass.CallStatic<string>("getAppSetId");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Android App Set ID plugin error: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get Google Advertising ID via Java/Kotlin
        /// </summary>
        private static string GetAndroidGAID()
        {
            try
            {
                using (var pluginClass = new AndroidJavaClass("com.boostops.unity.IdentifierPlugin"))
                {
                    return pluginClass.CallStatic<string>("getGoogleAdvertisingId");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Android GAID plugin error: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get install referrer click ID via Java/Kotlin
        /// </summary>
        private static string GetAndroidInstallReferrerClickId()
        {
            try
            {
                using (var pluginClass = new AndroidJavaClass("com.boostops.unity.IdentifierPlugin"))
                {
                    return pluginClass.CallStatic<string>("getInstallReferrerClickId");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Android Install Referrer plugin error: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get Android storefront country via system locale
        /// </summary>
        private static string GetAndroidStorefrontCountry()
        {
            try
            {
                using (var localeClass = new AndroidJavaClass("java.util.Locale"))
                {
                    var defaultLocale = localeClass.CallStatic<AndroidJavaObject>("getDefault");
                    return defaultLocale.Call<string>("getCountry");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Android storefront country error: {e.Message}");
                return null;
            }
        }
#endif
        
        #endregion
        
        #region Validation and Utilities
        
        /// <summary>
        /// Create a comprehensive identifier payload for analytics events
        /// </summary>
        /// <param name="includeInstallTimeExtras">Include install-time only identifiers (ASA token, install referrer)</param>
        /// <returns>Dictionary of all available identifiers</returns>
        public static Dictionary<string, object> CreateIdentifierPayload(bool includeInstallTimeExtras = false)
        {
            var identifiers = new Dictionary<string, object>();
            
            // Core identifiers (every call)
            string boostopsId = GetBoostOpsId();
            string installId = GetInstallId();
            string customUserId = GetCustomUserId();
            string sessionId = GenerateSessionId();
            
            // CRITICAL: Ensure install_id is NEVER null/empty (essential for revenue attribution)
            if (string.IsNullOrEmpty(installId))
            {
                Debug.LogError("[BoostOps] ❌ CreateIdentifierPayload - GetInstallId() returned null/empty! Generating fallback...");
                // Generate a new install_id as fallback
                installId = System.Guid.NewGuid().ToString("N");
                Debug.LogWarning($"[BoostOps] ⚠️ Using fallback install_id: {installId}");
                // Try to persist it
                try
                {
                    PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.INSTALL_ID, installId);
                    PlayerPrefs.Save();
                    Debug.Log("[BoostOps] ✅ Fallback install_id persisted to PlayerPrefs");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BoostOps] ❌ Failed to persist fallback install_id: {e.Message}");
                }
            }
            
            // Debug: Log install_id to verify it's being generated
            Debug.Log($"[BoostOps] CreateIdentifierPayload - install_id: {installId} (isEmpty: {string.IsNullOrEmpty(installId)})");
            
            identifiers["boostops_id"] = boostopsId;
            identifiers["install_id"] = installId;
            identifiers["custom_user_id"] = customUserId;
            identifiers["session_id"] = sessionId; // Note: Should be managed at session level
            
            // Store region
            var storefrontCountry = GetStorefrontCountry();
            if (!string.IsNullOrEmpty(storefrontCountry))
            {
                identifiers["storefront_country"] = storefrontCountry;
            }
            
#if UNITY_IOS
            // iOS-specific identifiers
            var appAccountToken = GetAppAccountToken();
            if (!string.IsNullOrEmpty(appAccountToken))
            {
                identifiers["app_account_token"] = appAccountToken;
            }
            
            var idfv = GetIDFV();
            if (!string.IsNullOrEmpty(idfv))
            {
                identifiers["idfv"] = idfv;
            }
            
            var idfa = GetIDFA();
            if (!string.IsNullOrEmpty(idfa))
            {
                identifiers["idfa"] = idfa;
            }
            
            // Install-time extras
            if (includeInstallTimeExtras)
            {
                var asaToken = GetASAAttributionToken();
                if (!string.IsNullOrEmpty(asaToken))
                {
                    identifiers["asa_token"] = asaToken;
                    // Also add as universal attribution_click_id (iOS uses ASA token as click ID)
                    identifiers["attribution_click_id"] = asaToken;
                }
                
                var skanSourceId = GetSKANSourceId();
                if (!string.IsNullOrEmpty(skanSourceId))
                {
                    identifiers["skan_source_id"] = skanSourceId;
                }
            }
#endif
            
#if UNITY_ANDROID
            // Android-specific identifiers
            var asidHash = GetASIDHash();
            if (!string.IsNullOrEmpty(asidHash))
            {
                identifiers["asid_sha256b64"] = asidHash; // Include algorithm in key name
            }
            
            var gaid = GetGAID();
            if (!string.IsNullOrEmpty(gaid))
            {
                identifiers["gaid"] = gaid;
            }
            
            // Install-time extras
            if (includeInstallTimeExtras)
            {
                var installReferrerClickId = GetInstallReferrerClickId();
                if (!string.IsNullOrEmpty(installReferrerClickId))
                {
                    identifiers["install_referrer_click_id"] = installReferrerClickId;
                    // Also add as universal attribution_click_id
                    identifiers["attribution_click_id"] = installReferrerClickId;
                }
            }
#endif
            
#if UNITY_STANDALONE_WIN
            // Windows-specific identifiers (works in both editor and builds since editor IS Windows)
            var windowsDeviceId = GetWindowsDeviceId();
            if (!string.IsNullOrEmpty(windowsDeviceId))
            {
                identifiers["windows_device_id"] = windowsDeviceId;
            }
            
            var windowsMachineGuid = GetWindowsMachineGuid();
            if (!string.IsNullOrEmpty(windowsMachineGuid))
            {
                identifiers["windows_machine_guid"] = windowsMachineGuid;
            }
            
            var windowsAdvertisingId = GetWindowsAdvertisingId();
            if (!string.IsNullOrEmpty(windowsAdvertisingId))
            {
                identifiers["msaid"] = windowsAdvertisingId;
            }
#endif
            
#if UNITY_EDITOR
            // Editor fallback: provide platform-specific simulated identifiers for testing/debugging
            try
            {
#if UNITY_IOS
                // iOS Editor simulation only
                if (!identifiers.ContainsKey("app_account_token"))
                {
                    string editorToken = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.APP_ACCOUNT_TOKEN, "");
                    if (string.IsNullOrEmpty(editorToken))
                    {
                        editorToken = System.Guid.NewGuid().ToString();
                        PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.APP_ACCOUNT_TOKEN, editorToken);
                        PlayerPrefs.Save();
                    }
                    identifiers["app_account_token"] = editorToken;
                }

                // IDFV is already handled by GetIDFV() method for iOS
                
                // Firebase App Instance ID (iOS format)
                if (!identifiers.ContainsKey("firebase_app_id"))
                {
                    identifiers["firebase_app_id"] = "1:577950095229:ios:d5d48b3ce5619a94a7de14";
                }
#elif UNITY_ANDROID
                // Android Editor simulation only
                if (!identifiers.ContainsKey("gaid"))
                {
                    var gaidSim = "00000000-0000-0000-0000-" + (SystemInfo.deviceUniqueIdentifier ?? "SIMULATOR").PadRight(12, '0').Substring(0, 12);
                    identifiers["gaid"] = gaidSim;
                }

                if (!identifiers.ContainsKey("asid_sha256"))
                {
                    var source = SystemInfo.deviceUniqueIdentifier ?? "SIMULATOR";
                    using (var sha = SHA256.Create())
                    {
                        var bytes = Encoding.UTF8.GetBytes(source);
                        var hash = sha.ComputeHash(bytes);
                        identifiers["asid_sha256"] = Convert.ToBase64String(hash);
                    }
                }

                // Firebase App Instance ID (Android format)
                if (!identifiers.ContainsKey("firebase_app_id"))
                {
                    identifiers["firebase_app_id"] = "1:577950095229:android:d5d48b3ce5619a94a7de14";
                }
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Editor identifier simulation failed: {e.Message}");
            }
#endif
            
            // Optional Firebase integration
            var firebaseAppId = GetFirebaseAppInstanceId();
            if (!string.IsNullOrEmpty(firebaseAppId))
            {
                identifiers["firebase_app_id"] = firebaseAppId;
            }
            
            return identifiers;
        }
        
        /// <summary>
        /// Validate server regex patterns for key identifiers
        /// </summary>
        /// <param name="identifiers">Identifier payload to validate</param>
        /// <returns>List of validation errors, empty if all valid</returns>
        public static List<string> ValidateIdentifierPayload(Dictionary<string, object> identifiers)
        {
            var errors = new List<string>();
            
            // Validate BoostOps ID format: ^boid_[0-9A-HJKMNP-TV-Z]{26}$
            if (identifiers.ContainsKey("boostops_id"))
            {
                string boostopsId = identifiers["boostops_id"] as string;
                if (!BoostOpsULIDGenerator.IsValidBoostOpsId(boostopsId))
                {
                    errors.Add($"Invalid boostops_id format: {boostopsId}");
                }
            }
            
            // Validate App Account Token format: ^[0-9a-fA-F-]{36}$
            if (identifiers.ContainsKey("app_account_token"))
            {
                string token = identifiers["app_account_token"] as string;
                if (!System.Text.RegularExpressions.Regex.IsMatch(token, @"^[0-9a-fA-F-]{36}$"))
                {
                    errors.Add($"Invalid app_account_token format: {token}");
                }
            }
            
            // Validate ASID hash format: ^[A-Za-z0-9+/]{43}=?$ (Base64 SHA-256)
            if (identifiers.ContainsKey("asid_sha256b64"))
            {
                string hash = identifiers["asid_sha256b64"] as string;
                if (!System.Text.RegularExpressions.Regex.IsMatch(hash, @"^[A-Za-z0-9+/]{43}=?$"))
                {
                    errors.Add($"Invalid asid_sha256b64 format: {hash}");
                }
            }
            
            return errors;
        }
        
        #endregion
    }
}