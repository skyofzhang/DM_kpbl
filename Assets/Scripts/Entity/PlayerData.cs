using System;

namespace CapybaraDuel.Entity
{
    /// <summary>
    /// 玩家数据
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string playerId;
        public string playerName;
        public Camp camp;
        public int duckLevel;           // 小黄鸭等级 1-10
        public int cumulativeDuckCount; // 累积小黄鸭数量
        public float baseForce;         // 基础推力
        public float contribution;      // 贡献值

        public PlayerData(string id, string name, Camp camp)
        {
            this.playerId = id;
            this.playerName = name;
            this.camp = camp;
            this.duckLevel = 0;
            this.cumulativeDuckCount = 0;
            this.baseForce = 10f; // 初始推力
            this.contribution = 0f;
        }
    }
}
