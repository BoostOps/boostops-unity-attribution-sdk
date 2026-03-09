using UnityEngine;
using BoostOps;

/// <summary>
/// Example demonstrating debug build detection with BoostOps SDK
/// Shows how to detect debug builds on Android and iOS
/// </summary>
public class BoostOpsDebugDetectionExample : MonoBehaviour
{
    void Start()
    {
        DemoDebugDetection();
        DemoConditionalFeatures();
        DemoAnalyticsFiltering();
    }
    
    /// <summary>
    /// Example: Basic debug build detection
    /// </summary>
    void DemoDebugDetection()
    {
        Debug.Log("=== Debug Build Detection Demo ===");
        
        // Method 1: Check if this is a debug build (Android FLAG_DEBUGGABLE)
        bool isDebugBuild = BoostOpsEnvironment.IsDebugBuild();
        Debug.Log($"Is Debug Build: {isDebugBuild}");
        
        // Method 2: Check if development environment (includes debug, simulator, etc)
        bool isDevelopment = BoostOpsEnvironment.IsDevelopment();
        Debug.Log($"Is Development: {isDevelopment}");
        
        // Method 3: Check if production
        bool isProduction = BoostOpsEnvironment.IsProduction();
        Debug.Log($"Is Production: {isProduction}");
        
        // Android-specific: Check installer source
        #if UNITY_ANDROID
        string installer = BoostOpsEnvironment.GetInstallerSource();
        Debug.Log($"Installer Source: {installer}");
        
        // Debug builds are often sideloaded during development
        if (isDebugBuild && installer == "sideload") {
            Debug.Log("✅ Development build detected (debug + sideload)");
        }
        #endif
        
        // Get full environment info
        string environment = BoostOpsEnvironment.GetEnvironment();
        Debug.Log($"Environment: {environment}");
        // Android Debug: Returns "development"
        // Android Release from Play: Returns "google_play"
        // iOS Debug: Returns "development"
        // iOS TestFlight: Returns "testflight"
    }
    
    /// <summary>
    /// Example: Enable/disable features based on debug build
    /// </summary>
    void DemoConditionalFeatures()
    {
        Debug.Log("=== Conditional Features Demo ===");
        
        if (BoostOpsEnvironment.IsDebugBuild())
        {
            // Features only enabled in debug builds
            Debug.Log("✅ Enabling debug features:");
            Debug.Log("  - Debug menu");
            Debug.Log("  - Console logging");
            Debug.Log("  - Cheat codes");
            Debug.Log("  - Level skip");
            EnableDebugMenu();
            EnableVerboseLogging();
        }
        else if (BoostOpsEnvironment.IsProduction())
        {
            // Production-only features
            Debug.Log("✅ Enabling production features:");
            Debug.Log("  - Crash reporting");
            Debug.Log("  - Analytics");
            Debug.Log("  - Cloud saves");
            Debug.Log("  - In-app purchases");
            DisableDebugMenu();
        }
    }
    
    /// <summary>
    /// Example: Filter analytics to exclude debug builds
    /// </summary>
    void DemoAnalyticsFiltering()
    {
        Debug.Log("=== Analytics Filtering Demo ===");
        
        // Option 1: Don't track analytics in debug builds at all
        if (!BoostOpsEnvironment.IsDebugBuild())
        {
            BoostOpsAnalyticsContract.TrackAppOpen();
            Debug.Log("✅ Tracked app_open event (production only)");
        }
        else
        {
            Debug.Log("⚠️ Skipped analytics (debug build)");
        }
        
        // Option 2: Track everything, server filters by is_debug_build flag
        // All purchase events automatically include is_debug_build flag
        if (BoostOpsEnvironment.IsDebugBuild())
        {
            Debug.Log("📊 Purchase events tagged with is_debug_build: true");
            Debug.Log("📊 Server can filter these out from production metrics");
        }
        else
        {
            Debug.Log("📊 Purchase events tagged with is_debug_build: false");
        }
    }
    
    void EnableDebugMenu()
    {
        Debug.Log("[Debug Feature] Debug menu enabled");
        // Your debug menu code here
    }
    
    void EnableVerboseLogging()
    {
        Debug.Log("[Debug Feature] Verbose logging enabled");
        // Your logging code here
    }
    
    void DisableDebugMenu()
    {
        Debug.Log("[Production] Debug menu disabled");
    }
    
    /// <summary>
    /// Display debug status in UI
    /// </summary>
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        
        GUILayout.Label("=== Build Type Detection ===", GUI.skin.box);
        
        // Show debug build status
        bool isDebug = BoostOpsEnvironment.IsDebugBuild();
        var oldColor = GUI.backgroundColor;
        GUI.backgroundColor = isDebug ? Color.yellow : Color.green;
        
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label($"Debug Build: {isDebug}");
        if (isDebug)
        {
            GUILayout.Label("⚠️ DEV", GUI.skin.box);
        }
        else
        {
            GUILayout.Label("✅ RELEASE", GUI.skin.box);
        }
        GUILayout.EndHorizontal();
        
        GUI.backgroundColor = oldColor;
        
        // Show environment
        GUILayout.Label($"Environment: {BoostOpsEnvironment.GetEnvironment()}");
        GUILayout.Label($"Is Production: {BoostOpsEnvironment.IsProduction()}");
        GUILayout.Label($"Is Development: {BoostOpsEnvironment.IsDevelopment()}");
        
        #if UNITY_ANDROID
        GUILayout.Label($"Installer: {BoostOpsEnvironment.GetInstallerSource()}");
        GUILayout.Label($"Is Emulator: {BoostOpsEnvironment.IsEmulator()}");
        #endif
        
        #if UNITY_IOS
        GUILayout.Label($"Is TestFlight: {BoostOpsEnvironment.IsTestFlight()}");
        #endif
        
        if (GUILayout.Button("Log Full Environment Report"))
        {
            BoostOpsEnvironment.LogEnvironmentInfo();
        }
        
        GUILayout.EndArea();
    }
}

