using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// Buff 效果条目 — 一个 buff/装备/选择可以携带多个属性效果
    /// 对应 buff_config.json 中的 buffEffects[] 和 tribe_aura_config.json 中的 effects[]
    /// </summary>
    [Serializable]
    public class BuffEffectItem
    {
        public string statType;       // "Attack", "Defense", "Hp", "MoveSpeed", "AttackSpeed"
        public bool isPercent;        // true=百分比加成, false=固定值加成
        public float value;           // 加成数值
        public int gameEffectType;    // 对应 GameEffect 枚举值（0=纯属性修改）
        public float duration;        // 持续时间（秒），0=永久，>0=限时（仅战斗内生效）
        public bool isPersistent;     // true=永久生效（跨战斗），false=仅战斗内

        /// <summary>
        /// 将 statType 字符串转为 StatType 枚举
        /// </summary>
        public StatType GetStatType()
        {
            if (Enum.TryParse<StatType>(statType, out var result))
                return result;
            return StatType.Attack;
        }
    }
}
