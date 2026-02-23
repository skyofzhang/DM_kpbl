using System;
using System.Collections.Generic;
using UnityEngine;
using CapybaraDuel.Core;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 阵营系统 - 管理玩家加入阵营事件 + VIP检测
    /// </summary>
    public class CampSystem : MonoBehaviour
    {
        public int LeftCount { get; private set; }
        public int RightCount { get; private set; }

        public event Action<string, string, string> OnPlayerJoined; // playerId, playerName, camp
        public event Action<PlayerJoinedData> OnVIPJoined;          // VIP玩家加入（周榜/月榜前20）

        public void PlayerJoined(string playerId, string playerName, string camp, int totalLeft, int totalRight)
        {
            LeftCount = totalLeft;
            RightCount = totalRight;
            OnPlayerJoined?.Invoke(playerId, playerName, camp);
        }

        /// <summary>带VIP信息的玩家加入（从完整PlayerJoinedData调用）</summary>
        public void PlayerJoinedFull(PlayerJoinedData data)
        {
            LeftCount = data.totalLeft;
            RightCount = data.totalRight;
            OnPlayerJoined?.Invoke(data.playerId, data.playerName, data.camp);

            // VIP检测
            if (data.isVip && data.vipRank > 0)
            {
                OnVIPJoined?.Invoke(data);
            }
        }

        public void Reset()
        {
            LeftCount = 0;
            RightCount = 0;
        }
    }
}
