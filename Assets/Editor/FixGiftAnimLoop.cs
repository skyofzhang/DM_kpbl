#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 修复所有Gift Pushing FBX的动画循环设置
/// 同时检查并修复所有相关的动画问题：Loop、WrapMode、Root Motion等
/// </summary>
public static class FixGiftAnimLoop
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
            if (importer == null) { Debug.LogWarning($"[FixLoop] Gift{cfg.tier}: importer not found"); continue; }

            // 获取动画clip设置
            var clipAnimations = importer.clipAnimations;

            // 如果没有自定义clip设置，从默认clip复制
            if (clipAnimations == null || clipAnimations.Length == 0)
                clipAnimations = importer.defaultClipAnimations;

            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                Debug.LogWarning($"[FixLoop] Gift{cfg.tier}: No animation clips found");
                continue;
            }

            bool changed = false;
            foreach (var clip in clipAnimations)
            {
                // 设置循环
                if (!clip.loopTime)
                {
                    clip.loopTime = true;
                    changed = true;
                }
                // 锁定Root Motion（防止模型位移漂移）
                if (!clip.lockRootRotation)
                {
                    clip.lockRootRotation = true;
                    changed = true;
                }
                if (!clip.lockRootHeightY)
                {
                    clip.lockRootHeightY = true;
                    changed = true;
                }
                if (!clip.lockRootPositionXZ)
                {
                    clip.lockRootPositionXZ = true;
                    changed = true;
                }
                // 保持原始位置（防止root motion导致偏移）
                if (!clip.keepOriginalPositionXZ)
                {
                    clip.keepOriginalPositionXZ = true;
                    changed = true;
                }
                if (!clip.keepOriginalPositionY)
                {
                    clip.keepOriginalPositionY = true;
                    changed = true;
                }
                if (!clip.keepOriginalOrientation)
                {
                    clip.keepOriginalOrientation = true;
                    changed = true;
                }

                Debug.Log($"[FixLoop] Gift{cfg.tier} clip '{clip.name}': loopTime={clip.loopTime}, " +
                          $"lockRootRot={clip.lockRootRotation}, lockRootY={clip.lockRootHeightY}, lockRootXZ={clip.lockRootPositionXZ}");
            }

            if (changed)
            {
                importer.clipAnimations = clipAnimations;
                importer.SaveAndReimport();
                Debug.Log($"[FixLoop] Gift{cfg.tier}: Reimported with loop + root motion lock");
            }
            else
            {
                Debug.Log($"[FixLoop] Gift{cfg.tier}: Already correct, no changes needed");
            }
        }

        // 重建Prefab以应用更新后的动画
        Debug.Log("[FixLoop] Now rebuilding prefabs...");
        RunGiftTierPrefabGen.Execute();

        Debug.Log("[FixLoop] === ALL DONE ===");
    }
}
#endif
