using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 范围过滤器 — 用于判断 buff 是否作用于目标单位
    /// </summary>
    [Serializable]
    public class ScopeFilter
    {
        public bool isAll;
        public int tribeType;     // TribeType 枚举值，0=None
        public int tier;          // 目标 tier，-1=不限
        public int rarity;        // 目标稀有度，-1=不限（Rarity 枚举值）

        /// <summary>
        /// 判断是否匹配
        /// </summary>
        public bool Matches(bool isEnemy, TribeType targetTribe, int targetTier, int targetRarity = -1)
        {
            if (isAll) return true;
            if (tribeType > 0 && (int)targetTribe != tribeType) return false;
            if (tier >= 0 && targetTier != tier) return false;
            if (rarity >= 0 && targetRarity != rarity) return false;
            return true;
        }
    }
}
