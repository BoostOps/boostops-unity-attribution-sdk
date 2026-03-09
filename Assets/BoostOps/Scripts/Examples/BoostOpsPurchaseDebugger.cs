using UnityEngine;
using BoostOps;

/// <summary>
/// Debug tool to verify purchase tracking is working correctly
/// Add this to a GameObject in your scene to see purchase tracking status
/// </summary>
public class BoostOpsPurchaseDebugger : MonoBehaviour
{
    [Header("Auto Refresh")]
    [Tooltip("Update status display every frame")]
    public bool autoRefresh = true;
    
    [Header("Status")]
    [SerializeField] private bool sdkInitialized = false;
    [SerializeField] private bool revenueTrackerEnabled = false;
    [SerializeField] private int purchasesDetected = 0;
    [SerializeField] private string lastPurchaseInfo = "None";
    [SerializeField] private string platform = "";
    [SerializeField] private bool isTestFlight = false;
    [SerializeField] private bool isEditor = false;
    
    [Header("Callback Handler Status")]
    [SerializeField] private bool callbackHandlerExists = false;
    [SerializeField] private string callbackHandlerName = "";
    
    private void Start()
    {
        Debug.Log("=== BoostOps Purchase Debugger Started ===");
        
        // Subscribe to revenue events
        BoostOpsRevenueTracker.OnRevenueTracked += OnPurchaseTracked;
        BoostOpsRevenueTracker.OnRevenueTrackingError += OnPurchaseError;
        
        // Check platform
        #if UNITY_IOS
        platform = "iOS";
        #elif UNITY_ANDROID
        platform = "Android";
        #elif UNITY_EDITOR
        platform = "Editor";
        #else
        platform = "Other";
        #endif
        
        isEditor = Application.isEditor;
        
        #if UNITY_IOS && !UNITY_EDITOR
        isTestFlight = BoostOpsEnvironment.IsTestFlight();
        #endif
        
        CheckStatus();
        
        // Log initial status
        LogStatus();
    }
    
    private void Update()
    {
        if (autoRefresh)
        {
            CheckStatus();
        }
    }
    
    private void CheckStatus()
    {
        // Check SDK initialization
        sdkInitialized = BoostOpsSDK.IsInitialized;
        
        // Check revenue tracker status
        revenueTrackerEnabled = BoostOpsRevenueTracker.AutoRevenueTrackingEnabled;
        
        // Check for callback handler GameObject
        var callbackHandler = GameObject.Find("BoostOpsRevenueTrackerNative");
        callbackHandlerExists = callbackHandler != null;
        if (callbackHandler != null)
        {
            callbackHandlerName = callbackHandler.name;
        }
    }
    
    private void OnPurchaseTracked(BoostOps.AutoRevenueEvent evt)
    {
        purchasesDetected++;
        lastPurchaseInfo = $"{evt.ProductId} - ${evt.LocalizedPrice} {evt.IsoCurrencyCode}";
        
        Debug.Log($"✅ PURCHASE DETECTED #{purchasesDetected}: {lastPurchaseInfo}");
        Debug.Log($"   Transaction ID: {evt.TransactionId}");
        Debug.Log($"   Platform: {evt.Platform}");
        Debug.Log($"   Has Receipt: {evt.HasReceipt}");
        Debug.Log($"   USD Value: ${evt.USDValue}");
        
        LogStatus();
    }
    
    private void OnPurchaseError(string source, System.Exception error)
    {
        Debug.LogError($"❌ PURCHASE ERROR ({source}): {error.Message}");
        LogStatus();
    }
    
    private void LogStatus()
    {
        Debug.Log("=== BoostOps Purchase Tracking Status ===");
        Debug.Log($"Platform: {platform}");
        Debug.Log($"Is Editor: {isEditor}");
        Debug.Log($"Is TestFlight: {isTestFlight}");
        Debug.Log($"SDK Initialized: {sdkInitialized}");
        Debug.Log($"Revenue Tracker Enabled: {revenueTrackerEnabled}");
        Debug.Log($"Callback Handler Exists: {callbackHandlerExists} (Name: {callbackHandlerName})");
        Debug.Log($"Purchases Detected: {purchasesDetected}");
        Debug.Log($"Last Purchase: {lastPurchaseInfo}");
        Debug.Log("=========================================");
    }
    
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== BoostOps Purchase Debug ===", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
        GUILayout.Space(10);
        
        GUILayout.Label($"Platform: {platform}");
        GUILayout.Label($"Is Editor: {isEditor}");
        GUILayout.Label($"Is TestFlight: {isTestFlight}");
        GUILayout.Space(5);
        
        GUI.color = sdkInitialized ? Color.green : Color.red;
        GUILayout.Label($"SDK Initialized: {sdkInitialized}");
        
        GUI.color = revenueTrackerEnabled ? Color.green : Color.red;
        GUILayout.Label($"Revenue Tracker: {revenueTrackerEnabled}");
        
        GUI.color = callbackHandlerExists ? Color.green : Color.red;
        GUILayout.Label($"Callback Handler: {callbackHandlerExists}");
        
        GUI.color = Color.white;
        GUILayout.Space(5);
        
        GUILayout.Label($"Purchases: {purchasesDetected}");
        if (purchasesDetected > 0)
        {
            GUI.color = Color.green;
            GUILayout.Label($"Last: {lastPurchaseInfo}");
        }
        
        GUI.color = Color.white;
        GUILayout.Space(10);
        
        if (GUILayout.Button("Log Full Status"))
        {
            LogStatus();
        }
        
        if (GUILayout.Button("Test Manual Purchase"))
        {
            TestManualPurchase();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    
    private void TestManualPurchase()
    {
        Debug.Log("🧪 Testing manual purchase...");
        
        try
        {
            // Test manual purchase tracking
            BoostOpsRevenueTracker.TrackPurchase(
                transactionId: $"test_{System.Guid.NewGuid()}",
                productId: "test.product.debug",
                amount: 4.99m,
                currency: "USD",
                properties: new System.Collections.Generic.Dictionary<string, object>
                {
                    { "test_source", "debug_button" },
                    { "environment", platform }
                }
            );
            
            Debug.Log("✅ Manual purchase test triggered");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ Manual purchase test failed: {ex.Message}");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        BoostOpsRevenueTracker.OnRevenueTracked -= OnPurchaseTracked;
        BoostOpsRevenueTracker.OnRevenueTrackingError -= OnPurchaseError;
    }
}

