#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CapybaraDuel.Core;
using CapybaraDuel.Systems;
using CapybaraDuel.VFX;
using CapybaraDuel.UI;
using TMPro;

namespace CapybaraDuel.Editor
{
    public static class SceneGenerator
    {
        // 中文字体缓存
        static TMP_FontAsset _chineseFont;
        // ==================== 颜色常量 ====================
        static readonly Color COL_LEFT = new Color(1f, 0.55f, 0f);        // 橙色 #FF8C00
        static readonly Color COL_RIGHT = new Color(0.68f, 1f, 0.18f);    // 黄绿 #ADFF2F
        static readonly Color COL_GOLD = new Color(1f, 0.84f, 0f);        // 金色
        static readonly Color COL_DARK_BG = new Color(0f, 0f, 0f, 0.7f);  // 深色半透明
        static readonly Color COL_PANEL_BG = new Color(0f, 0f, 0f, 0.5f); // 面板半透明

        [MenuItem("CapybaraDuel/Generate Scene (FULL RESET)", false, 1)]
        public static void GenerateScene()
        {
            // ⚠️ 二次确认：这会清空整个场景！
            if (!EditorUtility.DisplayDialog(
                "WARNING: Full Scene Reset",
                "这将【完全清空】当前场景并从零重建！\n\n" +
                "你在 Unity 编辑器中做的所有手工调整\n" +
                "（灯光、材质、UI位置、相机角度等）都会丢失！\n\n" +
                "如果你只是想更新代码组件和修复引用，请使用：\n" +
                "  CapybaraDuel → Update Scene (Safe)\n\n" +
                "确定要完全重建吗？",
                "是，完全重建", "取消"))
            {
                return;
            }

            // 确保目录存在
            EnsureDirectory("Assets/Scenes");
            EnsureDirectory("Assets/Materials");

            // 复制战斗界面美术资源
            CopyBattleArtAssets();

            // 生成/加载中文 SDF 字体
            _chineseFont = GetOrCreateChineseFontAsset();

            // 修复所有旧 Standard 材质为 URP Lit
            FixAllMaterialsToURP();

            // 创建新场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ==================== 环境设置 ====================
            SetupEnvironment();

            // ==================== 光照（3点布光） ====================
            SetupLighting();

            // ==================== 摄像机（竖屏视角） ====================
            var camGo = SetupCamera();

            // ==================== 3D场景物体 ====================
            var sceneRoot = new GameObject("[Scene]");

            // -- 地面（优先使用真实模型） --
            var ground = LoadSceneModel("301_Terrain001a", "Mat_Terrain", sceneRoot.transform);
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = new Vector3(3f, 1f, 2f);
                ground.transform.SetParent(sceneRoot.transform);
                var matGround = LoadOrCreateMaterial("Mat_Ground", new Color(0.3f, 0.6f, 0.2f));
                ground.GetComponent<Renderer>().sharedMaterial = matGround;
            }

            // -- 橘子（使用正式模型 Orange.prefab） --
            GameObject orange = LoadOrangeModel(sceneRoot.transform);
            if (orange == null)
            {
                orange = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orange.name = "Orange";
                orange.transform.SetParent(sceneRoot.transform);
                orange.transform.position = new Vector3(0, 1, 0);
                orange.transform.localScale = Vector3.one * 1.5f;
                var matOrange = LoadOrCreateMaterial("Mat_Orange", new Color(1f, 0.6f, 0f));
                orange.GetComponent<Renderer>().sharedMaterial = matOrange;
            }
            var oc = orange.GetComponent<OrangeController>();
            if (oc == null)
                oc = orange.AddComponent<OrangeController>();

            // 确保橘子使用魔法Shader材质
            EnsureMagicOrangeMaterial(orange);

            // 添加 MagicOrangeEffect 动态效果控制
            if (orange.GetComponent<MagicOrangeEffect>() == null)
                orange.AddComponent<MagicOrangeEffect>();

            // 添加 OrangeDustTrail 移动烟尘效果
            if (orange.GetComponent<OrangeDustTrail>() == null)
                orange.AddComponent<OrangeDustTrail>();

            // 添加 OrangeSpeedHUD 头顶速度+方向显示（场景中预创建UI对象）
            var speedHud = orange.GetComponent<OrangeSpeedHUD>();
            if (speedHud == null)
                speedHud = orange.AddComponent<OrangeSpeedHUD>();
            CreateOrUpdateSpeedHUD(sceneRoot.transform, orange, speedHud);

            // -- 生成点 --
            var leftSpawn = new GameObject("LeftSpawnPoint");
            leftSpawn.transform.SetParent(sceneRoot.transform);
            leftSpawn.transform.position = new Vector3(-18, 0.5f, 0);

            var rightSpawn = new GameObject("RightSpawnPoint");
            rightSpawn.transform.SetParent(sceneRoot.transform);
            rightSpawn.transform.position = new Vector3(18, 0.5f, 0);

            // -- 终点标记（3D场景中可见的终点线，路径±45） --
            CreateGoalMarker(sceneRoot.transform, "LeftGoalMarker", new Vector3(-38, 0, 0),
                "香橙终点", new Color(1f, 0.55f, 0f));
            CreateGoalMarker(sceneRoot.transform, "RightGoalMarker", new Vector3(38, 0, 0),
                "柚子终点", new Color(0.68f, 1f, 0.18f));

            // ==================== Managers ====================
            var managers = new GameObject("[Managers]");

            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(managers.transform);
            var gm = gmGo.AddComponent<GameManager>();

            var netGo = new GameObject("NetworkManager");
            netGo.transform.SetParent(managers.transform);
            netGo.AddComponent<NetworkManager>();

            var spawnerGo = new GameObject("CapybaraSpawner");
            spawnerGo.transform.SetParent(managers.transform);
            var spawner = spawnerGo.AddComponent<CapybaraSpawner>();
            spawner.leftSpawnPoint = leftSpawn.transform;
            spawner.rightSpawnPoint = rightSpawn.transform;
            spawner.orangeTarget = orange.transform;
            // 赋值 Kpbl（卡皮巴拉）正式模型 prefab
            var kpblPrefab = GetOrCreateKpblPrefab();
            if (kpblPrefab != null)
            {
                spawner.capybaraPrefab = kpblPrefab;
                Debug.Log("[SceneGen] KpblUnit prefab assigned to CapybaraSpawner");
            }
            else
            {
                Debug.LogWarning("[SceneGen] Kpbl model not found! Spawner will use fallback cubes.");
            }

            var forceGo = new GameObject("ForceSystem");
            forceGo.transform.SetParent(managers.transform);
            var fs = forceGo.AddComponent<ForceSystem>();

            var campGo = new GameObject("CampSystem");
            campGo.transform.SetParent(managers.transform);
            var cs = campGo.AddComponent<CampSystem>();

            var rankGo = new GameObject("RankingSystem");
            rankGo.transform.SetParent(managers.transform);
            var rs = rankGo.AddComponent<RankingSystem>();

            var giftGo = new GameObject("GiftHandler");
            giftGo.transform.SetParent(managers.transform);
            var gh = giftGo.AddComponent<GiftHandler>();

            // BarrageSimulator
            var simGo = new GameObject("BarrageSimulator");
            simGo.transform.SetParent(managers.transform);
            simGo.AddComponent<BarrageSimulator>();

            // VFX + CameraShake
            var vfxGo = new GameObject("VFXSpawner");
            vfxGo.transform.SetParent(managers.transform);
            vfxGo.AddComponent<VFXSpawner>();

            // FootDustManager 脚底烟雾（单例）
            var footDustGo = new GameObject("FootDustManager");
            footDustGo.transform.SetParent(managers.transform);
            footDustGo.AddComponent<FootDustManager>();

            var shakeGo = new GameObject("CameraShake");
            shakeGo.transform.SetParent(camGo.transform); // 挂在摄像机下
            shakeGo.AddComponent<CameraShake>();

            // AudioManager
            var audioGo = new GameObject("AudioManager");
            audioGo.transform.SetParent(managers.transform);
            var audioMgr = audioGo.AddComponent<AudioManager>();
            // BGM AudioSource
            var bgmSrc = audioGo.AddComponent<AudioSource>();
            bgmSrc.playOnAwake = false;
            bgmSrc.loop = true;
            bgmSrc.volume = 0.5f;
            audioMgr.bgmSource = bgmSrc;
            // SFX AudioSource
            var sfxGo = new GameObject("SFX");
            sfxGo.transform.SetParent(audioGo.transform);
            var sfxSrc = sfxGo.AddComponent<AudioSource>();
            sfxSrc.playOnAwake = false;
            sfxSrc.loop = false;
            audioMgr.sfxSource = sfxSrc;

            // 连线 GameManager
            gm.forceSystem = fs;
            gm.campSystem = cs;
            gm.rankingSystem = rs;
            gm.giftHandler = gh;
            gm.spawner = spawner;
            gm.orangeController = oc;

            // 连线摄像机跟随橘子
            var followCam = camGo.GetComponent<OrangeFollowCamera>();
            if (followCam != null)
                followCam.target = orange.transform;

            // ==================== UI Canvas ====================
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // ================== 加载/连接界面（启动时显示）==================
            var loadingScreen = CreateLoadingScreenUI(canvasGo.transform);

            // ================== 主界面（Idle 状态）==================
            var mainMenu = CreateMainMenuUI(canvasGo.transform);

            // ================== 对局界面（Running 状态）==================
            var gameUI = CreateGameUI(canvasGo.transform);

            // ================== 结算界面（Settlement 状态）==================
            var settlement = CreateSettlementUI(canvasGo.transform);

            // ================== 公告UI（比赛开始/结束提示）==================
            var announcement = CreateAnnouncementUI(canvasGo.transform);
            // 不要 SetActive(false)！AnnouncementUI 自己用 CanvasGroup alpha=0 隐藏
            // SetActive(false) 会阻止 Awake/OnEnable，导致 Instance 为 null，
            // GameManager 会创建第二个无字体的面板 → 中文乱码

            // ================== 排行榜面板（默认隐藏）==================
            var rankingPanel = CreateRankingPanel(canvasGo.transform);
            rankingPanel.SetActive(false);

            // ================== 底部控制栏（调试用，始终可见）==================
            var bottomBar = CreateBottomBar(canvasGo.transform);

            // ================== UI 组件挂载 ==================

            // TopBar 组件
            var topBarGO = gameUI.transform.Find("TopBar")?.gameObject;
            if (topBarGO != null)
            {
                var topBarUI = topBarGO.AddComponent<TopBarUI>();
                topBarUI.leftForceText = topBarGO.transform.Find("LeftForceText")?.GetComponent<TextMeshProUGUI>();
                topBarUI.rightForceText = topBarGO.transform.Find("RightForceText")?.GetComponent<TextMeshProUGUI>();
                topBarUI.timerText = topBarGO.transform.Find("TimerText")?.GetComponent<TextMeshProUGUI>();
                topBarUI.scorePoolText = topBarGO.transform.Find("ScorePoolText")?.GetComponent<TextMeshProUGUI>();
                var topBarBgTrans = topBarGO.transform.Find("TopBarBg");
                if (topBarBgTrans != null)
                {
                    topBarUI.progressBarLeft = topBarBgTrans.Find("ProgressBarLeft")?.GetComponent<Image>();
                    topBarUI.progressBarRight = topBarBgTrans.Find("ProgressBarRight")?.GetComponent<Image>();
                    // 距离标记
                    topBarUI.leftEndMarker = topBarBgTrans.Find("LeftEndMarker")?.GetComponent<TextMeshProUGUI>();
                    topBarUI.rightEndMarker = topBarBgTrans.Find("RightEndMarker")?.GetComponent<TextMeshProUGUI>();
                    topBarUI.centerMarker = topBarBgTrans.Find("CenterMarker")?.GetComponent<TextMeshProUGUI>();
                    topBarUI.posIndicatorText = topBarBgTrans.Find("PosIndicator")?.GetComponent<TextMeshProUGUI>();
                }
            }

            // PlayerList 组件
            var playerListGo = new GameObject("PlayerListUI");
            playerListGo.transform.SetParent(gameUI.transform, false);
            var playerListUI = playerListGo.AddComponent<PlayerListUI>();
            playerListUI.leftListContainer = gameUI.transform.Find("LeftPlayerList");
            playerListUI.rightListContainer = gameUI.transform.Find("RightPlayerList");

            // GiftNotification 组件
            var giftNotifGO = gameUI.transform.Find("GiftNotification")?.gameObject;
            if (giftNotifGO != null)
            {
                var giftNotifUI = giftNotifGO.AddComponent<GiftNotificationUI>();
                giftNotifUI.container = giftNotifGO.transform;
            }

