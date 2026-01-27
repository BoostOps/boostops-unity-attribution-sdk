package com.boostops.unity.referrer;

import android.content.Context;
import android.net.Uri;
import android.os.RemoteException;
import android.util.Log;

import com.android.installreferrer.api.InstallReferrerClient;
import com.android.installreferrer.api.InstallReferrerStateListener;
import com.android.installreferrer.api.ReferrerDetails;
import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

import java.util.HashMap;
import java.util.Map;

/**
 * Native Android install referrer tracking for BoostOps Unity SDK
 * Integrates with Google Play Install Referrer API for accurate attribution
 * Critical for comprehensive attribution tracking and campaign analytics
 */
public class BoostOpsInstallReferrerNative implements InstallReferrerStateListener {
    
    private static final String TAG = "BoostOpsReferrer";
    private static final String UNITY_GAME_OBJECT = "BoostOpsInstallReferrerNative";
    private static final String UNITY_CALLBACK_METHOD = "OnInstallReferrerReceivedCallback";
    
    // Singleton instance
    private static BoostOpsInstallReferrerNative instance;
    
    // Install Referrer API
    private InstallReferrerClient referrerClient;
    private boolean isConnected = false;
    private boolean hasProcessedReferrer = false;
    
    // Configuration
    private String apiKey;
    private boolean unityCallbackEnabled = false;
    
    /**
     * Get singleton instance
     */
    public static BoostOpsInstallReferrerNative getInstance() {
        if (instance == null) {
            synchronized (BoostOpsInstallReferrerNative.class) {
                if (instance == null) {
                    instance = new BoostOpsInstallReferrerNative();
                }
            }
        }
        return instance;
    }
    
    /**
     * Private constructor for singleton
     */
    private BoostOpsInstallReferrerNative() {
        // Private constructor
    }
    
    /**
     * Initialize install referrer tracking
     * Called from Unity
     */
    public void initialize(String apiKey) {
        Log.d(TAG, "Initializing BoostOps Install Referrer Tracking");
        
        // Critical validation
        if (apiKey == null || apiKey.trim().isEmpty()) {
            Log.e(TAG, "API key cannot be null or empty");
            return;
        }
        
        // Validate Unity context exists
        if (UnityPlayer.currentActivity == null) {
            Log.e(TAG, "Unity activity is null, cannot initialize install referrer");
            return;
        }
        
        this.apiKey = apiKey.trim();
        this.unityCallbackEnabled = true;
        
        try {
            // Check if already processed to avoid duplicate calls
            if (hasProcessedReferrer) {
                Log.d(TAG, "Install referrer already processed, skipping");
                return;
            }
            
            initializeInstallReferrerClient();
            
        } catch (Exception e) {
            Log.e(TAG, "Failed to initialize install referrer tracking", e);
        }
    }
    
    /**
     * Initialize the Install Referrer client
     */
    private void initializeInstallReferrerClient() {
        try {
            Context context = UnityPlayer.currentActivity;
            if (context == null) {
                Log.e(TAG, "Context became null during install referrer initialization");
                return;
            }
            
            // Build install referrer client
            referrerClient = InstallReferrerClient.newBuilder(context).build();
            
            // Start connection
            startConnection();
            
        } catch (Exception e) {
            Log.e(TAG, "Error initializing install referrer client", e);
        }
    }
    
    /**
     * Start connection to Install Referrer service
     */
    private void startConnection() {
        try {
            if (referrerClient == null) {
                Log.e(TAG, "Install referrer client is null");
                return;
            }
            
            Log.d(TAG, "Starting install referrer connection...");
            referrerClient.startConnection(this);
            
        } catch (Exception e) {
            Log.e(TAG, "Error starting install referrer connection", e);
        }
    }
    
