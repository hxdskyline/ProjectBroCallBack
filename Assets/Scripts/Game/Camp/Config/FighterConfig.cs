using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 强化属性修正条目 — 可配置强化时属性如何变化
    /// </summary>
    [Serializable]
    public class EnhanceStatModifier
    {
        public string statType;   // "Attack" / "Defense" / "Hp" / "MoveSpeed" / "AttackSpeed"
        public bool isPercent;   // true=百分比修正, false=固定值修正
        public float value;      // 修正数值
    }

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
        public int passiveSkillId;      // 被动技能 ID（旧字段，兼容）

        // ── 新字段 ──
        public List<string> mechanismTags;  // 机制标签列表，用于圣物系统匹配（如 ["poison", "bounce"]）
        public List<string> typeTags;       // 类型标签列表（如 ["tank"] 或 ["warrior", "tank"]）
        public string weightClass;          // 重量级标签（"Heavy" / "Medium" / "Light"，逗号分隔多个）
        public string skillIdOriginal;      // 原版技能ID
        public string skillIdEnhanced;      // 强化版技能ID
        public string skillDescriptionOriginal;  // 原版技能描述
        public string skillDescriptionEnhanced;  // 强化版技能描述
        public List<EnhanceStatModifier> enhanceStatModifiers;  // 强化属性修正（空=属性不变）
        public string targetPriority;      // 默认目标选择（如 "nearest"）

        // 旧字段兼容
        public string mechanismTag => mechanismTags != null && mechanismTags.Count > 0 ? mechanismTags[0] : "";

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

        /// <summary>
        /// 获取当前强化等级对应的技能ID
        /// </summary>
        public string GetSkillId(int enhanceLevel)
        {
            if (enhanceLevel >= 1 && !string.IsNullOrEmpty(skillIdEnhanced))
                return skillIdEnhanced;
            return skillIdOriginal;
        }

        /// <summary>
        /// 获取当前强化等级对应的技能描述
        /// </summary>
        public string GetSkillDescription(int enhanceLevel)
        {
            if (enhanceLevel >= 1 && !string.IsNullOrEmpty(skillDescriptionEnhanced))
                return skillDescriptionEnhanced;
            return skillDescriptionOriginal;
        }

        /// <summary>
        /// 判断是否有强化属性修正
        /// </summary>
        public bool HasEnhanceStatModifiers => enhanceStatModifiers != null && enhanceStatModifiers.Count > 0;

        /// <summary>
        /// 计算指定强化等级下的有效最大HP
        /// </summary>
        public int GetEffectiveMaxHp(int enhanceLevel)
        {
            int baseHp = hp;
            if (enhanceLevel < 1 || !HasEnhanceStatModifiers)
                return baseHp;

            float hpPercentMod = 0f;
            int hpFlatMod = 0;
            foreach (var mod in enhanceStatModifiers)
            {
                if (mod.statType == "Hp")
                {
                    if (mod.isPercent) hpPercentMod += mod.value;
                    else hpFlatMod += Mathf.RoundToInt(mod.value);
                }
            }
            return Mathf.RoundToInt(baseHp * (1f + hpPercentMod)) + hpFlatMod;
        }
    }
}
