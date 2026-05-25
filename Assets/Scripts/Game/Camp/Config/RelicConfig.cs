using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 圣物配置 — 对应 relic_config.json 中的单个圣物条目
    /// </summary>
    [Serializable]
    public class RelicConfig
    {
        public string relicId;
        public string name;
        public string description;
        public int rarity;            // 0=普通, 1=高级, 2=稀有, 3=Boss
        public string mechanismTag;   // 增强的机制标签
        public List<BuffEffectItem> effects;
        public bool isBossRelic;
    }
}
