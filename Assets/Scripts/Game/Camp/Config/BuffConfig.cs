using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// Buff 配置 — 对应 buff_config.json 中的单个 buff 条目
    /// </summary>
    [Serializable]
    public class BuffConfig
    {
        public int buffId;
        public string buffName;
        public string description;
        public int gameEffectType;
        public float effectParam1;
        public float effectParam2;
        public float duration;
        public bool visible;
        public int iconColorIndex;
        public List<BuffEffectItem> buffEffects;
    }
}
