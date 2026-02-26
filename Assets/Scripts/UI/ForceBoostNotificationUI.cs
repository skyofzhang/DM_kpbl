using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 推力提升通知UI - 显示666和点赞的推力加成弹窗
    /// 分阵营从左/右两侧滑入，三层底图（亮色边框+深色内底+顶部高光）
    /// 带阵营emoji标识 + 文字描边投影 + 柔和渐变边缘
    /// 666: "🍊 玩家名 发送666 推力+XX"  从左/右侧滑入
    /// 点赞: "🍊 玩家名 点赞×N 推力+XX"  从左/右侧滑入
    /// 挂载在Canvas上
    /// </summary>
    public class ForceBoostNotificationUI : MonoBehaviour
    {
        [Header("Config")]
        public float displayDuration = 1.2f;   // 缩短显示时长，减少遮挡
        public int maxVisible = 3;
        public float slideSpeed = 0.25f;        // 滑入动画时长

        private Queue<BoostInfo> _pendingQueue = new Queue<BoostInfo>();
        private List<GameObject> _activeNotifs = new List<GameObject>();
        private Queue<GameObject> _pool = new Queue<GameObject>();
        private TMP_FontAsset _chineseFont;
        private bool _isProcessing = false;
        private GameManager _gm;

        // === 阵营色系（三层底图：边框/底色/文字） ===
        // 左阵营（橙色系）
        private static readonly Color COL_LEFT_BORDER  = new Color(1f, 0.65f, 0.2f, 0.92f);    // 亮橙边框
        private static readonly Color COL_LEFT_BG      = new Color(0.7f, 0.28f, 0f, 0.88f);     // 深橙底色
        private static readonly Color COL_LEFT_GLOW    = new Color(1f, 0.9f, 0.6f, 0.15f);      // 暖光高光
        private static readonly Color COL_LEFT_TEXT    = new Color(1f, 0.95f, 0.85f);            // 暖白文字
        // 右阵营（绿色系）
        private static readonly Color COL_RIGHT_BORDER = new Color(0.5f, 0.92f, 0.3f, 0.92f);   // 亮绿边框
        private static readonly Color COL_RIGHT_BG     = new Color(0.15f, 0.5f, 0.05f, 0.88f);  // 深绿底色
        private static readonly Color COL_RIGHT_GLOW   = new Color(0.7f, 1f, 0.6f, 0.15f);      // 冷光高光
        private static readonly Color COL_RIGHT_TEXT   = new Color(0.9f, 1f, 0.88f);             // 冷白文字
        // 通用
        private static readonly Color COL_FORCE        = new Color(1f, 0.92f, 0.35f);            // 金黄推力值

        private struct BoostInfo
        {
            public string playerName;
            public string camp;
            public string action;     // "666" 或 "点赞×N"
            public float forceValue;
            public float duration;
        }

        private void Start()
        {
            // 通知层级：最低层，不遮挡入场视频(40)和礼物(30)
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
            _gm = GameManager.Instance;
            if (_gm != null)
            {
                _gm.OnForceBoost += HandleForceBoost;
                _gm.OnLikeBoost += HandleLikeBoost;
            }
        }

        private void OnDisable()
        {
            // 状态切换时清空队列，防止结算/主菜单还在播放通知
            ClearAll();
        }

        private void OnDestroy()
        {
            if (_gm != null)
            {
                _gm.OnForceBoost -= HandleForceBoost;
                _gm.OnLikeBoost -= HandleLikeBoost;
            }
        }

        /// <summary>立即清空所有通知和队列</summary>
        public void ClearAll()
        {
            _pendingQueue.Clear();
            _isProcessing = false;
            StopAllCoroutines();
            foreach (var go in _activeNotifs)
                if (go != null) Destroy(go);
            _activeNotifs.Clear();
            foreach (var go in _pool)
                if (go != null) Destroy(go);
            _pool.Clear();
        }

        private void HandleForceBoost(ForceBoostData data)
        {
            _pendingQueue.Enqueue(new BoostInfo
            {
                playerName = data.playerName,
                camp = data.camp,
                action = "666",
                forceValue = data.forceValue,
                duration = data.duration
            });
            if (!_isProcessing) StartCoroutine(ProcessQueue());
        }

        private void HandleLikeBoost(LikeBoostData data)
        {
            _pendingQueue.Enqueue(new BoostInfo
            {
                playerName = data.playerName,
                camp = data.camp,
                action = data.likeNum > 1 ? $"\u70b9\u8d5e\u00d7{data.likeNum}" : "\u70b9\u8d5e",
                forceValue = data.forceValue,
                duration = data.duration
            });
            if (!_isProcessing) StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _isProcessing = true;
            while (_pendingQueue.Count > 0)
            {
                // v116: 非Running状态立即清空退出
                if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Running)
                {
                    ClearAll();
                    yield break;
                }
                while (_activeNotifs.Count >= maxVisible && _activeNotifs.Count > 0)
                {
                    var oldest = _activeNotifs[0];
                    _activeNotifs.RemoveAt(0);
                    ReturnToPool(oldest);
                }

                var info = _pendingQueue.Dequeue();
                yield return ShowNotification(info);
                yield return new WaitForSeconds(0.12f);
            }
            _isProcessing = false;
        }

        private IEnumerator ShowNotification(BoostInfo info)
        {
            var go = GetFromPool();
            var rt = go.GetComponent<RectTransform>();

            // 清理旧子对象
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Destroy(go.transform.GetChild(i).gameObject);

            bool isLeft = info.camp == "left";

            // 阵营色选择
            Color borderColor = isLeft ? COL_LEFT_BORDER : COL_RIGHT_BORDER;
            Color bgColor     = isLeft ? COL_LEFT_BG : COL_RIGHT_BG;
            Color glowColor   = isLeft ? COL_LEFT_GLOW : COL_RIGHT_GLOW;
            Color textColor   = isLeft ? COL_LEFT_TEXT : COL_RIGHT_TEXT;

            // 阵营符号（TMP兼容，不用emoji，用彩色Unicode符号）
            string cBorder = ColorUtility.ToHtmlStringRGB(borderColor);
            string campSymbol = isLeft ? $"<color=#{cBorder}>\u25c6</color>" : $"<color=#{cBorder}>\u25c6</color>";  // ◆
            string actionSymbol = info.action.Contains("666") ? "<color=#FF4400>\u2605</color>" : "<color=#FFD700>\u2665</color>"; // ★ / ♥

            // 构建显示文本
            string truncName = TruncateName(info.playerName, 6);
            string forceText = info.forceValue >= 1000
                ? $"{info.forceValue / 1000f:F1}K"
                : $"{info.forceValue:F0}";

            // 估算宽度
            float nameWidth = truncName.Length * 24f;
            float estimatedWidth = Mathf.Clamp(nameWidth + 300f, 360f, 620f);
            rt.sizeDelta = new Vector2(estimatedWidth, 50);

            // ==================== 三层底图 ====================

            // === Layer 1: 外层边框（亮色） ===
            var bgImg = go.GetComponent<Image>();
            if (bgImg == null) bgImg = go.AddComponent<Image>();
            bgImg.enabled = true;
            bgImg.color = borderColor;
            bgImg.raycastTarget = false;

            // === Layer 2: 内层底色（比外框小2px，形成边框效果） ===
            var innerBgGo = new GameObject("InnerBg", typeof(RectTransform));
            innerBgGo.transform.SetParent(go.transform, false);
            var innerBgRT = innerBgGo.GetComponent<RectTransform>();
            innerBgRT.anchorMin = Vector2.zero;
            innerBgRT.anchorMax = Vector2.one;
            innerBgRT.offsetMin = new Vector2(2, 2);
            innerBgRT.offsetMax = new Vector2(-2, -2);
            var innerBgImg = innerBgGo.AddComponent<Image>();
            innerBgImg.color = bgColor;
            innerBgImg.raycastTarget = false;

            // === Layer 3: 顶部内发光高光线 ===
            var topGlow = new GameObject("TopGlow", typeof(RectTransform));
            topGlow.transform.SetParent(innerBgGo.transform, false);
            var topGlowRT = topGlow.GetComponent<RectTransform>();
            topGlowRT.anchorMin = new Vector2(0.04f, 0.8f);
            topGlowRT.anchorMax = new Vector2(0.96f, 1f);
            topGlowRT.offsetMin = Vector2.zero;
            topGlowRT.offsetMax = Vector2.zero;
            var topGlowImg = topGlow.AddComponent<Image>();
            topGlowImg.color = glowColor;
            topGlowImg.raycastTarget = false;

            // === 左侧渐变边缘（柔化消融） ===
            var leftFade = new GameObject("LeftFade", typeof(RectTransform));
            leftFade.transform.SetParent(go.transform, false);
            var leftFadeRT = leftFade.GetComponent<RectTransform>();
            leftFadeRT.anchorMin = new Vector2(0, 0);
            leftFadeRT.anchorMax = new Vector2(0, 1);
            leftFadeRT.pivot = new Vector2(1, 0.5f);
            leftFadeRT.anchoredPosition = Vector2.zero;
            leftFadeRT.sizeDelta = new Vector2(30, 0);
            var leftFadeImg = leftFade.AddComponent<Image>();
            leftFadeImg.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0.25f);
            leftFadeImg.raycastTarget = false;

            // === 右侧渐变边缘 ===
            var rightFade = new GameObject("RightFade", typeof(RectTransform));
            rightFade.transform.SetParent(go.transform, false);
            var rightFadeRT = rightFade.GetComponent<RectTransform>();
            rightFadeRT.anchorMin = new Vector2(1, 0);
            rightFadeRT.anchorMax = new Vector2(1, 1);
            rightFadeRT.pivot = new Vector2(0, 0.5f);
            rightFadeRT.anchoredPosition = Vector2.zero;
            rightFadeRT.sizeDelta = new Vector2(30, 0);
            var rightFadeImg = rightFade.AddComponent<Image>();
            rightFadeImg.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0.25f);
            rightFadeImg.raycastTarget = false;

            // ==================== 文字内容 ====================

            // 文字容器（水平布局）
            var textContainer = new GameObject("TextRow", typeof(RectTransform));
            textContainer.transform.SetParent(go.transform, false);
            var tcRT = textContainer.GetComponent<RectTransform>();
            tcRT.anchorMin = Vector2.zero;
            tcRT.anchorMax = Vector2.one;
            tcRT.offsetMin = new Vector2(12, 2);
            tcRT.offsetMax = new Vector2(-12, -2);

            var hlg = textContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 4;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            // --- emoji图标 ---
            var emojiGo = new GameObject("Emoji", typeof(RectTransform));
            emojiGo.transform.SetParent(textContainer.transform, false);
            var emojiTMP = emojiGo.AddComponent<TextMeshProUGUI>();
            emojiTMP.text = $"{campSymbol}{actionSymbol}";
            emojiTMP.richText = true;
            emojiTMP.fontSize = 22;
            emojiTMP.alignment = TextAlignmentOptions.Center;
            emojiTMP.enableWordWrapping = false;
            emojiTMP.raycastTarget = false;
            if (_chineseFont != null) emojiTMP.font = _chineseFont;
            var emojiLE = emojiGo.AddComponent<LayoutElement>();
            emojiLE.preferredWidth = 56;
            emojiLE.flexibleWidth = 0;

            // --- 玩家名（大号粗体白色） ---
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(textContainer.transform, false);
            var nameTMP = nameGo.AddComponent<TextMeshProUGUI>();
            nameTMP.text = truncName;
            nameTMP.fontSize = 22;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color = Color.white;
            nameTMP.alignment = TextAlignmentOptions.MidlineRight;
            nameTMP.enableWordWrapping = false;
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;
            nameTMP.raycastTarget = false;
            if (_chineseFont != null) nameTMP.font = _chineseFont;
            ApplyUnderlay(nameTMP);
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.preferredWidth = Mathf.Max(truncName.Length * 22f, 60f);
            nameLE.flexibleWidth = 1;

            // --- 动作+推力（阵营色+金色，紧凑一行） ---
            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(textContainer.transform, false);
            var infoTMP = infoGo.AddComponent<TextMeshProUGUI>();
            string cCamp = ColorUtility.ToHtmlStringRGB(textColor);
            string cForce = ColorUtility.ToHtmlStringRGB(COL_FORCE);
            infoTMP.text = $"<color=#{cCamp}>{info.action}</color> <color=#{cForce}>+{forceText}</color>";
            infoTMP.fontSize = 20;
            infoTMP.fontStyle = FontStyles.Bold;
            infoTMP.color = textColor;
            infoTMP.alignment = TextAlignmentOptions.MidlineLeft;
            infoTMP.enableWordWrapping = false;
            infoTMP.richText = true;
            infoTMP.raycastTarget = false;
            if (_chineseFont != null) infoTMP.font = _chineseFont;
            ApplyUnderlayLight(infoTMP);
            var infoLE = infoGo.AddComponent<LayoutElement>();
            infoLE.preferredWidth = 160;
            infoLE.flexibleWidth = 0;

            // ==================== 滑入动画（分阵营方向） ====================

            float yPos = -280f - _activeNotifs.Count * 56f;
            float startX = isLeft ? -650f : 650f;
            float endX   = isLeft ? -160f : 160f;

            rt.anchoredPosition = new Vector2(startX, yPos);
            go.SetActive(true);
            _activeNotifs.Add(go);

            // 滑入
            yield return SlideAnimation(rt, startX, endX, yPos, slideSpeed);

            // 停留（缩短后1.2秒总时长中扣除滑入滑出）
            float stayTime = Mathf.Max(displayDuration - slideSpeed * 2f, 0.3f);
            yield return FloatUpAndFade(go, rt, endX, yPos, stayTime);

            // 滑出
            float exitX = isLeft ? -650f : 650f;
            yield return SlideAnimation(rt, endX, exitX, yPos, slideSpeed * 0.6f);

            _activeNotifs.Remove(go);
            ReturnToPool(go);
            RearrangeActive();
        }

        // ==================== 动画辅助 ====================

        private IEnumerator SlideAnimation(RectTransform rt, float fromX, float toX, float y, float duration)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                if (rt != null)
                    rt.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, t), y);
                yield return null;
            }
            if (rt != null)
                rt.anchoredPosition = new Vector2(toX, y);
        }

        /// <summary>停留期间轻微上飘+尾段淡出</summary>
        private IEnumerator FloatUpAndFade(GameObject go, RectTransform rt, float baseX, float baseY, float duration)
        {
            float elapsed = 0;
            float fadeStart = duration * 0.5f;
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (rt == null) yield break;

                // 轻微上飘（比原来慢很多，仅微动）
                float yOffset = elapsed * 18f;
                rt.anchoredPosition = new Vector2(baseX, baseY + yOffset);

                // 后50%淡出
                if (elapsed > fadeStart)
                {
                    float fadeProgress = (elapsed - fadeStart) / (duration - fadeStart);
                    cg.alpha = 1f - fadeProgress;
                }

                yield return null;
            }
        }

        private void RearrangeActive()
        {
            for (int i = 0; i < _activeNotifs.Count; i++)
            {
                var rt = _activeNotifs[i].GetComponent<RectTransform>();
                if (rt)
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -280f - i * 56f);
            }
        }

        // ==================== 文字描边投影 ====================

        private void ApplyUnderlay(TextMeshProUGUI tmp)
        {
            tmp.ForceMeshUpdate();
            var mat = tmp.fontMaterial;
            if (mat == null) return;
            tmp.outlineWidth = 0.3f;
            tmp.outlineColor = new Color32(0, 0, 0, 200);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.65f));
            mat.SetFloat("_UnderlayOffsetX", 0.8f);
            mat.SetFloat("_UnderlayOffsetY", -0.8f);
            mat.SetFloat("_UnderlayDilate", 0.2f);
            mat.SetFloat("_UnderlaySoftness", 0.3f);
        }

        private void ApplyUnderlayLight(TextMeshProUGUI tmp)
        {
            tmp.ForceMeshUpdate();
            var mat = tmp.fontMaterial;
            if (mat == null) return;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(0, 0, 0, 160);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.5f));
            mat.SetFloat("_UnderlayOffsetX", 0.6f);
            mat.SetFloat("_UnderlayOffsetY", -0.6f);
            mat.SetFloat("_UnderlayDilate", 0.15f);
            mat.SetFloat("_UnderlaySoftness", 0.3f);
        }

        // ==================== 工具 ====================

        private string TruncateName(string name, int maxLen)
        {
            if (string.IsNullOrEmpty(name)) return "???";
            return name.Length > maxLen ? name.Substring(0, maxLen) + ".." : name;
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                var cg = pooled.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                return pooled;
            }
            var go = new GameObject("BoostNotif", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private void ReturnToPool(GameObject go)
        {
            go.SetActive(false);
            _pool.Enqueue(go);
        }
    }
}
