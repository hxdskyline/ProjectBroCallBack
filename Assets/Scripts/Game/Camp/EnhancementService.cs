using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 强化服务 — 将兵种从 enhanceLevel 0 提升到 1（全属性+50%），并回满HP
    /// </summary>
    public static class EnhancementService
    {
        /// <summary>
        /// 强化指定兵种：enhanceLevel 0→1，回满HP
        /// </summary>
        /// <returns>是否强化成功</returns>
        public static bool EnhanceFighter(FighterData unit)
        {
            if (unit == null || unit.IsEnhanced()) return false;

            unit.enhanceLevel = 1;

            // 回满HP：强化后实际 maxHp = 基础hp * 1.5（+50% buff），这里设为增强后满血
            var config = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
            int baseHp = config != null ? config.hp : 100;
            unit.currentHp = Mathf.RoundToInt(baseHp * 1.5f);

            // 重建 buff 并保存
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager != null)
            {
                dataManager.RebuildAllBuffs();
                dataManager.SavePlayerData();
            }

            GameLogger.Log("Enhance", $"强化成功: {unit.name} enhanceLevel={unit.enhanceLevel}");
            return true;
        }
    }
}
