using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Utils;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 玩家数据面板 — 显示当前参与玩家的完整排名/统计数据
    /// 挂载在Canvas上（始终active），通过panelRoot.SetActive控制面板显隐
    ///
    /// 每行固定坐标布局（不使用LayoutGroup，避免列宽被挤压）:
    ///   序号(40) | 阵营色条(6) | 头像(28) | 名字(140) | 贡献(85) | 周榜(70) | 月榜(70) | 连胜(80) | 时榜(70) | 胜点(65)
    ///   总宽 ≈ 654px + 间距 → 配合1080px宽度居中
    /// </summary>
    public class PlayerDataPanelUI : MonoBehaviour
    {
        [Header("Panel")]
        public Button btnOpen;         // BottomBar上的"玩家数据"按钮
        public Button btnClose;        // 面板关闭按钮
        public GameObject panelRoot;   // 面板根对象(SetActive控制)

        [Header("Title")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText; // 阵营人数分布

        [Header("Scroll Content")]
        public Transform contentParent; // ScrollView/Viewport/Content

        private const int MAX_ROWS = 50;

        private struct PlayerRow
        {
            public GameObject root;
            public Image rowBg;           // 行底色（按阵营区分）
            public Image campIndicator;   // 阵营色条
            public Image avatar;          // 头像
            public TextMeshProUGUI rankText;    // 序号
            public TextMeshProUGUI nameText;    // 名字
            public TextMeshProUGUI contribText; // 当局贡献
            public TextMeshProUGUI weeklyText;  // 周榜排名
            public TextMeshProUGUI monthlyText; // 月榜排名
            public TextMeshProUGUI streakText;  // 连胜
            public TextMeshProUGUI hourlyText;  // 时榜排名
            public TextMeshProUGUI spText;      // 胜点
        }

        private PlayerRow[] _rows = new PlayerRow[MAX_ROWS];
        private string[] _cachedAvatarUrls = new string[MAX_ROWS];
        private TMP_FontAsset _chineseFont;
        private float _lastRequestTime;
        private const float REQUEST_COOLDOWN = 2f;

        // 阵营色条颜色
        private static readonly Color COL_LEFT_BAR = new Color(1f, 0.55f, 0f);       // 橙
        private static readonly Color COL_RIGHT_BAR = new Color(0.4f, 0.9f, 0.2f);   // 绿

        // 阵营行底色（半透明）
        private static readonly Color COL_LEFT_ROW_EVEN = new Color(0.35f, 0.18f, 0.05f, 0.55f);   // 暖橙底（偶数行）
        private static readonly Color COL_LEFT_ROW_ODD = new Color(0.30f, 0.15f, 0.04f, 0.55f);    // 暖橙底（奇数行）
        private static readonly Color COL_RIGHT_ROW_EVEN = new Color(0.05f, 0.25f, 0.12f, 0.55f);  // 冷绿底（偶数行）
        private static readonly Color COL_RIGHT_ROW_ODD = new Color(0.04f, 0.20f, 0.10f, 0.55f);   // 冷绿底（奇数行）

        private void Start()
        {
            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");

            // 查找预创建的行
            if (contentParent != null)
            {
                for (int i = 0; i < MAX_ROWS; i++)
                {
                    var row = contentParent.Find($"PlayerRow_{i}");
                    if (row == null) break;

                    _rows[i].root = row.gameObject;
                    _rows[i].rowBg = row.GetComponent<Image>();
                    _rows[i].campIndicator = FindImage(row, "CampIndicator");
                    _rows[i].avatar = FindImage(row, "AvatarImg");
                    _rows[i].rankText = FindTMP(row, "RankText");
                    _rows[i].nameText = FindTMP(row, "NameText");
                    _rows[i].contribText = FindTMP(row, "ContribText");
                    _rows[i].weeklyText = FindTMP(row, "WeeklyRankText");
                    _rows[i].monthlyText = FindTMP(row, "MonthlyRankText");
                    _rows[i].streakText = FindTMP(row, "StreakRankText");
                    _rows[i].hourlyText = FindTMP(row, "HourlyRankText");
                    _rows[i].spText = FindTMP(row, "SPText");

                    row.gameObject.SetActive(false); // 默认隐藏所有行
                }
            }

            // 按钮绑定
            if (btnOpen != null)
                btnOpen.onClick.AddListener(OnOpenClicked);
            if (btnClose != null)
                btnClose.onClick.AddListener(Hide);

            // 订阅服务器数据返回
            var gm = GameManager.Instance;
            if (gm != null)
                gm.OnPlayerDataPanelReceived += HandlePlayerDataReceived;

            // 初始隐藏面板
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null)
                gm.OnPlayerDataPanelReceived -= HandlePlayerDataReceived;
        }

        private void OnOpenClicked()
        {
            // 请求冷却（防抖）
            if (Time.unscaledTime - _lastRequestTime < REQUEST_COOLDOWN)
                return;
            _lastRequestTime = Time.unscaledTime;

            GameManager.Instance?.RequestPlayerDataPanel();

            // 先显示面板（loading状态），数据到达后刷新
            if (panelRoot != null)
                panelRoot.SetActive(true);
            SetTMPText(titleText, "加载中...");
            SetTMPText(subtitleText, "");
        }

        public void Hide()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void HandlePlayerDataReceived(PlayerDataPanelData data)
        {
            // 只在面板可见时处理
            if (panelRoot == null || !panelRoot.activeSelf) return;

            var players = data.players;
            int count = players != null ? Mathf.Min(players.Length, MAX_ROWS) : 0;

            // 统计阵营人数
            int leftCount = 0, rightCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (players[i].camp == "left") leftCount++;
                else rightCount++;
            }

            SetTMPText(titleText, $"参战玩家数据 ({data.totalCount}人)");
            SetTMPText(subtitleText,
                $"<color=#FF8C00>橙方 {leftCount}人</color>  |  <color=#66FF33>绿方 {rightCount}人</color>");

            // 填充行数据
            for (int i = 0; i < MAX_ROWS; i++)
            {
                if (_rows[i].root == null) continue;

                if (i < count)
                {
                    _rows[i].root.SetActive(true);
                    FillRow(i, players[i]);
                }
                else
                {
                    _rows[i].root.SetActive(false);
                }
            }
        }

        private void FillRow(int index, PlayerDataEntry entry)
        {
            var row = _rows[index];
            bool isLeft = entry.camp == "left";

            // 行底色 — 按阵营+奇偶行交替
            if (row.rowBg != null)
            {
                if (isLeft)
                    row.rowBg.color = (index % 2 == 0) ? COL_LEFT_ROW_EVEN : COL_LEFT_ROW_ODD;
                else
                    row.rowBg.color = (index % 2 == 0) ? COL_RIGHT_ROW_EVEN : COL_RIGHT_ROW_ODD;
            }

            // 阵营色条
            if (row.campIndicator != null)
                row.campIndicator.color = isLeft ? COL_LEFT_BAR : COL_RIGHT_BAR;

            // 序号（加大字号）
            SetTMPText(row.rankText, (index + 1).ToString(), 24);

            // 名字（加大字号，扩展截断到8字符）
            SetTMPText(row.nameText, TruncateName(entry.playerName, 8), 22);

            // 当局贡献（加大字号）
            SetTMPText(row.contribText, FormatNumber(entry.contribution), 20);

            // 连胜（加大字号，醒目显示）
            SetTMPText(row.streakText, entry.currentStreak > 0
                ? $"{entry.currentStreak}连胜" : "-", 20);

            // === 隐藏不常用列，减少信息密度 ===
            if (row.weeklyText != null) { row.weeklyText.text = ""; row.weeklyText.gameObject.SetActive(false); }
            if (row.monthlyText != null) { row.monthlyText.text = ""; row.monthlyText.gameObject.SetActive(false); }
            if (row.hourlyText != null) { row.hourlyText.text = ""; row.hourlyText.gameObject.SetActive(false); }
            if (row.spText != null) { row.spText.text = ""; row.spText.gameObject.SetActive(false); }

            // 头像（放大到44×44，确保清晰可见）
            if (row.avatar != null)
            {
                row.avatar.rectTransform.sizeDelta = new Vector2(44, 44);

                if (!string.IsNullOrEmpty(entry.avatarUrl))
                {
                    if (_cachedAvatarUrls[index] != entry.avatarUrl)
                    {
                        _cachedAvatarUrls[index] = entry.avatarUrl;
                        var img = row.avatar;
                        AvatarLoader.Instance?.Load(entry.avatarUrl, tex =>
                        {
                            if (img != null && tex != null)
                                img.sprite = AvatarLoader.TextureToSprite(tex);
                        });
                    }
                }
            }
        }

        // ==================== 工具方法 ====================

        private void SetTMPText(TextMeshProUGUI tmp, string text, float fontSize = 0)
        {
            if (tmp == null) return;
            if (_chineseFont != null) tmp.font = _chineseFont;
            tmp.text = text;
            if (fontSize > 0) tmp.fontSize = fontSize;
        }

        private string FormatRank(int rank)
        {
            return rank > 0 ? $"#{rank}" : "-";
        }

        private string FormatNumber(int value)
        {
            if (value >= 10000)
                return $"{value / 10000f:F1}万";
            return value.ToString();
        }

        private string TruncateName(string name, int maxLen)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Length > maxLen ? name.Substring(0, maxLen) + ".." : name;
        }

        private TextMeshProUGUI FindTMP(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private Image FindImage(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }
    }
}
