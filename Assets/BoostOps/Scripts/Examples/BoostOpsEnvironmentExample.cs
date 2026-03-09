using UnityEngine;
using BoostOps;

/// <summary>
/// Example demonstrating environment detection with BoostOps SDK
/// Shows how to detect TestFlight, production, and development environments
/// </summary>
public class BoostOpsEnvironmentExample : MonoBehaviour
{
    void Start()
    {
        DemoEnvironmentDetection();
        DemoConditionalFeatures();
        DemoAnalyticsSegmentation();
    }
    
    /// <summary>
    /// Example: Basic environment detection
    /// </summary>
    void DemoEnvironmentDetection()
    {
        Debug.Log("=== Environment Detection Demo ===");
        
        // Get current environment
        string environment = BoostOpsEnvironment.GetEnvironment();
        Debug.Log($"Current Environment: {environment}");
        // iOS: "production", "testflight", "development", "simulator", "adhoc"
        // Android: "google_play"
        // Editor: "editor"
        
        // Check specific environments
        bool isTestFlight = BoostOpsEnvironment.IsTestFlight();
        Debug.Log($"Is TestFlight: {isTestFlight}");
        
        bool isProduction = BoostOpsEnvironment.IsProduction();
        Debug.Log($"Is Production: {isProduction}");
        
        bool isDevelopment = BoostOpsEnvironment.IsDevelopment();
        Debug.Log($"Is Development: {isDevelopment}");
        
        bool isRealDevice = BoostOpsEnvironment.IsRealDevice();
        Debug.Log($"Is Real Device: {isRealDevice}");
        
        // Get full report
        BoostOpsEnvironment.LogEnvironmentInfo();
    }
    
    /// <summary>
    /// Example: Enable/disable features based on environment
    /// </summary>
    void DemoConditionalFeatures()
    {
        Debug.Log("=== Conditional Features Demo ===");
        
        // Enable debug features only in development
        if (BoostOpsEnvironment.IsDevelopment())
        {
            Debug.Log("✅ Enabling debug menu");
            Debug.Log("✅ Enabling cheat codes");
            Debug.Log("✅ Enabling verbose logging");
        }
        
        // Show TestFlight feedback prompt
        if (BoostOpsEnvironment.IsTestFlight())
        {
            Debug.Log("✅ Showing TestFlight feedback prompt");
            Debug.Log("✅ Enabling crash reporting (staging endpoint)");
        }
        
        // Production-only features
        if (BoostOpsEnvironment.IsProduction())
        {
            Debug.Log("✅ Enabling in-app purchase validation");
            Debug.Log("✅ Enabling production analytics");
            Debug.Log("✅ Disabling debug shortcuts");
        }
        
        // Simulator-specific handling
        if (!BoostOpsEnvironment.IsRealDevice())
        {
            Debug.Log("⚠️ Running on simulator - skipping device-specific features");
        }
    }
    
    /// <summary>
    /// Example: Segment analytics data by environment
    /// </summary>
    void DemoAnalyticsSegmentation()
    {
        Debug.Log("=== Analytics Segmentation Demo ===");
        
        // Track events with environment context
        string environment = BoostOpsEnvironment.GetEnvironment();
        
        // Option 1: Filter out test data completely
        if (BoostOpsEnvironment.IsProduction())
        {
            // Only track production events
            BoostOpsAnalyticsContract.TrackAppOpen();
            Debug.Log("✅ Tracked production app_open event");
        }
        else
        {
            Debug.Log("⚠️ Skipped analytics (non-production environment)");
        }
        
        // Option 2: Track all events but tag with environment
        // This allows server-side filtering and separate test/prod dashboards
        Debug.Log($"📊 Event tagged with environment: {environment}");
        Debug.Log($"📊 Event tagged with is_testflight: {BoostOpsEnvironment.IsTestFlight()}");
        
        // Option 3: Use different analytics endpoints per environment
        if (BoostOpsEnvironment.IsTestFlight())
        {
            Debug.Log("📊 Sending to staging analytics endpoint");
        }
        else if (BoostOpsEnvironment.IsProduction())
        {
            Debug.Log("📊 Sending to production analytics endpoint");
        }
    }
    
    /// <summary>
    /// Example: Runtime environment switching (for testing)
    /// </summary>
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        
        GUILayout.Label("Environment Info:", GUI.skin.box);
        GUILayout.Label($"Environment: {BoostOpsEnvironment.GetEnvironment()}");
        GUILayout.Label($"TestFlight: {BoostOpsEnvironment.IsTestFlight()}");
        GUILayout.Label($"Production: {BoostOpsEnvironment.IsProduction()}");
        GUILayout.Label($"Development: {BoostOpsEnvironment.IsDevelopment()}");
        
        if (GUILayout.Button("Log Full Report"))
        {
            BoostOpsEnvironment.LogEnvironmentInfo();
        }
        
        GUILayout.EndArea();
    }
}

