using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GIF → Sprite帧序列 转换工具
/// 使用 System.Drawing 解码GIF的每一帧，导出为PNG，自动设置为Sprite
///
/// 使用方法：
/// 1. 将GIF文件放入 Assets/Art/GiftGifs/ 目录
/// 2. 菜单 Tools > Convert GIFs to Sprites
/// 3. 自动输出到 Assets/Resources/GiftAnimations/tierN/
///
/// GIF命名规则：tier1.gif, tier2.gif, ... tier6.gif
/// 或自定义名称，工具会按文件名创建子文件夹
/// </summary>
public class GifToSpriteConverter : EditorWindow
{
    private string inputFolder = "Assets/Art/GiftGifs";
    private string outputFolder = "Assets/Resources/GiftAnimations";
    private int maxFrames = 120; // 最大帧数限制
    private bool overwrite = true;

    [MenuItem("Tools/Convert GIFs to Sprites")]
    static void ShowWindow()
    {
        GetWindow<GifToSpriteConverter>("GIF to Sprite");
    }

    private void OnGUI()
    {
        GUILayout.Label("GIF → Sprite 帧序列转换", EditorStyles.boldLabel);
        GUILayout.Space(10);

        inputFolder = EditorGUILayout.TextField("GIF输入目录", inputFolder);
        outputFolder = EditorGUILayout.TextField("Sprite输出目录", outputFolder);
        maxFrames = EditorGUILayout.IntField("最大帧数", maxFrames);
        overwrite = EditorGUILayout.Toggle("覆盖已有文件", overwrite);

        GUILayout.Space(10);

        if (GUILayout.Button("开始转换", GUILayout.Height(40)))
        {
            ConvertAllGifs();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "GIF命名规则：tier1.gif ~ tier6.gif（对应6种礼物等级）\n" +
            "或其他名称（会按文件名创建子文件夹）\n\n" +
            "输出路径示例：Resources/GiftAnimations/tier1/frame_000.png\n\n" +
            "注意：需要 .NET Framework 支持 System.Drawing\n" +
            "如果编译报错，请改用下方的手动PNG帧导入方式",
            MessageType.Info);

        GUILayout.Space(10);
        GUILayout.Label("替代方案：手动PNG帧导入", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "如果 System.Drawing 不可用：\n" +
            "1. 用在线工具(ezgif.com)将GIF拆分为PNG帧\n" +
            "2. 将PNG帧放入 Assets/Resources/GiftAnimations/tierN/\n" +
            "3. 命名为 frame_000.png, frame_001.png ...\n" +
            "4. 选中所有PNG → Inspector → Texture Type: Sprite\n\n" +
            "下方按钮可批量设置已导入PNG的Import Settings",
            MessageType.Info);

        if (GUILayout.Button("批量设置PNG为Sprite (已导入的帧)", GUILayout.Height(30)))
        {
            BatchSetSpriteImportSettings();
        }
    }

    private void ConvertAllGifs()
    {
        string fullInputPath = Path.GetFullPath(inputFolder);
        if (!Directory.Exists(fullInputPath))
        {
            Directory.CreateDirectory(fullInputPath);
            EditorUtility.DisplayDialog("提示",
                $"已创建GIF输入目录：\n{fullInputPath}\n\n请将GIF文件放入后重新转换",
                "确定");
            return;
        }

        var gifFiles = Directory.GetFiles(fullInputPath, "*.gif");
        if (gifFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("提示",
                $"未找到GIF文件\n目录: {fullInputPath}",
                "确定");
            return;
        }

        int totalConverted = 0;
        foreach (var gifPath in gifFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(gifPath);
            string tierFolder = Path.Combine(outputFolder, fileName);
            string fullOutputPath = Path.GetFullPath(tierFolder);

            if (!Directory.Exists(fullOutputPath))
                Directory.CreateDirectory(fullOutputPath);

            try
            {
                int frameCount = ExtractGifFrames(gifPath, fullOutputPath);
                totalConverted += frameCount;
                Debug.Log($"[GifConverter] {fileName}.gif → {frameCount} frames");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GifConverter] Failed to convert {fileName}.gif: {e.Message}");
                Debug.LogWarning("[GifConverter] System.Drawing may not be available. Use manual PNG import instead.");
            }
        }

        AssetDatabase.Refresh();

        // 批量设置导入为Sprite
        BatchSetSpriteImportSettings();

        EditorUtility.DisplayDialog("转换完成",
            $"共转换 {gifFiles.Length} 个GIF，{totalConverted} 帧",
            "确定");
    }

    private int ExtractGifFrames(string gifPath, string outputDir)
    {
        // System.Drawing.Image 支持GIF多帧读取
        using (var gif = Image.FromFile(gifPath))
        {
            var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            int frameCount = Mathf.Min(gif.GetFrameCount(dimension), maxFrames);

            for (int i = 0; i < frameCount; i++)
            {
                gif.SelectActiveFrame(dimension, i);
                string framePath = Path.Combine(outputDir, $"frame_{i:D3}.png");

                if (!overwrite && File.Exists(framePath))
                    continue;

                using (var bmp = new Bitmap(gif.Width, gif.Height))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.DrawImage(gif, 0, 0, gif.Width, gif.Height);
                    }
                    bmp.Save(framePath, ImageFormat.Png);
                }
            }

            return frameCount;
        }
    }

    /// <summary>批量将 GiftAnimations 目录下的PNG设置为Sprite导入</summary>
    private void BatchSetSpriteImportSettings()
    {
        string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { outputFolder });
        int changed = 0;

        foreach (string guid in pngGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png")) continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool needsReimport = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                needsReimport = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                needsReimport = true;
            }
            // 保持原始尺寸，不压缩（GIF动画需要清晰）
            if (importer.maxTextureSize < 1024)
            {
                importer.maxTextureSize = 1024;
                needsReimport = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
                changed++;
            }
        }

        Debug.Log($"[GifConverter] Sprite import settings updated for {changed} files");
    }
}
