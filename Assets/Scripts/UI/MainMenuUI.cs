using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 主界面UI - 处理主菜单按钮点击事件
    /// 按钮：开始游戏、排行榜、礼物说明、规则说明、贴纸设置
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Main Buttons")]
        public Button btnStartGame;
        public Button btnLeaderboard;
        public Button btnGiftDesc;
        public Button btnRuleDesc;
        public Button btnStickerSettings;

        [Header("Info Panels (运行时创建)")]
        public GameObject infoPanelOverlay;  // 信息面板遮罩层

        private GameObject _activeInfoPanel; // 当前显示的信息面板
        private TMP_FontAsset _chineseFont;  // 中文字体缓存

        private void Start()
        {
            // 加载中文字体
            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");

            // 绑定按钮事件
            if (btnStartGame) btnStartGame.onClick.AddListener(OnStartGameClicked);
            if (btnLeaderboard) btnLeaderboard.onClick.AddListener(OnLeaderboardClicked);
            if (btnGiftDesc) btnGiftDesc.onClick.AddListener(OnGiftDescClicked);
            if (btnRuleDesc) btnRuleDesc.onClick.AddListener(OnRuleDescClicked);

            // 隐藏贴纸按钮（审核演示改用组合按键 Ctrl+Shift+D 触发）
            if (btnStickerSettings)
                btnStickerSettings.gameObject.SetActive(false);
        }

        // ==================== 按钮回调 ====================

        /// <summary>
        /// 开始游戏 - 服务器已在启动时连接，直接开始对局
        /// 如果未连接：有Loading面板→跳到Loading；无Loading面板→先连接再开始
        /// </summary>
        private void OnStartGameClicked()
        {
            var net = NetworkManager.Instance;

            // 如果未连接
            if (net == null || !net.IsConnected)
            {
                var uiMgr = UIManager.Instance;

                // 有 Loading 面板 → 跳到 Loading 重新连接
                if (uiMgr != null && uiMgr.loadingPanel != null)
                {
                    Debug.Log("[MainMenuUI] Server not connected, switching to loading screen.");
                    uiMgr.SwitchToState(GameManager.GameState.Connecting);
                    var loading = uiMgr.loadingPanel.GetComponent<LoadingScreenUI>();
                    if (loading != null) loading.StartConnecting();
                    return;
                }

                // 没有 Loading 面板（旧场景）→ 先发起连接，连接成功后自动开始
                Debug.Log("[MainMenuUI] Server not connected, connecting and starting...");
                if (net != null)
                {
                    net.OnConnected += OnConnectedThenStart;
                    net.Connect();
                }
                return;
            }

            // 已连接 → 直接开始
            StartGameAfterConnected();
        }

        /// <summary>连接成功后自动开始游戏（仅旧场景无Loading时使用）</summary>
        private void OnConnectedThenStart()
        {
            var net = NetworkManager.Instance;
            if (net != null)
                net.OnConnected -= OnConnectedThenStart;

            StartGameAfterConnected();
        }

        /// <summary>发送开始命令+切换到对局界面</summary>
        private void StartGameAfterConnected()
        {
            // 先重置旧房间状态（防止客户端关闭后重开进入上一局残局）
            GameManager.Instance?.ResetGame();
            GameManager.Instance?.RequestResetGame();

            // 通过服务器开始新一局游戏
            GameManager.Instance?.RequestStartGame();

            // 按住Shift点击"开始游戏" = 审核演示模式（自动模拟全部功能）
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Debug.Log("[MainMenuUI] ★ 审核演示模式启动（Shift+开始游戏）");
                GameManager.Instance?.RequestReviewSim();
            }

            // 切换到对局界面
            var uiMgr = UIManager.Instance;
            if (uiMgr != null)
                uiMgr.ShowGameUI();
        }

        // ==================== 组合按键检测 ====================

        /// <summary>
        /// Ctrl+Shift+D 组合按键触发审核演示（不与Unity快捷键冲突）
        /// 仅在主菜单界面激活时生效
        /// </summary>
        private void Update()
        {
            // 组合按键: Ctrl + Shift + D
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift)
                && Input.GetKeyDown(KeyCode.D))
            {
                Debug.Log("[MainMenuUI] ★ 审核演示模式启动 (Ctrl+Shift+D)");
                var net = NetworkManager.Instance;

                if (net == null || !net.IsConnected)
                {
                    Debug.Log("[MainMenuUI] 审核演示: 未连接，先发起连接...");
                    if (net != null)
                    {
                        net.OnConnected += OnConnectedThenReviewDemo;
                        net.Connect();
                    }
                    return;
                }

                StartReviewDemo();
            }
        }

        private void OnConnectedThenReviewDemo()
        {
            var net = NetworkManager.Instance;
            if (net != null)
                net.OnConnected -= OnConnectedThenReviewDemo;
            StartReviewDemo();
        }

        private void StartReviewDemo()
        {
            // 先重置旧房间状态
            GameManager.Instance?.ResetGame();
            GameManager.Instance?.RequestResetGame();

            GameManager.Instance?.RequestStartGame();
            GameManager.Instance?.RequestReviewSim();

            var uiMgr = UIManager.Instance;
            if (uiMgr != null)
                uiMgr.ShowGameUI();
        }

        /// <summary>
        /// 排行榜 — 打开排行榜面板
        /// </summary>
        private void OnLeaderboardClicked()
        {
            var rankingPanel = FindObjectOfType<RankingPanelUI>(true); // 包含 inactive 对象
            if (rankingPanel != null)
            {
                rankingPanel.Show();
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] RankingPanelUI not found");
            }
        }

        /// <summary>
        /// 礼物说明 — 简洁表格式
        /// </summary>
        private void OnGiftDescClicked()
        {
            ShowInfoPanel("礼物说明",
                "送出礼物为你的阵营增加推力\n" +
                "礼物越贵，推力越大!\n\n" +
                "<b>礼物名称          价格          推力</b>\n\n" +
                "仙女棒            0.1 抖币         +10\n" +
                "能力药丸          10 抖币          +343\n" +
                "甜甜圈            52 抖币          +808\n" +
                "能量电池          99 抖币          +1,415\n" +
                "爱的爆炸          199 抖币        +2,679\n" +
                "神秘空投          520 抖币        +6,988\n\n" +
                "累计推力决定橘子的移动方向\n" +
                "将橘子推入对方温泉池即可获胜!");
        }

        /// <summary>
        /// 规则说明 — 简洁编号列表
        /// </summary>
        private void OnRuleDescClicked()
        {
            ShowInfoPanel("规则说明",
                "<b>玩法规则</b>\n\n" +
                "1. 发弹幕选择阵营（左 / 右）\n\n" +
                "2. 加入后获得一只水豚角色\n\n" +
                "3. 送礼物为己方阵营增加推力\n\n" +
                "4. 推力决定中间橘子移动方向\n\n" +
                "5. 橘子到达对方温泉即获胜\n\n\n" +
                "每局 60 分钟\n" +
                "时间到时橘子更靠近哪方终点\n" +
                "则另一方获胜!");
        }

        // ==================== 信息面板 ====================

        /// <summary>
        /// 显示信息面板（通用弹窗）
        /// </summary>
        private void ShowInfoPanel(string title, string content)
        {
            // 关闭已有面板
            CloseInfoPanel();

            // 创建遮罩层
            var overlay = new GameObject("InfoPanelOverlay");
            overlay.transform.SetParent(transform, false);
            var overlayRT = overlay.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            // 半透明黑色背景（可点击关闭）
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.6f);
            var overlayBtn = overlay.AddComponent<Button>();
            overlayBtn.onClick.AddListener(CloseInfoPanel);

            // 内容面板
            var panel = new GameObject("InfoPanel");
            panel.transform.SetParent(overlay.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchoredPosition = new Vector2(0, 50);
            panelRT.sizeDelta = new Vector2(850, 1000);

            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.15f, 0.2f, 0.95f);

            // 标题
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panel.transform, false);
            var titleRT = titleGo.AddComponent<RectTransform>();
            titleRT.anchoredPosition = new Vector2(0, 420);
            titleRT.sizeDelta = new Vector2(700, 80);
            var titleTMP = titleGo.AddComponent<TextMeshProUGUI>();
            titleTMP.text = title;
            titleTMP.fontSize = 48;
            titleTMP.color = new Color(1f, 0.84f, 0f); // 金色
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.fontStyle = FontStyles.Bold;
            if (_chineseFont != null) titleTMP.font = _chineseFont;

            // 内容文字
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(panel.transform, false);
            var contentRT = contentGo.AddComponent<RectTransform>();
            contentRT.anchoredPosition = new Vector2(0, -20);
            contentRT.sizeDelta = new Vector2(700, 750);
            var contentTMP = contentGo.AddComponent<TextMeshProUGUI>();
            contentTMP.text = content;
            contentTMP.fontSize = 30;
            contentTMP.color = Color.white;
            contentTMP.alignment = TextAlignmentOptions.TopLeft;
            contentTMP.enableWordWrapping = true;
            contentTMP.overflowMode = TextOverflowModes.Ellipsis;
            contentTMP.lineSpacing = 8f; // 增加行间距提升可读性
            contentTMP.richText = true;
            if (_chineseFont != null) contentTMP.font = _chineseFont;

            // 关闭按钮（右上角 X）
            var closeGo = new GameObject("BtnClose");
            closeGo.transform.SetParent(panel.transform, false);
            var closeRT = closeGo.AddComponent<RectTransform>();
            closeRT.anchoredPosition = new Vector2(370, 440);
            closeRT.sizeDelta = new Vector2(70, 70);
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = new Color(0.8f, 0.2f, 0.2f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(CloseInfoPanel);

            // X 文字
            var xGo = new GameObject("XText");
            xGo.transform.SetParent(closeGo.transform, false);
            var xRT = xGo.AddComponent<RectTransform>();
            xRT.anchorMin = Vector2.zero;
            xRT.anchorMax = Vector2.one;
            xRT.offsetMin = Vector2.zero;
            xRT.offsetMax = Vector2.zero;
            var xTMP = xGo.AddComponent<TextMeshProUGUI>();
            xTMP.text = "X";
            xTMP.fontSize = 36;
            xTMP.color = Color.white;
            xTMP.alignment = TextAlignmentOptions.Center;
            if (_chineseFont != null) xTMP.font = _chineseFont;

            // 底部关闭按钮
            var bottomCloseGo = new GameObject("BtnCloseBottom");
            bottomCloseGo.transform.SetParent(panel.transform, false);
            var bcRT = bottomCloseGo.AddComponent<RectTransform>();
            bcRT.anchoredPosition = new Vector2(0, -430);
            bcRT.sizeDelta = new Vector2(250, 70);
            var bcImg = bottomCloseGo.AddComponent<Image>();
            bcImg.color = new Color(0.2f, 0.6f, 0.9f);
            var bcBtn = bottomCloseGo.AddComponent<Button>();
            bcBtn.targetGraphic = bcImg;
            bcBtn.onClick.AddListener(CloseInfoPanel);

            // 关闭文字
            var bcTextGo = new GameObject("Text");
            bcTextGo.transform.SetParent(bottomCloseGo.transform, false);
            var bcTextRT = bcTextGo.AddComponent<RectTransform>();
            bcTextRT.anchorMin = Vector2.zero;
            bcTextRT.anchorMax = Vector2.one;
            bcTextRT.offsetMin = Vector2.zero;
            bcTextRT.offsetMax = Vector2.zero;
            var bcTMP = bcTextGo.AddComponent<TextMeshProUGUI>();
            bcTMP.text = "关闭";
            bcTMP.fontSize = 32;
            bcTMP.color = Color.white;
            bcTMP.alignment = TextAlignmentOptions.Center;
            if (_chineseFont != null) bcTMP.font = _chineseFont;

            _activeInfoPanel = overlay;
        }

        /// <summary>
        /// 关闭信息面板
        /// </summary>
        public void CloseInfoPanel()
        {
            if (_activeInfoPanel != null)
            {
                Destroy(_activeInfoPanel);
                _activeInfoPanel = null;
            }
        }
    }
}
