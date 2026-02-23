using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// #52 战斗UI第三轮修复（6项）
/// 1. 去掉总推力黑底（TopBar ForceBg）
/// 2. 缩小上方横幅（TopBarBg太大）
/// 3. 文字描边/投影优化（看不清）
/// 4. 积分池底图缩小宽度
/// 5. 比赛开始弹窗修复（AnnouncementPanel被Inactive）
/// 6. 卡皮巴拉头像缩小（代码修改，不在此脚本）
/// </summary>
public class BattleUIFix52 : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Fix52 - Round3 Six Fixes")]
    public static void Execute()
    {
        int fixCount = 0;

        // === 修复1: 删除总推力黑底 ===
        string[] forceBgPaths = {
            "Canvas/GameUIPanel/TopBar/LeftForceText/ForceBg",
            "Canvas/GameUIPanel/TopBar/RightForceText/ForceBg"
        };
        foreach (var path in forceBgPaths)
        {
            var go = GameObject.Find(path);
            if (go != null)
            {
                Undo.DestroyObjectImmediate(go);
                Debug.Log($"[Fix52] 删除: {path}");
                fixCount++;
            }
        }

        // === 修复2: 缩小上方横幅 ===
        // TopBarBg: 1000×200 → 800×160（缩小20%）
        var topBarBg = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg");
        if (topBarBg != null)
        {
            var rt = topBarBg.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Fix52 topbar size");
                rt.sizeDelta = new Vector2(800f, 160f);
                Debug.Log("[Fix52] TopBarBg: 1000×200 → 800×160");
                fixCount++;
            }
        }

        // === 修复3: 文字描边/粗细优化 ===
        // 增强所有关键文字的描边和粗细
        FixTextReadability("Canvas/GameUIPanel/TopBar/LeftForceText", 0.4f, true, ref fixCount);
        FixTextReadability("Canvas/GameUIPanel/TopBar/RightForceText", 0.4f, true, ref fixCount);
        FixTextReadability("Canvas/GameUIPanel/TopBar/ScorePoolText", 0.4f, true, ref fixCount);
        FixTextReadability("Canvas/GameUIPanel/TopBar/TimerText", 0.4f, true, ref fixCount);
        FixTextReadability("Canvas/GameUIPanel/HintText", 0.45f, true, ref fixCount);
        // 连胜文字
        FixTextReadability("Canvas/GameUIPanel/WinStreakLeft", 0.4f, true, ref fixCount);
        FixTextReadability("Canvas/GameUIPanel/WinStreakRight", 0.4f, true, ref fixCount);
        // 终点标记
        FixTextReadability("Canvas/GameUIPanel/TopBar/TopBarBg/LeftEndMarker", 0.4f, true, ref fixCount);
        FixTextReadability("Canvas/GameUIPanel/TopBar/TopBarBg/RightEndMarker", 0.4f, true, ref fixCount);
        FixTextReadability("Canvas/GameUIPanel/TopBar/TopBarBg/CenterMarker", 0.35f, true, ref fixCount);
        FixTextReadability("Canvas/GameUIPanel/TopBar/TopBarBg/PosIndicator", 0.35f, true, ref fixCount);
        // 荣誉卡片文字
        string[] sides = { "LeftPlayerList", "RightPlayerList" };
        foreach (var side in sides)
        {
            for (int i = 0; i < 3; i++)
            {
                FixTextReadability($"Canvas/GameUIPanel/{side}/PlayerRow_{i}/PlayerName", 0.35f, true, ref fixCount);
                FixTextReadability($"Canvas/GameUIPanel/{side}/PlayerRow_{i}/PlayerForce", 0.35f, true, ref fixCount);
                FixTextReadability($"Canvas/GameUIPanel/{side}/PlayerRow_{i}/PlayerStreak", 0.35f, true, ref fixCount);
                FixTextReadability($"Canvas/GameUIPanel/{side}/PlayerRow_{i}/RankText", 0.4f, true, ref fixCount);
            }
        }

        // === 修复4: 积分池底图缩小宽度 ===
        // ScorePoolBg: 260×65 → 220×55
        var scorePoolBg = GameObject.Find("Canvas/GameUIPanel/TopBar/ScorePoolBg");
        if (scorePoolBg != null)
        {
            var rt = scorePoolBg.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Fix52 score pool size");
                rt.sizeDelta = new Vector2(220f, 55f);
                Debug.Log("[Fix52] ScorePoolBg: 260×65 → 220×55");
                fixCount++;
            }
        }
        // ScorePoolText也同步缩小
        var scorePoolText = GameObject.Find("Canvas/GameUIPanel/TopBar/ScorePoolText");
        if (scorePoolText != null)
        {
            var rt = scorePoolText.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Fix52 score pool text size");
                rt.sizeDelta = new Vector2(210f, 45f);
            }
        }

        // === 修复5: AnnouncementPanel 激活 ===
        // AnnouncementPanel被设为inactive，导致Awake不调用，比赛开始看不到
        var announcementPanel = GameObject.Find("Canvas/AnnouncementPanel");
        // GameObject.Find不能找inactive对象，用Canvas.Find
        if (announcementPanel == null)
        {
            // 手动找
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                var tf = canvas.transform.Find("AnnouncementPanel");
                if (tf != null)
                {
                    Undo.RecordObject(tf.gameObject, "Fix52 activate announcement");
                    tf.gameObject.SetActive(true);
                    Debug.Log("[Fix52] AnnouncementPanel: SetActive(true)");
                    fixCount++;
                }
            }
        }
        else
        {
            Debug.Log("[Fix52] AnnouncementPanel: 已经是active状态");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"=== BattleUIFix52 完成: {fixCount} 项修复 ===");
    }

    /// <summary>增强文字可读性：增加描边宽度、加粗</summary>
    static void FixTextReadability(string path, float outlineWidth, bool bold, ref int fixCount)
    {
        var go = GameObject.Find(path);
        if (go == null) return;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;

        Undo.RecordObject(tmp, "Fix52 text readability");

        // 描边
        tmp.outlineWidth = outlineWidth;
        tmp.outlineColor = new Color32(0, 0, 0, 255);

        // 加粗
        if (bold)
            tmp.fontStyle |= FontStyles.Bold;

        fixCount++;
    }
}
