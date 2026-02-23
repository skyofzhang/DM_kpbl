using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class FindUnusedSceneModels
{
    [MenuItem("Tools/Find Unused Scene Models")]
    public static string Execute()
    {
        var result = new System.Text.StringBuilder();
        result.AppendLine("=== Scene Model Usage Analysis ===\n");

        // 1. 获取 Models/Scene 下所有 FBX 文件及其 GUID
        string sceneModelsPath = "Assets/Models/Scene";
        var allModelGUIDs = new Dictionary<string, string>(); // GUID -> path

        if (!Directory.Exists(sceneModelsPath))
        {
            result.AppendLine("ERROR: Assets/Models/Scene directory not found");
            Debug.Log(result.ToString());
            return result.ToString();
        }

        var fbxFiles = Directory.GetFiles(sceneModelsPath, "*.fbx", SearchOption.AllDirectories);
        foreach (var fbx in fbxFiles)
        {
            string assetPath = fbx.Replace("\\", "/");
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(guid))
            {
                allModelGUIDs[guid] = assetPath;
                result.AppendLine($"Model: {assetPath}");
                result.AppendLine($"  GUID: {guid}");
            }
        }

        result.AppendLine($"\nTotal models in Scene dir: {allModelGUIDs.Count}\n");

        // 2. 扫描当前场景文件中引用了哪些 GUID
        string scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var usedGUIDs = new HashSet<string>();

        if (File.Exists(scenePath))
        {
            string sceneContent = File.ReadAllText(scenePath);
            foreach (var kvp in allModelGUIDs)
            {
                if (sceneContent.Contains(kvp.Key))
                {
                    usedGUIDs.Add(kvp.Key);
                }
            }
        }

        // 3. 也检查 Prefabs 目录
        var prefabFiles = Directory.GetFiles("Assets/Prefabs", "*.prefab", SearchOption.AllDirectories);
        foreach (var prefab in prefabFiles)
        {
            string content = File.ReadAllText(prefab);
            foreach (var kvp in allModelGUIDs)
            {
                if (content.Contains(kvp.Key) && !usedGUIDs.Contains(kvp.Key))
                {
                    usedGUIDs.Add(kvp.Key);
                }
            }
        }

        // 4. 输出结果
        result.AppendLine("=== USED Models ===");
        foreach (var guid in usedGUIDs)
        {
            result.AppendLine($"  ✅ {allModelGUIDs[guid]}");
        }

        result.AppendLine($"\n=== UNUSED Models (safe to delete) ===");
        var unusedGUIDs = allModelGUIDs.Keys.Where(g => !usedGUIDs.Contains(g)).ToList();
        foreach (var guid in unusedGUIDs)
        {
            result.AppendLine($"  ❌ {allModelGUIDs[guid]}");
        }

        result.AppendLine($"\nSummary: {usedGUIDs.Count} used, {unusedGUIDs.Count} unused out of {allModelGUIDs.Count} total");

        Debug.Log(result.ToString());
        return result.ToString();
    }
}
