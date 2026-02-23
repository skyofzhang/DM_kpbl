using UnityEngine;
using UnityEditor;
using System.IO;

public class CopyNetworkFiles
{
    public static void Execute()
    {
        string srcDir = @"\\192.168.1.198\共享\AI目录\羊羊对决";
        string dstDir = @"C:\temp\sheep_code";

        if (!Directory.Exists(srcDir))
        {
            Debug.LogError($"[CopyNetwork] Source not found: {srcDir}");
            // Try listing parent
            string parent = @"\\192.168.1.198\共享\AI目录";
            if (Directory.Exists(parent))
            {
                foreach (var d in Directory.GetDirectories(parent))
                    Debug.Log($"[CopyNetwork] Found dir: {d}");
            }
            else
            {
                Debug.LogError($"[CopyNetwork] Parent also not found: {parent}");
            }
            return;
        }

        if (!Directory.Exists(dstDir))
            Directory.CreateDirectory(dstDir);

        // Copy all files recursively
        int count = 0;
        foreach (var file in Directory.GetFiles(srcDir, "*.*", SearchOption.AllDirectories))
        {
            string rel = file.Substring(srcDir.Length + 1);
            string dst = Path.Combine(dstDir, rel);
            string dstFolder = Path.GetDirectoryName(dst);
            if (!Directory.Exists(dstFolder))
                Directory.CreateDirectory(dstFolder);
            File.Copy(file, dst, true);
            count++;
            Debug.Log($"[CopyNetwork] Copied: {rel}");
        }
        Debug.Log($"[CopyNetwork] Done! Copied {count} files to {dstDir}");
    }
}
