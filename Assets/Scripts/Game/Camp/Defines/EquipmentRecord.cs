using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 装备记录 — 记录本局获得的装备/奇物
    /// </summary>
    [Serializable]
    public class EquipmentRecord
    {
        public string equipmentId;
        public string displayName;
        public string description;
        public int buffApplyType;      // BuffApplyType 枚举值
        public string buffScopeText;
        public List<BuffEffectItem> effects;

        /// <summary>
        /// 解析 ScopeFilter
        /// </summary>
        public ScopeFilter GetScopeFilter()
        {
            var filter = new ScopeFilter();
            if (string.IsNullOrEmpty(buffScopeText) || buffScopeText == "all")
            {
                filter.isAll = true;
                return filter;
            }

            var parts = buffScopeText.Split('|');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("T") && int.TryParse(trimmed.Substring(1), out int t))
                    filter.tier = t;
                else if (trimmed == "Tabby") filter.tribeType = (int)TribeType.Tabby;
                else if (trimmed == "Orange") filter.tribeType = (int)TribeType.Orange;
                else if (trimmed == "Cow") filter.tribeType = (int)TribeType.Cow;
                else if (trimmed == "Siamese") filter.tribeType = (int)TribeType.Siamese;
            }

            return filter;
        }
    }
}
