using UnityEngine;
using System.Collections.Generic;
using Combat;

/// <summary>
/// 游戏总管理器 - 负责整个游戏的生命周期管理
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;

    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    // 各个管理器的引用
    private ResourceManager _resourceManager;
    private UIManager _uiManager;
    private AudioManager _audioManager;
    private DataManager _dataManager;
    private CurrencyManager _currencyManager;
    private SceneManager _sceneManager;
    private BattleCampaignRuntime _battleCampaignRuntime;
    private GameFlowController _gameFlowController;

    public ResourceManager ResourceManager => _resourceManager;
    public UIManager UIManager => _uiManager;
    public AudioManager AudioManager => _audioManager;
    public DataManager DataManager => _dataManager;
    public CurrencyManager CurrencyManager => _currencyManager;
    public SceneManager SceneManager => _sceneManager;
    public BattleCampaignRuntime BattleCampaignRuntime => _battleCampaignRuntime;
    public GameFlowController GameFlowController => _gameFlowController;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeManagers();
    }

    private void InitializeManagers()
    {
        // 0. 初始化日志系统（最先）
        GameLogger.Initialize();
        GameLogger.Log("GM", "InitStart");

        // 1. 初始化资源管理器（必须首先初始化）
        _resourceManager = gameObject.AddComponent<ResourceManager>();
        _resourceManager.Initialize();

        // 2. 初始化数据管理器
        _dataManager = gameObject.AddComponent<DataManager>();
        _dataManager.Initialize();
        _currencyManager = new CurrencyManager(_dataManager);
        GameLogger.Log("GM", "Data OK");

        // 3. 初始化音频管理器（依赖 ResourceManager）
        _audioManager = gameObject.AddComponent<AudioManager>();
        _audioManager.Initialize();

        // 4. 初始化场景管理器
        _sceneManager = gameObject.AddComponent<SceneManager>();
        _sceneManager.Initialize();

        // 5. 初始化UI管理器（依赖 ResourceManager）
        _uiManager = gameObject.AddComponent<UIManager>();
        _uiManager.Initialize();

        // 6. 初始化运行时战斗进度（仅在本次启动期间有效）
        _battleCampaignRuntime = new BattleCampaignRuntime();

        // 7. 初始化游戏流程控制器（管理游戏的整体流程）
        _gameFlowController = gameObject.AddComponent<GameFlowController>();

        GameLogger.Log("GM", "InitDone");
    }

    public void LoadGame()
    {
        GameLogger.Log("GM", "LoadGame");
        _dataManager.LoadPlayerData();
        _uiManager.ShowPanel<MainPanel>(UIManager.UILayer.Normal);
    }

    public void SaveGame()
    {
        GameLogger.Log("GM", "SaveGame");
        _currencyManager?.Save();
        _dataManager.SavePlayerData();
    }

    private void OnApplicationQuit()
    {
        SaveGame();
        GameLogger.Log("GM", "Quit");
        GameLogger.Shutdown();
    }
}
