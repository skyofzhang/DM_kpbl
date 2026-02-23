#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace CapybaraDuel.Editor
{
    /// <summary>
    /// 自动创建 Standard Material：扫描贴图名后缀匹配 shader 属性
    /// _BaseMap / _D → URP _BaseMap, _Normal / _NRM → _BumpMap
    /// </summary>
    public static class MaterialAutoBuilder
    {
        [MenuItem("CapybaraDuel/1. Build Materials", false, 10)]
        public static void BuildAllMaterials()
        {
            string modelsRoot = "Assets/Models";
            if (!AssetDatabase.IsValidFolder(modelsRoot))
            {
                EditorUtility.DisplayDialog("Error", "Assets/Models 目录不存在", "OK");
                return;
            }

            string matOutput = "Assets/Materials";
            EnsureFolder(matOutput);

            int created = 0;
            int skipped = 0;

            // 扫描每个子目录
            string[] subDirs = AssetDatabase.GetSubFolders(modelsRoot);
            foreach (string subDir in subDirs)
            {
                string folderName = Path.GetFileName(subDir);
                string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { subDir });

                if (pngGuids.Length == 0) continue;

                // 查找贴图
                Texture2D mainTex = null;
                Texture2D normalTex = null;

                foreach (string guid in pngGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

                    if (fileName.Contains("basemap") || fileName.Contains("_d") || fileName.Contains("color") ||
                        (fileName.Contains("body") && fileName.Contains("basemap")) ||
                        (fileName.Contains("head") && fileName.Contains("basemap")) ||
                        (!fileName.Contains("normal") && !fileName.Contains("nomal") && !fileName.Contains("nrm") && !fileName.Contains("spec") && !fileName.EndsWith("-m")))
                    {
                        // 优先选择 BaseMap，否则取第一个非 Normal 贴图
                        if (mainTex == null || fileName.Contains("basemap") || fileName.Contains("_d"))
                            mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    }

                    if (fileName.Contains("normal") || fileName.Contains("nomal") || fileName.Contains("nrm"))
                    {
                        normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        // 确保 Normal Map 导入设置正确
                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                        {
                            importer.textureType = TextureImporterType.NormalMap;
                            importer.SaveAndReimport();
                        }
                    }
                }

                if (mainTex == null) continue;

                // 卡皮巴拉单位用 CuteCapybara shader（毛绒+描边+SSS+边缘光）
                // 其他模型用 URP/Lit
                Shader cuteShader = Shader.Find("CapybaraDuel/CuteCapybara");
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                bool isUnit = IsCapybaraUnitFolder(folderName);
                Shader targetShader = (isUnit && cuteShader != null) ? cuteShader : urpLit;

                // 创建材质
                string matPath = $"{matOutput}/Mat_{folderName}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                if (mat == null)
                {
                    mat = new Material(targetShader);
                    AssetDatabase.CreateAsset(mat, matPath);
                    created++;
                }
                else
                {
                    // 更新到正确的shader
                    if (isUnit && cuteShader != null && mat.shader.name != cuteShader.name)
                        mat.shader = cuteShader;
                    else if (!isUnit && mat.shader != urpLit && mat.shader.name == "CapybaraDuel/CapybaraUnit")
                        mat.shader = urpLit;
                    skipped++;
                }

                // 设置基础属性
                mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", Color.white);

                if (normalTex != null)
                {
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    mat.DisableKeyword("_NORMALMAP");
                }

                mat.SetFloat("_Smoothness", 0.3f);
                mat.SetFloat("_Metallic", 0f);

                EditorUtility.SetDirty(mat);
            }

            // 特殊材质：橘子（使用魔法Shader）
            CreateMagicOrangeMaterial(matOutput, "Assets/Models/Orange", ref created, ref skipped);

            // 特殊材质：场景元素
            CreateSceneMaterials(matOutput, ref created, ref skipped);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MaterialBuilder] Done! Created: {created}, Skipped(existed): {skipped}");
            EditorUtility.DisplayDialog("Materials Built",
                $"创建了 {created} 个材质，跳过了 {skipped} 个已存在材质。\n输出目录: {matOutput}",
                "OK");
        }

        static void CreateSpecialMaterial(string matOutput, string sourceDir, string matName,
            Color fallbackColor, ref int created, ref int skipped)
        {
            string matPath = $"{matOutput}/{matName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) { skipped++; return; }

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            // 尝试找贴图
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { sourceDir });
            if (texGuids.Length > 0)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuids[0]);
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                mat.SetColor("_BaseColor", fallbackColor);
            }

            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(mat, matPath);
            created++;
        }

        static void CreateSceneMaterials(string matOutput, ref int created, ref int skipped)
        {
            // 地面草地材质
            CreateTexturedMaterial(matOutput, "Mat_Grass", "Assets/Models/Scene/Grass_green.png",
                new Color(0.3f, 0.6f, 0.2f), ref created, ref skipped);

            // 天空材质
            CreateTexturedMaterial(matOutput, "Mat_Sky", "Assets/Models/Scene/Cb_zhucheng_sky.png",
                new Color(0.5f, 0.7f, 1f), ref created, ref skipped);

            // 阵营材质（纯色）
            CreateColorMaterial(matOutput, "Mat_LeftPool", new Color(1f, 0.55f, 0f, 0.8f), ref created, ref skipped);
            CreateColorMaterial(matOutput, "Mat_RightPool", new Color(0.68f, 1f, 0.18f, 0.8f), ref created, ref skipped);
        }

        static void CreateTexturedMaterial(string matOutput, string matName, string texPath,
            Color fallback, ref int created, ref int skipped)
        {
            string matPath = $"{matOutput}/{matName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) { skipped++; return; }

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                mat.SetColor("_BaseColor", fallback);
            }
            mat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(mat, matPath);
            created++;
        }

        static void CreateColorMaterial(string matOutput, string matName, Color color,
            ref int created, ref int skipped)
        {
            string matPath = $"{matOutput}/{matName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) { skipped++; return; }

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            if (color.a < 1f)
            {
                // URP Transparent 模式设置
                mat.SetFloat("_Surface", 1); // 0=Opaque, 1=Transparent
                mat.SetFloat("_Blend", 0);   // 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            mat.SetFloat("_Smoothness", 0.6f);
            AssetDatabase.CreateAsset(mat, matPath);
            created++;
        }

        /// <summary>
        /// 创建魔法橘子材质 - 使用自定义 MagicOrange shader
        /// </summary>
        static void CreateMagicOrangeMaterial(string matOutput, string sourceDir,
            ref int created, ref int skipped)
        {
            string matPath = $"{matOutput}/Mat_Orange.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            // 查找魔法Shader
            Shader magicShader = Shader.Find("CapybaraDuel/MagicOrange");
            if (magicShader == null)
            {
                Debug.LogWarning("[MaterialBuilder] MagicOrange shader not found, falling back to URP Lit");
                magicShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            bool isNew = (mat == null);
            if (isNew)
            {
                mat = new Material(magicShader);
            }
            else
            {
                mat.shader = magicShader;
                skipped++;
            }

            // 查找橘子贴图
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { sourceDir });
            if (texGuids.Length > 0)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuids[0]);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetColor("_BaseColor", Color.white);
                }
            }
            else
            {
                mat.SetColor("_BaseColor", new Color(1f, 0.6f, 0f));
            }

            // 设置魔法效果默认参数（用户可在Inspector中微调）
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0f);

            // Fresnel
            mat.SetFloat("_FresnelOn", 1f);
            mat.EnableKeyword("_FRESNEL_ON");
            mat.SetColor("_FresnelColor", new Color(1f, 0.7f, 0.2f, 1f));
            mat.SetFloat("_FresnelPower", 2.5f);
            mat.SetFloat("_FresnelIntensity", 1.5f);

            // Outer Glow
            mat.SetFloat("_GlowOn", 1f);
            mat.EnableKeyword("_GLOW_ON");
            mat.SetColor("_GlowColor", new Color(1f, 0.5f, 0f, 1f));
            mat.SetFloat("_GlowIntensity", 2.0f);
            mat.SetFloat("_GlowPower", 1.5f);

            // Specular
            mat.SetFloat("_SpecBoost", 3f);
            mat.SetFloat("_SpecPower", 64f);

            // Flow Light
            mat.SetFloat("_FlowOn", 1f);
            mat.EnableKeyword("_FLOW_ON");
            mat.SetColor("_FlowColor", new Color(1f, 0.85f, 0.4f, 1f));
            mat.SetFloat("_FlowIntensity", 0.8f);
            mat.SetFloat("_FlowSpeed", 0.5f);
            mat.SetFloat("_FlowWidth", 0.12f);
            mat.SetVector("_FlowDirection", new Vector4(0.3f, 1f, 0.2f, 0f));

            // Pulse
            mat.SetFloat("_PulseOn", 1f);
            mat.EnableKeyword("_PULSE_ON");
            mat.SetFloat("_PulseSpeed", 1.2f);
            mat.SetFloat("_PulseMin", 0.7f);
            mat.SetFloat("_PulseMax", 1.3f);

            // Emission
            mat.SetColor("_EmissionColor", new Color(0.3f, 0.15f, 0f, 1f));
            mat.SetFloat("_EmissionIntensity", 0.5f);

            if (isNew)
            {
                AssetDatabase.CreateAsset(mat, matPath);
                created++;
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }
        }

        /// <summary>判断是否为卡皮巴拉单位目录（需使用CapybaraUnit shader）</summary>
        static bool IsCapybaraUnitFolder(string folderName)
        {
            string lower = folderName.ToLower();
            // 排除非单位目录
            if (lower == "orange" || lower == "scene" || lower == "terrain" ||
                lower.Contains("terrain") || lower.Contains("sky"))
                return false;
            // 包含单位相关关键字
            if (lower.Contains("kpbl") || lower.Contains("sheep") || lower.Contains("capybara") ||
                lower.Contains("201_") || lower.Contains("big"))
                return true;
            // 默认：有FBX模型的目录都算单位
            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { $"Assets/Models/{folderName}" });
            foreach (var guid in fbxGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folderName = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
#endif
