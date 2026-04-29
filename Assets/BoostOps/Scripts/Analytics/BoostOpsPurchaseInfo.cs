using System;

namespace BoostOps
{
    /// <summary>
    /// Typed input for the advanced TrackPurchase overload. Mirrors the
    /// fields accepted by the BoostOps dedicated purchase endpoint
    /// (POST /v1/purchases) so callers can express subscriptions, trials,
    /// original transaction IDs and explicit timestamps without juggling a
    /// dictionary.
    ///
    /// The simple overload — TrackPurchase(amount, currency, productId, ...)
    /// — covers the common one-shot IAP case. Use this struct when you need
    /// any of the optional fields below.
    /// </summary>
    [Serializable]
    public class BoostOpsPurchaseInfo
    {
        /// <summary>Purchase amount in local currency (REQUIRED).</summary>
        public decimal Amount;

        /// <summary>ISO 4217 currency code, uppercase (REQUIRED). Examples: "USD", "EUR", "JPY".</summary>
        public string Currency;

        /// <summary>Product SKU/identifier from the store (REQUIRED).</summary>
        public string ProductId;

        /// <summary>
        /// Store-issued transaction ID (REQUIRED for reliable deduplication).
        /// App Store: SKPaymentTransaction.transactionIdentifier.
        /// Google Play: Purchase.orderId (or purchaseToken if no orderId).
        /// </summary>
        public string TransactionId;

        /// <summary>
        /// Apple-only: original_transaction_id for subscription renewals.
        /// Leave null/empty for one-shot purchases (server defaults it to TransactionId).
        /// </summary>
        public string OriginalTransactionId;

        /// <summary>
        /// Raw store receipt or purchase token for server-side validation.
        /// iOS: app receipt or signed JWS transaction info.
        /// Android: purchaseToken.
        /// Capped at 32KB by the server; larger values are rejected.
        /// </summary>
        public string Receipt;

        /// <summary>
        /// Receipt format hint. One of: "apple_pkcs7", "google_purchase_token",
        /// "unity_wrapped", "other". Leave null to let the server sniff it.
        /// </summary>
        public string ReceiptFormat;

        /// <summary>
        /// Override the auto-detected store. One of: "app_store", "google_play".
        /// Leave null to derive from the runtime platform.
        /// </summary>
        public string Store;

        /// <summary>True if this is a subscription purchase.</summary>
        public bool IsSubscription;

        /// <summary>True if this purchase is a free-trial period.</summary>
        public bool IsTrial;

        /// <summary>
        /// Override sandbox detection. Leave as default (false) and the SDK will
        /// infer sandbox from TestFlight/Editor/debug-build context, OR set true
        /// explicitly when you know the receipt is from a sandbox/test environment.
        /// </summary>
        public bool? IsSandboxOverride;

        /// <summary>
        /// ISO 3166-1 alpha-2 storefront country (e.g. "US"). Optional;
        /// the server will not infer this for you.
        /// </summary>
        public string Country;

        /// <summary>
        /// When the purchase happened. Defaults to UtcNow if left at default(DateTime).
        /// </summary>
        public DateTime PurchaseTimestamp;

        /// <summary>
        /// Optional client-side correlation ID that survives retries. If left null
        /// the SDK generates one. Echoed back in BoostOps logs to correlate with
        /// other events from the same purchase flow.
        /// </summary>
        public string ClientEventId;
    }
}
