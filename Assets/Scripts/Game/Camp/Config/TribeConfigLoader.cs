using UnityEngine;
using System.Collections.Generic;
using System.IO;
using LitJson;

namespace Camp
{
    /// <summary>
    /// 配置加载器单例 — 从 StreamingAssets/Tables/ 加载所有 JSON 配置
    /// </summary>
    public class TribeConfigLoader
    {
        private static TribeConfigLoader _instance;
        public static TribeConfigLoader Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TribeConfigLoader();
                return _instance;
            }
        }

        // 配置表
        private Dictionary<int, FighterConfig> _fighters = new Dictionary<int, FighterConfig>();
        private Dictionary<int, BuffConfig> _buffs = new Dictionary<int, BuffConfig>();
        private Dictionary<int, TribeConfig> _tribes = new Dictionary<int, TribeConfig>();
        private List<AffixData> _affixes = new List<AffixData>();
        private Dictionary<string, RelicConfig> _relics = new Dictionary<string, RelicConfig>();
        private RaritySpawnConfig _raritySpawnConfig;
        private List<RitualTierData> _ritualTiers;
        private ShopConfigData _shopConfig;
        private Dictionary<string, ArtifactConfig> _artifacts;

        public bool IsLoaded { get; private set; }

        /// <summary>
        /// 加载所有配置文件
        /// </summary>
        public void LoadAllConfigs()
        {
            LoadFighterConfig();
            LoadBuffConfig();
            LoadTribeConfig();
            LoadAffixConfig();
            LoadRaritySpawnConfig();
            LoadRelicConfig();
            LoadRitualConfig();
            LoadShopConfig();
            LoadArtifactConfig();
            IsLoaded = true;
            Debug.Log("[TribeConfigLoader] 所有配置加载完成");
        }

        // ── 查询方法 ──

        public FighterConfig GetFighterConfig(int fighterId)
        {
            _fighters.TryGetValue(fighterId, out var config);
            return config;
        }

        public BuffConfig GetBuffConfig(int buffId)
        {
            _buffs.TryGetValue(buffId, out var config);
            return config;
        }

        public TribeConfig GetTribeConfig(TribeType tribeType)
        {
            _tribes.TryGetValue((int)tribeType, out var config);
            return config;
        }

        public TribeConfig GetTribeConfig(int tribeType)
        {
            _tribes.TryGetValue(tribeType, out var config);
            return config;
        }

        public List<AffixData> GetAffixesForFighter(int fighterId)
        {
            var results = new List<AffixData>();
            foreach (var affix in _affixes)
            {
                if (affix.fighterId == fighterId || affix.fighterId == 0)
                    results.Add(affix);
            }
            return results;
        }

        public List<AffixData> GetAffixesByTier(string tier)
        {
            var results = new List<AffixData>();
            foreach (var affix in _affixes)
            {
                if (affix.tier == tier)
                    results.Add(affix);
            }
            return results;
        }

        public List<TribeConfig> GetAllTribes()
        {
            return new List<TribeConfig>(_tribes.Values);
        }

        public List<FighterConfig> GetFightersByRarity(Rarity rarity)
        {
            var results = new List<FighterConfig>();
            foreach (var cfg in _fighters.Values)
            {
                if ((Rarity)cfg.rarity == rarity)
                    results.Add(cfg);
            }
            return results;
        }

        public List<FighterConfig> GetAllFighterConfigs()
        {
            return new List<FighterConfig>(_fighters.Values);
        }

        public RegionRarityConfig GetRegionRarityConfig(int regionId)
        {
            if (_raritySpawnConfig == null || _raritySpawnConfig.regions == null) return null;
            foreach (var region in _raritySpawnConfig.regions)
            {
                if (region.regionId == regionId)
                    return region;
            }
            return null;
        }

        public RelicConfig GetRelicConfig(string relicId)
        {
            _relics.TryGetValue(relicId, out var config);
            return config;
        }

        public List<RelicConfig> GetRelicsByRarity(int rarity)
        {
            var results = new List<RelicConfig>();
            foreach (var cfg in _relics.Values)
            {
                if (cfg.rarity == rarity)
                    results.Add(cfg);
            }
            return results;
        }

        public List<RelicConfig> GetBossRelics()
        {
            return GetRelicsByRarity(3);
        }

        // ── 命运/祈福 查询 ──

        public List<RitualTierData> GetRitualTiers()
        {
            return _ritualTiers ?? new List<RitualTierData>();
        }

        // ── 商店 查询 ──

        public ShopConfigData GetShopConfig()
        {
            return _shopConfig;
        }

        // ── 奇物 查询 ──

        public ArtifactConfig GetArtifact(string artifactId)
        {
            if (_artifacts == null || string.IsNullOrEmpty(artifactId)) return null;
            _artifacts.TryGetValue(artifactId, out var config);
            return config;
        }

        public List<ArtifactConfig> GetAllArtifacts()
        {
            return _artifacts != null ? new List<ArtifactConfig>(_artifacts.Values) : new List<ArtifactConfig>();
        }

        // ── 加载方法 ──

        private void LoadFighterConfig()
        {
            string json = ReadConfigFile("fighter_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);
            var array = data.ContainsKey("fighters") ? data["fighters"] : null;
            if (array == null) return;

            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                var config = new FighterConfig
                {
                    fighterId = ReadInt(item, "fighterId"),
                    fighterName = ReadString(item, "fighterName"),
                    tribeType = ReadInt(item, "tribeType"),
                    tier = ReadInt(item, "tier"),
                    attack = ReadInt(item, "attack"),
                    defense = ReadInt(item, "defense"),
                    hp = ReadInt(item, "hp"),
                    moveSpeed = ReadFloat(item, "moveSpeed"),
                    attackSpeed = ReadFloat(item, "attackSpeed"),
                    attackRange = ReadFloat(item, "attackRange"),
                    avatarId = ReadString(item, "avatarId"),
                    populationCost = item.ContainsKey("populationCost") ? (int)item["populationCost"] : 1,
                    deployZones = item.ContainsKey("deployZones") ? (int)item["deployZones"] : 1,
                    rarity = item.ContainsKey("rarity") ? ReadInt(item, "rarity") : 0,
                    enhanceLevel = item.ContainsKey("enhanceLevel") ? ReadInt(item, "enhanceLevel") : 0,
                    passiveSkillId = item.ContainsKey("passiveSkillId") ? ReadInt(item, "passiveSkillId") : 0,
                    weightClass = item.ContainsKey("weightClass") ? ReadString(item, "weightClass") : "",
                    skillIdOriginal = item.ContainsKey("skillIdOriginal") ? ReadString(item, "skillIdOriginal") : "",
                    skillIdEnhanced = item.ContainsKey("skillIdEnhanced") ? ReadString(item, "skillIdEnhanced") : "",
                    skillDescriptionOriginal = item.ContainsKey("skillDescriptionOriginal") ? ReadString(item, "skillDescriptionOriginal") : "",
                    skillDescriptionEnhanced = item.ContainsKey("skillDescriptionEnhanced") ? ReadString(item, "skillDescriptionEnhanced") : "",
                    targetPriority = item.ContainsKey("targetPriority") ? ReadString(item, "targetPriority") : "nearest",
                };

                // innateBuffIds
                var buffArray = item.ContainsKey("innateBuffIds") ? item["innateBuffIds"] : null;
                if (buffArray != null && buffArray.IsArray)
                {
                    config.innateBuffIds = new List<int>();
                    for (int j = 0; j < buffArray.Count; j++)
                        config.innateBuffIds.Add((int)buffArray[j]);
                }
                else
                {
                    config.innateBuffIds = new List<int>();
                }

                // tags
                var tagsArray = item.ContainsKey("tags") ? item["tags"] : null;
                if (tagsArray != null && tagsArray.IsArray)
                {
                    config.tags = new List<string>();
                    for (int j = 0; j < tagsArray.Count; j++)
                        config.tags.Add((string)tagsArray[j]);
                }
                else
                {
                    config.tags = new List<string>();
                }

                // mechanismTags (列表)
                var mechArray = item.ContainsKey("mechanismTags") ? item["mechanismTags"] : null;
                if (mechArray != null && mechArray.IsArray)
                {
                    config.mechanismTags = new List<string>();
                    for (int j = 0; j < mechArray.Count; j++)
                        config.mechanismTags.Add((string)mechArray[j]);
                }
                else
                {
                    // 兼容旧字段 mechanismTag (string)
                    string oldMech = item.ContainsKey("mechanismTag") ? ReadString(item, "mechanismTag") : "";
                    config.mechanismTags = string.IsNullOrEmpty(oldMech) ? new List<string>() : new List<string> { oldMech };
                }

                // typeTags (列表)
                var typeArray = item.ContainsKey("typeTags") ? item["typeTags"] : null;
                if (typeArray != null && typeArray.IsArray)
                {
                    config.typeTags = new List<string>();
                    for (int j = 0; j < typeArray.Count; j++)
                        config.typeTags.Add((string)typeArray[j]);
                }
                else
                {
                    config.typeTags = new List<string>();
                }

                // enhanceStatModifiers (列表)
                var enhArray = item.ContainsKey("enhanceStatModifiers") ? item["enhanceStatModifiers"] : null;
                if (enhArray != null && enhArray.IsArray)
                {
                    config.enhanceStatModifiers = new List<EnhanceStatModifier>();
                    for (int j = 0; j < enhArray.Count; j++)
                    {
                        var enh = enhArray[j];
                        config.enhanceStatModifiers.Add(new EnhanceStatModifier
                        {
                            statType = ReadString(enh, "statType"),
                            isPercent = ReadBool(enh, "isPercent", false),
                            value = ReadFloat(enh, "value"),
                        });
                    }
                }
                else
                {
                    config.enhanceStatModifiers = new List<EnhanceStatModifier>();
                }

                _fighters[config.fighterId] = config;
            }

            Debug.Log($"[TribeConfigLoader] 加载 { _fighters.Count} 个 fighter 配置");
        }

        private void LoadBuffConfig()
        {
            string json = ReadConfigFile("buff_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);
            var array = data.ContainsKey("buffs") ? data["buffs"] : null;
            if (array == null) return;

            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                var config = new BuffConfig
                {
                    buffId = ReadInt(item, "buffId"),
                    buffName = ReadString(item, "buffName"),
                    description = ReadString(item, "description"),
                    gameEffectType = ReadInt(item, "gameEffectType"),
                    effectParam1 = ReadFloat(item, "effectParam1"),
                    effectParam2 = ReadFloat(item, "effectParam2"),
                    duration = ReadFloat(item, "duration"),
                    visible = ReadBool(item, "visible", true),
                    iconColorIndex = ReadInt(item, "iconColorIndex"),
                };

                // buffEffects
                var effectsArray = item.ContainsKey("buffEffects") ? item["buffEffects"] : null;
                if (effectsArray != null && effectsArray.IsArray)
                {
                    config.buffEffects = new List<BuffEffectItem>();
                    for (int j = 0; j < effectsArray.Count; j++)
                    {
                        var eff = effectsArray[j];
                        config.buffEffects.Add(new BuffEffectItem
                        {
                            statType = ReadString(eff, "statType"),
                            isPercent = ReadBool(eff, "isPercent", false),
                            value = ReadFloat(eff, "value"),
                            gameEffectType = ReadInt(eff, "gameEffectType"),
                        });
                    }
                }

                _buffs[config.buffId] = config;
            }

            Debug.Log($"[TribeConfigLoader] 加载 { _buffs.Count} 个 buff 配置");
        }

        private void LoadTribeConfig()
        {
            string json = ReadConfigFile("tribe_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);
            var array = data.ContainsKey("tribes") ? data["tribes"] : null;
            if (array == null) return;

            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                var config = new TribeConfig
                {
                    tribeType = ReadInt(item, "tribeType"),
                    tribeName = ReadString(item, "tribeName"),
                    description = ReadString(item, "description"),
                    deployCostPerCat = ReadInt(item, "deployCostPerCat"),
                    leaderFighterId = ReadInt(item, "leaderFighterId"),
                };

                // unitTypes
                var typesArray = item.ContainsKey("unitTypes") ? item["unitTypes"] : null;
                if (typesArray != null && typesArray.IsArray)
                {
                    config.unitTypes = new List<TribeUnitType>();
                    for (int j = 0; j < typesArray.Count; j++)
                    {
                        config.unitTypes.Add(new TribeUnitType
                        {
                            tier = ReadInt(typesArray[j], "tier"),
                            fighterId = ReadInt(typesArray[j], "fighterId"),
                        });
                    }
                }

                _tribes[config.tribeType] = config;
            }

            Debug.Log($"[TribeConfigLoader] 加载 { _tribes.Count} 个族群配置");
        }

        private void LoadAffixConfig()
        {
            string json = ReadConfigFile("affix_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);
            var array = data.ContainsKey("affixes") ? data["affixes"] : null;
            if (array == null) return;

            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                var affix = new AffixData
                {
                    affixId = ReadString(item, "affixId"),
                    displayName = ReadString(item, "displayName"),
                    fighterId = ReadInt(item, "fighterId"),
                    tier = ReadString(item, "tier"),
                    weight = ReadInt(item, "weight"),
                };

                var buffArray = item.ContainsKey("buffIds") ? item["buffIds"] : null;
                if (buffArray != null && buffArray.IsArray)
                {
                    affix.buffIds = new List<int>();
                    for (int j = 0; j < buffArray.Count; j++)
                        affix.buffIds.Add((int)buffArray[j]);
                }
                else
                {
                    affix.buffIds = new List<int>();
                }

                _affixes.Add(affix);
            }

            Debug.Log($"[TribeConfigLoader] 加载 { _affixes.Count} 个词缀配置");
        }

        private void LoadRaritySpawnConfig()
        {
            string json = ReadConfigFile("rarity_spawn_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);
            var array = data.ContainsKey("regions") ? data["regions"] : null;
            if (array == null) return;

            _raritySpawnConfig = new RaritySpawnConfig { regions = new List<RegionRarityConfig>() };

            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                var region = new RegionRarityConfig
                {
                    regionId = ReadInt(item, "regionId"),
                    rates = new List<RaritySpawnEntry>()
                };

                var ratesArray = item.ContainsKey("rates") ? item["rates"] : null;
                if (ratesArray != null && ratesArray.IsArray)
                {
                    for (int j = 0; j < ratesArray.Count; j++)
                    {
                        region.rates.Add(new RaritySpawnEntry
                        {
                            rarity = ReadInt(ratesArray[j], "rarity"),
                            spawnRate = ReadFloat(ratesArray[j], "spawnRate"),
                            bornEnhanceRate = ReadFloat(ratesArray[j], "bornEnhanceRate")
                        });
                    }
                }

                _raritySpawnConfig.regions.Add(region);
            }

            Debug.Log($"[TribeConfigLoader] 加载 {_raritySpawnConfig.regions.Count} 个区域稀有度配置");
        }

        private void LoadRelicConfig()
        {
            string json = ReadConfigFile("relic_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);
            var array = data.ContainsKey("relics") ? data["relics"] : null;
            if (array == null) return;

            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                var config = new RelicConfig
                {
                    relicId = ReadString(item, "relicId"),
                    name = ReadString(item, "name"),
                    description = ReadString(item, "description"),
                    rarity = ReadInt(item, "rarity"),
                    mechanismTag = ReadString(item, "mechanismTag"),
                    isBossRelic = ReadBool(item, "isBossRelic", false),
                };

                // effects
                var effectsArray = item.ContainsKey("effects") ? item["effects"] : null;
                if (effectsArray != null && effectsArray.IsArray)
                {
                    config.effects = new List<BuffEffectItem>();
                    for (int j = 0; j < effectsArray.Count; j++)
                    {
                        var eff = effectsArray[j];
                        config.effects.Add(new BuffEffectItem
                        {
                            statType = ReadString(eff, "statType"),
                            isPercent = ReadBool(eff, "isPercent", false),
                            value = ReadFloat(eff, "value"),
                            gameEffectType = ReadInt(eff, "gameEffectType"),
                        });
                    }
                }

                _relics[config.relicId] = config;
            }

            Debug.Log($"[TribeConfigLoader] 加载 {_relics.Count} 个圣物配置");
        }

        private void LoadRitualConfig()
        {
            string json = ReadConfigFile("ritual_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);
            var array = data.ContainsKey("tiers") ? data["tiers"] : null;
            if (array == null) return;

            _ritualTiers = new List<RitualTierData>();
            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                var tier = new RitualTierData
                {
                    tierName = ReadString(item, "tierName"),
                    displayName = ReadString(item, "displayName"),
                    cost = ReadInt(item, "cost"),
                    drawCount = ReadInt(item, "drawCount"),
                    blessings = new List<RitualBlessingData>()
                };

                var blessingsArray = item.ContainsKey("blessings") ? item["blessings"] : null;
                if (blessingsArray != null && blessingsArray.IsArray)
                {
                    for (int j = 0; j < blessingsArray.Count; j++)
                    {
                        var b = blessingsArray[j];
                        var blessing = new RitualBlessingData
                        {
                            type = ReadString(b, "type"),
                            weight = ReadInt(b, "weight"),
                            minAmount = ReadInt(b, "minAmount"),
                            maxAmount = ReadInt(b, "maxAmount"),
                            minPercent = ReadFloat(b, "minPercent"),
                            maxPercent = ReadFloat(b, "maxPercent"),
                            minCount = ReadInt(b, "minCount"),
                            maxCount = ReadInt(b, "maxCount"),
                        };

                        var statArray = b.ContainsKey("statTypes") ? b["statTypes"] : null;
                        if (statArray != null && statArray.IsArray)
                        {
                            blessing.statTypes = new List<string>();
                            for (int k = 0; k < statArray.Count; k++)
                                blessing.statTypes.Add((string)statArray[k]);
                        }
                        else
                        {
                            blessing.statTypes = new List<string> { "Attack", "Defense", "Hp" };
                        }

                        tier.blessings.Add(blessing);
                    }
                }

                _ritualTiers.Add(tier);
            }

            Debug.Log($"[TribeConfigLoader] 加载 {_ritualTiers.Count} 个命运档次配置");
        }

        private void LoadShopConfig()
        {
            string json = ReadConfigFile("shop_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);
            _shopConfig = new ShopConfigData
            {
                baseRefreshCost = ReadInt(data, "baseRefreshCost", 50),
                refreshIncrement = ReadInt(data, "refreshIncrement", 50),
                slotCount = ReadInt(data, "slotCount", 4),
                shopInterval = ReadInt(data, "shopInterval", 5),
                startRound = ReadInt(data, "startRound", 5),
            };

            // items.artifact
            if (data.ContainsKey("items") && data["items"].IsObject)
            {
                var items = data["items"];
                _shopConfig.artifactConfig = new ArtifactShopConfig
                {
                    basePrice = ReadInt(items, "artifact", "basePrice", 500),
                    effects = new Dictionary<string, int>(),
                    icons = new Dictionary<string, string>()
                };

                if (items.ContainsKey("artifactEffects"))
                {
                    var effects = items["artifactEffects"];
                    foreach (string key in effects.Keys)
                        _shopConfig.artifactConfig.effects[key] = (int)(long)effects[key]; // JSON值为整数，LitJson存为long
                }

                if (items.ContainsKey("artifact") && items["artifact"].ContainsKey("icons"))
                {
                    var icons = items["artifact"]["icons"];
                    foreach (string key in icons.Keys)
                        _shopConfig.artifactConfig.icons[key] = (string)icons[key];
                }

                // items.consumable
                _shopConfig.consumableConfig = new ConsumableShopConfig
                {
                    basePriceMin = ReadInt(items, "consumable", "basePriceMin", 50),
                    basePriceMax = ReadInt(items, "consumable", "basePriceMax", 100),
                    icons = new Dictionary<string, string>()
                };

                if (items.ContainsKey("consumable") && items["consumable"].ContainsKey("icons"))
                {
                    var icons = items["consumable"]["icons"];
                    foreach (string key in icons.Keys)
                        _shopConfig.consumableConfig.icons[key] = (string)icons[key];
                }

                // items.cat
                _shopConfig.catConfig = new CatShopConfig
                {
                    basePrices = new Dictionary<int, int>(),
                    qualityMultipliers = new Dictionary<int, float>(),
                    priceVariation = ReadFloat(items, "cat", "priceVariation", 0.5f),
                    sellRatio = ReadFloat(items, "cat", "sellRatio", 0.5f),
                    tribeIcons = new Dictionary<int, List<string>>()
                };

                if (items.ContainsKey("cat") && items["cat"].ContainsKey("basePrices"))
                {
                    var bp = items["cat"]["basePrices"];
                    foreach (string key in bp.Keys)
                        _shopConfig.catConfig.basePrices[int.Parse(key)] = (int)(long)bp[key];
                }

                if (items.ContainsKey("cat") && items["cat"].ContainsKey("qualityBonusMultipliers"))
                {
                    var qm = items["cat"]["qualityBonusMultipliers"];
                    foreach (string key in qm.Keys)
                        _shopConfig.catConfig.qualityMultipliers[int.Parse(key)] = (float)(double)qm[key];
                }

                if (items.ContainsKey("cat") && items["cat"].ContainsKey("tribeIcons"))
                {
                    var ti = items["cat"]["tribeIcons"];
                    foreach (string key in ti.Keys)
                    {
                        var list = new List<string>();
                        var arr = ti[key];
                        for (int i = 0; i < arr.Count; i++)
                            list.Add((string)arr[i]);
                        _shopConfig.catConfig.tribeIcons[int.Parse(key)] = list;
                    }
                }
            }

            Debug.Log($"[TribeConfigLoader] 加载商店配置: slot={_shopConfig.slotCount}, refresh={_shopConfig.baseRefreshCost}");
        }

        private void LoadArtifactConfig()
        {
            string json = ReadConfigFile("artifact_config");
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonMapper.ToObject(json);

            _artifacts = new Dictionary<string, ArtifactConfig>();

            // artifacts 数组
            var array = data.ContainsKey("artifacts") ? data["artifacts"] : null;
            if (array != null && array.IsArray)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    var item = array[i];
                    var config = new ArtifactConfig
                    {
                        id = ReadString(item, "id"),
                        name = ReadString(item, "name"),
                        description = ReadString(item, "description"),
                        scope = ReadString(item, "scope"),
                        subType = ReadString(item, "subType"),
                    };

                    var effectsArray = item.ContainsKey("effects") ? item["effects"] : null;
                    if (effectsArray != null && effectsArray.IsArray)
                    {
                        config.effects = new List<BuffEffectItem>();
                        for (int j = 0; j < effectsArray.Count; j++)
                        {
                            var eff = effectsArray[j];
                            config.effects.Add(new BuffEffectItem
                            {
                                statType = ReadString(eff, "statType"),
                                isPercent = ReadBool(eff, "isPercent", false),
                                value = ReadFloat(eff, "value"),
                                gameEffectType = ReadInt(eff, "gameEffect"),
                            });
                        }
                    }

                    _artifacts[config.id] = config;
                }
            }

            Debug.Log($"[TribeConfigLoader] 加载 {_artifacts.Count} 个奇物配置");
        }

        // ── 工具方法 ──

        private static string ReadConfigFile(string name)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Tables", $"{name}.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[TribeConfigLoader] 配置文件不存在: {path}");
                return null;
            }
            return File.ReadAllText(path);
        }

        private static int ReadInt(JsonData data, string key)
        {
            if (data == null || !data.ContainsKey(key)) return 0;
            var val = data[key];
            if (val.IsInt) return (int)val;
            if (val.IsLong) return (int)(long)val;
            if (val.IsDouble) return (int)(double)val;
            return 0;
        }

        private static float ReadFloat(JsonData data, string key)
        {
            if (data == null || !data.ContainsKey(key)) return 0f;
            var val = data[key];
            if (val.IsDouble) return (float)(double)val;
            if (val.IsInt) return (int)val;
            if (val.IsLong) return (long)val;
            return 0f;
        }

        private static string ReadString(JsonData data, string key)
        {
            if (data == null || !data.ContainsKey(key)) return "";
            return (string)data[key];
        }

        private static bool ReadBool(JsonData data, string key, bool defaultVal = false)
        {
            if (data == null || !data.ContainsKey(key)) return defaultVal;
            return (bool)data[key];
        }

        // ── 带默认值的读取重载 ──

        private static int ReadInt(JsonData data, string key, int defaultVal)
        {
            if (data == null || !data.ContainsKey(key)) return defaultVal;
            var val = data[key];
            if (val.IsInt) return (int)val;
            if (val.IsLong) return (int)(long)val;
            if (val.IsDouble) return (int)(double)val;
            return defaultVal;
        }

        private static float ReadFloat(JsonData data, string key, float defaultVal)
        {
            if (data == null || !data.ContainsKey(key)) return defaultVal;
            var val = data[key];
            if (val.IsDouble) return (float)(double)val;
            if (val.IsInt) return (int)val;
            if (val.IsLong) return (long)val;
            return defaultVal;
        }

        // ── 嵌套对象读取（先取子对象，再取子key） ──

        private static int ReadInt(JsonData data, string objKey, string subKey, int defaultVal)
        {
            if (data == null || !data.ContainsKey(objKey) || !data[objKey].IsObject) return defaultVal;
            return ReadInt(data[objKey], subKey, defaultVal);
        }

        private static float ReadFloat(JsonData data, string objKey, string subKey, float defaultVal)
        {
            if (data == null || !data.ContainsKey(objKey) || !data[objKey].IsObject) return defaultVal;
            return ReadFloat(data[objKey], subKey, defaultVal);
        }
    }
}
