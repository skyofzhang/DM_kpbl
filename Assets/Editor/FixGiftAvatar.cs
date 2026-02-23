#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 为所有Gift Pushing FBX强制创建Avatar
/// Generic动画模式下需要设置avatarSetup=CreateFromThisModel才会自动生成Avatar
/// </summary>
public static class FixGiftAvatar
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
            string pushPath = $"{root}/{cfg.dir}/gift{cfg.tier}-Pushing.fbx";
            var importer = AssetImporter.GetAtPath(pushPath) as ModelImporter;
            if (importer == null) continue;

            // 关键：设置 animationType=Generic + avatarSetup=CreateFromThisModel
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            // avatarSetup: 0=NoAvatar, 1=CreateFromThisModel, 2=CopyFromOther
            // 通过SerializedObject设置（ModelImporter没有公开avatarSetup属性）
            var so = new SerializedObject(importer);
            var avatarSetupProp = so.FindProperty("m_AvatarSetup");
            if (avatarSetupProp != null && avatarSetupProp.intValue != 1)
            {
                avatarSetupProp.intValue = 1; // CreateFromThisModel
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
                Debug.Log($"[FixAvatar] Gift{cfg.tier}: Set avatarSetup=CreateFromThisModel");
            }

            // 确保rootMotionBoneName设置正确
            var rootBoneProp = so.FindProperty("m_HumanDescription.m_RootMotionBoneName");
            if (rootBoneProp != null)
            {
                Debug.Log($"[FixAvatar] Gift{cfg.tier}: rootMotionBone='{rootBoneProp.stringValue}'");
            }

            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log($"[FixAvatar] Gift{cfg.tier}: Reimported");
            }

            // 验证Avatar是否生成
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(pushPath);
            Avatar avatar = null;
            foreach (var asset in allAssets)
                if (asset is Avatar av) { avatar = av; break; }

            if (avatar != null)
                Debug.Log($"[FixAvatar] Gift{cfg.tier}: Avatar OK = {avatar.name} (isValid={avatar.isValid})");
            else
                Debug.LogWarning($"[FixAvatar] Gift{cfg.tier}: Still NO avatar after reimport!");
        }

        Debug.Log("[FixAvatar] === DONE ===");
    }
}
#endif
