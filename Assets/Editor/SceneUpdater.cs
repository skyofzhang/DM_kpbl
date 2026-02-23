#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CapybaraDuel.Core;
using CapybaraDuel.Entity;
using CapybaraDuel.Systems;
using CapybaraDuel.VFX;
using CapybaraDuel.UI;
using TMPro;

namespace CapybaraDuel.Editor
{
    /// <summary>
    /// 场景增量更新器 - 安全地更新代码组件和引用，不破坏手工美术调整
    ///
    /// 【核心原则】
    /// 1. 永远不创建新场景 — 在当前打开的场景上操作
    /// 2. 不修改任何 Transform（位置/旋转/缩放）
    /// 3. 不修改任何视觉属性（颜色/字体大小/sprite/材质手动调整）
    /// 4. 只做：确保组件存在 + 修复引用断线 + 添加新组件
    ///
    /// 【使用场景】
    /// - 代码里新增了组件/字段 → Update Scene 确保场景里的组件和引用是最新的
    /// - 某个引用断了 → Update Scene 自动修复
    /// - 不影响你在 Unity 编辑器里做的任何美术调整
    /// </summary>
    public static class SceneUpdater
    {
        [MenuItem("CapybaraDuel/Update Scene (Safe)", false, 2)]
        public static void UpdateScene()
        {
            // 安全检查：确保当前场景已保存
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene.isDirty)
            {
                if (!EditorUtility.DisplayDialog("场景有未保存的修改",
                    "当前场景有未保存的修改。\n\n建议先保存场景再进行更新。\n\n是否继续？",
                    "继续更新", "取消"))
                {
                    return;
                }
            }

            int updated = 0;
            int added = 0;
            int wired = 0;

            // ==================== 1. 确保 3D 场景对象的组件 ====================
            UpdateSceneObjects(ref updated, ref added, ref wired);

            // ==================== 2. 确保 Manager 组件和引用 ====================
            UpdateManagers(ref updated, ref added, ref wired);

            // ==================== 3. 确保 UI 组件和引用 ====================
            UpdateUIComponents(ref updated, ref added, ref wired);

            // ==================== 4. 确保 UIManager 引用 ====================
            UpdateUIManager(ref wired);

            // 标记场景已修改
            EditorSceneManager.MarkSceneDirty(currentScene);

            Debug.Log($"[SceneUpdater] Done! Components added: {added}, References wired: {wired}, Checked: {updated}");
            EditorUtility.DisplayDialog("Update Scene 完成",
                $"安全增量更新完成！\n\n" +
                $"• 检查组件: {updated}\n" +
                $"• 新增组件: {added}\n" +
                $"• 修复引用: {wired}\n\n" +
                "你的手工美术调整完全保留。",
                "OK");
        }

        // ==================== 3D 场景对象 ====================
        static void UpdateSceneObjects(ref int updated, ref int added, ref int wired)
        {
            // 查找橘子
            var orange = FindInScene<OrangeController>();
            if (orange != null)
            {
                updated++;
                // 确保有 MagicOrangeEffect
                if (orange.GetComponent<MagicOrangeEffect>() == null)
                {
                    orange.gameObject.AddComponent<MagicOrangeEffect>();
                    added++;
                    Debug.Log("[SceneUpdater] Added MagicOrangeEffect to Orange");
                }

                // 确保有 OrangeDustTrail（移动方向烟尘）
                if (orange.GetComponent<OrangeDustTrail>() == null)
                {
                    orange.gameObject.AddComponent<OrangeDustTrail>();
                    added++;
                    Debug.Log("[SceneUpdater] Added OrangeDustTrail to Orange");
                }

                // 确保有 OrangeSpeedHUD（头顶速度+方向显示）
                var speedHud = orange.GetComponent<CapybaraDuel.VFX.OrangeSpeedHUD>();
                if (speedHud == null)
                {
                    speedHud = orange.gameObject.AddComponent<CapybaraDuel.VFX.OrangeSpeedHUD>();
                    added++;
                    Debug.Log("[SceneUpdater] Added OrangeSpeedHUD to Orange");
                }
                // 确保HUD场景对象存在并wire引用（不覆盖用户手动调整的大小位置）
                if (speedHud.hudRoot == null || speedHud.mainText == null)
                {
                    SceneGenerator.CreateOrUpdateSpeedHUD(
                        orange.transform.root, orange.gameObject, speedHud);
                    wired++;
                    Debug.Log("[SceneUpdater] Wired OrangeSpeedHUD scene objects");
                }

                // 确保材质是魔法Shader（但不修改材质参数）
                var renderer = orange.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    var magicShader = Shader.Find("CapybaraDuel/MagicOrange");
                    if (magicShader != null && renderer.sharedMaterial.shader != magicShader)
                    {
                        // 加载 Mat_Orange.mat（如果存在魔法材质就用，否则不动）
                        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Orange.mat");
                        if (mat != null && mat.shader == magicShader)
                        {
                            var renderers = orange.GetComponentsInChildren<Renderer>();
                            foreach (var r in renderers)
                            {
                                var mats = r.sharedMaterials;
                                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                                r.sharedMaterials = mats;
                            }
                            Debug.Log("[SceneUpdater] Applied MagicOrange material to Orange");
                        }
                    }
                }
            }

