using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 精确调整战斗界面UI布局 — 基于Gemini视觉分析结果
/// 1080x1920竖屏分辨率
/// </summary>
public class BattleUILayoutAdjuster
{
    public static string Execute()
    {
        int adjusted = 0;
        var log = new System.Text.StringBuilder();

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return "ERROR: Canvas not found";
        var gameUI = canvas.transform.Find("GameUIPanel");
        if (gameUI == null) return "ERROR: GameUIPanel not found";

        // ==========================================
        // 1. TopBarBg (hint_bar_bg) — 顶部横幅
        // ==========================================
        var topBarBg = gameUI.Find("TopBar/TopBarBg");
        if (topBarBg != null)
        {
            var rect = topBarBg.GetComponent<RectTransform>();
            var img = topBarBg.GetComponent<Image>();
            // 居中顶部，sizeDelta控制大小
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(1080, 200);
            rect.anchoredPosition = new Vector2(0, 0);
            if (img != null)
            {
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ TopBarBg: 1080x200, top-center");
        }

        // ==========================================
        // 2. ProgressBarContainer — 推力进度条
        // ==========================================
        var progressBar = gameUI.Find("TopBar/TopBarBg/ProgressBarContainer");
        if (progressBar != null)
        {
            var rect = progressBar.GetComponent<RectTransform>();
            var img = progressBar.GetComponent<Image>();
            // 进度条在横幅内部偏下
            rect.anchorMin = new Vector2(0.05f, 0.05f);
            rect.anchorMax = new Vector2(0.95f, 0.38f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            if (img != null)
            {
                img.preserveAspect = false;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ ProgressBarContainer: 5%~95%, bottom of topbar");
        }

        // ==========================================
        // 3. PosIndicator 橘子图标
        // ==========================================
        var posIndicator = gameUI.Find("TopBar/TopBarBg/PosIndicator");
        if (posIndicator != null)
        {
            var orangeIcon = posIndicator.Find("OrangeIcon");
            if (orangeIcon != null)
            {
                var rect = orangeIcon.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(70, 70);
                rect.anchoredPosition = new Vector2(0, 10);
                EditorUtility.SetDirty(rect);
                adjusted++;
                log.AppendLine("✅ OrangeIcon: 70x70");
            }
        }

        // ==========================================
        // 4. TimerText + TimerBg — 倒计时
        // ==========================================
        var timerText = gameUI.Find("TopBar/TimerText");
        if (timerText != null)
        {
            var rect = timerText.GetComponent<RectTransform>();
            // 在TopBar内居中偏上
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260, 70);
            rect.anchoredPosition = new Vector2(0, 55);
            // 调整文字
            var tmp = timerText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 42;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            // 背景
            var timerBg = timerText.Find("TimerBg");
            if (timerBg != null)
            {
                var bgRect = timerBg.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = new Vector2(40, 16);
                bgRect.anchoredPosition = Vector2.zero;
                var bgImg = timerBg.GetComponent<Image>();
                if (bgImg != null) bgImg.preserveAspect = true;
                EditorUtility.SetDirty(bgRect);
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ TimerText: 260x70, center-top, font 42");
        }

        // ==========================================
        // 5. ScorePoolText + ScorePoolBg — 积分池
        // ==========================================
        var scoreText = gameUI.Find("TopBar/ScorePoolText");
        if (scoreText != null)
        {
            var rect = scoreText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(300, 55);
            rect.anchoredPosition = new Vector2(0, -15);
            var tmp = scoreText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 28;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            var scoreBg = scoreText.Find("ScorePoolBg");
            if (scoreBg != null)
            {
                var bgRect = scoreBg.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = new Vector2(30, 10);
                bgRect.anchoredPosition = Vector2.zero;
                var bgImg = scoreBg.GetComponent<Image>();
                if (bgImg != null) bgImg.preserveAspect = true;
                EditorUtility.SetDirty(bgRect);
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ ScorePoolText: 300x55, center, font 28");
        }

        // ==========================================
        // 6. LeftForceText — 左侧推力文字
        // ==========================================
        var leftForce = gameUI.Find("TopBar/LeftForceText");
        if (leftForce != null)
        {
            var rect = leftForce.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(200, 50);
            rect.anchoredPosition = new Vector2(120, 55);
            var tmp = leftForce.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 30;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = new Color(1f, 0.6f, 0.15f, 1f); // 橙色
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ LeftForceText: left, font 30");
        }

        // ==========================================
        // 7. RightForceText — 右侧推力文字
        // ==========================================
        var rightForce = gameUI.Find("TopBar/RightForceText");
        if (rightForce != null)
        {
            var rect = rightForce.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(200, 50);
            rect.anchoredPosition = new Vector2(-120, 55);
            var tmp = rightForce.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 30;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Right;
                tmp.color = new Color(0.3f, 0.85f, 0.3f, 1f); // 绿色
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ RightForceText: right, font 30");
        }

        // ==========================================
        // 8. BtnEnd — 结束按钮 (左上角)
        // ==========================================
        var btnEnd = gameUI.Find("BtnEnd");
        if (btnEnd != null)
        {
            var rect = btnEnd.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(100, 100);
            rect.anchoredPosition = new Vector2(15, -15);
            var img = btnEnd.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            // 清理子对象Text
            var oldText = btnEnd.Find("Text");
            if (oldText != null) oldText.gameObject.SetActive(false);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ BtnEnd: 100x100, top-left (15,-15)");
        }

        // ==========================================
        // 9. BtnSettings — 设置按钮 (右上角)
        // ==========================================
        var btnSettings = gameUI.Find("BtnSettings");
        if (btnSettings != null)
        {
            var rect = btnSettings.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(100, 100);
            rect.anchoredPosition = new Vector2(-15, -15);
            var img = btnSettings.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            var oldText = btnSettings.Find("Text");
            if (oldText != null) oldText.gameObject.SetActive(false);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ BtnSettings: 100x100, top-right (-15,-15)");
        }

        // ==========================================
        // 10. WinStreakLeft — 左连胜 (结束按钮下方)
        // ==========================================
        var winLeft = gameUI.Find("WinStreakLeft");
        if (winLeft != null)
        {
            var rect = winLeft.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(140, 45);
            rect.anchoredPosition = new Vector2(15, -120);
            var tmp = winLeft.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 22;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ WinStreakLeft: 140x45, below BtnEnd");
        }

        // ==========================================
        // 11. WinStreakRight — 右连胜 (设置按钮下方)
        // ==========================================
        var winRight = gameUI.Find("WinStreakRight");
        if (winRight != null)
        {
            var rect = winRight.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(140, 45);
            rect.anchoredPosition = new Vector2(-15, -120);
            var tmp = winRight.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 22;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ WinStreakRight: 140x45, below BtnSettings");
        }

        // ==========================================
        // 12. HintText — 提示文字 (最顶部)
        // ==========================================
        var hintText = gameUI.Find("HintText");
        if (hintText != null)
        {
            var rect = hintText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(600, 45);
            rect.anchoredPosition = new Vector2(0, -5);
            var tmp = hintText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 26;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ HintText: 600x45, top-center");
        }

        // ==========================================
        // 13. LeftPlayerList — 左侧阵营玩家列表
        // ==========================================
        var leftList = gameUI.Find("LeftPlayerList");
        if (leftList != null)
        {
            var rect = leftList.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.45f);
            rect.anchorMax = new Vector2(0.42f, 0.65f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var img = leftList.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = false;
                img.color = Color.white;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ LeftPlayerList: anchor 0~42%, y 45~65%");
        }

        // ==========================================
        // 14. RightPlayerList — 右侧阵营玩家列表
        // ==========================================
        var rightList = gameUI.Find("RightPlayerList");
        if (rightList != null)
        {
            var rect = rightList.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.58f, 0.45f);
            rect.anchorMax = new Vector2(1f, 0.65f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var img = rightList.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = false;
                img.color = Color.white;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ RightPlayerList: anchor 58~100%, y 45~65%");
        }

        // ==========================================
        // 15. BtnSticker — 贴纸按钮 (左下角)
        // ==========================================
        var btnSticker = gameUI.Find("BtnSticker");
        if (btnSticker != null)
        {
            var rect = btnSticker.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.sizeDelta = new Vector2(90, 90);
            rect.anchoredPosition = new Vector2(20, 20);
            var img = btnSticker.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            var oldText = btnSticker.Find("Text");
            if (oldText != null) oldText.gameObject.SetActive(false);
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ BtnSticker: 90x90, bottom-left (20,20)");
        }

        // ==========================================
        // 16. GiftInfoPanel — 礼物面板 (右下区域)
        // ==========================================
        var giftPanel = gameUI.Find("GiftInfoPanel");
        if (giftPanel != null)
        {
            var rect = giftPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.55f, 0.02f);
            rect.anchorMax = new Vector2(0.98f, 0.4f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var img = giftPanel.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ GiftInfoPanel: anchor 55~98%, y 2~40%");
        }

        // ==========================================
        // 17. TopBar 自身 — 容纳整个顶部区域
        // ==========================================
        var topBar = gameUI.Find("TopBar");
        if (topBar != null)
        {
            var rect = topBar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.85f);
            rect.anchorMax = new Vector2(1, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            EditorUtility.SetDirty(rect);
            adjusted++;
            log.AppendLine("✅ TopBar: full width, y 85~100%");
        }

        // 标记场景脏
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        log.Insert(0, $"=== UI布局调整完成 === 调整了 {adjusted} 个元素\n\n");
        return log.ToString();
    }
}
