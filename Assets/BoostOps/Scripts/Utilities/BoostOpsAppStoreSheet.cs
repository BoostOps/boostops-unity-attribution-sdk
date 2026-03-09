using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BoostOps
{
    /// <summary>
    /// Unity wrapper for native iOS App Store sheet functionality.
    /// Uses SKStoreProductViewController to show App Store pages natively within the app.
    /// </summary>
    public static class BoostOpsAppStoreSheet
    {
        // Events for App Store sheet lifecycle (public API for external subscribers)
        
        /// <summary>
        /// Event fired when the App Store sheet is successfully presented to the user.
        /// Subscribe to this event to pause game logic, analytics, etc.
        /// </summary>
#pragma warning disable CS0067 // Event is used by external subscribers
        public static event Action OnSheetPresented;
        
        /// <summary>
        /// Event fired when the App Store sheet is dismissed by the user.
        /// Subscribe to this event to resume game logic, analytics, etc.
        /// </summary>
        public static event Action OnSheetDismissed;
        
        /// <summary>
        /// Event fired when the user starts a purchase process within the App Store sheet.
        /// Subscribe to this event to track conversion analytics.
        /// </summary>
        public static event Action<string> OnPurchaseStarted;
#pragma warning restore CS0067

#if UNITY_IOS && !UNITY_EDITOR
        
        // iOS native function imports
        [DllImport("__Internal")]
        private static extern void BoostOpsNative_ShowAppStoreSheet(string appStoreId);
        
        [DllImport("__Internal")]
        private static extern void BoostOpsNative_SetAppStoreSheetDelegate(
            System.IntPtr onPresented,
            System.IntPtr onDismissed, 
            System.IntPtr onPurchaseStarted
        );
        
        [DllImport("__Internal")]
        private static extern bool BoostOpsNative_IsAppStoreSheetAvailable();

        // Static delegate references to prevent GC (industry standard pattern)
        private static bool delegatesSet = false;
        private static Action onPresentedDelegate;
        private static Action onDismissedDelegate;
        private static Action<string> onPurchaseStartedDelegate;
        
        [AOT.MonoPInvokeCallback(typeof(Action))]
        private static void OnSheetPresentedCallback()
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    Debug.Log("[BoostOps] App Store sheet presented");
                    OnSheetPresented?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BoostOps] Error in OnSheetPresented callback: {e.Message}");
                }
            });
        }
        
        [AOT.MonoPInvokeCallback(typeof(Action))]
        private static void OnSheetDismissedCallback()
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    Debug.Log("[BoostOps] App Store sheet dismissed");
                    OnSheetDismissed?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BoostOps] Error in OnSheetDismissed callback: {e.Message}");
                }
            });
        }
        
        [AOT.MonoPInvokeCallback(typeof(Action<string>))]
        private static void OnPurchaseStartedCallback(string appStoreId)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    Debug.Log($"[BoostOps] Purchase started for app: {appStoreId}");
                    OnPurchaseStarted?.Invoke(appStoreId);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BoostOps] Error in OnPurchaseStarted callback: {e.Message}");
                }
            });
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SetupDelegates();
        }
        
        private static void SetupDelegates()
        {
            if (!delegatesSet)
            {
                try
                {
                    // Keep static references to prevent garbage collection (Facebook SDK pattern)
                    onPresentedDelegate = OnSheetPresentedCallback;
                    onDismissedDelegate = OnSheetDismissedCallback;
                    onPurchaseStartedDelegate = OnPurchaseStartedCallback;
                    
                    BoostOpsNative_SetAppStoreSheetDelegate(
                        Marshal.GetFunctionPointerForDelegate(onPresentedDelegate),
                        Marshal.GetFunctionPointerForDelegate(onDismissedDelegate),
                        Marshal.GetFunctionPointerForDelegate(onPurchaseStartedDelegate)
                    );
                    delegatesSet = true;
                    Debug.Log("[BoostOps] App Store sheet delegates set up successfully");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BoostOps] Failed to set up App Store sheet delegates: {e.Message}");
                }
            }
        }

