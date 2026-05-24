using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 族群配置 — 对应 tribe_config.json 中的单个族群条目
    /// </summary>
    [Serializable]
    public class TribeConfig
    {
        public int tribeType;
        public string tribeName;
        public string description;
        public int deployCostPerCat;
        public int leaderFighterId;
        public List<TribeUnitType> unitTypes;

        public TribeType GetTribeType() => (TribeType)tribeType;
    }

    [Serializable]
    public class TribeUnitType
    {
        public int tier;
        public int fighterId;
    }
}
