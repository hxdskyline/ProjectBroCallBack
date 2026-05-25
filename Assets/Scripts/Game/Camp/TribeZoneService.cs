using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 三区系统服务 — 管理待上阵区/上阵区/生产区的单位流转
    /// </summary>
    public class TribeZoneService
    {
        /// <summary>
        /// Boss 关：全员强制上阵（包括生产区单位），人口限制解除
        /// </summary>
        public void ForceAllUnitsToBattle()
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
                    unit.SetZone(UnitZone.Deployed);
                }
            }

            Debug.Log("[TribeZoneService] Boss关全员上阵完成");
        }

        /// <summary>
        /// 结算生产区产出：每个在生产区的单位产出一定量的木天蓼叶
        /// </summary>
        public int SettleProductionOutput()
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return 0;

            var tribes = dataManager.GetTribes();
            if (tribes == null) return 0;

            int totalOutput = 0;

            foreach (var tribe in tribes)
            {
                if (tribe == null || !tribe.isActive) continue;
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    if (unit != null && unit.GetZone() == UnitZone.Production)
                    {
                        totalOutput += 10; // 每个生产区单位产出 10 木天蓼叶
                    }
                }
            }

            if (totalOutput > 0)
            {
                dataManager.AddCatFood(totalOutput);
            }

            return totalOutput;
        }

        /// <summary>
        /// 将单位从待上阵区移到上阵区（检查人口上限）
        /// </summary>
        public bool MoveToDeployed(FighterData unit)
        {
            if (unit == null) return false;

            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return false;

            int populationCap = dataManager.GetPopulationCap();
            int currentPopulation = CountDeployedUnits();

            // 查找单位所在族群的人口消耗
            var tribes = dataManager.GetTribes();
            int unitPopCost = 1; // 默认人口消耗

            if (currentPopulation + unitPopCost > populationCap)
            {
                Debug.Log("[TribeZoneService] 人口上限不足");
                return false;
            }

            unit.SetZone(UnitZone.Deployed);
            return true;
        }

        /// <summary>
        /// 将单位从上阵区移回待上阵区
        /// </summary>
        public void MoveToStandby(FighterData unit)
        {
            if (unit != null)
                unit.SetZone(UnitZone.Standby);
        }

        /// <summary>
        /// 将单位移到生产区（不可逆）
        /// </summary>
        public void MoveToProduction(FighterData unit)
        {
            if (unit != null)
                unit.SetZone(UnitZone.Production);
        }

        /// <summary>
        /// 计算当前上阵区单位数
        /// </summary>
        public int CountDeployedUnits()
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return 0;

            int count = 0;
            var tribes = dataManager.GetTribes();
            if (tribes == null) return 0;

            foreach (var tribe in tribes)
            {
                if (tribe == null || !tribe.isActive) continue;
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    if (unit != null && unit.GetZone() == UnitZone.Deployed)
                        count++;
                }
            }

            return count;
        }
    }
}
