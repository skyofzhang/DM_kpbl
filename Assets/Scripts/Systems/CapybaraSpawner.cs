using UnityEngine;
using System.Collections.Generic;
using CapybaraDuel.Entity;
using CapybaraDuel.VFX;

namespace CapybaraDuel.Systems
{
    /// <summary>
    /// 水豚生成器 - 多Prefab对象池 + 召唤逻辑
    ///
    /// 池结构: Dictionary&lt;int, Queue&lt;GameObject&gt;&gt;
    ///   key=0: 默认池（tier0/1 基础卡皮巴拉 + 无专属Prefab的tier）
    ///   key=2~6: 各tier礼物召唤单位专属池
    /// </summary>
    public class CapybaraSpawner : MonoBehaviour
    {
        [Header("Default Prefab (Tier 0/1 基础卡皮巴拉)")]
        public GameObject capybaraPrefab;

        [Header("Tier Prefabs (null = fallback to default)")]
        [Tooltip("Tier2: 能力药丸 (10币)")]
        public GameObject tier2Prefab;
        [Tooltip("Tier3: 甜甜圈 (52币)")]
        public GameObject tier3Prefab;
        [Tooltip("Tier4: 能量电池 (99币)")]
        public GameObject tier4Prefab;
        [Tooltip("Tier5: 爱的爆炸 (199币)")]
        public GameObject tier5Prefab;
        [Tooltip("Tier6: 神秘空投 (520币)")]
        public GameObject tier6Prefab;

        [Header("Config")]
        [SerializeField] private int maxCount = 400; // 每阵营最多200，总计400
        [SerializeField] private int preloadCount = 30; // 默认池预加载数量
        [SerializeField] private int tierPreloadCount = 5; // 每个tier池预加载数量
        [SerializeField] private float defaultMoveSpeed = 2f;

        [Header("Spawn Points")]
        public Transform leftSpawnPoint;
        public Transform rightSpawnPoint;
        public Transform orangeTarget; // 橘子位置，所有单位向这里移动
        [SerializeField] private float spawnRangeX = 4f;
        [SerializeField] private float spawnRangeZ = 5f;
        [Tooltip("随机从边缘入场（上/下/侧面），而不是固定从一个点")]
        [SerializeField] private bool randomEntryDirection = true;
        [Tooltip("角色整体Y偏移（地板高度补偿，防穿模）")]
        [SerializeField] private float characterYOffset = 0.2f;

        // 多Prefab池: key=poolTier (0=默认, 2~6=各tier专属)
        private Dictionary<int, Queue<GameObject>> pools = new Dictionary<int, Queue<GameObject>>();
        private List<GameObject> activeCapybaras = new List<GameObject>();

        private void Start()
        {
            PreloadPools();
        }

        /// <summary>预加载所有池</summary>
        private void PreloadPools()
        {
            // 默认池 (tier 0)
            PreloadSinglePool(0, capybaraPrefab, preloadCount);

            // 各tier专属池
            PreloadSinglePool(2, tier2Prefab, tierPreloadCount);
            PreloadSinglePool(3, tier3Prefab, tierPreloadCount);
            PreloadSinglePool(4, tier4Prefab, tierPreloadCount);
            PreloadSinglePool(5, tier5Prefab, tierPreloadCount);
            PreloadSinglePool(6, tier6Prefab, tierPreloadCount);
        }

        private void PreloadSinglePool(int poolTier, GameObject prefab, int count)
        {
            if (prefab == null)
            {
                if (poolTier == 0)
                    Debug.LogWarning("[Spawner] Default prefab not assigned — will use runtime cube");
                return; // tier专属prefab为null时不预加载（运行时fallback到默认池）
            }

            if (!pools.ContainsKey(poolTier))
                pools[poolTier] = new Queue<GameObject>();

            for (int i = 0; i < count; i++)
            {
                var obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                pools[poolTier].Enqueue(obj);
            }
        }

        /// <summary>根据tier获取对应的Prefab（有专属返回专属，否则fallback到默认）</summary>
        private GameObject GetPrefabForTier(int tier)
        {
            switch (tier)
            {
                case 2: return tier2Prefab != null ? tier2Prefab : capybaraPrefab;
                case 3: return tier3Prefab != null ? tier3Prefab : capybaraPrefab;
                case 4: return tier4Prefab != null ? tier4Prefab : capybaraPrefab;
                case 5: return tier5Prefab != null ? tier5Prefab : capybaraPrefab;
                case 6: return tier6Prefab != null ? tier6Prefab : capybaraPrefab;
                default: return capybaraPrefab;
            }
        }

        /// <summary>根据tier确定应该使用哪个poolTier（有专属Prefab用tier号，否则用0）</summary>
        private int GetPoolTier(int tier)
        {
            switch (tier)
            {
                case 2: return tier2Prefab != null ? 2 : 0;
                case 3: return tier3Prefab != null ? 3 : 0;
                case 4: return tier4Prefab != null ? 4 : 0;
                case 5: return tier5Prefab != null ? 5 : 0;
                case 6: return tier6Prefab != null ? 6 : 0;
                default: return 0;
            }
        }

        /// <summary>从指定池取出对象</summary>
        private GameObject GetFromPool(int poolTier)
        {
            // 先尝试从对应池取
            if (pools.ContainsKey(poolTier) && pools[poolTier].Count > 0)
                return pools[poolTier].Dequeue();

            // 池耗尽，根据poolTier动态创建
            var prefab = (poolTier == 0) ? capybaraPrefab : GetPrefabForTier(poolTier);
            if (prefab != null)
                return Instantiate(prefab, transform);

            // 无 prefab 时创建简易方块占位
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(transform);
            cube.transform.localScale = Vector3.one * 0.5f;
            cube.AddComponent<Capybara>();
            return cube;
        }

