using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 统一 Buff 数据结构 — 贯穿战斗、构筑、持久化的核心 buff 类型
    /// </summary>
    [Serializable]
    public class UnifiedBuff
    {
        // 身份
        public string buffId;
        public string displayName;
        public string description;

        // 来源
        public BuffSource source;
        public string sourceId;

        // 生命周期
        public BuffPersistence persistence;
        public BuffStackRule stackRule;
        public int maxStacks;
        public int currentStacks;

        // 属性修改（简单 buff 用）
        public StatType statType;
        public bool isPercent;
        public float value;

        // 游戏效果（复杂 buff 用，对应 GameEffect 枚举值）
        public GameEffect gameEffect;
        public int gameEffectType;   // JSON 中的 gameEffectType 数值
        public float effectParam1;
        public float effectParam2;

        // 计时
        public float remainingDuration;
        public float tickInterval;
        public float tickTimer;

        // 可见性
        public bool visible = true;
        public int iconColorIndex;

        // Buff 效果列表（对应 buff_config.json 中的 buffEffects[]）
        public List<BuffEffectItem> buffEffects;

        /// <summary>
        /// 是否已过期
        /// </summary>
        public bool IsExpired => remainingDuration == 0f ||
                                  (remainingDuration > 0f && remainingDuration < 0.001f);

        /// <summary>
        /// 克隆
        /// </summary>
        public UnifiedBuff Clone()
        {
            var clone = (UnifiedBuff)MemberwiseClone();
            if (buffEffects != null)
                clone.buffEffects = new List<BuffEffectItem>(buffEffects);
            return clone;
        }

        /// <summary>
        /// 创建属性 buff（用于持久化光环、装备等）
        /// </summary>
        public static UnifiedBuff CreateStatBuff(
            string id, string name,
            BuffSource src, string srcId,
            StatType stat, bool percent, float val,
            GameEffect gameEffectType = GameEffect.AttackPercent,
            string description = null)
        {
            return new UnifiedBuff
            {
                buffId = id,
                displayName = name,
                description = description,
                source = src,
                sourceId = srcId,
                persistence = BuffPersistence.Persistent,
                stackRule = BuffStackRule.None,
                maxStacks = 1,
                currentStacks = 1,
                statType = stat,
                isPercent = percent,
                value = val,
                gameEffect = gameEffectType,
                remainingDuration = -1f,
                tickInterval = 0f,
                tickTimer = 0f,
                visible = true
            };
        }

        /// <summary>
        /// 创建限时 buff（用于消耗品、战斗内效果）
        /// </summary>
        public static UnifiedBuff CreateTimedBuff(
            string id, string name,
            BuffSource src, string srcId,
            StatType stat, bool percent, float val,
            float duration, BuffStackRule stack, int maxStack)
        {
            return new UnifiedBuff
            {
                buffId = id,
                displayName = name,
                source = src,
                sourceId = srcId,
                persistence = BuffPersistence.BattleOnly,
                stackRule = stack,
                maxStacks = maxStack,
                currentStacks = 1,
                statType = stat,
                isPercent = percent,
                value = val,
                remainingDuration = duration,
                tickInterval = 0f,
                tickTimer = 0f,
                visible = true
            };
        }
    }
}
