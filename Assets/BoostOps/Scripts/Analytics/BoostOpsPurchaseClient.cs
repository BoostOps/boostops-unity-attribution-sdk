using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using BoostOps.Internal;

namespace BoostOps.Analytics
{
    /// <summary>
    /// Dedicated HTTP client for the BoostOps purchase endpoint
    /// (POST /v1/purchases). Purchases are revenue events with strict
    /// idempotency requirements: they live in their own pipeline so they
    /// don't share retry/backoff/quota with high-volume telemetry events.
    ///
    /// Wire:
    ///   - Single request per purchase (no batching).
    ///   - Idempotency on the server's natural key (project_id, store, transaction_id).
    ///   - Retry-until-acked persistence: each pending purchase is one JSON
    ///     file under persistentDataPath/BoostOps/purchases/{txnId}.json,
    ///     deleted on successful ack (including duplicate=true) or on a
    ///     non-recoverable 4xx validation error.
    ///   - On startup the client scans the queue dir and replays anything
    ///     pending, so a crash or app kill mid-purchase is recoverable.
    ///
    /// Auth: same BoostOps-Project-Key header as the events client.
    /// </summary>
    public class BoostOpsPurchaseClient
    {
        #region Constants

        private const string DEFAULT_PURCHASES_URL = "https://analytics.boostops.io/v1/purchases";
        private const string QUEUE_SUBDIR = "BoostOps/purchases";
        private const string QUEUE_FILE_EXT = ".json";

        private const float REQUEST_TIMEOUT_SECONDS = 30f;
        private const float RETRY_INTERVAL_SECONDS = 30f;
        private const float STARTUP_REPLAY_DELAY_SECONDS = 2f;
        private const float MIN_RETRY_BACKOFF_SECONDS = 5f;
        private const float MAX_RETRY_BACKOFF_SECONDS = 600f; // 10 min cap

        // Server caps: receipts are validated at 32KB and the whole body at 64KB.
        // We refuse to enqueue a request that would clearly violate the receipt
        // cap so we don't loop forever on a permanent 413/400.
        private const int MAX_RECEIPT_BYTES = 32 * 1024;

        #endregion

        #region Singleton

        private static BoostOpsPurchaseClient _instance;
        private static readonly object _lockObject = new object();

        public static BoostOpsPurchaseClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new BoostOpsPurchaseClient();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region State

        private string _projectKey;
        private string _purchasesUrl = DEFAULT_PURCHASES_URL;
        private string _queueDir;
        private bool _initialized;

        // Per-transaction backoff state. Key is the queue file's transactionId.
        // Lives in memory only; the file on disk is enough to ensure replay.
        private readonly Dictionary<string, float> _nextRetryAttemptTime = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _attemptCount = new Dictionary<string, int>();

        private CoroutineRunner _runner;

        #endregion

        #region Initialization

