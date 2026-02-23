using UnityEngine;
using UnityEditor;
using CapybaraDuel.VFX;

/// <summary>
/// 一键将10个升级材质球赋值到KpblUnit预制体的UnitVisualEffect组件
/// </summary>
public class AssignUpgradeMaterials
{
    [MenuItem("CapybaraDuel/4b. Assign Upgrade Materials to Prefab")]
    public static void Execute()
    {
        string prefabPath = "Assets/Prefabs/Units/KpblUnit.prefab";
        string matDir = "Assets/Materials/UpgradeLevels";

        // 加载prefab
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Prefab not found: {prefabPath}");
            return;
        }

        var vfx = prefab.GetComponent<UnitVisualEffect>();
        if (vfx == null)
        {
            // 组件不存在，自动添加到prefab
            Debug.LogWarning("[AssignUpgradeMaterials] UnitVisualEffect not found on prefab, adding it...");
            vfx = prefab.AddComponent<UnitVisualEffect>();
            EditorUtility.SetDirty(prefab);
        }

        // 加载10个材质
        Material[] mats = new Material[10];
        for (int i = 0; i < 10; i++)
        {
            string matPath = $"{matDir}/mat_upgrade_lv{(i + 1):D2}.mat";
            mats[i] = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mats[i] == null)
                Debug.LogWarning($"Material not found: {matPath}");
        }

        // 用SerializedObject赋值（确保序列化持久化）
        var so = new SerializedObject(vfx);
        var prop = so.FindProperty("upgradeMaterials");
        if (prop == null)
        {
            Debug.LogError("upgradeMaterials property not found on UnitVisualEffect");
            return;
        }

        prop.arraySize = 10;
        for (int i = 0; i < 10; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = mats[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(prefab);

        // 同时给GiftTier prefab也添加UnitVisualEffect组件（如果缺失）
        string[] giftPrefabs = {
            "Assets/Prefabs/Units/GiftTiers/Gift2Unit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift3Unit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift4Unit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift5Unit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift6Unit.prefab",
        };
        foreach (var gp in giftPrefabs)
        {
            var giftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gp);
            if (giftPrefab == null) continue;
            var giftVfx = giftPrefab.GetComponent<UnitVisualEffect>();
            if (giftVfx == null)
            {
                giftVfx = giftPrefab.AddComponent<UnitVisualEffect>();
                EditorUtility.SetDirty(giftPrefab);
                Debug.Log($"[AssignUpgradeMaterials] Added UnitVisualEffect to {gp}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AssignUpgradeMaterials] Assigned 10 upgrade materials to {prefabPath}");
    }
}
