using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 稀有度生成条目
    /// </summary>
    [Serializable]
    public class RaritySpawnEntry
    {
        public int rarity;           // Rarity 枚举值
        public float spawnRate;      // 出现概率
        public float bornEnhanceRate; // 天生强化概率
    }

    /// <summary>
    /// 区域稀有度配置
    /// </summary>
    [Serializable]
    public class RegionRarityConfig
    {
        public int regionId;
        public List<RaritySpawnEntry> rates;
    }

    /// <summary>
    /// 稀有度生成配置 — 对应 rarity_spawn_config.json
    /// </summary>
    [Serializable]
    public class RaritySpawnConfig
    {
        public List<RegionRarityConfig> regions;
    }
}
