using System;
using UnityEngine;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 推力系统 - 管理双方推力数据和进度条
    /// </summary>
    public class ForceSystem : MonoBehaviour
    {
        public float LeftForce { get; private set; }
        public float RightForce { get; private set; }
        public float OrangePos { get; private set; }

        /// <summary> 进度条归一化值 0=中间, -1=最左, 1=最右 </summary>
        public float NormalizedProgress => Mathf.Clamp(OrangePos / 100f, -1f, 1f);

        public event Action<float, float, float> OnForceUpdated; // leftForce, rightForce, orangePos

        public void UpdateForce(float leftForce, float rightForce, float orangePos)
        {
            LeftForce = leftForce;
            RightForce = rightForce;
            OrangePos = orangePos;
            OnForceUpdated?.Invoke(leftForce, rightForce, orangePos);
        }

        public void Reset()
        {
            LeftForce = 0;
            RightForce = 0;
            OrangePos = 0;
            OnForceUpdated?.Invoke(0, 0, 0);
        }
    }
}
