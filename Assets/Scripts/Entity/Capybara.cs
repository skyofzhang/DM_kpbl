using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CapybaraDuel.Entity
{
    /// <summary>
    /// 卡皮巴拉单位实体 - Animator 驱动 + 阵营分割 + 队形排列
    /// </summary>
    public class Capybara : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string unitId;
        [SerializeField] private float force = 10f;
        [SerializeField] private float moveSpeed = 0.8f;
        [SerializeField] private float lifetime = 30f;

        [Header("Visual")]
        [Tooltip("基础缩放倍数，用于调节模型显示大小")]
        [SerializeField] private float baseScale = 1f;

        [Header("Formation")]
        [Tooltip("距离橘子的最小停止距离（紧贴橘子外侧）")]
        [SerializeField] private float minStopDistance = 2.0f;
        [Tooltip("距离橘子的最大停止距离（后排）")]
        [SerializeField] private float maxStopDistance = 3.5f;

        [Header("Separation")]
        [Tooltip("与其他单位的最小间距（基准值，实际按单位缩放调整）")]
        [SerializeField] private float separationRadius = 0.8f;
        [Tooltip("分离推力强度")]
        [SerializeField] private float separationForce = 8.0f;

        [Header("Retreat Formation")]
        [Tooltip("退却时平滑移动速度")]
        [SerializeField] private float retreatLerpSpeed = 5f;
        [Tooltip("被压缩时Z轴最大散开幅度")]
        [SerializeField] private float zSpreadMax = 6f;
        [Tooltip("可用宽度低于此值时开始扩大Z轴散开")]
        [SerializeField] private float squeezedThreshold = 8f;
        [Tooltip("橘子外侧缓冲距离（增大防穿模）")]
        [SerializeField] private float campBuffer = 1.5f;

        [Header("Bounce Back (反推弹开)")]
        [Tooltip("反推时后排追踪前排的速度倍数")]
        [SerializeField] private float retreatChainSpeed = 15f;

        [Header("Spawn Animation")]
        [Tooltip("入场缩放动画时长（秒）")]
        [SerializeField] private float spawnAnimDuration = 0.6f;
        [Tooltip("入场时的弹性过冲倍数")]
        [SerializeField] private float spawnOvershoot = 1.15f;

        private Camp camp;
        private Transform target;
        private float spawnTime;
        private Animator _animator;
        private bool _isMoving;
        private float _stopDistance;
        private int _spawnOrder;
        private static int _globalSpawnCounter = 0;
        private float _targetScale = 1f;
        private bool _isSpawnAnimating = false;
        private Vector3 _prevFramePos;
        // 阵型目标位置平滑追踪：避免重排时所有单位瞬间收缩
        private float _smoothFormationZ = 0f;
        private float _smoothFormationX = 0f;
        private bool _hasFormationTarget = false;
        /// <summary>是否正在向橘子方向前进（供FootDustManager判断）</summary>
        public bool IsAdvancing { get; private set; }

        // 动态阵型：按阵营维护存活单位列表，每帧根据列表索引排列
        private static readonly List<Capybara> _leftAlive = new List<Capybara>();
        private static readonly List<Capybara> _rightAlive = new List<Capybara>();
        public static IReadOnlyList<Capybara> LeftAlive => _leftAlive;
        public static IReadOnlyList<Capybara> RightAlive => _rightAlive;
        private int _cachedFormationIdx = -1; // 缓存的阵型索引
        private static bool _formationDirty = true; // 列表变化时标记脏（仅新增/删除单位）
        private static float _lastFormationRefreshTime = -10f; // 上次全排重排时间
        private const float FORMATION_COOLDOWN = 1.0f; // 重排最小间隔（秒）—足够长以合并批量事件
        private static bool _formationPendingRefresh = false; // 冷却期间挂起的重排请求
        private int _stableSpawnOrder = 0; // 稳定排序用的出生顺序（同贡献值时不乱序）
        // (campMaxScale已移除：阵型排列不再按单位scale调整间距)
        private float _spawnArrivalTime = 0f; // 入场完成时间（过了渐进期后才参与排序）
        private const float SPAWN_SETTLE_DELAY = 0.8f; // 新单位入场后N秒才参与前排排序（缩短以减少空位）
        private bool _hasSettled = false; // 是否已过渐进期

        // 头顶HUD（头像+名字）
        private Transform _hudRoot;
        private RawImage _avatarImage;
        private Transform _nameLabel;
        private TextMeshProUGUI _nameText;
        private int _currentTier = 0;
        private int _upgradeLevel = 1; // 仙女棒升级等级 1~10

        /// <summary>对象池tier标记（Despawn时用于归还到正确的池）</summary>
        [HideInInspector] public int PoolTier = 0;

        /// <summary>单位贡献价值（用于阵型排序，值越高排越前面靠近橘子）</summary>
        [HideInInspector] public float ContributionValue = 0f;

        private static Camera _mainCam;
        private static TMP_FontAsset _chineseFont;
        private static Material _circleMaskMat; // 圆形裁剪材质（全局共享基础）

        // 头顶HUD自转参数（世界Y轴连续旋转 + 朝相机倾斜）
        private float _hudYAngle;                         // HUD累积的Y轴旋转角度
        private const float HUD_ROTATE_SPEED = 45f;       // 自转速度（度/秒），约8秒一圈
        private const float HUD_TILT_ANGLE = 35f;         // 朝相机倾斜角度（让俯视角能看到正面）

        /// <summary>玩家头像URL（由服务器推送）</summary>
        public string AvatarUrl { get; set; }
        /// <summary>玩家名称</summary>
        public string PlayerName { get; set; }
        /// <summary>玩家ID</summary>
        public string PlayerId { get; set; }

        // 缓存 Animator 参数哈希
        private static readonly int Hash_Speed = Animator.StringToHash("Speed");
        private static readonly int Hash_IsPushing = Animator.StringToHash("IsPushing");
        private static readonly int Hash_IsDead = Animator.StringToHash("IsDead");

        private bool _hasSpeed;
        private bool _hasIsPushing;
        private bool _hasIsDead;

        public string UnitId => unitId;
        public float Force => force;
        public Camp Camp => camp;

        /// <summary>回收回调，由 Spawner 订阅</summary>
        public event Action<Capybara> OnDespawned;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            CacheAnimatorParams();
        }

        private void CacheAnimatorParams()
        {
            _hasSpeed = false;
            _hasIsPushing = false;
            _hasIsDead = false;

            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            foreach (var p in _animator.parameters)
            {
                if (p.nameHash == Hash_Speed) _hasSpeed = true;
                else if (p.nameHash == Hash_IsPushing) _hasIsPushing = true;
                else if (p.nameHash == Hash_IsDead) _hasIsDead = true;
            }
        }

        public void Initialize(string id, Camp camp, float force, float lifetime, Transform target,
            string tier = "0")
        {
            this.unitId = id;
            this.camp = camp;
            this.force = force;
            this.lifetime = lifetime;
            this.target = target;
            this.spawnTime = Time.time;
            this._isMoving = false;

            // 头顶HUD自转初始角度随机（各单位不同步）
            _hudYAngle = UnityEngine.Random.Range(0f, 360f);

            // 生成顺序（用于稳定排序：同贡献值的单位按出生先后排，不乱跳）
            _spawnOrder = _globalSpawnCounter++;
            _stableSpawnOrder = _spawnOrder;
            // 贡献值 = 推力值（gift单位越贵推力越高，排前面）
            ContributionValue = force;
            // 新单位入场渐进期：先在末位，N秒后才参与前排排序
            _spawnArrivalTime = Time.time;
            _hasSettled = false;
            // 加入阵营存活列表（动态阵型用）
            var aliveList = (camp == Camp.Left) ? _leftAlive : _rightAlive;
            if (!aliveList.Contains(this))
                aliveList.Add(this);
            _formationDirty = true;

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                CacheAnimatorParams();
            }

            // 视觉效果：根据 tier 等级应用缩放+发光+颜色
            int tierInt = VFX.UnitVisualEffect.ParseTier(tier);
            _currentTier = tierInt;
            var visualEffect = GetComponent<VFX.UnitVisualEffect>();
            if (visualEffect != null)
            {
                float scale = GetBaseScale(id);
                visualEffect.ApplyTier(tierInt, scale);
            }
            else
            {
                ApplyScale(id); // 回退：无 UnitVisualEffect 组件时用旧逻辑
            }

            // 设置阵营菲涅尔颜色
            var campEffect = GetComponent<VFX.CapybaraCampEffect>();
            if (campEffect == null) campEffect = GetComponentInChildren<VFX.CapybaraCampEffect>();
            if (campEffect != null) campEffect.SetCamp(camp);

            var vfx = VFX.VFXSpawner.Instance;
            if (vfx != null)
                vfx.PlaySpawnVFX(transform.position);

            // 初始化位置追踪
            _prevFramePos = transform.position;
            IsAdvancing = false;
            // 重置阵型目标追踪（对象池复用时需要重新初始化）
            _hasFormationTarget = false;

            // 入场缩放动画：从0缩放弹性放大到目标大小
            _targetScale = transform.localScale.x;

            // 停止距离计算（必须在ApplyTier之后，_targetScale已有正确值）
            float t = Mathf.Clamp01(_spawnOrder / 10f);
            // 高tier单位体型大，增加额外停止距离防止穿模（scale 1.0→+0, 1.87→+0.7）
            float scaleBonus = Mathf.Max(0f, (_targetScale - 1f) * 0.8f);
            _stopDistance = Mathf.Lerp(minStopDistance, maxStopDistance, t)
                           + scaleBonus + UnityEngine.Random.Range(-0.2f, 0.2f);
            transform.localScale = Vector3.zero;
            _isSpawnAnimating = true;
        }

        /// <summary>设置玩家信息（头像+名字HUD）</summary>
        public void SetPlayerInfo(string playerId, string playerName, string avatarUrl = null)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            AvatarUrl = avatarUrl;

            Debug.Log($"[Capybara] SetPlayerInfo tier={_currentTier} id={playerId} name={playerName} hasAvatar={!string.IsNullOrEmpty(avatarUrl)} avatarUrl={avatarUrl ?? "NULL"}");

            // 创建或更新头顶HUD（头像+名字）
            CreateOrUpdateHeadHUD(playerName, avatarUrl);
        }

        /// <summary>应用升级等级视觉效果（仙女棒累积升级 Lv.1~10）</summary>
        public void ApplyUpgradeLevel(int level)
        {
            _upgradeLevel = Mathf.Clamp(level, 1, 10);
            // 升级后贡献值增加（等级×基础推力），排序更靠前
            ContributionValue = force + _upgradeLevel * 10f;
            _formationDirty = true;

            var visualEffect = GetComponent<VFX.UnitVisualEffect>();
            if (visualEffect != null)
            {
                float scale = GetBaseScale(unitId);
                visualEffect.ApplyUpgradeLevel(_upgradeLevel, scale);
            }
        }

        /// <summary>获取基础缩放（不含 tier 加成）</summary>
        private float GetBaseScale(string id)
        {
            float scale = baseScale;
            if (id != null)
            {
                if (id.StartsWith("31"))
                    scale = baseScale * 1.1f; // 召唤单位略大（tier会进一步放大）
            }
            return scale;
        }

        private void ApplyScale(string id)
        {
            transform.localScale = Vector3.one * GetBaseScale(id);
        }

        private void Update()
        {
            // 入场缩放动画
            if (_isSpawnAnimating)
            {
                float elapsed = Time.time - spawnTime;
                float t = Mathf.Clamp01(elapsed / spawnAnimDuration);
                // 弹性缓动：先过冲再回弹
                float scale;
                if (t < 0.6f)
                {
                    // 0~0.6: 从0到overshoot
                    scale = Mathf.Lerp(0f, _targetScale * spawnOvershoot, t / 0.6f);
                }
                else
                {
                    // 0.6~1.0: 从overshoot回弹到target
                    scale = Mathf.Lerp(_targetScale * spawnOvershoot, _targetScale, (t - 0.6f) / 0.4f);
                }
                transform.localScale = Vector3.one * scale;
                if (t >= 1f)
                    _isSpawnAnimating = false;
            }

            if (lifetime > 0 && Time.time - spawnTime > lifetime)
            {
                Despawn();
                return;
            }

            if (target != null)
            {
                // EnforceCampBoundary 全权负责X位置，moveSpeed只负责在阵型分配前让新单位靠近
                // 阵营边界约束 + 阵型排列（这里决定最终X位置）
                EnforceCampBoundary();

                // 朝向始终面向橘子
                var direction = (target.position - transform.position);
                direction.y = 0;
                if (direction != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(direction.normalized), 8f * Time.deltaTime);

                // 动画：始终播放推动动画
                if (_hasSpeed && _animator != null)
                    _animator.SetFloat(Hash_Speed, 1f);
            }

            // 分离力只作用于Z轴，X轴由阵型系统严格控制
            ApplySeparationZOnly();

            // 追踪前进状态（供FootDustManager判断是否发烟）
            float dx = transform.position.x - _prevFramePos.x;
            if (camp == Camp.Left)
                IsAdvancing = dx > 0.001f; // 左阵营向右(+X)是前进
            else
                IsAdvancing = dx < -0.001f; // 右阵营向左(-X)是前进
            _prevFramePos = transform.position;

            // 头顶HUD：世界Y轴持续自转 + 朝相机倾斜（从俯视角能清晰看到头像旋转）
            if (_hudRoot != null)
            {
                // 累加式旋转（避免Time.time过大时精度丢失）
                _hudYAngle += HUD_ROTATE_SPEED * Time.deltaTime;
                if (_hudYAngle > 360f) _hudYAngle -= 360f;

                // 世界空间旋转：先倾斜朝上（让俯视相机看到正面），再绕Y轴自转
                _hudRoot.rotation = Quaternion.Euler(HUD_TILT_ANGLE, _hudYAngle, 0f);
            }
        }

        /// <summary>
        /// 动态阵型 + 反推弹开（v4）
        ///
        /// 排列策略：
        /// 1. 按贡献值降序排列，大推力单位优先前排（靠近橘子）
        /// 2. 每排12个，同排内大单位放中间(Z≈0)，小单位向两侧散开
        /// 3. 稳定索引 + 冷却机制：减少频繁重排导致的全体抖动
        /// 4. 追踪速度降低，过渡更平滑自然
        /// </summary>
        private void EnforceCampBoundary()
        {
            if (target == null) return;
            float orangeX = target.position.x;
            Vector3 pos = transform.position;

            const float LEFT_SPAWN_X = -28f;
            const float RIGHT_SPAWN_X = 28f;
            const int ROW_CAPACITY = 12;         // 每排固定容量
            const float BASE_ROW_SPACING = 1.1f;  // X排间距
            const float BASE_Z_SPACING = 0.65f;   // Z列间距（12列需要更紧凑）

            // 动态索引（带冷却：避免短时间内反复全排重排导致抖动）
            if (_cachedFormationIdx < 0)
            {
                // 新单位必须立即分配索引
                RefreshAllFormationIndices();
            }
            else if (_formationDirty || _formationPendingRefresh)
            {
                float now = Time.time;
                if (now - _lastFormationRefreshTime >= FORMATION_COOLDOWN)
                {
                    RefreshAllFormationIndices();
                    _lastFormationRefreshTime = now;
                    _formationPendingRefresh = false;
                }
                else
                {
                    _formationPendingRefresh = true;
                }
            }
            int idx = _cachedFormationIdx;
            if (idx < 0) return;

            // boundary = 橘子外侧缓冲线（第0排的位置）
            float boundaryX, spawnEdge;
            if (camp == Camp.Left)
            {
                boundaryX = orangeX - campBuffer;
                spawnEdge = LEFT_SPAWN_X;
            }
            else
            {
                boundaryX = orangeX + campBuffer;
                spawnEdge = RIGHT_SPAWN_X;
            }
            float availableDepth = Mathf.Abs(spawnEdge - boundaryX);

            // 动态排间距：空间不足时压缩
            float rowSpacing = BASE_ROW_SPACING;
            if (availableDepth < squeezedThreshold)
            {
                float ratio = availableDepth / squeezedThreshold;
                rowSpacing = Mathf.Lerp(0.5f, BASE_ROW_SPACING, ratio);
            }

            // 动态Z散开：空间不足时扩大Z
            float zSpreadMultiplier = 1f;
            if (availableDepth < squeezedThreshold)
            {
                float ratio = availableDepth / squeezedThreshold;
                zSpreadMultiplier = Mathf.Lerp(2f, 1f, ratio);
            }

            // ====== 排号和列号（固定每排容量）======
            int row = idx / ROW_CAPACITY;
            int col = idx % ROW_CAPACITY;

            // ====== Z轴排列：大单位按scale放大间距，小单位紧凑排列 ======
            float baseZSpacing = BASE_Z_SPACING * zSpreadMultiplier;
            // 大单位(scale>=1.3)间距放大，小单位保持原间距
            float scaleFactor = Mathf.Max(_targetScale, 1f);
            float myZSpacing = baseZSpacing * Mathf.Lerp(1f, scaleFactor, 0.7f);
            float targetZ;
            if (col == 0)
            {
                targetZ = 0f;
            }
            else
            {
                int ring = (col + 1) / 2;
                float sign = (col % 2 == 1) ? 1f : -1f;
                targetZ = sign * ring * myZSpacing;
            }
            targetZ = Mathf.Clamp(targetZ, -zSpreadMax, zSpreadMax);
            // 相邻排Z偏移半个间距（砖墙式排列，减少前后排对齐穿模）
            targetZ += (row % 2) * baseZSpacing * 0.5f;

            // ====== idealX：从 boundary 按排号排列 ======
            // 统一排间距：不按单位scale调整，所有单位同一个网格
            float scaledRowSpacing = rowSpacing;
            float idealX;
            if (camp == Camp.Left)
                idealX = boundaryX - row * scaledRowSpacing;
            else
                idealX = boundaryX + row * scaledRowSpacing;

            // ====== 平滑阵型目标追踪（防止重排时"缩成一坨"）======
            if (!_hasFormationTarget)
            {
                _smoothFormationX = idealX;
                _smoothFormationZ = targetZ;
                _hasFormationTarget = true;
            }
            else
            {
                float trackSpeed = 1.5f * Time.deltaTime;
                _smoothFormationX = Mathf.Lerp(_smoothFormationX, idealX, trackSpeed);
                _smoothFormationZ = Mathf.Lerp(_smoothFormationZ, targetZ, trackSpeed);
            }

            // ====== 反推弹开 ======
            bool beingPushed;
            if (camp == Camp.Left)
                beingPushed = pos.x > _smoothFormationX + 0.1f;
            else
                beingPushed = pos.x < _smoothFormationX - 0.1f;

            // 硬约束：不越过 boundary
            if (camp == Camp.Left && pos.x > boundaryX)
                pos.x = boundaryX;
            else if (camp == Camp.Right && pos.x < boundaryX)
                pos.x = boundaryX;

            if (beingPushed)
            {
                pos.x = Mathf.Lerp(pos.x, _smoothFormationX, retreatLerpSpeed * 2.5f * Time.deltaTime);
            }
            else
            {
                pos.x = Mathf.Lerp(pos.x, _smoothFormationX, retreatLerpSpeed * Time.deltaTime);
            }

            // Z轴平滑移动
            pos.z = Mathf.Lerp(pos.z, _smoothFormationZ, retreatLerpSpeed * 0.6f * Time.deltaTime);

            // 硬约束：不越过 boundary
            if (camp == Camp.Left && pos.x > boundaryX)
                pos.x = boundaryX;
            else if (camp == Camp.Right && pos.x < boundaryX)
                pos.x = boundaryX;

            transform.position = pos;
        }

        /// <summary>
        /// Z轴分离力 — 防止同排单位重叠穿模
        /// X轴由阵型系统控制，分离只作用于Z
        ///
        /// v4+关键改动：分离力修改 _smoothFormationZ（阵型目标位置），
        /// 而非直接改 transform.position。这样分离力和阵型追踪不再互相对抗，
        /// 重叠的单位会慢慢滑开到新的目标位置并保持。
        /// </summary>
        private void ApplySeparationZOnly()
        {
            float scaleFactor = Mathf.Max(_targetScale, 1f);
            // 检测范围：适度放大（降低大单位的扩大倍率，防止过远检测）
            float effectiveRadius = separationRadius * Mathf.Lerp(1f, scaleFactor, 0.6f) * 1.2f;
            var colliders = Physics.OverlapSphere(transform.position, effectiveRadius);
            float zSeparation = 0f;
            int neighborCount = 0;

            foreach (var col in colliders)
            {
                if (col.gameObject == gameObject) continue;
                var otherCapy = col.GetComponent<Capybara>();
                if (otherCapy == null) continue;

                float otherScale = Mathf.Max(otherCapy._targetScale, 1f);
                // pair半径：两个单位scale的平均值×基础半径
                // 降低scale对pair半径的影响（lerp到0.5），防止大单位推开太远
                float pairScale = Mathf.Lerp(1f, (scaleFactor + otherScale) * 0.5f, 0.5f);
                float pairRadius = separationRadius * pairScale;

                float dz = transform.position.z - col.transform.position.z;
                float dx = Mathf.Abs(transform.position.x - col.transform.position.x);
                float dist = Mathf.Abs(dz);

                if (dist < 0.01f)
                {
                    dz = UnityEngine.Random.Range(-1f, 1f);
                    dist = 0.5f;
                }

                // 距离衰减：越近推力越强
                float strength = 1f - Mathf.Min(dist, pairRadius) / pairRadius;
                // 同排(X轴近)推力更强
                float xBoost = Mathf.Clamp01(1f - dx / 2f) + 0.5f;
                zSeparation += Mathf.Sign(dz) * strength * xBoost;
                neighborCount++;
            }

            if (neighborCount > 0)
            {
                float avgSep = zSeparation / neighborCount;
                // 分离力：不再按scaleFactor线性放大，用sqrt缓和
                float effectiveForce = separationForce * Mathf.Sqrt(scaleFactor);
                float crowdBoost = Mathf.Min(neighborCount, 5) * 0.2f + 0.8f;
                float delta = avgSep * effectiveForce * crowdBoost * Time.deltaTime;
                // 限制单帧偏移量，防止一次被弹太远
                delta = Mathf.Clamp(delta, -0.5f, 0.5f);
                _smoothFormationZ += delta;
            }

            // 回弹机制：如果分离力把目标Z推得太远（超出zSpreadMax×0.8），逐步拉回
            // 防止小单位被弹到阵型外缘后永远回不来
            float maxDeviation = zSpreadMax * 0.8f;
            if (Mathf.Abs(_smoothFormationZ) > maxDeviation)
            {
                float excess = Mathf.Abs(_smoothFormationZ) - maxDeviation;
                float pullBack = Mathf.Min(excess * 2f * Time.deltaTime, excess);
                _smoothFormationZ -= Mathf.Sign(_smoothFormationZ) * pullBack;
            }
        }

        private void SetMoving(bool moving)
        {
            if (_isMoving == moving) return;
            _isMoving = moving;
            if (_animator != null && _hasSpeed)
                _animator.SetFloat(Hash_Speed, moving ? 1f : 0f);
        }

        public void Despawn()
        {
            var vfx = VFX.VFXSpawner.Instance;
            if (vfx != null)
                vfx.PlayDespawnVFX(transform.position);

            if (_animator != null && _hasIsDead)
                _animator.SetBool(Hash_IsDead, true);

            // 从存活列表移除（剩余单位自动填补空位）
            if (_leftAlive.Remove(this) || _rightAlive.Remove(this))
                _formationDirty = true;

            // 清空头顶HUD（对象池回收时保留GO，只清空内容）
            if (_nameText != null)
                _nameText.text = "";
            if (_avatarImage != null)
                _avatarImage.texture = null;
            PlayerName = null;
            PlayerId = null;
            AvatarUrl = null;
            _currentTier = 0;
            _upgradeLevel = 1;
            // PoolTier 不在这里重置，Spawner.HandleDespawn 读取后再归零

            // 清理视觉效果（恢复共享材质，对象池复用时不残留）
            var visualEffect = GetComponent<VFX.UnitVisualEffect>();
            if (visualEffect != null) visualEffect.Cleanup();

            OnDespawned?.Invoke(this);
            OnDespawned = null;
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                CacheAnimatorParams();
            }

            if (_animator != null)
            {
                if (_hasIsDead) _animator.SetBool(Hash_IsDead, false);
                if (_hasIsPushing) _animator.SetBool(Hash_IsPushing, true);
                if (_hasSpeed) _animator.SetFloat(Hash_Speed, 1f);
            }

            // 安全措施：激活时清除所有残留的 MaterialPropertyBlock
            // 防止从对象池复用时保留了破坏 SRP Batcher 的 PropertyBlock
            ClearAllPropertyBlocks();
        }

        /// <summary>
        /// 清除所有 Renderer 上残留的 MaterialPropertyBlock
        /// 在 URP 中，SetPropertyBlock 会破坏 SRP Batcher 兼容性导致模型不可见
        /// </summary>
        private void ClearAllPropertyBlocks()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    try { r.SetPropertyBlock(null); }
                    catch (System.Exception) { }
                }
            }
        }

        /// <summary>重置全局生成计数器（游戏重置时调用）</summary>
        public static void ResetSpawnCounter()
        {
            _globalSpawnCounter = 0;
            _leftAlive.Clear();
            _rightAlive.Clear();
            _formationDirty = true;
            _formationPendingRefresh = false;
            _lastFormationRefreshTime = -10f;
        }

        /// <summary>
        /// 刷新阵型索引（v4+: 交错分配 + 入场渐进）
        /// </summary>
        private static void RefreshAllFormationIndices()
        {
            AssignInterleavedIndices(_leftAlive);
            AssignInterleavedIndices(_rightAlive);
            _formationDirty = false;
        }

        private const int FORMATION_ROW_CAPACITY = 12;

        /// <summary>
        /// 阵型分配算法 v4+ — 稳定排序 + 入场渐进
        ///
        /// 核心设计：
        /// 1. 未安定的新单位（_hasSettled=false）排在末尾，从后排入场
        /// 2. 已安定单位：贡献值 > 体型 > 出生顺序（三级排序键保证确定性）
        /// 3. 索引一旦分配，只有跨排变化才触发移动
        /// 4. 新单位过渐进期后自动参与排序，通过平滑追踪慢慢滑到前排
        /// </summary>
        private static void AssignInterleavedIndices(List<Capybara> aliveList)
        {
            if (aliveList.Count == 0) return;

            // 检查并更新渐进期状态
            float now = Time.time;
            bool anyNewlySettled = false;
            foreach (var c in aliveList)
            {
                if (!c._hasSettled && now - c._spawnArrivalTime >= SPAWN_SETTLE_DELAY)
                {
                    c._hasSettled = true;
                    anyNewlySettled = true;
                }
            }
            // 有新安定的单位时标记脏（触发重排让它加入正确位置）
            if (anyNewlySettled) _formationDirty = true;

            // 保存旧索引
            var oldIndices = new Dictionary<Capybara, int>(aliveList.Count);
            foreach (var c in aliveList)
                oldIndices[c] = c._cachedFormationIdx;

            // 排序：未安定单位排末尾，已安定单位按 贡献值降序→体型降序→出生顺序升序
            aliveList.Sort((a, b) =>
            {
                // 未安定的排后面（settled=true 排前面）
                if (a._hasSettled != b._hasSettled)
                    return a._hasSettled ? -1 : 1;

                int cmp = b.ContributionValue.CompareTo(a.ContributionValue);
                if (cmp != 0) return cmp;
                cmp = b._targetScale.CompareTo(a._targetScale);
                if (cmp != 0) return cmp;
                return a._stableSpawnOrder.CompareTo(b._stableSpawnOrder);
            });

            // 检查是否有空位需要填补（单位数量 < 最大旧索引+1 表示有人离开）
            int maxOldIdx = -1;
            foreach (var kv in oldIndices)
                if (kv.Value > maxOldIdx) maxOldIdx = kv.Value;
            bool hasGaps = aliveList.Count <= maxOldIdx; // 有单位离开导致索引不连续

            for (int i = 0; i < aliveList.Count; i++)
            {
                int newIdx = i;
                int oldIdx;
                oldIndices.TryGetValue(aliveList[i], out oldIdx);

                if (oldIdx >= 0 && oldIdx == newIdx)
                    continue;

                // 有空位时必须重新分配索引以填补空位
                if (!hasGaps && oldIdx >= 0)
                {
                    int oldRow = oldIdx / FORMATION_ROW_CAPACITY;
                    int newRow = newIdx / FORMATION_ROW_CAPACITY;
                    if (oldRow == newRow)
                        continue;
                }

                aliveList[i]._cachedFormationIdx = newIdx;
            }
        }

        // ==================== 头顶HUD（头像+名字） ====================

        /// <summary>
        /// 创建或更新头顶HUD
        /// - 所有玩家：显示圆形头像（有URL加载真实头像，无URL显示阵营色块）
        /// - 礼物召唤单位(tier>=1)：额外显示名字，颜色/描边随tier升级
        /// </summary>
        private void CreateOrUpdateHeadHUD(string playerName, string avatarUrl)
        {
            // ===== HUD已存在时只更新内容（对象池复用） =====
            // 安全检查：_hudRoot引用可能在对象池复用时变成destroyed但非null
            if (_hudRoot != null && _hudRoot.gameObject == null)
            {
                Debug.LogWarning($"[Capybara] HUD root destroyed but ref not null! tier={_currentTier} id={PlayerId} — recreating");
                _hudRoot = null;
                _avatarImage = null;
                _nameText = null;
            }
            if (_hudRoot != null)
            {
                // 更新HUD高度和缩放（对象池复用时tier可能不同）
                float reuseHeight = _currentTier >= 4 ? 0.7f : (_currentTier >= 2 ? 0.65f : 0.8f);
                _hudRoot.localPosition = new Vector3(0f, reuseHeight, 0f);
                // 使用_targetScale而非transform.localScale.x（入场动画期间localScale=0会导致HUD巨大）
                float reuseParentScale = Mathf.Max(_targetScale, 0.5f);
                float reuseHudScale = 0.004f / reuseParentScale;
                _hudRoot.localScale = new Vector3(reuseHudScale, reuseHudScale, reuseHudScale);

                UpdateAvatarContent(avatarUrl);
                UpdateNameContent(playerName);
                _hudRoot.gameObject.SetActive(true);
                return;
            }

            // ===== 创建HUD根节点（World Space Canvas） =====
            var hudGo = new GameObject("HeadHUD");
            hudGo.transform.SetParent(transform, false);
            // 高度根据tier调整：gift单位模型更大，需要稍高一点
            float hudHeight = _currentTier >= 4 ? 0.7f : (_currentTier >= 2 ? 0.65f : 0.8f);
            hudGo.transform.localPosition = new Vector3(0f, hudHeight, 0f);
            // 使用_targetScale而非transform.localScale.x（入场动画期间localScale=0会导致HUD巨大）
            // _targetScale是Initialize()中保存的最终缩放值，不受入场动画影响
            float parentScale = Mathf.Max(_targetScale, 0.5f);
            float hudScale = 0.004f / parentScale;
            hudGo.transform.localScale = new Vector3(hudScale, hudScale, hudScale);
            _hudRoot = hudGo.transform;

            var canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            var canvasRect = hudGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200f, 120f); // 足够放头像+名字

            // ===== 头像区域（圆形裁剪+边框） =====
            CreateAvatarDisplay(hudGo.transform);

            // ===== 名字区域（仅礼物召唤单位显示） =====
            CreateNameDisplay(hudGo.transform, playerName);

            // 加载头像
            UpdateAvatarContent(avatarUrl);
        }

        /// <summary>创建头像显示（圆形裁剪+边框）</summary>
        private void CreateAvatarDisplay(Transform parent)
        {
            // 头像容器（居中上部）
            var avatarGo = new GameObject("Avatar");
            avatarGo.transform.SetParent(parent, false);
            var avatarRect = avatarGo.AddComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 1f);
            avatarRect.anchorMax = new Vector2(0.5f, 1f);
            avatarRect.pivot = new Vector2(0.5f, 1f);
            // 头像大小：80x80（在0.012 scale下约 0.96m，视觉适中）
            float avatarSize = 80f;
            avatarRect.sizeDelta = new Vector2(avatarSize, avatarSize);
            avatarRect.anchoredPosition = new Vector2(0f, 0f);

            _avatarImage = avatarGo.AddComponent<RawImage>();
            _avatarImage.raycastTarget = false;

            // 应用圆形遮罩材质
            ApplyCircleMaskMaterial(_avatarImage);
        }

        /// <summary>创建名字显示（放在头像下方）</summary>
        private void CreateNameDisplay(Transform parent, string playerName)
        {
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(parent, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -84f); // 头像下方留4px间距
            nameRect.sizeDelta = new Vector2(200f, 30f);

            _nameText = nameGo.AddComponent<TextMeshProUGUI>();
            _nameText.fontSize = 14;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TextOverflowModes.Truncate;
            _nameText.raycastTarget = false;

            // 加载中文字体
            if (_chineseFont == null)
                _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ChineseFont SDF");
            if (_chineseFont != null)
                _nameText.font = _chineseFont;

            _nameText.fontStyle = FontStyles.Bold;

            // 根据tier设置名字样式
            UpdateNameContent(playerName);
        }

        /// <summary>更新头像内容</summary>
        private void UpdateAvatarContent(string avatarUrl)
        {
            if (_avatarImage == null)
            {
                Debug.LogWarning($"[Capybara] UpdateAvatarContent: _avatarImage is null! tier={_currentTier} id={PlayerId}");
                return;
            }

            // 设置边框颜色（阵营色）
            UpdateAvatarBorderColor();

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                // 先设置加载中占位（阵营色底板）
                // 必须在 loader.Load() 之前设置，因为缓存命中时回调是同步的，
                // 如果在 Load 之后设置会覆盖回调中已设好的正确纹理
                _avatarImage.texture = Texture2D.whiteTexture;
                _avatarImage.color = GetCampColor();

                // 异步加载头像
                var loader = Utils.AvatarLoader.Instance;
                if (loader == null)
                {
                    // AvatarLoader不在场景中，自动创建
                    var loaderGo = new GameObject("AvatarLoader");
                    DontDestroyOnLoad(loaderGo);
                    loader = loaderGo.AddComponent<Utils.AvatarLoader>();
                    Debug.Log("[Capybara] Auto-created AvatarLoader singleton");
                }
                if (loader != null)
                {
                    // 保存当前对象引用用于闭包安全检查
                    var capturedImage = _avatarImage;
                    var capturedId = PlayerId;
                    var capturedTier = _currentTier;
                    loader.Load(avatarUrl, tex =>
                    {
                        if (capturedImage != null && capturedImage.gameObject != null)
                        {
                            if (tex != null)
                            {
                                capturedImage.texture = tex;
                                capturedImage.color = Color.white;
                            }
                            else
                            {
                                Debug.LogWarning($"[Capybara] Avatar load failed: tier={capturedTier} id={capturedId} url={avatarUrl}");
                            }
                        }
                    });
                }
            }
            else
            {
                // 无头像 → 用纯白贴图+阵营色Tint → 显示为阵营色圆圈
                _avatarImage.texture = Texture2D.whiteTexture;
                _avatarImage.color = GetCampColor();
            }
        }

        /// <summary>获取阵营颜色（用于无头像时的底色）</summary>
        private Color GetCampColor()
        {
            return camp == Camp.Left
                ? new Color(1f, 0.65f, 0.2f)   // 香橙金色
                : new Color(0.4f, 0.9f, 0.4f);  // 柚子绿色
        }

        /// <summary>更新头像边框颜色（阵营底色+tier混合）</summary>
        private void UpdateAvatarBorderColor()
        {
            if (_avatarImage == null || _avatarImage.material == null) return;

            Color campColor = camp == Camp.Left
                ? new Color(1f, 0.65f, 0.2f)   // 香橙金色
                : new Color(0.4f, 0.9f, 0.4f);  // 柚子绿色

            Color borderColor;
            float borderWidth;

            if (_currentTier >= 1)
            {
                // 礼物单位：阵营色与tier色混合（tier越高，tier色权重越大）
                Color tierColor = GetTierBorderColor(_currentTier);
                float tierWeight = Mathf.Clamp01(_currentTier / 6f); // tier1=0.17, tier6=1.0
                borderColor = Color.Lerp(campColor, tierColor, tierWeight * 0.7f);
                // 边框宽度随tier增大
                borderWidth = Mathf.Lerp(0.06f, 0.12f, tierWeight);
            }
            else
            {
                // 普通单位：纯阵营色边框
                borderColor = campColor;
                borderWidth = 0.06f;
            }

            // 设置材质实例的边框颜色和宽度
            if (_avatarImage.material != null)
            {
                _avatarImage.material.SetColor("_BorderColor", borderColor);
                _avatarImage.material.SetFloat("_BorderWidth", borderWidth);
            }
        }

        /// <summary>更新名字内容和样式</summary>
        private void UpdateNameContent(string playerName)
        {
            if (_nameText == null) return;

            // 礼物召唤单位(tier>=1)才显示名字，普通加入的不显示
            if (_currentTier >= 1 && !string.IsNullOrEmpty(playerName))
            {
                string displayName = playerName.Length > 6
                    ? playerName.Substring(0, 6)
                    : playerName;
                _nameText.text = displayName;
                _nameText.gameObject.SetActive(true);

                // 根据tier设置名字颜色和描边
                ApplyTierNameStyle(_currentTier);
            }
            else
            {
                // 普通单位不显示名字
                _nameText.text = "";
                _nameText.gameObject.SetActive(false);
            }
        }

        /// <summary>根据tier设置名字视觉样式（颜色+描边+字号）</summary>
        private void ApplyTierNameStyle(int tier)
        {
            if (_nameText == null) return;

            switch (tier)
            {
                case 1: // 仙女棒 - 白色朴素
                    _nameText.color = new Color(0.95f, 0.95f, 0.9f);
                    _nameText.outlineWidth = 0.2f;
                    _nameText.outlineColor = new Color32(40, 30, 20, 180);
                    _nameText.fontSize = 13;
                    break;
                case 2: // 能力药丸 - 浅蓝发光
                    _nameText.color = new Color(0.6f, 0.85f, 1f);
                    _nameText.outlineWidth = 0.22f;
                    _nameText.outlineColor = new Color32(10, 30, 80, 200);
                    _nameText.fontSize = 14;
                    break;
                case 3: // 甜甜圈 - 紫色
                    _nameText.color = new Color(0.8f, 0.6f, 1f);
                    _nameText.outlineWidth = 0.25f;
                    _nameText.outlineColor = new Color32(30, 10, 60, 210);
                    _nameText.fontSize = 15;
                    break;
                case 4: // 能量电池 - 金色
                    _nameText.color = new Color(1f, 0.85f, 0.3f);
                    _nameText.outlineWidth = 0.28f;
                    _nameText.outlineColor = new Color32(60, 30, 0, 220);
                    _nameText.fontSize = 16;
                    break;
                case 5: // 爱的爆炸 - 红金
                    _nameText.color = new Color(1f, 0.5f, 0.3f);
                    _nameText.outlineWidth = 0.3f;
                    _nameText.outlineColor = new Color32(80, 10, 0, 230);
                    _nameText.fontSize = 17;
                    break;
                case 6: // 神秘空投 - 彩虹渐变（用金色底）
                    _nameText.color = new Color(1f, 0.92f, 0.5f);
                    _nameText.outlineWidth = 0.35f;
                    _nameText.outlineColor = new Color32(100, 50, 0, 240);
                    _nameText.fontSize = 18;
                    break;
                default:
                    _nameText.color = Color.white;
                    _nameText.outlineWidth = 0.2f;
                    _nameText.outlineColor = new Color32(0, 0, 0, 160);
                    _nameText.fontSize = 13;
                    break;
            }
        }

        /// <summary>获取tier对应的边框颜色</summary>
        private static Color GetTierBorderColor(int tier)
        {
            switch (tier)
            {
                case 1: return new Color(0.9f, 0.9f, 0.85f);       // 白银
                case 2: return new Color(0.4f, 0.7f, 1f);           // 蓝色
                case 3: return new Color(0.7f, 0.4f, 1f);           // 紫色
                case 4: return new Color(1f, 0.84f, 0f);            // 金色
                case 5: return new Color(1f, 0.35f, 0.2f);          // 红金
                case 6: return new Color(1f, 0.92f, 0.5f);          // 传说金
                default: return new Color(0.8f, 0.8f, 0.8f);        // 灰
            }
        }

        /// <summary>应用圆形裁剪材质（每个头像独立材质实例）</summary>
        private void ApplyCircleMaskMaterial(RawImage image)
        {
            if (image == null) return;

            // 从 Resources 加载预设材质（Build时不会丢失）
            if (_circleMaskMat == null)
            {
                _circleMaskMat = Resources.Load<Material>("Materials/Mat_CircleMask");
                if (_circleMaskMat == null)
                {
                    // fallback: 尝试 Shader.Find
                    var shader = Shader.Find("UI/CircleMask");
                    if (shader != null)
                    {
                        _circleMaskMat = new Material(shader);
                        _circleMaskMat.SetFloat("_BorderWidth", 0.06f);
                        _circleMaskMat.SetFloat("_Softness", 0.02f);
                    }
                }
            }

            if (_circleMaskMat != null)
            {
                // 每个头像创建独立材质实例（边框颜色各不相同）
                image.material = new Material(_circleMaskMat);
            }
        }
    }
}
