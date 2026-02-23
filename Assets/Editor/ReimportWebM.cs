#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ReimportWebM
{
    public static string Execute()
    {
        string[] webmPaths = {
            "Assets/Art/GiftGifs/tier1-sp.webm",
            "Assets/Art/GiftGifs/tier2-sp.webm",
            "Assets/Art/GiftGifs/tier3-sp.webm",
            "Assets/Art/GiftGifs/tier4-sp.webm",
            "Assets/Art/GiftGifs/tier5-sp.webm",
            "Assets/Art/GiftGifs/tier6-sp.webm"
        };

        int reimported = 0;
        var sb = new System.Text.StringBuilder();

        foreach (var path in webmPaths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(path);
            if (clip != null)
            {
                // 检查视频信息
                sb.AppendLine($"{path}: {clip.width}x{clip.height}, {clip.frameCount}frames, {clip.frameRate}fps, {clip.length:F2}s");
                reimported++;
            }
            else
            {
                // 强制重新导入
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(path);
                if (clip != null)
                {
                    sb.AppendLine($"{path}: REIMPORTED → {clip.width}x{clip.height}, {clip.frameCount}frames");
                    reimported++;
                }
                else
                {
                    sb.AppendLine($"{path}: FAILED to load!");
                }
            }
        }

        // 强制刷新所有被替换的WebM
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        return $"Checked {reimported}/6 WebM files:\n{sb}";
    }
}
#endif
