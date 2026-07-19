using UnityEngine;
using System.Collections.Generic;
using System.IO;
using LitJson;
using Camp;
using Combat.Fighter;

namespace Combat
{
    public enum LevelType
    {
        Normal,  // 普通关
        Elite,   // 精英关
        Boss     // Boss关
    }

    public class BattleCampaignRuntime
    {
        private const string BattleLevelConfigFileName = "Tables/levels_config.json";

        private readonly int[][] _enemyUnitIdsByBattle;

        // 随机变体：每关多组敌人配置，战斗时随机抽取一组
        private readonly Dictionary<int, List<int[]>> _enemyUnitVariantsMap
            = new Dictionary<int, List<int[]>>();

        private static readonly System.Random _rng = new System.Random();

        // Backward-compatible single-value enemy stats (fallback)
        private readonly UnitStaticAttributes[] _enemyStatsByBattle;

        // New: per-difficulty enemy stats [battleIndex][difficultyIndex]
        private readonly Dictionary<int, Dictionary<DifficultyLevel, UnitStaticAttributes>> _enemyStatsMap
            = new Dictionary<int, Dictionary<DifficultyLevel, UnitStaticAttributes>>();

        // New: per-formation enemy unit ids [battleIndex][formationType]
        private readonly Dictionary<int, Dictionary<EnemyFormationType, int[]>> _enemyUnitIdsMap
            = new Dictionary<int, Dictionary<EnemyFormationType, int[]>>();

        // New: scenario options per battle
        private readonly Dictionary<int, List<BattleScenarioOption>> _scenarioOptionsMap
            = new Dictionary<int, List<BattleScenarioOption>>();

        // New: difficulty options per battle
        private readonly Dictionary<int, List<DifficultyLevel>> _difficultyOptionsMap
            = new Dictionary<int, List<DifficultyLevel>>();

        // New: per-difficulty cat food reward
        private readonly Dictionary<int, Dictionary<DifficultyLevel, int>> _catFoodRewardMap
            = new Dictionary<int, Dictionary<DifficultyLevel, int>>();

        // New: free deploy quota per battle
        private readonly Dictionary<int, int> _freeDeployQuotaMap = new Dictionary<int, int>();

        // New: level type per battle (normal/elite/boss)
        private readonly Dictionary<int, LevelType> _levelTypeMap = new Dictionary<int, LevelType>();

        private readonly bool[] _hasRecruitmentByBattle;
        private readonly bool[] _hasRitualByBattle;
        private readonly bool[] _hasShopByBattle;
        private readonly bool[] _hasNewTribeEventByBattle;
        private readonly bool[] _hasRandomEventByBattle;
        private readonly bool[] _hasEnemyBillboardByBattle;
        private readonly int[] _catFoodRewardByBattle;

        // Enemy type names (loaded from enemyTypes in config)
        private readonly Dictionary<int, string> _enemyTypeNames = new Dictionary<int, string>();

        // 预生成：每个关卡的敌人组成在新游戏时一次性随机确定
        private int[][] _preGeneratedEnemyIds;

        // 敌方人口上限：3层×15关 = 45关
        // 第一层(1-15关), 第二层(16-30关), 第三层(31-45关)
        private static readonly int[] _enemyPopulationCap = new int[]
        {
            // 第一层 1-15
            2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 12, 12, 14, 14, 16,
            // 第二层 16-30
            12, 13, 14, 14, 15, 15, 16, 16, 17, 17, 18, 18, 19, 19, 20,
            // 第三层 31-45
            18, 19, 20, 20, 21, 21, 22, 22, 23, 23, 24, 24, 25, 25, 26
        };

        // Popup priorities
        private readonly Dictionary<string, int> _popupPriorities = new Dictionary<string, int>();

        private int _currentBattleIndex;
        private bool _isCompleted;

        public int CurrentBattleNumber => Mathf.Clamp(_currentBattleIndex + 1, 1, MaxBattleCount);
        public int MaxBattleCount => _enemyUnitIdsByBattle.Length;
        public bool IsCompleted => _isCompleted;
        public int CurrentEnemyCount => GetEnemyCountForBattle(CurrentBattleNumber);
        public bool HasNextBattle => !_isCompleted && CurrentBattleNumber < MaxBattleCount;

        public BattleCampaignRuntime()
        {
            _enemyUnitIdsByBattle = LoadConfig(
                out _hasRecruitmentByBattle,
                out _hasRitualByBattle,
                out _hasShopByBattle,
                out _hasNewTribeEventByBattle,
                out _hasRandomEventByBattle,
                out _hasEnemyBillboardByBattle,
                out _catFoodRewardByBattle,
                out _enemyStatsByBattle);
            ResetProgress();
        }

