using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Camp;

/// <summary>
/// 战斗结算面板 — 显示战斗结果、奖励和我方战斗统计
/// </summary>
public class BattleResultPanel : UIPanel
{
    private System.Action _onClosed;
    private Font _font;

    public override void Initialize()
    {
        base.Initialize();

        // 半透明深色背景
        var bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.8f);
    }

    public void Setup(bool victory, int battleNumber, int expReward, int catFoodReward,
        List<FighterBattleStats> battleStats, System.Action onClosed)
    {
        _onClosed = onClosed;
        _font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");

        // 清除旧子物体
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        // ── 标题 ──
        string title = victory ? "战斗胜利" : "战斗失败";
        Color titleColor = victory ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f);
        var titleGo = MakeText("Title", title, 38, titleColor);
        titleGo.rectTransform.anchoredPosition = new Vector2(0, 350);
        titleGo.rectTransform.sizeDelta = new Vector2(800, 50);

        // ── 关卡 ──
        var levelGo = MakeText("Level", $"第 {battleNumber} 关", 24, new Color(0.8f, 0.8f, 0.8f));
        levelGo.rectTransform.anchoredPosition = new Vector2(0, 305);
        levelGo.rectTransform.sizeDelta = new Vector2(400, 35);

        // ── 奖励 ──
        if (victory)
        {
            var expGo = MakeText("ExpReward", $"经验 +{expReward}", 22, new Color(0.4f, 1f, 0.4f));
            expGo.rectTransform.anchoredPosition = new Vector2(0, 270);
            expGo.rectTransform.sizeDelta = new Vector2(400, 30);

            var catFoodGo = MakeText("CatFoodReward", $"猫粮 +{catFoodReward}", 22, new Color(1f, 0.8f, 0.3f));
            catFoodGo.rectTransform.anchoredPosition = new Vector2(0, 240);
            catFoodGo.rectTransform.sizeDelta = new Vector2(400, 30);
        }

        // ── 战斗统计表 ──
        BuildStatsTable(battleStats);

        // ── 确认按钮 ──
        var confirmBtn = MakeButton("ConfirmButton", "确认", OnConfirm);
        confirmBtn.anchoredPosition = new Vector2(0, -400);
    }

    // ────────────────────────────────────────
    // 战斗统计表
    // ────────────────────────────────────────

    private void BuildStatsTable(List<FighterBattleStats> stats)
    {
        // 计算全队总量
        int totalDmg = 0, totalTaken = 0, totalHeal = 0;
        if (stats != null)
        {
            foreach (var s in stats)
            {
                totalDmg += s.totalDamageDealt;
                totalTaken += s.totalDamageTaken;
                totalHeal += s.totalHealingDone;
            }
        }

        // 表头背景
        var headerBg = MakeImage("HeaderBg", new Color(0.15f, 0.15f, 0.2f, 0.9f));
        headerBg.rectTransform.anchoredPosition = new Vector2(0, 195);
        headerBg.rectTransform.sizeDelta = new Vector2(900, 36);

        // 表头
        float[] colX = { -350, -230, -80, 30, 120, 230, 340 };
        string[] headers = { "头像", "名字", "输出", "占比", "承伤", "占比", "治疗" };
        for (int i = 0; i < headers.Length; i++)
        {
            var h = MakeText($"H{i}", headers[i], 16, new Color(0.7f, 0.8f, 1f));
            h.rectTransform.anchoredPosition = new Vector2(colX[i], 195);
            h.rectTransform.sizeDelta = new Vector2(110, 30);
        }

        // 数据行（按输出降序排列）
        if (stats == null || stats.Count == 0) return;
        stats.Sort((a, b) => b.totalDamageDealt.CompareTo(a.totalDamageDealt));

        float rowH = 50f;
        float startY = 155f;

        for (int row = 0; row < stats.Count; row++)
        {
            var s = stats[row];
            float y = startY - row * rowH;

            // 行背景（交替色）
            Color rowColor = row % 2 == 0
                ? new Color(0.1f, 0.1f, 0.15f, 0.6f)
                : new Color(0.12f, 0.12f, 0.18f, 0.6f);
            var rowBg = MakeImage($"Row{row}", rowColor);
            rowBg.rectTransform.anchoredPosition = new Vector2(0, y);
            rowBg.rectTransform.sizeDelta = new Vector2(900, rowH - 2);

            // 头像
            var avatarGo = new GameObject($"Avatar{row}");
            avatarGo.transform.SetParent(transform, false);
            var avatarRect = avatarGo.AddComponent<RectTransform>();
            avatarRect.anchoredPosition = new Vector2(colX[0], y);
            avatarRect.sizeDelta = new Vector2(38, 38);
            var avatarSr = avatarGo.AddComponent<Image>();
            avatarSr.preserveAspect = true;
            if (!string.IsNullOrEmpty(s.avatarId))
            {
                var sprite = GameManager.Instance.ResourceManager
                    .LoadResource<Sprite>($"avatartemp/{s.avatarId}1");
                if (sprite != null) avatarSr.sprite = sprite;
            }

            // 名字
            MakeCell($"Name{row}", s.name, colX[1], y, 16, Color.white);

            // 输出
            MakeCell($"Dmg{row}", FormatNum(s.totalDamageDealt), colX[2], y, 16, new Color(1f, 0.7f, 0.3f));

            // 输出占比
            string dmgPct = totalDmg > 0 ? $"{(float)s.totalDamageDealt / totalDmg * 100:F1}%" : "0%";
            MakeCell($"DmgPct{row}", dmgPct, colX[3], y, 14, new Color(1f, 0.7f, 0.3f));

            // 承伤
            MakeCell($"Taken{row}", FormatNum(s.totalDamageTaken), colX[4], y, 16, new Color(1f, 0.4f, 0.4f));

            // 承伤占比
            string takenPct = totalTaken > 0 ? $"{(float)s.totalDamageTaken / totalTaken * 100:F1}%" : "0%";
            MakeCell($"TakenPct{row}", takenPct, colX[5], y, 14, new Color(1f, 0.4f, 0.4f));

            // 治疗
            MakeCell($"Heal{row}", FormatNum(s.totalHealingDone), colX[6], y, 16, new Color(0.3f, 1f, 0.5f));
        }
    }

    // ────────────────────────────────────────
    // UI 工具
    // ────────────────────────────────────────

    private void MakeCell(string name, string text, float x, float y, int fontSize, Color color)
    {
        var txt = MakeText(name, text, fontSize, color);
        txt.rectTransform.anchoredPosition = new Vector2(x, y);
        txt.rectTransform.sizeDelta = new Vector2(110, 30);
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

    private Image MakeImage(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
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

    private static string FormatNum(int n)
    {
        if (n >= 10000) return $"{n / 1000f:F1}万";
        return n.ToString();
    }

    private void OnConfirm()
    {
        GameLogger.Log("BattleResult", "确认关闭");
        GameManager.Instance?.UIManager?.ClosePanel("BattleResultPanel");
        _onClosed?.Invoke();
    }
}
