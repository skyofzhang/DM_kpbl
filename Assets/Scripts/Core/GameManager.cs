using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CapybaraDuel.Systems;
using CapybaraDuel.UI;
using CapybaraDuel.Utils;

namespace CapybaraDuel.Core
{
    /// <summary>
    /// 游戏管理器 - 状态机 + 子系统协调 + 消息分发 + 玩家贡献追踪
    /// 所有游戏数据来自服务器，客户端不做本地模拟
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Idle, Connecting, Running, Settlement }

        [Header("Current State")]
        [SerializeField] private GameState currentState = GameState.Idle;
        public GameState CurrentState => currentState;

        [Header("Subsystems")]
        public ForceSystem forceSystem;
        public CampSystem campSystem;
        public RankingSystem rankingSystem;
        public GiftHandler giftHandler;
        public CapybaraSpawner spawner;
        public OrangeController orangeController;

        public float RemainingTime { get; private set; }
        public string Winner { get; private set; }

        public event Action<GameState, GameState> OnStateChanged;
        public event Action<float> OnCountdownTick;                // remainingTime
        public event Action<GameEndedData> OnGameEnded;            // 完整结算数据
        public event Action<PersistentRankingData> OnPersistentRankingReceived; // 持久化排行
        public event Action<PlayerDataPanelData> OnPlayerDataPanelReceived;  // 玩家数据面板
        public event Action<float> OnScorePoolUpdated;            // 积分池变化
        public event Action<ForceBoostData> OnForceBoost;           // 666推力提升
        public event Action<LikeBoostData> OnLikeBoost;             // 点赞推力提升

        private float _settlementTimer;

        // ==================== 玩家贡献追踪（基于服务器推送数据） ====================
        private class TrackedPlayer
        {
            public string playerId;
            public string playerName;
            public string camp;
            public float contribution;
        }
        private Dictionary<string, TrackedPlayer> _trackedPlayers = new Dictionary<string, TrackedPlayer>();
        private float _totalScorePool; // 累计礼物价值（积分池）
        private int _forceUpdateCount; // force_update消息计数（调试用）
        private float _clientOrangePos; // 客户端自算橘子位置（服务器orangePos=0时的后备方案）

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 确保AvatarLoader单例存在（头像下载+缓存）
            if (AvatarLoader.Instance == null)
            {
                var avatarLoaderGo = new GameObject("AvatarLoader");
                avatarLoaderGo.transform.SetParent(transform);
                avatarLoaderGo.AddComponent<AvatarLoader>();
            }
        }

        private void Start()
        {
            // 初始化 GiftHandler
            if (giftHandler != null && spawner != null)
                giftHandler.Initialize(spawner);

            // 订阅网络消息
            var net = NetworkManager.Instance;
            if (net != null)
            {
                net.OnConnected += HandleConnected;
                net.OnDisconnected += HandleDisconnected;
                net.OnMessageReceived += HandleMessage;
                net.OnHeartbeatTimeout += HandleHeartbeatTimeout;
            }

            // 订阅橘子到边判负事件
            if (orangeController != null)
                orangeController.OnReachedBoundary += HandleBoundaryReached;

            // 确保 AnnouncementUI 存在（即使场景中没有手动创建）
            EnsureAnnouncementUI();
        }

        private void EnsureAnnouncementUI()
        {
            if (AnnouncementUI.Instance != null) return;

            // 先找场景中可能存在但 inactive 的 AnnouncementPanel
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 搜索所有子物体（包括 inactive）找到已有的 AnnouncementPanel
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "AnnouncementPanel" && !child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                    // AnnouncementUI 的 Awake() 会设置 CanvasGroup alpha=0 隐藏
                    return;
                }
            }

            // 如果场景中完全没有，才创建新的
            var panelGo = new GameObject("AnnouncementPanel", typeof(RectTransform), typeof(CanvasGroup));
            panelGo.transform.SetParent(canvas.transform, false);
            var panelRT = panelGo.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            // 半透明背景
            var bg = panelGo.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.4f);
            bg.raycastTarget = false;

            panelGo.AddComponent<AnnouncementUI>();
        }

        private void OnDestroy()
        {
            var net = NetworkManager.Instance;
            if (net != null)
            {
                net.OnConnected -= HandleConnected;
                net.OnDisconnected -= HandleDisconnected;
                net.OnMessageReceived -= HandleMessage;
                net.OnHeartbeatTimeout -= HandleHeartbeatTimeout;
            }

            if (orangeController != null)
                orangeController.OnReachedBoundary -= HandleBoundaryReached;
        }

        private void Update()
        {
            // 结算界面不自动关闭，等用户手动点击返回按钮
        }

        // ==================== 公开方法 ====================

        public void ConnectToServer()
        {
            ChangeState(GameState.Connecting);
            NetworkManager.Instance?.Connect();
        }

        public void RequestStartGame()
        {
            NetworkManager.Instance?.SendMessage("start_game");
        }

        public void RequestResetGame()
        {
            NetworkManager.Instance?.SendMessage("reset_game");
        }

        /// <summary>请求持久化排行榜数据</summary>
        public void RequestRanking(string period)
        {
            var json = $"{{\"type\":\"ranking_query\",\"data\":{{\"period\":\"{period}\"}}}}";
            NetworkManager.Instance?.SendJson(json);
        }

        public void RequestToggleSim(bool enabled)
        {
            var json = $"{{\"type\":\"toggle_sim\",\"data\":{{\"enabled\":{(enabled ? "true" : "false")}}}}}";
            NetworkManager.Instance?.SendJson(json);
        }

        /// <summary>服务器展示模式：6种礼物轮流送，间隔5秒</summary>
        public void RequestShowcaseSim()
        {
            var json = "{\"type\":\"toggle_sim\",\"data\":{\"enabled\":true,\"showcase\":true}}";
            NetworkManager.Instance?.SendJson(json);
        }

        /// <summary>审核演示模式：完整功能覆盖，按时间线编排（玩家入场→6种礼物→升级→666→点赞→混合互动→结算）</summary>
        public void RequestReviewSim()
        {
            var json = "{\"type\":\"toggle_sim\",\"data\":{\"enabled\":true,\"review\":true}}";
            NetworkManager.Instance?.SendJson(json);
        }

        /// <summary>密集展示模式：100人5秒入场 + 持续500ms/个高频送礼（宣传片录制专用）</summary>
        public void RequestDenseSim()
        {
            var json = "{\"type\":\"toggle_sim\",\"data\":{\"enabled\":true,\"dense\":true}}";
            NetworkManager.Instance?.SendJson(json);
        }

        /// <summary>请求玩家数据面板（当前参与玩家完整数据）</summary>
        public void RequestPlayerDataPanel()
        {
            var json = "{\"type\":\"player_data_query\",\"timestamp\":" +
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
            NetworkManager.Instance?.SendJson(json);
        }

        public void ResetGame()
        {
            // 强制清理VIP入场视频
            FindObjectOfType<VIPAnnouncementUI>()?.ForceCleanup();

            forceSystem?.Reset();
            campSystem?.Reset();
            rankingSystem?.Reset();
            spawner?.ClearAllCapybaras();
            Entity.Capybara.ResetSpawnCounter();
            orangeController?.ResetPosition();
            orangeController?.SetGameActive(false);
            Winner = null;
            RemainingTime = 0;
            // 清空贡献追踪
            _trackedPlayers.Clear();
            _totalScorePool = 0;
            _forceUpdateCount = 0;
            _clientOrangePos = 0;
            ChangeState(GameState.Idle);
        }

        // InjectMessage 已移除：客户端不再做本地模拟，所有数据来自服务器

        // ==================== 网络事件处理 ====================

        private void HandleConnected()
        {
            // 保持在 Idle，等服务器发 game_state
        }

        private void HandleDisconnected(string reason)
        {
            if (currentState != GameState.Idle)
                ChangeState(GameState.Idle);
        }

        /// <summary>服务器断线标志（OnGUI显示用）</summary>
        private bool _showDisconnectOverlay = false;

        private void HandleHeartbeatTimeout()
        {
            Debug.LogError("[GM] 服务器心跳超时，连接可能已断开！");
            _showDisconnectOverlay = true;
        }

        /// <summary>断线全屏提示（使用OnGUI确保始终可见，不依赖Canvas）</summary>
        private void OnGUI()
        {
            if (!_showDisconnectOverlay) return;

            // 半透明黑色遮罩
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // 居中提示文字
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.4f, 0.3f) }
            };
            float centerY = Screen.height * 0.35f;
            GUI.Label(new Rect(0, centerY, Screen.width, 70), "服务器连接已断开", style);

            // 子标题
            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            GUI.Label(new Rect(0, centerY + 80, Screen.width, 40), "请检查网络或联系技术支持", subStyle);

            // 关闭按钮
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnStyle.normal.textColor = Color.white;
            float btnW = 300, btnH = 70;
            float btnX = (Screen.width - btnW) / 2f;
            float btnY = centerY + 160;
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "关闭客户端", btnStyle))
            {
                Debug.Log("[GM] 用户确认关闭客户端（心跳超时）");
                Application.Quit();
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
            }
        }

        private void HandleMessage(string type, string dataJson)
        {
            switch (type)
            {
                case "game_state":
                    var gs = JsonUtility.FromJson<GameStateData>(dataJson);
                    HandleGameState(gs);
                    break;

                case "force_update":
                    var fu = JsonUtility.FromJson<ForceUpdateData>(dataJson);
                    float orangePos = fu.orangePos;
                    // 如果服务器orangePos为0但有推力差，客户端自行计算位置
                    if (Mathf.Abs(orangePos) < 0.01f && (fu.leftForce + fu.rightForce) > 0)
                    {
                        orangePos = CalcClientOrangePos(fu.leftForce, fu.rightForce);
                    }
                    forceSystem?.UpdateForce(fu.leftForce, fu.rightForce, orangePos);

                    // 客户端预测模式：更新推力+服务器校正位置
                    if (orangeController != null)
                    {
                        orangeController.UpdateForce(fu.leftForce, fu.rightForce);
                        orangeController.UpdateServerPosition(orangePos);
                    }
                    // 每个force_update都带剩余时间，实时更新倒计时
                    if (fu.remainingTime > 0)
                    {
                        RemainingTime = fu.remainingTime;
                        OnCountdownTick?.Invoke(fu.remainingTime);
                    }
                    _forceUpdateCount++;
                    break;

                case "player_joined":
                    var pj = JsonUtility.FromJson<PlayerJoinedData>(dataJson);
                    campSystem?.PlayerJoinedFull(pj);
                    // 追踪玩家（服务器+本地通用）
                    TrackPlayer(pj.playerId, pj.playerName, pj.camp);
                    // 生成基础单位（服务器+本地通用，统一由GameManager负责）
                    if (spawner != null)
                        spawner.SpawnBasicUnit(
                            pj.camp == "left" ? Entity.Camp.Left : Entity.Camp.Right,
                            pj.playerId, pj.playerName, pj.avatarUrl);
                    break;

                case "gift_received":
                    var gr = JsonUtility.FromJson<GiftReceivedData>(dataJson);
                    giftHandler?.HandleGift(gr);
                    // 追踪贡献值（服务器+本地通用）
                    TrackContribution(gr.playerId, gr.playerName, gr.camp, gr.forceValue);
                    break;

                case "player_upgrade":
                    var pu = JsonUtility.FromJson<PlayerUpgradeData>(dataJson);
                    HandlePlayerUpgrade(pu);
                    break;

                case "force_boost":
                    var fb = JsonUtility.FromJson<ForceBoostData>(dataJson);
                    OnForceBoost?.Invoke(fb);
                    break;

                case "like_boost":
                    var lb = JsonUtility.FromJson<LikeBoostData>(dataJson);
                    OnLikeBoost?.Invoke(lb);
                    break;

                case "countdown":
                    var cd = JsonUtility.FromJson<CountdownData>(dataJson);
                    RemainingTime = cd.remainingTime;
                    OnCountdownTick?.Invoke(cd.remainingTime);
                    break;

                case "game_ended":
                    var ge = JsonUtility.FromJson<GameEndedData>(dataJson);
                    // 如果服务器game_ended缺少结算数据，客户端用追踪数据补全
                    if (ge.mvp == null && _trackedPlayers.Count > 0)
                    {
                        Debug.LogWarning($"[GM] Server game_ended lacks settlement data, supplementing from {_trackedPlayers.Count} tracked players");
                        SupplementSettlementData(ge);
                    }
                    HandleGameEnded(ge);
                    break;

                case "ranking_update":
                    var ru = JsonUtility.FromJson<RankingUpdateData>(dataJson);
                    rankingSystem?.UpdateRankings(ru.left, ru.right, ru.streakInfo);
                    break;

                case "persistent_ranking":
                    var pr = JsonUtility.FromJson<PersistentRankingData>(dataJson);
                    OnPersistentRankingReceived?.Invoke(pr);
                    break;

                case "player_data_panel":
                    var pdp = JsonUtility.FromJson<PlayerDataPanelData>(dataJson);
                    OnPlayerDataPanelReceived?.Invoke(pdp);
                    break;

                case "heartbeat":
                case "sim_status":  // 服务器模拟状态回执，静默忽略
                    break;

                default:
                    break;
            }
        }

        /// <summary>处理玩家升级消息（仙女棒累积→Lv.1~10）</summary>
        private void HandlePlayerUpgrade(PlayerUpgradeData data)
        {
            if (data == null || string.IsNullOrEmpty(data.playerId)) return;

            // 找到该玩家的基础卡皮巴拉单位，应用升级视觉
            if (spawner != null)
            {
                spawner.ApplyUpgradeToPlayer(data.playerId, data.newLevel);
            }

            // 弹出升级通知UI
            var upgradeUI = FindObjectOfType<UpgradeNotificationUI>();
            if (upgradeUI != null)
            {
                upgradeUI.ShowUpgrade(data.playerName, data.camp, data.newLevel, data.fairyWandCount);
            }

            #if UNITY_EDITOR
            Debug.Log($"[GM] Player upgrade: {data.playerName} Lv.{data.oldLevel}→Lv.{data.newLevel} (仙女棒×{data.fairyWandCount})");
            #endif
        }

        private void HandleGameState(GameStateData data)
        {
            RemainingTime = data.remainingTime;
            OnCountdownTick?.Invoke(data.remainingTime); // 同步触发倒计时更新
            forceSystem?.UpdateForce(data.leftForce, data.rightForce, data.orangePos);

            switch (data.state)
            {
                case "idle":
                    if (currentState != GameState.Idle)
                        ChangeState(GameState.Idle);
                    break;
                case "countdown":
                case "running":
                    if (currentState != GameState.Running)
                    {
                        ChangeState(GameState.Running);
                        orangeController?.SetGameActive(true); // 激活到边检测
                    }
                    break;
                case "settlement":
                    // 不直接进入Settlement，由HandleGameEnded延迟处理
                    // 这样可以先显示"获胜"公告再打开结算面板
                    break;
            }
        }

        // HandleLocalSettlement 已移除：客户端不再做本地模拟

        private void HandleGameEnded(GameEndedData data)
        {
            Winner = data.winner;
            _lastEndedData = data;
            orangeController?.SetGameActive(false);

            // 强制清理VIP入场视频，防止视频在结算界面弹出后卡住
            FindObjectOfType<VIPAnnouncementUI>()?.ForceCleanup();

            // 先触发事件（AnnouncementUI会显示"获胜"公告）
            OnGameEnded?.Invoke(data);

            // 回传结算数据给服务器（供排行榜持久化）
            ReportSettlementToServer(data);

            // 延迟5秒再进入结算状态（公告4秒+淡出0.5秒，让玩家充分感受胜利时刻）
            StartCoroutine(DelayedSettlement(5.0f));
        }

        private IEnumerator DelayedSettlement(float delay)
        {
            yield return new WaitForSeconds(delay);
            ChangeState(GameState.Settlement);
        }

        /// <summary>最近一次结算数据（供结算界面延迟获取）</summary>
        public GameEndedData LastEndedData => _lastEndedData;
        private GameEndedData _lastEndedData;

        /// <summary>
        /// 橘子到达边界 → 判定胜负并走结算流程
        /// 客户端用追踪数据补全结算信息（数据来源全部是服务器推送）
        /// </summary>
        private void HandleBoundaryReached(string winner)
        {
            if (currentState != GameState.Running) return;

            string reason = winner == "left" ? "orange_reached_right" : "orange_reached_left";
            Debug.Log($"[GM] Boundary reached! Winner: {winner} (reason: {reason}), tracked players: {_trackedPlayers.Count}");

            float leftForce = forceSystem != null ? forceSystem.LeftForce : 0f;
            float rightForce = forceSystem != null ? forceSystem.RightForce : 0f;
            var endData = new GameEndedData
            {
                winner = winner,
                reason = reason,
                leftForce = leftForce,
                rightForce = rightForce
            };

            // 用客户端追踪的服务器数据补全结算信息
            if (_trackedPlayers.Count > 0)
            {
                SupplementSettlementData(endData);
            }

            HandleGameEnded(endData);
        }

        private void ChangeState(GameState newState)
        {
            if (currentState == newState) return;
            var old = currentState;
            currentState = newState;
            OnStateChanged?.Invoke(old, newState);

            // === 音频触发 ===
            var audio = Systems.AudioManager.Instance;
            if (audio != null)
            {
                switch (newState)
                {
                    case GameState.Running:
                        audio.PlaySFX("game_start");
                        audio.CrossfadeBGM("normal_battle", 1.5f);
                        break;
                    case GameState.Settlement:
                        audio.PlaySFX("victory");
                        audio.StopBGM();
                        break;
                    case GameState.Idle:
                        audio.StopBGM();
                        break;
                }
            }
        }

        // ==================== 玩家贡献追踪方法 ====================

        /// <summary>追踪玩家加入（记录玩家ID/名/阵营）</summary>
        private void TrackPlayer(string playerId, string playerName, string camp)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!_trackedPlayers.ContainsKey(playerId))
            {
                _trackedPlayers[playerId] = new TrackedPlayer
                {
                    playerId = playerId,
                    playerName = playerName,
                    camp = camp,
                    contribution = 0f
                };
            }
        }

        /// <summary>追踪礼物贡献值</summary>
        private void TrackContribution(string playerId, string playerName, string camp, float forceValue)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                // 没有ID的情况（可能是某些旧协议），用名字当key
                playerId = "name_" + playerName;
            }

            if (!_trackedPlayers.ContainsKey(playerId))
            {
                _trackedPlayers[playerId] = new TrackedPlayer
                {
                    playerId = playerId,
                    playerName = playerName,
                    camp = camp,
                    contribution = 0f
                };
            }

            _trackedPlayers[playerId].contribution += forceValue;
            _totalScorePool += forceValue;
            // 实时通知UI更新积分池
            OnScorePoolUpdated?.Invoke(_totalScorePool);

            // 实时更新排行榜（客户端统计，每次礼物后刷新Top3）
            UpdateClientRankings();
        }

        /// <summary>用客户端追踪数据实时更新排行榜</summary>
        private void UpdateClientRankings()
        {
            if (rankingSystem == null) return;

            var leftTop = _trackedPlayers.Values
                .Where(p => p.camp == "left" && p.contribution > 0)
                .OrderByDescending(p => p.contribution)
                .Take(3)
                .Select(p => new RankingEntry { name = p.playerName, contribution = p.contribution })
                .ToArray();

            var rightTop = _trackedPlayers.Values
                .Where(p => p.camp == "right" && p.contribution > 0)
                .OrderByDescending(p => p.contribution)
                .Take(3)
                .Select(p => new RankingEntry { name = p.playerName, contribution = p.contribution })
                .ToArray();

            rankingSystem.UpdateRankings(leftTop, rightTop);
        }

        /// <summary>用追踪数据补全结算数据（MVP/排行/积分分配）</summary>
        private void SupplementSettlementData(GameEndedData data)
        {
            var players = _trackedPlayers.Values.ToList();

            // MVP: 全局贡献最高的玩家
            TrackedPlayer mvpPlayer = null;
            foreach (var p in players)
            {
                if (p.contribution > 0 && (mvpPlayer == null || p.contribution > mvpPlayer.contribution))
                    mvpPlayer = p;
            }
            if (mvpPlayer != null && data.mvp == null)
            {
                data.mvp = new SettlementMVP
                {
                    playerId = mvpPlayer.playerId,
                    playerName = mvpPlayer.playerName,
                    camp = mvpPlayer.camp,
                    totalContribution = mvpPlayer.contribution
                };
            }

            // 左阵营Top10
            if (data.leftRankings == null || data.leftRankings.Length == 0)
            {
                data.leftRankings = BuildTrackedCampRankings("left", 10);
            }

            // 右阵营Top10
            if (data.rightRankings == null || data.rightRankings.Length == 0)
            {
                data.rightRankings = BuildTrackedCampRankings("right", 10);
            }

            // 积分池
            if (data.scorePool <= 0)
            {
                data.scorePool = _totalScorePool;
            }

            // 积分分配: Top6 按 30%/25%/20%/12%/8%/5% 分配
            if (data.scoreDistribution == null || data.scoreDistribution.Length == 0)
            {
                float[] ratios = { 0.30f, 0.25f, 0.20f, 0.12f, 0.08f, 0.05f };
                var allSorted = players
                    .Where(p => p.contribution > 0)
                    .OrderByDescending(p => p.contribution)
                    .Take(ratios.Length)
                    .ToArray();

                data.scoreDistribution = new ScoreDistribution[allSorted.Length];
                for (int i = 0; i < allSorted.Length; i++)
                {
                    data.scoreDistribution[i] = new ScoreDistribution
                    {
                        rank = i + 1,
                        playerName = allSorted[i].playerName,
                        coins = _totalScorePool * ratios[i]
                    };
                }
            }
        }

        /// <summary>从追踪数据构建阵营排行</summary>
        private SettlementRankEntry[] BuildTrackedCampRankings(string camp, int count)
        {
            return _trackedPlayers.Values
                .Where(p => p.camp == camp && p.contribution > 0)
                .OrderByDescending(p => p.contribution)
                .Take(count)
                .Select((p, i) => new SettlementRankEntry
                {
                    rank = i + 1,
                    playerId = p.playerId,
                    playerName = p.playerName,
                    contribution = p.contribution
                })
                .ToArray();
        }

        /// <summary>
        /// 客户端自行计算橘子位置（服务器orangePos=0时的后备方案）
        /// 累积增量模式：根据推力差产生微小位移
        /// </summary>
        private float CalcClientOrangePos(float leftForce, float rightForce)
        {
            float total = leftForce + rightForce;
            if (total > 0)
            {
                float forceRatio = (leftForce - rightForce) / total;
                float increment = forceRatio * 0.5f;
                _clientOrangePos = Mathf.Clamp(_clientOrangePos + increment, -30f, 30f);
            }
            return _clientOrangePos;
        }

        /// <summary>获取追踪的玩家总数（供外部调试用）</summary>
        public int TrackedPlayerCount => _trackedPlayers.Count;
        /// <summary>获取追踪的积分池（供外部调试用）</summary>
        public float TrackedScorePool => _totalScorePool;

        // ==================== 结算数据回传服务器 ====================

        /// <summary>将客户端生成的结算数据回传服务器（供排行榜持久化）</summary>
        private void ReportSettlementToServer(GameEndedData data)
        {
            var net = NetworkManager.Instance;
            if (net == null || !net.IsConnected) return;

            // 构建精简的结算报告JSON
            var report = new SettlementReport
            {
                winner = data.winner ?? "",
                reason = data.reason ?? "",
                leftForce = data.leftForce,
                rightForce = data.rightForce,
                scorePool = data.scorePool,
                trackedPlayerCount = _trackedPlayers.Count
            };

            // MVP信息
            if (data.mvp != null)
            {
                report.mvpPlayerId = data.mvp.playerId ?? "";
                report.mvpPlayerName = data.mvp.playerName ?? "";
                report.mvpCamp = data.mvp.camp ?? "";
                report.mvpContribution = data.mvp.totalContribution;
            }

            // 所有贡献玩家数据（服务器用于持久化排行）
            var contributions = _trackedPlayers.Values
                .Where(p => p.contribution > 0)
                .OrderByDescending(p => p.contribution)
                .Select(p => new PlayerContributionEntry
                {
                    playerId = p.playerId,
                    playerName = p.playerName,
                    camp = p.camp,
                    contribution = p.contribution
                })
                .ToArray();
            report.contributions = contributions;

            string json = $"{{\"type\":\"settlement_report\",\"data\":{JsonUtility.ToJson(report)}}}";
            net.SendJson(json);
            Debug.Log($"[GM] Settlement report sent to server: winner={data.winner}, players={contributions.Length}, pool={data.scorePool:F0}");
        }
    }

    /// <summary>结算报告（发送给服务器）</summary>
    [System.Serializable]
    public class SettlementReport
    {
        public string winner;
        public string reason;
        public float leftForce;
        public float rightForce;
        public float scorePool;
        public int trackedPlayerCount;
        // MVP
        public string mvpPlayerId;
        public string mvpPlayerName;
        public string mvpCamp;
        public float mvpContribution;
        // 所有贡献玩家
        public PlayerContributionEntry[] contributions;
    }

    /// <summary>玩家贡献条目（结算报告用）</summary>
    [System.Serializable]
    public class PlayerContributionEntry
    {
        public string playerId;
        public string playerName;
        public string camp;
        public float contribution;
    }
}
