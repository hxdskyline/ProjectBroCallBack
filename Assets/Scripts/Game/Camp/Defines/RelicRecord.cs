using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 圣物持久化记录 — 存储在 PlayerData.ownedRelics
    /// </summary>
    [Serializable]
    public class RelicRecord
    {
        public string relicId;
        public string name;
        public string description;
        public string mechanismTag;
        public int rarity;
        public bool isBossRelic;
        public List<BuffEffectItem> effects;
    }
}
