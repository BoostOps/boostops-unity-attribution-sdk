using System;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Simple helper for generating impression IDs
    /// Impression IDs are stored directly on display objects, not cached
    /// </summary>
    public static class BoostOpsImpressionTracker
    {
        /// <summary>
        /// Generate a new impression ID
        /// </summary>
        /// <returns>Impression ID (e.g., "A1B2C3D4E5F6789012345678901234AB")</returns>
        public static string GenerateImpressionId()
        {
            // Generate raw UUID without dashes (32 characters, uppercase for consistency)
            return System.Guid.NewGuid().ToString("N").ToUpper();
        }
        
        /// <summary>
        /// Calculate time since impression
        /// </summary>
        /// <param name="impressionTimestamp">Impression timestamp in milliseconds</param>
        /// <returns>Milliseconds elapsed since impression</returns>
        public static int CalculateTimeToClick(long impressionTimestamp)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return (int)(now - impressionTimestamp);
        }
    }
}
