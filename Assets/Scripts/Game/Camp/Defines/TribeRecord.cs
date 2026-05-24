using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 族群记录 — 持久化存储中一个族群的所有数据
    /// </summary>
    [Serializable]
    public class TribeRecord
    {
        public int tribeId;
        public int tribeType;
        public bool isActive;
        public List<FighterData> units;

        public TribeRecord()
        {
            units = new List<FighterData>();
        }

        public TribeType GetTribeType() => (TribeType)tribeType;
    }
}
