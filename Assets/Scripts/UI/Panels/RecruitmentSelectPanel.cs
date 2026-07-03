using System;
using System.Collections.Generic;
using Camp;
using UnityEngine;
using UnityEngine.UI;

public class RecruitmentSelectPanel : UIPanel
{
    private Action<RecruitmentCard> _onSelected;
    private Action _onSkipped;
    private List<RecruitmentCard> _cards;
    private RecruitmentDiceSystem _recruitmentSystem;
    private string _title;
    private string _skipText;

    public void ShowRecruitment(
        List<RecruitmentCard> cards,
        Action<RecruitmentCard> onSelected,
        Action onSkipped,
        string title = "\u62DB\u52DF",
        string skipText = "\u8DF3\u8FC7")
    {
        _cards = cards != null ? new List<RecruitmentCard>(cards) : new List<RecruitmentCard>();
        _onSelected = onSelected;
        _onSkipped = onSkipped;
        _recruitmentSystem = new RecruitmentDiceSystem();
        _title = title;
        _skipText = skipText;

        BuildUI();
    }

    private void BuildUI()
    {
        ClearChildren();

        var bg = CreateBackground();
        bg.transform.SetParent(transform, false);
        bg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        bg.GetComponent<RectTransform>().anchorMax = Vector2.one;
        bg.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        bg.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        var titleText = CreateText("Title", _title, 34, new Color(1f, 0.85f, 0.4f));
        titleText.rectTransform.anchoredPosition = new Vector2(0, 350);

        if (_cards.Count > 0)
        {
            float cardWidth = 400f;
            float totalWidth = _cards.Count * cardWidth + (_cards.Count - 1) * 30f;
            float startX = -totalWidth / 2f + cardWidth / 2f;

            for (int i = 0; i < _cards.Count; i++)
            {
                float xPos = startX + i * (cardWidth + 30f);
                CreateCardUI(_cards[i], i, xPos);
            }
        }

        var skipBtn = CreateButton("SkipButton", _skipText, OnSkip, new Color(0.4f, 0.4f, 0.4f));
        skipBtn.anchoredPosition = new Vector2(0, -380);
    }

    private void CreateCardUI(RecruitmentCard card, int index, float xPos)
    {
        GameObject cardGo = new GameObject($"Card_{index}");
        cardGo.transform.SetParent(transform, false);
        var cardRect = cardGo.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(400, 600);
        cardRect.anchoredPosition = new Vector2(xPos, -20);

        var cardBg = cardGo.AddComponent<Image>();
        cardBg.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);

        Color rarityColor = card.rarity switch
        {
            2 => new Color(1f, 0.5f, 0f),
            1 => new Color(0.3f, 0.6f, 1f),
            _ => new Color(0.6f, 0.6f, 0.6f)
        };

        CreateChildImage(cardGo.transform, "RarityBar", new Vector2(380, 8), new Vector2(0, 280), rarityColor);

        string rarityStr = card.rarity switch
        {
            2 => "[\u7A00\u6709]",
            1 => "[\u9AD8\u7EA7]",
            _ => "[\u666E\u901A]"
        };
        var nameText = CreateChildText(cardGo.transform, "Name", $"{rarityStr} {card.name}", 26, Color.white);
        nameText.rectTransform.anchoredPosition = new Vector2(0, 230);

        var cfg = card.config;
        if (cfg != null)
        {
            string statsText = $"HP: {cfg.hp}  攻: {cfg.attack}  防: {cfg.defense}\n速度: {cfg.moveSpeed}  攻速: {cfg.attackSpeed}";
            var statsLabel = CreateChildText(cardGo.transform, "Stats", statsText, 18, new Color(0.8f, 0.8f, 0.8f));
            statsLabel.rectTransform.anchoredPosition = new Vector2(0, 150);

            string tierText = $"Tier: {cfg.tier}  人口: {card.populationCost}";
            var tierLabel = CreateChildText(cardGo.transform, "Tier", tierText, 16, new Color(0.6f, 0.6f, 0.6f));
            tierLabel.rectTransform.anchoredPosition = new Vector2(0, 90);
        }

        if (card.bornEnhanced)
        {
            var enhanceLabel = CreateChildText(cardGo.transform, "Enhanced", "\u5929\u751F\u5F3A\u5316", 18, new Color(1f, 0.85f, 0.3f));
            enhanceLabel.rectTransform.anchoredPosition = new Vector2(0, 50);
        }

        string costText = card.goldCost > 0 ? $"\u8D39\u7528: {card.goldCost}" : "\u514D\u8D39";
        var costLabel = CreateChildText(
            cardGo.transform,
            "Cost",
            costText,
            20,
            card.goldCost > 0 ? new Color(1f, 0.9f, 0.5f) : new Color(0.5f, 1f, 0.5f));
        costLabel.rectTransform.anchoredPosition = new Vector2(0, -10);

        if (card.diceResult == DiceResult.Pending)
        {
            var rollBtn = CreateChildButton(
                cardGo.transform,
                "RollBtn",
                "\u63B7\u9AB0\u5B50",
                () => OnRollDice(card),
                new Color(0.5f, 0.3f, 0.7f));
            rollBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -130);
            return;
        }

        if (card.diceResult == DiceResult.Success)
        {
            var resultLabel = CreateChildText(cardGo.transform, "Result", "\u62DB\u52DF\u6210\u529F", 24, new Color(0.3f, 1f, 0.3f));
            resultLabel.rectTransform.anchoredPosition = new Vector2(0, -60);

            var selectBtn = CreateChildButton(
                cardGo.transform,
                "SelectBtn",
                "\u62DB\u52DF",
                () => OnSelectCard(card),
                new Color(0.2f, 0.7f, 0.3f));
            selectBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -130);
            return;
        }

        var failLabel = CreateChildText(cardGo.transform, "Result", "\u4E0D\u53EF\u62DB\u52DF", 24, new Color(1f, 0.3f, 0.3f));
        failLabel.rectTransform.anchoredPosition = new Vector2(0, -130);
    }

    private void OnRollDice(RecruitmentCard card)
    {
        _recruitmentSystem.RollDice(card);
        BuildUI();
    }

    private void OnSelectCard(RecruitmentCard card)
    {
        _onSelected?.Invoke(card);
        _cards.Remove(card);
        BuildUI();
    }

    private void OnSkip()
    {
        _onSkipped?.Invoke();
        Close();
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private GameObject CreateBackground()
    {
        GameObject go = new GameObject("Background");
        go.AddComponent<RectTransform>();
        var image = go.AddComponent<Image>();
        image.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);
        return go;
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(800, 50);
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
        rect.sizeDelta = new Vector2(370, 40);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;
    }

    private Image CreateChildImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Button CreateChildButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 50);
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

        return btn;
    }

    private RectTransform CreateButton(string name, string text, UnityEngine.Events.UnityAction onClick, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280, 55);
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
