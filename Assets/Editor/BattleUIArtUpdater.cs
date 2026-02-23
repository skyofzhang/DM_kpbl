#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;

namespace CapybaraDuel.Editor
{
    /// <summary>
    /// 战斗界面美术资源替换脚本
    /// 将切图资源应用到场景中的TopBar UI元素上
    /// 菜单: CapybaraDuel > Update Battle UI Art
    /// </summary>
    public static class BattleUIArtUpdater
    {
        private const string ART_PATH = "Assets/Art/BattleUI/";

        [MenuItem("CapybaraDuel/Update Battle UI Art (Safe)")]
        public static void UpdateBattleUIArt()
        {
            // 查找场景中的 GameUIPanel Canvas
            var allCanvas = Object.FindObjectsOfType<Canvas>(true);
            Canvas gameUICanvas = null;
            foreach (var c in allCanvas)
            {
                if (c.gameObject.name == "GameUIPanel")
                {
                    gameUICanvas = c;
                    break;
                }
            }

            if (gameUICanvas == null)
            {
                EditorUtility.DisplayDialog("Error", "找不到 GameUIPanel Canvas!", "OK");
                return;
            }

            int updated = 0;

            // === 1. 结束按钮底图 ===
            updated += SetButtonSprite(gameUICanvas, "BtnEnd", "btn_end_bg.png");

            // === 2. 设置按钮底图 ===
            updated += SetButtonSprite(gameUICanvas, "BtnSettings", "btn_settings_bg.png");

            // === 3. 进度条底图 (ProgressBarContainer的背景) ===
            updated += SetOrCreateBgImage(gameUICanvas, "ProgressBarContainer", "progress_bar_bg.png", "ProgressBarBg", true);

            // === 4. 进度条橘子标尺 (替换BarDivider的Image) ===
            updated += SetDividerOrangeIcon(gameUICanvas);

            // === 5. 倒计时底图 ===
            updated += SetOrCreateBgImage(gameUICanvas, "TimerText", "timer_bg.png", "TimerBg", true);

            // === 6. 积分池底图 ===
            updated += SetOrCreateBgImage(gameUICanvas, "ScorePoolText", "score_pool_bg.png", "ScorePoolBg", true);

            // === 7. 顶部推力差提示底图 ===
            updated += SetOrCreateBgImage(gameUICanvas, "HintText", "hint_bar_bg.png", "HintBg", true);

            // === 8. 连胜底图（左） ===
            updated += SetOrCreateBgImage(gameUICanvas, "WinStreakLeft", "win_streak_left_bg.png", "WinStreakLeftBg", true);

            // === 9. 连胜底图（右） ===
            updated += SetOrCreateBgImage(gameUICanvas, "WinStreakRight", "win_streak_right_bg.png", "WinStreakRightBg", true);

            // === 10. 贴纸开关按钮底图 ===
            updated += SetButtonSprite(gameUICanvas, "BtnSticker", "btn_sticker.png");

            Debug.Log($"[BattleUIArt] ✅ Updated {updated} UI elements with new art assets.");
            EditorUtility.DisplayDialog("Battle UI Art Updated",
                $"成功更新 {updated} 个UI元素的美术资源。\n\n请检查场景并手动保存 (Ctrl+S 或 SaveCurrentScene)。",
                "OK");
        }

        /// <summary>设置按钮的Image sprite</summary>
        static int SetButtonSprite(Canvas canvas, string buttonName, string artFile)
        {
            var btn = FindChildRecursive(canvas.transform, buttonName);
            if (btn == null)
            {
                Debug.LogWarning($"[BattleUIArt] ⚠️ Button '{buttonName}' not found in scene");
                return 0;
            }

            var img = btn.GetComponent<Image>();
            if (img == null)
            {
                img = btn.gameObject.AddComponent<Image>();
            }

            var sprite = LoadSprite(artFile);
            if (sprite == null) return 0;

            Undo.RecordObject(img, $"Set {buttonName} sprite");
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white; // 确保不被染色
            EditorUtility.SetDirty(img);

            Debug.Log($"[BattleUIArt] ✅ {buttonName} → {artFile}");
            return 1;
        }

