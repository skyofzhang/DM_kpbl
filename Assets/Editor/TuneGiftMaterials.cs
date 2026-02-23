#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 调优礼物单位材质参数 — 大幅减弱CuteCapybara shader的卡通效果，
/// 让gift模型的高质量贴图细节能够充分展现
///
/// v2: 在v1基础上再减弱50%，用户反馈效果仍然太强烈
/// </summary>
public static class TuneGiftMaterials
{
    [MenuItem("CapybaraDuel/4. Tune Gift Material Params")]
    public static void Execute()
    {
        string root = "Assets/Models/Kpbl/5个礼物召唤的水豚单位";
        string[] dirs = { "卡皮巴拉礼物单位2", "卡皮巴拉礼物单位3", "卡皮巴拉礼物单位4", "卡皮巴拉礼物单位5", "卡皮巴拉礼物单位6" };

        int fixedCount = 0;
        for (int i = 0; i < 5; i++)
        {
            int tier = i + 2;
            string matPath = $"{root}/{dirs[i]}/mat_gift{tier}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Debug.LogWarning($"[TuneGift] mat_gift{tier}.mat not found at {matPath}");
                continue;
            }

            // 确认是CuteCapybara shader
            if (mat.shader == null || !mat.shader.name.Contains("CuteCapybara"))
            {
                Debug.LogWarning($"[TuneGift] Gift{tier} shader={mat.shader?.name}, skip (not CuteCapybara)");
                continue;
            }

            // ======= v2: 效果全面减半，贴图细节优先 =======

            // Base: 纯白底色，极低光泽
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
            mat.SetFloat("_Smoothness", 0.1f);   // v1: 0.2 → v2: 0.1
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_BumpScale", 1.0f);

            // Toon Lighting: 阴影极浅，过渡极柔 → 几乎看不出卡通阴影分界
            mat.SetFloat("_CelShadeMidPoint", -0.1f);   // v1: 0.0 → v2: -0.1（更少面积受阴影）
            mat.SetFloat("_CelShadeSoftness", 0.5f);    // v1: 0.25 → v2: 0.5（过渡更模糊）
            mat.SetColor("_ShadowColor", new Color(0.88f, 0.84f, 0.92f, 1f)); // v1更浅 → v2几乎白色

            // Specular: 高光极弱
            mat.SetColor("_SpecColor2", new Color(1f, 1f, 1f, 1f));
            mat.SetFloat("_Glossiness", 12f);       // v1: 24 → v2: 12（高光极散）
            mat.SetFloat("_SpecSmoothMin", 0.001f);  // v1: 0.003 → v2: 0.001
            mat.SetFloat("_SpecSmoothMax", 0.008f);  // v1: 0.015 → v2: 0.008

            // Rim Light: 减半强度
            mat.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, 0.5f)); // alpha减半
            mat.SetFloat("_RimPower", 5.0f);        // v1: 3.5 → v2: 5.0（更窄更集中）
            mat.SetFloat("_RimSmoothness", 0.7f);   // v1: 0.5 → v2: 0.7（更柔）

            // Fuzzy Edge: 几乎关闭 — 原始值0.25太强了
            mat.SetColor("_FuzzyEdgeColor", new Color(0.97f, 0.95f, 0.9f, 1f));
            mat.SetFloat("_FuzzyPower", 5.0f);       // v1: 3.5 → v2: 5.0（极集中在边缘）
            mat.SetFloat("_FuzzyIntensity", 0.06f);  // v1: 0.12 → v2: 0.06（再减半）
            mat.SetFloat("_FuzzyNoiseScale", 40f);    // v1: 30 → v2: 40（噪点更细更不明显）

            // Fake SSS: 几乎关闭
            mat.SetColor("_SSSColor", new Color(1f, 0.6f, 0.45f, 1f));
            mat.SetFloat("_SSSDistortion", 0.15f);   // v1: 0.3 → v2: 0.15
            mat.SetFloat("_SSSPower", 7.0f);          // v1: 5.0 → v2: 7.0（极集中）
            mat.SetFloat("_SSSScale", 0.1f);           // v1: 0.2 → v2: 0.1（再减半）

            // Outline: 更细
            mat.SetFloat("_OutlineWidth", 0.8f);    // v1: 1.2 → v2: 0.8
            mat.SetColor("_OutlineColor", new Color(0.25f, 0.18f, 0.12f, 1f));

            // Emission: 默认关闭
            mat.SetColor("_EmissionColor", Color.black);

            EditorUtility.SetDirty(mat);
            fixedCount++;
            Debug.Log($"[TuneGift] Gift{tier}: v2 params applied (50% weaker) ✓");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TuneGift] === DONE === {fixedCount}/5 materials tuned (v2 -50%)");
    }
}
#endif
