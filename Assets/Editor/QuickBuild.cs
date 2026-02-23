#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Linq;

/// <summary>
/// 一键打包脚本 — 通过Coplay execute_script调用
/// 流程: 保存场景 → 编译检查 → 打包到上架目录
/// 输出: Build/capybara_duel_1.0.0/capybara_duel.exe
/// </summary>
public class QuickBuild
{
    private const string GAME_NAME = "capybara_duel";
    private const string VERSION = "1.0.1";

    [MenuItem("CapybaraDuel/Quick Build (保存+打包)", false, 201)]
    public static void MenuBuild()
    {
        string result = Execute();
        Debug.Log(result);
    }

    public static string Execute()
    {
        // ==============================
        // Step 1: 保存当前场景
        // ==============================
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty)
        {
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[QuickBuild] 场景已保存: {scene.path}");
        }
        else
        {
            Debug.Log($"[QuickBuild] 场景无修改，跳过保存: {scene.path}");
        }

        // ==============================
        // Step 2: 确定输出目录（上架目录）
        // ==============================
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string versionFolder = $"{GAME_NAME}_{VERSION}";
        string outputDir = Path.Combine(projectRoot, "Build", versionFolder);
        string exePath = Path.Combine(outputDir, $"{GAME_NAME}.exe");

        // 清理旧构建
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
            Debug.Log($"[QuickBuild] 已清理旧构建: {versionFolder}");
        }
        Directory.CreateDirectory(outputDir);

        // ==============================
        // Step 3: 收集场景
        // ==============================
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            string currentScene = scene.path;
            if (!string.IsNullOrEmpty(currentScene))
                scenes = new[] { currentScene };
            else
                return "FAILED: 没有可打包的场景";
        }

        // ==============================
        // Step 4: 执行打包
        // ==============================
        Debug.Log($"[QuickBuild] 开始打包... 场景: {string.Join(", ", scenes)}");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            float sizeMB = report.summary.totalSize / (1024f * 1024f);
            long exeSize = 0;
            if (File.Exists(exePath))
                exeSize = new FileInfo(exePath).Length;
            string exeSizeStr = exeSize > 1024 * 1024
                ? $"{exeSize / 1024 / 1024}MB"
                : $"{exeSize / 1024}KB";

            return $"SUCCESS: {versionFolder}/{GAME_NAME}.exe ({exeSizeStr}, total {sizeMB:F1}MB, errors: 0) | 场景: {string.Join(", ", scenes)}";
        }
        else
        {
            return $"FAILED: {report.summary.result}, errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}";
        }
    }
}
#endif
