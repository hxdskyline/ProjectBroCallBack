using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// HP 持久化系统 — 战斗伤害跨关卡保留，战后全员回复
    /// </summary>
    public class HealthPersistenceSystem
    {
        /// <summary>
        /// 战斗结束后结算 HP：0 血单位移入待上阵区，全员 20% 回血
        /// </summary>
        public void OnBattleEnd(bool victory, bool isBoss)
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var tribes = dataManager.GetTribes();
            if (tribes == null) return;

            foreach (var tribe in tribes)
            {
                if (tribe == null || !tribe.isActive) continue;
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    if (unit == null) continue;

                    // 0 血单位移入待上阵区（下轮可用）
                    if (unit.currentHp <= 0)
                    {
                        unit.SetZone(UnitZone.Standby);
                        Debug.Log($"[HealthPersistenceSystem] {unit.name} HP=0，移入待上阵区");
                    }
                }
            }

            // 胜利后回血：存活单位已在战斗中回血，这里只处理死亡单位
            if (victory)
            {
                float healPercent = isBoss ? 1.0f : 0.2f;
                foreach (var tribe in tribes)
                {
                    if (tribe == null || !tribe.isActive || tribe.units == null) continue;
                    foreach (var unit in tribe.units)
                    {
                        if (unit == null) continue;
                        if (unit.currentHp > 0) continue; // 存活单位已在战斗中回血，跳过
                        if (isBoss)
                        {
                            // Boss 关卡：全员满血
                            var config = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
                            int maxHp = config?.hp ?? 100;
                            unit.currentHp = maxHp;
                        }
                        else
                        {
                            // 普通关卡：死亡单位回 20%
                            var config = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
                            int maxHp = config?.hp ?? 100;
                            int heal = Mathf.RoundToInt(maxHp * healPercent);
                            unit.currentHp = Mathf.Min(unit.currentHp + heal, maxHp);
                        }
                    }
                }
            }

            dataManager.SavePlayerData();
        }

        /// <summary>
        /// 回复所有友方单位指定百分比 HP
        /// </summary>
        public void HealAllAlliesPercent(float percent)
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var tribes = dataManager.GetTribes();
            if (tribes == null) return;

            foreach (var tribe in tribes)
            {
                if (tribe == null || !tribe.isActive) continue;
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    if (unit == null) continue;

                    var config = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
                    int maxHp = config?.hp ?? 100;

                    int heal = Mathf.RoundToInt(maxHp * percent);
                    unit.currentHp = Mathf.Min(unit.currentHp + heal, maxHp);
                }
            }

            dataManager.SavePlayerData();
            Debug.Log($"[HealthPersistenceSystem] 全体回复 {percent * 100}% HP");
        }

    }
}
