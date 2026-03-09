using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Utilities for detecting the app's runtime environment
    /// Useful for analytics segmentation, debugging, and conditional features
    /// </summary>
    public static class BoostOpsEnvironment
    {
        /// <summary>
        /// Check if the app is running in TestFlight (iOS only)
        /// Useful for separating test data from production analytics
        /// </summary>
        /// <returns>True if running in TestFlight, false otherwise</returns>
        public static bool IsTestFlight()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            return BoostOpsRevenueTrackerNative.IsTestFlightEnvironment();
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// Get the current runtime environment
        /// </summary>
        /// <returns>
        /// iOS: "production", "testflight", "development", "simulator", "adhoc"
        /// Android: "google_play"
        /// macOS: "macos_production" or "standalone"
        /// Editor: "editor"
        /// </returns>
        public static string GetEnvironment()
        {
            #if UNITY_EDITOR
            return "editor";
            #elif UNITY_IOS
            return BoostOpsRevenueTrackerNative.GetAppStoreEnvironment();
            #elif UNITY_ANDROID
            return "google_play";
            #elif UNITY_STANDALONE_OSX
            // Check if installed from Mac App Store
            try
            {
                string receiptPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Contents/_MASReceipt/receipt");
                if (System.IO.File.Exists(receiptPath))
                {
                    return "macos_production"; // Mac App Store production build
                }
            }
            catch
            {
                // If we can't check, assume standalone
            }
            return "standalone";  // Direct download or other distribution
            #else
            return "unknown";
            #endif
        }
        
        /// <summary>
        /// Check if running in a production environment
        /// (App Store or Google Play release build)
        /// </summary>
        public static bool IsProduction()
        {
            string env = GetEnvironment();
            return env == "production" || env == "google_play" || env == "macos_production";
        }
        
        /// <summary>
        /// Check if running in a development/debug environment
        /// </summary>
        public static bool IsDevelopment()
        {
            #if DEBUG
            return true;
            #else
            string env = GetEnvironment();
            return env == "development" || env == "simulator" || env == "editor";
            #endif
        }
        
        /// <summary>
        /// Check if running in Unity Editor
        /// </summary>
        public static bool IsEditor()
        {
            #if UNITY_EDITOR
            return true;
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// Check if running on a real device (not simulator/editor)
        /// </summary>
        public static bool IsRealDevice()
        {
            #if UNITY_EDITOR
            return false;
            #elif UNITY_ANDROID
            return !IsEmulator();
            #else
            string env = GetEnvironment();
            return env != "simulator" && env != "editor" && env != "emulator";
            #endif
        }
        
        /// <summary>
        /// Check if running on an emulator (Android/iOS simulator)
        /// </summary>
        public static bool IsEmulator()
        {
            #if UNITY_EDITOR
            return false;
            #elif UNITY_ANDROID
            return BoostOpsRevenueTrackerNative.IsEmulator();
            #elif UNITY_IOS
            return GetEnvironment() == "simulator";
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// Check if this is a debug/debuggable build (Android)
        /// For iOS, use IsDevelopment() instead
        /// </summary>
        public static bool IsDebugBuild()
        {
            #if DEBUG
            return true;
            #elif UNITY_ANDROID
            return BoostOpsRevenueTrackerNative.IsDebugBuild();
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// Get the app installer source
        /// Returns: "google_play", "amazon_appstore", "sideload", "unknown", etc.
        /// iOS returns "app_store"
        /// macOS returns "app_store" or "standalone"
        /// </summary>
        public static string GetInstallerSource()
        {
            #if UNITY_ANDROID
            return BoostOpsRevenueTrackerNative.GetInstallerPackageName();
            #elif UNITY_IOS
            return "app_store";
            #elif UNITY_STANDALONE_OSX
            // Check if installed from Mac App Store
            try
            {
                string receiptPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Contents/_MASReceipt/receipt");
                if (System.IO.File.Exists(receiptPath))
                {
                    return "app_store";  // Mac App Store (matches iOS convention)
                }
            }
            catch
            {
                // If we can't check, assume standalone
            }
            return "standalone";  // Direct download or other distribution
            #elif UNITY_EDITOR
            return "editor";
            #else
            return "unknown";
            #endif
        }
        
        /// <summary>
        /// Check if installed from Google Play Store (Android only)
        /// </summary>
        public static bool IsGooglePlayInstall()
        {
            #if UNITY_ANDROID
            return GetInstallerSource() == "google_play";
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// Check if the app was sideloaded (not from an official store)
        /// </summary>
        public static bool IsSideloaded()
        {
            #if UNITY_ANDROID
            string installer = GetInstallerSource();
            return installer == "sideload" || installer == "unknown";
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// Get a detailed environment report for debugging
        /// </summary>
        public static string GetEnvironmentReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== BoostOps Environment Report ===");
            report.AppendLine($"Platform: {Application.platform}");
            report.AppendLine($"Environment: {GetEnvironment()}");
            report.AppendLine($"Is Production: {IsProduction()}");
            report.AppendLine($"Is Development: {IsDevelopment()}");
            report.AppendLine($"Is TestFlight: {IsTestFlight()}");
            report.AppendLine($"Is Real Device: {IsRealDevice()}");
            report.AppendLine($"Is Emulator: {IsEmulator()}");
            report.AppendLine($"Unity Version: {Application.unityVersion}");
            report.AppendLine($"Device Model: {SystemInfo.deviceModel}");
            report.AppendLine($"OS: {SystemInfo.operatingSystem}");
            
            #if DEBUG
            report.AppendLine($"Build Type: DEBUG");
            #else
            report.AppendLine($"Build Type: RELEASE");
            #endif
            
            #if UNITY_ANDROID
            report.AppendLine($"Is Debug Build: {IsDebugBuild()}");
            report.AppendLine($"Installer Source: {GetInstallerSource()}");
            report.AppendLine($"Is Google Play: {IsGooglePlayInstall()}");
            report.AppendLine($"Is Sideloaded: {IsSideloaded()}");
            #endif
            
            return report.ToString();
        }
        
        /// <summary>
        /// Log environment information to console (useful for debugging)
        /// </summary>
        public static void LogEnvironmentInfo()
        {
            Debug.Log(GetEnvironmentReport());
        }
    }
}

