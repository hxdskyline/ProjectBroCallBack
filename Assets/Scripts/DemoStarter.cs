using UnityEngine;

/// <summary>
/// Demo启动器 - 场景启动时初始化游��
/// </summary>
public class DemoStarter : MonoBehaviour
{
    private void Start()
    {
        // 加载游戏数据
        GameManager.Instance.LoadGame();

        Debug.Log("[DemoStarter] by ai");
    }
}