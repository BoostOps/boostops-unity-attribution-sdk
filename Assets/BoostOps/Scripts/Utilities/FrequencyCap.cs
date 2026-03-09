using System;
using UnityEngine;

namespace BoostOps.Core
{
    /// <summary>
    /// Time unit for frequency capping
    /// Matches Google Ads and Facebook APIs for consistency
    /// </summary>
    [Serializable]
    public enum FrequencyCapTimeUnit
    {
        DAY = 0,        // Daily cap
        WEEK = 1,       // Weekly cap  
        MONTH = 2,      // Monthly cap
        LIFETIME = 3,   // Lifetime cap
        SESSION = 4     // Per-session cap (future extension)
    }



    /// <summary>
    /// Unified frequency cap object used throughout BoostOps SDK
    /// Self-describing format that matches Google Ads and Facebook standards
    /// Examples:
    /// - { "impressions": 3, "time_unit": "DAY" } = 3 per day
    /// - { "impressions": 10, "time_unit": "WEEK" } = 10 per week
    /// - { "impressions": 0 } = unlimited (time_unit ignored)
    /// </summary>
    [Serializable]
    public class FrequencyCap
    {
        [SerializeField] public int impressions = 0;                                           // 0 = unlimited
        [SerializeField] public FrequencyCapTimeUnit time_unit = FrequencyCapTimeUnit.DAY;   // Time window

        // Constructors
        public FrequencyCap() { }

        public FrequencyCap(int impressions, FrequencyCapTimeUnit timeUnit = FrequencyCapTimeUnit.DAY)
        {
            this.impressions = impressions;
            this.time_unit = timeUnit;
        }

        // Helper properties
        public bool IsUnlimited => impressions <= 0;
        public bool IsDaily => time_unit == FrequencyCapTimeUnit.DAY;
        public bool IsWeekly => time_unit == FrequencyCapTimeUnit.WEEK;
        public bool IsMonthly => time_unit == FrequencyCapTimeUnit.MONTH;
        public bool IsLifetime => time_unit == FrequencyCapTimeUnit.LIFETIME;
        public bool IsPerSession => time_unit == FrequencyCapTimeUnit.SESSION;
        


        // Industry standard factory methods
        public static FrequencyCap Unlimited() => new FrequencyCap(0);
        public static FrequencyCap Daily(int impressions) => new FrequencyCap(impressions, FrequencyCapTimeUnit.DAY);
        public static FrequencyCap Weekly(int impressions) => new FrequencyCap(impressions, FrequencyCapTimeUnit.WEEK);
        public static FrequencyCap Monthly(int impressions) => new FrequencyCap(impressions, FrequencyCapTimeUnit.MONTH);
        public static FrequencyCap Lifetime(int impressions) => new FrequencyCap(impressions, FrequencyCapTimeUnit.LIFETIME);



        // Human-readable description for debugging
        public override string ToString()
        {
            if (IsUnlimited) return "Unlimited";
            string unit = time_unit.ToString().ToLowerInvariant();
            return $"{impressions} per {unit}";
        }

        // Validation
        public bool IsValid()
        {
            return impressions >= 0 && Enum.IsDefined(typeof(FrequencyCapTimeUnit), time_unit);
        }

        // Get cache key for frequency cap tracking
        public string GetCacheKey(string baseKey)
        {
            if (IsUnlimited) return null; // No tracking needed
            
            string timeWindow = GetTimeWindowKey();
            return $"freq_cap_{baseKey}_{timeWindow}";
        }

        // Get time window key for cache partitioning
        private string GetTimeWindowKey()
        {
            DateTime now = DateTime.Now;
            switch (time_unit)
            {
                case FrequencyCapTimeUnit.DAY:
                    return now.ToString("yyyy-MM-dd");
                case FrequencyCapTimeUnit.WEEK:
                    // ISO week (Monday start)
                    int weekOfYear = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    return $"{now.Year}-W{weekOfYear:D2}";
                case FrequencyCapTimeUnit.MONTH:
                    return now.ToString("yyyy-MM");
                case FrequencyCapTimeUnit.LIFETIME:
                    return "lifetime";
                case FrequencyCapTimeUnit.SESSION:
                    return $"session_{DateTime.Now.Ticks}"; // Unique per session
                default:
                    return now.ToString("yyyy-MM-dd");
            }
        }

        // Equality for proper comparison
        public override bool Equals(object obj)
        {
            if (obj is FrequencyCap other)
            {
                return impressions == other.impressions && time_unit == other.time_unit;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(impressions, time_unit);
        }
    }

    /// <summary>
    /// JSON serialization helpers for frequency cap
    /// </summary>
    [Serializable]
    public class FrequencyCapJson
    {
        public int impressions;
        public string time_unit;  // "DAY", "WEEK", "MONTH", "LIFETIME", "SESSION"

        public static FrequencyCapJson FromFrequencyCap(FrequencyCap cap)
        {
            if (cap == null) return null;
            return new FrequencyCapJson
            {
                impressions = cap.impressions,
                time_unit = cap.time_unit.ToString()
            };
        }

        public FrequencyCap ToFrequencyCap()
        {
            var timeUnit = Enum.TryParse<FrequencyCapTimeUnit>(time_unit, true, out var parsedTimeUnit) ? parsedTimeUnit : FrequencyCapTimeUnit.DAY;
            return new FrequencyCap(impressions, timeUnit);
        }
    }
} 