using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// 战斗UI视觉修复 — 对齐美术效果图
/// 修复12项：进度条层级/推力文字/HintText/横幅尺寸/积分池/倒计时/连胜/卡片位置/按钮大小/礼物面板/PosIndicator/全局描边
/// </summary>
public class BattleUIVisualFix : MonoBehaviour
{
    [MenuItem("Tools/Battle UI/Visual Fix - Match Art Design")]
    public static void Execute()
    {
        int fixCount = 0;

        // ============ Fix1: 进度条高度和层级 ============
        var progressBar = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg/ProgressBarContainer");
        if (progressBar != null)
        {
            var rt = progressBar.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix ProgressBar");
            rt.sizeDelta = new Vector2(930f, 40f); // 60→40 更细
            // 确保sibling index = 0（最底层渲染）
            progressBar.transform.SetSiblingIndex(0);
            Debug.Log("[Fix1] ProgressBarContainer: height 60→40, sibling=0");
            fixCount++;
        }

        // BarLeft/BarRight 透明度微调
        var barLeft = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg/ProgressBarContainer/BarLeft");
        if (barLeft != null)
        {
            var img = barLeft.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Fix BarLeft alpha");
                img.color = new Color(1f, 0.55f, 0f, 0.85f); // 0.9→0.85
            }
        }
        var barRight = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg/ProgressBarContainer/BarRight");
        if (barRight != null)
        {
            var img = barRight.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Fix BarRight alpha");
                img.color = new Color(0.3f, 0.85f, 0.3f, 0.85f);
            }
        }

        // ============ Fix2: 推力文字加大 ============
        FixTMPText("Canvas/GameUIPanel/TopBar/LeftForceText", 28f, -1, -1, 150f, 50f, "Fix2 LeftForce");
        FixTMPText("Canvas/GameUIPanel/TopBar/RightForceText", 28f, -1, -1, 150f, 50f, "Fix2 RightForce");
        fixCount++;

        // ============ Fix3: HintText 推力差提示 ============
        var hintGo = GameObject.Find("Canvas/GameUIPanel/HintText");
        if (hintGo != null)
        {
            var rt = hintGo.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix HintText pos");
            rt.anchoredPosition = new Vector2(0f, -15f);
            rt.sizeDelta = new Vector2(600f, 45f);

            var tmp = hintGo.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                Undo.RecordObject(tmp, "Fix HintText style");
                tmp.fontSize = 26f;
                tmp.fontStyle = FontStyles.Bold;
                // 颜色由代码运行时设置，这里设置默认黄色
                tmp.color = new Color(1f, 0.9f, 0.3f, 1f);
            }
            Debug.Log("[Fix3] HintText: y=-15, size=600×45, fontSize=26");
            fixCount++;
        }

        // ============ Fix4: TopBarBg 横幅尺寸 ============
        var topBarBg = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg");
        if (topBarBg != null)
        {
            var rt = topBarBg.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix TopBarBg size");
            rt.sizeDelta = new Vector2(1000f, 200f); // 900×180 → 1000×200
            Debug.Log("[Fix4] TopBarBg: 900×180 → 1000×200");
            fixCount++;
        }

        // ============ Fix5: 积分池文字 ============
        var scorePool = GameObject.Find("Canvas/GameUIPanel/TopBar/ScorePoolText");
        if (scorePool != null)
        {
            var tmp = scorePool.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                Undo.RecordObject(tmp, "Fix ScorePool");
                tmp.fontSize = 24f;
                tmp.fontStyle = FontStyles.Bold;
            }
            Debug.Log("[Fix5] ScorePoolText: fontSize=24, Bold");
            fixCount++;
        }

        // ============ Fix6: 倒计时文字 ============
        var timer = GameObject.Find("Canvas/GameUIPanel/TopBar/TimerText");
        if (timer != null)
        {
            var rt = timer.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix Timer size");
            rt.sizeDelta = new Vector2(200f, 50f); // 40→50

            var tmp = timer.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                Undo.RecordObject(tmp, "Fix Timer font");
                tmp.fontSize = 32f; // 28→32
            }
            Debug.Log("[Fix6] TimerText: fontSize=32, height=50");
            fixCount++;
        }

        // ============ Fix7: 连胜文字 ============
        FixTMPText("Canvas/GameUIPanel/WinStreakLeft", 24f, -1, -1, -1, -1, "Fix7 WinStreakLeft");
        FixTMPText("Canvas/GameUIPanel/WinStreakRight", 24f, -1, -1, -1, -1, "Fix7 WinStreakRight");
        fixCount++;

        // ============ Fix8: 玩家卡片下移 ============
        var leftList = GameObject.Find("Canvas/GameUIPanel/LeftPlayerList");
        if (leftList != null)
        {
            var rt = leftList.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix LeftPlayerList pos");
            rt.anchoredPosition = new Vector2(15f, -290f); // -246→-290
            rt.sizeDelta = new Vector2(480f, 280f); // 458→480
            Debug.Log("[Fix8] LeftPlayerList: y=-246→-290, w=480");
            fixCount++;
        }
        var rightList = GameObject.Find("Canvas/GameUIPanel/RightPlayerList");
        if (rightList != null)
        {
            var rt = rightList.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix RightPlayerList pos");
            rt.anchoredPosition = new Vector2(-15f, -290f); // -251→-290
            rt.sizeDelta = new Vector2(480f, 280f);
            Debug.Log("[Fix8] RightPlayerList: y=-251→-290, w=480");
        }

        // ============ Fix9: 按钮放大 ============
        FixButtonSize("Canvas/GameUIPanel/BtnEnd", 110f, 110f);
        FixButtonSize("Canvas/GameUIPanel/BtnSettings", 110f, 110f);
        fixCount++;

        // ============ Fix10: 礼物面板微调 ============
        var giftPanel = GameObject.Find("Canvas/GameUIPanel/GiftInfoPanel");
        if (giftPanel != null)
        {
            var rt = giftPanel.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fix GiftPanel");
            rt.sizeDelta = new Vector2(370f, 450f); // 390×480 → 370×450
            rt.anchoredPosition = new Vector2(-50f, 95f); // -60,100 → -50,95
            Debug.Log("[Fix10] GiftInfoPanel: 390×480→370×450");
            fixCount++;
        }

        // ============ Fix11: PosIndicator 文字颜色修复 ============
        var posIndicator = GameObject.Find("Canvas/GameUIPanel/TopBar/TopBarBg/PosIndicator");
        if (posIndicator != null)
        {
            var tmp = posIndicator.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                Undo.RecordObject(tmp, "Fix PosIndicator");
                tmp.color = new Color(1f, 1f, 1f, 1f); // 黑色→白色
                tmp.fontSize = 18f; // 40→18
                tmp.fontStyle = FontStyles.Bold;
            }
            Debug.Log("[Fix11] PosIndicator: color=white, fontSize=18");
            fixCount++;
        }

        // ============ Fix12: 全局描边加粗 ============
        int outlineCount = 0;
        var gameUIPanel = GameObject.Find("Canvas/GameUIPanel");
        if (gameUIPanel != null)
        {
            var allTMP = gameUIPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in allTMP)
            {
                // 获取材质属性 — 通过 fontSharedMaterial
                if (tmp.fontMaterial != null)
                {
                    Undo.RecordObject(tmp, "Fix outline");

                    // 使用 TMP 的 outline 属性
                    tmp.outlineWidth = 0.35f; // 0.25→0.35
                    tmp.outlineColor = new Color32(0, 0, 0, 255); // 确保纯黑不透明

                    outlineCount++;
                }
            }
            Debug.Log($"[Fix12] 描边加粗: {outlineCount} 个TMP元素, outlineWidth=0.35");
            fixCount++;
        }

        // 标记场景已修改
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"=== BattleUIVisualFix 完成: {fixCount} 项修复 ===");
    }

    /// <summary>
    /// 修复TMP文字的fontSize和sizeDelta（-1表示不修改）
    /// </summary>
    static void FixTMPText(string path, float fontSize, float colorR, float colorG, float sizeW, float sizeH, string label)
    {
        var go = GameObject.Find(path);
        if (go == null) { Debug.LogWarning($"[{label}] 未找到: {path}"); return; }

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            Undo.RecordObject(tmp, label);
            if (fontSize > 0) tmp.fontSize = fontSize;
        }

        if (sizeW > 0 || sizeH > 0)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, label + " size");
                var sd = rt.sizeDelta;
                if (sizeW > 0) sd.x = sizeW;
                if (sizeH > 0) sd.y = sizeH;
                rt.sizeDelta = sd;
            }
        }
        Debug.Log($"[{label}] fontSize={fontSize}");
    }

    /// <summary>
    /// 修复按钮大小并调整Label子对象
    /// </summary>
    static void FixButtonSize(string path, float w, float h)
    {
        var go = GameObject.Find(path);
        if (go == null) return;

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            Undo.RecordObject(rt, "Fix btn size");
            rt.sizeDelta = new Vector2(w, h);
        }

        // 调整Label子对象
        var label = go.transform.Find("Label");
        if (label != null)
        {
            var labelRt = label.GetComponent<RectTransform>();
            if (labelRt != null)
            {
                Undo.RecordObject(labelRt, "Fix label size");
                labelRt.sizeDelta = new Vector2(100f, 30f); // 80×28 → 100×30
            }

            var tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                Undo.RecordObject(tmp, "Fix label font");
                tmp.fontSize = 20f; // 18→20
            }
        }
        Debug.Log($"[Fix9] {go.name}: {w}×{h}");
    }
}
