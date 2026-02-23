using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Systems;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 公告面板 - 版本更新日志+未来功能预告
    /// 挂载在Canvas上(始终active)，点击左上角"公告"按钮弹出
    /// 支持滚动查看，每次版本更新在此添加记录
    /// </summary>
    public class AnnouncementPanelUI : MonoBehaviour
    {
        private GameObject _panelRoot;
        private TMP_FontAsset _chineseFont;
        private bool _isOpen = false;

        private const string CURRENT_VERSION = "1.0.1";

        // ==================== 公告内容数据 ====================
        // 每次版本更新在此追加，格式: { 版本号, 日期, 内容条目[] }
        // 初始版本暂无内容，上线后由开发者手动添加更新记录
        private static readonly VersionNote[] VERSION_NOTES = new VersionNote[] { };

        private struct VersionNote
        {
            public string version;
            public string date;
            public string title;
            public string[] notes;
        }

        private void Start()
        {
            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
        }

        public void Toggle()
        {
            if (_isOpen) Close(); else Open();
        }

        public void Open()
        {
            if (_panelRoot != null) return;
            _isOpen = true;
            CreatePanel();
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("ui_click");
        }

        public void Close()
        {
            if (_panelRoot != null) { Destroy(_panelRoot); _panelRoot = null; }
            _isOpen = false;
        }

        // ==================== 面板构建 ====================

        private void CreatePanel()
        {
            // === 遮罩 ===
            _panelRoot = new GameObject("AnnouncementOverlay");
            _panelRoot.transform.SetParent(transform, false);
            var overlayRT = _panelRoot.AddComponent<RectTransform>();
            StretchFull(overlayRT);
            var overlayImg = _panelRoot.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.5f);
            var overlayBtn = _panelRoot.AddComponent<Button>();
            overlayBtn.onClick.AddListener(Close);

            // === 主面板 ===
            var panel = new GameObject("AnnouncementPanel");
            panel.transform.SetParent(_panelRoot.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchoredPosition = new Vector2(0, 0);  // 屏幕正中
            panelRT.sizeDelta = new Vector2(720, 800);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.08f, 0.14f, 0.97f);
            var panelBtn = panel.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None;

            float yOffset = 0;

            // === 标题栏 ===
            var titleBar = new GameObject("TitleBar", typeof(RectTransform));
            titleBar.transform.SetParent(panel.transform, false);
            var titleBarRT = titleBar.GetComponent<RectTransform>();
            titleBarRT.anchorMin = new Vector2(0, 1);
            titleBarRT.anchorMax = new Vector2(1, 1);
            titleBarRT.pivot = new Vector2(0.5f, 1);
            titleBarRT.anchoredPosition = Vector2.zero;
            titleBarRT.sizeDelta = new Vector2(0, 70);
            var titleBarImg = titleBar.AddComponent<Image>();
            titleBarImg.color = new Color(0.1f, 0.12f, 0.2f, 0.95f);

            CreateTMP(titleBar.transform, "Title", "公 告", 36,
                new Vector2(0, -35), new Vector2(300, 50),
                TextAlignmentOptions.Center, new Color(1f, 0.84f, 0f),
                FontStyles.Bold);

            // 版本号（放在标题左侧区域，避免与关闭按钮重叠）
            CreateTMP(titleBar.transform, "Version", $"v{CURRENT_VERSION}", 20,
                new Vector2(-200, -35), new Vector2(120, 30),
                TextAlignmentOptions.Left, new Color(0.6f, 0.6f, 0.6f));

            // 关闭按钮（面板宽720，半宽360，按钮中心在330留30px边距）
            var closeGo = new GameObject("BtnClose");
            closeGo.transform.SetParent(titleBar.transform, false);
            var closeRT = closeGo.AddComponent<RectTransform>();
            closeRT.anchoredPosition = new Vector2(330, -35);
            closeRT.sizeDelta = new Vector2(44, 44);
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = new Color(0.55f, 0.12f, 0.12f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Close);
            CreateTMP(closeGo.transform, "X", "X", 24,
                Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center, Color.white,
                FontStyles.Normal, true);

            // === ScrollView ===
            var scrollView = new GameObject("ScrollView", typeof(RectTransform));
            scrollView.transform.SetParent(panel.transform, false);
            var scrollRT = scrollView.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.offsetMin = new Vector2(30, 20);
            scrollRT.offsetMax = new Vector2(-30, -75);

            var scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;

            var scrollMask = scrollView.AddComponent<Mask>();
            var scrollMaskImg = scrollView.AddComponent<Image>();
            scrollMaskImg.color = new Color(1, 1, 1, 0.01f); // 几乎透明但需要Image才能Mask

            // Content容器
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(scrollView.transform, false);
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 10;
            vlg.padding = new RectOffset(20, 20, 10, 20);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            // === 填充内容 ===
            // 版本记录（倒序显示，最新在上）
            if (VERSION_NOTES.Length > 0)
            {
                for (int i = VERSION_NOTES.Length - 1; i >= 0; i--)
                {
                    var note = VERSION_NOTES[i];
                    AddContentText(content.transform,
                        $"v{note.version}  —  {note.title}", 24,
                        new Color(1f, 0.84f, 0f), FontStyles.Bold, 40);
                    AddContentText(content.transform, note.date, 18,
                        new Color(0.55f, 0.55f, 0.55f), FontStyles.Normal, 26);
                    foreach (var line in note.notes)
                    {
                        AddContentText(content.transform, $"  \u2022 {line}", 20,
                            new Color(0.85f, 0.85f, 0.85f), FontStyles.Normal, 30);
                    }
                    if (i > 0) AddDivider(content.transform);
                }
            }
            else
            {
                // 暂无公告内容（初始版本，上线后手动添加）
                AddContentText(content.transform, "暂无公告", 24,
                    new Color(0.5f, 0.5f, 0.5f), FontStyles.Normal, 60);
            }
        }

        // ==================== 内容构建工具 ====================

        private void AddContentText(Transform parent, string text, float fontSize,
            Color color, FontStyles style, float preferredHeight)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            if (_chineseFont != null) tmp.font = _chineseFont;
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = new Color32(0, 0, 0, 100);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
        }

        private void AddDivider(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.35f, 0.35f, 0.4f, 0.4f);
            img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1.5f;
        }

        // ==================== 工具 ====================

        private TextMeshProUGUI CreateTMP(Transform parent, string name,
            string text, float fontSize, Vector2 pos, Vector2 size,
            TextAlignmentOptions align, Color color,
            FontStyles style = FontStyles.Normal, bool stretchFill = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            if (stretchFill)
            {
                StretchFull(rt);
            }
            else
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.enableWordWrapping = true;
            if (_chineseFont != null) tmp.font = _chineseFont;
            return tmp;
        }

        private void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
