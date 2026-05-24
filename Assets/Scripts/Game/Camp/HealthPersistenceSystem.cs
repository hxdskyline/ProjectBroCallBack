using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// HP 持久化系统 — 战斗伤害跨关卡保留，0 血单位获得满目疮痍 debuff
    /// </summary>
    public class HealthPersistenceSystem
    {
        /// <summary>
        /// 战斗结束后结算 HP：更新 currentHp，处理满目疮痍
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

                    // 0 血单位获得满目疮痍
                    if (unit.currentHp <= 0)
                    {
                        unit.hasWoundsDebuff = true;
                        unit.SetZone(UnitZone.Production);
                        Debug.Log($"[HealthPersistenceSystem] {unit.name} 满目疮痍，移入生产区");
                    }
                }
            }

            // Boss 关胜利：所有单位满血复活
            if (victory && isBoss)
            {
                HealAllAlliesPercent(1.0f);
                ClearAllWoundsDebuff();
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

                    // 满血复活时清除满目疮痍（仅当完全回复时）
                    if (unit.currentHp >= maxHp)
                    {
                        unit.hasWoundsDebuff = false;
                    }
                }
            }

            dataManager.SavePlayerData();
            Debug.Log($"[HealthPersistenceSystem] 全体回复 {percent * 100}% HP");
        }

        /// <summary>
        /// 清除所有单位的满目疮痍 debuff
        /// </summary>
        public void ClearAllWoundsDebuff()
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
                    if (unit != null)
                        unit.hasWoundsDebuff = false;
                }
            }
        }
    }
}
