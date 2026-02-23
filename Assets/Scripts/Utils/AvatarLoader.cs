using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace CapybaraDuel.Utils
{
    /// <summary>
    /// 头像加载工具 - 从URL异步下载头像图片并缓存
    ///
    /// 使用方式:
    ///   AvatarLoader.Instance.Load(avatarUrl, texture => {
    ///       if (texture != null) myRawImage.texture = texture;
    ///   });
    ///
    /// 特性:
    /// - LRU缓存(最多100个纹理)，相同URL不重复下载
    /// - 下载失败返回null，UI层自行用色块兜底
    /// - 同一URL并发请求会合并（只下载一次，多个回调都会触发）
    /// </summary>
    public class AvatarLoader : MonoBehaviour
    {
        public static AvatarLoader Instance { get; private set; }

        private const int MAX_CACHE = 100;
        private const float TIMEOUT = 10f;

        // URL → 缓存的Texture2D
        private Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        // LRU顺序追踪
        private LinkedList<string> _lruOrder = new LinkedList<string>();
        // 正在下载中的URL → 等待回调列表
        private Dictionary<string, List<Action<Texture2D>>> _pending = new Dictionary<string, List<Action<Texture2D>>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 加载头像纹理（异步）
        /// </summary>
        /// <param name="url">头像URL（抖音推送的avatar_url）</param>
        /// <param name="callback">加载完成回调，成功返回Texture2D，失败返回null</param>
        public void Load(string url, Action<Texture2D> callback)
        {
            if (string.IsNullOrEmpty(url))
            {
                callback?.Invoke(null);
                return;
            }

            // 缓存命中
            if (_cache.TryGetValue(url, out var cached))
            {
                // 更新LRU
                _lruOrder.Remove(url);
                _lruOrder.AddFirst(url);
                callback?.Invoke(cached);
                return;
            }

            // 已在下载中，追加回调
            if (_pending.TryGetValue(url, out var waitList))
            {
                waitList.Add(callback);
                return;
            }

            // 发起新下载
            _pending[url] = new List<Action<Texture2D>> { callback };
            StartCoroutine(DownloadAvatar(url));
        }

        /// <summary>
        /// 将Texture2D转为Sprite（UI Image使用）
        /// </summary>
        public static Sprite TextureToSprite(Texture2D tex)
        {
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private IEnumerator DownloadAvatar(string url)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = (int)TIMEOUT;
                yield return request.SendWebRequest();

                Texture2D result = null;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    result = DownloadHandlerTexture.GetContent(request);
                    // 存入缓存
                    _cache[url] = result;
                    _lruOrder.AddFirst(url);
                    // 淘汰超出限制的缓存
                    while (_cache.Count > MAX_CACHE && _lruOrder.Count > MAX_CACHE)
                    {
                        var oldest = _lruOrder.Last.Value;
                        _lruOrder.RemoveLast();
                        if (_cache.TryGetValue(oldest, out var oldTex))
                        {
                            _cache.Remove(oldest);
                            if (oldTex != null) Destroy(oldTex);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[AvatarLoader] Download failed: {url} - {request.error}");
                }

                // 通知所有等待回调
                if (_pending.TryGetValue(url, out var callbacks))
                {
                    _pending.Remove(url);
                    foreach (var cb in callbacks)
                    {
                        try { cb?.Invoke(result); }
                        catch (Exception e) { Debug.LogError($"[AvatarLoader] Callback error: {e.Message}"); }
                    }
                }
            }
        }

        /// <summary>清理所有缓存</summary>
        public void ClearCache()
        {
            foreach (var tex in _cache.Values)
            {
                if (tex != null) Destroy(tex);
            }
            _cache.Clear();
            _lruOrder.Clear();
        }

        private void OnDestroy()
        {
            ClearCache();
            if (Instance == this) Instance = null;
        }
    }
}
