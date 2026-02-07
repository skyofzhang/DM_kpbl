using UnityEngine;
using System.Collections.Generic;
using CapybaraDuel.Entity;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 水豚生成器 - 管理水豚单位的生成和对象池
    /// </summary>
    public class CapybaraSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject capybaraPrefab;

        [Header("Config")]
        [SerializeField] private int maxCount = 100;
        [SerializeField] private int preloadCount = 20;
        [SerializeField] private float defaultMoveSpeed = 2f;

        [Header("Spawn Points")]
        [SerializeField] private Transform leftSpawnPoint;
        [SerializeField] private Transform rightSpawnPoint;
        [SerializeField] private float spawnRangeX = 2f;
        [SerializeField] private float spawnRangeZ = 2f;

        private Queue<GameObject> pool = new Queue<GameObject>();
        private List<GameObject> activeCapybaras = new List<GameObject>();

        private void Start()
        {
            PreloadPool();
        }

        private void PreloadPool()
        {
            if (capybaraPrefab == null)
            {
                Debug.LogWarning("[CapybaraSpawner] Capybara prefab not assigned");
                return;
            }

            for (int i = 0; i < preloadCount; i++)
            {
                var obj = Instantiate(capybaraPrefab, transform);
                obj.SetActive(false);
                pool.Enqueue(obj);
            }

            Debug.Log($"[CapybaraSpawner] Preloaded {preloadCount} capybaras");
        }

        /// <summary>
        /// 生成水豚
        /// </summary>
        public void SpawnCapybaras(Camp camp, int count, string giftName)
        {
            if (activeCapybaras.Count + count > maxCount)
            {
                count = maxCount - activeCapybaras.Count;
                Debug.LogWarning($"[CapybaraSpawner] Max count reached, spawning only {count}");
            }

            var spawnPoint = camp == Camp.Left ? leftSpawnPoint : rightSpawnPoint;
            if (spawnPoint == null)
            {
                Debug.LogError($"[CapybaraSpawner] Spawn point for {camp} not assigned");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var capybara = GetFromPool();
                if (capybara != null)
                {
                    var offset = new Vector3(
                        Random.Range(-spawnRangeX, spawnRangeX),
                        0,
                        Random.Range(-spawnRangeZ, spawnRangeZ)
                    );
                    capybara.transform.position = spawnPoint.position + offset;
                    capybara.SetActive(true);
                    activeCapybaras.Add(capybara);
                }
            }

            Debug.Log($"[CapybaraSpawner] Spawned {count} capybaras for {camp} ({giftName})");
        }

        private GameObject GetFromPool()
        {
            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }

            if (capybaraPrefab != null)
            {
                return Instantiate(capybaraPrefab, transform);
            }

            return null;
        }

        private void ReturnToPool(GameObject obj)
        {
            obj.SetActive(false);
            pool.Enqueue(obj);
        }

        /// <summary>
        /// 清除所有水豚
        /// </summary>
        public void ClearAllCapybaras()
        {
            foreach (var capybara in activeCapybaras)
            {
                ReturnToPool(capybara);
            }
            activeCapybaras.Clear();
            Debug.Log("[CapybaraSpawner] Cleared all capybaras");
        }

        public int GetCapybaraCount() => activeCapybaras.Count;
    }
}
