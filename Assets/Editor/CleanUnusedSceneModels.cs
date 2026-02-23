using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class CleanUnusedSceneModels
{
    [MenuItem("Tools/Clean Unused Scene Models (Dry Run)")]
    public static void DryRun() { Run(false); }

    [MenuItem("Tools/Clean Unused Scene Models (DELETE)")]
    public static void Delete() { Run(true); }

    static void Run(bool doDelete)
    {
        // Step 1: Collect all unique base names from scene hierarchy under scene-modle
        var sceneRoot = GameObject.Find("scene-modle");
        if (sceneRoot == null) { Debug.LogError("scene-modle not found in hierarchy"); return; }

        var usedNames = new HashSet<string>();
        foreach (Transform child in sceneRoot.transform)
        {
            // Strip instance suffix like " (1)", " (23)" etc
            string baseName = child.name;
            int parenIdx = baseName.LastIndexOf(" (");
            if (parenIdx > 0) baseName = baseName.Substring(0, parenIdx);
            usedNames.Add(baseName);
        }
        Debug.Log($"[CleanModels] Scene uses {usedNames.Count} unique model names");

        // Step 2: Scan shuchu-ceshi and shuchu directories
        string[] scanDirs = new string[]
        {
            "Assets/Models/Scene/shuchu-ceshi",
            "Assets/Models/Scene/shuchu/mod",
            "Assets/Models/Scene/shuchu/pre",
            "Assets/Models/Scene/shuchu/other"
        };

        int usedCount = 0, unusedCount = 0;
        long unusedBytes = 0;
        var toDelete = new List<string>();

        foreach (var scanDir in scanDirs)
        {
            string fullScanDir = Path.Combine(Application.dataPath.Replace("/Assets", ""), scanDir);
            if (!Directory.Exists(fullScanDir)) continue;

            // Each subdirectory is a model package (name.meta + name/ folder)
            foreach (var subDir in Directory.GetDirectories(fullScanDir))
            {
                string dirName = Path.GetFileName(subDir);
                if (dirName == "__MACOSX") continue;

                if (usedNames.Contains(dirName))
                {
                    usedCount++;
                }
                else
                {
                    unusedCount++;
                    long dirSize = Directory.GetFiles(subDir, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);
                    unusedBytes += dirSize;
                    toDelete.Add(subDir);

                    // Also mark the .meta file
                    string metaFile = subDir + ".meta";
                    if (File.Exists(metaFile)) toDelete.Add(metaFile);
                }
            }

            // Also check for orphan .meta files at scan level (meta without matching folder)
            foreach (var metaFile in Directory.GetFiles(fullScanDir, "*.meta"))
            {
                string baseName = Path.GetFileNameWithoutExtension(metaFile);
                if (baseName == "mod" || baseName == "pre" || baseName == "other") continue;
                string matchingDir = Path.Combine(fullScanDir, baseName);
                if (!Directory.Exists(matchingDir) && !usedNames.Contains(baseName))
                {
                    // Orphan meta, safe to delete
                    if (!toDelete.Contains(metaFile))
                    {
                        toDelete.Add(metaFile);
                        unusedCount++;
                    }
                }
            }
        }

        float unusedMB = unusedBytes / (1024f * 1024f);
        Debug.Log($"[CleanModels] Used: {usedCount} model dirs | Unused: {unusedCount} items | Unused size: {unusedMB:F1} MB");

        if (doDelete && toDelete.Count > 0)
        {
            int deleted = 0;
            foreach (var path in toDelete)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    deleted++;
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                }
            }
            AssetDatabase.Refresh();
            Debug.Log($"[CleanModels] DELETED {deleted} items, freed ~{unusedMB:F1} MB");
        }
        else if (!doDelete)
        {
            Debug.Log($"[CleanModels] DRY RUN - would delete {toDelete.Count} items. Use 'DELETE' menu to actually remove.");
            // Print first 20
            for (int i = 0; i < Mathf.Min(20, toDelete.Count); i++)
                Debug.Log($"  Would delete: {toDelete[i]}");
            if (toDelete.Count > 20) Debug.Log($"  ... and {toDelete.Count - 20} more");
        }
    }
}
