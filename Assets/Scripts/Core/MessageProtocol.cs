using System;
using System.Collections.Generic;
using UnityEngine;

namespace CapybaraDuel.Core
{
    // 通用消息包装
    [Serializable]
    public class ServerMessage
    {
        public string type;
        public long timestamp;
        public string data; // raw JSON, needs secondary parse
    }

    // game_state
    [Serializable]
    public class GameStateData
    {
        public string state;
        public float leftForce;
        public float rightForce;
        public float orangePos;
        public float remainingTime;
        public int leftCount;
        public int rightCount;
    }

    // force_update
    [Serializable]
    public class ForceUpdateData
    {
        public float leftForce;
        public float rightForce;
        public float orangePos;
        public float remainingTime; // 服务器每tick附带剩余时间
    }

    // player_joined
    [Serializable]
    public class PlayerJoinedData
    {
        public string playerId;
        public string playerName;
        public string avatarUrl;  // 抖音头像URL
        public string camp;
        public int totalLeft;
        public int totalRight;
        // VIP信息（周榜/月榜前20名）
        public bool isVip;
        public int vipRank;      // 排名 (1-20), 0 = 非VIP
        public string vipTitle;  // 例如 "周榜第3名"
        public string vipType;   // "weekly" 或 "monthly"
    }

    // gift_received
    [Serializable]
    public class GiftReceivedData
    {
        public string playerId;
        public string playerName;
        public string avatarUrl;    // 玩家头像URL（抖音推送）
        public string camp;
        public string giftId;
        public string giftName;
        public float forceValue;
        public bool isSummon;
        public string unitId;
        public int giftCount;
        public string tier;
    }

    // countdown
    [Serializable]
    public class CountdownData
    {
        public float remainingTime;
    }

    // game_ended（含完整结算数据）
    [Serializable]
    public class GameEndedData
    {
        public string winner;
        public string reason;
        public float leftForce;
        public float rightForce;
        // 结算扩展数据
        public SettlementMVP mvp;
        public SettlementRankEntry[] leftRankings;    // 左阵营Top10
        public SettlementRankEntry[] rightRankings;   // 右阵营Top10
        public float scorePool;                        // 积分池总额
        public ScoreDistribution[] scoreDistribution;  // 前N名积分分配
        public StreakInfo streakInfo;                   // 连胜信息
    }

    /// <summary>结算MVP信息</summary>
    [Serializable]
    public class SettlementMVP
    {
        public string playerId;
        public string playerName;
        public string avatarUrl;
        public string camp;
        public float totalContribution;
    }

    /// <summary>结算排行条目</summary>
    [Serializable]
    public class SettlementRankEntry
    {
        public int rank;
        public string playerId;
        public string playerName;
        public string avatarUrl;
        public float contribution;
        public int streakBet;    // 本局投入的连胜数
        public int streakGain;   // 连胜变化（正=瓜分获得+胜利，负=损失）
    }

    /// <summary>积分分配条目</summary>
    [Serializable]
    public class ScoreDistribution
    {
        public int rank;
        public string playerName;
        public string avatarUrl;        // 玩家头像URL
        public float contribution;      // 贡献值
        public float coins;             // 分配的积分
    }

    // ranking_update（含连胜信息）
    [Serializable]
    public class RankingUpdateData
    {
        public RankingEntry[] left;
        public RankingEntry[] right;
        public StreakInfo streakInfo;
    }

    [Serializable]
    public class RankingEntry
    {
        public string id;
        public string name;
        public string avatarUrl;  // 抖音头像URL（服务端从推送数据中提取）
        public float contribution;
        public int streakBet;     // 该玩家本局投注的连胜数
    }

    // sim_status
    [Serializable]
    public class SimStatusData
    {
        public bool enabled;
    }

    // 客户端发送的消息
    [Serializable]
    public class ClientMessage
    {
        public string type;
        public long timestamp;
    }

    [Serializable]
    public class ToggleSimMessage
    {
        public string type = "toggle_sim";
        public ToggleSimData data;
    }

    [Serializable]
    public class ToggleSimData
    {
        public bool enabled;
    }

    // ==================== 排行榜持久化协议 ====================

    /// <summary>客户端请求持久化排行</summary>
    [Serializable]
    public class RankingQueryData
    {
        public string period; // "weekly" | "monthly" | "hourly" | "streak"
    }

    /// <summary>服务器返回持久化排行</summary>
    [Serializable]
    public class PersistentRankingData
    {
        public string period;
        public PersistentRankEntry[] entries;
    }

    [Serializable]
    public class PersistentRankEntry
    {
        public int rank;
        public string playerId;
        public string playerName;
        public string avatarUrl;
        public float score;
        public int wins;            // 总胜场
        public int streak;          // 历史最高连胜
        public int currentStreak;   // 当前连胜
    }

    // ==================== 玩家升级数据 ====================

    /// <summary>服务器推送的玩家升级事件（仙女棒累积升级 Lv.1~10）</summary>
    [Serializable]
    public class PlayerUpgradeData
    {
        public string playerId;
        public string playerName;
        public string avatarUrl;
        public string camp;
        public int oldLevel;
        public int newLevel;
        public int fairyWandCount;
    }

    // ==================== 推力提升事件 ====================

    /// <summary>服务器推送的666推力提升事件</summary>
    [Serializable]
    public class ForceBoostData
    {
        public string playerId;
        public string playerName;
        public string camp;
        public float forceValue;
        public float duration;
        public string keyword;
    }

    /// <summary>服务器推送的点赞推力提升事件</summary>
    [Serializable]
    public class LikeBoostData
    {
        public string playerId;
        public string playerName;
        public string camp;
        public int likeNum;
        public float forcePerLike;
        public float forceValue;
        public float duration;
    }

    // ==================== 连胜数据 ====================

    /// <summary>连胜信息（左右阵营各一个最高连胜者）</summary>
    [Serializable]
    public class StreakInfo
    {
        public StreakPlayerInfo left;
        public StreakPlayerInfo right;
    }

    /// <summary>单个阵营的连胜最高者</summary>
    [Serializable]
    public class StreakPlayerInfo
    {
        public int streak;
        public string name;
    }

    // ==================== 玩家数据面板协议 ====================

    /// <summary>服务器返回的玩家数据面板（当前参与玩家完整数据）</summary>
    [Serializable]
    public class PlayerDataPanelData
    {
        public PlayerDataEntry[] players;
        public int totalCount;
    }

    /// <summary>单个玩家的完整数据条目</summary>
    [Serializable]
    public class PlayerDataEntry
    {
        public string playerId;
        public string playerName;
        public string avatarUrl;
        public string camp;
        public int contribution;      // 当局贡献分
        public int weeklyRank;        // 周榜排名 (0=未上榜)
        public int monthlyRank;       // 月榜排名
        public int streakRank;        // 连胜榜排名
        public int hourlyRank;        // 小时榜排名
        public int currentStreak;     // 当前连胜数
        public int weeklyScore;       // 周积分
        public int monthlyScore;      // 月积分
    }
}
