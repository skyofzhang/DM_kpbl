using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// #55 文字可读性专业修复 — TMP材质Underlay软阴影
/// 删除#54的粗暴黑底，改用TMP内置Underlay（软投影）
/// 参考项目中SettlementUI.cs已验证的方案
/// </summary>
public class BattleUIFix55
{
    [MenuItem("Tools/BattleUI Fix55 - TMP Underlay软阴影")]
    public static void Execute()
    {
        int fixCount = 0;

        // ============================================
        // Step 1: 删除#54添加的粗暴HintBg黑底
        // ============================================
        var hintGo = GameObject.Find("Canvas/GameUIPanel/HintText");
        if (hintGo != null)
        {
            var hintBg = hintGo.transform.Find("HintBg");
            if (hintBg != null)
            {
                Object.DestroyImmediate(hintBg.gameObject);
                fixCount++;
                Debug.Log("[Fix55] 删除 HintBg 黑底");
            }
        }

        // ============================================
        // Step 2: 对目标文字启用 TMP 材质 Underlay 软阴影
        // ============================================
        string[] targetPaths = new string[]
        {
            "Canvas/GameUIPanel/HintText",
            "Canvas/GameUIPanel/WinStreakLeft",
            "Canvas/GameUIPanel/WinStreakRight"
        };

        foreach (var path in targetPaths)
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                Debug.LogWarning($"[Fix55] 未找到: {path}");
                continue;
            }

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogWarning($"[Fix55] {path} 无TMP组件");
                continue;
            }

            // 获取材质实例（fontMaterial会自动创建实例，不影响共享材质）
            Material mat = tmp.fontMaterial;

            // 启用Shader关键字
            mat.EnableKeyword("OUTLINE_ON");
            mat.EnableKeyword("UNDERLAY_ON");

            // === Outline: 深色描边提供边缘定义 ===
            mat.SetFloat("_OutlineWidth", 0.15f);
            mat.SetColor("_OutlineColor", new Color(0.08f, 0.04f, 0f, 0.85f));
            mat.SetFloat("_OutlineSoftness", 0f);

            // === Underlay: 软阴影提供对比度（核心！） ===
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
            mat.SetFloat("_UnderlayOffsetX", 0.6f);
            mat.SetFloat("_UnderlayOffsetY", -0.6f);
            mat.SetFloat("_UnderlayDilate", 0.15f);
            mat.SetFloat("_UnderlaySoftness", 0.25f);

            // === FaceDilate: 微微加粗笔画 ===
            mat.SetFloat("_FaceDilate", 0.1f);

            // 降低组件级outlineWidth避免双重叠加过粗
            bool isHint = path.Contains("HintText");
            tmp.outlineWidth = isHint ? 0.2f : 0.15f;

            // 回写材质
            tmp.fontMaterial = mat;

            fixCount++;
            Debug.Log($"[Fix55] {go.name}: OUTLINE_ON + UNDERLAY_ON 已启用, outlineWidth={tmp.outlineWidth}");
        }

        // 标记场景dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"=== BattleUIFix55 完成: {fixCount} 项修复 ===");
    }
}
