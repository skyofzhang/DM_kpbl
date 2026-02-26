#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// 贴图批量优化工具
/// - UI贴图: maxSize 1024, Compressed
/// - 背景贴图: maxSize 1024 (原3200太大), Compressed
/// - 序列帧: maxSize 512, Compressed (礼物动画已迁移到WebM)
/// - 模型贴图: maxSize 2048, Compressed
///
/// 菜单: CapybaraDuel/Optimize All Textures
/// </summary>
public class TextureOptimizer
{
    [MenuItem("CapybaraDuel/Optimize All Textures", false, 301)]
    public static void OptimizeAll()
    {
        string result = Execute();
        Debug.Log(result);
        // 禁止DisplayDialog(#19踩坑: 会阻塞自动化)，只用Debug.Log
    }

    public static string Execute()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art", "Assets/Resources" });
        int optimized = 0;
        int skipped = 0;
        var changedPaths = new System.Collections.Generic.List<string>();

        // 批量模式: 先收集所有变更，再统一reimport
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { skipped++; continue; }

                bool changed = false;
                int targetMaxSize = GetTargetMaxSize(path);
                var targetCompression = TextureImporterCompression.Compressed;

                if (importer.maxTextureSize > targetMaxSize)
                {
                    importer.maxTextureSize = targetMaxSize;
                    changed = true;
                }

                if (importer.textureCompression != targetCompression)
                {
                    importer.textureCompression = targetCompression;
                    changed = true;
                }

                // UI贴图禁用mipmap
                if (path.Contains("/UI/") || path.Contains("/GiftGifs/"))
                {
                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        changed = true;
                    }
                }

                // Standalone平台: BC7压缩
                var platformSettings = importer.GetPlatformTextureSettings("Standalone");
                if (!platformSettings.overridden || platformSettings.maxTextureSize > targetMaxSize)
                {
                    platformSettings.overridden = true;
                    platformSettings.maxTextureSize = targetMaxSize;
                    platformSettings.format = TextureImporterFormat.BC7;
                    platformSettings.compressionQuality = 100;
                    importer.SetPlatformTextureSettings(platformSettings);
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    changedPaths.Add(path);
                    optimized++;
                }
                else
                {
                    skipped++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // Log changed files for reference
        if (changedPaths.Count > 0)
        {
            Debug.Log($"[TextureOptimizer] 已优化贴图清单 (前20): {string.Join(", ", changedPaths.GetRange(0, System.Math.Min(20, changedPaths.Count)))}");
        }

        return $"[TextureOptimizer] 优化完成: {optimized}张贴图已优化, {skipped}张跳过 (共{guids.Length}张)";
    }

    /// <summary>根据路径确定目标最大尺寸</summary>
    private static int GetTargetMaxSize(string path)
    {
        // 背景大图: 3200→1024 (节省75% VRAM)
        if (path.Contains("/Backgrounds/"))
            return 1024;

        // 序列帧(已弃用,但保留的): 512即可
        if (path.Contains("/GiftGifs/") && path.Contains("frame_"))
            return 512;

        // 礼物预览图: 512
        if (path.Contains("_preview"))
            return 512;

        // UI元素: 1024
        if (path.Contains("/UI/") || path.Contains("/BattleUI/"))
            return 1024;

        // VFX贴图: 256足够
        if (path.Contains("/VFX/"))
            return 256;

        // 升级材质贴图: 256
        if (path.Contains("/Upgrades/") || path.Contains("mat_upgrade"))
            return 256;

        // 模型贴图保持2048
        return 2048;
    }
}
#endif
