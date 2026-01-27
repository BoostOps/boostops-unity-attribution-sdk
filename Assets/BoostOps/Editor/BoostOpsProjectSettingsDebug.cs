#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

namespace BoostOps.Editor
{
    /// <summary>
    /// Debug and validation utilities for BoostOpsProjectSettings
    /// Provides functionality that was removed from the bare bones generated settings file
    /// </summary>
    public static class BoostOpsProjectSettingsDebug
    {
        /// <summary>
        /// Log current settings configuration for debugging
        /// </summary>
        public static void LogCurrentSettings()
        {
            var settings = BoostOpsProjectSettings.GetInstance();
            Debug.Log($"[BoostOps] Project Settings Configuration:");
            Debug.Log($"[BoostOps] - Apple App Store ID: {settings?.appleAppStoreId ?? "NOT SET"}");
            Debug.Log($"[BoostOps] - Android Package: {settings?.androidPackageName ?? "NOT SET"}");
            Debug.Log($"[BoostOps] - Android Cert Fingerprint: {(!string.IsNullOrEmpty(settings?.androidCertFingerprint) ? "***SET***" : "NOT SET")}");
            Debug.Log($"[BoostOps] - Amazon Store ID: {settings?.amazonStoreId ?? "NOT SET"}");
            Debug.Log($"[BoostOps] - Microsoft Store ID: {settings?.windowsStoreId ?? "NOT SET"}");
            Debug.Log($"[BoostOps] - Samsung Store ID: {settings?.samsungStoreId ?? "NOT SET"}");
            Debug.Log($"[BoostOps] - Project Slug: {settings?.projectSlug ?? "NOT SET"}");
            Debug.Log($"[BoostOps] - Custom Domain: {settings?.customDomain ?? "NOT SET"}");
            Debug.Log($"[BoostOps] - BoostOps Analytics: {(settings?.useRemoteManagement == true ? "Enabled (remote mode)" : "Disabled (local mode)")}");
            Debug.Log($"[BoostOps] - Firebase Analytics: {settings?.firebaseAnalytics ?? false}");
            Debug.Log($"[BoostOps] - Unity Analytics: {settings?.unityAnalytics ?? false}");
        }
        
        /// <summary>
        /// Validate settings and return any issues
        /// </summary>
        public static List<string> ValidateSettings()
        {
            var issues = new List<string>();
            var settings = BoostOpsProjectSettings.GetInstance();
            
            if (string.IsNullOrEmpty(settings?.androidPackageName))
            {
                issues.Add("Android package name is not set");
            }
            
            if (string.IsNullOrEmpty(settings?.projectSlug))
            {
                issues.Add("Project slug is required for Dynamic Links");
            }
            
            return issues;
        }
        
        /// <summary>
        /// Log validation results
        /// </summary>
        public static bool LogValidationResults()
        {
            var issues = ValidateSettings();
            
            if (issues.Count == 0)
            {
                Debug.Log("[BoostOps] ✅ Project settings validation passed - no issues found");
                return true;
            }
            else
            {
                Debug.LogWarning($"[BoostOps] ⚠️ Project settings validation found {issues.Count} issue(s):");
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"[BoostOps] - {issue}");
                }
                return false;
            }
        }
    }
}
#endif 