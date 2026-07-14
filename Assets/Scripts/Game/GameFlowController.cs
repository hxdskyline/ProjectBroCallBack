using UnityEngine;
using System;
using System.Collections.Generic;
using Camp;
using Combat;
using Combat.Avatar;
using Combat.Fighter;

/// <summary>
/// 游戏流程控制器 - 管理整个游戏的流程和状态转换
/// 负责协调各个游戏阶段：初始选择 → 选关 → 族群构筑 → 战斗 → 回合推进 → 游戏结束
/// </summary>
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    /// <summary>
    /// 游戏状态枚举
    /// </summary>
    public enum GameState
    {
        Uninitialized,      // 未初始化
        InitialSelection,   // 初始族群选择
        MapSelection,       // 选关（地图选择）
        RoundPreparation,   // 回合准备（族群构筑、命运、抉择、猫市）
        BattlePhase,        // 战斗阶段
        GameOver            // 游戏结束
    }

    // 事件系统
    public event Action<GameState> OnGameStateChanged;
    public event Action<int> OnRoundChanged;
    public event Action OnGameStarted;
    public event Action OnGameEnded;

    private GameState _currentState = GameState.Uninitialized;
    private GameManager _gameManager;
    private UIManager _uiManager;
    private DataManager _dataManager;
    private TribeBuildPanel _tribeBuildPanel;
    private RoundManager _roundManager;

    // 三区系统
    private TribeZoneService _zoneService;

    // 地图系统
    private MapGenerator _mapGenerator;
    private List<MapData> _mapDataList;
    private MapData _currentRegionMap;
    private int _currentRegion = 1;
    private int _currentNodeId = -1;
    private readonly List<FighterData> _lastBattleDeployedUnits = new List<FighterData>();
    private readonly List<int> _lastBattlePlayerFighterIds = new List<int>();
    private readonly List<int> _lastBattleEnemyFighterIds = new List<int>();

    private int _currentRound = 1;
    private bool _isGameStarted = false;
    private BattleFlowController _battleFlowController;

    // 构筑阶段三大系统
    private FateSystem _fateSystem;
    private ChoiceEventSystem _choiceEventSystem;
    private ShopSystem _shopSystem;

    public GameState CurrentState => _currentState;
    public int CurrentRound => _currentRound;
    public int CurrentRegion => _currentRegion;
    public bool IsGameStarted => _isGameStarted;

    public MapNode GetCurrentMapNode()
    {
        if (_currentRegionMap != null && _currentNodeId >= 0)
            return _currentRegionMap.GetNode(_currentNodeId);
        return null;
    }
    public MapData CurrentRegionMap => _currentRegionMap;
    public int CurrentNodeId => _currentNodeId;
    public TribeZoneService ZoneService => _zoneService;

    private void Awake()
    {
        // 单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[GameFlowController] Awake - initializing references");
    }

    private void Start()
    {
        // 在Start时获取引用，确保GameManager已完全初始化
        Debug.Log("[GameFlowController] Start - getting references");

        _gameManager = GameManager.Instance;
        Debug.Log($"[GameFlowController] GameManager.Instance: {(_gameManager != null ? "Found" : "NULL")}");

        if (_gameManager == null)
        {
            Debug.LogError("[GameFlowController] CRITICAL: GameManager.Instance is null!");
            Debug.LogError("[GameFlowController] Make sure GameManager is in the scene and initialized");
            return;
        }

        _uiManager = _gameManager.UIManager;
        Debug.Log($"[GameFlowController] UIManager: {(_uiManager != null ? "Found" : "NULL")}");

        _dataManager = _gameManager.DataManager;
        Debug.Log($"[GameFlowController] DataManager: {(_dataManager != null ? "Found" : "NULL")}");

        _roundManager = new RoundManager();
        _mapGenerator = new MapGenerator();
        _zoneService = new TribeZoneService();

        if (_uiManager == null)
        {
            Debug.LogError("[GameFlowController] UIManager not found!");
            return;
        }

        if (_dataManager == null)
        {
            Debug.LogError("[GameFlowController] DataManager not found!");
            return;
        }

        Debug.Log("[GameFlowController] Start - all references initialized successfully");
    }

    /// <summary>
    /// 初始化游戏流程控制器
    /// 由GameInitializer在系统初始化后调用
    /// </summary>
    public void Initialize()
    {
        GameLogger.Log("GFC", "Initialize");

        // 确保引用已初始化（如果Start()还没执行）
        if (_dataManager == null)
        {
            _gameManager = GameManager.Instance;
            _uiManager = _gameManager?.UIManager;
            _dataManager = _gameManager?.DataManager;

            if (_gameManager == null)
            {
                GameLogger.LogError("GFC", "Initialize: GameManager not found!");
                Debug.LogError("[GameFlowController] CRITICAL: GameManager not found!");
                return;
            }

            if (_dataManager == null)
            {
                GameLogger.LogError("GFC", "Initialize: DataManager not found");
                Debug.LogError("[GameFlowController] DataManager not found - cannot initialize game flow");
                return;
            }

            if (_uiManager == null)
            {
                GameLogger.LogError("GFC", "Initialize: UIManager not found");
                Debug.LogError("[GameFlowController] UIManager not found - cannot initialize game flow");
                return;
            }
        }

        // 初始化构筑阶段三大系统
        _fateSystem = new FateSystem();
        _fateSystem.Initialize();
        _choiceEventSystem = new ChoiceEventSystem();
        _choiceEventSystem.Initialize();
        _shopSystem = new ShopSystem();
        _shopSystem.Initialize();

        // 从存档加载当前回合
        int savedRound = _dataManager.GetCurrentRound();
        _roundManager.SetRound(savedRound);
        _currentRound = savedRound;

        // 检查是否需要初始族群选择
        bool isNewGame = _roundManager.CurrentRound == 1 &&
                        (_dataManager.GetTribes() == null ||
                         _dataManager.GetTribes().Count == 0);

        if (isNewGame)
        {
            GameLogger.Log("GFC", $"Initialize→NewGame round={_currentRound}");
            ChangeGameState(GameState.InitialSelection);
            SetupDefaultStartUnits();
            OnInitialTribeSelectionComplete();
        }
        else
        {
            GameLogger.Log("GFC", $"Initialize→Continue round={_currentRound}");
            EnterGameRound();
        }
    }

    /// <summary>
    /// 新游戏默认角色 — 橘猫(1001) + 长矛猫(1004)
    /// </summary>
    private void SetupDefaultStartUnits()
    {
        GameLogger.Log("GFC", "SetupDefaultStartUnits");

        // 确保配置已加载
        var loader = TribeConfigLoader.Instance;
        if (!loader.IsLoaded) loader.LoadAllConfigs();

        var dataManager = _dataManager;

        // 角色配置
        int[] fighterIds = { 1001, 1004 };
        foreach (int fid in fighterIds)
        {
            var cfg = loader.GetFighterConfig(fid);
            if (cfg == null)
            {
                GameLogger.LogError("GFC", $"Default fighter {fid} not found in config");
                continue;
            }

            // 确保对应族群的 TribeRecord 存在
            var tribes = dataManager.GetTribes();
            TribeRecord tribe = null;
            foreach (var t in tribes)
            {
                if (t.tribeType == cfg.tribeType) { tribe = t; break; }
            }
            if (tribe == null)
            {
                tribe = new TribeRecord
                {
                    tribeType = cfg.tribeType,
                    isActive = true
                };
                dataManager.AddTribe(tribe, false);
            }

            tribe.units.Add(new FighterData
            {
                fighterId = cfg.fighterId,
                tribeType = cfg.tribeType,
                tier = cfg.tier,
                name = cfg.fighterName,
                currentHp = cfg.hp,
                zone = (int)UnitZone.Deployed,
                rarity = cfg.rarity,
                enhanceLevel = cfg.enhanceLevel
            });

            GameLogger.Log("GFC", $"Added fighter={cfg.fighterName}({fid}) tribe={cfg.tribeType}");
        }

        dataManager.SavePlayerData();
    }

    /// <summary>
    /// 初始族群选择完成
    /// </summary>
    private void OnInitialTribeSelectionComplete()
    {
        GameLogger.Log("GFC", "OnInitialTribeSelectionComplete");

        OnGameStarted?.Invoke();
        _isGameStarted = true;

        // 生成整局地图
        GenerateFullMap();

        // 进入选关阶段
        EnterMapSelection();
    }

    /// <summary>
    /// 生成整局地图（3个大关）
    /// </summary>
    private void GenerateFullMap()
    {
        Debug.Log("[GameFlowController] 生成整局地图");
        _mapDataList = _mapGenerator.GenerateFullMap();

        // 设置第一个地区
        _currentRegion = 1;
        _currentRegionMap = _mapDataList[0];

        // 标记起点为Available
        if (_currentRegionMap.nodes.Count > 0)
        {
            _currentRegionMap.nodes[0].state = MapNodeState.Available;
        }

        // 应用初始迷雾：第1层可见，超过第4层的节点设为迷雾封锁
        _currentRegionMap.UpdateFog(1);

        Debug.Log($"[GameFlowController] 地图生成完成，共 {_mapDataList.Count} 个地区");
    }

    /// <summary>
    /// 进入选关阶段
    /// </summary>
    public void EnterMapSelection()
    {
        GameLogger.Log("GFC", $"EnterMapSelection region={_currentRegion}");
        ChangeGameState(GameState.MapSelection);

        if (_uiManager == null)
        {
            _uiManager = GameManager.Instance?.UIManager;
        }

        // 关闭旧 MapPanel，强制重建以刷新节点状态
        _uiManager.ClosePanel("MapPanel");

        // 显示地图面板
        var mapPanel = _uiManager?.ShowPanel<MapPanel>(UIManager.UILayer.Normal);

        if (mapPanel != null)
        {
            mapPanel.ShowMap(_currentRegionMap, _currentNodeId, OnMapNodeSelected);
        }
        else
        {
            Debug.LogWarning("[GameFlowController] MapPanel not found, using fallback");
            // 备用方案：自动选择第一个可用节点
            AutoSelectFirstAvailableNode();
        }
    }

    /// <summary>
    /// 备用方案：自动选择第一个可用节点
    /// </summary>
    private void AutoSelectFirstAvailableNode()
    {
        if (_currentRegionMap == null) return;

        var availableNodes = _currentRegionMap.GetAvailableNodes();
        if (availableNodes.Count > 0)
        {
            var firstNode = availableNodes[0];
            OnMapNodeSelected(firstNode.id, firstNode.nodeType);
        }
        else
        {
            Debug.LogError("[GameFlowController] No available nodes found");
        }
    }

    /// <summary>
    /// 地图节点选择完成
    /// </summary>
    private void OnMapNodeSelected(int nodeId, MapNodeType nodeType)
    {
        GameLogger.Log("GFC", $"OnMapNodeSelected nodeId={nodeId} type={nodeType}");

        _currentNodeId = nodeId;

        // 获取节点对应的关卡编号
        var node = _currentRegionMap.GetNode(nodeId);
        if (node != null)
        {
            _currentRound = node.battleNumber;
        }

        // 注意：不在这里标记 Visited，战斗胜利后才标记
        // 关闭构筑面板回到地图时，节点仍为 Available 状态

        // 进入回合准备阶段
        EnterGameRound();
    }

    /// <summary>
    /// 进入游戏回合 - 显示TribeBuildPanel
    /// </summary>
    public void EnterGameRound()
    {
        GameLogger.Log("GFC", $"EnterGameRound round={_currentRound}");

        GameLogger.Log("GFC", $"EnterGameRound round={_currentRound}");

        // 如果_uiManager是null，尝试从GameManager重新获取
        if (_uiManager == null)
        {
            Debug.LogWarning("[GameFlowController] _uiManager为null，尝试从GameManager重新获取...");
            _uiManager = GameManager.Instance?.UIManager;
        }

        ChangeGameState(GameState.RoundPreparation);

        // 关闭 MapPanel，避免遮挡构筑面板
        _uiManager.ClosePanel("MapPanel");

        if (_uiManager == null)
        {
            GameLogger.LogError("GFC", "EnterGameRound: UIManager null");
            Debug.LogError("[GameFlowController] UIManager not found - GameManager.UIManager也是null");
            return;
        }

        // 检查当前节点类型，如果是温泉节点，显示温泉界面
        if (_currentRegionMap != null && _currentNodeId >= 0)
        {
            var currentNode = _currentRegionMap.GetNode(_currentNodeId);
            if (currentNode != null && currentNode.nodeType == MapNodeType.HotSpring)
            {
                ShowHotSpringPanel();
                return;
            }

            // 商店节点：显示猫市面板
            if (currentNode != null && currentNode.nodeType == MapNodeType.Shop)
            {
                ShowShopNodePanel();
                return;
            }

            // 随机事件节点：显示抉择面板
            if (currentNode != null && currentNode.nodeType == MapNodeType.Event)
            {
                ShowEventNodePanel();
                return;
            }

            // 命运节点：显示命运面板
            if (currentNode != null && currentNode.nodeType == MapNodeType.Fate)
            {
                ShowFateNodePanel();
                return;
            }

            // Boss关：全员上阵（包括生产区单位）
            if (currentNode != null && currentNode.nodeType == MapNodeType.Boss)
            {
                Debug.Log("[GameFlowController] Boss关，全员上阵");
                if (_zoneService != null)
                {
                    _zoneService.ForceAllUnitsToBattle();
                }
            }
        }

        // 显示族群构筑主界面
        _tribeBuildPanel = _uiManager.ShowPanel<TribeBuildPanel>(UIManager.UILayer.Normal);

        if (_tribeBuildPanel == null)
        {
            GameLogger.LogError("GFC", "EnterGameRound: TribeBuildPanel show failed");
            Debug.LogError("[GameFlowController] Failed to show TribeBuildPanel");
        }
    }

    /// <summary>
    /// 显示温泉界面
    /// </summary>
    private void ShowHotSpringPanel()
    {
        Debug.Log("[GameFlowController] 显示温泉界面");

        // 显示温泉选择界面
        var hotSpringPanel = _uiManager?.ShowPanel<HotSpringPanel>(UIManager.UILayer.Normal);

        if (hotSpringPanel != null)
        {
            hotSpringPanel.ShowHotSpring(() =>
            {
                // 温泉选择完成后，标记节点已完成，回到地图选下一关
                CompleteNonBattleNode();
            });
        }
        else
        {
            Debug.LogWarning("[GameFlowController] HotSpringPanel not found, skip healing");
            CompleteNonBattleNode();
        }
    }

    /// <summary>
    /// 显示猫市面板（地图上的商店节点）
    /// </summary>
    private void ShowShopNodePanel()
    {
        Debug.Log("[GameFlowController] 显示猫市面板（节点）");

        // 确保实例已初始化
        if (_shopSystem == null)
        {
            _shopSystem = new ShopSystem();
            _shopSystem.Initialize();
        }
        _shopSystem.ResetForNewRound();
        _shopSystem.GenerateInventory();

        var shopPanel = _uiManager?.ShowPanel<ShopPanel>(UIManager.UILayer.Normal);
        if (shopPanel != null)
        {
            shopPanel.ShowShop(_shopSystem, () =>
            {
                CompleteNonBattleNode();
            });
        }
        else
        {
            Debug.LogWarning("[GameFlowController] ShopPanel not found, skip");
            CompleteNonBattleNode();
        }
    }

    /// <summary>
    /// 显示抉择事件面板（地图上的事件节点）
    /// </summary>
    private void ShowEventNodePanel()
    {
        Debug.Log("[GameFlowController] 显示抉择事件面板（节点）");

        if (_choiceEventSystem == null)
        {
            _choiceEventSystem = new ChoiceEventSystem();
            _choiceEventSystem.Initialize();
        }

        var evt = _choiceEventSystem.GetEventForLevel(_currentRound);
        if (evt == null)
        {
            Debug.LogWarning("[GameFlowController] 无抉择事件，跳过");
            CompleteNonBattleNode();
            return;
        }

        var panel = _uiManager?.ShowPanel<RandomEventPanel>(UIManager.UILayer.Normal);
        if (panel != null)
        {
            panel.ShowChoiceEvent(evt, _choiceEventSystem, () =>
            {
                CompleteNonBattleNode();
            });
        }
        else
        {
            Debug.LogWarning("[GameFlowController] RandomEventPanel not found, skip");
            CompleteNonBattleNode();
        }
    }

    /// <summary>
    /// 显示命运面板（地图上的命运节点）
    /// </summary>
    private void ShowFateNodePanel()
    {
        Debug.Log("[GameFlowController] 显示命运面板（节点）");

        if (_fateSystem == null)
        {
            _fateSystem = new FateSystem();
            _fateSystem.Initialize();
        }

        var panel = _uiManager?.ShowPanel<FatePanel>(UIManager.UILayer.Normal);
        if (panel != null)
        {
            panel.ShowFate(_fateSystem, () =>
            {
                CompleteNonBattleNode();
            });
        }
        else
        {
            Debug.LogWarning("[GameFlowController] FatePanel not found, skip");
            CompleteNonBattleNode();
        }
    }

    /// <summary>
    /// 非战斗节点（温泉/商店/事件）完成后，标记已访问并回到地图
    /// </summary>
    private void CompleteNonBattleNode()
    {
        if (_currentRegionMap != null && _currentNodeId >= 0)
        {
            _currentRegionMap.MarkNodeVisited(_currentNodeId);
            _currentRegionMap.UpdateAvailableNodes(_currentNodeId);
        }
        EnterMapSelection();
    }

    /// <summary>
    /// 备用方案：自动回复所有单位50%HP
    /// </summary>
    private void AutoHealAllUnits()
    {
        var healthSystem = new HealthPersistenceSystem();
        healthSystem.HealAllAlliesPercent(0.5f);
        Debug.Log("[GameFlowController] 自动回复所有单位50%HP");
    }

    /// <summary>
    /// 开始战斗阶段
    /// 由TribeBuildPanel或BattlePreparePanel调用
    /// </summary>
    public void EnterBattlePhase()
    {
        GameLogger.Log("GFC", $"EnterBattlePhase round={_currentRound}");
        ChangeGameState(GameState.BattlePhase);

        // 关闭战斗准备面板
        if (_uiManager != null)
            _uiManager.ClosePanel("BattlePreparePanel");

        // 获取战斗数据
        var campaign = GameManager.Instance.BattleCampaignRuntime;
        int battleNumber = _currentRound;

        // 敌方数据：优先使用节点预生成的敌人，否则回退到关卡配置
        int[] enemyIds = null;
        bool useNodeEnemyIds = true;
        if (campaign.GetEnemyUnitVariantsForBattle(battleNumber) != null)
            useNodeEnemyIds = false;

        if (useNodeEnemyIds && _currentRegionMap != null && _currentNodeId >= 0)
        {
            var currentNode = _currentRegionMap.GetNode(_currentNodeId);
            if (currentNode?.enemyUnitIds != null && currentNode.enemyUnitIds.Length > 0)
                enemyIds = currentNode.enemyUnitIds;
        }
        if (enemyIds == null)
            enemyIds = campaign.GetEnemyUnitIdsForBattle(battleNumber);

        // 保存实际出战敌人ID（用于招募）
        _lastBattleEnemyFighterIds.Clear();
        if (enemyIds != null)
            _lastBattleEnemyFighterIds.AddRange(enemyIds);

        var enemyStats = campaign.GetEnemyStats(battleNumber, DifficultyLevel.Normal);
        var scenarios = campaign.GetScenarioOptions(battleNumber);
        var scenario = scenarios.Count > 0 ? scenarios[0] : default;
        int enemyCount = enemyIds != null && enemyIds.Length > 0 ? enemyIds.Length : 3;

        // Avatar 定义
        var enemyAvatar = LoadAvatarDefinition("enemy");

        // 尝试从 fighter_config 构建每个敌人的独立定义（支持混合敌人类型）
        BattleFighterSpawnDefinition[] enemyDefs = null;
        if (enemyIds != null && enemyIds.Length > 0)
        {
            var defs = new List<BattleFighterSpawnDefinition>();
            foreach (int id in enemyIds)
            {
                var cfg = Camp.TribeConfigLoader.Instance?.GetFighterConfig(id);
                if (cfg != null)
                {
                    // 尝试加载该兵种的专属 avatar，失败则用通用 enemy avatar
                    string address = $"data/avatar/definitions/{cfg.avatarId.ToLower()}_avataranimdef";
                    var avatar = GameManager.Instance.ResourceManager.LoadResource<AvatarAnimationDefinition>(address);
                    if (avatar == null)
                        avatar = enemyAvatar;
                    defs.Add(new BattleFighterSpawnDefinition(
                        cfg.fighterName, cfg.ToStaticAttributes(), avatar,
                        1.0f, (Camp.TribeType)cfg.tribeType, cfg.fighterId));
                }
            }
            if (defs.Count == enemyIds.Length)
                enemyDefs = defs.ToArray();
        }
        var playerDefs = BuildPlayerFighterDefinitions(enemyAvatar);

        // 如果没有玩家单位，用默认 avatar 生成
        if (playerDefs == null || playerDefs.Length == 0)
        {
            GameLogger.LogWarning("GFC", "No player fighters, using default");
            var defaultAvatar = AvatarAnimationDefinition.CreateRuntime(
                "hero", "avatartemp/hero/idle/idle_01", "avatartemp/hero/attack/attack_01");
            playerDefs = new BattleFighterSpawnDefinition[]
            {
                new BattleFighterSpawnDefinition("默认战士", UnitStaticAttributes.Default, defaultAvatar)
            };
        }

        GameLogger.Log("GFC", $"BattleStart enemies={enemyCount} scenario={scenario.terrain}/{scenario.weather} fromDefs={enemyDefs != null}");

        // 是否有敌方看板
        bool hasEnemyBillboard = campaign.HasEnemyBillboardForBattle(battleNumber);

        // 启动战斗
        _battleFlowController = new BattleFlowController();
        _battleFlowController.StartBattle(
            levelId: battleNumber,
            fighterPrefab: null,
            playerDefinition: playerDefs[0].AvatarDefinition ?? enemyAvatar,
            enemyDefinition: enemyAvatar,
            enemyFighterCount: enemyCount,
            playerFighterDefinitions: playerDefs,
            onBattleEnded: OnBattleEndedFromScene,
            enemyStats: enemyDefs == null ? enemyStats : null,
            terrain: scenario.terrain,
            weather: scenario.weather,
            enemyDefinitions: enemyDefs,
            hasEnemyBillboard: hasEnemyBillboard);
    }

    private BattleFighterSpawnDefinition[] BuildPlayerFighterDefinitions(AvatarAnimationDefinition fallbackAvatar)
    {
        var tribes = _dataManager.GetTribes();
        var playerDefs = new List<BattleFighterSpawnDefinition>();

        foreach (var tribe in tribes)
        {
            foreach (var unit in tribe.units)
            {
                if ((UnitZone)unit.zone != UnitZone.Deployed) continue;

                var cfg = TribeConfigLoader.Instance.GetFighterConfig(unit.fighterId);
                if (cfg == null) continue;

                var avatar = LoadAvatarDefinition(cfg.avatarId);
                var attrs = cfg.ToStaticAttributes();
                // 使用当前 HP（战后可能不满血），不超过有效最大值
                int hpCap = cfg.GetEffectiveMaxHp(unit.enhanceLevel);
                attrs.MaxHp = unit.currentHp > 0 ? (int)Mathf.Min(unit.currentHp, hpCap) : cfg.hp;

                playerDefs.Add(new BattleFighterSpawnDefinition(
                    cfg.fighterName,
                    attrs,
                    avatar,
                    scaleMultiplier: 1.0f,
                    tribeType: (TribeType)cfg.tribeType,
                    fighterId: cfg.fighterId)
                {
                    AuraBuffs = unit.ActiveBuffs,
                    EnhanceLevel = unit.enhanceLevel
                });
            }
        }

        return playerDefs.ToArray();
    }

    public AvatarAnimationDefinition LoadAvatarDefinition(string avatarId)
    {
        string address = $"data/avatar/definitions/{avatarId.ToLower()}_avataranimdef";
        var def = GameManager.Instance.ResourceManager.LoadResource<AvatarAnimationDefinition>(address);
        if (def != null)
            return def;

        // 加载失败，用 CreateRuntime 兜底
        GameLogger.LogWarning("GFC", $"Avatar not found: {address}, using CreateRuntime");
        return AvatarAnimationDefinition.CreateRuntime(
            avatarId,
            $"avatartemp/{avatarId}1",
            $"avatartemp/{avatarId}2");
    }

    /// <summary>
    /// 从战斗准备阶段进入战斗（复用已有的 BattleManager，在玩家指定位置生成单位）
    /// </summary>
    public void EnterBattlePhaseFromPreparation(
        Combat.BattleFlowController existingFlowController,
        Combat.BattleManager existingManager,
        List<(FighterData unit, Vector3 worldPos)> deployedPositions,
        int battleNumber)
    {
        GameLogger.Log("GFC", "EnterBattlePhaseFromPreparation");
        ChangeGameState(GameState.BattlePhase);

        if (_uiManager != null)
            _uiManager.ClosePanel("BattlePreparePanel");

        // 保存实际出战敌人ID（用于招募）
        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        int[] enemyIds = null;
        if (campaign != null)
        {
            bool useNodeEnemyIds = campaign.GetEnemyUnitVariantsForBattle(battleNumber) == null;
            if (useNodeEnemyIds && _currentRegionMap != null && _currentNodeId >= 0)
            {
                var currentNode = _currentRegionMap.GetNode(_currentNodeId);
                if (currentNode?.enemyUnitIds != null && currentNode.enemyUnitIds.Length > 0)
                    enemyIds = currentNode.enemyUnitIds;
            }
            if (enemyIds == null)
                enemyIds = campaign.GetEnemyUnitIdsForBattle(battleNumber);
        }
        _lastBattleEnemyFighterIds.Clear();
        if (enemyIds != null)
            _lastBattleEnemyFighterIds.AddRange(enemyIds);

        // 构建玩家单位定义
        var playerDefs = new List<Combat.Fighter.BattleFighterSpawnDefinition>();
        var playerPositions = new List<Vector3>();
        _lastBattleDeployedUnits.Clear();

        foreach (var (unit, worldPos) in deployedPositions)
        {
            var cfg = Camp.TribeConfigLoader.Instance.GetFighterConfig(unit.fighterId);
            if (cfg == null) continue;

            var avatar = LoadAvatarDefinition(cfg.avatarId);
            var attrs = cfg.ToStaticAttributes();
            int hpCap = cfg.GetEffectiveMaxHp(unit.enhanceLevel);
            attrs.MaxHp = unit.currentHp > 0 ? (int)Mathf.Max(unit.currentHp, hpCap) : cfg.hp;

            var def = new Combat.Fighter.BattleFighterSpawnDefinition(
                cfg.fighterName, attrs, avatar,
                1.0f, (Camp.TribeType)cfg.tribeType, cfg.fighterId);
            def.CurrentHp = (int)unit.currentHp;
            playerDefs.Add(def);
            playerPositions.Add(worldPos);
            _lastBattleDeployedUnits.Add(unit);
        }

        // 加载默认玩家 avatar（用于 ConfigureDemoAvatars）
        var defaultPlayerAvatar = LoadAvatarDefinition(
            Camp.TribeConfigLoader.Instance.GetFighterConfig(1001)?.avatarId ?? "dajuleader");
        existingManager.ConfigureDemoAvatars(defaultPlayerAvatar, null);

        // 在指定位置添加玩家单位
        existingManager.AddPlayerFighters(
            playerDefs.ToArray(),
            playerPositions.ToArray());

        // 开始战斗模拟
        existingManager.StartBattleFromPrepare();

        _battleFlowController = existingFlowController;
        existingFlowController.BattleManager.BattleEnded += OnBattleEndedFromScene;
    }

    /// <summary>
    /// 取消战斗，返回布阵界面重新部署
    /// </summary>
    public void ReturnToPreparation()
    {
        GameLogger.Log("GFC", "ReturnToPreparation");

        // 取消当前战斗（不同步 HP、不触发 BattleEnded）
        if (_battleFlowController != null)
        {
            var bm = _battleFlowController.BattleManager;
            if (bm != null)
            {
                bm.BattleEnded -= OnBattleEndedFromScene;
                bm.CancelBattle();
            }
            _battleFlowController.StopAndDispose(null);
            _battleFlowController = null;
        }

        ChangeGameState(GameState.RoundPreparation);

        // 重新显示战斗准备面板
        ShowBattlePreparePanel(_currentRound);
    }

    /// <summary>
    /// 显示战斗准备面板
    /// </summary>
    private void ShowBattlePreparePanel(int battleNumber)
    {
        if (_uiManager == null)
            _uiManager = GameManager.Instance?.UIManager;

        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        var nodeType = _currentRegionMap?.GetNode(_currentNodeId)?.nodeType ?? MapNodeType.Battle;

        var panel = _uiManager?.ShowPanel<BattlePreparePanel>(UIManager.UILayer.Normal);
        if (panel != null)
        {
            panel.Setup(battleNumber, nodeType);
        }
    }

    private void OnBattleEndedFromScene(bool victory)
    {
        GameLogger.Log("GFC", $"BattleEndedFromScene victory={victory}");

        // 在销毁前收集战斗统计
        var battleStats = CollectBattleStats();

        // 销毁战斗场景
        if (_battleFlowController != null)
        {
            _battleFlowController.StopAndDispose(OnBattleEndedFromScene);
            _battleFlowController = null;
        }

        // 调用原有的战斗结束逻辑
        OnBattleEnded(victory, battleStats);
    }

    /// <summary>
    /// 战斗结束回调（由TribeBuildPanel调用）
    /// </summary>
    public void OnBattleEnded(bool victory, List<FighterBattleStats> battleStats = null)
    {
        GameLogger.Log("GFC", $"OnBattleEnded victory={victory}");

        if (battleStats == null)
            battleStats = CollectBattleStats();

        // 计算奖励数据
        int expReward = 0;
        int catFoodReward = 0;
        if (victory)
        {
            // 在 RecoverRestingUnitsAfterVictory 清空 _lastBattleDeployedUnits 之前，保存己方出战猫ID
            _lastBattlePlayerFighterIds.Clear();
            foreach (var unit in _lastBattleDeployedUnits)
                _lastBattlePlayerFighterIds.Add(unit.fighterId);

            RecoverRestingUnitsAfterVictory();
            expReward = 50 + _currentRound * 10;
            bool isBossBattle = _currentRegionMap != null &&
                               _currentNodeId >= 0 &&
                               _currentRegionMap.GetNode(_currentNodeId)?.nodeType == MapNodeType.Boss;
            if (isBossBattle) expReward *= 3;
        }

        // 根据节点类型确定难度，获取关卡奖励（失败时为胜利奖励的一半）
        var nodeType = _currentRegionMap?.GetNode(_currentNodeId)?.nodeType ?? MapNodeType.Battle;
        var difficulty = nodeType switch
        {
            MapNodeType.Boss => DifficultyLevel.Boss,
            MapNodeType.EliteBattle => DifficultyLevel.Hard,
            _ => DifficultyLevel.Normal
        };
        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        if (campaign != null)
        {
            int fullReward = campaign.GetCatFoodReward(_currentRound, difficulty);
            catFoodReward = victory ? fullReward : fullReward / 2;
        }

        // 显示结算面板，等待玩家确认后继续
        ShowBattleResultPanel(victory, _currentRound, expReward, catFoodReward, battleStats);
    }

    /// <summary>
    /// 收集我方战斗统计数据
    /// </summary>
    private System.Collections.Generic.List<FighterBattleStats> CollectBattleStats()
    {
        var stats = new System.Collections.Generic.List<FighterBattleStats>();
        var bm = _battleFlowController?.BattleManager;
        if (bm == null) return stats;

        var fighters = bm.PlayerFighters;
        if (fighters == null) return stats;

        foreach (var f in fighters)
        {
            if (f == null) continue;
            var cfg = Camp.TribeConfigLoader.Instance?.GetFighterConfig(f.FighterId);
            stats.Add(new FighterBattleStats
            {
                fighterId = f.FighterId,
                name = f.Name ?? (cfg?.fighterName ?? "???"),
                avatarId = cfg?.avatarId ?? "",
                totalDamageDealt = f.TotalDamageDealt,
                totalDamageTaken = f.TotalDamageTaken,
                totalHealingDone = f.TotalHealingDone
            });
        }
        return stats;
    }

    /// <summary>
    /// 显示战斗结算面板
    /// </summary>
    private void ShowBattleResultPanel(bool victory, int battleNumber, int expReward, int catFoodReward, List<FighterBattleStats> battleStats)
    {
        if (_uiManager == null)
            _uiManager = GameManager.Instance?.UIManager;

        var panel = _uiManager.ShowPanel<BattleResultPanel>(UIManager.UILayer.Top);
        panel.Setup(victory, battleNumber, expReward, catFoodReward, battleStats, () => ContinueAfterResult(victory));
    }

    /// <summary>
    /// 结算面板关闭后继续流程
    /// </summary>
    private void ContinueAfterResult(bool victory)
    {
        if (victory)
        {
            // 战后将所有已部署单位移回待上阵区，下轮重新部署
            ResetDeployedUnitsToStandby();

            // 标记节点已访问，解锁后续节点
            if (_currentRegionMap != null && _currentNodeId >= 0)
            {
                _currentRegionMap.MarkNodeVisited(_currentNodeId);
                _currentRegionMap.UpdateAvailableNodes(_currentNodeId);
            }

            // 结算生产区产出
            if (_zoneService != null)
            {
                int productionOutput = _zoneService.SettleProductionOutput();
                Debug.Log($"[GameFlowController] 生产区产出: {productionOutput} 木天蓼叶");
            }

            // 经验奖励
            GrantBattleExpReward();

            bool isBossBattle = _currentRegionMap != null &&
                               _currentNodeId >= 0 &&
                               _currentRegionMap.GetNode(_currentNodeId)?.nodeType == MapNodeType.Boss;

            if (isBossBattle)
            {
                // Boss关：先展示Boss奖励（稀有兵种三选一 + Boss圣物），再普通招募，然后切换到下一地区并选关
                ShowBossRareFighterReward(() =>
                {
                    ShowBossRelicReward(() =>
                    {
                        ShowBattleResultRecruitment(() =>
                        {
                            // 切换到下一地区
                            _currentRegion++;
                            if (_currentRegion <= _mapDataList.Count)
                            {
                                _currentRegionMap = _mapDataList[_currentRegion - 1];
                                if (_currentRegionMap.nodes.Count > 0)
                                {
                                    _currentRegionMap.nodes[0].state = MapNodeState.Available;
                                }
                                _currentNodeId = -1;
                            }
                        });
                    });
                });
            }
            else
            {
                ShowBattleResultRecruitment(null);
            }
        }
        else
        {
            _lastBattleDeployedUnits.Clear();
            bool isBossBattle = _currentRegionMap != null &&
                               _currentNodeId >= 0 &&
                               _currentRegionMap.GetNode(_currentNodeId)?.nodeType == MapNodeType.Boss;
            if (isBossBattle)
            {
                EndGame();
            }
            else
            {
                // 普通关失败：回到地图选关（可重新选择关卡）
                EnterMapSelection();
            }
        }
    }

    /// <summary>
    /// 战后将所有已部署单位移回待上阵区
    /// </summary>
    private void ResetDeployedUnitsToStandby()
    {
        if (_dataManager == null) return;
        var tribes = _dataManager.GetTribes();
        if (tribes == null) return;

        foreach (var tribe in tribes)
        {
            if (tribe?.units == null) continue;
            foreach (var unit in tribe.units)
            {
                if (unit.GetZone() == UnitZone.Deployed)
                    unit.SetZone(UnitZone.Standby);
            }
        }
    }

    private void RecoverRestingUnitsAfterVictory()
    {
        if (_dataManager == null) return;

        var tribes = _dataManager.GetTribes();
        if (tribes == null) return;

        var healedUnits = 0;
        foreach (var tribe in tribes)
        {
            if (tribe?.units == null) continue;

            foreach (var unit in tribe.units)
            {
                if (unit == null || _lastBattleDeployedUnits.Contains(unit)) continue;
                if (unit.GetZone() == UnitZone.Production) continue;

                var cfg = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
                if (cfg == null) continue;

                int maxHp = cfg.GetEffectiveMaxHp(unit.enhanceLevel);
                int healAmount = Mathf.RoundToInt(maxHp * 0.5f);
                unit.currentHp = Mathf.Min(unit.currentHp + healAmount, maxHp);
                healedUnits++;
            }
        }

        if (healedUnits > 0)
        {
            _dataManager.SavePlayerData();
            GameLogger.Log("GFC", $"RecoverRestingUnitsAfterVictory healed={healedUnits}");
        }

        _lastBattleDeployedUnits.Clear();
    }

    /// <summary>
    /// 战斗胜利经验奖励
    /// </summary>
    private void GrantBattleExpReward()
    {
        if (_dataManager == null) return;

        // 基础经验 = 50 + 关卡数 * 10
        int baseExp = 50 + _currentRound * 10;

        // Boss关额外经验
        bool isBossBattle = _currentRegionMap != null &&
                           _currentNodeId >= 0 &&
                           _currentRegionMap.GetNode(_currentNodeId)?.nodeType == MapNodeType.Boss;
        if (isBossBattle)
        {
            baseExp *= 3;
        }

        bool leveledUp = _dataManager.AddLeaderExp(baseExp);
        Debug.Log($"[GameFlowController] 战斗经验奖励: {baseExp}{(leveledUp ? " (升级了!)" : "")}");
    }

    /// <summary>
    /// 显示战斗后招募界面（使用 RecruitmentSelectPanel）
    /// </summary>
    /// <param name="onComplete">招募+构筑全部完成后回调（Boss关用于切换地区）</param>
    private void ShowBattleResultRecruitment(Action onComplete)
    {
        var recruitmentSystem = new RecruitmentDiceSystem();
        var cards = recruitmentSystem.GenerateRecruitmentCards(_lastBattleEnemyFighterIds, _lastBattlePlayerFighterIds);

        if (cards == null || cards.Count == 0)
        {
            Debug.Log("[GameFlowController] 没有生成招募卡片，进入构筑阶段");
            EnterBuildPhase(onComplete);
            return;
        }

        var panel = _uiManager?.ShowPanel<RecruitmentSelectPanel>(UIManager.UILayer.PopUp);
        if (panel == null)
        {
            EnterBuildPhase(onComplete);
            return;
        }

        panel.ShowRecruitment(
            cards,
            onSelected: card =>
            {
                return recruitmentSystem.RecruitUnit(card);
            },
            onSkipped: () =>
            {
                EnterBuildPhase(onComplete);
            },
            title: "招募",
            skipText: "完成招募");
    }

    /// <summary>
    /// 进入构筑阶段（命运/抉择/猫市），完成后进入选关
    /// 文档：每关流程 = 战斗准备→战斗→构筑→选关
    /// 弹出优先级由 BattleCampaignRuntime.GetSortedPopupEvents 决定：命运(20) > 抉择(10) > 猫市(0)
    /// </summary>
    /// <param name="onComplete">构筑+选关全部完成后回调（Boss关用于切换地区）</param>
    private void EnterBuildPhase(Action onComplete = null)
    {
        GameLogger.Log("GFC", "EnterBuildPhase");
        ChangeGameState(GameState.RoundPreparation);

        if (_uiManager == null)
            _uiManager = GameManager.Instance?.UIManager;

        if (_uiManager == null)
        {
            GameLogger.LogError("GFC", "EnterBuildPhase: UIManager null");
            EnterMapSelection();
            onComplete?.Invoke();
            return;
        }

        // 关闭旧面板
        _uiManager.ClosePanel("MapPanel");

        // 根据关卡配置获取本关需要弹出的构筑事件（按优先级排序）
        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        var events = campaign?.GetSortedPopupEvents(_currentRound);

        // 过滤掉当前层地图节点中已有的特殊事件（避免重复触发）
        if (events != null && events.Count > 0 && _currentRegionMap != null)
        {
            var nodeEvents = GetMapNodeEventsForCurrentLayer();
            if (nodeEvents.Count > 0)
            {
                events.RemoveAll(e => nodeEvents.Contains(e));
                GameLogger.Log("GFC", $"EnterBuildPhase: filtered map node events: {string.Join(",", nodeEvents)}");
            }
        }

        if (events == null || events.Count == 0)
        {
            // 无构筑事件，直接进入选关
            EnterMapSelection();
            onComplete?.Invoke();
            return;
        }

        // 依次处理事件队列，最后进入选关
        ProcessBuildPhaseEvents(events, 0, onComplete);
    }

    /// <summary>
    /// 获取当前层地图节点中已有的特殊事件类型（用于过滤构筑阶段重复事件）
    /// </summary>
    private HashSet<string> GetMapNodeEventsForCurrentLayer()
    {
        var result = new HashSet<string>();
        if (_currentRegionMap == null) return result;

        // 获取当前层（battleNumber 相同）的所有节点
        int currentBattleNum = _currentRound;
        foreach (var node in _currentRegionMap.nodes)
        {
            if (node.battleNumber != currentBattleNum) continue;
            switch (node.nodeType)
            {
                case MapNodeType.Shop:
                    result.Add("shop");
                    break;
                case MapNodeType.Fate:
                    result.Add("ritual");
                    break;
                case MapNodeType.Event:
                    result.Add("randomEvent");
                    break;
            }
        }
        return result;
    }

    /// <summary>
    /// 递归处理构筑事件队列
    /// </summary>
    private void ProcessBuildPhaseEvents(List<string> events, int index, Action onComplete = null)
    {
        if (index >= events.Count)
        {
            // 所有事件处理完毕，进入选关
            EnterMapSelection();
            onComplete?.Invoke();
            return;
        }

        string eventType = events[index];
        GameLogger.Log("GFC", $"ProcessBuildPhaseEvents [{index}] = {eventType}");

        switch (eventType)
        {
            case "ritual":
                ShowFatePanel(() => ProcessBuildPhaseEvents(events, index + 1, onComplete));
                break;
            case "randomEvent":
                ShowChoicePanel(() => ProcessBuildPhaseEvents(events, index + 1, onComplete));
                break;
            case "shop":
                ShowShopPanel(() => ProcessBuildPhaseEvents(events, index + 1, onComplete));
                break;
            default:
                // 未知事件类型（如 newTribeEvent/recruitment），跳过
                ProcessBuildPhaseEvents(events, index + 1, onComplete);
                break;
        }
    }

    /// <summary>
    /// 显示命运面板
    /// </summary>
    private void ShowFatePanel(Action onComplete)
    {
        GameLogger.Log("GFC", "ShowFatePanel");

        // 构筑阶段事件链也可能直接进入命运面板，这里需要和地图节点路径一样确保已初始化
        if (_fateSystem == null)
        {
            _fateSystem = new FateSystem();
            _fateSystem.Initialize();
        }

        var panel = _uiManager?.ShowPanel<FatePanel>(UIManager.UILayer.PopUp);
        if (panel == null)
        {
            GameLogger.LogWarning("GFC", "FatePanel 创建失败，跳过");
            onComplete?.Invoke();
            return;
        }

        panel.ShowFate(_fateSystem, onComplete);
    }

    /// <summary>
    /// 显示抉择事件面板
    /// </summary>
    private void ShowChoicePanel(Action onComplete)
    {
        GameLogger.Log("GFC", "ShowChoicePanel");
        GameLogger.LogFileOnly("ChoiceDiag", $"ShowChoicePanel enter round={_currentRound} choiceSystemNull={_choiceEventSystem == null} uiNull={_uiManager == null}");
        GameLogger.Flush();

        if (_choiceEventSystem == null)
        {
            GameLogger.LogFileOnly("ChoiceDiag", "ChoiceEventSystem is null, creating and initializing now");
            _choiceEventSystem = new ChoiceEventSystem();
            _choiceEventSystem.Initialize();
            GameLogger.Flush();
        }

        var evt = _choiceEventSystem.GetEventForLevel(_currentRound);
        GameLogger.LogFileOnly("ChoiceDiag", $"GetEventForLevel done round={_currentRound} eventNull={evt == null} eventId={(evt != null ? evt.eventId : "null")}");
        GameLogger.Flush();
        if (evt == null)
        {
            GameLogger.Log("GFC", "无抉择事件，跳过");
            onComplete?.Invoke();
            return;
        }

        var panel = _uiManager?.ShowPanel<RandomEventPanel>(UIManager.UILayer.PopUp);
        GameLogger.LogFileOnly("ChoiceDiag", $"ShowPanel RandomEventPanel done panelNull={panel == null}");
        GameLogger.Flush();
        if (panel == null)
        {
            GameLogger.LogWarning("GFC", "RandomEventPanel 创建失败，跳过");
            onComplete?.Invoke();
            return;
        }

        GameLogger.LogFileOnly("ChoiceDiag", $"ShowChoiceEvent eventId={evt.eventId} optionCount={(evt.options != null ? evt.options.Count : -1)}");
        GameLogger.Flush();
        panel.ShowChoiceEvent(evt, _choiceEventSystem, onComplete);
    }

    /// <summary>
    /// 显示猫市面板
    /// </summary>
    private void ShowShopPanel(Action onComplete)
    {
        GameLogger.Log("GFC", "ShowShopPanel");

        // 确保实例已初始化
        if (_shopSystem == null)
        {
            _shopSystem = new ShopSystem();
            _shopSystem.Initialize();
        }

        // 每回合开始时重置商店状态（清除奸商陷阱等）
        _shopSystem.ResetForNewRound();
        _shopSystem.GenerateInventory();

        var panel = _uiManager?.ShowPanel<ShopPanel>(UIManager.UILayer.PopUp);
        if (panel == null)
        {
            GameLogger.LogWarning("GFC", "ShopPanel 创建失败，跳过");
            onComplete?.Invoke();
            return;
        }

        panel.ShowShop(_shopSystem, onComplete);
    }

    /// <summary>
    /// 获取当前关卡的敌方兵种ID列表
    /// </summary>
    private List<int> GetEnemyFighterIdsForCurrentLevel()
    {
        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        if (campaign == null) return new List<int>();

        int[] enemyUnitIds = campaign.GetEnemyUnitIdsForBattle(_currentRound);
        if (enemyUnitIds == null) return new List<int>();

        return new List<int>(enemyUnitIds);
    }

    // ── Boss 奖励流程 ──

    /// <summary>
    /// Boss关稀有兵种三选一
    /// </summary>
    private void ShowBossRareFighterReward(Action onComplete)
    {
        var recruitmentSystem = new RecruitmentDiceSystem();
        var cards = recruitmentSystem.GenerateBossRareCards(3);

        if (cards == null || cards.Count == 0)
        {
            Debug.Log("[GameFlowController] 没有稀有兵种可展示，跳过Boss稀有兵种奖励");
            onComplete?.Invoke();
            return;
        }

        var panel = _uiManager?.ShowPanel<RecruitmentSelectPanel>(UIManager.UILayer.PopUp);
        if (panel == null)
        {
            recruitmentSystem.RecruitRareFighter(cards[0].config);
            onComplete?.Invoke();
            return;
        }

        panel.ShowRecruitment(
            cards,
            onSelected: card =>
            {
                return recruitmentSystem.RecruitRareFighter(card.config);
            },
            onSkipped: () =>
            {
                onComplete?.Invoke();
            },
            title: "Boss奖励 — 选择一名稀有兵种",
            skipText: "放弃");
    }

    /// <summary>
    /// Boss关圣物奖励
    /// </summary>
    private void ShowBossRelicReward(Action onComplete)
    {
        var relic = SelectBossRelicForRegion(_currentRegion);
        if (relic == null)
        {
            Debug.Log("[GameFlowController] 没有Boss圣物可展示，跳过");
            onComplete?.Invoke();
            return;
        }

        // 存储圣物
        var record = new RelicRecord
        {
            relicId = relic.relicId,
            name = relic.name,
            description = relic.description,
            mechanismTag = relic.mechanismTag,
            rarity = relic.rarity,
            isBossRelic = relic.isBossRelic,
            effects = relic.effects
        };
        _dataManager?.AddRelic(record);

        var panel = _uiManager?.ShowPanel<BossRelicRewardPanel>(UIManager.UILayer.PopUp);
        if (panel == null)
        {
            onComplete?.Invoke();
            return;
        }

        panel.ShowBossRelicReward(relic, onComplete);
    }

    /// <summary>
    /// 根据大关选择Boss圣物
    /// </summary>
    private RelicConfig SelectBossRelicForRegion(int regionId)
    {
        var bossRelics = TribeConfigLoader.Instance?.GetBossRelics();
        if (bossRelics == null || bossRelics.Count == 0) return null;

        string expectedId = $"Relic_Boss_Region{regionId}";
        foreach (var relic in bossRelics)
        {
            if (relic != null && relic.relicId == expectedId)
                return relic;
        }

        int idx = UnityEngine.Random.Range(0, bossRelics.Count);
        return bossRelics[idx];
    }

    /// <summary>
    /// 游戏即将结束的通知（由TribeBuildPanel调用）
    /// </summary>
    public void NotifyGameEnding()
    {
        Debug.Log("[GameFlowController] 收到游戏结束通知");
        // TribeBuildPanel会处理最后的UI显示（ShowGameClearScreen）
        // 这里只更新内部状态
    }

    /// <summary>
    /// 触发回合改变事件
    /// </summary>
    public void RaiseRoundChanged(int newRound)
    {
        OnRoundChanged?.Invoke(newRound);
    }

    /// <summary>
    /// 推进到下一回合
    /// </summary>
    private void AdvanceRound()
    {
        // 结束当前回合
        _roundManager.EndRound();
        _currentRound = _roundManager.CurrentRound;

        GameLogger.Log("GFC", $"AdvanceRound→{_currentRound}");

        // 同步到存档
        if (_dataManager != null)
        {
            _dataManager.SetCurrentRound(_currentRound);
        }

        OnRoundChanged?.Invoke(_currentRound);

        // 检查游戏是否结束
        if (_roundManager.IsGameOver)
        {
            EndGame();
        }
        else
        {
            // 进入下一回合
            EnterGameRound();
        }
    }

    /// <summary>
    /// 游戏结束
    /// </summary>
    private void EndGame()
    {
        GameLogger.Log("GFC", "EndGame");
        ChangeGameState(GameState.GameOver);

        OnGameEnded?.Invoke();

        // 显示通关界面
        if (_uiManager != null)
        {
            var victoryPanel = _uiManager.ShowPanel<VictoryPanel>(UIManager.UILayer.Top);

            if (victoryPanel != null)
            {
                Debug.Log("[GameFlowController] VictoryPanel 已显示");
            }
        }
    }

    /// <summary>
    /// 改变游戏状态
    /// </summary>
    private void ChangeGameState(GameState newState)
    {
        if (_currentState == newState) return;

        GameState oldState = _currentState;
        _currentState = newState;

        GameLogger.Log("GFC", $"State→{newState} (was {oldState})");
        OnGameStateChanged?.Invoke(_currentState);
    }

    /// <summary>
    /// 获取当前回合的事件列表
    /// </summary>
    public RoundEventType[] GetCurrentRoundEvents()
    {
        return _roundManager.GetRoundEvents().ToArray();
    }

    /// <summary>
    /// 获取当前回合描述
    /// </summary>
    public string GetCurrentRoundDescription()
    {
        return _roundManager.GetRoundDescription();
    }

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    public void RestartGame()
    {
        GameLogger.Log("GFC", "RestartGame");

        _roundManager.Reset();
        _currentRound = 1;
        _isGameStarted = false;

        if (_dataManager != null)
        {
            // 重新加载玩家数据（会创建新的玩家数据）
            _dataManager.LoadPlayerData();
            _dataManager.SetCurrentRound(1);
            // 清空本局饰品
            _dataManager.ClearRunEquipment();
        }

        ChangeGameState(GameState.InitialSelection);
        SetupDefaultStartUnits();
        OnInitialTribeSelectionComplete();
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void ReturnToMainMenu()
    {
        GameLogger.Log("GFC", "ReturnToMainMenu");

        _roundManager.Reset();
        _currentRound = 1;
        _isGameStarted = false;
        ChangeGameState(GameState.Uninitialized);

        if (_uiManager != null)
        {
            _uiManager.ShowPanel<MainPanel>(UIManager.UILayer.Normal);
        }
    }
}
