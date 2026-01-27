package com.boostops.unity;

import android.content.Context;
import android.util.Log;
import com.google.android.gms.ads.identifier.AdvertisingIdClient;
import com.google.android.gms.appset.AppSet;
import com.google.android.gms.appset.AppSetIdClient;
import com.google.android.gms.appset.AppSetIdInfo;
import com.google.android.gms.tasks.Task;
import com.unity3d.player.UnityPlayer;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Android Identifier Plugin for BoostOps Unity SDK
 * Provides access to Android App Set ID, GAID, and Install Referrer Click ID
 * 
 * Critical for attribution tracking and privacy-compliant user identification
 */
public class IdentifierPlugin {
    
    private static final String TAG = "BoostOps-Identifiers";
    private static final int TIMEOUT_SECONDS = 5;
    
    /**
     * Get Android App Set ID
     * Developer-scoped identifier that persists across app installs from the same developer
     * Used for cross-app attribution within the same developer portfolio
     * 
     * @return App Set ID or null if unavailable
     */
    public static String getAppSetId() {
        Log.d(TAG, "Getting Android App Set ID...");
        
        try {
            Context context = getUnityContext();
            if (context == null) {
                Log.e(TAG, "Unity context is null, cannot get App Set ID");
                return null;
            }
            
            // App Set ID API uses Tasks, need to block until result is ready
            final AtomicReference<String> appSetIdResult = new AtomicReference<>(null);
            final CountDownLatch latch = new CountDownLatch(1);
            
            AppSetIdClient client = AppSet.getClient(context);
            Task<AppSetIdInfo> task = client.getAppSetIdInfo();
            
            task.addOnSuccessListener(appSetIdInfo -> {
                try {
                    if (appSetIdInfo != null) {
                        String appSetId = appSetIdInfo.getId();
                        int scope = appSetIdInfo.getScope();
                        
                        Log.d(TAG, "✅ App Set ID retrieved successfully");
                        Log.d(TAG, "App Set ID Scope: " + 
                            (scope == AppSetIdInfo.SCOPE_APP ? "APP" : "DEVELOPER"));
                        
                        appSetIdResult.set(appSetId);
                    } else {
                        Log.w(TAG, "App Set ID info is null");
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error processing App Set ID result: " + e.getMessage());
                } finally {
                    latch.countDown();
                }
            });
            
            task.addOnFailureListener(exception -> {
                Log.e(TAG, "Failed to get App Set ID: " + exception.getMessage());
                latch.countDown();
            });
            
            // Wait for result with timeout
            boolean completed = latch.await(TIMEOUT_SECONDS, TimeUnit.SECONDS);
            if (!completed) {
                Log.w(TAG, "App Set ID request timed out after " + TIMEOUT_SECONDS + " seconds");
                return null;
            }
            
            return appSetIdResult.get();
            
        } catch (InterruptedException e) {
            Log.e(TAG, "App Set ID request interrupted: " + e.getMessage());
            Thread.currentThread().interrupt();
            return null;
        } catch (Exception e) {
            Log.e(TAG, "Unexpected error getting App Set ID: " + e.getMessage());
            return null;
        }
    }
    
    /**
     * Get Google Advertising ID (GAID)
     * User-resettable advertising identifier
     * Returns null if user has opted out of personalized ads or limit ad tracking is enabled
     * 
     * Note: Google is planning to deprecate GAID in the future
     * 
     * @return GAID or null if unavailable/opted out
     */
    public static String getGoogleAdvertisingId() {
        Log.d(TAG, "Getting Google Advertising ID (GAID)...");
        
        try {
            Context context = getUnityContext();
            if (context == null) {
                Log.e(TAG, "Unity context is null, cannot get GAID");
                return null;
            }
            
            // AdvertisingIdClient.getAdvertisingIdInfo() must be called on a background thread
            // We'll run it synchronously with a timeout since Unity will call this from a worker thread
            final AtomicReference<String> gaidResult = new AtomicReference<>(null);
            final AtomicReference<Exception> exceptionRef = new AtomicReference<>(null);
            final CountDownLatch latch = new CountDownLatch(1);
            
            // Run on background thread
            new Thread(() -> {
                try {
                    AdvertisingIdClient.Info adInfo = AdvertisingIdClient.getAdvertisingIdInfo(context);
                    
                    if (adInfo != null) {
                        boolean isLimitAdTrackingEnabled = adInfo.isLimitAdTrackingEnabled();
                        
                        if (isLimitAdTrackingEnabled) {
                            Log.d(TAG, "⚠️ User has enabled Limit Ad Tracking - GAID not available");
                            gaidResult.set(null);
                        } else {
                            String gaid = adInfo.getId();
                            
                            // Check for zero/invalid GAID
                            if (gaid != null && !gaid.equals("00000000-0000-0000-0000-000000000000")) {
                                Log.d(TAG, "✅ GAID retrieved successfully");
                                gaidResult.set(gaid);
                            } else {
                                Log.w(TAG, "GAID is zero or invalid");
                                gaidResult.set(null);
                            }
                        }
                    } else {
                        Log.w(TAG, "AdvertisingIdClient returned null info");
                    }
                } catch (Exception e) {
                    Log.e(TAG, "Error getting GAID: " + e.getMessage());
                    exceptionRef.set(e);
                } finally {
                    latch.countDown();
                }
            }).start();
            
            // Wait for result with timeout
            boolean completed = latch.await(TIMEOUT_SECONDS, TimeUnit.SECONDS);
            if (!completed) {
                Log.w(TAG, "GAID request timed out after " + TIMEOUT_SECONDS + " seconds");
                return null;
            }
            
            // Check if an exception occurred
            if (exceptionRef.get() != null) {
                throw exceptionRef.get();
            }
            
            return gaidResult.get();
            
        } catch (InterruptedException e) {
            Log.e(TAG, "GAID request interrupted: " + e.getMessage());
            Thread.currentThread().interrupt();
            return null;
        } catch (Exception e) {
            Log.e(TAG, "Unexpected error getting GAID: " + e.getMessage());
            return null;
        }
    }
    
    /**
     * Get Install Referrer Click ID
     * This is extracted from the Google Play Install Referrer
     * 
     * Note: This method relies on the Install Referrer having been processed
     * by BoostOpsInstallReferrerNative first
     * 
     * @return Install Referrer Click ID or null if unavailable
     */
    public static String getInstallReferrerClickId() {
        Log.d(TAG, "Getting Install Referrer Click ID...");
        
        try {
            Context context = getUnityContext();
            if (context == null) {
                Log.e(TAG, "Unity context is null, cannot get Install Referrer Click ID");
                return null;
            }
            
            // The install referrer data should have been processed and stored
            // by BoostOpsInstallReferrerNative during app initialization
            // We'll retrieve it from SharedPreferences where it was cached
            
            android.content.SharedPreferences prefs = context.getSharedPreferences(
                "boostops_attribution", 
                Context.MODE_PRIVATE
            );
            
            String clickId = prefs.getString("install_referrer_click_id", null);
            
            if (clickId != null && !clickId.isEmpty()) {
                Log.d(TAG, "✅ Install Referrer Click ID retrieved from cache");
                return clickId;
            } else {
                Log.d(TAG, "Install Referrer Click ID not available (may be organic install)");
                return null;
            }
            
        } catch (Exception e) {
            Log.e(TAG, "Error getting Install Referrer Click ID: " + e.getMessage());
            return null;
        }
    }
    
    /**
     * Get device locale in industry-standard format (e.g., "en_US", "es_MX", "pt_BR")
     * Returns language_COUNTRY format following ISO 639-1 and ISO 3166-1 standards
     * 
     * @return Locale string in format "en_US" or null if unavailable
     */
    public static String getDeviceLocale() {
        Log.d(TAG, "Getting device locale...");
        
        try {
            Context context = getUnityContext();
            if (context == null) {
                Log.e(TAG, "Unity context is null, cannot get locale");
                return null;
            }
            
            // Get default locale from system
            java.util.Locale locale = java.util.Locale.getDefault();
            
            if (locale != null) {
                String language = locale.getLanguage();  // e.g., "en"
                String country = locale.getCountry();    // e.g., "US"
                
                String localeString;
                if (country != null && !country.isEmpty()) {
                    // Full locale: language_COUNTRY (e.g., "en_US", "es_MX")
                    localeString = language + "_" + country;
                } else {
                    // Language only (e.g., "en", "es")
                    localeString = language;
                }
                
                Log.d(TAG, "✅ Device locale: " + localeString);
                return localeString;
            } else {
                Log.w(TAG, "System locale is null");
                return null;
            }
            
        } catch (Exception e) {
            Log.e(TAG, "Error getting device locale: " + e.getMessage());
            return null;
        }
    }
    
    /**
     * Get Unity application context
     * 
     * @return Application context or null if Unity Player is not available
     */
    private static Context getUnityContext() {
        try {
            if (UnityPlayer.currentActivity != null) {
                return UnityPlayer.currentActivity.getApplicationContext();
            }
            
            Log.e(TAG, "UnityPlayer.currentActivity is null");
            return null;
            
        } catch (Exception e) {
            Log.e(TAG, "Error getting Unity context: " + e.getMessage());
            return null;
        }
    }
}


