using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 猫市面板 — 自由交易商店
/// 设计参考：正式文档/104_系统_猫市.md
/// </summary>
public class ShopPanel : UIPanel
{
    private ShopSystem _shopSystem;
    private Action _onComplete;
    private Text _currencyText;
    private Text _refreshText;
    private List<ShopItem> _items;

    /// <summary>
    /// 显示商店面板
    /// </summary>
    public void ShowShop(ShopSystem shopSystem, Action onComplete)
    {
        _shopSystem = shopSystem;
        _onComplete = onComplete;
        _items = _shopSystem.GetCurrentItems();
        BuildUI();
    }

    private void BuildUI()
    {
        ClearChildren();

        var bg = CreateImage("BG", new Color(0.1f, 0.1f, 0.15f, 0.95f));
        bg.rectTransform.anchoredPosition = Vector2.zero;

        var title = CreateText("Title", "猫市", 36, new Color(1f, 0.85f, 0.4f));
        title.rectTransform.anchoredPosition = new Vector2(0, 400);

        long catFood = GameManager.Instance?.DataManager?.GetCatFood() ?? 0;
        _currencyText = CreateText("Currency", "小鱼干: " + catFood, 24, new Color(1f, 0.9f, 0.5f));
        _currencyText.rectTransform.anchoredPosition = new Vector2(0, 340);

        // 奸商陷阱提示
        var dm = GameManager.Instance?.DataManager;
        if (dm != null && dm.IsShopRefreshLocked())
        {
            var lockHint = CreateText("LockHint", "奸商陷阱已生效：商店价格+20%，刷新被禁止", 16, new Color(1f, 0.5f, 0.3f));
            lockHint.rectTransform.anchoredPosition = new Vector2(0, 290);
        }

        // 商品槽位
        float[] xPositions = { -450, -150, 150, 450 };
        for (int i = 0; i < _items.Count && i < 4; i++)
        {
            CreateShopItemSlot(i, _items[i], xPositions[i], 100);
        }

        // 刷新按钮
        int refreshCost = _shopSystem.GetRefreshCost();
        bool canRefresh = _shopSystem.CanRefresh();
        var refreshBtn = CreateButton("RefreshBtn", "刷新 (" + refreshCost + "小鱼干)", 250, 60, canRefresh ? new Color(0.2f, 0.4f, 0.6f) : new Color(0.3f, 0.3f, 0.3f));
        refreshBtn.anchoredPosition = new Vector2(-200, -300);
        refreshBtn.GetComponent<Button>().interactable = canRefresh;
        _refreshText = refreshBtn.GetComponentInChildren<Text>();
        refreshBtn.GetComponent<Button>().onClick.AddListener(OnRefresh);

        // 离开按钮
        var closeBtn = CreateButton("CloseBtn", "离开", 250, 60, new Color(0.5f, 0.2f, 0.2f));
        closeBtn.anchoredPosition = new Vector2(200, -300);
        closeBtn.GetComponent<Button>().onClick.AddListener(Complete);
    }

    private void CreateShopItemSlot(int index, ShopItem item, float x, float y)
    {
        var slotGo = new GameObject("Slot" + index);
        slotGo.transform.SetParent(transform, false);
        var slotRect = slotGo.AddComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0.5f, 0.5f);
        slotRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotRect.sizeDelta = new Vector2(280, 200);
        slotRect.anchoredPosition = new Vector2(x, y);

        var slotBg = slotGo.AddComponent<Image>();
        slotBg.color = item.sold ? new Color(0.2f, 0.2f, 0.2f, 0.8f) : new Color(0.25f, 0.25f, 0.35f, 0.9f);

        string typeLabel = item.type == ShopItemType.Artifact ? "[奇物]" : item.type == ShopItemType.Consumable ? "[消耗品]" : "[兵种]";
        var nameText = CreateChildText(slotGo.transform, "Name", typeLabel + " " + item.name, 18, Color.white);
        nameText.rectTransform.anchoredPosition = new Vector2(0, 70);

        var descText = CreateChildText(slotGo.transform, "Desc", item.description, 14, new Color(0.7f, 0.7f, 0.7f));
        descText.rectTransform.anchoredPosition = new Vector2(0, 20);

        var priceText = CreateChildText(slotGo.transform, "Price", item.price + " 小鱼干", 18, new Color(1f, 0.85f, 0.4f));
        priceText.rectTransform.anchoredPosition = new Vector2(0, -30);

        var buyBtnGo = new GameObject("BuyBtn");
        buyBtnGo.transform.SetParent(slotGo.transform, false);
        var buyRect = buyBtnGo.AddComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(0.5f, 0.5f);
        buyRect.anchorMax = new Vector2(0.5f, 0.5f);
        buyRect.sizeDelta = new Vector2(180, 40);
        buyRect.anchoredPosition = new Vector2(0, -70);

        var buyImg = buyBtnGo.AddComponent<Image>();
        buyImg.color = item.sold ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.2f, 0.5f, 0.2f);

        var buyBtn = buyBtnGo.AddComponent<Button>();
        buyBtn.targetGraphic = buyImg;
        buyBtn.interactable = !item.sold;

        var buyText = CreateChildText(buyBtnGo.transform, "Text", item.sold ? "已售" : "购买", 16, Color.white);
        buyText.rectTransform.anchoredPosition = Vector2.zero;

        if (!item.sold)
        {
            int capturedIndex = index;
            buyBtn.onClick.AddListener(() => OnPurchase(capturedIndex));
        }
    }

    private void OnPurchase(int slotIndex)
    {
        bool success = _shopSystem.TryBuyItem(slotIndex);
        if (success)
        {
            _items = _shopSystem.GetCurrentItems();
            RefreshUI();
        }
    }

    private void OnRefresh()
    {
        bool success = _shopSystem.TryRefresh();
        if (success)
        {
            _items = _shopSystem.GetCurrentItems();
            RefreshUI();
        }
    }

    private void RefreshUI()
    {
        // 更新货币
        long catFood = GameManager.Instance?.DataManager?.GetCatFood() ?? 0;
        if (_currencyText != null) _currencyText.text = "小鱼干: " + catFood;

        // 更新刷新按钮
        if (_refreshText != null)
        {
            int refreshCost = _shopSystem.GetRefreshCost();
            _refreshText.text = "刷新 (" + refreshCost + "小鱼干)";
        }

        // 重建商品槽位
        for (int i = 0; i < 4; i++)
        {
            var oldSlot = transform.Find("Slot" + i);
            if (oldSlot != null) Destroy(oldSlot.gameObject);
        }

        float[] xPositions = { -450, -150, 150, 450 };
        for (int i = 0; i < _items.Count && i < 4; i++)
        {
            CreateShopItemSlot(i, _items[i], xPositions[i], 100);
        }
    }

    private void Complete()
    {
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
        rect.sizeDelta = new Vector2(400, 60);
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

    private Text CreateChildText(Transform parent, string name, string text, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(260, 50);
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
        text.fontSize = 20;
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
