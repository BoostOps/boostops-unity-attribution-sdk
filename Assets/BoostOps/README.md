# BoostOps Unity SDK

Professional Unity SDK for mobile app cross-promotion, attribution tracking, and analytics with **maximum IP protection** through DLL compilation.

## ✨ Features

- **🎯 Cross-Promotion System**: Display and track campaigns between your apps
- **📊 Attribution Tracking**: Track installs, conversions, and user flows  
- **🔗 Deep Link Configuration**: Automated iOS Universal Links and Android App Links setup
- **🛡️ IP Protection**: Core logic protected in compiled DLL for Asset Store distribution
- **📈 Analytics Integration**: Comprehensive event tracking and reporting
- **⚙️ Unity Remote Config**: Server-managed campaign configuration

## 🏗️ Architecture Overview

### **Development Mode** (Source Code Access)
```
Assets/BoostOps/Scripts/
├── 📁 Public/              # Customer-facing API (3 files)
│   ├── BoostOpsSDK.cs      # Main static SDK interface
│   ├── BoostOpsManager.cs  # Manager component (facade)
│   └── Campaign.cs         # Campaign data structures
├── 📁 Internal/            # Core implementation (4 files)
├── 📁 Analytics/           # Analytics system (11 files)
├── 📁 Attribution/         # Install tracking (7 files)
├── 📁 CrossPromo/          # Campaign display (4 files)
├── 📁 RemoteConfig/        # Config providers (5 files)
├── 📁 Security/            # Security utilities (5 files)
├── 📁 Utilities/           # Helper classes (8 files)
└── 📁 Configuration/       # Settings management (2 files)
```

### **Production Mode** (DLL Protection)
```
Assets/BoostOps/
├── 📁 Plugins/
│   └── BoostOps.Internal.dll    # 🔒 ALL SDK logic (327KB)
├── 📁 Editor/                   # Unity Editor integration
├── 📁 Examples/                 # Demo applications  
├── 📁 Prefabs/                  # UI components
└── 📁 Resources/                # Configuration files
```

## 🚀 Quick Start

### 1. Initialize the SDK

```csharp
using BoostOps;

public class GameManager : MonoBehaviour
{
    async void Start()
    {
        // Initialize SDK - automatically detects configuration mode
        BoostOpsSDK.Initialize();
        
        if (BoostOpsSDK.IsInitialized)
        {
            Debug.Log("BoostOps SDK ready!");
        }
    }
}
```

### 2. Show Cross-Promotion

```csharp
// Smart cross-promotion (server optimizes format)
BoostOpsSDK.ShowCrossPromo("level_complete");

// Specific formats
BoostOpsSDK.ShowCrossPromo("main_menu", PromoFormat.Banner);
BoostOpsSDK.ShowCrossPromo("game_over", PromoFormat.Icon);

// Hide when needed
BoostOpsSDK.HideCrossPromo("main_menu");
```

### 3. Track Analytics

```csharp
// Track custom events
BoostOpsSDK.TrackEvent("level_complete", new Dictionary<string, object>
{
    ["level"] = 5,
    ["score"] = 1000,
    ["time"] = 120.5f
});

// Get campaign information
int campaignCount = BoostOpsSDK.GetCampaignCount();
var campaigns = BoostOpsSDK.GetAllCampaigns();
```

## 🔧 Configuration Modes

### **Local Mode** (Development)
- **Source files**: Full access for debugging and development
- **Local campaigns**: JSON files in StreamingAssets
- **Editor integration**: Full Unity Editor support
- **Hot reload**: Immediate code changes

### **BoostOps Managed Mode** (Production)
- **Unity Remote Config**: Server-managed campaign configuration
- **Real-time updates**: No app updates required for new campaigns
- **A/B testing**: Server-side campaign optimization
- **Analytics**: Comprehensive tracking and reporting

### **DLL Protected Mode** (Distribution)
- **IP protection**: All source code compiled into `BoostOps.Internal.dll`
- **Asset Store ready**: Clean package with minimal exposed code
- **Full functionality**: Same features as source code version
- **Customer friendly**: Simple integration without source complexity

## 🛠️ Development Workflow

### **For SDK Development:**
```bash
# All source files available in Unity Assets
# Full debugging and IntelliSense support
# Edit, test, and iterate normally
```

