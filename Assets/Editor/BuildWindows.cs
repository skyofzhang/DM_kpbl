using UnityEditor;
using UnityEngine;
using System.IO;

public class BuildWindows
{
    public static string Execute()
    {
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
            return $"BUILD SUCCESS! Output: {buildPath} (exe size: {size / 1024 / 1024}MB, total errors: {report.summary.totalErrors})";
        }
        else
        {
            return $"BUILD FAILED: {report.summary.result}, errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}";
        }
    }
}
