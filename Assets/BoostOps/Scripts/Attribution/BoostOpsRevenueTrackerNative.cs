using System;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Platform environment detection utilities
    /// NOTE: Automatic native purchase tracking has been REMOVED to match industry standards (explicit tracking only)
    /// All purchase tracking now uses explicit BoostOpsSDK.TrackPurchase() calls in your IAP ProcessPurchase() method
    /// This class only provides environment detection (TestFlight, debug builds, emulator, etc.) for analytics
    /// </summary>
    public static class BoostOpsRevenueTrackerNative
    {
        // NOTE: All automatic purchase tracking methods have been removed
        // Use explicit tracking: BoostOpsSDK.TrackPurchase() in your ProcessPurchase() method
        
        #region Environment Detection (Analytics Only)
        
        /// <summary>
        /// Check if running in TestFlight environment (iOS only)
        /// Used for analytics routing - TestFlight events go to debug tables
        /// 
        /// NOTE: TestFlight detection is handled via native iOS plugin
        /// This method is a fallback that checks for sandbox/genuine indicators
        /// </summary>
        public static bool IsTestFlightEnvironment()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            // Check for TestFlight indicators
            // Note: Application.sandboxType doesn't have "Development" - it's for sandbox integrity
            // For TestFlight, we check if the app is not genuine (TestFlight apps aren't signed properly)
            return Application.genuine == false;
            #else
            return false; // TestFlight is iOS-only
            #endif
        }
        
        /// <summary>
        /// Get app store environment (iOS: appstore/testflight, Android: production/debug)
        /// Used for analytics categorization
        /// 
        /// NOTE: iOS "development" builds (Xcode direct install) are detected by Debug.isDebugBuild
        /// </summary>
        public static string GetAppStoreEnvironment()
        {
            #if UNITY_EDITOR
            return "editor";
            #elif UNITY_IOS
            if (IsTestFlightEnvironment())
            {
                return "testflight";
            }
            // Check if debug build (Xcode direct install)
            return Debug.isDebugBuild ? "development" : "appstore";
            #elif UNITY_ANDROID
            return IsDebugBuild() ? "debug" : "production";
            #else
            return "unknown";
            #endif
        }
        
        /// <summary>
        /// Check if this is a debug/debuggable build
        /// Android: Checks ApplicationInfo.FLAG_DEBUGGABLE
        /// iOS/Others: Checks Debug.isDebugBuild
        /// </summary>
        public static bool IsDebugBuild()
        {
            #if UNITY_EDITOR
            return true; // Editor is always debug
            #elif UNITY_ANDROID && !UNITY_EDITOR
            // On Android, check if APK is debuggable
            try
            {
                using (AndroidJavaClass buildConfig = new AndroidJavaClass("android.os.Build"))
                using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    string packageName = activity.Call<string>("getPackageName");
                    AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                    AndroidJavaObject appInfo = packageInfo.Get<AndroidJavaObject>("applicationInfo");
                    int flags = appInfo.Get<int>("flags");
                    const int FLAG_DEBUGGABLE = 2;
                    return (flags & FLAG_DEBUGGABLE) != 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Could not check debug build status: {ex.Message}");
                return Debug.isDebugBuild;
            }
            #else
            return Debug.isDebugBuild;
            #endif
        }
        
        /// <summary>
        /// Get the installer package name (Android only)
        /// Returns: "com.android.vending" (Google Play), "com.amazon.venezia" (Amazon), etc.
        /// </summary>
        public static string GetInstallerPackageName()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    string packageName = activity.Call<string>("getPackageName");
                    string installer = packageManager.Call<string>("getInstallerPackageName", packageName);
                    return installer ?? "sideload"; // null means sideloaded
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Could not get installer package: {ex.Message}");
                return "unknown";
            }
            #else
            return "unknown"; // Android-specific
            #endif
        }
        
        /// <summary>
        /// Check if running on an emulator (Android only)
        /// Checks Build.FINGERPRINT and other emulator indicators
        /// </summary>
        public static bool IsEmulator()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass build = new AndroidJavaClass("android.os.Build"))
                {
                    string fingerprint = build.GetStatic<string>("FINGERPRINT");
                    string model = build.GetStatic<string>("MODEL");
                    string manufacturer = build.GetStatic<string>("MANUFACTURER");
                    string brand = build.GetStatic<string>("BRAND");
                    string device = build.GetStatic<string>("DEVICE");
                    string product = build.GetStatic<string>("PRODUCT");
                    string hardware = build.GetStatic<string>("HARDWARE");
                    
                    // Check common emulator indicators
                    return fingerprint.Contains("generic") ||
                           fingerprint.Contains("unknown") ||
                           model.Contains("google_sdk") ||
                           model.Contains("Emulator") ||
                           model.Contains("Android SDK") ||
                           manufacturer.Contains("Genymotion") ||
                           brand.StartsWith("generic") && device.StartsWith("generic") ||
                           product.Contains("google_sdk") ||
                           product.Contains("sdk_gphone") ||
                           product.Contains("sdk_x86") ||
                           product.Contains("vbox86p") ||
                           hardware.Contains("goldfish") ||
                           hardware.Contains("ranchu");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] Could not check emulator status: {ex.Message}");
                return false;
            }
            #else
            return false; // Android-specific
            #endif
        }
        
        /// <summary>
        /// Check if this is a production build from a known store (Android only)
        /// Returns true if installed from Google Play, Amazon AppStore, etc.
        /// </summary>
        public static bool IsProduction()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (IsDebugBuild()) return false;
            if (IsEmulator()) return false;
            
            string installer = GetInstallerPackageName();
            
            // Known store installers
            return installer == "com.android.vending" ||          // Google Play
                   installer == "com.amazon.venezia" ||           // Amazon AppStore
                   installer == "com.sec.android.app.samsungapps"; // Samsung Galaxy Store
            #else
            return false; // Android-specific
            #endif
        }
        
        #endregion
    }
}
