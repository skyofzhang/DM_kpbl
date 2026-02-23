using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 刷新AssetDatabase后执行Fix44
/// </summary>
public class RefreshAndFix44 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Refresh+Fix44")]
    public static void Execute()
    {
        // 先刷新AssetDatabase让Unity发现新复制的文件
        AssetDatabase.Refresh();
        Debug.Log("[RefreshAndFix44] AssetDatabase refreshed");

        // 加载左右侧专用底图
        string leftBgPath = "Assets/Art/BattleUI/player_card_left_bg.png";
        string rightBgPath = "Assets/Art/BattleUI/player_card_right_bg.png";
        var leftSprite = AssetDatabase.LoadAssetAtPath<Sprite>(leftBgPath);
        var rightSprite = AssetDatabase.LoadAssetAtPath<Sprite>(rightBgPath);

        if (leftSprite == null)
        {
            Debug.LogWarning($"[Fix44] 仍未找到左侧底图: {leftBgPath}");
            return;
        }
        if (rightSprite == null)
        {
            Debug.LogWarning($"[Fix44] 仍未找到右侧底图: {rightBgPath}");
            return;
        }

        int fixCount = 0;

        // === 左侧卡片 ===
        for (int i = 0; i < 3; i++)
        {
            var path = $"Canvas/GameUIPanel/LeftPlayerList/PlayerRow_{i}";
            var go = GameObject.Find(path);
            if (go == null) { Debug.LogWarning($"[Fix44] 未找到: {path}"); continue; }

            var img = go.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Fix44 left card bg");
                img.sprite = leftSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
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
            if (go == null) { Debug.LogWarning($"[Fix44] 未找到: {path}"); continue; }

            var img = go.GetComponent<Image>();
            if (img != null)
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
        Debug.Log($"=== Fix44 完成: {fixCount} 张卡片底图修复 ===");
    }
}
