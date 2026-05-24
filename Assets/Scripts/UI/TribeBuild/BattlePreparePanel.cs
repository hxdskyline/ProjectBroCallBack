using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 战斗准备面板 — 布阵和部署
/// </summary>
public class BattlePreparePanel : UIPanel
{
    public override void Initialize()
    {
        base.Initialize();

        var title = CreateText("Title", "战斗准备", 32, Color.white);
        title.rectTransform.anchoredPosition = new Vector2(0, 400);

        var startBtn = CreateButton("StartButton", "开始战斗", OnStartBattle);
        startBtn.anchoredPosition = new Vector2(0, -300);

        var backBtn = CreateButton("BackButton", "返回", OnBack);
        backBtn.anchoredPosition = new Vector2(-200, -300);

        // 显示上阵单位信息
        var dataManager = GameManager.Instance?.DataManager;
        if (dataManager != null)
        {
            int population = dataManager.GetPopulationCap();
            var info = CreateText("Info", $"人口上限: {population}", 20, Color.yellow);
            info.rectTransform.anchoredPosition = new Vector2(0, 330);
        }
    }

    private void OnStartBattle()
    {
        GameLogger.Log("BattlePrep", "StartBattle");
        Debug.Log("[BattlePreparePanel] 开始战斗");
        GameFlowController.Instance.EnterBattlePhase();
    }

    private void OnBack()
    {
        GameLogger.Log("BattlePrep", "Back");
        Debug.Log("[BattlePreparePanel] 返回");
        Hide();
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 60);
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
        rect.sizeDelta = new Vector2(200, 50);
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
        txt.fontSize = 22;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return rect;
    }
}
