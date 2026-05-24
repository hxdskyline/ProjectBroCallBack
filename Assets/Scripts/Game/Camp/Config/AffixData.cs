using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 词缀数据 — 对应 affix_config.json 中的单个词缀条目
    /// </summary>
    [Serializable]
    public class AffixData
    {
        public string affixId;
        public string displayName;
        public string description;
        public int fighterId;
        public string tier;       // "Fixed", "Low", "Mid", "High"
        public int weight;
        public List<int> buffIds;

        /// <summary>
        /// 将 buffIds 解析为 BuffEffectItem 列表（需要 TribeConfigLoader 查表）
        /// </summary>
        public List<BuffEffectItem> ResolveEffects()
        {
            var results = new List<BuffEffectItem>();
            if (buffIds == null) return results;

            foreach (int buffId in buffIds)
            {
                var buffConfig = TribeConfigLoader.Instance?.GetBuffConfig(buffId);
                if (buffConfig?.buffEffects != null)
                {
                    results.AddRange(buffConfig.buffEffects);
                }
            }
            return results;
        }

        /// <summary>
        /// 生成描述文本
        /// </summary>
        public string ResolveDescription()
        {
            var parts = new List<string>();
            if (buffIds == null) return displayName;

            foreach (int buffId in buffIds)
            {
                var buffConfig = TribeConfigLoader.Instance?.GetBuffConfig(buffId);
                if (buffConfig != null && !string.IsNullOrEmpty(buffConfig.description))
                {
                    parts.Add(buffConfig.description);
                }
            }
            return parts.Count > 0 ? string.Join("；", parts) : displayName;
        }
    }
}
