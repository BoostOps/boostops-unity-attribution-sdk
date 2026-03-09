using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

#if USE_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace BoostOps
{
    /// <summary>
    /// Handles asset resolution from various sources: embedded, cache, and remote URLs
    /// </summary>
    public static class AssetResolver
    {
        /// <summary>
        /// Asset loading mode configuration
        /// </summary>
        public enum AssetLoadMode
        {
            Online,   // Load from URLs with local cache
            Offline   // Load only from local resources/embedded assets
        }

        private static AssetLoadMode _assetLoadMode = AssetLoadMode.Online;

        /// <summary>
        /// Get the current asset loading mode
        /// </summary>
        public static AssetLoadMode CurrentMode => _assetLoadMode;

        /// <summary>
        /// Set the asset loading mode
        /// </summary>
        /// <param name="mode">Asset loading mode</param>
        public static void SetAssetLoadMode(AssetLoadMode mode)
        {
            _assetLoadMode = mode;
            Debug.Log($"[BoostOps] Asset load mode set to: {mode}");
        }

        /// <summary>
        /// Load a sprite from embedded assets, cache, or download from URL
        /// </summary>
        /// <param name="variant">Creative variant with asset information</param>
        /// <returns>Loaded sprite or null if failed</returns>
        public static async Task<Sprite> LoadSpriteAsync(CreativeVariant variant)
        {
            if (variant == null)
            {
                Debug.LogWarning("[BoostOps] Cannot load sprite: variant is null");
                return null;
            }

            // 1. Try embedded assets first (if local_key exists)
            if (!string.IsNullOrEmpty(variant.local_key))
            {
                var embeddedSprite = LoadEmbeddedSprite(variant.local_key);
                if (embeddedSprite != null)
                {
                    Debug.Log($"[BoostOps] Loaded embedded sprite: {variant.local_key}");
                    return embeddedSprite;
                }
            }

            // 2. If offline mode, don't try cache or remote
            if (_assetLoadMode == AssetLoadMode.Offline)
            {
                Debug.LogWarning($"[BoostOps] Asset not found in embedded assets and offline mode is enabled: {variant.local_key}");
                return null;
            }

            // 3. Try cache
            var cacheKey = GetCacheKey(variant.url);
            var cachedSprite = LoadFromCache(cacheKey);
            if (cachedSprite != null)
            {
                Debug.Log($"[BoostOps] Loaded sprite from cache: {cacheKey}");
                return cachedSprite;
            }

            // 4. Download from URL
            if (!string.IsNullOrEmpty(variant.url))
            {
                var downloadedSprite = await DownloadSpriteAsync(variant.url, cacheKey);
                if (downloadedSprite != null)
                {
                    Debug.Log($"[BoostOps] Downloaded and cached sprite: {variant.url}");
                    return downloadedSprite;
                }
            }

            Debug.LogWarning($"[BoostOps] Failed to load sprite from all sources: {variant.local_key ?? variant.url}");
            return null;
        }

        /// <summary>
        /// Load a texture from embedded assets, cache, or download from URL
        /// </summary>
        /// <param name="variant">Creative variant with asset information</param>
        /// <returns>Loaded texture or null if failed</returns>
        public static async Task<Texture2D> LoadTextureAsync(CreativeVariant variant)
        {
            if (variant == null)
            {
                Debug.LogWarning("[BoostOps] Cannot load texture: variant is null");
                return null;
            }

            // 1. Try embedded assets first
            if (!string.IsNullOrEmpty(variant.local_key))
            {
                var embeddedTexture = LoadEmbeddedTexture(variant.local_key);
                if (embeddedTexture != null)
                {
                    Debug.Log($"[BoostOps] Loaded embedded texture: {variant.local_key}");
                    return embeddedTexture;
                }
            }

            // 2. If offline mode, don't try cache or remote
            if (_assetLoadMode == AssetLoadMode.Offline)
            {
                Debug.LogWarning($"[BoostOps] Asset not found in embedded assets and offline mode is enabled: {variant.local_key}");
                return null;
            }

            // 3. Try cache
            var cacheKey = GetCacheKey(variant.url);
            var cachedTexture = LoadTextureFromCache(cacheKey);
            if (cachedTexture != null)
            {
                Debug.Log($"[BoostOps] Loaded texture from cache: {cacheKey}");
                return cachedTexture;
            }

            // 4. Download from URL
            if (!string.IsNullOrEmpty(variant.url))
            {
                var downloadedTexture = await DownloadTextureAsync(variant.url, cacheKey);
                if (downloadedTexture != null)
                {
                    Debug.Log($"[BoostOps] Downloaded and cached texture: {variant.url}");
                    return downloadedTexture;
                }
            }

            Debug.LogWarning($"[BoostOps] Failed to load texture from all sources: {variant.local_key ?? variant.url}");
            return null;
        }

        /// <summary>
        /// Preload assets for multiple creatives
        /// </summary>
        /// <param name="creatives">Array of creatives to preload</param>
        /// <returns>Task representing the preload operation</returns>
        public static async Task PreloadAssetsAsync(Creative[] creatives)
        {
            if (creatives == null || creatives.Length == 0)
                return;

            var preloadTasks = new List<Task>();

            foreach (var creative in creatives)
            {
                if (!creative.prefetch || creative.variants == null)
                    continue;

                foreach (var variant in creative.variants)
                {
                    switch (creative.Format)
                    {
                        case CreativeFormat.Icon:
                        case CreativeFormat.Banner:
                        case CreativeFormat.Native:
                            preloadTasks.Add(LoadSpriteAsync(variant));
                            break;

                        case CreativeFormat.Hero:
                            preloadTasks.Add(LoadTextureAsync(variant));
                            break;

                        default:
                            Debug.LogWarning($"[BoostOps] Unknown creative format: {creative.Format.ToString()}");
                            break;
                    }
                }
            }

            if (preloadTasks.Count > 0)
            {
                Debug.Log($"[BoostOps] Preloading {preloadTasks.Count} assets...");
                await Task.WhenAll(preloadTasks);
                Debug.Log("[BoostOps] Asset preloading complete");
            }
        }

        #region Embedded Assets

        /// <summary>
        /// Load sprite from embedded assets (BoostOps folder)
        /// </summary>
        /// <param name="localKey">Local key to identify the asset</param>
        /// <returns>Loaded sprite or null if not found</returns>
        private static Sprite LoadEmbeddedSprite(string localKey)
        {
            try
            {
                // Handle different asset types based on local_key path
                if (localKey.StartsWith("BoostOps/Downloads/DemoAssets/"))
                {
                    // Demo assets - only available in editor
#if UNITY_EDITOR
                    string assetPath = $"Assets/{localKey}.png";
                    var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                    // Try as texture and convert
                    var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture != null)
                    {
                        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    }
#endif
                    Debug.LogWarning($"[BoostOps] Demo asset not available at runtime: {localKey}");
                    return null;
                }
                else if (localKey.StartsWith("BoostOps/"))
                {
                    // Legacy full path - load from Resources as-is
                    var sprite = Resources.Load<Sprite>(localKey);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                    // Try as texture and convert
                    var texture = Resources.Load<Texture2D>(localKey);
                    if (texture != null)
                    {
                        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    }
                }
                else if (localKey.StartsWith("BoostOps/"))
                {
                    // Legacy BoostOps prefix - load from Resources as-is
                    var sprite = Resources.Load<Sprite>(localKey);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
                else
                {
                    // Modern resource-based format: prepend "BoostOps/" to create full path
                    string fullResourcePath = $"BoostOps/{localKey}";
                    var sprite = Resources.Load<Sprite>(fullResourcePath);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                    // Try as texture and convert
                    var texture = Resources.Load<Texture2D>(fullResourcePath);
                    if (texture != null)
                    {
                        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    }
                }

                // Try Addressables if available
#if USE_ADDRESSABLES
                try
                {
                    var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(localKey);
                    return handle.WaitForCompletion();
                }
                catch
                {
                    // Addressables not available or asset not found
                }
#endif

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to load embedded sprite '{localKey}': {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load texture from embedded assets
        /// </summary>
        /// <param name="localKey">Local key to identify the asset</param>
        /// <returns>Loaded texture or null if not found</returns>
        private static Texture2D LoadEmbeddedTexture(string localKey)
        {
            try
            {
                // Handle different asset types based on local_key path
                if (localKey.StartsWith("BoostOps/Downloads/DemoAssets/"))
                {
                    // Demo assets - only available in editor
#if UNITY_EDITOR
                    string assetPath = $"Assets/{localKey}.png";
                    var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture != null)
                    {
                        return texture;
                    }
#endif
                    Debug.LogWarning($"[BoostOps] Demo asset not available at runtime: {localKey}");
                    return null;
                }
                else if (localKey.StartsWith("BoostOps/"))
                {
                    // Legacy full path - load from Resources as-is
                    var texture = Resources.Load<Texture2D>(localKey);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
                else if (localKey.StartsWith("BoostOps/"))
                {
                    // Legacy BoostOps prefix - load from Resources as-is
                    var texture = Resources.Load<Texture2D>(localKey);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
                else
                {
                    // Modern resource-based format: prepend "BoostOps/" to create full path
                    string fullResourcePath = $"BoostOps/{localKey}";
                    var texture = Resources.Load<Texture2D>(fullResourcePath);
                    if (texture != null)
                    {
                        return texture;
                    }
                }

                // Try Addressables if available
#if USE_ADDRESSABLES
                try
                {
                    var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Texture2D>(localKey);
                    return handle.WaitForCompletion();
                }
                catch
                {
                    // Addressables not available or asset not found
                }
#endif

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BoostOps] Failed to load embedded texture '{localKey}': {e.Message}");
                return null;
            }
        }

        #endregion

        #region Cache Management

        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        private static string GetCacheKey(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";

            // Simple hash-based cache key
            return $"boostops_asset_{url.GetHashCode():X8}";
        }

        private static string GetCachePath(string cacheKey)
        {
            var cacheDir = Path.Combine(Application.persistentDataPath, "BoostOpsCache");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            return Path.Combine(cacheDir, cacheKey);
        }

        private static Sprite LoadFromCache(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey))
                return null;

            // Check memory cache first
            if (_spriteCache.TryGetValue(cacheKey, out var cachedSprite))
            {
                return cachedSprite;
            }

            // Check disk cache
            var cachePath = GetCachePath(cacheKey);
            if (File.Exists(cachePath))
            {
                try
                {
                    var data = File.ReadAllBytes(cachePath);
                    var texture = new Texture2D(2, 2);
                    if (texture.LoadImage(data))
                    {
                        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
                        _spriteCache[cacheKey] = sprite;
                        return sprite;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BoostOps] Failed to load cached sprite '{cacheKey}': {e.Message}");
                }
            }

            return null;
        }

        private static Texture2D LoadTextureFromCache(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey))
                return null;

            // Check memory cache first
            if (_textureCache.TryGetValue(cacheKey, out var cachedTexture))
            {
                return cachedTexture;
            }

            // Check disk cache
            var cachePath = GetCachePath(cacheKey);
            if (File.Exists(cachePath))
            {
                try
                {
                    var data = File.ReadAllBytes(cachePath);
                    var texture = new Texture2D(2, 2);
                    if (texture.LoadImage(data))
                    {
                        _textureCache[cacheKey] = texture;
                        return texture;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BoostOps] Failed to load cached texture '{cacheKey}': {e.Message}");
                }
            }

            return null;
        }

        #endregion

        #region Download

        private static async Task<Sprite> DownloadSpriteAsync(string url, string cacheKey)
        {
            var texture = await DownloadTextureAsync(url, cacheKey);
            if (texture != null)
            {
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
                _spriteCache[cacheKey] = sprite;
                return sprite;
            }
            return null;
        }

        private static async Task<Texture2D> DownloadTextureAsync(string url, string cacheKey)
        {
            try
            {
                using (var request = UnityWebRequestTexture.GetTexture(url))
                {
                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var texture = DownloadHandlerTexture.GetContent(request);
                        _textureCache[cacheKey] = texture;

                        // Save to disk cache
                        if (!string.IsNullOrEmpty(cacheKey))
                        {
                            try
                            {
                                var cachePath = GetCachePath(cacheKey);
                                var data = texture.EncodeToPNG();
                                File.WriteAllBytes(cachePath, data);
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning($"[BoostOps] Failed to cache texture '{cacheKey}': {e.Message}");
                            }
                        }

                        return texture;
                    }
                    else
                    {
                        Debug.LogWarning($"[BoostOps] Failed to download texture from {url}: {request.error}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BoostOps] Exception downloading texture from {url}: {e}");
            }

            return null;
        }

        #endregion
    }

    #region UnityWebRequest Extensions

    /// <summary>
    /// Extension methods for UnityWebRequest to support async/await
    /// </summary>
    public static class UnityWebRequestExtensions
    {
        public static Task SendWebRequest(this UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            
            operation.completed += _ =>
            {
                tcs.SetResult(true);
            };
            
            return tcs.Task;
        }
    }

    #endregion
} 