using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// #45 荣誉玩家区域重叠修复
/// 1. 统一所有PlayerRow高度为220（匹配内容高度：RankText+Avatar+Name+Force+Streak）
/// 2. 底图改为Filled/Sliced模式铺满卡片（不用preserveAspect避免空白）
/// 3. 容器高度调整匹配卡片
/// </summary>
public class BattleUIFix45 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix45 - Card Overlap Fix")]
    public static void Execute()
    {
        int fixCount = 0;

        // 加载底图
        string leftBgPath = "Assets/Art/BattleUI/player_card_left_bg.png";
        string rightBgPath = "Assets/Art/BattleUI/player_card_right_bg.png";
        var leftSprite = AssetDatabase.LoadAssetAtPath<Sprite>(leftBgPath);
        var rightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(rightBgPath);

        float cardHeight = 220f; // 内容实际需要~215，留5px余量
        float cardWidth = 150f;

        // === 左侧卡片 ===
        for (int i = 0; i < 3; i++)
        {
            var path = $"Canvas/GameUIPanel/LeftPlayerList/PlayerRow_{i}";
            var go = GameObject.Find(path);
            if (go == null) continue;

            // 修复卡片尺寸
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Fix45 card size");
                rt.sizeDelta = new Vector2(cardWidth, cardHeight);
                Debug.Log($"[Fix45] {go.name} (Left): sizeDelta → {cardWidth}×{cardHeight}");
                fixCount++;
            }

            // 修复底图：铺满卡片，不保持比例
            var img = go.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Fix45 left bg mode");
                if (leftSprite != null) img.sprite = leftSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false; // 铺满卡片，允许轻微拉伸
                img.raycastTarget = false;
                img.color = Color.white;
            }
        }

        // === 右侧卡片 ===
        for (int i = 0; i < 3; i++)
        {
            var path = $"Canvas/GameUIPanel/RightPlayerList/PlayerRow_{i}";
            var go = GameObject.Find(path);
            if (go == null) continue;

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Fix45 card size");
                rt.sizeDelta = new Vector2(cardWidth, cardHeight);
                Debug.Log($"[Fix45] {go.name} (Right): sizeDelta → {cardWidth}×{cardHeight}");
                fixCount++;
            }

            var img = go.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Fix45 right bg mode");
                if (rightSprite != null) img.sprite = rightSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.raycastTarget = false;
                img.color = Color.white;
            }
        }

        // === 容器高度调整 ===
        // 容器高度应略大于卡片高度（横排排列，只需匹配最高元素）
        float containerHeight = cardHeight + 10f; // 230

        var leftList = GameObject.Find("Canvas/GameUIPanel/LeftPlayerList");
        if (leftList != null)
        {
            var rt = leftList.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Fix45 container height");
                var sd = rt.sizeDelta;
                sd.y = containerHeight;
                rt.sizeDelta = sd;
                Debug.Log($"[Fix45] LeftPlayerList: height → {containerHeight}");
            }
        }

        var rightList = GameObject.Find("Canvas/GameUIPanel/RightPlayerList");
        if (rightList != null)
        {
            var rt = rightList.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Fix45 container height");
                var sd = rt.sizeDelta;
                sd.y = containerHeight;
                rt.sizeDelta = sd;
                Debug.Log($"[Fix45] RightPlayerList: height → {containerHeight}");
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix45 完成: {fixCount} 项修复 ===");
    }
}
