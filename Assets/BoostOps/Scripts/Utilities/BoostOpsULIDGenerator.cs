using System;
using System.Security.Cryptography;
using System.Text;

namespace BoostOps.Core
{
    /// <summary>
    /// ULID (Universally Unique Lexicographically Sortable Identifier) generator for BoostOps
    /// 
    /// Format: boid_XXXXXXXXXXXXXXXXXXXXXXXX (26 chars after prefix)
    /// Example: boid_01HYZFFJ4SJQBF8Z4KXR0VCT7N
    /// 
    /// ULID Structure:
    /// - 48-bit timestamp (milliseconds since Unix epoch)
    /// - 80-bit randomness
    /// - Base32 Crockford encoding (case-insensitive, no ambiguous chars)
    /// - Total: 128 bits = 26 characters when encoded
    /// 
    /// Benefits over UUID:
    /// - Lexicographically sortable (timestamp first)
    /// - URL-safe without escaping
    /// - Case-insensitive
    /// - No ambiguous characters (0/O, 1/I/L)
    /// - Shorter string representation than UUID
    /// </summary>
    public static class BoostOpsULIDGenerator
    {
        /// <summary>
        /// BoostOps ID prefix for easy identification in logs and databases
        /// </summary>
        public const string BOOSTOPS_ID_PREFIX = "boid_";
        
        /// <summary>
        /// Crockford Base32 alphabet (excludes ambiguous characters)
        /// </summary>
        private const string BASE32_CHARS = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        
        /// <summary>
        /// Unix epoch reference point for timestamp calculation
        /// </summary>
        private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        /// <summary>
        /// Thread-safe random number generator
        /// </summary>
        private static readonly RNGCryptoServiceProvider cryptoRng = new RNGCryptoServiceProvider();
        
        /// <summary>
        /// Generate a new BoostOps ID using ULID format
        /// </summary>
        /// <returns>BoostOps ID in format: boid_XXXXXXXXXXXXXXXXXXXXXXXX</returns>
        public static string GenerateBoostOpsId()
        {
            return BOOSTOPS_ID_PREFIX + GenerateULID();
        }
        
        /// <summary>
        /// Generate a ULID string (26 characters, Base32 Crockford encoded)
        /// </summary>
        /// <returns>26-character ULID string</returns>
        public static string GenerateULID()
        {
            return GenerateULIDAtTime(DateTime.UtcNow);
        }
        
        /// <summary>
        /// Generate a ULID for a specific timestamp (useful for testing)
        /// </summary>
        /// <param name="timestamp">UTC timestamp to use</param>
        /// <returns>26-character ULID string</returns>
        public static string GenerateULIDAtTime(DateTime timestamp)
        {
            // Calculate milliseconds since Unix epoch (48 bits)
            long timestampMs = (long)(timestamp - UNIX_EPOCH).TotalMilliseconds;
            
            // Generate 80 bits of randomness (10 bytes)
            byte[] randomBytes = new byte[10];
            cryptoRng.GetBytes(randomBytes);
            
            // Combine timestamp (6 bytes) and randomness (10 bytes) = 16 bytes total
            byte[] ulidBytes = new byte[16];
            
            // Encode timestamp (big-endian, 48 bits = 6 bytes)
            ulidBytes[0] = (byte)(timestampMs >> 40);
            ulidBytes[1] = (byte)(timestampMs >> 32);
            ulidBytes[2] = (byte)(timestampMs >> 24);
            ulidBytes[3] = (byte)(timestampMs >> 16);
            ulidBytes[4] = (byte)(timestampMs >> 8);
            ulidBytes[5] = (byte)(timestampMs);
            
            // Copy randomness bytes
            Array.Copy(randomBytes, 0, ulidBytes, 6, 10);
            
            // Encode as Base32 Crockford
            return EncodeBase32(ulidBytes);
        }
        
