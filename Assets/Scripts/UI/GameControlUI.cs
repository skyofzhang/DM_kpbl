using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Systems;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 底部GM工具面板 - 本地测试用
    /// 左上角快速点击6次唤出
    ///
    /// 功能:
    /// 1. GM登录 — 无抖音token时直接连服务器(GM模式)
    /// 2. 模拟 — 开启/关闭服务器端弹幕模拟（礼物+评论+加入）
    ///
    /// 生产环境默认隐藏，只有知道暗号（6连击左上角）才能呼出
    /// </summary>
    public class GameControlUI : MonoBehaviour
    {
        [Header("Buttons")]
        public Button gmLoginButton;       // GM登录
        public Button simulateButton;      // 模拟弹幕

        [Header("Status")]
        public TextMeshProUGUI statusText; // 状态文字

        private bool _simEnabled = false;
        private bool _connected = false;

        // 隐藏/唤出控制
        private CanvasGroup _cg;
        private bool _visible = false;
        private int _tapCount = 0;
        private float _tapTimer = 0f;
        private const float TAP_INTERVAL = 0.5f;
        private const int TAP_SHOW_PANEL = 6;   // 6次唤出面板

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void Update()
        {
            // 左上角1/6区域快速点击6次唤出/隐藏面板
            if (_tapTimer > 0f)
            {
                _tapTimer -= Time.unscaledDeltaTime;
                if (_tapTimer <= 0f) _tapCount = 0;
            }

            if (Input.GetMouseButtonDown(0))
            {
                var pos = Input.mousePosition;
                if (pos.x < Screen.width / 6f && pos.y > Screen.height * 5f / 6f)
                {
                    _tapCount++;
                    _tapTimer = TAP_INTERVAL;

                    if (_tapCount >= TAP_SHOW_PANEL)
                    {
                        _tapCount = 0;
                        SetVisible(!_visible);
                    }
                }
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_cg != null)
            {
                _cg.alpha = visible ? 1f : 0f;
                _cg.interactable = visible;
                _cg.blocksRaycasts = visible;
            }
        }

        private void Start()
        {
            if (gmLoginButton) gmLoginButton.onClick.AddListener(OnGMLoginClicked);
            if (simulateButton) simulateButton.onClick.AddListener(OnSimulateClicked);

            var net = NetworkManager.Instance;
            if (net != null)
            {
                net.OnConnected += HandleConnected;
                net.OnDisconnected += HandleDisconnected;
            }

            UpdateButtonStates(false);
            SetStatusText("GM工具就绪");
        }

        private void OnDestroy()
        {
            var net = NetworkManager.Instance;
            if (net != null)
            {
                net.OnConnected -= HandleConnected;
                net.OnDisconnected -= HandleDisconnected;
            }
        }

        /// <summary>GM登录：无token直接连服务器</summary>
        private void OnGMLoginClicked()
        {
            if (_connected)
            {
                SetStatusText("已连接，无需重复登录");
                return;
            }

            SetStatusText("GM连接中...");
            SetButtonText(gmLoginButton, "连接中...");

            // 直接调用Connect()，无token时会自动走GM模式
            GameManager.Instance?.ConnectToServer();
        }

        /// <summary>模拟弹幕开关</summary>
        private void OnSimulateClicked()
        {
            if (!_connected)
            {
                SetStatusText("请先GM登录");
                return;
            }

            _simEnabled = !_simEnabled;
            GameManager.Instance?.RequestToggleSim(_simEnabled);

            SetButtonText(simulateButton, _simEnabled ? "停止模拟" : "模拟");
            var img = simulateButton?.GetComponent<Image>();
            if (img != null)
                img.color = _simEnabled ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.9f, 0.6f, 0.1f);

            SetStatusText(_simEnabled ? "模拟已开启 - 弹幕/礼物自动生成中" : "模拟已关闭");

            if (_simEnabled)
            {
                var uiMgr = UIManager.Instance;
                if (uiMgr != null)
                    uiMgr.ShowGameUI();
            }
        }

        private void HandleConnected()
        {
            _connected = true;
            SetButtonText(gmLoginButton, "已连接✓");
            UpdateButtonStates(true);

            var net = NetworkManager.Instance;
            string mode = (net != null && net.IsGMMode) ? "GM模式" : "直播模式";
            SetStatusText($"已连接 ({mode})");
        }

        private void HandleDisconnected(string reason)
        {
            _connected = false;
            _simEnabled = false;
            SetButtonText(gmLoginButton, "GM登录");
            SetButtonText(simulateButton, "模拟");
            UpdateButtonStates(false);
            SetStatusText($"已断开: {reason}");
        }

        private void UpdateButtonStates(bool connected)
        {
            if (gmLoginButton) gmLoginButton.interactable = !connected;
            if (simulateButton) simulateButton.interactable = connected;
        }

        private void SetButtonText(Button btn, string text)
        {
            if (btn == null) return;
            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }

        private void SetStatusText(string text)
        {
            if (statusText != null)
                statusText.text = text;
            Debug.Log($"[GM] {text}");
        }
    }
}
