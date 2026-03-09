using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Device information utility for getting system uptime and boot time
    /// Used for fraud detection and device fingerprinting
    /// </summary>
    public static class BoostOpsDeviceInfo
    {
        #region iOS Native Imports
        
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern double _BoostOpsGetDeviceUptimeSeconds();
        
        [DllImport("__Internal")]
        private static extern long _BoostOpsGetDeviceBootTimestamp();
        
        [DllImport("__Internal")]
        private static extern long _BoostOpsGetAppInstallTimestamp();
#endif
        
        #endregion
        
        #region Android Native Integration
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass _deviceInfoClass;
        
        private static AndroidJavaClass DeviceInfoClass
        {
            get
            {
                if (_deviceInfoClass == null)
                {
                    try
                    {
                        _deviceInfoClass = new AndroidJavaClass("com.boostops.unity.BoostOpsDeviceInfo");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[BoostOps] Failed to load BoostOpsDeviceInfo Android class: {e.Message}");
                    }
                }
                return _deviceInfoClass;
            }
        }
#endif
        
        #endregion
        
        /// <summary>
        /// Get device uptime in milliseconds (time since device last booted)
        /// Uses monotonic clock - NOT affected by user changing device time
        /// Matches Firebase's elapsed_realtime parameter naming
        /// </summary>
        /// <returns>Milliseconds since device boot, or null if unavailable</returns>
        public static long? GetElapsedRealtimeMilliseconds()
        {
            try
            {
#if UNITY_EDITOR
                // In Unity Editor, return time since Unity started
                return (long)(Time.realtimeSinceStartup * 1000.0);
                
#elif UNITY_IOS
                // iOS: Use native sysctl to get boot time
                double uptimeSeconds = _BoostOpsGetDeviceUptimeSeconds();
                if (uptimeSeconds > 0)
                {
                    return (long)(uptimeSeconds * 1000.0);
                }
                return null;
                
#elif UNITY_ANDROID
                // Android: Use SystemClock.elapsedRealtime()
                if (DeviceInfoClass != null)
                {
                    double uptimeSeconds = DeviceInfoClass.CallStatic<double>("getDeviceUptimeSeconds");
                    if (uptimeSeconds > 0)
                    {
                        return (long)(uptimeSeconds * 1000.0);
                    }
                }
                return null;
                
#else
                // Other platforms: fallback to Unity runtime
                return (long)(Time.realtimeSinceStartup * 1000.0);
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to get device uptime: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get device boot timestamp in Unix seconds (when device was last booted)
        /// Server can also calculate this: boot_time = timestamp_ms - elapsed_realtime_ms
        /// </summary>
        /// <returns>Unix timestamp of device boot, or null if unavailable</returns>
        public static long? GetDeviceBootTimestamp()
        {
            try
            {
                // Most reliable: calculate from current time - uptime
                long? uptimeMs = GetElapsedRealtimeMilliseconds();
                if (uptimeMs.HasValue)
                {
                    long currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long bootTimeMs = currentTimeMs - uptimeMs.Value;
                    return bootTimeMs / 1000; // Return seconds
                }
                
                // Fallback: try native methods (iOS only)
#if UNITY_IOS && !UNITY_EDITOR
                long bootTime = _BoostOpsGetDeviceBootTimestamp();
                if (bootTime > 0)
                {
                    return bootTime;
                }
#endif
                
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to get device boot timestamp: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Validate that device time hasn't been tampered with by comparing
        /// boot time calculated from two different events
        /// </summary>
        /// <param name="timestamp1Ms">First event timestamp (wall clock)</param>
        /// <param name="elapsedRealtime1Ms">First event elapsed realtime</param>
        /// <param name="timestamp2Ms">Second event timestamp (wall clock)</param>
        /// <param name="elapsedRealtime2Ms">Second event elapsed realtime</param>
        /// <param name="toleranceMs">Tolerance for clock drift (default 1000ms)</param>
        /// <returns>True if clock tampering detected</returns>
        public static bool DetectClockTampering(
            long timestamp1Ms, 
            long elapsedRealtime1Ms, 
            long timestamp2Ms, 
            long elapsedRealtime2Ms,
            long toleranceMs = 1000)
        {
            try
            {
                // Calculate boot time from both events
                long bootTime1 = timestamp1Ms - elapsedRealtime1Ms;
                long bootTime2 = timestamp2Ms - elapsedRealtime2Ms;
                
                // If boot times differ significantly (and device didn't reboot), clock was changed
                long difference = Math.Abs(bootTime1 - bootTime2);
                
                // Also check if elapsed realtime went backwards (impossible unless device rebooted)
                bool realtimeWentBackwards = elapsedRealtime2Ms < elapsedRealtime1Ms;
                
                return difference > toleranceMs && !realtimeWentBackwards;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to detect clock tampering: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get the timestamp when the app was first installed on the device.
        /// Used for detecting SDK migrations vs true new installs.
        /// iOS: Uses Documents directory creation date
        /// Android: Uses PackageManager.getPackageInfo().firstInstallTime
        /// </summary>
        /// <returns>Unix timestamp (seconds) of app install, or 0 if unavailable</returns>
        public static long GetAppInstallTimestamp()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                long installTime = _BoostOpsGetAppInstallTimestamp();
                // Debug.Log($"[BoostOps] GetAppInstallTimestamp (iOS) - installTime: {installTime}");
                return installTime; // Return directly (0 if unavailable)
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    string packageName = currentActivity.Call<string>("getPackageName");
                    using (var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0))
                    {
                        long firstInstallTime = packageInfo.Get<long>("firstInstallTime");
                        long installTimeSeconds = firstInstallTime / 1000; // Convert milliseconds to seconds
                        // Debug.Log($"[BoostOps] GetAppInstallTimestamp (Android) - installTime: {installTimeSeconds}");
                        return installTimeSeconds;
                    }
                }
#else
                // Editor or unsupported platform - use fallback for testing
                // Use Application.persistentDataPath directory creation date as proxy
                string persistentPath = Application.persistentDataPath;
                if (System.IO.Directory.Exists(persistentPath))
                {
                    System.IO.DirectoryInfo dirInfo = new System.IO.DirectoryInfo(persistentPath);
                    long installTime = ((DateTimeOffset)dirInfo.CreationTimeUtc).ToUnixTimeSeconds();
                    // Debug.Log($"[BoostOps] GetAppInstallTimestamp (Editor fallback) - installTime: {installTime} ({dirInfo.CreationTimeUtc:yyyy-MM-dd HH:mm:ss} UTC)");
                    return installTime;
                }
                else
                {
                    Debug.LogWarning($"[BoostOps] GetAppInstallTimestamp - Editor fallback: persistentDataPath doesn't exist");
                    return 0;
                }
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to get app install timestamp: {e.Message}");
                return 0;
            }
        }
    }
}




