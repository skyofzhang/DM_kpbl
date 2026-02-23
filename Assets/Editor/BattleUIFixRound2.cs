using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 第二轮修复：
/// 1. TimerBg/ScorePoolBg 从子对象改为同级对象（解决被文字遮挡）
/// 2. WinStreakBg 同理
/// 3. 进度条设置 Sprite Border + Sliced
/// 4. 确保按钮在最上层
/// </summary>
public class BattleUIFixRound2
{
    private const string ART_PATH = "Assets/Art/BattleUI/";

    public static string Execute()
    {
        int fixed_count = 0;
        var log = new System.Text.StringBuilder();

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return "ERROR: Canvas not found";
        var gameUI = canvas.transform.Find("GameUIPanel");
        if (gameUI == null) return "ERROR: GameUIPanel not found";
        var topBar = gameUI.Find("TopBar");

        // ========== 1. 修复进度条 Sliced 模式 ==========
        // 先设置 Sprite 的 Border（通过 TextureImporter）
        SetSpriteBorder("progress_bar_bg.png", 80, 20, 80, 20);
        var progressBar = gameUI.Find("TopBar/TopBarBg/ProgressBarContainer");
        if (progressBar != null)
        {
            var img = progressBar.GetComponent<Image>();
            if (img != null)
            {
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
                img.fillCenter = true;
                EditorUtility.SetDirty(img);
                fixed_count++;
                log.AppendLine("✅ ProgressBarContainer → Sliced mode");
            }
        }

        // ========== 2. TimerBg：从TimerText子对象移到TopBar下同级 ==========
        var timerText = topBar?.Find("TimerText");
        if (timerText != null)
        {
            var oldBg = timerText.Find("TimerBg");
            if (oldBg != null)
            {
                // 删除旧的子对象
                Object.DestroyImmediate(oldBg.gameObject);
            }

            // 在TopBar下创建同级背景，放在TimerText之前
            var timerBg = CreateBgObject("TimerBg", topBar, "timer_bg.png",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(280, 70),
                new Vector2(0, 55)); // 与 TimerText 相同位置
            if (timerBg != null)
            {
                // 确保在 TimerText 之前渲染（索引更小）
                int timerIdx = timerText.GetSiblingIndex();
                timerBg.transform.SetSiblingIndex(timerIdx);
                fixed_count++;
                log.AppendLine("✅ TimerBg → 独立同级对象，在TimerText之下");
            }
        }

        // ========== 3. ScorePoolBg：同理 ==========
        var scoreText = topBar?.Find("ScorePoolText");
        if (scoreText != null)
        {
            var oldBg = scoreText.Find("ScorePoolBg");
            if (oldBg != null)
            {
                Object.DestroyImmediate(oldBg.gameObject);
            }

            var scoreBg = CreateBgObject("ScorePoolBg", topBar, "score_pool_bg.png",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(320, 55),
                new Vector2(0, -15)); // 与 ScorePoolText 相同位置
            if (scoreBg != null)
            {
                int scoreIdx = scoreText.GetSiblingIndex();
                scoreBg.transform.SetSiblingIndex(scoreIdx);
                fixed_count++;
                log.AppendLine("✅ ScorePoolBg → 独立同级对象，在ScorePoolText之下");
            }
        }

        // ========== 4. WinStreakLeftBg：从子对象改为同级 ==========
        var winLeft = gameUI.Find("WinStreakLeft");
        if (winLeft != null)
        {
            var oldBg = winLeft.Find("WinStreakLeftBg");
            if (oldBg != null) Object.DestroyImmediate(oldBg.gameObject);

            var winLeftBg = CreateBgObject("WinStreakLeftBg", gameUI, "win_streak_left_bg.png",
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(140, 45),
                new Vector2(15, -120)); // 与 WinStreakLeft 相同位置
            if (winLeftBg != null)
            {
                int winIdx = winLeft.GetSiblingIndex();
                winLeftBg.transform.SetSiblingIndex(winIdx);
                fixed_count++;
                log.AppendLine("✅ WinStreakLeftBg → 独立同级对象");
            }
        }

        // ========== 5. WinStreakRightBg：同理 ==========
        var winRight = gameUI.Find("WinStreakRight");
        if (winRight != null)
        {
            var oldBg = winRight.Find("WinStreakRightBg");
            if (oldBg != null) Object.DestroyImmediate(oldBg.gameObject);

            var winRightBg = CreateBgObject("WinStreakRightBg", gameUI, "win_streak_right_bg.png",
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(140, 45),
                new Vector2(-15, -120)); // 与 WinStreakRight 相同位置
            if (winRightBg != null)
            {
                int winIdx = winRight.GetSiblingIndex();
                winRightBg.transform.SetSiblingIndex(winIdx);
                fixed_count++;
                log.AppendLine("✅ WinStreakRightBg → 独立同级对象");
            }
        }

        // ========== 6. 确保 BtnEnd/BtnSettings 在最上层 ==========
        var btnEnd = gameUI.Find("BtnEnd");
        if (btnEnd != null)
        {
            btnEnd.SetAsLastSibling();
            fixed_count++;
            log.AppendLine("✅ BtnEnd → 设为最上层");
        }
        var btnSettings = gameUI.Find("BtnSettings");
        if (btnSettings != null)
        {
            btnSettings.SetAsLastSibling();
            fixed_count++;
            log.AppendLine("✅ BtnSettings → 设为最上层");
        }

        // ========== 7. 设置其他需要Sliced的Sprite Border ==========
        SetSpriteBorder("timer_bg.png", 30, 15, 30, 15);
        SetSpriteBorder("score_pool_bg.png", 30, 15, 30, 15);
        SetSpriteBorder("win_streak_left_bg.png", 20, 10, 20, 10);
        SetSpriteBorder("win_streak_right_bg.png", 20, 10, 20, 10);
        SetSpriteBorder("player_rank_item_bg.png", 25, 10, 25, 10);
        log.AppendLine("✅ 已设置所有需要的 Sprite Border");

        // 标记场景脏
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        log.Insert(0, $"=== Round2 修复完成 === 修复: {fixed_count}\n\n");
        return log.ToString();
    }

    private static GameObject CreateBgObject(string name, Transform parent, string spriteName,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        var img = go.AddComponent<Image>();
        var sprite = LoadSprite(spriteName);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            EditorUtility.SetDirty(img);
            return go;
        }
        return null;
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = ART_PATH + fileName;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
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

    private static void SetSpriteBorder(string fileName, int left, int bottom, int right, int top)
    {
        string path = ART_PATH + fileName;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = new Vector4(left, bottom, right, top);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
