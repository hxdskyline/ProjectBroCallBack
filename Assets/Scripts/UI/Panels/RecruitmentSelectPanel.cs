using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 招募选择面板 — 复用于普通招募和Boss稀有兵种三选一
/// </summary>
public class RecruitmentSelectPanel : UIPanel
{
    private Action<RecruitmentCard> _onSelected;
    private Action _onSkipped;
    private List<RecruitmentCard> _cards;
    private RecruitmentDiceSystem _recruitmentSystem;

    /// <summary>
    /// 展示招募卡片
    /// </summary>
    /// <param name="cards">候选卡片列表</param>
    /// <param name="onSelected">选中某张卡片后回调</param>
    /// <param name="onSkipped">跳过招募回调</param>
    /// <param name="title">面板标题（默认"招募"）</param>
    /// <param name="skipText">跳过按钮文字（默认"跳过"）</param>
    public void ShowRecruitment(
        List<RecruitmentCard> cards,
        Action<RecruitmentCard> onSelected,
        Action onSkipped,
        string title = "招募",
        string skipText = "跳过")
    {
        _cards = cards;
        _onSelected = onSelected;
        _onSkipped = onSkipped;
        _recruitmentSystem = new RecruitmentDiceSystem();

        BuildUI(title, skipText);
    }

    private void BuildUI(string title, string skipText)
    {
        ClearChildren();

        // 背景
        var bg = CreateBackground();
        bg.transform.SetParent(transform, false);
        bg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        bg.GetComponent<RectTransform>().anchorMax = Vector2.one;
        bg.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        bg.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // 标题
        var titleText = CreateText("Title", title, 34, new Color(1f, 0.85f, 0.4f));
        titleText.rectTransform.anchoredPosition = new Vector2(0, 350);

        // 卡片列表
        if (_cards != null && _cards.Count > 0)
        {
            float cardWidth = 400f;
            float totalWidth = _cards.Count * cardWidth + (_cards.Count - 1) * 30f;
            float startX = -totalWidth / 2f + cardWidth / 2f;

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                float xPos = startX + i * (cardWidth + 30f);
                CreateCardUI(card, i, xPos);
            }
        }

        // 跳过按钮
        var skipBtn = CreateButton("SkipButton", skipText, OnSkip, new Color(0.4f, 0.4f, 0.4f));
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

        // 稀有度颜色条
        Color rarityColor = card.rarity switch
        {
            2 => new Color(1f, 0.5f, 0f),    // Rare - 橙色
            1 => new Color(0.3f, 0.6f, 1f),  // Advanced - 蓝色
            _ => new Color(0.6f, 0.6f, 0.6f) // Normal - 灰色
        };

        var rarityBar = CreateChildImage(cardGo.transform, "RarityBar", new Vector2(380, 8), new Vector2(0, 280), rarityColor);

        // 兵种名称
        string rarityStr = card.rarity switch
        {
            2 => "[稀有]",
            1 => "[高级]",
            _ => "[普通]"
        };
        var nameText = CreateChildText(cardGo.transform, "Name", $"{rarityStr} {card.name}", 26, Color.white);
        nameText.rectTransform.anchoredPosition = new Vector2(0, 230);

        // 属性信息
        var cfg = card.config;
        if (cfg != null)
        {
            string statsText = $"HP: {cfg.hp}  攻: {cfg.attack}  防: {cfg.defense}\n速度: {cfg.moveSpeed}  攻速: {cfg.attackSpeed}";
            var statsLabel = CreateChildText(cardGo.transform, "Stats", statsText, 18, new Color(0.8f, 0.8f, 0.8f));
            statsLabel.rectTransform.anchoredPosition = new Vector2(0, 150);

            string tierText = $"Tier: {cfg.tier}";
            var tierLabel = CreateChildText(cardGo.transform, "Tier", tierText, 16, new Color(0.6f, 0.6f, 0.6f));
            tierLabel.rectTransform.anchoredPosition = new Vector2(0, 90);
        }

        // 天生强化标记
        if (card.bornEnhanced)
        {
            var enhanceLabel = CreateChildText(cardGo.transform, "Enhanced", "天生强化 (+50%)", 18, new Color(1f, 0.85f, 0.3f));
            enhanceLabel.rectTransform.anchoredPosition = new Vector2(0, 50);
        }

        // 费用
        string costText = card.goldCost > 0 ? $"费用: {card.goldCost}" : "免费";
        var costLabel = CreateChildText(cardGo.transform, "Cost", costText, 20, card.goldCost > 0 ? new Color(1f, 0.9f, 0.5f) : new Color(0.5f, 1f, 0.5f));
        costLabel.rectTransform.anchoredPosition = new Vector2(0, -10);

        // 掷骰子结果（如果已有）
        if (card.diceResult == DiceResult.Success)
        {
            var resultLabel = CreateChildText(cardGo.transform, "Result", "招募成功!", 24, new Color(0.3f, 1f, 0.3f));
            resultLabel.rectTransform.anchoredPosition = new Vector2(0, -60);

            var selectBtn = CreateChildButton(cardGo.transform, "SelectBtn", "招募", () => OnSelectCard(card), new Color(0.2f, 0.7f, 0.3f));
            selectBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -130);
        }
        else if (card.diceResult == DiceResult.Failure)
        {
            var resultLabel = CreateChildText(cardGo.transform, "Result", "招募失败", 24, new Color(1f, 0.3f, 0.3f));
            resultLabel.rectTransform.anchoredPosition = new Vector2(0, -130);
        }
        else
        {
            // 未掷骰子：先掷骰子再选择
            var rollBtn = CreateChildButton(cardGo.transform, "RollBtn", "掷骰子", () => OnRollDice(card, index, xPos), new Color(0.5f, 0.3f, 0.7f));
            rollBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -130);
        }
    }

    private void OnRollDice(RecruitmentCard card, int index, float xPos)
    {
        _recruitmentSystem.RollDice(card);

        // 重建这张卡片的UI
        // 先移除旧卡片
        Transform oldCard = transform.Find($"Card_{index}");
        if (oldCard != null)
            Destroy(oldCard.gameObject);

        // 重建
        CreateCardUI(card, index, xPos);
    }

    private void OnSelectCard(RecruitmentCard card)
    {
        _onSelected?.Invoke(card);
        Close();
    }

    private void OnSkip()
    {
        _onSkipped?.Invoke();
        Close();
    }

    // ── UI 工具方法 ──

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
        var rect = go.AddComponent<RectTransform>();
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
