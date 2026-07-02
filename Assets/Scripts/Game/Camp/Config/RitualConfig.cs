using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 命运/祈福档次配置
    /// </summary>
    [System.Serializable]
    public class RitualTierData
    {
        public string tierName;     // "free" / "low" / "high"
        public string displayName;  // "免费祈愿" / "普通祈愿" / "盛大祈愿"
        public int cost;            // 0 / 300 / 600
        public int drawCount;       // 抽取祝福数量（通常3）
        public List<RitualBlessingData> blessings;
    }

    /// <summary>
    /// 命运祝福条目
    /// </summary>
    [System.Serializable]
    public class RitualBlessingData
    {
        public string type;         // LeaderStatBoostTemporary / LeaderStatBoostPermanent / LeaderStatBoostPercent / Consumable / CatFood
        public int weight;
        public List<string> statTypes;  // 适用属性列表
        public int minAmount;       // 最小固定值
        public int maxAmount;       // 最大固定值
        public float minPercent;    // 最小百分比
        public float maxPercent;    // 最大百分比
        public int minCount;        // 最小数量（消耗品）
        public int maxCount;        // 最大数量（消耗品）
    }
}
