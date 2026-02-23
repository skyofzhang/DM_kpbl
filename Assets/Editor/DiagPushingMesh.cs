#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

public static class DiagPushingMesh
{
    public static void Execute()
    {
        string root = "Assets/Models/Kpbl/5个礼物召唤的水豚单位";
        var tiers = new (int tier, string dir)[]
        {
            (2, "卡皮巴拉礼物单位2"),
            (3, "卡皮巴拉礼物单位3"),
            (4, "卡皮巴拉礼物单位4"),
            (5, "卡皮巴拉礼物单位5"),
            (6, "卡皮巴拉礼物单位6"),
        };

        foreach (var cfg in tiers)
        {
            string subDir = $"{root}/{cfg.dir}";

            // Check Pushing FBX for mesh
            string pushPath = $"{subDir}/gift{cfg.tier}-Pushing.fbx";
            var pushFbx = AssetDatabase.LoadAssetAtPath<GameObject>(pushPath);
            if (pushFbx == null) { Debug.LogWarning($"[DiagMesh] Gift{cfg.tier}: Pushing FBX not found"); continue; }

            var pushInst = Object.Instantiate(pushFbx);

            // Check for SkinnedMeshRenderer (animated mesh) or MeshFilter (static mesh)
            var skinned = pushInst.GetComponentsInChildren<SkinnedMeshRenderer>();
            var meshFilters = pushInst.GetComponentsInChildren<MeshFilter>();
            var renderers = pushInst.GetComponentsInChildren<Renderer>();
            var animator = pushInst.GetComponent<Animator>();

            Debug.Log($"[DiagMesh] Gift{cfg.tier}-Pushing: SkinnedMesh={skinned.Length}, MeshFilter={meshFilters.Length}, Renderers={renderers.Length}, HasAnimator={animator != null}");

            if (skinned.Length > 0)
            {
                foreach (var s in skinned)
                {
                    Debug.Log($"[DiagMesh]   SkinnedMesh: '{s.name}' mesh={s.sharedMesh?.name} verts={s.sharedMesh?.vertexCount} bones={s.bones?.Length}");
                }
            }
            if (meshFilters.Length > 0)
            {
                foreach (var m in meshFilters)
                {
                    Debug.Log($"[DiagMesh]   MeshFilter: '{m.name}' mesh={m.sharedMesh?.name} verts={m.sharedMesh?.vertexCount}");
                }
            }

            // Check hierarchy
            var allT = pushInst.GetComponentsInChildren<Transform>();
            Debug.Log($"[DiagMesh]   Total transforms: {allT.Length}");

            // Also check model FBX for mesh type
            string modelPath = $"{subDir}/gift{cfg.tier}.fbx";
            var modelFbx = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelFbx != null)
            {
                var modelInst = Object.Instantiate(modelFbx);
                var mSkinned = modelInst.GetComponentsInChildren<SkinnedMeshRenderer>();
                var mMeshFilters = modelInst.GetComponentsInChildren<MeshFilter>();
                Debug.Log($"[DiagMesh] Gift{cfg.tier} MODEL: SkinnedMesh={mSkinned.Length}, MeshFilter={mMeshFilters.Length}");
                if (mMeshFilters.Length > 0)
                {
                    foreach (var m in mMeshFilters)
                        Debug.Log($"[DiagMesh]   ModelMesh: '{m.name}' mesh={m.sharedMesh?.name} verts={m.sharedMesh?.vertexCount}");
                }
                if (mSkinned.Length > 0)
                {
                    foreach (var s in mSkinned)
                        Debug.Log($"[DiagMesh]   ModelSkinned: '{s.name}' mesh={s.sharedMesh?.name} verts={s.sharedMesh?.vertexCount}");
                }
                Object.DestroyImmediate(modelInst);
            }

            // Check importer settings
            var modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            var pushImporter = AssetImporter.GetAtPath(pushPath) as ModelImporter;
            if (modelImporter != null)
                Debug.Log($"[DiagMesh] Gift{cfg.tier} MODEL importer: animType={modelImporter.animationType}, avatar={modelImporter.sourceAvatar?.name ?? "null"}");
            if (pushImporter != null)
                Debug.Log($"[DiagMesh] Gift{cfg.tier} PUSHING importer: animType={pushImporter.animationType}, avatar={pushImporter.sourceAvatar?.name ?? "null"}");

            Object.DestroyImmediate(pushInst);
        }

        // Also check the working KpblUnit for reference
        var kpblPath = "Assets/Models/Kpbl/KpblUnit.fbx";
        var kpblImporter = AssetImporter.GetAtPath(kpblPath) as ModelImporter;
        if (kpblImporter != null)
            Debug.Log($"[DiagMesh] KpblUnit importer: animType={kpblImporter.animationType}");

        Debug.Log("[DiagMesh] === DONE ===");
    }
}
#endif
