using System;
using UnityEngine;
using CapybaraDuel.Core;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 礼物处理 - 收到礼物后决定召唤单位或通知UI
    /// </summary>
    public class GiftHandler : MonoBehaviour
    {
        private CapybaraSpawner _spawner;

        public event Action<GiftReceivedData> OnGiftReceived;

        public void Initialize(CapybaraSpawner spawner)
        {
            _spawner = spawner;
        }

        public void HandleGift(GiftReceivedData gift)
        {
            // 通知UI显示礼物
            OnGiftReceived?.Invoke(gift);

            // 如果是召唤类礼物，生成单位（传递送礼者信息+tier等级）
            if (gift.isSummon && _spawner != null)
            {
                var camp = gift.camp == "left" ? Entity.Camp.Left : Entity.Camp.Right;
                float lifetime = GetLifetimeByTier(gift.tier);
                for (int i = 0; i < gift.giftCount; i++)
                {
                    _spawner.SpawnCapybara(camp, gift.unitId, gift.forceValue / gift.giftCount, lifetime,
                        gift.playerId, gift.playerName, gift.avatarUrl, gift.tier);
                }
            }
        }

        private float GetLifetimeByTier(string tier)
        {
            // 使用数字tier匹配
            switch (tier)
            {
                case "2": return 30f;   // 能力药丸
                case "3": return 90f;   // 甜甜圈
                case "4": return 120f;  // 能量电池
                case "5": return 150f;  // 爱的爆炸
                case "6": return 160f;  // 神秘空投
                // 兼容旧字符串
                case "common": return 30f;
                case "rare": return 90f;
                case "epic": return 120f;
                case "legendary": return 160f;
                default: return 20f;
            }
        }
    }
}
