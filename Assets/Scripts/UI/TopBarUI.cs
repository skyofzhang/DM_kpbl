using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CapybaraDuel.Core;
using CapybaraDuel.Systems;

namespace CapybaraDuel.UI
{
    /// <summary>
    /// 顶部栏 - 双向挤压拉力条 + 推力数字 + 计时器 + 积分池 + 提示文字 + 连胜
    /// Phase2重构: 拉力条改为anchor驱动的双向挤压血条
    ///
    /// 进度条结构 (ProgressBarContainer):
    ///   BarLeft  - 橙色，anchorMax.x 跟随分割点
    ///   BarRight - 绿色，anchorMin.x 跟随分割点
    ///   BarDivider - 白色细线，anchor.x 跟随分割点
    ///   LeftArrows  - ">>>" 文字
    ///   RightArrows - "<<<" 文字
    ///
    /// 分割点 = 0.5 + (orangePos / 100) * 0.5
    ///   orangePos=0   → 分割点=0.5（正中间）
    ///   orangePos=+100 → 分割点=1.0（橙方占满）
    ///   orangePos=-100 → 分割点=0.0（柚方占满）
    /// </summary>
    public class TopBarUI : MonoBehaviour
    {
        [Header("Force & Timer")]
        public TextMeshProUGUI leftForceText;
        public TextMeshProUGUI rightForceText;
        public TextMeshProUGUI timerText;
        public TextMeshProUGUI scorePoolText;

        [Header("Dual Progress Bar (Phase2)")]
        [Tooltip("橙色左侧条（RectTransform，通过anchorMax.x控制宽度）")]
        public RectTransform barLeft;
        [Tooltip("绿色右侧条（RectTransform，通过anchorMin.x控制宽度）")]
        public RectTransform barRight;
        [Tooltip("白色分割线（RectTransform，anchor.x跟随分割点）")]
        public RectTransform barDivider;

        [Header("Progress Bar (Legacy - 兼容)")]
        [Tooltip("旧版fillAmount条（Phase2后不再使用，保留防null报错）")]
        public Image progressBarLeft;
        public Image progressBarRight;

        [Header("Distance Markers")]
        public TextMeshProUGUI leftEndMarker;    // 左端距离
        public TextMeshProUGUI rightEndMarker;   // 右端距离
        public TextMeshProUGUI centerMarker;     // 中心位置
        public TextMeshProUGUI posIndicatorText; // 当前距离指示

        [Header("Hint & WinStreak")]
        [Tooltip("顶部提示文字（如'橙方差9999推力反击'）")]
        public TextMeshProUGUI hintText;
        [Tooltip("左阵营连胜数")]
        public TextMeshProUGUI winStreakLeftText;
        [Tooltip("右阵营连胜数")]
        public TextMeshProUGUI winStreakRightText;

        [Header("Buttons")]
        [Tooltip("公告按钮（左上角，原结束按钮）")]
        public Button btnEnd;
        [Tooltip("设置按钮（右上角）")]
        public Button btnSettings;

        [Header("OrangeIcon Follow")]
        [Tooltip("PosIndicator的RectTransform，跟随分割点水平移动")]
        [SerializeField] private RectTransform posIndicatorRect;
        [Tooltip("ProgressBarContainer的RectTransform")]
        [SerializeField] private RectTransform progressBarContainerRect;

        [Header("Bar Animation")]
        [SerializeField] private float barSmoothSpeed = 8f; // 分割线平滑速度

        private ForceSystem _forceSystem;
        private RankingSystem _rankingSystem;
        private bool _subscribed = false;
        private bool _needsInitialSync = true;

        private bool _layoutApplied = false;
        private float _prevOrangePos = 0f;

        // 双向进度条：当前分割点（平滑用）
        private float _currentSplit = 0.5f;
        private float _targetSplit = 0.5f;

        // 连胜数据
        private int _leftWinStreak = 0;
        private int _rightWinStreak = 0;

        // 箭头动画
        private float _arrowAnimTime = 0f;

        private float _lastHintDiff = -1f;

        // 动态进度条增强
        private Image _barLeftImg;
        private Image _barRightImg;
        private Image _barDividerImg;
        private GameObject _leftGlowEdge;
        private GameObject _rightGlowEdge;
        private float _shimmerPhase = 0f;

