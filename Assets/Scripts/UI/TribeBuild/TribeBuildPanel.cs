using System;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 族群构筑主面板 — 包含命运/抉择/商店/战斗入口
/// </summary>
public class TribeBuildPanel : UIPanel
{
    private Text _infoText;
    private Text _enemyText;

    public override void Initialize()
    {
        base.Initialize();

        // 背景
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        bgGo.transform.SetAsFirstSibling();
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(1920, 1080);
        bgRect.anchoredPosition = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        var bgSprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>("ui/sprite/buildcard/zhujiemian_bg_beijing");
        if (bgSprite != null)
        {
            bgImg.sprite = bgSprite;
            bgImg.SetNativeSize();
        }
        else
        {
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        }

        var title = CreateText("Title", "族群构筑", 32, Color.white);
        title.rectTransform.anchoredPosition = new Vector2(0, 400);

        _infoText = CreateText("Info", "", 20, Color.yellow);
        _infoText.rectTransform.anchoredPosition = new Vector2(0, 320);

        _enemyText = CreateText("EnemyInfo", "", 32, Color.white);
        _enemyText.rectTransform.anchoredPosition = new Vector2(0, 280);

        // 操作按钮
        var battleBtn = CreateButton("BattleButton", "开始战斗", OnStartBattle);
        battleBtn.anchoredPosition = new Vector2(0, 0);

        var closeBtn = CreateButton("CloseButton", "关闭", OnClose);
        closeBtn.anchoredPosition = new Vector2(0, -80);
    }

    public override void Show()
    {
        base.Show();
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        var dataManager = GameManager.Instance?.DataManager;
        var gfc = GameFlowController.Instance;
        if (dataManager == null) return;

        int round = gfc != null ? gfc.CurrentRound : dataManager.GetCurrentRound();
        long catFood = dataManager.GetCatFood();
        int leadership = dataManager.GetLeadership();

        _infoText.text = $"第 {round} 关 | 猫粮: {catFood} | 领导力: {leadership}";

        // 显示敌人信息
        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        if (campaign != null)
        {
            int[] enemyIds = campaign.GetEnemyUnitIdsForBattle(round);
            if (enemyIds != null && enemyIds.Length > 0)
            {
                var enemyCounts = new System.Collections.Generic.Dictionary<int, int>();
                foreach (int id in enemyIds)
                {
                    if (enemyCounts.ContainsKey(id)) enemyCounts[id]++;
                    else enemyCounts[id] = 1;
                }

                var parts = new System.Collections.Generic.List<string>();
                foreach (var kv in enemyCounts)
                {
                    var cfg = TribeConfigLoader.Instance?.GetFighterConfig(kv.Key);
                    string name = cfg?.fighterName ?? $"#{kv.Key}";
                    parts.Add(kv.Value > 1 ? $"{name}x{kv.Value}" : name);
                }

                _enemyText.text = $"敌人: {string.Join("、", parts)}";
            }
            else
            {
                _enemyText.text = "";
            }
        }
    }

    private void OnStartBattle()
    {
        GameLogger.Log("TribeBuild", "StartBattle → BattlePreparePanel");
        Hide();
        UIManager uiManager = GameManager.Instance?.UIManager;
        var panel = uiManager?.ShowPanel<BattlePreparePanel>(UIManager.UILayer.Normal);

        // 传入当前关卡和节点类型
        var gfc = GameFlowController.Instance;
        MapNodeType nodeType = MapNodeType.Battle;
        if (gfc.CurrentRegionMap != null && gfc.CurrentNodeId >= 0)
        {
            var node = gfc.CurrentRegionMap.GetNode(gfc.CurrentNodeId);
            if (node != null) nodeType = node.nodeType;
        }
        panel?.Setup(gfc.CurrentRound, nodeType);
    }

    private void OnClose()
    {
        GameLogger.Log("TribeBuild", "Close");
        Hide();
        GameFlowController.Instance.EnterMapSelection();
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 60);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        return txt;
    }

    private RectTransform CreateButton(string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 60);
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
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return rect;
    }
}
