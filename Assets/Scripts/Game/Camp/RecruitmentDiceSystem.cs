using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 骰子结果
    /// </summary>
    public enum DiceResult
    {
        Failure = 0,
        Success = 1
    }

    /// <summary>
    /// 招募卡片数据
    /// </summary>
    public class RecruitmentCard
    {
        public int fighterId;
        public string name;
        public int tribeType;
        public int populationCost;
        public int goldCost;
        public DiceResult diceResult;
        public FighterConfig config;
    }

    /// <summary>
    /// 招募系统 — 战后掷骰子招募敌方单位
    /// 设计参考：正式文档/103_系统_招募.md
    /// </summary>
    public class RecruitmentDiceSystem
    {
        private static readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 根据敌方兵种 ID 列表生成招募卡片
        /// </summary>
        public List<RecruitmentCard> GenerateRecruitmentCards(List<int> enemyFighterIds)
        {
            var cards = new List<RecruitmentCard>();
            if (enemyFighterIds == null) return cards;

            foreach (int fighterId in enemyFighterIds)
            {
                var config = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
                if (config == null) continue;

                cards.Add(new RecruitmentCard
                {
                    fighterId = fighterId,
                    name = config.fighterName,
                    tribeType = config.tribeType,
                    populationCost = 1,
                    goldCost = CalculateRecruitCost(config),
                    diceResult = DiceResult.Failure,
                    config = config
                });
            }

            return cards;
        }

        /// <summary>
        /// 掷骰子：根据主角魅力和单位稀有度计算成功率
        /// </summary>
        public void RollDice(RecruitmentCard card)
        {
            if (card == null) return;

            var dataManager = GameManager.Instance?.DataManager;
            int charisma = dataManager?.GetCharisma() ?? 1;

            // 基础成功率 40% + 魅力 * 5%
            float baseRate = 0.4f + charisma * 0.05f;
            // Tier 越高，成功率越低
            float tierPenalty = card.config?.tier > 0 ? card.config.tier * 0.1f : 0f;
            float finalRate = Mathf.Clamp01(baseRate - tierPenalty);

            card.diceResult = _rng.NextDouble() < finalRate ? DiceResult.Success : DiceResult.Failure;
        }

        /// <summary>
        /// 招募单位：将成功的卡片转为 FighterData 加入待上阵区
        /// </summary>
        public bool RecruitUnit(RecruitmentCard card)
        {
            if (card == null || card.diceResult != DiceResult.Success) return false;

            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return false;

            var fighterData = new FighterData
            {
                fighterId = card.fighterId,
                tribeType = card.tribeType,
                tier = card.config?.tier ?? 1,
                name = card.name,
                currentHp = card.config?.hp ?? 100,
                zone = (int)UnitZone.Standby,
                hasWoundsDebuff = false
            };

            // 找到匹配的族群（或创建新的）
            var tribes = dataManager.GetTribes();
            TribeRecord targetTribe = null;
            foreach (var tribe in tribes)
            {
                if (tribe.tribeType == card.tribeType)
                {
                    targetTribe = tribe;
                    break;
                }
            }

            if (targetTribe == null)
            {
                targetTribe = new TribeRecord
                {
                    tribeType = card.tribeType,
                    isActive = true
                };
                dataManager.AddTribe(targetTribe);
            }

            targetTribe.units.Add(fighterData);
            dataManager.SavePlayerData();

            Debug.Log($"[RecruitmentDiceSystem] 招募成功: {card.name}");
            return true;
        }

        private int CalculateRecruitCost(FighterConfig config)
        {
            if (config == null) return 100;
            // 基础费用 * tier 倍率
            return Mathf.RoundToInt(100 * (1 + config.tier * 0.5f));
        }
    }
}