        /// <summary>归还对象到指定池</summary>
        private void ReturnToPool(GameObject obj, int poolTier)
        {
            obj.SetActive(false);
            if (!pools.ContainsKey(poolTier))
                pools[poolTier] = new Queue<GameObject>();
            pools[poolTier].Enqueue(obj);
        }

        /// <summary>
        /// 生成单个水豚（由GiftHandler调用）
        /// </summary>
        public void SpawnCapybara(Camp camp, string unitId, float force, float lifetime,
            string playerId = null, string playerName = null, string avatarUrl = null,
            string tier = "0")
        {
            if (activeCapybaras.Count >= maxCount)
            {
                Debug.LogWarning("[Spawner] Max count reached");
                return;
            }

            var spawnPoint = camp == Camp.Left ? leftSpawnPoint : rightSpawnPoint;
            if (spawnPoint == null) return;

            // 根据 tier 从正确的池取对象
            int tierInt = UnitVisualEffect.ParseTier(tier);
            int poolTier = GetPoolTier(tierInt);
            var go = GetFromPool(poolTier);
            if (go == null) return;

            Vector3 spawnPos;
            if (randomEntryDirection)
            {
                spawnPos = GetRandomEntryPosition(camp, spawnPoint.position);
            }
            else
            {
                float xOffset = Random.Range(0f, spawnRangeX);
                if (camp == Camp.Left) xOffset = -xOffset;
                float zOffset = Random.Range(-spawnRangeZ, spawnRangeZ);
                spawnPos = spawnPoint.position + new Vector3(xOffset, 0, zOffset);
            }
            // 地板高度补偿：所有角色整体抬高，防止穿模
            spawnPos.y += characterYOffset;
            go.transform.position = spawnPos;
            go.SetActive(true);

            var capy = go.GetComponent<Capybara>();
            if (capy != null)
            {
                capy.Initialize(unitId, camp, force, lifetime, orangeTarget, tier);
                capy.OnDespawned += HandleDespawn;
                capy.PoolTier = poolTier; // 标记来源池，Despawn时归还

                if (!string.IsNullOrEmpty(playerId))
                    capy.SetPlayerInfo(playerId, playerName, avatarUrl);
            }

            activeCapybaras.Add(go);
        }

        /// <summary>
        /// 生成一批基础单位（玩家加入时）
        /// </summary>
        public void SpawnBasicUnit(Camp camp, string playerId = null, string playerName = null, string avatarUrl = null)
        {
            SpawnCapybara(camp, "201_Sheep", 10f, 0f, playerId, playerName, avatarUrl); // lifetime=0 表示永不消失
        }

        private void HandleDespawn(Capybara capy)
        {
            var go = capy.gameObject;
            activeCapybaras.Remove(go);
            int poolTier = capy.PoolTier;
            capy.PoolTier = 0; // 重置
            ReturnToPool(go, poolTier);
        }

        public void ClearAllCapybaras()
        {
            foreach (var go in activeCapybaras)
            {
                var capy = go.GetComponent<Capybara>();
                if (capy != null)
                {
                    capy.OnDespawned -= HandleDespawn;
                    int poolTier = capy.PoolTier;
                    capy.PoolTier = 0;
                    ReturnToPool(go, poolTier);
                }
                else
                {
                    ReturnToPool(go, 0);
                }
            }
            activeCapybaras.Clear();
        }

        public int GetCapybaraCount() => activeCapybaras.Count;

        /// <summary>
        /// 根据 playerId 找到该玩家的基础单位，应用升级视觉
        /// </summary>
        public void ApplyUpgradeToPlayer(string playerId, int newLevel)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            foreach (var go in activeCapybaras)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var capy = go.GetComponent<Capybara>();
                if (capy != null && capy.PlayerId == playerId)
                {
                    capy.ApplyUpgradeLevel(newLevel);
                    return; // 每个玩家只有一个基础单位
                }
            }
        }

        /// <summary>
        /// 随机入场位置：从阵营后方、上方、下方随机选一个方向入场
        /// </summary>
        private Vector3 GetRandomEntryPosition(Camp camp, Vector3 basePos)
        {
            float edge = Random.Range(0, 3); // 0=后方, 1=上方(+Z), 2=下方(-Z)
            float x, z;

            if (edge < 1)
            {
                // 从阵营后方入场（传统方式，但加大随机Z范围）
                x = basePos.x + Random.Range(0f, spawnRangeX) * (camp == Camp.Left ? -1f : 1f);
                z = Random.Range(-spawnRangeZ, spawnRangeZ);
            }
            else if (edge < 2)
            {
                // 从上方(+Z)入场
                x = basePos.x + Random.Range(-spawnRangeX * 0.5f, spawnRangeX) * (camp == Camp.Left ? -1f : 1f);
                z = spawnRangeZ + Random.Range(1f, 3f);
            }
            else
            {
                // 从下方(-Z)入场
                x = basePos.x + Random.Range(-spawnRangeX * 0.5f, spawnRangeX) * (camp == Camp.Left ? -1f : 1f);
                z = -spawnRangeZ - Random.Range(1f, 3f);
            }

            return new Vector3(x, basePos.y + characterYOffset, z);
        }
    }
}
