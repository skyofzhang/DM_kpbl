using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Systems;
using CapybaraDuel.Utils;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 排行榜面板UI - 积分周榜/月榜/连胜榜/小时榜
    /// 由 SceneGenerator 生成，MainMenuUI 调用 Show/Hide
    /// 数据来源：服务器推送的持久化排行榜数据
    /// </summary>
    public class RankingPanelUI : MonoBehaviour
    {
        [Header("Tabs")]
        public Button[] tabButtons;          // 4个标签按钮
        public Image[] tabImages;            // 4个标签图片（用于切换选中态）
        public Sprite tabNormalSprite;       // 普通状态 sprite
        public Sprite tabSelectedSprite;     // 选中状态 sprite

        [Header("Top 3")]
        public TextMeshProUGUI[] top3Names;   // [0]=1st, [1]=2nd, [2]=3rd
        public TextMeshProUGUI[] top3Scores;  // 贡献值
        public Image[] top3Avatars;           // 头像图片
        public Image[] top3AvatarFrames;      // 头像框

        [Header("Rank List (4-100名, ScrollView preferred)")]
        public RectTransform rankScrollContent;  // ScrollView/Viewport/Content (动态100行)
        public TextMeshProUGUI[] rankNumbers;    // 兼容旧场景：固定TMP数组
        public TextMeshProUGUI[] rankNames;
        public TextMeshProUGUI[] rankScores;

        [Header("Close")]
        public Button btnClose;

        [Header("Reset Time")]
        public TextMeshProUGUI resetTimeText;  // 排行榜重置时间说明

        private int _currentTab = 0;
        private TMP_FontAsset _chineseFont;

        // Top3 排名数字（运行时创建）
        private TextMeshProUGUI[] _top3RankNumbers;

        // 排行数据缓存（由模拟器或服务器推送）
        private RankingData[] _cachedData = null;

        // Tab → 服务器查询周期映射
        private static readonly string[] TAB_PERIODS = { "weekly", "monthly", "streak", "hourly" };

        private static readonly string[] TAB_RESET_DESCRIPTIONS = {
            "每周日0点重置榜单",
            "每月最后1日9点重置榜单",
            "每周日0点重置榜单",
            "每小时整点重置榜单"
        };

        // 各Tab缓存
        private Dictionary<int, RankingData[]> _tabCache = new Dictionary<int, RankingData[]>();

        /// <summary>排行数据结构</summary>
        public class RankingData
        {
            public string name;
            public string avatarUrl;
            public float score;
            public int streak;          // 历史最高连胜（连胜榜用）
            public int currentStreak;   // 当前连胜
        }

        private void Start()
        {
            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");

            // 运行时保障：Top3 贡献分文字（如果场景里没有，自动创建）
            EnsureTop3Scores();

            // 运行时保障：重置时间说明文字
            EnsureResetTimeText();

            // 运行时保障：Top3 排名数字（显示在头像上方）
            EnsureTop3RankNumbers();

            // 运行时排版保障：确保列表行的 TMP 对齐方式正确
            EnsureListLayout();

            // 绑定标签点击
            if (tabButtons != null)
            {
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    int idx = i;
                    if (tabButtons[i] != null)
                        tabButtons[i].onClick.AddListener(() => SwitchTab(idx));
                }
            }

            // 关闭按钮
            if (btnClose != null)
                btnClose.onClick.AddListener(Hide);

            // 订阅服务器持久化排行返回
            var gm = GameManager.Instance;
            if (gm != null)
                gm.OnPersistentRankingReceived += HandlePersistentRanking;

            // 默认选中第一个标签
            SwitchTab(0);
        }

        /// <summary>运行时保障：如果 Top3Score 文本未绑定，自动查找或创建</summary>
        private void EnsureTop3Scores()
        {
            if (top3Scores == null || top3Scores.Length < 3)
                top3Scores = new TextMeshProUGUI[3];

            // 用户手动校准坐标（来自 MEMORY.md，严禁代码覆盖位置/大小）
            // Top3Score: #1(0,334) 280×30, #2(-310,252) 280×30, #3(310,246) 280×30
            float[][] scorePositions = {
                new float[]{ 0f, 334f },
                new float[]{ -310f, 252f },
                new float[]{ 310f, 246f }
            };

            for (int i = 0; i < 3; i++)
            {
                if (top3Scores[i] != null) continue;

                // 先尝试在面板下查找
                var existing = transform.Find($"Top3Score_{i}");
                if (existing != null)
                {
                    top3Scores[i] = existing.GetComponent<TextMeshProUGUI>();
                    if (top3Scores[i] != null)
                    {
                        Debug.Log($"[RankingPanelUI] Top3Score_{i} 找到并绑定");
                        continue;
                    }
                }

                // 不存在则创建
                var go = new GameObject($"Top3Score_{i}");
                go.transform.SetParent(transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(scorePositions[i][0], scorePositions[i][1]);
                rt.sizeDelta = new Vector2(280, 30);
                rt.localScale = Vector3.one * 1.44f;

                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 22;
                tmp.color = new Color(1f, 0.92f, 0.7f); // 暖金色
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                if (_chineseFont != null) tmp.font = _chineseFont;

                top3Scores[i] = tmp;
                Debug.Log($"[RankingPanelUI] Top3Score_{i} 运行时自动创建");
            }

            // === 强制保障可见性 ===
            for (int i = 0; i < 3; i++)
            {
                if (top3Scores[i] == null) continue;
                // 确保渲染在最上层（在名字、头像之后）
                top3Scores[i].transform.SetAsLastSibling();
                // 确保字体已绑定（无字体时 TMP 不渲染任何东西）
                if (top3Scores[i].font == null && _chineseFont != null)
                    top3Scores[i].font = _chineseFont;
                // 确保 overflow 不裁剪
                top3Scores[i].enableWordWrapping = false;
                top3Scores[i].overflowMode = TextOverflowModes.Overflow;
                // 加描边确保可见
                top3Scores[i].outlineWidth = 0.25f;
                top3Scores[i].outlineColor = new Color32(0, 0, 0, 180);
                // 打印调试信息
                Debug.Log($"[RankingPanelUI] Top3Score_{i} 可见性保障完成: pos={top3Scores[i].rectTransform.anchoredPosition}, font={top3Scores[i].font?.name ?? "NULL"}");
            }
        }

        /// <summary>运行时保障：重置时间说明文字（查找或创建）</summary>
        private void EnsureResetTimeText()
        {
            if (resetTimeText != null) return;

            // 先查找已有
            var existing = transform.Find("ResetTimeText");
            if (existing != null)
            {
                resetTimeText = existing.GetComponent<TextMeshProUGUI>();
                if (resetTimeText != null)
                {
                    Debug.Log("[RankingPanelUI] ResetTimeText 找到并绑定");
                    return;
                }
            }

            // 不存在则创建（在BtnClose下方）
            var go = new GameObject("ResetTimeText");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, -820);
            rt.sizeDelta = new Vector2(600, 40);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            // 必须先设字体再设文字，否则中文乱码
            if (_chineseFont != null) tmp.font = _chineseFont;
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.92f, 0.7f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.outlineWidth = 0.35f;
            tmp.outlineColor = new Color32(0, 0, 0, 220);
            tmp.text = TAB_RESET_DESCRIPTIONS[0];
            tmp.ForceMeshUpdate();

            resetTimeText = tmp;
            Debug.Log("[RankingPanelUI] ResetTimeText 运行时自动创建");
        }

        /// <summary>运行时保障：隐藏场景中多余的Top3排名数字对象</summary>
        /// <remarks>排名已由视觉层级隐含表达（中=第1,左=第2,右=第3 + 金/银/铜头像框）</remarks>
        private void EnsureTop3RankNumbers()
        {
            // 隐藏场景中的 Top3Num_X 和可能残留的 Top3Rank_X，避免数字叠在头像上
            for (int i = 0; i < 3; i++)
            {
                var existing = transform.Find($"Top3Num_{i}");
                if (existing != null)
                    existing.gameObject.SetActive(false);

                var fallback = transform.Find($"Top3Rank_{i}");
                if (fallback != null)
                    fallback.gameObject.SetActive(false);
            }
            _top3RankNumbers = null;
        }

        private void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null)
                gm.OnPersistentRankingReceived -= HandlePersistentRanking;
        }

        /// <summary>处理服务器返回的持久化排行数据</summary>
        private void HandlePersistentRanking(PersistentRankingData data)
        {
            // 找到对应Tab索引
            int tabIdx = System.Array.IndexOf(TAB_PERIODS, data.period);
            if (tabIdx < 0) tabIdx = 0;

            // 转换为UI数据（保留avatarUrl用于加载头像，连胜榜附带streak字段）
            var uiData = data.entries?
                .Select(e => new RankingData {
                    name = e.playerName,
                    avatarUrl = e.avatarUrl,
                    score = e.score,
                    streak = e.streak,
                    currentStreak = e.currentStreak
                })
                .ToArray() ?? new RankingData[0];

            _tabCache[tabIdx] = uiData;

            // 如果当前就是这个Tab，立即刷新
            if (_currentTab == tabIdx)
            {
                _cachedData = uiData;
                RefreshUI(uiData);
            }
        }

        /// <summary>切换标签页</summary>
        public void SwitchTab(int index)
        {
            _currentTab = index;

            // 更新标签视觉
            if (tabImages != null && tabNormalSprite != null && tabSelectedSprite != null)
            {
                for (int i = 0; i < tabImages.Length; i++)
                {
                    if (tabImages[i] != null)
                        tabImages[i].sprite = (i == index) ? tabSelectedSprite : tabNormalSprite;
                }
            }

            // 优先使用Tab缓存
            if (_tabCache.TryGetValue(index, out var cached) && cached.Length > 0)
            {
                _cachedData = cached;
                RefreshUI(cached);
            }
            else
            {
                // 向服务器请求排行数据
                string period = index < TAB_PERIODS.Length ? TAB_PERIODS[index] : "weekly";
                GameManager.Instance?.RequestRanking(period);
                FillEmptyData(); // 先清空，等服务器数据返回
            }

            // 更新重置时间说明
            if (resetTimeText != null && index < TAB_RESET_DESCRIPTIONS.Length)
            {
                if (_chineseFont != null && resetTimeText.font != _chineseFont)
                    resetTimeText.font = _chineseFont;
                resetTimeText.text = TAB_RESET_DESCRIPTIONS[index];
                resetTimeText.ForceMeshUpdate();
            }
        }

        /// <summary>
        /// 从外部推送排行数据（服务器或模拟器调用）
        /// </summary>
        public void SetRankingData(RankingData[] data)
        {
            _cachedData = data;
            RefreshUI(data);
        }



        /// <summary>用排行数据刷新UI</summary>
        private void RefreshUI(RankingData[] data)
        {
            if (data == null || data.Length == 0)
            {
                FillEmptyData();
                return;
            }

            // Top 3
            if (top3Names != null)
            {
                for (int i = 0; i < top3Names.Length; i++)
                {
                    if (top3Names[i] != null)
                        top3Names[i].text = i < data.Length ? data[i].name : "";
                }
            }
            // 连胜榜(tab=2)显示连胜数，其他显示贡献值
            bool isStreakTab = (_currentTab == 2);
            if (top3Scores != null)
            {
                for (int i = 0; i < top3Scores.Length; i++)
                {
                    if (top3Scores[i] != null)
                    {
                        if (i < data.Length)
                            top3Scores[i].text = isStreakTab ? $"{data[i].currentStreak}连胜" : $"贡献值 {data[i].score:N0}";
                        else
                            top3Scores[i].text = "";
                    }
                }
            }

            // Top 3 头像：优先加载真实头像，无URL时显示默认色块
            if (top3Avatars != null)
            {
                Color[] avatarColors = {
                    new Color(1f, 0.84f, 0f),    // 金色（第1名）
                    new Color(0.75f, 0.75f, 0.8f), // 银色（第2名）
                    new Color(0.8f, 0.52f, 0.25f)  // 铜色（第3名）
                };
                for (int i = 0; i < top3Avatars.Length; i++)
                {
                    if (top3Avatars[i] == null) continue;
                    bool hasData = i < data.Length && !string.IsNullOrEmpty(data[i].name);
                    top3Avatars[i].gameObject.SetActive(hasData);
                    if (hasData)
                    {
                        // 默认色块
                        top3Avatars[i].color = avatarColors[Mathf.Min(i, avatarColors.Length - 1)];

                        // 尝试加载真实头像
                        string url = data[i].avatarUrl;
                        if (!string.IsNullOrEmpty(url) && AvatarLoader.Instance != null)
                        {
                            var img = top3Avatars[i];
                            AvatarLoader.Instance.Load(url, tex =>
                            {
                                if (img != null && tex != null)
                                {
                                    img.sprite = AvatarLoader.TextureToSprite(tex);
                                    img.color = Color.white; // 显示真实头像时去掉色块
                                }
                            });
                        }
                    }
                }
            }
            if (top3AvatarFrames != null)
            {
                for (int i = 0; i < top3AvatarFrames.Length; i++)
                {
                    if (top3AvatarFrames[i] == null) continue;
                    bool hasData = i < data.Length && !string.IsNullOrEmpty(data[i].name);
                    top3AvatarFrames[i].gameObject.SetActive(hasData);
                }
            }

            // Top3 排名数字显隐
            if (_top3RankNumbers != null)
            {
                for (int i = 0; i < _top3RankNumbers.Length; i++)
                {
                    if (_top3RankNumbers[i] == null) continue;
                    bool hasData = i < data.Length && !string.IsNullOrEmpty(data[i].name);
                    _top3RankNumbers[i].gameObject.SetActive(hasData);
                }
            }

            // 4-100 名（优先ScrollView，兼容固定数组）
            if (rankScrollContent != null)
            {
                FillScrollRankList(rankScrollContent, data, isStreakTab);
            }
            else if (rankNames != null)
            {
                for (int i = 0; i < rankNames.Length; i++)
                {
                    int dataIdx = i + 3; // 从第4名开始
                    if (rankNumbers != null && i < rankNumbers.Length && rankNumbers[i] != null)
                        rankNumbers[i].text = dataIdx < data.Length ? (dataIdx + 1).ToString() : "";
                    if (rankNames[i] != null)
                        rankNames[i].text = dataIdx < data.Length ? data[dataIdx].name : "";
                    if (rankScores != null && i < rankScores.Length && rankScores[i] != null)
                    {
                        if (dataIdx < data.Length)
                            rankScores[i].text = isStreakTab ? $"{data[dataIdx].currentStreak}连胜" : $"贡献值 {data[dataIdx].score:N0}";
                        else
                            rankScores[i].text = "";
                    }
                }
            }
        }

        /// <summary>清空显示</summary>
        private void FillEmptyData()
        {
            if (top3Names != null)
                foreach (var t in top3Names) if (t != null) t.text = "";
            if (top3Scores != null)
                foreach (var t in top3Scores) if (t != null) t.text = "";
            if (top3Avatars != null)
                foreach (var img in top3Avatars) if (img != null) img.gameObject.SetActive(false);
            if (top3AvatarFrames != null)
                foreach (var img in top3AvatarFrames) if (img != null) img.gameObject.SetActive(false);
            if (_top3RankNumbers != null)
                foreach (var t in _top3RankNumbers) if (t != null) t.gameObject.SetActive(false);
            if (rankNames != null)
                foreach (var t in rankNames) if (t != null) t.text = "";
            if (rankScores != null)
                foreach (var t in rankScores) if (t != null) t.text = "";
            if (rankNumbers != null)
                foreach (var t in rankNumbers) if (t != null) t.text = "";
        }

        /// <summary>显示排行榜</summary>
        public void Show()
        {
            gameObject.SetActive(true);

            // 如果已连接服务器，先请求所有Tab的排行数据
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                foreach (var period in TAB_PERIODS)
                    GameManager.Instance?.RequestRanking(period);
            }

            SwitchTab(0);
        }

        /// <summary>隐藏排行榜</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>运行时排版保障：确保 TMP 对齐、字体、描边清晰度</summary>
        private void EnsureListLayout()
        {
            // Top3 头像框样式: 加载avatar_frame sprite + 白色显示（让sprite自然渲染）
            if (top3AvatarFrames != null)
            {
                var frameSprite = Resources.Load<Sprite>("avatar_frame");
                for (int i = 0; i < top3AvatarFrames.Length; i++)
                {
                    if (top3AvatarFrames[i] == null) continue;
                    // 加载sprite图片（原先sprite为空，只显示纯色矩形）
                    if (frameSprite != null)
                        top3AvatarFrames[i].sprite = frameSprite;
                    // 白色让sprite自然显示，不染色
                    top3AvatarFrames[i].color = Color.white;
                    // 尺寸保持场景值，不覆盖（#0=136, #1/#2=116 已在场景中设好）
                }
            }

            // Top3 名字居中、字体放大+描边清晰
            if (top3Names != null)
            {
                foreach (var t in top3Names)
                {
                    if (t == null) continue;
                    t.alignment = TextAlignmentOptions.Center;
                    t.fontSize = 26f;
                    t.fontStyle = FontStyles.Bold;
                    t.color = Color.white;
                    t.outlineWidth = 0.3f;
                    t.outlineColor = new Color32(0, 0, 0, 200);
                    if (_chineseFont != null) t.font = _chineseFont;
                }
            }
            if (top3Scores != null)
            {
                foreach (var t in top3Scores)
                {
                    if (t == null) continue;
                    t.alignment = TextAlignmentOptions.Center;
                    t.fontSize = 22f;
                    t.color = new Color(1f, 0.85f, 0.4f); // 金色
                    t.outlineWidth = 0.25f;
                    t.outlineColor = new Color32(0, 0, 0, 180);
                    if (_chineseFont != null) t.font = _chineseFont;
                }
            }

            // 4-10 名列表：排名数字居中，名字左对齐，贡献值右对齐，全部加描边
            if (rankNumbers != null)
            {
                foreach (var t in rankNumbers)
                {
                    if (t == null) continue;
                    t.alignment = TextAlignmentOptions.Center;
                    t.fontStyle = FontStyles.Bold;
                    t.fontSize = 26f;
                    t.color = Color.white;
                    t.outlineWidth = 0.2f;
                    t.outlineColor = new Color32(0, 0, 0, 160);
                    if (_chineseFont != null) t.font = _chineseFont;
                }
            }
            if (rankNames != null)
            {
                foreach (var t in rankNames)
                {
                    if (t == null) continue;
                    t.alignment = TextAlignmentOptions.Left;
                    t.fontSize = 24f;
                    t.color = Color.white;
                    t.outlineWidth = 0.2f;
                    t.outlineColor = new Color32(0, 0, 0, 160);
                    if (_chineseFont != null) t.font = _chineseFont;
                }
            }
            if (rankScores != null)
            {
                foreach (var t in rankScores)
                {
                    if (t == null) continue;
                    t.alignment = TextAlignmentOptions.Right;
                    t.fontSize = 22f;
                    t.color = new Color(1f, 0.9f, 0.6f); // 浅金
                    t.outlineWidth = 0.2f;
                    t.outlineColor = new Color32(0, 0, 0, 160);
                    if (_chineseFont != null) t.font = _chineseFont;
                }
            }

            // 重置时间说明样式
            if (resetTimeText != null)
            {
                if (_chineseFont != null) resetTimeText.font = _chineseFont;
                resetTimeText.alignment = TextAlignmentOptions.Center;
                resetTimeText.fontSize = 28f;
                resetTimeText.color = new Color(1f, 0.92f, 0.7f); // 亮暖金色
                resetTimeText.fontStyle = FontStyles.Bold;
                resetTimeText.enableWordWrapping = false;
                resetTimeText.overflowMode = TextOverflowModes.Overflow;
                resetTimeText.outlineWidth = 0.35f;
                resetTimeText.outlineColor = new Color32(0, 0, 0, 220);
                // 确保初始文字
                if (string.IsNullOrEmpty(resetTimeText.text))
                    resetTimeText.text = TAB_RESET_DESCRIPTIONS[0];
                resetTimeText.ForceMeshUpdate();
            }
        }

        // ==================== ScrollView 动态排行列表（4-100名） ====================

        // 行背景Sprite缓存
        private Sprite _rankItemBgSprite;
        private Sprite _avatarFrameSprite;

        /// <summary>
        /// 动态填充ScrollView排行列表（从第4名开始）
        /// 布局完全复刻场景中 RankItem_0~6 的样式：
        ///   行880×70, 间距97(=680/7), 背景图ranking_item_bg.png
        ///   排名(-380,0) | 头像(-250,0) | 名字(-67,0) | 分数(138,0)
        /// </summary>
        private void FillScrollRankList(RectTransform content, RankingData[] data, bool isStreakTab)
        {
            if (content == null) return;

            // 清除旧行
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.Destroy(content.GetChild(i).gameObject);

            if (data == null || data.Length <= 3) return;

            // --- 布局参数（匹配场景RankItem） ---
            float rowWidth = 880f;
            float rowHeight = 70f;
            float rowSpacing = 97f;  // 680/7 = ~97, 同时显示7行
            int startIdx = 3;
            int count = Mathf.Min(data.Length - startIdx, 97);

            // 从场景已有RankItem获取行背景Sprite
            if (_rankItemBgSprite == null && rankNumbers != null && rankNumbers.Length > 0 && rankNumbers[0] != null)
            {
                var existingBg = rankNumbers[0].transform.parent?.GetComponent<Image>();
                if (existingBg != null) _rankItemBgSprite = existingBg.sprite;
            }
            // 加载头像框Sprite（从Resources文件夹）
            if (_avatarFrameSprite == null)
                _avatarFrameSprite = Resources.Load<Sprite>("avatar_frame");

            for (int i = 0; i < count; i++)
            {
                int dataIdx = startIdx + i;
                var d = data[dataIdx];

                // === 行容器（复刻RankItem: 880×70, 居中锚点） ===
                var row = new GameObject($"RankRow_{dataIdx + 1}", typeof(RectTransform));
                row.transform.SetParent(content, false);
                var rowRT = row.GetComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0.5f, 1);
                rowRT.anchorMax = new Vector2(0.5f, 1);
                rowRT.pivot = new Vector2(0.5f, 0.5f);
                rowRT.sizeDelta = new Vector2(rowWidth, rowHeight);
                rowRT.anchoredPosition = new Vector2(0, -(i * rowSpacing) - rowHeight * 0.5f);

                // 行背景图
                if (_rankItemBgSprite != null)
                {
                    var bgImg = row.AddComponent<Image>();
                    bgImg.sprite = _rankItemBgSprite;
                    bgImg.type = Image.Type.Sliced;
                    bgImg.raycastTarget = false;
                }

                // === 1) 排名数字 (复刻RankNum: pos(-380,0), size(60×50), scale1.48, fontSize28) ===
                var numGo = new GameObject("RankNum", typeof(RectTransform));
                numGo.transform.SetParent(row.transform, false);
                var numTMP = numGo.AddComponent<TextMeshProUGUI>();
                numTMP.text = (dataIdx + 1).ToString();
                numTMP.fontSize = 28f;
                numTMP.color = Color.white;
                numTMP.fontStyle = FontStyles.Bold;
                numTMP.alignment = TextAlignmentOptions.Center;
                numTMP.overflowMode = TextOverflowModes.Ellipsis;
                if (_chineseFont != null) numTMP.font = _chineseFont;
                numTMP.outlineWidth = 0.2f;
                numTMP.outlineColor = new Color32(0, 0, 0, 160);
                var numRT = numGo.GetComponent<RectTransform>();
                numRT.anchorMin = new Vector2(0.5f, 0.5f);
                numRT.anchorMax = new Vector2(0.5f, 0.5f);
                numRT.pivot = new Vector2(0.5f, 0.5f);
                numRT.sizeDelta = new Vector2(60, 50);
                numRT.anchoredPosition = new Vector2(-380, 0);
                numRT.localScale = new Vector3(1.48f, 1.48f, 1.48f);

                // === 2) 头像框 + 头像 (pos(-280,0), 框56×56, 头像44×44) ===
                var frameGo = new GameObject("AvatarFrame", typeof(RectTransform));
                frameGo.transform.SetParent(row.transform, false);
                var frameRT = frameGo.GetComponent<RectTransform>();
                frameRT.anchorMin = new Vector2(0.5f, 0.5f);
                frameRT.anchorMax = new Vector2(0.5f, 0.5f);
                frameRT.pivot = new Vector2(0.5f, 0.5f);
                frameRT.sizeDelta = new Vector2(56, 56);
                frameRT.anchoredPosition = new Vector2(-280, 0);
                if (_avatarFrameSprite != null)
                {
                    var frameImg = frameGo.AddComponent<Image>();
                    frameImg.sprite = _avatarFrameSprite;
                    frameImg.raycastTarget = false;
                }

                // 头像（头像框的子对象，居中，比框小12px）
                var avatarGo = new GameObject("Avatar", typeof(RectTransform));
                avatarGo.transform.SetParent(frameGo.transform, false);
                var avatarImg = avatarGo.AddComponent<RawImage>();
                avatarImg.color = new Color(0.5f, 0.5f, 0.6f); // 默认灰色色块
                var avatarRT = avatarGo.GetComponent<RectTransform>();
                avatarRT.anchorMin = new Vector2(0.5f, 0.5f);
                avatarRT.anchorMax = new Vector2(0.5f, 0.5f);
                avatarRT.pivot = new Vector2(0.5f, 0.5f);
                avatarRT.sizeDelta = new Vector2(44, 44);
                avatarRT.anchoredPosition = Vector2.zero;

                // 加载头像
                if (!string.IsNullOrEmpty(d.avatarUrl) && AvatarLoader.Instance != null)
                {
                    var img = avatarImg;
                    AvatarLoader.Instance.Load(d.avatarUrl, tex =>
                    {
                        if (img != null && tex != null)
                        {
                            img.texture = tex;
                            img.color = Color.white;
                        }
                    });
                }

                // === 3) 名字 (右移避开头像: pos(-50,0), size(240×40), scale1.48, fontSize26) ===
                var nameGo = new GameObject("Name", typeof(RectTransform));
                nameGo.transform.SetParent(row.transform, false);
                var nameTMP = nameGo.AddComponent<TextMeshProUGUI>();
                nameTMP.text = d.name;
                nameTMP.fontSize = 26f;
                nameTMP.color = Color.white;
                nameTMP.alignment = TextAlignmentOptions.Left;
                nameTMP.overflowMode = TextOverflowModes.Ellipsis;
                nameTMP.enableWordWrapping = false;
                if (_chineseFont != null) nameTMP.font = _chineseFont;
                nameTMP.outlineWidth = 0.2f;
                nameTMP.outlineColor = new Color32(0, 0, 0, 160);
                var nameRT = nameGo.GetComponent<RectTransform>();
                nameRT.anchorMin = new Vector2(0.5f, 0.5f);
                nameRT.anchorMax = new Vector2(0.5f, 0.5f);
                nameRT.pivot = new Vector2(0.5f, 0.5f);
                nameRT.sizeDelta = new Vector2(240, 40);
                nameRT.anchoredPosition = new Vector2(-50, 0);
                nameRT.localScale = new Vector3(1.48f, 1.48f, 1.48f);

                // === 4) 数值 (右移: pos(200,0), size(240×40), scale1.55, fontSize22, 浅金) ===
                var scoreGo = new GameObject("Score", typeof(RectTransform));
                scoreGo.transform.SetParent(row.transform, false);
                var scoreTMP = scoreGo.AddComponent<TextMeshProUGUI>();
                scoreTMP.text = isStreakTab ? $"{d.currentStreak}连胜" : $"贡献值 {d.score:N0}";
                scoreTMP.fontSize = 22f;
                scoreTMP.color = new Color(1f, 0.9f, 0.6f);
                scoreTMP.alignment = TextAlignmentOptions.Right;
                scoreTMP.overflowMode = TextOverflowModes.Ellipsis;
                scoreTMP.enableWordWrapping = false;
                if (_chineseFont != null) scoreTMP.font = _chineseFont;
                scoreTMP.outlineWidth = 0.2f;
                scoreTMP.outlineColor = new Color32(0, 0, 0, 160);
                var scoreRT = scoreGo.GetComponent<RectTransform>();
                scoreRT.anchorMin = new Vector2(0.5f, 0.5f);
                scoreRT.anchorMax = new Vector2(0.5f, 0.5f);
                scoreRT.pivot = new Vector2(0.5f, 0.5f);
                scoreRT.sizeDelta = new Vector2(240, 40);
                scoreRT.anchoredPosition = new Vector2(200, 0);
                scoreRT.localScale = new Vector3(1.55f, 1.55f, 1.55f);
            }

            // 设置Content总高度
            content.sizeDelta = new Vector2(content.sizeDelta.x, count * rowSpacing);
        }
    }
}
