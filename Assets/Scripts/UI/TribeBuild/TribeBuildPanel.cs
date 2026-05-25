using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Camp;

/// <summary>
/// 族群构筑主面板 — 包含敌人展示、我方兵种列表、战斗入口
/// </summary>
public class TribeBuildPanel : UIPanel
{
    private Font _font;
    private RectTransform _enemyContainer;
    private RectTransform _allyContainer;
    private Text _infoText;
    private GameObject _tooltip;
    private Text _tooltipName;
    private Text _tooltipDetail;
    private Image _tooltipAvatar;

    public override void Initialize()
    {
        base.Initialize();
        _font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");

        // ── 背景 ──
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        bgGo.transform.SetAsFirstSibling();
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = bgRect.anchorMax = bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.sizeDelta = new Vector2(1920, 1080);
        bgRect.anchoredPosition = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        var bgSprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>("ui/sprite/buildcard/zhujiemian_bg_beijing");
        if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.SetNativeSize(); }
        else { bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f); }

        // ── 标题 ──
        var title = MakeText("Title", "族群构筑", 32, Color.white);
        title.rectTransform.anchoredPosition = new Vector2(0, 480);
        title.rectTransform.sizeDelta = new Vector2(400, 50);

        // ── 信息栏 ──
        _infoText = MakeText("Info", "", 20, Color.yellow);
        _infoText.rectTransform.anchoredPosition = new Vector2(0, 440);
        _infoText.rectTransform.sizeDelta = new Vector2(800, 35);

        // ── 敌人区标题 ──
        var enemyTitle = MakeText("EnemyTitle", "— 敌人 —", 24, new Color(1f, 0.6f, 0.6f));
        enemyTitle.rectTransform.anchoredPosition = new Vector2(0, 390);
        enemyTitle.rectTransform.sizeDelta = new Vector2(400, 35);

        // ── 敌人容器 ──
        var enemyContGo = new GameObject("EnemyContainer");
        enemyContGo.transform.SetParent(transform, false);
        _enemyContainer = enemyContGo.AddComponent<RectTransform>();
        _enemyContainer.anchorMin = _enemyContainer.anchorMax = _enemyContainer.pivot = new Vector2(0.5f, 0.5f);
        _enemyContainer.anchoredPosition = new Vector2(0, 290);
        _enemyContainer.sizeDelta = new Vector2(1200, 120);

        // ── 我方区标题 ──
        var allyTitle = MakeText("AllyTitle", "— 我方兵种 —", 24, new Color(0.6f, 0.85f, 1f));
        allyTitle.rectTransform.anchoredPosition = new Vector2(0, 220);
        allyTitle.rectTransform.sizeDelta = new Vector2(400, 35);

        // ── 我方容器（带滚动） ──
        var allyContGo = new GameObject("AllyContainer");
        allyContGo.transform.SetParent(transform, false);
        _allyContainer = allyContGo.AddComponent<RectTransform>();
        _allyContainer.anchorMin = _allyContainer.anchorMax = _allyContainer.pivot = new Vector2(0.5f, 0.5f);
        _allyContainer.anchoredPosition = new Vector2(0, 80);
        _allyContainer.sizeDelta = new Vector2(1400, 280);

        // ── 按钮 ──
        var battleBtn = MakeButton("BattleButton", "开始战斗", OnStartBattle);
        battleBtn.anchoredPosition = new Vector2(0, -160);

        var closeBtn = MakeButton("CloseButton", "返回地图", OnClose);
        closeBtn.anchoredPosition = new Vector2(0, -240);

        // ── Tooltip（默认隐藏） ──
        BuildTooltip();
    }

    public override void Show()
    {
        base.Show();
        RefreshAll();
    }

    // ══════════════════════════════════════
    // 刷新
    // ══════════════════════════════════════

    private void RefreshAll()
    {
        var dataManager = GameManager.Instance?.DataManager;
        var gfc = GameFlowController.Instance;
        if (dataManager == null) return;

        int round = gfc != null ? gfc.CurrentRound : dataManager.GetCurrentRound();
        long catFood = dataManager.GetCatFood();
        int leadership = dataManager.GetLeadership();
        _infoText.text = $"第 {round} 关 | 猫粮: {catFood} | 领导力: {leadership}";

        RefreshEnemies(round);
        RefreshAllies();
        HideTooltip();
    }

    // ── 敌人 ──

    private void RefreshEnemies(int round)
    {
        ClearChildren(_enemyContainer);

        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        if (campaign == null) return;
        int[] enemyIds = campaign.GetEnemyUnitIdsForBattle(round);
        if (enemyIds == null || enemyIds.Length == 0) return;

        // 合并同类
        var counts = new Dictionary<int, int>();
        foreach (int id in enemyIds)
        {
            if (counts.ContainsKey(id)) counts[id]++;
            else counts[id] = 1;
        }

        float itemW = 100f;
        float gap = 20f;
        int idx = 0;
        foreach (var kv in counts)
        {
            var cfg = TribeConfigLoader.Instance?.GetFighterConfig(kv.Key);
            float x = (idx - (counts.Count - 1) / 2f) * (itemW + gap);
            CreateEnemyItem(_enemyContainer, cfg, kv.Key, kv.Value, x);
            idx++;
        }
    }

    private void CreateEnemyItem(RectTransform parent, FighterConfig cfg, int fighterId, int count, float x)
    {
        var go = new GameObject($"Enemy_{fighterId}");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0);
        rect.sizeDelta = new Vector2(90, 110);

        // 背景
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.15f, 0.15f, 0.8f);

        // 头像
        var avatarGo = new GameObject("Avatar");
        avatarGo.transform.SetParent(go.transform, false);
        var avatarRect = avatarGo.AddComponent<RectTransform>();
        avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0.5f, 0.7f);
        avatarRect.pivot = new Vector2(0.5f, 0.5f);
        avatarRect.sizeDelta = new Vector2(60, 60);
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.preserveAspect = true;
        if (cfg != null && !string.IsNullOrEmpty(cfg.avatarId))
        {
            var sprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>($"avatartemp/{cfg.avatarId}1");
            if (sprite != null) avatarImg.sprite = sprite;
        }
        avatarImg.raycastTarget = false;

        // 数量
        var countGo = new GameObject("Count");
        countGo.transform.SetParent(go.transform, false);
        var countRect = countGo.AddComponent<RectTransform>();
        countRect.anchorMin = countRect.anchorMax = new Vector2(0.5f, 0.1f);
        countRect.pivot = new Vector2(0.5f, 0.5f);
        countRect.sizeDelta = new Vector2(80, 25);
        var countTxt = countGo.AddComponent<Text>();
        countTxt.text = count > 1 ? $"x{count}" : "";
        countTxt.font = _font;
        countTxt.fontSize = 18;
        countTxt.color = Color.white;
        countTxt.alignment = TextAnchor.MiddleCenter;
        countTxt.raycastTarget = false;

        // 名字（简短）
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(go.transform, false);
        var nameRect = nameGo.AddComponent<RectTransform>();
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.28f);
        nameRect.pivot = new Vector2(0.5f, 0.5f);
        nameRect.sizeDelta = new Vector2(85, 20);
        var nameTxt = nameGo.AddComponent<Text>();
        nameTxt.text = cfg?.fighterName ?? $"#{fighterId}";
        nameTxt.font = _font;
        nameTxt.fontSize = 14;
        nameTxt.color = new Color(1f, 0.8f, 0.8f);
        nameTxt.alignment = TextAnchor.MiddleCenter;
        nameTxt.raycastTarget = false;

        // 点击显示 tooltip
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        int capturedId = fighterId;
        btn.onClick.AddListener(() => ShowTooltipForFighter(capturedId));
    }

    // ── 我方兵种 ──

    private void RefreshAllies()
    {
        ClearChildren(_allyContainer);

        var dataManager = GameManager.Instance?.DataManager;
        if (dataManager == null) return;
        var tribes = dataManager.GetTribes();
        if (tribes == null) return;

        // 表头
        float[] colX = { -550, -440, -300, -100 };
        string[] headers = { "头像", "名字", "HP", "" };
        var headerBg = MakeImageIn(_allyContainer, "HeaderBg", new Color(0.15f, 0.15f, 0.25f, 0.8f));
        headerBg.rectTransform.anchorMin = headerBg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        headerBg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        headerBg.rectTransform.anchoredPosition = new Vector2(0, 115);
        headerBg.rectTransform.sizeDelta = new Vector2(1300, 30);
        for (int i = 0; i < headers.Length; i++)
        {
            var h = MakeTextIn(_allyContainer, $"H{i}", headers[i], 16, new Color(0.7f, 0.8f, 1f));
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            h.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            h.rectTransform.anchoredPosition = new Vector2(colX[i], 115);
            h.rectTransform.sizeDelta = new Vector2(150, 28);
        }

        // 收集所有单位
        var allUnits = new List<FighterData>();
        foreach (var tribe in tribes)
        {
            if (tribe?.units == null) continue;
            allUnits.AddRange(tribe.units);
        }

        float rowH = 50f;
        float startY = 80f;

        for (int row = 0; row < allUnits.Count; row++)
        {
            var unit = allUnits[row];
            var cfg = TribeConfigLoader.Instance?.GetFighterConfig(unit.fighterId);
            float y = startY - row * rowH;

            // 行背景
            Color rowColor = row % 2 == 0
                ? new Color(0.1f, 0.1f, 0.15f, 0.5f)
                : new Color(0.13f, 0.13f, 0.18f, 0.5f);
            var rowBg = MakeImageIn(_allyContainer, $"Row{row}", rowColor);
            rowBg.rectTransform.anchorMin = rowBg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rowBg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rowBg.rectTransform.anchoredPosition = new Vector2(0, y);
            rowBg.rectTransform.sizeDelta = new Vector2(1300, rowH - 2);

            // 头像
            var avatarGo = new GameObject($"Avatar{row}");
            avatarGo.transform.SetParent(_allyContainer, false);
            var avatarRect = avatarGo.AddComponent<RectTransform>();
            avatarRect.anchorMin = avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
            avatarRect.pivot = new Vector2(0.5f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(colX[0], y);
            avatarRect.sizeDelta = new Vector2(40, 40);
            var avatarImg = avatarGo.AddComponent<Image>();
            avatarImg.preserveAspect = true;
            if (cfg != null && !string.IsNullOrEmpty(cfg.avatarId))
            {
                var sprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>($"avatartemp/{cfg.avatarId}1");
                if (sprite != null) avatarImg.sprite = sprite;
            }
            avatarImg.raycastTarget = false;

            // 名字
            string displayName = unit.name ?? cfg?.fighterName ?? "???";
            if (unit.IsEnhanced()) displayName += "+";
            var nameTxt = MakeTextIn(_allyContainer, $"Name{row}", displayName, 18, Color.white);
            nameTxt.rectTransform.anchorMin = nameTxt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            nameTxt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            nameTxt.rectTransform.anchoredPosition = new Vector2(colX[1], y);
            nameTxt.rectTransform.sizeDelta = new Vector2(130, 30);
            nameTxt.raycastTarget = false;

            // HP 条
            int maxHp = GetEffectiveMaxHp(unit, cfg);
            int curHp = Mathf.RoundToInt(unit.currentHp);
            float hpPct = maxHp > 0 ? Mathf.Min(1f, (float)curHp / maxHp) : 0f;
            CreateHpBar(_allyContainer, colX[2], y, 200, 25, hpPct, curHp, maxHp, row);

            // 点击行显示 tooltip
            var clickArea = new GameObject($"Click{row}");
            clickArea.transform.SetParent(_allyContainer, false);
            var clickRect = clickArea.AddComponent<RectTransform>();
            clickRect.anchorMin = clickRect.anchorMax = new Vector2(0.5f, 0.5f);
            clickRect.pivot = new Vector2(0.5f, 0.5f);
            clickRect.anchoredPosition = new Vector2(0, y);
            clickRect.sizeDelta = new Vector2(1300, rowH);
            var clickImg = clickArea.AddComponent<Image>();
            clickImg.color = new Color(0, 0, 0, 0);
            var clickBtn = clickArea.AddComponent<Button>();
            clickBtn.targetGraphic = clickImg;
            int capturedId = unit.fighterId;
            clickBtn.onClick.AddListener(() => ShowTooltipForFighter(capturedId));
        }
    }

    private void CreateHpBar(RectTransform parent, float x, float y, float w, float h, float pct, int curHp, int maxHp, int row)
    {
        // 背景
        var bgGo = new GameObject($"HpBg{row}");
        bgGo.transform.SetParent(parent, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = new Vector2(x, y);
        bgRect.sizeDelta = new Vector2(w, h);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // 填充
        var fillGo = new GameObject($"HpFill{row}");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(pct, 1f);
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = pct > 0.5f ? new Color(0.2f, 0.8f, 0.2f) :
                        pct > 0.25f ? new Color(1f, 0.7f, 0.2f) : new Color(1f, 0.3f, 0.3f);
        fillImg.raycastTarget = false;

        // 文字
        var txtGo = new GameObject($"HpTxt{row}");
        txtGo.transform.SetParent(bgGo.transform, false);
        var txtRect = txtGo.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.text = $"{curHp}/{maxHp}";
        txt.font = _font;
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
    }

    private int GetEffectiveMaxHp(FighterData unit, FighterConfig cfg)
    {
        int baseHp = cfg != null ? cfg.hp : 100;
        return unit.IsEnhanced() ? Mathf.RoundToInt(baseHp * 1.5f) : baseHp;
    }

    // ══════════════════════════════════════
    // Tooltip
    // ══════════════════════════════════════

    private void BuildTooltip()
    {
        _tooltip = new GameObject("Tooltip");
        _tooltip.transform.SetParent(transform, false);
        var ttRect = _tooltip.AddComponent<RectTransform>();
        ttRect.anchorMin = ttRect.anchorMax = new Vector2(0.5f, 0.5f);
        ttRect.pivot = new Vector2(0.5f, 0.5f);
        ttRect.sizeDelta = new Vector2(360, 300);
        ttRect.anchoredPosition = new Vector2(300, 50);
        var ttBg = _tooltip.AddComponent<Image>();
        ttBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // 边框
        var outline = _tooltip.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.5f, 0.7f, 0.8f);
        outline.effectDistance = new Vector2(2, -2);

        // 头像
        var avGo = new GameObject("TtAvatar");
        avGo.transform.SetParent(_tooltip.transform, false);
        var avRect = avGo.AddComponent<RectTransform>();
        avRect.anchorMin = avRect.anchorMax = new Vector2(0.5f, 1f);
        avRect.pivot = new Vector2(0.5f, 1f);
        avRect.anchoredPosition = new Vector2(0, -10);
        avRect.sizeDelta = new Vector2(80, 80);
        _tooltipAvatar = avGo.AddComponent<Image>();
        _tooltipAvatar.preserveAspect = true;

        // 名字
        var nameGo = new GameObject("TtName");
        nameGo.transform.SetParent(_tooltip.transform, false);
        var nameRect = nameGo.AddComponent<RectTransform>();
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0, -100);
        nameRect.sizeDelta = new Vector2(320, 30);
        _tooltipName = nameGo.AddComponent<Text>();
        _tooltipName.font = _font;
        _tooltipName.fontSize = 22;
        _tooltipName.color = new Color(1f, 0.85f, 0.3f);
        _tooltipName.alignment = TextAnchor.MiddleCenter;
        _tooltipName.raycastTarget = false;

        // 详情
        var detailGo = new GameObject("TtDetail");
        detailGo.transform.SetParent(_tooltip.transform, false);
        var detailRect = detailGo.AddComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0, 0);
        detailRect.anchorMax = new Vector2(1, 1f);
        detailRect.offsetMin = new Vector2(20, 10);
        detailRect.offsetMax = new Vector2(-20, -140);
        _tooltipDetail = detailGo.AddComponent<Text>();
        _tooltipDetail.font = _font;
        _tooltipDetail.fontSize = 18;
        _tooltipDetail.color = new Color(0.85f, 0.85f, 0.9f);
        _tooltipDetail.alignment = TextAnchor.UpperLeft;
        _tooltipDetail.verticalOverflow = VerticalWrapMode.Overflow;
        _tooltipDetail.raycastTarget = false;

        // 关闭按钮
        var closeBtn = new GameObject("TtClose");
        closeBtn.transform.SetParent(_tooltip.transform, false);
        var closeRect = closeBtn.AddComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-5, -5);
        closeRect.sizeDelta = new Vector2(30, 30);
        var closeImg = closeBtn.AddComponent<Image>();
        closeImg.color = new Color(0.8f, 0.3f, 0.3f, 0.8f);
        var closeTxtGo = new GameObject("X");
        closeTxtGo.transform.SetParent(closeBtn.transform, false);
        var closeTxtRect = closeTxtGo.AddComponent<RectTransform>();
        closeTxtRect.anchorMin = closeTxtRect.anchorMax = Vector2.one * 0.5f;
        closeTxtRect.pivot = new Vector2(0.5f, 0.5f);
        closeTxtRect.sizeDelta = new Vector2(30, 30);
        var closeTxt = closeTxtGo.AddComponent<Text>();
        closeTxt.text = "X";
        closeTxt.font = _font;
        closeTxt.fontSize = 18;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.raycastTarget = false;
        var closeBtnComp = closeBtn.AddComponent<Button>();
        closeBtnComp.targetGraphic = closeImg;
        closeBtnComp.onClick.AddListener(HideTooltip);

        _tooltip.SetActive(false);
    }

    private void ShowTooltipForFighter(int fighterId)
    {
        var cfg = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
        if (cfg == null) return;

        // 头像
        if (!string.IsNullOrEmpty(cfg.avatarId))
        {
            var sprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>($"avatartemp/{cfg.avatarId}1");
            if (sprite != null) _tooltipAvatar.sprite = sprite;
        }

        // 名字
        _tooltipName.text = cfg.fighterName;

        // 属性详情
        string rangeType = cfg.attackRange > 2f ? "远程" : "近战";
        string passive = GetPassiveSkillName(cfg);

        _tooltipDetail.text =
            $"攻击力: {cfg.attack}\n" +
            $"HP: {cfg.hp}\n" +
            $"防御: {cfg.defense}\n" +
            $"攻击类型: {rangeType}\n" +
            $"被动技能: {passive}";

        _tooltip.SetActive(true);
    }

    private string GetPassiveSkillName(FighterConfig cfg)
    {
        if (cfg.innateBuffIds == null || cfg.innateBuffIds.Count == 0)
            return "无";
        var names = new List<string>();
        foreach (int buffId in cfg.innateBuffIds)
        {
            var buffCfg = TribeConfigLoader.Instance?.GetBuffConfig(buffId);
            names.Add(buffCfg != null ? buffCfg.buffName : $"#{buffId}");
        }
        return string.Join(", ", names);
    }

    private void HideTooltip()
    {
        if (_tooltip != null)
            _tooltip.SetActive(false);
    }

    // ══════════════════════════════════════
    // UI 工具
    // ══════════════════════════════════════

    private void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private Text MakeText(string name, string text, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 40);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = _font;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;
    }

    private Text MakeTextIn(RectTransform parent, string name, string text, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 30);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = _font;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return txt;
    }

    private Image MakeImageIn(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private RectTransform MakeButton(string name, string text, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 55);
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
        txt.font = _font;
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        return rect;
    }

    // ══════════════════════════════════════
    // 按钮回调
    // ══════════════════════════════════════

    private void OnStartBattle()
    {
        HideTooltip();
        GameLogger.Log("TribeBuild", "StartBattle → BattlePreparePanel");
        Hide();
        UIManager uiManager = GameManager.Instance?.UIManager;
        var panel = uiManager?.ShowPanel<BattlePreparePanel>(UIManager.UILayer.Normal);

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
        HideTooltip();
        GameLogger.Log("TribeBuild", "Close");
        Hide();
        GameFlowController.Instance.EnterMapSelection();
    }
}
