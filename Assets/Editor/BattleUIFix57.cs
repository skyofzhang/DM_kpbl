using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// #57 荣誉卡片文字颜色修复 — 颜色改为高对比度纯白
/// 当前 PlayerForce 淡金(1,0.85,0.4)、PlayerStreak 暗橙(0.9,0.5,0.2) 在底图上看不清
/// 统一改为纯白，配合Underlay阴影确保可读性
/// 同时加强小文字的Underlay阴影参数
/// </summary>
public class BattleUIFix57
{
    [MenuItem("Tools/BattleUI Fix57 - 荣誉卡片文字颜色+阴影加强")]
    public static void Execute()
    {
        int fixCount = 0;

        string[] sides = { "LeftPlayerList", "RightPlayerList" };
        string[] rows = { "PlayerRow_0", "PlayerRow_1", "PlayerRow_2" };

        foreach (var side in sides)
        {
            foreach (var row in rows)
            {
                string basePath = $"Canvas/GameUIPanel/{side}/{row}";

                // PlayerForce: 淡金→纯白
                FixTextColor(basePath + "/PlayerForce", Color.white, ref fixCount);
                // PlayerStreak: 暗橙→纯白
                FixTextColor(basePath + "/PlayerStreak", Color.white, ref fixCount);
                // PlayerName: 已是白色，但加强阴影
                EnhanceUnderlay(basePath + "/PlayerName", ref fixCount);
                // RankText: 加强阴影
                EnhanceUnderlay(basePath + "/RankText", ref fixCount);
            }
        }

        // 标记场景dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"=== BattleUIFix57 完成: {fixCount} 项修复 ===");
    }

    static void FixTextColor(string path, Color newColor, ref int fixCount)
    {
        var go = GameObject.Find(path);
        if (go == null) return;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;

        // 修改文字颜色
        Color oldColor = tmp.color;
        tmp.color = newColor;

        // 加强Underlay阴影（小文字需要更强的阴影对比）
        Material mat = tmp.fontMaterial;
        mat.EnableKeyword("OUTLINE_ON");
        mat.EnableKeyword("UNDERLAY_ON");

        // 加强参数：比Fix56的小文字档更强
        mat.SetFloat("_OutlineWidth", 0.12f);
        mat.SetColor("_OutlineColor", new Color(0.05f, 0.02f, 0f, 0.9f));
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));  // 0.45→0.6
        mat.SetFloat("_UnderlayOffsetX", 0.55f);
        mat.SetFloat("_UnderlayOffsetY", -0.55f);
        mat.SetFloat("_UnderlayDilate", 0.15f);   // 0.1→0.15
        mat.SetFloat("_UnderlaySoftness", 0.2f);
        mat.SetFloat("_FaceDilate", 0.08f);
        mat.SetFloat("_OutlineSoftness", 0f);

        tmp.fontMaterial = mat;
        fixCount++;

        Debug.Log($"[Fix57] {go.name}: ({oldColor.r:F2},{oldColor.g:F2},{oldColor.b:F2})→白色, Underlay加强");
    }

    static void EnhanceUnderlay(string path, ref int fixCount)
    {
        var go = GameObject.Find(path);
        if (go == null) return;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;

        // 只加强阴影，不改颜色
        Material mat = tmp.fontMaterial;
        mat.EnableKeyword("OUTLINE_ON");
        mat.EnableKeyword("UNDERLAY_ON");

        mat.SetFloat("_OutlineWidth", 0.12f);
        mat.SetColor("_OutlineColor", new Color(0.05f, 0.02f, 0f, 0.9f));
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
        mat.SetFloat("_UnderlayOffsetX", 0.55f);
        mat.SetFloat("_UnderlayOffsetY", -0.55f);
        mat.SetFloat("_UnderlayDilate", 0.15f);
        mat.SetFloat("_UnderlaySoftness", 0.2f);
        mat.SetFloat("_FaceDilate", 0.08f);
        mat.SetFloat("_OutlineSoftness", 0f);

        tmp.fontMaterial = mat;
        fixCount++;
    }
}
