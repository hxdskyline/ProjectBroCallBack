using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 祝福选项（已解析的具体数值，供UI展示）
    /// </summary>
    public class FateBlessingOption
    {
        public string id;
        public string displayName;
        public string description;
        public string type;          // LeaderStatBoostTemporary / Permanent / Percent / Consumable / CatFood
        public StatType statType;
        public bool isPercent;
        public float value;
        public int catFoodAmount;
        public ConsumableItem consumable;
        public string iconColor;
    }

    /// <summary>
    /// 命运/祈福服务 — 管理档次选择和祝福抽取
    /// 设计参考：正式文档/105_系统_命运.md
    /// </summary>
    public class FateSystem
    {
        private static readonly System.Random _rng = new System.Random();
        private List<RitualTierData> _tierConfigs;

        public void Initialize()
        {
            _tierConfigs = TribeConfigLoader.Instance?.GetRitualTiers();
            if (_tierConfigs == null || _tierConfigs.Count == 0)
            {
                Debug.LogWarning("[FateSystem] 命运配置为空，使用默认值");
                _tierConfigs = CreateDefaultTiers();
            }
            GameLogger.Log("Fate", $"初始化完成，加载 {_tierConfigs.Count} 个档次");
        }

        /// <summary>
        /// 获取所有档次配置
        /// </summary>
        public List<RitualTierData> GetTierConfigs()
        {
            return _tierConfigs ?? new List<RitualTierData>();
        }

        /// <summary>
        /// 根据档次名称获取配置
        /// </summary>
        public RitualTierData GetTierConfig(string tierName)
        {
            return _tierConfigs?.Find(t => t.tierName == tierName);
        }

        /// <summary>
        /// 检查玩家是否能负担某档次的猫币
        /// </summary>
        public bool CanAffordTier(string tierName)
        {
            var tier = GetTierConfig(tierName);
            if (tier == null) return false;
            if (tier.cost <= 0) return true;

            var currencyMgr = GameManager.Instance?.CurrencyManager;
            if (currencyMgr == null) return true;
            return currencyMgr.GetCurrencyAmount(CurrencyType.Gold) >= tier.cost;
        }

        /// <summary>
        /// 生成某个档次的3个祝福选项
        /// </summary>
        public List<FateBlessingOption> GenerateBlessings(string tierName)
        {
            var results = new List<FateBlessingOption>();
            var tier = GetTierConfig(tierName);
            if (tier == null || tier.blessings == null || tier.blessings.Count == 0)
                return results;

            int drawCount = tier.drawCount > 0 ? tier.drawCount : 3;
            var pool = new List<RitualBlessingData>(tier.blessings);
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < drawCount && usedIndices.Count < pool.Count; i++)
            {
                var blessing = WeightedRandomPick(pool, usedIndices);
                if (blessing == null) break;
                results.Add(ResolveBlessing(blessing, i));
            }

            return results;
        }

        /// <summary>
        /// 扣除档次费用
        /// </summary>
        public bool TrySpendTierCost(string tierName)
        {
            var tier = GetTierConfig(tierName);
            if (tier == null || tier.cost <= 0) return true;

            var currencyMgr = GameManager.Instance?.CurrencyManager;
            if (currencyMgr == null) return true;
            return currencyMgr.TrySpendCurrency(CurrencyType.Gold, tier.cost);
        }

        /// <summary>
        /// 应用选中的祝福效果
        /// </summary>
        public void ApplyBlessing(FateBlessingOption option)
        {
            if (option == null) return;

            GameLogger.Log("Fate", $"应用祝福: {option.displayName} type={option.type}");

            switch (option.type)
            {
                case "LeaderStatBoostTemporary":
                    ApplyStatBuffToAllUnits(option.statType, option.isPercent, option.value, BuffPersistence.TemporaryRoundBased, 3);
                    break;
                case "LeaderStatBoostPermanent":
                    ApplyStatBuffToAllUnits(option.statType, option.isPercent, option.value, BuffPersistence.Persistent, -1);
                    AddChoiceToPlayerData(option);
                    break;
                case "LeaderStatBoostPercent":
                    ApplyStatBuffToAllUnits(option.statType, true, option.value, BuffPersistence.Persistent, -1);
                    AddChoiceToPlayerData(option);
                    break;
                case "Consumable":
                    if (option.consumable != null)
                    {
                        GameManager.Instance?.DataManager?.AddConsumable(option.consumable);
                    }
                    break;
                case "CatFood":
                    GameManager.Instance?.DataManager?.AddCatFood(option.catFoodAmount);
                    break;
            }
        }

        /// <summary>
        /// 所有档次均额外获得100猫币保底
        /// </summary>
        public void ApplyGuaranteedCatFood()
        {
            GameManager.Instance?.DataManager?.AddCatFood(100);
            GameLogger.Log("Fate", "保底猫币 +100");
        }

        // ── 内部方法 ──

        private void ApplyStatBuffToAllUnits(StatType stat, bool isPercent, float value, BuffPersistence persistence, int rounds)
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            string choiceId = $"fate_{stat}_{DateTime.UtcNow.Ticks % 100000}";
            var choice = new GameChoice
            {
                choiceId = choiceId,
                displayName = $"命运-{stat}",
                description = $"{(isPercent ? $"{value * 100:F0}%" : $"+{value}")}{stat}",
                category = (int)ChoiceCategory.Buff,
                buffApplyType = (int)BuffApplyType.Aura,
                buffScopeFilter = "all",
                targetTribeType = (int)TribeType.None,
                buffEffects = new List<BuffEffectItem>
                {
                    new BuffEffectItem
                    {
                        statType = stat.ToString(),
                        isPercent = isPercent,
                        value = value,
                        gameEffectType = (int)GameEffect.AttackPercent,
                    }
                }
            };

            dataManager.PlayerData.runChoices.Add(choice);
            dataManager.RebuildAllBuffs();
            dataManager.SavePlayerData();
        }

        private void AddChoiceToPlayerData(FateBlessingOption option)
        {
            var dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var choice = new GameChoice
            {
                choiceId = $"fate_perm_{DateTime.UtcNow.Ticks % 100000}",
                displayName = option.displayName,
                description = option.description,
                category = (int)ChoiceCategory.Buff,
                buffApplyType = (int)BuffApplyType.Aura,
                buffScopeFilter = "all",
                buffEffects = new List<BuffEffectItem>
                {
                    new BuffEffectItem
                    {
                        statType = option.statType.ToString(),
                        isPercent = option.isPercent,
                        value = option.value,
                        gameEffectType = (int)GameEffect.AttackPercent,
                    }
                }
            };

            dataManager.PlayerData.runChoices.Add(choice);
            dataManager.SavePlayerData();
        }

        private FateBlessingOption ResolveBlessing(RitualBlessingData data, int index)
        {
            var option = new FateBlessingOption
            {
                id = $"blessing_{index}",
                type = data.type,
            };

            switch (data.type)
            {
                case "LeaderStatBoostTemporary":
                    option.statType = RollStatType(data.statTypes);
                    option.isPercent = false;
                    option.value = RollInt(data.minAmount, data.maxAmount);
                    option.displayName = $"临时+{option.value}{option.statType}";
                    option.description = $"3回合内{option.statType}+{option.value}";
                    option.iconColor = "#8BC34A";
                    break;

                case "LeaderStatBoostPermanent":
                    option.statType = RollStatType(data.statTypes);
                    option.isPercent = false;
                    option.value = RollInt(data.minAmount, data.maxAmount);
                    option.displayName = $"永久+{option.value}{option.statType}";
                    option.description = $"所有单位{option.statType}永久+{option.value}";
                    option.iconColor = "#2196F3";
                    break;

                case "LeaderStatBoostPercent":
                    option.statType = RollStatType(data.statTypes);
                    option.isPercent = true;
                    option.value = RollFloat(data.minPercent, data.maxPercent);
                    option.displayName = $"永久+{option.value * 100:F0}%{option.statType}";
                    option.description = $"所有单位{option.statType}永久+{option.value * 100:F0}%";
                    option.iconColor = "#FF9800";
                    break;

                case "Consumable":
                    int count = RollInt(data.minCount, data.maxCount);
                    option.consumable = GenerateConsumable(count);
                    option.displayName = $"消耗品×{count}";
                    option.description = $"获得{count}个消耗品";
                    option.iconColor = "#9C27B0";
                    break;

                case "CatFood":
                    option.catFoodAmount = RollInt(data.minAmount, data.maxAmount);
                    option.displayName = $"+{option.catFoodAmount}猫币";
                    option.description = $"获得{option.catFoodAmount}小鱼干";
                    option.iconColor = "#4CAF50";
                    break;

                default:
                    option.displayName = "未知祝福";
                    option.description = "该祝福类型未实现";
                    option.iconColor = "#999999";
                    break;
            }

            return option;
        }

        private StatType RollStatType(List<string> statTypes)
        {
            if (statTypes == null || statTypes.Count == 0) return StatType.Attack;
            string pick = statTypes[_rng.Next(statTypes.Count)];
            if (Enum.TryParse<StatType>(pick, out var result))
                return result;
            return StatType.Attack;
        }

        private int RollInt(int min, int max)
        {
            if (max <= min) return min;
            return _rng.Next(min, max + 1);
        }

        private float RollFloat(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }

        private RitualBlessingData WeightedRandomPick(List<RitualBlessingData> pool, HashSet<int> used)
        {
            int totalWeight = 0;
            for (int i = 0; i < pool.Count; i++)
                if (!used.Contains(i)) totalWeight += pool[i].weight;

            if (totalWeight <= 0) return null;

            int roll = _rng.Next(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (used.Contains(i)) continue;
                cumulative += pool[i].weight;
                if (roll < cumulative)
                {
                    used.Add(i);
                    return pool[i];
                }
            }

            for (int i = 0; i < pool.Count; i++)
                if (!used.Contains(i)) { used.Add(i); return pool[i]; }
            return null;
        }

        private ConsumableItem GenerateConsumable(int count)
        {
            var types = new[] { "HealPotion", "AttackBuff", "DefenseBuff", "Bomb", "FreezeTrap" };
            string pick = types[_rng.Next(types.Length)];
            int effectType = 0;
            float value = 0;
            switch (pick)
            {
                case "HealPotion": effectType = 0; value = 50; break;
                case "AttackBuff": effectType = 1; value = 0.3f; break;
                case "DefenseBuff": effectType = 2; value = 0.3f; break;
                case "Bomb": effectType = 3; value = 200; break;
                case "FreezeTrap": effectType = 4; value = 3; break;
            }

            return new ConsumableItem
            {
                id = _rng.Next(100000, 999999),
                name = pick,
                effectType = effectType,
                value = value,
            };
        }

        private List<RitualTierData> CreateDefaultTiers()
        {
            return new List<RitualTierData>
            {
                new RitualTierData
                {
                    tierName = "free", displayName = "免费祈愿", cost = 0, drawCount = 3,
                    blessings = new List<RitualBlessingData>
                    {
                        new RitualBlessingData { type = "CatFood", weight = 50, minAmount = 50, maxAmount = 150 },
                        new RitualBlessingData { type = "LeaderStatBoostTemporary", weight = 30, statTypes = new List<string>{ "Attack","Defense" }, minAmount = 5, maxAmount = 15 },
                        new RitualBlessingData { type = "Consumable", weight = 20, minCount = 1, maxCount = 1 },
                    }
                },
                new RitualTierData
                {
                    tierName = "low", displayName = "普通祈愿", cost = 300, drawCount = 3,
                    blessings = new List<RitualBlessingData>
                    {
                        new RitualBlessingData { type = "LeaderStatBoostPermanent", weight = 40, statTypes = new List<string>{ "Attack","Defense","Hp" }, minAmount = 10, maxAmount = 30 },
                        new RitualBlessingData { type = "CatFood", weight = 35, minAmount = 100, maxAmount = 300 },
                        new RitualBlessingData { type = "Consumable", weight = 25, minCount = 1, maxCount = 2 },
                    }
                },
                new RitualTierData
                {
                    tierName = "high", displayName = "盛大祈愿", cost = 600, drawCount = 3,
                    blessings = new List<RitualBlessingData>
                    {
                        new RitualBlessingData { type = "LeaderStatBoostPercent", weight = 50, statTypes = new List<string>{ "Attack","Defense","Hp" }, minPercent = 0.05f, maxPercent = 0.15f },
                        new RitualBlessingData { type = "CatFood", weight = 50, minAmount = 300, maxAmount = 800 },
                    }
                }
            };
        }
    }
}
