#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 修复原本使用 Unlit/Transparent 的材质 + 地面贴花 + 路面亮度
/// </summary>
public class FixTransparentMaterials
{
    public static string Execute()
    {
        var log = new System.Text.StringBuilder();
        int fixed_count = 0;

        // === 1. Alpha Clip 材质 (叶子/植物，原 Unlit/Transparent) ===
        string[] alphaClipMats = new[]
        {
            "Assets/Models/Scene/shuchu-ceshi/mod_1001_hgs_bajiao/Materials/mat_2013_hgs_bajiao_0.mat",
            "Assets/Models/Scene/shuchu/mod/mod_20002_x_zw_h2/Materials/mat_20002_x_zw10.mat",
        };

        foreach (string matPath in alphaClipMats)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { log.AppendLine($"  NOT FOUND: {matPath}"); continue; }

            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_Cull", 0f);       // double-sided
            mat.SetFloat("_Smoothness", 0.1f);
            mat.renderQueue = 2450;

            EditorUtility.SetDirty(mat);
            fixed_count++;
            log.AppendLine($"  ALPHA_CLIP: {matPath}");
        }

        // === 2. 透明贴花材质 (地面overlay，原 Particles/Standard Unlit) ===
        string[] transparentMats = new[]
        {
            "Assets/Models/Scene/shuchu/mod/mod_neihai_dibiao006/Materials/mat_eff_wanmiaolinggu_02_1.mat",
        };

        foreach (string matPath in transparentMats)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { log.AppendLine($"  NOT FOUND: {matPath}"); continue; }

            // 设为透明表面
            mat.SetFloat("_Surface", 1f);     // Transparent
            mat.SetFloat("_Blend", 0f);       // Alpha blend
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);      // 透明不写深度
            mat.SetFloat("_Cull", 0f);        // double-sided
            mat.SetFloat("_Smoothness", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;

            EditorUtility.SetDirty(mat);
            fixed_count++;
            log.AppendLine($"  TRANSPARENT: {matPath}");
        }

        // === 3. 降低路面/地基 smoothness (防止过亮) ===
        string[] groundMats = new[]
        {
            "Assets/Models/Scene/shuchu-ceshi/mod_3001_tyc_lu01_1/Materials/mat_3001_tyc_lu01_1_0.mat",
            "Assets/Models/Scene/shuchu-ceshi/mod_3001_tyc_lu01_2/Materials/mat_3001_tyc_lu01_4.mat",
            "Assets/Models/Scene/shuchu-ceshi/mod_3002_ghj_diji03a/Materials/mat_3002_ghj_zoulang01.mat",
            "Assets/Models/Scene/shuchu-ceshi/mod_3002_ghj_diji04a/Materials/mat_3002_ghj_zoulang01.mat",
            "Assets/Models/Scene/shuchu-ceshi/mod_3002_ghj_huatan01a/Materials/mat_3002_ghj_zoulang01.mat",
        };

        foreach (string matPath in groundMats)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) continue;

            mat.SetFloat("_Smoothness", 0.05f);  // 接近全粗糙
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            log.AppendLine($"  GROUND_FIX: {matPath} (smoothness→0.05)");
        }

        AssetDatabase.SaveAssets();

        string summary = $"[FixTransparent] Fixed {fixed_count} transparent + ground mats";
        log.Insert(0, summary + "\n");
        return log.ToString();
    }
}
#endif
