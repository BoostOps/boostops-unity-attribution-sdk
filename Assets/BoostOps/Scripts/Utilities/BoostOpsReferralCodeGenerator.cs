using UnityEngine;
using System.Linq;

namespace BoostOps.Utils
{
    /// <summary>
    /// Generates and manages referral codes using Base32 format (Crockford's Base32)
    /// 
    /// Format: 8-character uppercase Base32
    /// Character set: ABCDEFGHJKLMNPQRSTUVWXYZ23456789 (32 chars)
    /// Excludes: 0, 1, I, L, O (confusing characters)
    /// Example: R7XK2PQM
    /// 
    /// Features:
    /// - Client-side generation (no server required)
    /// - Persistent across sessions (stored in PlayerPrefs)
    /// - URL-friendly (no encoding needed)
    /// - Easy to read and communicate
    /// - 1.1 trillion unique combinations (8 chars)
    /// </summary>
    public static class BoostOpsReferralCodeGenerator
    {
        // Base32 character set (Crockford's Base32)
        // Excludes: 0, O, I, 1, L (confusing characters)
        private const string BASE32_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        
        // Default code length
        private const int DEFAULT_LENGTH = 8;
        
        // PlayerPrefs key for storing user's referral code
        private const string PREF_KEY_REFERRAL_CODE = "BoostOps_ReferralCode";
        
        /// <summary>
        /// Generate a new Base32 referral code
        /// </summary>
        /// <param name="length">Code length (default: 8 characters)</param>
        /// <returns>Generated referral code (e.g., "R7XK2PQM")</returns>
        public static string GenerateCode(int length = DEFAULT_LENGTH)
        {
            if (length < 4 || length > 16)
            {
                Debug.LogWarning($"[BoostOps] Invalid code length: {length}. Using default: {DEFAULT_LENGTH}");
                length = DEFAULT_LENGTH;
            }
            
            var result = new char[length];
            
            for (int i = 0; i < length; i++)
            {
                result[i] = BASE32_CHARS[Random.Range(0, BASE32_CHARS.Length)];
            }
            
            return new string(result);
        }
        
        /// <summary>
        /// Get or create the user's persistent referral code
        /// Code is generated once and stored locally for reuse
        /// </summary>
        /// <returns>User's referral code (e.g., "R7XK2PQM")</returns>
        public static string GetOrCreateCode()
        {
            // Check if code already exists
            if (PlayerPrefs.HasKey(PREF_KEY_REFERRAL_CODE))
            {
                string existingCode = PlayerPrefs.GetString(PREF_KEY_REFERRAL_CODE);
                
                // Validate existing code
                if (IsValidCode(existingCode))
                {
                    return existingCode;
                }
                
                Debug.LogWarning($"[BoostOps] Invalid stored referral code: {existingCode}. Generating new code.");
            }
            
            // Generate new code
            string newCode = GenerateCode();
            
            // Store for future use
            PlayerPrefs.SetString(PREF_KEY_REFERRAL_CODE, newCode);
            PlayerPrefs.Save();
            
            Debug.Log($"[BoostOps] Generated new referral code: {newCode}");
            
            return newCode;
        }
        
        /// <summary>
        /// Validate if a code matches Base32 format
        /// </summary>
        /// <param name="code">Code to validate</param>
        /// <returns>True if valid Base32 code</returns>
        public static bool IsValidCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;
            
            // Check length (typically 6-10 chars, but accept wider range)
            if (code.Length < 4 || code.Length > 16)
                return false;
            
            // Check all characters are in Base32 set
            return code.All(c => BASE32_CHARS.Contains(c));
        }
        
        /// <summary>
        /// Normalize a referral code to standard format
        /// Handles various input formats: lowercase, spaces, hyphens
        /// </summary>
        /// <param name="code">Code to normalize</param>
        /// <returns>Normalized uppercase Base32 code</returns>
        public static string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;
            
            // Convert to uppercase
            code = code.ToUpper();
            
            // Remove common separators
            code = code.Replace("-", "")
                       .Replace(" ", "")
                       .Replace("_", "");
            
            // Remove prefix if present
            if (code.StartsWith("#") || code.StartsWith("@"))
            {
                code = code.Substring(1);
            }
            
            return code;
        }
        
        /// <summary>
        /// Reset the stored referral code (for testing)
        /// </summary>
        public static void ResetCode()
        {
            PlayerPrefs.DeleteKey(PREF_KEY_REFERRAL_CODE);
            PlayerPrefs.Save();
            Debug.Log("[BoostOps] Referral code reset");
        }
        
        /// <summary>
        /// Check if user has a stored referral code
        /// </summary>
        /// <returns>True if code exists</returns>
        public static bool HasStoredCode()
        {
            return PlayerPrefs.HasKey(PREF_KEY_REFERRAL_CODE);
        }
        
        /// <summary>
        /// Get the stored referral code without generating a new one
        /// </summary>
        /// <returns>Stored code or null if none exists</returns>
        public static string GetStoredCode()
        {
            if (PlayerPrefs.HasKey(PREF_KEY_REFERRAL_CODE))
            {
                return PlayerPrefs.GetString(PREF_KEY_REFERRAL_CODE);
            }
            
            return null;
        }
        
        /// <summary>
        /// Set a custom referral code (useful for server-assigned codes)
        /// </summary>
        /// <param name="code">Custom code to store</param>
        /// <returns>True if code was valid and stored</returns>
        public static bool SetCustomCode(string code)
        {
            code = NormalizeCode(code);
            
            if (!IsValidCode(code))
            {
                Debug.LogError($"[BoostOps] Invalid custom referral code: {code}");
                return false;
            }
            
            PlayerPrefs.SetString(PREF_KEY_REFERRAL_CODE, code);
            PlayerPrefs.Save();
            
            Debug.Log($"[BoostOps] Custom referral code set: {code}");
            
            return true;
        }
    }
}