        // 阵营色
        private static readonly Color BAR_LEFT_BASE  = new Color(1f, 0.55f, 0.1f);    // 橙色
        private static readonly Color BAR_RIGHT_BASE = new Color(0.35f, 0.85f, 0.2f); // 绿色
        private static readonly Color BAR_LEFT_GLOW  = new Color(1f, 0.8f, 0.3f, 0.6f);
        private static readonly Color BAR_RIGHT_GLOW = new Color(0.5f, 1f, 0.4f, 0.6f);

        private void OnEnable()
        {
            TrySubscribe();
            _needsInitialSync = true;

            if (!_layoutApplied)
            {
                EnsureLayout();
                _layoutApplied = true;
            }

            // 初始化进度条到中间
            _currentSplit = 0.5f;
            _targetSplit = 0.5f;
            ApplyBarSplit(0.5f);

            // 进度条视觉增强
            EnhanceProgressBar();

            // 旧版兼容归零
            if (progressBarLeft) progressBarLeft.fillAmount = 0f;
            if (progressBarRight) progressBarRight.fillAmount = 0f;

            if (hintText) hintText.text = "";
            UpdateWinStreakDisplay();
        }

        private void EnsureLayout()
        {
            // TopBar层级=50，重要数据始终可见（高于通知20和礼物30）
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // 【重要】不再覆盖RectTransform位置！位置由场景设定，代码不动
            // 之前 rt.anchoredPosition = new Vector2(0, -100f) 会覆盖用户手动调整

            // 按钮事件绑定（不修改位置/样式，只绑定功能）
            if (btnEnd != null)
            {
                btnEnd.onClick.RemoveAllListeners();
                btnEnd.onClick.AddListener(OnAnnouncementButtonClicked);
            }
            if (btnSettings != null)
            {
                btnSettings.onClick.RemoveAllListeners();
                btnSettings.onClick.AddListener(OnSettingsButtonClicked);
            }
        }

        // 【移除】SetupForceTextStyle — 文字样式由场景设定，代码不覆盖

