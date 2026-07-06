using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// UI管理器 - 负责UI面板的加载、显示、隐藏和销毁（使用 Addressable）
/// </summary>
public class UIManager : MonoBehaviour
{
    private Canvas _mainCanvas;
    private Dictionary<string, UIPanel> _activePanels = new Dictionary<string, UIPanel>();

    public enum UILayer
    {
        Background = 0,
        Normal = 1,
        Top = 2,
        PopUp = 3,
        Alert = 4
    }

    public void Initialize()
    {
        // 查找或创建主Canvas
        _mainCanvas = FindObjectOfType<Canvas>();
        if (_mainCanvas == null)
        {
            GameObject canvasGo = new GameObject("MainCanvas");
            _mainCanvas = canvasGo.AddComponent<Canvas>();
            _mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // Drag and drop requires an EventSystem in scene.
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }

        Debug.Log("[UIManager] Initialized");
    }

    /// <summary>
    /// 显示面板（动态创建，不依赖预制体）
    /// </summary>
    public T ShowPanel<T>(UILayer layer = UILayer.Normal) where T : UIPanel
    {
        string panelName = typeof(T).Name;

        if (_activePanels.ContainsKey(panelName))
        {
            _activePanels[panelName].Show();
            _activePanels[panelName].gameObject.transform.SetAsLastSibling();
            Debug.Log($"[UIManager] Reusing panel and moving to top: {panelName}");
            return _activePanels[panelName] as T;
        }

        // 动态创建面板 GameObject
        GameObject panelGo = new GameObject(panelName);
        panelGo.transform.SetParent(GetUILayerTransform(layer), false);

        RectTransform rect = panelGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        T panel = panelGo.AddComponent<T>();
        panel.Initialize();
        panel.Show();
        panel.gameObject.transform.SetAsLastSibling();

        _activePanels[panelName] = panel;
        GameLogger.Log("UIM", $"Create:{typeof(T).Name}");
        Debug.Log($"[UIManager] Panel created and shown: {panelName}");

        return panel;
    }

    /// <summary>
    /// 显示面板（兼容旧接口，仍然尝试加载预制体，失败则动态创建）
    /// </summary>
    public T ShowPanel<T>(string panelAddress, UILayer layer = UILayer.Normal) where T : UIPanel
    {
        string panelName = typeof(T).Name;

        if (_activePanels.ContainsKey(panelName))
        {
            _activePanels[panelName].Show();
            return _activePanels[panelName] as T;
        }

        // 尝试加载预制体
        GameObject panelPrefab = null;
        if (GameManager.Instance?.ResourceManager != null)
        {
            panelPrefab = GameManager.Instance.ResourceManager.LoadPrefab(panelAddress);
        }

        GameObject panelInstance;
        if (panelPrefab != null)
        {
            panelInstance = Instantiate(panelPrefab, GetUILayerTransform(layer));
            panelInstance.name = panelPrefab.name;
        }
        else
        {
            // 预制体不存在，动态创建
            panelInstance = new GameObject(panelName);
            panelInstance.transform.SetParent(GetUILayerTransform(layer), false);

            RectTransform rect = panelInstance.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        T panel = panelInstance.GetComponent<T>();
        if (panel == null)
        {
            panel = panelInstance.AddComponent<T>();
        }

        panel.Initialize();
        panel.Show();

        _activePanels[panelName] = panel;
        Debug.Log($"[UIManager] Panel shown: {panelName}");

        return panel;
    }

    /// <summary>
    /// 异步显示面板
    /// </summary>
    public void ShowPanelAsync<T>(string panelAddress, UILayer layer, System.Action<T> onComplete) where T : UIPanel
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            _activePanels[panelAddress].Show();
            onComplete?.Invoke(_activePanels[panelAddress] as T);
            return;
        }

        GameManager.Instance.ResourceManager.LoadPrefabAsync(panelAddress, (prefab) =>
        {
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] Panel prefab not found: {panelAddress}");
                onComplete?.Invoke(null);
                return;
            }

            GameObject panelInstance = Instantiate(prefab, GetUILayerTransform(layer));
            panelInstance.name = prefab.name;

            T panel = panelInstance.GetComponent<T>();
            if (panel == null)
            {
                panel = panelInstance.AddComponent<T>();
            }

            panel.Initialize();
            panel.Show();

            _activePanels[panelAddress] = panel;
            Debug.Log($"[UIManager] Panel shown async: {panelAddress}");

            onComplete?.Invoke(panel);
        });
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void HidePanel(string panelAddress)
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            _activePanels[panelAddress].Hide();
            Debug.Log($"[UIManager] Panel hidden: {panelAddress}");
        }
    }

    /// <summary>
    /// 关闭并销毁面板
    /// </summary>
    public void ClosePanel(string panelAddress)
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            GameLogger.Log("UIM", $"Close:{panelAddress}");
            UIPanel panel = _activePanels[panelAddress];
            panel.Close();
            _activePanels.Remove(panelAddress);
            Destroy(panel.gameObject);

            // 释放资源
            GameManager.Instance.ResourceManager.UnloadResource(panelAddress);

            Debug.Log($"[UIManager] Panel closed: {panelAddress}");
        }
    }

    /// <summary>
    /// 获取UI层级的Transform
    /// </summary>
    private Transform GetUILayerTransform(UILayer layer)
    {
        Transform layerTransform = _mainCanvas.transform.Find(layer.ToString());
        if (layerTransform == null)
        {
            GameObject layerGo = new GameObject(layer.ToString());
            layerGo.transform.SetParent(_mainCanvas.transform, false);
            RectTransform rectTransform = layerGo.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = layerGo.AddComponent<RectTransform>();
            }
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            layerTransform = layerGo.transform;
        }

        // 保证层级顺序按 UILayer 枚举排序
        layerTransform.SetSiblingIndex((int)layer);
        return layerTransform;
    }

    /// <summary>
    /// 获取已显示的面板
    /// </summary>
    public T GetPanel<T>(string panelAddress) where T : UIPanel
    {
        if (_activePanels.ContainsKey(panelAddress))
        {
            return _activePanels[panelAddress] as T;
        }
        return null;
    }

    /// <summary>
    /// 关闭所有面板
    /// </summary>
    public void CloseAllPanels()
    {
        List<string> panelNames = new List<string>(_activePanels.Keys);
        foreach (string panelName in panelNames)
        {
            ClosePanel(panelName);
        }
    }
}