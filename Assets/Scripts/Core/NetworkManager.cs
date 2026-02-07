using UnityEngine;
using System;
using System.Collections;

namespace CapybaraDuel.Core
{
    /// <summary>
    /// 网络管理器 - WebSocket连接管理
    /// TODO: 集成NativeWebSocket
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Server Config")]
        [SerializeField] private string serverUrl = "ws://localhost:8081";
        [SerializeField] private float heartbeatInterval = 30f;
        [SerializeField] private float reconnectDelay = 3f;
        [SerializeField] private int maxReconnectAttempts = 5;

        private bool isConnected = false;
        private int reconnectAttempts = 0;

        // 事件
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<int, int, int> OnForceChanged;
        public event Action<float> OnOrangePositionReceived;
        public event Action<float> OnCountdownReceived;
        public event Action<string, string> OnGameEnded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // TODO: 初始化WebSocket连接
            Debug.Log("[NetworkManager] Initialized - WebSocket integration pending");
        }

        public void Connect()
        {
            Debug.Log($"[NetworkManager] Connecting to {serverUrl}...");
            // TODO: 实现WebSocket连接
            StartCoroutine(SimulateConnection());
        }

        private IEnumerator SimulateConnection()
        {
            yield return new WaitForSeconds(0.5f);
            isConnected = true;
            reconnectAttempts = 0;
            OnConnected?.Invoke();
            Debug.Log("[NetworkManager] Connected (simulated)");
        }

        public void Disconnect()
        {
            isConnected = false;
            OnDisconnected?.Invoke("Manual disconnect");
            Debug.Log("[NetworkManager] Disconnected");
        }

        public void SendMessage(string type, object data)
        {
            if (!isConnected)
            {
                Debug.LogWarning("[NetworkManager] Not connected, cannot send message");
                return;
            }

            // TODO: 发送WebSocket消息
            Debug.Log($"[NetworkManager] Sending: {type}");
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
