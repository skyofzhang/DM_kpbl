using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// #48 修复推力/连胜底图遮挡文字
/// 问题：ForceBg/StreakBg是PlayerForce/PlayerStreak的子对象，
///       子对象永远渲染在父对象上面，所以底图挡住了文字。
/// 修复：删除旧的，在PlayerRow级别创建底图，
///       通过绝对定位对齐到PlayerForce/PlayerStreak的位置。
/// </summary>
public class BattleUIFix48 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix48 - BG Layer Fix")]
    public static void Execute()
    {
        AssetDatabase.Refresh();

        int fixCount = 0;

        var forceBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/player_force_bg.png");
        var streakLeftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/win_streak_left_bg.png");
        var streakRightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/win_streak_right_bg.png");

        string[] sides = { "LeftPlayerList", "RightPlayerList" };

        foreach (var side in sides)
        {
            bool isLeft = side.Contains("Left");
            var streakSprite = isLeft ? streakLeftSprite : streakRightSprite;

            for (int i = 0; i < 3; i++)
            {
                var rowPath = $"Canvas/GameUIPanel/{side}/PlayerRow_{i}";
                var rowGo = GameObject.Find(rowPath);
                if (rowGo == null) continue;

                // === 删除旧的 ForceBg（在PlayerForce下面） ===
                var forcePath = $"{rowPath}/PlayerForce";
                var forceGo = GameObject.Find(forcePath);
                if (forceGo != null)
                {
                    var oldForceBg = forceGo.transform.Find("ForceBg");
                    if (oldForceBg != null)
                        Undo.DestroyObjectImmediate(oldForceBg.gameObject);
                }

                // === 删除旧的 StreakBg（在PlayerStreak下面） ===
                var streakPath = $"{rowPath}/PlayerStreak";
                var streakGo = GameObject.Find(streakPath);
                if (streakGo != null)
                {
                    var oldStreakBg = streakGo.transform.Find("StreakBg");
                    if (oldStreakBg != null)
                        Undo.DestroyObjectImmediate(oldStreakBg.gameObject);
                }

                // === 删除旧的 PlayerRow 级别底图（如果有） ===
                var oldRowForceBg = rowGo.transform.Find("ForceBg");
                if (oldRowForceBg != null)
                    Undo.DestroyObjectImmediate(oldRowForceBg.gameObject);
                var oldRowStreakBg = rowGo.transform.Find("StreakBg");
                if (oldRowStreakBg != null)
                    Undo.DestroyObjectImmediate(oldRowStreakBg.gameObject);

                // === 在 PlayerRow 级别创建 ForceBg ===
                if (forceGo != null)
                {
                    var forceRect = forceGo.GetComponent<RectTransform>();
                    var bgGo = new GameObject("ForceBg");
                    bgGo.transform.SetParent(rowGo.transform, false);

                    var rt = bgGo.AddComponent<RectTransform>();
                    // 复制 PlayerForce 的位置和大小
                    rt.anchorMin = forceRect.anchorMin;
                    rt.anchorMax = forceRect.anchorMax;
                    rt.pivot = forceRect.pivot;
                    rt.anchoredPosition = forceRect.anchoredPosition;
                    // 底图比文字稍大一点
                    rt.sizeDelta = new Vector2(forceRect.sizeDelta.x + 10f, forceRect.sizeDelta.y + 12f);

                    var img = bgGo.AddComponent<Image>();
                    if (forceBgSprite != null)
                    {
                        img.sprite = forceBgSprite;
                        img.type = Image.Type.Simple;
                        img.preserveAspect = true;
                    }
                    else
                    {
                        img.color = new Color(0f, 0f, 0f, 0.3f);
                    }
                    img.raycastTarget = false;

                    // 放到 PlayerForce 之前（sibling index 小 = 渲染在下层）
                    int forceIndex = forceGo.transform.GetSiblingIndex();
                    bgGo.transform.SetSiblingIndex(forceIndex);

                    Undo.RegisterCreatedObjectUndo(bgGo, "Fix48 ForceBg");
                    Debug.Log($"[Fix48] {side}/Row_{i}: ForceBg → PlayerRow级别, sibling={forceIndex}");
                    fixCount++;
                }

                // === 在 PlayerRow 级别创建 StreakBg ===
                if (streakGo != null)
                {
                    var streakRect = streakGo.GetComponent<RectTransform>();
                    var bgGo = new GameObject("StreakBg");
                    bgGo.transform.SetParent(rowGo.transform, false);

                    var rt = bgGo.AddComponent<RectTransform>();
                    rt.anchorMin = streakRect.anchorMin;
                    rt.anchorMax = streakRect.anchorMax;
                    rt.pivot = streakRect.pivot;
                    rt.anchoredPosition = streakRect.anchoredPosition;
                    rt.sizeDelta = new Vector2(streakRect.sizeDelta.x + 10f, streakRect.sizeDelta.y + 14f);

                    var img = bgGo.AddComponent<Image>();
                    if (streakSprite != null)
                    {
                        img.sprite = streakSprite;
                        img.type = Image.Type.Simple;
                        img.preserveAspect = true;
                    }
                    else
                    {
                        img.color = new Color(0f, 0f, 0f, 0.3f);
                    }
                    img.raycastTarget = false;

                    int streakIndex = streakGo.transform.GetSiblingIndex();
                    bgGo.transform.SetSiblingIndex(streakIndex);

                    Undo.RegisterCreatedObjectUndo(bgGo, "Fix48 StreakBg");
                    Debug.Log($"[Fix48] {side}/Row_{i}: StreakBg → PlayerRow级别, sibling={streakIndex}");
                    fixCount++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix48 完成: {fixCount} 项修复 ===");
    }
}
