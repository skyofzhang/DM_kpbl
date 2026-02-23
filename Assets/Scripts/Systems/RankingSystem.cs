using System;
using UnityEngine;
using CapybaraDuel.Core;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 排行榜系统 - 维护左右阵营Top4 + 连胜信息
    /// </summary>
    public class RankingSystem : MonoBehaviour
    {
        public RankingEntry[] LeftRankings { get; private set; } = Array.Empty<RankingEntry>();
        public RankingEntry[] RightRankings { get; private set; } = Array.Empty<RankingEntry>();

        /// <summary>左右阵营连胜信息（每次ranking_update更新）</summary>
        public StreakInfo CurrentStreakInfo { get; private set; }

        public event Action OnRankingsUpdated;

        public void UpdateRankings(RankingEntry[] left, RankingEntry[] right, StreakInfo streakInfo = null)
        {
            LeftRankings = left ?? Array.Empty<RankingEntry>();
            RightRankings = right ?? Array.Empty<RankingEntry>();
            if (streakInfo != null)
                CurrentStreakInfo = streakInfo;
            OnRankingsUpdated?.Invoke();
        }

        public void Reset()
        {
            LeftRankings = Array.Empty<RankingEntry>();
            RightRankings = Array.Empty<RankingEntry>();
            CurrentStreakInfo = null;
            OnRankingsUpdated?.Invoke();
        }
    }
}
