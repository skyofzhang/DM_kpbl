using UnityEngine;
using System.Collections.Generic;

namespace CapybaraDuel.VFX
{
    /// <summary>
    /// 特效管理器 - 对象池 + 自动回收
    /// </summary>
    public class VFXSpawner : MonoBehaviour
    {
        public static VFXSpawner Instance { get; private set; }

        [Header("VFX Prefabs")]
        public GameObject spawnVFX;
        public GameObject despawnVFX;
        public GameObject giftSmallVFX;
        public GameObject giftBigVFX;
        public GameObject giftLegendVFX;
        public GameObject victoryVFX;

        [Header("Config")]
        [SerializeField] private int poolSizePerType = 10;

        private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// 在指定位置播放特效
        /// </summary>
        public void PlayVFX(string vfxType, Vector3 position, float autoDestroyTime = 2f)
        {
            GameObject prefab = GetPrefab(vfxType);
            if (prefab == null) return;

            var instance = GetFromPool(vfxType, prefab);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            instance.SetActive(true);

            // 重新播放粒子
            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear();
                ps.Play();
            }

            // 自动回收
            StartCoroutine(ReturnAfterDelay(vfxType, instance, autoDestroyTime));
        }

        /// <summary>
        /// 播放单位生成特效
        /// </summary>
        public void PlaySpawnVFX(Vector3 position)
        {
            PlayVFX("spawn", position, 1f);
        }

        /// <summary>
        /// 播放单位消亡特效
        /// </summary>
        public void PlayDespawnVFX(Vector3 position)
        {
            PlayVFX("despawn", position, 1.5f);
        }

        /// <summary>
        /// 播放礼物特效（根据金额自动选择级别）
        /// </summary>
        public void PlayGiftVFX(Vector3 position, float giftValue)
        {
            if (giftValue >= 199)
                PlayVFX("gift_legend", position, 3f);
            else if (giftValue >= 52)
                PlayVFX("gift_big", position, 2f);
            else
                PlayVFX("gift_small", position, 1.5f);
        }

        /// <summary>
        /// 播放胜利特效
        /// </summary>
        public void PlayVictoryVFX(Vector3 position)
        {
            PlayVFX("victory", position, 4f);
        }

        private GameObject GetPrefab(string vfxType)
        {
            switch (vfxType)
            {
                case "spawn": return spawnVFX;
                case "despawn": return despawnVFX;
                case "gift_small": return giftSmallVFX;
                case "gift_big": return giftBigVFX;
                case "gift_legend": return giftLegendVFX;
                case "victory": return victoryVFX;
                default: return null;
            }
        }

        private GameObject GetFromPool(string type, GameObject prefab)
        {
            if (!pools.ContainsKey(type))
                pools[type] = new Queue<GameObject>();

            var pool = pools[type];
            if (pool.Count > 0)
            {
                var obj = pool.Dequeue();
                if (obj != null) return obj;
            }

            // 创建新的
            var instance = Instantiate(prefab, transform);
            instance.SetActive(false);
            return instance;
        }

        private System.Collections.IEnumerator ReturnAfterDelay(string type, GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
            {
                obj.SetActive(false);
                if (!pools.ContainsKey(type))
                    pools[type] = new Queue<GameObject>();
                pools[type].Enqueue(obj);
            }
        }
    }
}
