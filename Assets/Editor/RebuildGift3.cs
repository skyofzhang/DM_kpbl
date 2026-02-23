#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using CapybaraDuel.Entity;
using CapybaraDuel.VFX;

/// <summary>
/// 单独重建 Gift3Unit Prefab
/// 场景：用户用3DMAX重新导出gift3-Pushing.fbx（修改蒙皮），覆盖到工程
/// 需要：重新设置FBX导入参数 → 重建Avatar → 重建AC → 重建Prefab → 赋值到Spawner
/// </summary>
public static class RebuildGift3
{
    public static void Execute()
    {
        string subDir = "Assets/Models/Kpbl/5个礼物召唤的水豚单位/卡皮巴拉礼物单位3";
        string pushFbxPath = $"{subDir}/gift3-Pushing.fbx";
        string matPath = $"{subDir}/mat_gift3.mat";
        string acPath = "Assets/Animations/AC_Gift3.controller";
        string prefabPath = "Assets/Prefabs/Units/GiftTiers/Gift3Unit.prefab";

        // ==================== Step 1: FBX 导入设置 ====================
        var importer = AssetImporter.GetAtPath(pushFbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[RebuildGift3] FBX not found: {pushFbxPath}");
            return;
        }

        // 动画类型 → Generic + 从此模型创建Avatar
        bool needReimport = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            needReimport = true;
        }

        // 确保avatarSetup=CreateFromThisModel（用SerializedObject修改内部属性）
        var soImporter = new SerializedObject(importer);
        var avatarSetupProp = soImporter.FindProperty("m_AvatarSetup");
        if (avatarSetupProp != null && avatarSetupProp.intValue != 1)
        {
            avatarSetupProp.intValue = 1; // 1 = CreateFromThisModel
            soImporter.ApplyModifiedProperties();
            needReimport = true;
            Debug.Log("[RebuildGift3] Set avatarSetup = CreateFromThisModel");
        }

        // 确保导入动画
        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            needReimport = true;
        }

        if (needReimport)
        {
            importer.SaveAndReimport();
            Debug.Log("[RebuildGift3] FBX reimported with Generic + Avatar + Animation");
        }

        // ==================== Step 2: 设置动画clip循环 ====================
        // 重新获取importer（reimport后需刷新）
        importer = AssetImporter.GetAtPath(pushFbxPath) as ModelImporter;
        var clipAnims = importer.clipAnimations;
        if (clipAnims == null || clipAnims.Length == 0)
            clipAnims = importer.defaultClipAnimations;

        if (clipAnims != null && clipAnims.Length > 0)
        {
            bool clipChanged = false;
            foreach (var clip in clipAnims)
            {
                if (!clip.loopTime)
                {
                    clip.loopTime = true;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                    clipChanged = true;
                }
            }
            if (clipChanged)
            {
                importer.clipAnimations = clipAnims;
                importer.SaveAndReimport();
                Debug.Log("[RebuildGift3] Animation clip set to loop + Root Motion locked");
            }
        }

        // ==================== Step 3: 加载资源 ====================
        var pushFbx = AssetDatabase.LoadAssetAtPath<GameObject>(pushFbxPath);
        if (pushFbx == null)
        {
            Debug.LogError("[RebuildGift3] Failed to load Pushing FBX after reimport");
            return;
        }

        // Avatar
        Avatar pushAvatar = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(pushFbxPath))
        {
            if (asset is Avatar av) { pushAvatar = av; break; }
        }
        Debug.Log($"[RebuildGift3] Avatar: {(pushAvatar != null ? pushAvatar.name : "NULL")}");

        // Animation clip
        AnimationClip pushClip = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(pushFbxPath))
        {
            if (asset is AnimationClip c && !c.name.StartsWith("__preview__")) { pushClip = c; break; }
        }
        Debug.Log($"[RebuildGift3] Clip: {(pushClip != null ? pushClip.name : "NULL")}");

        // Material
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        // ==================== Step 4: 重建 AnimatorController ====================
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(acPath) != null)
            AssetDatabase.DeleteAsset(acPath);

        AnimatorController ac = null;
        if (pushClip != null)
        {
            ac = AnimatorController.CreateAnimatorControllerAtPath(acPath);
            var sm = ac.layers[0].stateMachine;
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var idle = sm.AddState("Idle");
            idle.motion = pushClip;
            idle.speed = 0f;
            sm.defaultState = idle;
            var push = sm.AddState("Pushing");
            push.motion = pushClip;
            push.speed = 1f;
            var t1 = idle.AddTransition(push);
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t1.hasExitTime = false; t1.duration = 0.15f;
            var t2 = push.AddTransition(idle);
            t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            t2.hasExitTime = false; t2.duration = 0.15f;
            EditorUtility.SetDirty(ac);
            Debug.Log($"[RebuildGift3] AC rebuilt: {acPath}");
        }

        // ==================== Step 5: 重建 Prefab ====================
        var inst = Object.Instantiate(pushFbx);
        inst.name = "Gift3Unit";

        // 赋材质
        if (mat != null)
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                r.sharedMaterials = ms;
            }
        }

        // Animator
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        if (pushAvatar != null) anim.avatar = pushAvatar;
        if (ac != null) anim.runtimeAnimatorController = ac;

        // 确保组件
        if (inst.GetComponent<Capybara>() == null) inst.AddComponent<Capybara>();
        if (inst.GetComponent<CapybaraCampEffect>() == null) inst.AddComponent<CapybaraCampEffect>();
        if (inst.GetComponent<UnitVisualEffect>() == null) inst.AddComponent<UnitVisualEffect>();

        // 保存 Prefab
        PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
        Object.DestroyImmediate(inst);

        // ★ SerializedObject 后补 AC引用（SaveAsPrefabAsset无法正确序列化AC）
        if (ac != null)
        {
            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var savedAnim = savedPrefab.GetComponent<Animator>();
            if (savedAnim != null)
            {
                var so = new SerializedObject(savedAnim);
                so.FindProperty("m_Controller").objectReferenceValue = ac;
                // 同时补Avatar（可能也丢失）
                if (pushAvatar != null)
                    so.FindProperty("m_Avatar").objectReferenceValue = pushAvatar;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(savedPrefab);
            }
        }

        // ==================== Step 6: 同步序列化值 ====================
        {
            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var capy = savedPrefab.GetComponent<Capybara>();
            if (capy != null)
            {
                var so = new SerializedObject(capy);
                var p1 = so.FindProperty("separationRadius");
                if (p1 != null) p1.floatValue = 0.8f;
                var p2 = so.FindProperty("separationForce");
                if (p2 != null) p2.floatValue = 2.0f;
                var p3 = so.FindProperty("campBuffer");
                if (p3 != null) p3.floatValue = 1.5f;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(savedPrefab);
            }
        }

        // ==================== Step 7: 赋值到Spawner ====================
        var spawner = Object.FindObjectOfType<CapybaraDuel.Systems.CapybaraSpawner>();
        if (spawner != null)
        {
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty("tier3Prefab");
            if (prop != null)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                prop.objectReferenceValue = pf;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(spawner);
                Debug.Log("[RebuildGift3] Assigned to CapybaraSpawner.tier3Prefab");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RebuildGift3] === DONE === Gift3Unit Prefab rebuilt successfully");
    }
}
#endif
