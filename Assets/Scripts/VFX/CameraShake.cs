using UnityEngine;

namespace CapybaraDuel.VFX
{
    /// <summary>
    /// 摄像机震动 - 大额礼物/胜负时刻
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        private Vector3 originalPosition;
        private float shakeDuration = 0f;
        private float shakeIntensity = 0f;
        private float decreaseFactor = 1.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            originalPosition = transform.localPosition;
        }

        private void Update()
        {
            if (shakeDuration > 0)
            {
                transform.localPosition = originalPosition +
                    Random.insideUnitSphere * shakeIntensity;

                shakeDuration -= Time.deltaTime * decreaseFactor;

                if (shakeDuration <= 0)
                {
                    shakeDuration = 0f;
                    transform.localPosition = originalPosition;
                }
            }
        }

        /// <summary>
        /// 触发摄像机震动
        /// </summary>
        /// <param name="duration">持续时间(秒)</param>
        /// <param name="intensity">震动强度</param>
        public void Shake(float duration = 0.3f, float intensity = 0.2f)
        {
            originalPosition = transform.localPosition;
            shakeDuration = duration;
            shakeIntensity = intensity;
        }

        /// <summary>
        /// 小震动（小额礼物）
        /// </summary>
        public void ShakeSmall() => Shake(0.15f, 0.05f);

        /// <summary>
        /// 中震动（大额礼物）
        /// </summary>
        public void ShakeMedium() => Shake(0.3f, 0.15f);

        /// <summary>
        /// 大震动（传说礼物/胜负）
        /// </summary>
        public void ShakeHeavy() => Shake(0.5f, 0.3f);
    }
}
