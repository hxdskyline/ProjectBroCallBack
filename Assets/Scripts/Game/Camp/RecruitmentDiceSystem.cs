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
        public int rarity;          // Rarity 枚举值
        public bool bornEnhanced;   // 天生强化
    }

    /// <summary>
    /// 招募系统 — 战后掷骰子招募敌方单位
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
        /// 根据敌方兵种 ID 列表生成招募卡片
        /// 批次内去重：同兵种只保留一张，强化版覆盖普通版
        /// </summary>
        public List<RecruitmentCard> GenerateRecruitmentCards(List<int> enemyFighterIds)
        {
            var cards = new List<RecruitmentCard>();
            if (enemyFighterIds == null) return cards;

            int regionId = GetCurrentRegionId();
            // fighterId → 已加入的卡片索引，用于批次内去重
            var addedMap = new Dictionary<int, int>();

            foreach (int fighterId in enemyFighterIds)
            {
                var config = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
                if (config == null) continue;

                // 根据稀有度概率决定招募版本稀有度
                Rarity rollRarity = RollRarity(regionId);
                FighterConfig recruitConfig = config;

                // 如果掷出的稀有度与原始不同，尝试找同族同稀有度的兵种
                if ((Rarity)config.rarity != rollRarity)
                {
                    var altConfig = FindFighterByRarityAndTribe(rollRarity, (TribeType)config.tribeType);
                    if (altConfig != null)
                        recruitConfig = altConfig;
                }

                // 天生强化判定
                bool bornEnhanced = RollBornEnhanced(rollRarity, regionId);

                // 批次内去重：同 fighterId 只保留一张，强化版覆盖普通版
                if (addedMap.TryGetValue(recruitConfig.fighterId, out int existingIndex))
                {
                    if (bornEnhanced && !cards[existingIndex].bornEnhanced)
                    {
                        // 新的是强化版，替换已有的普通版
                        cards[existingIndex] = new RecruitmentCard
                        {
                            fighterId = recruitConfig.fighterId,
                            name = recruitConfig.fighterName,
                            tribeType = recruitConfig.tribeType,
                            populationCost = recruitConfig.populationCost,
                            goldCost = CalculateRecruitCost(recruitConfig),
                            diceResult = DiceResult.Failure,
                            config = recruitConfig,
                            rarity = (int)rollRarity,
                            bornEnhanced = true
                        };
                    }
                    // 否则跳过（已有更强或相同版本）
                    continue;
                }

                addedMap[recruitConfig.fighterId] = cards.Count;
                cards.Add(new RecruitmentCard
                {
                    fighterId = recruitConfig.fighterId,
                    name = recruitConfig.fighterName,
                    tribeType = recruitConfig.tribeType,
                    populationCost = recruitConfig.populationCost,
                    goldCost = CalculateRecruitCost(recruitConfig),
                    diceResult = DiceResult.Failure,
                    config = recruitConfig,
                    rarity = (int)rollRarity,
                    bornEnhanced = bornEnhanced
                });
            }

            return cards;
        }

        /// <summary>
        /// 生成Boss关稀有兵种三选一卡片（不掷骰子，直接可招募）
        /// </summary>
        public List<RecruitmentCard> GenerateBossRareCards(int count = 3)
        {
            var rareFighters = TribeConfigLoader.Instance?.GetFightersByRarity(Rarity.Rare);
            if (rareFighters == null || rareFighters.Count == 0)
            {
                // 没有稀有兵种配置时，回退到高级兵种
                rareFighters = TribeConfigLoader.Instance?.GetFightersByRarity(Rarity.Advanced);
            }
            if (rareFighters == null || rareFighters.Count == 0) return new List<RecruitmentCard>();

            var ownedIds = GetOwnedFighterIds();

            // 过滤掉已拥有的
            var available = new List<FighterConfig>();
            foreach (var cfg in rareFighters)
            {
                if (!ownedIds.Contains(cfg.fighterId))
                    available.Add(cfg);
            }
            if (available.Count == 0) return new List<RecruitmentCard>();

            // 随机选 count 个
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
                cards.Add(new RecruitmentCard
                {
                    fighterId = cfg.fighterId,
                    name = cfg.fighterName,
                    tribeType = cfg.tribeType,
                    populationCost = cfg.populationCost,
                    goldCost = 0, // Boss关稀有兵种免费
                    diceResult = DiceResult.Success, // Boss关不需要掷骰子
                    config = cfg,
                    rarity = (int)Rarity.Rare,
                    bornEnhanced = false // Boss关稀有兵种不天生强化
                });
            }

            return cards;
        }

        /// <summary>
        /// 掷骰子：根据兵种属性和主角咪格魅力计算成功率
        /// </summary>
        public void RollDice(RecruitmentCard card)
        {
            if (card == null || card.config == null) return;

            // 基础成功率：品质越高越难招募
            float baseRate = card.config.rarity switch
            {
                0 => 0.7f,   // 普通 70%
                1 => 0.5f,   // 高级 50%
                2 => 0.3f,   // 稀有 30%
                _ => 0.5f
            };

            // 咪格魅力加成：每点魅力+5%
            int charisma = GameManager.Instance?.DataManager?.GetCharisma() ?? 1;
            float charismaBonus = charisma * 0.05f;

            float finalRate = Mathf.Clamp01(baseRate + charismaBonus);
            card.diceResult = _rng.NextDouble() < finalRate ? DiceResult.Success : DiceResult.Failure;

            GameLogger.Log("Recruit", $"掷骰子: {card.name} base={baseRate:F0%} charisma+{charismaBonus:F0%} final={finalRate:F0%} result={card.diceResult}");
        }

        /// <summary>
        /// 招募单位：将成功的卡片转为 FighterData 加入待上阵区，扣除招募费用
        /// </summary>
        public bool RecruitUnit(RecruitmentCard card)
        {
            if (card == null || card.diceResult != DiceResult.Success) return false;

            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return false;

            // 扣除招募费用
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

            GameLogger.Log("Recruit", $"招募成功: {card.name} rarity={card.rarity} enhanced={card.bornEnhanced}");
            return true;
        }

        /// <summary>
        /// 直接招募稀有兵种（Boss三选一用，无需掷骰子）
        /// </summary>
        public bool RecruitRareFighter(FighterConfig config)
        {
            if (config == null) return false;

            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return false;

            var fighterData = new FighterData
            {
                fighterId = config.fighterId,
                tribeType = config.tribeType,
                tier = config.tier,
                name = config.fighterName,
                currentHp = config.hp,
                zone = (int)UnitZone.Standby,
                rarity = (int)Rarity.Rare,
                enhanceLevel = 0
            };

            var tribes = dataManager.GetTribes();
            TribeRecord targetTribe = null;
            foreach (var tribe in tribes)
            {
                if (tribe.tribeType == config.tribeType)
                {
                    targetTribe = tribe;
                    break;
                }
            }

            if (targetTribe == null)
            {
                targetTribe = new TribeRecord
                {
                    tribeType = config.tribeType,
                    isActive = true
                };
                dataManager.AddTribe(targetTribe);
            }

            targetTribe.units.Add(fighterData);
            dataManager.SavePlayerData();

            GameLogger.Log("Recruit", $"Boss招募稀有兵种: {config.fighterName}");
            return true;
        }

        // ── 内部方法 ──

        private int GetCurrentRegionId()
        {
            var gfc = GameFlowController.Instance;
            if (gfc != null)
                return gfc.CurrentRegion;
            return 1;
        }

        private Rarity RollRarity(int regionId)
        {
            var regionConfig = TribeConfigLoader.Instance?.GetRegionRarityConfig(regionId);
            if (regionConfig == null || regionConfig.rates == null || regionConfig.rates.Count == 0)
                return Rarity.Normal;

            double roll = _rng.NextDouble();
            double cumulative = 0;
            foreach (var entry in regionConfig.rates)
            {
                cumulative += entry.spawnRate;
                if (roll < cumulative)
                    return (Rarity)entry.rarity;
            }
            return Rarity.Normal;
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

        private FighterConfig FindFighterByRarityAndTribe(Rarity rarity, TribeType tribeType)
        {
            var fighters = TribeConfigLoader.Instance?.GetFightersByRarity(rarity);
            if (fighters == null || fighters.Count == 0) return null;

            // 优先找同族的
            foreach (var cfg in fighters)
            {
                if ((TribeType)cfg.tribeType == tribeType)
                    return cfg;
            }
            // 没有同族的，随机返回一个（排除敌方和看板）
            foreach (var cfg in fighters)
            {
                if (cfg.tribeType > 0 && cfg.tier > 0)
                    return cfg;
            }
            return null;
        }

        private int CalculateRecruitCost(FighterConfig config)
        {
            if (config == null) return 100;
            // 基础费用 * tier 倍率 * 稀有度倍率
            float rarityMultiplier = 1f + config.rarity * 0.5f;
            return Mathf.RoundToInt(100 * (1 + config.tier * 0.5f) * rarityMultiplier);
        }
    }
}
