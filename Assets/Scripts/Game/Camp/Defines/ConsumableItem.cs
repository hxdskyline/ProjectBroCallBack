using System;

namespace Camp
{
    /// <summary>
    /// 消耗品条目
    /// </summary>
    [Serializable]
    public class ConsumableItem
    {
        public int id;
        public string name;
        public int effectType;  // ConsumableEffectType 枚举值
        public float value;
    }
}
