using System.Collections.Generic;
using UnityEngine;
using Camp;
using Combat.Fighter;

namespace Combat
{
    /// <summary>
    /// 战斗 Buff 生命周期服务 — 管理 FighterData ↔ RuntimeAttributes 之间的 buff 同步
    /// </summary>
    public static class BattleBuffService
    {
        /// <summary>
        /// 战斗结束时，将战斗内 Persistent buff 从 RuntimeAttributes 同步回 FighterData.ActiveBuffs，
        /// 以便跨战斗保留。
        /// </summary>
        public static void SyncPersistentBuffsToUnits(BattleFighter[] playerFighters)
        {
            if (playerFighters == null) return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            var tribes = dataManager?.PlayerData?.tribes;
            if (tribes == null) return;

            foreach (BattleFighter fighter in playerFighters)
            {
                if (fighter == null || fighter.RuntimeAttributes == null) continue;

                FighterData unit = FindUnit(tribes, fighter.TribeType, fighter.FighterId);
                if (unit == null) continue;

                var runtimeBuffs = fighter.RuntimeAttributes.ActiveBuffs;
                var unitBuffs = unit.ActiveBuffs;

                for (int i = runtimeBuffs.Count - 1; i >= 0; i--)
                {
                    var runtimeBuff = runtimeBuffs[i];
                    if (runtimeBuff.persistence != BuffPersistence.Persistent
                        && runtimeBuff.persistence != BuffPersistence.TemporaryRoundBased) continue;

                    bool found = false;
                    for (int j = 0; j < unitBuffs.Count; j++)
                    {
                        if (unitBuffs[j].buffId == runtimeBuff.buffId)
                        {
                            unitBuffs[j].currentStacks = runtimeBuff.currentStacks;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        unit.AddUnifiedBuff(runtimeBuff.Clone());
                    }
                }
            }
        }

        private static FighterData FindUnit(List<TribeRecord> tribes, TribeType tribeType, int fighterId)
        {
            for (int t = 0; t < tribes.Count; t++)
            {
                if (tribes[t] == null || (TribeType)tribes[t].tribeType != tribeType) continue;
                if (tribes[t].units == null) continue;
                for (int u = 0; u < tribes[t].units.Count; u++)
                {
                    if (tribes[t].units[u].fighterId == fighterId)
                        return tribes[t].units[u];
                }
            }
            return null;
        }
    }
}
