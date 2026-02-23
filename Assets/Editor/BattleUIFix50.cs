using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// #50 修复底图位置不对齐文字
/// 问题：Fix49创建底图时读取PlayerForce的anchoredPosition，
///       但VerticalLayoutGroup会在脚本执行过程中动态修改子对象位置，
///       导致Row_0之外的底图位置不正确。
/// 修复：不再复制坐标，改用localPosition（VerticalLayoutGroup真正控制的值）
///       并且先收集所有位置信息，再一次性创建底图。
/// </summary>
public class BattleUIFix50 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix50 - BG Position Align")]
    public static void Execute()
    {
        AssetDatabase.Refresh();

        int fixCount = 0;

        var forceBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/player_force_bg.png");
        var streakLeftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/win_streak_left_bg.png");
        var streakRightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/win_streak_right_bg.png");

        Debug.Log($"[Fix50] Sprites: force={forceBgSprite != null}, streakL={streakLeftSprite != null}, streakR={streakRightSprite != null}");

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

                // === Step 1: 清理所有旧的 ForceBg / StreakBg ===
                for (int c = rowGo.transform.childCount - 1; c >= 0; c--)
                {
                    var child = rowGo.transform.GetChild(c);
                    if (child.name == "ForceBg" || child.name == "StreakBg")
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                    }
                }
                // 也清理子级中的
                var forceTf = rowGo.transform.Find("PlayerForce");
                if (forceTf != null)
                {
                    var childBg = forceTf.Find("ForceBg");
                    if (childBg != null) Undo.DestroyObjectImmediate(childBg.gameObject);
                }
                var streakTf = rowGo.transform.Find("PlayerStreak");
                if (streakTf != null)
                {
                    var childBg = streakTf.Find("StreakBg");
                    if (childBg != null) Undo.DestroyObjectImmediate(childBg.gameObject);
                }

                // === Step 2: 强制重建布局 ===
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                    rowGo.GetComponent<RectTransform>());

                // === Step 3: Re-find ===
                forceTf = rowGo.transform.Find("PlayerForce");
                streakTf = rowGo.transform.Find("PlayerStreak");

                // === Step 4: 创建 ForceBg ===
                if (forceTf != null)
                {
                    var forceRect = forceTf.GetComponent<RectTransform>();
                    if (forceRect != null)
                    {
                        // 读取VerticalLayoutGroup计算后的实际localPosition
                        var forceLocalPos = forceTf.localPosition;
                        var forceSize = forceRect.sizeDelta;

                        var bgGo = new GameObject("ForceBg");
                        bgGo.transform.SetParent(rowGo.transform, false);

                        var le = bgGo.AddComponent<LayoutElement>();
                        le.ignoreLayout = true;

                        var rt = bgGo.GetComponent<RectTransform>();
                        // 和PlayerForce使用相同的anchor设置
                        rt.anchorMin = forceRect.anchorMin;
                        rt.anchorMax = forceRect.anchorMax;
                        rt.pivot = forceRect.pivot;
                        // 直接用localPosition设置（这是VerticalLayoutGroup真正控制的）
                        rt.localPosition = forceLocalPos;
                        rt.sizeDelta = new Vector2(forceSize.x + 10f, forceSize.y + 12f);

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

                        // 放到 PlayerForce 之前
                        int forceIndex = forceTf.GetSiblingIndex();
                        bgGo.transform.SetSiblingIndex(forceIndex);

                        Undo.RegisterCreatedObjectUndo(bgGo, "Fix50 ForceBg");
                        Debug.Log($"[Fix50] {side}/Row_{i}: ForceBg localPos={forceLocalPos}, size={rt.sizeDelta}");
                        fixCount++;
                    }
                }

                // === Step 5: 创建 StreakBg ===
                if (streakTf != null)
                {
                    var streakRect = streakTf.GetComponent<RectTransform>();
                    if (streakRect != null)
                    {
                        var streakLocalPos = streakTf.localPosition;
                        var streakSize = streakRect.sizeDelta;

                        var bgGo = new GameObject("StreakBg");
                        bgGo.transform.SetParent(rowGo.transform, false);

                        var le = bgGo.AddComponent<LayoutElement>();
                        le.ignoreLayout = true;

                        var rt = bgGo.GetComponent<RectTransform>();
                        rt.anchorMin = streakRect.anchorMin;
                        rt.anchorMax = streakRect.anchorMax;
                        rt.pivot = streakRect.pivot;
                        rt.localPosition = streakLocalPos;
                        rt.sizeDelta = new Vector2(streakSize.x + 10f, streakSize.y + 14f);

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

                        int streakIndex = streakTf.GetSiblingIndex();
                        bgGo.transform.SetSiblingIndex(streakIndex);

                        Undo.RegisterCreatedObjectUndo(bgGo, "Fix50 StreakBg");
                        Debug.Log($"[Fix50] {side}/Row_{i}: StreakBg localPos={streakLocalPos}, size={rt.sizeDelta}");
                        fixCount++;
                    }
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix50 完成: {fixCount} 项修复 ===");
    }
}
