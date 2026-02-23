using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 加载/连接服务器界面 - 启动时自动显示
    /// 黑屏 + "正在连接服务器..." 文字 + 旋转动画
    /// 连接成功后自动隐藏，进入主界面
    /// 连接失败显示重试按钮
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI dotText;        // 动态省略号
        public Button retryButton;
        public TextMeshProUGUI retryButtonText;
        public Image spinnerImage;             // 旋转指示器

        private float _dotTimer;
        private int _dotCount;
        private bool _isConnecting;
        private float _spinAngle;
        private TMP_FontAsset _chineseFont;

        private void Start()
        {
            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");

            // 确保 UI 文字有字体
            if (_chineseFont != null)
            {
                if (statusText != null) statusText.font = _chineseFont;
                if (dotText != null) dotText.font = _chineseFont;
                if (retryButtonText != null) retryButtonText.font = _chineseFont;
            }

            // 隐藏重试按钮
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(false);
                retryButton.onClick.AddListener(OnRetryClicked);
            }

            // 订阅网络事件
            var net = NetworkManager.Instance;
            if (net != null)
            {
                net.OnConnected += HandleConnected;
                net.OnDisconnected += HandleDisconnected;
            }

            // 启动连接
            StartConnecting();
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

        private void Update()
        {
            if (!_isConnecting) return;

            // 动态省略号动画
            _dotTimer += Time.deltaTime;
            if (_dotTimer >= 0.5f)
            {
                _dotTimer = 0f;
                _dotCount = (_dotCount + 1) % 4;
                if (dotText != null)
                    dotText.text = new string('.', _dotCount);
            }

            // 旋转动画
            if (spinnerImage != null)
            {
                _spinAngle -= 180f * Time.deltaTime; // 每秒半圈
                spinnerImage.transform.localRotation = Quaternion.Euler(0, 0, _spinAngle);
            }

            // 超时检测（15秒）
            _connectTimer += Time.deltaTime;
            if (_connectTimer > 15f && !NetworkManager.Instance.IsConnected)
            {
                ShowConnectionFailed("连接超时，请检查网络");
            }
        }

        private float _connectTimer;

        /// <summary>开始连接</summary>
        public void StartConnecting()
        {
            _isConnecting = true;
            _connectTimer = 0f;
            _dotCount = 0;

            if (statusText != null)
                statusText.text = "正在连接服务器";
            if (dotText != null)
                dotText.text = "";
            if (retryButton != null)
                retryButton.gameObject.SetActive(false);
            if (spinnerImage != null)
                spinnerImage.gameObject.SetActive(true);

            // 发起连接
            GameManager.Instance?.ConnectToServer();
        }

        /// <summary>连接成功</summary>
        private void HandleConnected()
        {
            _isConnecting = false;

            if (statusText != null)
                statusText.text = "连接成功！";
            if (dotText != null)
                dotText.text = "";
            if (spinnerImage != null)
                spinnerImage.gameObject.SetActive(false);

            // 延迟0.5秒后隐藏Loading，显示主界面
            Invoke(nameof(HideAndShowMainMenu), 0.5f);
        }

        /// <summary>断开连接</summary>
        private void HandleDisconnected(string reason)
        {
            // 只有在Loading界面活跃时才处理
            if (!gameObject.activeSelf) return;

            ShowConnectionFailed($"连接断开: {reason}");
        }

        /// <summary>显示连接失败状态</summary>
        private void ShowConnectionFailed(string message)
        {
            _isConnecting = false;

            if (statusText != null)
                statusText.text = message;
            if (dotText != null)
                dotText.text = "";
            if (spinnerImage != null)
                spinnerImage.gameObject.SetActive(false);
            if (retryButton != null)
                retryButton.gameObject.SetActive(true);
        }

        /// <summary>重试按钮点击</summary>
        private void OnRetryClicked()
        {
            // 断开旧连接
            NetworkManager.Instance?.Disconnect();
            // 重新开始
            StartConnecting();
        }

        /// <summary>隐藏Loading，切换到主界面</summary>
        private void HideAndShowMainMenu()
        {
            gameObject.SetActive(false);

            // 通知 UIManager 显示主界面
            var uiMgr = UIManager.Instance;
            if (uiMgr != null)
                uiMgr.ShowMainMenu();
        }
    }
}
