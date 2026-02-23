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
            string campName = winner == "left" ? "香橙" : winner == "right" ? "柚子" : "";
            Color color = winner == "left"
                ? new Color(1f, 0.55f, 0f)      // 橙色
                : new Color(0.68f, 1f, 0.18f);  // 绿色

            if (winner == "draw")
            {
                ShowAnnouncement("比赛结束", "平局！", Color.white, 4.0f);
            }
            else
            {
                string reason = data.reason == "reached_end" ? "推到终点！" : "时间到！";
                ShowAnnouncement($"恭喜 {campName}阵营 获胜！", reason, color, 4.0f);
            }
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
