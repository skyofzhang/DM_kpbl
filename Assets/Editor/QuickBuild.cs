#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Linq;

/// <summary>
/// 一键打包脚本 — 通过Coplay execute_script调用
/// 流程: 保存场景 → IL2CPP加密 → 打包到上架目录
/// 输出: Build/capybaraduel_1.0.4/capybaraduel.exe
///
/// v1.0.4变更:
/// - 切换到IL2CPP后端（代码加密/混淆，防反编译）
/// - 启用StripUnusedMeshComponents（减小包体）
/// - 设置ManagedStrippingLevel为Medium（移除未使用的.NET代码）
/// </summary>
public class QuickBuild
{
    private const string GAME_NAME = "capybaraduel";
    private const string VERSION = "1.0.4";

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
        // Step 2: 设置打包后端 + 优化选项
        // ==============================
        // IL2CPP后端：代码编译为C++原生二进制，防止dnSpy反编译
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
        // 移除未使用的Mesh组件数据
        PlayerSettings.stripUnusedMeshComponents = true;
        // 中等裁剪：移除未使用的.NET代码，减小包体
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Minimal);

        Debug.Log($"[QuickBuild] IL2CPP打包模式（代码加密，防反编译）");

        // ==============================
        // Step 3: 确定输出目录（上架目录）
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
        // Step 4: 收集场景
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
        // Step 5: 执行打包
        // ==============================
        Debug.Log($"[QuickBuild] 开始打包(IL2CPP)... 场景: {string.Join(", ", scenes)}");

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

            return $"SUCCESS: {versionFolder}/{GAME_NAME}.exe ({exeSizeStr}, total {sizeMB:F1}MB, errors: 0, IL2CPP) | 场景: {string.Join(", ", scenes)}";
        }
        else
        {
            return $"FAILED: {report.summary.result}, errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}";
        }
    }
}
#endif
