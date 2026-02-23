using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 一键替换战斗界面(GameUIPanel)的美术资源
/// 将旧版资源替换为 Assets/Art/BattleUI/ 下的新资源
/// </summary>
public class BattleUIResourceReplacer
{
    private const string ART_PATH = "Assets/Art/BattleUI/";

    public static string Execute()
    {
        int replaced = 0;
        int created = 0;
        var log = new System.Text.StringBuilder();

        // 找到 Canvas/GameUIPanel
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { return "ERROR: Canvas not found"; }

        var gameUI = canvas.transform.Find("GameUIPanel");
        if (gameUI == null) { return "ERROR: GameUIPanel not found"; }

        // ========== 1. TopBarBg → hint_bar_bg.png ==========
        var topBarBg = gameUI.Find("TopBar/TopBarBg");
        if (topBarBg != null)
        {
            var img = topBarBg.GetComponent<Image>();
            if (img != null)
            {
                var sprite = LoadSprite("hint_bar_bg.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white; // 重置颜色，显示原始美术
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    EditorUtility.SetDirty(img);
                    replaced++;
                    log.AppendLine("✅ TopBarBg → hint_bar_bg.png");
                }
            }
        }

        // ========== 2. ProgressBarContainer → progress_bar_bg.png ==========
        var progressBar = gameUI.Find("TopBar/TopBarBg/ProgressBarContainer");
        if (progressBar != null)
        {
            var img = progressBar.GetComponent<Image>();
            if (img != null)
            {
                var sprite = LoadSprite("progress_bar_bg.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    EditorUtility.SetDirty(img);
                    replaced++;
                    log.AppendLine("✅ ProgressBarContainer → progress_bar_bg.png");
                }
            }
        }

        // ========== 3. PosIndicator → progress_bar_orange.png ==========
        var posIndicator = gameUI.Find("TopBar/TopBarBg/PosIndicator");
        if (posIndicator != null)
        {
            // PosIndicator 是 TMP 文字，需要添加 Image 子对象或直接替换
            // 先检查是否已有 Image
            var img = posIndicator.GetComponent<Image>();
            if (img == null)
            {
                // 创建一个子对象作为橘子图标
                var orangeIcon = new GameObject("OrangeIcon");
                orangeIcon.transform.SetParent(posIndicator, false);
                var rect = orangeIcon.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(60, 60);
                rect.anchoredPosition = Vector2.zero;

                var newImg = orangeIcon.AddComponent<Image>();
                var sprite = LoadSprite("progress_bar_orange.png");
                if (sprite != null)
                {
                    newImg.sprite = sprite;
                    newImg.color = Color.white;
                    newImg.preserveAspect = true;
                    newImg.raycastTarget = false;
                    EditorUtility.SetDirty(newImg);
                    created++;
                    log.AppendLine("✅ PosIndicator/OrangeIcon → progress_bar_orange.png (新建)");
                }
            }
        }

        // ========== 4. BtnEnd → btn_end_bg.png ==========
        var btnEnd = gameUI.Find("BtnEnd");
        if (btnEnd != null)
        {
            var img = btnEnd.GetComponent<Image>();
            if (img != null)
            {
                var sprite = LoadSprite("btn_end_bg.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    // 调整按钮尺寸适配新美术
                    var rect = btnEnd.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(140, 140);
                    EditorUtility.SetDirty(img);
                    EditorUtility.SetDirty(rect);
                    replaced++;
                    log.AppendLine("✅ BtnEnd → btn_end_bg.png (140x140)");
                }
            }
        }

        // ========== 5. BtnSettings → btn_settings_bg.png ==========
        var btnSettings = gameUI.Find("BtnSettings");
        if (btnSettings != null)
        {
            var img = btnSettings.GetComponent<Image>();
            if (img != null)
            {
                var sprite = LoadSprite("btn_settings_bg.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    var rect = btnSettings.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(140, 140);
                    EditorUtility.SetDirty(img);
                    EditorUtility.SetDirty(rect);
                    replaced++;
                    log.AppendLine("✅ BtnSettings → btn_settings_bg.png (140x140)");
                }
            }
        }

        // ========== 6. BtnSticker → btn_sticker.png ==========
        var btnSticker = gameUI.Find("BtnSticker");
        if (btnSticker != null)
        {
            var img = btnSticker.GetComponent<Image>();
            if (img != null)
            {
                var sprite = LoadSprite("btn_sticker.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    EditorUtility.SetDirty(img);
                    replaced++;
                    log.AppendLine("✅ BtnSticker → btn_sticker.png");
                }
            }
        }

        // ========== 7. LeftPlayerList → avatar_frame_left.png ==========
        var leftList = gameUI.Find("LeftPlayerList");
        if (leftList != null)
        {
            var img = leftList.GetComponent<Image>();
            if (img != null)
            {
                var sprite = LoadSprite("avatar_frame_left.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    EditorUtility.SetDirty(img);
                    replaced++;
                    log.AppendLine("✅ LeftPlayerList → avatar_frame_left.png");
                }
            }
        }

        // ========== 8. RightPlayerList → avatar_frame_right.png ==========
        var rightList = gameUI.Find("RightPlayerList");
        if (rightList != null)
        {
            var img = rightList.GetComponent<Image>();
            if (img != null)
            {
                var sprite = LoadSprite("avatar_frame_right.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    EditorUtility.SetDirty(img);
                    replaced++;
                    log.AppendLine("✅ RightPlayerList → avatar_frame_right.png");
                }
            }
        }

        // ========== 9. GiftInfoPanel → gift_sticker_panel.png ==========
        var giftPanel = gameUI.Find("GiftInfoPanel");
        if (giftPanel != null)
        {
            var img = giftPanel.GetComponent<Image>();
            if (img != null)
            {
                var sprite = LoadSprite("gift_sticker_panel.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    EditorUtility.SetDirty(img);
                    replaced++;
                    log.AppendLine("✅ GiftInfoPanel → gift_sticker_panel.png");
                }
            }
        }

        // ========== 10. WinStreakLeft 添加背景 ==========
        var winLeft = gameUI.Find("WinStreakLeft");
        if (winLeft != null)
        {
            // WinStreakLeft 当前是纯 TMP 文字，需要添加 Image 背景
            var img = winLeft.GetComponent<Image>();
            if (img == null)
            {
                // 创建背景子对象（放在文字下面）
                var bg = new GameObject("WinStreakLeftBg");
                bg.transform.SetParent(winLeft, false);
                bg.transform.SetAsFirstSibling(); // 确保在文字下面
                var bgRect = bg.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
                bgRect.anchoredPosition = Vector2.zero;

                var bgImg = bg.AddComponent<Image>();
                var sprite = LoadSprite("win_streak_left_bg.png");
                if (sprite != null)
                {
                    bgImg.sprite = sprite;
                    bgImg.color = Color.white;
                    bgImg.preserveAspect = true;
                    bgImg.raycastTarget = false;
                    EditorUtility.SetDirty(bgImg);
                    created++;
                    log.AppendLine("✅ WinStreakLeft/WinStreakLeftBg → win_streak_left_bg.png (新建)");
                }
            }
        }

        // ========== 11. WinStreakRight 添加背景 ==========
        var winRight = gameUI.Find("WinStreakRight");
        if (winRight != null)
        {
            var img = winRight.GetComponent<Image>();
            if (img == null)
            {
                var bg = new GameObject("WinStreakRightBg");
                bg.transform.SetParent(winRight, false);
                bg.transform.SetAsFirstSibling();
                var bgRect = bg.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
                bgRect.anchoredPosition = Vector2.zero;

                var bgImg = bg.AddComponent<Image>();
                var sprite = LoadSprite("win_streak_right_bg.png");
                if (sprite != null)
                {
                    bgImg.sprite = sprite;
                    bgImg.color = Color.white;
                    bgImg.preserveAspect = true;
                    bgImg.raycastTarget = false;
                    EditorUtility.SetDirty(bgImg);
                    created++;
                    log.AppendLine("✅ WinStreakRight/WinStreakRightBg → win_streak_right_bg.png (新建)");
                }
            }
        }

        // ========== 12. TimerText 添加背景 ==========
        var timerText = gameUI.Find("TopBar/TimerText");
        if (timerText != null)
        {
            // 检查是否已有背景
            var existingBg = timerText.Find("TimerBg");
            if (existingBg == null)
            {
                var bg = new GameObject("TimerBg");
                bg.transform.SetParent(timerText, false);
                bg.transform.SetAsFirstSibling();
                var bgRect = bg.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                // 稍微放大让背景框住文字
                bgRect.sizeDelta = new Vector2(40, 16);
                bgRect.anchoredPosition = Vector2.zero;

                var bgImg = bg.AddComponent<Image>();
                var sprite = LoadSprite("timer_bg.png");
                if (sprite != null)
                {
                    bgImg.sprite = sprite;
                    bgImg.color = Color.white;
                    bgImg.preserveAspect = true;
                    bgImg.raycastTarget = false;
                    EditorUtility.SetDirty(bgImg);
                    created++;
                    log.AppendLine("✅ TimerText/TimerBg → timer_bg.png (新建)");
                }
            }
        }

        // ========== 13. ScorePoolText 添加背景 ==========
        var scoreText = gameUI.Find("TopBar/ScorePoolText");
        if (scoreText != null)
        {
            var existingBg = scoreText.Find("ScorePoolBg");
            if (existingBg == null)
            {
                var bg = new GameObject("ScorePoolBg");
                bg.transform.SetParent(scoreText, false);
                bg.transform.SetAsFirstSibling();
                var bgRect = bg.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = new Vector2(30, 10);
                bgRect.anchoredPosition = Vector2.zero;

                var bgImg = bg.AddComponent<Image>();
                var sprite = LoadSprite("score_pool_bg.png");
                if (sprite != null)
                {
                    bgImg.sprite = sprite;
                    bgImg.color = Color.white;
                    bgImg.preserveAspect = true;
                    bgImg.raycastTarget = false;
                    EditorUtility.SetDirty(bgImg);
                    created++;
                    log.AppendLine("✅ ScorePoolText/ScorePoolBg → score_pool_bg.png (新建)");
                }
            }
        }

        // ========== 14. PlayerRow 背景 (player_rank_item_bg.png) ==========
        // 左右各3行 PlayerRow
        foreach (var side in new[] { "LeftPlayerList", "RightPlayerList" })
        {
            var list = gameUI.Find(side);
            if (list == null) continue;

            for (int i = 0; i < 3; i++)
            {
                var row = list.Find($"PlayerRow_{i}");
                if (row == null) continue;

                var img = row.GetComponent<Image>();
                if (img == null)
                {
                    img = row.gameObject.AddComponent<Image>();
                    created++;
                }

                var sprite = LoadSprite("player_rank_item_bg.png");
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = new Color(1f, 1f, 1f, 0.9f);
                    img.type = Image.Type.Simple;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    EditorUtility.SetDirty(img);
                    replaced++;
                    log.AppendLine($"✅ {side}/PlayerRow_{i} → player_rank_item_bg.png");
                }
            }
        }

        // 标记场景为脏
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        log.Insert(0, $"=== 战斗界面资源替换完成 ===\n替换: {replaced} | 新建: {created}\n\n");
        return log.ToString();
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = ART_PATH + fileName;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning($"[BattleUIReplacer] 找不到 Sprite: {path}，检查 TextureImporter 是否设为 Sprite");

            // 尝试设置 TextureImporter
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return sprite;
    }
}
