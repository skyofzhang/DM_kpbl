using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// #54 战斗UI三项修复:
/// 1. HintText(反推差提示)增大+底图+RichText高亮数字
/// 2. 进度条LeftArrows/RightArrows删除(静态无意义)，OrangeSpeedHUD已有动态箭头
/// 3. 连胜文字增大+确保可见+底图增大
/// </summary>
public class BattleUIFix54
{
    [MenuItem("Tools/BattleUI Fix54 - 反推差+箭头+连胜")]
    public static void Execute()
    {
        int fixCount = 0;

        // ============================================
        // Fix1: HintText 增强可读性
        // ============================================
        var hintGo = GameObject.Find("Canvas/GameUIPanel/HintText");
        if (hintGo != null)
        {
            var tmp = hintGo.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                // 加大字号: 30 → 36
                tmp.fontSize = 36;
                // 加粗+描边增强
                tmp.fontStyle = FontStyles.Bold;
                tmp.outlineWidth = 0.5f;
                tmp.outlineColor = new Color32(0, 0, 0, 255);
                // 启用RichText（用于代码中高亮数字）
                tmp.richText = true;
                // enableExtraPadding确保描边不被裁切
                tmp.extraPadding = true;
                fixCount++;
                Debug.Log($"[Fix54] HintText: fontSize=36, outlineWidth=0.5, extraPadding=true");
            }

            // 增大sizeDelta确保文字不被截断
            var rt = hintGo.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(700f, 60f); // 600×50 → 700×60
                fixCount++;
                Debug.Log($"[Fix54] HintText sizeDelta: 700×60");
            }
        }

        // 给HintText添加底色背景（半透明黑底提高对比度）
        if (hintGo != null)
        {
            // 检查是否已有HintBg
            var existingBg = hintGo.transform.Find("HintBg");
            if (existingBg == null)
            {
                var bgGo = new GameObject("HintBg");
                bgGo.transform.SetParent(hintGo.transform, false);
                bgGo.transform.SetAsFirstSibling(); // 底层

                var bgImg = bgGo.AddComponent<Image>();
                bgImg.color = new Color(0f, 0f, 0f, 0.45f); // 半透明黑底

                var bgRt = bgGo.GetComponent<RectTransform>();
                // 撑满父对象并留padding
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = new Vector2(-15f, -5f);  // 左下多出
                bgRt.offsetMax = new Vector2(15f, 5f);    // 右上多出
                // 圆角效果需要Sprite，暂用纯色
                fixCount++;
                Debug.Log("[Fix54] HintText: 添加半透明黑底 HintBg");
            }
            else
            {
                Debug.Log("[Fix54] HintText: HintBg已存在，跳过");
            }
        }

        // ============================================
        // Fix2: 删除进度条内的静态箭头(LeftArrows/RightArrows)
        // 这些箭头用旧版Text组件且代码不更新，OrangeSpeedHUD已有动态速度指示
        // ============================================
        string[] arrowPaths = new string[] {
            "Canvas/GameUIPanel/TopBar/TopBarBg/ProgressBarContainer/LeftArrows",
            "Canvas/GameUIPanel/TopBar/TopBarBg/ProgressBarContainer/RightArrows"
        };
        foreach (var path in arrowPaths)
        {
            var arrowGo = GameObject.Find(path);
            if (arrowGo != null)
            {
                Object.DestroyImmediate(arrowGo);
                fixCount++;
                Debug.Log($"[Fix54] 删除: {path}");
            }
        }

        // ============================================
        // Fix3: 连胜文字增强
        // ============================================
        string[] streakPaths = new string[] {
            "Canvas/GameUIPanel/WinStreakLeft",
            "Canvas/GameUIPanel/WinStreakRight"
        };
        foreach (var path in streakPaths)
        {
            var streakGo = GameObject.Find(path);
            if (streakGo != null)
            {
                var tmp = streakGo.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    // 字号加大: 24 → 28
                    tmp.fontSize = 28;
                    // 确保加粗+描边
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.outlineWidth = 0.4f;
                    tmp.outlineColor = new Color32(0, 0, 0, 255);
                    tmp.extraPadding = true;
                    // 确保overflow=Overflow不截断
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    fixCount++;
                    Debug.Log($"[Fix54] {streakGo.name}: fontSize=28, outlineWidth=0.4");
                }

                // sizeDelta增大: 120×45 → 160×50
                var rt = streakGo.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(160f, 50f);
                    fixCount++;
                    Debug.Log($"[Fix54] {streakGo.name} sizeDelta: 160×50");
                }
            }
        }

        // 连胜底图也增大匹配
        string[] streakBgPaths = new string[] {
            "Canvas/GameUIPanel/WinStreakLeftBg",
            "Canvas/GameUIPanel/WinStreakRightBg"
        };
        foreach (var path in streakBgPaths)
        {
            var bgGo = GameObject.Find(path);
            if (bgGo != null)
            {
                var rt = bgGo.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // 底图略大于文字
                    rt.sizeDelta = new Vector2(180f, 55f);
                    fixCount++;
                    Debug.Log($"[Fix54] {bgGo.name} sizeDelta: 180×55");
                }
            }
        }

        // 标记场景dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"=== BattleUIFix54 完成: {fixCount} 项修复 ===");
    }
}
