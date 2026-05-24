using UnityEngine;
using System;
using System.Collections.Generic;
using Camp;

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

    private int _currentRound = 1;
    private bool _isGameStarted = false;

    public GameState CurrentState => _currentState;
    public int CurrentRound => _currentRound;
    public bool IsGameStarted => _isGameStarted;
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
    /// 新游戏默认角色 — 橘猫族长(2000) + 矛猫(1001)
    /// </summary>
    private void SetupDefaultStartUnits()
    {
        GameLogger.Log("GFC", "SetupDefaultStartUnits");

        // 确保配置已加载
        var loader = TribeConfigLoader.Instance;
        if (!loader.IsLoaded) loader.LoadAllConfigs();

        var dataManager = _dataManager;

        // 角色配置
        int[] fighterIds = { 2000, 1001 };
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
                hasWoundsDebuff = false
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
        // 确保从DataManager加载最新的回合数
        if (_dataManager != null)
        {
            int savedRound = _dataManager.GetCurrentRound();
            _roundManager.SetRound(savedRound);
            _currentRound = savedRound;
        }

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
                _tribeBuildPanel = _uiManager.ShowPanel<TribeBuildPanel>(UIManager.UILayer.Normal);
            });
        }
        else
        {
            Debug.LogWarning("[GameFlowController] HotSpringPanel not found, using fallback");
            AutoHealAllUnits();
            _tribeBuildPanel = _uiManager.ShowPanel<TribeBuildPanel>(UIManager.UILayer.Normal);
        }
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
        GameLogger.Log("GFC", "EnterBattlePhase");
        ChangeGameState(GameState.BattlePhase);

        // 关闭战斗准备面板
        if (_uiManager != null)
            _uiManager.ClosePanel("BattlePreparePanel");

        // TODO: 启动战斗系统，战斗结束后调用 OnBattleEnded(true/false)
        // 当前战斗系统未实现，临时：模拟战斗胜利后回到构筑
        GameLogger.Log("GFC", "BattlePhase entered (no battle system yet)");
    }

    /// <summary>
    /// 战斗结束回调（由TribeBuildPanel调用）
    /// </summary>
    public void OnBattleEnded(bool victory)
    {
        GameLogger.Log("GFC", $"OnBattleEnded victory={victory}");
        if (victory)
        {
            // 战斗胜利后才标记节点为 Visited，解锁下一批 Available 节点
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

            // 战斗胜利经验奖励
            GrantBattleExpReward();

            // 检查是否是Boss战
            bool isBossBattle = _currentRegionMap != null &&
                               _currentNodeId >= 0 &&
                               _currentRegionMap.GetNode(_currentNodeId)?.nodeType == MapNodeType.Boss;

            if (isBossBattle)
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
            }

            // 进入战斗后招募流程
            ShowBattleResultRecruitment();
        }
        else
        {
            // 检查是否是Boss关失败
            bool isBossBattle = _currentRegionMap != null &&
                               _currentNodeId >= 0 &&
                               _currentRegionMap.GetNode(_currentNodeId)?.nodeType == MapNodeType.Boss;

            if (isBossBattle)
            {
                // Boss关失败 → 本局结束
                EndGame();
            }
            else
            {
                ChangeGameState(GameState.RoundPreparation);
            }
        }
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
    /// 显示战斗后招募界面
    /// </summary>
    private void ShowBattleResultRecruitment()
    {
        // 获取敌方兵种ID列表
        List<int> enemyFighterIds = GetEnemyFighterIdsForCurrentLevel();

        if (enemyFighterIds == null || enemyFighterIds.Count == 0)
        {
            // 没有可招募的敌方兵种，直接进入构筑阶段
            Debug.Log("[GameFlowController] 没有可招募的敌方兵种，直接进入构筑阶段");
            EnterBuildPhase();
            return;
        }

        // 显示战斗结果招募界面
        // 这里需要创建一个UI面板来显示招募卡片和掷骰子动画
        // 暂时简化处理：直接调用招募系统
        var recruitmentSystem = new RecruitmentDiceSystem();
        var cards = recruitmentSystem.GenerateRecruitmentCards(enemyFighterIds);

        Debug.Log($"[GameFlowController] 生成 {cards.Count} 张招募卡片");

        // TODO: 显示招募UI，让玩家选择是否招募
        // 暂时自动处理：尝试招募所有卡片
        foreach (var card in cards)
        {
            recruitmentSystem.RollDice(card);
            if (card.diceResult == DiceResult.Success)
            {
                recruitmentSystem.RecruitUnit(card);
            }
        }

        // 招募完成后进入构筑阶段（命运/抉择/猫市），再选关
        // 文档流程：战斗 → 招募 → 构筑 → 选关
        EnterBuildPhase();
    }

    /// <summary>
    /// 进入构筑阶段（命运/抉择/猫市），完成后进入选关
    /// 文档：每关流程 = 战斗准备→战斗→构筑→选关
    /// </summary>
    private void EnterBuildPhase()
    {
        GameLogger.Log("GFC", "EnterBuildPhase");
        ChangeGameState(GameState.RoundPreparation);

        if (_uiManager == null)
        {
            _uiManager = GameManager.Instance?.UIManager;
        }

        // 显示族群构筑主界面（包含命运/抉择/猫市）
        _tribeBuildPanel = _uiManager?.ShowPanel<TribeBuildPanel>(UIManager.UILayer.Normal);

        if (_tribeBuildPanel == null)
        {
            Debug.LogWarning("[GameFlowController] TribeBuildPanel not found, fallback to MapSelection");
            EnterMapSelection();
        }
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

        // 给默认角色，重新开始
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