            // GiftAnimationUI 组件（礼物GIF动画，锚定在屏幕下方）
            var giftAnimGO = new GameObject("GiftAnimation");
            giftAnimGO.transform.SetParent(gameUI.transform, false);
            var giftAnimRT = giftAnimGO.AddComponent<RectTransform>();
            giftAnimRT.anchorMin = new Vector2(0, 0);
            giftAnimRT.anchorMax = new Vector2(1, 0.4f); // 屏幕下方40%区域
            giftAnimRT.offsetMin = Vector2.zero;
            giftAnimRT.offsetMax = Vector2.zero;
            var giftAnimUI = giftAnimGO.AddComponent<GiftAnimationUI>();
            giftAnimUI.animationContainer = giftAnimRT;

            // PlayerJoinNotification 组件（玩家加入侧滑通知）
            var joinNotifGO = gameUI.transform.Find("JoinNotification")?.gameObject;
            if (joinNotifGO != null)
            {
                var joinNotifUI = joinNotifGO.AddComponent<PlayerJoinNotificationUI>();
                joinNotifUI.container = joinNotifGO.transform;
            }

            // VIPAnnouncementUI 组件
            var vipAnnouncementGO = gameUI.transform.Find("VIPAnnouncement")?.gameObject;
            if (vipAnnouncementGO != null)
            {
                var vipUI = vipAnnouncementGO.AddComponent<VIPAnnouncementUI>();
                vipUI.announcementText = vipAnnouncementGO.transform.Find("VIPText")?.GetComponent<TextMeshProUGUI>();
                vipUI.backgroundOverlay = vipAnnouncementGO.GetComponent<Image>();
                vipUI.canvasGroup = vipAnnouncementGO.GetComponent<CanvasGroup>();
            }

            // MainMenuUI 组件（主界面按钮功能）
            var mainMenuUI = mainMenu.AddComponent<MainMenuUI>();
            var btnGroup = mainMenu.transform.Find("ButtonGroup");
            if (btnGroup != null)
            {
                mainMenuUI.btnStartGame = btnGroup.Find("BtnStartGame")?.GetComponent<Button>();
                mainMenuUI.btnLeaderboard = btnGroup.Find("BtnLeaderboard")?.GetComponent<Button>();
                mainMenuUI.btnGiftDesc = btnGroup.Find("BtnGiftDesc")?.GetComponent<Button>();
                mainMenuUI.btnRuleDesc = btnGroup.Find("BtnRuleDesc")?.GetComponent<Button>();
                mainMenuUI.btnStickerSettings = btnGroup.Find("BtnStickerSettings")?.GetComponent<Button>();
            }

            // RankingPanelUI 组件
            WireRankingPanel(rankingPanel);

            // Settlement 组件
            var settlementUI = settlement.AddComponent<SettlementUI>();
            settlementUI.winnerText = settlement.transform.Find("WinnerText")?.GetComponent<TextMeshProUGUI>();
            settlementUI.restartButton = settlement.transform.Find("BtnRestart")?.GetComponent<Button>();

            // MVP
            var mvpAreaTrans = settlement.transform.Find("MVPArea");
            if (mvpAreaTrans != null)
            {
                // MVPName 在 MVPNameBar 子节点下
                var nameBar = mvpAreaTrans.Find("MVPNameBar");
                settlementUI.mvpNameText = nameBar?.Find("MVPName")?.GetComponent<TextMeshProUGUI>();
                settlementUI.mvpLabelText = mvpAreaTrans.Find("MVPLabel")?.GetComponent<TextMeshProUGUI>();
                settlementUI.mvpContributionText = mvpAreaTrans.Find("MVPContribution")?.GetComponent<TextMeshProUGUI>();
                // 头像在 MVPAvatarFrame/MVPAvatar
                var avatarFrame = mvpAreaTrans.Find("MVPAvatarFrame");
                settlementUI.mvpAvatarImage = avatarFrame?.Find("MVPAvatar")?.GetComponent<Image>();
            }

            // 左阵营排行 (10行双TMP)
            var leftCol = settlement.transform.Find("LeftRankColumn");
            if (leftCol != null)
            {
                settlementUI.leftRankTitle = leftCol.Find("LeftRankTitle")?.GetComponent<TextMeshProUGUI>();
                var leftNames = new TextMeshProUGUI[10];
                var leftValues = new TextMeshProUGUI[10];
                for (int i = 0; i < 10; i++)
                {
                    leftNames[i] = leftCol.Find($"LeftRankName_{i}")?.GetComponent<TextMeshProUGUI>();
                    leftValues[i] = leftCol.Find($"LeftRankVal_{i}")?.GetComponent<TextMeshProUGUI>();
                }
                settlementUI.leftRankNames = leftNames;
                settlementUI.leftRankValues = leftValues;
            }

            // 右阵营排行 (10行双TMP)
            var rightCol = settlement.transform.Find("RightRankColumn");
            if (rightCol != null)
            {
                settlementUI.rightRankTitle = rightCol.Find("RightRankTitle")?.GetComponent<TextMeshProUGUI>();
                var rightNames = new TextMeshProUGUI[10];
                var rightValues = new TextMeshProUGUI[10];
                for (int i = 0; i < 10; i++)
                {
                    rightNames[i] = rightCol.Find($"RightRankName_{i}")?.GetComponent<TextMeshProUGUI>();
                    rightValues[i] = rightCol.Find($"RightRankVal_{i}")?.GetComponent<TextMeshProUGUI>();
                }
                settlementUI.rightRankNames = rightNames;
                settlementUI.rightRankValues = rightValues;
            }

            // 积分瓜分 (6条双TMP)
            var scoreDistAreaTrans = settlement.transform.Find("ScoreDistArea");
            if (scoreDistAreaTrans != null)
            {
                settlementUI.scorePoolLabel = scoreDistAreaTrans.Find("ScorePoolLabel")?.GetComponent<TextMeshProUGUI>();
                var distNames = new TextMeshProUGUI[6];
                var distValues = new TextMeshProUGUI[6];
                for (int i = 0; i < 6; i++)
                {
                    distNames[i] = scoreDistAreaTrans.Find($"ScoreDistName_{i}")?.GetComponent<TextMeshProUGUI>();
                    distValues[i] = scoreDistAreaTrans.Find($"ScoreDistVal_{i}")?.GetComponent<TextMeshProUGUI>();
                }
                settlementUI.scoreDistNames = distNames;
                settlementUI.scoreDistValues = distValues;
            }

            // GameControl 组件（底部GM工具面板，默认隐藏，6次点击唤出）
            bottomBar.AddComponent<CanvasGroup>();
            var controlUI = bottomBar.AddComponent<GameControlUI>();
            controlUI.gmLoginButton = bottomBar.transform.Find("BtnGMLogin")?.GetComponent<Button>();
            controlUI.simulateButton = bottomBar.transform.Find("BtnSimulate")?.GetComponent<Button>();
            controlUI.statusText = bottomBar.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();

            // ================== UIManager 连线 ==================
            var uiMgrGo = new GameObject("UIManager");
            uiMgrGo.transform.SetParent(managers.transform);
            var uiMgr = uiMgrGo.AddComponent<UIManager>();
            // 新版面板赋值
            uiMgr.loadingPanel = loadingScreen;
            uiMgr.mainMenuPanel = mainMenu;
            uiMgr.gameUIPanel = gameUI;
            uiMgr.settlementPanel = settlement;
            // 旧版子面板兼容赋值
            uiMgr.topBar = gameUI.transform.Find("TopBar")?.gameObject;
            uiMgr.leftPlayerList = gameUI.transform.Find("LeftPlayerList")?.gameObject;
            uiMgr.rightPlayerList = gameUI.transform.Find("RightPlayerList")?.gameObject;
            uiMgr.giftNotification = gameUI.transform.Find("GiftNotification")?.gameObject;
            uiMgr.bottomBar = bottomBar;

