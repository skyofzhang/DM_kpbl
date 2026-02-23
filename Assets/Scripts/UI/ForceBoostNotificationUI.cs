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
    /// 从屏幕中部弹出，向上飘散消失
    /// 666: "XXX 发送666 推力+XX (5秒)"  橙/绿阵营色
    /// 点赞: "XXX 点赞×N 推力+XX (3秒)"  橙/绿阵营色
    /// 挂载在Canvas上
    /// </summary>
    public class ForceBoostNotificationUI : MonoBehaviour
    {
        [Header("Config")]
        public float displayDuration = 1.8f;   // 显示时长（缩短，不遮挡太久）
        public int maxVisible = 4;
        public float floatSpeed = 60f;  // 上飘速度（降低，配合缩短时长）

        private Queue<BoostInfo> _pendingQueue = new Queue<BoostInfo>();
        private List<GameObject> _activeNotifs = new List<GameObject>();
        private Queue<GameObject> _pool = new Queue<GameObject>();
        private TMP_FontAsset _chineseFont;
        private bool _isProcessing = false;
        private GameManager _gm;

        // 阵营颜色
        private static readonly Color COL_LEFT = new Color(1f, 0.55f, 0.1f);   // 橙色
        private static readonly Color COL_RIGHT = new Color(0.4f, 0.85f, 0.2f); // 绿色
        private static readonly Color COL_FORCE = new Color(1f, 0.95f, 0.4f);   // 金黄色推力值

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
            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
            _gm = GameManager.Instance;
            if (_gm != null)
            {
                _gm.OnForceBoost += HandleForceBoost;
                _gm.OnLikeBoost += HandleLikeBoost;
            }
        }

        private void OnDestroy()
        {
            if (_gm != null)
            {
                _gm.OnForceBoost -= HandleForceBoost;
                _gm.OnLikeBoost -= HandleLikeBoost;
            }
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
                action = data.likeNum > 1 ? $"点赞×{data.likeNum}" : "点赞",
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
                // 清理超出最大数量的旧通知
                while (_activeNotifs.Count >= maxVisible && _activeNotifs.Count > 0)
                {
                    var oldest = _activeNotifs[0];
                    _activeNotifs.RemoveAt(0);
                    ReturnToPool(oldest);
                }

                var info = _pendingQueue.Dequeue();
                ShowNotification(info);
                yield return new WaitForSeconds(0.15f); // 错开显示
            }
            _isProcessing = false;
        }

        private void ShowNotification(BoostInfo info)
        {
            var go = GetFromPool();
            var rt = go.GetComponent<RectTransform>();

            // 清理旧子对象
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Destroy(go.transform.GetChild(i).gameObject);

            bool isLeft = info.camp == "left";
            Color campColor = isLeft ? COL_LEFT : COL_RIGHT;
            string campIcon = isLeft ? "🍊" : "🍈";

            // 构建文本：用RichText在一个TMP里完成
            // 格式: "玩家名 666 推力+XX (5秒)"
            string forceText = info.forceValue >= 1000
                ? $"{info.forceValue / 1000f:F1}K"
                : $"{info.forceValue:F0}";
            string displayText = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.white)}>{TruncateName(info.playerName, 6)}</color> " +
                                 $"<color=#{ColorUtility.ToHtmlStringRGB(campColor)}>{info.action}</color> " +
                                 $"<color=#{ColorUtility.ToHtmlStringRGB(COL_FORCE)}>推力+{forceText}</color> " +
                                 $"<color=#{ColorUtility.ToHtmlStringRGB(new Color(0.7f, 0.7f, 0.7f))}>({info.duration}秒)</color>";

            // 估算宽度
            float estimatedWidth = Mathf.Clamp(displayText.Length * 10f + 40f, 350f, 700f);
            rt.sizeDelta = new Vector2(estimatedWidth, 44);

            // 半透明背景
            var bgImg = go.GetComponent<Image>();
            if (bgImg == null) bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(campColor.r * 0.3f, campColor.g * 0.3f, campColor.b * 0.3f, 0.75f);
            bgImg.raycastTarget = false;

            // 左侧阵营色条
            var colorBar = new GameObject("ColorBar", typeof(RectTransform));
            colorBar.transform.SetParent(go.transform, false);
            var barRT = colorBar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(0, 1);
            barRT.pivot = new Vector2(0, 0.5f);
            barRT.anchoredPosition = Vector2.zero;
            barRT.sizeDelta = new Vector2(4, 0);
            var barImg = colorBar.AddComponent<Image>();
            barImg.color = campColor;
            barImg.raycastTarget = false;

            // 文字
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(12, 2);
            textRT.offsetMax = new Vector2(-8, -2);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = displayText;
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.richText = true;
            if (_chineseFont != null) tmp.font = _chineseFont;

            // Underlay安全应用
            tmp.ForceMeshUpdate();
            var mat = tmp.fontMaterial;
            if (mat != null)
            {
                tmp.outlineWidth = 0.2f;
                tmp.outlineColor = new Color32(0, 0, 0, 180);
                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.6f));
                mat.SetFloat("_UnderlayOffsetX", 0.7f);
                mat.SetFloat("_UnderlayOffsetY", -0.7f);
                mat.SetFloat("_UnderlayDilate", 0.15f);
                mat.SetFloat("_UnderlaySoftness", 0.25f);
            }

            // 起始位置: 屏幕中下部（往下移），根据已有通知数量递减Y
            float startY = -250f - _activeNotifs.Count * 52f;
            rt.anchoredPosition = new Vector2(0, startY);
            go.SetActive(true);
            _activeNotifs.Add(go);

            // 启动上飘+淡出动画
            StartCoroutine(FloatAndFade(go, rt, bgImg, tmp, startY));
        }

        private IEnumerator FloatAndFade(GameObject go, RectTransform rt, Image bg, TextMeshProUGUI tmp, float startY)
        {
            float elapsed = 0;
            float fadeStart = displayDuration * 0.6f;
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            while (elapsed < displayDuration)
            {
                elapsed += Time.deltaTime;
                if (rt == null) yield break;

                // 上飘
                float y = startY + elapsed * floatSpeed;
                rt.anchoredPosition = new Vector2(0, y);

                // 后40%开始淡出
                if (elapsed > fadeStart)
                {
                    float fadeProgress = (elapsed - fadeStart) / (displayDuration - fadeStart);
                    cg.alpha = 1f - fadeProgress;
                }

                yield return null;
            }

            _activeNotifs.Remove(go);
            ReturnToPool(go);
        }

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
