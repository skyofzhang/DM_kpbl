using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// #47 修复卡片内推力/连胜底图
/// Fix43创建的ForceBg/StreakBg有问题：
/// - ForceBg: sprite加载失败(meta非Sprite)，anchor铺满导致太大
/// - StreakBg: anchor铺满+padding导致太大
/// 修复：删除旧的，用正确尺寸重建（固定sizeDelta，不用anchor铺满）
/// </summary>
public class BattleUIFix47 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix47 - Force Streak Bg Fix")]
    public static void Execute()
    {
        // 先刷新让Unity识别修改后的meta
        AssetDatabase.Refresh();

        int fixCount = 0;

        // 加载资源
        var forceBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/player_force_bg.png");
        var streakLeftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/win_streak_left_bg.png");
        var streakRightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BattleUI/win_streak_right_bg.png");

        if (forceBgSprite == null) Debug.LogWarning("[Fix47] player_force_bg.png sprite NOT loaded!");
        else Debug.Log("[Fix47] player_force_bg.png loaded OK");

        if (streakLeftSprite == null) Debug.LogWarning("[Fix47] win_streak_left_bg sprite NOT loaded!");
        if (streakRightSprite == null) Debug.LogWarning("[Fix47] win_streak_right_bg sprite NOT loaded!");

        string[] sides = { "LeftPlayerList", "RightPlayerList" };

        foreach (var side in sides)
        {
            bool isLeft = side.Contains("Left");
            var streakSprite = isLeft ? streakLeftSprite : streakRightSprite;

            for (int i = 0; i < 3; i++)
            {
                // === 修复 ForceBg ===
                var forcePath = $"Canvas/GameUIPanel/{side}/PlayerRow_{i}/PlayerForce";
                var forceGo = GameObject.Find(forcePath);
                if (forceGo != null)
                {
                    // 删除旧的ForceBg
                    var oldForceBg = forceGo.transform.Find("ForceBg");
                    if (oldForceBg != null)
                    {
                        Undo.DestroyObjectImmediate(oldForceBg.gameObject);
                    }

                    // 创建新的ForceBg（固定尺寸，居中对齐）
                    var bgGo = new GameObject("ForceBg");
                    bgGo.transform.SetParent(forceGo.transform, false);

                    var rt = bgGo.AddComponent<RectTransform>();
                    // 居中对齐，固定尺寸（不用anchor铺满）
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    // PlayerForce是150×24，底图328×99 → 按比例缩放到宽度120
                    // 120 / 328 * 99 = 36.2 → 用120×36
                    rt.sizeDelta = new Vector2(120f, 36f);

                    var img = bgGo.AddComponent<Image>();
                    if (forceBgSprite != null)
                    {
                        img.sprite = forceBgSprite;
                        img.type = Image.Type.Simple;
                        img.preserveAspect = true;
                    }
                    else
                    {
                        // fallback: 小的半透明底
                        img.color = new Color(0f, 0f, 0f, 0.3f);
                    }
                    img.raycastTarget = false;

                    // 渲染在文字下方
                    bgGo.transform.SetAsFirstSibling();
                    Undo.RegisterCreatedObjectUndo(bgGo, "Fix47 ForceBg");
                    Debug.Log($"[Fix47] {side}/PlayerRow_{i}/PlayerForce/ForceBg: 重建 120×36");
                    fixCount++;
                }

                // === 修复 StreakBg ===
                var streakPath = $"Canvas/GameUIPanel/{side}/PlayerRow_{i}/PlayerStreak";
                var streakGo = GameObject.Find(streakPath);
                if (streakGo != null)
                {
                    // 删除旧的StreakBg
                    var oldStreakBg = streakGo.transform.Find("StreakBg");
                    if (oldStreakBg != null)
                    {
                        Undo.DestroyObjectImmediate(oldStreakBg.gameObject);
                    }

                    // 创建新的StreakBg
                    var bgGo = new GameObject("StreakBg");
                    bgGo.transform.SetParent(streakGo.transform, false);

                    var rt = bgGo.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    // PlayerStreak是150×24，底图328×103 → 按比例缩放到宽度120
                    // 120 / 328 * 103 = 37.7 → 用120×38
                    rt.sizeDelta = new Vector2(120f, 38f);

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

                    bgGo.transform.SetAsFirstSibling();
                    Undo.RegisterCreatedObjectUndo(bgGo, "Fix47 StreakBg");
                    Debug.Log($"[Fix47] {side}/PlayerRow_{i}/PlayerStreak/StreakBg: 重建 120×38");
                    fixCount++;
                }
            }
        }

        // === 同时修复顶部推力文字的ForceBg（Fix43也有问题）===
        string[] topForcePaths = {
            "Canvas/GameUIPanel/TopBar/LeftForceText",
            "Canvas/GameUIPanel/TopBar/RightForceText"
        };
        foreach (var path in topForcePaths)
        {
            var go = GameObject.Find(path);
            if (go == null) continue;

            var oldBg = go.transform.Find("ForceBg");
            if (oldBg != null)
            {
                Undo.DestroyObjectImmediate(oldBg.gameObject);
            }

            // 重建：紧贴文字大小
            var bgGo = new GameObject("ForceBg");
            bgGo.transform.SetParent(go.transform, false);

            var rt = bgGo.AddComponent<RectTransform>();
            // 铺满父级但只留2px padding
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-4f, -2f);
            rt.offsetMax = new Vector2(4f, 2f);

            var img = bgGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.4f); // 半透明黑底
            img.raycastTarget = false;

            bgGo.transform.SetAsFirstSibling();
            Undo.RegisterCreatedObjectUndo(bgGo, "Fix47 TopForceBg");
            Debug.Log($"[Fix47] {go.name}/ForceBg: 重建（半透明黑底）");
            fixCount++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix47 完成: {fixCount} 项修复 ===");
    }
}
