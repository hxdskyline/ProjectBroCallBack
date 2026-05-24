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
    }
}
