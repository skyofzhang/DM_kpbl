using System;
using UnityEngine;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 橘子控制器 - 控制大橘子的位置和旋转，到边触发判负
    /// 带重量感的移动：缓慢加速、明显减速、最大速度限制
    /// </summary>
    public class OrangeController : MonoBehaviour
    {
        [Header("Position Config")]
        [SerializeField] private float positionRangeMin = -100f;
        [SerializeField] private float positionRangeMax = 100f;

        [Header("Weight Feel")]
        [Tooltip("SmoothDamp平滑时间，越大越重（1.5~2.5推荐）")]
        [SerializeField] private float smoothTime = 2.0f;
        [Tooltip("最大移动速度限制（即使碾压推力，橘子也不会飞过去）")]
        [SerializeField] private float maxSpeed = 2f;
        [Tooltip("加速度限制（每秒速度变化上限，越小起步越慢）")]
        [SerializeField] private float maxAcceleration = 1.5f;

        [Header("Server Follow")]
        [Tooltip("服务器位置跟随速度（服务器现在是速度驱动，客户端只需平滑跟随）")]
        [SerializeField] private float serverFollowSpeed = 4f;

        [Header("Spin (自转)")]
        [Tooltip("静止时基础自转速度（度/秒）")]
        [SerializeField] private float baseSpinSpeed = 20f;
        [Tooltip("移动时额外自转倍数")]
        [SerializeField] private float moveSpinMultiplier = 8f;

        [Header("Tilt (倾斜)")]
        [Tooltip("最大倾斜角度（度）")]
        [SerializeField] private float maxTiltAngle = 40f;
        [Tooltip("倾斜响应速度")]
        [SerializeField] private float tiltSmoothSpeed = 5f;
        [Tooltip("速度低于此值时不倾斜")]
        [SerializeField] private float tiltVelocityThreshold = 0.05f;

        [Header("Force Wobble (推力晃动)")]
        [Tooltip("启用推力晃动：双方有推力时橘子微微晃动（表现活力）")]
        [SerializeField] private bool enableForceWobble = true;
        [Tooltip("晃动最大幅度（米）— 固定值，不随推力线性增长")]
        [SerializeField] private float wobbleMaxAmplitude = 0.00007f;
        [Tooltip("晃动频率（Hz）")]
        [SerializeField] private float wobbleFrequency = 0.6f;
        [Tooltip("总推力低于此值时不晃动")]
        [SerializeField] private float wobbleForceThreshold = 50f;

        [SerializeField] private bool enableRotation = true;

        [Header("Idle Sway (静止微晃)")]
        [Tooltip("启用左右微晃（让画面生动）")]
        [SerializeField] private bool enableIdleSway = true;
        [Tooltip("微晃角度（度）")]
        [SerializeField] private float swayAngle = 5f;
        [Tooltip("微晃频率（Hz）")]
        [SerializeField] private float swayFrequency = 0.35f;

        [Header("Breathing Scale (呼吸缩放)")]
        [Tooltip("启用呼吸缩放效果")]
        [SerializeField] private bool enableBreathing = true;
        [Tooltip("呼吸缩放幅度（±比例）")]
        [SerializeField] private float breathScale = 0.03f;
        [Tooltip("呼吸频率（Hz）")]
        [SerializeField] private float breathFrequency = 0.5f;

        [Header("Reversal Frenzy (反推疯狂自转)")]
        [Tooltip("启用反推疯狂自转反馈")]
        [SerializeField] private bool enableReversalFrenzy = true;
        [Tooltip("反推疯狂自转速度（度/秒）")]
        [SerializeField] private float frenzySpinSpeed = 1080f;
        [Tooltip("反推疯狂自转持续时间（秒）")]
        [SerializeField] private float frenzyDuration = 2f;
        [Tooltip("速度方向反转的最小速度阈值（太慢不触发）")]
        [SerializeField] private float reversalSpeedThreshold = 0.01f;

        private float targetPosition = 0f;
        private float currentPosition = 0f;
        private float _velocity = 0f;
        private bool _gameActive = false;
        private float _currentTiltAngle = 0f;
        private float _spinAngle = 0f;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;

        // 服务器位置跟随
        private float _serverAuthorityPos = 0f; // 服务器权威位置
        private float _totalForce = 0f;         // 双方推力总和（用于判断是否有活动）

        // 推力晃动相关
        private float _wobblePhase = 0f;        // 晃动相位
        private float _wobbleOffset = 0f;       // 当前晃动偏移量

        // 微晃和呼吸相位
        private float _swayPhase = 0f;
        private float _breathPhase = 0f;

        // 反推疯狂自转
        private float _frenzyTimer = 0f;        // 疯狂自转剩余时间
        private float _prevMoveDir = 0f;         // 上一帧移动方向（用于检测反转）
        private float _smoothVelocity = 0f;      // 平滑后的速度（用于反转检测，避免抖动误触发）
        private float _prevServerPos = 0f;       // 上一次服务器位置（用于检测服务器方向反转）
        private float _serverMoveDir = 0f;       // 服务器位置移动方向

        /// <summary>
        /// 到边判负事件：参数为获胜方 "left" 或 "right"
        /// </summary>
        public event Action<string> OnReachedBoundary;

        private void Awake()
        {
            _baseRotation = transform.rotation;
            _baseScale = transform.localScale;
        }

        private float _prevVelocity = 0f; // 上一帧速度（用于加速度限制）

        private void Update()
        {
            // === 游戏未激活时：锁定在原位，不做任何位移 ===
            if (!_gameActive)
            {
                // 清零所有速度和晃动，防止开局瞬移
                _velocity = 0f;
                _prevVelocity = 0f;
                _wobbleOffset = Mathf.Lerp(_wobbleOffset, 0f, 5f * Time.deltaTime);
                targetPosition = currentPosition; // 目标锁定到当前位置
                // 仍然更新旋转（自转保持活力）
                UpdateRotation();
                return;
            }

            // === 直接跟随服务器位置（服务器已是速度驱动，位置平滑变化）===
            targetPosition = Mathf.Lerp(targetPosition, _serverAuthorityPos, serverFollowSpeed * Time.deltaTime);

            // === 推力晃动（固定幅度，不随推力增长）===
            if (enableForceWobble && _totalForce > wobbleForceThreshold)
            {
                _wobblePhase += wobbleFrequency * 2 * Mathf.PI * Time.deltaTime;
                // 晃动幅度固定为 wobbleMaxAmplitude，不与推力线性绑定
                float targetWobble = Mathf.Sin(_wobblePhase) * wobbleMaxAmplitude;
                _wobbleOffset = Mathf.Lerp(_wobbleOffset, targetWobble, 3f * Time.deltaTime);
            }
            else
            {
                _wobbleOffset = Mathf.Lerp(_wobbleOffset, 0f, 3f * Time.deltaTime);
            }

            // === 重量感移动：SmoothDamp + 加速度限制 ===
            currentPosition = Mathf.SmoothDamp(
                currentPosition, targetPosition, ref _velocity, smoothTime, maxSpeed);

            // 加速度限制：每帧速度变化不超过 maxAcceleration * deltaTime
            // 这让橘子起步很慢，减速也有惯性拖拽感
            float accel = (_velocity - _prevVelocity) / Mathf.Max(Time.deltaTime, 0.001f);
            if (Mathf.Abs(accel) > maxAcceleration)
            {
                _velocity = _prevVelocity + Mathf.Sign(accel) * maxAcceleration * Time.deltaTime;
                currentPosition = Mathf.MoveTowards(currentPosition, targetPosition, Mathf.Abs(_velocity) * Time.deltaTime);
            }
            _prevVelocity = _velocity;

            float finalPosX = currentPosition + _wobbleOffset;
            transform.position = new Vector3(finalPosX, transform.position.y, transform.position.z);

            UpdateRotation();

            // 到边检测
            if (_gameActive)
            {
                float threshold = (positionRangeMax - positionRangeMin) * 0.02f;

                if (currentPosition >= positionRangeMax - threshold)
                {
                    _gameActive = false;
                    OnReachedBoundary?.Invoke("left");
                }
                else if (currentPosition <= positionRangeMin + threshold)
                {
                    _gameActive = false;
                    OnReachedBoundary?.Invoke("right");
                }
            }
        }

        /// <summary>
        /// 更新位置 (0-1 范围映射到 min~max)
        /// </summary>
        public void UpdatePosition(float normalizedPosition)
        {
            _serverAuthorityPos = Mathf.Lerp(positionRangeMin, positionRangeMax, normalizedPosition);
        }

        /// <summary>
        /// 更新服务器权威位置
        /// </summary>
        public void UpdateServerPosition(float serverPosition)
        {
            _serverAuthorityPos = Mathf.Clamp(serverPosition, positionRangeMin, positionRangeMax);
        }

        /// <summary>
        /// 更新推力（用于晃动判断）
        /// </summary>
        public void UpdateForce(float leftForce, float rightForce)
        {
            _totalForce = leftForce + rightForce;
        }

        /// <summary>
        /// 直接设置位置
        /// </summary>
        public void SetPosition(float position)
        {
            targetPosition = Mathf.Clamp(position, positionRangeMin, positionRangeMax);
        }

        /// <summary>
        /// 重置到中心
        /// </summary>
        public void ResetPosition()
        {
            targetPosition = 0f;
            currentPosition = 0f;
            _velocity = 0f;
            _currentTiltAngle = 0f;
            _spinAngle = 0f;
            _serverAuthorityPos = 0f;
            _totalForce = 0f;
            _wobblePhase = 0f;
            _wobbleOffset = 0f;
            _swayPhase = 0f;
            _breathPhase = 0f;
            _frenzyTimer = 0f;
            _prevMoveDir = 0f;
            _smoothVelocity = 0f;
            _prevServerPos = 0f;
            _serverMoveDir = 0f;
            transform.position = new Vector3(0, transform.position.y, transform.position.z);
            transform.rotation = _baseRotation;
            transform.localScale = _baseScale;
        }

        /// <summary>旋转更新：自转 + 倾斜 + 微晃 + 呼吸缩放 + 反推疯狂自转（pivot已在球心，直接旋转即可）</summary>
        private void UpdateRotation()
        {
            if (!enableRotation) return;

            float speed = Mathf.Abs(_velocity);

            // === 反推检测：服务器位置方向反转时触发疯狂自转 ===
            // 使用服务器位置差值检测（比客户端smoothed velocity更可靠）
            if (enableReversalFrenzy && _gameActive)
            {
                float serverDelta = _serverAuthorityPos - _prevServerPos;

                // 只在有明显移动时检测方向（忽略微小抖动）
                if (Mathf.Abs(serverDelta) > reversalSpeedThreshold)
                {
                    float newDir = Mathf.Sign(serverDelta);

                    // 方向反转 → 触发疯狂自转
                    if (_serverMoveDir != 0f && newDir != _serverMoveDir)
                    {
                        _frenzyTimer = frenzyDuration;
                    }

                    _serverMoveDir = newDir;
                }

                _prevServerPos = _serverAuthorityPos;
            }

            // 疯狂自转计时递减
            if (_frenzyTimer > 0f)
                _frenzyTimer -= Time.deltaTime;

            // 1. 自转：始终旋转，移动时更快；反推时疯狂加速
            float spinSpeed = baseSpinSpeed + speed * moveSpinMultiplier;
            if (_frenzyTimer > 0f)
            {
                // 疯狂自转：从peak速度平滑衰减到正常速度
                float frenzyFactor = Mathf.Clamp01(_frenzyTimer / frenzyDuration);
                // 用缓出曲线让前半段更疯狂，后半段自然回归
                frenzyFactor = frenzyFactor * frenzyFactor; // ease-in for deceleration
                spinSpeed += frenzySpinSpeed * frenzyFactor;
            }
            float spinDir = _velocity >= 0 ? -1f : 1f;
            _spinAngle += spinSpeed * spinDir * Time.deltaTime;

            // 2. 倾斜：移动时向移动方向倾斜（增强响应速度）
            float targetTilt = 0f;
            if (speed > tiltVelocityThreshold)
            {
                float tiltAmount = Mathf.Clamp01(speed / maxSpeed) * maxTiltAngle;
                targetTilt = _velocity > 0 ? tiltAmount : -tiltAmount;
            }
            _currentTiltAngle = Mathf.Lerp(_currentTiltAngle, targetTilt, Time.deltaTime * tiltSmoothSpeed);

            // 3. 左右微晃：缓慢的Z轴摆动（让静止时也有生命感）
            float swayZ = 0f;
            if (enableIdleSway)
            {
                _swayPhase += swayFrequency * 2f * Mathf.PI * Time.deltaTime;
                // 移动越快微晃越小（移动时倾斜已经提供了动感）
                float swayDampen = 1f - Mathf.Clamp01(speed / (maxSpeed * 0.3f));
                swayZ = Mathf.Sin(_swayPhase) * swayAngle * swayDampen;
            }

            // 4. 组合旋转：FBX pivot已在球心，直接旋转不会偏移
            Quaternion spin = Quaternion.Euler(0f, _spinAngle, 0f);
            Quaternion tilt = Quaternion.Euler(0f, 0f, _currentTiltAngle + swayZ);
            transform.rotation = _baseRotation * tilt * spin;

            // 5. 呼吸缩放：微妙的大小律动
            if (enableBreathing)
            {
                _breathPhase += breathFrequency * 2f * Mathf.PI * Time.deltaTime;
                float breathFactor = 1f + Mathf.Sin(_breathPhase) * breathScale;
                transform.localScale = _baseScale * breathFactor;
            }
        }

        public void SetGameActive(bool active)
        {
            _gameActive = active;
            if (active)
            {
                // 激活时重置速度，防止累积的位置差导致瞬移
                _velocity = 0f;
                _prevVelocity = 0f;
            }
        }

        public bool IsGameActive => _gameActive;
        public float GetCurrentPosition() => currentPosition;
        public float GetVelocity() => _velocity;
        public float GetNormalizedPosition() => Mathf.InverseLerp(positionRangeMin, positionRangeMax, currentPosition);
        public float PositionRangeMin => positionRangeMin;
        public float PositionRangeMax => positionRangeMax;
    }
}
