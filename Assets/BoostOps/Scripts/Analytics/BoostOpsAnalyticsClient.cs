using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using BoostOps.Internal;

namespace BoostOps.Analytics
{
    /// <summary>
    /// HTTP client for sending analytics events to analytics.boostops.io
    /// Simple, focused API with project key authentication and retry logic
    /// Uses single endpoint URL from configuration for maximum routing flexibility
    /// 
    /// FEATURES:
    /// • Single endpoint URL: No string concatenation - uses complete URL from config
    /// • Automatic payload cleaning: Removes empty strings, null values, and empty objects before sending
    /// • Debug logging: Shows clean JSON payloads when isDevelopmentMode=true
    /// • Unified format: Always sends {"events": [...]} format (even for single events)
    /// 
    /// DEBUG MODE: To see cleaned JSON payloads in logs, initialize with isDevelopmentMode=true:
    /// BoostOpsAnalyticsClient.Instance.Initialize(projectKey, endpointUrl, isDevelopmentMode: true)
    /// </summary>
    public class BoostOpsAnalyticsClient
    {
        #region Configuration
        
        private const string PRODUCTION_BASE_URL = "https://analytics.boostops.io";
        private const string DEVELOPMENT_BASE_URL = "https://analytics-dev.boostops.io";
        
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const float BASE_RETRY_DELAY = 1.0f;
        private const float REQUEST_TIMEOUT = 30.0f;
        private const int MAX_BATCH_SIZE = 100;
        private const int MAX_QUEUE_SIZE = 1000;
        
        #endregion
        
        #region Private Fields
        
        private static BoostOpsAnalyticsClient _instance;
        private static readonly object _lockObject = new object();
        
        // Track last app_open time to prevent duplicate events from deep links + resume handlers
        private static float _lastAppOpenTime = 0f;
        private const float APP_OPEN_DEDUP_WINDOW = 2f; // 2 seconds to prevent duplicates
        
        private string _projectKey;
        private string _eventsUrl;
        private bool _isDevelopmentMode;
        private Queue<AnalyticsEventData> _eventQueue;
        private bool _isProcessingQueue;
        private CoroutineRunner _coroutineRunner;
        
        // Analytics control and backoff state
        private bool _isAnalyticsDisabled;
        private System.DateTime _backoffUntil;
        private string _disableReason;
        
        // Retry and offline support
        private Dictionary<string, int> _failedEventRetryCount;
        private Queue<AnalyticsEventData> _offlineQueue;
        private const int MAX_RETRY_ATTEMPTS_PER_EVENT = 3;
        private const int OFFLINE_QUEUE_MAX_SIZE = 100;
        
        // Schema version validation
        private int[] _acceptedSchemaVersions = new int[] { 1, 2, 3 }; // Default: accept v1, v2, and v3
        private bool _enforceSchemaValidation = false; // Default: warn but don't block
        private bool _hasReceivedServerResponse = false; // Track if we've received schema info from server
        
        #endregion
        
        #region Public Properties
        
        /// <summary>
        /// Singleton instance of the analytics client
        /// </summary>
        public static BoostOpsAnalyticsClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new BoostOpsAnalyticsClient();
                        }
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Whether the client is initialized and ready to send events
        /// </summary>
        public bool IsInitialized => !string.IsNullOrEmpty(_projectKey);
        
        /// <summary>
        /// Whether analytics is currently disabled due to server response or backoff
        /// </summary>
        public bool IsAnalyticsDisabled => _isAnalyticsDisabled || System.DateTime.UtcNow < _backoffUntil;
        
        /// <summary>
        /// Current number of queued events waiting to be sent
        /// </summary>
        public int QueuedEventCount => _eventQueue?.Count ?? 0;
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Private constructor for singleton pattern
        /// </summary>
        private BoostOpsAnalyticsClient()
        {
            _eventQueue = new Queue<AnalyticsEventData>();
            _failedEventRetryCount = new Dictionary<string, int>();
            _offlineQueue = new Queue<AnalyticsEventData>();
            
            // Initialize immediately with project key from settings
            InitializeFromProjectSettings();
            
            // Load any persisted offline events
            LoadOfflineQueue();
        }
        
        /// <summary>
        /// Initialize the analytics client from project settings at startup
        /// </summary>
        private void InitializeFromProjectSettings()
        {
            try
            {
                var settings = InternalSettingsCache.GetProjectSettings();
                if (settings != null && !string.IsNullOrEmpty(settings.ProjectKey))
                {
                    // Initialize with project key and default endpoint
                    // Remote config will determine if events are actually sent or discarded
                    _projectKey = settings.ProjectKey;
                    _eventsUrl = "https://analytics.boostops.io/v1/events"; // Default endpoint
                    _isDevelopmentMode = false; // Not used anymore - JSON logging happens for all events
                }
                else
                {
                    BoostOpsLogger.LogWarning("Analytics", "⚠️ No project key found - analytics disabled");
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to initialize analytics client from project settings: {ex.Message}");
            }
        }
        

        
        /// <summary>
        /// Initialize the analytics client with project key (simplified authentication)
        /// </summary>
        /// <param name="projectKey">Project key (starts with bo_live_ or bo_test_)</param>
        /// <param name="endpointUrl">Full endpoint URL (e.g., https://analytics.boostops.io/v1/events)</param>
        /// <param name="isDevelopmentMode">Use development mode</param>
        public void Initialize(string projectKey, string endpointUrl, bool isDevelopmentMode = false)
        {
            if (string.IsNullOrEmpty(projectKey))
            {
                BoostOpsLogger.LogError("Analytics", "Project key cannot be null or empty");
                return;
            }
            
            if (string.IsNullOrEmpty(endpointUrl))
            {
                BoostOpsLogger.LogError("Analytics", "Endpoint URL cannot be null or empty");
                return;
            }
            
            // Validate project key format using official BoostOps specification
            if (!IsValidProjectKey(projectKey))
            {
                BoostOpsLogger.LogWarning("Analytics", "Project key format may be incorrect. Expected format: bo_{env}_{publicProjectId}_{randomSuffix}\nExample: bo_live_p7q9K2z_1f4ac6d8e7b3c2d1");
            }
            
            _projectKey = projectKey;
            _eventsUrl = endpointUrl; // Full endpoint URL (e.g., https://analytics.boostops.io/v1/events)
            _isDevelopmentMode = isDevelopmentMode;
            
            // Extract and log environment information
            string keyEnvironment = GetProjectKeyEnvironment(projectKey) ?? "unknown";
            string runtimeMode = _isDevelopmentMode ? "Development" : "Production";
            
            // Minimal init logging - only in debug mode
            if (_isDevelopmentMode)
            {
                BoostOpsLogger.LogInfo("Analytics", $"✅ Analytics initialized | Env: {keyEnvironment} | Mode: {runtimeMode} | Endpoint: {_eventsUrl}");
            }
            
            // Load any persisted analytics disable state from previous sessions
            LoadPersistedAnalyticsState();
            
            // Fresh initialization = fresh start. Clear any persisted disabled state
            // so the server gets another chance. If it 403s again, it will re-disable.
            if (_isAnalyticsDisabled)
            {
                Debug.LogWarning($"[BoostOps Analytics] 🔄 Clearing stale disabled state from previous session (was: {_disableReason}). Server will be retried.");
                ClearBackoff();
            }
            Debug.Log($"[BoostOps Analytics] 🔧 State after init: disabled={_isAnalyticsDisabled}, backoffUntil={_backoffUntil:u}, reason={_disableReason ?? "none"}, endpoint={_eventsUrl}");
            
            // Start batch processing with coroutine (consolidated threading pattern)
            StartBatchProcessing();
        }
        
        /// <summary>
        /// Update the accepted schema versions from server event response
        /// NOTE: This is ONLY called from event responses now (not from remote config)
        /// This ensures we always respect the server's real-time requirements
        /// </summary>
        /// <param name="acceptedVersions">Array of accepted schema version numbers (e.g., [1, 2])</param>
        private void SetAcceptedSchemaVersions(int[] acceptedVersions)
        {
            if (acceptedVersions != null && acceptedVersions.Length > 0)
            {
                // Only log if versions actually changed
                bool changed = _acceptedSchemaVersions == null || 
                               _acceptedSchemaVersions.Length != acceptedVersions.Length ||
                               !_acceptedSchemaVersions.SequenceEqual(acceptedVersions);
                
                _acceptedSchemaVersions = acceptedVersions;
                _hasReceivedServerResponse = true; // Mark that we now have real server data
            }
            else
            {
                // If server doesn't specify, default to permissive (accept v1, v2, and v3)
                _acceptedSchemaVersions = new int[] { 1, 2, 3 };
                _hasReceivedServerResponse = true; // Mark that we received a response (even if empty)
                BoostOpsLogger.LogWarning("Analytics", "No accepted schema versions provided by server - defaulting to [1, 2, 3]");
            }
        }
        
        /// <summary>
        /// Start batch processing using pure coroutine-based approach (consolidated threading pattern)
        /// </summary>
        private void StartBatchProcessing()
        {
            StartCoroutineRunner();
            _coroutineRunner.StartCoroutine(BatchProcessingLoop());
        }
        
        /// <summary>
        /// Main batch processing loop - runs continuously and processes queue periodically
        /// Consolidates threading to pure coroutine pattern for simplicity and Unity lifecycle compatibility
        /// </summary>
        private System.Collections.IEnumerator BatchProcessingLoop()
        {
            while (true)
            {
                // Wait 10 seconds between batch processing attempts
                yield return new WaitForSeconds(10f);
                
                // Skip if already processing or queue is empty
                if (_isProcessingQueue || _eventQueue == null || _eventQueue.Count == 0)
                    continue;
                
                // Check if we should process (10+ events for batch, or any events to send)
                var shouldProcess = _eventQueue.Count > 0;
                
                if (shouldProcess)
                {
                    _isProcessingQueue = true;
                    
                    var eventsToSend = new List<AnalyticsEventData>();
                    var batchSize = Mathf.Min(50, _eventQueue.Count);
                    
                    // Dequeue events for this batch
                    for (int i = 0; i < batchSize && _eventQueue.Count > 0; i++)
                    {
                        eventsToSend.Add(_eventQueue.Dequeue());
                    }
                    
                    if (eventsToSend.Count > 0)
                    {
                        // Send batch with retry logic
                        yield return SendBatchWithRetry(eventsToSend, MAX_RETRY_ATTEMPTS_PER_EVENT);
                    }
                    
                    _isProcessingQueue = false;
                }
            }
        }
        
        

        

        
        /// <summary>
        /// Validate BoostOps Project Key format according to official specification
        /// </summary>
        /// <param name="projectKey">Project key to validate</param>
        /// <returns>True if project key format is valid</returns>
        private static bool IsValidProjectKey(string projectKey)
        {
            if (string.IsNullOrEmpty(projectKey))
                return false;
            
            // Official BoostOps Project Key regex: ^bo_(live|test|dev)_[A-Za-z0-9]{7}_[A-Fa-f0-9]{16}$
            // Format: bo_{env}_{publicProjectId}_{randomSuffix}
            // Example: bo_live_p7q9K2z_1f4ac6d8e7b3c2d1
            var regex = new System.Text.RegularExpressions.Regex(@"^bo_(live|test|dev)_[A-Za-z0-9]{7}_[A-Fa-f0-9]{16}$");
            return regex.IsMatch(projectKey);
        }
        
        /// <summary>
        /// Extract environment from a valid BoostOps Project Key
        /// </summary>
        /// <param name="projectKey">Valid project key</param>
        /// <returns>Environment (live, test, dev) or null if invalid</returns>
        private static string GetProjectKeyEnvironment(string projectKey)
        {
            if (!IsValidProjectKey(projectKey))
                return null;
            
            var parts = projectKey.Split('_');
            return parts.Length >= 2 ? parts[1] : null;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Send a single analytics event
        /// </summary>
        /// <param name="eventData">Event data to send</param>
        /// <param name="onComplete">Callback with success status and response data</param>
        public void SendEvent(AnalyticsEventData eventData, Action<bool, AnalyticsEventResponse> onComplete = null)
        {
            // CRITICAL: Wrap everything in try-catch to prevent UI crashes
            try
        {
            if (!IsInitialized)
            {
                BoostOpsLogger.LogError("Analytics", "Analytics client not initialized. Call Initialize() first.");
                    SafeInvokeCallback(() => onComplete?.Invoke(false, null));
                return;
            }
            
            if (!ValidateEventData(eventData))
            {
                    SafeInvokeCallback(() => onComplete?.Invoke(false, null));
                return;
            }
            
                var cleanJsonString = CreateCleanJsonString(eventData);
                
#if BOOSTOPS_DEBUG_LOGGING
                BoostOpsLogger.LogDebug("Analytics", $"📤 Sending single event to: {_eventsUrl}");
                Debug.Log($"[BoostOps Analytics] 📄 RAW JSON PAYLOAD:\n{cleanJsonString}");
#endif
                
                // Event logging removed - batch send logs show aggregate
                
                // Send all events via the unified events endpoint with safe callback wrapper
                SendEventsBatch(new List<AnalyticsEventData> { eventData }, (success, batchResponse) =>
                {
                    SafeInvokeCallback(() =>
                    {
                        var response = success && batchResponse != null ? 
                            new AnalyticsEventResponse 
                            { 
                                status = batchResponse.status,
                                install_token = null  // No longer provided by server
                            } : null;
                        onComplete?.Invoke(success, response);
                    });
                });
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"🚨 CRITICAL: Analytics SendEvent crashed - {ex.Message}. UI protected from crash.");
                SafeInvokeCallback(() => onComplete?.Invoke(false, null));
            }
        }
        
        
        /// <summary>
        /// Send multiple analytics events in a batch
        /// </summary>
        /// <param name="events">List of events to send</param>
        /// <param name="onComplete">Callback with success status and response data</param>
        public void SendEventsBatch(List<AnalyticsEventData> events, Action<bool, AnalyticsBatchResponse> onComplete = null)
        {
            // CRITICAL: Wrap everything in try-catch to prevent UI crashes
            try
        {
            if (!IsInitialized)
            {
                BoostOpsLogger.LogError("Analytics", "Analytics client not initialized. Call Initialize() first.");
                    SafeInvokeCallback(() => onComplete?.Invoke(false, null));
                return;
            }
            
            if (events == null || events.Count == 0)
            {
                BoostOpsLogger.LogWarning("Analytics", "No events to send in batch");
                    SafeInvokeCallback(() => onComplete?.Invoke(true, new AnalyticsBatchResponse { status = "success", accepted = 0, rejected = 0 }));
                return;
            }
            
            if (events.Count > MAX_BATCH_SIZE)
            {
                    BoostOpsLogger.LogWarning("Analytics", $"Batch size ({events.Count}) exceeds maximum ({MAX_BATCH_SIZE}). Taking first {MAX_BATCH_SIZE} events.");
                
                    // Take only the first MAX_BATCH_SIZE events to avoid overwhelming the API
                    events = events.Take(MAX_BATCH_SIZE).ToList();
            }
            
            // Validate all events
            var validEvents = new List<AnalyticsEventData>();
            foreach (var evt in events)
            {
                if (ValidateEventData(evt))
                {
                    validEvents.Add(evt);
                }
            }
            
            if (validEvents.Count == 0)
            {
                BoostOpsLogger.LogError("Analytics", "No valid events in batch");
                    SafeInvokeCallback(() => onComplete?.Invoke(false, null));
                return;
            }
            
                // Use coroutine version with safe callback wrapper
                SendBatchWithCallbackAsync(validEvents, (success, response) =>
                {
                    SafeInvokeCallback(() => onComplete?.Invoke(success, response));
                });
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"🚨 CRITICAL: Analytics SendEventsBatch crashed - {ex.Message}. UI protected from crash.");
                SafeInvokeCallback(() => onComplete?.Invoke(false, null));
            }
        }
        
