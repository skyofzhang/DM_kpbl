#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 全面诊断Gift Tier Prefab的Animator配置
/// </summary>
public static class DiagGiftPrefabRuntime
{
    public static void Execute()
    {
        string outputDir = "Assets/Prefabs/Units/GiftTiers";

        for (int t = 2; t <= 6; t++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{outputDir}/Gift{t}Unit.prefab");
            if (prefab == null) { Debug.LogWarning($"[DiagRT] Gift{t}: Prefab not found"); continue; }

            var animator = prefab.GetComponent<Animator>();
            if (animator == null) { Debug.LogError($"[DiagRT] Gift{t}: NO Animator component!"); continue; }

            // Animator基本信息
            Debug.Log($"[DiagRT] Gift{t} Animator: " +
                $"AC={(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NULL")}, " +
                $"Avatar={(animator.avatar != null ? animator.avatar.name + "(valid=" + animator.avatar.isValid + ")" : "NULL")}, " +
                $"applyRootMotion={animator.applyRootMotion}, " +
                $"cullingMode={animator.cullingMode}");

            // AC参数
            if (animator.runtimeAnimatorController != null)
            {
                var ac = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                if (ac != null)
                {
                    string paramList = "";
                    foreach (var p in ac.parameters)
                        paramList += $"{p.name}({p.type}={p.defaultFloat}/{p.defaultBool}) ";
                    Debug.Log($"[DiagRT] Gift{t} AC params: {paramList}");

                    // 状态
                    foreach (var layer in ac.layers)
                    {
                        var sm = layer.stateMachine;
                        Debug.Log($"[DiagRT] Gift{t} Layer '{layer.name}': {sm.states.Length} states, default='{sm.defaultState?.name}'");
                        foreach (var state in sm.states)
                        {
                            var s = state.state;
                            Debug.Log($"[DiagRT]   State '{s.name}': motion={(s.motion != null ? s.motion.name : "NULL")}, speed={s.speed}, " +
                                $"transitions={s.transitions.Length}");
                            if (s.motion != null && s.motion is AnimationClip clip)
                            {
                                Debug.Log($"[DiagRT]     Clip: '{clip.name}', length={clip.length}s, isLooping={clip.isLooping}, " +
                                    $"wrapMode={clip.wrapMode}, legacy={clip.legacy}");

                                // 检查clip的binding paths
                                var bindings = AnimationUtility.GetCurveBindings(clip);
                                Debug.Log($"[DiagRT]     Bindings: {bindings.Length} curves");
                            }
                        }
                    }
                }
            }

            // SkinnedMeshRenderer检查
            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
            Debug.Log($"[DiagRT] Gift{t}: SkinnedMesh={skinned.Length}, MeshFilter={meshFilters.Length}");
            foreach (var s in skinned)
                Debug.Log($"[DiagRT]   Skinned '{s.name}': bones={s.bones?.Length}, rootBone={(s.rootBone != null ? s.rootBone.name : "null")}");

            // 实例化测试
            var inst = Object.Instantiate(prefab);
            var instAnim = inst.GetComponent<Animator>();
            if (instAnim != null)
            {
                Debug.Log($"[DiagRT] Gift{t} INSTANCE: isActiveAndEnabled={instAnim.isActiveAndEnabled}, " +
                    $"hasRootMotion={instAnim.hasRootMotion}, isInitialized={instAnim.isInitialized}, " +
                    $"paramCount={instAnim.parameterCount}");

                // 尝试获取参数
                foreach (var p in instAnim.parameters)
                    Debug.Log($"[DiagRT]   Param: {p.name} type={p.type}");
            }
            Object.DestroyImmediate(inst);
        }

        // 对比：检查默认KpblUnit
        var kpblPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/KpblUnit.prefab");
        if (kpblPrefab != null)
        {
            var kAnim = kpblPrefab.GetComponent<Animator>();
            if (kAnim != null)
            {
                Debug.Log($"[DiagRT] KpblUnit Animator: " +
                    $"AC={(kAnim.runtimeAnimatorController != null ? kAnim.runtimeAnimatorController.name : "NULL")}, " +
                    $"Avatar={(kAnim.avatar != null ? kAnim.avatar.name + "(valid=" + kAnim.avatar.isValid + ")" : "NULL")}, " +
                    $"applyRootMotion={kAnim.applyRootMotion}");
            }
            var kSkinned = kpblPrefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            var kMeshF = kpblPrefab.GetComponentsInChildren<MeshFilter>();
            Debug.Log($"[DiagRT] KpblUnit: SkinnedMesh={kSkinned.Length}, MeshFilter={kMeshF.Length}");
        }

        Debug.Log("[DiagRT] === DONE ===");
    }
}
#endif
