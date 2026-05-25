using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 战斗单位配置 — 对应 fighter_config.json 中的单个 fighter 条目
    /// </summary>
    [Serializable]
    public class FighterConfig
    {
        public int fighterId;
        public string fighterName;
        public int tribeType;
        public int tier;
        public int attack;
        public int defense;
        public int hp;
        public float moveSpeed;
        public float attackSpeed;
        public float attackRange;
        public List<int> innateBuffIds;
        public string avatarId;
        public List<string> tags;
        public int populationCost;
        public int deployZones; // 位标志: inner=1, middle=2, outer=4
        public int rarity;              // Rarity 枚举值，0=普通/1=高级/2=稀有
        public int enhanceLevel;        // 配置中的默认强化等级（通常0，天生强化时为1）
        public string mechanismTag;     // 机制标签，用于圣物系统匹配
        public int passiveSkillId;      // 被动技能 ID

        public bool CanDeployInner => (deployZones & 1) != 0;
        public bool CanDeployMiddle => (deployZones & 2) != 0;
        public bool CanDeployOuter => (deployZones & 4) != 0;

        /// <summary>
        /// 转换为 UnitStaticAttributes
        /// </summary>
        public UnitStaticAttributes ToStaticAttributes()
        {
            return new UnitStaticAttributes
            {
                Attack = attack,
                Defense = defense,
                MaxHp = hp,
                MoveSpeed = moveSpeed,
                AttackSpeed = attackSpeed,
                AttackRange = attackRange
            };
        }

        /// <summary>
        /// 转换为 TribeType 枚举
        /// </summary>
        public TribeType GetTribeType()
        {
            return (TribeType)tribeType;
        }

        /// <summary>
        /// 转换为 Rarity 枚举
        /// </summary>
        public Rarity GetRarity()
        {
            return (Rarity)rarity;
        }
    }
}
