using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BoostOps
{
    /// <summary>
    /// Configuration for BoostLinks™ Dynamic Links with multiple domain support
    /// DEPRECATED: This class has been consolidated into BoostOpsProjectSettings. Use BoostOpsProjectSettings instead.
    /// </summary>
    [System.Obsolete("BoostOpsDynamicLinksConfig has been consolidated into BoostOpsProjectSettings. Use BoostOpsProjectSettings.GetOrCreateSettings() instead.", false)]
    [CreateAssetMenu(fileName = "BoostOpsDynamicLinksConfig", menuName = "BoostOps/Dynamic Links Config")]
    public class BoostOpsDynamicLinksConfig : ScriptableObject
    {
        [Header("Associated Domain(s)")]
        [SerializeField] 
        [Tooltip("Your domains for BoostLinks™ (e.g., game.example.com or yourslug.boostlink.me)")]
        private List<string> domains = new List<string>();
        
        [Header("Configuration")]
        [SerializeField] 
        [Tooltip("Validate hosts and generate AASA/assetlinks files for all domains")]
        private bool validateAllHosts = true;
        
        // Legacy fields for backward compatibility (hidden from inspector)
        [SerializeField, HideInInspector] 
        private string primaryHost = "";
        [SerializeField, HideInInspector] 
#pragma warning disable CS0414 // Field assigned but never used - legacy compatibility field
        private bool enableMultipleHosts = false;
#pragma warning restore CS0414
        [SerializeField, HideInInspector] 
        private List<string> additionalHosts = new List<string>();
        
        // Constants
        public const int MAX_DOMAINS = 5;
        
        // Properties
        public List<string> Domains => new List<string>(domains ?? new List<string>());
        public bool ValidateAllHosts => validateAllHosts;
        
        /// <summary>
        /// Get all configured domains
        /// </summary>
        public List<string> GetAllHosts()
        {
            // Migrate from legacy format if needed
            MigrateLegacyFormat();
            
            return domains?.Where(d => !string.IsNullOrEmpty(d)).Distinct().ToList() ?? new List<string>();
        }
        
        /// <summary>
        /// Migrate from legacy primaryHost/additionalHosts format to new domains list
        /// </summary>
        private void MigrateLegacyFormat()
        {
            // Check if we have legacy data and no new data
            if ((domains == null || domains.Count == 0) && 
                (!string.IsNullOrEmpty(primaryHost) || (additionalHosts != null && additionalHosts.Count > 0)))
            {
                Debug.Log("[BoostOps] Migrating BoostOpsDynamicLinksConfig from legacy format to new domains list format.");
                
                if (domains == null)
                    domains = new List<string>();
                
                // Add primary host first
                if (!string.IsNullOrEmpty(primaryHost))
                {
                    domains.Add(primaryHost);
                }
                
                // Add additional hosts
                if (additionalHosts != null)
                {
                    foreach (var host in additionalHosts)
                    {
                        if (!string.IsNullOrEmpty(host) && !domains.Contains(host))
                        {
                            domains.Add(host);
                        }
                    }
                }
                
                // Clear legacy fields
                primaryHost = "";
                enableMultipleHosts = false;
                additionalHosts = new List<string>();
                
                // Mark as dirty to save changes
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
                
                Debug.Log($"[BoostOps] Migration complete. Now have {domains.Count} domains configured.");
            }
        }
        
        /// <summary>
        /// Get the total number of configured domains
        /// </summary>
        public int GetHostCount()
        {
            return GetAllHosts().Count;
        }
        
        /// <summary>
        /// Get the first domain (for backward compatibility with "primary" concept)
        /// </summary>
        public string PrimaryHost => GetAllHosts().FirstOrDefault() ?? "";
        
        /// <summary>
        /// Add a domain with validation
        /// </summary>
        public bool AddDomain(string domain)
        {
            if (domains == null)
                domains = new List<string>();
                
            if (domains.Count >= MAX_DOMAINS)
            {
                Debug.LogWarning($"Maximum domains ({MAX_DOMAINS}) reached.");
                return false;
            }
            
            var cleanDomain = CleanHost(domain);
            if (!ValidateHostFormat(cleanDomain))
            {
                Debug.LogWarning($"Domain '{domain}' is not a valid format.");
                return false;
            }
            
            if (domains.Contains(cleanDomain))
            {
                Debug.LogWarning($"Domain '{cleanDomain}' already exists.");
                return false;
            }
            
            domains.Add(cleanDomain);
            return true;
        }
        
        /// <summary>
        /// Remove a domain
        /// </summary>
        public bool RemoveDomain(string domain)
        {
            if (domains == null)
                return false;
                
            var cleanDomain = CleanHost(domain);
            return domains.Remove(cleanDomain);
        }
        
        /// <summary>
        /// Remove a domain by index
        /// </summary>
        public bool RemoveDomainAt(int index)
        {
            if (domains == null || index < 0 || index >= domains.Count)
                return false;
                
            domains.RemoveAt(index);
            return true;
        }
        
        /// <summary>
        /// Clear all domains
        /// </summary>
        public void ClearDomains()
        {
            if (domains == null)
                domains = new List<string>();
            else
                domains.Clear();
        }
        
        /// <summary>
        /// Set domains from a list
        /// </summary>
        public void SetDomains(List<string> newDomains)
        {
            domains = newDomains?.Where(d => !string.IsNullOrEmpty(d))
                                 .Select(CleanHost)
                                 .Where(ValidateHostFormat)
                                 .Distinct()
                                 .ToList() ?? new List<string>();
        }
        
        /// <summary>
        /// Check if a domain exists
        /// </summary>
        public bool ContainsDomain(string domain)
        {
            if (domains == null)
                return false;
                
            var cleanDomain = CleanHost(domain);
            return domains.Contains(cleanDomain);
        }
        
        /// <summary>
        /// Validate a host format
        /// </summary>
        public static bool ValidateHostFormat(string host)
        {
            if (string.IsNullOrEmpty(host))
                return false;
            
            // Remove protocol if present
            host = host.Replace("https://", "").Replace("http://", "");
            
            // Remove trailing slash
            host = host.TrimEnd('/');
            
            // Basic domain validation
            if (host.Contains(" ") || host.Contains("..") || host.StartsWith(".") || host.EndsWith("."))
                return false;
            
            // Must contain at least one dot (domain.com)
            if (!host.Contains("."))
                return false;
            
            // Check for valid characters (simplified)
            var validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-";
            if (host.Any(c => !validChars.Contains(c)))
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Clean and normalize a host (remove protocol, trailing slash, etc.)
        /// </summary>
        public static string CleanHost(string host)
        {
            if (string.IsNullOrEmpty(host))
                return "";
            
            // Remove protocol
            host = host.Replace("https://", "").Replace("http://", "");
            
            // Remove trailing slash
            host = host.TrimEnd('/');
            
            // Convert to lowercase
            host = host.ToLower();
            
            return host;
        }
        
        /// <summary>
        /// Validate the entire configuration
        /// </summary>
        public ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult();
            var allDomains = GetAllHosts();
            
            if (allDomains.Count == 0)
            {
                result.AddError("At least one domain is required");
            }
            
            if (allDomains.Count > MAX_DOMAINS)
            {
                result.AddError($"Too many domains. Maximum {MAX_DOMAINS} allowed.");
            }
            
            foreach (var domain in allDomains)
            {
                if (!ValidateHostFormat(domain))
                {
                    result.AddError($"Domain '{domain}' is not a valid domain format");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Create a default configuration
        /// </summary>
        public static BoostOpsDynamicLinksConfig CreateDefault()
        {
            var config = CreateInstance<BoostOpsDynamicLinksConfig>();
            config.domains = new List<string>();
            config.validateAllHosts = true;
            return config;
        }
        
        /// <summary>
        /// Validation result for configuration
        /// </summary>
        [System.Serializable]
        public class ValidationResult
        {
            public bool IsValid => errors.Count == 0;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
            
            public void AddError(string error)
            {
                errors.Add(error);
            }
            
            public void AddWarning(string warning)
            {
                warnings.Add(warning);
            }
            
            public string GetErrorsString()
            {
                return string.Join("\n", errors);
            }
            
            public string GetWarningsString()
            {
                return string.Join("\n", warnings);
            }
        }
    }
} 