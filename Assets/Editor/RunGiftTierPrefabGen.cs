#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using CapybaraDuel.Entity;
using CapybaraDuel.VFX;
using System.IO;
using System.Linq;

/// <summary>
/// 静默版 Gift Tier Prefab 生成器 v2
/// 关键改进：使用 Pushing FBX 作为 Prefab 基础（包含骨骼+SkinnedMesh+动画）
/// 而非静态 giftN.fbx（无骨骼，动画无法播放）
/// </summary>
public static class RunGiftTierPrefabGen
{
    public static void Execute()
    {
        string giftModelsRoot = "Assets/Models/Kpbl/5个礼物召唤的水豚单位";
        string outputDir = "Assets/Prefabs/Units/GiftTiers";
        string animDir = "Assets/Animations";

        EnsureFolder(outputDir);
        EnsureFolder(animDir);

        // 使用CuteCapybara shader（和KpblUnit一致的卡通风格，避免PBR油腻感）
        Shader capyShader = Shader.Find("CapybaraDuel/CuteCapybara");
        if (capyShader == null)
        {
            var sa = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CuteCapybara.shader");
            if (sa != null) capyShader = sa;
        }

        var configs = new (int tier, string dir)[]
        {
            (2, "卡皮巴拉礼物单位2"),
            (3, "卡皮巴拉礼物单位3"),
            (4, "卡皮巴拉礼物单位4"),
            (5, "卡皮巴拉礼物单位5"),
            (6, "卡皮巴拉礼物单位6"),
        };

        int created = 0;
        foreach (var cfg in configs)
        {
            string subDir = $"{giftModelsRoot}/{cfg.dir}";

            // ★ 优先使用 Pushing FBX（包含骨骼+SkinnedMesh+动画）
            string pushFbxPath = $"{subDir}/gift{cfg.tier}-Pushing.fbx";
            string staticFbxPath = $"{subDir}/gift{cfg.tier}.fbx";

            var pushFbx = AssetDatabase.LoadAssetAtPath<GameObject>(pushFbxPath);
            var staticFbx = AssetDatabase.LoadAssetAtPath<GameObject>(staticFbxPath);

            // 选择base FBX：有Pushing就用Pushing（有骨骼），否则用静态
            GameObject baseFbx = pushFbx != null ? pushFbx : staticFbx;
            bool usingPushFbx = pushFbx != null;
            string baseFbxPath = usingPushFbx ? pushFbxPath : staticFbxPath;

            if (baseFbx == null)
            {
                Debug.LogWarning($"[PrefabGen] Gift{cfg.tier}: No FBX found");
                continue;
            }
            Debug.Log($"[PrefabGen] Gift{cfg.tier}: Using {(usingPushFbx ? "Pushing" : "Static")} FBX as base");

            // Material — 从静态FBX目录加载材质
            string matPath = $"{subDir}/mat_gift{cfg.tier}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null && capyShader != null && mat.shader != capyShader)
            {
                Texture mainTex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap")
                    : (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
                Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                mat.shader = capyShader;
                if (mainTex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
                }
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                EditorUtility.SetDirty(mat);
                Debug.Log($"[PrefabGen] Gift{cfg.tier}: Switched material shader to CapybaraUnit");
            }

            // AnimatorController — 从 Pushing FBX 提取动画clip
            AnimatorController ac = null;
            if (usingPushFbx)
            {
                string acPath = $"{animDir}/AC_Gift{cfg.tier}.controller";
                // 删除旧的AC重建
                if (AssetDatabase.LoadAssetAtPath<AnimatorController>(acPath) != null)
                    AssetDatabase.DeleteAsset(acPath);

                var clips = AssetDatabase.LoadAllAssetsAtPath(pushFbxPath);
                AnimationClip pushClip = null;
                foreach (var a in clips)
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { pushClip = c; break; }

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
                    Debug.Log($"[PrefabGen] Created AC: {acPath} (clip='{pushClip.name}')");
                }
            }

            // ★ 从Pushing FBX实例化（包含骨骼+SkinnedMesh）
            var inst = Object.Instantiate(baseFbx);
            inst.name = $"Gift{cfg.tier}Unit";

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

            // ★ Animator设置 — 从Pushing FBX的ModelImporter获取Avatar
            var anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();

            if (usingPushFbx)
            {
                // 获取Pushing FBX的avatar（Generic动画需要avatar匹配骨骼）
                var pushImporter = AssetImporter.GetAtPath(pushFbxPath) as ModelImporter;
                if (pushImporter != null)
                {
                    // 确保FBX设置为Generic动画类型
                    if (pushImporter.animationType != ModelImporterAnimationType.Generic)
                    {
                        pushImporter.animationType = ModelImporterAnimationType.Generic;
                        pushImporter.SaveAndReimport();
                        Debug.Log($"[PrefabGen] Gift{cfg.tier}: Set Pushing FBX to Generic animation type");
                    }

                    // 从Pushing FBX加载avatar
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(pushFbxPath);
                    Avatar pushAvatar = null;
                    foreach (var asset in allAssets)
                    {
                        if (asset is Avatar av)
                        {
                            pushAvatar = av;
                            break;
                        }
                    }

                    if (pushAvatar != null)
                    {
                        anim.avatar = pushAvatar;
                        Debug.Log($"[PrefabGen] Gift{cfg.tier}: Avatar assigned = {pushAvatar.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"[PrefabGen] Gift{cfg.tier}: No avatar found in Pushing FBX!");
                    }
                }
            }

            if (ac != null)
                anim.runtimeAnimatorController = ac;

            // 确保必要组件
            if (inst.GetComponent<Capybara>() == null) inst.AddComponent<Capybara>();
            if (inst.GetComponent<CapybaraCampEffect>() == null) inst.AddComponent<CapybaraCampEffect>();
            if (inst.GetComponent<UnitVisualEffect>() == null) inst.AddComponent<UnitVisualEffect>();

            // 保存Prefab
            string prefabPath = $"{outputDir}/Gift{cfg.tier}Unit.prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
            Object.DestroyImmediate(inst);

            // ★ Object.Instantiate + SaveAsPrefabAsset 无法正确序列化AC引用
            //   必须用 SerializedObject 直接修改Prefab资产的 m_Controller
            if (ac != null)
            {
                var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var savedAnimator = savedPrefab.GetComponent<Animator>();
                if (savedAnimator != null)
                {
                    var soPrefab = new SerializedObject(savedAnimator);
                    soPrefab.FindProperty("m_Controller").objectReferenceValue = ac;
                    soPrefab.ApplyModifiedProperties();
                    EditorUtility.SetDirty(savedPrefab);
                }
            }

            string acName = (ac != null) ? ac.name : "NONE";
            Debug.Log($"[PrefabGen] Gift{cfg.tier}Unit OK (AC={acName}, base={System.IO.Path.GetFileName(baseFbxPath)})");
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Auto-assign to spawner
        var spawner = Object.FindObjectOfType<CapybaraDuel.Systems.CapybaraSpawner>();
        if (spawner != null)
        {
            var so = new SerializedObject(spawner);
            for (int t = 2; t <= 6; t++)
            {
                var prop = so.FindProperty($"tier{t}Prefab");
                if (prop == null) continue;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{outputDir}/Gift{t}Unit.prefab");
                if (pf != null) prop.objectReferenceValue = pf;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);
            Debug.Log("[PrefabGen] All tier prefabs assigned to CapybaraSpawner");
        }

        Debug.Log($"[PrefabGen] DONE: {created} Gift Tier Prefabs rebuilt (using Pushing FBX as base with skeleton+animation)");
    }

    static void EnsureFolder(string p)
    {
        if (!AssetDatabase.IsValidFolder(p))
        {
            string parent = Path.GetDirectoryName(p).Replace("\\", "/");
            string name = Path.GetFileName(p);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