### **For Production Release:**
```bash
# Build protected DLL for distribution
./BoostOps-DLL-Build/build-production-dll.sh

# Result: BoostOps.Internal.dll in Assets/BoostOps/Plugins/
# Source files remain in Assets/ for continued development
```

## 📁 Project Structure

### **Unity Assets (Customer Facing)**
```
Assets/BoostOps/
├── 📁 Plugins/
│   ├── BoostOps.Internal.dll        # 🔒 Protected SDK implementation
│   ├── 📁 Android/                  # Native Android plugins
│   └── 📁 iOS/                      # Native iOS plugins
├── 📁 Editor/                       # Unity Editor integration
│   ├── BoostOpsEditorWindow.cs      # Main configuration window
│   ├── BoostOpsPackageBuilder.cs    # Package export tools
│   └── BoostOpsPostProcessBuild.cs  # Build automation
├── 📁 Examples/                     # Demo applications
│   └── Scripts/
│       └── BoostOpsDemo.cs          # Complete usage example
├── 📁 Prefabs/                      # UI campaign prefabs
│   ├── BoostOpsBanner.prefab
│   ├── BoostOpsIconInterstitial.prefab
│   ├── BoostOpsRichInterstitial.prefab
│   └── BoostOpsNative.prefab
└── 📁 Resources/                    # Configuration files
    └── BoostOpsProjectSettings.asset
```

### **Development Source (Internal)**
```
BoostOps-Internal-Source/            # External build directory
├── All SDK source files            # Copied during build
└── (Compiled into DLL)             # Not visible to customers
```

## 🎨 Prefab System

### **Automatic Campaign Display**
- **Smart prefab selection**: Server chooses optimal format
- **Auto-population**: Campaign data fills UI elements automatically  
- **Naming conventions**: Standard component names for easy customization
- **Preserve styling**: Only content updated, design preserved

### **Custom Prefab Creation**
Create prefabs with these component names:

**Text Components:**
- `CampaignTitle` or `Title` → Campaign name
- `CampaignDescription` or `Description` → Game description  
- `CTA` or `ButtonText` → Call-to-action text
- `Developer` or `Studio` → Developer name

**Image Components:**
- `CampaignIcon` or `Icon` → Game icon
- `CampaignImage` or `Screenshot` → Game screenshot

**Button Components:**
- `CampaignButton` or `PlayButton` → Main action button
- `CloseButton` or `XButton` → Close/dismiss button

## 📊 Unity Remote Config Integration

### **Server-Managed Campaigns**
```csharp
// Demo app fetches remote config
await Unity.Services.Core.UnityServices.InitializeAsync();
await Unity.Services.RemoteConfig.RemoteConfigService.Instance.FetchConfigsAsync();

// SDK reads config when showing campaigns
BoostOpsSDK.ShowCrossPromo("level_complete"); // Uses remote config
```

### **Configuration Key**
- **Remote Config Key**: `"boostops_config"`
- **Format**: JSON campaign configuration
- **Updates**: Real-time without app updates
- **Fallback**: No local fallback in managed mode

## 🔒 IP Protection Strategy

### **Development Benefits**
- ✅ **Full source access** for debugging and development
- ✅ **Unity IntelliSense** and code completion
- ✅ **Breakpoint debugging** and step-through
- ✅ **Hot reload** and real-time compilation
- ✅ **Easy refactoring** and code navigation

### **Distribution Benefits**  
- ✅ **Complete IP protection** - All logic hidden in DLL
- ✅ **Asset Store ready** - Clean, professional package
- ✅ **Customer friendly** - Simple integration without source complexity
- ✅ **Competitive advantage** - Implementation details protected

### **Build Process**
1. **Development**: Edit source files in Unity Assets
2. **Production**: Run build script to create DLL
3. **Distribution**: Package includes DLL + Editor tools + Examples
4. **Customer**: Gets full functionality without source exposure

## 🧪 Testing & Validation

### **Editor Testing**
```csharp
// Test SDK functionality in editor
public void TestSDKFunctionality()
{
    // Check initialization
    bool initialized = BoostOpsSDK.IsInitialized;
    
    // Test campaign access
    int count = BoostOpsSDK.GetCampaignCount();
    var campaigns = BoostOpsSDK.GetAllCampaigns();
    
    // Test analytics
    BoostOpsSDK.TrackEvent("test_event", new Dictionary<string, object>
    {
        {"test_param", "test_value"}
    });
}
```

