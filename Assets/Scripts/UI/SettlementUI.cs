using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Utils;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 结算面板 - MVP展示 + 双阵营排行(最多100人ScrollView) + 积分瓜分 + 返回按钮
    /// 美术资源: 美术界面资源/结算界面/
    /// 排行支持两种模式：
    ///   1. ScrollView动态行（优先）：leftScrollContent/rightScrollContent 不为null时自动生成行
    ///   2. 固定TMP数组（兼容旧场景）：leftRankNames/leftRankValues Inspector绑定
    /// 内置排版保障：OnEnable 时自动校正所有 TMP 的位置和对齐
    ///
    /// 重要：使用 OnEnable/OnDisable 订阅事件（而非 Start）
    /// 因为面板初始是 SetActive(false)，Start() 不会被调用
    /// UIManager 在 GameState.Settlement 时才激活面板
    /// </summary>
    public class SettlementUI : MonoBehaviour
    {
        [Header("Winner")]
        public TextMeshProUGUI winnerText;

        [Header("MVP")]
        public TextMeshProUGUI mvpNameText;
        public TextMeshProUGUI mvpLabelText;
        public TextMeshProUGUI mvpContributionText;
        public Image mvpAvatarImage;

        [Header("Left Rankings (ScrollView preferred, fallback to fixed array)")]
        public TextMeshProUGUI leftRankTitle;
        public RectTransform leftScrollContent;    // ScrollView/Viewport/Content (动态100行)
        public TextMeshProUGUI[] leftRankNames;    // 兼容旧场景：固定TMP数组
        public TextMeshProUGUI[] leftRankValues;

        [Header("Right Rankings")]
        public TextMeshProUGUI rightRankTitle;
        public RectTransform rightScrollContent;   // ScrollView/Viewport/Content (动态100行)
        public TextMeshProUGUI[] rightRankNames;
        public TextMeshProUGUI[] rightRankValues;

        [Header("Score Distribution (6 entries - dual TMP)")]
        public TextMeshProUGUI scorePoolLabel;
        public TextMeshProUGUI[] scoreDistNames;   // 左对齐: "第1名: 玩家名"
        public TextMeshProUGUI[] scoreDistValues;  // 右对齐: "贡献分"

        [Header("Controls")]
        public Button restartButton;

        private static readonly Color COL_LEFT = new Color(1f, 0.55f, 0f);
        private static readonly Color COL_RIGHT = new Color(0.68f, 1f, 0.18f);
        private static readonly Color COL_GOLD = new Color(1f, 0.84f, 0f);

        private bool _layoutApplied = false;

        private void EnsureCanvasSortOrder(int order)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            // Canvas需要GraphicRaycaster
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        // 动态创建的排行榜头像（每行一个）
        private Image[] _leftRankAvatars;
        private Image[] _rightRankAvatars;
        private Image[] _scoreDistAvatars;
        private static Material _circleMaskMat;

        private void OnEnable()
        {
            // 结算面板层级提升到最高（覆盖通知弹窗）
            EnsureCanvasSortOrder(100);

            // 运行时排版保障：确保所有 TMP 位置和对齐正确
            if (!_layoutApplied)
            {
                EnsureLayout();
                _layoutApplied = true;
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnGameEnded += HandleGameEnded;

                // 面板刚被激活时，检查是否已有结算数据（解决时序问题）
                if (gm.LastEndedData != null && gm.CurrentState == GameManager.GameState.Settlement)
                {
                    HandleGameEnded(gm.LastEndedData);
                }
            }

            if (restartButton)
                restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnDisable()
        {
            var gm = GameManager.Instance;
            if (gm != null)
                gm.OnGameEnded -= HandleGameEnded;

            if (restartButton)
                restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        private void HandleGameEnded(GameEndedData data)
        {
            #if UNITY_EDITOR
            Debug.Log($"[SettlementUI] Data received: winner={data.winner}, " +
                $"mvp={(data.mvp != null ? data.mvp.playerName : "NULL")}, " +
                $"leftRanks={(data.leftRankings?.Length ?? 0)}, " +
                $"rightRanks={(data.rightRankings?.Length ?? 0)}, " +
                $"scoreDist={(data.scoreDistribution?.Length ?? 0)}");
            #endif

            gameObject.SetActive(true);

            // ===== 胜利标题 =====
            if (winnerText)
            {
                switch (data.winner)
                {
                    case "left":
                        winnerText.text = "香橙温泉 胜利!";
                        winnerText.color = COL_LEFT;
                        break;
                    case "right":
                        winnerText.text = "柚子温泉 胜利!";
                        winnerText.color = COL_RIGHT;
                        break;
                    default:
                        winnerText.text = "平局!";
                        winnerText.color = Color.white;
                        break;
                }
            }

            // ===== MVP展示 =====
            if (data.mvp != null)
            {
                if (mvpNameText) mvpNameText.text = data.mvp.playerName;
                if (mvpLabelText) mvpLabelText.text = "MVP";
                if (mvpContributionText)
                    mvpContributionText.text = $"总贡献 {data.mvp.totalContribution:N0}";

                LoadMVPAvatar(data.mvp.avatarUrl);
            }
            else
            {
                if (mvpNameText) mvpNameText.text = "暂无";
                if (mvpLabelText) mvpLabelText.text = "";
                if (mvpContributionText) mvpContributionText.text = "";
            }

            // ===== 左阵营排行 =====
            if (leftRankTitle)
            {
                leftRankTitle.richText = true;
                leftRankTitle.enableWordWrapping = true;
                leftRankTitle.text = "香橙温泉 贡献榜\n<size=18><color=#FFD700AA>玩家  |  贡献  |  连胜</color></size>";
            }
            if (leftScrollContent != null)
                FillScrollRankColumn(leftScrollContent, data.leftRankings);
            else
                FillRankColumn(leftRankNames, leftRankValues, _leftRankAvatars, data.leftRankings);

            // ===== 右阵营排行 =====
            if (rightRankTitle)
            {
                rightRankTitle.richText = true;
                rightRankTitle.enableWordWrapping = true;
                rightRankTitle.text = "柚子温泉 贡献榜\n<size=18><color=#FFD700AA>玩家  |  贡献  |  连胜</color></size>";
            }
            if (rightScrollContent != null)
                FillScrollRankColumn(rightScrollContent, data.rightRankings);
            else
                FillRankColumn(rightRankNames, rightRankValues, _rightRankAvatars, data.rightRankings);

            // ===== 积分瓜分 =====
            if (scorePoolLabel)
                scorePoolLabel.text = "积分瓜分";

            FillScoreDistribution(data.scoreDistribution);
        }

        private void FillRankColumn(TextMeshProUGUI[] names, TextMeshProUGUI[] values,
            Image[] avatars, SettlementRankEntry[] rankings)
        {
            if (names == null) return;
            int count = names.Length;
            for (int i = 0; i < count; i++)
            {
                if (rankings != null && i < rankings.Length)
                {
                    var r = rankings[i];
                    Color rowColor = i < 3 ? COL_GOLD : Color.white;

                    if (names[i] != null)
                    {
                        names[i].text = i < 3 ? r.playerName : $"{r.rank}. {r.playerName}";
                        names[i].color = rowColor;
                    }
                    if (values != null && i < values.Length && values[i] != null)
                    {
                        // 所有人都显示：贡献值 + 连胜变化（简洁格式: 8+9 / 8-9）
                        string streakStr = "";
                        if (r.streakBet > 0 || r.streakGain != 0)
                        {
                            if (r.streakGain > 0)
                                streakStr = $"  <color=#FFD700>{r.streakBet}+{r.streakGain}</color>";
                            else if (r.streakGain < 0)
                                streakStr = $"  <color=#FF6666>{r.streakBet}{r.streakGain}</color>";
                        }
                        values[i].text = $"{r.contribution:N0}贡献{streakStr}";
                        values[i].color = rowColor;
                    }

                    // 加载排行榜头像
                    if (avatars != null && i < avatars.Length && avatars[i] != null)
                    {
                        LoadRankAvatar(avatars[i], r.avatarUrl);
                    }
                }
                else
                {
                    if (names[i] != null) names[i].text = "";
                    if (values != null && i < values.Length && values[i] != null)
                        values[i].text = "";
                    if (avatars != null && i < avatars.Length && avatars[i] != null)
                        avatars[i].gameObject.SetActive(false);
                }
            }
        }

        private void FillScoreDistribution(ScoreDistribution[] dist)
        {
            if (scoreDistNames == null) return;
            int count = scoreDistNames.Length;
            for (int i = 0; i < count; i++)
            {
                if (dist != null && i < dist.Length)
                {
                    Color c = i == 0 ? COL_GOLD : new Color(1f, 0.9f, 0.6f);
                    if (scoreDistNames[i] != null)
                    {
                        // 显示 "第X名: 玩家名" + 贡献分（不显示金币）
                        scoreDistNames[i].text = $"第{dist[i].rank}名: {dist[i].playerName}";
                        scoreDistNames[i].color = c;
                    }
                    if (scoreDistValues != null && i < scoreDistValues.Length && scoreDistValues[i] != null)
                    {
                        scoreDistValues[i].text = $"{dist[i].contribution:N0} 贡献分";
                        scoreDistValues[i].color = c;
                    }

                    // 加载积分瓜分头像
                    if (_scoreDistAvatars != null && i < _scoreDistAvatars.Length && _scoreDistAvatars[i] != null)
                    {
                        LoadRankAvatar(_scoreDistAvatars[i], dist[i].avatarUrl);
                    }
                }
                else
                {
                    if (scoreDistNames[i] != null) scoreDistNames[i].text = "";
                    if (scoreDistValues != null && i < scoreDistValues.Length && scoreDistValues[i] != null)
                        scoreDistValues[i].text = "";
                    if (_scoreDistAvatars != null && i < _scoreDistAvatars.Length && _scoreDistAvatars[i] != null)
                        _scoreDistAvatars[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnRestartClicked()
        {
            GameManager.Instance?.RequestResetGame();
            GameManager.Instance?.ResetGame();
            gameObject.SetActive(false);
        }

        // ==================== 运行时文本属性保障 ====================
        // 重要：只设置文本属性（对齐/字号/描边/投影），不覆盖位置/大小！
        // 位置/大小由用户在场景编辑器中手动调整，代码严禁覆盖

        private void EnsureLayout()
        {
            // 标题文字
            EnsureTitleTextProps();
            // MVP区域文字
            EnsureMVPTextProps();
            // 排行榜文字+头像
            _leftRankAvatars = CreateRankAvatars(leftRankNames, 30f);
            _rightRankAvatars = CreateRankAvatars(rightRankNames, 30f);
            EnsureRankColumnTextProps(leftRankNames, leftRankValues);
            EnsureRankColumnTextProps(rightRankNames, rightRankValues);
            // 排行榜标题
            EnsureRankTitleProps(leftRankTitle);
            EnsureRankTitleProps(rightRankTitle);
            // 积分瓜分文字+头像
            _scoreDistAvatars = CreateRankAvatars(scoreDistNames, 26f);
            EnsureScoreDistTextProps(scoreDistNames, scoreDistValues);
            // 积分瓜分标题
            EnsureScoreDistLabelProps();

            // MVP头像圆形裁剪
            EnsureMVPAvatarCircle();
        }

        /// <summary>统一文字效果：加粗+描边+投影</summary>
        private void ApplyUnifiedTextEffect(TextMeshProUGUI tmp, float fontSize,
            Color textColor, float outlineWidth = 0.25f)
        {
            if (tmp == null) return;
            tmp.fontStyle = FontStyles.Bold;
            tmp.fontSize = fontSize;
            tmp.outlineWidth = outlineWidth;
            tmp.outlineColor = new Color32(20, 10, 0, 220);
            // TMP投影效果 — 通过underlay实现（比Shadow组件更清晰）
            tmp.fontMaterial.EnableKeyword("UNDERLAY_ON");
            tmp.fontMaterial.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.6f));
            tmp.fontMaterial.SetFloat("_UnderlayOffsetX", 0.5f);
            tmp.fontMaterial.SetFloat("_UnderlayOffsetY", -0.5f);
            tmp.fontMaterial.SetFloat("_UnderlayDilate", 0.1f);
            tmp.fontMaterial.SetFloat("_UnderlaySoftness", 0.2f);
        }

        /// <summary>标题文字（胜利标题）</summary>
        private void EnsureTitleTextProps()
        {
            if (winnerText == null) return;
            ApplyUnifiedTextEffect(winnerText, 58, COL_GOLD, 0.35f);
            winnerText.enableWordWrapping = false;
            winnerText.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>MVP区域文字属性</summary>
        private void EnsureMVPTextProps()
        {
            if (mvpNameText != null)
            {
                ApplyUnifiedTextEffect(mvpNameText, 30, Color.white, 0.28f);
                mvpNameText.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (mvpLabelText != null)
            {
                ApplyUnifiedTextEffect(mvpLabelText, 44, COL_GOLD, 0.35f);
            }
            if (mvpContributionText != null)
            {
                ApplyUnifiedTextEffect(mvpContributionText, 28, new Color(1f, 0.95f, 0.8f), 0.28f);
            }
        }

        /// <summary>排行榜标题</summary>
        private void EnsureRankTitleProps(TextMeshProUGUI title)
        {
            if (title == null) return;
            ApplyUnifiedTextEffect(title, 24, COL_GOLD, 0.28f);
            title.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>积分瓜分标题</summary>
        private void EnsureScoreDistLabelProps()
        {
            if (scorePoolLabel == null) return;
            ApplyUnifiedTextEffect(scorePoolLabel, 24, COL_GOLD, 0.28f);
            scorePoolLabel.alignment = TextAlignmentOptions.Center;
        }

        private void EnsureRankColumnTextProps(TextMeshProUGUI[] names, TextMeshProUGUI[] values)
        {
            if (names == null) return;

            for (int i = 0; i < names.Length; i++)
            {
                // 玩家名：左对齐 + 统一文字效果（不动位置！）
                if (names[i] != null)
                {
                    float fs = i < 3 ? 24 : 22;
                    ApplyUnifiedTextEffect(names[i], fs, i < 3 ? COL_GOLD : Color.white, 0.25f);
                    names[i].alignment = TextAlignmentOptions.MidlineLeft;
                    names[i].overflowMode = TextOverflowModes.Ellipsis;
                    names[i].enableWordWrapping = false;
                }

                // 贡献值+连胜：右对齐 + 单行横排（不换行）
                if (values != null && i < values.Length && values[i] != null)
                {
                    float fs = i < 3 ? 22 : 20;
                    ApplyUnifiedTextEffect(values[i], fs, i < 3 ? COL_GOLD : Color.white, 0.22f);
                    values[i].alignment = TextAlignmentOptions.MidlineRight;
                    values[i].overflowMode = TextOverflowModes.Overflow;
                    values[i].richText = true;
                    values[i].enableWordWrapping = false; // 单行横排，不换行
                }
            }
        }

        private void EnsureScoreDistTextProps(TextMeshProUGUI[] names, TextMeshProUGUI[] values)
        {
            if (names == null) return;

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != null)
                {
                    float fs = i == 0 ? 22 : 20;
                    ApplyUnifiedTextEffect(names[i], fs,
                        i == 0 ? COL_GOLD : new Color(1f, 0.9f, 0.6f), 0.22f);
                    names[i].alignment = TextAlignmentOptions.Left;
                    names[i].overflowMode = TextOverflowModes.Ellipsis;
                }

                if (values != null && i < values.Length && values[i] != null)
                {
                    float fs = i == 0 ? 20 : 18;
                    ApplyUnifiedTextEffect(values[i], fs,
                        i == 0 ? COL_GOLD : new Color(1f, 0.9f, 0.6f), 0.22f);
                    values[i].alignment = TextAlignmentOptions.Right;
                    values[i].overflowMode = TextOverflowModes.Ellipsis;
                }
            }
        }

        // ==================== 排行榜头像（动态创建） ====================

        /// <summary>
        /// 在每个排行榜名字TMP的左侧创建一个小圆形头像
        /// </summary>
        private Image[] CreateRankAvatars(TextMeshProUGUI[] names, float avatarSize)
        {
            if (names == null) return null;
            var avatars = new Image[names.Length];

            // 加载圆形遮罩材质
            if (_circleMaskMat == null)
            {
                _circleMaskMat = Resources.Load<Material>("Materials/Mat_CircleMask");
            }

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == null) continue;

                var nameRect = names[i].GetComponent<RectTransform>();
                if (nameRect == null) continue;

                // 在名字TMP的父对象下创建头像
                var avatarGo = new GameObject($"RankAvatar_{i}");
                avatarGo.transform.SetParent(nameRect.parent, false);

                var avatarRect = avatarGo.AddComponent<RectTransform>();
                // 头像锚点与名字TMP相同
                avatarRect.anchorMin = nameRect.anchorMin;
                avatarRect.anchorMax = nameRect.anchorMax;
                avatarRect.pivot = new Vector2(1f, 0.5f); // 右对齐，紧贴名字左边
                // 位置：名字左边偏移，Y对齐
                float nameLeftX = nameRect.anchoredPosition.x - nameRect.sizeDelta.x * nameRect.pivot.x;
                avatarRect.anchoredPosition = new Vector2(nameLeftX - 4f, nameRect.anchoredPosition.y);
                avatarRect.sizeDelta = new Vector2(avatarSize, avatarSize);

                var img = avatarGo.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;

                // 应用圆形遮罩
                if (_circleMaskMat != null)
                {
                    img.material = new Material(_circleMaskMat);
                    img.material.SetFloat("_BorderWidth", 0.05f);
                    img.material.SetFloat("_Softness", 0.02f);
                    img.material.SetColor("_BorderColor", COL_GOLD);
                }

                avatarGo.SetActive(false); // 默认隐藏，有数据时才显示
                avatars[i] = img;
            }

            return avatars;
        }

        /// <summary>加载排行榜小头像</summary>
        private void LoadRankAvatar(Image avatarImg, string avatarUrl)
        {
            if (avatarImg == null) return;

            if (string.IsNullOrEmpty(avatarUrl))
            {
                avatarImg.gameObject.SetActive(false);
                return;
            }

            avatarImg.gameObject.SetActive(true);
            avatarImg.color = new Color(0.5f, 0.5f, 0.5f); // 加载中显示灰色

            var loader = AvatarLoader.Instance;
            if (loader != null)
            {
                loader.Load(avatarUrl, tex =>
                {
                    if (avatarImg != null && tex != null)
                    {
                        avatarImg.sprite = AvatarLoader.TextureToSprite(tex);
                        avatarImg.color = Color.white;
                    }
                });
            }
        }

        // ==================== MVP头像 ====================

        /// <summary>MVP头像圆形裁剪</summary>
        private void EnsureMVPAvatarCircle()
        {
            if (mvpAvatarImage == null) return;

            var circleMat = Resources.Load<Material>("Materials/Mat_CircleMask");
            if (circleMat != null)
            {
                bool needApply = mvpAvatarImage.material == null
                    || mvpAvatarImage.material.shader == null
                    || mvpAvatarImage.material.shader.name != "UI/CircleMask";
                if (needApply)
                {
                    mvpAvatarImage.material = new Material(circleMat);
                    // 边框极薄（外框sprite已提供装饰性边框，这里只做圆形裁剪+柔和抗锯齿）
                    mvpAvatarImage.material.SetColor("_BorderColor", COL_GOLD);
                    mvpAvatarImage.material.SetFloat("_BorderWidth", 0.02f);
                    mvpAvatarImage.material.SetFloat("_Softness", 0.015f);
                }
            }
        }

        /// <summary>加载MVP头像</summary>
        private void LoadMVPAvatar(string avatarUrl)
        {
            if (mvpAvatarImage == null) return;

            if (string.IsNullOrEmpty(avatarUrl))
            {
                mvpAvatarImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                mvpAvatarImage.sprite = null;
                return;
            }

            var loader = AvatarLoader.Instance;
            if (loader != null)
            {
                loader.Load(avatarUrl, tex =>
                {
                    if (mvpAvatarImage != null && tex != null)
                    {
                        mvpAvatarImage.sprite = AvatarLoader.TextureToSprite(tex);
                        mvpAvatarImage.color = Color.white;
                    }
                });
            }
        }

        // ==================== ScrollView 动态排行（最多100人） ====================

        private TMP_FontAsset _scrollFont;

        /// <summary>
        /// 动态填充ScrollView排行列表（替代固定TMP数组）
        /// 表头行 + 数据行，4列布局：排名 | 玩家 | 贡献 | 连胜
        /// </summary>
        private void FillScrollRankColumn(RectTransform content, SettlementRankEntry[] rankings)
        {
            if (content == null) return;

            // 清除旧行
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            if (rankings == null || rankings.Length == 0) return;

            if (_scrollFont == null)
                _scrollFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");

            float headerHeight = 36f;
            float rowHeight = 40f;
            int count = Mathf.Min(rankings.Length, 100);

            // === 表头行 ===
            var headerRow = new GameObject("Header", typeof(RectTransform));
            headerRow.transform.SetParent(content, false);
            var headerRT = headerRow.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = new Vector2(0, 0);
            headerRT.sizeDelta = new Vector2(0, headerHeight);

            // 表头4列：排名(0-12%) | 玩家(12-50%) | 贡献(50-78%) | 连胜(78-100%)
            CreateHeaderCell(headerRow.transform, "排名", 0f, 0.12f);
            CreateHeaderCell(headerRow.transform, "玩家", 0.12f, 0.50f);
            CreateHeaderCell(headerRow.transform, "贡献", 0.50f, 0.78f);
            CreateHeaderCell(headerRow.transform, "连胜", 0.78f, 1f);

            // === 数据行 ===
            for (int i = 0; i < count; i++)
            {
                var r = rankings[i];
                bool isTop3 = i < 3;
                Color rowColor = isTop3 ? COL_GOLD : Color.white;
                float fontSize = isTop3 ? 22 : 20;

                // 行容器
                var row = new GameObject($"Row_{i}", typeof(RectTransform));
                row.transform.SetParent(content, false);
                var rowRT = row.GetComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0, 1);
                rowRT.anchorMax = new Vector2(1, 1);
                rowRT.pivot = new Vector2(0.5f, 1);
                rowRT.anchoredPosition = new Vector2(0, -headerHeight - i * rowHeight);
                rowRT.sizeDelta = new Vector2(0, rowHeight);

                // 1) 排名 (0-12%, 居中)
                CreateDataCell(row.transform, r.rank.ToString(), fontSize, rowColor,
                    TextAlignmentOptions.Center, 0f, 0.12f);

                // 2) 玩家名 (12-50%, 左对齐)
                CreateDataCell(row.transform, r.playerName, fontSize, rowColor,
                    TextAlignmentOptions.MidlineLeft, 0.12f, 0.50f, 6f);

                // 3) 贡献 (50-78%, 右对齐)
                CreateDataCell(row.transform, $"{r.contribution:N0}", fontSize, rowColor,
                    TextAlignmentOptions.MidlineRight, 0.50f, 0.78f, 0f, -4f);

                // 4) 连胜变化 (78-100%, 右对齐, RichText)
                string streakStr = "";
                if (r.streakBet > 0 || r.streakGain != 0)
                {
                    if (r.streakGain > 0)
                        streakStr = $"<color=#FFD700>{r.streakBet}+{r.streakGain}</color>";
                    else if (r.streakGain < 0)
                        streakStr = $"<color=#FF6666>{r.streakBet}{r.streakGain}</color>";
                    else
                        streakStr = r.streakBet.ToString();
                }
                else
                {
                    streakStr = "-";
                }
                CreateDataCell(row.transform, streakStr, fontSize, rowColor,
                    TextAlignmentOptions.MidlineRight, 0.78f, 1f, 0f, -4f, true);
            }

            // 设置Content总高度
            content.sizeDelta = new Vector2(content.sizeDelta.x, headerHeight + count * rowHeight);
        }

        /// <summary>创建表头单元格</summary>
        private void CreateHeaderCell(Transform parent, string text, float anchorMinX, float anchorMaxX)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = new Color(1f, 0.85f, 0.4f); // 淡金标题色
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            if (_scrollFont != null) tmp.font = _scrollFont;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(0, 0, 0, 160);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorMinX, 0);
            rt.anchorMax = new Vector2(anchorMaxX, 1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>创建数据行单元格</summary>
        private void CreateDataCell(Transform parent, string text, float fontSize, Color color,
            TextAlignmentOptions alignment, float anchorMinX, float anchorMaxX,
            float paddingLeft = 0f, float paddingRight = 0f, bool richText = false)
        {
            var go = new GameObject("Cell", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.richText = richText;
            if (_scrollFont != null) tmp.font = _scrollFont;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorMinX, 0);
            rt.anchorMax = new Vector2(anchorMaxX, 1);
            rt.offsetMin = new Vector2(paddingLeft, 0);
            rt.offsetMax = new Vector2(paddingRight, 0);
        }
    }
}
