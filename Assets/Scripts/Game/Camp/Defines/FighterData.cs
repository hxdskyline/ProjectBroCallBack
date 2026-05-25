using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 单位区域枚举
    /// </summary>
    public enum UnitZone
    {
        Standby = 0,     // 待上阵区
        Deployed = 1,    // 上阵区
        Production = 2   // 生产区
    }

    /// <summary>
    /// 战斗单位持久化数据 — 实现 IHasBuffs
    /// </summary>
    [Serializable]
    public class FighterData : IHasBuffs
    {
        public int fighterId;
        public int tribeType;
        public int tier;
        public string name;
        public float currentHp;
        public int zone;             // UnitZone 枚举值
        public int rarity;           // Rarity 枚举值
        public int enhanceLevel;     // 0=未强化, 1=已强化（全属性+50%）

        [NonSerialized]
        private List<UnifiedBuff> _activeBuffs = new List<UnifiedBuff>();

        public List<UnifiedBuff> ActiveBuffs
        {
            get => _activeBuffs ?? (_activeBuffs = new List<UnifiedBuff>());
            set => _activeBuffs = value;
        }

        public void AddUnifiedBuff(UnifiedBuff buff)
        {
            if (buff == null) return;
            ActiveBuffs.Add(buff);
        }

        public TribeType GetTribeType() => (TribeType)tribeType;
        public UnitZone GetZone() => (UnitZone)zone;
        public void SetZone(UnitZone z) => zone = (int)z;
        public Rarity GetRarity() => (Rarity)rarity;
        public bool IsEnhanced() => enhanceLevel >= 1;
    }
}
