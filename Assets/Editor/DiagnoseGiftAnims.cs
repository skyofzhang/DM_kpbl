#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

public static class DiagnoseGiftAnims
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

            // Check model FBX skeleton
            string modelPath = $"{subDir}/gift{cfg.tier}.fbx";
            var modelFbx = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelFbx == null) { Debug.LogWarning($"[Diag] Gift{cfg.tier}: model FBX not found"); continue; }

            // List model hierarchy
            var modelInst = Object.Instantiate(modelFbx);
            var allTransforms = modelInst.GetComponentsInChildren<Transform>();
            string hierarchy = string.Join("\n    ", allTransforms.Select(t => GetPath(t, modelInst.transform)));
            Debug.Log($"[Diag] Gift{cfg.tier} MODEL hierarchy ({allTransforms.Length} bones):\n    {hierarchy}");

            // Check if model FBX itself contains animation clips
            var modelAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            var modelClips = modelAssets.Where(a => a is AnimationClip c && !c.name.StartsWith("__preview__")).ToArray();
            if (modelClips.Length > 0)
            {
                Debug.Log($"[Diag] Gift{cfg.tier} MODEL FBX contains {modelClips.Length} clip(s): {string.Join(", ", modelClips.Select(c => c.name))}");
                foreach (var mc in modelClips)
                {
                    var clip = mc as AnimationClip;
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    var paths = bindings.Select(b => b.path).Distinct().Take(10).ToArray();
                    Debug.Log($"[Diag]   Clip '{clip.name}' bindings ({bindings.Length} curves), paths: {string.Join(", ", paths)}");
                }
            }
            else
            {
                Debug.Log($"[Diag] Gift{cfg.tier} MODEL FBX has NO animation clips");
            }

            // Check Pushing FBX animation clips
            string pushPath = $"{subDir}/gift{cfg.tier}-Pushing.fbx";
            var pushFbx = AssetDatabase.LoadAssetAtPath<GameObject>(pushPath);
            if (pushFbx == null) { Debug.LogWarning($"[Diag] Gift{cfg.tier}: Pushing FBX not found"); Object.DestroyImmediate(modelInst); continue; }

            var pushAssets = AssetDatabase.LoadAllAssetsAtPath(pushPath);
            var pushClips = pushAssets.Where(a => a is AnimationClip c && !c.name.StartsWith("__preview__")).ToArray();
            Debug.Log($"[Diag] Gift{cfg.tier} PUSHING FBX contains {pushClips.Length} clip(s): {string.Join(", ", pushClips.Select(c => c.name))}");

            foreach (var pc in pushClips)
            {
                var clip = pc as AnimationClip;
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var paths = bindings.Select(b => b.path).Distinct().ToArray();
                Debug.Log($"[Diag]   Clip '{clip.name}' has {bindings.Length} curves, {paths.Length} unique paths");
                Debug.Log($"[Diag]   Paths: {string.Join(", ", paths.Take(15))}");

                // Check if paths match model hierarchy
                int matched = 0, unmatched = 0;
                foreach (var p in paths)
                {
                    var found = modelInst.transform.Find(p);
                    if (found != null) matched++;
                    else { unmatched++; if (unmatched <= 3) Debug.LogWarning($"[Diag]   MISMATCH: path '{p}' not found in model"); }
                }
                Debug.Log($"[Diag]   Path match: {matched}/{paths.Length} matched, {unmatched} unmatched");
            }

            // Check Pushing FBX skeleton
            var pushInst = Object.Instantiate(pushFbx);
            var pushTransforms = pushInst.GetComponentsInChildren<Transform>();
            Debug.Log($"[Diag] Gift{cfg.tier} PUSHING hierarchy ({pushTransforms.Length} bones)");

            Object.DestroyImmediate(modelInst);
            Object.DestroyImmediate(pushInst);
        }

        // Also check current AC assignments on prefabs
        string outputDir = "Assets/Prefabs/Units/GiftTiers";
        for (int t = 2; t <= 6; t++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{outputDir}/Gift{t}Unit.prefab");
            if (prefab == null) continue;
            var animator = prefab.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                Debug.Log($"[Diag] Gift{t}Unit.prefab AC = {animator.runtimeAnimatorController.name}");
                // Check avatar
                Debug.Log($"[Diag] Gift{t}Unit.prefab Avatar = {(animator.avatar != null ? animator.avatar.name : "NULL")}");
            }
            else
            {
                Debug.LogWarning($"[Diag] Gift{t}Unit.prefab has NO animator or NO AC!");
            }
        }

        Debug.Log("[Diag] === DIAGNOSIS COMPLETE ===");
    }

    static string GetPath(Transform t, Transform root)
    {
        if (t == root) return "(root)";
        string path = t.name;
        var parent = t.parent;
        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
#endif
