using System;
using System.Threading.Tasks;
using UnityEngine;

#if WINDOWS_UWP || ENABLE_WINMD_SUPPORT
using System.Linq;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Foundation.Metadata;
using Windows.Services.Store;
#endif

namespace BoostOps
{
    /// <summary>
    /// Microsoft Store campaign attribution reader.
    ///
    /// When a user installs a Windows app from a Store URL containing a campaign id —
    /// e.g. <c>apps.microsoft.com/detail/&lt;app_id&gt;?cid=msads-search-brand</c> — the Store
    /// records the campaign at install time and exposes it at runtime via WinRT
    /// (<see cref="StoreContext"/> / <see cref="StoreAppLicense"/>). This class reads
    /// that value, caches it, and persists it to PlayerPrefs so the SDK's first_open
    /// event picks it up via the same code path that handles Android's Install
    /// Referrer.
    ///
    /// Supported targets:
    /// <list type="bullet">
    ///   <item>Unity UWP (UNITY_WSA): WinRT projection is available natively.</item>
    ///   <item>Unity StandaloneWindows / StandaloneWindows64 distributed via MSIX:
    ///         requires the <c>Microsoft.Windows.SDK.Contracts</c> NuGet package and the
    ///         <c>ENABLE_WINMD_SUPPORT</c> scripting define symbol. See
    ///         <c>BoostOpsProjectSettings.distributeViaMicrosoftStore</c>.</item>
    ///   <item>StandaloneWindows distributed as a bare .exe: there is no Store-issued
    ///         package identity, so this class compiles to no-op stubs and the SDK
    ///         silently falls back to organic install attribution.</item>
    ///   <item>Non-Windows platforms (iOS, Android, macOS, Linux, WebGL): compiles to
    ///         no-op stubs, no runtime cost.</item>
    /// </list>
    ///
    /// Wire mapping (lands on the next first_open event via the existing pipeline):
    /// <list type="bullet">
    ///   <item><c>attribution_source        = "microsoft_store"</c></item>
    ///   <item><c>attribution_channel       = "ua:microsoft_store"</c></item>
    ///   <item><c>attribution_campaign_slug = &lt;cid&gt;</c></item>
    ///   <item><c>attribution_campaign      = &lt;cid&gt;</c></item>
    ///   <item><c>attribution_method        = "deterministic"</c></item>
    ///   <item><c>touch_type                = "click"</c></item>
    /// </list>
    ///
    /// Caching behavior: only non-empty campaign ids are cached. An empty result on
    /// first launch (user not signed in, transient WinRT failure, race against the
    /// Store license becoming available) is recorded as "attempted" so we don't
    /// retry constantly within a single session, but is NOT cached as the final
    /// answer; subsequent cold starts will retry until we read a non-empty value
    /// or the OS confirms there is no campaign. This matches the Android Install
    /// Referrer behavior.
    /// </summary>
    public static class BoostOpsMicrosoftStoreCampaign
    {
        // Wire-format constants — matches the existing attribution_* fields in the
        // event schema so MS Store-attributed events bucket cleanly alongside Android's
        // Play Install Referrer and iOS's AdServices/Apple Search Ads paths.
        public const string AttributionSource = "microsoft_store";
        public const string AttributionChannel = "ua:microsoft_store";
        public const string AttributionMethod = "deterministic";
        public const string TouchType = "click";

        /// <summary>
        /// Returns the cached MS Store campaign id (empty string if none is known).
        /// Synchronous; safe to call from the SDK's first_open event path.
        /// </summary>
        public static string GetCachedCampaignId()
        {
            return PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.MS_STORE_CAMPAIGN_ID, "");
        }

