using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.Fighter;

namespace Combat
{
    /// <summary>
    /// 连携技效果类型
    /// </summary>
    public enum ComboEffectType
    {
        StatBuff,           // 属性增益
        AreaDamage,         // 范围伤害
        CrowdControl,       // 控制效果
        Summon              // 召唤效果
    }

    /// <summary>
    /// 连携技配置
    /// </summary>
    [Serializable]
    public class ComboSkillConfig
    {
        public string skillId;                  // 技能ID
        public string skillName;                // 技能名称
        public string description;              // 描述
        public List<string> requiredTags;       // 所需标签组合
        public ComboEffectType effectType;      // 效果类型
        public float effectValue;               // 效果数值
        public float effectDuration;            // 效果持续时间
        public float cooldown;                  // 冷却时间

        public ComboSkillConfig()
        {
            skillId = "";
            skillName = "";
            description = "";
            requiredTags = new List<string>();
            effectType = ComboEffectType.StatBuff;
            effectValue = 0;
            effectDuration = 0;
            cooldown = 0;
        }
    }

    /// <summary>
    /// 连携技实例
    /// </summary>
    [Serializable]
    public class ComboSkillInstance
    {
        public ComboSkillConfig config;
        public float remainingCooldown;
        public bool isActive;

        public ComboSkillInstance()
        {
            config = new ComboSkillConfig();
            remainingCooldown = 0;
            isActive = false;
        }
    }

    /// <summary>
    /// 连携技系统 - 特定标签组合的单位同时在场时，自动触发连携效果
    /// </summary>
    public class ComboSkillSystem
    {
        private List<ComboSkillConfig> _comboConfigs;
        private List<ComboSkillInstance> _activeCombos;

        // 事件
        public event Action<ComboSkillInstance> OnComboActivated;
        public event Action<ComboSkillInstance> OnComboTriggered;

        public ComboSkillSystem()
        {
            _comboConfigs = new List<ComboSkillConfig>();
            _activeCombos = new List<ComboSkillInstance>();
            InitializeComboConfigs();
        }

        /// <summary>
        /// 初始化连携技配置
        /// </summary>
        private void InitializeComboConfigs()
        {
            // 根据需求文档示例：场上同时存在"飞行"标签单位和"犬科"标签单位时，触发"天降正义"效果
            _comboConfigs = new List<ComboSkillConfig>
            {
                new ComboSkillConfig
                {
                    skillId = "combo_sky_justice",
                    skillName = "天降正义",
                    description = "场上同时存在飞行单位和犬科单位时，对敌方全体造成伤害",
                    requiredTags = new List<string> { "flying", "dog" },
                    effectType = ComboEffectType.AreaDamage,
                    effectValue = 100f,
                    effectDuration = 0,
                    cooldown = 10f
                },
                new ComboSkillConfig
                {
                    skillId = "combo_cat_swarm",
                    skillName = "猫群效应",
                    description = "场上同时存在3个以上猫科单位时，全体攻击力+20%",
                    requiredTags = new List<string> { "cat", "cat", "cat" },
                    effectType = ComboEffectType.StatBuff,
                    effectValue = 0.2f,
                    effectDuration = 5f,
                    cooldown = 15f
                },
                new ComboSkillConfig
                {
                    skillId = "combo_frost_nova",
                    skillName = "霜冻新星",
                    description = "场上同时存在冰系单位和远程单位时，冻结所有敌人2秒",
                    requiredTags = new List<string> { "ice", "ranged" },
                    effectType = ComboEffectType.CrowdControl,
                    effectValue = 2f,
                    effectDuration = 2f,
                    cooldown = 20f
                },
                new ComboSkillConfig
                {
                    skillId = "combo_summon_spirit",
                    skillName = "召唤精灵",
                    description = "场上同时存在3个不同种族单位时，召唤1个精灵助战",
                    requiredTags = new List<string> { "cat", "dog", "bird" },
                    effectType = ComboEffectType.Summon,
                    effectValue = 1f,
                    effectDuration = 10f,
                    cooldown = 30f
                }
            };
        }

        /// <summary>
        /// 检查当前场上单位可触发的连携技
        /// </summary>
        public List<ComboSkillInstance> CheckAvailableCombos(List<BattleFighter> playerFighters)
        {
            var availableCombos = new List<ComboSkillInstance>();

            if (playerFighters == null || playerFighters.Count == 0)
                return availableCombos;

            // 收集场上所有标签
            var fieldTags = CollectTags(playerFighters);

            foreach (var config in _comboConfigs)
            {
                if (CheckComboRequirement(config.requiredTags, fieldTags))
                {
                    var instance = new ComboSkillInstance
                    {
                        config = config,
                        remainingCooldown = 0,
                        isActive = true
                    };
                    availableCombos.Add(instance);
                }
            }

            return availableCombos;
        }

        /// <summary>
        /// 收集场上所有标签
        /// </summary>
        private Dictionary<string, int> CollectTags(List<BattleFighter> fighters)
        {
            var tags = new Dictionary<string, int>();

            foreach (var fighter in fighters)
            {
                if (fighter == null || !fighter.IsAlive)
                    continue;

                // 从FighterConfig获取标签
                var fighterConfig = Camp.TribeConfigLoader.Instance?.GetFighterConfig(fighter.FighterId);
                if (fighterConfig != null && fighterConfig.tags != null)
                {
                    foreach (var tag in fighterConfig.tags)
                    {
                        if (tags.ContainsKey(tag))
                            tags[tag]++;
                        else
                            tags[tag] = 1;
                    }
                }
            }

            return tags;
        }

