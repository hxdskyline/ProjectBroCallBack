using UnityEngine;
using Camp;

namespace Combat.Effects
{
    /// <summary>
    /// 状态效果工厂 — 创建各种战斗状态效果的 UnifiedBuff 实例
    /// 数值参考正式文档/03_基础_战斗技能Buff系统.md
    /// </summary>
    public static class StatusEffectFactory
    {
        // ── 异常伤害类 ──

        /// <summary>
        /// 创建中毒效果（可叠加，每层每秒3点伤害，最高5层，持续6秒）— 数值对齐 status_effect_config.json 标准配置
        /// </summary>
        public static UnifiedBuff CreatePoison(float dpsPerStack = 3f, float duration = 6f, int maxStacks = 5)
        {
            return new UnifiedBuff
            {
                buffId = "poison",
                displayName = "中毒",
                source = BuffSource.Innate,
                sourceId = "poison",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.Stack,
                maxStacks = maxStacks,
                currentStacks = 1,
                gameEffect = GameEffect.Poison,
                effectParam1 = dpsPerStack,
                effectParam2 = duration,
                remainingDuration = duration,
                tickInterval = 1f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建灼烧效果（不可叠加，每秒5点伤害，持续3秒）
        /// </summary>
        public static UnifiedBuff CreateBurn(float dps = 5f, float duration = 3f)
        {
            return new UnifiedBuff
            {
                buffId = "burn",
                displayName = "灼烧",
                source = BuffSource.Innate,
                sourceId = "burn",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Burn,
                effectParam1 = dps,
                effectParam2 = duration,
                remainingDuration = duration,
                tickInterval = 1f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建流血效果（可叠加）
        /// </summary>
        public static UnifiedBuff CreateBleed(float dps, float duration, int maxStacks = 3)
        {
            return new UnifiedBuff
            {
                buffId = "bleed",
                displayName = "流血",
                source = BuffSource.Innate,
                sourceId = "bleed",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.Stack,
                maxStacks = maxStacks,
                currentStacks = 1,
                gameEffect = GameEffect.Bleed,
                effectParam1 = dps,
                effectParam2 = duration,
                remainingDuration = duration,
                tickInterval = 1f,
                tickTimer = 0f,
            };
        }

        // ── 控制类 ──

        /// <summary>
        /// 创建减速效果（取最高值，移速 -value%，持续 duration 秒）
        /// </summary>
        public static UnifiedBuff CreateSlow(float speedReductionPercent, float duration)
        {
            return new UnifiedBuff
            {
                buffId = "slow",
                displayName = "\u51CF\u901F",
                source = BuffSource.Innate,
                sourceId = "slow",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Slow,
                effectParam1 = speedReductionPercent,
                effectParam2 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建缠绕效果（无法移动，可攻击，持续 duration 秒）
        /// </summary>
        public static UnifiedBuff CreateRoot(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "root",
                displayName = "缠绕",
                source = BuffSource.Innate,
                sourceId = "root",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Root,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建沉默效果（无法触发技能，持续 duration 秒）
        /// </summary>
        public static UnifiedBuff CreateSilence(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "silence",
                displayName = "沉默",
                source = BuffSource.Innate,
                sourceId = "silence",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Silence,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建眩晕效果（无法行动和攻击，持续 duration 秒）
        /// </summary>
        public static UnifiedBuff CreateStun(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "stun",
                displayName = "眩晕",
                source = BuffSource.Innate,
                sourceId = "stun",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Stun,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建冰冻效果（无法攻击，结束时受到破冰伤害10点，持续 duration 秒）
        /// </summary>
        public static UnifiedBuff CreateFreeze(float duration, float breakDamage = 10f)
        {
            return new UnifiedBuff
            {
                buffId = "freeze",
                displayName = "冰冻",
                source = BuffSource.Innate,
                sourceId = "freeze",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Freeze,
                effectParam1 = duration,
                effectParam2 = breakDamage,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建击退效果（位移 distance 米，击退期间无法输出）
        /// </summary>
        public static UnifiedBuff CreateKnockBack(float distance)
        {
            return new UnifiedBuff
            {
                buffId = "knock_back",
                displayName = "击退",
                source = BuffSource.Innate,
                sourceId = "knock_back",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.KnockBack,
                effectParam1 = distance,
                remainingDuration = 0.5f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建击倒效果（倒地 duration 秒，期间无法输出）
        /// </summary>
        public static UnifiedBuff CreateKnockDown(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "knock_down",
                displayName = "\u51FB\u5012",
                source = BuffSource.Innate,
                sourceId = "knock_down",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.KnockDown,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建击飞效果（浮空+倒地 duration 秒，期间无法输出）
        /// </summary>
        public static UnifiedBuff CreateKnockUp(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "knock_up",
                displayName = "击飞",
                source = BuffSource.Innate,
                sourceId = "knock_up",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.KnockUp,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建嘲讽效果（强制攻击施法者，持续 duration 秒）
        /// </summary>
        public static UnifiedBuff CreateTaunt(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "taunt",
                displayName = "嘲讽",
                source = BuffSource.Innate,
                sourceId = "taunt",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Taunt,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        // ── 防御/增益类 ──

        /// <summary>
        /// 创建治疗buff（立即回复 amount 点HP）
        /// </summary>
        public static UnifiedBuff CreateHeal(float amount)
        {
            return new UnifiedBuff
            {
                buffId = "heal",
                displayName = "治疗",
                source = BuffSource.Innate,
                sourceId = "heal",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Heal,
                effectParam1 = amount,
                remainingDuration = 0.1f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建分摊连接（与友军分摊伤害和治疗，持续 duration 秒）
        /// </summary>
        public static UnifiedBuff CreateShareDamage(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "share_damage",
                displayName = "分摊",
                source = BuffSource.Innate,
                sourceId = "share_damage",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.ShareDamage,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建霸体效果（免疫所有控制效果，持续 duration 秒）
        /// </summary>
        public static UnifiedBuff CreateSuperArmor(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "super_armor",
                displayName = "霸体",
                source = BuffSource.Innate,
                sourceId = "super_armor",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.SuperArmor,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建吸血buff（攻击伤害的 value% 转化为自身HP回复）
        /// </summary>
        public static UnifiedBuff CreateLifesteal(float percent, float duration = -1f)
        {
            return new UnifiedBuff
            {
                buffId = "lifesteal",
                displayName = "吸血",
                source = BuffSource.Innate,
                sourceId = "lifesteal",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Lifesteal,
                effectParam1 = percent,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        // ── 特殊效果类 ──

        /// <summary>
        /// 创建分裂buff（触发时向主目标旁的其他敌人发射额外子弹）
        /// </summary>
        public static UnifiedBuff CreateSplit(float chance = 1f)
        {
            return new UnifiedBuff
            {
                buffId = "split",
                displayName = "分裂",
                source = BuffSource.Innate,
                sourceId = "split",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Split,
                effectParam1 = chance,
                remainingDuration = -1f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建弹射buff（伤害后弹射至另一敌人，造成50%伤害）
        /// </summary>
        public static UnifiedBuff CreateBounce(float chance = 1f, float damageMultiplier = 0.5f)
        {
            return new UnifiedBuff
            {
                buffId = "bounce",
                displayName = "弹射",
                source = BuffSource.Innate,
                sourceId = "bounce",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Bounce,
                effectParam1 = chance,
                effectParam2 = damageMultiplier,
                remainingDuration = -1f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建召唤buff（召唤 count 个单位）
        /// </summary>
        public static UnifiedBuff CreateSummon(int count)
        {
            return new UnifiedBuff
            {
                buffId = "summon",
                displayName = "召唤",
                source = BuffSource.Innate,
                sourceId = "summon",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Summon,
                effectParam1 = count,
                remainingDuration = 0.1f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        // ── 标记类 ──

        /// <summary>
        /// 创建狩猎标记（不可叠加，刷新持续时间）
        /// </summary>
        public static UnifiedBuff CreateHuntMark(float damageBonusPercent, float duration)
        {
            return new UnifiedBuff
            {
                buffId = "hunt_mark",
                displayName = "狩猎印记",
                source = BuffSource.Innate,
                sourceId = "hunt_mark",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.HuntMark,
                effectParam1 = damageBonusPercent,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        // ── 通用工厂方法：按 buffId 创建 buff ──

        /// <summary>
        /// 根据 buffId 字符串创建对应的 UnifiedBuff，用于技能配置驱动
        /// </summary>
        public static UnifiedBuff CreateBuff(string buffId)
        {
            switch (buffId)
            {
                // 异常伤害
                case "poison":              return CreatePoison();
                case "burn":                return CreateBurn(5f, 3f);
                case "bleed":               return CreateBleed(5f, 4f);
                // 控制
                case "freeze":              return CreateFreeze(2f, 10f);
                case "slow":                return CreateSlow(0.3f, 3f);
                case "root":                return CreateRoot(2f);
                case "silence":             return CreateSilence(2f);
                case "stun":                return CreateStun(2f);
                case "knock_back":          return CreateKnockBack(5f);
                case "knock_down":          return CreateKnockDown(2f);
                case "knock_up":            return CreateKnockUp(2f);
                case "taunt":               return CreateTaunt(3f);
                // 防御/增益
                case "heal":                return CreateHeal(20f);
                case "share_damage":        return CreateShareDamage(3f);
                case "super_armor":         return CreateSuperArmor(3f);
                case "lifesteal":           return CreateLifesteal(0.2f);
                // 特殊
                case "split":               return CreateSplit(0.3f);
                case "bounce":              return CreateBounce(0.5f, 0.5f);
                case "summon":              return CreateSummon(1);
                // 标记
                case "hunt_mark":           return CreateHuntMark(0.2f, 5f);
                default:
                    Debug.LogWarning($"[StatusEffectFactory] 未知 buffId: '{buffId}'");
                    return null;
            }
        }

        // ── 查询方法 ──

        /// <summary>
        /// 判断 GameEffect 是否为控制效果
        /// </summary>
        public static bool IsControlEffect(GameEffect effect)
        {
            return effect == GameEffect.Slow ||
                   effect == GameEffect.Root ||
                   effect == GameEffect.Silence ||
                   effect == GameEffect.Stun ||
                   effect == GameEffect.Freeze ||
                   effect == GameEffect.KnockBack ||
                   effect == GameEffect.KnockDown ||
                   effect == GameEffect.KnockUp ||
                   effect == GameEffect.Taunt;
        }

        /// <summary>
        /// 判断 GameEffect 是否为位移型控制（会解除束缚型/仇恨型控制）
        /// </summary>
        public static bool IsDisplacementControl(GameEffect effect)
        {
            return effect == GameEffect.KnockBack ||
                   effect == GameEffect.KnockDown ||
                   effect == GameEffect.KnockUp;
        }

        /// <summary>
        /// 判断 GameEffect 是否为束缚型控制
        /// </summary>
        public static bool IsImmobilizeControl(GameEffect effect)
        {
            return effect == GameEffect.Root ||
                   effect == GameEffect.Freeze;
        }

        /// <summary>
        /// 判断单位是否处于霸体状态（免疫控制）
        /// </summary>
        public static bool HasSuperArmor(System.Collections.Generic.List<UnifiedBuff> buffs)
        {
            if (buffs == null) return false;
            foreach (var buff in buffs)
            {
                if (buff.gameEffect == GameEffect.SuperArmor && !buff.IsExpired)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断单位是否被眩晕（无法行动和攻击）
        /// </summary>
        public static bool IsStunned(System.Collections.Generic.List<UnifiedBuff> buffs)
        {
            if (buffs == null) return false;
            foreach (var buff in buffs)
            {
                if ((buff.gameEffect == GameEffect.Stun ||
                     buff.gameEffect == GameEffect.Freeze ||
                     buff.gameEffect == GameEffect.KnockUp ||
                     buff.gameEffect == GameEffect.KnockDown) && !buff.IsExpired)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断单位是否被缠绕（无法移动）
        /// </summary>
        public static bool IsRooted(System.Collections.Generic.List<UnifiedBuff> buffs)
        {
            if (buffs == null) return false;
            foreach (var buff in buffs)
            {
                if (buff.gameEffect == GameEffect.Root && !buff.IsExpired)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断单位是否被沉默（无法触发技能）
        /// </summary>
        public static bool IsSilenced(System.Collections.Generic.List<UnifiedBuff> buffs)
        {
            if (buffs == null) return false;
            foreach (var buff in buffs)
            {
                if (buff.gameEffect == GameEffect.Silence && !buff.IsExpired)
                    return true;
            }
            return false;
        }
    }
}
