using System;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// Boss圣物奖励面板 — 展示获得的Boss圣物
/// </summary>
public class BossRelicRewardPanel : UIPanel
{
    private Action _onConfirm;

    /// <summary>
    /// 展示Boss圣物奖励
    /// </summary>
    /// <param name="relic">圣物配置</param>
    /// <param name="onConfirm">确认回调</param>
    public void ShowBossRelicReward(RelicConfig relic, Action onConfirm)
    {
        _onConfirm = onConfirm;
        BuildUI(relic);
    }

    private void BuildUI(RelicConfig relic)
    {
        ClearChildren();

        // 背景
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.03f, 0.1f, 0.92f);

        // 标题
        var title = CreateText("Title", "Boss圣物奖励", 36, new Color(1f, 0.7f, 0.2f));
        title.rectTransform.anchoredPosition = new Vector2(0, 280);

        if (relic != null)
        {
            // 圣物卡片背景
            var cardGo = new GameObject("RelicCard");
            cardGo.transform.SetParent(transform, false);
            var cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(500, 350);
            cardRect.anchoredPosition = new Vector2(0, 20);
            var cardBg = cardGo.AddComponent<Image>();
            cardBg.color = new Color(0.15f, 0.1f, 0.2f, 0.95f);

            // Boss金边
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(cardGo.transform, false);
            var borderRect = borderGo.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-4, -4);
            borderRect.offsetMax = new Vector2(4, 4);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(1f, 0.7f, 0.1f, 0.6f);
            borderImg.raycastTarget = false;

            // 圣物名称
            var nameText = CreateChildText(cardGo.transform, "RelicName", relic.name, 30, new Color(1f, 0.85f, 0.3f));
            nameText.rectTransform.anchoredPosition = new Vector2(0, 120);

            // 描述
            var descText = CreateChildText(cardGo.transform, "Desc", relic.description, 18, new Color(0.8f, 0.8f, 0.8f));
            descText.rectTransform.anchoredPosition = new Vector2(0, 50);
            descText.rectTransform.sizeDelta = new Vector2(460, 100);

            // 机制标签
            if (!string.IsNullOrEmpty(relic.mechanismTag))
            {
                var tagText = CreateChildText(cardGo.transform, "Tag", $"适用: {relic.mechanismTag}", 16, new Color(0.6f, 0.8f, 1f));
                tagText.rectTransform.anchoredPosition = new Vector2(0, -30);
            }
        }
        else
        {
            var emptyText = CreateText("Empty", "未获得圣物", 24, Color.gray);
            emptyText.rectTransform.anchoredPosition = new Vector2(0, 20);
        }

        // 确认按钮
        var confirmBtn = CreateButton("ConfirmButton", "确认", OnConfirm, new Color(0.6f, 0.4f, 0.1f));
        confirmBtn.anchoredPosition = new Vector2(0, -250);
    }

    private void OnConfirm()
    {
        _onConfirm?.Invoke();
        Close();
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 50);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;
    }

    private Text CreateChildText(Transform parent, string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(460, 40);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;
    }

    private RectTransform CreateButton(string name, string text, UnityEngine.Events.UnityAction onClick, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250, 55);
        var image = go.AddComponent<Image>();
        image.color = bgColor;
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