        public void ResetProgress()
        {
            _currentBattleIndex = 0;
            _isCompleted = false;
            PreGenerateEnemyCompositions();
        }

        /// <summary>
        /// 新游戏时一次性随机确定所有关卡的敌人组成
        /// 遵从敌方人口上限，尽量按照上限生成
        /// </summary>
        private void PreGenerateEnemyCompositions()
        {
            int count = MaxBattleCount;
            _preGeneratedEnemyIds = new int[count][];
            for (int i = 0; i < count; i++)
            {
                int battleNumber = i + 1;
                // RollEnemyUnitIds 内部已应用人口上限裁剪和填充
                _preGeneratedEnemyIds[i] = RollEnemyUnitIds(battleNumber);
            }
        }

        public int GetEnemyCountForBattle(int battleNumber)
        {
            int[] ids = GetEnemyUnitIdsForBattle(battleNumber);
            return ids != null && ids.Length > 0 ? ids.Length : 1;
        }

        public int[] GetEnemyUnitIdsForBattle(int battleNumber)
        {
            if (_preGeneratedEnemyIds != null)
            {
                int idx = battleNumber - 1;
                if (idx >= 0 && idx < _preGeneratedEnemyIds.Length)
                    return _preGeneratedEnemyIds[idx];
            }
            return RollEnemyUnitIds(battleNumber);
        }

        public List<int[]> GetEnemyUnitVariantsForBattle(int battleNumber)
        {
            if (_enemyUnitVariantsMap.TryGetValue(battleNumber, out var variants) && variants != null && variants.Count > 0)
            {
                var result = new List<int[]>(variants.Count);
                for (int i = 0; i < variants.Count; i++)
                {
                    var variant = variants[i];
                    if (variant == null || variant.Length == 0) continue;
                    var copy = new int[variant.Length];
                    System.Array.Copy(variant, copy, variant.Length);
                    result.Add(copy);
                }
                return result;
            }

            return null;
        }

        private int[] RollEnemyUnitIds(int battleNumber)
        {
            int cap = GetEnemyPopulationCap(battleNumber);
            LevelType levelType = GetLevelType(battleNumber);

            switch (levelType)
            {
                case LevelType.Boss:
                    return GenerateBossComposition(cap);
                case LevelType.Elite:
                    return GenerateEliteComposition(cap);
                default:
                    return GenerateNormalComposition(cap);
            }
        }

        /// <summary>
        /// 普通关卡：鼠辈(5000) + 长矛猫(5010) + 苍蝇猫(1002) 任意比例，填满人口上限
        /// </summary>
        private int[] GenerateNormalComposition(int cap)
        {
            // 5000(鼠辈) cost=1, 5010(长矛猫) cost=1, 1002(苍蝇猫) cost=1
            var result = new List<int>();
            int remaining = cap;

            while (remaining > 0)
            {
                int roll = _rng.Next(3);
                result.Add(roll == 0 ? 5000 : roll == 1 ? 5010 : 1002);
                remaining--;
            }

            return result.ToArray();
        }

