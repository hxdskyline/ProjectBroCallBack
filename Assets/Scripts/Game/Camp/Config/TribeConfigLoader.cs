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
    }
}
