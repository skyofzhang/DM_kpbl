using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Systems;
using CapybaraDuel.Utils;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 荣誉玩家列表UI - 两侧各显示Top3
    /// 每个玩家卡片结构（竖向）:
    ///   排名数字(RankText) → 头像框(AvatarFrame+PlayerAvatar) → 玩家名(PlayerName) → 推力(PlayerForce) → 连胜(PlayerStreak)
    /// 左侧排列: 3, 2, 1 (从左到右)
    /// 右侧排列: 1, 2, 3 (从左到右)
    /// 面板位置由场景设定（不运行时覆盖）
    /// </summary>
    public class PlayerListUI : MonoBehaviour
    {
        [Header("References")]
        public Transform leftListContainer;
        public Transform rightListContainer;
        public TextMeshProUGUI leftCountText;
        public TextMeshProUGUI rightCountText;

        private RankingSystem _rankingSystem;
        private CampSystem _campSystem;

        // 每个玩家卡片的组件引用
        private struct PlayerCard
        {
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI forceText;
            public TextMeshProUGUI streakText;
            public TextMeshProUGUI rankText;
            public Image avatar;
            public GameObject cardRoot;
        }

        private PlayerCard[] _leftCards = new PlayerCard[3];
        private PlayerCard[] _rightCards = new PlayerCard[3];
        private string[] _leftAvatarUrls = new string[3];
        private string[] _rightAvatarUrls = new string[3];

        // 左侧排列: 场景中 Row_0=排名3, Row_1=排名2, Row_2=排名1
        private int[] _leftRankMap = { 3, 2, 1 };
        // 右侧排列: 场景中 Row_0=排名1, Row_1=排名2, Row_2=排名3
        private int[] _rightRankMap = { 1, 2, 3 };

        private void Start()
        {
            _rankingSystem = FindObjectOfType<RankingSystem>();
            _campSystem = FindObjectOfType<CampSystem>();

            if (_rankingSystem != null)
                _rankingSystem.OnRankingsUpdated += UpdateRankings;
            if (_campSystem != null)
                _campSystem.OnPlayerJoined += HandlePlayerJoined;

            FindCards(leftListContainer, _leftCards);
            FindCards(rightListContainer, _rightCards);
        }

        private void OnDestroy()
        {
            if (_rankingSystem != null)
                _rankingSystem.OnRankingsUpdated -= UpdateRankings;
            if (_campSystem != null)
                _campSystem.OnPlayerJoined -= HandlePlayerJoined;
        }

        /// <summary>查找卡片结构中的各组件</summary>
        private void FindCards(Transform container, PlayerCard[] cards)
        {
            if (container == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                var row = container.Find($"PlayerRow_{i}");
                if (row == null) continue;

                cards[i].cardRoot = row.gameObject;
                cards[i].nameText = FindTMP(row, "PlayerName");
                cards[i].forceText = FindTMP(row, "PlayerForce");
                cards[i].streakText = FindTMP(row, "PlayerStreak");
                cards[i].rankText = FindTMP(row, "RankText");

                // 强制放大字号，确保清晰可读
                if (cards[i].nameText != null && cards[i].nameText.fontSize < 22f)
                    cards[i].nameText.fontSize = 24f;
                if (cards[i].forceText != null && cards[i].forceText.fontSize < 18f)
                    cards[i].forceText.fontSize = 20f;
                if (cards[i].streakText != null && cards[i].streakText.fontSize < 18f)
                    cards[i].streakText.fontSize = 20f;
                if (cards[i].rankText != null && cards[i].rankText.fontSize < 28f)
                    cards[i].rankText.fontSize = 32f;

                // 头像: AvatarContainer/PlayerAvatar
                var avatarContainer = row.Find("AvatarContainer");
                if (avatarContainer != null)
                {
                    var avatarObj = avatarContainer.Find("PlayerAvatar");
                    if (avatarObj != null)
                        cards[i].avatar = avatarObj.GetComponent<Image>();
                }
            }
        }

        private TextMeshProUGUI FindTMP(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private void HandlePlayerJoined(string playerId, string playerName, string camp)
        {
            if (_campSystem == null) return;
            if (leftCountText) leftCountText.text = $"({_campSystem.LeftCount}人)";
            if (rightCountText) rightCountText.text = $"({_campSystem.RightCount}人)";
        }

        private void UpdateRankings()
        {
            if (_rankingSystem == null) return;

            UpdateCards(_leftCards, _leftAvatarUrls, _leftRankMap, _rankingSystem.LeftRankings);
            UpdateCards(_rightCards, _rightAvatarUrls, _rightRankMap, _rankingSystem.RightRankings);
        }

        /// <summary>更新卡片内容</summary>
        private void UpdateCards(PlayerCard[] cards, string[] cachedUrls, int[] rankMap, RankingEntry[] rankings)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i].cardRoot == null) continue;

                // rankMap[i] 是这个卡片显示的排名（1/2/3）
                int displayRank = rankMap[i];
                // 排名对应的数据索引（排名1=index0, 排名2=index1, 排名3=index2）
                int dataIdx = displayRank - 1;

                if (dataIdx < rankings.Length)
                {
                    var r = rankings[dataIdx];
                    cards[i].cardRoot.SetActive(true);

                    // 排名
                    if (cards[i].rankText != null)
                        cards[i].rankText.text = $"{displayRank}";

                    // 名字（截断）
                    if (cards[i].nameText != null)
                    {
                        string shortName = r.name.Length > 5 ? r.name.Substring(0, 5) + ".." : r.name;
                        cards[i].nameText.text = shortName;
                    }

                    // 推力
                    if (cards[i].forceText != null)
                    {
                        string contribStr = r.contribution >= 10000
                            ? $"推力:{r.contribution / 10000f:F1}万"
                            : $"推力:{r.contribution:F0}";
                        cards[i].forceText.text = contribStr;
                    }

                    // 连胜：显示该玩家个人的投注数
                    if (cards[i].streakText != null)
                    {
                        cards[i].streakText.text = $"连胜:{r.streakBet}";
                    }

                    // 头像
                    if (cards[i].avatar != null)
                    {
                        string url = r.avatarUrl;
                        if (!string.IsNullOrEmpty(url))
                        {
                            cards[i].avatar.gameObject.SetActive(true);
                            if (cachedUrls[i] != url)
                            {
                                cachedUrls[i] = url;
                                var img = cards[i].avatar;
                                AvatarLoader.Instance?.Load(url, tex =>
                                {
                                    if (img != null && tex != null)
                                        img.sprite = AvatarLoader.TextureToSprite(tex);
                                });
                            }
                        }
                        else
                        {
                            cards[i].avatar.gameObject.SetActive(false);
                            cachedUrls[i] = null;
                        }
                    }
                }
                else
                {
                    // 没有数据，隐藏卡片或显示空
                    if (cards[i].nameText != null) cards[i].nameText.text = "";
                    if (cards[i].forceText != null) cards[i].forceText.text = "";
                    if (cards[i].streakText != null) cards[i].streakText.text = "";
                    if (cards[i].avatar != null)
                    {
                        cards[i].avatar.gameObject.SetActive(false);
                        cachedUrls[i] = null;
                    }
                }
            }
        }
    }
}
