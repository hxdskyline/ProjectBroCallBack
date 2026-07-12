using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 掷骰结果
    /// </summary>
    public enum DiceResult
    {
        Pending = -1,
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
        public int rarity;
        public bool bornEnhanced;
    }

    /// <summary>
    /// 招募系统
    /// 设计参考：正式文档/103_系统_招募.md
    /// </summary>
    public class RecruitmentDiceSystem
    {
        private static readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 获取玩家已拥有的所有单位 fighterId 集合
        /// </summary>
        private HashSet<int> GetOwnedFighterIds()
        {
            var owned = new HashSet<int>();
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return owned;

            var tribes = dataManager.GetTribes();
            if (tribes == null) return owned;

            foreach (var tribe in tribes)
            {
                if (tribe?.units == null) continue;
                foreach (var unit in tribe.units)
                    owned.Add(unit.fighterId);
            }

            return owned;
        }

        /// <summary>
        /// 生成招募卡片：一个己方本次出战的猫（随机选） + 一个本次敌方出战的猫（随机选）。
        /// 两张卡片都可直接招募（无掷骰子），都按原公式收费。
        /// </summary>
        /// <param name="enemyFighterIds">本次敌方出战的猫ID列表</param>
        /// <param name="playerFighterIds">本次己方出战的猫ID列表</param>
        public List<RecruitmentCard> GenerateRecruitmentCards(List<int> enemyFighterIds, List<int> playerFighterIds = null)
        {
            var cards = new List<RecruitmentCard>();
            int regionId = GetCurrentRegionId();
            bool isBossBattle = IsCurrentBattleBoss();

            // 预处理：去重并过滤有效ID
            var validPlayer = DeduplicateAndFilter(playerFighterIds);
            var validEnemy = DeduplicateAndFilter(enemyFighterIds);

            // 排除敌方中与己方重复的ID
            if (validPlayer.Count > 0)
            {
                var playerSet = new HashSet<int>(validPlayer);
                validEnemy.RemoveAll(id => playerSet.Contains(id));
            }

            GameLogger.Log("Recruit", $"GenerateCards validPlayer=[{string.Join(",", validPlayer)}] validEnemy=[{string.Join(",", validEnemy)}]");

            // 1. 从己方本次出战的猫中随机选1个
            if (validPlayer.Count > 0)
            {
                int pickedId = validPlayer[_rng.Next(validPlayer.Count)];
                var cfg = TribeConfigLoader.Instance.GetFighterConfig(pickedId);
                bool bornEnhanced = !isBossBattle && RollBornEnhanced((Rarity)cfg.rarity, regionId);
                cards.Add(CreateCard(cfg, bornEnhanced));
            }

            // 2. 从本次敌方出战的猫中随机选1个
            if (validEnemy.Count > 0)
            {
                int pickedId = validEnemy[_rng.Next(validEnemy.Count)];
                var cfg = TribeConfigLoader.Instance.GetFighterConfig(pickedId);
                bool bornEnhanced = !isBossBattle && RollBornEnhanced((Rarity)cfg.rarity, regionId);
                cards.Add(CreateCard(cfg, bornEnhanced));
            }

            // 保底：如果某一来源为空，从另一来源补选（确保有2张卡）
            if (cards.Count < 2)
            {
                var fallbackSource = cards.Count == 0 ? validPlayer : (validPlayer.Count > 0 ? validPlayer : validEnemy);
                if (fallbackSource.Count > 0)
                {
                    // 排除已选的
                    var existingIds = new HashSet<int>();
                    foreach (var c in cards) existingIds.Add(c.fighterId);
                    var remaining = new List<int>();
                    foreach (int fid in fallbackSource)
                    {
                        if (!existingIds.Contains(fid))
                            remaining.Add(fid);
                    }
                    // 如果没有不同的，允许重复
                    if (remaining.Count == 0) remaining = new List<int>(fallbackSource);

                    int pickedId = remaining[_rng.Next(remaining.Count)];
                    var cfg = TribeConfigLoader.Instance.GetFighterConfig(pickedId);
                    bool bornEnhanced = !isBossBattle && RollBornEnhanced((Rarity)cfg.rarity, regionId);
                    cards.Add(CreateCard(cfg, bornEnhanced));
                }
            }

            return cards;
        }

        private List<int> DeduplicateAndFilter(List<int> ids)
        {
            var result = new List<int>();
            if (ids == null) return result;
            var seen = new HashSet<int>();
            foreach (int fid in ids)
            {
                if (seen.Add(fid) && TribeConfigLoader.Instance?.GetFighterConfig(fid) != null)
                    result.Add(fid);
            }
            return result;
        }

        /// <summary>
        /// 生成 Boss 关稀有兵种三选一卡片。
        /// </summary>
        public List<RecruitmentCard> GenerateBossRareCards(int count = 3)
        {
            var rareFighters = TribeConfigLoader.Instance?.GetFightersByRarity(Rarity.Rare);
            if (rareFighters == null || rareFighters.Count == 0)
            {
                rareFighters = TribeConfigLoader.Instance?.GetFightersByRarity(Rarity.Advanced);
            }
            if (rareFighters == null || rareFighters.Count == 0) return new List<RecruitmentCard>();

            var ownedIds = GetOwnedFighterIds();
            var available = new List<FighterConfig>();
            foreach (var cfg in rareFighters)
            {
                if (!ownedIds.Contains(cfg.fighterId))
                    available.Add(cfg);
            }
            if (available.Count == 0) return new List<RecruitmentCard>();

            var shuffled = new List<FighterConfig>(available);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            var cards = new List<RecruitmentCard>();
            int take = Mathf.Min(count, shuffled.Count);
            for (int i = 0; i < take; i++)
            {
                var cfg = shuffled[i];
                var card = CreateCard(cfg, false);
                card.goldCost = 0;
                card.diceResult = DiceResult.Success;
                card.rarity = (int)Rarity.Rare;
                cards.Add(card);
            }

            return cards;
        }

        /// <summary>
        /// 掷骰子：根据兵种品质、魅力和 Boss 加成计算成功率。
        /// </summary>
        public void RollDice(RecruitmentCard card)
        {
            if (card == null || card.config == null) return;
            if (card.diceResult != DiceResult.Pending) return;

            float baseRate = card.config.rarity switch
            {
                0 => 0.7f,
                1 => 0.5f,
                2 => 0.3f,
                _ => 0.5f
            };

            int charisma = GameManager.Instance?.DataManager?.GetCharisma() ?? 1;
            float charismaBonus = charisma * 0.05f;
            float bossBonus = IsCurrentBattleBoss() ? 0.2f : 0f;

            float finalRate = Mathf.Clamp01(baseRate + charismaBonus + bossBonus);
            card.diceResult = _rng.NextDouble() < finalRate ? DiceResult.Success : DiceResult.Failure;

            GameLogger.Log(
                "Recruit",
                $"掷骰子: {card.name} base={baseRate:F0%} charisma+{charismaBonus:F0%} boss+{bossBonus:F0%} final={finalRate:F0%} result={card.diceResult}");
        }

        /// <summary>
        /// 招募单位：将成功的卡片转为 FighterData 加入待上阵区，扣除招募费用。
        /// </summary>
        public bool RecruitUnit(RecruitmentCard card)
        {
            if (card == null || card.diceResult != DiceResult.Success) return false;

            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return false;

            if (card.goldCost > 0)
            {
                var currencyMgr = GameManager.Instance?.CurrencyManager;
                if (currencyMgr != null && !currencyMgr.TrySpendCurrency(CurrencyType.Gold, card.goldCost))
                {
                    GameLogger.Log("Recruit", $"招募失败：猫币不足 {card.goldCost}");
                    return false;
                }
            }

            var cfg = card.config;
            var fighterData = new FighterData
            {
                fighterId = card.fighterId,
                tribeType = card.tribeType,
                tier = cfg?.tier ?? 1,
                name = card.name,
                currentHp = card.bornEnhanced && cfg != null ? cfg.GetEffectiveMaxHp(1) : (cfg?.hp ?? 100),
                zone = (int)UnitZone.Standby,
                rarity = card.rarity,
                enhanceLevel = card.bornEnhanced ? 1 : 0
            };

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

            GameLogger.Log("Recruit", $"招募成功: {card.name} rarity={card.rarity} enhanced={card.bornEnhanced}");
            return true;
        }

        /// <summary>
        /// 直接招募稀有兵种。
        /// </summary>
        public bool RecruitRareFighter(FighterConfig config)
        {
            if (config == null) return false;

            var card = CreateCard(config, false);
            card.goldCost = 0;
            card.diceResult = DiceResult.Success;
            card.rarity = (int)Rarity.Rare;
            return RecruitUnit(card);
        }

        private RecruitmentCard CreateCard(FighterConfig config, bool bornEnhanced)
        {
            return new RecruitmentCard
            {
                fighterId = config.fighterId,
                name = config.fighterName,
                tribeType = config.tribeType,
                populationCost = config.populationCost,
                goldCost = CalculateRecruitCost(config),
                diceResult = DiceResult.Success,
                config = config,
                rarity = config.rarity,
                bornEnhanced = bornEnhanced
            };
        }

        private int GetCurrentRegionId()
        {
            var gfc = GameFlowController.Instance;
            return gfc != null ? gfc.CurrentRegion : 1;
        }

        private bool RollBornEnhanced(Rarity rarity, int regionId)
        {
            var regionConfig = TribeConfigLoader.Instance?.GetRegionRarityConfig(regionId);
            if (regionConfig == null || regionConfig.rates == null) return false;

            foreach (var entry in regionConfig.rates)
            {
                if ((Rarity)entry.rarity == rarity)
                    return _rng.NextDouble() < entry.bornEnhanceRate;
            }
            return false;
        }

        private int CalculateRecruitCost(FighterConfig config)
        {
            if (config == null) return 100;
            float rarityMultiplier = 1f + config.rarity * 0.5f;
            return Mathf.RoundToInt(100 * (1 + config.tier * 0.5f) * rarityMultiplier);
        }

        private bool IsCurrentBattleBoss()
        {
            var gfc = GameFlowController.Instance;
            if (gfc?.CurrentRegionMap == null) return false;

            var currentNode = gfc.CurrentRegionMap.GetNode(gfc.CurrentNodeId);
            return currentNode != null && currentNode.nodeType == MapNodeType.Boss;
        }
    }
}
