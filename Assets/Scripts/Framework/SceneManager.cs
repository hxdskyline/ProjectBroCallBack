using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景管理器 - 管理场景的加载和切换
/// </summary>
public class SceneManager : MonoBehaviour
{
    private static SceneManager _instance;

    public static SceneManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SceneManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SceneManager");
                    _instance = go.AddComponent<SceneManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public void Initialize()
    {
        Debug.Log("[SceneManager] Initialized");
    }

    /// <summary>
    /// 加载场景
    /// </summary>
    public void LoadScene(string sceneName)
    {
        Debug.Log($"[SceneManager] Loading scene: {sceneName}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 异步加载场景
    /// </summary>
    public void LoadSceneAsync(string sceneName)
    {
        Debug.Log($"[SceneManager] Loading scene asynchronously: {sceneName}");
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
    }

    /// <summary>
    /// 重新加载当前场景
    /// </summary>
    public void ReloadCurrentScene()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        LoadScene(currentScene);
    }
}