        /// <summary>为目标元素创建或更新背景Image子物体</summary>
        static int SetOrCreateBgImage(Canvas canvas, string targetName, string artFile, string bgObjName, bool insertBehind)
        {
            var target = FindChildRecursive(canvas.transform, targetName);
            if (target == null)
            {
                Debug.LogWarning($"[BattleUIArt] ⚠️ Target '{targetName}' not found in scene");
                return 0;
            }

            var sprite = LoadSprite(artFile);
            if (sprite == null) return 0;

            // 查找是否已有bg子物体
            Transform bgTr = target.Find(bgObjName);
            if (bgTr == null)
            {
                // 在父物体下创建bg（与target同级，在target之前）
                var parent = target.parent;
                var bgGo = new GameObject(bgObjName);
                Undo.RegisterCreatedObjectUndo(bgGo, $"Create {bgObjName}");
                bgGo.transform.SetParent(parent, false);

                // 设置与target相同的位置和大小
                var bgRect = bgGo.AddComponent<RectTransform>();
                var targetRect = target.GetComponent<RectTransform>();
                if (targetRect != null)
                {
                    bgRect.anchorMin = targetRect.anchorMin;
                    bgRect.anchorMax = targetRect.anchorMax;
                    bgRect.anchoredPosition = targetRect.anchoredPosition;
                    bgRect.sizeDelta = targetRect.sizeDelta;
                    bgRect.pivot = targetRect.pivot;
                }

                var bgImg = bgGo.AddComponent<Image>();
                bgImg.sprite = sprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.preserveAspect = false;
                bgImg.color = Color.white;
                bgImg.raycastTarget = false; // 背景不拦截点击

                // 放到target之前（渲染在后面）
                if (insertBehind)
                {
                    int targetIdx = target.GetSiblingIndex();
                    bgGo.transform.SetSiblingIndex(targetIdx);
                }

                bgTr = bgGo.transform;
                Debug.Log($"[BattleUIArt] ✅ Created {bgObjName} behind {targetName}");
            }
            else
            {
                // 已存在，更新sprite
                var bgImg = bgTr.GetComponent<Image>();
                if (bgImg != null)
                {
                    Undo.RecordObject(bgImg, $"Update {bgObjName} sprite");
                    bgImg.sprite = sprite;
                    bgImg.color = Color.white;
                    EditorUtility.SetDirty(bgImg);
                }
                Debug.Log($"[BattleUIArt] ✅ Updated {bgObjName} sprite");
            }

            return 1;
        }

        /// <summary>把BarDivider的白色方块替换为橘子图标</summary>
        static int SetDividerOrangeIcon(Canvas canvas)
        {
            var divider = FindChildRecursive(canvas.transform, "BarDivider");
            if (divider == null)
            {
                Debug.LogWarning("[BattleUIArt] ⚠️ BarDivider not found");
                return 0;
            }

            var sprite = LoadSprite("progress_bar_orange.png");
            if (sprite == null) return 0;

            var img = divider.GetComponent<Image>();
            if (img == null) img = divider.gameObject.AddComponent<Image>();

            Undo.RecordObject(img, "Set BarDivider orange icon");
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            EditorUtility.SetDirty(img);

            // 调整大小让橘子图标明显
            var rect = divider.GetComponent<RectTransform>();
            if (rect != null)
            {
                Undo.RecordObject(rect, "Resize BarDivider for orange icon");
                rect.sizeDelta = new Vector2(40f, 40f);
                EditorUtility.SetDirty(rect);
            }

            Debug.Log("[BattleUIArt] ✅ BarDivider → orange icon");
            return 1;
        }

        /// <summary>加载Sprite资源</summary>
        static Sprite LoadSprite(string fileName)
        {
            string path = ART_PATH + fileName;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"[BattleUIArt] ❌ Failed to load sprite: {path}. Make sure TextureImporter type is Sprite.");
            }
            return sprite;
        }

        /// <summary>递归查找子物体（包括inactive）</summary>
        static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindChildRecursive(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
