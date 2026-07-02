using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    public enum ShopItemType
    {
        Artifact = 0,
        Consumable = 1,
        Fighter = 2
    }

    public class ShopItem
    {
        public ShopItemType type;
        public string id;
        public string name;
        public string description;
        public int price;
        public int sellPrice;
        public bool sold;
        public FighterConfig fighterConfig;
        public List<BuffEffectItem> effects;
        public string subType;
    }

    public class ShopSystem
    {
        private static readonly System.Random _rng = new System.Random();
        private ShopConfigData _config;
        private List<ShopItem> _currentItems;

        public void Initialize()
        {
            _config = TribeConfigLoader.Instance != null ? TribeConfigLoader.Instance.GetShopConfig() : null;
            if (_config == null)
            {
                Debug.LogWarning("[ShopSystem] 商店配置为空，使用默认值");
                _config = CreateDefaultConfig();
            }
            GameLogger.Log("Shop", "初始化完成");
        }

        public List<ShopItem> GetCurrentItems()
        {
            if (_currentItems == null || _currentItems.Count == 0)
                _currentItems = GenerateInventory();
            return _currentItems;
        }

        public List<ShopItem> GenerateInventory()
        {
            var items = new List<ShopItem>();
            int slotCount = _config.slotCount > 0 ? _config.slotCount : 4;
            var usedIds = new HashSet<string>();
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            float modifier = dm != null ? dm.GetShopPriceModifier() : 1.0f;

            for (int i = 0; i < slotCount; i++)
            {
                float roll = (float)_rng.NextDouble();
                ShopItem item;
                if (roll < 0.3f)
                {
                    item = GenerateArtifactItem(usedIds);
                    if (item == null) item = GenerateFighterItem(usedIds);
                }
                else if (roll < 0.7f)
                {
                    item = GenerateConsumableItem(usedIds);
                }
                else
                {
                    item = GenerateFighterItem(usedIds);
                }
                if (item != null)
                {
                    item.price = Mathf.RoundToInt(item.price * modifier);
                    items.Add(item);
                }
            }
            _currentItems = items;
            return items;
        }

        public int GetRefreshCost()
        {
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            if (dm == null) return _config.baseRefreshCost;
            int count = dm.GetShopRefreshCount();
            int cost = _config.baseRefreshCost + count * _config.refreshIncrement;
            float modifier = dm.GetShopPriceModifier();
            return Mathf.RoundToInt(cost * modifier);
        }

        public bool CanRefresh()
        {
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            if (dm == null) return false;
            if (dm.IsShopRefreshLocked()) return false;
            int cost = GetRefreshCost();
            var currencyMgr = GameManager.Instance != null ? GameManager.Instance.CurrencyManager : null;
            if (currencyMgr == null) return true;
            return currencyMgr.GetCurrencyAmount(CurrencyType.Gold) >= cost;
        }

        public bool TryRefresh()
        {
            if (!CanRefresh()) return false;
            var currencyMgr = GameManager.Instance != null ? GameManager.Instance.CurrencyManager : null;
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            if (currencyMgr == null || dm == null) return false;
            int cost = GetRefreshCost();
            if (!currencyMgr.TrySpendCurrency(CurrencyType.Gold, cost)) return false;
            dm.IncrementShopRefreshCount();
            _currentItems = GenerateInventory();
            return true;
        }

        public bool TryBuyItem(int slotIndex)
        {
            if (_currentItems == null || slotIndex < 0 || slotIndex >= _currentItems.Count) return false;
            var item = _currentItems[slotIndex];
            if (item.sold) return false;
            var currencyMgr = GameManager.Instance != null ? GameManager.Instance.CurrencyManager : null;
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            if (currencyMgr == null || dm == null) return false;
            if (!currencyMgr.TrySpendCurrency(CurrencyType.Gold, item.price)) return false;
            item.sold = true;

            if (item.type == ShopItemType.Artifact)
            {
                dm.PlayerData.runEquipments.Add(new EquipmentRecord
                {
                    equipmentId = item.id,
                    displayName = item.name,
                    description = item.description,
                    buffApplyType = (int)BuffApplyType.Aura,
                    buffScopeText = "all",
                    effects = item.effects
                });
                dm.RebuildAllBuffs();
            }
            else if (item.type == ShopItemType.Consumable)
            {
                dm.AddConsumable(new ConsumableItem { id = item.id.GetHashCode(), name = item.name, effectType = 0, value = 0 });
            }
            else if (item.type == ShopItemType.Fighter && item.fighterConfig != null)
            {
                var fighter = new FighterData
                {
                    fighterId = item.fighterConfig.fighterId,
                    tribeType = item.fighterConfig.tribeType,
                    tier = item.fighterConfig.tier,
                    name = item.fighterConfig.fighterName,
                    currentHp = item.fighterConfig.hp,
                    zone = (int)UnitZone.Standby,
                    rarity = item.fighterConfig.rarity,
                    enhanceLevel = 0
                };
                var tribes = dm.GetTribes();
                TribeRecord targetTribe = null;
                foreach (var tribe in tribes)
                {
                    if (tribe.tribeType == fighter.tribeType) { targetTribe = tribe; break; }
                }
                if (targetTribe == null)
                {
                    targetTribe = new TribeRecord { tribeType = fighter.tribeType, isActive = true };
                    dm.AddTribe(targetTribe);
                }
                targetTribe.units.Add(fighter);
            }

            dm.SavePlayerData();
            GameLogger.Log("Shop", "购买: " + item.name + " 价格=" + item.price);
            return true;
        }

        public bool TrySellUnit(FighterData unit)
        {
            if (unit == null) return false;
            int price = GetSellPrice(unit);
            if (price <= 0) return false;
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            var currencyMgr = GameManager.Instance != null ? GameManager.Instance.CurrencyManager : null;
            if (dm == null) return false;
            var tribes = dm.GetTribes();
            foreach (var tribe in tribes)
            {
                if (tribe != null && tribe.units != null && tribe.units.Remove(unit))
                {
                    if (currencyMgr != null) currencyMgr.AddCurrency(CurrencyType.Gold, price);
                    dm.SavePlayerData();
                    GameLogger.Log("Shop", "出售兵种: " + unit.name + " 获得=" + price);
                    return true;
                }
            }
            return false;
        }

        public int GetSellPrice(FighterData unit)
        {
            if (unit == null) return 0;
            var config = TribeConfigLoader.Instance != null ? TribeConfigLoader.Instance.GetFighterConfig(unit.fighterId) : null;
            if (config == null) return 50;
            var catConfig = _config.catConfig;
            int basePrice = 100;
            if (catConfig != null && catConfig.basePrices != null && catConfig.basePrices.ContainsKey(config.tribeType))
                basePrice = catConfig.basePrices[config.tribeType];
            float qualityMult = 1.0f;
            if (catConfig != null && catConfig.qualityMultipliers != null && catConfig.qualityMultipliers.ContainsKey(config.rarity))
                qualityMult = catConfig.qualityMultipliers[config.rarity];
            float sellRatio = catConfig != null ? catConfig.sellRatio : 0.5f;
            return Mathf.RoundToInt(basePrice * qualityMult * sellRatio);
        }

        public void ResetForNewRound()
        {
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            if (dm == null) return;
            dm.SetShopRefreshCount(0, false);
            dm.SetShopPriceModifier(1.0f, false);
            dm.SetShopRefreshLocked(false, false);
            dm.SavePlayerData();
            _currentItems = null;
        }

        private ShopItem GenerateArtifactItem(HashSet<string> usedIds)
        {
            var artifacts = TribeConfigLoader.Instance != null ? TribeConfigLoader.Instance.GetAllArtifacts() : null;
            if (artifacts == null || artifacts.Count == 0) return null;
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            var owned = new HashSet<string>();
            if (dm != null && dm.PlayerData != null && dm.PlayerData.runEquipments != null)
                foreach (var eq in dm.PlayerData.runEquipments) owned.Add(eq.equipmentId);
            var available = new List<ArtifactConfig>();
            foreach (var art in artifacts)
                if (!usedIds.Contains(art.id) && !owned.Contains(art.id)) available.Add(art);
            if (available.Count == 0) return null;
            var config = available[_rng.Next(available.Count)];
            return new ShopItem
            {
                type = ShopItemType.Artifact,
                id = config.id,
                name = config.name,
                description = config.description,
                price = _config.artifactConfig != null ? _config.artifactConfig.basePrice : 500,
                effects = config.effects,
                subType = config.subType,
            };
        }

        private ShopItem GenerateConsumableItem(HashSet<string> usedIds)
        {
            string[] names = { "炸弹", "冰冻陷阱", "回复药水", "攻击强化", "防御强化" };
            string[] descs = { "对所有敌人造成200点伤害", "所有敌人停止攻击3秒", "回复所有己方单位50%生命值", "己方攻击力+30%，持续15秒", "己方防御力+30%，持续15秒" };
            int idx = _rng.Next(names.Length);
            int priceRange = _config.consumableConfig.basePriceMax - _config.consumableConfig.basePriceMin;
            int price = _config.consumableConfig.basePriceMin + (priceRange > 0 ? _rng.Next(priceRange) : 0);
            return new ShopItem
            {
                type = ShopItemType.Consumable,
                id = "consumable_" + idx,
                name = names[idx],
                description = descs[idx],
                price = price,
                effects = new List<BuffEffectItem>(),
            };
        }

        private ShopItem GenerateFighterItem(HashSet<string> usedIds)
        {
            var loader = TribeConfigLoader.Instance;
            if (loader == null) return null;
            int region = GameFlowController.Instance != null ? GameFlowController.Instance.CurrentRegion : 1;
            var rarity = RollShopFighterRarity(region);
            var fighters = loader.GetFightersByRarity(rarity);
            if (fighters == null || fighters.Count == 0) return null;
            var dm = GameManager.Instance != null ? GameManager.Instance.DataManager : null;
            var owned = new HashSet<int>();
            if (dm != null)
            {
                foreach (var tribe in dm.GetTribes())
                    if (tribe != null && tribe.units != null)
                        foreach (var unit in tribe.units) owned.Add(unit.fighterId);
            }
            var available = new List<FighterConfig>();
            foreach (var f in fighters)
                if (f.tribeType > 0 && f.tier > 0 && !owned.Contains(f.fighterId)) available.Add(f);
            if (available.Count == 0) return null;
            var cfg = available[_rng.Next(available.Count)];
            float rarityMult = 1f + cfg.rarity * 0.3f;
            int price = Mathf.RoundToInt(100 * (1 + cfg.tier * 0.5f) * rarityMult);
            return new ShopItem
            {
                type = ShopItemType.Fighter,
                id = "fighter_" + cfg.fighterId,
                name = cfg.fighterName,
                description = "品质: " + ((Rarity)cfg.rarity).ToString(),
                price = price,
                fighterConfig = cfg,
            };
        }

        private Rarity RollShopFighterRarity(int region)
        {
            var regionConfig = TribeConfigLoader.Instance != null ? TribeConfigLoader.Instance.GetRegionRarityConfig(region) : null;
            if (regionConfig == null || regionConfig.rates == null) return Rarity.Normal;
            double roll = _rng.NextDouble();
            double cumulative = 0;
            foreach (var entry in regionConfig.rates)
            {
                cumulative += entry.spawnRate;
                if (roll < cumulative) return (Rarity)entry.rarity;
            }
            return Rarity.Normal;
        }

        private ShopConfigData CreateDefaultConfig()
        {
            return new ShopConfigData
            {
                baseRefreshCost = 50,
                refreshIncrement = 50,
                slotCount = 4,
                shopInterval = 5,
                startRound = 5,
                artifactConfig = new ArtifactShopConfig { basePrice = 500, effects = new Dictionary<string, int>(), icons = new Dictionary<string, string>() },
                consumableConfig = new ConsumableShopConfig { basePriceMin = 50, basePriceMax = 100, icons = new Dictionary<string, string>() },
                catConfig = new CatShopConfig
                {
                    basePrices = new Dictionary<int, int> { { 1, 100 }, { 2, 100 }, { 3, 100 }, { 4, 100 } },
                    qualityMultipliers = new Dictionary<int, float> { { 0, 1.0f }, { 1, 1.3f }, { 2, 1.6f } },
                    priceVariation = 0.5f,
                    sellRatio = 0.5f,
                    tribeIcons = new Dictionary<int, List<string>>(),
                }
            };
        }
    }
}