        /// <summary>
        /// 精英关卡：鼠辈(5000) + 长矛猫(5010) + 苍蝇猫(1002) + 敌方游侠(5040) + 敌方猫骑士(5020) + 奶爸猫(1005) + 巫毒猫(1101)
        /// 至少包含一只游侠或猫骑士，填满人口上限
        /// </summary>
        private int[] GenerateEliteComposition(int cap)
        {
            // 5000 cost=1, 5010 cost=1, 1002 cost=1, 5040 cost=4, 1005 cost=2, 1101 cost=2, 5020 cost=4
            var result = new List<int>();
            int remaining = cap;

            // 先确保至少有一只游侠(5040)或猫骑士(5020)
            if (remaining >= 4 && _rng.Next(2) == 0)
            {
                result.Add(5020); // 猫骑士 cost=4
                remaining -= 4;
            }
            else if (remaining >= 4)
            {
                result.Add(5040); // 游侠 cost=4
                remaining -= 4;
            }
            else if (remaining >= 1)
            {
                // 人口太少，放一只cost=1的保证至少有敌人
                result.Add(PickRandomCost1Enemy());
                remaining -= 1;
            }

            // 填充剩余人口
            while (remaining > 0)
            {
                // 按概率选择单位：cost=1 45%, cost=2 35%, cost=4 20%
                int roll = _rng.Next(100);
                if (remaining >= 4 && roll < 20)
                {
                    result.Add(5020); // 猫骑士 cost=4
                    remaining -= 4;
                }
                else if (remaining >= 2 && roll < 55)
                {
                    // cost=2: 奶爸猫/巫毒猫
                    result.Add(PickRandomCost2Enemy());
                    remaining -= 2;
                }
                else
                {
                    result.Add(PickRandomCost1Enemy()); // cost=1: 鼠辈/矛猫/苍蝇猫
                    remaining -= 1;
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Boss关卡：必定至少一只敌方奶牛猫族长(5030)，其余任意填充，填满人口上限
        /// </summary>
        private int[] GenerateBossComposition(int cap)
        {
            // 5030 cost=5
            var result = new List<int>();
            int remaining = cap;

            // 必定放一只奶牛猫族长
            result.Add(5030);
            remaining -= 5;

            // 填充剩余人口（可用所有敌方单位）
            while (remaining > 0)
            {
                if (remaining >= 5 && _rng.Next(100) < 20)
                {
                    result.Add(5030); // 再来一只族长 cost=5
                    remaining -= 5;
                }
                else if (remaining >= 4 && _rng.Next(100) < 25)
                {
                    result.Add(5020); // 猫骑士 cost=4
                    remaining -= 4;
                }
                else if (remaining >= 2 && _rng.Next(100) < 35)
                {
                    // cost=2: 游侠/奶爸猫/巫毒猫
                    result.Add(PickRandomCost2Enemy());
                    remaining -= 2;
                }
                else
                {
                    result.Add(PickRandomCost1Enemy()); // cost=1: 鼠辈/矛猫/苍蝇猫
                    remaining -= 1;
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 随机选择一个 cost=1 的敌方单位：鼠辈(5000) / 长矛猫(5010) / 苍蝇猫(1002)
        /// </summary>
        private int PickRandomCost1Enemy()
        {
            int roll = _rng.Next(3);
            return roll == 0 ? 5000 : roll == 1 ? 5010 : 1002;
        }

        /// <summary>
        /// 随机选择一个 cost=2 的敌方单位：奶爸猫(1005) / 巫毒猫(1101)
        /// </summary>
        private int PickRandomCost2Enemy()
        {
            return _rng.Next(2) == 0 ? 1005 : 1101;
        }

        /// <summary>
        /// 获取敌方属性乘算系数（基于层数和关卡类型，仅影响攻击和血量）
        /// 层数：第一层=1.0, 第二层=1.2, 第三层=1.5
        /// 类型：普通=1.0, 精英=1.2, Boss=2.0
        /// </summary>
        public float GetEnemyStatMultiplier(int battleNumber)
        {
            // 层数系数：1-15关=第一层, 16-30关=第二层, 31-45关=第三层
            int region = (battleNumber - 1) / 15;
            float regionMultiplier = region switch
            {
                0 => 1.0f,
                1 => 1.2f,
                _ => 1.5f
            };

            // 关卡类型系数
            float typeMultiplier = GetLevelType(battleNumber) switch
            {
                LevelType.Elite => 1.2f,
                LevelType.Boss => 2.0f,
                _ => 1.0f
            };

            return regionMultiplier * typeMultiplier;
        }

        /// <summary>
        /// 获取指定关卡的敌方人口上限
        /// </summary>
        public int GetEnemyPopulationCap(int battleNumber)
        {
            int idx = battleNumber - 1;
            if (idx >= 0 && idx < _enemyPopulationCap.Length)
                return _enemyPopulationCap[idx];
            return 6; // 默认值
        }

        /// <summary>
        /// 计算敌方单位ID数组的总人口成本
        /// </summary>
        private int CalculateEnemyPopulationCost(int[] enemyIds)
        {
            int total = 0;
            for (int i = 0; i < enemyIds.Length; i++)
            {
                var cfg = TribeConfigLoader.Instance?.GetFighterConfig(enemyIds[i]);
                total += cfg != null ? cfg.populationCost : 1;
            }
            return total;
        }

        public string GetEnemyName(int enemyUnitId)
        {
            if (_enemyTypeNames.TryGetValue(enemyUnitId, out string name))
                return name;
            return $"敌方兵种 {enemyUnitId}";
        }

        public int GetNextBattleNumber(int currentBattleNumber)
        {
            return Mathf.Clamp(currentBattleNumber + 1, 1, MaxBattleCount);
        }

        public void AdvanceAfterVictory(int battleNumber)
        {
            int resolvedBattleNumber = Mathf.Clamp(battleNumber, 1, MaxBattleCount);
            int resolvedIndex = resolvedBattleNumber - 1;
            if (resolvedIndex != _currentBattleIndex)
                return;

            if (_currentBattleIndex >= MaxBattleCount - 1)
            {
                _isCompleted = true;
                return;
            }

            _currentBattleIndex++;
        }

        public int GetCatFoodRewardForBattle(int battleNumber)
        {
            return GetCatFoodReward(battleNumber, DifficultyLevel.Normal);
        }

        public bool HasRecruitmentForBattle(int battleNumber)
        {
            if (_hasRecruitmentByBattle == null || _hasRecruitmentByBattle.Length == 0) return false;
            int index = Mathf.Clamp(battleNumber - 1, 0, _hasRecruitmentByBattle.Length - 1);
            return _hasRecruitmentByBattle[index];
        }

        public bool HasRitualForBattle(int battleNumber)
        {
            if (_hasRitualByBattle == null || _hasRitualByBattle.Length == 0) return false;
            int index = Mathf.Clamp(battleNumber - 1, 0, _hasRitualByBattle.Length - 1);
            return _hasRitualByBattle[index];
        }

        public bool HasShopForBattle(int battleNumber)
        {
            if (_hasShopByBattle == null || _hasShopByBattle.Length == 0) return false;
            int index = Mathf.Clamp(battleNumber - 1, 0, _hasShopByBattle.Length - 1);
            return _hasShopByBattle[index];
        }

        public bool HasNewTribeEventForBattle(int battleNumber)
        {
            if (_hasNewTribeEventByBattle == null || _hasNewTribeEventByBattle.Length == 0) return false;
            int index = Mathf.Clamp(battleNumber - 1, 0, _hasNewTribeEventByBattle.Length - 1);
            return _hasNewTribeEventByBattle[index];
        }

        public bool HasRandomEventForBattle(int battleNumber)
        {
            if (_hasRandomEventByBattle == null || _hasRandomEventByBattle.Length == 0) return false;
            int index = Mathf.Clamp(battleNumber - 1, 0, _hasRandomEventByBattle.Length - 1);
            return _hasRandomEventByBattle[index];
        }

        public bool HasEnemyBillboardForBattle(int battleNumber)
        {
            if (_hasEnemyBillboardByBattle == null || _hasEnemyBillboardByBattle.Length == 0) return true;
            int index = Mathf.Clamp(battleNumber - 1, 0, _hasEnemyBillboardByBattle.Length - 1);
            return _hasEnemyBillboardByBattle[index];
        }

        public int GetPopupPriority(string eventType)
        {
            if (_popupPriorities.TryGetValue(eventType, out int priority))
                return priority;
            return 0;
        }

        public List<string> GetSortedPopupEvents(int battleNumber)
        {
            var events = new List<System.Tuple<string, int>>();

            if (HasNewTribeEventForBattle(battleNumber))
                events.Add(new System.Tuple<string, int>("newTribeEvent", GetPopupPriority("newTribeEvent")));
            // 招募已由 ShowBattleResultRecruitment 单独处理，不加入构筑阶段事件
            if (HasRitualForBattle(battleNumber))
                events.Add(new System.Tuple<string, int>("ritual", GetPopupPriority("ritual")));
            if (HasRandomEventForBattle(battleNumber))
                events.Add(new System.Tuple<string, int>("randomEvent", GetPopupPriority("randomEvent")));
            if (HasShopForBattle(battleNumber))
                events.Add(new System.Tuple<string, int>("shop", GetPopupPriority("shop")));

            events.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            var result = new List<string>();
            foreach (var e in events)
                result.Add(e.Item1);
            return result;
        }

        // --- New methods for scenario/difficulty system ---

        /// <summary>
        /// 获取指定关卡的敌人情况选项卡列表
        /// </summary>
        public List<BattleScenarioOption> GetScenarioOptions(int battleNumber)
        {
            if (_scenarioOptionsMap.TryGetValue(battleNumber, out var options))
                return options;

            // Fallback: return a default plain/sunny/single scenario
            return new List<BattleScenarioOption>
            {
                new BattleScenarioOption
                {
                    terrain = TerrainType.Plain,
                    weather = WeatherType.Sunny,
                    formationType = EnemyFormationType.Single
                }
            };
        }

        /// <summary>
        /// 获取指定关卡的难度选项列表
        /// </summary>
        public List<DifficultyLevel> GetDifficultyOptions(int battleNumber)
        {
            if (_difficultyOptionsMap.TryGetValue(battleNumber, out var options))
                return options;

            // Fallback: normal only
            return new List<DifficultyLevel> { DifficultyLevel.Normal };
        }

        /// <summary>
        /// 获取指定关卡指定难度的敌人属性
        /// </summary>
        public UnitStaticAttributes GetEnemyStats(int battleNumber, DifficultyLevel difficulty)
        {
            if (_enemyStatsMap.TryGetValue(battleNumber, out var byDifficulty))
            {
                if (byDifficulty.TryGetValue(difficulty, out var stats))
                    return stats;
                // If this difficulty not available, fall to normal
                if (byDifficulty.TryGetValue(DifficultyLevel.Normal, out var normalStats))
                    return normalStats;
            }

            // Fallback to legacy
            if (_enemyStatsByBattle == null || _enemyStatsByBattle.Length == 0)
                return UnitStaticAttributes.Default;

            int index = Mathf.Clamp(battleNumber - 1, 0, _enemyStatsByBattle.Length - 1);
            return _enemyStatsByBattle[index];
        }

        /// <summary>
        /// 获取指定关卡指定难度的猫粮奖励
        /// </summary>
        public int GetCatFoodReward(int battleNumber, DifficultyLevel difficulty)
        {
            if (_catFoodRewardMap.TryGetValue(battleNumber, out var byDifficulty))
            {
                if (byDifficulty.TryGetValue(difficulty, out var reward))
                    return reward;
                if (byDifficulty.TryGetValue(DifficultyLevel.Normal, out var normalReward))
                    return normalReward;
            }

            // Fallback to legacy
            if (_catFoodRewardByBattle == null || _catFoodRewardByBattle.Length == 0)
                return 0;

            int index = Mathf.Clamp(battleNumber - 1, 0, _catFoodRewardByBattle.Length - 1);
            return _catFoodRewardByBattle[index];
        }

        /// <summary>
        /// 获取指定关卡的免费出战额度
        /// </summary>
        public int GetFreeDeployQuota(int battleNumber)
        {
            if (_freeDeployQuotaMap.TryGetValue(battleNumber, out var quota))
                return quota;
            return 0;
        }

        /// <summary>
        /// 获取指定关卡的类型（普通/精英/Boss）
        /// </summary>
        public LevelType GetLevelType(int battleNumber)
        {
            if (_levelTypeMap.TryGetValue(battleNumber, out var levelType))
                return levelType;
            return LevelType.Normal;
        }

        /// <summary>
        /// 获取指定关卡指定敌人类别的单位ID
        /// </summary>
        public int[] GetEnemyUnitIds(int battleNumber, EnemyFormationType formation)
        {
            if (_enemyUnitIdsMap.TryGetValue(battleNumber, out var byFormation))
            {
                if (byFormation.TryGetValue(formation, out var ids))
                    return ids;
            }
            return null;
        }

        // --- Loading ---

        private int[][] LoadConfig(
            out bool[] hasRecruitmentByBattle,
            out bool[] hasRitualByBattle,
            out bool[] hasShopByBattle,
            out bool[] hasNewTribeEventByBattle,
            out bool[] hasRandomEventByBattle,
            out bool[] hasEnemyBillboardByBattle,
            out int[] catFoodRewardByBattle,
            out UnitStaticAttributes[] enemyStatsByBattle)
        {
            string configPath = Path.Combine(Application.streamingAssetsPath, BattleLevelConfigFileName);
            if (!File.Exists(configPath))
            {
                Debug.LogError($"[BattleCampaignRuntime] Battle level config file not found: {configPath}");
                return LoadFallback(
                    out hasRecruitmentByBattle,
                    out hasRitualByBattle,
                    out hasShopByBattle,
                    out hasNewTribeEventByBattle,
                    out hasRandomEventByBattle,
                    out hasEnemyBillboardByBattle,
                    out catFoodRewardByBattle,
                    out enemyStatsByBattle);
            }

            try
            {
                string jsonContent = File.ReadAllText(configPath);
                JsonData rootJson = JsonMapper.ToObject(jsonContent);

                // Load enemy type names
                if (rootJson != null && rootJson.Keys.Contains("enemyTypes"))
                {
                    JsonData enemyTypesJson = rootJson["enemyTypes"];
                    foreach (string key in enemyTypesJson.Keys)
                    {
                        if (int.TryParse(key, out int id))
                        {
                            string name = ReadString(enemyTypesJson[key], "name", $"敌方兵种 {id}");
                            _enemyTypeNames[id] = name;
                        }
                    }
                }

                // Load popup priorities
                if (rootJson != null && rootJson.Keys.Contains("popupPriorities"))
                {
                    JsonData prioritiesJson = rootJson["popupPriorities"];
                    foreach (string key in prioritiesJson.Keys)
                    {
                        int val = int.TryParse(prioritiesJson[key].ToString(), out int v) ? v : 0;
                        _popupPriorities[key] = val;
                    }
                }

                JsonData levelsJson = rootJson != null && rootJson.Keys.Contains("levels")
                    ? rootJson["levels"]
                    : rootJson;

                if (levelsJson == null || !levelsJson.IsArray || levelsJson.Count == 0)
                {
                    Debug.LogError($"[BattleCampaignRuntime] Battle level config format is invalid: {configPath}");
                    return LoadFallback(
                        out hasRecruitmentByBattle,
                        out hasRitualByBattle,
                        out hasShopByBattle,
                        out hasNewTribeEventByBattle,
                        out hasRandomEventByBattle,
                        out hasEnemyBillboardByBattle,
                        out catFoodRewardByBattle,
                        out enemyStatsByBattle);
                }

                int count = levelsJson.Count;
                int[][] enemyUnitIdsByBattle = new int[count][];
                hasRecruitmentByBattle = new bool[count];
                hasRitualByBattle = new bool[count];
                hasShopByBattle = new bool[count];
                hasNewTribeEventByBattle = new bool[count];
                hasRandomEventByBattle = new bool[count];
                hasEnemyBillboardByBattle = new bool[count];
                catFoodRewardByBattle = new int[count];
                enemyStatsByBattle = new UnitStaticAttributes[count];

                for (int i = 0; i < count; i++)
                {
                    JsonData levelJson = levelsJson[i];
                    int battleNumber = ReadInt(levelJson, "battleNumber", i + 1);

                    // Parse enemyUnitIds: 支持三种格式
                    // 1. Legacy: flat int array [5000, 5000, 5000]
                    // 2. Formation: { "single": [...], "swarm": [...] }
                    // 3. Variants: [[5000,5000], [5010,5010], ...] — 每关多组配置，战斗时随机抽取
                    if (levelJson.Keys.Contains("enemyUnitIds"))
                    {
                        JsonData idsData = levelJson["enemyUnitIds"];
                        if (idsData.IsArray && idsData.Count > 0 && idsData[0].IsArray)
                        {
                            // Variants format: array of arrays
                            var variants = new List<int[]>();
                            for (int v = 0; v < idsData.Count; v++)
                            {
                                int[] ids = ReadIntArrayFromJsonData(idsData[v]);
                                if (ids != null && ids.Length > 0)
                                    variants.Add(ids);
                            }
                            if (variants.Count > 0)
                            {
                                _enemyUnitVariantsMap[battleNumber] = variants;
                                enemyUnitIdsByBattle[i] = variants[0]; // fallback: first variant
                            }
                            else
                            {
                                enemyUnitIdsByBattle[i] = new[] { 1 };
                            }
                        }
                        else if (idsData.IsArray)
                        {
                            // Legacy format: flat int array
                            enemyUnitIdsByBattle[i] = ReadIntArray(levelJson, "enemyUnitIds");
                        }
                        else if (idsData.IsObject)
                        {
                            // Formation format: { "single": [...], "swarm": [...] }
                            var formationMap = new Dictionary<EnemyFormationType, int[]>();
                            int[] fallbackIds = null;

                            foreach (string key in idsData.Keys)
                            {
                                EnemyFormationType fmt = ParseFormationType(key);
                                int[] ids = ReadIntArrayFromJsonData(idsData[key]);
                                formationMap[fmt] = ids;
                                if (fallbackIds == null) fallbackIds = ids;
                            }

                            _enemyUnitIdsMap[battleNumber] = formationMap;
                            enemyUnitIdsByBattle[i] = fallbackIds ?? new[] { 1 };
                        }
                    }
                    else
                    {
                        enemyUnitIdsByBattle[i] = new[] { 1 };
                    }

                    hasRecruitmentByBattle[i] = ReadBool(levelJson, "hasRecruitment");
                    hasRitualByBattle[i] = ReadBool(levelJson, "hasRitual");
                    hasShopByBattle[i] = ReadBool(levelJson, "hasShop");
                    hasNewTribeEventByBattle[i] = ReadBool(levelJson, "hasNewTribeEvent");
                    hasRandomEventByBattle[i] = ReadBool(levelJson, "hasRandomEvent");
                    hasEnemyBillboardByBattle[i] = ReadBool(levelJson, "hasEnemyBillboard", true);

                    // Parse enemyStats: could be legacy (object with attack/defense/hp) or new (object with difficulty keys)
                    if (levelJson.Keys.Contains("enemyStats"))
                    {
                        JsonData statsData = levelJson["enemyStats"];
                        if (statsData.IsObject && statsData.Keys.Contains("attack"))
                        {
                            // Legacy format: single enemy stats object
                            enemyStatsByBattle[i] = ReadEnemyStats(statsData);
                        }
                        else if (statsData.IsObject)
                        {
                            // New format: { "normal": {...}, "hard": {...}, "boss": {...} }
                            var statsMap = new Dictionary<DifficultyLevel, UnitStaticAttributes>();

                            foreach (string key in statsData.Keys)
                            {
                                DifficultyLevel dl = ParseDifficulty(key);
                                statsMap[dl] = ReadEnemyStats(statsData[key]);
                            }

                            _enemyStatsMap[battleNumber] = statsMap;

                            // Set legacy fallback to normal
                            if (statsMap.TryGetValue(DifficultyLevel.Normal, out var normalStats))
                                enemyStatsByBattle[i] = normalStats;
                            else
                                enemyStatsByBattle[i] = UnitStaticAttributes.Default;
                        }
                    }
                    else
                    {
                        enemyStatsByBattle[i] = UnitStaticAttributes.Default;
                    }

                    // Parse catFoodReward: could be int or object with difficulty keys
                    if (levelJson.Keys.Contains("catFoodReward"))
                    {
                        JsonData rewardData = levelJson["catFoodReward"];
                        if (rewardData.IsInt || rewardData.IsLong)
                        {
                            // Legacy format: single int
                            catFoodRewardByBattle[i] = ReadInt(levelJson, "catFoodReward");
                        }
                        else if (rewardData.IsObject)
                        {
                            // New format: { "normal": 100, "hard": 150, "boss": 250 }
                            var rewardMap = new Dictionary<DifficultyLevel, int>();

                            foreach (string key in rewardData.Keys)
                            {
                                DifficultyLevel dl = ParseDifficulty(key);
                                int val = int.TryParse(rewardData[key].ToString(), out int rv) ? rv : 0;
                                rewardMap[dl] = val;
                            }

                            _catFoodRewardMap[battleNumber] = rewardMap;

                            // Set legacy fallback to normal
                            if (rewardMap.TryGetValue(DifficultyLevel.Normal, out var normalReward))
                                catFoodRewardByBattle[i] = normalReward;
                        }
                    }

                    // Parse scenarioOptions
                    if (levelJson.Keys.Contains("scenarioOptions") && levelJson["scenarioOptions"].IsArray)
                    {
                        JsonData scenariosData = levelJson["scenarioOptions"];
                        var scenarios = new List<BattleScenarioOption>();
                        for (int s = 0; s < scenariosData.Count; s++)
                        {
                            JsonData scenarioData = scenariosData[s];
                            scenarios.Add(new BattleScenarioOption
                            {
                                terrain = ParseTerrain(ReadString(scenarioData, "terrain", "plain")),
                                weather = ParseWeather(ReadString(scenarioData, "weather", "sunny")),
                                formationType = ParseFormationType(ReadString(scenarioData, "formationType", "single"))
                            });
                        }
                        _scenarioOptionsMap[battleNumber] = scenarios;
                    }

                    // Parse difficultyOptions
                    if (levelJson.Keys.Contains("difficultyOptions") && levelJson["difficultyOptions"].IsArray)
                    {
                        JsonData diffData = levelJson["difficultyOptions"];
                        var difficulties = new List<DifficultyLevel>();
                        for (int d = 0; d < diffData.Count; d++)
                        {
                            difficulties.Add(ParseDifficulty(diffData[d].ToString()));
                        }
                        _difficultyOptionsMap[battleNumber] = difficulties;
                    }

                    // Parse freeDeployQuota
                    if (levelJson.Keys.Contains("freeDeployQuota"))
                    {
                        _freeDeployQuotaMap[battleNumber] = ReadInt(levelJson, "freeDeployQuota");
                    }

                    // Parse levelType
                    if (levelJson.Keys.Contains("levelType"))
                    {
                        _levelTypeMap[battleNumber] = ParseLevelType(levelJson["levelType"].ToString());
                    }
                }

                return enemyUnitIdsByBattle;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[BattleCampaignRuntime] Failed to load battle level config: {exception.Message}");
                return LoadFallback(
                    out hasRecruitmentByBattle,
                    out hasRitualByBattle,
                    out hasShopByBattle,
                    out hasNewTribeEventByBattle,
                    out hasRandomEventByBattle,
                    out hasEnemyBillboardByBattle,
                    out catFoodRewardByBattle,
                    out enemyStatsByBattle);
            }
        }

        private int[][] LoadFallback(
            out bool[] hasRecruitmentByBattle,
            out bool[] hasRitualByBattle,
            out bool[] hasShopByBattle,
            out bool[] hasNewTribeEventByBattle,
            out bool[] hasRandomEventByBattle,
            out bool[] hasEnemyBillboardByBattle,
            out int[] catFoodRewardByBattle,
            out UnitStaticAttributes[] enemyStatsByBattle)
        {
            hasRecruitmentByBattle = new[] { false };
            hasRitualByBattle = new[] { false };
            hasShopByBattle = new[] { false };
            hasNewTribeEventByBattle = new[] { false };
            hasRandomEventByBattle = new[] { false };
            hasEnemyBillboardByBattle = new[] { true };
            catFoodRewardByBattle = new[] { 0 };
            enemyStatsByBattle = new[] { UnitStaticAttributes.Default };
            return new[] { new[] { 1 } };
        }

        // --- Parsing helpers ---

        private static int ReadInt(JsonData json, string key)
        {
            return ReadInt(json, key, 0);
        }

        private static int ReadInt(JsonData json, string key, int defaultValue)
        {
            return json.Keys.Contains(key) && int.TryParse(json[key].ToString(), out int v) ? v : defaultValue;
        }

        private static float ReadFloat(JsonData json, string key, float defaultValue)
        {
            return json.Keys.Contains(key) && float.TryParse(json[key].ToString(), out float v) ? v : defaultValue;
        }

        private static bool ReadBool(JsonData json, string key)
        {
            return json.Keys.Contains(key)
                && bool.TryParse(json[key].ToString(), out bool v)
                && v;
        }

        private static bool ReadBool(JsonData json, string key, bool defaultValue)
        {
            if (!json.Keys.Contains(key)) return defaultValue;
            return bool.TryParse(json[key].ToString(), out bool v) && v;
        }

        private static string ReadString(JsonData json, string key, string defaultValue)
        {
            return json.Keys.Contains(key) ? json[key].ToString() : defaultValue;
        }

        private static int[] ReadIntArray(JsonData json, string key)
        {
            if (json == null || !json.Keys.Contains(key))
                return new[] { 1 };

            JsonData valuesJson = json[key];
            return ReadIntArrayFromJsonData(valuesJson);
        }

        private static int[] ReadIntArrayFromJsonData(JsonData valuesJson)
        {
            if (valuesJson == null || !valuesJson.IsArray || valuesJson.Count == 0)
                return new[] { 1 };

            int[] values = new int[valuesJson.Count];
            for (int i = 0; i < valuesJson.Count; i++)
            {
                values[i] = int.TryParse(valuesJson[i].ToString(), out int value)
                    ? Mathf.Max(1, value)
                    : 1;
            }

            return values;
        }

        private static UnitStaticAttributes ReadEnemyStats(JsonData json)
        {
            var stats = UnitStaticAttributes.Default;

            if (json == null)
                return stats;

            var defaults = UnitStaticAttributes.Default;
            stats.Attack = ReadInt(json, "attack", defaults.Attack);
            stats.Defense = ReadInt(json, "defense", defaults.Defense);
            stats.MaxHp = ReadInt(json, "hp", defaults.MaxHp);
            // JSON 中速度值为整数，直接读取
            stats.MoveSpeed = ReadInt(json, "moveSpeed", (int)defaults.MoveSpeed);
            stats.AttackSpeed = ReadInt(json, "attackSpeed", (int)defaults.AttackSpeed);
            stats.AttackRange = ReadFloat(json, "attackRange", defaults.AttackRange);

            // moveSpeed/attackSpeed 在配置中是放大1000倍的整数，需要还原
            stats.MoveSpeed = stats.MoveSpeed / 1000f;
            stats.AttackSpeed = stats.AttackSpeed / 1000f;

            return stats;
        }

        private static DifficultyLevel ParseDifficulty(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "hard": return DifficultyLevel.Hard;
                case "boss": return DifficultyLevel.Boss;
                default: return DifficultyLevel.Normal;
            }
        }

        private static TerrainType ParseTerrain(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "brush": return TerrainType.Brush;
                default: return TerrainType.Plain;
            }
        }

        private static WeatherType ParseWeather(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "rainy": return WeatherType.Rainy;
                case "night": return WeatherType.Night;
                case "windy": return WeatherType.Windy;
                default: return WeatherType.Sunny;
            }
        }

        private static EnemyFormationType ParseFormationType(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "swarm": return EnemyFormationType.Swarm;
                default: return EnemyFormationType.Single;
            }
        }

        private static LevelType ParseLevelType(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "elite": return LevelType.Elite;
                case "boss": return LevelType.Boss;
                default: return LevelType.Normal;
            }
        }
    }
}