        /// <summary>
        /// True when this build is running on a Windows target where we can attempt
        /// to read the campaign id from the Store. False on non-Windows platforms,
        /// or on Standalone Windows builds without ENABLE_WINMD_SUPPORT (bare .exe
        /// distribution).
        /// </summary>
        public static bool IsSupportedOnThisBuild
        {
            get
            {
#if WINDOWS_UWP || ENABLE_WINMD_SUPPORT
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Asynchronously read the MS Store campaign id and persist it to PlayerPrefs.
        ///
        /// Call once per cold start during SDK initialization. Returns the campaign
        /// id (or empty string), and writes it to <see cref="BoostOpsPlayerPrefsKeys.MS_STORE_CAMPAIGN_ID"/>
        /// on success. Fails silently and returns empty on any error — including
        /// running on a non-Store install, the user not being signed in, or the
        /// WinRT API not being available on this OS version.
        /// </summary>
        public static async Task<string> InitializeAndReadAsync()
        {
            // Fast path: already have a cached non-empty value, return it.
            string cached = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.MS_STORE_CAMPAIGN_ID, "");
            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

#if WINDOWS_UWP || ENABLE_WINMD_SUPPORT
            string campaignId = await ReadCampaignIdFromStoreAsync();

            // Mark that we've attempted a read this install (for diagnostics).
            PlayerPrefs.SetString(
                BoostOpsPlayerPrefsKeys.MS_STORE_CAMPAIGN_ATTEMPTED,
                DateTime.UtcNow.ToString("O"));

            if (!string.IsNullOrEmpty(campaignId))
            {
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.MS_STORE_CAMPAIGN_ID, campaignId);
                PlayerPrefs.SetString(
                    BoostOpsPlayerPrefsKeys.MS_STORE_CAMPAIGN_PROCESSED,
                    DateTime.UtcNow.ToString("O"));
                PlayerPrefs.Save();

                Debug.Log($"[BoostOps] ✅ MS Store campaign captured: {campaignId}");
            }
            else
            {
                PlayerPrefs.Save();
                Debug.Log("[BoostOps] No MS Store campaign id available (organic install, sideload, or non-Store build)");
            }

            return campaignId ?? "";
#else
            await Task.CompletedTask;
            return "";
#endif
        }

#if WINDOWS_UWP || ENABLE_WINMD_SUPPORT
        // The actual WinRT call. Isolated in its own method so the gate above keeps
        // it out of compilation on non-Windows targets. Wrapped in try/catch because
        // every layer here can fail on legitimate non-Store installs and we never
        // want to surface that as an error to the host app.
        private static async Task<string> ReadCampaignIdFromStoreAsync()
        {
            try
            {
                // StoreContext / StoreProductResult require Windows 10 1709+ (build 16299).
                // ApiInformation lets older Windows 10 builds compile-link cleanly without
                // throwing TypeLoadException at runtime.
                if (!ApiInformation.IsTypePresent("Windows.Services.Store.StoreContext"))
                {
                    Debug.Log("[BoostOps] StoreContext not present on this Windows build — skipping MS Store campaign read");
                    return "";
                }

                StoreContext context = StoreContext.GetDefault();
                if (context == null)
                {
                    return "";
                }

                // Primary: signed-in MSA users — campaign rides on the user's collection
                // metadata for the SKU they installed.
                string fromCollection = await TryReadFromCollectionAsync(context);
                if (!string.IsNullOrEmpty(fromCollection))
                {
                    return fromCollection;
                }

                // Fallback: non-MSA users — campaign is embedded in the app license JSON
                // under the `customPolicyField1` key (Microsoft's documented fallback path
                // for ad-supported / non-signed-in installs).
                string fromLicense = await TryReadFromLicenseAsync(context);
                if (!string.IsNullOrEmpty(fromLicense))
                {
                    return fromLicense;
                }
            }
            catch (Exception ex)
            {
                // Expected on non-Store installs, sideloads, or transient Store outages.
                // Log once for diagnostics and move on — the SDK keeps working.
                Debug.LogWarning($"[BoostOps] Could not read MS Store campaign id: {ex.GetType().Name}: {ex.Message}");
            }

            return "";
        }

        private static async Task<string> TryReadFromCollectionAsync(StoreContext context)
        {
            try
            {
                StoreProductResult result = await context.GetStoreProductForCurrentAppAsync();
                if (result?.Product?.Skus == null)
                {
                    return "";
                }

                // Prefer the SKU the current user actually owns; fall back to the first
                // SKU that has a non-null CollectionData (covers edge cases where the
                // IsInUserCollection flag isn't yet populated).
                StoreSku sku = result.Product.Skus.FirstOrDefault(s => s != null && s.IsInUserCollection)
                            ?? result.Product.Skus.FirstOrDefault(s => s?.CollectionData != null);

                string cid = sku?.CollectionData?.CampaignId;
                return string.IsNullOrEmpty(cid) ? "" : cid;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] MS Store collection read failed: {ex.Message}");
                return "";
            }
        }

        private static async Task<string> TryReadFromLicenseAsync(StoreContext context)
        {
            try
            {
                StoreAppLicense license = await context.GetAppLicenseAsync();
                if (license == null || string.IsNullOrEmpty(license.ExtendedJsonData))
                {
                    return "";
                }

                JsonObject json = JsonObject.Parse(license.ExtendedJsonData);
                if (json == null || !json.ContainsKey("customPolicyField1"))
                {
                    return "";
                }

                string cid = json["customPolicyField1"].GetString();
                return string.IsNullOrEmpty(cid) ? "" : cid;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BoostOps] MS Store license read failed: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Best-effort detection that this app was installed from the Microsoft Store
        /// (vs sideloaded / direct .exe). Used by <see cref="BoostOpsEnvironment"/>
        /// and <see cref="BoostOpsStoreDetector"/>. Synchronous and cached.
        /// </summary>
        public static bool IsMicrosoftStoreInstall()
        {
            try
            {
                // Package.Current.SignatureKind == Store proves this build was signed by
                // the Microsoft Store and shipped through it (works for both UWP and
                // packaged Win32 / MSIX). Sideloaded MSIX is "Developer", classic .exe
                // raises an InvalidOperationException reading Package.Current at all.
                Package package = Package.Current;
                if (package == null)
                {
                    return false;
                }

                return package.SignatureKind == PackageSignatureKind.Store;
            }
            catch
            {
                return false;
            }
        }
#else
        public static bool IsMicrosoftStoreInstall() => false;
#endif
    }
}