        /// <summary>
        /// 检查连携技需求是否满足
        /// </summary>
        private bool CheckComboRequirement(List<string> requiredTags, Dictionary<string, int> availableTags)
        {
            var requiredCount = new Dictionary<string, int>();
            foreach (var tag in requiredTags)
            {
                if (requiredCount.ContainsKey(tag))
                    requiredCount[tag]++;
                else
                    requiredCount[tag] = 1;
            }

            foreach (var kvp in requiredCount)
            {
                if (!availableTags.ContainsKey(kvp.Key) || availableTags[kvp.Key] < kvp.Value)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 触发连携技
        /// </summary>
        public ComboEffectResult TriggerCombo(ComboSkillInstance combo, List<BattleFighter> playerFighters, List<BattleFighter> enemyFighters)
        {
            if (combo == null || !combo.isActive || combo.remainingCooldown > 0)
                return null;

            ComboEffectResult result = null;

            switch (combo.config.effectType)
            {
                case ComboEffectType.StatBuff:
                    result = ApplyStatBuff(combo, playerFighters);
                    break;
                case ComboEffectType.AreaDamage:
                    result = ApplyAreaDamage(combo, enemyFighters);
                    break;
                case ComboEffectType.CrowdControl:
                    result = ApplyCrowdControl(combo, enemyFighters);
                    break;
                case ComboEffectType.Summon:
                    result = ApplySummon(combo, playerFighters);
                    break;
            }

            if (result != null)
            {
                combo.remainingCooldown = combo.config.cooldown;
                OnComboTriggered?.Invoke(combo);
            }

            return result;
        }

        /// <summary>
        /// 应用属性增益 — 通过buff系统
        /// </summary>
        private ComboEffectResult ApplyStatBuff(ComboSkillInstance combo, List<BattleFighter> targets)
        {
            foreach (var target in targets)
            {
                if (target != null && target.IsAlive && target.RuntimeAttributes != null)
                {
                    var buff = Camp.UnifiedBuff.CreateTimedBuff(
                        $"combo_{combo.config.skillId}", combo.config.skillName,
                        Camp.BuffSource.Innate, combo.config.skillId,
                        Camp.StatType.Attack, true, combo.config.effectValue,
                        combo.config.effectDuration, Camp.BuffStackRule.None, 1);
                    target.RuntimeAttributes.ApplyBuff(buff);
                    target.RuntimeAttributes.SyncStatBuffs();
                    target.RuntimeAttributes.Recalculate();
                }
            }

            return new ComboEffectResult
            {
                comboName = combo.config.skillName,
                effectType = combo.config.effectType,
                value = combo.config.effectValue,
                duration = combo.config.effectDuration
            };
        }

        /// <summary>
        /// 应用范围伤害
        /// </summary>
        private ComboEffectResult ApplyAreaDamage(ComboSkillInstance combo, List<BattleFighter> targets)
        {
            foreach (var target in targets)
            {
                if (target != null && target.IsAlive && target.RuntimeAttributes != null)
                {
                    int dmg = (int)combo.config.effectValue;
                    target.RuntimeAttributes.CurrentHp = Mathf.Max(0, target.RuntimeAttributes.CurrentHp - dmg);
                }
            }

            return new ComboEffectResult
            {
                comboName = combo.config.skillName,
                effectType = combo.config.effectType,
                value = combo.config.effectValue
            };
        }

        /// <summary>
        /// 应用控制效果 — 冻结
        /// </summary>
        private ComboEffectResult ApplyCrowdControl(ComboSkillInstance combo, List<BattleFighter> targets)
        {
            var freezeBuff = Combat.Effects.StatusEffectFactory.CreateFreeze(combo.config.effectDuration);
            foreach (var target in targets)
            {
                if (target != null && target.IsAlive && target.RuntimeAttributes != null)
                {
                    target.RuntimeAttributes.ApplyBuff(freezeBuff);
                    target.FreezeTimer = Mathf.Max(target.FreezeTimer, combo.config.effectDuration);
                    GameLogger.LogFileOnly("Combat", $"FreezeApplied source=Combo combo={combo.config.skillName} target={target.Name} targetCamp={target.Camp} duration={combo.config.effectDuration:F2}");
                }
            }

            return new ComboEffectResult
            {
                comboName = combo.config.skillName,
                effectType = combo.config.effectType,
                value = combo.config.effectValue,
                duration = combo.config.effectDuration
            };
        }

        /// <summary>
        /// 应用召唤效果
        /// </summary>
        private ComboEffectResult ApplySummon(ComboSkillInstance combo, List<BattleFighter> allies)
        {
            Debug.Log($"[ComboSkillSystem] 触发连携技: {combo.config.skillName}");
            // 召唤逻辑由 SummonManager 处理，此处仅记录
            return new ComboEffectResult
            {
                comboName = combo.config.skillName,
                effectType = combo.config.effectType,
                value = combo.config.effectValue,
                duration = combo.config.effectDuration
            };
        }

        /// <summary>
        /// 更新冷却时间
        /// </summary>
        public void Update(float deltaTime)
        {
            foreach (var combo in _activeCombos)
            {
                if (combo.remainingCooldown > 0)
                {
                    combo.remainingCooldown -= deltaTime;
                    if (combo.remainingCooldown < 0)
                        combo.remainingCooldown = 0;
                }
            }
        }

        /// <summary>
        /// 重置所有连携技
        /// </summary>
        public void Reset()
        {
            foreach (var combo in _activeCombos)
            {
                combo.remainingCooldown = 0;
            }
        }
    }

    /// <summary>
    /// 连携技效果结果
    /// </summary>
    public class ComboEffectResult
    {
        public string comboName;
        public ComboEffectType effectType;
        public float value;
        public float duration;
    }
}
