using UnityEngine;
using CapybaraDuel.Entity;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 第三人称俯视角摄像机 - 跟随橘子 X 轴移动
    /// 挂载在 Main Camera 上，与 CameraShake（挂在子对象上）不冲突
    /// 玩家越多摄像机越高，看到更大的战场
    /// </summary>
    public class OrangeFollowCamera : MonoBehaviour
    {
        [Header("Target")]
        public Transform target; // 橘子 Transform

        [Header("Camera Config")]
        [SerializeField] private float cameraHeight = 16.5f;   // Y 基础高度（2026-02-13微调+1.5）
        [SerializeField] private float cameraDistance = 14f;    // Z 方向后退距离
        [SerializeField] private float pitchAngle = 50f;       // 俯视角度
        [SerializeField] private float followSmooth = 5f;      // 跟随平滑度

        [Header("Follow Axis")]
        [SerializeField] private bool followX = true;          // 跟随 X 轴（橘子左右移动）
        [SerializeField] private bool followZ = false;         // 跟随 Z 轴（橘子不动 Z，默认关闭）

        [Header("Dynamic Height (玩家越多镜头越高)")]
        [Tooltip("启用动态高度")]
        [SerializeField] private bool enableDynamicHeight = true;
        [Tooltip("每个单位增加的高度")]
        [SerializeField] private float heightPerUnit = 0.04f;
        [Tooltip("最大额外高度（200人/边 × 约3~5单位/人 ≈ 800~2000单位）")]
        [SerializeField] private float maxExtraHeight = 35f;
        [Tooltip("高度变化平滑速度")]
        [SerializeField] private float heightSmooth = 2f;

        [Header("Mouse Wheel Zoom (鼠标滚轮缩放)")]
        [Tooltip("启用鼠标滚轮缩放")]
        [SerializeField] private bool enableMouseWheelZoom = true;
        [Tooltip("每滚一档的高度变化量")]
        [SerializeField] private float zoomSpeed = 3f;
        [Tooltip("滚轮缩放最小高度")]
        [SerializeField] private float minZoomHeight = 8f;
        [Tooltip("滚轮缩放最大高度")]
        [SerializeField] private float maxZoomHeight = 25f;
        [Tooltip("缩放平滑速度")]
        [SerializeField] private float zoomSmooth = 5f;

        private Vector3 _initialPosition;
        private float _baseHeight;
        private float _currentExtraHeight = 0f;
        private float _manualZoomOffset = 0f;  // 玩家手动滚轮调整的高度偏移
        private float _currentZoomOffset = 0f; // 当前平滑值

        private void Start()
        {
            // 记录初始位置（由 SceneGenerator 设定）
            _initialPosition = transform.position;
            _baseHeight = _initialPosition.y;

            // 设置初始旋转为指定俯视角
            transform.rotation = Quaternion.Euler(pitchAngle, 0, 0);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // === 鼠标滚轮缩放 ===
            if (enableMouseWheelZoom)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    // 滚轮向上(正值)=拉近(高度减)，向下(负值)=拉远(高度增)
                    _manualZoomOffset -= scroll * zoomSpeed * 10f;
                }
                // 平滑过渡
                _currentZoomOffset = Mathf.Lerp(_currentZoomOffset, _manualZoomOffset, zoomSmooth * Time.deltaTime);
            }

            // === 动态高度：根据场上单位总数调整 ===
            if (enableDynamicHeight)
            {
                int totalUnits = Capybara.LeftAlive.Count + Capybara.RightAlive.Count;
                float targetExtra = Mathf.Clamp(totalUnits * heightPerUnit, 0f, maxExtraHeight);
                _currentExtraHeight = Mathf.Lerp(_currentExtraHeight, targetExtra, heightSmooth * Time.deltaTime);
            }

            Vector3 targetPos = _initialPosition;
            // 最终高度 = 基础高度 + 单位动态高度 + 玩家滚轮偏移
            float totalHeight = _baseHeight + _currentExtraHeight + _currentZoomOffset;
            // 限制在允许范围内
            totalHeight = Mathf.Clamp(totalHeight, minZoomHeight, maxZoomHeight);
            targetPos.y = totalHeight;

            // 跟随橘子的 X 轴位移
            if (followX)
            {
                targetPos.x = target.position.x;
            }

            // 跟随橘子的 Z 轴位移（一般关闭，橘子只在 X 轴移动）
            if (followZ)
            {
                targetPos.z = target.position.z - cameraDistance;
            }

            // 同步调整 Z 后退距离：保持俯视角看点始终在橘子位置
            // 数学原理：相机pitch=50°时，地面看点距离 = height / tan(50°)
            // 当高度增加Δh时，Z需后退 Δh / tan(50°) ≈ Δh × 0.84
            float extraHeight = totalHeight - _baseHeight;
            if (Mathf.Abs(extraHeight) > 0.1f)
            {
                float tanPitch = Mathf.Tan(pitchAngle * Mathf.Deg2Rad);
                float zOffset = (tanPitch > 0.1f) ? extraHeight / tanPitch : extraHeight * 0.84f;
                targetPos.z = _initialPosition.z - zOffset;
            }

            // 平滑跟随
            transform.position = Vector3.Lerp(transform.position, targetPos, followSmooth * Time.deltaTime);
        }

        /// <summary>
        /// 运行时重新设定摄像机位置参数
        /// </summary>
        public void SetCameraParams(float height, float distance, float pitch)
        {
            cameraHeight = height;
            cameraDistance = distance;
            pitchAngle = pitch;

            // 重新计算位置
            _initialPosition = new Vector3(0, cameraHeight, -cameraDistance);
            _baseHeight = cameraHeight;
            transform.position = _initialPosition;
            transform.rotation = Quaternion.Euler(pitchAngle, 0, 0);
        }
    }
}
