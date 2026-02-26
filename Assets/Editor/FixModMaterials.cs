#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 修复 shuchu/shuchu-ceshi 下 mod_* 模型材质
/// 问题: 使用 Unlit/Texture shader，完全不接收阴影
/// 方案: 改为 URP/Lit，保留贴图，启用阴影接收
/// </summary>
public class FixModMaterials
{
    [MenuItem("CapybaraDuel/Fix Mod Materials (修复mod模型阴影)", false, 302)]
    public static void MenuFix()
    {
        string result = Execute();
        Debug.Log(result);
    }

    public static string Execute()
    {
        var log = new System.Text.StringBuilder();
        int matFixed = 0;
        int matSkipped = 0;
        int rendererFixed = 0;

        // Search paths
        string[] searchPaths = new[]
        {
            "Assets/Models/Scene/shuchu-ceshi",
            "Assets/Models/Scene/shuchu"
        };

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            return "ERROR: Universal Render Pipeline/Lit shader not found!";
        }

        // Step 1: Find and fix all .mat files
        foreach (string searchPath in searchPaths)
        {
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { searchPath });

            foreach (string guid in matGuids)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                string oldShaderName = mat.shader.name;

                // Only fix Unlit materials
                if (oldShaderName.Contains("Unlit") || oldShaderName.Contains("unlit"))
                {
                    // Save the current main texture before switching shader
                    Texture mainTex = null;
                    Color mainColor = Color.white;

                    if (mat.HasProperty("_MainTex"))
                        mainTex = mat.GetTexture("_MainTex");
                    if (mat.HasProperty("_Color"))
                        mainColor = mat.GetColor("_Color");

                    // Switch to URP/Lit
                    mat.shader = urpLit;

                    // Reassign the texture to URP properties
                    if (mainTex != null)
                    {
                        mat.SetTexture("_BaseMap", mainTex);
                        mat.SetTexture("_MainTex", mainTex); // URP also uses this as fallback
                    }

                    // Set base color
                    mat.SetColor("_BaseColor", mainColor);

                    // Configure for proper shadow receiving
                    mat.SetFloat("_Surface", 0f);        // Opaque
                    mat.SetFloat("_AlphaClip", 0f);      // No alpha clip
                    mat.SetFloat("_Smoothness", 0.3f);    // Reasonable default
                    mat.SetFloat("_Metallic", 0f);        // Non-metallic
                    mat.SetFloat("_Cull", 2f);            // Back face culling

                    // Enable shadow receiving (disable the OFF keyword)
                    mat.DisableKeyword("_RECEIVE_SHADOWS_OFF");

                    // Set render queue to default
                    mat.renderQueue = -1;

                    EditorUtility.SetDirty(mat);
                    matFixed++;
                    log.AppendLine($"  FIXED: {matPath} ({oldShaderName} → URP/Lit, tex={mainTex != null})");
                }
                else
                {
                    matSkipped++;
                    log.AppendLine($"  SKIP: {matPath} (already {oldShaderName})");
                }
            }
        }

        // Step 2: Fix all renderer shadow settings for mod_ objects in scene
        var sceneModel = GameObject.Find("[Scene]/scene-modle");
        if (sceneModel != null)
        {
            var renderers = sceneModel.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                // Check if this is a mod_ object
                if (!r.gameObject.name.StartsWith("mod_")) continue;

                bool changed = false;

                // Enable shadow casting
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.On)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    changed = true;
                }

                // Enable shadow receiving
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
        }

        // Save all changes
        AssetDatabase.SaveAssets();

        // Mark scene dirty
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        string summary = $"[FixMod] DONE: {matFixed} materials fixed (Unlit→URP/Lit), {matSkipped} skipped, {rendererFixed} renderers fixed";
        log.Insert(0, summary + "\n\n");
        return log.ToString();
    }
}
#endif
