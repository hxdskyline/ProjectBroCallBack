using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 温泉面板 — 二选一：全体回复满 HP 或选择一个单位强化（全属性+50%，回满HP）
/// </summary>
public class HotSpringPanel : UIPanel
{
    private Action _onComplete;
    private bool _showingEnhanceList;

    public void ShowHotSpring(Action onComplete)
    {
        _onComplete = onComplete;
        _showingEnhanceList = false;
        ShowMainChoices();
    }

    // ── 主选择界面 ──

    private void ShowMainChoices()
    {
        ClearChildren();

        var title = CreateText("Title", "温泉", 36, new Color(0.7f, 0.9f, 1f));
        title.rectTransform.anchoredPosition = new Vector2(0, 200);

        var desc = CreateText("Desc", "选择一个效果", 24, Color.white);
        desc.rectTransform.anchoredPosition = new Vector2(0, 100);

        var healBtn = CreateButton("HealButton", "全体回复满 HP", OnHeal);
        healBtn.anchoredPosition = new Vector2(0, -20);

        var buffBtn = CreateButton("BuffButton", "选择一个单位强化", OnShowEnhanceList);
        buffBtn.anchoredPosition = new Vector2(0, -100);
    }

    // ── 选项A：全体回满血 ──

    private void OnHeal()
    {
        GameLogger.Log("HotSpring", "Heal");
        var healthSystem = new HealthPersistenceSystem();
        healthSystem.HealAllAlliesPercent(1.0f);
        Close();
        _onComplete?.Invoke();
    }

    // ── 选项B：展示可强化兵种列表 ──

    private void OnShowEnhanceList()
    {
        var enhanceable = GetEnhanceableUnits();
        if (enhanceable.Count == 0)
        {
            // 没有可强化的兵种，直接回满血
            GameLogger.Log("HotSpring", "NoEnhanceable-Heal");
            var healthSystem = new HealthPersistenceSystem();
            healthSystem.HealAllAlliesPercent(1.0f);
            Close();
            _onComplete?.Invoke();
            return;
        }

        _showingEnhanceList = true;
        ShowEnhanceList(enhanceable);
    }

    private List<FighterData> GetEnhanceableUnits()
    {
        var result = new List<FighterData>();
        var dataManager = GameManager.Instance?.DataManager;
        if (dataManager == null) return result;

        var tribes = dataManager.GetTribes();
        if (tribes == null) return result;

        foreach (var tribe in tribes)
        {
            if (tribe == null || !tribe.isActive || tribe.units == null) continue;
            foreach (var unit in tribe.units)
            {
                if (unit != null && !unit.IsEnhanced())
                    result.Add(unit);
            }
        }
        return result;
    }

    private void ShowEnhanceList(List<FighterData> units)
    {
        ClearChildren();

        var title = CreateText("Title", "选择强化单位", 30, new Color(1f, 0.85f, 0.4f));
        title.rectTransform.anchoredPosition = new Vector2(0, 320);

        var desc = CreateText("Desc", "强化后全属性+50%，HP回满", 20, new Color(0.8f, 0.8f, 0.8f));
        desc.rectTransform.anchoredPosition = new Vector2(0, 270);

        // 兵种列表
        float startY = 180f;
        float spacing = 70f;
        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            var config = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);

            string rarityStr = unit.GetRarity() switch
            {
                Rarity.Rare => "[稀有] ",
                Rarity.Advanced => "[高级] ",
                _ => ""
            };
            string label = $"{rarityStr}{unit.name}  HP:{unit.currentHp}/{config?.hp ?? 100}  攻:{config?.attack ?? 0}  防:{config?.defense ?? 0}";

            int capturedIndex = i;
            var btn = CreateButton($"Unit_{i}", label, () => OnEnhanceUnit(units[capturedIndex]));
            btn.anchoredPosition = new Vector2(0, startY - i * spacing);
        }

        // 返回按钮
        float backY = startY - units.Count * spacing - 30f;
        var backBtn = CreateButton("BackButton", "返回", ShowMainChoices);
        backBtn.anchoredPosition = new Vector2(0, backY);
    }

    private void OnEnhanceUnit(FighterData unit)
    {
        GameLogger.Log("HotSpring", $"Enhance: {unit.name}");
        EnhancementService.EnhanceFighter(unit);
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

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(800, 60);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;
    }

    private RectTransform CreateButton(string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 55);
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
        txt.fontSize = 20;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return rect;
    }
}
