using UnityEngine;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 橘子控制器 - 控制大橘子的位置和旋转
    /// </summary>
    public class OrangeController : MonoBehaviour
    {
        [Header("Position Config")]
        [SerializeField] private float positionRangeMin = -10f;
        [SerializeField] private float positionRangeMax = 10f;
        [SerializeField] private float moveSmooth = 5f;

        [Header("Rotation Config")]
        [SerializeField] private float rotationSpeed = 100f;
        [SerializeField] private bool enableRotation = true;

        private float targetPosition = 0f;
        private float currentPosition = 0f;

        private void Update()
        {
            // 平滑移动
            currentPosition = Mathf.Lerp(currentPosition, targetPosition, moveSmooth * Time.deltaTime);
            transform.position = new Vector3(currentPosition, transform.position.y, transform.position.z);

            // 旋转效果
            if (enableRotation)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// 更新位置 (0-1 范围映射到 min~max)
        /// </summary>
        public void UpdatePosition(float normalizedPosition)
        {
            targetPosition = Mathf.Lerp(positionRangeMin, positionRangeMax, normalizedPosition);
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
            transform.position = new Vector3(0, transform.position.y, transform.position.z);
        }

        public float GetCurrentPosition() => currentPosition;
        public float GetNormalizedPosition() => Mathf.InverseLerp(positionRangeMin, positionRangeMax, currentPosition);
    }
}
