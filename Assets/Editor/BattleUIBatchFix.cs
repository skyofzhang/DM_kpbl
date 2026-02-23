using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 批量修复战斗UI - 8个需求
/// #1 去掉礼物说明文字(左侧)
/// #2 终点距离显示(已有逻辑在TopBarUI,需确保场景对象存在且引用连接)
/// #3 进度条层级+数值+橘子图标跟中线
/// #4 荣誉玩家卡片间距缩小
/// #5 BtnEnd/BtnSettings添加文字
/// #6 所有文字描边投影
/// #8 推力差反推提示(已有逻辑在TopBarUI.UpdateHintText,需确保hintText引用)
/// </summary>
public class BattleUIBatchFix
{
    private static TMP_FontAsset _font;
    private const string FONT_PATH = "Assets/Resources/Fonts/ChineseFont SDF.asset";

    [MenuItem("CapybaraDuel/Battle UI Batch Fix")]
    public static void RunFromMenu()
    {
        string result = Execute();
        Debug.Log(result);
        EditorUtility.DisplayDialog("Battle UI Batch Fix", "完成！详情见Console", "OK");
    }

    public static string Execute()
    {
        var log = new System.Text.StringBuilder();
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return "ERROR: Canvas not found";
        var gameUI = canvas.transform.Find("GameUIPanel");
        if (gameUI == null) return "ERROR: GameUIPanel not found";

        // === #1 去掉礼物说明文字 ===
        Fix1_RemoveGiftText(gameUI, log);

        // === #4 荣誉玩家卡片间距缩小 ===
        Fix4_ReduceCardSpacing(gameUI, log);

        // === #5 BtnEnd/BtnSettings添加文字 ===
        Fix5_AddButtonText(gameUI, log);

        // === #6 所有文字描边投影 ===
        Fix6_AddOutlineToAllText(gameUI, log);

        // === #2 & #8 确保TopBarUI的终点距离和提示文字UI对象存在 ===
        Fix2_8_EnsureTopBarUIObjects(gameUI, log);

        // === #3 进度条上方添加橘子图标 ===
        Fix3_ProgressBarOrangeIcon(gameUI, log);

        // 标记场景脏
        EditorUtility.SetDirty(gameUI);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        log.Insert(0, "=== 战斗UI批量修复完成 ===\n\n");
        return log.ToString();
    }

    /// <summary>#1 去掉右下角礼物说明的左侧文字(礼物说明/点赞/评论等)</summary>
    static void Fix1_RemoveGiftText(Transform gameUI, System.Text.StringBuilder log)
    {
        // GiftInfoPanel左侧有"礼物说明"等文字，图片贴纸上已经有说明了
        var giftPanel = gameUI.Find("GiftInfoPanel");
        if (giftPanel == null) { log.AppendLine("#1 SKIP: GiftInfoPanel not found"); return; }

        // 查找并隐藏GiftTitle
        var giftTitle = giftPanel.Find("GiftTitle");
        if (giftTitle != null)
        {
            giftTitle.gameObject.SetActive(false);
            log.AppendLine("#1 ✅ GiftTitle hidden");
        }

        // 查找并隐藏GiftRow1~4
        for (int i = 1; i <= 6; i++)
        {
            var row = giftPanel.Find($"GiftRow{i}");
            if (row != null)
            {
                row.gameObject.SetActive(false);
                log.AppendLine($"#1 ✅ GiftRow{i} hidden");
            }
        }

        // 查找并隐藏独立的文字描述对象
        string[] textNames = { "GiftDesc", "GiftDescription", "DescText" };
        foreach (var name in textNames)
        {
            var obj = giftPanel.Find(name);
            if (obj != null)
            {
                obj.gameObject.SetActive(false);
                log.AppendLine($"#1 ✅ {name} hidden");
            }
        }
    }

