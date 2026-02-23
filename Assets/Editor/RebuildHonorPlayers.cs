using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 重建荣誉玩家卡片结构
/// 基于效果图：每个玩家卡片竖向排列
///   排名数字 → 圆形头像框 → 玩家名 → 推力值 → 连胜数
/// 左侧: 3 2 1 从左到右
/// 右侧: 1 2 3 从左到右
///
/// 效果图坐标(1080x1920):
/// 左侧3人区域: x≈24~270, y≈330~460
/// 右侧3人区域: x≈810~1056, y≈330~460
/// 每个卡片宽约90px, 高约130px
/// </summary>
public class RebuildHonorPlayers
{
    private const string ART_PATH = "Assets/Art/BattleUI/";
    private const float CARD_WIDTH = 100f;
    private const float CARD_HEIGHT = 140f;
    private const float CARD_SPACING = 5f;
    private const float AVATAR_SIZE = 65f;

    public static string Execute()
    {
        int count = 0;
        var log = new System.Text.StringBuilder();

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return "ERROR: Canvas not found";
        var gameUI = canvas.transform.Find("GameUIPanel");
        if (gameUI == null) return "ERROR: GameUIPanel not found";

        // 获取中文字体
        var fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/Fonts/ChineseFont SDF.asset");

        // ==========================================
        // 左侧: 清除旧的 LeftPlayerList 内容，重建
        // ==========================================
        var leftList = gameUI.Find("LeftPlayerList");
        if (leftList != null)
        {
            // 清除所有子对象
            for (int i = leftList.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(leftList.GetChild(i).gameObject);

            // 设置容器属性
            var listRect = leftList.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0, 1);
            listRect.anchorMax = new Vector2(0, 1);
            listRect.pivot = new Vector2(0, 1);
            listRect.sizeDelta = new Vector2(330, 150);
            listRect.anchoredPosition = new Vector2(15, -330);

            var listImg = leftList.GetComponent<Image>();
            if (listImg != null)
            {
                listImg.color = new Color(0, 0, 0, 0); // 透明容器
                listImg.sprite = null;
            }

            // 添加水平布局
            var hlg = leftList.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = leftList.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = CARD_SPACING;
            hlg.childAlignment = TextAnchor.UpperCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.padding = new RectOffset(5, 5, 0, 0);

            // 左侧排列: 3, 2, 1 (从左到右)
            int[] leftRanks = { 3, 2, 1 };
            for (int i = 0; i < 3; i++)
            {
                BuildPlayerCard(leftList, $"PlayerRow_{i}", leftRanks[i], true, fontAsset);
                count++;
            }

            EditorUtility.SetDirty(leftList);
            log.AppendLine($"✅ 左侧荣誉玩家 3个卡片重建完成");
        }

        // ==========================================
        // 右侧: 清除旧的 RightPlayerList 内容，重建
        // ==========================================
        var rightList = gameUI.Find("RightPlayerList");
        if (rightList != null)
        {
            for (int i = rightList.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(rightList.GetChild(i).gameObject);

            var listRect = rightList.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(1, 1);
            listRect.anchorMax = new Vector2(1, 1);
            listRect.pivot = new Vector2(1, 1);
            listRect.sizeDelta = new Vector2(330, 150);
            listRect.anchoredPosition = new Vector2(-15, -330);

            var listImg = rightList.GetComponent<Image>();
            if (listImg != null)
            {
                listImg.color = new Color(0, 0, 0, 0);
                listImg.sprite = null;
            }

            var hlg = rightList.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = rightList.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = CARD_SPACING;
            hlg.childAlignment = TextAnchor.UpperCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.padding = new RectOffset(5, 5, 0, 0);

            // 右侧排列: 1, 2, 3 (从左到右)
            int[] rightRanks = { 1, 2, 3 };
            for (int i = 0; i < 3; i++)
            {
                BuildPlayerCard(rightList, $"PlayerRow_{i}", rightRanks[i], false, fontAsset);
                count++;
            }

            EditorUtility.SetDirty(rightList);
            log.AppendLine($"✅ 右侧荣誉玩家 3个卡片重建完成");
        }

        // 标记场景脏
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        log.Insert(0, $"=== 荣誉玩家卡片重建完成 === 共 {count} 个卡片\n\n");
        return log.ToString();
    }

    /// <summary>
    /// 构建一个玩家卡片:
    ///   排名数字
    ///   圆形头像框 (avatar_frame_left/right.png)
    ///   头像图片 (PlayerAvatar)
    ///   玩家名
    ///   推力值
    ///   连胜数
    /// </summary>
    private static void BuildPlayerCard(Transform parent, string name, int rank, bool isLeft, TMP_FontAsset font)
    {
        var card = new GameObject(name);
        card.transform.SetParent(parent, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(CARD_WIDTH, CARD_HEIGHT);

        // 垂直布局
        var vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 1;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;

        // --- 排名数字 ---
        var rankGo = CreateTMP(card.transform, "RankText", $"{rank}",
            font, 18, FontStyles.Bold, Color.white, 40, 20);

        // --- 头像框容器 (含头像+边框) ---
        var avatarContainer = new GameObject("AvatarContainer");
        avatarContainer.transform.SetParent(card.transform, false);
        var acRect = avatarContainer.AddComponent<RectTransform>();
        acRect.sizeDelta = new Vector2(AVATAR_SIZE, AVATAR_SIZE);
        var acLayout = avatarContainer.AddComponent<LayoutElement>();
        acLayout.preferredWidth = AVATAR_SIZE;
        acLayout.preferredHeight = AVATAR_SIZE;

        // 头像图片 (实际头像，运行时加载)
        var avatarGo = new GameObject("PlayerAvatar");
        avatarGo.transform.SetParent(avatarContainer.transform, false);
        var avatarRect = avatarGo.AddComponent<RectTransform>();
        avatarRect.anchorMin = new Vector2(0.15f, 0.15f);
        avatarRect.anchorMax = new Vector2(0.85f, 0.85f);
        avatarRect.sizeDelta = Vector2.zero;
        avatarRect.anchoredPosition = Vector2.zero;
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.color = new Color(0.5f, 0.5f, 0.5f, 0.4f); // 默认灰色占位
        avatarImg.preserveAspect = true;

        // 头像边框 (avatar_frame_left/right.png 覆盖在上面)
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

        // --- 玩家名 ---
        var nameGo = CreateTMP(card.transform, "PlayerName", "玩家名",
            font, 14, FontStyles.Normal, Color.white, 95, 18);

        // --- 推力值 ---
        var forceGo = CreateTMP(card.transform, "PlayerForce", "推力:0",
            font, 12, FontStyles.Normal, new Color(1f, 0.85f, 0.4f), 95, 16);

        // --- 连胜数 ---
        var streakGo = CreateTMP(card.transform, "PlayerStreak", "连胜:0",
            font, 12, FontStyles.Normal, new Color(0.9f, 0.5f, 0.2f), 95, 16);
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
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = new Color32(0, 0, 0, 160);
        if (font != null) tmp.font = font;

        return go;
    }
}
