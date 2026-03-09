using System.Collections.Generic;
using UnityEngine;

namespace BoostOps.Utilities
{
    /// <summary>
    /// Simple currency conversion helper for multi-currency revenue normalization
    /// 
    /// V1.0: Hardcoded exchange rates (top 20 currencies)
    /// V2.0+: Will be replaced by server-side conversion
    /// 
    /// Rates updated: 2025-10-17
    /// Source: European Central Bank (ECB)
    /// Update frequency: Monthly (SDK release)
    /// 
    /// NOTE: For production apps with frequent purchases, consider server-side
    /// conversion for real-time rates. This helper is sufficient for v1.
    /// </summary>
    public static class BoostOpsCurrencyHelper
    {
        /// <summary>
        /// Hardcoded exchange rates to USD (as of 2025-10-17)
        /// Key: ISO 4217 currency code
        /// Value: Rate to USD (e.g., JPY=0.0067 means ¥150 = $1 USD)
        /// 
        /// Top 20 currencies by global app revenue
        /// Covers ~95% of worldwide mobile purchases
        /// </summary>
        private static readonly Dictionary<string, decimal> ExchangeRatesToUsd = new Dictionary<string, decimal>
        {
            // Base currency
            { "USD", 1.00m },
            
            // Major currencies
            { "EUR", 1.10m },   // Euro: €1 = $1.10 USD
            { "GBP", 1.27m },   // British Pound: £1 = $1.27 USD
            { "JPY", 0.0067m }, // Japanese Yen: ¥150 = $1 USD
            { "CNY", 0.14m },   // Chinese Yuan: ¥7.2 = $1 USD
            
            // Americas
            { "CAD", 0.74m },   // Canadian Dollar
            { "BRL", 0.20m },   // Brazilian Real
            { "MXN", 0.059m },  // Mexican Peso
            { "ARS", 0.0012m }, // Argentine Peso
            
            // Europe
            { "CHF", 1.18m },   // Swiss Franc
            { "SEK", 0.096m },  // Swedish Krona
            { "NOK", 0.094m },  // Norwegian Krone
            { "DKK", 0.15m },   // Danish Krone
            { "PLN", 0.25m },   // Polish Zloty
            
            // Asia-Pacific
            { "AUD", 0.66m },   // Australian Dollar
            { "KRW", 0.00075m },// South Korean Won
            { "INR", 0.012m },  // Indian Rupee
            { "THB", 0.029m },  // Thai Baht
            { "IDR", 0.000064m },// Indonesian Rupiah
            { "HKD", 0.13m },   // Hong Kong Dollar
            { "SGD", 0.75m },   // Singapore Dollar
            { "NZD", 0.61m },   // New Zealand Dollar
            
            // Middle East
            { "SAR", 0.27m },   // Saudi Riyal
            { "AED", 0.27m },   // UAE Dirham
            { "ILS", 0.27m },   // Israeli Shekel
            { "TRY", 0.029m },  // Turkish Lira
            
            // Other
            { "ZAR", 0.055m },  // South African Rand
            { "RUB", 0.011m },  // Russian Ruble (volatile)
        };
        
        /// <summary>
        /// Convert an amount in any currency to USD using hardcoded rates
        /// 
        /// If currency is not found, returns original amount (treats as USD)
        /// This is a safe fallback that prevents conversion errors
        /// </summary>
        /// <param name="amount">Amount in local currency</param>
        /// <param name="currency">ISO 4217 currency code (e.g., "JPY", "EUR")</param>
        /// <returns>Amount in USD</returns>
        public static decimal ConvertToUsd(decimal amount, string currency)
        {
            // Already USD or null/empty
            if (string.IsNullOrEmpty(currency) || currency.ToUpper() == "USD")
                return amount;
            
            // Normalize to uppercase
            currency = currency.ToUpper();
            
            // Look up rate
            if (ExchangeRatesToUsd.TryGetValue(currency, out var rate))
            {
                var amountUsd = amount * rate;
                
                #if BOOSTOPS_DEBUG_LOGGING
                BoostOpsLogger.LogDebug("Currency", 
                    $"Converted {amount:F2} {currency} → ${amountUsd:F2} USD (rate: {rate:F6})");
                #endif
                
                return amountUsd;
            }
            
            // Unknown currency - treat as USD (safe fallback)
            Debug.LogWarning($"[BoostOps Currency] Unknown currency: {currency}, treating as USD. " +
                           $"Amount: {amount:F2}. Add to ExchangeRatesToUsd if this currency is important.");
            return amount;
        }
        
        /// <summary>
        /// Check if a currency is supported by the hardcoded rates
        /// </summary>
        public static bool IsCurrencySupported(string currency)
        {
            if (string.IsNullOrEmpty(currency))
                return false;
            
            return ExchangeRatesToUsd.ContainsKey(currency.ToUpper());
        }
        
        /// <summary>
        /// Get the exchange rate for a currency (for debugging/logging)
        /// Returns null if currency not supported
        /// </summary>
        public static decimal? GetExchangeRate(string currency)
        {
            if (string.IsNullOrEmpty(currency))
                return null;
            
            currency = currency.ToUpper();
            if (ExchangeRatesToUsd.TryGetValue(currency, out var rate))
                return rate;
            
            return null;
        }
        
        /// <summary>
        /// Get list of all supported currencies
        /// </summary>
        public static IEnumerable<string> GetSupportedCurrencies()
        {
            return ExchangeRatesToUsd.Keys;
        }
        
        /// <summary>
        /// Get count of supported currencies
        /// </summary>
        public static int GetSupportedCurrencyCount()
        {
            return ExchangeRatesToUsd.Count;
        }
    }
}

