using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// #51 排名数字下移，叠在头像框上方
/// 需求：RankText（"1"/"2"/"3"）往下移动，叠加在AvatarFrame的上部区域
/// 方案：RankText设置ignoreLayout=true，绝对定位到头像顶部，
///       sibling index放在AvatarContainer之后确保渲染在头像上方
/// </summary>
public class BattleUIFix51 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix51 - Rank Overlap Avatar")]
    public static void Execute()
    {
        int fixCount = 0;

        string[] sides = { "LeftPlayerList", "RightPlayerList" };

        foreach (var side in sides)
        {
            for (int i = 0; i < 3; i++)
            {
                var rowPath = $"Canvas/GameUIPanel/{side}/PlayerRow_{i}";
                var rowGo = GameObject.Find(rowPath);
                if (rowGo == null) continue;

                // 强制重建布局获取准确位置
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                    rowGo.GetComponent<RectTransform>());

                var rankTf = rowGo.transform.Find("RankText");
                var avatarTf = rowGo.transform.Find("AvatarContainer");
                if (rankTf == null || avatarTf == null) continue;

                var rankRect = rankTf.GetComponent<RectTransform>();
                var avatarRect = avatarTf.GetComponent<RectTransform>();

                // 给RankText添加ignoreLayout（如果还没有的话）
                var le = rankTf.GetComponent<LayoutElement>();
                if (le == null)
                    le = rankTf.gameObject.AddComponent<LayoutElement>();
                Undo.RecordObject(le, "Fix51 rank ignoreLayout");
                le.ignoreLayout = true;

                // 计算新位置：头像中心往上偏移，让排名数字在头像顶部
                // AvatarContainer localPos: y=26.5, size=85x85
                // 头像顶部 y = avatarLocalPos.y + avatarSize.y/2 = 26.5 + 42.5 = 69
                // RankText高36，想让它下半部分和头像顶部重叠
                // 新的y = 头像顶部y - rankHeight * 0.15 = 69 - 5 = 64
                var avatarLocalPos = avatarTf.localPosition;
                var avatarSize = avatarRect.sizeDelta;
                float avatarTop = avatarLocalPos.y + avatarSize.y / 2f;
                float newRankY = avatarTop - rankRect.sizeDelta.y * 0.15f;

                Undo.RecordObject(rankRect, "Fix51 rank position");
                rankRect.localPosition = new Vector3(0f, newRankY, 0f);

                // 确保RankText渲染在AvatarContainer上方
                // sibling index: AvatarContainer之后
                int avatarIndex = avatarTf.GetSiblingIndex();
                rankTf.SetSiblingIndex(avatarIndex + 1);

                Debug.Log($"[Fix51] {side}/Row_{i}: RankText moved to y={newRankY:F1}, sibling after Avatar");
                fixCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix51 完成: {fixCount} 项修复 ===");
    }
}
