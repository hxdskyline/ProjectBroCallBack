using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 商店全局配置
    /// </summary>
    [System.Serializable]
    public class ShopConfigData
    {
        public int baseRefreshCost;     // 基础刷新费用
        public int refreshIncrement;    // 每次刷新递增费用
        public int slotCount;           // 商品槽位数
        public int shopInterval;        // 商店间隔关数
        public int startRound;          // 起始回合
        public ArtifactShopConfig artifactConfig;
        public ConsumableShopConfig consumableConfig;
        public CatShopConfig catConfig;
    }

    [System.Serializable]
    public class ArtifactShopConfig
    {
        public int basePrice;
        public Dictionary<string, int> effects;      // id -> effect value
        public Dictionary<string, string> icons;      // id -> icon path
    }

    [System.Serializable]
    public class ConsumableShopConfig
    {
        public int basePriceMin;
        public int basePriceMax;
        public Dictionary<string, string> icons;
    }

    [System.Serializable]
    public class CatShopConfig
    {
        public Dictionary<int, int> basePrices;           // tribeType -> base price
        public Dictionary<int, float> qualityMultipliers; // rarity -> multiplier
        public float priceVariation;                      // 价格浮动
        public float sellRatio;                           // 出售倍率
        public Dictionary<int, List<string>> tribeIcons;
    }
}
