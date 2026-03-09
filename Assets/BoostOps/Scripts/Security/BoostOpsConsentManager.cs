using System;
using UnityEngine;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Manages user privacy consent for GDPR/CCPA/privacy law compliance.
    /// Integrates with app's consent management system or provides standalone consent tracking.
    /// </summary>
    public class BoostOpsConsentManager : MonoBehaviour
    {
        private static BoostOpsConsentManager _instance;
        
        public static BoostOpsConsentManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<BoostOpsConsentManager>();
                    if (_instance == null)
                    {
                        // Create a new instance if none exists
                        var go = new GameObject("BoostOpsConsentManager");
                        _instance = go.AddComponent<BoostOpsConsentManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        // ✅ Region detection removed - developers determine GDPR status themselves
        // SDK provides only consent management APIs (see below)

        #region Consent Status

        /// <summary>
        /// Check if user has granted consent for analytics/measurement
        /// </summary>
        public bool HasAnalyticsConsent()
        {
            return PlayerPrefs.GetInt("BoostOps_Consent_Analytics", GetDefaultConsentValue()) == 1;
        }

        /// <summary>
        /// Check if user has granted consent for marketing/advertising
        /// </summary>
        public bool HasMarketingConsent()
        {
            return PlayerPrefs.GetInt("BoostOps_Consent_Marketing", GetDefaultConsentValue()) == 1;
        }

        /// <summary>
        /// Check if user has granted consent for personalized content
        /// </summary>
        public bool HasPersonalizationConsent()
        {
            return PlayerPrefs.GetInt("BoostOps_Consent_Personalization", GetDefaultConsentValue()) == 1;
        }

        /// <summary>
        /// Check if user has granted consent for data sharing with partners
        /// </summary>
        public bool HasDataSharingConsent()
        {
            return PlayerPrefs.GetInt("BoostOps_Consent_DataSharing", GetDefaultConsentValue()) == 1;
        }

        private int GetDefaultConsentValue()
        {
            // Default to "no consent" (0) - developers must explicitly enable consent
            // This is the safest approach for privacy compliance
            return 0;
        }

        #endregion

        #region Consent Metadata

        /// <summary>
        /// Get timestamp when consent was last recorded
        /// </summary>
        public long? GetConsentTimestamp()
        {
            var timestamp = PlayerPrefs.GetString("BoostOps_Consent_Timestamp", "");
            if (long.TryParse(timestamp, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// Get version of consent policy user agreed to
        /// </summary>
        public string GetConsentVersion()
        {
            return PlayerPrefs.GetString("BoostOps_Consent_Version", "1.0");
        }

        /// <summary>
        /// Get language consent was presented in
        /// </summary>
        public string GetConsentLanguage()
        {
            return PlayerPrefs.GetString("BoostOps_Consent_Language", "en");
        }

        /// <summary>
        /// Get method used to collect consent
        /// </summary>
        public string GetConsentMethod()
        {
            return PlayerPrefs.GetString("BoostOps_Consent_Method", "implicit");
        }

        /// <summary>
        /// Get source where consent was collected
        /// </summary>
        public string GetConsentSource()
        {
            return PlayerPrefs.GetString("BoostOps_Consent_Source", "first_launch");
        }

        /// <summary>
        /// Get legal basis for data processing (GDPR Article 6)
        /// </summary>
        public string GetLegalBasis()
        {
            // Return "consent" if explicitly granted, otherwise "legitimate_interest"
            // Developer determines which legal basis applies based on user's region
            return HasAnalyticsConsent() ? "consent" : "legitimate_interest";
        }

        #endregion

        #region Consent Withdrawal

        /// <summary>
        /// Get timestamp when consent was withdrawn (if applicable)
        /// </summary>
        public long? GetWithdrawalTimestamp()
        {
            var timestamp = PlayerPrefs.GetString("BoostOps_Consent_WithdrawalTimestamp", "");
            if (long.TryParse(timestamp, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// Get method used to withdraw consent
        /// </summary>
        public string GetWithdrawalMethod()
        {
            return PlayerPrefs.GetString("BoostOps_Consent_WithdrawalMethod", "");
        }

        #endregion

        #region Public API for App Integration

        /// <summary>
        /// Set analytics consent status (for app integration)
        /// </summary>
        public void SetAnalyticsConsent(bool granted, string method = "api", string source = "app")
        {
            PlayerPrefs.SetInt("BoostOps_Consent_Analytics", granted ? 1 : 0);
            UpdateConsentMetadata(method, source);
        }

        /// <summary>
        /// Set marketing consent status (for app integration)
        /// </summary>
        public void SetMarketingConsent(bool granted, string method = "api", string source = "app")
        {
            PlayerPrefs.SetInt("BoostOps_Consent_Marketing", granted ? 1 : 0);
            UpdateConsentMetadata(method, source);
        }

        /// <summary>
        /// Set all consent categories at once (for consent banner integration)
        /// </summary>
        public void SetAllConsent(bool analytics, bool marketing, bool personalization, bool dataSharing, 
            string method = "banner", string source = "consent_modal", string version = "1.0", string language = "en")
        {
            PlayerPrefs.SetInt("BoostOps_Consent_Analytics", analytics ? 1 : 0);
            PlayerPrefs.SetInt("BoostOps_Consent_Marketing", marketing ? 1 : 0);
            PlayerPrefs.SetInt("BoostOps_Consent_Personalization", personalization ? 1 : 0);
            PlayerPrefs.SetInt("BoostOps_Consent_DataSharing", dataSharing ? 1 : 0);
            
            PlayerPrefs.SetString("BoostOps_Consent_Method", method);
            PlayerPrefs.SetString("BoostOps_Consent_Source", source);
            PlayerPrefs.SetString("BoostOps_Consent_Version", version);
            PlayerPrefs.SetString("BoostOps_Consent_Language", language);
            PlayerPrefs.SetString("BoostOps_Consent_Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Withdraw all consent (for GDPR compliance)
        /// </summary>
        public void WithdrawAllConsent(string method = "settings", string source = "privacy_settings")
        {
            PlayerPrefs.SetInt("BoostOps_Consent_Analytics", 0);
            PlayerPrefs.SetInt("BoostOps_Consent_Marketing", 0);
            PlayerPrefs.SetInt("BoostOps_Consent_Personalization", 0);
            PlayerPrefs.SetInt("BoostOps_Consent_DataSharing", 0);
            
            PlayerPrefs.SetString("BoostOps_Consent_WithdrawalTimestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.SetString("BoostOps_Consent_WithdrawalMethod", method);
            
            PlayerPrefs.Save();
        }

        private void UpdateConsentMetadata(string method, string source)
        {
            PlayerPrefs.SetString("BoostOps_Consent_Method", method);
            PlayerPrefs.SetString("BoostOps_Consent_Source", source);
            PlayerPrefs.SetString("BoostOps_Consent_Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.Save();
        }

        #endregion
    }
}