using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 游戏选择记录 — 记录玩家在命运/抉择中获得的选择
    /// </summary>
    [Serializable]
    public class GameChoice
    {
        public string choiceId;
        public string displayName;
        public string description;
        public int category;           // ChoiceCategory 枚举值
        public int buffApplyType;      // BuffApplyType 枚举值
        public string buffScopeFilter; // JSON 字符串或简单文本
        public string buffScopeText;
        public List<BuffEffectItem> buffEffects;
        public int targetTribeType;    // TribeType 枚举值

        /// <summary>
        /// 解析 ScopeFilter
        /// </summary>
        public ScopeFilter GetScopeFilter()
        {
            var filter = new ScopeFilter();
            if (string.IsNullOrEmpty(buffScopeFilter) || buffScopeFilter == "all")
            {
                filter.isAll = true;
                return filter;
            }

            // 格式示例："Tabby | T1 | R1"  R0=普通 R1=高级 R2=稀有
            var parts = buffScopeFilter.Split('|');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("T") && int.TryParse(trimmed.Substring(1), out int t))
                    filter.tier = t;
                else if (trimmed.StartsWith("R") && int.TryParse(trimmed.Substring(1), out int r))
                    filter.rarity = r;
                else if (trimmed == "Tabby") filter.tribeType = (int)TribeType.Tabby;
                else if (trimmed == "Orange") filter.tribeType = (int)TribeType.Orange;
                else if (trimmed == "Cow") filter.tribeType = (int)TribeType.Cow;
                else if (trimmed == "Siamese") filter.tribeType = (int)TribeType.Siamese;
            }

            return filter;
        }
    }
}
