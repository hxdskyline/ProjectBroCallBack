using System.Collections.Generic;
using Camp;

namespace Combat
{
    /// <summary>
    /// 出战消耗计算器
    /// cost = SUM(每只出战小猫的种族基础消耗)
    /// actualCost = MAX(cost - freeQuota, 0)
    /// </summary>
    public static class DeployCostCalculator
    {
        /// <summary>
        /// 计算总出战消耗（不扣除免费额度）
        /// </summary>
        public static int CalculateTotalCost(List<TribeRecord> deployedTribes)
        {
            if (deployedTribes == null || deployedTribes.Count == 0)
                return 0;

            int total = 0;
            foreach (TribeRecord tribe in deployedTribes)
            {
                int costPerCat = GetDeployCostPerCat((TribeType)tribe.tribeType);
                int catCount = tribe.units?.Count ?? 0;
                total += costPerCat * catCount;
            }
            return total;
        }

        /// <summary>
        /// 计算实际消耗（扣除免费额度后）
        /// </summary>
        public static int CalculateActualCost(int totalCost, int freeQuota)
        {
            return System.Math.Max(0, totalCost - freeQuota);
        }

        /// <summary>
        /// 检查是否可以出战（猫粮是否足够支付超出免费额度的部分）
        /// </summary>
        public static bool CanDeploy(int totalCost, int freeQuota, int currentCatFood)
        {
            int actualCost = CalculateActualCost(totalCost, freeQuota);
            return currentCatFood >= actualCost;
        }

        /// <summary>
        /// 获取每个种族每只小猫的出战消耗
        /// </summary>
        private static int GetDeployCostPerCat(TribeType tribeType)
        {
            TribeConfig config = TribeConfigLoader.Instance.GetTribeConfig(tribeType);
            if (config != null && config.deployCostPerCat > 0)
                return config.deployCostPerCat;

            // Fallback defaults
            switch (tribeType)
            {
                case TribeType.Siamese: return 12; // 暹罗高攻，消耗高
                case TribeType.Cow:     return 8;  // 奶牛防御型，消耗低
                default:                return 10;
            }
        }
    }
}
