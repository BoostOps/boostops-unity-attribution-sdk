using System;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Single source of truth for where device traffic (events, purchases) is sent.
    /// One subdomain per service on the SDK-only domain; services that may carry the IDFA
    /// have an "att-" twin, which is the only kind of host declared in NSPrivacyTrackingDomains.
    /// </summary>
    internal static class BoostOpsEndpoints
    {
        // Device traffic never goes to boostops.*, so a blocklist entry for SDK traffic
        // can't take down the website and dashboard. Every host here receives the project key.
        private const string Domain = "boostopssdk.com";
        private const string EventsHost = "events." + Domain;
        private const string PurchasesHost = "purchases." + Domain;

        // iOS blocks hosts in NSPrivacyTrackingDomains for users who haven't granted ATT.
        // "att-" hosts are never override targets, so non-consented traffic can't land on them.
        private const string TrackingPrefix = "att-";
        internal const string TrackingEventsHost = TrackingPrefix + EventsHost;

        // Hosts the server (or SetCustomEndpoint) may redirect traffic to.
        private static readonly string[] AllowedDomainSuffixes = { Domain };
        private static readonly string[] AllowedExactHosts = { };

        private const string EventsPath = "/v1/events";
        private const string PurchasesPath = "/v1/purchases";

        // Server-provided host (BoostOpsProjectSettings.ingestUrl), used for both services
        // until it fails at the connection level; then the built-in service hosts take over.
        private static string _configuredHost;
        private static bool _configuredHostFailed;
        private static string _customBaseUrl;

        internal static string EventsUrl => BaseUrlFor(EventsHost) + EventsPath;
        internal static string PurchasesUrl => BaseUrlFor(PurchasesHost) + PurchasesPath;
        internal static bool HasCustomBaseUrl => _customBaseUrl != null;

        internal static string TrackingEventsUrl => "https://" + TrackingEventsHost + EventsPath;

        /// <summary>
        /// The IDFA when this events send should take the tracking route: iOS, project opted in to
        /// ATT tracking, endpoint not pinned, and the user granted ATT. Null otherwise.
        /// Checked per send so a later grant or revoke takes effect immediately.
        /// </summary>
        internal static string TrackingRouteIdfa()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_customBaseUrl != null)
                return null;
            var settings = BoostOps.Internal.InternalSettingsCache.GetProjectSettings();
            if (settings == null || !settings.AttTracking)
                return null;
            return BoostOpsIdentifierManager.GetIDFA();
#else
            return null;
#endif
        }

        private static string BaseUrlFor(string serviceHost)
        {
            if (_customBaseUrl != null)
                return _customBaseUrl;
            if (_configuredHost != null && !_configuredHostFailed)
                return "https://" + _configuredHost;
            return "https://" + serviceHost;
        }

        /// <summary>
        /// HTTPS only, on a BoostOps-owned device host, never a tracking host.
        /// </summary>
        internal static bool IsAllowed(string url)
        {
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                return false;

            string host = uri.Host;
            if (host.StartsWith(TrackingPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            foreach (var exact in AllowedExactHosts)
            {
                if (string.Equals(host, exact, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            foreach (var suffix in AllowedDomainSuffixes)
            {
                if (string.Equals(host, suffix, StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Called after a connection-level failure on <paramref name="failedUrl"/>.
        /// Returns the events URL to use next. A failed configured host falls back
        /// to the built-in service hosts.
        /// </summary>
        internal static string EventsUrlAfterConnectionFailure(string failedUrl)
        {
            MarkConfiguredHostFailed(failedUrl);
            return EventsUrl;
        }

        /// <summary>
        /// Same as <see cref="EventsUrlAfterConnectionFailure"/> for the purchases path.
        /// </summary>
        internal static string PurchasesUrlAfterConnectionFailure(string failedUrl)
        {
            MarkConfiguredHostFailed(failedUrl);
            return PurchasesUrl;
        }

        /// <summary>
        /// Use the project's configured ingest host (BoostOpsProjectSettings.ingestUrl, written by
        /// the server at registration) for both services, keeping the built-in hosts for failover.
        /// A built-in service host or the bare domain means "use the defaults".
        /// </summary>
        internal static bool SetConfiguredIngestUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !IsAllowed(url))
                return false;

            string host = new Uri(url).Host.ToLowerInvariant();
            bool isDefault = host == Domain || host == EventsHost || host == PurchasesHost;
            _configuredHost = isDefault ? null : host;
            _configuredHostFailed = false;
            return true;
        }

        /// <summary>
        /// Pin all device traffic to a base URL (e.g. "https://events.boostopssdk.com").
        /// Accepts "/v1" or "/v1/events" suffixes. Null or empty clears the pin.
        /// </summary>
        internal static bool SetCustomBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                _customBaseUrl = null;
                return true;
            }

            if (!IsAllowed(url))
                return false;

            string normalized = url.TrimEnd('/');
            foreach (var suffix in new[] { EventsPath, PurchasesPath, "/v1" })
            {
                if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(0, normalized.Length - suffix.Length);
                    break;
                }
            }
            _customBaseUrl = normalized;
            return true;
        }

        internal static void ResetStaticState()
        {
            _configuredHost = null;
            _configuredHostFailed = false;
            _customBaseUrl = null;
        }

        private static void MarkConfiguredHostFailed(string failedUrl)
        {
            if (_customBaseUrl != null || _configuredHost == null)
                return;

            if (Uri.TryCreate(failedUrl, UriKind.Absolute, out var uri)
                && string.Equals(uri.Host, _configuredHost, StringComparison.OrdinalIgnoreCase))
                _configuredHostFailed = true;
        }
    }
}
