using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结算面板 — 显示战斗结果和奖励
/// </summary>
public class BattleResultPanel : UIPanel
{
    private System.Action _onClosed;

    public override void Initialize()
    {
        base.Initialize();

        // 半透明深色背景
        var bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.75f);
    }

    public void Setup(bool victory, int battleNumber, int expReward, int catFoodReward, System.Action onClosed)
    {
        _onClosed = onClosed;

        // 清除旧子物体
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        // 标题
        string title = victory ? "战斗胜利" : "战斗失败";
        Color titleColor = victory ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f);
        var titleGo = CreateText("Title", title, 42, titleColor);
        titleGo.rectTransform.anchoredPosition = new Vector2(0, 180);

        // 关卡信息
        var levelGo = CreateText("Level", $"第 {battleNumber} 关", 28, Color.white);
        levelGo.rectTransform.anchoredPosition = new Vector2(0, 100);

        if (victory)
        {
            // 奖励信息
            var expGo = CreateText("ExpReward", $"经验 +{expReward}", 24, new Color(0.4f, 1f, 0.4f));
            expGo.rectTransform.anchoredPosition = new Vector2(0, 30);

            var catFoodGo = CreateText("CatFoodReward", $"猫粮 +{catFoodReward}", 24, new Color(1f, 0.8f, 0.3f));
            catFoodGo.rectTransform.anchoredPosition = new Vector2(0, -20);
        }

        // 确认按钮
        var confirmBtn = CreateButton("ConfirmButton", "确认", OnConfirm);
        confirmBtn.anchoredPosition = new Vector2(0, -120);

        GameLogger.Log("BattleResult", $"显示结算面板 victory={victory} bn={battleNumber}");
    }

    private void OnConfirm()
    {
        GameLogger.Log("BattleResult", "确认关闭");
        GameManager.Instance?.UIManager?.ClosePanel("BattleResultPanel");
        _onClosed?.Invoke();
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 60);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;
    }

    private RectTransform CreateButton(string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
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
