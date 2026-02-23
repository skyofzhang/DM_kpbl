#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 修复两个问题：
/// 1. Gift材质油腻 → 从CapybaraUnit shader换成CuteCapybara shader（和KpblUnit一致）
/// 2. 头像HUD太高 → 降低localPosition.y
/// </summary>
public static class FixGiftMaterialAndHUD
{
    public static void Execute()
    {
        // ========== 1. 修复Gift材质：换成CuteCapybara shader ==========
        Shader cuteShader = Shader.Find("CapybaraDuel/CuteCapybara");
        if (cuteShader == null)
        {
            cuteShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CuteCapybara.shader");
        }
        if (cuteShader == null)
        {
            Debug.LogError("[Fix] CuteCapybara shader not found!");
            return;
        }

        // 参考KpblUnit的材质参数
        var kpblMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Kpbl.mat");

        string root = "Assets/Models/Kpbl/5个礼物召唤的水豚单位";
        var dirs = new (int tier, string dir)[]
        {
            (2, "卡皮巴拉礼物单位2"),
            (3, "卡皮巴拉礼物单位3"),
            (4, "卡皮巴拉礼物单位4"),
            (5, "卡皮巴拉礼物单位5"),
            (6, "卡皮巴拉礼物单位6"),
        };

        foreach (var cfg in dirs)
        {
            string matPath = $"{root}/{cfg.dir}/mat_gift{cfg.tier}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) continue;

            // 保存原有贴图
            Texture baseTex = null;
            if (mat.HasProperty("_BaseMap")) baseTex = mat.GetTexture("_BaseMap");
            if (baseTex == null && mat.HasProperty("_MainTex")) baseTex = mat.GetTexture("_MainTex");

            // 切换shader
            mat.shader = cuteShader;

            // 恢复贴图
            if (baseTex != null && mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", baseTex);

            // 设置和KpblUnit一致的参数
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.25f);
            mat.SetFloat("_Metallic", 0f);

            // CuteCapybara特有参数（使用shader默认值即可，和KpblUnit统一风格）
            // Toon Lighting
            mat.SetFloat("_CelShadeMidPoint", 0.15f);
            mat.SetFloat("_CelShadeSoftness", 0.1f);
            mat.SetColor("_ShadowColor", new Color(0.75f, 0.65f, 0.85f, 1f));
            // Specular
            mat.SetColor("_SpecColor2", Color.white);
            mat.SetFloat("_Glossiness", 32f);
            mat.SetFloat("_SpecSmoothMin", 0.005f);
            mat.SetFloat("_SpecSmoothMax", 0.02f);
            // Rim Light
            mat.SetColor("_RimColor", new Color(1f, 0.85f, 0.65f, 1f));
            mat.SetFloat("_RimPower", 3.0f);
            mat.SetFloat("_RimSmoothness", 0.4f);
            // Fuzzy Edge
            mat.SetColor("_FuzzyEdgeColor", new Color(0.95f, 0.9f, 0.8f, 1f));
            mat.SetFloat("_FuzzyPower", 2.5f);
            mat.SetFloat("_FuzzyIntensity", 0.25f);
            mat.SetFloat("_FuzzyNoiseScale", 25f);
            // Fake SSS
            mat.SetColor("_SSSColor", new Color(1f, 0.4f, 0.3f, 1f));
            mat.SetFloat("_SSSDistortion", 0.4f);
            mat.SetFloat("_SSSPower", 4.0f);
            mat.SetFloat("_SSSScale", 0.4f);
            // Outline
            mat.SetFloat("_OutlineWidth", 1.5f);
            mat.SetColor("_OutlineColor", new Color(0.25f, 0.18f, 0.12f, 1f));
            // Emission (off by default)
            mat.SetColor("_EmissionColor", Color.black);

            EditorUtility.SetDirty(mat);
            Debug.Log($"[Fix] Gift{cfg.tier}: shader → CuteCapybara, params synced with KpblUnit");
        }

        // ========== 2. 修复头像HUD高度 ==========
        // 在Capybara.cs中 localPosition = (0, 1.8, 0) 太高
        // 改为 (0, 1.2, 0) 更贴近头顶
        // 注意：这个值是代码中的硬编码，需要修改源文件
        Debug.Log("[Fix] HUD height needs code change: Capybara.cs line 619, localPosition.y 1.8→1.2");

        AssetDatabase.SaveAssets();
        Debug.Log("[Fix] === Material fix done. HUD height needs code edit. ===");
    }
}
#endif
