using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// #43 战斗UI第二轮反馈修复（4项）
/// 1. 推力/连胜文字底图
/// 2. 总推力不缩略（overflowMode→Overflow + 加宽）
/// 3. 进度条左右缩短
/// 4. 推力差文字加大加粗
/// </summary>
public class BattleUIFix43 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix43 - Round2 Feedback")]
    public static void Execute()
    {
        int fixCount = 0;

        fixCount += Fix1_ForceAndStreakBg();
        fixCount += Fix2_ForceTextNoTruncate();
        fixCount += Fix3_ProgressBarShorter();
        fixCount += Fix4_HintTextBigger();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix43 完成: {fixCount} 项修复 ===");
    }

    /// <summary>
    /// Fix1: 给推力文字和连胜文字添加/确认底图
    /// </summary>
    static int Fix1_ForceAndStreakBg()
    {
        int count = 0;

        // === 连胜底图 — WinStreakLeftBg/WinStreakRightBg 已有sprite，确保可见 ===
        // 已确认 WinStreakLeftBg 有 win_streak_left_bg.png，无需修改

        // === 推力文字底图 — 需要在 LeftForceText/RightForceText 下方创建背景 ===
        // 效果图中推力文字有独立底图
        // 注意：用户已手动调整了位置，不要覆盖位置！

        // 加载推力底图sprite（顶部推力差的底 = hint_bar_bg，前三荣誉推力底图是卡片内的）
        // 顶部推力文字使用 score_pool_bg 风格的半透明底
        string[] forceTextPaths = {
            "Canvas/GameUIPanel/TopBar/LeftForceText",
            "Canvas/GameUIPanel/TopBar/RightForceText"
        };

        foreach (var path in forceTextPaths)
        {
            var go = GameObject.Find(path);
            if (go == null) continue;

            // 检查是否已有背景子对象
            var existingBg = go.transform.Find("ForceBg");
            if (existingBg != null)
            {
                Debug.Log($"[Fix1] {go.name}: ForceBg already exists");
                continue;
            }

            // 创建背景Image子对象（放在文字下方）
            var bgGo = new GameObject("ForceBg");
            bgGo.transform.SetParent(go.transform, false);

            var rt = bgGo.AddComponent<RectTransform>();
            // 铺满父级（推力文字的范围）
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-10f, -5f); // 留出padding
            rt.offsetMax = new Vector2(10f, 5f);

            var img = bgGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f); // 半透明黑底
            img.raycastTarget = false;

            // 设为第一个子对象（渲染在文字下方）
            bgGo.transform.SetAsFirstSibling();

            Undo.RegisterCreatedObjectUndo(bgGo, "Fix1 ForceBg");
            Debug.Log($"[Fix1] {go.name}: 添加半透明底图 ForceBg");
            count++;
        }

        // === 荣誉卡片内的推力底图 ===
        string playerForceBgPath = "Assets/Art/BattleUI/player_force_bg.png";
        var forceBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(playerForceBgPath);

        string[] sides = { "LeftPlayerList", "RightPlayerList" };
        foreach (var side in sides)
        {
            for (int i = 0; i < 3; i++)
            {
                var forcePath = $"Canvas/GameUIPanel/{side}/PlayerRow_{i}/PlayerForce";
                var forceGo = GameObject.Find(forcePath);
                if (forceGo == null) continue;

                // 检查是否已有背景
                var existingBg = forceGo.transform.Find("ForceBg");
                if (existingBg != null) continue;

                var bgGo = new GameObject("ForceBg");
                bgGo.transform.SetParent(forceGo.transform, false);

                var rt = bgGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-5f, -2f);
                rt.offsetMax = new Vector2(5f, 2f);

                var img = bgGo.AddComponent<Image>();
                if (forceBgSprite != null)
                {
                    img.sprite = forceBgSprite;
                    img.type = Image.Type.Sliced;
                    img.preserveAspect = false;
                }
                else
                {
                    img.color = new Color(0.3f, 0.2f, 0.1f, 0.6f);
                }
                img.raycastTarget = false;

                bgGo.transform.SetAsFirstSibling();
                Undo.RegisterCreatedObjectUndo(bgGo, "Fix1 CardForceBg");
            }
        }

        // === 荣誉卡片内的连胜底图 ===
        string leftStreakBgPath = "Assets/Art/BattleUI/win_streak_left_bg.png";
        string rightStreakBgPath = "Assets/Art/BattleUI/win_streak_right_bg.png";
        var leftStreakSprite = AssetDatabase.LoadAssetAtPath<Sprite>(leftStreakBgPath);
        var rightStreakSprite = AssetDatabase.LoadAssetAtPath<Sprite>(rightStreakBgPath);

        for (int i = 0; i < 3; i++)
        {
            // 左侧
            AddStreakBgToCard($"Canvas/GameUIPanel/LeftPlayerList/PlayerRow_{i}/PlayerStreak", leftStreakSprite);
            // 右侧
            AddStreakBgToCard($"Canvas/GameUIPanel/RightPlayerList/PlayerRow_{i}/PlayerStreak", rightStreakSprite);
        }

        Debug.Log($"[Fix1] 推力/连胜底图处理完成");
        return count > 0 ? 1 : 0;
    }

    static void AddStreakBgToCard(string path, Sprite sprite)
    {
        var go = GameObject.Find(path);
        if (go == null) return;

        var existingBg = go.transform.Find("StreakBg");
        if (existingBg != null) return;

        var bgGo = new GameObject("StreakBg");
        bgGo.transform.SetParent(go.transform, false);

        var rt = bgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-5f, -2f);
        rt.offsetMax = new Vector2(5f, 2f);

        var img = bgGo.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
        }
        else
        {
            img.color = new Color(0.3f, 0.2f, 0.1f, 0.6f);
        }
        img.raycastTarget = false;

        bgGo.transform.SetAsFirstSibling();
        Undo.RegisterCreatedObjectUndo(bgGo, "Fix1 StreakBg");
    }

    /// <summary>
    /// Fix2: 推力文字不缩略 — overflowMode=Overflow + 加宽
    /// </summary>
    static int Fix2_ForceTextNoTruncate()
    {
        string[] paths = {
            "Canvas/GameUIPanel/TopBar/LeftForceText",
            "Canvas/GameUIPanel/TopBar/RightForceText"
        };

        int count = 0;
        foreach (var path in paths)
        {
            var go = GameObject.Find(path);
            if (go == null) continue;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                Undo.RecordObject(tmp, "Fix2 noTruncate");
                tmp.overflowMode = TextOverflowModes.Overflow; // Ellipsis→Overflow
                tmp.enableAutoSizing = false; // 保持固定大小
                Debug.Log($"[Fix2] {go.name}: overflowMode→Overflow");
                count++;
            }

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Fix2 wider");
                // 加宽但不覆盖用户设定的位置（只改sizeDelta.x）
                var sd = rt.sizeDelta;
                sd.x = 200f; // 150→200
                rt.sizeDelta = sd;
                Debug.Log($"[Fix2] {go.name}: width 150→200");
            }
        }

        return count > 0 ? 1 : 0;
    }

    /// <summary>
    /// Fix3: 进度条缩短
    /// </summary>
    static int Fix3_ProgressBarShorter()
    {
        var go = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg/ProgressBarContainer");
        if (go == null) { Debug.LogWarning("[Fix3] ProgressBarContainer not found"); return 0; }

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            Undo.RecordObject(rt, "Fix3 shorter");
            var sd = rt.sizeDelta;
            sd.x = 780f; // 930→780
            rt.sizeDelta = sd;
            Debug.Log($"[Fix3] ProgressBarContainer: width 930→780");
        }

        return 1;
    }

    /// <summary>
    /// Fix4: 推力差文字加大 — 不覆盖用户设定的y位置
    /// </summary>
    static int Fix4_HintTextBigger()
    {
        var go = GameObject.Find("Canvas/GameUIPanel/HintText");
        if (go == null) { Debug.LogWarning("[Fix4] HintText not found"); return 0; }

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            Undo.RecordObject(tmp, "Fix4 bigger");
            tmp.fontSize = 30f; // 26→30
            tmp.outlineWidth = 0.4f; // 0.35→0.4
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            Debug.Log("[Fix4] HintText: fontSize=30, outlineWidth=0.4");
        }

        // 加大sizeDelta高度（不动y位置！用户已手动调整）
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            Undo.RecordObject(rt, "Fix4 height");
            var sd = rt.sizeDelta;
            sd.y = 50f; // 45→50（fontSize=30需要≥45高度）
            rt.sizeDelta = sd;
            Debug.Log("[Fix4] HintText: height 45→50");
        }

        return 1;
    }
}
