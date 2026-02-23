#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 修复橘子模型接缝：将法线计算模式改为 Calculate + 平滑角180度
/// 球体模型不需要硬边，全部平滑可以消除UV接缝处的法线不连续
/// </summary>
public static class FixOrangeSeam
{
    public static void Execute()
    {
        string fbxPath = "Assets/Models/Orange/527_Chengzi.fbx";
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[FixOrangeSeam] ModelImporter not found: {fbxPath}");
            return;
        }

        Debug.Log($"[FixOrangeSeam] Before: normalImportMode={importer.importNormals}, normalSmoothAngle={importer.normalSmoothingAngle}, normalCalcMode={importer.normalCalculationMode}");

        // 改为 Calculate 模式（不依赖FBX内置法线，由Unity重新计算）
        importer.importNormals = ModelImporterNormals.Calculate;
        // 平滑角180度 = 全部平滑，不产生硬边（球体完全适用）
        importer.normalSmoothingAngle = 180f;
        // 使用 Unweighted_Legacy 或 AreaAndAngleWeighted 计算模式
        importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
        // 平滑来源改为 From Smoothing Groups 以尊重Max的平滑组
        importer.normalSmoothingSource = ModelImporterNormalSmoothingSource.PreferSmoothingGroups;

        importer.SaveAndReimport();

        Debug.Log($"[FixOrangeSeam] After: normalImportMode={importer.importNormals}, normalSmoothAngle={importer.normalSmoothingAngle} => DONE, seam should be eliminated");
    }
}
#endif
