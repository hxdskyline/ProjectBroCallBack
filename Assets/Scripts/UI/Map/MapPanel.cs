using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Camp;
using Combat;

/// <summary>
/// 地图面板 — v2.0 连线绘制 + 按类型着色 + 状态视觉 + 呼吸动画
/// </summary>
public class MapPanel : UIPanel
{
    private MapData _mapData;
    private Action<int, MapNodeType> _onNodeSelected;
    private RectTransform _content;
    private ScrollRect _scrollRect;

    private float _padding = 40f;
    private float _minX;
    private float _minY;

    private const float NodeW = 400f;
    private const float NodeH = 100f;

    private readonly List<RectTransform> _availableNodes = new List<RectTransform>();

    private void Update()
    {
        // 呼吸动画：Available 节点 PingPong 缩放
        float scale = 1f + Mathf.PingPong(Time.time * 1.5f, 0.15f);
        for (int i = _availableNodes.Count - 1; i >= 0; i--)
        {
            if (_availableNodes[i] == null)
            {
                _availableNodes.RemoveAt(i);
                continue;
            }
            _availableNodes[i].localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>
    /// 显示地图
    /// </summary>
    public void ShowMap(MapData mapData, int currentNodeId, Action<int, MapNodeType> onNodeSelected)
    {
        _mapData = mapData;
        _onNodeSelected = onNodeSelected;
        _availableNodes.Clear();

        // 清除旧内容
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        // 标题（固定在顶部，不随滚动）
        var title = CreateText("Title", "选择下一关", 32, Color.white);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1);
        title.rectTransform.pivot = new Vector2(0.5f, 1);
        title.rectTransform.anchoredPosition = new Vector2(0, -20);

        if (mapData?.nodes == null) return;

        // 创建 ScrollView 结构
        CreateScrollView();

        // 计算节点坐标边界
        float minX_node = float.MaxValue, maxX_node = float.MinValue;
        float minY_node = float.MaxValue, maxY_node = float.MinValue;

        foreach (var n in mapData.nodes)
        {
            if (n.x < minX_node) minX_node = n.x;
            if (n.x > maxX_node) maxX_node = n.x;
            if (n.y < minY_node) minY_node = n.y;
            if (n.y > maxY_node) maxY_node = n.y;
        }

        _minX = minX_node;
        _minY = minY_node;

        float contentW = (maxX_node - minX_node) + NodeW + _padding * 2f;
        float contentH = (maxY_node - minY_node) + NodeH + _padding * 2f;

        _content.sizeDelta = new Vector2(contentW, contentH);

        // Phase 1: 画连线（在节点下层）
        DrawEdges(mapData);

        // Phase 2: 画节点（覆盖连线）
        for (int i = 0; i < mapData.nodes.Count; i++)
            CreateNodeButton(mapData.nodes[i]);

        // 自动滚动到起始节点
        ScrollToFirstAvailableNode(mapData, currentNodeId);
    }

    // ====== Coordinate Conversion ======

    /// <summary>
    /// 将地图节点坐标转换为 content 内的 UI 锚点位置（左上角锚点）
    /// </summary>
    private Vector2 NodeToUIPos(MapNode node)
    {
        // x: 从左到右递增，加 nodeW/2 确保节点不超出 content 左边
        // y: 从上到下递增（index 大 → y 大 → UI 位置更下方），加 nodeH/2 同理
        float x = node.x - _minX + _padding + NodeW / 2f;
        float y = node.y - _minY + _padding + NodeH / 2f;
        return new Vector2(x, y);
    }

    // ====== Edge Drawing ======

    private void DrawEdges(MapData mapData)
    {
        var positions = new Dictionary<int, Vector2>();
        foreach (var n in mapData.nodes)
            positions[n.id] = NodeToUIPos(n);

        foreach (var n in mapData.nodes)
        {
            Vector2 fromPos = positions[n.id];
            foreach (var nextId in n.nextNodeIds)
            {
                if (!positions.ContainsKey(nextId)) continue;
                Vector2 toPos = positions[nextId];

                var nextNode = mapData.GetNode(nextId);
                Color lineColor = GetEdgeColor(n, nextNode);
                DrawLine($"Edge_{n.id}_{nextId}", fromPos, toPos, lineColor);
            }
        }
    }

    private void DrawLine(string name, Vector2 from, Vector2 to, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_content, false);

        var rect = go.AddComponent<RectTransform>();
        Vector2 delta = to - from;
        float length = delta.magnitude;

        // 从节点边缘起止：沿方向各缩进半个节点宽度
        Vector2 dir = delta.normalized;
        Vector2 edgeFrom = from + dir * (NodeW / 2f);
        Vector2 edgeTo = to - dir * (NodeW / 2f);
        Vector2 edgeDelta = edgeTo - edgeFrom;
        float edgeLength = edgeDelta.magnitude;

        if (edgeLength <= 0) { Destroy(go); return; }

        // Z 旋转取反修正
        float angle = -Mathf.Atan2(-edgeDelta.y, edgeDelta.x) * Mathf.Rad2Deg;

        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(edgeLength, 3f);

        // 中点定位
        rect.anchoredPosition = new Vector2((edgeFrom.x + edgeTo.x) / 2f, (edgeFrom.y + edgeTo.y) / 2f);
        rect.localEulerAngles = new Vector3(0, 0, angle);

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private Color GetEdgeColor(MapNode fromNode, MapNode toNode)
    {
        if (fromNode.state == MapNodeState.Visited && toNode.state == MapNodeState.Visited)
            return new Color(0.4f, 0.4f, 0.4f, 0.6f);

        if (fromNode.state == MapNodeState.Visited && toNode.state == MapNodeState.Available)
            return new Color(1f, 0.9f, 0.3f, 0.9f);

        return new Color(0.3f, 0.3f, 0.35f, 0.4f);
    }

    // ====== Node Creation ======

    private void CreateNodeButton(MapNode node)
    {
        string label = GetNodeLabel(node);
        Color bgColor = GetNodeBackgroundColor(node);

        GameObject go = new GameObject($"Node_{node.id}");
        go.transform.SetParent(_content, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(NodeW, NodeH);
        Vector2 uiPos = NodeToUIPos(node);
        // anchoredPosition: anchor 在 (0,1) 即左上角，y 正方向向下
        // NodeToUIPos 已产出 UI 坐标（y 小=上，y 大=下），直接使用
        rect.anchoredPosition = uiPos;

        var image = go.AddComponent<Image>();
        image.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.interactable = (node.state == MapNodeState.Available);
        btn.onClick.AddListener(() => OnNodeClicked(node));

        // 类型色条（顶部小条）
        var stripGo = new GameObject("TypeStrip");
        stripGo.transform.SetParent(go.transform, false);
        var stripRect = stripGo.AddComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0, 1);
        stripRect.anchorMax = new Vector2(1, 1);
        stripRect.pivot = new Vector2(0.5f, 1);
        stripRect.sizeDelta = new Vector2(0, 6);
        stripRect.anchoredPosition = Vector2.zero;
        var stripImg = stripGo.AddComponent<Image>();
        stripImg.color = GetTypeColor(node.nodeType);
        stripImg.raycastTarget = false;

        // 战斗类节点：显示敌方单位头像
        if (node.nodeType == MapNodeType.Battle || node.nodeType == MapNodeType.EliteBattle || node.nodeType == MapNodeType.Boss)
        {
            CreateEnemyIcons(go, node);
        }

        // 文字
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var txt = textGo.AddComponent<Text>();
        txt.text = label;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = 28;
        txt.color = node.state == MapNodeState.Visited ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        // 已访问节点：加勾号
        if (node.state == MapNodeState.Visited)
        {
            var checkGo = new GameObject("Check");
            checkGo.transform.SetParent(go.transform, false);
            var checkRect = checkGo.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(1, 1);
            checkRect.anchorMax = new Vector2(1, 1);
            checkRect.pivot = new Vector2(1, 1);
            checkRect.sizeDelta = new Vector2(30, 30);
            checkRect.anchoredPosition = new Vector2(-4, -4);
            var checkTxt = checkGo.AddComponent<Text>();
            checkTxt.text = "✓";
            checkTxt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
            checkTxt.fontSize = 22;
            checkTxt.color = new Color(0.5f, 1f, 0.5f);
            checkTxt.alignment = TextAnchor.MiddleCenter;
            checkTxt.raycastTarget = false;
        }

        // 可用节点：加入呼吸动画列表
        if (node.state == MapNodeState.Available)
            _availableNodes.Add(rect);
    }

    /// <summary>
    /// 在战斗类节点上显示敌方单位头像
    /// </summary>
    private void CreateEnemyIcons(GameObject nodeGo, MapNode node)
    {
        // 情报系统：根据街头情报等级决定显示内容
        int intelLevel = GameManager.Instance?.DataManager?.GetStreetIntel() ?? 0;
        if (intelLevel <= 0)
        {
            // Lv.0：不显示任何敌方信息
            return;
        }

        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        if (campaign == null) return;

        int[] enemyIds = node.enemyUnitIds ?? campaign.GetEnemyUnitIdsForBattle(node.battleNumber);
        if (enemyIds == null || enemyIds.Length == 0) return;

        // 去重，取最多5个
        var uniqueIds = new HashSet<int>();
        var displayIds = new List<int>();
        foreach (int id in enemyIds)
        {
            if (uniqueIds.Add(id) && displayIds.Count < 5)
                displayIds.Add(id);
        }

        if (displayIds.Count == 0) return;

        // Lv.1：模糊描述（只显示数量，不显示头像）
        if (intelLevel <= 1)
        {
            var hintGo = new GameObject("EnemyHint");
            hintGo.transform.SetParent(nodeGo.transform, false);
            var hintRect = hintGo.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0, 0);
            hintRect.anchorMax = new Vector2(1, 0.5f);
            hintRect.sizeDelta = Vector2.zero;
            var hintTxt = hintGo.AddComponent<Text>();
            string desc = enemyIds.Length <= 3 ? "少量敌人" : enemyIds.Length <= 6 ? "中等数量" : "大量敌人";
            hintTxt.text = desc;
            try { hintTxt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk"); } catch { }
            hintTxt.fontSize = 20;
            hintTxt.color = new Color(0.7f, 0.7f, 0.7f);
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.raycastTarget = false;
            return;
        }

        // Lv.2：大致范围（显示数量范围，不显示头像）
        if (intelLevel <= 2)
        {
            var hintGo = new GameObject("EnemyHint");
            hintGo.transform.SetParent(nodeGo.transform, false);
            var hintRect = hintGo.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0, 0);
            hintRect.anchorMax = new Vector2(1, 0.5f);
            hintRect.sizeDelta = Vector2.zero;
            var hintTxt = hintGo.AddComponent<Text>();
            int min = Mathf.Max(1, enemyIds.Length - 3);
            int max = enemyIds.Length + 3;
            hintTxt.text = $"敌人约 {min}-{max} 只";
            try { hintTxt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk"); } catch { }
            hintTxt.fontSize = 20;
            hintTxt.color = new Color(0.8f, 0.8f, 0.8f);
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.raycastTarget = false;
            return;
        }

        // Lv.3：精确数字 + 敌方头像（原逻辑继续）

        // 创建头像容器
        var iconsGo = new GameObject("EnemyIcons");
        iconsGo.transform.SetParent(nodeGo.transform, false);
        var iconsRect = iconsGo.AddComponent<RectTransform>();
        iconsRect.anchorMin = new Vector2(0, 0);
        iconsRect.anchorMax = new Vector2(1, 0.5f);
        iconsRect.sizeDelta = Vector2.zero;
        iconsRect.anchoredPosition = Vector2.zero;

        int count = displayIds.Count;
        float iconSize = 120f;
        float gap = 8f;
        float totalW = count * iconSize + (count - 1) * gap;
        float startX = -totalW / 2f + iconSize / 2f;

        for (int i = 0; i < count; i++)
        {
            var cfg = TribeConfigLoader.Instance?.GetFighterConfig(displayIds[i]);
            if (cfg == null) continue;

            var iconGo = new GameObject($"Icon_{displayIds[i]}");
            iconGo.transform.SetParent(iconsGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(startX + i * (iconSize + gap), 0);

            var iconImg = iconGo.AddComponent<Image>();
            string addr = $"avatartemp/{cfg.avatarId}1";
            var sprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>(addr);
            if (sprite != null)
            {
                iconImg.sprite = sprite;
                iconImg.SetNativeSize();
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            }
            else
            {
                iconImg.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            }
            iconImg.raycastTarget = false;
        }
    }

    // ====== ScrollView ======

    private void CreateScrollView()
    {
        // 背景层
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
        var bgSprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>("ui/sprite/common/greenbg");
        if (bgSprite != null)
        {
            bgImg.sprite = bgSprite;
            bgImg.SetNativeSize();
        }
        else
        {
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        }

        // Viewport
        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(transform, false);
        var viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0, 0.5f);
        viewportRect.anchorMax = new Vector2(1, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.sizeDelta = new Vector2(0, 900);
        viewportRect.anchoredPosition = new Vector2(0, -777f);
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = Color.clear;
        viewportImg.raycastTarget = false;

        // Content
        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        _content = contentGo.AddComponent<RectTransform>();
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = new Vector2(0, 1);
        _content.pivot = new Vector2(0, 1);

        // ScrollRect
        _scrollRect = viewportGo.AddComponent<ScrollRect>();
        _scrollRect.content = _content;
        _scrollRect.viewport = viewportRect;
        _scrollRect.horizontal = true;
        _scrollRect.vertical = false;
        _scrollRect.movementType = ScrollRect.MovementType.Elastic;
        _scrollRect.scrollSensitivity = 30f;
        _scrollRect.elasticity = 0.1f;
    }

    // ====== Auto Scroll ======

    private void ScrollToFirstAvailableNode(MapData mapData, int currentNodeId)
    {
        MapNode targetNode = null;
        if (currentNodeId >= 0)
        {
            targetNode = mapData.GetNode(currentNodeId);
            if (targetNode != null && targetNode.state != MapNodeState.Available)
                targetNode = null;
        }

        if (targetNode == null)
        {
            foreach (var n in mapData.nodes)
            {
                if (n.state == MapNodeState.Available)
                {
                    targetNode = n;
                    break;
                }
            }
        }
        if (targetNode == null) return;

        float contentWidth = _content.sizeDelta.x;
        float viewportWidth = ((RectTransform)_scrollRect.viewport).rect.width;
        float scrollableWidth = contentWidth - viewportWidth;

        if (scrollableWidth <= 0) return;

        float nodeActualX = targetNode.x - _minX + _padding + NodeW / 2f;
        float targetScrollX = nodeActualX - viewportWidth / 2f;
        float normalizedPos = Mathf.Clamp01(targetScrollX / scrollableWidth);

        _scrollRect.horizontalNormalizedPosition = normalizedPos;
    }

    // ====== Interaction ======

    private void OnNodeClicked(MapNode node)
    {
        GameLogger.Log("MapP", $"Node={node.id} type={node.nodeType}");
        if (node.state != MapNodeState.Available) return;
        _onNodeSelected?.Invoke(node.id, node.nodeType);
    }

    // ====== Visual Helpers ======

    private string GetNodeLabel(MapNode node)
    {
        switch (node.nodeType)
        {
            case MapNodeType.Battle: return "战斗";
            case MapNodeType.EliteBattle: return "精英";
            case MapNodeType.Shop: return "商店";
            case MapNodeType.Event: return "事件";
            case MapNodeType.HotSpring: return "温泉";
            case MapNodeType.Boss: return "BOSS";
            case MapNodeType.Fate: return "命运";
            default: return "?";
        }
    }

    private Color GetNodeBackgroundColor(MapNode node)
    {
        switch (node.state)
        {
            case MapNodeState.Visited:
                return new Color(0.25f, 0.25f, 0.25f, 0.9f);
            case MapNodeState.Available:
                return new Color(0.4f, 0.4f, 0.45f, 0.95f);
            case MapNodeState.Locked:
                return new Color(0.15f, 0.15f, 0.18f, 0.6f);
            case MapNodeState.Fogged:
                return new Color(0.08f, 0.08f, 0.1f, 0.85f);
            default:
                return Color.gray;
        }
    }

    private Color GetTypeColor(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Battle: return new Color(0.6f, 0.6f, 0.6f);
            case MapNodeType.EliteBattle: return new Color(0.9f, 0.2f, 0.2f);
            case MapNodeType.Shop: return new Color(0.2f, 0.5f, 0.9f);
            case MapNodeType.Event: return new Color(0.9f, 0.6f, 0.15f);
            case MapNodeType.HotSpring: return new Color(0.2f, 0.8f, 0.4f);
            case MapNodeType.Boss: return new Color(0.7f, 0.1f, 0.15f);
            case MapNodeType.Fate: return new Color(0.6f, 0.2f, 0.8f);
            default: return Color.white;
        }
    }

    private Text CreateText(string name, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 60);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        return txt;
    }
}
