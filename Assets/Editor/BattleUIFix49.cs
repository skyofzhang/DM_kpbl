using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// #49 修复推力/连胜底图被VerticalLayoutGroup重新排列
/// 问题：Fix48把ForceBg/StreakBg移到PlayerRow级别，
///       但PlayerRow有VerticalLayoutGroup，底图被当作布局子项重新排列了。
/// 修复：给ForceBg/StreakBg添加LayoutElement(ignoreLayout=true)，
///       让VerticalLayoutGroup跳过它们，底图用绝对定位叠在文字下方。
/// </summary>
public class BattleUIFix49 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix49 - BG IgnoreLayout Fix")]
    public static void Execute()
    {
        AssetDatabase.Refresh();

        int fixCount = 0;

        var forceBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/player_force_bg.png");
        var streakLeftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/win_streak_left_bg.png");
        var streakRightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/win_streak_right_bg.png");

        Debug.Log($"[Fix49] Sprites: force={forceBgSprite != null}, streakL={streakLeftSprite != null}, streakR={streakRightSprite != null}");

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
                // 在PlayerRow级别
                for (int c = rowGo.transform.childCount - 1; c >= 0; c--)
                {
                    var child = rowGo.transform.GetChild(c);
                    if (child.name == "ForceBg" || child.name == "StreakBg")
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                    }
                }

                // 在PlayerForce/PlayerStreak子级
                var forceGo = rowGo.transform.Find("PlayerForce");
                if (forceGo != null)
                {
                    var childBg = forceGo.Find("ForceBg");
                    if (childBg != null)
                        Undo.DestroyObjectImmediate(childBg.gameObject);
                }
                var streakTf = rowGo.transform.Find("PlayerStreak");
                if (streakTf != null)
                {
                    var childBg = streakTf.Find("StreakBg");
                    if (childBg != null)
                        Undo.DestroyObjectImmediate(childBg.gameObject);
                }

                // === Step 2: Re-find after destruction ===
                forceGo = rowGo.transform.Find("PlayerForce");
                streakTf = rowGo.transform.Find("PlayerStreak");

                // === Step 3: 创建 ForceBg（ignoreLayout=true, 绝对定位）===
                if (forceGo != null)
                {
                    var forceRect = forceGo.GetComponent<RectTransform>();
                    if (forceRect != null)
                    {
                        var bgGo = new GameObject("ForceBg");
                        bgGo.transform.SetParent(rowGo.transform, false);

                        // LayoutElement: 让VerticalLayoutGroup忽略
                        var le = bgGo.AddComponent<LayoutElement>();
                        le.ignoreLayout = true;

                        // SetParent到Canvas子级时Unity自动添加RectTransform，用GetComponent
                        var rt = bgGo.GetComponent<RectTransform>();
                        rt.anchorMin = forceRect.anchorMin;
                        rt.anchorMax = forceRect.anchorMax;
                        rt.pivot = forceRect.pivot;
                        rt.anchoredPosition = forceRect.anchoredPosition;
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

                        // 放到 PlayerForce 之前（渲染在下层）
                        int forceIndex = forceGo.GetSiblingIndex();
                        bgGo.transform.SetSiblingIndex(forceIndex);

                        Undo.RegisterCreatedObjectUndo(bgGo, "Fix49 ForceBg");
                        Debug.Log($"[Fix49] {side}/Row_{i}: ForceBg created, ignoreLayout=true, pos={rt.anchoredPosition}, size={rt.sizeDelta}");
                        fixCount++;
                    }
                }

                // === Step 4: 创建 StreakBg（ignoreLayout=true, 绝对定位）===
                if (streakTf != null)
                {
                    var streakRect = streakTf.GetComponent<RectTransform>();
                    if (streakRect != null)
                    {
                        var bgGo = new GameObject("StreakBg");
                        bgGo.transform.SetParent(rowGo.transform, false);

                        var le = bgGo.AddComponent<LayoutElement>();
                        le.ignoreLayout = true;

                        // SetParent到Canvas子级时Unity自动添加RectTransform，用GetComponent
                        var rt = bgGo.GetComponent<RectTransform>();
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

                        // 放到 PlayerStreak 之前
                        int streakIndex = streakTf.GetSiblingIndex();
                        bgGo.transform.SetSiblingIndex(streakIndex);

                        Undo.RegisterCreatedObjectUndo(bgGo, "Fix49 StreakBg");
                        Debug.Log($"[Fix49] {side}/Row_{i}: StreakBg created, ignoreLayout=true, pos={rt.anchoredPosition}, size={rt.sizeDelta}");
                        fixCount++;
                    }
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix49 完成: {fixCount} 项修复 ===");
    }
}
