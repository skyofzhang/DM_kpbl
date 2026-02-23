using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Systems;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 设置面板 - 运行时动态创建
    /// 功能: 分辨率说明 / 礼物视频开关 / 入场视频开关 /
    ///        BGM开关+音量 / SFX开关+音量 / 结束按钮(二次确认)
    /// 排版: 整洁清晰，分割线分区，标签对齐，间距统一
    ///
    /// 挂载在 Canvas 上(始终active), 通过 _panelRoot 控制显隐
    /// Toggle 状态通过静态属性暴露给 VIPAnnouncementUI 和 GiftAnimationUI
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        // === 静态开关状态（其他UI脚本读取） ===
        public static bool GiftVideoEnabled { get; private set; } = true;
        public static bool VIPVideoEnabled { get; private set; } = true;

        private GameObject _panelRoot;
        private GameObject _confirmDialog;
        private TMP_FontAsset _chineseFont;
        private bool _isOpen = false;

        // 标准布局参数
        private const float PANEL_W = 780f;
        private const float PANEL_H = 850f;
        private const float LABEL_X = -180f;
        private const float TOGGLE_X = 240f;
        private const float ROW_HEIGHT = 56f;
        private const float SECTION_GAP = 14f;
        private const float DIVIDER_W = 680f;

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
            // 播放UI点击音效
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("ui_click");
        }

        public void Close()
        {
            if (_confirmDialog != null) { Destroy(_confirmDialog); _confirmDialog = null; }
            if (_panelRoot != null) { Destroy(_panelRoot); _panelRoot = null; }
            _isOpen = false;
        }

        // ==================== 面板构建 ====================

        private void CreatePanel()
        {
            // === 遮罩层 ===
            _panelRoot = new GameObject("SettingsOverlay");
            _panelRoot.transform.SetParent(transform, false);
            var overlayRT = _panelRoot.AddComponent<RectTransform>();
            StretchFull(overlayRT);
            var overlayImg = _panelRoot.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.55f);
            var overlayBtn = _panelRoot.AddComponent<Button>();
            overlayBtn.onClick.AddListener(Close);

            // === 主面板 ===
            var panel = new GameObject("SettingsPanel");
            panel.transform.SetParent(_panelRoot.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchoredPosition = new Vector2(0, 30);
            panelRT.sizeDelta = new Vector2(PANEL_W, PANEL_H);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.1f, 0.16f, 0.96f);
            panelImg.raycastTarget = true;
            // 阻止穿透
            var panelBtn = panel.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None;

            float yPos = PANEL_H / 2 - 50f;  // 从顶部开始

            // === 标题（去掉全角空格，用characterSpacing代替） ===
            var titleTMP = CreateTMP(panel.transform, "Title", "设置", 40,
                new Vector2(0, yPos), new Vector2(600, 55),
                TextAlignmentOptions.Center, new Color(1f, 0.84f, 0f),
                FontStyles.Bold);
            titleTMP.characterSpacing = 20f;  // 适度字间距
            CreateCloseButton(panel.transform, new Vector2(PANEL_W / 2 - 50, yPos));
            yPos -= 60f;

            // ═══════════════ Section 1: 显示设置 ═══════════════
            CreateSectionHeader(panel.transform, "显示设置", ref yPos);

            // 1.1 分辨率说明（提高对比度）
            CreateTMP(panel.transform, "ResDesc",
                "分辨率: 拖动窗口边缘自由调整", 20,
                new Vector2(0, yPos), new Vector2(DIVIDER_W, 32),
                TextAlignmentOptions.Left, new Color(0.78f, 0.78f, 0.78f));
            yPos -= 40f;

            // 1.2 礼物视频开关
            CreateToggleRow(panel.transform, "GiftVideo", "礼物视频", ref yPos,
                GiftVideoEnabled, val => { GiftVideoEnabled = val; });

            // 1.3 入场视频开关
            CreateToggleRow(panel.transform, "VIPVideo", "玩家入场视频", ref yPos,
                VIPVideoEnabled, val => { VIPVideoEnabled = val; });

            yPos -= SECTION_GAP;
            CreateDivider(panel.transform, yPos);
            yPos -= SECTION_GAP;

            // ═══════════════ Section 2: 音频设置 ═══════════════
            CreateSectionHeader(panel.transform, "音频设置", ref yPos);

            // 2.1 BGM开关 + 音量滑条
            var audio = AudioManager.Instance;
            bool bgmOn = audio != null ? audio.BGMEnabled : true;
            float bgmVol = audio != null ? audio.BGMVolume : 0.6f;
            CreateToggleRow(panel.transform, "BGM", "背景音乐", ref yPos,
                bgmOn, val => { if (AudioManager.Instance != null) AudioManager.Instance.BGMEnabled = val; });
            CreateSliderRow(panel.transform, "BGMVol", "音乐音量", ref yPos,
                bgmVol, val => { if (AudioManager.Instance != null) AudioManager.Instance.BGMVolume = val; });

            // 2.2 SFX开关 + 音量滑条
            bool sfxOn = audio != null ? audio.SFXEnabled : true;
            float sfxVol = audio != null ? audio.SFXVolume : 0.8f;
            CreateToggleRow(panel.transform, "SFX", "音效", ref yPos,
                sfxOn, val => { if (AudioManager.Instance != null) AudioManager.Instance.SFXEnabled = val; });
            CreateSliderRow(panel.transform, "SFXVol", "音效音量", ref yPos,
                sfxVol, val => { if (AudioManager.Instance != null) AudioManager.Instance.SFXVolume = val; });

            yPos -= SECTION_GAP;
            CreateDivider(panel.transform, yPos);
            yPos -= SECTION_GAP + 10f;

            // ═══════════════ Section 3: 操作 ═══════════════
            // 结束本局按钮
            var endBtnGo = new GameObject("BtnEndGame");
            endBtnGo.transform.SetParent(panel.transform, false);
            var endBtnRT = endBtnGo.AddComponent<RectTransform>();
            endBtnRT.anchoredPosition = new Vector2(0, yPos - 10f);
            endBtnRT.sizeDelta = new Vector2(260, 58);
            var endBtnImg = endBtnGo.AddComponent<Image>();
            endBtnImg.color = new Color(0.72f, 0.18f, 0.18f);
            var endBtn = endBtnGo.AddComponent<Button>();
            endBtn.targetGraphic = endBtnImg;
            endBtn.onClick.AddListener(ShowConfirmDialog);
            CreateTMP(endBtnGo.transform, "Text", "结束本局", 28,
                Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center, Color.white,
                FontStyles.Bold, true);
        }

        // ==================== Section Header ====================

        private void CreateSectionHeader(Transform parent, string title, ref float yPos)
        {
            CreateTMP(parent, $"Section_{title}", title, 26,
                new Vector2(LABEL_X + 60, yPos), new Vector2(300, 36),
                TextAlignmentOptions.Left, new Color(1f, 0.84f, 0f, 0.8f),
                FontStyles.Bold);
            yPos -= 40f;
        }

        // ==================== Toggle行 ====================

        private void CreateToggleRow(Transform parent, string name,
            string label, ref float yPos, bool defaultValue,
            System.Action<bool> onValueChanged)
        {
            // 标签
            CreateTMP(parent, $"{name}Label", label, 24,
                new Vector2(LABEL_X, yPos), new Vector2(300, 36),
                TextAlignmentOptions.Left, Color.white);

            // Toggle背景条
            var toggleBg = new GameObject($"{name}Bg");
            toggleBg.transform.SetParent(parent, false);
            var bgRT = toggleBg.AddComponent<RectTransform>();
            bgRT.anchoredPosition = new Vector2(TOGGLE_X, yPos);
            bgRT.sizeDelta = new Vector2(82, 38);
            var bgImg = toggleBg.AddComponent<Image>();
            bgImg.color = defaultValue ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.35f, 0.35f, 0.35f);

            // 滑块
            var handle = new GameObject("Handle");
            handle.transform.SetParent(toggleBg.transform, false);
            var handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(30, 30);
            handleRT.anchoredPosition = defaultValue ? new Vector2(20, 0) : new Vector2(-20, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            // 状态文字
            var statusGo = new GameObject($"{name}Status");
            statusGo.transform.SetParent(parent, false);
            var statusRT = statusGo.AddComponent<RectTransform>();
            statusRT.anchoredPosition = new Vector2(TOGGLE_X + 60, yPos);
            statusRT.sizeDelta = new Vector2(55, 32);
            var statusTMP = statusGo.AddComponent<TextMeshProUGUI>();
            statusTMP.text = defaultValue ? "ON" : "OFF";
            statusTMP.fontSize = 20;
            statusTMP.alignment = TextAlignmentOptions.Left;
            statusTMP.color = defaultValue ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.55f, 0.55f, 0.55f);
            if (_chineseFont != null) statusTMP.font = _chineseFont;

            bool currentValue = defaultValue;
            var btn = toggleBg.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(() =>
            {
                currentValue = !currentValue;
                onValueChanged(currentValue);
                bgImg.color = currentValue ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.35f, 0.35f, 0.35f);
                handleRT.anchoredPosition = currentValue ? new Vector2(20, 0) : new Vector2(-20, 0);
                statusTMP.text = currentValue ? "ON" : "OFF";
                statusTMP.color = currentValue ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.55f, 0.55f, 0.55f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("ui_click");
            });

            yPos -= ROW_HEIGHT;
        }

        // ==================== Slider行 ====================

        private void CreateSliderRow(Transform parent, string name,
            string label, ref float yPos, float defaultValue,
            System.Action<float> onValueChanged)
        {
            // 标签
            CreateTMP(parent, $"{name}Label", label, 22,
                new Vector2(LABEL_X, yPos), new Vector2(200, 32),
                TextAlignmentOptions.Left, new Color(0.8f, 0.8f, 0.8f));

            // 滑条底
            var sliderBg = new GameObject($"{name}SliderBg");
            sliderBg.transform.SetParent(parent, false);
            var sliderBgRT = sliderBg.AddComponent<RectTransform>();
            sliderBgRT.anchoredPosition = new Vector2(120, yPos);
            sliderBgRT.sizeDelta = new Vector2(280, 16);
            var sliderBgImg = sliderBg.AddComponent<Image>();
            sliderBgImg.color = new Color(0.25f, 0.25f, 0.3f);

            // Unity Slider
            var sliderGo = new GameObject($"{name}Slider");
            sliderGo.transform.SetParent(parent, false);
            var sliderRT = sliderGo.AddComponent<RectTransform>();
            sliderRT.anchoredPosition = new Vector2(120, yPos);
            sliderRT.sizeDelta = new Vector2(280, 32);

            // Fill area
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRT = fillArea.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(0, 8);
            fillAreaRT.offsetMax = new Vector2(0, -8);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(defaultValue, 1);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.7f, 1f, 0.8f);

            // Handle area
            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var handleAreaRT = handleArea.GetComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = Vector2.zero;
            handleAreaRT.offsetMax = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(handleArea.transform, false);
            var handleGoRT = handleGo.GetComponent<RectTransform>();
            handleGoRT.sizeDelta = new Vector2(24, 24);
            var handleGoImg = handleGo.AddComponent<Image>();
            handleGoImg.color = Color.white;

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = defaultValue;
            slider.fillRect = fillRT;
            slider.handleRect = handleGoRT;
            slider.targetGraphic = handleGoImg;
            slider.direction = Slider.Direction.LeftToRight;

            // 百分比文字
            var valGo = new GameObject($"{name}Val");
            valGo.transform.SetParent(parent, false);
            var valRT = valGo.AddComponent<RectTransform>();
            valRT.anchoredPosition = new Vector2(TOGGLE_X + 60, yPos);
            valRT.sizeDelta = new Vector2(60, 32);
            var valTMP = valGo.AddComponent<TextMeshProUGUI>();
            valTMP.text = $"{Mathf.RoundToInt(defaultValue * 100)}%";
            valTMP.fontSize = 20;
            valTMP.alignment = TextAlignmentOptions.Left;
            valTMP.color = new Color(0.8f, 0.8f, 0.8f);
            if (_chineseFont != null) valTMP.font = _chineseFont;

            slider.onValueChanged.AddListener(v =>
            {
                onValueChanged(v);
                valTMP.text = $"{Mathf.RoundToInt(v * 100)}%";
                // 动态更新fill
                fillRT.anchorMax = new Vector2(v, 1);
            });

            yPos -= ROW_HEIGHT;
        }

        // ==================== 二次确认弹窗 ====================

        private void ShowConfirmDialog()
        {
            if (_confirmDialog != null) return;

            _confirmDialog = new GameObject("ConfirmDialog");
            _confirmDialog.transform.SetParent(_panelRoot.transform, false);
            var dlgOverlayRT = _confirmDialog.AddComponent<RectTransform>();
            StretchFull(dlgOverlayRT);
            var dlgOverlayImg = _confirmDialog.AddComponent<Image>();
            dlgOverlayImg.color = new Color(0, 0, 0, 0.6f);

            var dialog = new GameObject("DialogBox");
            dialog.transform.SetParent(_confirmDialog.transform, false);
            var dialogRT = dialog.AddComponent<RectTransform>();
            dialogRT.sizeDelta = new Vector2(580, 280);
            var dialogImg = dialog.AddComponent<Image>();
            dialogImg.color = new Color(0.12f, 0.12f, 0.2f, 0.98f);
            var outline = dialog.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.84f, 0f, 0.7f);
            outline.effectDistance = new Vector2(2, 2);

            CreateTMP(dialog.transform, "WarningText",
                "是否确认结束本局\n结束后本局数据不保存", 28,
                new Vector2(0, 35), new Vector2(500, 100),
                TextAlignmentOptions.Center, Color.white);

            // 确认按钮
            var confirmGo = CreateActionButton(dialog.transform, "确认结束",
                new Vector2(-95, -80), new Color(0.75f, 0.18f, 0.18f));
            confirmGo.GetComponent<Button>().onClick.AddListener(OnConfirmEnd);

            // 取消按钮
            var cancelGo = CreateActionButton(dialog.transform, "取消",
                new Vector2(95, -80), new Color(0.25f, 0.45f, 0.7f));
            cancelGo.GetComponent<Button>().onClick.AddListener(DismissConfirmDialog);
        }

        private void OnConfirmEnd()
        {
            Debug.Log("[SettingsPanelUI] 用户确认结束本局");
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.RequestResetGame();
                gm.ResetGame();
            }
            Close();
        }

        private void DismissConfirmDialog()
        {
            if (_confirmDialog != null) { Destroy(_confirmDialog); _confirmDialog = null; }
        }

        // ==================== 工具方法 ====================

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
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
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
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(0, 0, 0, 140);
            return tmp;
        }

        private void CreateDivider(Transform parent, float yPos)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, yPos);
            rt.sizeDelta = new Vector2(DIVIDER_W, 1.5f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.4f, 0.4f, 0.45f, 0.5f);
            img.raycastTarget = false;
        }

        private void CreateCloseButton(Transform parent, Vector2 pos)
        {
            var go = new GameObject("BtnClose");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(50, 50);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.35f, 0.35f, 0.4f);  // 灰色，与红色"结束本局"区分
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Close);

            var xTMP = CreateTMP(go.transform, "X", "X", 28,  // 用普通X替代✕避免字体缺字
                Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center, Color.white,
                FontStyles.Normal, true);
        }

        private GameObject CreateActionButton(Transform parent, string text, Vector2 pos, Color bgColor)
        {
            var go = new GameObject($"Btn{text}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(170, 50);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            CreateTMP(go.transform, "Text", text, 26,
                Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Center, Color.white,
                FontStyles.Bold, true);
            return go;
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
