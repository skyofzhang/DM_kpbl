#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using CapybaraDuel.Entity;

/// <summary>
/// 更新场景中Capybara预制体和实例的序列化值，使其与代码默认值一致
/// </summary>
public static class UpdateSceneValues
{
    public static void Execute()
    {
        // 更新KpblUnit Prefab
        string[] prefabPaths = {
            "Assets/Prefabs/Units/KpblUnit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift2Unit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift3Unit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift4Unit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift5Unit.prefab",
            "Assets/Prefabs/Units/GiftTiers/Gift6Unit.prefab"
        };

        foreach (var path in prefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[UpdateValues] Prefab not found: {path}");
                continue;
            }

            var capy = prefab.GetComponent<Capybara>();
            if (capy == null)
            {
                Debug.LogWarning($"[UpdateValues] No Capybara on: {path}");
                continue;
            }

            var so = new SerializedObject(capy);

            // 更新 separationRadius → 1.5
            var sepRadiusProp = so.FindProperty("separationRadius");
            if (sepRadiusProp != null)
            {
                float old = sepRadiusProp.floatValue;
                sepRadiusProp.floatValue = 1.5f;
                Debug.Log($"[UpdateValues] {path}: separationRadius {old} → 1.5");
            }

            // 更新 separationForce → 6.0
            var sepForceProp = so.FindProperty("separationForce");
            if (sepForceProp != null)
            {
                float old = sepForceProp.floatValue;
                sepForceProp.floatValue = 6.0f;
                Debug.Log($"[UpdateValues] {path}: separationForce {old} → 6.0");
            }

            // 更新 campBuffer → 1.5
            var campBufferProp = so.FindProperty("campBuffer");
            if (campBufferProp != null)
            {
                float old = campBufferProp.floatValue;
                campBufferProp.floatValue = 1.5f;
                Debug.Log($"[UpdateValues] {path}: campBuffer {old} → 1.5");
            }

            so.ApplyModifiedProperties();

            // 更新 UnitVisualEffect 的 giftModelTintStrength → 0.02
            var uve = prefab.GetComponent<CapybaraDuel.VFX.UnitVisualEffect>();
            if (uve != null)
            {
                var uveSo = new SerializedObject(uve);
                var tintProp = uveSo.FindProperty("giftModelTintStrength");
                if (tintProp != null)
                {
                    float old = tintProp.floatValue;
                    tintProp.floatValue = 0.02f;
                    Debug.Log($"[UpdateValues] {path}: giftModelTintStrength {old} → 0.02");
                }
                uveSo.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(prefab);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[UpdateValues] === DONE ===");
    }
}
#endif
