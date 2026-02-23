#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using CapybaraDuel.Entity;
using CapybaraDuel.Systems;
using CapybaraDuel.Config;
using CapybaraDuel.VFX;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace CapybaraDuel.Editor
{
    /// <summary>
    /// Prefab 生成器 - 多角色批量生成
    /// 组装: Model + Material + AnimatorController → Prefab
    /// </summary>
    public static class PrefabGenerator
    {
        [MenuItem("CapybaraDuel/3. Build All Prefabs", false, 12)]
        public static void GenerateAllPrefabs()
        {
            string modelsRoot = "Assets/Models";
            string matDir = "Assets/Materials";
            string animDir = "Assets/Animations";
            string prefabDir = "Assets/Prefabs/Units";
            EnsureFolder(prefabDir);

            int created = 0;
            List<PrefabRecord> records = new List<PrefabRecord>();

            // 遍历所有模型目录
            string[] subDirs = AssetDatabase.GetSubFolders(modelsRoot);
            foreach (string subDir in subDirs)
            {
                string folderName = Path.GetFileName(subDir);

                // 查找 FBX
                string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { subDir });
                string fbxPath = null;
                foreach (string guid in fbxGuids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        fbxPath = p;
                        break;
                    }
                }

                if (fbxPath == null) continue; // 跳过没有 FBX 的目录（如 Orange, Scene）

                // 查找对应的 Material 和 AnimatorController
                string matPath = $"{matDir}/Mat_{folderName}.mat";
                string acPath = $"{animDir}/AC_{folderName}.controller";

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(acPath);

                // 实例化 FBX
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                instance.name = folderName + "Unit";

                // 赋材质
                if (mat != null)
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers)
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            mats[i] = mat;
                        r.sharedMaterials = mats;
                    }
                }

                // 赋 Animator
                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                    animator = instance.AddComponent<Animator>();
                if (ac != null)
                    animator.runtimeAnimatorController = ac;

                // 添加 Capybara 组件
                if (instance.GetComponent<Capybara>() == null)
                    instance.AddComponent<Capybara>();

                // 添加 CapybaraCampEffect（阵营菲涅尔颜色设置器）
                if (instance.GetComponent<CapybaraCampEffect>() == null)
                    instance.AddComponent<CapybaraCampEffect>();

                // 添加 UnitVisualEffect（母材质系统：缩放+发光+颜色按tier分级）
                if (instance.GetComponent<UnitVisualEffect>() == null)
                    instance.AddComponent<UnitVisualEffect>();

                // 添加 Collider（已禁用：与橘子碰撞导致单位无法穿过橘子）
                // if (instance.GetComponent<Collider>() == null)
                // {
                //     var capsule = instance.AddComponent<CapsuleCollider>();
                //     capsule.center = new Vector3(0, 0.5f, 0);
                //     capsule.radius = 0.3f;
                //     capsule.height = 1f;
                // }

                // 保存 Prefab
                string prefabPath = $"{prefabDir}/{instance.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);

                records.Add(new PrefabRecord
                {
                    name = folderName,
                    prefabPath = prefabPath,
                    hasMaterial = mat != null,
                    hasAnimator = ac != null
                });
                created++;
            }

            // 生成橘子 Prefab（OBJ 模型）
            GenerateOrangePrefab(matDir, ref created, records);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 尝试赋值给场景中的 Spawner
            AssignToSpawner(records);

            // 输出报告
            string report = $"生成了 {created} 个 Prefab:\n\n";
            foreach (var r in records)
            {
                report += $"  {r.name}: Mat={BoolIcon(r.hasMaterial)} AC={BoolIcon(r.hasAnimator)}\n";
            }
            report += $"\n输出目录: {prefabDir}";

            Debug.Log($"[PrefabGen] {report}");
            EditorUtility.DisplayDialog("Prefabs Built", report, "OK");
        }

        static void GenerateOrangePrefab(string matDir, ref int created, List<PrefabRecord> records)
        {
            string orangeObjPath = "Assets/Models/Orange/527_Chengzi.obj";
            var orangeMesh = AssetDatabase.LoadAssetAtPath<GameObject>(orangeObjPath);
            if (orangeMesh == null)
            {
                // 用球体占位
                orangeMesh = null;
            }

            string matPath = $"{matDir}/Mat_Orange.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            GameObject orangeInstance;
            if (orangeMesh != null)
            {
                orangeInstance = (GameObject)PrefabUtility.InstantiatePrefab(orangeMesh);
                orangeInstance.name = "Orange";
            }
            else
            {
                orangeInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orangeInstance.name = "Orange";
                orangeInstance.transform.localScale = Vector3.one * 1.5f;
            }

            // 赋材质
            if (mat != null)
            {
                var renderers = orangeInstance.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }

            // 添加 OrangeController
            if (orangeInstance.GetComponent<OrangeController>() == null)
                orangeInstance.AddComponent<OrangeController>();

            // 添加 MagicOrangeEffect（运行时动态调整Shader参数）
            if (orangeInstance.GetComponent<MagicOrangeEffect>() == null)
                orangeInstance.AddComponent<MagicOrangeEffect>();

            // 添加 OrangeDustTrail（移动方向烟尘）
            if (orangeInstance.GetComponent<OrangeDustTrail>() == null)
                orangeInstance.AddComponent<OrangeDustTrail>();

            // 添加 OrangeSpeedHUD（头顶速度+方向显示）
            if (orangeInstance.GetComponent<CapybaraDuel.VFX.OrangeSpeedHUD>() == null)
                orangeInstance.AddComponent<CapybaraDuel.VFX.OrangeSpeedHUD>();

            // 添加 Collider（已禁用：与卡皮巴拉碰撞导致单位无法穿过橘子）
            // if (orangeInstance.GetComponent<Collider>() == null)
            // {
            //     var sc = orangeInstance.AddComponent<SphereCollider>();
            //     sc.radius = 0.75f;
            // }

            string prefabPath = "Assets/Prefabs/Scene/Orange.prefab";
            EnsureFolder("Assets/Prefabs/Scene");
            PrefabUtility.SaveAsPrefabAsset(orangeInstance, prefabPath);
            Object.DestroyImmediate(orangeInstance);

            records.Add(new PrefabRecord { name = "Orange", prefabPath = prefabPath, hasMaterial = mat != null, hasAnimator = false });
            created++;
        }

        static void AssignToSpawner(List<PrefabRecord> records)
        {
            var spawner = Object.FindObjectOfType<CapybaraSpawner>();
            if (spawner == null) return;

            // 优先用 Kpbl（美术输出的卡皮巴拉），其次 201_Sheep
            var kpblRecord = records.FirstOrDefault(r => r.name != null && r.name.Contains("Kpbl"));
            var targetRecord = kpblRecord.name != null ? kpblRecord
                : records.FirstOrDefault(r => r.name != null && r.name.Contains("201_Sheep"));
            if (targetRecord.name != null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetRecord.prefabPath);
                if (prefab != null)
                {
                    spawner.capybaraPrefab = prefab;
                    EditorUtility.SetDirty(spawner);
                    Debug.Log($"[PrefabGen] Assigned {prefab.name} to CapybaraSpawner");
                }
            }
        }

        // ==================== Gift Tier Prefab 生成 ====================

        /// <summary>
        /// 为5种礼物召唤单位(tier2~6)生成独立Prefab
        /// 每个Prefab: FBX模型 + 材质(切到CapybaraUnit shader) + AnimatorController + 组件
        /// </summary>
        [MenuItem("CapybaraDuel/3b. Build Gift Tier Prefabs", false, 12)]
        public static void GenerateGiftTierPrefabs()
        {
            string giftModelsRoot = "Assets/Models/Kpbl/5个礼物召唤的水豚单位";
            string outputDir = "Assets/Prefabs/Units/GiftTiers";
            string animDir = "Assets/Animations";
            string shaderName = "CapybaraDuel/CapybaraUnit"; // CapybaraUnit.shader 的 shader名

            EnsureFolder(outputDir);
            EnsureFolder(animDir);

            // Shader引用
            Shader capyShader = Shader.Find(shaderName);
            if (capyShader == null)
            {
                // 尝试从文件加载
                var shaderAsset = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CapybaraUnit.shader");
                if (shaderAsset != null)
                    capyShader = shaderAsset;
            }

            // 默认 AC（gift5/6没有Pushing动画时的占位）
            AnimatorController defaultAC = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/AC_Kpbl.controller");

            // Gift tier 配置: tier号 → 子目录名
            var giftConfigs = new (int tier, string dirName, bool hasPushingAnim)[]
            {
                (2, "卡皮巴拉礼物单位2", true),
                (3, "卡皮巴拉礼物单位3", true),
                (4, "卡皮巴拉礼物单位4", true),  // gift4 now has Pushing
                (5, "卡皮巴拉礼物单位5", true),
                (6, "卡皮巴拉礼物单位6", true),
            };

            int created = 0;
            string report = "";

            foreach (var cfg in giftConfigs)
            {
                string subDir = $"{giftModelsRoot}/{cfg.dirName}";

                // 1. 加载主FBX (gift{N}.fbx)
                string fbxPath = $"{subDir}/gift{cfg.tier}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null)
                {
                    Debug.LogWarning($"[PrefabGen] Gift{cfg.tier}: FBX not found at {fbxPath}");
                    report += $"  Gift{cfg.tier}: SKIP (no FBX)\n";
                    continue;
                }

                // 2. 加载材质
                string matPath = $"{subDir}/mat_gift{cfg.tier}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                // 切换材质 shader 到 CapybaraUnit（启用阵营边缘光）
                if (mat != null && capyShader != null && mat.shader != capyShader)
                {
                    // 保存原始贴图引用
                    Texture mainTex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap")
                                    : (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
                    Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;

                    mat.shader = capyShader;

                    // 恢复贴图
                    if (mainTex != null)
                    {
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
                    }
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);

                    EditorUtility.SetDirty(mat);
                    Debug.Log($"[PrefabGen] Gift{cfg.tier}: Switched mat shader to CapybaraUnit");
                }

                // 3. 创建/加载 AnimatorController
                AnimatorController ac = null;
                string pushFbxPath = $"{subDir}/gift{cfg.tier}-Pushing.fbx";
                bool pushFbxExists = AssetDatabase.LoadAssetAtPath<GameObject>(pushFbxPath) != null;

                if (cfg.hasPushingAnim && pushFbxExists)
                {
                    // 从 Pushing FBX 提取动画 clip 并创建 AC
                    string acPath = $"{animDir}/AC_Gift{cfg.tier}.controller";
                    ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(acPath);
                    if (ac == null)
                    {
                        ac = CreatePushingAnimController(pushFbxPath, acPath, $"Gift{cfg.tier}");
                    }
                }

                // 如果没有专属AC，使用默认卡皮巴拉AC占位
                if (ac == null)
                    ac = defaultAC;

                // 4. 实例化并组装 Prefab
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                instance.name = $"Gift{cfg.tier}Unit";

                // 赋材质
                if (mat != null)
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers)
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            mats[i] = mat;
                        r.sharedMaterials = mats;
                    }
                }

                // 赋 Animator
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                if (ac != null) animator.runtimeAnimatorController = ac;

                // 添加组件
                if (instance.GetComponent<Capybara>() == null)
                    instance.AddComponent<Capybara>();
                if (instance.GetComponent<CapybaraCampEffect>() == null)
                    instance.AddComponent<CapybaraCampEffect>();
                if (instance.GetComponent<UnitVisualEffect>() == null)
                    instance.AddComponent<UnitVisualEffect>();

                // 5. 保存 Prefab
                string prefabPath = $"{outputDir}/Gift{cfg.tier}Unit.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);

                string acName = (ac == defaultAC) ? "AC_Kpbl(fallback)" : ac.name;
                report += $"  Gift{cfg.tier}Unit: Mat={BoolIcon(mat != null)} AC={acName} Shader={BoolIcon(capyShader != null)}\n";
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 尝试自动赋值到场景 CapybaraSpawner
            AssignGiftTierPrefabsToSpawner(outputDir);

            string fullReport = $"生成了 {created} 个 Gift Tier Prefab:\n\n{report}\n输出目录: {outputDir}";
            Debug.Log($"[PrefabGen] {fullReport}");
            EditorUtility.DisplayDialog("Gift Tier Prefabs Built", fullReport, "OK");
        }

        /// <summary>
        /// 从 Pushing FBX 创建简单的 AnimatorController（Idle → Pushing）
        /// </summary>
        static AnimatorController CreatePushingAnimController(string pushFbxPath, string outputPath, string prefix)
        {
            // 从 FBX 中提取动画 clip
            var clips = AssetDatabase.LoadAllAssetsAtPath(pushFbxPath);
            AnimationClip pushClip = null;
            foreach (var asset in clips)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    pushClip = clip;
                    break;
                }
            }

            if (pushClip == null)
            {
                Debug.LogWarning($"[PrefabGen] No animation clip found in {pushFbxPath}");
                return null;
            }

            // 创建 AnimatorController
            var ac = AnimatorController.CreateAnimatorControllerAtPath(outputPath);
            var rootStateMachine = ac.layers[0].stateMachine;

            // 添加 Speed 参数（兼容 Capybara.cs 的 Hash_Speed）
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);

            // 创建 Idle 状态（用 Pushing clip 的第一帧冻结作为静止姿势）
            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = pushClip;
            idleState.speed = 0f; // 冻结在第一帧
            rootStateMachine.defaultState = idleState;

            // 创建 Pushing 状态
            var pushState = rootStateMachine.AddState("Pushing");
            pushState.motion = pushClip;
            pushState.speed = 1f;

            // Idle → Pushing: Speed > 0.1
            var toRun = idleState.AddTransition(pushState);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            toRun.hasExitTime = false;
            toRun.duration = 0.15f;

            // Pushing → Idle: Speed < 0.1
            var toIdle = pushState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;

            EditorUtility.SetDirty(ac);
            Debug.Log($"[PrefabGen] Created AnimatorController: {outputPath}");
            return ac;
        }

        /// <summary>
        /// 自动将 Gift Tier Prefab 赋值给场景中的 CapybaraSpawner
        /// </summary>
        static void AssignGiftTierPrefabsToSpawner(string outputDir)
        {
            var spawner = Object.FindObjectOfType<CapybaraSpawner>();
            if (spawner == null) return;

            var so = new SerializedObject(spawner);
            for (int tier = 2; tier <= 6; tier++)
            {
                string fieldName = $"tier{tier}Prefab";
                var prop = so.FindProperty(fieldName);
                if (prop == null) continue; // field doesn't exist yet (will be added in Step 6)

                string prefabPath = $"{outputDir}/Gift{tier}Unit.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    prop.objectReferenceValue = prefab;
                    Debug.Log($"[PrefabGen] Assigned Gift{tier}Unit to spawner.{fieldName}");
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);
        }

        // ==================== VFX Prefab 生成 ====================

        [MenuItem("CapybaraDuel/4. Build VFX Prefabs", false, 13)]
        public static void GenerateVFXPrefabs()
        {
            string vfxDir = "Assets/Prefabs/VFX";
            EnsureFolder(vfxDir);
            int count = 0;

            // 生成各种 VFX Prefab
            CreateVFXPrefab(vfxDir, "SpawnVFX", new Color(0.3f, 1f, 0.5f), 0.8f, 20, ref count);
            CreateVFXPrefab(vfxDir, "DespawnVFX", new Color(0.8f, 0.3f, 0.3f), 1.0f, 15, ref count);
            CreateVFXPrefab(vfxDir, "GiftSmallVFX", new Color(1f, 1f, 0.3f), 0.6f, 12, ref count);
            CreateVFXPrefab(vfxDir, "GiftBigVFX", new Color(1f, 0.6f, 0f), 1.2f, 30, ref count);
            CreateVFXPrefab(vfxDir, "GiftLegendVFX", new Color(1f, 0.84f, 0f), 2.0f, 50, ref count);
            CreateVFXPrefab(vfxDir, "VictoryVFX", new Color(1f, 0.95f, 0.2f), 3.0f, 80, ref count);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 自动赋值给 VFXSpawner
            AssignVFXToSpawner(vfxDir);

            Debug.Log($"[PrefabGen] Generated {count} VFX Prefabs in {vfxDir}");
            EditorUtility.DisplayDialog("VFX Prefabs Built",
                $"生成了 {count} 个 VFX Prefab:\n" +
                "• SpawnVFX (绿色粒子)\n" +
                "• DespawnVFX (红色粒子)\n" +
                "• GiftSmallVFX (黄色粒子)\n" +
                "• GiftBigVFX (橙色粒子)\n" +
                "• GiftLegendVFX (金色粒子)\n" +
                "• VictoryVFX (金色爆发)\n\n" +
                "已自动赋值给场景中的 VFXSpawner", "OK");
        }

        static void CreateVFXPrefab(string dir, string vfxName, Color color,
            float size, int particleCount, ref int count)
        {
            var go = new GameObject(vfxName);
            var ps = go.AddComponent<ParticleSystem>();

            // 主模块
            var main = ps.main;
            main.duration = size * 0.8f;
            main.startLifetime = size;
            main.startSpeed = size * 2f;
            main.startSize = size * 0.3f;
            main.startColor = color;
            main.maxParticles = particleCount * 2;
            main.loop = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 发射模块
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, (short)particleCount)
            });

            // 形状模块
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = size * 0.3f;

            // 颜色渐变（淡出）
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 0.5f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // 大小渐变（缩小）
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

            // Renderer 设置（使用默认粒子材质）
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");

            string path = $"{dir}/{vfxName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            count++;
        }

        static void AssignVFXToSpawner(string vfxDir)
        {
            var spawner = Object.FindObjectOfType<VFXSpawner>();
            if (spawner == null) return;

            spawner.spawnVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{vfxDir}/SpawnVFX.prefab");
            spawner.despawnVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{vfxDir}/DespawnVFX.prefab");
            spawner.giftSmallVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{vfxDir}/GiftSmallVFX.prefab");
            spawner.giftBigVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{vfxDir}/GiftBigVFX.prefab");
            spawner.giftLegendVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{vfxDir}/GiftLegendVFX.prefab");
            spawner.victoryVFX = AssetDatabase.LoadAssetAtPath<GameObject>($"{vfxDir}/VictoryVFX.prefab");
            EditorUtility.SetDirty(spawner);

            Debug.Log("[PrefabGen] VFX Prefabs assigned to VFXSpawner");
        }

        static string BoolIcon(bool val) => val ? "✅" : "❌";

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folderName = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        struct PrefabRecord
        {
            public string name;
            public string prefabPath;
            public bool hasMaterial;
            public bool hasAnimator;
        }
    }
}
#endif
