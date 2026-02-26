#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 修复 Tripo 第三方模型材质问题
/// 问题: FBX嵌入材质没有正确绑定贴图，导致无阴影/变白
/// 方案: 提取材质 → 绑定.fbm贴图 → 配置URP阴影 → 重映射FBX
/// </summary>
public class FixTripoMaterials
{
    [MenuItem("CapybaraDuel/Fix Tripo Materials (修复第三方模型材质)", false, 301)]
    public static void MenuFix()
    {
        string result = Execute();
        Debug.Log(result);
    }

    public static string Execute()
    {
        string sceneModelPath = "Assets/Models/Scene/scene-modle";
        var log = new System.Text.StringBuilder();
        int fixedCount = 0;
        int errorCount = 0;
        int textureCount = 0;

        // Step 1: Find all tripo FBX files
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        var fbxPaths = allAssets
            .Where(p => p.StartsWith(sceneModelPath) &&
                       p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) &&
                       Path.GetFileName(p).StartsWith("tripo_convert"))
            .ToList();

        log.AppendLine($"[FixTripo] Found {fbxPaths.Count} tripo FBX files");

        foreach (string fbxPath in fbxPaths)
        {
            try
            {
                string fbxDir = Path.GetDirectoryName(fbxPath).Replace("\\", "/");
                string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
                string fbmFolder = fbxDir + "/" + fbxName + ".fbm";

                // Create Materials folder next to FBX
                string matFolder = fbxDir + "/Materials";
                if (!AssetDatabase.IsValidFolder(matFolder))
                {
                    string parentFolder = fbxDir;
                    AssetDatabase.CreateFolder(parentFolder, "Materials");
                }

                // Get the ModelImporter
                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    log.AppendLine($"  SKIP: {fbxPath} (not a model)");
                    continue;
                }

                // Load embedded materials from FBX
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                Material[] embeddedMats = subAssets.OfType<Material>().ToArray();

                if (embeddedMats.Length == 0)
                {
                    log.AppendLine($"  SKIP: {fbxPath} (no materials)");
                    continue;
                }

                // Collect all textures from .fbm folder
                Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
                if (AssetDatabase.IsValidFolder(fbmFolder))
                {
                    string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { fbmFolder });
                    foreach (string texGuid in texGuids)
                    {
                        string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                        string texNameLower = Path.GetFileNameWithoutExtension(texPath).ToLower();
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        if (tex != null)
                        {
                            textures[texNameLower] = tex;
                            // Also store by path for reference
                            textures["__path__" + texNameLower] = tex;
                        }
                    }
                }

                // Find textures by role
                Texture2D colorTex = FindTextureByRole(textures, "color");
                Texture2D normalTex = FindTextureByRole(textures, "normal");
                Texture2D metallicTex = FindTextureByRole(textures, "metallic");
                Texture2D roughnessTex = FindTextureByRole(textures, "roughness");

                foreach (Material embeddedMat in embeddedMats)
                {
                    string matName = embeddedMat.name;
                    string matPath = matFolder + "/" + matName + ".mat";

                    // Get URP/Lit shader
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit == null)
                    {
                        log.AppendLine($"  ERROR: URP/Lit shader not found!");
                        errorCount++;
                        continue;
                    }

                    // Create or load external material
                    Material newMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    bool isNew = false;
                    if (newMat == null)
                    {
                        newMat = new Material(urpLit);
                        newMat.name = matName;
                        isNew = true;
                    }
                    else
                    {
                        // Ensure using URP/Lit
                        newMat.shader = urpLit;
                    }

                    // Copy base color from embedded material
                    Color baseColor = Color.white;
                    if (embeddedMat.HasProperty("_BaseColor"))
                        baseColor = embeddedMat.GetColor("_BaseColor");
                    else if (embeddedMat.HasProperty("_Color"))
                        baseColor = embeddedMat.GetColor("_Color");
                    newMat.SetColor("_BaseColor", baseColor);

                    // Also try to copy existing texture references from embedded mat
                    if (colorTex == null && embeddedMat.HasProperty("_BaseMap"))
                    {
                        Texture embTex = embeddedMat.GetTexture("_BaseMap");
                        if (embTex != null) colorTex = embTex as Texture2D;
                    }
                    if (colorTex == null && embeddedMat.HasProperty("_MainTex"))
                    {
                        Texture embTex = embeddedMat.GetTexture("_MainTex");
                        if (embTex != null) colorTex = embTex as Texture2D;
                    }

                    // Assign Color/BaseMap texture
                    if (colorTex != null)
                    {
                        newMat.SetTexture("_BaseMap", colorTex);
                        newMat.SetTexture("_MainTex", colorTex); // fallback property
                        textureCount++;
                    }

