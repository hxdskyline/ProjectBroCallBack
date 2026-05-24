using UnityEngine;
using Camp;

namespace Combat.Effects
{
    /// <summary>
    /// 状态效果工厂 — 创建各种战斗状态效果的 UnifiedBuff 实例
    /// </summary>
    public static class StatusEffectFactory
    {
        // ── DoT 类 ──

        /// <summary>
        /// 创建毒效果（可叠加）
        /// </summary>
        public static UnifiedBuff CreatePoison(float dps, float duration, int maxStacks = 5)
        {
            return new UnifiedBuff
            {
                buffId = "poison",
                displayName = "毒",
                source = BuffSource.Innate,
                sourceId = "poison",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.Stack,
                maxStacks = maxStacks,
                currentStacks = 1,
                gameEffect = GameEffect.Poison,
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

        /// <summary>
        /// 创建燃烧效果（可叠加）
        /// </summary>
        public static UnifiedBuff CreateBurn(float dps, float duration, int maxStacks = 3)
        {
            return new UnifiedBuff
            {
                buffId = "burn",
                displayName = "燃烧",
                source = BuffSource.Innate,
                sourceId = "burn",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.Stack,
                maxStacks = maxStacks,
                currentStacks = 1,
                gameEffect = GameEffect.Burn,
                effectParam1 = dps,
                effectParam2 = duration,
                remainingDuration = duration,
                tickInterval = 1f,
                tickTimer = 0f,
            };
        }

        // ── 控制类 ──

        /// <summary>
        /// 创建冻结效果（不可叠加，刷新持续时间）
        /// </summary>
        public static UnifiedBuff CreateFreeze(float duration)
        {
            return new UnifiedBuff
            {
                buffId = "freeze",
                displayName = "冻结",
                source = BuffSource.Consumable,
                sourceId = "freeze",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                gameEffect = GameEffect.Freeze,
                effectParam1 = duration,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建减速效果（可叠加，移速 -value%）
        /// </summary>
        public static UnifiedBuff CreateSlow(float speedReductionPercent, float duration, int maxStacks = 3)
        {
            return new UnifiedBuff
            {
                buffId = "slow",
                displayName = "减速",
                source = BuffSource.Innate,
                sourceId = "slow",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.Stack,
                maxStacks = maxStacks,
                currentStacks = 1,
                gameEffect = GameEffect.Slow,
                effectParam1 = speedReductionPercent,
                effectParam2 = duration,
                remainingDuration = duration,
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

        // ── 战斗内成长类（可叠加层数） ──

        /// <summary>
        /// 创建橘猫饱食层数（跨战斗保留，可叠加）
        /// </summary>
        public static UnifiedBuff CreateFullnessStack(float hpPerStack = 2f, float atkPerStack = 1f)
        {
            return new UnifiedBuff
            {
                buffId = "fullness_stack",
                displayName = "饱食",
                source = BuffSource.Innate,
                sourceId = "fullness_stack",
                persistence = BuffPersistence.Persistent,
                stackRule = BuffStackRule.Stack,
                maxStacks = 999,
                currentStacks = 1,
                statType = StatType.Hp,
                isPercent = false,
                value = hpPerStack,
                gameEffect = GameEffect.FullnessStack,
                effectParam1 = hpPerStack,
                effectParam2 = atkPerStack,
                remainingDuration = -1f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建橘猫饱食 ATK 层数（跨战斗保留，可叠加，独立于 HP 层数）
        /// </summary>
        public static UnifiedBuff CreateFullnessAtkStack(float atkPerStack = 1f)
        {
            return new UnifiedBuff
            {
                buffId = "fullness_atk_stack",
                displayName = "饱食",
                source = BuffSource.Innate,
                sourceId = "fullness_atk_stack",
                persistence = BuffPersistence.Persistent,
                stackRule = BuffStackRule.Stack,
                maxStacks = 999,
                currentStacks = 1,
                statType = StatType.Attack,
                isPercent = false,
                value = atkPerStack,
                gameEffect = GameEffect.FullnessStack,
                effectParam1 = atkPerStack,
                remainingDuration = -1f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建无毛猫龙语充能（永久本场，可叠加）
        /// </summary>
        public static UnifiedBuff CreateDragonCharge(float spellDamagePercentPerStack = 2f)
        {
            return new UnifiedBuff
            {
                buffId = "dragon_charge",
                displayName = "龙语充能",
                source = BuffSource.Innate,
                sourceId = "dragon_charge",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.Stack,
                maxStacks = 10,
                currentStacks = 1,
                gameEffect = GameEffect.DragonCharge,
                effectParam1 = spellDamagePercentPerStack,
                remainingDuration = -1f,
                tickInterval = 0f,
                tickTimer = 0f,
            };
        }

        /// <summary>
        /// 创建狸花猫猎手专注（永久本场，可叠加）
        /// </summary>
        public static UnifiedBuff CreateHunterFocus(float markedDamagePercentPerStack = 5f)
        {
            return new UnifiedBuff
            {
                buffId = "hunter_focus",
                displayName = "猎手专注",
                source = BuffSource.Innate,
                sourceId = "hunter_focus",
                persistence = BuffPersistence.BattleOnly,
                stackRule = BuffStackRule.Stack,
                maxStacks = 999,
                currentStacks = 1,
                gameEffect = GameEffect.HunterFocus,
                effectParam1 = markedDamagePercentPerStack,
                remainingDuration = -1f,
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
                case "fullness_stack":      return CreateFullnessStack(60f, 4f);
                case "fullness_atk_stack":  return CreateFullnessAtkStack(4f);
                case "poison":              return CreatePoison(3f, 6f);
                case "bleed":               return CreateBleed(5f, 4f);
                case "burn":                return CreateBurn(5f, 4f);
                case "freeze":              return CreateFreeze(3f);
                case "slow":                return CreateSlow(0.3f, 6f);
                case "dragon_charge":       return CreateDragonCharge(2f);
                case "hunter_focus":        return CreateHunterFocus(5f);
                default:
                    Debug.LogWarning($"[StatusEffectFactory] 未知 buffId: '{buffId}'");
                    return null;
            }
        }
    }
}