        /// <summary>
        /// Validate that a string is a properly formatted BoostOps ID
        /// </summary>
        /// <param name="boostopsId">ID to validate</param>
        /// <returns>True if valid BoostOps ID format</returns>
        public static bool IsValidBoostOpsId(string boostopsId)
        {
            if (string.IsNullOrEmpty(boostopsId))
                return false;
                
            // Check prefix
            if (!boostopsId.StartsWith(BOOSTOPS_ID_PREFIX))
                return false;
                
            // Check total length (prefix + 26 ULID chars)
            if (boostopsId.Length != BOOSTOPS_ID_PREFIX.Length + 26)
                return false;
                
            // Extract ULID part and validate characters
            string ulidPart = boostopsId.Substring(BOOSTOPS_ID_PREFIX.Length);
            return IsValidULIDString(ulidPart);
        }
        
        /// <summary>
        /// Validate that a string contains only valid ULID characters
        /// </summary>
        /// <param name="ulid">ULID string to validate</param>
        /// <returns>True if valid ULID format</returns>
        public static bool IsValidULIDString(string ulid)
        {
            if (string.IsNullOrEmpty(ulid) || ulid.Length != 26)
                return false;
                
            foreach (char c in ulid.ToUpperInvariant())
            {
                if (BASE32_CHARS.IndexOf(c) == -1)
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Extract timestamp from a BoostOps ID
        /// </summary>
        /// <param name="boostopsId">BoostOps ID to decode</param>
        /// <returns>UTC timestamp when the ID was generated, or null if invalid</returns>
        public static DateTime? ExtractTimestamp(string boostopsId)
        {
            if (!IsValidBoostOpsId(boostopsId))
                return null;
                
            string ulidPart = boostopsId.Substring(BOOSTOPS_ID_PREFIX.Length);
            return ExtractTimestampFromULID(ulidPart);
        }
        
        /// <summary>
        /// Extract timestamp from a ULID string
        /// </summary>
        /// <param name="ulid">ULID string to decode</param>
        /// <returns>UTC timestamp when the ULID was generated, or null if invalid</returns>
        public static DateTime? ExtractTimestampFromULID(string ulid)
        {
            if (!IsValidULIDString(ulid))
                return null;
                
            try
            {
                // Decode first 10 characters (48-bit timestamp)
                byte[] timestampBytes = DecodeBase32Partial(ulid.Substring(0, 10));
                
                // Reconstruct timestamp (big-endian)
                long timestampMs = 0;
                for (int i = 0; i < 6; i++)
                {
                    timestampMs = (timestampMs << 8) | timestampBytes[i];
                }
                
                return UNIX_EPOCH.AddMilliseconds(timestampMs);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Encode byte array as Base32 Crockford (26 characters for 16 bytes)
        /// Standard ULID encoding: 128 bits → 26 Base32 characters
        /// </summary>
        private static string EncodeBase32(byte[] data)
        {
            if (data.Length != 16)
                throw new ArgumentException("Data must be exactly 16 bytes for ULID encoding");
            
            var result = new char[26];
            
            // ULID Base32 encoding (128 bits = 26 chars)
            // Process bits in groups of 5, reading from most significant to least significant
            result[0] = BASE32_CHARS[(data[0] & 0xE0) >> 5];
            result[1] = BASE32_CHARS[data[0] & 0x1F];
            result[2] = BASE32_CHARS[(data[1] & 0xF8) >> 3];
            result[3] = BASE32_CHARS[((data[1] & 0x07) << 2) | ((data[2] & 0xC0) >> 6)];
            result[4] = BASE32_CHARS[(data[2] & 0x3E) >> 1];
            result[5] = BASE32_CHARS[((data[2] & 0x01) << 4) | ((data[3] & 0xF0) >> 4)];
            result[6] = BASE32_CHARS[((data[3] & 0x0F) << 1) | ((data[4] & 0x80) >> 7)];
            result[7] = BASE32_CHARS[(data[4] & 0x7C) >> 2];
            result[8] = BASE32_CHARS[((data[4] & 0x03) << 3) | ((data[5] & 0xE0) >> 5)];
            result[9] = BASE32_CHARS[data[5] & 0x1F];
            
            result[10] = BASE32_CHARS[(data[6] & 0xF8) >> 3];
            result[11] = BASE32_CHARS[((data[6] & 0x07) << 2) | ((data[7] & 0xC0) >> 6)];
            result[12] = BASE32_CHARS[(data[7] & 0x3E) >> 1];
            result[13] = BASE32_CHARS[((data[7] & 0x01) << 4) | ((data[8] & 0xF0) >> 4)];
            result[14] = BASE32_CHARS[((data[8] & 0x0F) << 1) | ((data[9] & 0x80) >> 7)];
            result[15] = BASE32_CHARS[(data[9] & 0x7C) >> 2];
            result[16] = BASE32_CHARS[((data[9] & 0x03) << 3) | ((data[10] & 0xE0) >> 5)];
            result[17] = BASE32_CHARS[data[10] & 0x1F];
            result[18] = BASE32_CHARS[(data[11] & 0xF8) >> 3];
            result[19] = BASE32_CHARS[((data[11] & 0x07) << 2) | ((data[12] & 0xC0) >> 6)];
            
            result[20] = BASE32_CHARS[(data[12] & 0x3E) >> 1];
            result[21] = BASE32_CHARS[((data[12] & 0x01) << 4) | ((data[13] & 0xF0) >> 4)];
            result[22] = BASE32_CHARS[((data[13] & 0x0F) << 1) | ((data[14] & 0x80) >> 7)];
            result[23] = BASE32_CHARS[(data[14] & 0x7C) >> 2];
            result[24] = BASE32_CHARS[((data[14] & 0x03) << 3) | ((data[15] & 0xE0) >> 5)];
            result[25] = BASE32_CHARS[data[15] & 0x1F];
            
            return new string(result);
        }
        
        /// <summary>
        /// Decode partial Base32 string to bytes (for timestamp extraction)
        /// </summary>
        private static byte[] DecodeBase32Partial(string base32)
        {
            // This is a simplified decoder for timestamp extraction only
            // Full ULID decoding would be more complex
            var result = new byte[6]; // 48 bits = 6 bytes
            long accumulator = 0;
            int bits = 0;
            int byteIndex = 0;
            
            foreach (char c in base32.ToUpperInvariant())
            {
                int value = BASE32_CHARS.IndexOf(c);
                if (value == -1)
                    throw new ArgumentException($"Invalid Base32 character: {c}");
                    
                accumulator = (accumulator << 5) | (uint)value;
                bits += 5;
                
                if (bits >= 8)
                {
                    result[byteIndex++] = (byte)(accumulator >> (bits - 8));
                    bits -= 8;
                    accumulator &= (1 << bits) - 1;
                    
                    if (byteIndex >= 6)
                        break;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Generate a session ID (shorter format for session tracking)
        /// Format: sess_XXXXXXXX (8 hex characters = 32 bits of randomness)
        /// </summary>
        /// <returns>Session ID in format: sess_XXXXXXXX</returns>
        public static string GenerateSessionId()
        {
            byte[] randomBytes = new byte[4]; // 32 bits
            cryptoRng.GetBytes(randomBytes);
            
            string hexString = BitConverter.ToString(randomBytes).Replace("-", "").ToLowerInvariant();
            return $"sess_{hexString}";
        }
        
        /// <summary>
        /// Generate an App Account Token (iOS UUID format)
        /// Format: Standard UUID v4 (36 characters with hyphens)
        /// Example: c56ab0b8-7f9e-46da-9c80-a9d7bdc8e36e
        /// </summary>
        /// <returns>UUID v4 string for iOS App Account Token</returns>
        public static string GenerateAppAccountToken()
        {
            return Guid.NewGuid().ToString();
        }
    }
}