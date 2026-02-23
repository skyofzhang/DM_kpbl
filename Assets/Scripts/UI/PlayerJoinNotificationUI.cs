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
    /// 玩家加入通知 - 美化版
    /// 阵营色渐变底+名字大号粗体+阵营文字常规+从两侧滑入
    /// 底图自适应文字长度，边缘半透渐变
    /// </summary>
    public class PlayerJoinNotificationUI : MonoBehaviour
    {
        [Header("Config")]
        public Transform container;
        public float displayDuration = 2.2f;
        public float slideSpeed = 0.3f;
        public int maxVisible = 3;

        private Queue<JoinData> _pendingQueue = new Queue<JoinData>();
        private List<GameObject> _active = new List<GameObject>();
        private Queue<GameObject> _pool = new Queue<GameObject>();
        private CampSystem _campSystem;
        private TMP_FontAsset _chineseFont;
        private bool _subscribed = false;
        private bool _isProcessing = false;
        // 去重：记录最近显示过的playerId，防止短时间内重复通知
        private Dictionary<string, float> _recentJoins = new Dictionary<string, float>();
        private const float DEDUP_WINDOW = 5f; // 5秒内同一玩家不重复显示

        // 阵营底色（半透明）
        private static readonly Color COL_LEFT_BG = new Color(1f, 0.5f, 0f, 0.75f);
        private static readonly Color COL_RIGHT_BG = new Color(0.45f, 0.82f, 0.12f, 0.75f);
        // 阵营名文字色
        private static readonly Color COL_LEFT_CAMP = new Color(1f, 0.75f, 0.3f);
        private static readonly Color COL_RIGHT_CAMP = new Color(0.6f, 1f, 0.35f);

        private struct JoinData
        {
            public string playerName;
            public string camp;
        }

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
            if (_subscribed) return;
            _campSystem = FindObjectOfType<CampSystem>();
            if (_campSystem != null)
            {
                _campSystem.OnPlayerJoined += HandlePlayerJoined;
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_campSystem != null)
                _campSystem.OnPlayerJoined -= HandlePlayerJoined;
        }

        private void HandlePlayerJoined(string playerId, string playerName, string camp)
        {
            if (!gameObject.activeInHierarchy) return;

            // 去重：同一playerId在5秒内不重复显示
            float now = Time.time;
            if (_recentJoins.TryGetValue(playerId, out float lastTime) && now - lastTime < DEDUP_WINDOW)
                return;
            _recentJoins[playerId] = now;

            // 清理过期记录（防止内存泄漏）
            if (_recentJoins.Count > 200)
            {
                var expired = new List<string>();
                foreach (var kv in _recentJoins)
                    if (now - kv.Value > DEDUP_WINDOW * 2) expired.Add(kv.Key);
                foreach (var k in expired) _recentJoins.Remove(k);
            }

            _pendingQueue.Enqueue(new JoinData { playerName = playerName, camp = camp });
            if (!_isProcessing)
                StartCoroutine(ProcessQueue());

            // 播放加入音效
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("player_join");
        }

        private IEnumerator ProcessQueue()
        {
            _isProcessing = true;
            while (_pendingQueue.Count > 0)
            {
                while (_active.Count >= maxVisible && _active.Count > 0)
                {
                    var oldest = _active[0];
                    _active.RemoveAt(0);
                    ReturnToPool(oldest);
                }

                var data = _pendingQueue.Dequeue();
                yield return ShowJoinNotification(data);
                yield return new WaitForSeconds(0.15f);
            }
            _isProcessing = false;
        }

        private IEnumerator ShowJoinNotification(JoinData data)
        {
            var go = GetFromPool();
            var rt = go.GetComponent<RectTransform>();

            bool isLeft = data.camp == "left";
            Color bgColor = isLeft ? COL_LEFT_BG : COL_RIGHT_BG;
            Color campColor = isLeft ? COL_LEFT_CAMP : COL_RIGHT_CAMP;
            string campName = isLeft ? "香橙阵营" : "柚子阵营";

            // 清理旧子对象
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Destroy(go.transform.GetChild(i).gameObject);

            // 自适应宽度：名字长度 + "加入XX阵营" 固定字数
            float nameWidth = Mathf.Max(data.playerName.Length * 26f, 80f);
            float totalWidth = nameWidth + 180f + 40f; // 名字 + 阵营文字 + padding
            totalWidth = Mathf.Clamp(totalWidth, 300f, 580f);
            rt.sizeDelta = new Vector2(totalWidth, 50);

            // === 底图 ===
            var bgImg = go.GetComponent<Image>();
            if (bgImg == null) bgImg = go.AddComponent<Image>();
            bgImg.enabled = true;
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            // === 左渐变边 ===
            CreateFadeEdge(go.transform, true, bgColor);
            // === 右渐变边 ===
            CreateFadeEdge(go.transform, false, bgColor);

            // === 文字容器 (水平布局) ===
            var textContainer = new GameObject("TextRow", typeof(RectTransform));
            textContainer.transform.SetParent(go.transform, false);
            var tcRT = textContainer.GetComponent<RectTransform>();
            tcRT.anchorMin = Vector2.zero;
            tcRT.anchorMax = Vector2.one;
            tcRT.offsetMin = new Vector2(14, 3);
            tcRT.offsetMax = new Vector2(-14, -3);

            var hlg = textContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 6;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            // === 玩家名 (大号粗体白色) ===
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(textContainer.transform, false);
            var nameTMP = nameGo.AddComponent<TextMeshProUGUI>();
            nameTMP.text = data.playerName;
            nameTMP.fontSize = 26;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color = Color.white;
            nameTMP.alignment = TextAlignmentOptions.MidlineRight;
            nameTMP.enableWordWrapping = false;
            nameTMP.overflowMode = TextOverflowModes.Ellipsis;
            if (_chineseFont != null) nameTMP.font = _chineseFont;
            ApplyUnderlay(nameTMP);
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.preferredWidth = nameWidth;
            nameLE.flexibleWidth = 1;

            // === 加入文字 (常规, 阵营色) ===
            var joinGo = new GameObject("JoinText", typeof(RectTransform));
            joinGo.transform.SetParent(textContainer.transform, false);
            var joinTMP = joinGo.AddComponent<TextMeshProUGUI>();
            joinTMP.text = $"加入了 <b>{campName}</b>";
            joinTMP.fontSize = 20;
            joinTMP.richText = true;
            joinTMP.color = campColor;
            joinTMP.alignment = TextAlignmentOptions.MidlineLeft;
            joinTMP.enableWordWrapping = false;
            if (_chineseFont != null) joinTMP.font = _chineseFont;
            ApplyUnderlayLight(joinTMP);
            var joinLE = joinGo.AddComponent<LayoutElement>();
            joinLE.preferredWidth = 170;

            // ====== 滑入动画 ======
            float yPos = -450f - _active.Count * 58f;
            float startX = isLeft ? -700f : 700f;
            float endX = isLeft ? -180f : 180f;

            rt.anchoredPosition = new Vector2(startX, yPos);
            go.SetActive(true);
            _active.Add(go);

            yield return SlideAnimation(rt, startX, endX, yPos, slideSpeed);
            yield return new WaitForSeconds(displayDuration);

            float exitX = isLeft ? -700f : 700f;
            yield return SlideAnimation(rt, endX, exitX, yPos, slideSpeed * 0.7f);

            _active.Remove(go);
            ReturnToPool(go);
            RearrangeActive();
        }

        private void CreateFadeEdge(Transform parent, bool isLeftEdge, Color baseColor)
        {
            var fade = new GameObject(isLeftEdge ? "LeftFade" : "RightFade", typeof(RectTransform));
            fade.transform.SetParent(parent, false);
            var fadeRT = fade.GetComponent<RectTransform>();
            if (isLeftEdge)
            {
                fadeRT.anchorMin = new Vector2(0, 0);
                fadeRT.anchorMax = new Vector2(0, 1);
                fadeRT.pivot = new Vector2(1, 0.5f);
            }
            else
            {
                fadeRT.anchorMin = new Vector2(1, 0);
                fadeRT.anchorMax = new Vector2(1, 1);
                fadeRT.pivot = new Vector2(0, 0.5f);
            }
            fadeRT.anchoredPosition = Vector2.zero;
            fadeRT.sizeDelta = new Vector2(30, 0);
            var fadeImg = fade.AddComponent<Image>();
            fadeImg.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.2f);
            fadeImg.raycastTarget = false;
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

        private void RearrangeActive()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var rt = _active[i].GetComponent<RectTransform>();
                if (rt)
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -450f - i * 58f);
            }
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0) return _pool.Dequeue();
            var go = new GameObject("JoinNotif", typeof(RectTransform));
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
    }
}
