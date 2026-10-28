using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BoostOps.Analytics
{
    /// <summary>
    /// Privacy decisions made at send time: whether ad identifiers (IDFA, GAID) may leave the
    /// device, and whether first-launch sends should wait for the ATT prompt.
    /// Main thread only (PlayerPrefs, JNI).
    /// </summary>
    internal static class BoostOpsPrivacyGate
    {
        internal const int AttUnavailable = -1;
        internal const int AttNotDetermined = 0;
        internal const int AttRestricted = 1;
        internal const int AttDenied = 2;
        internal const int AttAuthorized = 3;

        internal const int MaxAttWaitSeconds = 360;

        // TCF v2 purposes (1-based): 1 = store/access information on a device, 7 = measure ad performance.
        private const int PurposeDeviceAccess = 1;
        private const int PurposeMeasureAds = 7;

        // Written by BoostOpsConsentManager.SetMarketingConsent / SetAllConsent / WithdrawAllConsent.
        private const string MarketingConsentKey = "BoostOps_Consent_Marketing";

        // Unix seconds the first-launch ATT wait ends, or "done" once it is over for good.
        private const string AttWaitUntilKey = "boostops.att_wait_until";
        private const string AttWaitDone = "done";

        private const float TcfCacheSeconds = 5f;

        internal struct TcfState
        {
            /// <summary>1 = GDPR applies, 0 = does not, -1 = no consent tool data.</summary>
            public int GdprApplies;
            public string TcString;
            public string PurposeConsents;

            public bool Present => GdprApplies == 0 || GdprApplies == 1;
            public bool HasPurpose(int purpose) =>
                PurposeConsents != null && PurposeConsents.Length >= purpose && PurposeConsents[purpose - 1] == '1';
            public bool AllowsAdMeasurement =>
                GdprApplies != 1 || (HasPurpose(PurposeDeviceAccess) && HasPurpose(PurposeMeasureAds));
        }

        private static TcfState _tcfCache;
        private static float _tcfCacheTime = float.NegativeInfinity;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int BoostOpsGetATTStatus();

        [DllImport("__Internal")]
        private static extern int BoostOpsGetUserDefaultInt(string key, int fallback);

        [DllImport("__Internal")]
        private static extern string BoostOpsGetUserDefaultString(string key);
#endif

        /// <summary>
        /// IAB TCF v2 values written by the app's consent tool (Google UMP, OneTrust, ...).
        /// </summary>
        internal static TcfState ReadTcf()
        {
            if (Time.realtimeSinceStartup - _tcfCacheTime < TcfCacheSeconds)
                return _tcfCache;

            var state = new TcfState { GdprApplies = -1 };
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                state.GdprApplies = BoostOpsGetUserDefaultInt("IABTCF_gdprApplies", -1);
                state.TcString = BoostOpsGetUserDefaultString("IABTCF_TCString");
                state.PurposeConsents = BoostOpsGetUserDefaultString("IABTCF_PurposeConsents");
#elif UNITY_ANDROID && !UNITY_EDITOR
                // TCF lives in the default SharedPreferences file, which is not Unity's PlayerPrefs file.
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var prefs = activity.Call<AndroidJavaObject>("getSharedPreferences",
                           activity.Call<string>("getPackageName") + "_preferences", 0))
                {
                    state.GdprApplies = AndroidPrefInt(prefs, "IABTCF_gdprApplies");
                    state.TcString = AndroidPrefString(prefs, "IABTCF_TCString");
                    state.PurposeConsents = AndroidPrefString(prefs, "IABTCF_PurposeConsents");
                }
#else
                state.GdprApplies = PlayerPrefs.GetInt("IABTCF_gdprApplies", -1);
                state.TcString = NullIfEmpty(PlayerPrefs.GetString("IABTCF_TCString", ""));
                state.PurposeConsents = NullIfEmpty(PlayerPrefs.GetString("IABTCF_PurposeConsents", ""));
#endif
            }
            catch (Exception e)
            {
                BoostOpsLogger.LogWarning("Privacy", $"Could not read TCF consent: {e.Message}");
                state = new TcfState { GdprApplies = -1 };
            }

            _tcfCache = state;
            _tcfCacheTime = Time.realtimeSinceStartup;
            return state;
        }

        /// <summary>
        /// Whether IDFA/GAID may be sent. Blocked when TCF says GDPR applies without purposes 1 and 7,
        /// or when the app recorded "no" via BoostOpsConsentManager. Allowed when neither source has an answer.
        /// </summary>
        internal static bool AdIdsAllowed()
        {
            bool tcfAllows = ReadTcf().AllowsAdMeasurement;
            bool appAllows = !PlayerPrefs.HasKey(MarketingConsentKey) || PlayerPrefs.GetInt(MarketingConsentKey, 0) == 1;
            return tcfAllows && appAllows;
        }

        internal static int GetAttStatus()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return BoostOpsGetATTStatus();
#else
            return AttUnavailable;
#endif
        }

        internal static string AttStatusName(int status)
        {
            switch (status)
            {
                case AttNotDetermined: return "not_determined";
                case AttRestricted: return "restricted";
                case AttDenied: return "denied";
                case AttAuthorized: return "authorized";
                default: return "not_applicable";
            }
        }

        /// <summary>
        /// True while first-launch event sends should wait for the ATT answer (attWaitSeconds).
        /// The deadline is set on first check and persisted; once the user answers or it passes,
        /// it never waits again on this install.
        /// </summary>
        internal static bool HoldForAtt()
        {
#if UNITY_IOS && !UNITY_EDITOR
            var settings = BoostOps.Internal.InternalSettingsCache.GetProjectSettings();
            int waitSeconds = settings != null && settings.AttTracking
                ? Mathf.Clamp(settings.AttWaitSeconds, 0, MaxAttWaitSeconds)
                : 0;
            if (waitSeconds == 0)
                return false;

            string stored = PlayerPrefs.GetString(AttWaitUntilKey, "");
            if (stored == AttWaitDone)
                return false;

            if (GetAttStatus() != AttNotDetermined || !AdIdsAllowed())
            {
                EndAttWait();
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!long.TryParse(stored, out long until))
            {
                until = now + waitSeconds;
                PlayerPrefs.SetString(AttWaitUntilKey, until.ToString());
                PlayerPrefs.Save();
                BoostOpsLogger.LogInfo("Privacy", $"Holding event sends up to {waitSeconds}s for the ATT prompt");
            }

            if (now >= until)
            {
                EndAttWait();
                return false;
            }
            return true;
#else
            return false;
#endif
        }

        private static void EndAttWait()
        {
            PlayerPrefs.SetString(AttWaitUntilKey, AttWaitDone);
            PlayerPrefs.Save();
            BoostOpsLogger.LogInfo("Privacy", $"ATT wait over (status: {AttStatusName(GetAttStatus())})");
        }

        private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

#if UNITY_ANDROID && !UNITY_EDITOR
        // The TCF spec says integer, but some consent tools store gdprApplies as a string.
        private static int AndroidPrefInt(AndroidJavaObject prefs, string key)
        {
            if (!prefs.Call<bool>("contains", key))
                return -1;
            try
            {
                return prefs.Call<int>("getInt", key, -1);
            }
            catch (AndroidJavaException)
            {
                return int.TryParse(AndroidPrefString(prefs, key), out int parsed) ? parsed : -1;
            }
        }

        private static string AndroidPrefString(AndroidJavaObject prefs, string key)
        {
            try
            {
                return NullIfEmpty(prefs.Call<string>("getString", key, ""));
            }
            catch (AndroidJavaException)
            {
                return null;
            }
        }
#endif
    }
}
