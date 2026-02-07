using UnityEngine;

namespace CapybaraDuel.Entity
{
    /// <summary>
    /// 水豚单位
    /// </summary>
    public class Capybara : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string unitId;
        [SerializeField] private float force = 10f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float lifetime = 30f;

        private Camp camp;
        private Transform target;
        private float spawnTime;

        public string UnitId => unitId;
        public float Force => force;
        public Camp Camp => camp;

        public void Initialize(string id, Camp camp, float force, float lifetime, Transform target)
        {
            this.unitId = id;
            this.camp = camp;
            this.force = force;
            this.lifetime = lifetime;
            this.target = target;
            this.spawnTime = Time.time;
        }

        private void Update()
        {
            // 检查生命周期
            if (lifetime > 0 && Time.time - spawnTime > lifetime)
            {
                Despawn();
                return;
            }

            // 向目标移动
            if (target != null)
            {
                var direction = (target.position - transform.position).normalized;
                direction.y = 0;
                transform.position += direction * moveSpeed * Time.deltaTime;

                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

        private void Despawn()
        {
            // TODO: 播放消失特效
            gameObject.SetActive(false);
        }
    }
}