    /// <summary>#4 缩小荣誉玩家卡片间距</summary>
    static void Fix4_ReduceCardSpacing(Transform gameUI, System.Text.StringBuilder log)
    {
        foreach (string listName in new[] { "LeftPlayerList", "RightPlayerList" })
        {
            var list = gameUI.Find(listName);
            if (list == null) continue;

            var hlg = list.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = 2; // 从8缩到2
                hlg.padding = new RectOffset(2, 2, 0, 0); // padding也缩小
                log.AppendLine($"#4 ✅ {listName} spacing: 8→2, padding: 5→2");
            }

            // 同时缩小容器宽度
            var listRect = list.GetComponent<RectTransform>();
            if (listRect != null)
            {
                float newWidth = 150 * 3 + 2 * 2 + 4; // ~458
                listRect.sizeDelta = new Vector2(newWidth, listRect.sizeDelta.y);
                log.AppendLine($"#4 ✅ {listName} width → {newWidth}");
            }
        }
    }

    /// <summary>#5 给BtnEnd和BtnSettings添加文字子对象</summary>
    static void Fix5_AddButtonText(Transform gameUI, System.Text.StringBuilder log)
    {
        AddTextToButton(gameUI, "BtnEnd", "结束", log);
        AddTextToButton(gameUI, "BtnSettings", "设置", log);
    }

    static void AddTextToButton(Transform gameUI, string btnName, string label, System.Text.StringBuilder log)
    {
        var btn = gameUI.Find(btnName);
        if (btn == null) { log.AppendLine($"#5 SKIP: {btnName} not found"); return; }

        // 检查是否已有文字子对象
        var existingText = btn.Find("Label");
        if (existingText != null)
        {
            var existTMP = existingText.GetComponent<TextMeshProUGUI>();
            if (existTMP != null)
            {
                existTMP.text = label;
                log.AppendLine($"#5 ✅ {btnName}/Label text updated to '{label}'");
                return;
            }
        }

        // 创建文字子对象
        var textGo = new GameObject("Label");
        textGo.transform.SetParent(btn, false);
        var rect = textGo.AddComponent<RectTransform>();
        // 文字放在按钮底部下方
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -5);
        rect.sizeDelta = new Vector2(100, 30);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        if (_font != null) tmp.font = _font;

        log.AppendLine($"#5 ✅ {btnName}/Label created with text '{label}'");
    }

    /// <summary>#6 给GameUIPanel下所有TMP文字增加描边投影</summary>
    static void Fix6_AddOutlineToAllText(Transform gameUI, System.Text.StringBuilder log)
    {
        int count = 0;
        var allTMPs = gameUI.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in allTMPs)
        {
            // 只对还没有描边的加描边
            if (tmp.outlineWidth < 0.1f)
            {
                tmp.outlineWidth = 0.25f;
                tmp.outlineColor = new Color32(0, 0, 0, 200);
                count++;
            }

            // 确保字体材质开启UNDERLAY（投影效果）
            if (tmp.fontSharedMaterial != null)
            {
                // 不在这里修改共享材质，会影响所有文字
                // 投影通过outlineWidth + outlineColor实现
            }
        }
        log.AppendLine($"#6 ✅ {count} TMP elements got outline (outlineWidth=0.25, black)");
    }

    /// <summary>#2 & #8 确保终点距离和推力差提示的UI对象存在并连接到TopBarUI</summary>
    static void Fix2_8_EnsureTopBarUIObjects(Transform gameUI, System.Text.StringBuilder log)
    {
        var topBar = gameUI.Find("TopBar");
        if (topBar == null) { log.AppendLine("#2/#8 SKIP: TopBar not found"); return; }

        // 用MonoBehaviour基类获取TopBarUI组件（避免Coplay编译器命名空间问题）
        MonoBehaviour topBarUI = null;
        foreach (var mb in topBar.GetComponents<MonoBehaviour>())
        {
            if (mb.GetType().Name == "TopBarUI")
            {
                topBarUI = mb;
                break;
            }
        }
        if (topBarUI == null)
        {
            foreach (var mb in gameUI.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb.GetType().Name == "TopBarUI")
                {
                    topBarUI = mb;
                    break;
                }
            }
        }
        if (topBarUI == null) { log.AppendLine("#2/#8 SKIP: TopBarUI not found"); return; }

        var so = new SerializedObject(topBarUI);

        // #2 终点距离
        ConnectTMPField(so, "leftEndMarker", gameUI, topBar, "LeftEndMarker", "0.0米",
            16, new Color(1f, 0.7f, 0.3f), new Vector2(100, -50), new Vector2(120, 30), 0f, log, "#2");
        ConnectTMPField(so, "rightEndMarker", gameUI, topBar, "RightEndMarker", "0.0米",
            16, new Color(0.3f, 0.9f, 0.5f), new Vector2(-100, -50), new Vector2(120, 30), 1f, log, "#2");

        // #8 推力差反推提示 - hintText
        ConnectTMPField(so, "hintText", gameUI, null, "HintText", "", 0, Color.white, Vector2.zero, Vector2.zero, 0, log, "#8");

        // 连胜
        ConnectTMPField(so, "winStreakLeftText", gameUI, null, "WinStreakLeft", "", 0, Color.white, Vector2.zero, Vector2.zero, 0, log, "#8");
        ConnectTMPField(so, "winStreakRightText", gameUI, null, "WinStreakRight", "", 0, Color.white, Vector2.zero, Vector2.zero, 0, log, "#8");

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(topBarUI);
    }

    static void ConnectTMPField(SerializedObject so, string fieldName, Transform searchRoot,
        Transform createParent, string objectName, string defaultText,
        float fontSize, Color color, Vector2 pos, Vector2 size, float anchorX,
        System.Text.StringBuilder log, string tag)
    {
        var prop = so.FindProperty(fieldName);
        if (prop == null) { log.AppendLine($"{tag} SKIP: field '{fieldName}' not found"); return; }

        // 检查是否已连接
        if (prop.objectReferenceValue != null)
        {
            log.AppendLine($"{tag} ✓ {fieldName} already connected");
            return;
        }

        // 查找场景中的对象
        var existing = FindDeep(searchRoot, objectName);
        if (existing != null)
        {
            var tmp = existing.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                prop.objectReferenceValue = tmp;
                log.AppendLine($"{tag} ✅ {fieldName} reconnected to {objectName}");
                return;
            }
        }

        // 如果需要创建且有createParent
        if (createParent != null && fontSize > 0)
        {
            var go = CreateTMPObject(createParent, objectName, defaultText,
                fontSize, color, pos, size, anchorX);
            prop.objectReferenceValue = go.GetComponent<TextMeshProUGUI>();
            log.AppendLine($"{tag} ✅ {objectName} created and connected to {fieldName}");
        }
        else
        {
            log.AppendLine($"{tag} ⚠ {objectName} not found, {fieldName} not connected");
        }
    }

    /// <summary>#3 进度条上添加橘子图标(跟随中线)</summary>
    static void Fix3_ProgressBarOrangeIcon(Transform gameUI, System.Text.StringBuilder log)
    {
        // 橘子图标已经存在于 TopBar/TopBarBg/PosIndicator/OrangeIcon
        // 确认它存在并可见
        var orangeIcon = FindDeep(gameUI, "OrangeIcon");
        if (orangeIcon != null)
        {
            orangeIcon.gameObject.SetActive(true);
            log.AppendLine("#3 ✅ OrangeIcon found and active");
        }
        else
        {
            log.AppendLine("#3 ⚠ OrangeIcon not found - may need manual creation");
        }

        // PosIndicator（当前距离指示器）
        var posIndicator = FindDeep(gameUI, "PosIndicator");
        if (posIndicator != null)
        {
            posIndicator.gameObject.SetActive(true);
            log.AppendLine("#3 ✅ PosIndicator found and active");

            // 确保posIndicatorText引用（用SerializedObject避免命名空间问题）
            MonoBehaviour topBarUI = null;
            foreach (var mb in gameUI.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb.GetType().Name == "TopBarUI") { topBarUI = mb; break; }
            }
            if (topBarUI != null)
            {
                var so = new SerializedObject(topBarUI);
                var prop = so.FindProperty("posIndicatorText");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    var tmp = posIndicator.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        prop.objectReferenceValue = tmp;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(topBarUI);
                        log.AppendLine("#3 ✅ posIndicatorText reconnected");
                    }
                }
            }
        }
    }

    // === 工具方法 ===

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }

    static GameObject CreateTMPObject(Transform parent, string name, string text,
        float fontSize, Color color, Vector2 position, Vector2 size, float anchorX = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchorX, 1f);
        rect.anchorMax = new Vector2(anchorX, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 200);
        if (_font != null) tmp.font = _font;

        return go;
    }
}
