using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 一键设置Bloom后处理
/// 1. 创建VolumeProfile（包含Bloom配置）
/// 2. 在场景中创建Global Volume
/// 3. 启用Camera后处理
///
/// 菜单：CapybaraDuel/5. Setup Bloom Post Processing
/// </summary>
public class SetupBloomPostProcessing
{
    [MenuItem("CapybaraDuel/5. Setup Bloom Post Processing")]
    public static void Execute()
    {
        // === 1. 创建或加载 Volume Profile ===
        string profileDir = "Assets/Settings";
        string profilePath = $"{profileDir}/BloomVolumeProfile.asset";

        if (!AssetDatabase.IsValidFolder(profileDir))
            AssetDatabase.CreateFolder("Assets", "Settings");

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            Debug.Log($"[SetupBloom] Created VolumeProfile: {profilePath}");
        }

        // === 2. 配置Bloom ===
        Bloom bloom;
        if (!profile.TryGet(out bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }

        // Bloom参数（柔和光晕，不过分刺眼）
        bloom.active = true;
        bloom.threshold.Override(0.8f);      // 只有HDR亮度>0.8的区域才Bloom
        bloom.intensity.Override(1.5f);      // 中等强度
        bloom.scatter.Override(0.65f);       // 散射范围（0.65 = 柔和扩散）
        bloom.tint.Override(new Color(1f, 0.95f, 0.9f)); // 微暖白色调

        // === 3. 配置Tonemapping（HDR→LDR映射，确保Bloom可见） ===
        Tonemapping tonemap;
        if (!profile.TryGet(out tonemap))
        {
            tonemap = profile.Add<Tonemapping>(true);
        }
        tonemap.active = true;
        tonemap.mode.Override(TonemappingMode.ACES); // ACES色调映射，电影级

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupBloom] Bloom + Tonemapping configured");

        // === 4. 在场景中查找或创建 Global Volume ===
        var existingVolume = Object.FindObjectOfType<Volume>();
        Volume volume;
        if (existingVolume != null)
        {
            volume = existingVolume;
            Debug.Log("[SetupBloom] Using existing Volume in scene");
        }
        else
        {
            var volumeGo = new GameObject("PostProcessVolume");
            volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            Debug.Log("[SetupBloom] Created Global Volume in scene");
        }
        volume.profile = profile;

        // === 5. 启用Camera后处理 ===
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            var camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
                EditorUtility.SetDirty(camData);
                Debug.Log("[SetupBloom] Camera Post Processing enabled");
            }
            else
            {
                Debug.LogWarning("[SetupBloom] No UniversalAdditionalCameraData on Main Camera!");
            }

            // 确保HDR启用
            if (!mainCam.allowHDR)
            {
                mainCam.allowHDR = true;
                EditorUtility.SetDirty(mainCam);
                Debug.Log("[SetupBloom] Camera HDR enabled");
            }
        }
        else
        {
            Debug.LogWarning("[SetupBloom] Main Camera not found!");
        }

        // === 6. 检查URP Asset的HDR设置 ===
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            // URP Asset HDR通过SerializedObject修改
            var so = new SerializedObject(urpAsset);
            var hdrProp = so.FindProperty("m_SupportsHDR");
            if (hdrProp != null && !hdrProp.boolValue)
            {
                hdrProp.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(urpAsset);
                Debug.Log("[SetupBloom] URP Asset HDR enabled");
            }
            so.Dispose();
        }

        Debug.Log("[SetupBloom] ✅ Bloom后处理设置完成！自发光效果现在会有柔和光晕。");
    }
}