        private BoostOpsPurchaseClient()
        {
            try
            {
                var settings = InternalSettingsCache.GetProjectSettings();
                if (settings != null && !string.IsNullOrEmpty(settings.ProjectKey))
                {
                    _projectKey = settings.ProjectKey;
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogWarning("Purchases", $"Could not auto-init from project settings: {ex.Message}");
            }

            _queueDir = Path.Combine(Application.persistentDataPath, QUEUE_SUBDIR);
            EnsureQueueDir();
            EnsureRunner();

            _initialized = !string.IsNullOrEmpty(_projectKey);
            if (_initialized)
            {
                _runner.StartCoroutine(StartupReplayCoroutine());
                _runner.StartCoroutine(RetryLoopCoroutine());
            }
        }

        /// <summary>
        /// Explicit init hook for cases where the project key is set after
        /// the singleton is constructed. Safe to call multiple times.
        /// </summary>
        public void Initialize(string projectKey, string purchasesUrl = null)
        {
            if (string.IsNullOrEmpty(projectKey))
            {
                BoostOpsLogger.LogError("Purchases", "Project key cannot be null or empty");
                return;
            }

            _projectKey = projectKey;
            if (!string.IsNullOrEmpty(purchasesUrl))
            {
                _purchasesUrl = purchasesUrl;
            }

            EnsureQueueDir();
            EnsureRunner();

            if (!_initialized)
            {
                _runner.StartCoroutine(StartupReplayCoroutine());
                _runner.StartCoroutine(RetryLoopCoroutine());
            }
            _initialized = true;
        }

        public bool IsInitialized => _initialized && !string.IsNullOrEmpty(_projectKey);

        #endregion

        #region Public API

        /// <summary>
        /// Enqueue a purchase for delivery. Synchronous: writes the request
        /// to the persistent queue, then kicks off an async send. Returns
        /// true if the purchase was accepted into the queue (which is the
        /// SDK's durability guarantee — even an app kill before the network
        /// call will not lose the purchase).
        /// </summary>
        public bool TrackPurchase(BoostOpsPurchaseRequest request)
        {
            if (request == null)
            {
                BoostOpsLogger.LogError("Purchases", "TrackPurchase called with null request");
                return false;
            }

            // Hard requirements that we will not retry around.
            if (string.IsNullOrEmpty(request.transaction_id))
            {
                BoostOpsLogger.LogError("Purchases", $"Purchase rejected: transaction_id is required (product_id={request.product_id})");
                return false;
            }
            if (string.IsNullOrEmpty(request.product_id))
            {
                BoostOpsLogger.LogError("Purchases", "Purchase rejected: product_id is required");
                return false;
            }
            if (string.IsNullOrEmpty(request.currency))
            {
                BoostOpsLogger.LogError("Purchases", $"Purchase rejected: currency is required (txn={request.transaction_id})");
                return false;
            }
            if (string.IsNullOrEmpty(request.store))
            {
                BoostOpsLogger.LogError("Purchases", $"Purchase rejected: store is required (txn={request.transaction_id})");
                return false;
            }
            if (request.amount_micros < 0)
            {
                BoostOpsLogger.LogError("Purchases", $"Purchase rejected: amount_micros must be >= 0 (txn={request.transaction_id})");
                return false;
            }

            // Defend against receipt-too-large before we even write the queue file.
            if (!string.IsNullOrEmpty(request.receipt) &&
                Encoding.UTF8.GetByteCount(request.receipt) > MAX_RECEIPT_BYTES)
            {
                BoostOpsLogger.LogWarning("Purchases",
                    $"Receipt for txn={request.transaction_id} exceeds {MAX_RECEIPT_BYTES} bytes; sending without receipt to avoid 413.");
                request.receipt = null;
                request.receipt_format = null;
            }

            EnsureQueueDir();
            EnsureRunner();

            string queueKey = SanitizeForFilename(request.store + "_" + request.transaction_id);
            string filePath = Path.Combine(_queueDir, queueKey + QUEUE_FILE_EXT);
            string json = SerializeRequest(request);

            if (!WriteAtomic(filePath, json))
            {
                BoostOpsLogger.LogError("Purchases", $"Failed to persist purchase to {filePath}");
                return false;
            }

            BoostOpsLogger.LogDebug("Purchases",
                $"📥 Queued purchase: store={request.store} txn={request.transaction_id} product={request.product_id} amount_micros={request.amount_micros} {request.currency}");

            // Reset attempt tracking for this txn (this might be a new purchase
            // that happens to share a sanitized key with a prior attempt).
            _attemptCount[queueKey] = 0;
            _nextRetryAttemptTime[queueKey] = 0f;

            if (!IsInitialized)
            {
                BoostOpsLogger.LogWarning("Purchases",
                    "Purchase queued but client not yet initialized (no project key). Will send once initialized.");
                return true;
            }

            _runner.StartCoroutine(SendOneCoroutine(filePath, queueKey));
            return true;
        }

        /// <summary>Number of pending purchases currently on disk awaiting ack.</summary>
        public int PendingCount
        {
            get
            {
                try
                {
                    if (!Directory.Exists(_queueDir)) return 0;
                    return Directory.GetFiles(_queueDir, "*" + QUEUE_FILE_EXT).Length;
                }
                catch { return 0; }
            }
        }

        #endregion

        #region Send / Retry

        private IEnumerator StartupReplayCoroutine()
        {
            // Small delay so the runtime (and project settings) finish bootstrapping.
            yield return new WaitForSeconds(STARTUP_REPLAY_DELAY_SECONDS);
            ReplayPending();
        }

        private IEnumerator RetryLoopCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(RETRY_INTERVAL_SECONDS);
                ReplayPending();
            }
        }

        private void ReplayPending()
        {
            if (!IsInitialized) return;
            if (!Directory.Exists(_queueDir)) return;

            string[] files;
            try { files = Directory.GetFiles(_queueDir, "*" + QUEUE_FILE_EXT); }
            catch (Exception ex)
            {
                BoostOpsLogger.LogWarning("Purchases", $"Could not enumerate queue dir: {ex.Message}");
                return;
            }

            if (files.Length == 0) return;

            float now = Time.realtimeSinceStartup;
            foreach (var filePath in files)
            {
                string queueKey = Path.GetFileNameWithoutExtension(filePath);

                // Per-txn backoff: skip if we're not due yet.
                if (_nextRetryAttemptTime.TryGetValue(queueKey, out var dueAt) && now < dueAt)
                {
                    continue;
                }

                _runner.StartCoroutine(SendOneCoroutine(filePath, queueKey));
            }
        }

