using UnityEngine;
using UnityEditor;

/// <summary>
/// VFX预制体重建工具 —— 用URP Particles/Unlit + 程序化贴图
/// 修复所有VFX预制体的"色块"问题（缺少透明贴图+正确材质）
///
/// 用法: 菜单 Tools → Rebuild VFX Prefabs
/// </summary>
public static class VFXPrefabBuilder
{
    private static Material _cachedParticleMat;

    [MenuItem("Tools/Rebuild VFX Prefabs")]
    public static void Execute()
    {
        // 加载或创建基础URP粒子材质
        EnsureParticleMaterials();

        BuildSpawnVFX();
        BuildDespawnVFX();
        BuildGiftSmallVFX();
        BuildGiftBigVFX();
        BuildGiftLegendVFX();
        BuildVictoryVFX();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VFXPrefabBuilder] All 6 VFX prefabs rebuilt successfully!");
    }

    // ==================== 材质与贴图 ====================

    static void EnsureParticleMaterials()
    {
        // 柔光粒子材质 (Additive)
        EnsureMaterial("Assets/Materials/Mat_ParticleAdd.mat",
            "Universal Render Pipeline/Particles/Unlit",
            mat =>
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 2);   // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_BLENDMODE_ADD");
                mat.renderQueue = 3000;
                mat.SetTexture("_BaseMap", GetOrCreateSoftCircleTex());
                mat.SetColor("_BaseColor", Color.white);
            });

        // Alpha混合粒子材质
        EnsureMaterial("Assets/Materials/Mat_ParticleAlpha.mat",
            "Universal Render Pipeline/Particles/Unlit",
            mat =>
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);   // Alpha
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
                mat.SetTexture("_BaseMap", GetOrCreateSoftCircleTex());
                mat.SetColor("_BaseColor", Color.white);
            });

        // 星形粒子材质 (Additive)
        EnsureMaterial("Assets/Materials/Mat_ParticleStar.mat",
            "Universal Render Pipeline/Particles/Unlit",
            mat =>
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 2);   // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_BLENDMODE_ADD");
                mat.renderQueue = 3000;
                mat.SetTexture("_BaseMap", GetOrCreateStarTex());
                mat.SetColor("_BaseColor", Color.white);
            });
    }

    static void EnsureMaterial(string path, string shaderName, System.Action<Material> setup)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            setup(existing);
            EditorUtility.SetDirty(existing);
            return;
        }

        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"[VFXPrefabBuilder] Shader not found: {shaderName}");
            return;
        }
        var mat = new Material(shader);
        setup(mat);
        AssetDatabase.CreateAsset(mat, path);
    }

    // -------- 程序化贴图 --------

    static Texture2D GetOrCreateSoftCircleTex()
    {
        string path = "Assets/Art/Textures/Tex_SoftCircle.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        EnsureDirectory("Assets/Art/Textures");

        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f;
                float dy = y - center + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / center;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha * alpha; // 三次方柔化
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        SaveTexturePNG(tex, path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D GetOrCreateStarTex()
    {
        string path = "Assets/Art/Textures/Tex_Star4.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        EnsureDirectory("Assets/Art/Textures");

        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center + 0.5f) / center;
                float dy = (y - center + 0.5f) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 4角星形：沿X/Y轴凸起
                float angle = Mathf.Atan2(dy, dx);
                float starShape = Mathf.Abs(Mathf.Cos(angle * 2f));
                starShape = Mathf.Pow(starShape, 0.5f);

                float radius = Mathf.Lerp(0.3f, 1f, starShape);
                float alpha = Mathf.Clamp01(1f - dist / radius);
                alpha = alpha * alpha;

                // 加一个核心高亮
                float core = Mathf.Clamp01(1f - dist * 3f);
                core = core * core;
                alpha = Mathf.Max(alpha, core);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        SaveTexturePNG(tex, path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void SaveTexturePNG(Texture2D tex, string path)
    {
        var bytes = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        // 设置为Sprite，启用Alpha
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }

    // ==================== VFX预制体构建 ====================

    static void BuildSpawnVFX()
    {
        string path = "Assets/Prefabs/VFX/SpawnVFX.prefab";
        var go = new GameObject("SpawnVFX");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startColor = new Color(1f, 0.9f, 0.4f, 0.9f); // 暖黄色
        main.maxParticles = 20;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.5f; // 上飘

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12, 18) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        // 颜色渐变
        SetColorFade(ps, new Color(1f, 0.9f, 0.4f, 1f), new Color(1f, 0.7f, 0.1f, 0f));

        // 大小渐变
        SetSizeCurve(ps, new Keyframe(0f, 0.8f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.1f));

        // 渲染器
        SetRenderer(go, "Assets/Materials/Mat_ParticleAdd.mat");

        SavePrefab(go, path);
        Debug.Log("[VFXPrefabBuilder] SpawnVFX rebuilt: warm yellow burst");
    }

    static void BuildDespawnVFX()
    {
        string path = "Assets/Prefabs/VFX/DespawnVFX.prefab";
        var go = new GameObject("DespawnVFX");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startColor = new Color(0.6f, 0.55f, 0.5f, 0.6f); // 灰色烟尘
        main.maxParticles = 15;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 14) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        SetColorFade(ps, new Color(0.6f, 0.55f, 0.5f, 0.7f), new Color(0.4f, 0.38f, 0.35f, 0f));
        SetSizeCurve(ps, new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0.2f));
        SetRenderer(go, "Assets/Materials/Mat_ParticleAlpha.mat");

        SavePrefab(go, path);
        Debug.Log("[VFXPrefabBuilder] DespawnVFX rebuilt: grey smoke puff");
    }

    static void BuildGiftSmallVFX()
    {
        string path = "Assets/Prefabs/VFX/GiftSmallVFX.prefab";
        var go = new GameObject("GiftSmallVFX");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new Color(0.4f, 0.7f, 1f, 1f); // 蓝色
        main.maxParticles = 15;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f; // 略微下坠

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 14) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.2f;

        SetColorFade(ps, new Color(0.4f, 0.8f, 1f, 1f), new Color(0.2f, 0.5f, 1f, 0f));
        SetSizeCurve(ps, new Keyframe(0f, 1f), new Keyframe(0.5f, 0.7f), new Keyframe(1f, 0f));
        SetRenderer(go, "Assets/Materials/Mat_ParticleStar.mat");

        SavePrefab(go, path);
        Debug.Log("[VFXPrefabBuilder] GiftSmallVFX rebuilt: blue star burst");
    }

    static void BuildGiftBigVFX()
    {
        string path = "Assets/Prefabs/VFX/GiftBigVFX.prefab";
        var go = new GameObject("GiftBigVFX");

        // 主粒子：金色星星向外扩散
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startColor = new Color(1f, 0.84f, 0f, 1f); // 金色
        main.maxParticles = 25;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 22) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        SetColorFade(ps, new Color(1f, 0.9f, 0.3f, 1f), new Color(1f, 0.6f, 0f, 0f));
        SetSizeCurve(ps, new Keyframe(0f, 0.8f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.1f));
        SetRenderer(go, "Assets/Materials/Mat_ParticleStar.mat");

        // 子粒子：光环扩散
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(go.transform, false);
        var ringPS = ringGo.AddComponent<ParticleSystem>();
        var ringMain = ringPS.main;
        ringMain.duration = 0.5f;
        ringMain.startLifetime = 0.4f;
        ringMain.startSpeed = 0f;
        ringMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        ringMain.startColor = new Color(1f, 0.9f, 0.5f, 0.6f);
        ringMain.maxParticles = 5;
        ringMain.loop = false;
        ringMain.playOnAwake = true;
        ringMain.simulationSpace = ParticleSystemSimulationSpace.World;

        var ringEmission = ringPS.emission;
        ringEmission.rateOverTime = 0f;
        ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 5) });

        var ringShape = ringPS.shape;
        ringShape.enabled = false;

        SetSizeCurve(ringPS, new Keyframe(0f, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1.2f));
        SetColorFade(ringPS, new Color(1f, 0.9f, 0.5f, 0.8f), new Color(1f, 0.8f, 0.2f, 0f));
        SetRenderer(ringGo, "Assets/Materials/Mat_ParticleAdd.mat");

        SavePrefab(go, path);
        Debug.Log("[VFXPrefabBuilder] GiftBigVFX rebuilt: gold star burst + ring");
    }

    static void BuildGiftLegendVFX()
    {
        string path = "Assets/Prefabs/VFX/GiftLegendVFX.prefab";
        var go = new GameObject("GiftLegendVFX");

        // 主粒子：大量彩色星星
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.maxParticles = 50;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.2f;
        // startColor 使用渐变（彩虹色）
        var colorGrad = new Gradient();
        colorGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.84f, 0f), 0.25f),
                new GradientColorKey(new Color(0.3f, 1f, 0.5f), 0.5f),
                new GradientColorKey(new Color(0.3f, 0.5f, 1f), 0.75f),
                new GradientColorKey(new Color(0.8f, 0.3f, 1f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(colorGrad);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] {
            new ParticleSystem.Burst(0f, 20, 30),
            new ParticleSystem.Burst(0.2f, 10, 15)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        SetSizeCurve(ps, new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.05f));
        // 透明度渐变
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fadeGrad = new Gradient();
        fadeGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(0.8f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = fadeGrad;
        SetRenderer(go, "Assets/Materials/Mat_ParticleStar.mat");

        // 子粒子：冲击光环
        var ringGo = new GameObject("ShockRing");
        ringGo.transform.SetParent(go.transform, false);
        var ringPS = ringGo.AddComponent<ParticleSystem>();
        var ringMain = ringPS.main;
        ringMain.duration = 0.6f;
        ringMain.startLifetime = 0.5f;
        ringMain.startSpeed = 0f;
        ringMain.startSize = 0.5f;
        ringMain.startColor = new Color(1f, 0.95f, 0.7f, 0.7f);
        ringMain.maxParticles = 3;
        ringMain.loop = false;
        ringMain.playOnAwake = true;

        var ringEmission = ringPS.emission;
        ringEmission.rateOverTime = 0f;
        ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 3) });

        var ringShape = ringPS.shape;
        ringShape.enabled = false;

        SetSizeCurve(ringPS, new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.5f));
        SetColorFade(ringPS, new Color(1f, 0.95f, 0.7f, 0.8f), new Color(1f, 0.8f, 0.4f, 0f));
        SetRenderer(ringGo, "Assets/Materials/Mat_ParticleAdd.mat");

        SavePrefab(go, path);
        Debug.Log("[VFXPrefabBuilder] GiftLegendVFX rebuilt: rainbow star burst + shock ring");
    }

    static void BuildVictoryVFX()
    {
        string path = "Assets/Prefabs/VFX/VictoryVFX.prefab";
        var go = new GameObject("VictoryVFX");

        // 主粒子：金色向上喷发
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = new Color(1f, 0.84f, 0f, 1f);
        main.maxParticles = 60;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.8f; // 烟花下落感

        var startRotation = main.startRotation;
        startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startRotation = startRotation;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] {
            new ParticleSystem.Burst(0f, 20, 30),
            new ParticleSystem.Burst(0.3f, 15, 20),
            new ParticleSystem.Burst(0.6f, 10, 15)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.5f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 向上喷射

        SetSizeCurve(ps, new Keyframe(0f, 1f), new Keyframe(0.3f, 0.8f), new Keyframe(1f, 0.1f));
        SetColorFade(ps, new Color(1f, 0.9f, 0.3f, 1f), new Color(1f, 0.5f, 0f, 0f));
        SetRenderer(go, "Assets/Materials/Mat_ParticleStar.mat");

        // 子粒子：多色碎片
        var confettiGo = new GameObject("Confetti");
        confettiGo.transform.SetParent(go.transform, false);
        var cps = confettiGo.AddComponent<ParticleSystem>();
        var cMain = cps.main;
        cMain.duration = 1.5f;
        cMain.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        cMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        cMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        cMain.maxParticles = 40;
        cMain.loop = false;
        cMain.playOnAwake = true;
        cMain.simulationSpace = ParticleSystemSimulationSpace.World;
        cMain.gravityModifier = 1.2f;

        // 随机彩色
        var cGrad = new Gradient();
        cGrad.SetKeys(
            new[] {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(Color.green, 0.33f),
                new GradientColorKey(Color.blue, 0.66f),
                new GradientColorKey(Color.yellow, 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        cMain.startColor = new ParticleSystem.MinMaxGradient(cGrad);

        var cEmission = cps.emission;
        cEmission.rateOverTime = 0f;
        cEmission.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 20, 35) });

        var cShape = cps.shape;
        cShape.shapeType = ParticleSystemShapeType.Sphere;
        cShape.radius = 0.8f;

        SetColorFade(cps, Color.white, new Color(1f, 1f, 1f, 0f));
        SetSizeCurve(cps, new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));
        SetRenderer(confettiGo, "Assets/Materials/Mat_ParticleAlpha.mat");

        SavePrefab(go, path);
        Debug.Log("[VFXPrefabBuilder] VictoryVFX rebuilt: gold fountain + confetti");
    }

    // ==================== 工具方法 ====================

    static void SetColorFade(ParticleSystem ps, Color from, Color to)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(to.a, 1f) }
        );
        col.color = grad;
    }

    static void SetSizeCurve(ParticleSystem ps, params Keyframe[] keys)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
    }

    static void SetRenderer(GameObject go, string materialPath)
    {
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 5;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat != null)
            renderer.sharedMaterial = mat;
        else
            Debug.LogWarning($"[VFXPrefabBuilder] Material not found: {materialPath}");
    }

    static void SavePrefab(GameObject go, string path)
    {
        EnsureDirectory(System.IO.Path.GetDirectoryName(path));

        // 删除旧的
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void EnsureDirectory(string dir)
    {
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);
    }
}