### **Runtime Testing**
- **Demo application**: Complete example in `Assets/BoostOps/Examples/`
- **Button testing**: Test all SDK functionality through UI
- **Remote config**: Test server-managed campaign loading
- **Analytics**: Verify event tracking and campaign metrics

## 📖 API Reference

### **Core SDK Methods**
```csharp
// Initialization
BoostOpsSDK.Initialize()                    // Initialize SDK
bool BoostOpsSDK.IsInitialized             // Check initialization status

// Cross-Promotion  
BoostOpsSDK.ShowCrossPromo(placement, format, options)
BoostOpsSDK.HideCrossPromo(placement)

// Campaign Access
int BoostOpsSDK.GetCampaignCount()
List<Campaign> BoostOpsSDK.GetAllCampaigns()

// Analytics
BoostOpsSDK.TrackEvent(eventName, parameters)

// Configuration
BoostOpsSDK.SetSdkKey(key)
BoostOpsSDK.SetDemoDataFile(path)
```

### **Manager Component**
```csharp
// Access via BoostOpsManager.Instance
BoostOpsManager.Instance.InitializeAsync()
BoostOpsManager.Instance.ShowCrossPromo(placement, format)
BoostOpsManager.Instance.GetCampaignCount()
BoostOpsManager.Instance.GetAllCampaigns()
```

### **Campaign Data Structure**
```csharp
public class Campaign
{
    public string id;
    public string name;
    public TargetGame target_project;
    public CampaignSchedule schedule;
    public Creative[] creatives;
    
    // Utility methods
    public string ExtractIosAppStoreId()
    public string ExtractAndroidPackageId()  
    public string GetIconUrl()
    public bool HasValidStoreUrl()
}
```

## 🔧 Editor Integration

### **BoostOps Configuration Window**
**Unity Menu → BoostOps → Configuration**

#### **Overview Tab**
- Project status and account management
- Quick access to main features
- Real-time sync status

#### **Cross-Promo Tab**  
- Campaign management interface
- Local vs managed mode selection
- Target game configuration
- Analytics and testing tools

#### **Accounts Tab**
- Google OAuth authentication
- Project linking and management
- Account settings

### **Package Builder**
**Unity Menu → BoostOps Admin → Package Builder**
- Export development packages (with source)
- Export production packages (DLL protected)
- Asset Store submission tools
- Validation and testing

## 🚀 Distribution Modes

### **Development Package**
- **Includes**: All source files + Editor tools + Examples
- **Use case**: SDK development, debugging, customization
- **IP protection**: None (full source access)

### **Production Package** 
- **Includes**: `BoostOps.Internal.dll` + Editor tools + Examples
- **Use case**: Asset Store distribution, customer releases
- **IP protection**: Maximum (all logic in DLL)

### **Asset Store Package**
- **Includes**: Production package + Asset Store metadata
- **Use case**: Unity Asset Store submission
- **IP protection**: Maximum + professional presentation

## 🛠️ Build Requirements

### **Development Environment**
- **Unity**: 2019.4 LTS or newer
- **Packages**: Unity Services Core, Unity Remote Config
- **.NET**: Standard 2.1 compatibility
- **Platforms**: iOS, Android support

### **Production Build**
- **.NET SDK**: 6.0 or newer for DLL compilation
- **Unity**: Must be closed during DLL build
- **Platform**: macOS, Windows, Linux supported
- **Dependencies**: All Unity assemblies automatically referenced

## 📄 License & Support

This SDK is part of the BoostOps platform. 

### **For Customers**
- Professional Unity SDK with full functionality
- Clean integration without source complexity
- Complete documentation and examples
- Editor tools for easy configuration

### **For Developers**  
- Full source code access for development
- Professional build tools for distribution
- IP protection for competitive advantage
- Asset Store ready packaging

---

**🎯 Professional Unity SDK Development**

This SDK follows Unity best practices and professional development patterns:
- ✅ **Clean architecture** with facade pattern for IP protection
- ✅ **Comprehensive editor tools** for easy configuration  
- ✅ **Automated build process** for development and production
- ✅ **Complete documentation** with examples and API reference
- ✅ **Asset Store ready** with maximum IP protection
- ✅ **Professional packaging** for distribution and licensing