                    // Assign Normal map
                    if (normalTex != null)
                    {
                        // Ensure texture import type is NormalMap
                        string normalTexPath = AssetDatabase.GetAssetPath(normalTex);
                        if (!string.IsNullOrEmpty(normalTexPath))
                        {
                            TextureImporter texImporter = AssetImporter.GetAtPath(normalTexPath) as TextureImporter;
                            if (texImporter != null && texImporter.textureType != TextureImporterType.NormalMap)
                            {
                                texImporter.textureType = TextureImporterType.NormalMap;
                                texImporter.SaveAndReimport();
                                // Reload after reimport
                                normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexPath);
                            }
                        }
                        newMat.SetTexture("_BumpMap", normalTex);
                        newMat.SetFloat("_BumpScale", 1.0f);
                        newMat.EnableKeyword("_NORMALMAP");
                        textureCount++;
                    }

                    // Assign Metallic map
                    if (metallicTex != null)
                    {
                        newMat.SetTexture("_MetallicGlossMap", metallicTex);
                        newMat.SetFloat("_Metallic", 1.0f);
                        newMat.EnableKeyword("_METALLICSPECGLOSSMAP");
                        textureCount++;
                    }
                    else
                    {
                        newMat.SetFloat("_Metallic", 0.0f);
                    }

                    // Set smoothness (Tripo roughness = 1 - smoothness)
                    // Without combining textures, use a reasonable default
                    newMat.SetFloat("_Smoothness", 0.3f);

                    // === Critical: Shadow and rendering settings ===
                    // Surface type: Opaque
                    newMat.SetFloat("_Surface", 0f);
                    // Receive shadows ON
                    newMat.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                    // Alpha clip OFF for opaque
                    newMat.SetFloat("_AlphaClip", 0f);
                    // Render face: Front only
                    newMat.SetFloat("_Cull", 2f);
                    // Set render queue
                    newMat.renderQueue = -1; // default

                    // Save the material
                    if (isNew)
                    {
                        AssetDatabase.CreateAsset(newMat, matPath);
                    }
                    else
                    {
                        EditorUtility.SetDirty(newMat);
                    }

                    // Remap the FBX to use external material
                    var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), matName);
                    importer.AddRemap(identifier, newMat);
                }

                // Apply the remapping and reimport
                importer.SaveAndReimport();
                fixedCount++;

                string texInfo = $"color={colorTex != null} normal={normalTex != null} metal={metallicTex != null}";
                log.AppendLine($"  OK: {fbxName} ({embeddedMats.Length} mats, {texInfo})");
            }
            catch (System.Exception e)
            {
                errorCount++;
                log.AppendLine($"  ERROR: {fbxPath} - {e.Message}\n{e.StackTrace}");
            }
        }

        // Step 2: Fix renderer shadow settings on all scene objects
        int rendererFixed = 0;
        var sceneModel = GameObject.Find("[Scene]/scene-modle");
        if (sceneModel != null)
        {
            var renderers = sceneModel.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                bool changed = false;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.On)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    changed = true;
                }
                if (!r.receiveShadows)
                {
                    r.receiveShadows = true;
                    changed = true;
                }
                if (changed)
                {
                    EditorUtility.SetDirty(r.gameObject);
                    rendererFixed++;
                }
            }
            log.AppendLine($"\n[FixTripo] Fixed {rendererFixed} renderer shadow settings");
        }

        // Step 3: Mark scene dirty for save
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        AssetDatabase.SaveAssets();

        string summary = $"[FixTripo] DONE: {fixedCount} FBX fixed, {errorCount} errors, {textureCount} textures assigned, {rendererFixed} renderers fixed";
        log.Insert(0, summary + "\n\n");
        return log.ToString();
    }

    /// <summary>
    /// 根据角色在纹理字典中查找对应贴图
    /// </summary>
    static Texture2D FindTextureByRole(Dictionary<string, Texture2D> textures, string role)
    {
        foreach (var kvp in textures)
        {
            string name = kvp.Key;
            if (name.StartsWith("__path__")) continue;

            switch (role)
            {
                case "color":
                    if (name == "color" || name.Contains("basecolor") || name.Contains("albedo"))
                        return kvp.Value;
                    // Tripo special naming: tripo_image_* is often the color map
                    if (name.StartsWith("tripo_image"))
                        return kvp.Value;
                    break;
                case "normal":
                    if (name.Contains("normal") || name.Contains("_normal_bake"))
                        return kvp.Value;
                    break;
                case "metallic":
                    if (name.Contains("metallic"))
                        return kvp.Value;
                    break;
                case "roughness":
                    if (name.Contains("roughness"))
                        return kvp.Value;
                    break;
            }
        }
        return null;
    }
}
#endif
