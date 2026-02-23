#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// 使用SerializedObject直接修改Prefab的Animator.Controller引用
/// </summary>
public static class FixGiftPrefabAC2
{
    public static void Execute()
    {
        string outputDir = "Assets/Prefabs/Units/GiftTiers";
        string animDir = "Assets/Animations";

        // 首先确认所有AC都存在
        for (int t = 2; t <= 6; t++)
        {
            string acPath = $"{animDir}/AC_Gift{t}.controller";
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(acPath);
            Debug.Log($"[FixAC2] AC_Gift{t}: exists={ac != null}, path={acPath}");
            if (ac != null)
            {
                Debug.Log($"[FixAC2]   params={ac.parameters.Length}, layers={ac.layers.Length}");
                foreach (var p in ac.parameters)
                    Debug.Log($"[FixAC2]   param: {p.name} ({p.type})");
            }
        }

        for (int t = 2; t <= 6; t++)
        {
            string prefabPath = $"{outputDir}/Gift{t}Unit.prefab";
            string acPath = $"{animDir}/AC_Gift{t}.controller";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(acPath);

            if (prefab == null || ac == null)
            {
                Debug.LogError($"[FixAC2] Gift{t}: prefab={prefab != null}, ac={ac != null}");
                continue;
            }

            // 方法: 使用SerializedObject编辑Prefab的Animator
            var animator = prefab.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError($"[FixAC2] Gift{t}: No Animator on prefab!");
                continue;
            }

            var so = new SerializedObject(animator);
            var controllerProp = so.FindProperty("m_Controller");
            Debug.Log($"[FixAC2] Gift{t} BEFORE: m_Controller={controllerProp.objectReferenceValue}");

            controllerProp.objectReferenceValue = ac;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefab);

            Debug.Log($"[FixAC2] Gift{t} AFTER: m_Controller={controllerProp.objectReferenceValue}, " +
                $"runtimeAC={animator.runtimeAnimatorController?.name ?? "NULL"}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 最终验证
        for (int t = 2; t <= 6; t++)
        {
            string prefabPath = $"{outputDir}/Gift{t}Unit.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var anim = prefab?.GetComponent<Animator>();
            Debug.Log($"[FixAC2] FINAL Gift{t}: AC={(anim?.runtimeAnimatorController?.name ?? "NULL")}, " +
                $"Avatar={(anim?.avatar?.name ?? "NULL")}");
        }

        Debug.Log("[FixAC2] === DONE ===");
    }
}
#endif
