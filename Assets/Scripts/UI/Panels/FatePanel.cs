using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 命运面板 — 选择档次 + 选择祝福 两步流程
/// 设计参考：正式文档/105_系统_命运.md
/// </summary>
public class FatePanel : UIPanel
{
    private Action _onComplete;
    private FateSystem _fateSystem;
    private string _selectedTier;
    private List<FateBlessingOption> _blessings;
    private bool _showingBlessings;

    /// <summary>
    /// 显示命运面板
    /// </summary>
    public void ShowFate(FateSystem fateSystem, Action onComplete)
    {
        _fateSystem = fateSystem;
        if (_fateSystem == null)
        {
            _fateSystem = new FateSystem();
            _fateSystem.Initialize();
        }
        _onComplete = onComplete;
        _showingBlessings = false;
        ShowTierSelection();
    }

    // ── 第一步：选择档次 ──

    private void ShowTierSelection()
    {
        ClearChildren();

        var bg = CreateImage("BG", new Color(0.1f, 0.1f, 0.15f, 0.95f));
        bg.rectTransform.anchoredPosition = Vector2.zero;

        var title = CreateText("Title", "命运 — 选择祈愿档次", 36, new Color(1f, 0.85f, 0.4f));
        title.rectTransform.anchoredPosition = new Vector2(0, 350);

        long currentGold = GameManager.Instance?.CurrencyManager?.GetCurrencyAmount(CurrencyType.Gold) ?? 0;
        var statusText = CreateText("Status", $"当前猫币: {currentGold} | 档次数量: {_fateSystem.GetTierConfigs()?.Count ?? 0}", 18, new Color(0.8f, 0.8f, 0.8f));
        statusText.rectTransform.anchoredPosition = new Vector2(0, 310);

        var tiers = _fateSystem.GetTierConfigs();
        if (tiers == null || tiers.Count == 0)
        {
            GameLogger.LogWarning("FatePanel", "命运档次配置为空，显示默认提示");
            var emptyHint = CreateText("EmptyHint", "命运档次加载失败，无法显示祈愿档次。", 24, Color.white);
            emptyHint.rectTransform.anchoredPosition = new Vector2(0, 100);

            var backBtn = CreateButton("BackBtn", "返回", 220, 60);
            backBtn.anchoredPosition = new Vector2(0, -120);
            backBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                GameLogger.Log("FatePanel", "返回命运");
                Close();
                _onComplete?.Invoke();
            });

            return;
        }

        float startX = -300f;
        float spacing = 300f;
        for (int i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            bool canAfford = _fateSystem.CanAffordTier(tier.tierName);
            string label = $"{tier.displayName}\n{costText(tier.cost)}";
            var btnRect = CreateButton($"Tier_{i}", label, 260, 100);
            btnRect.anchoredPosition = new Vector2(startX + i * spacing, 100);

            if (!canAfford)
            {
                btnRect.GetComponent<Button>().interactable = false;
                var text = btnRect.GetComponentInChildren<Text>();
                if (text != null) text.color = new Color(0.4f, 0.4f, 0.4f);
            }

            string capturedTier = tier.tierName;
            btnRect.GetComponent<Button>().onClick.AddListener(() => OnTierSelected(capturedTier));
        }

        // 跳过按钮
        var skipBtn = CreateButton("SkipBtn", "先等等", 200, 50);
        skipBtn.anchoredPosition = new Vector2(0, -250);
        skipBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            GameLogger.Log("FatePanel", "跳过祈愿");
            Close();
            _onComplete?.Invoke();
        });

        var hint = CreateText("Hint", "保底：每次祈愿必定获得100木天蓼叶", 18, new Color(0.6f, 0.6f, 0.6f));
        hint.rectTransform.anchoredPosition = new Vector2(0, -180);
    }

    private string costText(int cost)
    {
        return cost > 0 ? $"{cost} 木天蓼叶" : "免费";
    }

    private void OnTierSelected(string tierName)
    {
        GameLogger.Log("FatePanel", "选择档次: " + tierName);
        _selectedTier = tierName;

        // 扣除档次费用
        if (!_fateSystem.TrySpendTierCost(tierName))
        {
            GameLogger.Log("FatePanel", "猫币不足");
            return;
        }

        // 生成祝福
        _blessings = _fateSystem.GenerateBlessings(tierName);
        _showingBlessings = true;
        ShowBlessingSelection();
    }

    // ── 第二步：选择祝福 ──

    private void ShowBlessingSelection()
    {
        ClearChildren();

        var bg = CreateImage("BG", new Color(0.1f, 0.15f, 0.1f, 0.95f));
        bg.rectTransform.anchoredPosition = Vector2.zero;

        var tierConfig = _fateSystem.GetTierConfig(_selectedTier);
        string tierName = tierConfig != null ? tierConfig.displayName : _selectedTier;

        var title = CreateText("Title", $"{tierName} — 选择祝福", 32, new Color(1f, 0.85f, 0.4f));
        title.rectTransform.anchoredPosition = new Vector2(0, 350);

        if (_blessings == null || _blessings.Count == 0)
        {
            var noBlessing = CreateText("NoBlessing", "没有可用的祝福", 24, Color.gray);
            noBlessing.rectTransform.anchoredPosition = new Vector2(0, 100);

            var confirmBtn = CreateButton("ConfirmBtn", "确定", 200, 50);
            confirmBtn.anchoredPosition = new Vector2(0, -100);
            confirmBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                _fateSystem.ApplyGuaranteedCatFood();
                Close();
                _onComplete?.Invoke();
            });
            return;
        }

        float cardWidth = 280f;
        float spacing = 310f;
        float startX = -((_blessings.Count - 1) * spacing) / 2f;

        for (int i = 0; i < _blessings.Count; i++)
        {
            var blessing = _blessings[i];
            float x = startX + i * spacing;

            // 卡片背景
            var card = CreateImage($"Card_{i}", new Color(0.2f, 0.2f, 0.25f, 1f));
            card.rectTransform.anchoredPosition = new Vector2(x, 80);
            card.rectTransform.sizeDelta = new Vector2(cardWidth, 220);

            // 祝福名称
            var nameText = CreateText($"Name_{i}", blessing.displayName, 22, new Color(1f, 0.9f, 0.6f));
            nameText.rectTransform.anchoredPosition = new Vector2(x, 140);

            // 祝福描述
            var descText = CreateText($"Desc_{i}", blessing.description, 18, new Color(0.8f, 0.8f, 0.8f));
            descText.rectTransform.anchoredPosition = new Vector2(x, 80);

            // 选择按钮
            int capturedIndex = i;
            var selectBtn = CreateButton($"Select_{i}", "选择", 200, 45);
            selectBtn.anchoredPosition = new Vector2(x, 0);
            selectBtn.GetComponent<Button>().onClick.AddListener(() => OnBlessingSelected(capturedIndex));
        }
    }

    private void OnBlessingSelected(int index)
    {
        if (_blessings == null || index < 0 || index >= _blessings.Count) return;

        var blessing = _blessings[index];
        GameLogger.Log("FatePanel", "选择祝福: " + blessing.displayName);

        _fateSystem.ApplyBlessing(blessing);
        _fateSystem.ApplyGuaranteedCatFood();

        Close();
        _onComplete?.Invoke();
    }

    // ── UI 工具方法 ──

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private Image CreateImage(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1920, 1080);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
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

    private RectTransform CreateButton(string name, string label, float width, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.4f, 1f);

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
