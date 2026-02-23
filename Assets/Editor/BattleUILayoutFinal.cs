using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 基于美术效果图的精确像素布局（1080x1920）
/// 效果图参考: C:\Users\Administrator\Desktop\反馈\美术AI的效果图.png
///
/// 坐标系: Unity Canvas 左下角(0,0)，右上角(1080,1920)
/// 效果图坐标系: 左上角(0,0) → 需要翻转Y轴
/// Unity anchoredPosition 基于 anchor+pivot
/// </summary>
public class BattleUILayoutFinal
{
    public static string Execute()
    {
        int adjusted = 0;
        var log = new System.Text.StringBuilder();

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return "ERROR: Canvas not found";
        var gameUI = canvas.transform.Find("GameUIPanel");
        if (gameUI == null) return "ERROR: GameUIPanel not found";

        // ============================================================
        // 1. TopBar — 容纳顶部所有元素，占屏幕上方约 25%
        // 效果图中顶部区域从 y=0 到约 y=450（含荣誉玩家）
        // ============================================================
        var topBar = gameUI.Find("TopBar");
        if (topBar != null)
        {
            var rect = topBar.GetComponent<RectTransform>();
            // 全宽，顶部到屏幕约 75% 高度处
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 480);
            rect.anchoredPosition = Vector2.zero;
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ TopBar: fullWidth x 480, top");
        }

        // ============================================================
        // 2. TopBarBg (hint_bar_bg) — 顶部横幅
        // 效果图: x=90, y=0, w=900, h=180
        // ============================================================
        var topBarBg = gameUI.Find("TopBar/TopBarBg");
        if (topBarBg != null)
        {
            var rect = topBarBg.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(900, 180);
            rect.anchoredPosition = new Vector2(0, 0);
            var img = topBarBg.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ TopBarBg: 900x180, top-center");
        }

        // ============================================================
        // 3. BtnEnd — 结束按钮
        // 效果图: x=15, y=15, w=90, h=90
        // ============================================================
        var btnEnd = gameUI.Find("BtnEnd");
        if (btnEnd != null)
        {
            var rect = btnEnd.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(90, 90);
            rect.anchoredPosition = new Vector2(15, -15);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ BtnEnd: 90x90, (15,-15)");
        }

        // ============================================================
        // 4. BtnSettings — 设置按钮
        // 效果图: x=975, y=15, w=90, h=90
        // ============================================================
        var btnSettings = gameUI.Find("BtnSettings");
        if (btnSettings != null)
        {
            var rect = btnSettings.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(90, 90);
            rect.anchoredPosition = new Vector2(-15, -15);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ BtnSettings: 90x90, (-15,-15)");
        }

        // ============================================================
        // 5. WinStreakLeft — 左连胜
        // 效果图: x=80, y=135, w=120, h=45
        // 相对 GameUIPanel 左上角
        // ============================================================
        var winLeft = gameUI.Find("WinStreakLeft");
        if (winLeft != null)
        {
            var rect = winLeft.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(120, 45);
            rect.anchoredPosition = new Vector2(80, -135);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ WinStreakLeft: 120x45, (80,-135)");
        }
        // 同步背景位置
        var winLeftBg = gameUI.Find("WinStreakLeftBg");
        if (winLeftBg != null)
        {
            var rect = winLeftBg.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(120, 45);
            rect.anchoredPosition = new Vector2(80, -135);
            EditorUtility.SetDirty(rect);
            adjusted++;
        }

        // ============================================================
        // 6. WinStreakRight — 右连胜
        // 效果图: x=880, y=135, w=120, h=45
        // ============================================================
        var winRight = gameUI.Find("WinStreakRight");
        if (winRight != null)
        {
            var rect = winRight.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(120, 45);
            rect.anchoredPosition = new Vector2(-80, -135);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ WinStreakRight: 120x45, (-80,-135)");
        }
        var winRightBg = gameUI.Find("WinStreakRightBg");
        if (winRightBg != null)
        {
            var rect = winRightBg.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(120, 45);
            rect.anchoredPosition = new Vector2(-80, -135);
            EditorUtility.SetDirty(rect);
            adjusted++;
        }

        // ============================================================
        // 7. LeftForceText — 左推力
        // 效果图: 约 x=360, y=135, w=150, h=45 (在LOGO左侧)
        // 相对TopBar: anchorMin=(0,1) pos=(360, -135)
        // ============================================================
        var leftForce = gameUI.Find("TopBar/LeftForceText");
        if (leftForce != null)
        {
            var rect = leftForce.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(1, 1); // 右对齐
            rect.sizeDelta = new Vector2(150, 45);
            rect.anchoredPosition = new Vector2(510, -135);
            var tmp = leftForce.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 24;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Right;
                tmp.color = new Color(1f, 0.6f, 0.15f, 1f);
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ LeftForceText: 150x45, right-aligned");
        }

