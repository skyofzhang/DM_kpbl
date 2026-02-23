#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace CapybaraDuel.Editor
{
    /// <summary>
    /// 抖音直播小玩法 - Windows Standalone 打包工具
    ///
    /// 输出规范:
    ///   capybara_duel_1.0.0/
    ///   ├── capybara_duel.exe
    ///   ├── UnityPlayer.dll
    ///   └── capybara_duel_Data/
    ///
    /// zip命名: capybara_duel_1.0.0.zip
    /// </summary>
    public static class BuildTool
    {
        private const string GAME_NAME = "capybara_duel";
        private const string VERSION = "1.0.0";

        [MenuItem("CapybaraDuel/Build Windows (抖音上架)", false, 200)]
        public static void BuildWindows()
        {
            // 输出目录
            string buildFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build");
            string versionFolder = $"{GAME_NAME}_{VERSION}";
            string outputDir = Path.Combine(buildFolder, versionFolder);
            string exePath = Path.Combine(outputDir, $"{GAME_NAME}.exe");

            // 清理旧构建
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
                Debug.Log($"[BuildTool] 已清理旧构建: {outputDir}");
            }
            Directory.CreateDirectory(outputDir);

            // 收集所有启用的场景
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // 如果Build Settings里没有场景，用当前场景
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                if (!string.IsNullOrEmpty(currentScene))
                    scenes = new[] { currentScene };
                else
                {
                    EditorUtility.DisplayDialog("打包失败", "没有找到可打包的场景。请在 Build Settings 中添加场景。", "确定");
                    return;
                }
            }

            Debug.Log($"[BuildTool] 开始打包 Windows Standalone");
            Debug.Log($"[BuildTool] 场景: {string.Join(", ", scenes)}");
            Debug.Log($"[BuildTool] 输出: {exePath}");

            // 打包选项
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            // 执行打包
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                float sizeMB = (long)report.summary.totalSize / (1024f * 1024f);

                Debug.Log($"[BuildTool] ✅ 打包成功！");
                Debug.Log($"[BuildTool] 大小: {sizeMB:F1} MB");
                Debug.Log($"[BuildTool] 路径: {outputDir}");
                Debug.Log($"[BuildTool] 下一步: 将 {versionFolder} 文件夹压缩为 {versionFolder}.zip 上传抖音平台");

                // 打开输出目录
                EditorUtility.RevealInFinder(outputDir);

                EditorUtility.DisplayDialog("打包成功",
                    $"打包完成！大小: {sizeMB:F1} MB\n\n" +
                    $"输出路径:\n{outputDir}\n\n" +
                    $"请将此文件夹压缩为:\n{versionFolder}.zip\n然后上传到抖音开放平台。",
                    "确定");
            }
            else
            {
                Debug.LogError($"[BuildTool] ❌ 打包失败: {report.summary.result}");
                Debug.LogError($"[BuildTool] 错误数: {report.summary.totalErrors}");

                EditorUtility.DisplayDialog("打包失败",
                    $"打包失败: {report.summary.result}\n错误数: {report.summary.totalErrors}\n\n请查看 Console 窗口获取详细错误信息。",
                    "确定");
            }
        }
    }
}
#endif
