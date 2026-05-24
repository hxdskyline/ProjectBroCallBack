using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 可持有 Buff 的接口
    /// </summary>
    public interface IHasBuffs
    {
        List<UnifiedBuff> ActiveBuffs { get; }
        void AddUnifiedBuff(UnifiedBuff buff);
    }
}
