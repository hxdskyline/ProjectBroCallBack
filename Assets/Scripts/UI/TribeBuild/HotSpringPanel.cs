using System;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 温泉面板 — 二选一：全体回复 50% HP 或一个单位属性永久提升
/// </summary>
public class HotSpringPanel : UIPanel
{
    public void ShowHotSpring(Action onComplete)
    {
        _onComplete = onComplete;

        var title = CreateText("Title", "温泉", 36, new Color(0.7f, 0.9f, 1f));
        title.rectTransform.anchoredPosition = new Vector2(0, 200);

        var desc = CreateText("Desc", "选择一个效果", 24, Color.white);
        desc.rectTransform.anchoredPosition = new Vector2(0, 100);

        var healBtn = CreateButton("HealButton", "全体回复 50% HP", OnHeal);
        healBtn.anchoredPosition = new Vector2(0, -20);

        var buffBtn = CreateButton("BuffButton", "一个单位属性永久 +50%", OnBuff);
        buffBtn.anchoredPosition = new Vector2(0, -100);
    }

    private Action _onComplete;

    private void OnHeal()
    {
        GameLogger.Log("HotSpring", "Heal");
        Debug.Log("[HotSpringPanel] 选择全体回复");
        var healthSystem = new HealthPersistenceSystem();
        healthSystem.HealAllAlliesPercent(0.5f);
        Close();
        _onComplete?.Invoke();
    }

    private void OnBuff()
    {
        GameLogger.Log("HotSpring", "Buff");
        Debug.Log("[HotSpringPanel] 选择属性提升");
        // TODO: 选择一个单位并永久提升属性
        Close();
        _onComplete?.Invoke();
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 60);
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
        rect.sizeDelta = new Vector2(350, 60);
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
