package com.boostops.unity;

import android.os.SystemClock;
import android.util.Log;

/**
 * Native Android plugin for device information
 * Provides device uptime and boot timestamp
 */
public class BoostOpsDeviceInfo {
    private static final String TAG = "BoostOpsDeviceInfo";
    
    /**
     * Get device uptime in seconds (time since device last booted)
     * Uses SystemClock.elapsedRealtime() which returns time since boot
     * @return Device uptime in seconds, or -1 if failed
     */
    public static double getDeviceUptimeSeconds() {
        try {
            // SystemClock.elapsedRealtime() returns milliseconds since boot
            long uptimeMillis = SystemClock.elapsedRealtime();
            double uptimeSeconds = uptimeMillis / 1000.0;
            
            return uptimeSeconds;
        } catch (Exception e) {
            Log.e(TAG, "Failed to get device uptime", e);
            return -1.0;
        }
    }
    
    /**
     * Get device boot timestamp in Unix seconds (when device was last booted)
     * Calculated as: current_time - uptime
     * @return Unix timestamp of device boot, or -1 if failed
     */
    public static long getDeviceBootTimestamp() {
        try {
            // Current time in seconds
            long currentTimeSeconds = System.currentTimeMillis() / 1000L;
            
            // Uptime in seconds
            long uptimeSeconds = SystemClock.elapsedRealtime() / 1000L;
            
            // Boot timestamp = current time - uptime
            long bootTimestamp = currentTimeSeconds - uptimeSeconds;
            
            return bootTimestamp;
        } catch (Exception e) {
            Log.e(TAG, "Failed to get device boot timestamp", e);
            return -1L;
        }
    }
}




