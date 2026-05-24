using UnityEngine;
using System.Collections;
using Camp;

/// <summary>
/// 游戏初始化器 - 负责游戏启动初始化
/// GameManager和GameFlowController由GameManager自动管理
/// GameInitializer只负责数据加载和配置初始化
/// </summary>
public class GameInitializer : MonoBehaviour
{
    private void Start()
    {
        // 启动游戏初始化流程
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        Debug.Log("[GameInitializer] 开始初始化游戏");

        // 等待GameManager和GameFlowController初始化完成
        yield return null;

        var gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("[GameInitializer] GameManager not found");
            yield break;
        }
        GameLogger.Log("Init", "GM OK");

        var dataManager = gameManager.DataManager;
        if (dataManager == null)
        {
            Debug.LogError("[GameInitializer] DataManager not found");
            yield break;
        }

        // 加载玩家数据
        dataManager.LoadPlayerData();
        GameLogger.Log("Init", "DataLoaded");

        // 初始化配置加载器
        TribeConfigLoader.Instance.LoadAllConfigs();
        GameLogger.Log("Init", "ConfigsLoaded");

        // 再等一帧确保GameFlowController已初始化
        yield return null;

        var gameFlowController = GameFlowController.Instance;
        if (gameFlowController == null)
        {
            Debug.LogError("[GameInitializer] GameFlowController not found");
            yield break;
        }

        // 将控制权交给GameFlowController
        gameFlowController.Initialize();
        GameLogger.Log("Init", "GFC Ready");
    }
}