        /// <summary>
        /// Send batch with exponential backoff retry logic
        /// </summary>
        private System.Collections.IEnumerator SendBatchWithRetry(List<AnalyticsEventData> events, int maxRetries)
        {
            if (!IsInitialized || events == null || events.Count == 0)
                yield break;
            
            // Validate events
            var validEvents = new List<AnalyticsEventData>();
            foreach (var evt in events)
            {
                if (ValidateEventData(evt))
                {
                    validEvents.Add(evt);
                }
            }
            
            if (validEvents.Count == 0)
                yield break;
            
            int retryDelay = 1; // Start at 1 second
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                bool success = false;
                bool shouldRetry = false;
                
                // Try to send the batch
                yield return SendBatchCoroutineWithResult(validEvents, (result) => 
                {
                    success = result.success;
                    shouldRetry = result.shouldRetry;
                });
                
                if (success)
                {
                    // Success - clear retry counts for these events
                    foreach (var evt in validEvents)
                    {
                        if (_failedEventRetryCount.ContainsKey(evt.nonce))
                            _failedEventRetryCount.Remove(evt.nonce);
                    }
                    yield break;
                }
                
                // Failed - check if we should retry
                if (!shouldRetry)
                {
                    BoostOpsLogger.LogWarning("Analytics", $"Non-retryable error - dropping {validEvents.Count} events");
                    yield break;
                }
                
                // Check if this is the last attempt
                if (attempt < maxRetries - 1)
                {
                    yield return new WaitForSeconds(retryDelay);
                    retryDelay *= 2; // Exponential backoff
                }
                else
                {
                    // Max retries exceeded - save to offline queue
                    BoostOpsLogger.LogWarning("Analytics", $"Max retries exceeded - saving {validEvents.Count} events offline");
                    SaveToOfflineQueue(validEvents);
                }
            }
        }
        
        /// <summary>
        /// Send batch coroutine with result callback for retry logic
        /// </summary>
        private System.Collections.IEnumerator SendBatchCoroutineWithResult(List<AnalyticsEventData> events, Action<(bool success, bool shouldRetry)> onResult)
        {
            UnityWebRequest request = null;
            var url = _eventsUrl;
            
            // Generate fresh nonce for each event (per-attempt, for replay attack prevention)
            foreach (var evt in events)
            {
                evt.nonce = System.Guid.NewGuid().ToString("N"); // 32 hex chars without dashes
            }
            
            // Create clean JSON payload
            string jsonData = null;
            try
            {
                var cleanJsonEvents = events.Select(e => CreateCleanJsonString(e)).ToArray();
                jsonData = "{\"events\":[" + string.Join(",", cleanJsonEvents) + "]}";
                
                var eventTypes = string.Join(", ", events.Select(e => e.event_type));
                Debug.Log($"[BoostOps Analytics] 📤 POST {url} | {events.Count} event(s): [{eventTypes}]\n{jsonData}");
                
                request = CreatePostRequest(url, jsonData);
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"❌ Failed to prepare batch request: {ex.Message}");
                onResult?.Invoke((false, false));
                yield break;
            }
            
            yield return request.SendWebRequest();
            
            try
            {
                var responseBody = request.downloadHandler?.text ?? "(empty)";
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[BoostOps Analytics] ✅ {request.responseCode} Response: {responseBody}");
                    HandleServerResponse(request.responseCode, request.downloadHandler?.text);
                    onResult?.Invoke((true, false));
                }
                else
                {
                    bool shouldRetry = IsRetryableError(request.responseCode, request.result);
                    Debug.LogError($"[BoostOps Analytics] ❌ {request.responseCode} {request.error} | {responseBody}{(shouldRetry ? "" : " [NON-RETRYABLE]")}");
                    
                    HandleServerResponse(request.responseCode, request.downloadHandler?.text);
                    onResult?.Invoke((false, shouldRetry));
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"❌ Error processing batch response: {ex.Message}");
                onResult?.Invoke((false, true)); // Network errors are retryable
            }
            finally
            {
                request?.Dispose();
            }
        }
        
        /// <summary>
        /// Determine if an HTTP error is retryable
        /// 5xx errors, timeouts, and network errors are retryable
        /// 4xx errors (except 429) are not retryable
        /// </summary>
        private bool IsRetryableError(long responseCode, UnityWebRequest.Result result)
        {
            // Network errors are retryable (connection issues, timeouts)
            if (result == UnityWebRequest.Result.ConnectionError || 
                result == UnityWebRequest.Result.ProtocolError && responseCode == 0)
            {
                return true;
            }
            
            // Server errors (5xx) are retryable
            if (responseCode >= 500 && responseCode < 600)
            {
                return true;
            }
            
            // Rate limiting (429) is retryable
            if (responseCode == 429)
            {
                return true;
            }
            
            // Client errors (4xx except 429) are not retryable
            if (responseCode >= 400 && responseCode < 500)
            {
                return false;
            }
            
            // Default: retryable
            return true;
        }
        
        /// <summary>
        /// Fire-and-forget coroutine for sending events without callbacks
        /// </summary>
        private System.Collections.IEnumerator SendBatchFireAndForgetCoroutine(List<AnalyticsEventData> events)
        {
            UnityWebRequest request = null;
            
            var url = _eventsUrl;
            
            // Create clean JSON payload without empty fields (outside try-catch for safety)
            string jsonData = null;
            try
            {
                var cleanJsonEvents = events.Select(e => CreateCleanJsonString(e)).ToArray();
                jsonData = "{\"events\":[" + string.Join(",", cleanJsonEvents) + "]}";
                
                var eventTypes = string.Join(", ", events.Select(e => e.event_type));
                Debug.Log($"[BoostOps Analytics] 📤 POST {url} | {events.Count} event(s): [{eventTypes}]\n{jsonData}");
                
                request = CreatePostRequest(url, jsonData);
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"❌ Failed to prepare batch request: {ex.Message}");
                yield break;
            }
            
            yield return request.SendWebRequest();
            
            try
            {
                var responseBody = request.downloadHandler?.text ?? "(empty)";
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[BoostOps Analytics] ✅ {request.responseCode} Response: {responseBody}");
                }
                else
                {
                    Debug.LogError($"[BoostOps Analytics] ❌ {request.responseCode} {request.error} | {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps Analytics] ❌ Exception: {ex.Message}");
            }
            finally
            {
                request?.Dispose();
            }
        }
        
        /// <summary>
        /// Send batch with callback support for legacy API - NON-BLOCKING
        /// </summary>
        private void SendBatchWithCallbackAsync(List<AnalyticsEventData> events, Action<bool, AnalyticsBatchResponse> onComplete)
        {
            // Fire-and-forget coroutine to prevent UI blocking
            StartCoroutineRunner();
            _coroutineRunner.StartCoroutine(SendBatchCoroutine(events, onComplete));
        }
        
        /// <summary>
        /// Non-blocking coroutine for sending events with callback
        /// </summary>
        private System.Collections.IEnumerator SendBatchCoroutine(List<AnalyticsEventData> events, Action<bool, AnalyticsBatchResponse> onComplete)
        {
            UnityWebRequest request = null;
            
            var url = _eventsUrl;
            
            // Generate fresh nonce for each event (per-attempt, for replay attack prevention)
            foreach (var evt in events)
            {
                evt.nonce = System.Guid.NewGuid().ToString("N"); // 32 hex chars without dashes
            }
            
            // Create clean JSON payload without empty fields (outside try-catch for safety)
            string jsonData = null;
            try
            {
                var cleanJsonEvents = events.Select(e => CreateCleanJsonString(e)).ToArray();
                jsonData = "{\"events\":[" + string.Join(",", cleanJsonEvents) + "]}";
                
                var eventTypes = string.Join(", ", events.Select(e => e.event_type));
                Debug.Log($"[BoostOps Analytics] 📤 POST {url} | {events.Count} event(s): [{eventTypes}]\n{jsonData}");
                
                request = CreatePostRequest(url, jsonData);
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"❌ Failed to prepare batch request: {ex.Message}");
                SafeInvokeCallback(() => onComplete?.Invoke(false, null));
                yield break;
            }
            
            // Yield return MUST be outside try-catch
            yield return request.SendWebRequest();
            
            // Handle response (can be in try-catch since no yield)
            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string rawResponse = request.downloadHandler.text;
                        Debug.Log($"[BoostOps Analytics] ✅ {request.responseCode} Response: {rawResponse}");
                        
                        var response = JsonUtility.FromJson<AnalyticsBatchResponse>(rawResponse);
                        
                        // CRITICAL: Apply server config (kill switch, schemas, endpoint)
                        if (response != null)
                        {
                            // Update accepted schema versions
                            if (response.accepted_schema_major != null && response.accepted_schema_major.Length > 0)
                            {
                                SetAcceptedSchemaVersions(response.accepted_schema_major);
                            }
                            
                            // Update schema enforcement flag (fail-open strategy)
                            // Only enforce if server explicitly sets it to true
                            bool previousEnforcement = _enforceSchemaValidation;
                            _enforceSchemaValidation = response.enforce_schema_validation;
                            
                            // Only log when enforcement mode actually changes
                            if (_enforceSchemaValidation != previousEnforcement)
                            {
                                string mode = _enforceSchemaValidation ? "STRICT (blocking unsupported schemas)" : "PERMISSIVE (warn only)";
                                BoostOpsLogger.LogInfo("Analytics", $"📋 Schema validation mode changed: {mode}");
                            }
                            
                            // ✅ APPLY SERVER CONFIG (kill switch, schemas, endpoint)
                            // Note: disabled=true means kill switch ON (analytics disabled)
                            BoostOpsAnalyticsProvider.ApplyServerConfig(
                                disabled: response.disabled,
                                acceptedSchemas: response.accepted_schema_major,
                                endpoint: response.endpoint
                            );
                        }
                        
                        // Handle server response for analytics control
                        HandleServerResponse(request.responseCode, request.downloadHandler?.text);
                        
                        SafeInvokeCallback(() => onComplete?.Invoke(true, response));
                    }
                    catch (Exception parseEx)
                    {
                        BoostOpsLogger.LogError("Analytics", $"❌ Failed to parse batch response: {parseEx.Message}");
                        BoostOpsLogger.LogError("Analytics", $"   Raw response: {request.downloadHandler.text}");
                        SafeInvokeCallback(() => onComplete?.Invoke(false, null));
                    }
                }
                else
                {
                    var responseBody = request.downloadHandler?.text ?? "(empty)";
                    Debug.LogError($"[BoostOps Analytics] ❌ {request.responseCode} {request.error} | {responseBody}");
                    
                    HandleServerResponse(request.responseCode, request.downloadHandler?.text);
                    SafeInvokeCallback(() => onComplete?.Invoke(false, null));
                }
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"❌ Error processing batch response: {ex.Message}");
                SafeInvokeCallback(() => onComplete?.Invoke(false, null));
            }
            finally
            {
                request?.Dispose();
            }
        }
        
        /// <summary>
        /// Queue an event to be sent in the next batch (for performance)
        /// </summary>
        /// <param name="eventData">Event data to queue</param>
        public void QueueEvent(AnalyticsEventData eventData)
        {
            // Check if analytics is disabled due to server response or backoff
            if (IsAnalyticsDisabled)
            {
                Debug.LogWarning($"[BoostOps Analytics] ⛔ Event DROPPED (analytics disabled): {eventData.event_type} | reason: {_disableReason} | backoff until: {_backoffUntil:u}");
                return;
            }
            
            // CRITICAL: Only validate schema version AFTER receiving server response
            // Don't warn on events queued before we know what the server actually accepts
            if (_hasReceivedServerResponse && _acceptedSchemaVersions != null && _acceptedSchemaVersions.Length > 0)
            {
                if (!_acceptedSchemaVersions.Contains(eventData.schema_version))
                {
                    if (_enforceSchemaValidation)
                    {
                        // Strict mode: Block the event
                        BoostOpsLogger.LogWarning("Analytics", 
                            $"🚫 Event schema version {eventData.schema_version} not accepted by server (enforcement ACTIVE). " +
                            $"Accepted versions: [{string.Join(", ", _acceptedSchemaVersions)}]. " +
                            $"Event type '{eventData.event_type}' will be BLOCKED. " +
                            $"SDK may need updating or server config needs to accept schema v{eventData.schema_version}.");
                        return; // DROP THE EVENT
                    }
                    else
                    {
                        // Permissive mode: Warn but allow
                        BoostOpsLogger.LogWarning("Analytics", 
                            $"⚠️ Event schema version {eventData.schema_version} not accepted by server. " +
                            $"Accepted versions: [{string.Join(", ", _acceptedSchemaVersions)}]. " +
                            $"Event type '{eventData.event_type}' will be sent anyway (permissive mode). " +
                            $"SDK or server config may need updating.");
                        // Continue to queue the event
                    }
                }
            }
            // Else: No server response yet - queue the event without validation (fail-open)
            
            if (!ValidateEventData(eventData))
            {
                return;
            }
            
            // Queue logging removed - batch send logs show aggregate
            
            if (_eventQueue.Count >= MAX_QUEUE_SIZE)
            {
                BoostOpsLogger.LogWarning("Analytics", "Event queue full. Dropping oldest event.");
                _eventQueue.Dequeue();
            }
            
            // Queue the original event (cleaning happens during JSON serialization)
            _eventQueue.Enqueue(eventData);
            Debug.Log($"[BoostOps Analytics] 📥 Queued: {eventData.event_type} | queue size: {_eventQueue.Count} | endpoint: {_eventsUrl}");
        }
        
        /// <summary>
        /// Flush all queued events immediately
        /// </summary>
        /// <param name="onComplete">Callback when flush is complete</param>
        public void FlushQueue(Action<bool> onComplete = null)
        {
            if (_eventQueue.Count == 0)
            {
                Debug.Log("[BoostOps Analytics] 🚿 FlushQueue called but queue is empty");
                onComplete?.Invoke(true);
                return;
            }
            
            var eventsToSend = new List<AnalyticsEventData>();
            while (_eventQueue.Count > 0)
            {
                eventsToSend.Add(_eventQueue.Dequeue());
            }
            
            var eventTypes = string.Join(", ", eventsToSend.Select(e => e.event_type));
            Debug.Log($"[BoostOps Analytics] 🚿 Flushing {eventsToSend.Count} event(s): [{eventTypes}]");
            
            SendEventsBatch(eventsToSend, (success, response) =>
            {
                if (success)
                {
                    Debug.Log($"[BoostOps Analytics] 🚿 Flush complete - {eventsToSend.Count} event(s) sent successfully");
                }
                else
                {
                    BoostOpsLogger.LogError("Analytics", $"Failed to flush {eventsToSend.Count} queued events");
                }
                onComplete?.Invoke(success);
            });
        }
        
        /// <summary>
        /// Clear all queued events without sending them (when analytics is disabled)
        /// </summary>
        public void ClearQueue()
        {
            int eventCount;
            lock (_eventQueue)
            {
                eventCount = _eventQueue.Count;
                _eventQueue.Clear();
            }
            
            if (eventCount > 0)
            {
            }
        }
        
        #endregion
        
        #region Private Implementation
        
        /// <summary>
        /// Validate event data before sending
        /// </summary>
        private bool ValidateEventData(AnalyticsEventData eventData)
        {
            if (eventData == null)
            {
                BoostOpsLogger.LogError("Analytics", "Event data cannot be null");
                return false;
            }
            
            if (string.IsNullOrEmpty(eventData.event_type))
            {
                BoostOpsLogger.LogError("Analytics", "Event type is required");
                return false;
            }
            
            if (eventData.context == null || string.IsNullOrEmpty(eventData.context.source))
            {
                BoostOpsLogger.LogError("Analytics", "Event context and source are required");
                return false;
            }
            
            // Note: project_key is sent ONLY in HTTP header (BoostOps-Project-Key) for security, not in payload
            
            var validEventTypes = new[] { 
                BoostOpsAnalyticsContract.EventNames.IMPRESSION,
                BoostOpsAnalyticsContract.EventNames.CLICK,
                BoostOpsAnalyticsContract.EventNames.APP_OPEN,  // Includes installs (first_open=true)
                BoostOpsAnalyticsContract.EventNames.PURCHASE,
                BoostOpsAnalyticsContract.EventNames.INSTALL_ATTRIBUTION_UPDATE
            };
            if (Array.IndexOf(validEventTypes, eventData.event_type) == -1)
            {
                BoostOpsLogger.LogError("Analytics", $"Invalid event type: {eventData.event_type}");
                return false;
            }
            
            // CRITICAL: Verify install_id for purchase events (essential for revenue attribution)
            // This is especially important on Android where timing issues can cause missing install_id
            if (eventData.event_type == BoostOpsAnalyticsContract.EventNames.PURCHASE)
            {
                if (string.IsNullOrEmpty(eventData.install_id))
                {
                    BoostOpsLogger.LogError("Analytics", "❌ CRITICAL: Purchase event is missing install_id! Attempting recovery...");
                    // Attempt to recover by fetching install_id
                    eventData.install_id = BoostOps.Analytics.BoostOpsIdentifierManager.GetInstallId();
                    if (string.IsNullOrEmpty(eventData.install_id))
                    {
                        BoostOpsLogger.LogError("Analytics", "❌ FATAL: Could not recover install_id for purchase event! Event WILL BE BLOCKED.");
                        // Block the event - sending purchase without install_id is worse than not sending
                        return false;
                    }
                    else
                    {
                        BoostOpsLogger.LogInfo("Analytics", $"✅ Recovered install_id for purchase event: {eventData.install_id}");
                    }
                }
                else
                {
                    BoostOpsLogger.LogDebug("Analytics", $"✅ Purchase event has install_id: {eventData.install_id}");
                }
            }
            
            return true;
        }
        

        

        

        
        /// <summary>
        /// Create clean JSON string that excludes empty/null fields (Unity JsonUtility includes all fields)
        /// </summary>
        private string CreateCleanJsonString(AnalyticsEventData eventData)
        {
            if (eventData == null) return "null";
            
            var jsonParts = new List<string>();
            
            // Always include event_type
            jsonParts.Add($"\"event_type\":\"{eventData.event_type}\"");
            
            // Always include schema_version for safe evolution
            jsonParts.Add($"\"schema_version\":{eventData.schema_version}");
            
            // Always include timestamp for ETL compatibility
            jsonParts.Add($"\"timestamp_ms\":{eventData.timestamp_ms}");
            
            // Include elapsed_realtime_ms (monotonic clock) for fraud detection
            if (eventData.elapsed_realtime_ms.HasValue)
            {
                jsonParts.Add($"\"elapsed_realtime_ms\":{eventData.elapsed_realtime_ms.Value}");
            }
            
            // Always include event_id for database UNIQUE INDEX (source_project_id, event_id)
            if (!string.IsNullOrEmpty(eventData.event_id))
            {
                jsonParts.Add($"\"event_id\":\"{eventData.event_id}\"");
            }
            
            // Always include nonce for network replay attack prevention (fresh nonce per attempt)
            if (!string.IsNullOrEmpty(eventData.nonce))
            {
                jsonParts.Add($"\"nonce\":\"{eventData.nonce}\"");
            }
            
            // Four-tier ID hierarchy (schema v6)
            if (!string.IsNullOrEmpty(eventData.boostops_id))
            {
                jsonParts.Add($"\"boostops_id\":\"{eventData.boostops_id}\"");
            }
            
            if (!string.IsNullOrEmpty(eventData.install_id))
            {
                jsonParts.Add($"\"install_id\":\"{eventData.install_id}\"");
            }
            
            if (eventData.install_time_ms.HasValue && eventData.install_time_ms.Value > 0)
            {
                jsonParts.Add($"\"install_time_ms\":{eventData.install_time_ms.Value}");
            }
            
            if (!string.IsNullOrEmpty(eventData.custom_user_id))
            {
                jsonParts.Add($"\"custom_user_id\":\"{eventData.custom_user_id}\"");
            }
            
            if (!string.IsNullOrEmpty(eventData.session_id))
            {
                jsonParts.Add($"\"session_id\":\"{eventData.session_id}\"");
            }
            
            // TOP-LEVEL: Critical routing flags (determines which Bronze table to use)
            if (eventData.is_unity_editor.HasValue && eventData.is_unity_editor.Value)
                jsonParts.Add("\"is_unity_editor\":true");
            if (eventData.is_debug_build.HasValue && eventData.is_debug_build.Value)
                jsonParts.Add("\"is_debug_build\":true");
            if (eventData.is_testflight.HasValue && eventData.is_testflight.Value)
                jsonParts.Add("\"is_testflight\":true");
            if (eventData.is_emulator.HasValue && eventData.is_emulator.Value)
                jsonParts.Add("\"is_emulator\":true");
            
            // Note: project_key is sent ONLY in HTTP header (BoostOps-Project-Key), never in payload for security
            
            // Note: storefront_country moved to context (environmental data)
            
            // Include consent at top-level for compliance
            if (eventData.consent != null)
            {
                var consentJson = CreateCleanConsentJson(eventData.consent);
                if (!string.IsNullOrEmpty(consentJson))
                {
                    jsonParts.Add($"\"consent\":{{{consentJson}}}");
                }
                else
                {
                }
            }
            else
            {
            }
            
            // Add context if it has meaningful content
            if (eventData.context != null)
            {
                var contextJson = CreateCleanContextJson(eventData.context);
                if (!string.IsNullOrEmpty(contextJson))
                    jsonParts.Add($"\"context\":{{{contextJson}}}");
            }
            
            // Always include event object (even if empty, it's structurally important)
            if (eventData.@event != null)
            {
                var eventJson = CreateCleanEventJson(eventData.@event);
                if (!string.IsNullOrEmpty(eventJson))
                    jsonParts.Add($"\"event\":{{{eventJson}}}");
                else
                    jsonParts.Add($"\"event\":{{}}"); // Empty event object
            }
            
            return "{" + string.Join(",", jsonParts) + "}";
        }
        
        /// <summary>
        /// Create clean JSON for context data
        /// </summary>
        private string CreateCleanContextJson(EventContext context)
        {
            var contextParts = new List<string>();
            
            if (!string.IsNullOrEmpty(context.source)) contextParts.Add($"\"source\":\"{context.source}\"");
            if (!string.IsNullOrEmpty(context.platform)) contextParts.Add($"\"os\":\"{context.platform}\"");
            if (!string.IsNullOrEmpty(context.os_version)) contextParts.Add($"\"os_version\":\"{context.os_version}\"");
            if (!string.IsNullOrEmpty(context.app_version)) contextParts.Add($"\"app_version\":\"{context.app_version}\"");
            if (!string.IsNullOrEmpty(context.app_identifier)) contextParts.Add($"\"app_identifier\":\"{context.app_identifier}\"");
            if (!string.IsNullOrEmpty(context.sdk_version)) contextParts.Add($"\"sdk_version\":\"{context.sdk_version}\"");
            if (!string.IsNullOrEmpty(context.store)) contextParts.Add($"\"store\":\"{context.store}\"");
            if (!string.IsNullOrEmpty(context.store_id)) contextParts.Add($"\"store_id\":\"{context.store_id}\"");
            if (!string.IsNullOrEmpty(context.device_model)) contextParts.Add($"\"device_model\":\"{context.device_model}\"");
            if (!string.IsNullOrEmpty(context.device_brand)) contextParts.Add($"\"device_brand\":\"{context.device_brand}\"");
            if (!string.IsNullOrEmpty(context.country)) contextParts.Add($"\"country\":\"{context.country}\"");
            if (!string.IsNullOrEmpty(context.storefront_country)) contextParts.Add($"\"storefront_country\":\"{context.storefront_country}\"");
            if (!string.IsNullOrEmpty(context.region)) contextParts.Add($"\"region\":\"{context.region}\"");
            if (!string.IsNullOrEmpty(context.city)) contextParts.Add($"\"city\":\"{context.city}\"");
            if (context.timezone_offset_minutes.HasValue) contextParts.Add($"\"timezone_offset_minutes\":{context.timezone_offset_minutes.Value}");
            if (!string.IsNullOrEmpty(context.locale)) contextParts.Add($"\"locale\":\"{context.locale}\"");
            if (!string.IsNullOrEmpty(context.language)) contextParts.Add($"\"language\":\"{context.language}\"");
            if (!string.IsNullOrEmpty(context.carrier)) contextParts.Add($"\"carrier\":\"{context.carrier}\"");
            if (!string.IsNullOrEmpty(context.connection_type)) contextParts.Add($"\"connection_type\":\"{context.connection_type}\"");
            if (!string.IsNullOrEmpty(context.ip_address)) contextParts.Add($"\"ip_address\":\"{context.ip_address}\"");
            // Note: timestamp is at top-level (milliseconds precision) - not duplicated in context
            
            // Device identifiers (cross-app correlation)
            // Note: install_id and custom_user_id moved to top-level (schema v6)
            if (!string.IsNullOrEmpty(context.app_account_token)) contextParts.Add($"\"app_account_token\":\"{context.app_account_token}\"");
            if (!string.IsNullOrEmpty(context.idfv)) contextParts.Add($"\"idfv\":\"{context.idfv}\"");
            if (!string.IsNullOrEmpty(context.idfa)) contextParts.Add($"\"idfa\":\"{context.idfa}\"");
            if (!string.IsNullOrEmpty(context.asid_sha256)) contextParts.Add($"\"asid_sha256\":\"{context.asid_sha256}\"");
            if (!string.IsNullOrEmpty(context.gaid)) contextParts.Add($"\"gaid\":\"{context.gaid}\"");
            if (!string.IsNullOrEmpty(context.firebase_app_id)) contextParts.Add($"\"firebase_app_id\":\"{context.firebase_app_id}\"");
            if (!string.IsNullOrEmpty(context.windows_device_id)) contextParts.Add($"\"windows_device_id\":\"{context.windows_device_id}\"");
            if (!string.IsNullOrEmpty(context.windows_machine_guid)) contextParts.Add($"\"windows_machine_guid\":\"{context.windows_machine_guid}\"");
            if (!string.IsNullOrEmpty(context.msaid)) contextParts.Add($"\"msaid\":\"{context.msaid}\"");
            
            // Environment detection
            if (!string.IsNullOrEmpty(context.environment)) contextParts.Add($"\"environment\":\"{context.environment}\"");
            if (!string.IsNullOrEmpty(context.installer_source)) contextParts.Add($"\"installer_source\":\"{context.installer_source}\"");
            
            // Privacy consent data is now handled at the top-level event data
            
            return string.Join(",", contextParts);
        }
        
        /// <summary>
        /// Create clean JSON for consent data (privacy compliance)
        /// </summary>
        private string CreateCleanConsentJson(ConsentData consent)
        {
            var consentParts = new List<string>();
            
            // Framework identification (backward compatible + enhanced)
            if (!string.IsNullOrEmpty(consent.framework)) 
                consentParts.Add($"\"framework\":\"{consent.framework}\"");
            if (consent.gdpr_consent_required.HasValue) 
                consentParts.Add($"\"gdpr_required\":{consent.gdpr_consent_required.Value.ToString().ToLower()}");
            if (consent.ccpa_consent_required.HasValue) 
                consentParts.Add($"\"ccpa_required\":{consent.ccpa_consent_required.Value.ToString().ToLower()}");
            
            // Consent timestamps and metadata (enhanced fields)
            if (consent.consent_timestamp.HasValue) 
                consentParts.Add($"\"timestamp\":{consent.consent_timestamp.Value}");
            if (!string.IsNullOrEmpty(consent.consent_version)) 
                consentParts.Add($"\"version\":\"{consent.consent_version}\"");
            if (!string.IsNullOrEmpty(consent.consent_language)) 
                consentParts.Add($"\"language\":\"{consent.consent_language}\"");
            if (!string.IsNullOrEmpty(consent.consent_method)) 
                consentParts.Add($"\"method\":\"{consent.consent_method}\"");
            if (!string.IsNullOrEmpty(consent.consent_source)) 
                consentParts.Add($"\"source\":\"{consent.consent_source}\"");
            if (!string.IsNullOrEmpty(consent.legal_basis)) 
                consentParts.Add($"\"legal_basis\":\"{consent.legal_basis}\"");
            
            // Legacy TCF/consent string support
            if (!string.IsNullOrEmpty(consent.consent_string)) 
                consentParts.Add($"\"consent_string\":\"{consent.consent_string}\"");
            
            // GDPR-specific consent (structured)
            if (consent.gdpr != null)
            {
                var gdprParts = new List<string>();
                if (consent.gdpr.applies.HasValue) 
                    gdprParts.Add($"\"applies\":{consent.gdpr.applies.Value.ToString().ToLower()}");
                if (consent.gdpr.consent_given.HasValue) 
                    gdprParts.Add($"\"consent_given\":{consent.gdpr.consent_given.Value.ToString().ToLower()}");
                if (consent.gdpr.analytics.HasValue) 
                    gdprParts.Add($"\"analytics\":{consent.gdpr.analytics.Value.ToString().ToLower()}");
                if (consent.gdpr.advertising.HasValue) 
                    gdprParts.Add($"\"advertising\":{consent.gdpr.advertising.Value.ToString().ToLower()}");
                if (consent.gdpr.measurement.HasValue) 
                    gdprParts.Add($"\"measurement\":{consent.gdpr.measurement.Value.ToString().ToLower()}");
                if (!string.IsNullOrEmpty(consent.gdpr.legal_basis)) 
                    gdprParts.Add($"\"legal_basis\":\"{consent.gdpr.legal_basis}\"");
                
                if (gdprParts.Count > 0)
                    consentParts.Add($"\"gdpr\":{{{string.Join(",", gdprParts)}}}");
            }
            
            // ATT (iOS App Tracking Transparency)
            if (consent.att != null)
            {
                var attParts = new List<string>();
                if (!string.IsNullOrEmpty(consent.att.status)) 
                    attParts.Add($"\"status\":\"{consent.att.status}\"");
                if (consent.att.authorized_time.HasValue) 
                    attParts.Add($"\"authorized_time\":{consent.att.authorized_time.Value}");
                if (consent.att.idfa_available.HasValue) 
                    attParts.Add($"\"idfa_available\":{consent.att.idfa_available.Value.ToString().ToLower()}");
                
                if (attParts.Count > 0)
                    consentParts.Add($"\"att\":{{{string.Join(",", attParts)}}}");
            }
            
            // Android privacy settings
            if (consent.android != null)
            {
                var androidParts = new List<string>();
                if (consent.android.advertising_id.HasValue) 
                    androidParts.Add($"\"advertising_id\":{consent.android.advertising_id.Value.ToString().ToLower()}");
                if (consent.android.limited_ad_tracking.HasValue) 
                    androidParts.Add($"\"limited_ad_tracking\":{consent.android.limited_ad_tracking.Value.ToString().ToLower()}");
                
                if (androidParts.Count > 0)
                    consentParts.Add($"\"android\":{{{string.Join(",", androidParts)}}}");
            }
            
            // Withdrawal tracking (enhanced fields)
            if (consent.withdrawal_timestamp.HasValue) 
                consentParts.Add($"\"withdrawal_timestamp\":{consent.withdrawal_timestamp.Value}");
            if (!string.IsNullOrEmpty(consent.withdrawal_method)) 
                consentParts.Add($"\"withdrawal_method\":\"{consent.withdrawal_method}\"");
            
            return string.Join(",", consentParts);
        }
        
        /// <summary>
        /// Create clean JSON for event data (only non-empty fields)
        /// </summary>
        private string CreateCleanEventJson(EventData eventData)
        {
            var eventParts = new List<string>();
            
            // Attribution & Identity
            // Note: boostops_id and session_id moved to top-level, no longer in event data
            if (!string.IsNullOrEmpty(eventData.user_id)) eventParts.Add($"\"user_id\":\"{eventData.user_id}\"");
            
            // Cross-Promotion Attribution
            // Note: source_store_id is in context.store_id (universal) - not duplicated here
            // Note: source_project_id is derived server-side from project_key (not sent from SDK)
            if (!string.IsNullOrEmpty(eventData.target_store_id)) eventParts.Add($"\"target_store_id\":\"{eventData.target_store_id}\"");
            if (!string.IsNullOrEmpty(eventData.target_project_id)) eventParts.Add($"\"target_project_id\":\"{eventData.target_project_id}\"");
            if (!string.IsNullOrEmpty(eventData.network_campaign_id)) eventParts.Add($"\"network_campaign_id\":\"{eventData.network_campaign_id}\"");
            if (!string.IsNullOrEmpty(eventData.placement_id)) eventParts.Add($"\"placement_id\":\"{eventData.placement_id}\"");
            
            // Campaign attribution  
            if (eventData.campaign_id.HasValue) eventParts.Add($"\"campaign_id\":{eventData.campaign_id}");
            if (!string.IsNullOrEmpty(eventData.campaign_slug)) eventParts.Add($"\"campaign_slug\":\"{eventData.campaign_slug}\"");
            if (eventData.creative_id.HasValue) eventParts.Add($"\"creative_id\":{eventData.creative_id}");
            if (!string.IsNullOrEmpty(eventData.keyword)) eventParts.Add($"\"keyword\":\"{eventData.keyword}\"");
            
            // App context
            if (!string.IsNullOrEmpty(eventData.project_slug)) eventParts.Add($"\"project_slug\":\"{eventData.project_slug}\"");
            
            // Revenue & Commerce
            if (!string.IsNullOrEmpty(eventData.currency)) eventParts.Add($"\"currency\":\"{eventData.currency}\"");
            if (eventData.amount_micros.HasValue) eventParts.Add($"\"amount_micros\":{eventData.amount_micros}");
            if (eventData.tax_micros.HasValue) eventParts.Add($"\"tax_micros\":{eventData.tax_micros}");
            if (eventData.discount_micros.HasValue) eventParts.Add($"\"discount_micros\":{eventData.discount_micros}");
            
            // Product details
            if (!string.IsNullOrEmpty(eventData.product_id)) eventParts.Add($"\"product_id\":\"{eventData.product_id}\"");
            if (!string.IsNullOrEmpty(eventData.product_name)) eventParts.Add($"\"product_name\":\"{eventData.product_name}\"");
            if (!string.IsNullOrEmpty(eventData.product_category)) eventParts.Add($"\"product_category\":\"{eventData.product_category}\"");
            if (eventData.quantity.HasValue) eventParts.Add($"\"quantity\":{eventData.quantity}");
            if (!string.IsNullOrEmpty(eventData.transaction_id)) eventParts.Add($"\"transaction_id\":\"{eventData.transaction_id}\"");
            if (!string.IsNullOrEmpty(eventData.receipt))
            {
                var escapedReceipt = eventData.receipt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                eventParts.Add($"\"receipt\":\"{escapedReceipt}\"");
            }
            
            // Commerce context
            if (eventData.is_trial.HasValue) eventParts.Add($"\"is_trial\":{(eventData.is_trial.Value ? "true" : "false")}");
            if (eventData.is_subscription.HasValue) eventParts.Add($"\"is_subscription\":{(eventData.is_subscription.Value ? "true" : "false")}");
            if (!string.IsNullOrEmpty(eventData.billing_period)) eventParts.Add($"\"billing_period\":\"{eventData.billing_period}\"");
            if (eventData.renewal_number.HasValue) eventParts.Add($"\"renewal_number\":{eventData.renewal_number}");
            
            // Cross-promotion specific
            if (!string.IsNullOrEmpty(eventData.format)) eventParts.Add($"\"format\":\"{eventData.format}\"");
            if (!string.IsNullOrEmpty(eventData.channel)) eventParts.Add($"\"channel\":\"{eventData.channel}\"");
            if (eventData.duration_ms.HasValue) eventParts.Add($"\"duration_ms\":{eventData.duration_ms}");
            if (eventData.viewable.HasValue) eventParts.Add($"\"viewable\":{(eventData.viewable.Value ? "true" : "false")}");
            if (eventData.above_fold.HasValue) eventParts.Add($"\"above_fold\":{(eventData.above_fold.Value ? "true" : "false")}");
            if (eventData.completion_rate.HasValue) eventParts.Add($"\"completion_rate\":{eventData.completion_rate}");
            
            // Impression ↔ Click linkage (industry standard)
            if (!string.IsNullOrEmpty(eventData.impression_id)) eventParts.Add($"\"impression_id\":\"{eventData.impression_id}\"");
            if (eventData.impression_timestamp.HasValue) eventParts.Add($"\"impression_timestamp\":{eventData.impression_timestamp}");
            if (!string.IsNullOrEmpty(eventData.container_impression_id)) eventParts.Add($"\"container_impression_id\":\"{eventData.container_impression_id}\"");
            
            // Click specific (only include coordinates if not 0,0)
            if (eventData.click_coordinates != null && (eventData.click_coordinates.x != 0 || eventData.click_coordinates.y != 0))
                eventParts.Add($"\"click_coordinates\":{{\"x\":{eventData.click_coordinates.x},\"y\":{eventData.click_coordinates.y}}}");
            if (eventData.time_to_click_ms.HasValue) eventParts.Add($"\"time_to_click_ms\":{eventData.time_to_click_ms}");
            if (!string.IsNullOrEmpty(eventData.click_id)) eventParts.Add($"\"click_id\":\"{eventData.click_id}\"");
            if (!string.IsNullOrEmpty(eventData.referrer)) eventParts.Add($"\"referrer\":\"{eventData.referrer}\"");
            if (eventData.click_through_rate.HasValue) eventParts.Add($"\"click_through_rate\":{eventData.click_through_rate}");
            
            // Cross-App Navigation
            if (!string.IsNullOrEmpty(eventData.deep_link_url)) eventParts.Add($"\"deep_link_url\":\"{eventData.deep_link_url}\"");
            if (!string.IsNullOrEmpty(eventData.redirect_url)) eventParts.Add($"\"redirect_url\":\"{eventData.redirect_url}\"");
            if (eventData.store_redirect.HasValue) eventParts.Add($"\"store_redirect\":{(eventData.store_redirect.Value ? "true" : "false")}");
            if (eventData.attribution_window_hours.HasValue) eventParts.Add($"\"attribution_window_hours\":{eventData.attribution_window_hours}");
            
            // Revenue Context
            if (eventData.revenue_share_rate.HasValue) eventParts.Add($"\"revenue_share_rate\":{eventData.revenue_share_rate}");
            if (eventData.estimated_cpm_micros.HasValue) eventParts.Add($"\"estimated_cpm_micros\":{eventData.estimated_cpm_micros}");
            if (eventData.impression_value_micros.HasValue) eventParts.Add($"\"impression_value_micros\":{eventData.impression_value_micros}");
            if (eventData.click_value_micros.HasValue) eventParts.Add($"\"click_value_micros\":{eventData.click_value_micros}");
            
            // Install specific
            if (eventData.organic.HasValue) eventParts.Add($"\"organic\":{(eventData.organic.Value ? "true" : "false")}");
            if (eventData.reinstall.HasValue) eventParts.Add($"\"reinstall\":{(eventData.reinstall.Value ? "true" : "false")}");
            if (eventData.install_size_bytes.HasValue) eventParts.Add($"\"install_size_bytes\":{eventData.install_size_bytes}");
            if (eventData.install_duration_ms.HasValue) eventParts.Add($"\"install_duration_ms\":{eventData.install_duration_ms}");
            
            // App open specific
            if (eventData.first_open.HasValue) eventParts.Add($"\"first_open\":{(eventData.first_open.Value ? "true" : "false")}");
            if (!string.IsNullOrEmpty(eventData.launch_type)) eventParts.Add($"\"launch_type\":\"{eventData.launch_type}\"");
            if (!string.IsNullOrEmpty(eventData.deep_link_url)) eventParts.Add($"\"deep_link_url\":\"{eventData.deep_link_url}\"");
            if (eventData.time_since_install_ms.HasValue) eventParts.Add($"\"time_since_install_ms\":{eventData.time_since_install_ms}");
            
            // Device identification fields (app open events)
            // Note: network_type is in context.connection_type (universal) - not duplicated here
            // Note: country is in context.country (universal) - not duplicated here
            // Note: locale is in context.locale (universal) - not duplicated here
            // Note: language is in context.language (universal) - not duplicated here
            // Note: timezone_offset_minutes is in context (universal) - not duplicated here
            if (eventData.screen_width.HasValue) eventParts.Add($"\"screen_width\":{eventData.screen_width}");
            if (eventData.screen_height.HasValue) eventParts.Add($"\"screen_height\":{eventData.screen_height}");
            if (!string.IsNullOrEmpty(eventData.device_orientation)) eventParts.Add($"\"device_orientation\":\"{eventData.device_orientation}\"");
            
            // Attribution update specific
            if (!string.IsNullOrEmpty(eventData.attribution_source)) eventParts.Add($"\"attribution_source\":\"{eventData.attribution_source}\"");
            if (!string.IsNullOrEmpty(eventData.attribution_method)) eventParts.Add($"\"attribution_method\":\"{eventData.attribution_method}\"");
            if (eventData.attribution_confidence.HasValue) eventParts.Add($"\"attribution_confidence\":{eventData.attribution_confidence}");
            
            // Install-time identifiers (first_open events only)
            if (!string.IsNullOrEmpty(eventData.asa_token)) eventParts.Add($"\"asa_token\":\"{eventData.asa_token}\"");
            if (!string.IsNullOrEmpty(eventData.skan_source_id)) eventParts.Add($"\"skan_source_id\":\"{eventData.skan_source_id}\"");
            if (!string.IsNullOrEmpty(eventData.install_referrer_click_id)) eventParts.Add($"\"install_referrer_click_id\":\"{eventData.install_referrer_click_id}\"");
            if (!string.IsNullOrEmpty(eventData.attribution_click_id)) eventParts.Add($"\"attribution_click_id\":\"{eventData.attribution_click_id}\"");
            
            // Google Play Install Referrer data (Android first_open events only)
            if (eventData.play_install_referrer != null && !string.IsNullOrEmpty(eventData.play_install_referrer.referrer))
            {
                var referrerParts = new List<string>();
                referrerParts.Add($"\"referrer\":\"{eventData.play_install_referrer.referrer}\"");
                if (eventData.play_install_referrer.click_ts.HasValue) 
                    referrerParts.Add($"\"click_ts\":{eventData.play_install_referrer.click_ts.Value}");
                if (eventData.play_install_referrer.install_begin_ts.HasValue) 
                    referrerParts.Add($"\"install_begin_ts\":{eventData.play_install_referrer.install_begin_ts.Value}");
                if (!string.IsNullOrEmpty(eventData.play_install_referrer.click_id))
                    referrerParts.Add($"\"click_id\":\"{eventData.play_install_referrer.click_id}\"");
                
                eventParts.Add($"\"play_install_referrer\":{{{string.Join(",", referrerParts)}}}");
            }
            
            // Apple Search Ads attribution data (iOS first_open events only)
            if (eventData.apple_search_ads != null && !string.IsNullOrEmpty(eventData.apple_search_ads.token))
            {
                var asaParts = new List<string>();
                asaParts.Add($"\"token\":\"{eventData.apple_search_ads.token}\"");
                if (eventData.apple_search_ads.campaign_id.HasValue)
                    asaParts.Add($"\"campaign_id\":{eventData.apple_search_ads.campaign_id.Value}");
                if (eventData.apple_search_ads.ad_group_id.HasValue)
                    asaParts.Add($"\"ad_group_id\":{eventData.apple_search_ads.ad_group_id.Value}");
                if (eventData.apple_search_ads.keyword_id.HasValue)
                    asaParts.Add($"\"keyword_id\":{eventData.apple_search_ads.keyword_id.Value}");
                if (eventData.apple_search_ads.creative_set_id.HasValue)
                    asaParts.Add($"\"creative_set_id\":{eventData.apple_search_ads.creative_set_id.Value}");
                
                eventParts.Add($"\"apple_search_ads\":{{{string.Join(",", asaParts)}}}");
            }
            
            // SKAdNetwork postback data (iOS attribution update events)
            if (eventData.skan != null && !string.IsNullOrEmpty(eventData.skan.version))
            {
                var skanParts = new List<string>();
                skanParts.Add($"\"version\":\"{eventData.skan.version}\"");
                if (eventData.skan.postback_sequence.HasValue)
                    skanParts.Add($"\"postback_sequence\":{eventData.skan.postback_sequence.Value}");
                if (eventData.skan.conversion_value.HasValue)
                    skanParts.Add($"\"conversion_value\":{eventData.skan.conversion_value.Value}");
                if (!string.IsNullOrEmpty(eventData.skan.coarse_value))
                    skanParts.Add($"\"coarse_value\":\"{eventData.skan.coarse_value}\"");
                if (!string.IsNullOrEmpty(eventData.skan.source_identifier))
                    skanParts.Add($"\"source_identifier\":\"{eventData.skan.source_identifier}\"");
                if (eventData.skan.fidelity_type.HasValue)
                    skanParts.Add($"\"fidelity_type\":{eventData.skan.fidelity_type.Value}");
                if (eventData.skan.lock_window.HasValue)
                    skanParts.Add($"\"lock_window\":{(eventData.skan.lock_window.Value ? "true" : "false")}");
                if (eventData.skan.redownload.HasValue)
                    skanParts.Add($"\"redownload\":{(eventData.skan.redownload.Value ? "true" : "false")}");
                if (eventData.skan.campaign_id.HasValue)
                    skanParts.Add($"\"campaign_id\":{eventData.skan.campaign_id.Value}");
                if (!string.IsNullOrEmpty(eventData.skan.attribution_signature))
                    skanParts.Add($"\"attribution_signature\":\"{eventData.skan.attribution_signature}\"");
                if (eventData.skan.postback_timestamp.HasValue)
                    skanParts.Add($"\"postback_timestamp\":{eventData.skan.postback_timestamp.Value}");
                
                eventParts.Add($"\"skan\":{{{string.Join(",", skanParts)}}}");
            }
            
            // AdAttributionKit data (iOS 17.4+ attribution)
            if (eventData.aak != null && !string.IsNullOrEmpty(eventData.aak.conversion_type))
            {
                var aakParts = new List<string>();
                aakParts.Add($"\"conversion_type\":\"{eventData.aak.conversion_type}\"");
                if (!string.IsNullOrEmpty(eventData.aak.marketplace_identifier))
                    aakParts.Add($"\"marketplace_identifier\":\"{eventData.aak.marketplace_identifier}\"");
                if (eventData.aak.attribution_window.HasValue)
                    aakParts.Add($"\"attribution_window\":{eventData.aak.attribution_window.Value}");
                if (eventData.aak.cooldown_window.HasValue)
                    aakParts.Add($"\"cooldown_window\":{eventData.aak.cooldown_window.Value}");
                
                eventParts.Add($"\"aak\":{{{string.Join(",", aakParts)}}}");
            }
            
            // Device identifiers (hashed)
            if (!string.IsNullOrEmpty(eventData.idfa_hash)) eventParts.Add($"\"idfa_hash\":\"{eventData.idfa_hash}\"");
            if (!string.IsNullOrEmpty(eventData.idfv_hash)) eventParts.Add($"\"idfv_hash\":\"{eventData.idfv_hash}\"");
            if (!string.IsNullOrEmpty(eventData.gaid_hash)) eventParts.Add($"\"gaid_hash\":\"{eventData.gaid_hash}\"");
            if (!string.IsNullOrEmpty(eventData.android_id_hash)) eventParts.Add($"\"android_id_hash\":\"{eventData.android_id_hash}\"");
            if (!string.IsNullOrEmpty(eventData.custom_user_id)) eventParts.Add($"\"custom_user_id\":\"{eventData.custom_user_id}\"");
            if (!string.IsNullOrEmpty(eventData.fingerprint_hash)) eventParts.Add($"\"fingerprint_hash\":\"{eventData.fingerprint_hash}\"");
            
            // App Wall specific: Serialize items array (nested item impression data)
            if (eventData.items != null && eventData.items.Count > 0)
            {
                try
                {
                    var itemsJsonParts = new List<string>();
                    foreach (var item in eventData.items)
                    {
                        var itemParts = new List<string>();
                        foreach (var kvp in item)
                        {
                            // Serialize each key-value pair in the item
                            if (kvp.Value != null)
                            {
                                if (kvp.Value is string strValue)
                                {
                                    // Escape quotes in string values
                                    var escapedValue = strValue.Replace("\\", "\\\\").Replace("\"", "\\\"");
                                    itemParts.Add($"\"{kvp.Key}\":\"{escapedValue}\"");
                                }
                                else if (kvp.Value is int || kvp.Value is long || kvp.Value is float || kvp.Value is double || kvp.Value is bool)
                                {
                                    // Numeric and boolean values don't need quotes
                                    var valueStr = kvp.Value.ToString().ToLower(); // Lowercase for bool (true/false)
                                    itemParts.Add($"\"{kvp.Key}\":{valueStr}");
                                }
                                else
                                {
                                    // For other types, convert to string and quote
                                    var valueStr = kvp.Value.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
                                    itemParts.Add($"\"{kvp.Key}\":\"{valueStr}\"");
                                }
                            }
                        }
                        if (itemParts.Count > 0)
                        {
                            itemsJsonParts.Add("{" + string.Join(",", itemParts) + "}");
                        }
                    }
                    
                    if (itemsJsonParts.Count > 0)
                    {
                        var itemsJson = $"\"items\":[{string.Join(",", itemsJsonParts)}]";
                        eventParts.Add(itemsJson);
                    }
                    else
                    {
                        Debug.LogWarning("[Analytics] Items array exists but no items were serialized");
                    }
                }
                catch (System.Exception ex)
                {
                    BoostOpsLogger.LogError("Analytics", $"Failed to serialize items array: {ex.Message}");
                }
            }
            // else
            // {
            //     Debug.Log($"[Analytics] Items array is null or empty: null={eventData.items == null}, count={eventData.items?.Count ?? 0}");
            // }
            
            return string.Join(",", eventParts);
        }
        
        /// <summary>
        /// Create HTTP POST request with project key authentication
        /// </summary>
        private UnityWebRequest CreatePostRequest(string fullUrl, string jsonData)
        {
            var request = new UnityWebRequest(fullUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = (int)REQUEST_TIMEOUT;
            
            // Project key authentication headers
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("BoostOps-Project-Key", _projectKey);
            
#if UNITY_EDITOR
            // Editor only: Custom header for debugging (Unity Editor doesn't reliably send User-Agent)
            request.SetRequestHeader("X-BoostOps-User-Agent", $"BoostOps-SDK/2.0.6 Unity/{Application.unityVersion}");
#endif
            // Device builds: Unity automatically sends standard User-Agent
            // e.g., "UnityPlayer/2022.3.10f1 (iPhone; iOS 17.2; Scale/3.00)"
            
            return request;
        }
        

        

        
        /// <summary>
        /// Handle server response for analytics control (kill switch, versioning, etc.)
        /// </summary>
        private void HandleServerResponse(long responseCode, string responseText)
        {
            try
            {
                switch (responseCode)
                {
                    case 401: // AUTH_ERROR - project key not authorized for this platform
                        _isAnalyticsDisabled = true;
                        _disableReason = "Auth error (401) - project key not authorized for this platform";
                        BoostOpsLogger.LogError("Analytics", "🔑 Auth error (401) - project key not authorized. Analytics disabled for this session. Check platform authorization on BoostOps dashboard.");
                        break;
                        
                    case 403: // PROJECT_DISABLED (kill switch)
                        _isAnalyticsDisabled = true;
                        _disableReason = "Project disabled by server";
                        BoostOpsLogger.LogWarning("Analytics", "🚫 Analytics KILLED by server (403) - project disabled permanently until re-enabled");
                        PersistAnalyticsState();
                        break;
                        
                    case 410: // GONE - endpoint retired
                        _isAnalyticsDisabled = true;
                        _disableReason = "Analytics endpoint retired";
                        BoostOpsLogger.LogWarning("Analytics", "🚫 Analytics permanently disabled (410) - endpoint retired");
                        PersistAnalyticsState();
                        break;
                        
                    case 426: // UPGRADE_REQUIRED
                        var backoffSeconds = ParseBackoffFromResponse(responseText) ?? 86400; // Default 24h
                        SetBackoff(backoffSeconds, "SDK upgrade required");
                        BoostOpsLogger.LogWarning("Analytics", $"🚫 Analytics disabled (426) - SDK upgrade required. Backoff: {backoffSeconds}s");
                        break;
                        
                    case 429: // RATE_LIMITED
                        var retryAfter = ParseBackoffFromResponse(responseText) ?? 3600; // Default 1h
                        SetBackoff(retryAfter, "Rate limited");
                        BoostOpsLogger.LogWarning("Analytics", $"⏳ Analytics rate limited (429). Backoff: {retryAfter}s");
                        break;
                        
                    case 200:
                    case 201:
                    case 202:
                        // Success - clear any previous backoff
                        ClearBackoff();
                        break;
                        
                    default:
                        // Handle 5xx errors with exponential backoff (but don't persist)
                        if (responseCode >= 500 && responseCode < 600)
                        {
                            var backoff = Math.Min(3600, 60 * (int)Math.Pow(2, 1)); // Start with 2 min, max 1h
                            SetBackoff(backoff, $"Server error ({responseCode})", persist: false);
                            BoostOpsLogger.LogWarning("Analytics", $"⚠️ Server error ({responseCode}). Temporary backoff: {backoff}s");
                        }
                        break;
                }
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Error handling server response: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Parse backoff duration from server response (Retry-After header or JSON)
        /// </summary>
        private int? ParseBackoffFromResponse(string responseText)
        {
            try
            {
                if (string.IsNullOrEmpty(responseText))
                    return null;
                    
                // Try parsing JSON response for custom backoff
                if (responseText.Contains("backoff_seconds"))
                {
                    var start = responseText.IndexOf("\"backoff_seconds\":");
                    if (start >= 0)
                    {
                        start = responseText.IndexOf(":", start) + 1;
                        var end = responseText.IndexOfAny(new char[] { ',', '}' }, start);
                        if (end > start)
                        {
                            var valueStr = responseText.Substring(start, end - start).Trim().Trim('"');
                            if (int.TryParse(valueStr, out int backoff))
                                return backoff;
                        }
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Set backoff state with optional persistence
        /// </summary>
        private void SetBackoff(int seconds, string reason, bool persist = true)
        {
            _backoffUntil = System.DateTime.UtcNow.AddSeconds(seconds);
            _disableReason = reason;
            
            if (persist)
                PersistAnalyticsState();
        }
        
        /// <summary>
        /// Clear backoff state
        /// </summary>
        private void ClearBackoff()
        {
            _isAnalyticsDisabled = false;
            _backoffUntil = System.DateTime.MinValue;
            _disableReason = null;
            PersistAnalyticsState();
        }
        
        /// <summary>
        /// Persist analytics disable state across app sessions
        /// </summary>
        private void PersistAnalyticsState()
        {
            try
            {
                PlayerPrefs.SetInt(BoostOpsPlayerPrefsKeys.ANALYTICS_DISABLED, _isAnalyticsDisabled ? 1 : 0);
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.ANALYTICS_BACKOFF_UNTIL, _backoffUntil.ToBinary().ToString());
                PlayerPrefs.SetString(BoostOpsPlayerPrefsKeys.ANALYTICS_DISABLE_REASON, _disableReason ?? "");
                PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to persist analytics state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load persisted analytics state on initialization
        /// </summary>
        private void LoadPersistedAnalyticsState()
        {
            try
            {
                _isAnalyticsDisabled = PlayerPrefs.GetInt(BoostOpsPlayerPrefsKeys.ANALYTICS_DISABLED, 0) == 1;
                _disableReason = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.ANALYTICS_DISABLE_REASON, "");
                
                var backoffBinary = PlayerPrefs.GetString(BoostOpsPlayerPrefsKeys.ANALYTICS_BACKOFF_UNTIL, "");
                if (!string.IsNullOrEmpty(backoffBinary) && long.TryParse(backoffBinary, out long binary))
                {
                    _backoffUntil = System.DateTime.FromBinary(binary);
                }
                
                if (IsAnalyticsDisabled)
                {
                    BoostOpsLogger.LogDebug("Analytics", $"Analytics disabled from previous session: {_disableReason}");
                }
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to load persisted analytics state: {ex.Message}");
                // Reset to safe defaults on error
                _isAnalyticsDisabled = false;
                _backoffUntil = System.DateTime.MinValue;
                _disableReason = null;
            }
        }
        
        #endregion
        
        #region Offline Queue Management
        
        /// <summary>
        /// Save failed events to offline queue for later retry
        /// </summary>
        private void SaveToOfflineQueue(List<AnalyticsEventData> events)
        {
            try
            {
                foreach (var evt in events)
                {
                    if (_offlineQueue.Count >= OFFLINE_QUEUE_MAX_SIZE)
                    {
                        // Remove oldest event to make room
                        _offlineQueue.Dequeue();
                    }
                    
                    _offlineQueue.Enqueue(evt);
                }
                
                // Persist to PlayerPrefs (serialize to JSON)
                PersistOfflineQueue();
                
                BoostOpsLogger.LogInfo("Analytics", $"💾 Saved {events.Count} events to offline queue (total: {_offlineQueue.Count})");
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to save to offline queue: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load offline queue from PlayerPrefs on initialization
        /// </summary>
        private void LoadOfflineQueue()
        {
            try
            {
                var offlineJson = PlayerPrefs.GetString("BoostOps_OfflineQueue", "");
                if (string.IsNullOrEmpty(offlineJson))
                    return;
                
                // Deserialize offline events (simple JSON array of event_type + nonce for tracking)
                var offlineData = JsonUtility.FromJson<OfflineQueueData>(offlineJson);
                if (offlineData?.events != null && offlineData.events.Length > 0)
                {
                    BoostOpsLogger.LogInfo("Analytics", $"📥 Loaded {offlineData.events.Length} events from offline queue");
                    
                    // Note: We only persist event metadata (type, nonce) not full payloads
                    // Full events would be too large for PlayerPrefs
                    // These are just markers to show that events were lost
                }
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to load offline queue: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Persist offline queue to PlayerPrefs
        /// Note: Only stores event metadata (type + nonce) to avoid PlayerPrefs size limits
        /// </summary>
        private void PersistOfflineQueue()
        {
            try
            {
                // Create lightweight representation (event_type + event_id)
                // Note: nonce is NOT persisted - it's regenerated fresh per send attempt
                var offlineMetadata = _offlineQueue.Select(e => new OfflineEventMetadata 
                { 
                    event_type = e.event_type,
                    event_id = e.event_id,
                    timestamp_ms = e.timestamp_ms
                }).Take(OFFLINE_QUEUE_MAX_SIZE).ToArray();
                
                var offlineData = new OfflineQueueData { events = offlineMetadata };
                var json = JsonUtility.ToJson(offlineData);
                
                PlayerPrefs.SetString("BoostOps_OfflineQueue", json);
                PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"Failed to persist offline queue: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Clear offline queue (called after successful send)
        /// </summary>
        private void ClearOfflineQueue()
        {
            _offlineQueue.Clear();
            PlayerPrefs.DeleteKey("BoostOps_OfflineQueue");
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Try to send any queued offline events when coming back online
        /// </summary>
        public void ProcessOfflineQueue()
        {
            if (_offlineQueue.Count == 0)
                return;
            
            BoostOpsLogger.LogInfo("Analytics", $"🔄 Processing {_offlineQueue.Count} offline events");
            
            // Move offline events to main queue for retry
            while (_offlineQueue.Count > 0)
            {
                var evt = _offlineQueue.Dequeue();
                _eventQueue.Enqueue(evt);
            }
            
            // Clear persisted offline queue
            ClearOfflineQueue();
            
            // Trigger immediate processing
            FlushQueue();
        }
        
        #endregion
        
        #region App Open Deduplication
        
        /// <summary>
        /// Check if an app_open event was recently sent (within dedup window)
        /// Used to prevent duplicate events from deep links + lifecycle handlers
        /// </summary>
        public static bool WasAppOpenRecentlySent()
        {
            return (Time.realtimeSinceStartup - _lastAppOpenTime) < APP_OPEN_DEDUP_WINDOW;
        }
        
        /// <summary>
        /// Record that an app_open event was just sent
        /// </summary>
        public static void RecordAppOpenSent()
        {
            _lastAppOpenTime = Time.realtimeSinceStartup;
        }
        
        #endregion
        
        #region Application Lifecycle
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // App is being paused/backgrounded - flush queue and record timestamp
                FlushQueue();
                BoostOpsEventBuilder.RecordBackgroundTimestamp();
                
                // Note: No need to clean up impressions - they're stored on display objects
            }
            else
            {
                // App is resuming from background - check if session timeout exceeded
                if (BoostOpsEventBuilder.ShouldStartNewSession())
                {
                    BoostOpsEventBuilder.RegenerateSessionId();
                    Debug.Log("[BoostOps Analytics] App resumed after timeout - regenerated session ID");
                    
                    // Check if an app_open was recently sent (e.g., by deep link handler)
                    // Deep links ALWAYS fire app_open, so we defer to them if present
                    if (WasAppOpenRecentlySent())
                    {
                        // Debug.Log("[BoostOps Analytics] ⏭️ Skipping app_open - one was just sent (likely from deep link)");
                    }
                    else
                    {
                        // Fire app_open event for new session (industry standard)
                        // This follows the same logic as session ID regeneration
                        BoostOpsAnalyticsContract.TrackAppOpen(
                            launchType: "warm",
                            deeplinkUrl: null,
                            isFirstSession: false
                        );
                        RecordAppOpenSent();
                        // Debug.Log("[BoostOps Analytics] 🚀 Fired app_open event for new warm start session");
                    }
                }
                else
                {
                    // Debug.Log("[BoostOps Analytics] App resumed within session timeout - continuing same session (no app_open)");
                }
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                // App is losing focus - flush queue and record timestamp
                FlushQueue();
                BoostOpsEventBuilder.RecordBackgroundTimestamp();
                
                // Note: No need to clean up impressions - they're stored on display objects
            }
            else
            {
                // App is gaining focus - check if session timeout exceeded
                if (BoostOpsEventBuilder.ShouldStartNewSession())
                {
                    BoostOpsEventBuilder.RegenerateSessionId();
                    Debug.Log("[BoostOps Analytics] App gained focus after timeout - regenerated session ID");
                    
                    // Check if an app_open was recently sent (e.g., by deep link handler)
                    // Deep links ALWAYS fire app_open, so we defer to them if present
                    if (WasAppOpenRecentlySent())
                    {
                        // Debug.Log("[BoostOps Analytics] ⏭️ Skipping app_open - one was just sent (likely from deep link)");
                    }
                    else
                    {
                        // Fire app_open event for new session (industry standard)
                        // This follows the same logic as session ID regeneration
                        BoostOpsAnalyticsContract.TrackAppOpen(
                            launchType: "warm",
                            deeplinkUrl: null,
                            isFirstSession: false
                        );
                        RecordAppOpenSent();
                        // Debug.Log("[BoostOps Analytics] 🚀 Fired app_open event for new warm start session");
                    }
                }
                else
                {
                    // Debug.Log("[BoostOps Analytics] App gained focus within session timeout - continuing same session (no app_open)");
                }
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                FlushQueue();
                _instance = null;
            }
        }
        
        /// <summary>
        /// Ensures coroutine runner is available for non-blocking requests
        /// </summary>
        private void StartCoroutineRunner()
        {
            if (_coroutineRunner == null)
            {
                var go = new UnityEngine.GameObject("BoostOpsAnalyticsCoroutineRunner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _coroutineRunner = go.AddComponent<CoroutineRunner>();
            }
        }
    
    /// <summary>
        /// Safely invoke callbacks with exception protection to prevent UI crashes
    /// </summary>
        private void SafeInvokeCallback(System.Action callback)
        {
            try
            {
                callback?.Invoke();
            }
            catch (Exception ex)
            {
                BoostOpsLogger.LogError("Analytics", $"🚨 Analytics callback crashed - {ex.Message}. UI protected from crash.");
            }
        }
        
        #endregion
    }
    
    #region Data Classes
    
    // Note: AnalyticsEventData and supporting classes are now defined in BoostOpsAnalyticsEventSchema.cs
    
    /// <summary>
    /// Single event response from analytics API (simplified - no legacy fields)
    /// </summary>
    [Serializable]
    public class AnalyticsEventResponse
    {
        public string status;        // "success" or "error"
        public string message;       // Optional message from server  
        public int event_id;         // Event ID assigned by server
        public string install_token; // Kept for backward compatibility in client code (always null from server)
    }
    
    /// <summary>
    /// Batch request structure
    /// </summary>
    [Serializable]
    public class AnalyticsBatchRequest
    {
        public List<AnalyticsEventData> events;
    }
    
    /// <summary>
    /// Privacy and GDPR information returned by server
    /// </summary>
    [Serializable]
    public class PrivacyInfo
    {
        // Region detection (from IP geolocation)
        public string country;              // ISO 3166-1 alpha-2 country code (e.g., "US", "DE", "GB")
        public string region;               // Geographic region: "eu", "us", "asia", "other"
        
        // Privacy framework detection
        public bool gdpr_applies;           // Is user in GDPR region? (EU/UK/EEA)
        public bool ccpa_applies;           // Is user in CCPA region? (California)
        public bool lgpd_applies;           // Is user in LGPD region? (Brazil)
        public bool pipeda_applies;         // Is user in PIPEDA region? (Canada)
        
        // Consent requirements
        public bool consent_required;       // Should app show consent banner?
        public bool consent_valid;          // Is SDK's consent data valid? (optional)
        public string[] consent_issues;     // Issues if consent invalid (optional)
        
        // Data processing
        public string data_residency;       // Where data is stored (e.g., "eu-central-1", "us-east-1")
        public bool data_localization_enabled; // Is data kept in-region?
        
        // Attribution limitations (optional)
        public bool attribution_limited;    // Is attribution limited by privacy settings?
        public string attribution_reason;   // Reason if limited: "att_denied", "tracking_limited", "no_idfa"
    }
    
    /// <summary>
    /// Batch response from analytics API (new format - no legacy fields)
    /// </summary>
    [Serializable]
    public class AnalyticsBatchResponse
    {
        public string status;              // "success" or "error"
        public string message;             // Optional message from server
        public int accepted;               // Number of events accepted by server
        public int rejected;               // Number of events rejected by server
        public int[] accepted_schema_major; // Schema major versions accepted by server (e.g., [1, 2])
        public bool enforce_schema_validation; // Whether to strictly block unsupported schemas (false = warn only)
        public string min_sdk_version;     // Minimum SDK version required by server
        public string ingest_mode;         // Ingest mode: "FULL", "SAMPLING", etc.
        
        // ✅ SAFE: Kill switch is OPT-IN (must explicitly set disabled: true)
        // Missing field = NOT disabled = analytics enabled (fail-open) ✅
        // disabled: false = NOT disabled = analytics enabled ✅
        // disabled: true = disabled = analytics DISABLED (kill switch) ❌
        public bool disabled = false;      // Kill switch (default: false = not disabled)
        
        public string endpoint;            // Server can override analytics endpoint
        
        // ✅ NEW: Privacy & GDPR info (Phase 1)
        // Missing field = null = client-side detection fallback (fail-open) ✅
        public PrivacyInfo privacy;        // Privacy and GDPR information from server
    }
    
    /// <summary>
    /// Offline queue metadata for persistence (lightweight storage)
    /// </summary>
    [Serializable]
    public class OfflineEventMetadata
    {
        public string event_type;
        public string event_id;     // Database UNIQUE INDEX (never changes)
        // Note: nonce is NOT persisted (regenerated fresh per send attempt)
        public long timestamp_ms;
    }
    
    /// <summary>
    /// Offline queue data container for serialization
    /// </summary>
    [Serializable]
    public class OfflineQueueData
    {
        public OfflineEventMetadata[] events;
    }
    
    #endregion
    
    /// <summary>
    /// MonoBehaviour for running analytics coroutines to prevent UI blocking
    /// </summary>
    public class CoroutineRunner : UnityEngine.MonoBehaviour
    {
        // This empty MonoBehaviour just provides coroutine functionality
        // All analytics network requests run through this to prevent UI freezing
    }
    
    // Legacy SerializableDictionary class removed - new schema uses structured JSONB data
}