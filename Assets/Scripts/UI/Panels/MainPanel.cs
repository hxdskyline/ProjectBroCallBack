using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单面板
/// </summary>
public class MainPanel : UIPanel
{
    public override void Initialize()
    {
        base.Initialize();

        // 动态创建主菜单 UI
        var titleText = CreateText("Title", "ProjectBroCallBack", 48, new Color(1f, 0.85f, 0.3f));
        titleText.rectTransform.anchoredPosition = new Vector2(0, 200);

        var newGameBtn = CreateButton("NewGameButton", "新游戏", OnNewGame);
        newGameBtn.anchoredPosition = new Vector2(0, 0);

        var continueBtn = CreateButton("ContinueButton", "继续游戏", OnContinue);
        continueBtn.anchoredPosition = new Vector2(0, -80);

        var clearBtn = CreateButton("ClearSaveButton", "清除存档", OnClearSave);
        clearBtn.anchoredPosition = new Vector2(0, -160);
        // 红色警示
        clearBtn.GetComponent<Image>().color = new Color(0.7f, 0.2f, 0.2f);
    }

    private void OnNewGame()
    {
        GameLogger.Log("MainP", "NewGame");
        Debug.Log("[MainPanel] 新游戏");

        // 重置存档并开始新游戏
        var dataManager = GameManager.Instance.DataManager;
        dataManager.ResetPlayerData();

        Hide();

        // 通知 GameFlowController 开始初始选择
        GameFlowController.Instance.RestartGame();
    }

    private void OnContinue()
    {
        GameLogger.Log("MainP", "Continue");
        Debug.Log("[MainPanel] 继续游戏");
        Hide();
        GameFlowController.Instance.Initialize();
    }

    private void OnClearSave()
    {
        GameLogger.Log("MainP", "ClearSave");

        var dataManager = GameManager.Instance.DataManager;
        dataManager.DeleteSaveData();

        // 重新初始化解锁"继续游戏"按钮状态
        GameFlowController.Instance.ReturnToMainMenu();
    }

    // ── UI 工具方法 ──

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 80);
        rect.anchoredPosition = Vector2.zero;

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;

        return txt;
    }

    private RectTransform CreateButton(string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 60);
        rect.anchoredPosition = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.5f, 0.8f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.onClick.AddListener(onClick);

        // 按钮文字
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var txt = textGo.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        return rect;
    }
}
