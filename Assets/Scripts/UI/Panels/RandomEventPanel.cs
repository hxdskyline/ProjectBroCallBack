using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 抉择事件面板 — 三选一随机事件（必须选择1个，无跳过）
/// 设计参考：正式文档/106_系统_抉择.md
/// </summary>
public class RandomEventPanel : UIPanel
{
    private Action _onComplete;

    /// <summary>
    /// 显示抉择事件面板
    /// </summary>
    public void ShowChoiceEvent(ChoiceEvent evt, ChoiceEventSystem system, Action onComplete)
    {
        _onComplete = onComplete;
        ClearChildren();

        var bg = CreateImage("BG", new Color(0.1f, 0.1f, 0.15f, 0.95f));
        bg.rectTransform.anchoredPosition = Vector2.zero;

        var title = CreateText("Title", evt.name, 36, new Color(1f, 0.85f, 0.4f));
        title.rectTransform.anchoredPosition = new Vector2(0, 350);

        var desc = CreateText("Desc", evt.description, 22, new Color(0.8f, 0.8f, 0.8f));
        desc.rectTransform.anchoredPosition = new Vector2(0, 280);

        float[] yPositions = { 100, -50, -200 };
        Color[] optionColors = {
            new Color(0.2f, 0.5f, 0.2f),   // 低风险 - 绿
            new Color(0.5f, 0.2f, 0.2f),   // 高风险 - 红
            new Color(0.6f, 0.5f, 0.1f)    // 我全要了 - 金
        };

        for (int i = 0; i < evt.options.Count && i < 3; i++)
        {
            var option = evt.options[i];
            var btnRect = CreateButton($"Option{i}_Btn", option.name, 500, 100, optionColors[i]);
            btnRect.anchoredPosition = new Vector2(0, yPositions[i]);

            var btn = btnRect.GetComponent<Button>();
            int capturedIndex = i;
            btn.onClick.AddListener(() => OnOptionSelected(evt, system, capturedIndex));

            var optDesc = CreateText($"Option{i}_Desc", option.description, 16, new Color(0.7f, 0.7f, 0.7f));
            optDesc.rectTransform.anchoredPosition = new Vector2(0, yPositions[i] - 70);
        }
    }

    private void OnOptionSelected(ChoiceEvent evt, ChoiceEventSystem system, int index)
    {
        GameLogger.Log("ChoicePanel", "选择: " + evt.name + " - 选项" + index);
        system.ApplyOption(evt, index);
        Close();
        _onComplete?.Invoke();
    }

    // ── UI 工具方法 ──

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
        catch { }
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
        catch { }

        return rect;
    }
}
