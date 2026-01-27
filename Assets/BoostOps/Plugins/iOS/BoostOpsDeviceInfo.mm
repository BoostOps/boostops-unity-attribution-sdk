#import <Foundation/Foundation.h>
#import <sys/sysctl.h>

extern "C" {
    
    /// <summary>
    /// Get device uptime in seconds (time since device last booted)
    /// Uses sysctl to get boot time directly from kernel
    /// </summary>
    /// <returns>Device uptime in seconds, or -1 if failed</returns>
    double _BoostOpsGetDeviceUptimeSeconds() {
        @autoreleasepool {
            struct timeval boottime;
            size_t len = sizeof(boottime);
            int mib[2] = { CTL_KERN, KERN_BOOTTIME };
            
            if (sysctl(mib, 2, &boottime, &len, NULL, 0) == 0) {
                // Calculate uptime: current time - boot time
                time_t currentTime = time(NULL);
                time_t bootTime = boottime.tv_sec;
                double uptimeSeconds = difftime(currentTime, bootTime);
                
                return uptimeSeconds;
            } else {
                NSLog(@"[BoostOps] Failed to get device boot time via sysctl");
                return -1.0;
            }
        }
    }
    
    /// <summary>
    /// Get device boot timestamp in Unix seconds (when device was last booted)
    /// </summary>
    /// <returns>Unix timestamp of device boot, or -1 if failed</returns>
    long _BoostOpsGetDeviceBootTimestamp() {
        @autoreleasepool {
            struct timeval boottime;
            size_t len = sizeof(boottime);
            int mib[2] = { CTL_KERN, KERN_BOOTTIME };
            
            if (sysctl(mib, 2, &boottime, &len, NULL, 0) == 0) {
                return (long)boottime.tv_sec;
            } else {
                NSLog(@"[BoostOps] Failed to get device boot timestamp via sysctl");
                return -1L;
            }
        }
    }
    
    /// <summary>
    /// Get app install timestamp (when app was first installed on device)
    /// Uses Documents directory creation date as proxy for install time
    /// </summary>
    /// <returns>Unix timestamp of app install, or 0 if failed</returns>
    long _BoostOpsGetAppInstallTimestamp() {
        @autoreleasepool {
            // Get Documents directory (created on first app launch)
            NSArray *paths = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES);
            if (paths.count == 0) {
                NSLog(@"[BoostOps] Failed to get Documents directory");
                return 0;
            }
            
            NSString *documentsPath = paths[0];
            NSError *error = nil;
            NSDictionary *attributes = [[NSFileManager defaultManager] attributesOfItemAtPath:documentsPath error:&error];
            
            if (error || !attributes) {
                NSLog(@"[BoostOps] Failed to get Documents directory attributes: %@", error);
                return 0;
            }
            
            NSDate *creationDate = attributes[NSFileCreationDate];
            if (!creationDate) {
                NSLog(@"[BoostOps] Documents directory has no creation date");
                return 0;
            }
            
            return (long)[creationDate timeIntervalSince1970];
        }
    }
}




