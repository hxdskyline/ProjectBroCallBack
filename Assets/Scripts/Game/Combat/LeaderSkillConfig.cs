using System;
using System.Collections.Generic;
using UnityEngine;
using Camp;

namespace Combat
{
    /// <summary>
    /// 首领技能类型
    /// </summary>
    public enum SkillType
    {
        Passive,    // 被动：满足条件时自动触发
        Active      // 主动：冷却完毕后自动释放
    }

    /// <summary>
    /// 技能目标类型
    /// </summary>
    public enum TargetType
    {
        Self,       // 自身
        Ally,       // 单个友方
        Enemy,      // 单个敌人
        Area,       // 区域（周围范围）
        AllEnemies, // 所有敌人
        AllAllies   // 所有友方
    }

    /// <summary>
    /// 技能效果类型
    /// </summary>
    public enum SkillEffectType
    {
        None = 0,

        // ── 伤害/治疗 ──
        Damage,             // 造成伤害 (value = 伤害值)
        Heal,               // 治疗 (value = 治疗量或百分比)
        TrueDamage,         // 真实伤害 (value = 伤害值)

        // ── 属性修改 ──
        AttackBuff,         // 攻击力加成 (value = 百分比, duration = 持续秒数)
        DefenseBuff,        // 防御力加成
        SpeedBuff,          // 移速加成
        AttackSpeedBuff,    // 攻速加成

        // ── 状态效果 ──
        ApplyPoison,        // 附加毒 (value = dps, duration = 持续秒数)
        ApplyBleed,         // 附加流血
        ApplyBurn,          // 附加燃烧
        ApplyFreeze,        // 附加冻结 (value = 冻结秒数)
        ApplySlow,          // 附加减速 (value = 减速百分比)
        ApplyHuntMark,      // 附加狩猎标记

        // ── 通用 buff 赋予 ──
        ApplyBuff,          // 对目标赋予指定 buff（buffId 必填，value = 叠加层数）

        // ── 特殊效果 ──
        SummonUnit,         // 召唤单位 (value = 模板ID)
        ResurrectCorpse,    // 复活尸体
        ConsumeCorpse,      // 消耗尸体
        PhaseShift,         // 相位转移（无敌）(value = 秒数)
        Shield,             // 护盾 (value = 护盾量)
        ThrowUnit,          // 投掷单位
        Stealth,            // 隐匿 (value = 秒数)
    }

    /// <summary>
    /// 单个技能效果条目
    /// </summary>
    [Serializable]
    public class SkillEffectEntry
    {
        public SkillEffectType effectType;
        public float value;         // 效果数值
        public float duration;      // 持续时间（秒）
        public TargetType target;   // 目标类型
        public float areaRadius;    // 区域半径（仅 Area 目标时使用）
        public string buffId;       // ApplyBuff 时必填：StatusEffectFactory 中的工厂方法名

        // ── 条件分支 ──
        public string conditionBuffId;                      // 检查技能发送者（caster）是否拥有此 buffId
        public List<SkillEffectEntry> conditionEffects;     // caster 有该 buff 时执行的效果列表
        public List<SkillEffectEntry> conditionFallbackEffects; // caster 无该 buff 时执行的效果列表
    }

    /// <summary>
    /// 首领技能数据（从 JSON 配置加载）
    /// </summary>
    [Serializable]
    public class LeaderSkillData
    {
        public int skillId;
        public string skillName;
        public string description;
        public SkillType skillType;
        public float cooldown;                      // 主动技能冷却时间（秒），被动为 0
        public float passiveCheckInterval;          // 被动检查间隔（秒），默认 1
        public List<SkillEffectEntry> effects;      // 技能效果列表

        // ── 条件分支（技能级别，检查发送者） ──
        public string conditionBuffId;
        public List<SkillEffectEntry> conditionEffects;
        public List<SkillEffectEntry> conditionFallbackEffects;

        public LeaderSkillData()
        {
            skillId = 0;
            skillName = "";
            description = "";
            skillType = SkillType.Active;
            cooldown = 6f;
            passiveCheckInterval = 1f;
            effects = new List<SkillEffectEntry>();
        }
    }

    /// <summary>
    /// 首领技能配置表（从 leader_skill_config.json 加载）
    /// </summary>
    [Serializable]
    public class LeaderSkillConfigTable
    {
        public List<LeaderSkillEntry> skills;

        public LeaderSkillConfigTable()
        {
            skills = new List<LeaderSkillEntry>();
        }

        public LeaderSkillData GetSkill(int skillId)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].skillId == skillId)
                    return skills[i].ToSkillData();
            }
            return null;
        }

        public List<LeaderSkillData> GetSkillsForTribe(TribeType tribeType)
        {
            var result = new List<LeaderSkillData>();
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].tribeType == tribeType.ToString())
                    result.Add(skills[i].ToSkillData());
            }
            return result;
        }
    }

    /// <summary>
    /// JSON 序用化用的技能条目
    /// </summary>
    [Serializable]
    public class LeaderSkillEntry
    {
        public int skillId;
        public string skillName;
        public string description;
        public string tribeType;         // 种族名（Tabby/Orange/Cow/Siamese）
        public string skillType;         // "Passive" / "Active"
        public float cooldown;
        public float passiveCheckInterval;
        public List<SkillEffectEntry> effects;

        // ── 条件分支（技能级别，检查发送者） ──
        public string conditionBuffId;
        public List<SkillEffectEntry> conditionEffects;
        public List<SkillEffectEntry> conditionFallbackEffects;

        public LeaderSkillEntry()
        {
            skillId = 0;
            skillName = "";
            description = "";
            tribeType = "";
            skillType = "Active";
            cooldown = 6f;
            passiveCheckInterval = 1f;
            effects = new List<SkillEffectEntry>();
        }

        public LeaderSkillData ToSkillData()
        {
            return new LeaderSkillData
            {
                skillId = skillId,
                skillName = skillName,
                description = description,
                skillType = skillType == "Passive" ? SkillType.Passive : SkillType.Active,
                cooldown = cooldown,
                passiveCheckInterval = passiveCheckInterval > 0 ? passiveCheckInterval : 1f,
                effects = effects ?? new List<SkillEffectEntry>(),
                conditionBuffId = conditionBuffId,
                conditionEffects = conditionEffects,
                conditionFallbackEffects = conditionFallbackEffects
            };
        }
    }
}
