using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

public class BuildWindowsAsync
{
    private static string _statusFile = "D:/claude/DM_kpbl/build_status.txt";

    public static string Execute()
    {
        // 先保存场景
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[BuildWindowsAsync] Scenes saved (phase 1).");

        File.WriteAllText(_statusFile, "BUILDING...");

        // 第一层delayCall: 等待编译完成（因为修改本脚本会触发重编译）
        EditorApplication.delayCall += () =>
        {
            // 第二层delayCall: 确保编译彻底完成后再保存+打包
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // 打包前再次保存所有场景
                    EditorSceneManager.SaveOpenScenes();
                    Debug.Log("[BuildWindowsAsync] Scenes saved (phase 2). Starting build...");

                    string buildPath = "D:/claude/DM_kpbl/Build/Windows/CapybaraDuel.exe";
                    string dir = Path.GetDirectoryName(buildPath);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var scenes = new string[] { "Assets/Scenes/MainScene.unity" };

                    var options = new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = buildPath,
                        target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.None
                    };

                    var report = BuildPipeline.BuildPlayer(options);

                    if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    {
                        long size = 0;
                        if (File.Exists(buildPath))
                            size = new FileInfo(buildPath).Length;
                        string sizeStr = size > 1024 * 1024
                            ? $"{size / 1024 / 1024}MB"
                            : $"{size / 1024}KB";
                        string msg = $"BUILD SUCCESS! Output: {buildPath} ({sizeStr}, errors: {report.summary.totalErrors})";
                        File.WriteAllText(_statusFile, msg);
                        Debug.Log(msg);
                    }
                    else
                    {
                        string msg = $"BUILD FAILED: {report.summary.result}, errors: {report.summary.totalErrors}";
                        File.WriteAllText(_statusFile, msg);
                        Debug.LogError(msg);
                    }
                }
                catch (System.Exception e)
                {
                    string msg = $"BUILD EXCEPTION: {e.Message}";
                    File.WriteAllText(_statusFile, msg);
                    Debug.LogError(msg);
                }
            };
        };

        return "Build started async (double-delayed). Check D:/claude/DM_kpbl/build_status.txt for result.";
    }
}