        private IEnumerator SendOneCoroutine(string filePath, string queueKey)
        {
            string json;
            try
            {
                if (!File.Exists(filePath)) yield break;
                json = File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogWarning("Purchases", $"Could not read queue file {filePath}: {ex.Message}");
                yield break;
            }

            if (string.IsNullOrEmpty(json))
            {
                TryDelete(filePath);
                yield break;
            }

            int attempt;
            _attemptCount.TryGetValue(queueKey, out attempt);
            attempt++;
            _attemptCount[queueKey] = attempt;

            using (var request = BuildPostRequest(_purchasesUrl, json))
            {
                yield return request.SendWebRequest();

                long code = request.responseCode;
                bool networkError =
                    request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.DataProcessingError;

                if (!networkError && code >= 200 && code < 300)
                {
                    // Accepted (or duplicate, which the server returns 202 for too).
                    var responseText = request.downloadHandler != null ? request.downloadHandler.text : "";
                    BoostOpsLogger.LogInfo("Purchases",
                        $"✅ Ack ({code}) for {queueKey} after {attempt} attempt(s). Response: {Truncate(responseText, 200)}");
                    TryDelete(filePath);
                    _attemptCount.Remove(queueKey);
                    _nextRetryAttemptTime.Remove(queueKey);
                    yield break;
                }

                // Non-2xx: classify.
                bool permanent = !networkError && code >= 400 && code < 500
                                 && code != 408 // Request Timeout - retry
                                 && code != 425 // Too Early - retry
                                 && code != 429; // Rate Limited - retry with backoff
                if (permanent)
                {
                    var responseText = request.downloadHandler != null ? request.downloadHandler.text : "";
                    BoostOpsLogger.LogError("Purchases",
                        $"❌ Permanent failure ({code}) for {queueKey}: {Truncate(responseText, 500)}. Dropping.");
                    TryDelete(filePath);
                    _attemptCount.Remove(queueKey);
                    _nextRetryAttemptTime.Remove(queueKey);
                    yield break;
                }

                // Retryable. Schedule backoff.
                float backoff = ComputeBackoffSeconds(attempt);
                _nextRetryAttemptTime[queueKey] = Time.realtimeSinceStartup + backoff;
                BoostOpsLogger.LogWarning("Purchases",
                    $"⏳ Transient failure ({code}, networkError={networkError}) for {queueKey} attempt={attempt}. Retry in {backoff:F0}s.");
            }
        }

        private static float ComputeBackoffSeconds(int attempt)
        {
            // Exponential with jitter, clamped to MAX. attempt is 1-indexed.
            float baseDelay = MIN_RETRY_BACKOFF_SECONDS * Mathf.Pow(2f, Mathf.Min(attempt - 1, 10));
            float jittered = baseDelay * UnityEngine.Random.Range(0.75f, 1.25f);
            return Mathf.Clamp(jittered, MIN_RETRY_BACKOFF_SECONDS, MAX_RETRY_BACKOFF_SECONDS);
        }

