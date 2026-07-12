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
    private HashSet<int> _selectedIndices = new HashSet<int>();
    private string _title;
    private string _skipText;

    public void ShowRecruitment(
        List<RecruitmentCard> cards,
        Action<RecruitmentCard> onSelected,
        Action onSkipped,
        string title = "招募",
        string skipText = "跳过")
    {
        _cards = cards != null ? new List<RecruitmentCard>(cards) : new List<RecruitmentCard>();
        _onSelected = onSelected;
        _onSkipped = onSkipped;
        _selectedIndices = new HashSet<int>();
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
                bool isSelected = _selectedIndices.Contains(i);
                CreateCardUI(_cards[i], i, xPos, isSelected);
            }
        }

        // 所有卡都已选择时自动关闭
        if (_cards.Count > 0 && _selectedIndices.Count >= _cards.Count)
        {
            Close();
            _onSkipped?.Invoke();
            return;
        }

        var skipBtn = CreateButton("SkipButton", _skipText, OnSkip, new Color(0.4f, 0.4f, 0.4f));
        skipBtn.anchoredPosition = new Vector2(0, -380);
    }

    private void CreateCardUI(RecruitmentCard card, int index, float xPos, bool isSelected)
    {
        GameObject cardGo = new GameObject($"Card_{index}");
        cardGo.transform.SetParent(transform, false);
        var cardRect = cardGo.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(400, 600);
        cardRect.anchoredPosition = new Vector2(xPos, -20);

        var cardBg = cardGo.AddComponent<Image>();
        cardBg.color = isSelected
            ? new Color(0.1f, 0.1f, 0.15f, 0.7f)
            : new Color(0.15f, 0.15f, 0.25f, 0.95f);

        Color rarityColor = card.rarity switch
        {
            2 => new Color(1f, 0.5f, 0f),
            1 => new Color(0.3f, 0.6f, 1f),
            _ => new Color(0.6f, 0.6f, 0.6f)
        };
        if (isSelected) rarityColor *= 0.5f;

        CreateChildImage(cardGo.transform, "RarityBar", new Vector2(380, 8), new Vector2(0, 280), rarityColor);

        string rarityStr = card.rarity switch
        {
            2 => "[稀有]",
            1 => "[高级]",
            _ => "[普通]"
        };
        var nameColor = isSelected ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
        var nameText = CreateChildText(cardGo.transform, "Name", $"{rarityStr} {card.name}", 26, nameColor);
        nameText.rectTransform.anchoredPosition = new Vector2(0, 230);

        var cfg = card.config;
        if (cfg != null)
        {
            var statsColor = isSelected ? new Color(0.4f, 0.4f, 0.4f) : new Color(0.8f, 0.8f, 0.8f);
            string statsText = $"HP: {cfg.hp}  攻: {cfg.attack}  防: {cfg.defense}\n速度: {cfg.moveSpeed}  攻速: {cfg.attackSpeed}";
            var statsLabel = CreateChildText(cardGo.transform, "Stats", statsText, 18, statsColor);
            statsLabel.rectTransform.anchoredPosition = new Vector2(0, 150);

            var tierColor = isSelected ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
            string tierText = $"Tier: {cfg.tier}  人口: {card.populationCost}";
            var tierLabel = CreateChildText(cardGo.transform, "Tier", tierText, 16, tierColor);
            tierLabel.rectTransform.anchoredPosition = new Vector2(0, 90);
        }

        if (card.bornEnhanced && !isSelected)
        {
            var enhanceLabel = CreateChildText(cardGo.transform, "Enhanced", "天生强化", 18, new Color(1f, 0.85f, 0.3f));
            enhanceLabel.rectTransform.anchoredPosition = new Vector2(0, 50);
        }

        if (isSelected)
        {
            var recruitedLabel = CreateChildText(cardGo.transform, "Recruited", "✔ 已招募", 28, new Color(0.3f, 1f, 0.3f));
            recruitedLabel.rectTransform.anchoredPosition = new Vector2(0, -80);
        }
        else
        {
            string costText = card.goldCost > 0 ? $"费用: {card.goldCost}" : "免费";
            var costLabel = CreateChildText(
                cardGo.transform,
                "Cost",
                costText,
                20,
                card.goldCost > 0 ? new Color(1f, 0.9f, 0.5f) : new Color(0.5f, 1f, 0.5f));
            costLabel.rectTransform.anchoredPosition = new Vector2(0, -10);

            int capturedIndex = index;
            var selectBtn = CreateChildButton(
                cardGo.transform,
                "SelectBtn",
                "招募",
                () => OnSelectCard(capturedIndex),
                new Color(0.2f, 0.7f, 0.3f));
            selectBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -130);
        }
    }

    private void OnSelectCard(int index)
    {
        if (index < 0 || index >= _cards.Count) return;
        if (_selectedIndices.Contains(index)) return;

        _selectedIndices.Add(index);
        _onSelected?.Invoke(_cards[index]);

        // 只能招募一个，选完直接关闭
        Close();
        _onSkipped?.Invoke();
    }

    private void OnSkip()
    {
        Close();
        _onSkipped?.Invoke();
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
