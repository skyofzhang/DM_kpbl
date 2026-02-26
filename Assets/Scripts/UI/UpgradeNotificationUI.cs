using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 升级通知UI - 玩家基础角色升级时弹出醒目通知
    /// 阵营色底图+渐变半透边缘+文字抖动+LV信息
    /// 左阵营从左侧弹出，右阵营从右侧弹出
    /// </summary>
    public class UpgradeNotificationUI : MonoBehaviour
    {
        [Header("Config")]
        public Transform container;
        public float displayDuration = 3f;
        public float slideSpeed = 0.35f;
        public int maxVisible = 2;
        public float shakeIntensity = 6f;
        public float shakeDuration = 0.4f;

        private Queue<UpgradeData> _pendingQueue = new Queue<UpgradeData>();
        private List<GameObject> _active = new List<GameObject>();
        private Queue<GameObject> _pool = new Queue<GameObject>();
        private TMP_FontAsset _chineseFont;
        private bool _isProcessing = false;

        // 阵营颜色（三层质感：亮色边框 + 深色底色 + 阵营高光）
        private static readonly Color COL_LEFT_BG = new Color(0.65f, 0.22f, 0f, 0.92f);        // 深橙底
        private static readonly Color COL_RIGHT_BG = new Color(0.12f, 0.45f, 0.03f, 0.92f);    // 深绿底
        private static readonly Color COL_LEFT_BORDER = new Color(1f, 0.68f, 0.22f, 0.95f);    // 亮橙边框
        private static readonly Color COL_RIGHT_BORDER = new Color(0.5f, 0.92f, 0.3f, 0.95f);  // 亮绿边框
        private static readonly Color COL_LEFT_GLOW = new Color(1f, 0.85f, 0.5f, 0.18f);       // 暖色高光
        private static readonly Color COL_RIGHT_GLOW = new Color(0.6f, 1f, 0.5f, 0.18f);       // 冷色高光
        private static readonly Color COL_LEFT_TEXT = new Color(1f, 0.95f, 0.8f);               // 暖白
        private static readonly Color COL_RIGHT_TEXT = new Color(0.9f, 1f, 0.85f);              // 冷白
        private static readonly Color COL_LEVEL = new Color(1f, 0.84f, 0f);                     // 金色

        private struct UpgradeData
        {
            public string playerName;
            public string camp;
            public int newLevel;
            public int fairyWandCount;
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
            if (container == null) container = transform;
        }

        private void OnDisable()
        {
            // 状态切换时清空队列
            _pendingQueue.Clear();
            _isProcessing = false;
            StopAllCoroutines();
            foreach (var go in _active)
                if (go != null) Destroy(go);
            _active.Clear();
            foreach (var go in _pool)
                if (go != null) Destroy(go);
            _pool.Clear();
        }

        /// <summary>外部调用：显示升级通知</summary>
        public void ShowUpgrade(string playerName, string camp, int newLevel, int fairyWandCount)
        {
            if (!gameObject.activeInHierarchy) return;

            _pendingQueue.Enqueue(new UpgradeData
            {
                playerName = playerName,
                camp = camp,
                newLevel = newLevel,
                fairyWandCount = fairyWandCount
            });
            if (!_isProcessing)
                StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _isProcessing = true;
            while (_pendingQueue.Count > 0)
            {
                // v116: 非Running状态立即清空退出
                if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Running)
                {
                    _pendingQueue.Clear();
                    _isProcessing = false;
                    StopAllCoroutines();
                    foreach (var go in _active) if (go != null) Destroy(go);
                    _active.Clear();
                    yield break;
                }
                while (_active.Count >= maxVisible && _active.Count > 0)
                {
                    var oldest = _active[0];
                    _active.RemoveAt(0);
                    ReturnToPool(oldest);
                }

                var data = _pendingQueue.Dequeue();
                yield return ShowNotification(data);
                yield return new WaitForSeconds(0.2f);
            }
            _isProcessing = false;
        }

        private IEnumerator ShowNotification(UpgradeData data)
        {
            var go = GetFromPool();
            var rt = go.GetComponent<RectTransform>();

            bool isLeft = data.camp == "left";
            Color bgColor = isLeft ? COL_LEFT_BG : COL_RIGHT_BG;
            Color textColor = isLeft ? COL_LEFT_TEXT : COL_RIGHT_TEXT;
            string campName = isLeft ? "香橙阵营" : "柚子阵营";

            // ====== 构建通知面板 ======
            // 清理旧子对象
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Destroy(go.transform.GetChild(i).gameObject);

            // 根据内容计算宽度
            string nameText = data.playerName;
            string levelText = $"升级为 LV.{data.newLevel}";
            float estimatedWidth = Mathf.Max(nameText.Length * 28f + levelText.Length * 24f + 100f, 400f);
            estimatedWidth = Mathf.Min(estimatedWidth, 680f);
            rt.sizeDelta = new Vector2(estimatedWidth, 64);

            Color borderColor = isLeft ? COL_LEFT_BORDER : COL_RIGHT_BORDER;

            // === 外层边框（亮色） ===
            var bgImg = go.GetComponent<Image>();
            if (bgImg == null) bgImg = go.AddComponent<Image>();
            bgImg.enabled = true;
            bgImg.color = borderColor;
            bgImg.raycastTarget = false;

            // === 内层底色（比外框小2px，形成边框效果） ===
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

            // === 内发光高光线（顶部，阵营色调） ===
            Color glowColor = isLeft ? COL_LEFT_GLOW : COL_RIGHT_GLOW;
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

            // === 底部微光线（增加层次） ===
            var botGlow = new GameObject("BottomGlow", typeof(RectTransform));
            botGlow.transform.SetParent(innerBgGo.transform, false);
            var botGlowRT = botGlow.GetComponent<RectTransform>();
            botGlowRT.anchorMin = new Vector2(0.1f, 0f);
            botGlowRT.anchorMax = new Vector2(0.9f, 0.12f);
            botGlowRT.offsetMin = Vector2.zero;
            botGlowRT.offsetMax = Vector2.zero;
            var botGlowImg = botGlow.AddComponent<Image>();
            botGlowImg.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.08f);
            botGlowImg.raycastTarget = false;

            // === 左侧渐变边缘 ===
            var leftFade = new GameObject("LeftFade", typeof(RectTransform));
            leftFade.transform.SetParent(go.transform, false);
            var leftFadeRT = leftFade.GetComponent<RectTransform>();
            leftFadeRT.anchorMin = new Vector2(0, 0);
            leftFadeRT.anchorMax = new Vector2(0, 1);
            leftFadeRT.pivot = new Vector2(1, 0.5f);
            leftFadeRT.anchoredPosition = Vector2.zero;
            leftFadeRT.sizeDelta = new Vector2(40, 0);
            var leftFadeImg = leftFade.AddComponent<Image>();
            leftFadeImg.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0.3f);
            leftFadeImg.raycastTarget = false;

            // === 右侧渐变边缘 ===
            var rightFade = new GameObject("RightFade", typeof(RectTransform));
            rightFade.transform.SetParent(go.transform, false);
            var rightFadeRT = rightFade.GetComponent<RectTransform>();
            rightFadeRT.anchorMin = new Vector2(1, 0);
            rightFadeRT.anchorMax = new Vector2(1, 1);
            rightFadeRT.pivot = new Vector2(0, 0.5f);
            rightFadeRT.anchoredPosition = Vector2.zero;
            rightFadeRT.sizeDelta = new Vector2(40, 0);
            var rightFadeImg = rightFade.AddComponent<Image>();
            rightFadeImg.color = new Color(bgColor.r, bgColor.g, bgColor.b, 0.3f);
            rightFadeImg.raycastTarget = false;

            // === 阵营符号前缀（TMP兼容Unicode） ===
            string cBdr = ColorUtility.ToHtmlStringRGB(borderColor);
            string campSymbol = isLeft ? $"<color=#{cBdr}>\u25c6</color>" : $"<color=#{cBdr}>\u25c6</color>"; // ◆
            var symbolGo = new GameObject("CampSymbol", typeof(RectTransform));
            symbolGo.transform.SetParent(go.transform, false);
            var symbolTMP = symbolGo.AddComponent<TextMeshProUGUI>();
            var symbolRT = symbolGo.GetComponent<RectTransform>();
            symbolRT.anchorMin = new Vector2(0, 0);
            symbolRT.anchorMax = new Vector2(0, 1);
            symbolRT.pivot = new Vector2(0, 0.5f);
            symbolRT.anchoredPosition = new Vector2(10, 0);
            symbolRT.sizeDelta = new Vector2(36, 0);
            symbolTMP.text = campSymbol;
            symbolTMP.richText = true;
            symbolTMP.fontSize = 26;
            symbolTMP.alignment = TextAlignmentOptions.Center;
            symbolTMP.enableWordWrapping = false;
            symbolTMP.raycastTarget = false;
            if (_chineseFont != null) symbolTMP.font = _chineseFont;

            // === 玩家名 (大号, 粗体) ===
            var nameGo = new GameObject("PlayerName", typeof(RectTransform));
            nameGo.transform.SetParent(go.transform, false);
            var nameTMP = nameGo.AddComponent<TextMeshProUGUI>();
            var nameRT = nameGo.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0);
            nameRT.anchorMax = new Vector2(0.48f, 1);
            nameRT.offsetMin = new Vector2(42, 4);
            nameRT.offsetMax = new Vector2(-4, -4);
            nameTMP.text = nameText;
            nameTMP.fontSize = 28;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color = Color.white;
            nameTMP.alignment = TextAlignmentOptions.MidlineRight;
            nameTMP.enableWordWrapping = false;
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;
            if (_chineseFont != null) nameTMP.font = _chineseFont;
            ApplyUnderlay(nameTMP);

            // === 升级信息 (金色+星星emoji, 醒目) ===
            var levelGo = new GameObject("LevelInfo", typeof(RectTransform));
            levelGo.transform.SetParent(go.transform, false);
            var levelTMP = levelGo.AddComponent<TextMeshProUGUI>();
            var levelRT = levelGo.GetComponent<RectTransform>();
            levelRT.anchorMin = new Vector2(0.48f, 0);
            levelRT.anchorMax = new Vector2(1, 1);
            levelRT.offsetMin = new Vector2(4, 4);
            levelRT.offsetMax = new Vector2(-16, -4);
            levelTMP.text = $"<color=#FFD700>\u2605 LV.{data.newLevel}</color>"; // ★
            levelTMP.fontSize = 30;
            levelTMP.fontStyle = FontStyles.Bold;
            levelTMP.color = COL_LEVEL;
            levelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            levelTMP.enableWordWrapping = false;
            levelTMP.richText = true;
            if (_chineseFont != null) levelTMP.font = _chineseFont;
            ApplyUnderlay(levelTMP);

            // ====== 滑入动画 ======
            float yPos = -320f - _active.Count * 76f;
            float startX = isLeft ? -900f : 900f;
            float endX = isLeft ? -200f : 200f;

            rt.anchoredPosition = new Vector2(startX, yPos);
            go.SetActive(true);
            _active.Add(go);

            // 滑入
            yield return SlideAnimation(rt, startX, endX, yPos, slideSpeed);

            // 抖动效果
            yield return ShakeAnimation(rt, endX, yPos);

            // 播放升级音效
            if (Systems.AudioManager.Instance != null)
                Systems.AudioManager.Instance.PlaySFX("upgrade");

            // 停留
            yield return new WaitForSeconds(displayDuration);

            // 滑出
            float exitX = isLeft ? -900f : 900f;
            yield return SlideAnimation(rt, endX, exitX, yPos, slideSpeed * 0.7f);

            _active.Remove(go);
            ReturnToPool(go);
            RearrangeActive();
        }

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

        private IEnumerator ShakeAnimation(RectTransform rt, float baseX, float baseY)
        {
            float elapsed = 0;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - (elapsed / shakeDuration);
                float offsetX = Random.Range(-shakeIntensity, shakeIntensity) * decay;
                float offsetY = Random.Range(-shakeIntensity * 0.5f, shakeIntensity * 0.5f) * decay;
                if (rt != null)
                    rt.anchoredPosition = new Vector2(baseX + offsetX, baseY + offsetY);
                yield return null;
            }
            if (rt != null)
                rt.anchoredPosition = new Vector2(baseX, baseY);
        }

        private void RearrangeActive()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var rt = _active[i].GetComponent<RectTransform>();
                if (rt)
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -320f - i * 76f);
            }
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0) return _pool.Dequeue();
            var go = new GameObject("UpgradeNotif", typeof(RectTransform));
            go.transform.SetParent(container, false);
            return go;
        }

        private void ReturnToPool(GameObject go)
        {
            go.SetActive(false);
            _pool.Enqueue(go);
        }

        private void ApplyUnderlay(TextMeshProUGUI tmp)
        {
            // ForceMeshUpdate确保材质已初始化（动态AddComponent后material可能为null）
            tmp.ForceMeshUpdate();
            var mat = tmp.fontMaterial;
            if (mat == null) return;
            tmp.outlineWidth = 0.3f;
            tmp.outlineColor = new Color32(0, 0, 0, 200);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.7f));
            mat.SetFloat("_UnderlayOffsetX", 1f);
            mat.SetFloat("_UnderlayOffsetY", -1f);
            mat.SetFloat("_UnderlayDilate", 0.3f);
            mat.SetFloat("_UnderlaySoftness", 0.3f);
        }
    }
}
