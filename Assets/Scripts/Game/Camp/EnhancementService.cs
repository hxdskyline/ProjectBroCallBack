using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 强化服务 — 将兵种从 enhanceLevel 0 提升到 1，回满HP
    /// 强化属性变化由 fighter_config.json 的 enhanceStatModifiers 配置决定
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

            // 回满HP：有效最大HP由配置的 enhanceStatModifiers 决定
            var config = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
            int effectiveMaxHp = config != null ? config.GetEffectiveMaxHp(1) : 100;
            unit.currentHp = effectiveMaxHp;

            // 重建 buff 并保存
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager != null)
            {
                dataManager.RebuildAllBuffs();
                dataManager.SavePlayerData();
            }

            GameLogger.Log("Enhance", $"强化成功: {unit.name} enhanceLevel={unit.enhanceLevel} maxHp={effectiveMaxHp}");
            return true;
        }
    }
}
