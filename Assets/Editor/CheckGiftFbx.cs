#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 检查 gift5/gift6 Pushing FBX 的导入状态
/// </summary>
public static class CheckGiftFbx
{
    public static void Execute()
    {
        string[] fbxPaths = {
            "Assets/Models/Kpbl/5个礼物召唤的水豚单位/卡皮巴拉礼物单位5/gift5-Pushing.fbx",
            "Assets/Models/Kpbl/5个礼物召唤的水豚单位/卡皮巴拉礼物单位6/gift6-Pushing.fbx"
        };

        foreach (var path in fbxPaths)
        {
            Debug.Log($"============ {System.IO.Path.GetFileName(path)} ============");

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"  ModelImporter is NULL for: {path}");
                continue;
            }

            // 动画类型
            Debug.Log($"  animationType = {importer.animationType}");
            Debug.Log($"  importAnimation = {importer.importAnimation}");

            // Avatar 设置
            var so = new SerializedObject(importer);
            var avatarSetup = so.FindProperty("m_AvatarSetup");
            Debug.Log($"  m_AvatarSetup = {(avatarSetup != null ? avatarSetup.intValue.ToString() : "NOT FOUND")} (0=NoAvatar, 1=CreateFromThis, 2=CopyFromOther)");

            // 查找 Avatar
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            int avatarCount = 0;
            int clipCount = 0;
            int boneCount = 0;
            string clipNames = "";
            foreach (var a in allAssets)
            {
                if (a is Avatar av) { avatarCount++; Debug.Log($"  Avatar found: {av.name}"); }
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    clipCount++;
                    clipNames += $"'{c.name}' ({c.length:F2}s) ";
                }
            }
            Debug.Log($"  Avatars: {avatarCount}, AnimClips: {clipCount} {clipNames}");

            // 检查骨骼/SkinnedMeshRenderer
            var fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbxObj != null)
            {
                var smr = fbxObj.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    boneCount = smr.bones != null ? smr.bones.Length : 0;
                    Debug.Log($"  SkinnedMeshRenderer: YES, bones={boneCount}");
                }
                else
                {
                    var mf = fbxObj.GetComponentInChildren<MeshFilter>();
                    Debug.Log($"  SkinnedMeshRenderer: NO (MeshFilter={mf != null})");
                }

                // hierarchy
                var transforms = fbxObj.GetComponentsInChildren<Transform>();
                Debug.Log($"  Hierarchy nodes: {transforms.Length}");

                // AnimationClip loop setting
                var clips = importer.clipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    foreach (var c in clips)
                        Debug.Log($"  Clip '{c.name}': loopTime={c.loopTime}");
                }
                else
                {
                    var defaults = importer.defaultClipAnimations;
                    Debug.Log($"  clipAnimations empty, defaultClipAnimations count={defaults?.Length ?? 0}");
                    if (defaults != null)
                        foreach (var c in defaults)
                            Debug.Log($"  DefaultClip '{c.name}': loopTime={c.loopTime}");
                }
            }
        }

        Debug.Log("============ CHECK DONE ============");
    }
}
#endif
