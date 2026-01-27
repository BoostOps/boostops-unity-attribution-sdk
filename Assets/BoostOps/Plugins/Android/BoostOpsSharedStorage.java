package com.boostops.unity;

import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.util.Log;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;

/**
 * BoostOps cross-app persistent storage for Android
 * Uses SharedPreferences with signature-based cross-app sharing
 * Similar patterns to Branch and AppsFlyer for maximum compatibility
 */
public class BoostOpsSharedStorage {
    
    private static final String TAG = "BoostOps-Storage";
    private static final String PREFS_NAME = "boostops_shared_data";
    private static final String KEY_BOOSTOPS_ID = "boostops_id";
    private static final String KEY_SIGNATURE_HASH = "signature_hash";
    
    private static Context applicationContext;
    private static String cachedSignatureHash;
    
    /**
     * Initialize the storage system with application context
     * Must be called before any other operations
     */
    public static void initialize(Context context) {
        applicationContext = context.getApplicationContext();
        Log.d(TAG, "BoostOps SharedStorage initialized");
    }
    
    /**
     * Get application signature hash for cross-app validation
     * Apps signed with the same certificate can share data
     */
    private static String getSignatureHash() {
        if (cachedSignatureHash != null) {
            return cachedSignatureHash;
        }
        
        try {
            PackageManager pm = applicationContext.getPackageManager();
            PackageInfo packageInfo = pm.getPackageInfo(
                applicationContext.getPackageName(), 
                PackageManager.GET_SIGNATURES
            );
            
            if (packageInfo.signatures != null && packageInfo.signatures.length > 0) {
                Signature signature = packageInfo.signatures[0];
                MessageDigest md = MessageDigest.getInstance("SHA-256");
                md.update(signature.toByteArray());
                
                byte[] digest = md.digest();
                StringBuilder hexString = new StringBuilder();
                for (byte b : digest) {
                    String hex = Integer.toHexString(0xff & b);
                    if (hex.length() == 1) {
                        hexString.append('0');
                    }
                    hexString.append(hex);
                }
                
                cachedSignatureHash = hexString.toString();
                Log.d(TAG, "Generated signature hash: " + cachedSignatureHash.substring(0, 8) + "...");
                return cachedSignatureHash;
            }
        } catch (PackageManager.NameNotFoundException | NoSuchAlgorithmException e) {
            Log.e(TAG, "Failed to generate signature hash: " + e.getMessage());
        }
        
        return null;
    }
    
    /**
     * Get SharedPreferences instance for BoostOps data
     * Uses signature-based naming for cross-app sharing
     */
    private static SharedPreferences getBoostOpsPreferences() {
        String signatureHash = getSignatureHash();
        String prefsName;
        
        if (signatureHash != null) {
            // Use signature hash for cross-app sharing (similar to Branch pattern)
            prefsName = "boostops_" + signatureHash.substring(0, 8);
            Log.d(TAG, "Using cross-app preferences: " + prefsName);
        } else {
            // Fallback to app-specific preferences
            prefsName = PREFS_NAME + "_" + applicationContext.getPackageName();
            Log.w(TAG, "Using app-specific preferences (no cross-app sharing): " + prefsName);
        }
        
        return applicationContext.getSharedPreferences(prefsName, Context.MODE_PRIVATE);
    }
    
    /**
     * Store BoostOps ID in shared storage
     * @param boostopsId The BoostOps ID to store
     * @return true if successful, false otherwise
     */
    public static boolean storeBoostOpsId(String boostopsId) {
        if (boostopsId == null || boostopsId.trim().isEmpty()) {
            Log.e(TAG, "Cannot store empty BoostOps ID");
            return false;
        }
        
        if (applicationContext == null) {
            Log.e(TAG, "Storage not initialized. Call initialize() first.");
            return false;
        }
        
        try {
            SharedPreferences prefs = getBoostOpsPreferences();
            SharedPreferences.Editor editor = prefs.edit();
            
            // Store the BoostOps ID
            editor.putString(KEY_BOOSTOPS_ID, boostopsId);
            
            // Store signature hash for validation
            String signatureHash = getSignatureHash();
            if (signatureHash != null) {
                editor.putString(KEY_SIGNATURE_HASH, signatureHash);
            }
            
            // Store timestamp for debugging
            editor.putLong("stored_timestamp", System.currentTimeMillis());
            
            boolean success = editor.commit();
            
            if (success) {
                Log.d(TAG, "✅ Successfully stored BoostOps ID in SharedPreferences");
            } else {
                Log.e(TAG, "❌ Failed to commit BoostOps ID to SharedPreferences");
            }
            
            return success;
        } catch (Exception e) {
            Log.e(TAG, "❌ Exception storing BoostOps ID: " + e.getMessage());
            return false;
        }
    }
    
    /**
     * Retrieve BoostOps ID from shared storage
     * @return BoostOps ID if found, null otherwise
     */
    public static String retrieveBoostOpsId() {
        if (applicationContext == null) {
            Log.e(TAG, "Storage not initialized. Call initialize() first.");
            return null;
        }
        
        try {
            SharedPreferences prefs = getBoostOpsPreferences();
            String storedId = prefs.getString(KEY_BOOSTOPS_ID, null);
            
            if (storedId != null && !storedId.trim().isEmpty()) {
                // Validate signature hash if available
                String storedSignatureHash = prefs.getString(KEY_SIGNATURE_HASH, null);
                String currentSignatureHash = getSignatureHash();
                
                if (storedSignatureHash != null && currentSignatureHash != null) {
                    if (!storedSignatureHash.equals(currentSignatureHash)) {
                        Log.w(TAG, "⚠️ Signature mismatch. Stored ID may be from different developer.");
                        // Could decide to return null here for security, but being permissive for now
                    }
                }
                
                Log.d(TAG, "✅ Successfully retrieved BoostOps ID from SharedPreferences");
                return storedId;
            } else {
                Log.d(TAG, "BoostOps ID not found in SharedPreferences (first launch)");
                return null;
            }
        } catch (Exception e) {
            Log.e(TAG, "❌ Exception retrieving BoostOps ID: " + e.getMessage());
            return null;
        }
    }
    
