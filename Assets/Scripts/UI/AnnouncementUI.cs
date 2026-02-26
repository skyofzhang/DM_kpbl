using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using CapybaraDuel.Core;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 全屏公告UI - 比赛开始/结束等重要提示
    /// 居中大字 + 渐入渐出动画
    /// </summary>
    public class AnnouncementUI : MonoBehaviour
    {
        public static AnnouncementUI Instance { get; private set; }

        [Header("References")]
        public CanvasGroup canvasGroup;
        public TextMeshProUGUI mainText;
        public TextMeshProUGUI subText;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        private Coroutine _currentAnnouncement;

        private void Awake()
        {
            Instance = this;
            EnsureComponents();
            HideVisual();

            // 公告层级：高于通知(20)和礼物(30)，低于结算(100)
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 90;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        private void OnEnable()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnStateChanged += HandleStateChanged;
                gm.OnGameEnded += HandleGameEnded;
            }
        }

        /// <summary>如果引用丢失，运行时自动创建必要组件</summary>
        private void EnsureComponents()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (mainText == null)
            {
                var existing = transform.Find("MainText");
                if (existing != null) mainText = existing.GetComponent<TextMeshProUGUI>();
            }
            if (mainText == null)
            {
                var go = new GameObject("MainText", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                mainText = go.AddComponent<TextMeshProUGUI>();
                mainText.fontSize = 72;
                mainText.color = Color.yellow;
                mainText.alignment = TextAlignmentOptions.Center;
                mainText.fontStyle = FontStyles.Bold;
                mainText.enableWordWrapping = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, 30);
                rt.sizeDelta = new Vector2(900, 120);
                // 描边
                mainText.outlineWidth = 0.4f;
                mainText.outlineColor = new Color32(0, 0, 0, 255);
                // 尝试加载中文字体
                var font = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
                if (font != null) mainText.font = font;
            }

            if (subText == null)
            {
                var existing = transform.Find("SubText");
                if (existing != null) subText = existing.GetComponent<TextMeshProUGUI>();
            }
            if (subText == null)
            {
                var go = new GameObject("SubText", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                subText = go.AddComponent<TextMeshProUGUI>();
                subText.fontSize = 36;
                subText.color = Color.white;
                subText.alignment = TextAlignmentOptions.Center;
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0, -50);
                rt.sizeDelta = new Vector2(600, 60);
                var font = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
                if (font != null) subText.font = font;
            }
        }

        private void OnDisable()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnStateChanged -= HandleStateChanged;
                gm.OnGameEnded -= HandleGameEnded;
            }
        }

        private void HandleStateChanged(GameManager.GameState oldState, GameManager.GameState newState)
        {
            if (newState == GameManager.GameState.Running && oldState != GameManager.GameState.Running)
            {
                // 延迟0.1秒显示，确保UIManager已切换到游戏界面
                // 并检查gameUIPanel是否可见，避免在主菜单界面显示"比赛开始"
                StartCoroutine(DelayedStartAnnouncement());
            }
        }

        private IEnumerator DelayedStartAnnouncement()
        {
            yield return new WaitForSeconds(0.1f);
            // 确认仍然在Running状态且游戏面板可见
            var gm = GameManager.Instance;
            var uiMgr = UIManager.Instance;
            if (gm != null && gm.CurrentState == GameManager.GameState.Running
                && uiMgr != null && uiMgr.gameUIPanel != null && uiMgr.gameUIPanel.activeSelf)
            {
                ShowAnnouncement("比赛开始！", "", new Color(1f, 0.85f, 0.2f), 2.0f);
            }
        }

        private void HandleGameEnded(GameEndedData data)
        {
            // 先弹出获胜公告，延迟后再让结算面板显示
            string winner = data.winner;
            string campName = winner == "left" ? "\u9999\u6a59" : winner == "right" ? "\u67da\u5b50" : "";
            Color color = winner == "left"
                ? new Color(1f, 0.55f, 0f)      // 橙色
                : new Color(0.68f, 1f, 0.18f);  // 绿色

            if (winner == "draw")
            {
                ShowAnnouncement("\u6bd4\u8d5b\u7ed3\u675f", "\u5e73\u5c40\uff01", Color.white, 4.0f);
            }
            else
            {
                string reason = data.reason == "reached_end" ? "\u63a8\u5230\u7ec8\u70b9\uff01" : "\u65f6\u95f4\u5230\uff01";
                // 增强版胜利公告：大符号 + 阵营名 + 胜利
                string victorySymbol = winner == "left" ? "\u2605\u25c6\u2605" : "\u2605\u25c6\u2605"; // ★◆★
                ShowVictoryAnnouncement($"{victorySymbol} {campName}\u9635\u8425 \u80dc\u5229 {victorySymbol}", reason, color, 4.5f);
            }
        }

        /// <summary>增强版胜利公告：抖动+描边发光+扫光+呼吸缩放</summary>
        public void ShowVictoryAnnouncement(string main, string sub, Color color, float duration)
        {
            if (_currentAnnouncement != null)
                StopCoroutine(_currentAnnouncement);
            _currentAnnouncement = StartCoroutine(VictoryAnnouncementRoutine(main, sub, color, duration));
        }

        private IEnumerator VictoryAnnouncementRoutine(string main, string sub, Color color, float duration)
        {
            var chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
            if (chineseFont != null)
            {
                if (mainText != null && mainText.font != chineseFont)
                    mainText.font = chineseFont;
                if (subText != null && subText.font != chineseFont)
                    subText.font = chineseFont;
            }

            // 设置内容 - 大字体
            if (mainText != null)
            {
                mainText.text = main;
                mainText.color = color;
                mainText.fontSize = 96; // 比普通公告更大
                mainText.fontStyle = FontStyles.Bold;
                // 加强描边
                mainText.outlineWidth = 0.5f;
                mainText.outlineColor = new Color32(0, 0, 0, 255);
                // 发光投影 — v118: 降低Dilate/Softness，防止文字膨胀错乱
                mainText.ForceMeshUpdate();
                var mat = mainText.fontMaterial;
                if (mat != null)
                {
                    mat.EnableKeyword("UNDERLAY_ON");
                    mat.SetColor("_UnderlayColor", new Color(color.r, color.g, color.b, 0.5f));
                    mat.SetFloat("_UnderlayOffsetX", 0f);
                    mat.SetFloat("_UnderlayOffsetY", 0f);
                    mat.SetFloat("_UnderlayDilate", 0.3f);
                    mat.SetFloat("_UnderlaySoftness", 0.3f);
                }
            }
            if (subText != null)
            {
                subText.text = sub;
                subText.color = Color.white;
                subText.fontSize = 42;
            }

            // === Phase 1: 弹入 + 抖动 (0.5s) ===
            if (canvasGroup)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            float t = 0;
            float enterDuration = 0.4f;
            while (t < enterDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / enterDuration);
                if (canvasGroup) canvasGroup.alpha = p;

                // 从2倍缩小到1倍 + 弹性过冲
                float eased = 1f + 2.7f * Mathf.Pow(p - 1f, 3f) + 1.7f * Mathf.Pow(p - 1f, 2f);
                float scale = Mathf.Lerp(2.5f, 1f, eased);
                if (mainText) mainText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            if (canvasGroup) canvasGroup.alpha = 1;

            // === Phase 2: 着陆抖动 (0.5s) ===
            float shakeDuration = 0.5f;
            float shakeIntensity = 12f;
            t = 0;
            Vector3 basePos = mainText != null ? mainText.transform.localPosition : Vector3.zero;
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                float decay = 1f - (t / shakeDuration);
                decay *= decay; // 加速衰减
                float shakeX = Random.Range(-shakeIntensity, shakeIntensity) * decay;
                float shakeY = Random.Range(-shakeIntensity * 0.6f, shakeIntensity * 0.6f) * decay;
                if (mainText) mainText.transform.localPosition = basePos + new Vector3(shakeX, shakeY, 0);
                yield return null;
            }
            if (mainText)
            {
                mainText.transform.localPosition = basePos;
                mainText.transform.localScale = Vector3.one;
            }

            // === Phase 3: 呼吸缩放 + 停留 ===
            float stayDuration = duration - enterDuration - shakeDuration - fadeOutDuration;
            t = 0;
            while (t < stayDuration)
            {
                t += Time.deltaTime;
                // 微呼吸缩放（增加仪式感）
                float breathScale = 1f + Mathf.Sin(t * 3f) * 0.04f;
                if (mainText) mainText.transform.localScale = Vector3.one * breathScale;
                yield return null;
            }

            // === Phase 4: 淡出 + 重置材质 ===
            if (mainText)
            {
                mainText.transform.localScale = Vector3.one;
                mainText.fontSize = 72; // 恢复默认字号
                // v118: 重置underlay参数，防止污染后续公告文字
                var matReset = mainText.fontMaterial;
                if (matReset != null)
                {
                    matReset.SetFloat("_UnderlayDilate", 0.1f);
                    matReset.SetFloat("_UnderlaySoftness", 0.2f);
                    matReset.SetFloat("_UnderlayOffsetX", 0.5f);
                    matReset.SetFloat("_UnderlayOffsetY", -0.5f);
                    matReset.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.6f));
                }
            }
            t = 0;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                if (canvasGroup) canvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeOutDuration);
                yield return null;
            }

            HideVisual();
            _currentAnnouncement = null;
        }

        /// <summary>
        /// 显示公告（自动隐藏）
        /// </summary>
        public void ShowAnnouncement(string main, string sub, Color color, float duration)
        {
            if (_currentAnnouncement != null)
                StopCoroutine(_currentAnnouncement);
            _currentAnnouncement = StartCoroutine(AnnouncementRoutine(main, sub, color, duration));
        }

        private IEnumerator AnnouncementRoutine(string main, string sub, Color color, float duration)
        {
            // 确保字体正确加载（解决乱码问题）
            var chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
            if (chineseFont != null)
            {
                if (mainText != null && mainText.font != chineseFont)
                    mainText.font = chineseFont;
                if (subText != null && subText.font != chineseFont)
                    subText.font = chineseFont;
            }

            // 设置内容
            if (mainText != null)
            {
                mainText.text = main;
                mainText.color = color;
            }
            if (subText != null)
            {
                subText.text = sub;
                subText.color = Color.white;
            }

            // 淡入（恢复可见状态）
            if (canvasGroup)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
            float t = 0;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                if (canvasGroup) canvasGroup.alpha = Mathf.Lerp(0, 1, t / fadeInDuration);
                // 缩放弹出效果
                float scale = Mathf.Lerp(1.5f, 1f, t / fadeInDuration);
                if (mainText) mainText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            if (canvasGroup) canvasGroup.alpha = 1;
            if (mainText) mainText.transform.localScale = Vector3.one;

            // 保持显示
            yield return new WaitForSeconds(duration);

            // 淡出
            t = 0;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                if (canvasGroup) canvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeOutDuration);
                yield return null;
            }

            HideVisual();
            _currentAnnouncement = null;
        }

        /// <summary>隐藏视觉，但保持 GameObject 激活（不影响事件订阅）</summary>
        private void HideVisual()
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = 0;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }
    }
}
