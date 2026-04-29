using System.Collections.Generic;

namespace BoostOps
{
    /// <summary>
    /// Interface for analytics providers (Firebase, Unity, BoostOps backend)
    /// Defines the common contract for sending analytics events
    /// </summary>
    public interface IAnalyticsProvider
    {
        /// <summary>
        /// Name of the provider (for logging/debugging)
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Whether this provider is available/enabled in the current configuration
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Initialize the analytics provider
        /// </summary>
        void Initialize();

        /// <summary>
        /// Track impression event
        /// </summary>
        /// <param name="eventName">Event name to track</param>
        /// <param name="parameters">Event parameters</param>
        void TrackImpression(string eventName, Dictionary<string, string> parameters);

        /// <summary>
        /// Track click event
        /// </summary>
        /// <param name="eventName">Event name to track</param>
        /// <param name="parameters">Event parameters</param>
        void TrackClick(string eventName, Dictionary<string, string> parameters);

        /// <summary>
        /// Track install event
        /// </summary>
        /// <param name="eventName">Event name to track</param>
        /// <param name="parameters">Event parameters</param>
        [System.Obsolete("Install events are deprecated. Providers should handle APP_OPEN with first_open=true instead (industry standard)")]
        void TrackInstall(string eventName, Dictionary<string, string> parameters);

        // NOTE: TrackPurchase was removed from this interface in SDK 1.1.0.
        // Purchases now flow through BoostOpsPurchaseClient → POST /v1/purchases
        // (a dedicated, idempotent, retry-until-acked path). Third-party mirror
        // providers (Unity Analytics, Firebase) still expose TrackPurchase as a
        // concrete method on their concrete types and are called directly from
        // BoostOpsAnalyticsContract.TrackPurchase — they're no longer part of
        // this interface contract.

        /// <summary>
        /// Track generic event
        /// </summary>
        /// <param name="eventName">Event name to track</param>
        /// <param name="parameters">Event parameters</param>
        void TrackEvent(string eventName, Dictionary<string, string> parameters);
    }
} 