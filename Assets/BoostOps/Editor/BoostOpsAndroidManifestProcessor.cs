using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace BoostOps.Editor
{
    /// <summary>
    /// Build-time processor that automatically configures AndroidManifest.xml with BoostOps deep linking intent-filters.
    /// Similar to Branch and AppsFlyer Unity SDK approach - developers never manually edit the manifest.
    /// </summary>
    public class BoostOpsAndroidManifestProcessor : IPostGenerateGradleAndroidProject
    {
        // Callback order - run after Unity's default processing but before other plugins
        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            Debug.Log("[BoostOps] 🔧 Processing AndroidManifest.xml for deep linking...");

            try
            {
                // Load BoostOps project settings
                var settings = BoostOpsProjectSettings.GetInstance();
                if (settings == null)
                {
                    Debug.LogWarning("[BoostOps] ⚠️ BoostOpsProjectSettings not found - skipping AndroidManifest processing");
                    return;
                }

                // Get configured domains
                var domains = settings.GetAllHosts();
                if (domains == null || domains.Count == 0)
                {
                    Debug.LogWarning("[BoostOps] ⚠️ No domains configured in BoostOpsProjectSettings - skipping AndroidManifest processing");
                    Debug.LogWarning("[BoostOps] ⚠️ Configure your domains in BoostOps > BoostOps Cloud to enable deep linking");
                    return;
                }

                Debug.Log($"[BoostOps] 🔍 Found {domains.Count} configured domain(s): {string.Join(", ", domains)}");

                // Locate AndroidManifest.xml
                string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
                if (!File.Exists(manifestPath))
                {
                    Debug.LogError($"[BoostOps] ❌ AndroidManifest.xml not found at: {manifestPath}");
                    return;
                }

                // Load and process the manifest
                var manifest = new AndroidManifestHelper(manifestPath);
                
                // Get package name for validation
                string packageName = manifest.GetPackageName();
                Debug.Log($"[BoostOps] 📦 Package name: {packageName}");

                // Validate package name matches settings
                if (!string.IsNullOrEmpty(settings.androidPackageName) && 
                    settings.androidPackageName != packageName)
                {
                    Debug.LogWarning($"[BoostOps] ⚠️ Package name mismatch!");
                    Debug.LogWarning($"[BoostOps]    AndroidManifest.xml: {packageName}");
                    Debug.LogWarning($"[BoostOps]    BoostOpsProjectSettings: {settings.androidPackageName}");
                    Debug.LogWarning($"[BoostOps]    This may cause Android App Links to fail!");
                }

                // Find the main activity
                var mainActivityNode = manifest.GetMainActivity();
                if (mainActivityNode == null)
                {
                    Debug.LogError("[BoostOps] ❌ Main activity (MAIN/LAUNCHER) not found in AndroidManifest.xml");
                    return;
                }

                string activityName = manifest.GetActivityName(mainActivityNode);
                Debug.Log($"[BoostOps] 🎯 Main activity: {activityName}");

                // Process each domain
                int addedCount = 0;
                int skippedCount = 0;

                foreach (var domain in domains)
                {
                    if (string.IsNullOrEmpty(domain))
                        continue;

                    // Check if intent-filter already exists for this domain
                    if (manifest.HasAppLinkIntentFilterForDomain(mainActivityNode, domain))
                    {
                        Debug.Log($"[BoostOps] ✅ Intent-filter already exists for domain: {domain}");
                        skippedCount++;
                        continue;
                    }

                    // Add App Links intent-filter
                    manifest.AddAppLinkIntentFilter(mainActivityNode, domain);
                    Debug.Log($"[BoostOps] ➕ Added App Links intent-filter for domain: {domain}");
                    addedCount++;
                }

                // Save the modified manifest
                manifest.Save();

                // Log summary
                Debug.Log("[BoostOps] ✅ AndroidManifest.xml processing complete!");
                Debug.Log($"[BoostOps]    Added: {addedCount} intent-filter(s)");
                Debug.Log($"[BoostOps]    Skipped: {skippedCount} intent-filter(s) (already exist)");
                
                if (addedCount > 0)
                {
                    Debug.Log("[BoostOps] 📱 Don't forget to:");
                    Debug.Log("[BoostOps]    1. Generate assetlinks.json in BoostOps Editor");
                    Debug.Log("[BoostOps]    2. Upload to https://{domain}/.well-known/assetlinks.json");
                    Debug.Log("[BoostOps]    3. Verify with: https://digitalassetlinks.googleapis.com/v1/statements:list?source.web.site=https://{domain}&relation=delegate_permission/common.handle_all_urls");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BoostOps] ❌ Failed to process AndroidManifest.xml: {ex.Message}");
                Debug.LogError($"[BoostOps] Stack trace: {ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Helper class for parsing and modifying AndroidManifest.xml
    /// Handles XML namespace management and common manifest operations
    /// </summary>
    public class AndroidManifestHelper
    {
        private readonly string _path;
        private readonly XmlDocument _doc;
        private readonly XmlNamespaceManager _nsmgr;
        private const string ANDROID_NS = "http://schemas.android.com/apk/res/android";

        public AndroidManifestHelper(string path)
        {
            _path = path;
            _doc = new XmlDocument();
            _doc.Load(path);

            // Set up namespace manager for android: attributes
            _nsmgr = new XmlNamespaceManager(_doc.NameTable);
            _nsmgr.AddNamespace("android", ANDROID_NS);
        }

        public void Save()
        {
            // Preserve formatting by using XmlWriterSettings
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace
            };

            using (var writer = XmlWriter.Create(_path, settings))
            {
                _doc.Save(writer);
            }

            Debug.Log($"[BoostOps] 💾 Saved AndroidManifest.xml to: {_path}");
        }

        /// <summary>
        /// Get the package name from the manifest
        /// </summary>
        public string GetPackageName()
        {
            var manifestNode = _doc.SelectSingleNode("/manifest");
            return manifestNode?.Attributes?["package"]?.Value ?? "";
        }

        /// <summary>
        /// Find the main activity (activity with MAIN/LAUNCHER intent-filter)
        /// </summary>
        public XmlNode GetMainActivity()
        {
            // Find all activities
            var activities = _doc.SelectNodes("//activity");
            if (activities == null) return null;

            foreach (XmlNode activity in activities)
            {
                // Check if this activity has a MAIN/LAUNCHER intent-filter
                var intentFilters = activity.SelectNodes("intent-filter");
                if (intentFilters == null) continue;

                foreach (XmlNode filter in intentFilters)
                {
                    var hasMain = filter.SelectSingleNode("action[@android:name='android.intent.action.MAIN']", _nsmgr) != null;
                    var hasLauncher = filter.SelectSingleNode("category[@android:name='android.intent.category.LAUNCHER']", _nsmgr) != null;

                    if (hasMain && hasLauncher)
                    {
                        return activity;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get the activity name (android:name attribute)
        /// </summary>
        public string GetActivityName(XmlNode activityNode)
        {
            return activityNode?.Attributes?["android:name"]?.Value ?? "Unknown";
        }

        /// <summary>
        /// Check if an App Links intent-filter already exists for a domain
        /// </summary>
        public bool HasAppLinkIntentFilterForDomain(XmlNode activityNode, string domain)
        {
            var intentFilters = activityNode.SelectNodes("intent-filter");
            if (intentFilters == null) return false;

            foreach (XmlNode filter in intentFilters)
            {
                // Check if this is an App Links filter (has autoVerify=true)
                var autoVerifyAttr = filter.Attributes?["android:autoVerify"];
                if (autoVerifyAttr?.Value != "true") continue;

                // Check if this filter has the correct structure
                var hasView = filter.SelectSingleNode("action[@android:name='android.intent.action.VIEW']", _nsmgr) != null;
                var hasDefault = filter.SelectSingleNode("category[@android:name='android.intent.category.DEFAULT']", _nsmgr) != null;
                var hasBrowsable = filter.SelectSingleNode("category[@android:name='android.intent.category.BROWSABLE']", _nsmgr) != null;

                if (!hasView || !hasDefault || !hasBrowsable) continue;

                // Check if this filter has a data tag for our domain
                var dataNodes = filter.SelectNodes("data");
                if (dataNodes == null) continue;

                foreach (XmlNode data in dataNodes)
                {
                    var scheme = data.Attributes?["android:scheme"]?.Value;
                    var host = data.Attributes?["android:host"]?.Value;

                    if (scheme == "https" && host == domain)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Add an App Links intent-filter to an activity for a specific domain
        /// </summary>
        public void AddAppLinkIntentFilter(XmlNode activityNode, string domain)
        {
            // Create intent-filter element
            var intentFilter = _doc.CreateElement("intent-filter");
            
            // Add android:autoVerify="true" attribute
            var autoVerifyAttr = _doc.CreateAttribute("android", "autoVerify", ANDROID_NS);
            autoVerifyAttr.Value = "true";
            intentFilter.Attributes.Append(autoVerifyAttr);

            // Add action: VIEW
            var action = _doc.CreateElement("action");
            var actionNameAttr = _doc.CreateAttribute("android", "name", ANDROID_NS);
            actionNameAttr.Value = "android.intent.action.VIEW";
            action.Attributes.Append(actionNameAttr);
            intentFilter.AppendChild(action);

            // Add category: DEFAULT
            var categoryDefault = _doc.CreateElement("category");
            var categoryDefaultNameAttr = _doc.CreateAttribute("android", "name", ANDROID_NS);
            categoryDefaultNameAttr.Value = "android.intent.category.DEFAULT";
            categoryDefault.Attributes.Append(categoryDefaultNameAttr);
            intentFilter.AppendChild(categoryDefault);

            // Add category: BROWSABLE
            var categoryBrowsable = _doc.CreateElement("category");
            var categoryBrowsableNameAttr = _doc.CreateAttribute("android", "name", ANDROID_NS);
            categoryBrowsableNameAttr.Value = "android.intent.category.BROWSABLE";
            categoryBrowsable.Attributes.Append(categoryBrowsableNameAttr);
            intentFilter.AppendChild(categoryBrowsable);

            // Add data: scheme="https" host="{domain}"
            var data = _doc.CreateElement("data");
            var schemeAttr = _doc.CreateAttribute("android", "scheme", ANDROID_NS);
            schemeAttr.Value = "https";
            data.Attributes.Append(schemeAttr);
            
            var hostAttr = _doc.CreateAttribute("android", "host", ANDROID_NS);
            hostAttr.Value = domain;
            data.Attributes.Append(hostAttr);
            intentFilter.AppendChild(data);

            // Append to activity
            activityNode.AppendChild(intentFilter);
        }

        /// <summary>
        /// Get all domains configured in existing App Links intent-filters
        /// </summary>
        public List<string> GetConfiguredDomains()
        {
            var domains = new List<string>();
            var intentFilters = _doc.SelectNodes("//intent-filter[@android:autoVerify='true']", _nsmgr);
            
            if (intentFilters == null) return domains;

            foreach (XmlNode filter in intentFilters)
            {
                var dataNodes = filter.SelectNodes("data");
                if (dataNodes == null) continue;

                foreach (XmlNode data in dataNodes)
                {
                    var scheme = data.Attributes?["android:scheme"]?.Value;
                    var host = data.Attributes?["android:host"]?.Value;

                    if (scheme == "https" && !string.IsNullOrEmpty(host))
                    {
                        domains.Add(host);
                    }
                }
            }

            return domains.Distinct().ToList();
        }
    }
}

