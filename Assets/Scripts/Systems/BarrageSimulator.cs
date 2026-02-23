using System.Collections;
using UnityEngine;
using CapybaraDuel.Core;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 弹幕模拟器 - 仅保留服务器通信功能
    /// 所有游戏数据（玩家加入、礼物、推力等）均由服务器推送
    /// 客户端不做任何本地模拟
    /// </summary>
    public class BarrageSimulator : MonoBehaviour
    {
        public static BarrageSimulator Instance { get; private set; }

        public bool IsRunning => false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ==================== 服务器通信（唯一功能） ====================

        /// <summary>
        /// 服务器联调测试：连接服务器 → 开始游戏 → 启用服务器端模拟
        /// 数据走正式服务器流程，不使用本地模拟
        /// </summary>
        public void StartServerTest()
        {
            var net = NetworkManager.Instance;
            if (net == null)
            {
                Debug.LogError("[BarrageSim] NetworkManager not found!");
                return;
            }

            if (net.IsConnected)
            {
                Debug.Log("[BarrageSim] Already connected, starting server test...");
                ExecuteServerTest();
            }
            else
            {
                Debug.Log("[BarrageSim] Connecting to server for test...");
                net.OnConnected += OnConnectedForTest;
                GameManager.Instance?.ConnectToServer();
            }
        }

        private void OnConnectedForTest()
        {
            NetworkManager.Instance.OnConnected -= OnConnectedForTest;
            Debug.Log("[BarrageSim] Connected! Executing server test...");
            StartCoroutine(DelayedServerTest());
        }

        private IEnumerator DelayedServerTest()
        {
            yield return new WaitForSeconds(0.5f);
            ExecuteServerTest();
        }

        private void ExecuteServerTest()
        {
            GameManager.Instance?.RequestStartGame();
            GameManager.Instance?.RequestToggleSim(true);
            Debug.Log("[BarrageSim] Server test started - waiting for server simulation data");
        }

        /// <summary>停止服务器测试</summary>
        public void StopServerTest()
        {
            GameManager.Instance?.RequestToggleSim(false);
            Debug.Log("[BarrageSim] Server test stopped");
        }

        // ==================== 空壳方法（保持编译兼容） ====================

        public void StartSimulation()
        {
            Debug.LogWarning("[BarrageSim] Local simulation disabled. Use server mode instead.");
        }

        public void StopSimulation()
        {
            // no-op
        }

        public void StartShowcase()
        {
            Debug.LogWarning("[BarrageSim] Local showcase disabled. Use server showcase (RequestShowcaseSim) instead.");
        }

        public void SimulatePlayerJoinCamp(string camp)
        {
            Debug.LogWarning("[BarrageSim] Local player join disabled. All data comes from server.");
        }

        public GameEndedData BuildSettlementData(string winner)
        {
            return new GameEndedData { winner = winner };
        }

        public CapybaraDuel.UI.RankingPanelUI.RankingData[] GetGlobalRankings()
        {
            return new CapybaraDuel.UI.RankingPanelUI.RankingData[0];
        }

        public PersistentRankingData GetSimRankingForTab(int tabIndex)
        {
            return new PersistentRankingData();
        }
    }
}
