using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 鍛借繍闈㈡澘 鈥?閫夋嫨妗ｆ + 閫夋嫨绁濈 涓ゆ娴佺▼
/// 璁捐鍙傝€冿細姝ｅ紡鏂囨。/105_绯荤粺_鍛借繍.md
/// </summary>
public class FatePanel : UIPanel
{
    private Action _onComplete;
    private FateSystem _fateSystem;
    private string _selectedTier;
    private List<FateBlessingOption> _blessings;
    private bool _showingBlessings;

    /// <summary>
    /// 鏄剧ず鍛借繍闈㈡澘
    /// </summary>
    public void ShowFate(FateSystem fateSystem, Action onComplete)
    {
        _fateSystem = fateSystem;
        _onComplete = onComplete;
        _showingBlessings = false;

        if (_fateSystem == null)
        {
            GameLogger.LogError("FatePanel", "ShowFate called with null FateSystem");
            Close();
            _onComplete?.Invoke();
            return;
        }

        ShowTierSelection();
    }

    // 鈹€鈹€ 绗竴姝ワ細閫夋嫨妗ｆ 鈹€鈹€

    private void ShowTierSelection()
    {
        ClearChildren();

        var bg = CreateImage("BG", new Color(0.1f, 0.1f, 0.15f, 0.95f));
        bg.rectTransform.anchoredPosition = Vector2.zero;

        var title = CreateText("Title", "鍛借繍 鈥?閫夋嫨绁堟効妗ｆ", 36, new Color(1f, 0.85f, 0.4f));
        title.rectTransform.anchoredPosition = new Vector2(0, 350);

        var tiers = _fateSystem.GetTierConfigs();
        if (tiers == null || tiers.Count == 0)
        {
            GameLogger.LogWarning("FatePanel", "No tier configs available");

            var emptyText = CreateText("Empty", "\u6682\u65E0\u53EF\u7528\u7684\u7948\u798F\u6863\u6B21", 24, Color.gray);
            emptyText.rectTransform.anchoredPosition = new Vector2(0, 100);

            var confirmBtn = CreateButton("ConfirmBtn", "纭畾", 200, 50);
            confirmBtn.anchoredPosition = new Vector2(0, -100);
            confirmBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
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

        // 璺宠繃鎸夐挳
        var skipBtn = CreateButton("SkipBtn", "\u5148\u7B49\u7B49", 200, 50);
        skipBtn.anchoredPosition = new Vector2(0, -250);
        skipBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            GameLogger.Log("FatePanel", "璺宠繃绁堟効");
            Close();
            _onComplete?.Invoke();
        });

        var hint = CreateText("Hint", "\u4FDD\u5E95\uFF1A\u6BCF\u6B21\u7948\u613F\u5FC5\u5B9A\u83B7\u5F97 100 \u5C0F\u9C7C\u5E72", 18, new Color(0.6f, 0.6f, 0.6f));
        hint.rectTransform.anchoredPosition = new Vector2(0, -180);
    }

    private string costText(int cost)
    {
        return cost > 0 ? $"{cost} \u5C0F\u9C7C\u5E72" : "\u514D\u8D39";
    }

    private void OnTierSelected(string tierName)
    {
        GameLogger.Log("FatePanel", "閫夋嫨妗ｆ: " + tierName);
        _selectedTier = tierName;

        // 鎵ｉ櫎妗ｆ璐圭敤
        if (!_fateSystem.TrySpendTierCost(tierName))
        {
            GameLogger.Log("FatePanel", "鐚竵涓嶈冻");
            return;
        }

        // 鐢熸垚绁濈
        _blessings = _fateSystem.GenerateBlessings(tierName);
        _showingBlessings = true;
        ShowBlessingSelection();
    }

    // 鈹€鈹€ 绗簩姝ワ細閫夋嫨绁濈 鈹€鈹€

    private void ShowBlessingSelection()
    {
        ClearChildren();

        var bg = CreateImage("BG", new Color(0.1f, 0.15f, 0.1f, 0.95f));
        bg.rectTransform.anchoredPosition = Vector2.zero;

        var tierConfig = _fateSystem.GetTierConfig(_selectedTier);
        string tierName = tierConfig != null ? tierConfig.displayName : _selectedTier;

        var title = CreateText("Title", $"{tierName} 鈥?閫夋嫨绁濈", 32, new Color(1f, 0.85f, 0.4f));
        title.rectTransform.anchoredPosition = new Vector2(0, 350);

        if (_blessings == null || _blessings.Count == 0)
        {
            var noBlessing = CreateText("NoBlessing", "\u6CA1\u6709\u53EF\u7528\u7684\u795D\u798F", 24, Color.gray);
            noBlessing.rectTransform.anchoredPosition = new Vector2(0, 100);

            var confirmBtn = CreateButton("ConfirmBtn", "纭畾", 200, 50);
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

            // 鍗＄墖鑳屾櫙
            var card = CreateImage($"Card_{i}", new Color(0.2f, 0.2f, 0.25f, 1f));
            card.rectTransform.anchoredPosition = new Vector2(x, 80);
            card.rectTransform.sizeDelta = new Vector2(cardWidth, 220);

            // 绁濈鍚嶇О
            var nameText = CreateText($"Name_{i}", blessing.displayName, 22, new Color(1f, 0.9f, 0.6f));
            nameText.rectTransform.anchoredPosition = new Vector2(x, 140);

            // 绁濈鎻忚堪
            var descText = CreateText($"Desc_{i}", blessing.description, 18, new Color(0.8f, 0.8f, 0.8f));
            descText.rectTransform.anchoredPosition = new Vector2(x, 80);

            // 閫夋嫨鎸夐挳
            int capturedIndex = i;
            var selectBtn = CreateButton($"Select_{i}", "閫夋嫨", 200, 45);
            selectBtn.anchoredPosition = new Vector2(x, 0);
            selectBtn.GetComponent<Button>().onClick.AddListener(() => OnBlessingSelected(capturedIndex));
        }
    }

    private void OnBlessingSelected(int index)
    {
        if (_blessings == null || index < 0 || index >= _blessings.Count) return;

        var blessing = _blessings[index];
        GameLogger.Log("FatePanel", "閫夋嫨绁濈: " + blessing.displayName);

        _fateSystem.ApplyBlessing(blessing);
        _fateSystem.ApplyGuaranteedCatFood();

        Close();
        _onComplete?.Invoke();
    }

    // 鈹€鈹€ UI 宸ュ叿鏂规硶 鈹€鈹€

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
