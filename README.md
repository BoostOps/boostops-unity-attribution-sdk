# BoostOps Unity SDK

[![Unity Version](https://img.shields.io/badge/Unity-2020.3%2B-blue.svg)](https://unity.com/)
[![Platform](https://img.shields.io/badge/platform-iOS%20%7C%20Android-lightgrey.svg)](https://boostops.io)
[![License](https://img.shields.io/badge/license-Commercial-green.svg)](https://boostops.io)

The official Unity SDK for [BoostOps](https://boostops.io) - a complete mobile game growth platform with best-in-class attribution and cross-promotion.

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
  - [Attribution](#attribution)
  - [Deep Links](#deep-links)
  - [Cross-Promotion](#cross-promotion)
  - [Event Tracking](#event-tracking)
- [Troubleshooting](#troubleshooting)
- [Best Practices](#best-practices)
- [Support](#support)
- [License](#license)

## Features

- **🎯 Precise Attribution Tracking** - Track installs, events, and revenue back to the exact source with industry-leading accuracy
- **🔗 Dynamic Deep Links** - Create trackable branded links with full attribution of clicks, installs, and post-install events
- **📊 Complete Player Journey Tracking** - Understand the full path from ad click → install → first session → purchase → retention
- **💰 LTV & ROAS Measurement** - Identify which channels drive your highest-value players and optimize spend accordingly
- **🎮 Smart Cross-Promotion** - Promote your portfolio of games with full attribution of cross-installs
- **🎨 Remote Configuration** - Update game behavior and campaigns without app store submissions
- **⭐ StoreKit & RevenueCat Integration** - Automatic purchase tracking with receipt validation

## Requirements

- **Unity:** 2020.3 or higher
- **iOS:** 12.0 or higher
- **Android:** API Level 21 (Android 5.0) or higher
- **BoostOps Account:** [Sign up for free](https://boostops.io)

## Installation

### Via Unity Package Manager

1. Download the latest `.unitypackage` from the [Releases](https://github.com/boostops/boostops-unity-sdk/releases) page
2. In Unity, go to **Assets → Import Package → Custom Package**
3. Select the downloaded `.unitypackage` file
4. Click **Import** to add the SDK to your project

### Post-Installation

The SDK will automatically configure itself during import. You can verify installation by checking for the **BoostOps** menu in the Unity toolbar.

## Quick Start

### 1. Initialize the SDK

```csharp
using BoostOps;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        BoostOpsSDK.Initialize();
    }
}
```

### 2. Show Cross-Promotion

```csharp
void OnLevelComplete()
{
    BoostOpsSDK.ShowCrossPromo("level_complete");
}
```

### 3. Track Purchases

```csharp
void OnPurchaseComplete(string productId, decimal price, string currency)
{
    BoostOpsSDK.TrackEvent("iap_purchase", new Dictionary<string, object>
    {
        { "product_id", productId },
        { "price_usd", price },
        { "currency", currency }
    });
}
```

## Core Concepts

### Attribution

BoostOps provides **multi-touch attribution** that connects every player action back to its acquisition source.

When a player installs your game via a tracked source (ad campaign, deep link, cross-promotion), BoostOps captures:

1. **Install Attribution** - The original source (campaign, ad, link, etc.) 
2. **Revenue Attribution** - In-app purchases attributed back to acquisition campaign
3. **Cohort Analysis** - Compare player quality and LTV across traffic sources

### Boostlink Deep Links

Create branded boostlink short links in the BoostOps dashboard that drive installs and attribute all downstream actions.

**Free:** `https://yourgame.boostlink.me/summer-sale?offer=50off`  
**Premium:** `https://yourgame.bst.to/summer-sale?offer=50off`

These links track clicks, attribute installs, and pass parameters to your app on first open.

### Cross-Promotion

Promote your other games to existing players with full attribution tracking.

```csharp
// Show interstitial at natural break points
BoostOpsSDK.ShowCrossPromo("level_complete");

// Hide when returning to gameplay
BoostOpsSDK.HideCrossPromo();
```

## Troubleshooting

### SDK Not Initializing

**Symptoms:** `IsInitialized` returns false, campaigns don't show

**Solutions:**
- Check Unity console for initialization errors
- Verify BoostOpsManager exists in your first scene
- Ensure you called `BoostOpsSDK.Initialize()`
- Check that campaign data is available (local or remote)

### Campaigns Not Appearing

**Symptoms:** `GetCampaignCount()` returns 0

**Solutions:**
- In local mode: Check `StreamingAssets/campaigns.json` exists
- In server mode: Verify Unity Remote Config is set up
- Check Unity console for campaign loading errors
- Subscribe to `OnCampaignsReady` to know when campaigns load

### Deep Links Not Working

**iOS:**
- Verify Associated Domains capability is enabled in Xcode
- Check that your domain is configured in Apple Developer portal
- Ensure your app's `Info.plist` includes proper URL schemes

**Android:**
- Verify `AndroidManifest.xml` includes intent filters for your domain
- Check that Digital Asset Links file is hosted at your domain
- Test with `adb shell am start -W -a android.intent.action.VIEW -d "your-deep-link-url"`

### Events Not Tracking

**Symptoms:** Events don't appear in dashboard

**Solutions:**
- Verify SDK is initialized before tracking events
- Check internet connectivity
- Events may take 1-2 minutes to appear in real-time view
- Enable debug logging to see event confirmation

## Best Practices

### 1. Initialize Early

Initialize the SDK as early as possible in your app lifecycle:

```csharp
public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        BoostOpsSDK.Initialize();
    }
}
```

### 2. Strategic Cross-Promo Placement

Show campaigns at natural break points:

```csharp
// ✅ Good: Natural break points
BoostOpsSDK.ShowCrossPromo("level_complete");
BoostOpsSDK.ShowCrossPromo("game_over");
BoostOpsSDK.ShowCrossPromo("daily_reward");

// ❌ Bad: During gameplay
// BoostOpsSDK.ShowCrossPromo("mid_level"); // Interrupts player
```

### 3. Test Attribution Before Launch

Test your attribution setup thoroughly:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
void TestAttribution()
{
    // Create test deep link
    string testLink = "https://yourgame.boostlink.me/test?campaign=test&source=dev";
    
    // Simulate deep link open
    Debug.Log($"Testing attribution with link: {testLink}");
    
    // Track test events
    BoostOpsSDK.TrackEvent("test_event", new Dictionary<string, object>
    {
        { "test_campaign", "test" },
        { "test_timestamp", DateTime.Now.ToString() }
    });
}
#endif
```

## Support

- **Documentation:** [boostops.io/docs](https://boostops.io/docs)
- **Email Support:** support@boostops.io
- **Dashboard:** [app.boostops.io](https://app.boostops.io)
- **GitHub Issues:** [Report bugs and feature requests](https://github.com/boostops/boostops-unity-sdk/issues)

## License

Copyright © 2024 BoostOps. All rights reserved.

This SDK is distributed under a commercial license. For licensing inquiries, contact sales@boostops.io.

---

<div align="center">

**[boostops.io](https://boostops.io)** • **[Documentation](https://boostops.io/docs)** • **[Dashboard](https://app.boostops.io)**

Made with ❤️ by the BoostOps team

</div>
