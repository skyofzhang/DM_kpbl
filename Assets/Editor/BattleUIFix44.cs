using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// #44 荣誉玩家卡片底图修复
/// 原 player_rank_item_bg.png (328×99) 在竖向卡片上严重拉伸
/// 替换为专用左右侧底图（~300×300 正方形）
/// </summary>
public class BattleUIFix44 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix44 - Card BG Fix")]
    public static void Execute()
    {
        int fixCount = 0;

        // 加载左右侧专用底图
        string leftBgPath = "Assets/Art/BattleUI/player_card_left_bg.png";
        string rightBgPath = "Assets/Art/BattleUI/player_card_right_bg.png";
        var leftSprite = AssetDatabase.LoadAssetAtPath<Sprite>(leftBgPath);
        var rightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(rightBgPath);

        if (leftSprite == null)
        {
            Debug.LogWarning($"[Fix44] 未找到左侧底图: {leftBgPath}");
        }
        if (rightSprite == null)
        {
            Debug.LogWarning($"[Fix44] 未找到右侧底图: {rightBgPath}");
        }

        // === 左侧卡片 ===
        for (int i = 0; i < 3; i++)
        {
            var path = $"Canvas/GameUIPanel/LeftPlayerList/PlayerRow_{i}";
            var go = GameObject.Find(path);
            if (go == null) continue;

            var img = go.GetComponent<Image>();
            if (img != null && leftSprite != null)
            {
                Undo.RecordObject(img, "Fix44 left card bg");
                img.sprite = leftSprite;
                img.type = Image.Type.Simple;        // Simple模式，不做9-slice
                img.preserveAspect = true;            // 保持比例不拉伸
                img.raycastTarget = false;
                img.color = Color.white;
                Debug.Log($"[Fix44] {go.name} (Left): 替换为 player_card_left_bg, Simple+preserveAspect");
                fixCount++;
            }
        }

        // === 右侧卡片 ===
        for (int i = 0; i < 3; i++)
        {
            var path = $"Canvas/GameUIPanel/RightPlayerList/PlayerRow_{i}";
            var go = GameObject.Find(path);
            if (go == null) continue;

            var img = go.GetComponent<Image>();
            if (img != null && rightSprite != null)
            {
                Undo.RecordObject(img, "Fix44 right card bg");
                img.sprite = rightSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = Color.white;
                Debug.Log($"[Fix44] {go.name} (Right): 替换为 player_card_right_bg, Simple+preserveAspect");
                fixCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix44 完成: {fixCount} 张卡片底图修复 ===");
    }
}
