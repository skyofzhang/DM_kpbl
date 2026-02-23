using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 重建玩家数据面板 — 固定坐标布局，确保列宽精确、对齐整齐
/// 分辨率: 1080x1920
///
/// 行内列布局（固定anchoredPosition，不使用LayoutGroup）:
///   序号(40) | 色条(6) | 头像(30) | 名字(150) | 贡献(90) | 周榜(75) | 月榜(75) | 连胜(90) | 时榜(75) | 胜点(70)
///   总宽 = 701px, 左偏移=30px, 配合1080px宽度
/// </summary>
public class CreatePlayerDataPanel
{
    // 列定义：name, x偏移, 宽度, 对齐
    struct ColDef
    {
        public string name;
        public float x;
        public float w;
        public TextAlignmentOptions align;
        public string headerText;
        public bool isImage;

        public ColDef(string n, float xx, float ww, TextAlignmentOptions a, string h, bool img = false)
        {
            name = n; x = xx; w = ww; align = a; headerText = h; isImage = img;
        }
    }

    static readonly float ROW_LEFT = 30f;  // 行内容区域左起点
    static readonly ColDef[] COLS = new ColDef[]
    {
        new ColDef("RankText",        0,   40, TextAlignmentOptions.Center,  "#",     false),
        new ColDef("CampIndicator",  42,    6, TextAlignmentOptions.Center,  "",      true),
        new ColDef("AvatarImg",      52,   30, TextAlignmentOptions.Center,  "",      true),
        new ColDef("NameText",       88,  150, TextAlignmentOptions.Left,    "名字",   false),
        new ColDef("ContribText",   242,   90, TextAlignmentOptions.Center,  "贡献",   false),
        new ColDef("WeeklyRankText",336,   75, TextAlignmentOptions.Center,  "周榜",   false),
        new ColDef("MonthlyRankText",415,  75, TextAlignmentOptions.Center,  "月榜",   false),
        new ColDef("StreakRankText", 494,  90, TextAlignmentOptions.Center,  "连胜",   false),
        new ColDef("HourlyRankText", 588,  75, TextAlignmentOptions.Center,  "时榜",   false),
        new ColDef("SPText",        667,   70, TextAlignmentOptions.Center,  "胜点",   false),
    };

