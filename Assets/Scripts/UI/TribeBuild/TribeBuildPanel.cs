using System;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 族群构筑主面板 — 包含命运/抉择/商店/战斗入口
/// </summary>
public class TribeBuildPanel : UIPanel
{
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

        // 操作按钮
        var battleBtn = CreateButton("BattleButton", "开始战斗", OnStartBattle);
        battleBtn.anchoredPosition = new Vector2(0, 0);

        var closeBtn = CreateButton("CloseButton", "关闭", OnClose);
        closeBtn.anchoredPosition = new Vector2(0, -80);

        // 显示当前信息
        var dataManager = GameManager.Instance?.DataManager;
        if (dataManager != null)
        {
            int round = dataManager.GetCurrentRound();
            long catFood = dataManager.GetCatFood();
            int leadership = dataManager.GetLeadership();

            var infoText = CreateText("Info", $"第 {round} 关 | 猫粮: {catFood} | 领导力: {leadership}", 20, Color.yellow);
            infoText.rectTransform.anchoredPosition = new Vector2(0, 320);
        }
    }

    private void OnStartBattle()
    {
        GameLogger.Log("TribeBuild", "StartBattle → BattlePreparePanel");
        Hide();
        UIManager uiManager = GameManager.Instance?.UIManager;
        uiManager?.ShowPanel<BattlePreparePanel>(UIManager.UILayer.Normal);
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