        // ============================================================
        // 8. RightForceText — 右推力
        // 效果图: 约 x=570, y=135
        // ============================================================
        var rightForce = gameUI.Find("TopBar/RightForceText");
        if (rightForce != null)
        {
            var rect = rightForce.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1); // 左对齐
            rect.sizeDelta = new Vector2(150, 45);
            rect.anchoredPosition = new Vector2(-510, -135);
            var tmp = rightForce.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 24;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = new Color(0.3f, 0.85f, 0.3f, 1f);
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ RightForceText: 150x45, left-aligned");
        }

        // ============================================================
        // 9. ProgressBarContainer — 进度条
        // 效果图: x=75, y=195, w=930, h=60
        // 相对TopBarBg
        // ============================================================
        var progressBar = gameUI.Find("TopBar/TopBarBg/ProgressBarContainer");
        if (progressBar != null)
        {
            var rect = progressBar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(930, 60);
            rect.anchoredPosition = new Vector2(0, -15); // 略低于横幅底部
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ ProgressBar: 930x60, below banner");
        }

        // ============================================================
        // 10. TimerText — 倒计时
        // 效果图中在进度条左端标记（35198兵）区域
        // 相对TopBar的TimerText
        // ============================================================
        var timerText = gameUI.Find("TopBar/TimerText");
        if (timerText != null)
        {
            var rect = timerText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200, 40);
            rect.anchoredPosition = new Vector2(0, -130); // 横幅中间偏上
            var tmp = timerText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 28;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ TimerText: 200x40, center of banner");
        }
        // TimerBg 对齐
        var timerBg = gameUI.Find("TopBar/TimerBg");
        if (timerBg != null)
        {
            var rect = timerBg.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(220, 50);
            rect.anchoredPosition = new Vector2(0, -130);
            EditorUtility.SetDirty(rect);
            adjusted++;
        }

        // ============================================================
        // 11. ScorePoolText — 积分池
        // 效果图: x=420, y=255, w=240, h=60 (进度条正下方居中)
        // ============================================================
        var scoreText = gameUI.Find("TopBar/ScorePoolText");
        if (scoreText != null)
        {
            var rect = scoreText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(240, 60);
            rect.anchoredPosition = new Vector2(0, -255);
            var tmp = scoreText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 20;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ ScorePoolText: 240x60, below progress bar");
        }
        var scoreBg = gameUI.Find("TopBar/ScorePoolBg");
        if (scoreBg != null)
        {
            var rect = scoreBg.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(260, 65);
            rect.anchoredPosition = new Vector2(0, -253);
            EditorUtility.SetDirty(rect);
            adjusted++;
        }

        // ============================================================
        // 12. LeftPlayerList — 左侧前三荣誉玩家
        // 效果图: 约 x=24, y=330, 三个玩家横排
        // 每个约 90px宽, 120px高, 间距约 66px
        // 整体区域: x=24, y=330, w=270, h=120
        // ============================================================
        var leftList = gameUI.Find("LeftPlayerList");
        if (leftList != null)
        {
            var rect = leftList.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(330, 150);
            rect.anchoredPosition = new Vector2(15, -330);
            var img = leftList.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(1, 1, 1, 0); // 透明容器
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ LeftPlayerList: 330x150, (15,-330)");
        }

        // ============================================================
        // 13. RightPlayerList — 右侧前三荣誉玩家
        // 效果图: 对称于右侧
        // ============================================================
        var rightList = gameUI.Find("RightPlayerList");
        if (rightList != null)
        {
            var rect = rightList.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(330, 150);
            rect.anchoredPosition = new Vector2(-15, -330);
            var img = rightList.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(1, 1, 1, 0); // 透明容器
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ RightPlayerList: 330x150, (-15,-330)");
        }

        // ============================================================
        // 14. BtnSticker — 贴纸按钮（右下角！不是左下角）
        // 效果图: x=990, y=1590, w=75, h=75
        // ============================================================
        var btnSticker = gameUI.Find("BtnSticker");
        if (btnSticker != null)
        {
            var rect = btnSticker.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(75, 75);
            rect.anchoredPosition = new Vector2(-15, 15);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ BtnSticker: 75x75, bottom-right (-15,15)");
        }

        // ============================================================
        // 15. GiftInfoPanel — 礼物面板（右下区域）
        // 效果图: x=630, y=1380, w=390, h=480
        // ============================================================
        var giftPanel = gameUI.Find("GiftInfoPanel");
        if (giftPanel != null)
        {
            var rect = giftPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(390, 480);
            rect.anchoredPosition = new Vector2(-60, 100);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ GiftInfoPanel: 390x480, bottom-right");
        }

        // ============================================================
        // 16. HintText — 提示文字（横幅顶部中间）
        // 效果图中 "提方案9999推力反击" 在横幅顶部
        // ============================================================
        var hintText = gameUI.Find("HintText");
        if (hintText != null)
        {
            var rect = hintText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(500, 40);
            rect.anchoredPosition = new Vector2(0, -40);
            var tmp = hintText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 22;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ HintText: 500x40, top-center");
        }

        // ========== 确保按钮在最上层 ==========
        if (btnEnd != null) btnEnd.SetAsLastSibling();
        if (btnSettings != null) btnSettings.SetAsLastSibling();

        // 标记场景脏
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        log.Insert(0, $"=== 基于效果图精确布局 === 调整: {adjusted}\n\n");
        return log.ToString();
    }
}
