using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通关/失败面板
/// </summary>
public class VictoryPanel : UIPanel
{
    public override void Initialize()
    {
        base.Initialize();

        var titleText = CreateText("Title", "游戏结束", 48, Color.white);
        titleText.rectTransform.anchoredPosition = new Vector2(0, 150);

        var restartBtn = CreateButton("RestartButton", "重新开始", OnRestart);
        restartBtn.anchoredPosition = new Vector2(0, -50);

        var mainMenuBtn = CreateButton("MainMenuButton", "返回主菜单", OnMainMenu);
        mainMenuBtn.anchoredPosition = new Vector2(0, -130);
    }

    private void OnRestart()
    {
        GameLogger.Log("VictoryP", "Restart");
        Debug.Log("[VictoryPanel] 重新开始");
        Hide();
        GameFlowController.Instance.RestartGame();
    }

    private void OnMainMenu()
    {
        GameLogger.Log("VictoryP", "MainMenu");
        Debug.Log("[VictoryPanel] 返回主菜单");
        Hide();
        GameFlowController.Instance.ReturnToMainMenu();
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 80);
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
        var image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.5f, 0.8f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.onClick.AddListener(onClick);
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
