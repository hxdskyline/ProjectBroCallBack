using System;
using Camp;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抉择事件面板 - 三选一随机事件，必须选择一个选项。
/// </summary>
public class RandomEventPanel : UIPanel
{
    private Action _onComplete;

    public void ShowChoiceEvent(ChoiceEvent evt, ChoiceEventSystem system, Action onComplete)
    {
        if (evt == null || system == null)
        {
            GameLogger.LogErrorFileOnly("ChoiceDiag", $"RandomEventPanel.ShowChoiceEvent invalid input eventNull={evt == null} systemNull={system == null}");
            GameLogger.Flush();
            onComplete?.Invoke();
            Close();
            return;
        }

        _onComplete = onComplete;
        ClearChildren();

        GameLogger.LogFileOnly("ChoiceDiag", $"RandomEventPanel.ShowChoiceEvent eventId={evt.eventId} optionCount={(evt.options != null ? evt.options.Count : -1)}");
        GameLogger.Flush();

        var bg = CreateImage("BG", new Color(0.1f, 0.1f, 0.15f, 0.95f));
        bg.rectTransform.anchoredPosition = Vector2.zero;

        var title = CreateText("Title", evt.name, 36, new Color(1f, 0.85f, 0.4f));
        title.rectTransform.anchoredPosition = new Vector2(0, 350);

        var desc = CreateText("Desc", evt.description, 22, new Color(0.8f, 0.8f, 0.8f));
        desc.rectTransform.anchoredPosition = new Vector2(0, 280);

        float[] yPositions = { 100f, -50f, -200f };
        Color[] optionColors =
        {
            new Color(0.2f, 0.5f, 0.2f),
            new Color(0.5f, 0.2f, 0.2f),
            new Color(0.6f, 0.5f, 0.1f)
        };

        int optionCount = evt.options != null ? Mathf.Min(evt.options.Count, 3) : 0;
        for (int i = 0; i < optionCount; i++)
        {
            var option = evt.options[i];
            var btnRect = CreateButton($"Option{i}_Btn", option.name, 500, 100, optionColors[i]);
            btnRect.anchoredPosition = new Vector2(0, yPositions[i]);

            var btn = btnRect.GetComponent<Button>();
            int capturedIndex = i;
            btn.onClick.AddListener(() => OnOptionSelected(evt, system, capturedIndex));

            var optDesc = CreateText($"Option{i}_Desc", option.description, 16, new Color(0.7f, 0.7f, 0.7f));
            optDesc.rectTransform.anchoredPosition = new Vector2(0, yPositions[i] - 70f);
        }
    }

    private void OnOptionSelected(ChoiceEvent evt, ChoiceEventSystem system, int index)
    {
        GameLogger.LogFileOnly("ChoiceDiag", $"RandomEventPanel.OnOptionSelected eventId={(evt != null ? evt.eventId : "null")} index={index} systemNull={system == null}");
        GameLogger.Flush();

        if (evt == null || system == null)
        {
            GameLogger.LogErrorFileOnly("ChoiceDiag", $"RandomEventPanel.OnOptionSelected invalid input eventNull={evt == null} systemNull={system == null}");
            GameLogger.Flush();
            Close();
            _onComplete?.Invoke();
            return;
        }

        GameLogger.Log("ChoicePanel", $"选择: {evt.name} - 选项{index}");
        system.ApplyOption(evt, index);
        Close();
        _onComplete?.Invoke();
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private Image CreateImage(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(800, 60);

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        try
        {
            var font = GameManager.Instance?.ResourceManager?.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
            if (font != null) txt.font = font;
        }
        catch
        {
        }

        return txt;
    }

    private RectTransform CreateButton(string name, string label, float width, float height, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var text = textGo.AddComponent<Text>();
        text.text = label;
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;

        try
        {
            var font = GameManager.Instance?.ResourceManager?.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
            if (font != null) text.font = font;
        }
        catch
        {
        }

        return rect;
    }
}
