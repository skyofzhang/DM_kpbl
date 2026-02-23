using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// #46 荣誉玩家区域修正
/// 1. 移除 PlayerRow 上错误的背景 Image（卡片本身不需要底）
/// 2. AvatarFrame 改用正确的头像底图（player_card_left/right_bg）
/// </summary>
public class BattleUIFix46 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix46 - Card BG Correct")]
    public static void Execute()
    {
        int fixCount = 0;

        // 加载头像底图
        string leftAvatarBgPath = "Assets/Art/BattleUI/player_card_left_bg.png";
        string rightAvatarBgPath = "Assets/Art/BattleUI/player_card_right_bg.png";
        var leftAvatarBg = AssetDatabase.LoadAssetAtPath<Sprite>(leftAvatarBgPath);
        var rightAvatarBg = AssetDatabase.LoadAssetAtPath<Sprite>(rightAvatarBgPath);

        if (leftAvatarBg == null) Debug.LogWarning($"[Fix46] 未找到: {leftAvatarBgPath}");
        if (rightAvatarBg == null) Debug.LogWarning($"[Fix46] 未找到: {rightAvatarBgPath}");

        // === 左侧 ===
        for (int i = 0; i < 3; i++)
        {
            var rowPath = $"Canvas/GameUIPanel/LeftPlayerList/PlayerRow_{i}";
            var rowGo = GameObject.Find(rowPath);
            if (rowGo == null) continue;

            // 1. 移除 PlayerRow 上的 Image 组件（不要卡片背景）
            var rowImg = rowGo.GetComponent<Image>();
            if (rowImg != null)
            {
                Undo.DestroyObjectImmediate(rowImg);
                Debug.Log($"[Fix46] {rowGo.name}: 移除错误的 Image 背景");
                fixCount++;
            }

            // 同时移除 CanvasRenderer（Image 依赖它，Image 删了它也不需要了）
            // 注意：CanvasRenderer 可能被子对象需要，保留更安全

            // 2. AvatarFrame 改用头像底图
            var framePath = $"{rowPath}/AvatarContainer/AvatarFrame";
            var frameGo = GameObject.Find(framePath);
            if (frameGo != null && leftAvatarBg != null)
            {
                var frameImg = frameGo.GetComponent<Image>();
                if (frameImg != null)
                {
                    Undo.RecordObject(frameImg, "Fix46 avatar bg left");
                    frameImg.sprite = leftAvatarBg;
                    frameImg.type = Image.Type.Simple;
                    frameImg.preserveAspect = true;
                    Debug.Log($"[Fix46] Left AvatarFrame_{i}: 替换为 player_card_left_bg");
                }
            }
        }

        // === 右侧 ===
        for (int i = 0; i < 3; i++)
        {
            var rowPath = $"Canvas/GameUIPanel/RightPlayerList/PlayerRow_{i}";
            var rowGo = GameObject.Find(rowPath);
            if (rowGo == null) continue;

            // 1. 移除 PlayerRow 上的 Image
            var rowImg = rowGo.GetComponent<Image>();
            if (rowImg != null)
            {
                Undo.DestroyObjectImmediate(rowImg);
                Debug.Log($"[Fix46] {rowGo.name}: 移除错误的 Image 背景");
                fixCount++;
            }

            // 2. AvatarFrame 改用头像底图
            var framePath = $"{rowPath}/AvatarContainer/AvatarFrame";
            var frameGo = GameObject.Find(framePath);
            if (frameGo != null && rightAvatarBg != null)
            {
                var frameImg = frameGo.GetComponent<Image>();
                if (frameImg != null)
                {
                    Undo.RecordObject(frameImg, "Fix46 avatar bg right");
                    frameImg.sprite = rightAvatarBg;
                    frameImg.type = Image.Type.Simple;
                    frameImg.preserveAspect = true;
                    Debug.Log($"[Fix46] Right AvatarFrame_{i}: 替换为 player_card_right_bg");
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix46 完成: {fixCount} 项修复 ===");
    }
}
