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
            var cacheKey = GetCacheKey(variant);
            var cachedSprite = LoadFromCache(cacheKey);
            if (cachedSprite != null)
            {
                Debug.Log($"[BoostOps] Loaded sprite from cache: {cacheKey}");
                return cachedSprite;
            }

            // 4. Download from URL
            if (!string.IsNullOrEmpty(variant.url))
            {
                var downloadedSprite = await DownloadSpriteOnceAsync(variant.url, cacheKey);
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
            var cacheKey = GetCacheKey(variant);
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

        /// <summary>
        /// The variant to display for a creative: the smallest hosted variant at least
        /// <paramref name="minPixels"/> wide, else the largest one. Every variant shares the
        /// bundled local_key, so this only decides which URL is downloaded.
        /// </summary>
        public static CreativeVariant PickDisplayVariant(Creative creative, int minPixels = 512)
        {
            var variants = creative?.variants;
            if (variants == null || variants.Length == 0) return null;

            var hosted = variants.Where(v => v != null && !string.IsNullOrEmpty(v.url))
                .OrderBy(v => v.GetResolution().width)
                .ToList();
            if (hosted.Count == 0) return variants[0];

            return hosted.FirstOrDefault(v => v.GetResolution().width >= minPixels) ?? hosted[hosted.Count - 1];
        }

        /// <summary>
        /// A sprite that is available right now (bundled, in memory, or in the disk cache), without
        /// touching the network. Null when it still has to be downloaded.
        /// </summary>
        public static Sprite TryGetLoadedSprite(CreativeVariant variant)
        {
            if (variant == null) return null;

            if (!string.IsNullOrEmpty(variant.local_key))
            {
                var embeddedSprite = LoadEmbeddedSprite(variant.local_key);
                if (embeddedSprite != null) return embeddedSprite;
            }

            if (_assetLoadMode == AssetLoadMode.Offline) return null;
            return LoadFromCache(GetCacheKey(variant));
        }

        /// <summary>
        /// True when the campaign can be shown with its real icon. Campaigns whose icon creative is
        /// marked `required` (partner deals) are held back until the icon is bundled or cached, so a
        /// placeholder is never shown for another studio's game; the download starts here.
        /// </summary>
        public static bool IsRequiredIconReady(Campaign campaign)
        {
            var creative = campaign?.target_project?.FindCreative(CreativeFormat.Icon);
            if (creative == null || !creative.required) return true;

            var variant = PickDisplayVariant(creative);
            if (TryGetLoadedSprite(variant) != null) return true;

            if (_assetLoadMode == AssetLoadMode.Online && !string.IsNullOrEmpty(variant?.url))
                _ = LoadSpriteAsync(variant);
            return false;
        }

        /// <summary>
        /// Download the display icon of every campaign marked `prefetch` that isn't bundled or cached
        /// yet, so the first interstitial or native promo shows the real icon. Runs on the main thread
        /// (UnityWebRequest requires it); safe to call from any thread.
        /// </summary>
        public static void PrefetchCampaignIcons(IEnumerable<Campaign> campaigns)
        {
            if (campaigns == null || _assetLoadMode == AssetLoadMode.Offline) return;

            var variants = campaigns
                .Select(c => c?.target_project?.FindCreative(CreativeFormat.Icon))
                .Where(c => c != null && c.prefetch)
                .Select(c => PickDisplayVariant(c))
                .Where(v => v != null && !string.IsNullOrEmpty(v.url))
                .ToList();
            if (variants.Count == 0) return;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                foreach (var variant in variants)
                {
                    if (TryGetLoadedSprite(variant) == null)
                        _ = LoadSpriteAsync(variant);
                }
            });
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
        
        // Insertion order for eviction. Without a bound, every downloaded
        // creative stays in memory for the app's lifetime (textures are the
        // dominant cost — a few MB each on retina-scale creatives).
        private static readonly List<string> _spriteCacheOrder = new List<string>();
        private static readonly List<string> _textureCacheOrder = new List<string>();
        private const int MAX_CACHED_ASSETS = 50;
        
        private static void CacheSprite(string cacheKey, Sprite sprite)
        {
            if (string.IsNullOrEmpty(cacheKey) || sprite == null) return;
            
            if (!_spriteCache.ContainsKey(cacheKey))
                _spriteCacheOrder.Add(cacheKey);
            _spriteCache[cacheKey] = sprite;
            
            while (_spriteCacheOrder.Count > MAX_CACHED_ASSETS)
            {
                var oldestKey = _spriteCacheOrder[0];
                _spriteCacheOrder.RemoveAt(0);
                if (_spriteCache.TryGetValue(oldestKey, out var evicted))
                {
                    _spriteCache.Remove(oldestKey);
                    DestroySprite(evicted);
                }
            }
        }
        
        private static void CacheTexture(string cacheKey, Texture2D texture)
        {
            if (string.IsNullOrEmpty(cacheKey) || texture == null) return;
            
            if (!_textureCache.ContainsKey(cacheKey))
                _textureCacheOrder.Add(cacheKey);
            _textureCache[cacheKey] = texture;
            
            while (_textureCacheOrder.Count > MAX_CACHED_ASSETS)
            {
                var oldestKey = _textureCacheOrder[0];
                _textureCacheOrder.RemoveAt(0);
                if (_textureCache.TryGetValue(oldestKey, out var evicted))
                {
                    _textureCache.Remove(oldestKey);
                    if (evicted != null)
                        UnityEngine.Object.Destroy(evicted);
                }
            }
        }
        
        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            // Don't destroy the backing texture if the texture cache still
            // references it (sprite + texture caches can share textures).
            var texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (texture != null && !_textureCache.ContainsValue(texture))
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
        
        /// <summary>
        /// Release all in-memory cached sprites/textures (disk cache is kept).
        /// </summary>
        public static void ClearMemoryCache()
        {
            foreach (var sprite in _spriteCache.Values.ToList())
            {
                DestroySprite(sprite);
            }
            _spriteCache.Clear();
            _spriteCacheOrder.Clear();
            
            foreach (var texture in _textureCache.Values)
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }
            _textureCache.Clear();
            _textureCacheOrder.Clear();
        }

        // Hosted creatives are content-addressed by sha256, so the key never goes stale. Store-icon
        // fallbacks have no sha256 and are keyed by URL. string.GetHashCode isn't guaranteed stable
        // across runs, which would orphan the disk cache, so hash with MD5 instead.
        private static string GetCacheKey(CreativeVariant variant)
        {
            if (variant == null) return "";
            var sha = variant.sha256;
            if (!string.IsNullOrEmpty(sha) && sha.Length == 64 && sha.All(Uri.IsHexDigit))
                return $"boostops_asset_{sha.ToLowerInvariant()}";
            return GetCacheKey(variant.url);
        }

        private static string GetCacheKey(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
                return $"boostops_asset_{BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}";
            }
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

            // Check memory cache first (entry may hold a destroyed object
            // after eviction/scene teardown — treat it as a miss)
            if (_spriteCache.TryGetValue(cacheKey, out var cachedSprite))
            {
                if (cachedSprite != null)
                    return cachedSprite;
                _spriteCache.Remove(cacheKey);
                _spriteCacheOrder.Remove(cacheKey);
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
                        CacheSprite(cacheKey, sprite);
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

            // Check memory cache first (entry may hold a destroyed object
            // after eviction/scene teardown — treat it as a miss)
            if (_textureCache.TryGetValue(cacheKey, out var cachedTexture))
            {
                if (cachedTexture != null)
                    return cachedTexture;
                _textureCache.Remove(cacheKey);
                _textureCacheOrder.Remove(cacheKey);
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
                        CacheTexture(cacheKey, texture);
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

        private static readonly Dictionary<string, Task<Sprite>> _inFlightSprites = new Dictionary<string, Task<Sprite>>();

        // A prefetch, an eligibility check and a display can all ask for the same icon at once.
        private static async Task<Sprite> DownloadSpriteOnceAsync(string url, string cacheKey)
        {
            var key = string.IsNullOrEmpty(cacheKey) ? url : cacheKey;
            if (_inFlightSprites.TryGetValue(key, out var pending))
                return await pending;

            var task = DownloadSpriteAsync(url, cacheKey);
            _inFlightSprites[key] = task;
            try
            {
                return await task;
            }
            finally
            {
                _inFlightSprites.Remove(key);
            }
        }

        private static async Task<Sprite> DownloadSpriteAsync(string url, string cacheKey)
        {
            var texture = await DownloadTextureAsync(url, cacheKey);
            if (texture != null)
            {
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
                CacheSprite(cacheKey, sprite);
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
                        CacheTexture(cacheKey, texture);

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