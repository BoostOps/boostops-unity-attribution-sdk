#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BoostOps.Editor
{
    public static class BoostOpsFileGenerator
    {
        public static void GenerateAppleAppSiteAssociation(string customDomain, string iosTeamId, string iosBundleId)
        {
            // Generate iOS Universal Links configuration file
            string appleConfigJson = $@"{{
  ""applinks"": {{
    ""apps"": [],
    ""details"": [
      {{
        ""appID"": ""{iosTeamId}.{iosBundleId}"",
        ""paths"": [ ""*"" ]
      }}
    ]
  }}
}}";
            
            // ✅ NEW PATH: Separate folder (preserved on SDK updates)
            // ✅ RENAMED: well_known_server (visible on macOS/iOS - no dot prefix)
            // NOTE: Upload this to your server as ".well-known" (with dot)
            File.WriteAllText("Assets/BoostOpsGenerated/ServerFiles/well_known_server/apple-app-site-association", appleConfigJson);
        }
        
        public static void GenerateIOSEntitlements(string customDomain)
        {
            // Generate iOS entitlements file for Universal Links
            string entitlementsContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>com.apple.developer.associated-domains</key>
    <array>
        <string>applinks:{customDomain}</string>
    </array>
</dict>
</plist>";
            
            // ✅ NEW PATH: Separate folder (preserved on SDK updates)
            File.WriteAllText("Assets/BoostOpsGenerated/BoostOps.entitlements", entitlementsContent);
        }
        
        public static void GenerateAndroidAssetLinks(string customDomain, string androidBundleId, string androidCertFingerprint)
        {
            // Generate Android App Links configuration file
            string androidConfigJson = $@"[
  {{
    ""relation"": [""delegate_permission/common.handle_all_urls""],
    ""target"": {{
      ""namespace"": ""android_app"",
      ""package_name"": ""{androidBundleId}"",
      ""sha256_cert_fingerprints"": [
        ""{androidCertFingerprint}""
      ]
    }}
  }}
]";
            
            // ✅ NEW PATH: Separate folder (preserved on SDK updates)
            // ✅ RENAMED: well_known_server (visible on macOS/iOS - no dot prefix)
            // NOTE: Upload this to your server as ".well-known" (with dot)
            File.WriteAllText("Assets/BoostOpsGenerated/ServerFiles/well_known_server/assetlinks.json", androidConfigJson);
        }
        
        public static void GenerateAndroidManifest(string customDomain)
        {
            // Generate Android Manifest Fragment with only App Links intent filter
            string androidManifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- BoostOps Android Manifest Fragment -->
<!-- Domain: {customDomain} -->


<manifest xmlns:android=""http://schemas.android.com/apk/res/android"">

    <!-- BoostOps Required Permissions -->
    
    <!-- Internet permission for BoostOps API calls and Install Referrer -->
    <uses-permission android:name=""android.permission.INTERNET"" />
    
    <!-- Network state permission for connectivity checks -->
    <uses-permission android:name=""android.permission.ACCESS_NETWORK_STATE"" />
    
    <!-- AD_ID permission for Android API 33+ advertising attribution -->
    <!-- Required for Install Referrer and attribution on Android 13+ -->
    <!-- Apps targeting children should remove this permission -->
    <uses-permission android:name=""com.google.android.gms.permission.AD_ID"" />
    
    <!-- Google Play Billing permission (if using revenue tracking) -->
    <uses-permission android:name=""com.android.vending.BILLING"" />

    <application>
        <activity android:name=""com.unity3d.player.UnityPlayerActivity"">
            <!-- BoostOps App Links Intent Filter -->
            <intent-filter android:autoVerify=""true"">
                <action android:name=""android.intent.action.VIEW"" />
                <category android:name=""android.intent.category.DEFAULT"" />
                <category android:name=""android.intent.category.BROWSABLE"" />
                <data android:scheme=""https""
                      android:host=""{customDomain}"" />
            </intent-filter>
        </activity>
    </application>
</manifest>";
            
            // ✅ NEW PATH: Separate folder (preserved on SDK updates)
            File.WriteAllText("Assets/BoostOpsGenerated/Plugins/Android/AndroidManifest.xml", androidManifest);
        }
        
        public static void GenerateServerSetupInstructions(string customDomain, string iosTeamId, string iosBundleId, string androidBundleId)
        {
            string instructions = GenerateSetupInstructionsContent(customDomain, iosTeamId, iosBundleId, androidBundleId);
            // ✅ NEW PATH: Separate folder (preserved on SDK updates)
            File.WriteAllText("Assets/BoostOpsGenerated/SETUP_INSTRUCTIONS.md", instructions);
        }
        
        private static string GenerateSetupInstructionsContent(string customDomain, string iosTeamId, string iosBundleId, string androidBundleId)
        {
            return $@"# Universal Links Setup Instructions
Domain: {customDomain}

## Files Generated
1. apple-app-site-association - iOS Universal Links configuration
2. assetlinks.json - Android App Links configuration

## Server Setup

### 1. Upload .well-known files to your server

⚠️ **IMPORTANT:** The folder is named `well_known_server` in Unity (visible), but must be renamed to `.well-known` on your server!

**Steps:**
1. Go to `Assets/BoostOpsGenerated/ServerFiles/`
2. Copy the `well_known_server` folder to your web server
3. **RENAME** it to `.well-known` (with the dot)
4. Verify files are accessible:
   - https://{customDomain}/.well-known/apple-app-site-association
   - https://{customDomain}/.well-known/assetlinks.json

**Why different names?**
- Unity/macOS hide folders starting with `.` (dot)
- iOS/Android REQUIRE the folder to be `.well-known` on the server

### 2. Server Configuration
Ensure your server serves these files with correct headers:

**Apache (.htaccess)**:
```apache
<Files ""apple-app-site-association"">
    Header set Content-Type application/json
    Header set Access-Control-Allow-Origin *
</Files>

<Files ""assetlinks.json"">
    Header set Content-Type application/json
    Header set Access-Control-Allow-Origin *
</Files>
```

**Nginx**:
```nginx
location /.well-known/apple-app-site-association {{
    add_header Content-Type application/json;
    add_header Access-Control-Allow-Origin *;
}}

location /.well-known/assetlinks.json {{
    add_header Content-Type application/json;
    add_header Access-Control-Allow-Origin *;
}}
```

### 3. iOS App Configuration
Add to your iOS app's entitlements file:
```xml
<key>com.apple.developer.associated-domains</key>
<array>
    <string>applinks:{customDomain}</string>
</array>
```

### 4. Android App Configuration

#### Option A: Use Generated AndroidManifest.xml
1. Copy `Assets/BoostOpsGenerated/Plugins/Android/AndroidManifest.xml` to `Assets/Plugins/Android/AndroidManifest.xml`
2. If you already have an AndroidManifest.xml, merge the intent filters from the generated file into your existing manifest

#### Option B: Manual Integration
Add these intent filters to your existing AndroidManifest.xml:
```xml
<activity android:name=""com.unity3d.player.UnityPlayerActivity"">
    <!-- App Links Intent Filter -->
    <intent-filter android:autoVerify=""true"">
        <action android:name=""android.intent.action.VIEW"" />
        <category android:name=""android.intent.category.DEFAULT"" />
        <category android:name=""android.intent.category.BROWSABLE"" />
        <data android:scheme=""https""
              android:host=""{customDomain}"" />
    </intent-filter>
</activity>
```

### 5. Android Certificate Fingerprint
Replace ""TO_BE_REPLACED_WITH_YOUR_CERT_FINGERPRINT"" in assetlinks.json with your app's SHA256 certificate fingerprint.

Get your fingerprint with:
```bash
keytool -list -v -keystore your-keystore.jks -alias your-alias -storepass your-password
```

### 6. Testing
- iOS: https://developer.apple.com/library/archive/documentation/General/Conceptual/AppSearch/UniversalLinks.html
- Android: https://developer.android.com/training/app-links/verify-site-associations

Test your configuration:
- iOS: https://search.developer.apple.com/appsearch-validation-tool/
- Android: adb shell pm get-app-links {androidBundleId}

## Unity Integration

1. Create a ScriptableObject from BoostOpsUniversalLinksConfig
2. Configure the paths and domains
3. Handle deep links in your Unity app using the generated configuration

## Important Notes

- Files must be served over HTTPS
- No redirects allowed for .well-known files
- iOS requires Team ID and Bundle ID to match exactly
- Android requires SHA256 certificate fingerprint
- Both platforms cache these files, so changes may take time to propagate

## Team ID and Bundle ID Used
- iOS Team ID: {iosTeamId}
- iOS Bundle ID: {iosBundleId}
- Android Bundle ID: {androidBundleId}

Verify these match your app's configuration in Xcode and Android Studio.
";
        }
        
        public static void EnsureDirectoriesExist()
        {
            // ✅ NEW: Separate folder (preserved on SDK updates)
            string generatedRoot = "Assets/BoostOpsGenerated";
            string serverFilesPath = "Assets/BoostOpsGenerated/ServerFiles";
            string wellKnownPath = "Assets/BoostOpsGenerated/ServerFiles/well_known_server";
            string androidPath = "Assets/BoostOpsGenerated/Plugins/Android";
            
            if (!Directory.Exists(generatedRoot))
            {
                Directory.CreateDirectory(generatedRoot);
            }
            if (!Directory.Exists(serverFilesPath))
            {
                Directory.CreateDirectory(serverFilesPath);
            }
            if (!Directory.Exists(wellKnownPath))
            {
                Directory.CreateDirectory(wellKnownPath);
                
                // Create README to explain the folder name
                CreateWellKnownReadme(serverFilesPath);
            }
            if (!Directory.Exists(androidPath))
            {
                Directory.CreateDirectory(androidPath);
            }
            
            AssetDatabase.Refresh();
        }
        
        private static void CreateWellKnownReadme(string serverFilesPath)
        {
            string readmeContent = @"# Server Files for Universal Links

## ⚠️ IMPORTANT: Folder Name on Server

The folder `well_known_server` is named WITHOUT a dot (.) prefix so it's **visible** in Unity and macOS Finder.

When you upload these files to your web server, **RENAME the folder** to `.well-known` (WITH the dot):

```
✅ ON SERVER: https://yourdomain.com/.well-known/apple-app-site-association
❌ NOT THIS: https://yourdomain.com/well_known_server/apple-app-site-association
```

## Why the Different Name?

- **In Unity/macOS:** Folders starting with `.` (dot) are hidden by default
- **On Web Server:** iOS and Android REQUIRE the folder to be named `.well-known` (with dot)

## Files in This Folder

- `apple-app-site-association` - iOS Universal Links config (NO file extension)
- `assetlinks.json` - Android App Links config

## Server Upload Steps

1. Copy this entire folder to your server
2. **RENAME** `well_known_server` → `.well-known`
3. Verify the files are accessible:
   - https://yourdomain.com/.well-known/apple-app-site-association
   - https://yourdomain.com/.well-known/assetlinks.json

See `SETUP_INSTRUCTIONS.md` in the parent folder for full setup guide.
";
            
            File.WriteAllText($"{serverFilesPath}/README.md", readmeContent);
        }
    }
}
#endif 