using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// Buff 静态服务
    /// </summary>
    public static class BuffService
    {
        /// <summary>
        /// 清除所有战斗专属 buff（战斗结束后调用）— 遍历所有族群的所有单位
        /// </summary>
        public static void ClearAllBattleBuffs()
        {
            var dataManager = UnityEngine.GameObject.FindObjectOfType<GameManager>()?.DataManager;
            if (dataManager == null) return;

            var tribes = dataManager.GetTribes();
            if (tribes == null) return;

            foreach (var tribe in tribes)
            {
                if (tribe?.units == null) continue;
                foreach (var unit in tribe.units)
                {
                    if (unit?.ActiveBuffs != null)
                        ClearAllBattleBuffs(unit.ActiveBuffs);
                }
            }
        }

        /// <summary>
        /// 清除指定 buff 列表中的战斗专属 buff
        /// </summary>
        public static void ClearAllBattleBuffs(List<UnifiedBuff> buffs)
        {
            if (buffs == null) return;
            buffs.RemoveAll(b => b.persistence == BuffPersistence.BattleOnly);
        }
    }
}