    public static void Execute()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("Canvas not found!"); return; }

        // 删除旧面板 + 旧Canvas上的组件
        var existingComp = canvas.GetComponent<CapybaraDuel.UI.PlayerDataPanelUI>();
        if (existingComp != null)
            Undo.DestroyObjectImmediate(existingComp);

        var existing = canvas.transform.Find("PlayerDataPanel");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        var chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");

        // ==================== Root Panel (全屏, 初始inactive) ====================
        var panel = CreateGO("PlayerDataPanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        panel.SetActive(false);

        // ==================== BG (半透明黑底) ====================
        var bg = CreateGO("BG", panel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.88f);
        bgImg.raycastTarget = true;

        // ==================== TitleText ====================
        var title = CreateTMP("TitleText", panel.transform, chineseFont, "参战玩家数据",
            30, TextAlignmentOptions.Center, Color.white);
        SetRT(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            new Vector2(0, -15f), new Vector2(0, 45));

        // ==================== SubtitleText (阵营人数) ====================
        var subtitle = CreateTMP("SubtitleText", panel.transform, chineseFont, "",
            20, TextAlignmentOptions.Center, Color.white);
        SetRT(subtitle, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            new Vector2(0, -58f), new Vector2(0, 30));
        subtitle.GetComponent<TextMeshProUGUI>().richText = true;

        // ==================== 分隔线 ====================
        var divider1 = CreateGO("Divider1", panel.transform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -90f), new Vector2(-40, 1));
        divider1.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        var divImg1 = divider1.AddComponent<Image>();
        divImg1.color = new Color(1f, 0.85f, 0.4f, 0.4f);
        divImg1.raycastTarget = false;

        // ==================== HeaderRow (列标题) ====================
        var header = CreateGO("HeaderRow", panel.transform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -95f), new Vector2(0, 28));
        header.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);

        // 表头背景
        var headerBg = header.AddComponent<Image>();
        headerBg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        headerBg.raycastTarget = false;

        // 创建表头列
        foreach (var col in COLS)
        {
            if (col.isImage || string.IsNullOrEmpty(col.headerText)) continue;
            var hdr = CreateTMP("H_" + col.name, header.transform, chineseFont, col.headerText,
                14, col.align, new Color(1f, 0.85f, 0.4f, 1f));
            var hrt = hdr.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 0);
            hrt.anchorMax = new Vector2(0, 1);
            hrt.pivot = new Vector2(0, 0.5f);
            hrt.anchoredPosition = new Vector2(ROW_LEFT + col.x, 0);
            hrt.sizeDelta = new Vector2(col.w, 0);
        }

        // ==================== 分隔线2 ====================
        var divider2 = CreateGO("Divider2", panel.transform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -123f), new Vector2(-40, 1));
        divider2.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        var divImg2 = divider2.AddComponent<Image>();
        divImg2.color = new Color(1f, 0.85f, 0.4f, 0.3f);
        divImg2.raycastTarget = false;

        // ==================== ScrollView ====================
        var scrollView = CreateGO("ScrollView", panel.transform, new Vector2(0, 0), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);
        var svRT = scrollView.GetComponent<RectTransform>();
        svRT.offsetMin = new Vector2(0, 55);    // 底部留空给关闭按钮
        svRT.offsetMax = new Vector2(0, -126);   // 顶部留空给标题+表头
        var scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Viewport
        var viewport = CreateGO("Viewport", scrollView.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        var vpRT = viewport.GetComponent<RectTransform>();
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1, 1, 1, 0.003f);
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scrollRect.viewport = vpRT;

        // Content
        var content = CreateGO("Content", viewport.transform, new Vector2(0, 1), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.pivot = new Vector2(0.5f, 1f);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 1f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRT;

        // ==================== 50 PlayerRows ====================
        float rowH = 34f;
        for (int i = 0; i < 50; i++)
            CreatePlayerRow(i, content.transform, chineseFont, rowH);

        // ==================== BtnClose ====================
        var btnCloseGO = CreateGO("BtnClose", panel.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 10), new Vector2(160, 40));
        btnCloseGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
        var btnImg = btnCloseGO.AddComponent<Image>();
        btnImg.color = new Color(0.7f, 0.15f, 0.15f, 1f);
        btnCloseGO.AddComponent<Button>();

        var btnText = CreateTMP("Text", btnCloseGO.transform, chineseFont, "关闭",
            20, TextAlignmentOptions.Center, Color.white);
        SetRT(btnText, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        // ==================== 挂载 PlayerDataPanelUI 到 Canvas ====================
        var panelUI = canvas.AddComponent<CapybaraDuel.UI.PlayerDataPanelUI>();

        var btnPlayerData = GameObject.Find("Canvas/BottomBar/BtnPlayerData");
        if (btnPlayerData != null)
            panelUI.btnOpen = btnPlayerData.GetComponent<Button>();

        panelUI.btnClose = btnCloseGO.GetComponent<Button>();
        panelUI.panelRoot = panel;
        panelUI.titleText = title.GetComponent<TextMeshProUGUI>();
        panelUI.subtitleText = subtitle.GetComponent<TextMeshProUGUI>();
        panelUI.contentParent = content.transform;

        // 标记场景已修改
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("PlayerDataPanel V2 创建成功！50行 × 10列，固定坐标布局");
    }

    // ==================== 创建单个玩家行 ====================
    static void CreatePlayerRow(int index, Transform parent, TMP_FontAsset font, float height)
    {
        var row = new GameObject($"PlayerRow_{index}", typeof(RectTransform), typeof(CanvasRenderer));
        row.transform.SetParent(parent, false);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0, height);

        // 行底色（初始灰色，运行时按阵营动态改色）
        var rowImg = row.AddComponent<Image>();
        rowImg.color = (index % 2 == 0)
            ? new Color(0.15f, 0.15f, 0.15f, 0.5f)
            : new Color(0.2f, 0.2f, 0.2f, 0.5f);
        rowImg.raycastTarget = false;

        // 创建每列子元素（固定anchoredPosition定位）
        foreach (var col in COLS)
        {
            if (col.isImage)
            {
                // 图片元素(色条/头像)
                var imgGO = new GameObject(col.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imgGO.transform.SetParent(row.transform, false);
                var imgRT = imgGO.GetComponent<RectTransform>();
                imgRT.anchorMin = new Vector2(0, 0);
                imgRT.anchorMax = new Vector2(0, 1);
                imgRT.pivot = new Vector2(0, 0.5f);
                imgRT.anchoredPosition = new Vector2(ROW_LEFT + col.x, 0);
                imgRT.sizeDelta = new Vector2(col.w, col.name == "CampIndicator" ? 0 : -4); // 色条满高，头像留边
                var img = imgGO.GetComponent<Image>();
                img.color = col.name == "CampIndicator"
                    ? new Color(0.5f, 0.5f, 0.5f, 1f)
                    : new Color(0.3f, 0.3f, 0.3f, 1f);
                img.raycastTarget = false;
            }
            else
            {
                // TMP文字元素
                var txtGO = new GameObject(col.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtGO.transform.SetParent(row.transform, false);
                var txtRT = txtGO.GetComponent<RectTransform>();
                txtRT.anchorMin = new Vector2(0, 0);
                txtRT.anchorMax = new Vector2(0, 1);
                txtRT.pivot = new Vector2(0, 0.5f);
                txtRT.anchoredPosition = new Vector2(ROW_LEFT + col.x, 0);
                txtRT.sizeDelta = new Vector2(col.w, 0);
                var tmp = txtGO.GetComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.fontSize = col.name == "RankText" ? 13 : 14;
                tmp.alignment = col.align;
                tmp.color = col.name == "RankText"
                    ? new Color(0.7f, 0.7f, 0.7f, 1f)  // 序号灰色
                    : Color.white;
                tmp.text = "";
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.enableWordWrapping = false;
            }
        }

        row.SetActive(false); // 默认隐藏
    }

    // ==================== 工具方法 ====================

    static GameObject CreateGO(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return go;
    }

    static void SetRT(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    static GameObject CreateTMP(string name, Transform parent, TMP_FontAsset font, string text,
        int fontSize, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return go;
    }
}
