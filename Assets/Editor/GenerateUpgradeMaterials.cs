using UnityEngine;
using UnityEditor;

/// <summary>
/// 编辑器工具：生成10个升级等级材质球（Lv.1~Lv.10）
/// 基于 CuteCapybara shader，从基础材质复制后调整各通道参数
/// 用户可在Inspector中进一步微调每个材质
///
/// 菜单：CapybaraDuel/4. Generate Upgrade Materials
/// </summary>
public class GenerateUpgradeMaterials
{
    private const string OUTPUT_DIR = "Assets/Materials/UpgradeLevels";
    private const string BASE_MAT_PATH = "Assets/Models/Kpbl/kapibala-Material.mat";

    [MenuItem("CapybaraDuel/4. Generate Upgrade Materials")]
    public static void Generate()
    {
        // 确保输出目录存在
        if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
        {
            string parent = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateFolder(parent, "UpgradeLevels");
        }

        // 加载基础材质（用于复制贴图引用）
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(BASE_MAT_PATH);
        if (baseMat == null)
        {
            Debug.LogError($"[GenerateUpgradeMaterials] Base material not found: {BASE_MAT_PATH}");
            return;
        }

        // 找到CuteCapybara shader（正确名称）
        var shader = Shader.Find("CapybaraDuel/CuteCapybara");
        if (shader == null)
        {
            Debug.LogError("[GenerateUpgradeMaterials] Shader 'CapybaraDuel/CuteCapybara' not found! Make sure CuteCapybara.shader is in the project.");
            return;
        }

        Debug.Log($"[GenerateUpgradeMaterials] Using shader: {shader.name}");

        int created = 0;
        for (int level = 1; level <= 10; level++)
        {
            string matName = $"mat_upgrade_lv{level:D2}";
            string matPath = $"{OUTPUT_DIR}/{matName}.mat";

            // 删除已存在的材质（强制重新生成，修复之前shader错误的问题）
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(matPath);
                Debug.Log($"[GenerateUpgradeMaterials] Deleted old (wrong shader): {matPath}");
            }

            // 创建新材质，直接使用CuteCapybara shader
            var mat = new Material(shader);
            mat.name = matName;

            // 从基础材质复制贴图引用
            if (baseMat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", baseMat.GetTexture("_BaseMap"));
            if (baseMat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", baseMat.GetTexture("_MainTex"));
            if (baseMat.HasProperty("_BumpMap"))
                mat.SetTexture("_BumpMap", baseMat.GetTexture("_BumpMap"));
            if (baseMat.HasProperty("_MetallicGlossMap"))
                mat.SetTexture("_MetallicGlossMap", baseMat.GetTexture("_MetallicGlossMap"));

            // 应用等级参数（Rim/Fuzzy/SSS/Emission/Shadow各通道）
            ApplyLevelParams(mat, level);

            // 保存
            AssetDatabase.CreateAsset(mat, matPath);
            created++;
            Debug.Log($"[GenerateUpgradeMaterials] Created: {matPath} (shader: {shader.name})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GenerateUpgradeMaterials] Done! Created {created} materials in {OUTPUT_DIR}");
    }

    private static void ApplyLevelParams(Material mat, int level)
    {
        // 根据等级设置各通道参数
        // 这些是初始值，用户可以在Inspector中微调

        switch (level)
        {
            case 1: // 原始态 — 不做任何修改
                break;

            case 2: // 初醒 — 暖白轮廓光微亮
                SetRim(mat, new Color(1f, 0.95f, 0.85f), 2.5f, 0.5f);
                SetEmission(mat, new Color(1f, 0.95f, 0.85f), 0.05f,
                    flickerSpeed: 1.0f, flickerMin: 0.5f, threshold: 0.3f, useTexColor: 0.5f);
                break;

            case 3: // 觉醒 — 浅金Rim + 暖毛绒边缘
                SetRim(mat, new Color(1f, 0.9f, 0.6f), 2.2f, 0.55f);
                SetFuzzy(mat, new Color(1f, 0.92f, 0.75f), 0.35f);
                SetEmission(mat, new Color(1f, 0.95f, 0.8f), 0.1f,
                    flickerSpeed: 1.2f, flickerMin: 0.4f, threshold: 0.25f, useTexColor: 0.5f);
                break;

            case 4: // 蓄力 — 天蓝系
                SetRim(mat, new Color(0.6f, 0.85f, 1f), 2.0f, 0.55f);
                SetFuzzy(mat, new Color(0.8f, 0.9f, 1f), 0.3f);
                SetEmission(mat, new Color(0.5f, 0.8f, 1f), 0.2f,
                    flickerSpeed: 1.5f, flickerMin: 0.35f, threshold: 0.2f, useTexColor: 0.3f);
                SetSSS(mat, new Color(0.6f, 0.8f, 1f), 0.5f);
                break;

            case 5: // 凝聚 — 钴蓝加深
                SetRim(mat, new Color(0.35f, 0.65f, 1f), 1.8f, 0.6f);
                SetFuzzy(mat, new Color(0.7f, 0.82f, 1f), 0.35f);
                SetEmission(mat, new Color(0.3f, 0.6f, 1f), 0.35f,
                    flickerSpeed: 1.8f, flickerMin: 0.3f, threshold: 0.15f, useTexColor: 0.3f);
                SetSSS(mat, new Color(0.4f, 0.65f, 1f), 0.55f);
                SetShadow(mat, new Color(0.55f, 0.55f, 0.8f));
                break;

            case 6: // 炽热 — 金色系
                SetRim(mat, new Color(1f, 0.82f, 0.25f), 1.6f, 0.6f);
                SetFuzzy(mat, new Color(1f, 0.88f, 0.5f), 0.4f);
                SetEmission(mat, new Color(1f, 0.85f, 0.3f), 0.5f,
                    flickerSpeed: 2.0f, flickerMin: 0.3f, threshold: 0.1f, useTexColor: 0.4f);
                SetSSS(mat, new Color(1f, 0.75f, 0.35f), 0.6f);
                SetShadow(mat, new Color(0.8f, 0.65f, 0.5f));
                break;

            case 7: // 辉煌 — 琥珀深金
                SetRim(mat, new Color(1f, 0.7f, 0.15f), 1.5f, 0.65f);
                SetFuzzy(mat, new Color(1f, 0.82f, 0.4f), 0.45f);
                SetEmission(mat, new Color(1f, 0.75f, 0.2f), 0.7f,
                    flickerSpeed: 2.5f, flickerMin: 0.25f, threshold: 0.08f, useTexColor: 0.4f);
                SetSSS(mat, new Color(1f, 0.65f, 0.25f), 0.65f);
                SetShadow(mat, new Color(0.75f, 0.58f, 0.45f));
                break;

            case 8: // 炎皇 — 红橙火焰
                SetRim(mat, new Color(1f, 0.45f, 0.1f), 1.3f, 0.7f);
                SetFuzzy(mat, new Color(1f, 0.7f, 0.4f), 0.5f);
                SetEmission(mat, new Color(1f, 0.5f, 0.15f), 1.0f,
                    flickerSpeed: 3.0f, flickerMin: 0.2f, threshold: 0.05f, useTexColor: 0.5f);
                SetSSS(mat, new Color(1f, 0.4f, 0.15f), 0.7f);
                SetShadow(mat, new Color(0.7f, 0.45f, 0.35f));
                break;

            case 9: // 神威 — 品红紫焰
                SetRim(mat, new Color(1f, 0.3f, 0.6f), 1.2f, 0.75f);
                SetFuzzy(mat, new Color(1f, 0.6f, 0.8f), 0.55f);
                SetEmission(mat, new Color(0.9f, 0.3f, 0.6f), 1.2f,
                    flickerSpeed: 3.5f, flickerMin: 0.2f, threshold: 0f, useTexColor: 0.5f);
                SetSSS(mat, new Color(1f, 0.5f, 0.7f), 0.75f);
                SetShadow(mat, new Color(0.65f, 0.4f, 0.6f));
                break;

            case 10: // 传说 — 彩虹流转（基础白色，运行时脚本控制彩虹）
                SetRim(mat, new Color(1f, 0.9f, 0.95f), 1.0f, 0.8f);
                SetFuzzy(mat, new Color(1f, 0.95f, 1f), 0.6f);
                SetEmission(mat, new Color(1f, 0.9f, 0.95f), 1.5f,
                    flickerSpeed: 4.0f, flickerMin: 0.15f, threshold: 0f, useTexColor: 0.6f);
                SetSSS(mat, new Color(1f, 0.7f, 0.9f), 0.8f);
                SetShadow(mat, new Color(0.6f, 0.5f, 0.7f));
                break;
        }
    }

    private static void SetRim(Material mat, Color color, float power, float smoothness)
    {
        if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", color);
        if (mat.HasProperty("_RimPower")) mat.SetFloat("_RimPower", power);
        if (mat.HasProperty("_RimSmoothness")) mat.SetFloat("_RimSmoothness", smoothness);
    }

    private static void SetFuzzy(Material mat, Color color, float intensity)
    {
        if (mat.HasProperty("_FuzzyEdgeColor")) mat.SetColor("_FuzzyEdgeColor", color);
        if (mat.HasProperty("_FuzzyIntensity")) mat.SetFloat("_FuzzyIntensity", intensity);
    }

    private static void SetEmission(Material mat, Color color, float intensity,
        float flickerSpeed = 0f, float flickerMin = 0.3f, float threshold = 0f, float useTexColor = 0f)
    {
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * intensity);
        if (mat.HasProperty("_EmissionFlickerSpeed")) mat.SetFloat("_EmissionFlickerSpeed", flickerSpeed);
        if (mat.HasProperty("_EmissionFlickerMin")) mat.SetFloat("_EmissionFlickerMin", flickerMin);
        if (mat.HasProperty("_EmissionThreshold")) mat.SetFloat("_EmissionThreshold", threshold);
        if (mat.HasProperty("_EmissionUseTexColor")) mat.SetFloat("_EmissionUseTexColor", useTexColor);
    }

    private static void SetSSS(Material mat, Color color, float scale)
    {
        if (mat.HasProperty("_SSSColor")) mat.SetColor("_SSSColor", color);
        if (mat.HasProperty("_SSSScale")) mat.SetFloat("_SSSScale", scale);
    }

    private static void SetShadow(Material mat, Color color)
    {
        if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", color);
    }
}