    /**
     * Delete BoostOps ID from shared storage
     * @return true if successful, false otherwise
     */
    public static boolean deleteBoostOpsId() {
        if (applicationContext == null) {
            Log.e(TAG, "Storage not initialized. Call initialize() first.");
            return false;
        }
        
        try {
            SharedPreferences prefs = getBoostOpsPreferences();
            SharedPreferences.Editor editor = prefs.edit();
            
            editor.remove(KEY_BOOSTOPS_ID);
            editor.remove(KEY_SIGNATURE_HASH);
            editor.remove("stored_timestamp");
            
            boolean success = editor.commit();
            
            if (success) {
                Log.d(TAG, "✅ Successfully deleted BoostOps ID from SharedPreferences");
            } else {
                Log.e(TAG, "❌ Failed to delete BoostOps ID from SharedPreferences");
            }
            
            return success;
        } catch (Exception e) {
            Log.e(TAG, "❌ Exception deleting BoostOps ID: " + e.getMessage());
            return false;
        }
    }
    
    /**
     * Check if BoostOps ID exists in shared storage
     * @return true if exists, false otherwise
     */
    public static boolean boostOpsIdExists() {
        if (applicationContext == null) {
            Log.e(TAG, "Storage not initialized. Call initialize() first.");
            return false;
        }
        
        try {
            SharedPreferences prefs = getBoostOpsPreferences();
            boolean exists = prefs.contains(KEY_BOOSTOPS_ID);
            
            Log.d(TAG, "BoostOps ID exists in SharedPreferences: " + exists);
            return exists;
        } catch (Exception e) {
            Log.e(TAG, "❌ Exception checking BoostOps ID existence: " + e.getMessage());
            return false;
        }
    }
    
    /**
     * Debug function: Log all stored BoostOps data
     */
    public static void debugStoredData() {
        if (applicationContext == null) {
            Log.e(TAG, "Storage not initialized. Call initialize() first.");
            return;
        }
        
        Log.d(TAG, "=== DEBUG: BoostOps Stored Data ===");
        
        try {
            SharedPreferences prefs = getBoostOpsPreferences();
            
            String storedId = prefs.getString(KEY_BOOSTOPS_ID, null);
            String storedSignatureHash = prefs.getString(KEY_SIGNATURE_HASH, null);
            long storedTimestamp = prefs.getLong("stored_timestamp", 0);
            
            Log.d(TAG, "BoostOps ID: " + (storedId != null ? storedId.substring(0, Math.min(20, storedId.length())) + "..." : "null"));
            Log.d(TAG, "Signature Hash: " + (storedSignatureHash != null ? storedSignatureHash.substring(0, 8) + "..." : "null"));
            Log.d(TAG, "Stored Timestamp: " + storedTimestamp);
            Log.d(TAG, "Current Signature: " + (getSignatureHash() != null ? getSignatureHash().substring(0, 8) + "..." : "null"));
            
        } catch (Exception e) {
            Log.e(TAG, "❌ Exception during debug: " + e.getMessage());
        }
        
        Log.d(TAG, "=== END DEBUG ===");
    }
}

/**
 * Unity plugin interface for BoostOps shared storage
 * Provides C-style functions that can be called from Unity
 */
class BoostOpsUnityPlugin {
    
    private static final String TAG = "BoostOps-Unity";
    
    /**
     * Initialize storage with Unity's current activity context
     */
    public static void initializeStorage() {
        try {
            // Get Unity's current activity
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            android.app.Activity currentActivity = (android.app.Activity) unityPlayerClass.getField("currentActivity").get(null);
            
            if (currentActivity != null) {
                BoostOpsSharedStorage.initialize(currentActivity.getApplicationContext());
                Log.d(TAG, "✅ Storage initialized with Unity activity context");
            } else {
                Log.e(TAG, "❌ Unity current activity is null");
            }
        } catch (Exception e) {
            Log.e(TAG, "❌ Failed to initialize storage: " + e.getMessage());
        }
    }
    
    /**
     * Store BoostOps ID (Unity callable)
     */
    public static boolean storeBoostOpsId(String boostopsId) {
        return BoostOpsSharedStorage.storeBoostOpsId(boostopsId);
    }
    
    /**
     * Retrieve BoostOps ID (Unity callable)
     */
    public static String retrieveBoostOpsId() {
        return BoostOpsSharedStorage.retrieveBoostOpsId();
    }
    
    /**
     * Delete BoostOps ID (Unity callable)
     */
    public static boolean deleteBoostOpsId() {
        return BoostOpsSharedStorage.deleteBoostOpsId();
    }
    
    /**
     * Check if BoostOps ID exists (Unity callable)
     */
    public static boolean boostOpsIdExists() {
        return BoostOpsSharedStorage.boostOpsIdExists();
    }
    
    /**
     * Debug stored data (Unity callable)
     */
    public static void debugStoredData() {
        BoostOpsSharedStorage.debugStoredData();
    }
}