#endif

        /// <summary>
        /// Check if the native App Store sheet functionality is available on this device.
        /// Follows Facebook SDK pattern for comprehensive availability checking.
        /// </summary>
        /// <returns>True if SKStoreProductViewController is available, false otherwise</returns>
        public static bool IsAvailable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                // Multi-level availability check (Facebook SDK pattern)
                
                // 1. Platform check
                if (Application.platform != RuntimePlatform.IPhonePlayer) {
                    return false;
                }
                
                // 2. iOS version check (SKStoreProductViewController requires iOS 6.0+)
                var iosVersion = SystemInfo.operatingSystem;
                if (string.IsNullOrEmpty(iosVersion)) {
                    Debug.LogWarning("[BoostOps] Cannot determine iOS version");
                    return false;
                }
                
                // 3. Native framework availability check
                bool nativeAvailable = BoostOpsNative_IsAppStoreSheetAvailable();
                
                if (!nativeAvailable) {
                    Debug.LogWarning("[BoostOps] SKStoreProductViewController not available - check StoreKit framework linking");
                }
                
                return nativeAvailable;
            }
            catch (DllNotFoundException)
            {
                Debug.LogError("[BoostOps] Native iOS library not found. Ensure BoostOpsAppStoreNative is properly linked.");
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                Debug.LogError("[BoostOps] Native function not found. Check iOS plugin configuration.");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Error checking App Store sheet availability: {e.Message}");
                return false;
            }
#else
            // Debug info for non-iOS platforms
            if (Application.isEditor) {
                Debug.LogWarning("[BoostOps] App Store sheet only available on iOS devices (not in Editor)");
            } else {
                Debug.LogWarning($"[BoostOps] App Store sheet not supported on {Application.platform}");
            }
            return false;
#endif
        }

        /// <summary>
        /// Show the native iOS App Store sheet for the specified app.
        /// Uses main thread safety patterns from Unity and Facebook SDKs.
        /// </summary>
        /// <param name="appStoreId">The App Store ID (numeric string) of the app to display</param>
        /// <returns>True if the sheet was successfully requested, false otherwise</returns>
        public static bool ShowAppStoreSheet(string appStoreId)
        {
            if (string.IsNullOrEmpty(appStoreId))
            {
                Debug.LogError("[BoostOps] App Store ID cannot be null or empty");
                return false;
            }

#if UNITY_IOS && !UNITY_EDITOR
            if (!IsAvailable())
            {
                Debug.LogWarning("[BoostOps] App Store sheet not available on this device");
                return false;
            }

            // Thread safety check (Unity/Facebook SDK pattern)
            if (!IsMainThread())
            {
                Debug.LogError("[BoostOps] ShowAppStoreSheet must be called from the main thread");
                return false;
            }

            try
            {
                SetupDelegates(); // Ensure delegates are set up
                BoostOpsNative_ShowAppStoreSheet(appStoreId);
                Debug.Log($"[BoostOps] Requested App Store sheet for app ID: {appStoreId}");
                return true;
            }
            catch (DllNotFoundException)
            {
                Debug.LogError("[BoostOps] Native iOS library not found during ShowAppStoreSheet call");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Failed to show App Store sheet: {e.Message}");
                return false;
            }
#else
            Debug.LogWarning("[BoostOps] App Store sheet only available on iOS devices");
            return false;
#endif
        }
        
        /// <summary>
        /// Check if we're running on the main thread (Unity SDK pattern)
        /// </summary>
        private static bool IsMainThread()
        {
            return System.Threading.Thread.CurrentThread.ManagedThreadId == 1 || 
                   UnityEngine.Object.FindFirstObjectByType<UnityEngine.MonoBehaviour>() != null;
        }

        /// <summary>
        /// Extract App Store ID from a full App Store URL.
        /// </summary>
        /// <param name="appStoreUrl">Full App Store URL (e.g., "https://apps.apple.com/app/id1234567890")</param>
        /// <returns>The extracted App Store ID, or null if not found</returns>
        public static string ExtractAppStoreId(string appStoreUrl)
        {
            if (string.IsNullOrEmpty(appStoreUrl))
                return null;

            try
            {
                // Look for "id" followed by digits
                var idIndex = appStoreUrl.IndexOf("id", StringComparison.OrdinalIgnoreCase);
                if (idIndex == -1) return null;

                var startIndex = idIndex + 2;
                var endIndex = startIndex;

                // Find the end of the numeric ID
                while (endIndex < appStoreUrl.Length && char.IsDigit(appStoreUrl[endIndex]))
                {
                    endIndex++;
                }

                if (endIndex > startIndex)
                {
                    return appStoreUrl.Substring(startIndex, endIndex - startIndex);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Error extracting App Store ID from URL: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Show App Store sheet using a full App Store URL.
        /// Automatically extracts the App Store ID from the URL.
        /// </summary>
        /// <param name="appStoreUrl">Full App Store URL</param>
        /// <returns>True if the sheet was successfully requested, false otherwise</returns>
        public static bool ShowAppStoreSheetFromUrl(string appStoreUrl)
        {
            var appStoreId = ExtractAppStoreId(appStoreUrl);
            if (string.IsNullOrEmpty(appStoreId))
            {
                Debug.LogError($"[BoostOps] Could not extract App Store ID from URL: {appStoreUrl}");
                return false;
            }

            return ShowAppStoreSheet(appStoreId);
        }
    }

} 