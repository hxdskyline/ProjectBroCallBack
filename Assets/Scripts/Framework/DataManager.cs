using System;
using UnityEngine;
using System.IO;
using Camp;
using LitJson;

/// <summary>
/// 数据管理器 - 负责玩家数据的保存、加载和管理
/// </summary>
public class DataManager : MonoBehaviour
    , ICurrencyStorage
{
    private PlayerData _playerData;
    private string _savePath;

    public PlayerData PlayerData => _playerData;
    public string SaveId => _playerData != null ? _playerData.playerId : string.Empty;

    public void Initialize()
    {
        _savePath = Path.Combine(Application.persistentDataPath, "PlayerData");
        
        // 如果目录不存在则创建
        if (!Directory.Exists(_savePath))
        {
            Directory.CreateDirectory(_savePath);
        }

        Debug.Log($"[DataManager] Initialized at: {_savePath}");
    }

    /// <summary>
    /// 加载玩家数据
    /// </summary>
    public void LoadPlayerData()
    {
        GameLogger.Log("Data", "Load");
        string filePath = Path.Combine(_savePath, "playerdata.json");

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                _playerData = JsonUtility.FromJson<PlayerData>(json);
                EnsurePlayerDataDefaults();
                Debug.Log("[DataManager] Player data loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataManager] Error loading player data: {e.Message}");
                CreateNewPlayerData();
            }
        }
        else
        {
            CreateNewPlayerData();
        }
    }

    /// <summary>
    /// 保存玩家数据
    /// </summary>
    public void SavePlayerData()
    {
        GameLogger.Log("Data", "Save");
        if (_playerData == null)
            return;

        try
        {
            _playerData.lastSaveTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string filePath = Path.Combine(_savePath, "playerdata.json");
            string json = JsonUtility.ToJson(_playerData, true);
            File.WriteAllText(filePath, json);
            Debug.Log("[DataManager] Player data saved successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataManager] Error saving player data: {e.Message}");
        }
    }

    /// <summary>
    /// 创建新的玩家数据
    /// </summary>
    private void CreateNewPlayerData()
    {
        _playerData = new PlayerData();
        _playerData.playerId = System.Guid.NewGuid().ToString();
        _playerData.playerName = "Player";
        _playerData.level = 1;
        _playerData.currentLevel = 1;
        _playerData.currencies = new System.Collections.Generic.List<CurrencyData>();
        SetCurrencyAmount(CurrencyManager.GetCurrencyKey(CurrencyType.Gold), 0, false);
        SetCurrencyAmount(CurrencyManager.GetCurrencyKey(CurrencyType.Diamond), 0, false);

        // 初始化主角属性
        _playerData.leadership = 4;      // 领导力初始值4
        _playerData.streetIntel = 1;     // 街头情报初始值1
        _playerData.charisma = 1;        // 咪格魅力初始值1
        _playerData.leaderExp = 0;       // 主角经验值初始值0
        _playerData.leaderExpToNextLevel = 100; // 升级所需经验值初始值100
        _playerData.leaderSkillPoints = 0; // 技能点初始值0

        // Initialize tribe fields
        _playerData.tribes = new System.Collections.Generic.List<TribeRecord>();
        _playerData.currentRound = 1;
        _playerData.catFood = 1000; // Initial cat food
        _playerData.shopRefreshCount = 0;
        _playerData.lastShopRound = 0;
        _playerData.runChoices = new System.Collections.Generic.List<GameChoice>();
        _playerData.runEquipments = new System.Collections.Generic.List<EquipmentRecord>();

        SavePlayerData();
        Debug.Log("[DataManager] New player data created");
    }

    /// <summary>
    /// 重置玩家数据
    /// </summary>
    public void ResetPlayerData()
    {
        GameLogger.Log("Data", "Reset");
        string filePath = Path.Combine(_savePath, "playerdata.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        CreateNewPlayerData();
        Debug.Log("[DataManager] Player data reset");
    }

    /// <summary>
    /// 删除存档数据
    /// </summary>
    public void DeleteSaveData()
    {
        string filePath = Path.Combine(_savePath, "playerdata.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        // 创建一个全新且干净的数据覆盖当前内存
        CreateNewPlayerData();
        Debug.Log("[DataManager] Player save data deleted and reset to initial state");
    }

    public long GetCurrencyAmount(string currencyId)
    {
        if (_playerData == null || string.IsNullOrEmpty(currencyId))
        {
            return 0;
        }

        EnsurePlayerDataDefaults();

        if (currencyId == CurrencyManager.GetCurrencyKey(CurrencyType.Gold))
        {
            return _playerData.catFood;
        }

        return GetCurrencyAmountInternal(currencyId);
    }

    private long GetCurrencyAmountInternal(string currencyId)
    {
        for (int i = 0; i < _playerData.currencies.Count; i++)
        {
            CurrencyData currency = _playerData.currencies[i];
            if (currency != null && currency.currencyId == currencyId)
            {
                return currency.amount;
            }
        }

        return 0;
    }

    public void SetCurrencyAmount(string currencyId, long amount, bool saveImmediately)
    {
        if (_playerData == null || string.IsNullOrEmpty(currencyId))
        {
            return;
        }

        EnsurePlayerDataDefaults();

        if (currencyId == CurrencyManager.GetCurrencyKey(CurrencyType.Gold))
        {
            _playerData.catFood = Math.Max(0L, amount);
            if (saveImmediately)
            {
                SavePlayerData();
            }
            return;
        }

        bool updated = false;
        for (int i = 0; i < _playerData.currencies.Count; i++)
        {
            CurrencyData currency = _playerData.currencies[i];
            if (currency == null || currency.currencyId != currencyId)
            {
                continue;
            }

            currency.amount = amount;
            _playerData.currencies[i] = currency;
            updated = true;
            break;
        }

        if (!updated)
        {
            _playerData.currencies.Add(new CurrencyData
            {
                currencyId = currencyId,
                amount = amount
            });
        }

        if (saveImmediately)
        {
            SavePlayerData();
        }
    }

    public void SaveCurrencyData()
    {
        SavePlayerData();
    }

    // --- CatSystem persistence helpers ---
    public CatRecord AddCat(CatRecord record, bool saveImmediately = true)
    {
        if (_playerData == null || record == null) return null;
        EnsurePlayerDataDefaults();
        if (record.id == 0) record.id = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _playerData.catRoster.Add(record);
        if (saveImmediately) SavePlayerData();
        return record;
    }

    public CatRecord GetCat(long id)
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.catRoster.Find(c => c != null && c.id == id);
    }

    public bool RemoveCat(long id, bool saveImmediately = true)
    {
        if (_playerData == null) return false;
        EnsurePlayerDataDefaults();
        var cat = _playerData.catRoster.Find(c => c != null && c.id == id);
        if (cat == null) return false;
        _playerData.catRoster.Remove(cat);
        if (saveImmediately) SavePlayerData();
        return true;
    }

    public OutingRequestRecord AddOutingRequest(OutingRequestRecord req, bool saveImmediately = true)
    {
        if (_playerData == null || req == null) return null;
        EnsurePlayerDataDefaults();
        if (req.requestId == 0) req.requestId = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _playerData.outingRequests.Add(req);
        if (saveImmediately) SavePlayerData();
        return req;
    }

    public System.Collections.Generic.List<OutingRequestRecord> GetOutingRequests()
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.outingRequests;
    }

    public PlayerArtifactInstance AddArtifactInstance(PlayerArtifactInstance inst, bool saveImmediately = true)
    {
        if (_playerData == null || inst == null) return null;
        EnsurePlayerDataDefaults();
        if (inst.instanceId == 0) inst.instanceId = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _playerData.playerArtifacts.Add(inst);
        if (saveImmediately) SavePlayerData();
        return inst;
    }

    public System.Collections.Generic.List<PlayerArtifactInstance> GetArtifactInstances()
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.playerArtifacts;
    }

    public RitualResultRecord AddRitualResult(RitualResultRecord record, bool saveImmediately = true)
    {
        if (_playerData == null || record == null) return null;
        EnsurePlayerDataDefaults();
        _playerData.ritualHistory.Add(record);
        if (saveImmediately) SavePlayerData();
        return record;
    }

    public BlessingRecord AddBlessing(BlessingRecord blessing, bool saveImmediately = true)
    {
        if (_playerData == null || blessing == null) return null;
        EnsurePlayerDataDefaults();
        _playerData.blessings.Add(blessing);
        if (saveImmediately) SavePlayerData();
        return blessing;
    }

    // --- Tribe persistence helpers ---
    public TribeRecord AddTribe(TribeRecord tribe, bool saveImmediately = true)
    {
        if (_playerData == null || tribe == null) return null;
        EnsurePlayerDataDefaults();
        if (tribe.tribeId < 0) tribe.tribeId = _playerData.tribes.Count;
        _playerData.tribes.Add(tribe);
        if (saveImmediately) SavePlayerData();
        return tribe;
    }

    public System.Collections.Generic.List<TribeRecord> GetTribes()
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.tribes;
    }

    public TribeRecord GetTribe(int tribeId)
    {
        if (_playerData == null) return null;
        EnsurePlayerDataDefaults();
        return _playerData.tribes.Find(t => t != null && t.tribeId == tribeId);
    }

    public bool RemoveTribe(int tribeId, bool saveImmediately = true)
    {
        if (_playerData == null) return false;
        EnsurePlayerDataDefaults();
        var tribe = _playerData.tribes.Find(t => t != null && t.tribeId == tribeId);
        if (tribe == null) return false;
        _playerData.tribes.Remove(tribe);
        if (saveImmediately) SavePlayerData();
        return true;
    }

    public int GetCurrentRound()
    {
        if (_playerData == null) return 1;
        EnsurePlayerDataDefaults();
        // 确保currentRound不为0（处理旧存档或未初始化的情况）
        if (_playerData.currentRound <= 0)
        {
            _playerData.currentRound = 1;
        }
        return _playerData.currentRound;
    }

    public void SetCurrentRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.currentRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public long GetCatFood()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.catFood;
    }

    public void SetCatFood(long amount, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.catFood = amount;
        if (saveImmediately) SavePlayerData();
    }

    public void AddCatFood(long amount, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.catFood += amount;
        if (saveImmediately) SavePlayerData();
    }

    public bool TrySpendCatFood(long amount, bool saveImmediately = true)
    {
        if (_playerData == null) return false;
        EnsurePlayerDataDefaults();
        if (_playerData.catFood < amount) return false;
        _playerData.catFood -= amount;
        if (saveImmediately) SavePlayerData();
        return true;
    }

    /// <summary>
    /// 清空本局获得的装备（新游戏开始时调用）
    /// </summary>
    public void ClearRunEquipment()
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.runChoices?.Clear();
        _playerData.runEquipments?.Clear();
        _playerData.ownedRelics?.Clear();
        SavePlayerData();
    }

    // --- 圣物方法 ---

    /// <summary>
    /// 添加圣物
    /// </summary>
    public void AddRelic(Camp.RelicRecord relic, bool saveImmediately = true)
    {
        EnsurePlayerDataDefaults();
        _playerData.ownedRelics.Add(relic);
        if (saveImmediately) SavePlayerData();
        Debug.Log($"[DataManager] 添加圣物: {relic.name}");
    }

    /// <summary>
    /// 获取已拥有的圣物列表
    /// </summary>
    public System.Collections.Generic.List<Camp.RelicRecord> GetOwnedRelics()
    {
        EnsurePlayerDataDefaults();
        return _playerData.ownedRelics;
    }

    // --- 主角属性方法 ---

    /// <summary>
    /// 获取领导力（决定人口上限）
    /// </summary>
    public int GetLeadership()
    {
        if (_playerData == null) return 3;
        EnsurePlayerDataDefaults();
        return _playerData.leadership;
    }

    /// <summary>
    /// 设置领导力
    /// </summary>
    public void SetLeadership(int value, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.leadership = Mathf.Max(1, value);
        if (saveImmediately) SavePlayerData();
    }

    /// <summary>
    /// 获取人口上限（等于领导力）
    /// </summary>
    public int GetPopulationCap()
    {
        return GetLeadership();
    }

    /// <summary>
    /// 获取街头情报（决定地图敌人信息准确度）
    /// </summary>
    public int GetStreetIntel()
    {
        if (_playerData == null) return 1;
        EnsurePlayerDataDefaults();
        return _playerData.streetIntel;
    }

    /// <summary>
    /// 设置街头情报
    /// </summary>
    public void SetStreetIntel(int value, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.streetIntel = Mathf.Max(0, value);
        if (saveImmediately) SavePlayerData();
    }

    /// <summary>
    /// 获取咪格魅力（影响招募成功率等）
    /// </summary>
    public int GetCharisma()
    {
        if (_playerData == null) return 1;
        EnsurePlayerDataDefaults();
        return _playerData.charisma;
    }

    /// <summary>
    /// 设置咪格魅力
    /// </summary>
    public void SetCharisma(int value, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.charisma = Mathf.Max(0, value);
        if (saveImmediately) SavePlayerData();
    }

    /// <summary>
    /// 获取主角经验值
    /// </summary>
    public int GetLeaderExp()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.leaderExp;
    }

    /// <summary>
    /// 获取升级所需经验值
    /// </summary>
    public int GetLeaderExpToNextLevel()
    {
        if (_playerData == null) return 100;
        EnsurePlayerDataDefaults();
        return _playerData.leaderExpToNextLevel;
    }

    /// <summary>
    /// 获取技能点
    /// </summary>
    public int GetLeaderSkillPoints()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.leaderSkillPoints;
    }

    /// <summary>
    /// 添加主角经验值，返回是否升级
    /// </summary>
    public bool AddLeaderExp(int exp, bool saveImmediately = true)
    {
        if (_playerData == null || exp <= 0) return false;
        EnsurePlayerDataDefaults();

        _playerData.leaderExp += exp;
        bool leveledUp = false;

        while (_playerData.leaderExp >= _playerData.leaderExpToNextLevel && _playerData.level < 99)
        {
            _playerData.leaderExp -= _playerData.leaderExpToNextLevel;
            LeaderLevelUp();
            leveledUp = true;
        }

        if (saveImmediately) SavePlayerData();
        return leveledUp;
    }

    /// <summary>
    /// 主角升级
    /// </summary>
    private void LeaderLevelUp()
    {
        _playerData.level++;

        // 领导力每级+1（上限20）
        if (_playerData.leadership < 20)
        {
            _playerData.leadership++;
        }

        // 每3级街头情报+1（上限3）
        if (_playerData.level % 3 == 0 && _playerData.streetIntel < 3)
        {
            _playerData.streetIntel++;
        }

        // 每5级咪格魅力+1（上限10）
        if (_playerData.level % 5 == 0 && _playerData.charisma < 10)
        {
            _playerData.charisma++;
        }

        // 获得技能点
        _playerData.leaderSkillPoints++;

        // 更新升级所需经验值（指数增长）
        _playerData.leaderExpToNextLevel = Mathf.RoundToInt(100 * Mathf.Pow(1.2f, _playerData.level - 1));

        Debug.Log($"[DataManager] 主角升级! 等级: {_playerData.level}, 领导力: {_playerData.leadership}, 街头情报: {_playerData.streetIntel}, 咪格魅力: {_playerData.charisma}");
    }

    /// <summary>
    /// 消耗技能点
    /// </summary>
    public bool SpendSkillPoints(int points, bool saveImmediately = true)
    {
        if (_playerData == null || points <= 0) return false;
        EnsurePlayerDataDefaults();

        if (_playerData.leaderSkillPoints < points) return false;

        _playerData.leaderSkillPoints -= points;
        if (saveImmediately) SavePlayerData();
        return true;
    }

    public int GetShopRefreshCount()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.shopRefreshCount;
    }

    public void SetShopRefreshCount(int count, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.shopRefreshCount = count;
        if (saveImmediately) SavePlayerData();
    }

    public void IncrementShopRefreshCount(bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.shopRefreshCount++;
        if (saveImmediately) SavePlayerData();
    }

    // --- Consumable inventory ---

    public System.Collections.Generic.List<ConsumableItem> GetConsumables()
    {
        if (_playerData == null) return new System.Collections.Generic.List<ConsumableItem>();
        EnsurePlayerDataDefaults();
        return _playerData.consumables;
    }

    public void AddConsumable(ConsumableItem item)
    {
        if (_playerData == null || item == null) return;
        EnsurePlayerDataDefaults();
        _playerData.consumables.Add(item);
        SavePlayerData();
    }

    public void RemoveConsumable(int id)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.consumables.RemoveAll(c => c.id == id);
        SavePlayerData();
    }

    public int GetConsumableCount()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.consumables.Count;
    }

    public int GetLastShopRound()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.lastShopRound;
    }

    public void SetLastShopRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.lastShopRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public bool IsRecruitmentCompletedForRound(int round)
    {
        if (_playerData == null) return false;
        return _playerData.recruitmentCompletedRound == round;
    }

    public void SetRecruitmentCompletedForRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.recruitmentCompletedRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public bool IsRitualCompletedForRound(int round)
    {
        if (_playerData == null) return false;
        return _playerData.ritualCompletedRound == round;
    }

    public void SetRitualCompletedForRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.ritualCompletedRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public bool IsNewTribeEventCompletedForRound(int round)
    {
        if (_playerData == null) return false;
        return _playerData.newTribeEventCompletedRound == round;
    }

    public void SetNewTribeEventCompletedForRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.newTribeEventCompletedRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public bool IsRandomEventCompletedForRound(int round)
    {
        if (_playerData == null) return false;
        return _playerData.randomEventCompletedRound == round;
    }

    public void SetRandomEventCompletedForRound(int round, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.randomEventCompletedRound = round;
        if (saveImmediately) SavePlayerData();
    }

    public int GetLastStandCount()
    {
        if (_playerData == null) return 0;
        EnsurePlayerDataDefaults();
        return _playerData.lastStandCount;
    }

    public void SetLastStandCount(int count, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        EnsurePlayerDataDefaults();
        _playerData.lastStandCount = count;
        if (saveImmediately) SavePlayerData();
    }

    public int GetLastExpandedTribeId()
    {
        if (_playerData == null) return -1;
        return _playerData.lastExpandedTribeId;
    }

    public void SetLastExpandedTribeId(int tribeId, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.lastExpandedTribeId = tribeId;
        if (saveImmediately) SavePlayerData();
    }

    private void EnsurePlayerDataDefaults()
    {
        if (_playerData == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_playerData.playerId))
        {
            _playerData.playerId = System.Guid.NewGuid().ToString();
        }

        if (_playerData.currencies == null)
        {
            _playerData.currencies = new System.Collections.Generic.List<CurrencyData>();
        }

        // 确保主角属性有默认值
        if (_playerData.leadership <= 0)
        {
            _playerData.leadership = 4;  // 领导力初始值4
        }
        if (_playerData.streetIntel <= 0)
        {
            _playerData.streetIntel = 1; // 街头情报初始值1
        }
        if (_playerData.charisma <= 0)
        {
            _playerData.charisma = 1;    // 咪格魅力初始值1
        }
        if (_playerData.leaderExp < 0)
        {
            _playerData.leaderExp = 0;   // 主角经验值初始值0
        }
        if (_playerData.leaderExpToNextLevel <= 0)
        {
            _playerData.leaderExpToNextLevel = 100; // 升级所需经验值初始值100
        }
        if (_playerData.leaderSkillPoints < 0)
        {
            _playerData.leaderSkillPoints = 0; // 技能点初始值0
        }

        // Ensure tribe collections exist
        if (_playerData.tribes == null)
        {
            _playerData.tribes = new System.Collections.Generic.List<TribeRecord>();
        }
        if (_playerData.ownedAffixes == null)
        {
            _playerData.ownedAffixes = new System.Collections.Generic.List<string>();
        }

        // 修复旧存档：确保每个族群的units列表不为null
        foreach (var tribe in _playerData.tribes)
        {
            if (tribe == null) continue;

            if (tribe.units == null)
            {
                tribe.units = new System.Collections.Generic.List<FighterData>();
            }
        }

        // 从 runChoices / runEquipments 重建单位的 ActiveBuffs
        RebuildAuraBuffs();

        if (_playerData.consumables == null)
        {
            _playerData.consumables = new System.Collections.Generic.List<ConsumableItem>();
        }

        if (_playerData.runChoices == null)
        {
            _playerData.runChoices = new System.Collections.Generic.List<GameChoice>();
        }

        if (_playerData.runEquipments == null)
        {
            _playerData.runEquipments = new System.Collections.Generic.List<EquipmentRecord>();
        }

        // Ensure new persistent collections exist for CatSystem integration (legacy)
        if (_playerData.catRoster == null)
        {
            _playerData.catRoster = new System.Collections.Generic.List<CatRecord>();
        }

        if (_playerData.outingRequests == null)
        {
            _playerData.outingRequests = new System.Collections.Generic.List<OutingRequestRecord>();
        }

        if (_playerData.playerArtifacts == null)
        {
            _playerData.playerArtifacts = new System.Collections.Generic.List<PlayerArtifactInstance>();
        }

        if (_playerData.ritualHistory == null)
        {
            _playerData.ritualHistory = new System.Collections.Generic.List<RitualResultRecord>();
        }

        if (_playerData.blessings == null)
        {
            _playerData.blessings = new System.Collections.Generic.List<BlessingRecord>();
        }

        if (_playerData.shopSession == null)
        {
            _playerData.shopSession = new ShopSessionRecord();
        }

        if (_playerData.ownedRelics == null)
        {
            _playerData.ownedRelics = new System.Collections.Generic.List<Camp.RelicRecord>();
        }

        if (_playerData.historyLog == null)
        {
            _playerData.historyLog = new HistoryLog();
        }
    }

    // ── 商店/抉择相关 accessor ──

    public float GetShopPriceModifier()
    {
        if (_playerData == null) return 1.0f;
        return _playerData.shopPriceModifier;
    }

    public void SetShopPriceModifier(float modifier, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.shopPriceModifier = modifier;
        if (saveImmediately) SavePlayerData();
    }

    public bool IsShopRefreshLocked()
    {
        if (_playerData == null) return false;
        return _playerData.shopRefreshLocked;
    }

    public void SetShopRefreshLocked(bool locked, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.shopRefreshLocked = locked;
        if (saveImmediately) SavePlayerData();
    }

    public int GetExtraWeatherCount()
    {
        if (_playerData == null) return 0;
        return _playerData.extraWeatherCount;
    }

    public void SetExtraWeatherCount(int count, bool saveImmediately = true)
    {
        if (_playerData == null) return;
        _playerData.extraWeatherCount = count;
        if (saveImmediately) SavePlayerData();
    }

    /// <summary>
    /// 公开接口：重建所有单位的 ActiveBuffs（强化、圣物、装备等）
    /// </summary>
    public void RebuildAllBuffs()
    {
        RebuildAuraBuffs();
    }

    /// <summary>
    /// 从 runChoices / runEquipments 重建 leader/cat 的 ActiveBuffs
    /// ActiveBuffs 是 [NonSerialized] 的，加载存档后需要从持久化的 runChoices 重建
    /// </summary>
    private void RebuildAuraBuffs()
    {
        if (_playerData.tribes == null) return;

        // 先清空所有单位的 ActiveBuffs，防止重复叠加
        foreach (var tribe in _playerData.tribes)
        {
            if (tribe?.units == null) continue;
            foreach (var unit in tribe.units)
                unit.ActiveBuffs.Clear();
        }

        Debug.Log($"[RebuildAuraBuffs] runChoices={_playerData.runChoices?.Count ?? 0}, runEquipments={_playerData.runEquipments?.Count ?? 0}, tribes={_playerData.tribes.Count}");

        // 收集所有需要应用的 aura buff（runChoices + runEquipments 中 BuffApplyType.Aura/CurrentUnit 的条目）
        var auraEntries = new System.Collections.Generic.List<GameChoice>();
        if (_playerData.runChoices != null)
        {
            foreach (var choice in _playerData.runChoices)
            {
                if ((ChoiceCategory)choice.category != ChoiceCategory.Buff) continue;
                // 重建 Aura 和 CurrentUnit 类型的 buff（CurrentUnit 也需持久化，否则存档加载后丢失）
                if ((BuffApplyType)choice.buffApplyType != BuffApplyType.Aura
                    && (BuffApplyType)choice.buffApplyType != BuffApplyType.CurrentUnit) continue;
                Debug.Log($"[RebuildAuraBuffs] runChoice: id={choice.choiceId}, name={choice.displayName}, type={choice.buffApplyType}, effects={choice.buffEffects?.Count ?? 0}");
                auraEntries.Add(choice);
            }
        }
        if (_playerData.runEquipments != null)
        {
            foreach (var equip in _playerData.runEquipments)
            {
                if ((BuffApplyType)equip.buffApplyType != BuffApplyType.Aura) continue;
                Debug.Log($"[RebuildAuraBuffs] runEquipment: id={equip.equipmentId}, name={equip.displayName}, type={equip.buffApplyType}, effects={equip.effects?.Count ?? 0}");
                auraEntries.Add(new GameChoice
                {
                    choiceId = equip.equipmentId,
                    displayName = equip.displayName,
                    description = equip.description,
                    buffScopeFilter = equip.buffScopeText,
                    buffScopeText = equip.buffScopeText,
                    buffApplyType = equip.buffApplyType,
                    buffEffects = equip.effects,
                    targetTribeType = (int)TribeType.None
                });
            }
        }

        Debug.Log($"[RebuildAuraBuffs] auraEntries count={auraEntries.Count}");
        if (auraEntries.Count == 0) return;

        foreach (var tribe in _playerData.tribes)
        {
            if (tribe == null || !tribe.isActive) continue;

            int buffCountBefore = 0;
            if (tribe.units != null) foreach (var u in tribe.units) buffCountBefore += u.ActiveBuffs?.Count ?? 0;

            foreach (var entry in auraEntries)
            {
                var filter = entry.GetScopeFilter();

                if (tribe.units != null)
                {
                    foreach (var unit in tribe.units)
                    {
                        if (filter.Matches(false, (TribeType)tribe.tribeType, unit.tier, unit.rarity))
                        {
                            ApplyAuraEffectsGeneric(unit, entry.buffEffects, entry.displayName, entry.choiceId, entry.description);
                        }
                    }
                }
            }

            int buffCountAfter = 0;
            if (tribe.units != null) foreach (var u in tribe.units) buffCountAfter += u.ActiveBuffs?.Count ?? 0;
            Debug.Log($"[RebuildAuraBuffs] Tribe {tribe.tribeType}: unit buffs {buffCountBefore}->{buffCountAfter}");
        }

        // ── 强化 buff：enhanceLevel == 1 时，按配置的 enhanceStatModifiers 添加属性修正 ──
        foreach (var tribe in _playerData.tribes)
        {
            if (tribe == null || !tribe.isActive || tribe.units == null) continue;
            foreach (var unit in tribe.units)
            {
                if (unit.enhanceLevel >= 1)
                {
                    ApplyEnhancementBuffs(unit);
                }
            }
        }

        // ── 圣物 buff：遍历已拥有的圣物，为匹配 mechanismTags 的兵种添加效果 ──
        if (_playerData.ownedRelics != null)
        {
            foreach (var relic in _playerData.ownedRelics)
            {
                if (relic.effects == null || string.IsNullOrEmpty(relic.mechanismTag)) continue;
                var relicTags = relic.mechanismTag.Split(',');
                foreach (var tribe in _playerData.tribes)
                {
                    if (tribe == null || !tribe.isActive || tribe.units == null) continue;
                    foreach (var unit in tribe.units)
                    {
                        var config = Camp.TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
                        if (config == null || config.mechanismTags == null) continue;
                        bool matched = false;
                        foreach (var tag in relicTags)
                        {
                            var trimmed = tag.Trim();
                            if (!string.IsNullOrEmpty(trimmed) && config.mechanismTags.Contains(trimmed))
                            {
                                matched = true;
                                break;
                            }
                        }
                        if (matched)
                        {
                            ApplyAuraEffectsGeneric(unit, relic.effects, relic.name, relic.relicId, relic.description);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 为强化兵种应用配置定义的属性修正 buff
    /// </summary>
    private static void ApplyEnhancementBuffs(Camp.IHasBuffs unit)
    {
        // 获取该单位的配置
        var fighterData = unit as FighterData;
        if (fighterData == null) return;

        var config = Camp.TribeConfigLoader.Instance?.GetFighterConfig(fighterData.fighterId);
        if (config == null || !config.HasEnhanceStatModifiers) return;

        foreach (var mod in config.enhanceStatModifiers)
        {
            if (string.IsNullOrEmpty(mod.statType)) continue;
            var statType = Camp.StatType.Attack;
            if (System.Enum.TryParse<Camp.StatType>(mod.statType, out var parsed))
                statType = parsed;

            var buff = Camp.UnifiedBuff.CreateStatBuff(
                $"enhance_{mod.statType}", "强化",
                Camp.BuffSource.Enhancement, "enhance",
                statType, mod.isPercent, mod.value);
            unit.AddUnifiedBuff(buff);
        }
    }

    private static void ApplyAuraEffectsGeneric(IHasBuffs unit, System.Collections.Generic.List<BuffEffectItem> effects, string displayName, string uniqueId, string description = null)
    {
        if (effects == null) return;
        foreach (var eff in effects)
        {
            var buff = UnifiedBuff.CreateStatBuff(
                $"aura_{uniqueId}_{eff.statType}", displayName,
                BuffSource.Equipment, uniqueId,
                eff.GetStatType(), eff.isPercent, eff.value,
                gameEffectType: (GameEffect)eff.gameEffectType,
                description: description);
            unit.AddUnifiedBuff(buff);
        }
    }

}

/// <summary>
/// 玩家数据结构
/// </summary>
[System.Serializable]
public class PlayerData
{
    public string playerId;
    public string playerName;
    public int level;
    public int currentLevel;
    public long lastSaveTime;
    public System.Collections.Generic.List<CurrencyData> currencies;

    // 主角属性
    public int leadership;          // 领导力 - 决定人口上限（初始值3，每升一级+1）
    public int streetIntel;         // 街头情报 - 决定地图敌人信息准确度
    public int charisma;            // 咪格魅力 - 影响招募成功率等
    public int leaderExp;           // 主角经验值
    public int leaderExpToNextLevel; // 升级所需经验值
    public int leaderSkillPoints;   // 技能点（每次升级获得1点）

    // Tribe persistent fields
    public System.Collections.Generic.List<TribeRecord> tribes;
    public int currentRound;
    public long catFood;
    public int shopRefreshCount;
    public int lastShopRound;
    public System.Collections.Generic.List<ConsumableItem> consumables;

    // 统一 Choice / Equipment 系统（本局记录）
    public System.Collections.Generic.List<GameChoice> runChoices;
    public System.Collections.Generic.List<EquipmentRecord> runEquipments;

    // 圣物系统（本局记录）
    public System.Collections.Generic.List<Camp.RelicRecord> ownedRelics;

    // 本回合事件完成标记（存回合号；与currentRound相同则表示本回合已完成）
    public int recruitmentCompletedRound;
    public int ritualCompletedRound;
    public int newTribeEventCompletedRound;
    public int randomEventCompletedRound;

    // 撸铁系统：已拥有的词缀ID列表
    public System.Collections.Generic.List<string> ownedAffixes;

    // 上一关的难度（用于判断是否触发双倍撸铁）
    public int lastBattleDifficulty;

    // Legacy Cat system persistent fields (kept for compatibility, marked obsolete)
    [System.Obsolete("Legacy compatibility")]
    public System.Collections.Generic.List<CatRecord> catRoster;
    [System.Obsolete("Legacy compatibility")]
    public System.Collections.Generic.List<OutingRequestRecord> outingRequests;
    [System.Obsolete("Legacy compatibility")]
    public System.Collections.Generic.List<PlayerArtifactInstance> playerArtifacts;
    [System.Obsolete("Legacy compatibility")]
    public System.Collections.Generic.List<RitualResultRecord> ritualHistory;
    [System.Obsolete("Legacy compatibility")]
    public System.Collections.Generic.List<BlessingRecord> blessings;
    [System.Obsolete("Legacy compatibility")]
    public ShopSessionRecord shopSession;
    public int lastStandCount;

    // 上次展开的族群ID（-1表示无）
    public int lastExpandedTribeId = -1;

    // 历史记录
    public HistoryLog historyLog;

    // ── 抉择事件对商店/天气的影响（每次新关卡重置） ──
    public float shopPriceModifier = 1.0f;   // 1.0=正常, 1.2=奸商陷阱加价
    public bool shopRefreshLocked = false;    // 奸商陷阱禁止刷新
    public int extraWeatherCount = 0;         // "我全要了"额外天气数
}

[System.Serializable]
public class CurrencyData
{
    public string currencyId;
    public long amount;
}

[System.Serializable]
public class CatRecord
{
    public long id;
    public int templateId;
    public string name;
    public bool nameChanged;
    public string gender;
    public int level;
    public int attack;
    public int defense;
    public int hp;
    public float moveSpeed;
    public int energy;
    public int energyMax;
    public System.Collections.Generic.List<int> skills;
    public System.Collections.Generic.List<int> traits;
    public System.Collections.Generic.List<long> parents;
    public System.Collections.Generic.List<long> children;
    public CatFlags flags;
    public long createdAt;
}

[System.Serializable]
public class CatFlags
{
    public bool isOutingRequested;
    public bool isOutingActive;
    public bool isDeployed;
    public bool deadPermanently;
}

[System.Serializable]
public class OutingRequestRecord
{
    public long requestId;
    public System.Collections.Generic.List<long> pairIds;
    public int initiatedCycle;
    public int returnCycle;
    public string status;
}

[System.Serializable]
public class PlayerArtifactInstance
{
    public long instanceId;
    public int artifactId;
    public long ownerCatId;
    public int remainingDurability;
    public long acquiredAt;
}

[System.Serializable]
public class RitualResultRecord
{
    public long requestId;
    public string offerType;
    public string selectedOptionId;
    public System.Collections.Generic.List<RewardEntry> rewards;
    public long timestamp;
}

[System.Serializable]
public class RewardEntry
{
    public string type;
    public string payloadJson;
}

[System.Serializable]
public class BlessingRecord
{
    public string id;
    public string name;
    public string effectType;
    public float effectValue;
    public int durationRounds;
    public bool persistent;
}

[System.Serializable]
public class ShopSessionRecord
{
    public int timesRefreshed;
    public long lastRefreshAt;
}