        private UnityWebRequest BuildPostRequest(string fullUrl, string jsonBody)
        {
            var request = new UnityWebRequest(fullUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = (int)REQUEST_TIMEOUT_SECONDS;

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("BoostOps-Project-Key", _projectKey);

#if UNITY_EDITOR
            request.SetRequestHeader("X-BoostOps-User-Agent",
                $"BoostOps-SDK/Purchases Unity/{Application.unityVersion}");
#endif
            return request;
        }

        #endregion

        #region Persistence helpers

        private void EnsureQueueDir()
        {
            try
            {
                if (string.IsNullOrEmpty(_queueDir))
                {
                    _queueDir = Path.Combine(Application.persistentDataPath, QUEUE_SUBDIR);
                }
                if (!Directory.Exists(_queueDir))
                {
                    Directory.CreateDirectory(_queueDir);
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Purchases", $"Failed to create queue dir {_queueDir}: {ex.Message}");
            }
        }

        private void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("BoostOpsPurchaseClient_Runner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<CoroutineRunner>();
        }

        private static bool WriteAtomic(string finalPath, string contents)
        {
            string tmpPath = finalPath + ".tmp";
            try
            {
                File.WriteAllText(tmpPath, contents, new UTF8Encoding(false));
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
                File.Move(tmpPath, finalPath);
                return true;
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Purchases", $"Atomic write to {finalPath} failed: {ex.Message}");
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex)
            {
                BoostOpsLogger.LogWarning("Purchases", $"Could not delete {path}: {ex.Message}");
            }
        }

        private static string SanitizeForFilename(string input)
        {
            if (string.IsNullOrEmpty(input)) return "_";
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                          (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                sb.Append(ok ? c : '_');
            }
            // Hard cap so unusually long transaction IDs don't blow up the filename.
            const int MaxLen = 200;
            if (sb.Length > MaxLen) sb.Length = MaxLen;
            return sb.ToString();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        #endregion

        #region Serialization

        // We write JSON by hand so empty optional fields are omitted (Unity's
        // JsonUtility includes everything, which the server tolerates but
        // bloats every receipt-bearing payload toward the 64KB cap).
        //
        // Layout:
        //   1. Common envelope first (schema/timestamps/identifiers/routing
        //      flags/consent/context) — emitted by the shared serializer so
        //      this stays in lockstep with the events endpoint.
        //   2. Purchase-specific fields after.
        private static string SerializeRequest(BoostOpsPurchaseRequest r)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');

            // Shared envelope identical in shape to the events payload.
            // Nonce is minted here at enqueue time and baked into the queue
            // file; retries re-send the same nonce. That's safe for
            // purchases because the server's idempotency key is
            // (project_id, store, transaction_id), not nonce — nonce is
            // only carried as a defense-in-depth replay marker so the
            // bronze layer's raw_payload matches the events shape.
            string nonce = Guid.NewGuid().ToString("N");
            BoostOpsCommonPayloadJson.AppendEnvelope(sb, r.Common, nonceOverride: nonce);

            // Purchase-specific (server-parsed) fields.
            BoostOpsCommonPayloadJson.AppendString(sb, "store", r.store);
            BoostOpsCommonPayloadJson.AppendString(sb, "transaction_id", r.transaction_id);
            if (!string.IsNullOrEmpty(r.original_transaction_id) && r.original_transaction_id != r.transaction_id)
            {
                BoostOpsCommonPayloadJson.AppendString(sb, "original_transaction_id", r.original_transaction_id);
            }
            BoostOpsCommonPayloadJson.AppendString(sb, "product_id", r.product_id);
            BoostOpsCommonPayloadJson.AppendLong(sb, "amount_micros", r.amount_micros);
            BoostOpsCommonPayloadJson.AppendString(sb, "currency", r.currency);
            if (!string.IsNullOrEmpty(r.country))        BoostOpsCommonPayloadJson.AppendString(sb, "country", r.country);
            if (!string.IsNullOrEmpty(r.receipt))        BoostOpsCommonPayloadJson.AppendString(sb, "receipt", r.receipt);
            if (!string.IsNullOrEmpty(r.receipt_format)) BoostOpsCommonPayloadJson.AppendString(sb, "receipt_format", r.receipt_format);
            if (r.is_subscription) BoostOpsCommonPayloadJson.AppendBool(sb, "is_subscription", true);
            if (r.is_trial)        BoostOpsCommonPayloadJson.AppendBool(sb, "is_trial", true);
            if (r.is_sandbox)      BoostOpsCommonPayloadJson.AppendBool(sb, "is_sandbox", true);
            BoostOpsCommonPayloadJson.AppendString(sb, "purchase_timestamp", r.purchase_timestamp);
            if (!string.IsNullOrEmpty(r.client_event_id))
            {
                BoostOpsCommonPayloadJson.AppendString(sb, "client_event_id", r.client_event_id);
            }

            // Trim trailing comma left by the last Append call.
            if (sb.Length > 1 && sb[sb.Length - 1] == ',') sb.Length--;
            sb.Append('}');
            return sb.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Wire-shape mirror of the server's PurchaseRequest. Internal — callers
    /// should use <see cref="BoostOpsPurchaseInfo"/> (the friendly typed input)
    /// and let the contract layer build this struct.
    ///
    /// The struct is split into two parts on the wire:
    ///   - <see cref="Common"/>: the shared envelope that every BoostOps
    ///     payload (events and purchases alike) carries — schema/timestamps,
    ///     four-tier identifier hierarchy, routing flags, consent, and
    ///     device/platform context.
    ///   - The remaining flat fields below: the purchase-specific data the
    ///     server's <c>PurchaseRequest</c> actually parses (store, txn,
    ///     receipt, etc.).
    ///
    /// At serialization time the two are flattened into one JSON object so
    /// the bronze layer sees one cohesive payload identical in structure to
    /// what the events endpoint receives (modulo the purchase-specific fields).
    /// </summary>
    [Serializable]
    public class BoostOpsPurchaseRequest
    {
        // Shared envelope (same shape as on the events endpoint).
        public BoostOpsCommonPayload Common;

        // Purchase-specific fields (server-parsed).
        public string store;
        public string transaction_id;
        public string original_transaction_id;
        public string product_id;
        public long amount_micros;
        public string currency;
        public string country;
        public string receipt;
        public string receipt_format;
        public bool is_subscription;
        public bool is_trial;
        public bool is_sandbox;
        public string purchase_timestamp; // ISO 8601
        public string client_event_id;
    }
}
