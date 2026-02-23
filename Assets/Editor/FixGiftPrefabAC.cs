#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// 直接修改Prefab资产，给每个Gift Tier Prefab赋上对应的AnimatorController
/// 使用 PrefabUtility.LoadPrefabContents 直接编辑Prefab资产
/// </summary>
public static class FixGiftPrefabAC
{
    public static void Execute()
    {
        string outputDir = "Assets/Prefabs/Units/GiftTiers";
        string animDir = "Assets/Animations";

        for (int t = 2; t <= 6; t++)
        {
            string prefabPath = $"{outputDir}/Gift{t}Unit.prefab";
            string acPath = $"{animDir}/AC_Gift{t}.controller";

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(acPath);
            if (ac == null)
            {
                Debug.LogError($"[FixAC] Gift{t}: AC not found at {acPath}");
                continue;
            }

            // 使用 LoadPrefabContents 直接编辑Prefab（不需要场景实例）
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[FixAC] Gift{t}: Failed to load prefab");
                continue;
            }

            var animator = prefabRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = prefabRoot.AddComponent<Animator>();
                Debug.Log($"[FixAC] Gift{t}: Added Animator component");
            }

            // 赋AC
            animator.runtimeAnimatorController = ac;

            // 确认Avatar也在
            if (animator.avatar == null)
            {
                // 从对应Pushing FBX查找avatar
                string root = "Assets/Models/Kpbl/5个礼物召唤的水豚单位";
                string[] dirs = { "", "卡皮巴拉礼物单位2", "卡皮巴拉礼物单位3", "卡皮巴拉礼物单位4", "卡皮巴拉礼物单位5", "卡皮巴拉礼物单位6" };
                string pushPath = $"{root}/{dirs[t]}/gift{t}-Pushing.fbx";
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(pushPath);
                foreach (var asset in allAssets)
                {
                    if (asset is Avatar av)
                    {
                        animator.avatar = av;
                        Debug.Log($"[FixAC] Gift{t}: Avatar assigned = {av.name}");
                        break;
                    }
                }
            }

            // 确保applyRootMotion=false
            animator.applyRootMotion = false;

            // 保存回Prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            // 验证
            var verify = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var vAnim = verify.GetComponent<Animator>();
            Debug.Log($"[FixAC] Gift{t}: VERIFIED AC={(vAnim.runtimeAnimatorController != null ? vAnim.runtimeAnimatorController.name : "NULL")}, " +
                $"Avatar={(vAnim.avatar != null ? vAnim.avatar.name : "NULL")}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FixAC] === ALL DONE ===");
    }
}
#endif
