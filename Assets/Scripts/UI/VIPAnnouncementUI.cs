using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Systems;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// VIP入场公告 + 高Tier礼物全屏公告
    /// - 周榜/月榜前20名玩家加入时播放入场视频
    /// - 第1-3名: 全屏视频(无Alpha)
    /// - 第4-20名: 半屏视频(有Alpha透明，叠加在游戏画面上)
    /// - T5/T6礼物: 文字公告(保留原有逻辑)
    /// </summary>
    public class VIPAnnouncementUI : MonoBehaviour
    {
        [Header("Text Announcement References")]
        public TextMeshProUGUI announcementText;
        public Image backgroundOverlay;
        public CanvasGroup canvasGroup;

        [Header("Video Announcement")]
        [Tooltip("用于播放入场视频的RawImage(场景预创建)")]
        public RawImage videoDisplay;
        [Tooltip("视频容器的CanvasGroup(控制淡入淡出)")]
        public CanvasGroup videoCanvasGroup;

        [Header("VIP Entry Videos - Weekly")]
        [SerializeField] private VideoClip weeklyRank1Clip;
        [SerializeField] private VideoClip weeklyRank2Clip;
        [SerializeField] private VideoClip weeklyRank3Clip;
        [SerializeField] private VideoClip weeklyRank4_10Clip;
        [SerializeField] private VideoClip weeklyRank11_20Clip;

        [Header("VIP Entry Videos - Monthly")]
        [SerializeField] private VideoClip monthlyRank1Clip;
        [SerializeField] private VideoClip monthlyRank2Clip;
        [SerializeField] private VideoClip monthlyRank3Clip;
        [SerializeField] private VideoClip monthlyRank4_10Clip;
        [SerializeField] private VideoClip monthlyRank11_20Clip;

        [Header("Config")]
        public float fadeInDuration = 0.5f;
        public float holdDuration = 3f;
        public float fadeOutDuration = 0.5f;
        public float videoFadeOutDuration = 1f;

        private const int MAX_QUEUE_SIZE = 3;  // 最多排队3个公告，防止大量VIP堆积
        private Queue<System.Action> _pendingAnnouncements = new Queue<System.Action>();
        private bool _isShowing = false;
        private CampSystem _campSystem;
        private GiftHandler _giftHandler;
        private TMP_FontAsset _chineseFont;
        private bool _subscribed = false;

        // Video playback state
        private VideoPlayer _videoPlayer;
        private RenderTexture _renderTexture;
        private bool _isVideoPlaying = false;
        private float _currentVideoMaxDuration = 15f; // 当前视频的播放时长限制

        private static readonly Color COL_GOLD = new Color(1f, 0.84f, 0f);
        private static readonly Color COL_LEFT = new Color(1f, 0.55f, 0f);
        private static readonly Color COL_RIGHT = new Color(0.68f, 1f, 0.18f);

        private void Start()
        {
            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
            TrySubscribe();

            // VIP入场视频层级=40，高于通知(10)和礼物(30)，低于TopBar(50)
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 40;
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 初始隐藏
            if (canvasGroup) canvasGroup.alpha = 0;
            if (backgroundOverlay) backgroundOverlay.enabled = false;
            if (videoCanvasGroup) videoCanvasGroup.alpha = 0;
            if (videoDisplay) videoDisplay.enabled = false;
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            _campSystem = FindObjectOfType<CampSystem>();
            if (_campSystem != null)
                _campSystem.OnVIPJoined += HandleVIPJoined;

            _giftHandler = FindObjectOfType<GiftHandler>();
            if (_giftHandler != null)
                _giftHandler.OnGiftReceived += HandleHighTierGift;

            if (_campSystem != null || _giftHandler != null)
                _subscribed = true;
        }

        private void OnDestroy()
        {
            if (_campSystem != null)
                _campSystem.OnVIPJoined -= HandleVIPJoined;
            if (_giftHandler != null)
                _giftHandler.OnGiftReceived -= HandleHighTierGift;
            CleanupVideo();
        }

        private void HandleVIPJoined(PlayerJoinedData data)
        {
            if (!gameObject.activeInHierarchy) return;
            if (!SettingsPanelUI.VIPVideoEnabled) return;  // 设置面板关闭了入场视频
            if (_pendingAnnouncements.Count >= MAX_QUEUE_SIZE) return;  // 队列满则丢弃

            // 尝试播放入场视频
            float playDuration;
            var clip = GetEntryClip(data.vipType, data.vipRank, out playDuration);
            if (clip != null)
            {
                float dur = playDuration; // 闭包捕获
                _pendingAnnouncements.Enqueue(() => PlayEntryVideo(clip, data, dur));
            }
            else
            {
                // 没有对应视频时回退到文字公告
                _pendingAnnouncements.Enqueue(() => ShowVIPAnnouncement(data));
            }

            if (!_isShowing)
                StartCoroutine(ProcessQueue());
        }

        /// <summary>高Tier礼物(T5/T6)全屏公告 — 已禁用：礼物动画弹窗已包含玩家+礼物信息，此公告文字重复</summary>
        private void HandleHighTierGift(GiftReceivedData gift)
        {
            // 已禁用：打赏礼物会弹出礼物动画（GiftAnimationUI），文字公告重复
            // 保留代码以备将来复用
            /*
            if (!gameObject.activeInHierarchy) return;
            if (_pendingAnnouncements.Count >= MAX_QUEUE_SIZE) return;  // 队列满则丢弃

            int tier = 1;
            if (!string.IsNullOrEmpty(gift.tier))
                int.TryParse(gift.tier, out tier);
            if (tier < 5) return;

            _pendingAnnouncements.Enqueue(() => ShowGiftAnnouncement(gift, tier));
            if (!_isShowing)
                StartCoroutine(ProcessQueue());
            */
        }

        private IEnumerator ProcessQueue()
        {
            _isShowing = true;
            while (_pendingAnnouncements.Count > 0)
            {
                var action = _pendingAnnouncements.Dequeue();
                action?.Invoke();

                // 检查是否在播放视频
                if (_isVideoPlaying)
                {
                    yield return VideoPlaySequence();
                }
                else
                {
                    yield return FadeSequence();
                }

                yield return new WaitForSeconds(0.3f);
            }
            _isShowing = false;
        }

        // ==================== 入场视频播放 ====================

        /// <summary>根据vipType和vipRank选择对应VideoClip，同时返回播放时长限制</summary>
        private VideoClip GetEntryClip(string vipType, int vipRank, out float playDuration)
        {
            playDuration = 15f; // 默认完整播放
            if (vipRank <= 0 || vipRank > 20) return null;
            if (string.IsNullOrEmpty(vipType)) return null;

            bool isMonthly = vipType == "monthly";

            if (vipRank == 1)
            {
                playDuration = isMonthly ? 11f : 14f;
                var clip1 = isMonthly ? monthlyRank1Clip : weeklyRank1Clip;
                #if UNITY_EDITOR
                Debug.Log($"[VIP] GetEntryClip: type={vipType}, rank={vipRank}, clip={clip1?.name ?? "NULL"}, duration={playDuration}s");
                #endif
                return clip1;
            }
            if (vipRank == 2)
            {
                playDuration = isMonthly ? 13f : 13f;
                var clip2 = isMonthly ? monthlyRank2Clip : weeklyRank2Clip;
                #if UNITY_EDITOR
                Debug.Log($"[VIP] GetEntryClip: type={vipType}, rank={vipRank}, clip={clip2?.name ?? "NULL"}, duration={playDuration}s");
                #endif
                return clip2;
            }
            if (vipRank == 3)
            {
                playDuration = isMonthly ? 9f : 11f;
                var clip3 = isMonthly ? monthlyRank3Clip : weeklyRank3Clip;
                #if UNITY_EDITOR
                Debug.Log($"[VIP] GetEntryClip: type={vipType}, rank={vipRank}, clip={clip3?.name ?? "NULL"}, duration={playDuration}s");
                #endif
                return clip3;
            }

            if (vipRank >= 4 && vipRank <= 10)
            {
                playDuration = isMonthly ? 6f : 6f;  // 非Top3缩短2秒 (8→6)
                var clip4 = isMonthly ? monthlyRank4_10Clip : weeklyRank4_10Clip;
                #if UNITY_EDITOR
                Debug.Log($"[VIP] GetEntryClip: type={vipType}, rank={vipRank}, clip={clip4?.name ?? "NULL"}, duration={playDuration}s");
                #endif
                return clip4;
            }
            if (vipRank >= 11 && vipRank <= 20)
            {
                playDuration = isMonthly ? 5f : 7f;  // 非Top3缩短2秒 (7→5, 9→7)
                var clip11 = isMonthly ? monthlyRank11_20Clip : weeklyRank11_20Clip;
                #if UNITY_EDITOR
                Debug.Log($"[VIP] GetEntryClip: type={vipType}, rank={vipRank}, clip={clip11?.name ?? "NULL"}, duration={playDuration}s");
                #endif
                return clip11;
            }

            #if UNITY_EDITOR
            Debug.Log($"[VIP] GetEntryClip: type={vipType}, rank={vipRank} → no clip (out of range)");
            #endif
            return null;
        }

        /// <summary>播放入场视频（maxDuration控制最大播放秒数，到时自动关闭）</summary>
        private void PlayEntryVideo(VideoClip clip, PlayerJoinedData data, float maxDuration = 15f)
        {
            #if UNITY_EDITOR
            Debug.Log($"[VIP] PlayEntryVideo: clip={clip?.name ?? "NULL"}, player={data.playerName}, maxDuration={maxDuration}s");
            #endif
            _currentVideoMaxDuration = maxDuration;
            if (videoDisplay == null)
            {
                // 没有配置视频显示组件，回退到文字
                ShowVIPAnnouncement(data);
                return;
            }

            CleanupVideo();

            // 创建RenderTexture
            int texW = (int)clip.width;
            int texH = (int)clip.height;
            if (texW <= 0) texW = 1080;
            if (texH <= 0) texH = 1920;
            _renderTexture = new RenderTexture(texW, texH, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();

            // 清除为透明
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;

            // 配置RawImage
            videoDisplay.texture = _renderTexture;
            videoDisplay.color = Color.white;
            videoDisplay.enabled = true;

            // 配置VideoPlayer
            if (_videoPlayer == null)
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();

            _videoPlayer.clip = clip;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;
            _videoPlayer.isLooping = false;
            _videoPlayer.playOnAwake = false;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            _videoPlayer.skipOnDrop = true;

            _videoPlayer.Play();
            _isVideoPlaying = true;

            // 同时显示文字公告（叠加在视频上）
            if (announcementText)
            {
                string title = !string.IsNullOrEmpty(data.vipTitle)
                    ? data.vipTitle
                    : $"第{data.vipRank}名";
                announcementText.text = $"<size=32>{title}</size>\n<size=48>{data.playerName}</size>\n<size=36>驾到!</size>";
                announcementText.color = COL_GOLD;
                if (_chineseFont != null) announcementText.font = _chineseFont;
            }
        }

        /// <summary>视频播放序列: 淡入 → 等待视频播完 → 淡出</summary>
        private IEnumerator VideoPlaySequence()
        {
            // 淡入视频
            if (videoCanvasGroup)
                StartCoroutine(FadeGroup(videoCanvasGroup, 0, 1, fadeInDuration));

            // 同时淡入文字
            if (backgroundOverlay) backgroundOverlay.enabled = true;
            yield return FadeCanvasGroup(0, 1, fadeInDuration);

            // 等待视频播完（或达到播放时长限制自动关闭）
            float maxWait = _currentVideoMaxDuration;
            float waited = 0f;
            while (_videoPlayer != null && _videoPlayer.isPlaying && waited < maxWait)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // 淡出
            if (videoCanvasGroup)
                StartCoroutine(FadeGroup(videoCanvasGroup, 1, 0, videoFadeOutDuration));
            yield return FadeCanvasGroup(1, 0, fadeOutDuration);
            if (backgroundOverlay) backgroundOverlay.enabled = false;

            // 清理视频
            CleanupVideo();
        }

        private void CleanupVideo()
        {
            _isVideoPlaying = false;

            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
                _videoPlayer.clip = null;
                _videoPlayer.targetTexture = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (videoDisplay != null)
            {
                videoDisplay.texture = null;
                videoDisplay.enabled = false;
            }

            if (videoCanvasGroup != null)
                videoCanvasGroup.alpha = 0;
        }

        // ==================== 文字公告（原有逻辑） ====================

        /// <summary>淡入->停留->淡出 序列 (文字公告)</summary>
        private IEnumerator FadeSequence()
        {
            if (backgroundOverlay) backgroundOverlay.enabled = true;
            yield return FadeCanvasGroup(0, 1, fadeInDuration);
            yield return new WaitForSeconds(holdDuration);
            yield return FadeCanvasGroup(1, 0, fadeOutDuration);
            if (backgroundOverlay) backgroundOverlay.enabled = false;
        }

        private void ShowVIPAnnouncement(PlayerJoinedData data)
        {
            if (announcementText)
            {
                string title = !string.IsNullOrEmpty(data.vipTitle)
                    ? data.vipTitle
                    : $"周榜第{data.vipRank}名";
                announcementText.text = $"<size=32>{title}</size>\n<size=48>{data.playerName}</size>\n<size=36>驾到!</size>";
                announcementText.color = COL_GOLD;
                if (_chineseFont != null) announcementText.font = _chineseFont;
            }
        }

        /// <summary>高Tier礼物全屏公告</summary>
        private void ShowGiftAnnouncement(GiftReceivedData gift, int tier)
        {
            if (announcementText)
            {
                Color campColor = gift.camp == "left" ? COL_LEFT : COL_RIGHT;
                string tierLabel = tier >= 6 ? "传说" : "史诗";
                string countStr = gift.giftCount > 1 ? $"x{gift.giftCount}" : "";
                announcementText.text = $"<size=36>{tierLabel}礼物!</size>\n<size=48>{gift.playerName}</size>\n<size=40>送出 {gift.giftName}{countStr}</size>";
                announcementText.color = campColor;
                if (_chineseFont != null) announcementText.font = _chineseFont;
            }
        }

        // ==================== 强制清理（游戏结束/重置时调用） ====================

        /// <summary>
        /// 强制停止所有公告/视频播放，立即隐藏。
        /// 由 GameManager 在 game_ended / ResetGame 时调用，
        /// 防止视频在结算界面弹出后仍然卡在屏幕上。
        /// </summary>
        public void ForceCleanup()
        {
            StopAllCoroutines();
            _pendingAnnouncements.Clear();
            CleanupVideo();

            if (canvasGroup) canvasGroup.alpha = 0;
            if (videoCanvasGroup) videoCanvasGroup.alpha = 0;
            if (backgroundOverlay) backgroundOverlay.enabled = false;

            _isShowing = false;
            _isVideoPlaying = false;
        }

        // ==================== 通用工具 ====================

        private IEnumerator FadeCanvasGroup(float from, float to, float duration)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
