using UnityEngine;
using System;

namespace CapybaraDuel.Config
{
    /// <summary>
    /// 角色配置数据库 - ScriptableObject
    /// 定义所有单位类型及其 Prefab 映射
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "CapybaraDuel/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [Header("基础单位")]
        public CharacterEntry[] basicUnits;

        [Header("召唤单位")]
        public CharacterEntry[] summonUnits;

        /// <summary>
        /// 根据 unitId 查找 Prefab
        /// </summary>
        public GameObject GetPrefab(string unitId)
        {
            // 先搜索基础单位
            if (basicUnits != null)
            {
                foreach (var entry in basicUnits)
                {
                    if (entry.unitId == unitId)
                        return entry.prefab;
                }
            }

            // 再搜索召唤单位
            if (summonUnits != null)
            {
                foreach (var entry in summonUnits)
                {
                    if (entry.unitId == unitId)
                        return entry.prefab;
                }
            }

            // 未找到，返回基础单位的 Prefab 作为 fallback
            if (basicUnits != null && basicUnits.Length > 0)
            {
                Debug.LogWarning($"[CharDB] unitId '{unitId}' not found, using fallback");
                return basicUnits[0].prefab;
            }

            return null;
        }

        /// <summary>
        /// 获取单位配置
        /// </summary>
        public CharacterEntry GetEntry(string unitId)
        {
            if (basicUnits != null)
                foreach (var e in basicUnits)
                    if (e.unitId == unitId) return e;

            if (summonUnits != null)
                foreach (var e in summonUnits)
                    if (e.unitId == unitId) return e;

            return null;
        }
    }

    [Serializable]
    public class CharacterEntry
    {
        [Tooltip("单位ID，如 201_Capybara, 3111_RunCapy")]
        public string unitId;

        [Tooltip("显示名称")]
        public string displayName;

        [Tooltip("单位 Prefab")]
        public GameObject prefab;

        [Tooltip("基础推力")]
        public float baseForce = 10f;

        [Tooltip("存活时间(秒)，0=永久")]
        public float lifetime = 0f;

        [Tooltip("单位等级 0=基础, 1=普通召唤, 2=升级召唤, 3=传说")]
        [Range(0, 3)]
        public int tier = 0;

        [Tooltip("缩放倍数")]
        public float scale = 1f;
    }
}
