using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 修复荣誉玩家卡片 - 基于效果图精确分析
/// 效果图(1080x1920)中:
///   每个卡片约150宽x220高
///   头像框约85px
///   排名数字24号粗体
///   玩家名16号
///   推力/连胜14号
///   左侧排列: 3 2 1 (从左到右)
///   右侧排列: 1 2 3 (从左到右)
///   整体区域在进度条下方，y约380~600
/// </summary>
public class FixHonorPlayerCards
{
    private const float CARD_WIDTH = 150f;
    private const float CARD_HEIGHT = 270f; // ChineseFont SDF行高比例约1.4x，需要足够空间
    private const float CARD_SPACING = 8f;
    private const float AVATAR_SIZE = 85f;
    private const string ART_PATH = "Assets/Art/BattleUI/";

    public static string Execute()
    {
        var log = new System.Text.StringBuilder();

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return "ERROR: Canvas not found";
        var gameUI = canvas.transform.Find("GameUIPanel");
        if (gameUI == null) return "ERROR: GameUIPanel not found";

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/Fonts/ChineseFont SDF.asset");

        // ===== 左侧 =====
        var leftList = gameUI.Find("LeftPlayerList");
        if (leftList != null)
        {
            RebuildSide(leftList, true, fontAsset, log);
            EditorUtility.SetDirty(leftList);
        }

        // ===== 右侧 =====
        var rightList = gameUI.Find("RightPlayerList");
        if (rightList != null)
        {
            RebuildSide(rightList, false, fontAsset, log);
            EditorUtility.SetDirty(rightList);
        }

        // 标记场景脏
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        log.Insert(0, "=== 荣誉玩家卡片修复完成 ===\n\n");
        return log.ToString();
    }

    private static void RebuildSide(Transform listContainer, bool isLeft, TMP_FontAsset font, System.Text.StringBuilder log)
    {
        string sideName = isLeft ? "左侧" : "右侧";

        // 清除所有子对象
        for (int i = listContainer.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(listContainer.GetChild(i).gameObject);

        // 设置容器大小: 3张卡片 + 间距 + padding
        float totalWidth = CARD_WIDTH * 3 + CARD_SPACING * 2 + 20; // ~484
        float totalHeight = CARD_HEIGHT + 10; // ~280

        var listRect = listContainer.GetComponent<RectTransform>();
        // 位置保持当前的（用户已手动调过）,只调sizeDelta
        listRect.sizeDelta = new Vector2(totalWidth, totalHeight);

        // 透明容器
        var listImg = listContainer.GetComponent<Image>();
        if (listImg != null)
        {
            listImg.color = new Color(0, 0, 0, 0);
            listImg.sprite = null;
        }

        // 水平布局
        var hlg = listContainer.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = listContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = CARD_SPACING;
        hlg.childAlignment = TextAnchor.UpperCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.padding = new RectOffset(5, 5, 0, 0);

        // 排名顺序
        int[] ranks = isLeft ? new int[] { 3, 2, 1 } : new int[] { 1, 2, 3 };

        for (int i = 0; i < 3; i++)
        {
            BuildCard(listContainer, $"PlayerRow_{i}", ranks[i], isLeft, font);
        }

        log.AppendLine($"✅ {sideName} 3个荣誉玩家卡片重建完成 (卡片尺寸: {CARD_WIDTH}x{CARD_HEIGHT})");
    }

    private static void BuildCard(Transform parent, string name, int rank, bool isLeft, TMP_FontAsset font)
    {
        var card = new GameObject(name);
        card.transform.SetParent(parent, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(CARD_WIDTH, CARD_HEIGHT);

        // 可选: 给卡片加个半透明背景
        // var cardBg = card.AddComponent<Image>();
        // cardBg.color = new Color(0, 0, 0, 0.15f);

        // 垂直布局
        var vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.padding = new RectOffset(0, 0, 2, 2);

        // --- 排名数字 (大号粗体) ---
        // ChineseFont SDF: fontSize=24 需要约36px高度 (lineHeight ratio ~1.5)
        CreateTMP(card.transform, "RankText", $"{rank}",
            font, 24, FontStyles.Bold, Color.white, 60, 36);

        // --- 头像框容器 ---
        var avatarContainer = new GameObject("AvatarContainer");
        avatarContainer.transform.SetParent(card.transform, false);
        var acRect = avatarContainer.AddComponent<RectTransform>();
        acRect.sizeDelta = new Vector2(AVATAR_SIZE, AVATAR_SIZE);
        var acLayout = avatarContainer.AddComponent<LayoutElement>();
        acLayout.preferredWidth = AVATAR_SIZE;
        acLayout.preferredHeight = AVATAR_SIZE;

        // 头像图片 (运行时加载)
        var avatarGo = new GameObject("PlayerAvatar");
        avatarGo.transform.SetParent(avatarContainer.transform, false);
        var avatarRect = avatarGo.AddComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0.12f, 0.12f);
        avatarRect.anchorMax = new Vector2(0.88f, 0.88f);
        avatarRect.sizeDelta = Vector2.zero;
        avatarRect.anchoredPosition = Vector2.zero;
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        avatarImg.preserveAspect = true;

        // 头像边框
        var frameName = isLeft ? "avatar_frame_left.png" : "avatar_frame_right.png";
        var frameGo = new GameObject("AvatarFrame");
        frameGo.transform.SetParent(avatarContainer.transform, false);
        var frameRect = frameGo.AddComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.sizeDelta = Vector2.zero;
        frameRect.anchoredPosition = Vector2.zero;
        var frameImg = frameGo.AddComponent<Image>();
        var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ART_PATH + frameName);
        if (frameSprite != null)
        {
            frameImg.sprite = frameSprite;
            frameImg.color = Color.white;
            frameImg.preserveAspect = true;
            frameImg.raycastTarget = false;
        }

        // --- 玩家名 (白色) ---
        // ChineseFont SDF: fontSize=18 需要约28px高度
        CreateTMP(card.transform, "PlayerName", "玩家名",
            font, 18, FontStyles.Normal, Color.white, CARD_WIDTH, 28);

        // --- 推力值 (黄色) ---
        // ChineseFont SDF: fontSize=15 需要约24px高度
        CreateTMP(card.transform, "PlayerForce", "推力:0",
            font, 15, FontStyles.Normal, new Color(1f, 0.85f, 0.4f), CARD_WIDTH, 24);

        // --- 连胜数 (橙色) ---
        CreateTMP(card.transform, "PlayerStreak", "连胜:0",
            font, 15, FontStyles.Normal, new Color(0.9f, 0.5f, 0.2f), CARD_WIDTH, 24);
    }

    private static GameObject CreateTMP(Transform parent, string name, string text,
        TMP_FontAsset font, float fontSize, FontStyles style, Color color, float width, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 180);
        if (font != null) tmp.font = font;

        return go;
    }
}