    @Override
    public void onInstallReferrerSetupFinished(int responseCode) {
        try {
            switch (responseCode) {
                case InstallReferrerClient.InstallReferrerResponse.OK:
                    Log.d(TAG, "Install referrer connection successful");
                    isConnected = true;
                    queryInstallReferrer();
                    break;
                    
                case InstallReferrerClient.InstallReferrerResponse.FEATURE_NOT_SUPPORTED:
                    Log.w(TAG, "Install referrer API not supported on this device");
                    break;
                    
                case InstallReferrerClient.InstallReferrerResponse.SERVICE_UNAVAILABLE:
                    Log.w(TAG, "Install referrer service unavailable, will retry later");
                    scheduleRetry();
                    break;
                    
                default:
                    Log.w(TAG, "Install referrer setup failed with code: " + responseCode);
                    break;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error in install referrer setup finished", e);
        }
    }
    
    @Override
    public void onInstallReferrerServiceDisconnected() {
        try {
            Log.w(TAG, "Install referrer service disconnected");
            isConnected = false;
            
            // Schedule reconnection if we haven't processed the referrer yet
            if (!hasProcessedReferrer) {
                scheduleRetry();
            }
        } catch (Exception e) {
            Log.e(TAG, "Error handling install referrer disconnect", e);
        }
    }
    
    /**
     * Query install referrer details
     */
    private void queryInstallReferrer() {
        try {
            if (!isConnected || referrerClient == null) {
                Log.e(TAG, "Cannot query install referrer - not connected");
                return;
            }
            
            // Get referrer details
            ReferrerDetails referrerDetails = referrerClient.getInstallReferrer();
            
            if (referrerDetails != null) {
                processInstallReferrer(referrerDetails);
            } else {
                Log.w(TAG, "Install referrer details are null");
            }
            
        } catch (RemoteException e) {
            Log.e(TAG, "Remote exception getting install referrer", e);
        } catch (Exception e) {
            Log.e(TAG, "Error querying install referrer", e);
        } finally {
            cleanup();
        }
    }
    
    /**
     * Process the install referrer details
     */
    private void processInstallReferrer(ReferrerDetails referrerDetails) {
        try {
            String installReferrer = referrerDetails.getInstallReferrer();
            long referrerClickTimestamp = referrerDetails.getReferrerClickTimestampSeconds();
            long installBeginTimestamp = referrerDetails.getInstallBeginTimestampSeconds();
            
            // Note: getGooglePlayInstantParam() method was removed from newer versions
            // of the Install Referrer API. Setting to false as default.
            boolean instantExperienceLaunched = false;
            
            Log.d(TAG, "Install referrer received: " + installReferrer);
            
            // Parse referrer URL parameters
            Map<String, String> referrerParams = parseReferrerUrl(installReferrer);
            
            // Create attribution data
            JSONObject attributionData = createAttributionData(
                installReferrer, 
                referrerParams,
                referrerClickTimestamp,
                installBeginTimestamp,
                instantExperienceLaunched
            );
            
            // Send to Unity
            sendAttributionToUnity(attributionData);
            
            // Mark as processed
            hasProcessedReferrer = true;
            
            Log.d(TAG, "Install referrer processed successfully");
            
        } catch (Exception e) {
            Log.e(TAG, "Error processing install referrer", e);
        }
    }
    
    /**
     * Parse referrer URL parameters
     */
    private Map<String, String> parseReferrerUrl(String referrerUrl) {
        Map<String, String> params = new HashMap<>();
        
        try {
            if (referrerUrl == null || referrerUrl.trim().isEmpty()) {
                return params;
            }
            
            // Handle URL-encoded referrer
            String decodedReferrer = Uri.decode(referrerUrl);
            
            // Parse parameters
            String[] pairs = decodedReferrer.split("&");
            for (String pair : pairs) {
                String[] keyValue = pair.split("=", 2);
                if (keyValue.length == 2) {
                    String key = Uri.decode(keyValue[0]);
                    String value = Uri.decode(keyValue[1]);
                    params.put(key, value);
                }
            }
            
        } catch (Exception e) {
            Log.e(TAG, "Error parsing referrer URL", e);
        }
        
        return params;
    }
    
    /**
     * Create attribution data JSON
     */
    private JSONObject createAttributionData(String rawReferrer, Map<String, String> params,
                                           long clickTimestamp, long installTimestamp, 
                                           boolean instantExperience) throws JSONException {
        JSONObject data = new JSONObject();
        
        // Basic referrer info
        data.put("raw_referrer", rawReferrer);
        data.put("click_timestamp", clickTimestamp);
        data.put("install_timestamp", installTimestamp);
        data.put("instant_experience", instantExperience);
        
        // Attribution parameters
        data.put("utm_source", params.get("utm_source"));
        data.put("utm_medium", params.get("utm_medium"));
        data.put("utm_campaign", params.get("utm_campaign"));
        data.put("utm_term", params.get("utm_term"));
        data.put("utm_content", params.get("utm_content"));
        
        // BoostOps specific parameters
        data.put("campaign_id", params.get("campaign_id"));
        data.put("source_app_id", params.get("source_app_id"));
        data.put("boost_referrer", params.get("boost_referrer"));
        
        // CRITICAL: Extract and save click_id for attribution
        String clickId = params.get("click_id");
        data.put("click_id", clickId);
        
        // Save click_id to SharedPreferences so IdentifierPlugin can access it
        if (clickId != null && !clickId.isEmpty()) {
            try {
                Context context = UnityPlayer.currentActivity.getApplicationContext();
                android.content.SharedPreferences prefs = context.getSharedPreferences(
                    "boostops_attribution", 
                    Context.MODE_PRIVATE
                );
                prefs.edit().putString("install_referrer_click_id", clickId).apply();
                Log.d(TAG, "✅ Saved click_id to SharedPreferences: " + clickId);
            } catch (Exception e) {
                Log.e(TAG, "Failed to save click_id to SharedPreferences: " + e.getMessage());
            }
        } else {
            Log.d(TAG, "No click_id found in install referrer (organic install)");
        }
        
        // Also extract and save schema v3 cross-promo parameters
        String sourceStoreId = params.get("source_store_id");
        String sourceProjectId = params.get("source_project_id");
        String targetStoreId = params.get("target_store_id");
        String targetProjectId = params.get("target_project_id");
        
        data.put("source_store_id", sourceStoreId);
        data.put("source_project_id", sourceProjectId);
        data.put("target_store_id", targetStoreId);
        data.put("target_project_id", targetProjectId);
        
        // Metadata
        data.put("attribution_source", "install_referrer_api");
        data.put("sdk_version", "1.0.0");
        data.put("timestamp", System.currentTimeMillis());
        
        return data;
    }
    
    /**
     * Send attribution data to Unity
     */
    private void sendAttributionToUnity(JSONObject attributionData) {
        try {
            if (!unityCallbackEnabled) {
                Log.w(TAG, "Unity callback not enabled, attribution data will be lost");
                return;
            }
            
            String jsonString = attributionData.toString();
            
            // Send to Unity on main thread
            UnityPlayer.currentActivity.runOnUiThread(() -> {
                try {
                    UnityPlayer.UnitySendMessage(UNITY_GAME_OBJECT, UNITY_CALLBACK_METHOD, jsonString);
                } catch (Exception e) {
                    Log.e(TAG, "Error sending attribution to Unity", e);
                }
            });
            
        } catch (Exception e) {
            Log.e(TAG, "Error preparing attribution for Unity", e);
        }
    }
    
    /**
     * Schedule retry connection
     */
    private void scheduleRetry() {
        // Simple retry after 5 seconds
        new android.os.Handler().postDelayed(() -> {
            if (!hasProcessedReferrer && !isConnected) {
                Log.d(TAG, "Retrying install referrer connection");
                startConnection();
            }
        }, 5000);
    }
    
    /**
     * Cleanup resources
     */
    private void cleanup() {
        try {
            if (referrerClient != null && isConnected) {
                referrerClient.endConnection();
                isConnected = false;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error during cleanup", e);
        }
    }
    
    /**
     * Check if install referrer has been processed
     */
    public boolean hasProcessedInstallReferrer() {
        return hasProcessedReferrer;
    }
    
    /**
     * Manually trigger install referrer query (for testing)
     */
    public void queryInstallReferrerManually() {
        if (!hasProcessedReferrer) {
            initializeInstallReferrerClient();
        } else {
            Log.d(TAG, "Install referrer already processed");
        }
    }
} 