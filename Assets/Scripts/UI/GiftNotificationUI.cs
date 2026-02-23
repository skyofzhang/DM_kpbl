using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Systems;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 礼物通知 - 分层显示（普通/稀有/史诗/传说），最多同屏4条
    /// tier 1 = 普通黄字, tier 2 = 蓝色面板, tier 3 = 紫色面板, legendary = 金色横幅
    /// </summary>
    public class GiftNotificationUI : MonoBehaviour
    {
        [Header("Config")]
        public Transform container;
        public int maxVisible = 4;

        private Queue<GameObject> _pool = new Queue<GameObject>();
        private List<GameObject> _active = new List<GameObject>();
        private GiftHandler _giftHandler;
        private TMP_FontAsset _chineseFont;
        private bool _subscribed = false;

        // 层级颜色配置
        private static readonly Color TIER1_COLOR = Color.yellow;
        private static readonly Color TIER2_BG = new Color(0.15f, 0.3f, 0.6f, 0.85f);
        private static readonly Color TIER2_TEXT = new Color(0.6f, 0.8f, 1f);
        private static readonly Color TIER3_BG = new Color(0.45f, 0.15f, 0.6f, 0.85f);
        private static readonly Color TIER3_TEXT = new Color(0.9f, 0.7f, 1f);
        private static readonly Color LEGEND_BG = new Color(0.7f, 0.5f, 0.1f, 0.9f);
        private static readonly Color LEGEND_TEXT = new Color(1f, 0.95f, 0.7f);

        private void Start()
        {
            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
            if (container == null) container = transform;
            TrySubscribe();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            // 已弃用：礼物通知信息已合并到GiftAnimationUI的弹窗动画中显示
            // 不再订阅GiftHandler事件，避免底部文字与动画弹窗重复显示
        }

        private void OnDestroy()
        {
            if (_giftHandler != null)
                _giftHandler.OnGiftReceived -= ShowGiftNotification;
        }

        private void ShowGiftNotification(GiftReceivedData gift)
        {
            // 安全检查
            if (!gameObject.activeInHierarchy) return;

            // 超过最大数量，移除最早的
            while (_active.Count >= maxVisible && _active.Count > 0)
            {
                var oldest = _active[0];
                _active.RemoveAt(0);
                ReturnToPool(oldest);
            }

            // 解析层级
            int tier = ParseTier(gift.tier);
            float displayDuration = GetDurationByTier(tier);

            var go = GetFromPool();

            // 背景面板（tier 2+才有）
            var bgImg = go.GetComponent<Image>();
            if (bgImg == null) bgImg = go.AddComponent<Image>();

            if (tier >= 2)
            {
                bgImg.enabled = true;
                if (tier == 2) bgImg.color = TIER2_BG;
                else if (tier == 3) bgImg.color = TIER3_BG;
                else bgImg.color = LEGEND_BG; // tier 4/5/6 都用金色背景
            }
            else
            {
                bgImg.enabled = false;
            }

            // 文本
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp == null)
            {
                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(go.transform, false);
                tmp = textGo.AddComponent<TextMeshProUGUI>();
                var textRT = textGo.GetComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = new Vector2(10, 2);
                textRT.offsetMax = new Vector2(-10, -2);
            }

            string campIcon = gift.camp == "left" ? "【橙】" : "【柚】";
            string summonTag = gift.isSummon ? " ★召唤!" : "";
            string tierTag = tier >= 5 ? " ✦✦" : (tier >= 3 ? " ✦" : "");
            string forceStr = gift.forceValue > 0 ? $" (+{gift.forceValue}推力)" : "";
            tmp.text = $"{campIcon} {gift.playerName} 送出 {gift.giftName}×{gift.giftCount}{forceStr}{summonTag}{tierTag}";

            // 按层级设置样式（6级对应6种礼物）
            switch (tier)
            {
                case 1: // 仙女棒 - 普通黄字
                    tmp.fontSize = 22;
                    tmp.color = TIER1_COLOR;
                    break;
                case 2: // 能力药丸 - 蓝色面板
                    tmp.fontSize = 26;
                    tmp.color = TIER2_TEXT;
                    tmp.fontStyle = TMPro.FontStyles.Bold;
                    break;
                case 3: // 甜甜圈 - 紫色面板
                    tmp.fontSize = 30;
                    tmp.color = TIER3_TEXT;
                    tmp.fontStyle = TMPro.FontStyles.Bold;
                    break;
                case 4: // 能量电池 - 金色横幅
                    tmp.fontSize = 34;
                    tmp.color = LEGEND_TEXT;
                    tmp.fontStyle = TMPro.FontStyles.Bold;
                    break;
                case 5: // 爱的爆炸 - 金色大号
                    tmp.fontSize = 38;
                    tmp.color = LEGEND_TEXT;
                    tmp.fontStyle = TMPro.FontStyles.Bold;
                    break;
                default: // 神秘空投(tier 6) - 金色最大号
                    tmp.fontSize = 42;
                    tmp.color = LEGEND_TEXT;
                    tmp.fontStyle = TMPro.FontStyles.Bold;
                    break;
            }

            tmp.alignment = TextAlignmentOptions.Center;
            if (_chineseFont != null) tmp.font = _chineseFont;

            // 尺寸和位置（6级递增）
            var rt = go.GetComponent<RectTransform>();
            float height = tier >= 5 ? 58 : (tier >= 3 ? 50 : (tier == 2 ? 45 : 38));
            float width = tier >= 5 ? 750 : (tier >= 4 ? 700 : (tier >= 2 ? 600 : 550));
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0, -_active.Count * (height + 5));

            go.SetActive(true);
            _active.Add(go);

            StartCoroutine(FadeAndRemove(go, displayDuration));
        }

        private int ParseTier(string tier)
        {
            if (string.IsNullOrEmpty(tier) || tier == "basic") return 1;
            // 数字tier直接解析（服务端现在统一发 "1"~"6"）
            if (int.TryParse(tier, out int t)) return Mathf.Clamp(t, 1, 6);
            // 兼容旧字符串格式
            switch (tier)
            {
                case "common":    return 2;
                case "rare":      return 3;
                case "epic":      return 4;
                case "legendary": return 5;
                default:          return 1;
            }
        }

        private float GetDurationByTier(int tier)
        {
            switch (tier)
            {
                case 1: return 2f;
                case 2: return 3f;
                case 3: return 4f;
                case 4: return 5f;
                case 5: return 6f;
                default: return 7f; // tier 6
            }
        }

        private IEnumerator FadeAndRemove(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (go != null && go.activeSelf)
            {
                _active.Remove(go);
                ReturnToPool(go);
                // 重新排列剩余项
                RearrangeActive();
            }
        }

        private void RearrangeActive()
        {
            float yOffset = 0;
            for (int i = 0; i < _active.Count; i++)
            {
                var rt = _active[i].GetComponent<RectTransform>();
                if (rt)
                {
                    rt.anchoredPosition = new Vector2(0, -yOffset);
                    yOffset += rt.sizeDelta.y + 5;
                }
            }
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0) return _pool.Dequeue();
            var go = new GameObject("GiftNotif", typeof(RectTransform));
            go.transform.SetParent(container, false);
            return go;
        }

        private void ReturnToPool(GameObject go)
        {
            go.SetActive(false);
            _pool.Enqueue(go);
        }
    }
}
