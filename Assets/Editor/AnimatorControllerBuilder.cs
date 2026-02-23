#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;

namespace CapybaraDuel.Editor
{
    /// <summary>
    /// 从 FBX 提取 AnimationClip → 创建模板化 Animator Controller
    /// 状态机: Idle → Run → Attack → Dead (通过 clip 名关键字匹配)
    /// </summary>
    public static class AnimatorControllerBuilder
    {
        [MenuItem("CapybaraDuel/2. Build AnimatorControllers", false, 11)]
        public static void BuildAllControllers()
        {
            string modelsRoot = "Assets/Models";
            string animOutput = "Assets/Animations";
            EnsureFolder(animOutput);

            int created = 0;
            int skipped = 0;

            string[] subDirs = AssetDatabase.GetSubFolders(modelsRoot);
            foreach (string subDir in subDirs)
            {
                string folderName = Path.GetFileName(subDir);

                // 查找 FBX 文件
                string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { subDir });
                if (fbxGuids.Length == 0) continue;

                string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
                if (!fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                // 提取所有 AnimationClip
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                var clips = allAssets
                    .OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__"))
                    .ToArray();

                if (clips.Length == 0)
                {
                    Debug.Log($"[AnimBuilder] {folderName}: No clips in FBX, creating basic controller");
                }

                // 创建 AnimatorController
                string controllerPath = $"{animOutput}/AC_{folderName}.controller";
                if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                {
                    skipped++;
                    continue;
                }

                var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var rootStateMachine = controller.layers[0].stateMachine;

                // 添加参数
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
                controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

                // 创建状态
                AnimatorState idleState = null;
                AnimatorState runState = null;
                AnimatorState attackState = null;
                AnimatorState deadState = null;

                // 尝试匹配 clip 到状态
                foreach (var clip in clips)
                {
                    string clipName = clip.name.ToLower();

                    if (idleState == null && (clipName.Contains("idle") || clipName.Contains("stand") || clipName.Contains("wait")))
                    {
                        idleState = rootStateMachine.AddState("Idle");
                        idleState.motion = clip;
                    }
                    else if (runState == null && (clipName.Contains("run") || clipName.Contains("walk") || clipName.Contains("move")))
                    {
                        runState = rootStateMachine.AddState("Run");
                        runState.motion = clip;
                    }
                    else if (attackState == null && (clipName.Contains("attack") || clipName.Contains("tui") || clipName.Contains("push") || clipName.Contains("hit")))
                    {
                        attackState = rootStateMachine.AddState("Attack");
                        attackState.motion = clip;
                    }
                    else if (deadState == null && (clipName.Contains("dead") || clipName.Contains("die") || clipName.Contains("fall") || clipName.Contains("fan")))
                    {
                        deadState = rootStateMachine.AddState("Dead");
                        deadState.motion = clip;
                    }
                }

                // 如果没有匹配到，用第一个 clip 作为 Idle，按顺序分配
                if (clips.Length > 0)
                {
                    int clipIdx = 0;
                    if (idleState == null && clipIdx < clips.Length)
                    {
                        idleState = rootStateMachine.AddState("Idle");
                        idleState.motion = clips[clipIdx++];
                    }
                    if (runState == null && clipIdx < clips.Length)
                    {
                        runState = rootStateMachine.AddState("Run");
                        runState.motion = clips[clipIdx++];
                    }
                    if (attackState == null && clipIdx < clips.Length)
                    {
                        attackState = rootStateMachine.AddState("Attack");
                        attackState.motion = clips[clipIdx++];
                    }
                    if (deadState == null && clipIdx < clips.Length)
                    {
                        deadState = rootStateMachine.AddState("Dead");
                        deadState.motion = clips[clipIdx++];
                    }
                }
                else
                {
                    // 没有任何 clip，创建空状态
                    idleState = rootStateMachine.AddState("Idle");
                    runState = rootStateMachine.AddState("Run");
                    attackState = rootStateMachine.AddState("Attack");
                    deadState = rootStateMachine.AddState("Dead");
                }

                // 设置默认状态
                rootStateMachine.defaultState = idleState;

                // 创建转换
                if (idleState != null && runState != null)
                {
                    var t1 = idleState.AddTransition(runState);
                    t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                    t1.duration = 0.15f;
                    t1.hasExitTime = false;

                    var t2 = runState.AddTransition(idleState);
                    t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
                    t2.duration = 0.15f;
                    t2.hasExitTime = false;
                }

                if (runState != null && attackState != null)
                {
                    var t3 = runState.AddTransition(attackState);
                    t3.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
                    t3.duration = 0.1f;
                    t3.hasExitTime = false;

                    var t4 = attackState.AddTransition(runState);
                    t4.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
                    t4.duration = 0.1f;
                    t4.hasExitTime = true;
                }

                // Any State → Dead
                if (deadState != null)
                {
                    var tDead = rootStateMachine.AddAnyStateTransition(deadState);
                    tDead.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
                    tDead.duration = 0.1f;
                    tDead.hasExitTime = false;
                }

                EditorUtility.SetDirty(controller);
                created++;
                Debug.Log($"[AnimBuilder] Created: {controllerPath} ({clips.Length} clips)");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnimBuilder] Done! Created: {created}, Skipped: {skipped}");
            EditorUtility.DisplayDialog("AnimatorControllers Built",
                $"创建了 {created} 个动画控制器，跳过了 {skipped} 个已存在。\n输出目录: {animOutput}",
                "OK");
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folderName = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
#endif
