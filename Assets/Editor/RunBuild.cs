#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

public class RunBuild
{
    public static string Execute()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDir = Path.Combine(projectRoot, "Build", "capybara_duel_1.0.0");
        string exePath = Path.Combine(outputDir, "capybara_duel.exe");

        // 清理旧构建
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }
        Directory.CreateDirectory(outputDir);

        // 收集场景
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(currentScene))
                scenes = new[] { currentScene };
            else
                return "ERROR: No scenes found!";
        }

        Debug.Log($"[RunBuild] Starting build... Scenes: {string.Join(", ", scenes)}");

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
            float sizeMB = (long)report.summary.totalSize / (1024f * 1024f);
            return $"SUCCESS: Build completed! Size: {sizeMB:F1} MB, Path: {outputDir}, Scenes: {string.Join(", ", scenes)}";
        }
        else
        {
            return $"FAILED: {report.summary.result}, Errors: {report.summary.totalErrors}, Warnings: {report.summary.totalWarnings}";
        }
    }
}
#endif