            // ==================== 保存场景 ====================
            string scenePath = "Assets/Scenes/MainScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[SceneGenerator] Scene saved to {scenePath}");
            EditorUtility.DisplayDialog("Done",
                "MainScene.unity 已生成！\n\n" +
                "场景包含：\n" +
                "• 3D场景（地形+橘子正式模型）\n" +
                "• 3点布光（主光+填充光+环境光）\n" +
                "• 第三人称俯视摄像机（50°跟随橘子）\n" +
                "• BigSheep 正式玩家模型\n" +
                "• 主界面UI（Logo+按钮+背景）\n" +
                "• 对局UI（推力条/玩家列表/礼物通知）\n" +
                "• 结算UI（胜利面板）\n" +
                "• 所有Manager组件\n\n" +
                "下一步：点击底部「模拟」按钮测试",
                "OK");
        }

        // ==================== 环境设置 ====================
        static void SetupEnvironment()
        {
            // 环境光
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.50f, 0.55f); // 柔和冷色环境光

            // 天空（纯色或天空盒）
            var skyTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Scene/Cb_zhucheng_sky.png");
            if (skyTex != null)
            {
                // 创建 Skybox/Panoramic 材质
                string skyPath = "Assets/Materials/Mat_Sky.mat";
                var skyMat = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                if (skyMat == null)
                {
                    EnsureDirectory("Assets/Materials");
                    var skyShader = Shader.Find("Skybox/Panoramic");
                    if (skyShader == null) skyShader = Shader.Find("Skybox/6 Sided");
                    if (skyShader == null) skyShader = Shader.Find("Skybox/Procedural"); // fallback
                    skyMat = new Material(skyShader);
                    AssetDatabase.CreateAsset(skyMat, skyPath);
                }
                skyMat.SetTexture("_MainTex", skyTex);
                RenderSettings.skybox = skyMat;
            }
            else
            {
                RenderSettings.skybox = null;
            }

            // 雾效
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.9f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 80f;
        }

        // ==================== 3点布光 ====================
        static void SetupLighting()
        {
            // 主光（暖色，模拟阳光）
            var mainLight = new GameObject("Main Light");
            var dl = mainLight.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(1f, 0.95f, 0.82f); // 暖白
            dl.intensity = 1.3f;
            dl.shadows = LightShadows.Soft;
            dl.shadowStrength = 0.5f;
            mainLight.transform.rotation = Quaternion.Euler(45, -30, 0);

            // 填充光（冷色，补光阴影面）
            var fillLight = new GameObject("Fill Light");
            var fl = fillLight.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.color = new Color(0.6f, 0.75f, 1f); // 冷蓝
            fl.intensity = 0.35f;
            fl.shadows = LightShadows.None;
            fillLight.transform.rotation = Quaternion.Euler(30, 150, 0);

            // 背光/边缘光
            var rimLight = new GameObject("Rim Light");
            var rl = rimLight.AddComponent<Light>();
            rl.type = LightType.Directional;
            rl.color = new Color(1f, 0.9f, 0.7f); // 金色边缘
            rl.intensity = 0.25f;
            rl.shadows = LightShadows.None;
            rimLight.transform.rotation = Quaternion.Euler(15, -160, 0);
        }

        // ==================== 摄像机（第三人称俯视角，跟随橘子） ====================
        static GameObject SetupCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.3f, 0.6f, 0.85f); // 天蓝色 fallback
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            // 第三人称俯视角 50 度（1080x1920 竖屏）
            // height=15, distance=12 → 从后上方看向橘子区域
            camGo.transform.position = new Vector3(0, 15, -12);
            camGo.transform.rotation = Quaternion.Euler(50, 0, 0);

            camGo.AddComponent<AudioListener>();

            // 添加橘子跟随摄像机组件（target 在 GenerateScene 中赋值）
            camGo.AddComponent<OrangeFollowCamera>();

            return camGo;
        }

        // ==================== 加载3D模型 ====================
        static GameObject LoadSceneModel(string modelName, string materialName, Transform parent)
        {
            string objPath = $"Assets/Models/Scene/{modelName}.obj";

            // 方案1：直接 Instantiate OBJ（保留自带的 MeshRenderer + Material）
            var objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (objAsset != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(objAsset);
                go.name = modelName;
                go.transform.SetParent(parent, false);

                // 如果 Assets/Materials/ 下有自定义材质，覆盖 OBJ 自带材质
                var customMat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{materialName}.mat");
                if (customMat != null)
                {
                    var rend = go.GetComponentInChildren<MeshRenderer>();
                    if (rend != null) rend.sharedMaterial = customMat;
                }

                Debug.Log($"[SceneGen] Loaded scene model: {modelName} (from OBJ with built-in material)");
                return go;
            }

            // 方案2：尝试手动加载 Mesh（兼容旧逻辑）
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(objPath);
            if (mesh == null)
            {
                Debug.LogWarning($"[SceneGen] Model not found: {objPath}");
                return null;
            }

            var goManual = new GameObject(modelName);
            goManual.transform.SetParent(parent, false);

            var meshFilter = goManual.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = goManual.AddComponent<MeshRenderer>();

            // 加载材质：自定义 → OBJ 自带 → fallback
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{materialName}.mat");
            if (mat == null)
                mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Models/Scene/{modelName}_0Mat.mat");
            if (mat == null)
                mat = FindBestMaterial(modelName);
            if (mat != null)
                renderer.sharedMaterial = mat;

            var col = goManual.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;

            return goManual;
        }

        static GameObject LoadOrangeModel(Transform parent)
        {
            // 优先直接加载 FBX 模型（OBJ 已废弃）
            string[] fbxPaths = {
                "Assets/Models/Orange/527_Chengzi.fbx",
                "Assets/Models/Orange/chengzi.fbx"
            };

            foreach (var fbxPath in fbxPaths)
            {
                var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbxAsset != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                    inst.name = "Orange";
                    inst.transform.SetParent(parent, false);
                    inst.transform.position = new Vector3(0, 1.2f, 0);
                    inst.transform.localScale = Vector3.one * 2f;

                    // 确保有碰撞体
                    if (inst.GetComponent<Collider>() == null && inst.GetComponentInChildren<Collider>() == null)
                    {
                        var sc = inst.AddComponent<SphereCollider>();
                        sc.radius = 0.75f;
                    }

                    Debug.Log($"[SceneGen] Orange FBX loaded: {fbxPath}");
                    return inst;
                }
            }

            // 回退：加载 Prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Scene/Orange.prefab");
            if (prefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetParent(parent, false);
                inst.transform.position = new Vector3(0, 1.2f, 0);
                inst.transform.localScale = Vector3.one * 2f;
                Debug.Log("[SceneGen] Orange prefab loaded");
                return inst;
            }

            Debug.LogWarning("[SceneGen] Orange model not found, using sphere fallback");
            return null;
        }

        /// <summary>
        /// 获取或创建 Kpbl（卡皮巴拉）角色 prefab
        /// 从 kapibala.fbx 实例化，添加 Animator + Capybara + CapsuleCollider
        /// </summary>
        static GameObject GetOrCreateKpblPrefab()
        {
            // 优先使用用户已配置好的 Pushing.prefab（基于 Pushing.fbx，含动画+Avatar+Controller）
            string pushingPrefabPath = "Assets/Prefabs/Units/Pushing.prefab";
            var pushingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pushingPrefabPath);
            if (pushingPrefab != null)
            {
                Debug.Log("[SceneGen] Using existing Pushing.prefab");
                return pushingPrefab;
            }

            // Fallback: 使用 KpblUnit.prefab
            string kpblPrefabPath = "Assets/Prefabs/Units/KpblUnit.prefab";
            var kpblPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(kpblPrefabPath);
            if (kpblPrefab != null)
            {
                Debug.Log("[SceneGen] Using existing KpblUnit.prefab");
                return kpblPrefab;
            }

            Debug.LogWarning("[SceneGen] No capybara prefab found in Prefabs/Units/");
            return null;
        }

        static Material FindBestMaterial(string modelName)
        {
            // 尝试在 Materials 目录下找同名材质
            string[] guesses = {
                $"Assets/Materials/Mat_{modelName}.mat",
                $"Assets/Materials/{modelName}.mat",
            };
            foreach (var p in guesses)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null) return m;
            }
            return null;
        }

        // ==================== 加载/连接界面UI ====================
        static GameObject CreateLoadingScreenUI(Transform canvasTransform)
        {
            var panel = CreatePanel(canvasTransform, "LoadingScreen", Vector2.zero, Vector2.zero);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            // 黑色背景
            var bgImg = panel.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            bgImg.raycastTarget = true;

            // 旋转指示器（圆形）
            var spinnerGo = CreatePanel(panel.transform, "Spinner",
                new Vector2(0, 80), new Vector2(80, 80));
            var spinnerImg = spinnerGo.AddComponent<Image>();
            spinnerImg.color = new Color(1f, 0.7f, 0.2f, 0.9f); // 橙色
            spinnerImg.raycastTarget = false;

            // 状态文字
            var statusText = CreateText(panel.transform, "StatusText", "正在连接服务器",
                new Vector2(-30, -30), 38, Color.white);
            var statusRT = statusText.GetComponent<RectTransform>();
            statusRT.sizeDelta = new Vector2(600, 60);
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontStyle = FontStyles.Bold;

            // 动态省略号
            var dotText = CreateText(panel.transform, "DotText", "",
                new Vector2(270, -30), 38, Color.white);
            var dotRT = dotText.GetComponent<RectTransform>();
            dotRT.sizeDelta = new Vector2(80, 60);
            dotText.alignment = TextAlignmentOptions.Left;

            // 版本号/提示文字（底部）
            var tipText = CreateText(panel.transform, "TipText", "卡皮巴拉对决 v1.0",
                new Vector2(0, -400), 22, new Color(0.5f, 0.5f, 0.5f));
            var tipRT = tipText.GetComponent<RectTransform>();
            tipRT.sizeDelta = new Vector2(400, 40);
            tipText.alignment = TextAlignmentOptions.Center;

            // 重试按钮（默认隐藏）
            var retryGo = CreatePanel(panel.transform, "BtnRetry",
                new Vector2(0, -120), new Vector2(280, 70));
            var retryImg = retryGo.AddComponent<Image>();
            retryImg.color = new Color(0.9f, 0.5f, 0.1f);
            var retryBtn = retryGo.AddComponent<Button>();
            retryBtn.targetGraphic = retryImg;

            var retryTextTMP = CreateText(retryGo.transform, "Text", "重新连接",
                Vector2.zero, 30, Color.white);
            var retryTextRT = retryTextTMP.GetComponent<RectTransform>();
            retryTextRT.anchorMin = Vector2.zero;
            retryTextRT.anchorMax = Vector2.one;
            retryTextRT.offsetMin = Vector2.zero;
            retryTextRT.offsetMax = Vector2.zero;
            retryTextTMP.alignment = TextAlignmentOptions.Center;

            retryGo.SetActive(false); // 默认隐藏

            // 挂载 LoadingScreenUI 组件并连线
            var loadingUI = panel.AddComponent<LoadingScreenUI>();
            loadingUI.statusText = statusText;
            loadingUI.dotText = dotText;
            loadingUI.retryButton = retryBtn;
            loadingUI.retryButtonText = retryTextTMP;
            loadingUI.spinnerImage = spinnerImg;

            return panel;
        }

        // ==================== 主界面UI ====================
        static GameObject CreateMainMenuUI(Transform canvasTransform)
        {
            var mainMenu = CreatePanel(canvasTransform, "MainMenuPanel", Vector2.zero, Vector2.zero);
            var mainMenuRT = mainMenu.GetComponent<RectTransform>();
            // 全屏拉伸
            mainMenuRT.anchorMin = Vector2.zero;
            mainMenuRT.anchorMax = Vector2.one;
            mainMenuRT.offsetMin = Vector2.zero;
            mainMenuRT.offsetMax = Vector2.zero;

            // -- 全屏背景图 --
            var bgSprite = LoadSprite("Assets/Art/UI/MainMenu/background_main.png");
            var bgImg = mainMenu.AddComponent<Image>();
            if (bgSprite != null)
            {
                bgImg.sprite = bgSprite;
                bgImg.type = Image.Type.Simple;
                bgImg.preserveAspect = false;
            }
            else
            {
                bgImg.color = new Color(0.15f, 0.25f, 0.35f); // 深蓝绿色备用
            }

            // -- Logo（上方，用户调整后 y=602）--
            var logoSprite = LoadSprite("Assets/Art/UI/MainMenu/logo.png");
            if (logoSprite != null)
            {
                var logoGo = CreatePanel(mainMenu.transform, "Logo", new Vector2(0, 602), new Vector2(900, 650));
                var logoImg = logoGo.AddComponent<Image>();
                logoImg.sprite = logoSprite;
                logoImg.preserveAspect = true;
                logoImg.raycastTarget = false;
            }
            else
            {
                // 文字 Logo 作为 fallback
                CreateText(mainMenu.transform, "LogoText", "卡皮巴拉对决", new Vector2(0, 602), 64, COL_GOLD);
            }

            // -- 面板装饰框（用户调整后 y=-35）--
            var frameSprite = LoadSprite("Assets/Art/UI/MainMenu/ui_panel_frame_clean.png");
            if (frameSprite != null)
            {
                var frameGo = CreatePanel(mainMenu.transform, "PanelFrame", new Vector2(0, -35), new Vector2(800, 930));
                var frameImg = frameGo.AddComponent<Image>();
                frameImg.sprite = frameSprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.preserveAspect = false;
                frameImg.raycastTarget = false;
            }

            // -- 主菜单按钮组（用户调整后 y=-35）--
            var btnGroup = CreatePanel(mainMenu.transform, "ButtonGroup", new Vector2(0, -35), new Vector2(600, 700));

            // 开始游戏按钮
            CreateSpriteButton(btnGroup.transform, "BtnStartGame",
                "Assets/Art/UI/MainMenu/btn_start_game.png",
                "开始玩法", new Vector2(0, 200), new Vector2(500, 110));

            // 排行榜按钮
            CreateSpriteButton(btnGroup.transform, "BtnLeaderboard",
                "Assets/Art/UI/MainMenu/btn_leaderboard.png",
                "排行榜", new Vector2(0, 60), new Vector2(500, 110));

            // 礼物说明按钮
            CreateSpriteButton(btnGroup.transform, "BtnGiftDesc",
                "Assets/Art/UI/MainMenu/btn_gift_desc.png",
                "礼物说明", new Vector2(0, -80), new Vector2(500, 110));

            // 规则说明按钮
            CreateSpriteButton(btnGroup.transform, "BtnRuleDesc",
                "Assets/Art/UI/MainMenu/btn_rule_desc.png",
                "规则说明", new Vector2(0, -220), new Vector2(500, 110));

            // 贴纸设置按钮（小一点，放右下角）
            CreateSpriteButton(btnGroup.transform, "BtnStickerSettings",
                "Assets/Art/UI/MainMenu/btn_sticker_settings.png",
                "贴纸设置", new Vector2(200, -360), new Vector2(280, 80));

            return mainMenu;
        }

        // ==================== 排行榜面板 ====================
        static GameObject CreateRankingPanel(Transform canvasTransform)
        {
            // 全屏遮罩
            var panel = CreatePanel(canvasTransform, "RankingPanel", Vector2.zero, Vector2.zero);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            // 半透明黑色背景（点击关闭）
            var overlayImg = panel.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.5f);

            // ---- 上半区：积分周榜背景（三个奖台）----
            var bgSprite = LoadSprite("Assets/Art/UI/Ranking/ranking_bg.png");
            var bgGo = CreatePanel(panel.transform, "RankingBg", new Vector2(0, 6), new Vector2(1080, 900));
            bgGo.transform.localScale = Vector3.one * 2.1194f;
            var bgImg = bgGo.AddComponent<Image>();
            if (bgSprite != null)
            {
                bgImg.sprite = bgSprite;
                bgImg.preserveAspect = true;
            }
            else
            {
                bgImg.color = new Color(0.2f, 0.3f, 0.4f);
            }
            bgImg.raycastTarget = false;

            // ---- 标题: "积分周榜" ----
            var titleTMP = CreateText(panel.transform, "Title", "积分周榜",
                new Vector2(0, 810), 52, COL_GOLD);
            var titleRT = titleTMP.GetComponent<RectTransform>();
            titleRT.sizeDelta = new Vector2(600, 80);
            titleTMP.fontStyle = FontStyles.Bold;

            // ---- Tab 栏 ----
            string[] tabLabels = { "周榜", "月榜", "连胜榜", "小时榜" };
            float tabStartX = -300f;
            float tabSpacing = 200f;
            var tabNormalSprite = LoadSprite("Assets/Art/UI/Ranking/tab_normal.png");
            var tabSelectedSprite = LoadSprite("Assets/Art/UI/Ranking/tab_selected.png");

            for (int i = 0; i < 4; i++)
            {
                float x = tabStartX + i * tabSpacing;
                var tabGo = CreatePanel(panel.transform, $"Tab_{i}", new Vector2(x, 720), new Vector2(170, 55));
                tabGo.transform.localScale = Vector3.one * 1.6599f;

                var tabImg = tabGo.AddComponent<Image>();
                tabImg.sprite = (i == 0) ? tabSelectedSprite : tabNormalSprite;
                tabImg.type = Image.Type.Simple;
                tabImg.preserveAspect = true;
                if (tabImg.sprite == null)
                    tabImg.color = (i == 0) ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.5f, 0.35f, 0.15f);

                var tabBtn = tabGo.AddComponent<Button>();
                tabBtn.targetGraphic = tabImg;

                // Tab 文字
                var tabTextTMP = CreateText(tabGo.transform, "Text", tabLabels[i],
                    Vector2.zero, 26, Color.white);
                var tabTextRT = tabTextTMP.GetComponent<RectTransform>();
                tabTextRT.anchorMin = Vector2.zero;
                tabTextRT.anchorMax = Vector2.one;
                tabTextRT.offsetMin = Vector2.zero;
                tabTextRT.offsetMax = Vector2.zero;
                tabTextRT.anchoredPosition = Vector2.zero;
                tabTextRT.sizeDelta = Vector2.zero;
                tabTextTMP.alignment = TextAlignmentOptions.Center;
            }

            // ---- Top3 文字区（用户手动校准位置 2026-02-11）----
            // 名字、头像、贡献值位置独立指定（不再用统一公式，以场景实际值为准）
            Vector2[] top3NamePos = {
                new Vector2(0, 374),          // 1st (中)
                new Vector2(-310, 292),       // 2nd (左)
                new Vector2(310, 286),        // 3rd (右)
            };
            Vector2[] top3ScorePos = {
                new Vector2(0, 334),
                new Vector2(-310, 252),
                new Vector2(310, 246),
            };
            Vector2[] top3AvatarFramePos = {
                new Vector2(0, 464),
                new Vector2(-310, 382),
                new Vector2(310, 376),
            };
            string[] top3DefaultNames = { "水豚大王", "温泉王子", "泡泡公主" };
            string[] top3DefaultScores = { "贡献值 10,000,000", "贡献值 8,500,000", "贡献值 7,200,000" };
            int[] top3Numbers = { 1, 2, 3 };

            // 头像尺寸（用户校准值）
            float[] avatarSizes = { 120f, 100f, 100f };
            float[] frameSizes = { 136f, 116f, 116f };
            Color[] frameColors = {
                new Color(1f, 0.84f, 0f),      // 金
                new Color(0.75f, 0.75f, 0.8f),  // 银
                new Color(0.8f, 0.52f, 0.25f)   // 铜
            };
            Color[] avatarBgColors = {
                new Color(1f, 0.84f, 0f, 0.3f),
                new Color(0.75f, 0.75f, 0.8f, 0.3f),
                new Color(0.8f, 0.52f, 0.25f, 0.3f)
            };

            for (int i = 0; i < 3; i++)
            {
                float avatarSize = avatarSizes[i];
                float frameSize = frameSizes[i];

                // === 头像框（外圈） ===
                var frameGo = CreatePanel(panel.transform, $"Top3AvatarFrame_{i}",
                    top3AvatarFramePos[i], new Vector2(frameSize, frameSize));
                var frameImg = frameGo.AddComponent<Image>();
                frameImg.color = frameColors[i];
                frameImg.raycastTarget = false;

                // === 头像图片（内圈） ===
                var avatarGo = CreatePanel(frameGo.transform, $"Top3Avatar_{i}",
                    Vector2.zero, new Vector2(avatarSize, avatarSize));
                var avatarImg = avatarGo.AddComponent<Image>();
                avatarImg.color = avatarBgColors[i];
                avatarImg.raycastTarget = false;

                // === 头像中间的排名大字 ===
                var avatarNumTMP = CreateText(avatarGo.transform, "RankNum", top3Numbers[i].ToString(),
                    Vector2.zero, 36, Color.white);
                var avatarNumRT = avatarNumTMP.GetComponent<RectTransform>();
                avatarNumRT.sizeDelta = new Vector2(avatarSize, avatarSize);
                avatarNumTMP.fontStyle = FontStyles.Bold;
                avatarNumTMP.outlineWidth = 0.3f;
                avatarNumTMP.outlineColor = new Color32(0, 0, 0, 200);

                // 名字
                var nameTMP = CreateText(panel.transform, $"Top3Name_{i}", top3DefaultNames[i],
                    top3NamePos[i], 26, Color.white);
                var nameRT = nameTMP.GetComponent<RectTransform>();
                nameRT.sizeDelta = new Vector2(280, 40);
                nameTMP.fontStyle = FontStyles.Bold;

                // 贡献值（scale 1.44 放大）
                var scoreTMP = CreateText(panel.transform, $"Top3Score_{i}", top3DefaultScores[i],
                    top3ScorePos[i], 18, new Color(1f, 0.9f, 0.6f));
                var scoreRT = scoreTMP.GetComponent<RectTransform>();
                scoreRT.sizeDelta = new Vector2(280, 30);
                scoreTMP.transform.localScale = Vector3.one * 1.44f;
            }

            // ---- 列表底框 ----
            var listBgSprite = LoadSprite("Assets/Art/UI/Ranking/ranking_list_bg.png");
            var listArea = CreatePanel(panel.transform, "ListArea", new Vector2(0, -349), new Vector2(950, 680));
            var listBgImg = listArea.AddComponent<Image>();
            if (listBgSprite != null)
            {
                listBgImg.sprite = listBgSprite;
                listBgImg.type = Image.Type.Sliced;
            }
            else
            {
                listBgImg.color = new Color(0.25f, 0.15f, 0.08f, 0.95f);
            }
            listBgImg.raycastTarget = false;

            // ---- 排名列表项（4-10 名）----
            var itemBgSprite = LoadSprite("Assets/Art/UI/Ranking/ranking_item_bg.png");
            string[] defaultNames = { "暖暖的家", "蒸汽使者", "乐乐豚", "温泉旅人", "幸福汤", "悠闲时光", "月光浴" };
            string[] defaultScores = { "贡献值 5,000,000", "贡献值 4,800,000", "贡献值 4,500,000",
                "贡献值 4,200,000", "贡献值 4,000,000", "贡献值 3,800,000", "贡献值 3,500,000" };

            float itemStartY = 260f;
            float itemHeight = 80f;

            for (int i = 0; i < 7; i++)
            {
                float y = itemStartY - i * itemHeight;
                var itemGo = CreatePanel(listArea.transform, $"RankItem_{i}",
                    new Vector2(0, y), new Vector2(880, 70));

                // 条目底图
                var itemImg = itemGo.AddComponent<Image>();
                if (itemBgSprite != null)
                {
                    itemImg.sprite = itemBgSprite;
                    itemImg.type = Image.Type.Sliced;
                }
                else
                {
                    itemImg.color = new Color(0.6f, 0.5f, 0.3f, 0.7f);
                }
                itemImg.raycastTarget = false;

                // 排名数字
                var rankNumTMP = CreateText(itemGo.transform, "RankNum", (i + 4).ToString(),
                    new Vector2(-380, 0), 28, Color.white);
                var rankNumRT = rankNumTMP.GetComponent<RectTransform>();
                rankNumRT.sizeDelta = new Vector2(60, 50);
                rankNumTMP.fontStyle = FontStyles.Bold;

                // 玩家名
                var nameTMP = CreateText(itemGo.transform, "Name", defaultNames[i],
                    new Vector2(-150, 0), 26, Color.white);
                var nameRT = nameTMP.GetComponent<RectTransform>();
                nameRT.sizeDelta = new Vector2(300, 40);
                nameTMP.alignment = TextAlignmentOptions.Left;

                // 贡献值
                var scoreTMP = CreateText(itemGo.transform, "Score", defaultScores[i],
                    new Vector2(250, 0), 22, new Color(1f, 0.9f, 0.6f));
                var scoreRT = scoreTMP.GetComponent<RectTransform>();
                scoreRT.sizeDelta = new Vector2(300, 40);
                scoreTMP.alignment = TextAlignmentOptions.Right;
            }

            // ---- 关闭按钮（底部）----
            var closeBtnSprite = LoadSprite("Assets/Art/UI/Ranking/btn_close.png");
            var closeGo = CreatePanel(panel.transform, "BtnClose", new Vector2(0, -751), new Vector2(300, 80));
            var closeImg = closeGo.AddComponent<Image>();
            if (closeBtnSprite != null)
            {
                closeImg.sprite = closeBtnSprite;
                closeImg.preserveAspect = true;
            }
            else
            {
                closeImg.color = new Color(0.8f, 0.4f, 0.1f);
            }
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;

            // 关闭文字
            var closeTMP = CreateText(closeGo.transform, "Text", "关闭",
                Vector2.zero, 30, Color.white);
            var closeTextRT = closeTMP.GetComponent<RectTransform>();
            closeTextRT.anchorMin = Vector2.zero;
            closeTextRT.anchorMax = Vector2.one;
            closeTextRT.offsetMin = Vector2.zero;
            closeTextRT.offsetMax = Vector2.zero;
            closeTextRT.anchoredPosition = Vector2.zero;
            closeTextRT.sizeDelta = Vector2.zero;

            // ---- 重置时间说明 ----
            var resetTimeTMP = CreateText(panel.transform, "ResetTimeText", "每周日0点重置榜单",
                new Vector2(0, -820), 22, new Color(0.85f, 0.78f, 0.6f));
            var resetTimeRT = resetTimeTMP.GetComponent<RectTransform>();
            resetTimeRT.sizeDelta = new Vector2(600, 40);

            return panel;
        }

        /// <summary>为 RankingPanel 挂载 RankingPanelUI 脚本并连线</summary>
        static void WireRankingPanel(GameObject panel)
        {
            var ui = panel.AddComponent<RankingPanelUI>();

            // Tab 按钮和图片
            ui.tabButtons = new Button[4];
            ui.tabImages = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var tabGo = panel.transform.Find($"Tab_{i}")?.gameObject;
                if (tabGo != null)
                {
                    ui.tabButtons[i] = tabGo.GetComponent<Button>();
                    ui.tabImages[i] = tabGo.GetComponent<Image>();
                }
            }

            // Tab sprites
            ui.tabNormalSprite = LoadSprite("Assets/Art/UI/Ranking/tab_normal.png");
            ui.tabSelectedSprite = LoadSprite("Assets/Art/UI/Ranking/tab_selected.png");

            // Top3
            ui.top3Names = new TextMeshProUGUI[3];
            ui.top3Scores = new TextMeshProUGUI[3];
            ui.top3Avatars = new Image[3];
            ui.top3AvatarFrames = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                ui.top3Names[i] = panel.transform.Find($"Top3Name_{i}")?.GetComponent<TextMeshProUGUI>();
                ui.top3Scores[i] = panel.transform.Find($"Top3Score_{i}")?.GetComponent<TextMeshProUGUI>();
                var frameTransform = panel.transform.Find($"Top3AvatarFrame_{i}");
                ui.top3AvatarFrames[i] = frameTransform?.GetComponent<Image>();
                ui.top3Avatars[i] = frameTransform?.Find($"Top3Avatar_{i}")?.GetComponent<Image>();
            }

            // 排名列表 (4-10)
            var listArea = panel.transform.Find("ListArea");
            ui.rankNumbers = new TextMeshProUGUI[7];
            ui.rankNames = new TextMeshProUGUI[7];
            ui.rankScores = new TextMeshProUGUI[7];
            if (listArea != null)
            {
                for (int i = 0; i < 7; i++)
                {
                    var item = listArea.Find($"RankItem_{i}");
                    if (item != null)
                    {
                        ui.rankNumbers[i] = item.Find("RankNum")?.GetComponent<TextMeshProUGUI>();
                        ui.rankNames[i] = item.Find("Name")?.GetComponent<TextMeshProUGUI>();
                        ui.rankScores[i] = item.Find("Score")?.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            // 关闭按钮
            ui.btnClose = panel.transform.Find("BtnClose")?.GetComponent<Button>();

            ui.resetTimeText = panel.transform.Find("ResetTimeText")?.GetComponent<TextMeshProUGUI>();
        }

        // ==================== 对局界面UI ====================
        static GameObject CreateGameUI(Transform canvasTransform)
        {
            var gameUI = CreatePanel(canvasTransform, "GameUIPanel", Vector2.zero, Vector2.zero);
            var gameUIRT = gameUI.GetComponent<RectTransform>();
            gameUIRT.anchorMin = Vector2.zero;
            gameUIRT.anchorMax = Vector2.one;
            gameUIRT.offsetMin = Vector2.zero;
            gameUIRT.offsetMax = Vector2.zero;

            // ========== TopBar（顶部拉力条+倒计时+积分池）==========
            var topBar = CreatePanel(gameUI.transform, "TopBar", Vector2.zero, Vector2.zero);
            var topBarRT = topBar.GetComponent<RectTransform>();
            topBarRT.anchorMin = new Vector2(0, 1);
            topBarRT.anchorMax = new Vector2(1, 1);
            topBarRT.pivot = new Vector2(0.5f, 1f);
            topBarRT.anchoredPosition = new Vector2(0, -60f); // 下移60px，给直播平台UI留空间
            topBarRT.sizeDelta = new Vector2(0, 400);

            // -- 顶部底图（拉力条底图+橘子徽章+倒计时小框）--
            var topBarBgSprite = LoadSprite("Assets/Art/UI/Battle/top_bar_bg.png");
            var topBarBg = CreatePanel(topBar.transform, "TopBarBg", Vector2.zero, Vector2.zero);
            var topBarBgRT = topBarBg.GetComponent<RectTransform>();
            topBarBgRT.anchorMin = new Vector2(0, 1);
            topBarBgRT.anchorMax = new Vector2(1, 1);
            topBarBgRT.pivot = new Vector2(0.5f, 1f);
            topBarBgRT.anchoredPosition = Vector2.zero;
            topBarBgRT.sizeDelta = new Vector2(0, 368); // 1080/1456*496≈368
            var topBarBgImg = topBarBg.AddComponent<Image>();
            if (topBarBgSprite != null)
            {
                topBarBgImg.sprite = topBarBgSprite;
                topBarBgImg.type = Image.Type.Simple;
                topBarBgImg.preserveAspect = false;
            }
            else
            {
                topBarBgImg.color = new Color(0, 0, 0, 0.65f);
            }
            topBarBgImg.raycastTarget = false;

            // -- 左侧拉力条填充（在底图金属凹槽内）--
            var barLeft = CreatePanel(topBarBg.transform, "ProgressBarLeft", Vector2.zero, Vector2.zero);
            var imgLeft = barLeft.AddComponent<Image>();
            imgLeft.color = COL_LEFT;
            imgLeft.type = Image.Type.Filled;
            imgLeft.fillMethod = Image.FillMethod.Horizontal;
            imgLeft.fillOrigin = 1; // 从右向左（从橘子向左端延伸）
            imgLeft.fillAmount = 0f;
            var rtLeft = barLeft.GetComponent<RectTransform>();
            rtLeft.anchorMin = new Vector2(0.03f, 0.38f);
            rtLeft.anchorMax = new Vector2(0.37f, 0.55f);
            rtLeft.offsetMin = Vector2.zero;
            rtLeft.offsetMax = Vector2.zero;

            // -- 右侧拉力条填充（镜像对称）--
            var barRight = CreatePanel(topBarBg.transform, "ProgressBarRight", Vector2.zero, Vector2.zero);
            var imgRight = barRight.AddComponent<Image>();
            imgRight.color = COL_RIGHT;
            imgRight.type = Image.Type.Filled;
            imgRight.fillMethod = Image.FillMethod.Horizontal;
            imgRight.fillOrigin = 0; // 从左向右（从橘子向右端延伸）
            imgRight.fillAmount = 0f;
            var rtRight = barRight.GetComponent<RectTransform>();
            rtRight.anchorMin = new Vector2(0.63f, 0.38f);
            rtRight.anchorMax = new Vector2(0.97f, 0.55f);
            rtRight.offsetMin = Vector2.zero;
            rtRight.offsetMax = Vector2.zero;

            // -- 距离标记（拉力条上的米制标记）--
            var leftEndMarker = CreateText(topBarBg.transform, "LeftEndMarker", "← 柚子终点",
                Vector2.zero, 14, Color.white, false);
            var lemRT = leftEndMarker.GetComponent<RectTransform>();
            lemRT.anchorMin = new Vector2(0.02f, 0.56f);
            lemRT.anchorMax = new Vector2(0.28f, 0.70f);
            lemRT.offsetMin = Vector2.zero;
            lemRT.offsetMax = Vector2.zero;
            lemRT.sizeDelta = Vector2.zero;

            var centerMarker = CreateText(topBarBg.transform, "CenterMarker", "0.0m",
                Vector2.zero, 16, Color.white, false);
            var cmRT = centerMarker.GetComponent<RectTransform>();
            cmRT.anchorMin = new Vector2(0.40f, 0.56f);
            cmRT.anchorMax = new Vector2(0.60f, 0.70f);
            cmRT.offsetMin = Vector2.zero;
            cmRT.offsetMax = Vector2.zero;
            cmRT.sizeDelta = Vector2.zero;

            var rightEndMarker = CreateText(topBarBg.transform, "RightEndMarker", "香橙终点 →",
                Vector2.zero, 14, Color.white, false);
            var remRT = rightEndMarker.GetComponent<RectTransform>();
            remRT.anchorMin = new Vector2(0.72f, 0.56f);
            remRT.anchorMax = new Vector2(0.98f, 0.70f);
            remRT.offsetMin = Vector2.zero;
            remRT.offsetMax = Vector2.zero;
            remRT.sizeDelta = Vector2.zero;

            // 当前距离指示（进度条中间下方）
            var posIndicator = CreateText(topBarBg.transform, "PosIndicator", "0.0m",
                Vector2.zero, 20, COL_GOLD, false);
            var piRT = posIndicator.GetComponent<RectTransform>();
            piRT.anchorMin = new Vector2(0.38f, 0.20f);
            piRT.anchorMax = new Vector2(0.62f, 0.37f);
            piRT.offsetMin = Vector2.zero;
            piRT.offsetMax = Vector2.zero;
            piRT.sizeDelta = Vector2.zero;

            // -- 倒计时（橘子下方棕色小框位置）--
            var timerText = CreateText(topBar.transform, "TimerText", "10:00",
                new Vector2(0, -200), 48, Color.white);
            timerText.fontStyle = TMPro.FontStyles.Bold;
            var timerRT = timerText.GetComponent<RectTransform>();
            timerRT.sizeDelta = new Vector2(260, 65);

            // -- 积分池（倒计时下方）--
            var scorePoolText = CreateText(topBar.transform, "ScorePoolText", "积分池 0",
                new Vector2(0, -260), 28, Color.white);
            var spRT = scorePoolText.GetComponent<RectTransform>();
            spRT.sizeDelta = new Vector2(300, 45);

            // -- 推力增量文字（橘子左右上方浮动）--
            var leftForceText = CreateText(topBar.transform, "LeftForceText", "",
                new Vector2(-220, -80), 32, COL_LEFT);
            leftForceText.fontStyle = TMPro.FontStyles.Bold;
            leftForceText.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 55);

            var rightForceText = CreateText(topBar.transform, "RightForceText", "",
                new Vector2(220, -80), 32, COL_RIGHT);
            rightForceText.fontStyle = TMPro.FontStyles.Bold;
            rightForceText.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 55);

            // ========== 左侧阵营面板 ==========
            var leftPanelSprite = LoadSprite("Assets/Art/UI/Battle/panel_left_camp.png");
            var crownSprite = LoadSprite("Assets/Art/UI/Icons/crown_gold.png");

            var leftList = CreatePanel(gameUI.transform, "LeftPlayerList", Vector2.zero, Vector2.zero);
            var leftListRT = leftList.GetComponent<RectTransform>();
            leftListRT.anchorMin = new Vector2(0.01f, 0.02f);
            leftListRT.anchorMax = new Vector2(0.48f, 0.22f);
            leftListRT.offsetMin = Vector2.zero;
            leftListRT.offsetMax = Vector2.zero;
            var leftBgImg = leftList.AddComponent<Image>();
            if (leftPanelSprite != null)
            {
                leftBgImg.sprite = leftPanelSprite;
                leftBgImg.type = Image.Type.Simple;
                leftBgImg.preserveAspect = true;
            }
            else
            {
                leftBgImg.color = new Color(0.4f, 0.25f, 0.12f, 0.9f);
            }
            leftBgImg.raycastTarget = false;

            // 左侧标题
            var leftTitleGo = CreateText(leftList.transform, "LeftListTitle", "香橙温泉",
                Vector2.zero, 32, Color.white);
            leftTitleGo.fontStyle = TMPro.FontStyles.Bold;
            var ltRT = leftTitleGo.GetComponent<RectTransform>();
            ltRT.anchorMin = new Vector2(0.15f, 0.82f);
            ltRT.anchorMax = new Vector2(0.85f, 0.97f);
            ltRT.offsetMin = Vector2.zero;
            ltRT.offsetMax = Vector2.zero;
            ltRT.anchoredPosition = Vector2.zero;

            // 左侧3行玩家
            CreatePlayerRows(leftList.transform, crownSprite, 3);

            // ========== 右侧阵营面板（镜像）==========
            var rightPanelSprite = LoadSprite("Assets/Art/UI/Battle/panel_right_camp.png");

            var rightList = CreatePanel(gameUI.transform, "RightPlayerList", Vector2.zero, Vector2.zero);
            var rightListRT = rightList.GetComponent<RectTransform>();
            rightListRT.anchorMin = new Vector2(0.52f, 0.02f);
            rightListRT.anchorMax = new Vector2(0.99f, 0.22f);
            rightListRT.offsetMin = Vector2.zero;
            rightListRT.offsetMax = Vector2.zero;
            var rightBgImg = rightList.AddComponent<Image>();
            if (rightPanelSprite != null)
            {
                rightBgImg.sprite = rightPanelSprite;
                rightBgImg.type = Image.Type.Simple;
                rightBgImg.preserveAspect = true;
            }
            else
            {
                rightBgImg.color = new Color(0.4f, 0.25f, 0.12f, 0.9f);
            }
            rightBgImg.raycastTarget = false;

            // 右侧标题
            var rightTitleGo = CreateText(rightList.transform, "RightListTitle", "柚子温泉",
                Vector2.zero, 32, Color.white);
            rightTitleGo.fontStyle = TMPro.FontStyles.Bold;
            var rtTitleRT = rightTitleGo.GetComponent<RectTransform>();
            rtTitleRT.anchorMin = new Vector2(0.15f, 0.82f);
            rtTitleRT.anchorMax = new Vector2(0.85f, 0.97f);
            rtTitleRT.offsetMin = Vector2.zero;
            rtTitleRT.offsetMax = Vector2.zero;
            rtTitleRT.anchoredPosition = Vector2.zero;

            // 右侧3行玩家
            CreatePlayerRows(rightList.transform, crownSprite, 3);

            // ========== 礼物通知 ==========
            var giftNotif = CreatePanel(gameUI.transform, "GiftNotification", Vector2.zero, Vector2.zero);
            var giftNotifRT = giftNotif.GetComponent<RectTransform>();
            giftNotifRT.anchorMin = new Vector2(0.1f, 0.22f);
            giftNotifRT.anchorMax = new Vector2(0.9f, 0.28f);
            giftNotifRT.offsetMin = Vector2.zero;
            giftNotifRT.offsetMax = Vector2.zero;

            // ========== 玩家加入通知（居中偏上，独立容器）==========
            var joinNotif = CreatePanel(gameUI.transform, "JoinNotification", Vector2.zero, Vector2.zero);
            var joinNotifRT = joinNotif.GetComponent<RectTransform>();
            joinNotifRT.anchorMin = new Vector2(0.15f, 0.55f);
            joinNotifRT.anchorMax = new Vector2(0.85f, 0.65f);
            joinNotifRT.offsetMin = Vector2.zero;
            joinNotifRT.offsetMax = Vector2.zero;

            // ========== VIP欢迎横幅（全屏覆盖层）==========
            var vipPanel = CreatePanel(gameUI.transform, "VIPAnnouncement", Vector2.zero, Vector2.zero);
            var vipRT = vipPanel.GetComponent<RectTransform>();
            vipRT.anchorMin = Vector2.zero;
            vipRT.anchorMax = Vector2.one;
            vipRT.offsetMin = Vector2.zero;
            vipRT.offsetMax = Vector2.zero;

            // VIP背景遮罩
            var vipOverlay = vipPanel.AddComponent<Image>();
            vipOverlay.color = new Color(0, 0, 0, 0.5f);
            vipOverlay.raycastTarget = false;

            // VIP文字
            var vipText = CreateText(vipPanel.transform, "VIPText", "",
                Vector2.zero, 48, COL_GOLD);
            vipText.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 300);
            vipText.alignment = TMPro.TextAlignmentOptions.Center;
            vipText.enableWordWrapping = true;
            vipText.fontStyle = TMPro.FontStyles.Bold;

            // CanvasGroup for fade
            var vipCG = vipPanel.AddComponent<CanvasGroup>();
            vipCG.alpha = 0;
            vipCG.blocksRaycasts = false;
            vipCG.interactable = false;

            // 初始状态隐藏对局UI
            gameUI.SetActive(false);

            return gameUI;
        }

        /// <summary>在阵营面板内创建玩家行（皇冠+名字）</summary>
        static void CreatePlayerRows(Transform parent, Sprite crownSprite, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float yTop = 0.72f - i * 0.22f;
                float yBottom = yTop - 0.18f;

                var row = CreatePanel(parent, $"PlayerRow_{i}", Vector2.zero, Vector2.zero);
                var rowRT = row.GetComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0.08f, yBottom);
                rowRT.anchorMax = new Vector2(0.92f, yTop);
                rowRT.offsetMin = Vector2.zero;
                rowRT.offsetMax = Vector2.zero;

                // 皇冠图标
                var crownGo = CreatePanel(row.transform, "Crown", Vector2.zero, Vector2.zero);
                var crownRT = crownGo.GetComponent<RectTransform>();
                crownRT.anchorMin = new Vector2(0, 0.1f);
                crownRT.anchorMax = new Vector2(0.15f, 0.9f);
                crownRT.offsetMin = Vector2.zero;
                crownRT.offsetMax = Vector2.zero;
                var crownImg = crownGo.AddComponent<Image>();
                if (crownSprite != null) { crownImg.sprite = crownSprite; crownImg.preserveAspect = true; }
                else { crownImg.color = COL_GOLD; }
                crownImg.raycastTarget = false;

                // 玩家名文本
                var playerText = CreateText(row.transform, "PlayerName", "",
                    Vector2.zero, 26, Color.white);
                playerText.alignment = TMPro.TextAlignmentOptions.Left;
                var ptRT = playerText.GetComponent<RectTransform>();
                ptRT.anchorMin = new Vector2(0.18f, 0f);
                ptRT.anchorMax = new Vector2(1f, 1f);
                ptRT.offsetMin = Vector2.zero;
                ptRT.offsetMax = Vector2.zero;
                ptRT.anchoredPosition = Vector2.zero;
            }
        }

        // ==================== 结算界面UI ====================
        static GameObject CreateSettlementUI(Transform canvasTransform)
        {
            // 复制结算美术资源
            CopySettlementArtAssets();

            var settlement = CreatePanel(canvasTransform, "SettlementPanel", Vector2.zero, Vector2.zero);
            var settRT = settlement.GetComponent<RectTransform>();
            settRT.anchorMin = Vector2.zero;
            settRT.anchorMax = Vector2.one;
            settRT.offsetMin = Vector2.zero;
            settRT.offsetMax = Vector2.zero;

            // ===== 背景图（全屏铺满）=====
            var bgSprite = LoadSprite("Assets/Art/UI/Settlement/settlement_bg.png");
            var settBg = settlement.AddComponent<Image>();
            if (bgSprite != null)
            {
                settBg.sprite = bgSprite;
                settBg.type = Image.Type.Simple;
                settBg.preserveAspect = false;
                settBg.color = Color.white;
            }
            else
            {
                settBg.color = COL_DARK_BG;
            }

            // ============================================================
            // 坐标说明（Canvas 1080x1920, pivot center, y=0=屏幕中心）
            // 基于用户手动调整后的 ui_layout_snapshot.md 精确坐标：
            //   WinnerText:     y = +880
            //   MVPArea:        y = +362  (size 500x420)
            //   双列排行面板:   y = -230  (各 480x640)
            //   积分瓜分区:     y = -650  (size 960x200)
            //   返回按钮:       y = -849
            // ============================================================

            // ===== 胜利标题 (皇冠上方，屏幕顶部) =====
            var winnerText = CreateText(settlement.transform, "WinnerText", "香橙温泉 胜利!",
                new Vector2(0, 880), 52, COL_GOLD);
            winnerText.fontStyle = TMPro.FontStyles.Bold;
            winnerText.enableWordWrapping = false;
            winnerText.outlineWidth = 0.3f;
            winnerText.outlineColor = new Color32(100, 50, 0, 255);
            var winnerRT = winnerText.GetComponent<RectTransform>();
            winnerRT.sizeDelta = new Vector2(800, 70);

            // ===== MVP区域 =====
            var mvpArea = CreatePanel(settlement.transform, "MVPArea",
                new Vector2(0, 362), new Vector2(500, 420));

            // MVP头像底图（水豚王冠）
            var mvpFrameSprite = LoadSprite("Assets/Art/UI/Settlement/mvp_avatar_frame.png");
            var mvpAvatarFrame = CreatePanel(mvpArea.transform, "MVPAvatarFrame",
                new Vector2(0, 100), new Vector2(260, 260));
            var frameImg = mvpAvatarFrame.AddComponent<Image>();
            if (mvpFrameSprite != null)
            {
                frameImg.sprite = mvpFrameSprite;
                frameImg.preserveAspect = true;
            }
            else
            {
                frameImg.color = new Color(0.8f, 0.6f, 0.2f, 0.8f);
            }
            frameImg.raycastTarget = false;

            // MVP头像占位（透明，未来加载真实头像）
            var mvpAvatar = CreatePanel(mvpAvatarFrame.transform, "MVPAvatar",
                new Vector2(0, -10), new Vector2(140, 140));
            var avatarImg = mvpAvatar.AddComponent<Image>();
            avatarImg.color = new Color(0.3f, 0.3f, 0.3f, 0f);

            // MVP铭牌底条
            var mvpBarSprite = LoadSprite("Assets/Art/UI/Settlement/mvp_name_bar.png");
            var mvpBar = CreatePanel(mvpArea.transform, "MVPNameBar",
                new Vector2(0, -55), new Vector2(460, 55));
            var barImg = mvpBar.AddComponent<Image>();
            if (mvpBarSprite != null)
            {
                barImg.sprite = mvpBarSprite;
                barImg.preserveAspect = true;
            }
            else
            {
                barImg.color = new Color(0.8f, 0.5f, 0.1f, 0.9f);
            }
            barImg.raycastTarget = false;

            // MVP名字（居中在铭牌上）
            var mvpName = CreateText(mvpBar.transform, "MVPName", "",
                Vector2.zero, 30, Color.white);
            var mvpNameRT = mvpName.GetComponent<RectTransform>();
            mvpNameRT.anchorMin = Vector2.zero;
            mvpNameRT.anchorMax = Vector2.one;
            mvpNameRT.offsetMin = new Vector2(10, 2);
            mvpNameRT.offsetMax = new Vector2(-10, -2);
            mvpName.alignment = TextAlignmentOptions.Center;
            mvpName.fontStyle = FontStyles.Bold;
            mvpName.outlineWidth = 0.2f;
            mvpName.outlineColor = new Color32(60, 30, 0, 200);

            // "MVP"标签
            var mvpLabel = CreateText(mvpArea.transform, "MVPLabel", "MVP",
                new Vector2(0, -100), 44, COL_GOLD);
            mvpLabel.fontStyle = TMPro.FontStyles.Bold;
            mvpLabel.outlineWidth = 0.25f;
            mvpLabel.outlineColor = new Color32(80, 40, 0, 220);
            mvpLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 55);

            // 总贡献
            var mvpContrib = CreateText(mvpArea.transform, "MVPContribution", "总贡献 0",
                new Vector2(0, -121), 28, new Color(1f, 0.95f, 0.8f));
            mvpContrib.fontStyle = TMPro.FontStyles.Bold;
            mvpContrib.outlineWidth = 0.15f;
            mvpContrib.outlineColor = new Color32(40, 20, 0, 160);
            mvpContrib.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 40);

            // ===== 双列排行榜 =====
            // 排行面板中心 y = -230（基于 ui_layout_snapshot）
            float rankPanelCenterY = -230f;
            float rankPanelW = 480f;
            float rankPanelH = 640f;

            // --- 左阵营 ---
            var leftListSprite = LoadSprite("Assets/Art/UI/Settlement/rank_list_left.png");
            var leftRankCol = CreatePanel(settlement.transform, "LeftRankColumn",
                new Vector2(-265, rankPanelCenterY), new Vector2(rankPanelW, rankPanelH));
            var leftBgImg = leftRankCol.AddComponent<Image>();
            if (leftListSprite != null)
            {
                leftBgImg.sprite = leftListSprite;
                leftBgImg.type = Image.Type.Simple;
                leftBgImg.preserveAspect = true;
            }
            else
            {
                leftBgImg.color = new Color(0.5f, 0.4f, 0.15f, 0.9f);
            }
            leftBgImg.raycastTarget = false;

            // 左标题（卷轴区域）
            var leftTitle = CreateText(leftRankCol.transform, "LeftRankTitle", "香橙温泉 贡献榜",
                new Vector2(0, 285), 22, COL_GOLD);
            leftTitle.fontStyle = TMPro.FontStyles.Bold;
            leftTitle.outlineWidth = 0.2f;
            leftTitle.outlineColor = new Color32(60, 30, 0, 200);
            leftTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 36);

            // 左排行10行（每行双TMP）
            // 美术资源自带前3名序号图标（金银铜底条），前3行间距60，4-10名间距48
            // 效果图：名字左对齐避开序号图标，贡献值右对齐到面板右侧
            for (int i = 0; i < 10; i++)
            {
                // 前3名行间距60（底条更高），4-10名行间距48
                float y;
                if (i < 3)
                    y = 240 - i * 60;        // 240, 180, 120
                else
                    y = 120 - 60 - (i - 3) * 48; // 60, 12, -36, -84, -132, -180, -228

                Color rowColor = i < 3 ? COL_GOLD : Color.white;
                int fontSize = i < 3 ? 22 : 20;
                // 前3名有序号图标(约100px)，4-10名文字序号(约50px)
                float nameX = i < 3 ? -130f : -190f;

                // 名字TMP（左对齐，pivot左）
                var nameTMP = CreateText(leftRankCol.transform, $"LeftRankName_{i}",
                    "", new Vector2(nameX, y), fontSize, rowColor, false);
                nameTMP.alignment = TMPro.TextAlignmentOptions.Left;
                nameTMP.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                nameTMP.outlineWidth = 0.2f;
                nameTMP.outlineColor = new Color32(40, 20, 0, 180);
                var nameRT = nameTMP.GetComponent<RectTransform>();
                nameRT.pivot = new Vector2(0f, 0.5f);
                nameRT.sizeDelta = new Vector2(200, 38);

                // 贡献值TMP（右对齐，pivot右，贴面板右边缘）
                var valTMP = CreateText(leftRankCol.transform, $"LeftRankVal_{i}",
                    "", new Vector2(230, y), fontSize, rowColor, false);
                valTMP.alignment = TMPro.TextAlignmentOptions.Right;
                valTMP.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                valTMP.outlineWidth = 0.2f;
                valTMP.outlineColor = new Color32(40, 20, 0, 180);
                var valRT = valTMP.GetComponent<RectTransform>();
                valRT.pivot = new Vector2(1f, 0.5f);
                valRT.sizeDelta = new Vector2(160, 38);
            }

            // --- 右阵营 ---
            var rightListSprite = LoadSprite("Assets/Art/UI/Settlement/rank_list_right.png");
            var rightRankCol = CreatePanel(settlement.transform, "RightRankColumn",
                new Vector2(265, rankPanelCenterY), new Vector2(rankPanelW, rankPanelH));
            var rightBgImg = rightRankCol.AddComponent<Image>();
            if (rightListSprite != null)
            {
                rightBgImg.sprite = rightListSprite;
                rightBgImg.type = Image.Type.Simple;
                rightBgImg.preserveAspect = true;
            }
            else
            {
                rightBgImg.color = new Color(0.4f, 0.3f, 0.12f, 0.9f);
            }
            rightBgImg.raycastTarget = false;

            // 右标题
            var rightTitle = CreateText(rightRankCol.transform, "RightRankTitle", "柚子温泉 贡献榜",
                new Vector2(0, 285), 22, COL_GOLD);
            rightTitle.fontStyle = TMPro.FontStyles.Bold;
            rightTitle.outlineWidth = 0.2f;
            rightTitle.outlineColor = new Color32(60, 30, 0, 200);
            rightTitle.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 36);

            // 右排行10行（与左侧对称）
            for (int i = 0; i < 10; i++)
            {
                float y;
                if (i < 3)
                    y = 240 - i * 60;
                else
                    y = 120 - 60 - (i - 3) * 48;

                Color rowColor = i < 3 ? COL_GOLD : Color.white;
                int fontSize = i < 3 ? 22 : 20;
                float nameX = i < 3 ? -130f : -190f;

                var nameTMP = CreateText(rightRankCol.transform, $"RightRankName_{i}",
                    "", new Vector2(nameX, y), fontSize, rowColor, false);
                nameTMP.alignment = TMPro.TextAlignmentOptions.Left;
                nameTMP.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                nameTMP.outlineWidth = 0.2f;
                nameTMP.outlineColor = new Color32(40, 20, 0, 180);
                var nameRT = nameTMP.GetComponent<RectTransform>();
                nameRT.pivot = new Vector2(0f, 0.5f);
                nameRT.sizeDelta = new Vector2(200, 38);

                var valTMP = CreateText(rightRankCol.transform, $"RightRankVal_{i}",
                    "", new Vector2(230, y), fontSize, rowColor, false);
                valTMP.alignment = TMPro.TextAlignmentOptions.Right;
                valTMP.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                valTMP.outlineWidth = 0.2f;
                valTMP.outlineColor = new Color32(40, 20, 0, 180);
                var valRT = valTMP.GetComponent<RectTransform>();
                valRT.pivot = new Vector2(1f, 0.5f);
                valRT.sizeDelta = new Vector2(160, 38);
            }

            // ===== 积分瓜分区域 =====
            var scoreDistArea = CreatePanel(settlement.transform, "ScoreDistArea",
                new Vector2(0, -650), new Vector2(960, 200));

            // 积分瓜分底条
            var scoreBarSprite = LoadSprite("Assets/Art/UI/Settlement/score_dist_bar.png");
            if (scoreBarSprite != null)
            {
                var scoreBarImg = scoreDistArea.AddComponent<Image>();
                scoreBarImg.sprite = scoreBarSprite;
                scoreBarImg.type = Image.Type.Sliced;
                scoreBarImg.raycastTarget = false;
            }

            // 积分瓜分标题
            var scoreLabel = CreateText(scoreDistArea.transform, "ScorePoolLabel", "积分瓜分",
                new Vector2(0, 78), 26, COL_GOLD);
            scoreLabel.fontStyle = TMPro.FontStyles.Bold;
            scoreLabel.outlineWidth = 0.2f;
            scoreLabel.outlineColor = new Color32(60, 30, 0, 200);
            scoreLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 38);

            // 6条积分分配（2列×3行，每条双TMP）
            // 面板宽960，左列中心=-240，右列中心=+240
            for (int i = 0; i < 6; i++)
            {
                int col = i / 3; // 0=左列, 1=右列
                int row = i % 3;
                float colCenter = col == 0 ? -240f : 240f;
                float y = 35 - row * 38;
                Color c = (i == 0 || i == 3) ? COL_GOLD : new Color(1f, 0.92f, 0.7f);

                // 名字TMP（左对齐，pivot左）
                var distName = CreateText(scoreDistArea.transform, $"ScoreDistName_{i}",
                    "", new Vector2(colCenter - 200, y), 20, c, false);
                distName.alignment = TMPro.TextAlignmentOptions.Left;
                distName.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                distName.outlineWidth = 0.15f;
                distName.outlineColor = new Color32(40, 20, 0, 160);
                var distNameRT = distName.GetComponent<RectTransform>();
                distNameRT.pivot = new Vector2(0f, 0.5f);
                distNameRT.sizeDelta = new Vector2(260, 32);

                // 金币TMP（右对齐，pivot右）
                var distVal = CreateText(scoreDistArea.transform, $"ScoreDistVal_{i}",
                    "", new Vector2(colCenter + 200, y), 20, c, false);
                distVal.alignment = TMPro.TextAlignmentOptions.Right;
                distVal.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                distVal.outlineWidth = 0.15f;
                distVal.outlineColor = new Color32(40, 20, 0, 160);
                var distValRT = distVal.GetComponent<RectTransform>();
                distValRT.pivot = new Vector2(1f, 0.5f);
                distValRT.sizeDelta = new Vector2(150, 32);
            }

            // ===== 返回按钮（使用美术图，已含"返回"文字）=====
            var btnBackSprite = LoadSprite("Assets/Art/UI/Settlement/btn_back.png");
            var btnRestartGo = new GameObject("BtnRestart", typeof(RectTransform));
            btnRestartGo.transform.SetParent(settlement.transform, false);
            var btnRT = btnRestartGo.GetComponent<RectTransform>();
            btnRT.anchoredPosition = new Vector2(0, -849);
            btnRT.sizeDelta = new Vector2(300, 80);
            var btnImg = btnRestartGo.AddComponent<Image>();
            if (btnBackSprite != null)
            {
                btnImg.sprite = btnBackSprite;
                btnImg.preserveAspect = true;
            }
            else
            {
                btnImg.color = new Color(0.85f, 0.55f, 0.1f, 0.9f);
                // fallback: 添加文字
                var btnTextGo = new GameObject("Text", typeof(RectTransform));
                btnTextGo.transform.SetParent(btnRestartGo.transform, false);
                var btnTextRT = btnTextGo.GetComponent<RectTransform>();
                btnTextRT.anchorMin = Vector2.zero;
                btnTextRT.anchorMax = Vector2.one;
                btnTextRT.offsetMin = new Vector2(4, 4);
                btnTextRT.offsetMax = new Vector2(-4, -4);
                var btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
                btnTmp.text = "返回";
                btnTmp.fontSize = 30;
                btnTmp.color = Color.white;
                btnTmp.alignment = TextAlignmentOptions.Center;
                if (_chineseFont != null) btnTmp.font = _chineseFont;
            }
            var btnComp = btnRestartGo.AddComponent<Button>();
            btnComp.targetGraphic = btnImg;

            settlement.SetActive(false);
            return settlement;
        }

        /// <summary>复制结算界面美术资源到 Assets/Art/UI/Settlement/</summary>
        static void CopySettlementArtAssets()
        {
            EnsureDirectory("Assets/Art/UI/Settlement");
            var copyMap = new (string src, string dst)[] {
                ("美术界面资源/结算界面/背景底图.png",             "Assets/Art/UI/Settlement/settlement_bg.png"),
                ("美术界面资源/结算界面/第一名荣誉头像底.png",     "Assets/Art/UI/Settlement/mvp_avatar_frame.png"),
                ("美术界面资源/结算界面/第一名玩家贡献底.png",     "Assets/Art/UI/Settlement/mvp_name_bar.png"),
                ("美术界面资源/结算界面/左侧方玩家结算列表.png",   "Assets/Art/UI/Settlement/rank_list_left.png"),
                ("美术界面资源/结算界面/右侧方玩家结算列表.png",   "Assets/Art/UI/Settlement/rank_list_right.png"),
                ("美术界面资源/结算界面/积分池寡妇前6名底.png",    "Assets/Art/UI/Settlement/score_dist_bar.png"),
                ("美术界面资源/结算界面/返回按钮.png",             "Assets/Art/UI/Settlement/btn_back.png"),
            };
            bool changed = false;
            foreach (var (src, dst) in copyMap)
            {
                // 强制更新：如果源文件更新了也重新复制
                if (System.IO.File.Exists(src))
                {
                    if (System.IO.File.Exists(dst))
                        System.IO.File.Delete(dst);
                    FileUtil.CopyFileOrDirectory(src, dst);
                    changed = true;
                }
            }
            if (changed) AssetDatabase.Refresh();
        }

        // ==================== 底部控制栏 ====================
        static GameObject CreateBottomBar(Transform canvasTransform)
        {
            var bottomBar = CreatePanel(canvasTransform, "BottomBar", Vector2.zero, Vector2.zero);
            var bbRT = bottomBar.GetComponent<RectTransform>();
            bbRT.anchorMin = new Vector2(0, 0);
            bbRT.anchorMax = new Vector2(1, 0);
            bbRT.pivot = new Vector2(0.5f, 0);
            bbRT.anchoredPosition = Vector2.zero;
            bbRT.sizeDelta = new Vector2(0, 50);

            var bbBg = bottomBar.AddComponent<Image>();
            bbBg.color = new Color(0, 0, 0, 0.7f);

            // 7个紧凑按钮: 连接|开始|模拟|展示|重置|GM左|GM右
            CreateSmallButton(bottomBar.transform, "BtnConnect", "连接",
                new Vector2(-480, 0), new Color(0.2f, 0.6f, 0.9f));
            CreateSmallButton(bottomBar.transform, "BtnStart", "开始",
                new Vector2(-330, 0), new Color(0.2f, 0.8f, 0.2f));
            CreateSmallButton(bottomBar.transform, "BtnSimulate", "模拟",
                new Vector2(-180, 0), new Color(0.9f, 0.6f, 0.1f));
            CreateSmallButton(bottomBar.transform, "BtnShowcase", "展示",
                new Vector2(-30, 0), new Color(0.2f, 0.8f, 0.4f));
            CreateSmallButton(bottomBar.transform, "BtnReset", "重置",
                new Vector2(120, 0), new Color(0.8f, 0.2f, 0.2f));
            CreateSmallButton(bottomBar.transform, "BtnGMLeft", "GM左",
                new Vector2(270, 0), new Color(0.9f, 0.5f, 0.1f));
            CreateSmallButton(bottomBar.transform, "BtnGMRight", "GM右",
                new Vector2(420, 0), new Color(0.4f, 0.8f, 0.2f));

            return bottomBar;
        }

        /// <summary>创建紧凑小按钮（130x40, 字号18）</summary>
        static Button CreateSmallButton(Transform parent, string name, string label, Vector2 pos, Color bgColor)
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
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(2, 2);
            trt.offsetMax = new Vector2(-2, -2);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            if (_chineseFont != null) tmp.font = _chineseFont;

            return btn;
        }

        // ==================== 辅助方法 ====================

        /// <summary>复制战斗界面美术切图到 Assets/Art/UI/Battle/</summary>
        static void CopyBattleArtAssets()
        {
            EnsureDirectory("Assets/Art/UI/Battle");
            var copyMap = new (string src, string dst)[] {
                ("美术界面资源/战斗界面/顶部拉力条底图和倒计时.png", "Assets/Art/UI/Battle/top_bar_bg.png"),
                ("美术界面资源/战斗界面/左侧阵营前三玩家.png",       "Assets/Art/UI/Battle/panel_left_camp.png"),
                ("美术界面资源/战斗界面/右侧侧阵营前三玩家.png",     "Assets/Art/UI/Battle/panel_right_camp.png"),
            };
            bool changed = false;
            foreach (var (src, dst) in copyMap)
            {
                if (!System.IO.File.Exists(dst) && System.IO.File.Exists(src))
                {
                    FileUtil.CopyFileOrDirectory(src, dst);
                    changed = true;
                }
            }
            if (changed) AssetDatabase.Refresh();
        }

        static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }

        static Shader FindURPLitShader()
        {
            // 尝试多种方式找到 URP Lit Shader
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;

            // 尝试简写名
            shader = Shader.Find("URP/Lit");
            if (shader != null) return shader;

            // 尝试从 URP 包加载
            shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
            if (shader != null) return shader;

            Debug.LogError("[SceneGen] Cannot find URP Lit shader! Materials will be pink.");
            return null;
        }

        /// <summary>
        /// 确保橘子使用魔法Shader材质
        /// </summary>
        static void EnsureMagicOrangeMaterial(GameObject orange)
        {
            var magicShader = Shader.Find("CapybaraDuel/MagicOrange");
            if (magicShader == null)
            {
                Debug.LogWarning("[SceneGen] MagicOrange shader not found, skipping magic material setup");
                return;
            }

            // 加载现有材质
            string matPath = "Assets/Materials/Mat_Orange.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                // 如果还是旧Shader，切换到魔法Shader
                if (mat.shader != magicShader)
                {
                    // 保存旧贴图
                    var baseTex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;

                    mat.shader = magicShader;

                    // 恢复贴图
                    if (baseTex != null)
                        mat.SetTexture("_BaseMap", baseTex);

                    // 启用所有效果关键字
                    mat.EnableKeyword("_FRESNEL_ON");
                    mat.EnableKeyword("_GLOW_ON");
                    mat.EnableKeyword("_FLOW_ON");
                    mat.EnableKeyword("_PULSE_ON");

                    EditorUtility.SetDirty(mat);
                    Debug.Log("[SceneGen] Upgraded Mat_Orange to MagicOrange shader");
                }

                // 赋给橘子所有renderer
                var renderers = orange.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }
        }

        static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = $"Assets/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                // 修复旧的 Standard shader 材质 → URP Lit
                if (!mat.shader.name.Contains("Universal Render Pipeline"))
                {
                    var urpLit = FindURPLitShader();
                    if (urpLit != null)
                    {
                        Debug.Log($"[SceneGen] Fixing material {name}: {mat.shader.name} → URP Lit");
                        mat.shader = urpLit;
                        mat.SetColor("_BaseColor", color);
                        EditorUtility.SetDirty(mat);
                        AssetDatabase.SaveAssets();
                    }
                }
                return mat;
            }
            EnsureDirectory("Assets/Materials");
            var litShader = FindURPLitShader();
            if (litShader == null) litShader = Shader.Find("Standard"); // 最终 fallback
            mat = new Material(litShader);
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Sprite LoadSprite(string path)
        {
            // 先确保贴图的导入设置为 Sprite
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return go;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 pos, int fontSize, Color color, bool outline = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(400, 60);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            if (_chineseFont != null) tmp.font = _chineseFont;

            // 描边 + 投影效果（增强美术表现）
            if (outline && tmp.fontSharedMaterial != null)
            {
                // 创建材质实例以免影响其他文本
                var mat = new Material(tmp.fontSharedMaterial);

                // 描边（深棕色）
                mat.EnableKeyword("OUTLINE_ON");
                mat.SetFloat("_OutlineWidth", 0.15f);
                mat.SetColor("_OutlineColor", new Color(0.12f, 0.06f, 0.02f, 0.8f));

                // 投影（模拟阴影）
                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetFloat("_UnderlayOffsetX", 0.7f);
                mat.SetFloat("_UnderlayOffsetY", -0.7f);
                mat.SetFloat("_UnderlayDilate", 0.1f);
                mat.SetFloat("_UnderlaySoftness", 0.25f);
                mat.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.5f));

                tmp.fontMaterial = mat;
            }

            return tmp;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(220, 72);

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // 按钮文字
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4, 4);
            trt.offsetMax = new Vector2(-4, -4);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 32;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 20;
            tmp.fontSizeMax = 32;
            if (_chineseFont != null) tmp.font = _chineseFont;

            return btn;
        }

        static Button CreateSpriteButton(Transform parent, string name, string spritePath,
            string label, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            var sprite = LoadSprite(spritePath);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.6f, 0.4f, 0.1f); // 金色 fallback
            }

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // 仅在没有 sprite 时才显示文字（美术按钮自带文字）
            if (sprite == null)
            {
                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(go.transform, false);
                var trt = textGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.fontSize = 30;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                if (_chineseFont != null) tmp.font = _chineseFont;
            }

            return btn;
        }

        // ==================== URP 材质修复 ====================

        /// <summary>
        /// 将 Assets/Materials/ 下所有 Standard shader 材质批量转换为 URP Lit
        /// </summary>
        static void FixAllMaterialsToURP()
        {
            var urpLit = FindURPLitShader();
            if (urpLit == null)
            {
                Debug.LogWarning("[SceneGen] URP Lit shader not found!");
                return;
            }

            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" });
            int fixedCount = 0;
            foreach (string guid in matGuids)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                // 跳过已经是 URP 的或 Skybox 类的
                if (mat.shader.name.Contains("Universal Render Pipeline")) continue;
                if (mat.shader.name.Contains("Skybox")) continue;
                if (mat.shader.name.Contains("Particles")) continue;

                // 保存旧属性
                Color oldColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture oldTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Texture oldNormal = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;

                // 切换 shader
                mat.shader = urpLit;
                mat.SetColor("_BaseColor", oldColor);
                if (oldTex != null) mat.SetTexture("_BaseMap", oldTex);
                if (oldNormal != null) mat.SetTexture("_BumpMap", oldNormal);

                EditorUtility.SetDirty(mat);
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[SceneGen] Fixed {fixedCount} materials from Standard → URP Lit");
            }
        }

        // ==================== 中文字体生成 ====================

        /// <summary>
        /// 获取或创建中文 TMP SDF Font Asset
        /// 从 Assets/Resources/ 下的中文 TTF 生成
        /// </summary>
        static TMP_FontAsset GetOrCreateChineseFontAsset()
        {
            string sdfPath = "Assets/Resources/Fonts/ChineseFont SDF.asset";

            // 先检查是否已有
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
            if (existing != null)
            {
                // 验证 Atlas 纹理是否完好（之前可能没正确保存子资产）
                if (existing.atlasTexture != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(existing.atlasTexture)))
                {
                    return existing;
                }
                // Atlas 纹理丢失，删除重建
                Debug.LogWarning("[SceneGen] Chinese font atlas texture missing, rebuilding...");
                AssetDatabase.DeleteAsset(sdfPath);
            }

            // 查找中文 TTF 字体（按优先级）
            string[] fontPaths = {
                "Assets/Resources/AlibabaPuHuiTi-2-75-SemiBold.ttf",
                "Assets/Resources/DingTalk JinBuTi.ttf",
                "Assets/Resources/优设标题黑.ttf",
                "Assets/Resources/AlimamaFangYuanTiVF-Thin.ttf",
            };

            Font sourceFont = null;
            foreach (var fp in fontPaths)
            {
                sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fp);
                if (sourceFont != null)
                {
                    Debug.Log($"[SceneGen] Found Chinese font: {fp}");
                    break;
                }
            }

            if (sourceFont == null)
            {
                Debug.LogWarning("[SceneGen] No Chinese TTF font found! Text will show squares.");
                return null;
            }

            // 创建 SDF Font Asset
            EnsureDirectory("Assets/Resources/Fonts");

            // 使用 TMP_FontAsset.CreateFontAsset 生成
            var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                Debug.LogWarning("[SceneGen] Failed to create TMP_FontAsset");
                return null;
            }

            fontAsset.name = "ChineseFont SDF";

            // Dynamic 模式：运行时按需生成字符的 SDF 纹理，自动支持所有中文
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            // 创建主资产
            AssetDatabase.CreateAsset(fontAsset, sdfPath);

            // ★ 关键：将 Atlas Texture 和 Material 作为子资产保存
            // 否则运行时会报 "Font Atlas Texture is missing" 错误
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = "ChineseFont SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "ChineseFont SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            // 保存
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneGen] Created Chinese SDF font: {sdfPath} (Dynamic mode, atlas+material saved as sub-assets)");
            return fontAsset;
        }

        /// <summary>
        /// 创建全屏公告UI面板（比赛开始/结束提示）
        /// </summary>
        static GameObject CreateAnnouncementUI(Transform canvasTransform)
        {
            // 全屏半透明背景
            var panel = CreatePanel(canvasTransform, "AnnouncementPanel", Vector2.zero, Vector2.zero);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 半透明背景
            var bg = panel.GetComponent<Image>();
            if (bg != null)
                bg.color = new Color(0, 0, 0, 0.4f);

            // CanvasGroup（控制整体淡入淡出）
            var canvasGroup = panel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;
            canvasGroup.blocksRaycasts = false;

            // 主文字（大字）
            var mainText = CreateText(panel.transform, "MainText", "",
                new Vector2(0, 30), 72, Color.yellow, true);
            mainText.fontStyle = FontStyles.Bold;
            mainText.alignment = TextAlignmentOptions.Center;
            var mainRT = mainText.GetComponent<RectTransform>();
            mainRT.sizeDelta = new Vector2(900, 120);
            mainText.enableWordWrapping = false;

            // 副文字（小字）
            var subText = CreateText(panel.transform, "SubText", "",
                new Vector2(0, -50), 36, Color.white, true);
            subText.alignment = TextAlignmentOptions.Center;
            var subRT = subText.GetComponent<RectTransform>();
            subRT.sizeDelta = new Vector2(600, 60);

            // 挂载 AnnouncementUI 组件
            var announcementUI = panel.AddComponent<CapybaraDuel.UI.AnnouncementUI>();
            announcementUI.canvasGroup = canvasGroup;
            announcementUI.mainText = mainText;
            announcementUI.subText = subText;

            return panel;
        }

        /// <summary>
        /// 在3D场景中创建终点标记（竖线 + 文字标签）
        /// </summary>
        static void CreateGoalMarker(Transform parent, string name, Vector3 position, string label, Color color)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent);
            marker.transform.position = position;

            // 竖线（用一个扁长方体表示终点线）
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "GoalLine";
            line.transform.SetParent(marker.transform);
            line.transform.localPosition = new Vector3(0, 3f, 0);
            line.transform.localScale = new Vector3(0.15f, 6f, 12f); // 薄的竖线，Z方向跨整个赛道
            var lineMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lineMat.color = new Color(color.r, color.g, color.b, 0.6f);
            // 半透明设置
            lineMat.SetFloat("_Surface", 1); // Transparent
            lineMat.SetFloat("_Blend", 0);
            lineMat.SetFloat("_AlphaClip", 0);
            lineMat.SetOverrideTag("RenderType", "Transparent");
            lineMat.renderQueue = 3000;
            lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMat.SetInt("_ZWrite", 0);
            lineMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            line.GetComponent<Renderer>().sharedMaterial = lineMat;
            // 移除碰撞体
            var col = line.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            // 文字标签（World Space Canvas）
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(marker.transform);
            labelGo.transform.localPosition = new Vector3(0, 7f, 0);
            labelGo.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);

            var canvas = labelGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            var canvasRect = labelGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(300f, 60f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(labelGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 36;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.outlineWidth = 0.3f;
            tmp.outlineColor = new Color32(0, 0, 0, 200);

            var font = GetOrCreateChineseFontAsset();
            if (font != null) tmp.font = font;
        }

        // ==================== 橘子头顶速度HUD（场景预创建）====================
        public static void CreateOrUpdateSpeedHUD(Transform sceneRoot, GameObject orange, OrangeSpeedHUD speedHud)
        {
            // 查找或创建 HUD Root（独立于橘子，不跟随自转）
            var existingRoot = sceneRoot.Find("OrangeSpeedHUD_Root");
            GameObject hudRoot;
            if (existingRoot != null)
            {
                hudRoot = existingRoot.gameObject;
            }
            else
            {
                hudRoot = new GameObject("OrangeSpeedHUD_Root");
                hudRoot.transform.SetParent(sceneRoot);
                hudRoot.transform.position = orange.transform.position + Vector3.up * 0.6f;
                hudRoot.transform.localScale = Vector3.one * 0.004f;
            }

            // 添加 Canvas（World Space）
            var canvas = hudRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = hudRoot.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 100;
            }

            var canvasRect = hudRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(260f, 40f);

            // 背景底板
            Transform bgTrans = hudRoot.transform.Find("BG");
            Image bgImage;
            if (bgTrans == null)
            {
                var bgGo = new GameObject("BG");
                bgGo.transform.SetParent(hudRoot.transform, false);
                var bgRect = bgGo.AddComponent<RectTransform>();
                bgRect.anchoredPosition = Vector2.zero;
                bgRect.sizeDelta = new Vector2(240f, 36f);
                bgImage = bgGo.AddComponent<Image>();
                bgImage.color = new Color(0.08f, 0.04f, 0f, 0.6f);
            }
            else
            {
                bgImage = bgTrans.GetComponent<Image>();
            }

            // 速度文字
            Transform textTrans = hudRoot.transform.Find("MainText");
            TextMeshProUGUI mainText;
            if (textTrans == null)
            {
                var textGo = new GameObject("MainText");
                textGo.transform.SetParent(hudRoot.transform, false);
                var textRect = textGo.AddComponent<RectTransform>();
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(250f, 38f);
                mainText = textGo.AddComponent<TextMeshProUGUI>();
                mainText.text = "0.00 米/秒";
                mainText.fontSize = 24;
                mainText.color = Color.white;
                mainText.alignment = TextAlignmentOptions.Center;
                mainText.fontStyle = FontStyles.Bold;
                mainText.enableWordWrapping = false;
                mainText.overflowMode = TextOverflowModes.Overflow;
                mainText.outlineWidth = 0.3f;
                mainText.outlineColor = new Color32(20, 10, 0, 230);
                mainText.richText = true;

                // 使用 LiberationSans（有数字）+ 中文fallback
                var latinFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                if (latinFont == null)
                    latinFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                var cjkFont = GetOrCreateChineseFontAsset();
                if (latinFont != null)
                {
                    mainText.font = latinFont;
                    if (cjkFont != null)
                    {
                        if (latinFont.fallbackFontAssetTable == null)
                            latinFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                        if (!latinFont.fallbackFontAssetTable.Contains(cjkFont))
                            latinFont.fallbackFontAssetTable.Add(cjkFont);
                    }
                }
                else if (cjkFont != null)
                {
                    mainText.font = cjkFont;
                }
            }
            else
            {
                mainText = textTrans.GetComponent<TextMeshProUGUI>();
            }

            // Wire 引用到 OrangeSpeedHUD 组件
            speedHud.hudRoot = hudRoot;
            speedHud.mainText = mainText;
            speedHud.bgImage = bgImage;

            Debug.Log("[SceneGenerator] SpeedHUD scene objects created/wired — 可在Inspector直接调整大小位置");
        }
    }
}
#endif
