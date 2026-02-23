using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// #56 战斗UI文字统一Underlay软阴影 + HintText缩小
/// 1. HintText字号36→30（出框修复）
/// 2. 所有战斗UI TMP文字统一启用 OUTLINE_ON + UNDERLAY_ON
/// </summary>
public class BattleUIFix56
{
    [MenuItem("Tools/BattleUI Fix56 - 全局Underlay + HintText缩小")]
    public static void Execute()
    {
        int fixCount = 0;

        // ============================================
        // Fix1: HintText 字号缩小 (出框修复)
        // ============================================
        var hintGo = GameObject.Find("Canvas/GameUIPanel/HintText");
        if (hintGo != null)
        {
            var tmp = hintGo.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 30; // 36→30, 配合代码中<size=130%>数字实际约39pt
                fixCount++;
                Debug.Log("[Fix56] HintText: fontSize 36→30");
            }
        }

        // ============================================
        // Fix2: 所有战斗UI TMP文字统一启用 Underlay 软阴影
        // 排除已处理的 HintText/WinStreakLeft/WinStreakRight (Fix55已设)
        // ============================================
        string[] alreadyDone = new string[] {
            "Canvas/GameUIPanel/HintText",
            "Canvas/GameUIPanel/WinStreakLeft",
            "Canvas/GameUIPanel/WinStreakRight"
        };

        // 所有需要处理的TMP文字路径
        string[] allTextPaths = new string[]
        {
            // TopBar区域
            "Canvas/GameUIPanel/TopBar/TimerText",
            "Canvas/GameUIPanel/TopBar/ScorePoolText",
            "Canvas/GameUIPanel/TopBar/LeftForceText",
            "Canvas/GameUIPanel/TopBar/RightForceText",
            "Canvas/GameUIPanel/VIPAnnouncement/VIPText",
            "Canvas/GameUIPanel/BtnEnd/Label",
            "Canvas/GameUIPanel/BtnSettings/Label",
            // 进度条标记
            "Canvas/GameUIPanel/TopBar/TopBarBg/LeftEndMarker",
            "Canvas/GameUIPanel/TopBar/TopBarBg/CenterMarker",
            "Canvas/GameUIPanel/TopBar/TopBarBg/RightEndMarker",
            "Canvas/GameUIPanel/TopBar/TopBarBg/PosIndicator",
            // 左侧荣誉玩家卡片
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_0/PlayerName",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_0/RankText",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_0/PlayerForce",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_0/PlayerStreak",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_1/PlayerName",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_1/RankText",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_1/PlayerForce",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_1/PlayerStreak",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_2/PlayerName",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_2/RankText",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_2/PlayerForce",
            "Canvas/GameUIPanel/LeftPlayerList/PlayerRow_2/PlayerStreak",
            // 右侧荣誉玩家卡片
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_0/PlayerName",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_0/RankText",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_0/PlayerForce",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_0/PlayerStreak",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_1/PlayerName",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_1/RankText",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_1/PlayerForce",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_1/PlayerStreak",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_2/PlayerName",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_2/RankText",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_2/PlayerForce",
            "Canvas/GameUIPanel/RightPlayerList/PlayerRow_2/PlayerStreak",
        };

        foreach (var path in allTextPaths)
        {
            var go = GameObject.Find(path);
            if (go == null) continue;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) continue;

            // 获取材质实例
            Material mat = tmp.fontMaterial;

            // 启用Shader关键字
            mat.EnableKeyword("OUTLINE_ON");
            mat.EnableKeyword("UNDERLAY_ON");

            // 根据文字类型调整参数
            bool isSmallText = path.Contains("PlayerName") || path.Contains("PlayerForce")
                            || path.Contains("PlayerStreak") || path.Contains("RankText");
            bool isMarker = path.Contains("Marker") || path.Contains("PosIndicator");
            bool isButton = path.Contains("Btn") || path.Contains("Label");

            if (isSmallText)
            {
                // 荣誉卡片小文字：轻量阴影
                mat.SetFloat("_OutlineWidth", 0.1f);
                mat.SetColor("_OutlineColor", new Color(0.06f, 0.03f, 0f, 0.8f));
                mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.45f));
                mat.SetFloat("_UnderlayOffsetX", 0.5f);
                mat.SetFloat("_UnderlayOffsetY", -0.5f);
                mat.SetFloat("_UnderlayDilate", 0.1f);
                mat.SetFloat("_UnderlaySoftness", 0.2f);
                mat.SetFloat("_FaceDilate", 0.05f);
                tmp.outlineWidth = 0.1f;
            }
            else if (isMarker)
            {
                // 进度条标记：中等阴影
                mat.SetFloat("_OutlineWidth", 0.12f);
                mat.SetColor("_OutlineColor", new Color(0.05f, 0.03f, 0f, 0.8f));
                mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.5f));
                mat.SetFloat("_UnderlayOffsetX", 0.5f);
                mat.SetFloat("_UnderlayOffsetY", -0.5f);
                mat.SetFloat("_UnderlayDilate", 0.12f);
                mat.SetFloat("_UnderlaySoftness", 0.22f);
                mat.SetFloat("_FaceDilate", 0.08f);
                tmp.outlineWidth = 0.12f;
            }
            else
            {
                // 大文字(推力/计时器/积分池/按钮)：标准阴影
                mat.SetFloat("_OutlineWidth", 0.15f);
                mat.SetColor("_OutlineColor", new Color(0.08f, 0.04f, 0f, 0.85f));
                mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
                mat.SetFloat("_UnderlayOffsetX", 0.6f);
                mat.SetFloat("_UnderlayOffsetY", -0.6f);
                mat.SetFloat("_UnderlayDilate", 0.15f);
                mat.SetFloat("_UnderlaySoftness", 0.25f);
                mat.SetFloat("_FaceDilate", 0.1f);
                tmp.outlineWidth = 0.15f;
            }

            mat.SetFloat("_OutlineSoftness", 0f);

            // 回写材质
            tmp.fontMaterial = mat;
            fixCount++;
        }

        Debug.Log($"[Fix56] 统一Underlay: {fixCount}个文字元素已处理");

        // 标记场景dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"=== BattleUIFix56 完成: {fixCount} 项修复 ===");
    }
}
