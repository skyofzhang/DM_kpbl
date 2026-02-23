using UnityEngine;
using CapybaraDuel.Core;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// UI总管 - 管理所有面板的显示/隐藏
    /// 三个主面板：MainMenu(Idle) / GameUI(Running) / Settlement(Settlement)
    /// BottomBar 始终可见（调试控制用）
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Main Panels")]
        public GameObject loadingPanel;    // 加载/连接界面（启动时显示）
        public GameObject mainMenuPanel;   // 主界面（Idle 状态显示）
        public GameObject gameUIPanel;     // 对局界面（Running 状态显示）
        public GameObject settlementPanel; // 结算界面（Settlement 状态显示）

        [Header("Game UI Sub-panels (for backward compat)")]
        public GameObject topBar;
        public GameObject leftPlayerList;
        public GameObject rightPlayerList;
        public GameObject giftNotification;
        public GameObject bottomBar;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnStateChanged += HandleStateChanged;
            }

            // 初始状态：显示Loading界面（自动连接服务器）
            if (loadingPanel != null)
            {
                SwitchToState(GameManager.GameState.Connecting);
            }
            else
            {
                // 没有Loading面板时，直接显示主界面
                SwitchToState(GameManager.GameState.Idle);
                // 但仍然自动连接服务器（后台静默连接）
                AutoConnectInBackground();
            }
        }

        /// <summary>
        /// 后台静默连接服务器（旧场景无Loading面板时使用）
        /// </summary>
        private void AutoConnectInBackground()
        {
            var gm = GameManager.Instance;
            var net = NetworkManager.Instance;
            if (gm != null && net != null && !net.IsConnected)
            {
                Debug.Log("[UIManager] No LoadingScreen found, auto-connecting in background...");
                net.Connect();
            }
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null)
                gm.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameManager.GameState oldState, GameManager.GameState newState)
        {
            SwitchToState(newState);
        }

        /// <summary>
        /// 根据游戏状态切换 UI 面板
        /// </summary>
        public void SwitchToState(GameManager.GameState state)
        {
            switch (state)
            {
                case GameManager.GameState.Idle:
                    SetPanel(mainMenu: true, gameUI: false, settlement: false, loading: false);
                    break;

                case GameManager.GameState.Connecting:
                    // 连接中，显示Loading界面
                    SetPanel(mainMenu: false, gameUI: false, settlement: false, loading: true);
                    break;

                case GameManager.GameState.Running:
                    SetPanel(mainMenu: false, gameUI: true, settlement: false, loading: false);
                    break;

                case GameManager.GameState.Settlement:
                    SetPanel(mainMenu: false, gameUI: true, settlement: true, loading: false);
                    break;
            }
        }

        private void SetPanel(bool mainMenu, bool gameUI, bool settlement, bool loading = false)
        {
            if (loadingPanel) loadingPanel.SetActive(loading);
            if (mainMenuPanel) mainMenuPanel.SetActive(mainMenu);
            if (gameUIPanel) gameUIPanel.SetActive(gameUI);
            if (settlementPanel) settlementPanel.SetActive(settlement);

            // 旧版兼容：如果没有 gameUIPanel，用子面板控制
            if (gameUIPanel == null)
            {
                ShowGameSubPanels(gameUI);
            }
        }

        private void ShowGameSubPanels(bool show)
        {
            if (topBar) topBar.SetActive(show);
            if (leftPlayerList) leftPlayerList.SetActive(show);
            if (rightPlayerList) rightPlayerList.SetActive(show);
            if (giftNotification) giftNotification.SetActive(show);
        }

        /// <summary>
        /// 显示主菜单（外部调用）
        /// </summary>
        public void ShowMainMenu()
        {
            SwitchToState(GameManager.GameState.Idle);
        }

        /// <summary>
        /// 显示对局界面（外部调用）
        /// </summary>
        public void ShowGameUI()
        {
            SwitchToState(GameManager.GameState.Running);
        }
    }
}
