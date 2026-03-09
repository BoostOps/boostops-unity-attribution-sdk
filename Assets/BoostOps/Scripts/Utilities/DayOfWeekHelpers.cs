using System;
using System.Collections.Generic;

namespace BoostOps.Core
{
    /// <summary>
    /// Day-of-Week utilities for BoostOps SDK
    /// Convention: Sunday = 0, Monday = 1, ..., Saturday = 6
    /// Matches Unity Analytics, Meta, and GA4 for direct joins
    /// </summary>
    public static class DayOfWeekHelpers
    {
        /// <summary>
        /// Convert current day to BoostOps 0-6 format (Sunday = 0)
        /// </summary>
        public static int GetCurrentDayOfWeek()
        {
            return (int)DateTime.Now.DayOfWeek; // Native C# cast, no math required
        }
        
        /// <summary>
        /// Convert string day names to 0-6 format for Remote Config ingestion
        /// Accepts: "MONDAY", "TUESDAY", etc. (case insensitive)
        /// </summary>
        public static int[] ConvertStringArrayToInts(string[] dayNames)
        {
            if (dayNames == null || dayNames.Length == 0)
                return new int[0]; // Empty = all days
                
            var result = new List<int>();
            var dayMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                {"SUNDAY", 0}, {"SUN", 0},
                {"MONDAY", 1}, {"MON", 1},
                {"TUESDAY", 2}, {"TUE", 2}, {"TUES", 2},
                {"WEDNESDAY", 3}, {"WED", 3},
                {"THURSDAY", 4}, {"THU", 4}, {"THUR", 4}, {"THURS", 4},
                {"FRIDAY", 5}, {"FRI", 5},
                {"SATURDAY", 6}, {"SAT", 6}
            };
            
            foreach (string dayName in dayNames)
            {
                if (dayMap.TryGetValue(dayName.Trim(), out int dayValue))
                {
                    if (!result.Contains(dayValue))
                        result.Add(dayValue);
                }
            }
            
            result.Sort();
            return result.ToArray();
        }
        
        /// <summary>
        /// Validate and normalize day-of-week array for Remote Config/REST input
        /// Handles both int arrays (0-6) and string arrays ("MONDAY", etc.)
        /// </summary>
        public static int[] NormalizeDaysOfWeek(object input)
        {
            if (input == null)
                return new int[0]; // Empty = all days
                
            // Handle int array (already 0-6 format)
            if (input is int[] intArray)
            {
                var valid = new List<int>();
                foreach (int day in intArray)
                {
                    if (day >= 0 && day <= 6 && !valid.Contains(day))
                        valid.Add(day);
                }
                valid.Sort();
                return valid.ToArray();
            }
            
            // Handle string array ("MONDAY", "TUESDAY", etc.)
            if (input is string[] stringArray)
            {
                return ConvertStringArrayToInts(stringArray);
            }
            
            // Handle single values
            if (input is int singleInt && singleInt >= 0 && singleInt <= 6)
            {
                return new int[] { singleInt };
            }
            
            if (input is string singleString)
            {
                return ConvertStringArrayToInts(new string[] { singleString });
            }
            
            return new int[0]; // Default: all days
        }
        
        /// <summary>
        /// Helper functions for BI/warehouse layer compatibility
        /// </summary>
        public static class BI
        {
            /// <summary>
            /// Convert 0-6 format to ISO format (Monday = 1, Sunday = 7)
            /// </summary>
            public static int ToISO(int boostOpsDayOfWeek)
            {
                return boostOpsDayOfWeek == 0 ? 7 : boostOpsDayOfWeek;
            }
            
            /// <summary>
            /// Convert 0-6 format to human-readable name
            /// </summary>
            public static string ToDayName(int boostOpsDayOfWeek)
            {
                string[] dayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
                return (boostOpsDayOfWeek >= 0 && boostOpsDayOfWeek <= 6) 
                    ? dayNames[boostOpsDayOfWeek] 
                    : "Unknown";
            }
            
            /// <summary>
            /// Generate SQL helper for warehouse/BI queries
            /// Usage: SELECT dow_zero, BI.ToISO(dow_zero) as dow_iso, BI.ToDayName(dow_zero) as dow_name
            /// </summary>
            public static string GetSQLHelperExample()
            {
                return @"
-- BoostOps Day-of-Week SQL Helper Functions
-- dow_zero: 0-6 internal format (Sunday = 0)
-- dow_iso: ISO format (Monday = 1, Sunday = 7)  
-- dow_name: Human readable names

SELECT 
    dow_zero,
    CASE WHEN dow_zero = 0 THEN 7 ELSE dow_zero END as dow_iso,
    CASE dow_zero
        WHEN 0 THEN 'Sun'
        WHEN 1 THEN 'Mon' 
        WHEN 2 THEN 'Tue'
        WHEN 3 THEN 'Wed'
        WHEN 4 THEN 'Thu'
        WHEN 5 THEN 'Fri'
        WHEN 6 THEN 'Sat'
        ELSE 'Unknown'
    END as dow_name
FROM analytics_events;
";
            }
        }
    }
} 