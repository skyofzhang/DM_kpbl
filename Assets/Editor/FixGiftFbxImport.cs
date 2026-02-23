#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 修复所有Gift Pushing FBX的导入设置，确保生成Avatar
/// </summary>
public static class FixGiftFbxImport
{
    public static void Execute()
    {
        string root = "Assets/Models/Kpbl/5个礼物召唤的水豚单位";
        var tiers = new (int tier, string dir)[]
        {
            (2, "卡皮巴拉礼物单位2"),
            (3, "卡皮巴拉礼物单位3"),
            (4, "卡皮巴拉礼物单位4"),
            (5, "卡皮巴拉礼物单位5"),
            (6, "卡皮巴拉礼物单位6"),
        };

        foreach (var cfg in tiers)
        {
            string pushPath = $"{root}/{cfg.dir}/gift{cfg.tier}-Pushing.fbx";
            var importer = AssetImporter.GetAtPath(pushPath) as ModelImporter;
            if (importer == null) { Debug.LogWarning($"[FixImport] Gift{cfg.tier}: importer not found"); continue; }

            Debug.Log($"[FixImport] Gift{cfg.tier} BEFORE: animType={importer.animationType}, " +
                      $"importAnim={importer.importAnimation}, generateAnimClips={importer.generateAnimations}");

            // 强制设置为Generic + 导入动画
            bool needReimport = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                needReimport = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                needReimport = true;
            }

            if (needReimport)
            {
                importer.SaveAndReimport();
                Debug.Log($"[FixImport] Gift{cfg.tier}: Reimported with Generic animation type");
            }

            // 检查reimport后的avatar
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(pushPath);
            Avatar avatar = null;
            int clipCount = 0;
            foreach (var asset in allAssets)
            {
                if (asset is Avatar av) avatar = av;
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) clipCount++;
            }

            Debug.Log($"[FixImport] Gift{cfg.tier} AFTER: avatar={(avatar != null ? avatar.name : "NULL")}, " +
                      $"clips={clipCount}, animType={importer.animationType}");

            // 列出所有子资产
            string assetList = "";
            foreach (var asset in allAssets)
            {
                assetList += $"\n  [{asset.GetType().Name}] {asset.name}";
            }
            Debug.Log($"[FixImport] Gift{cfg.tier} all sub-assets:{assetList}");
        }

        Debug.Log("[FixImport] === DONE ===");
    }
}
#endif