        private void TrySubscribe()
        {
            if (_subscribed) return;

            _forceSystem = FindObjectOfType<ForceSystem>();
            if (_forceSystem != null)
                _forceSystem.OnForceUpdated += HandleForceUpdate;

            _rankingSystem = FindObjectOfType<RankingSystem>();
            if (_rankingSystem != null)
                _rankingSystem.OnRankingsUpdated += HandleRankingsUpdated;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnCountdownTick += HandleCountdown;
                gm.OnScorePoolUpdated += UpdateScorePool;
            }

            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                if (_forceSystem != null)
                    _forceSystem.OnForceUpdated -= HandleForceUpdate;
                if (_rankingSystem != null)
                    _rankingSystem.OnRankingsUpdated -= HandleRankingsUpdated;
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.OnCountdownTick -= HandleCountdown;
                    gm.OnScorePoolUpdated -= UpdateScorePool;
                }
                _subscribed = false;
            }
        }

        private void Update()
        {
            // === 平滑分割线动画 ===
            if (Mathf.Abs(_currentSplit - _targetSplit) > 0.001f)
            {
                _currentSplit = Mathf.Lerp(_currentSplit, _targetSplit, barSmoothSpeed * Time.deltaTime);
                ApplyBarSplit(_currentSplit);
            }

            // === 箭头脉冲动画 ===
            _arrowAnimTime += Time.deltaTime;

            // === 进度条动态效果 ===
            UpdateBarEffects();
        }

        private void LateUpdate()
        {
            if (_needsInitialSync)
            {
                _needsInitialSync = false;

                if (_forceSystem != null)
                    HandleForceUpdate(_forceSystem.LeftForce, _forceSystem.RightForce, _forceSystem.OrangePos);

                var gm = GameManager.Instance;
                if (gm != null && gm.RemainingTime > 0)
                    HandleCountdown(gm.RemainingTime);
            }
        }

        /// <summary>进度条视觉增强：设置阵营色+边框+发光边缘</summary>
        private void EnhanceProgressBar()
        {
            // 获取进度条Image组件并设置阵营色（如果没有Image则添加）
            if (barLeft != null)
            {
                _barLeftImg = barLeft.GetComponent<Image>();
                if (_barLeftImg == null) _barLeftImg = barLeft.gameObject.AddComponent<Image>();
                // 颜色完全交给材质Shader的GradientColor控制
                // Image.color和材质_Color都设白色，避免多重乘法导致偏红/偏暗
                _barLeftImg.color = Color.white;
                if (_barLeftImg.material != null)
                    _barLeftImg.material.SetColor("_Color", Color.white);
                _barLeftImg.raycastTarget = false;
            }
            if (barRight != null)
            {
                _barRightImg = barRight.GetComponent<Image>();
                if (_barRightImg == null) _barRightImg = barRight.gameObject.AddComponent<Image>();
                _barRightImg.color = Color.white;
                if (_barRightImg.material != null)
                    _barRightImg.material.SetColor("_Color", Color.white);
                _barRightImg.raycastTarget = false;
            }
            if (barDivider != null)
            {
                _barDividerImg = barDivider.GetComponent<Image>();
                if (_barDividerImg == null) _barDividerImg = barDivider.gameObject.AddComponent<Image>();
                _barDividerImg.color = Color.white;
                _barDividerImg.raycastTarget = false;
            }

            // 为左侧条创建右边缘发光
            if (barLeft != null && _leftGlowEdge == null)
            {
                _leftGlowEdge = new GameObject("LeftGlow", typeof(RectTransform));
                _leftGlowEdge.transform.SetParent(barLeft, false);
                var grt = _leftGlowEdge.GetComponent<RectTransform>();
                grt.anchorMin = new Vector2(1f, 0f);
                grt.anchorMax = new Vector2(1f, 1f);
                grt.pivot = new Vector2(1f, 0.5f);
                grt.sizeDelta = new Vector2(8f, 0f);
                grt.anchoredPosition = Vector2.zero;
                var gimg = _leftGlowEdge.AddComponent<Image>();
                gimg.color = BAR_LEFT_GLOW;
                gimg.raycastTarget = false;
            }

            // 为右侧条创建左边缘发光
            if (barRight != null && _rightGlowEdge == null)
            {
                _rightGlowEdge = new GameObject("RightGlow", typeof(RectTransform));
                _rightGlowEdge.transform.SetParent(barRight, false);
                var grt = _rightGlowEdge.GetComponent<RectTransform>();
                grt.anchorMin = new Vector2(0f, 0f);
                grt.anchorMax = new Vector2(0f, 1f);
                grt.pivot = new Vector2(0f, 0.5f);
                grt.sizeDelta = new Vector2(8f, 0f);
                grt.anchoredPosition = Vector2.zero;
                var gimg = _rightGlowEdge.AddComponent<Image>();
                gimg.color = BAR_RIGHT_GLOW;
                gimg.raycastTarget = false;
            }
        }

        /// <summary>进度条动态效果：边缘光脉冲+颜色亮度随进度</summary>
        private void UpdateBarEffects()
        {
            _shimmerPhase += Time.deltaTime * 2.5f;
            float pulse = 0.7f + 0.3f * Mathf.Sin(_shimmerPhase * Mathf.PI);

            // 左侧进度越大（split越高），发光越亮
            float leftProgress = Mathf.Clamp01(_currentSplit * 2f - 0.3f);   // 0.15~1映射
            float rightProgress = Mathf.Clamp01((1f - _currentSplit) * 2f - 0.3f);

            if (_leftGlowEdge != null)
            {
                var img = _leftGlowEdge.GetComponent<Image>();
                if (img != null)
                {
                    float a = Mathf.Lerp(0.3f, 0.9f, leftProgress) * pulse;
                    float w = Mathf.Lerp(4f, 14f, leftProgress);
                    img.color = new Color(BAR_LEFT_GLOW.r, BAR_LEFT_GLOW.g, BAR_LEFT_GLOW.b, a);
                    var grt = _leftGlowEdge.GetComponent<RectTransform>();
                    grt.sizeDelta = new Vector2(w, 0f);
                }
            }

            if (_rightGlowEdge != null)
            {
                var img = _rightGlowEdge.GetComponent<Image>();
                if (img != null)
                {
                    float a = Mathf.Lerp(0.3f, 0.9f, rightProgress) * pulse;
                    float w = Mathf.Lerp(4f, 14f, rightProgress);
                    img.color = new Color(BAR_RIGHT_GLOW.r, BAR_RIGHT_GLOW.g, BAR_RIGHT_GLOW.b, a);
                    var grt = _rightGlowEdge.GetComponent<RectTransform>();
                    grt.sizeDelta = new Vector2(w, 0f);
                }
            }

            // 进度条颜色由材质Shader控制（_PulseIntensity=0.08自带脉冲），
            // 代码不再覆盖Image.color，保持白色让材质渐变正常显示

            // 分割线脉冲白色
            if (_barDividerImg != null)
            {
                float divAlpha = 0.8f + 0.2f * Mathf.Sin(_shimmerPhase * Mathf.PI * 1.5f);
                _barDividerImg.color = new Color(1f, 1f, 1f, divAlpha);
            }
        }

        private void HandleForceUpdate(float left, float right, float orangePos)
        {
            // === 推力文字 ===
            if (leftForceText)
                leftForceText.text = left > 0 ? $"推力:{left:F0}" : "推力:0";
            if (rightForceText)
                rightForceText.text = right > 0 ? $"推力:{right:F0}" : "推力:0";

            // === 双向挤压进度条：计算目标分割点 ===
            const float GOAL_DIST = 100f; // 与服务器winThreshold一致
            // orangePos: -45(柚方赢) ~ 0(中间) ~ +45(橙方赢)
            // split:      0.0         ~ 0.5     ~ 1.0
            _targetSplit = Mathf.Clamp01(0.5f + (orangePos / GOAL_DIST) * 0.5f);

            // 旧版兼容（如果还有引用）
            if (progressBarLeft) progressBarLeft.fillAmount = Mathf.Clamp01(orangePos / GOAL_DIST);
            if (progressBarRight) progressBarRight.fillAmount = Mathf.Clamp01(-orangePos / GOAL_DIST);

            // === 距离标记 ===
            float distToRight = Mathf.Max(0f, GOAL_DIST - orangePos);
            float distToLeft = Mathf.Max(0f, GOAL_DIST + orangePos);

            if (leftEndMarker)
                leftEndMarker.text = $"{distToLeft:F1}米";
            if (rightEndMarker)
                rightEndMarker.text = $"{distToRight:F1}米";

            if (centerMarker)
                centerMarker.text = $"{Mathf.Abs(orangePos):F1}米";

            // 移动方向
            float deltaPos = orangePos - _prevOrangePos;
            _prevOrangePos = orangePos;

            if (posIndicatorText)
            {
                string dirArrow = "";
                if (Mathf.Abs(deltaPos) > 0.01f)
                    dirArrow = deltaPos > 0 ? " >>>" : " <<<";

                if (distToRight < distToLeft)
                    posIndicatorText.text = $"距香橙终点 {distToRight:F1}米{dirArrow}";
                else
                    posIndicatorText.text = $"距柚子终点 {distToLeft:F1}米{dirArrow}";
            }

            UpdateHintText(left, right);
        }

        /// <summary>
        /// 应用分割点到进度条UI
        /// split: 0.0(柚方满) ~ 0.5(中间) ~ 1.0(橙方满)
        /// </summary>
        private void ApplyBarSplit(float split)
        {
            split = Mathf.Clamp01(split);

            // BarLeft: anchor从0到split
            if (barLeft != null)
            {
                barLeft.anchorMin = new Vector2(0f, 0f);
                barLeft.anchorMax = new Vector2(split, 1f);
                barLeft.offsetMin = Vector2.zero;
                barLeft.offsetMax = Vector2.zero;
            }

            // BarRight: anchor从split到1
            if (barRight != null)
            {
                barRight.anchorMin = new Vector2(split, 0f);
                barRight.anchorMax = new Vector2(1f, 1f);
                barRight.offsetMin = Vector2.zero;
                barRight.offsetMax = Vector2.zero;
            }

            // BarDivider: 跟随分割点
            if (barDivider != null)
            {
                barDivider.anchorMin = new Vector2(split, 0f);
                barDivider.anchorMax = new Vector2(split, 1f);
                barDivider.sizeDelta = new Vector2(4f, 0f);
                barDivider.anchoredPosition = Vector2.zero;
            }

            // PosIndicator + OrangeIcon: 跟随分割点水平移动
            if (posIndicatorRect != null && progressBarContainerRect != null)
            {
                // ProgressBarContainer pivot=0.5, 所以中心=anchoredPosition.x
                // 分割点在容器内的局部x坐标 = (split - 0.5) * containerWidth
                float containerWidth = progressBarContainerRect.sizeDelta.x;
                float barCenterX = progressBarContainerRect.anchoredPosition.x;
                float posX = barCenterX + (split - 0.5f) * containerWidth;
                // 限制橘子图标不超出进度条两端（防止与距离数字重叠）
                float halfBar = containerWidth * 0.5f;
                float margin = 40f; // 两端留出40px防止与距离文字重叠
                posX = Mathf.Clamp(posX, barCenterX - halfBar + margin, barCenterX + halfBar - margin);
                posIndicatorRect.anchoredPosition = new Vector2(posX, posIndicatorRect.anchoredPosition.y);
            }
        }

        private void UpdateHintText(float left, float right)
        {
            if (hintText == null) return;

            float diff = Mathf.Abs(left - right);

            // 防抖：差值变化不足5时不更新
            if (Mathf.Abs(diff - _lastHintDiff) < 5f) return;
            _lastHintDiff = diff;

            if (diff < 10)
            {
                hintText.text = "势均力敌!";
                hintText.color = Color.white;
            }
            else if (left > right)
            {
                hintText.text = $"柚方差 {diff:F0} 推力反击";
                hintText.color = new Color(0.4f, 0.9f, 0.4f, 1f);
            }
            else
            {
                hintText.text = $"橙方差 {diff:F0} 推力反击";
                hintText.color = new Color(1f, 0.6f, 0.2f, 1f);
            }
        }

        private void HandleCountdown(float remaining)
        {
            if (timerText)
            {
                int min = Mathf.FloorToInt(remaining / 60f);
                int sec = Mathf.FloorToInt(remaining % 60f);
                timerText.text = $"{min:00}:{sec:00}";
                timerText.color = remaining <= 30 ? Color.red : Color.white;
            }
        }

        public void UpdateScorePool(float amount)
        {
            if (scorePoolText)
            {
                if (amount >= 10000)
                    scorePoolText.text = $"积分池:{amount / 10000f:F1}万";
                else
                    scorePoolText.text = $"积分池:{amount:F0}";
            }
        }

        /// <summary>排行榜更新时自动读取连胜信息</summary>
        private void HandleRankingsUpdated()
        {
            if (_rankingSystem == null) return;
            var info = _rankingSystem.CurrentStreakInfo;
            if (info != null)
            {
                int leftS = info.left != null ? info.left.streak : 0;
                int rightS = info.right != null ? info.right.streak : 0;
                SetWinStreak(leftS, rightS);
            }
        }

        public void SetWinStreak(int leftStreak, int rightStreak)
        {
            _leftWinStreak = leftStreak;
            _rightWinStreak = rightStreak;
            UpdateWinStreakDisplay();
        }

        private void UpdateWinStreakDisplay()
        {
            // 显示阵营连胜池（所有玩家投注之和）
            if (winStreakLeftText)
                winStreakLeftText.text = _leftWinStreak > 0 ? $"连胜池:<size=130%>{_leftWinStreak}</size>" : "连胜池:0";
            if (winStreakRightText)
                winStreakRightText.text = _rightWinStreak > 0 ? $"连胜池:<size=130%>{_rightWinStreak}</size>" : "连胜池:0";
        }

        private void OnAnnouncementButtonClicked()
        {
            Debug.Log("[TopBarUI] 公告按钮被点击");
            var announcementUI = FindObjectOfType<AnnouncementPanelUI>();
            if (announcementUI != null)
            {
                announcementUI.Toggle();
            }
            else
            {
                Debug.LogWarning("[TopBarUI] AnnouncementPanelUI not found on Canvas");
            }
        }

        private void OnSettingsButtonClicked()
        {
            Debug.Log("[TopBarUI] 设置按钮被点击");
            var settingsPanel = FindObjectOfType<SettingsPanelUI>(true);
            if (settingsPanel != null)
            {
                settingsPanel.Toggle();
            }
            else
            {
                Debug.LogWarning("[TopBarUI] SettingsPanelUI not found. Attach it to Canvas.");
            }
        }
    }
}