            // 查找相机
            var cam = Camera.main;
            if (cam != null)
            {
                updated++;
                if (cam.GetComponent<OrangeFollowCamera>() == null)
                {
                    cam.gameObject.AddComponent<OrangeFollowCamera>();
                    added++;
                    Debug.Log("[SceneUpdater] Added OrangeFollowCamera to Main Camera");
                }

                // 确保 CameraShake 存在（在相机子节点）
                var shake = cam.GetComponentInChildren<CameraShake>();
                if (shake == null)
                {
                    var shakeGo = new GameObject("CameraShake");
                    shakeGo.transform.SetParent(cam.transform);
                    shakeGo.AddComponent<CameraShake>();
                    added++;
                    Debug.Log("[SceneUpdater] Added CameraShake under Main Camera");
                }

                // 修复 OrangeFollowCamera target 引用
                var followCam = cam.GetComponent<OrangeFollowCamera>();
                if (followCam != null && followCam.target == null && orange != null)
                {
                    followCam.target = orange.transform;
                    wired++;
                    Debug.Log("[SceneUpdater] Wired OrangeFollowCamera.target → Orange");
                }
            }

            // EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                added++;
                Debug.Log("[SceneUpdater] Added EventSystem");
            }
            updated++;
        }

        // ==================== Manager 组件 ====================
        static void UpdateManagers(ref int updated, ref int added, ref int wired)
        {
            // 确保所有 Manager 组件存在
            var gm = EnsureComponent<GameManager>("[Managers]", "GameManager", ref updated, ref added);
            var net = EnsureComponent<NetworkManager>("[Managers]", "NetworkManager", ref updated, ref added);
            var spawner = EnsureComponent<CapybaraSpawner>("[Managers]", "CapybaraSpawner", ref updated, ref added);
            var fs = EnsureComponent<ForceSystem>("[Managers]", "ForceSystem", ref updated, ref added);
            var cs = EnsureComponent<CampSystem>("[Managers]", "CampSystem", ref updated, ref added);
            var rs = EnsureComponent<RankingSystem>("[Managers]", "RankingSystem", ref updated, ref added);
            var gh = EnsureComponent<GiftHandler>("[Managers]", "GiftHandler", ref updated, ref added);
            EnsureComponent<BarrageSimulator>("[Managers]", "BarrageSimulator", ref updated, ref added);
            EnsureComponent<VFXSpawner>("[Managers]", "VFXSpawner", ref updated, ref added);
            var footDust = EnsureComponent<FootDustManager>("[Managers]", "FootDustManager", ref updated, ref added);

            // === Wire粒子材质（Build中Shader.Find不可用，必须预设材质引用）===
            var dustMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_ParticleDust.mat");
            if (dustMat != null)
            {
                if (footDust != null && footDust.particleMaterial == null)
                {
                    footDust.particleMaterial = dustMat;
                    EditorUtility.SetDirty(footDust);
                    wired++;
                    Debug.Log("[SceneUpdater] Wired FootDustManager.particleMaterial → Mat_ParticleDust");
                }

                // OrangeDustTrail 在橘子身上
                var orangeCtrl = FindInScene<OrangeController>();
                if (orangeCtrl != null)
                {
                    var dustTrail = orangeCtrl.GetComponent<OrangeDustTrail>();
                    if (dustTrail != null && dustTrail.particleMaterial == null)
                    {
                        dustTrail.particleMaterial = dustMat;
                        EditorUtility.SetDirty(dustTrail);
                        wired++;
                        Debug.Log("[SceneUpdater] Wired OrangeDustTrail.particleMaterial → Mat_ParticleDust");
                    }
                }
            }

            // AudioManager（特殊：需要 AudioSource 子组件）
            var audioMgr = EnsureComponent<AudioManager>("[Managers]", "AudioManager", ref updated, ref added);
            if (audioMgr != null)
            {
                // 确保 BGM AudioSource
                if (audioMgr.bgmSource == null)
                {
                    var bgmSrc = audioMgr.GetComponent<AudioSource>();
                    if (bgmSrc == null)
                    {
                        bgmSrc = audioMgr.gameObject.AddComponent<AudioSource>();
                        bgmSrc.playOnAwake = false;
                        bgmSrc.loop = true;
                        bgmSrc.volume = 0.5f;
                    }
                    audioMgr.bgmSource = bgmSrc;
                    wired++;
                }

                // 确保 SFX AudioSource
                if (audioMgr.sfxSource == null)
                {
                    var sfxTrans = audioMgr.transform.Find("SFX");
                    AudioSource sfxSrc;
                    if (sfxTrans != null)
                    {
                        sfxSrc = sfxTrans.GetComponent<AudioSource>();
                        if (sfxSrc == null) sfxSrc = sfxTrans.gameObject.AddComponent<AudioSource>();
                    }
                    else
                    {
                        var sfxGo = new GameObject("SFX");
                        sfxGo.transform.SetParent(audioMgr.transform);
                        sfxSrc = sfxGo.AddComponent<AudioSource>();
                        sfxSrc.playOnAwake = false;
                        sfxSrc.loop = false;
                    }
                    audioMgr.sfxSource = sfxSrc;
                    wired++;
                }
            }

            // === 修复 GameManager 引用 ===
            if (gm != null)
            {
                if (gm.forceSystem == null && fs != null) { gm.forceSystem = fs; wired++; }
                if (gm.campSystem == null && cs != null) { gm.campSystem = cs; wired++; }
                if (gm.rankingSystem == null && rs != null) { gm.rankingSystem = rs; wired++; }
                if (gm.giftHandler == null && gh != null) { gm.giftHandler = gh; wired++; }
                if (gm.spawner == null && spawner != null) { gm.spawner = spawner; wired++; }

                var orange = FindInScene<OrangeController>();
                if (gm.orangeController == null && orange != null) { gm.orangeController = orange; wired++; }

                EditorUtility.SetDirty(gm);
            }

            // === 修复 CapybaraSpawner 引用 ===
            if (spawner != null)
            {
                if (spawner.orangeTarget == null)
                {
                    var orange = FindInScene<OrangeController>();
                    if (orange != null) { spawner.orangeTarget = orange.transform; wired++; }
                }
                if (spawner.leftSpawnPoint == null)
                {
                    var lsp = GameObject.Find("LeftSpawnPoint");
                    if (lsp != null) { spawner.leftSpawnPoint = lsp.transform; wired++; }
                }
                if (spawner.rightSpawnPoint == null)
                {
                    var rsp = GameObject.Find("RightSpawnPoint");
                    if (rsp != null) { spawner.rightSpawnPoint = rsp.transform; wired++; }
                }
                {
                    // 修复 capybaraPrefab：必须是带 Capybara 组件的正确 prefab
                    // 常见问题：引用到 FBX 导出的裸 prefab（如 Pushing.prefab），没有脚本和材质
                    bool needsFix = (spawner.capybaraPrefab == null);
                    if (!needsFix && spawner.capybaraPrefab.GetComponent<Capybara>() == null)
                    {
                        Debug.LogWarning($"[SceneUpdater] capybaraPrefab 引用了错误的 prefab: {spawner.capybaraPrefab.name}，正在修复...");
                        needsFix = true;
                    }
                    if (needsFix)
                    {
                        // 优先用 KpblUnit（美术输出的卡皮巴拉模型）
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/KpblUnit.prefab");
                        if (prefab == null)
                            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/201_SheepUnit.prefab");
                        if (prefab != null)
                        {
                            spawner.capybaraPrefab = prefab;
                            wired++;
                            Debug.Log($"[SceneUpdater] capybaraPrefab 已修复为: {prefab.name}");
                        }
                    }
                }
                EditorUtility.SetDirty(spawner);
            }
        }

        // ==================== UI 组件 ====================
        static void UpdateUIComponents(ref int updated, ref int added, ref int wired)
        {
            // 查找 Canvas
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[SceneUpdater] Canvas not found! Cannot update UI components.");
                return;
            }

            // --- GameUI Panel ---
            var gameUI = FindChildRecursive(canvas.transform, "GameUIPanel");
            if (gameUI != null)
            {
                updated++;

                // TopBarUI
                var topBarGO = gameUI.Find("TopBar");
                if (topBarGO != null)
                {
                    var topBarUI = EnsureComponentOnExisting<TopBarUI>(topBarGO.gameObject, ref updated, ref added);
                    if (topBarUI != null)
                    {
                        WireIfNull(ref topBarUI.leftForceText, topBarGO, "LeftForceText", ref wired);
                        WireIfNull(ref topBarUI.rightForceText, topBarGO, "RightForceText", ref wired);
                        WireIfNull(ref topBarUI.timerText, topBarGO, "TimerText", ref wired);
                        WireIfNull(ref topBarUI.scorePoolText, topBarGO, "ScorePoolText", ref wired);

                        var topBarBg = topBarGO.Find("TopBarBg");
                        if (topBarBg != null)
                        {
                            WireImageIfNull(ref topBarUI.progressBarLeft, topBarBg, "ProgressBarLeft", ref wired);
                            WireImageIfNull(ref topBarUI.progressBarRight, topBarBg, "ProgressBarRight", ref wired);
                            WireIfNull(ref topBarUI.leftEndMarker, topBarBg, "LeftEndMarker", ref wired);
                            WireIfNull(ref topBarUI.rightEndMarker, topBarBg, "RightEndMarker", ref wired);
                            WireIfNull(ref topBarUI.centerMarker, topBarBg, "CenterMarker", ref wired);
                            WireIfNull(ref topBarUI.posIndicatorText, topBarBg, "PosIndicator", ref wired);
                        }
                        EditorUtility.SetDirty(topBarUI);
                    }
                }

                // PlayerListUI
                var playerListUI = gameUI.GetComponentInChildren<PlayerListUI>();
                if (playerListUI == null)
                {
                    // 查找已有的 PlayerListUI 对象
                    var plGo = gameUI.Find("PlayerListUI");
                    if (plGo != null)
                    {
                        playerListUI = plGo.GetComponent<PlayerListUI>();
                        if (playerListUI == null)
                        {
                            playerListUI = plGo.gameObject.AddComponent<PlayerListUI>();
                            added++;
                        }
                    }
                }
                if (playerListUI != null)
                {
                    if (playerListUI.leftListContainer == null)
                    {
                        playerListUI.leftListContainer = gameUI.Find("LeftPlayerList");
                        if (playerListUI.leftListContainer != null) wired++;
                    }
                    if (playerListUI.rightListContainer == null)
                    {
                        playerListUI.rightListContainer = gameUI.Find("RightPlayerList");
                        if (playerListUI.rightListContainer != null) wired++;
                    }
                    EditorUtility.SetDirty(playerListUI);
                }

                // GiftNotificationUI
                var giftNotifGO = gameUI.Find("GiftNotification");
                if (giftNotifGO != null)
                {
                    var gn = EnsureComponentOnExisting<GiftNotificationUI>(giftNotifGO.gameObject, ref updated, ref added);
                    if (gn != null && gn.container == null)
                    {
                        gn.container = giftNotifGO;
                        wired++;
                        EditorUtility.SetDirty(gn);
                    }
                }

                // GiftAnimationUI
                var giftAnimGO = gameUI.Find("GiftAnimation");
                if (giftAnimGO != null)
                {
                    var ga = EnsureComponentOnExisting<GiftAnimationUI>(giftAnimGO.gameObject, ref updated, ref added);
                    if (ga != null)
                    {
                        if (ga.animationContainer == null)
                        {
                            ga.animationContainer = giftAnimGO.GetComponent<RectTransform>();
                            wired++;
                        }

                        // Auto-wire VideoClips from Assets/Art/GiftGifs/
                        var so = new UnityEditor.SerializedObject(ga);
                        var clipsProp = so.FindProperty("tierVideoClips");
                        if (clipsProp != null)
                        {
                            if (clipsProp.arraySize < 6)
                                clipsProp.arraySize = 6;
                            bool anyWired = false;
                            for (int t = 0; t < 6; t++)
                            {
                                var elem = clipsProp.GetArrayElementAtIndex(t);
                                if (elem.objectReferenceValue == null)
                                {
                                    string path = $"Assets/Art/GiftGifs/tier{t + 1}-sp.webm";
                                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(path);
                                    if (clip != null)
                                    {
                                        elem.objectReferenceValue = clip;
                                        anyWired = true;
                                    }
                                }
                            }
                            if (anyWired)
                            {
                                so.ApplyModifiedProperties();
                                wired++;
                            }
                        }

                        EditorUtility.SetDirty(ga);
                    }
                }

                // PlayerJoinNotificationUI
                var joinNotifGO = gameUI.Find("JoinNotification");
                if (joinNotifGO != null)
                {
                    var jn = EnsureComponentOnExisting<PlayerJoinNotificationUI>(joinNotifGO.gameObject, ref updated, ref added);
                    if (jn != null && jn.container == null)
                    {
                        jn.container = joinNotifGO;
                        wired++;
                        EditorUtility.SetDirty(jn);
                    }
                }

                // VIPAnnouncementUI
                var vipGO = gameUI.Find("VIPAnnouncement");
                if (vipGO != null)
                {
                    var vip = EnsureComponentOnExisting<VIPAnnouncementUI>(vipGO.gameObject, ref updated, ref added);
                    if (vip != null)
                    {
                        if (vip.announcementText == null)
                        {
                            vip.announcementText = vipGO.Find("VIPText")?.GetComponent<TextMeshProUGUI>();
                            if (vip.announcementText != null) wired++;
                        }
                        if (vip.backgroundOverlay == null)
                        {
                            vip.backgroundOverlay = vipGO.GetComponent<Image>();
                            if (vip.backgroundOverlay != null) wired++;
                        }
                        if (vip.canvasGroup == null)
                        {
                            vip.canvasGroup = vipGO.GetComponent<CanvasGroup>();
                            if (vip.canvasGroup != null) wired++;
                        }
                        EditorUtility.SetDirty(vip);
                    }
                }
            }

            // --- MainMenuUI ---
            var mainMenu = FindChildRecursive(canvas.transform, "MainMenuPanel");
            if (mainMenu != null)
            {
                var mainMenuUI = EnsureComponentOnExisting<MainMenuUI>(mainMenu.gameObject, ref updated, ref added);
                if (mainMenuUI != null)
                {
                    var btnGroup = mainMenu.Find("ButtonGroup");
                    if (btnGroup != null)
                    {
                        WireButtonIfNull(ref mainMenuUI.btnStartGame, btnGroup, "BtnStartGame", ref wired);
                        WireButtonIfNull(ref mainMenuUI.btnLeaderboard, btnGroup, "BtnLeaderboard", ref wired);
                        WireButtonIfNull(ref mainMenuUI.btnGiftDesc, btnGroup, "BtnGiftDesc", ref wired);
                        WireButtonIfNull(ref mainMenuUI.btnRuleDesc, btnGroup, "BtnRuleDesc", ref wired);
                        WireButtonIfNull(ref mainMenuUI.btnStickerSettings, btnGroup, "BtnStickerSettings", ref wired);
                    }
                    EditorUtility.SetDirty(mainMenuUI);
                }
            }

            // --- SettlementUI ---
            var settlement = FindChildRecursive(canvas.transform, "SettlementPanel");
            if (settlement != null)
            {
                var sui = EnsureComponentOnExisting<SettlementUI>(settlement.gameObject, ref updated, ref added);
                if (sui != null)
                {
                    // Winner Text
                    if (sui.winnerText == null)
                    {
                        sui.winnerText = settlement.Find("WinnerText")?.GetComponent<TextMeshProUGUI>();
                        if (sui.winnerText != null) wired++;
                    }
                    // Restart Button
                    if (sui.restartButton == null)
                    {
                        sui.restartButton = settlement.Find("BtnRestart")?.GetComponent<Button>();
                        if (sui.restartButton != null) wired++;
                    }

                    // MVP
                    var mvpArea = settlement.Find("MVPArea");
                    if (mvpArea != null)
                    {
                        if (sui.mvpNameText == null)
                        {
                            var nameBar = mvpArea.Find("MVPNameBar");
                            sui.mvpNameText = nameBar?.Find("MVPName")?.GetComponent<TextMeshProUGUI>();
                            if (sui.mvpNameText != null) wired++;
                        }
                        if (sui.mvpLabelText == null)
                        {
                            sui.mvpLabelText = mvpArea.Find("MVPLabel")?.GetComponent<TextMeshProUGUI>();
                            if (sui.mvpLabelText != null) wired++;
                        }
                        if (sui.mvpContributionText == null)
                        {
                            sui.mvpContributionText = mvpArea.Find("MVPContribution")?.GetComponent<TextMeshProUGUI>();
                            if (sui.mvpContributionText != null) wired++;
                        }
                        if (sui.mvpAvatarImage == null)
                        {
                            var avatarFrame = mvpArea.Find("MVPAvatarFrame");
                            sui.mvpAvatarImage = avatarFrame?.Find("MVPAvatar")?.GetComponent<Image>();
                            if (sui.mvpAvatarImage != null) wired++;
                        }
                    }

                    // Left Rank Column (10 rows)
                    WireRankColumn(settlement, "LeftRankColumn", "LeftRank",
                        ref sui.leftRankTitle, ref sui.leftRankNames, ref sui.leftRankValues, ref wired);

                    // Right Rank Column (10 rows)
                    WireRankColumn(settlement, "RightRankColumn", "RightRank",
                        ref sui.rightRankTitle, ref sui.rightRankNames, ref sui.rightRankValues, ref wired);

                    // Score Distribution (6 rows)
                    var scoreDistArea = settlement.Find("ScoreDistArea");
                    if (scoreDistArea != null)
                    {
                        if (sui.scorePoolLabel == null)
                        {
                            sui.scorePoolLabel = scoreDistArea.Find("ScorePoolLabel")?.GetComponent<TextMeshProUGUI>();
                            if (sui.scorePoolLabel != null) wired++;
                        }
                        if (sui.scoreDistNames == null || sui.scoreDistNames.Length == 0)
                        {
                            sui.scoreDistNames = new TextMeshProUGUI[6];
                            sui.scoreDistValues = new TextMeshProUGUI[6];
                            for (int i = 0; i < 6; i++)
                            {
                                sui.scoreDistNames[i] = scoreDistArea.Find($"ScoreDistName_{i}")?.GetComponent<TextMeshProUGUI>();
                                sui.scoreDistValues[i] = scoreDistArea.Find($"ScoreDistVal_{i}")?.GetComponent<TextMeshProUGUI>();
                            }
                            wired++;
                        }
                    }

                    EditorUtility.SetDirty(sui);
                }
            }

            // --- RankingPanel ---
            var rankingPanel = FindChildRecursive(canvas.transform, "RankingPanel");
            if (rankingPanel != null)
            {
                var rpUI = EnsureComponentOnExisting<RankingPanelUI>(rankingPanel.gameObject, ref updated, ref added);
                if (rpUI != null)
                {
                    // Wire Top3 names（检查null元素）
                    bool needsNameWire = rpUI.top3Names == null || rpUI.top3Names.Length < 3;
                    if (!needsNameWire && rpUI.top3Names != null)
                    {
                        for (int i = 0; i < rpUI.top3Names.Length; i++)
                            if (rpUI.top3Names[i] == null) { needsNameWire = true; break; }
                    }
                    if (needsNameWire)
                    {
                        rpUI.top3Names = new TextMeshProUGUI[3];
                        for (int i = 0; i < 3; i++)
                            rpUI.top3Names[i] = rankingPanel.Find($"Top3Name_{i}")?.GetComponent<TextMeshProUGUI>();
                        wired++;
                    }
                    // Wire Top3 scores（独立检查，防止被遗漏）
                    // 检查数组为null、长度不足、或存在null元素（引用丢失）
                    bool needsScoreWire = rpUI.top3Scores == null || rpUI.top3Scores.Length < 3;
                    if (!needsScoreWire && rpUI.top3Scores != null)
                    {
                        for (int i = 0; i < rpUI.top3Scores.Length; i++)
                            if (rpUI.top3Scores[i] == null) { needsScoreWire = true; break; }
                    }
                    if (needsScoreWire)
                    {
                        rpUI.top3Scores = new TextMeshProUGUI[3];
                        for (int i = 0; i < 3; i++)
                            rpUI.top3Scores[i] = rankingPanel.Find($"Top3Score_{i}")?.GetComponent<TextMeshProUGUI>();
                        wired++;
                    }

                    // Wire Top3 avatar + avatar frames
                    if (rpUI.top3Avatars == null || rpUI.top3Avatars.Length == 0)
                    {
                        rpUI.top3Avatars = new Image[3];
                        rpUI.top3AvatarFrames = new Image[3];
                        for (int i = 0; i < 3; i++)
                        {
                            var frameT = rankingPanel.Find($"Top3AvatarFrame_{i}");
                            rpUI.top3AvatarFrames[i] = frameT?.GetComponent<Image>();
                            rpUI.top3Avatars[i] = frameT?.Find($"Top3Avatar_{i}")?.GetComponent<Image>();
                        }
                        wired++;
                    }

                    // Wire tabs
                    if (rpUI.tabButtons == null || rpUI.tabButtons.Length == 0)
                    {
                        rpUI.tabButtons = new Button[4];
                        rpUI.tabImages = new Image[4];
                        for (int i = 0; i < 4; i++)
                        {
                            var tabGo = rankingPanel.Find($"Tab_{i}");
                            if (tabGo != null)
                            {
                                rpUI.tabButtons[i] = tabGo.GetComponent<Button>();
                                rpUI.tabImages[i] = tabGo.GetComponent<Image>();
                            }
                        }
                        wired++;
                    }

                    // Wire rank list (4-10)
                    if (rpUI.rankNames == null || rpUI.rankNames.Length == 0)
                    {
                        var listArea = rankingPanel.Find("ListArea");
                        if (listArea != null)
                        {
                            rpUI.rankNumbers = new TextMeshProUGUI[7];
                            rpUI.rankNames = new TextMeshProUGUI[7];
                            rpUI.rankScores = new TextMeshProUGUI[7];
                            for (int i = 0; i < 7; i++)
                            {
                                var item = listArea.Find($"RankItem_{i}");
                                if (item != null)
                                {
                                    rpUI.rankNumbers[i] = item.Find("RankNum")?.GetComponent<TextMeshProUGUI>();
                                    rpUI.rankNames[i] = item.Find("Name")?.GetComponent<TextMeshProUGUI>();
                                    rpUI.rankScores[i] = item.Find("Score")?.GetComponent<TextMeshProUGUI>();
                                }
                            }
                            wired++;
                        }
                    }

                    // Wire close button
                    WireButtonIfNull(ref rpUI.btnClose, rankingPanel, "BtnClose", ref wired);

                    if (rpUI.resetTimeText == null)
                    {
                        var resetTimeT = rankingPanel.Find("ResetTimeText");
                        if (resetTimeT != null)
                        {
                            rpUI.resetTimeText = resetTimeT.GetComponent<TextMeshProUGUI>();
                            if (rpUI.resetTimeText != null) wired++;
                        }
                    }

                    EditorUtility.SetDirty(rpUI);
                }
            }

            // --- BottomBar ---
            var bottomBar = FindChildRecursive(canvas.transform, "BottomBar");
            if (bottomBar != null)
            {
                // 确保有 CanvasGroup
                if (bottomBar.GetComponent<CanvasGroup>() == null)
                {
                    bottomBar.gameObject.AddComponent<CanvasGroup>();
                    added++;
                }

                var controlUI = EnsureComponentOnExisting<GameControlUI>(bottomBar.gameObject, ref updated, ref added);
                if (controlUI != null)
                {
                    WireButtonIfNull(ref controlUI.gmLoginButton, bottomBar, "BtnGMLogin", ref wired);
                    WireButtonIfNull(ref controlUI.simulateButton, bottomBar, "BtnSimulate", ref wired);

                    // statusText 连线
                    if (controlUI.statusText == null)
                    {
                        var statusTrans = bottomBar.Find("StatusText");
                        if (statusTrans != null)
                            controlUI.statusText = statusTrans.GetComponent<TextMeshProUGUI>();
                    }

                    EditorUtility.SetDirty(controlUI);
                }
            }

            // --- AnnouncementUI ---
            // 查找（可能是 inactive 的）
            var announcementPanels = Resources.FindObjectsOfTypeAll<AnnouncementUI>();
            foreach (var a in announcementPanels)
            {
                if (a.gameObject.scene.isLoaded)
                {
                    updated++;
                    // 确保有 CanvasGroup
                    if (a.GetComponent<CanvasGroup>() == null)
                    {
                        a.gameObject.AddComponent<CanvasGroup>();
                        added++;
                    }
                    break;
                }
            }
        }

        // ==================== UIManager 引用 ====================
        static void UpdateUIManager(ref int wired)
        {
            var uiMgr = Object.FindObjectOfType<UIManager>();
            if (uiMgr == null)
            {
                // 尝试在 [Managers] 下创建
                var managersRoot = GameObject.Find("[Managers]");
                if (managersRoot != null)
                {
                    var go = new GameObject("UIManager");
                    go.transform.SetParent(managersRoot.transform);
                    uiMgr = go.AddComponent<UIManager>();
                    Debug.Log("[SceneUpdater] Created UIManager");
                }
                else return;
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null) return;

            if (uiMgr.loadingPanel == null)
            {
                var t = FindChildRecursive(canvas.transform, "LoadingScreen");
                if (t != null) { uiMgr.loadingPanel = t.gameObject; wired++; }
            }
            if (uiMgr.mainMenuPanel == null)
            {
                var t = FindChildRecursive(canvas.transform, "MainMenuPanel");
                if (t != null) { uiMgr.mainMenuPanel = t.gameObject; wired++; }
            }
            if (uiMgr.gameUIPanel == null)
            {
                var t = FindChildRecursive(canvas.transform, "GameUIPanel");
                if (t != null) { uiMgr.gameUIPanel = t.gameObject; wired++; }
            }
            if (uiMgr.settlementPanel == null)
            {
                var t = FindChildRecursive(canvas.transform, "SettlementPanel");
                if (t != null) { uiMgr.settlementPanel = t.gameObject; wired++; }
            }

            // 子面板引用
            if (uiMgr.gameUIPanel != null)
            {
                var guiTrans = uiMgr.gameUIPanel.transform;
                if (uiMgr.topBar == null)
                {
                    var t = guiTrans.Find("TopBar");
                    if (t != null) { uiMgr.topBar = t.gameObject; wired++; }
                }
                if (uiMgr.leftPlayerList == null)
                {
                    var t = guiTrans.Find("LeftPlayerList");
                    if (t != null) { uiMgr.leftPlayerList = t.gameObject; wired++; }
                }
                if (uiMgr.rightPlayerList == null)
                {
                    var t = guiTrans.Find("RightPlayerList");
                    if (t != null) { uiMgr.rightPlayerList = t.gameObject; wired++; }
                }
                if (uiMgr.giftNotification == null)
                {
                    var t = guiTrans.Find("GiftNotification");
                    if (t != null) { uiMgr.giftNotification = t.gameObject; wired++; }
                }
            }

            var bottomBar = FindChildRecursive(canvas.transform, "BottomBar");
            if (uiMgr.bottomBar == null && bottomBar != null)
            {
                uiMgr.bottomBar = bottomBar.gameObject;
                wired++;
            }

            EditorUtility.SetDirty(uiMgr);
        }

        // ==================== 工具方法 ====================

        /// <summary>在场景中查找指定类型的组件（包括 inactive）</summary>
        static T FindInScene<T>() where T : Component
        {
            // 先尝试 active
            var result = Object.FindObjectOfType<T>();
            if (result != null) return result;

            // 再尝试 inactive
            var all = Resources.FindObjectsOfTypeAll<T>();
            foreach (var c in all)
            {
                if (c.gameObject.scene.isLoaded)
                    return c;
            }
            return null;
        }

        /// <summary>
        /// 确保组件存在于指定路径。
        /// 如果 parentName 下没有 childName 对象，创建之。
        /// 如果对象没有 T 组件，添加之。
        /// </summary>
        static T EnsureComponent<T>(string parentName, string childName, ref int updated, ref int added) where T : Component
        {
            updated++;

            // 先在场景中搜索已有的
            var existing = FindInScene<T>();
            if (existing != null) return existing;

            // 确保父节点存在
            var parent = GameObject.Find(parentName);
            if (parent == null)
            {
                parent = new GameObject(parentName);
                Debug.Log($"[SceneUpdater] Created root: {parentName}");
            }

            // 查找或创建子节点
            var childTrans = parent.transform.Find(childName);
            GameObject childGo;
            if (childTrans != null)
            {
                childGo = childTrans.gameObject;
            }
            else
            {
                childGo = new GameObject(childName);
                childGo.transform.SetParent(parent.transform);
            }

            // 添加组件
            var comp = childGo.GetComponent<T>();
            if (comp == null)
            {
                comp = childGo.AddComponent<T>();
                added++;
                Debug.Log($"[SceneUpdater] Added {typeof(T).Name} to {parentName}/{childName}");
            }

            return comp;
        }

        /// <summary>在已有的 GameObject 上确保组件存在</summary>
        static T EnsureComponentOnExisting<T>(GameObject go, ref int updated, ref int added) where T : Component
        {
            updated++;
            var comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = go.AddComponent<T>();
                added++;
                Debug.Log($"[SceneUpdater] Added {typeof(T).Name} to {go.name}");
            }
            return comp;
        }

        /// <summary>递归查找子节点（支持嵌套查找）</summary>
        static Transform FindChildRecursive(Transform parent, string name)
        {
            // 先直接查找
            var direct = parent.Find(name);
            if (direct != null) return direct;

            // 递归查找
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>修复 TextMeshProUGUI 引用</summary>
        static void WireIfNull(ref TextMeshProUGUI field, Transform parent, string childName, ref int wired)
        {
            if (field != null) return;
            var child = parent.Find(childName);
            if (child == null) return;
            field = child.GetComponent<TextMeshProUGUI>();
            if (field != null) wired++;
        }

        /// <summary>修复 Image 引用</summary>
        static void WireImageIfNull(ref Image field, Transform parent, string childName, ref int wired)
        {
            if (field != null) return;
            var child = parent.Find(childName);
            if (child == null) return;
            field = child.GetComponent<Image>();
            if (field != null) wired++;
        }

        /// <summary>修复 Button 引用</summary>
        static void WireButtonIfNull(ref Button field, Transform parent, string childName, ref int wired)
        {
            if (field != null) return;
            var child = parent.Find(childName);
            if (child == null) return;
            field = child.GetComponent<Button>();
            if (field != null) wired++;
        }

        /// <summary>在 Update Scene (Safe) 中创建紧凑小按钮（与 SceneGenerator 格式一致）</summary>
        static void CreateSmallButtonForUpdate(Transform parent, string name, string label, Vector2 pos, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(150, 38);

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        /// <summary>修复排行列引用 (10行)</summary>
        static void WireRankColumn(Transform settlement, string colName, string prefix,
            ref TextMeshProUGUI title, ref TextMeshProUGUI[] names, ref TextMeshProUGUI[] values, ref int wired)
        {
            var col = settlement.Find(colName);
            if (col == null) return;

            if (title == null)
            {
                title = col.Find($"{prefix}Title")?.GetComponent<TextMeshProUGUI>();
                if (title != null) wired++;
            }

            if (names == null || names.Length == 0)
            {
                names = new TextMeshProUGUI[10];
                values = new TextMeshProUGUI[10];
                for (int i = 0; i < 10; i++)
                {
                    names[i] = col.Find($"{prefix}Name_{i}")?.GetComponent<TextMeshProUGUI>();
                    values[i] = col.Find($"{prefix}Val_{i}")?.GetComponent<TextMeshProUGUI>();
                }
                wired++;
            }
        }

        // ==================== 旧菜单改名提示 ====================

        // 给 Generate Scene 添加确认对话框，防止误操作
        // 注意：这个不会覆盖原来的 MenuItem，只是额外的安全措施
        // 我们在 SceneGenerator.cs 中修改 GenerateScene 添加二次确认
    }
}
#endif
