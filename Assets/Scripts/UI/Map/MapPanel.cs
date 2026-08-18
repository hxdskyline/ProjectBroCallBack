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

    private const float NodeW = 220f;
    private const float NodeH = 80f;

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

        // 显示红心（固定在顶部中间上方）
        CreateLivesDisplay();

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

        // Phase 3: 画迷雾遮罩（覆盖所有节点和连线）
        DrawFogOverlay(mapData);

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

        // 连线从源节点右侧中心点出发，到目标节点左侧中心点结束
        Vector2 edgeFrom = new Vector2(from.x + NodeW / 2f, from.y);
        Vector2 edgeTo = new Vector2(to.x - NodeW / 2f, to.y);
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

    // ====== Fog Overlay ======

    /// <summary>
    /// 绘制迷雾遮罩：从第一个迷雾节点所在列开始，覆盖到内容最右端
    /// </summary>
    private void DrawFogOverlay(MapData mapData)
    {
        // 找到第一个迷雾节点的最小 x 坐标（节点左边缘）
        float fogStartX = float.MaxValue;
        foreach (var n in mapData.nodes)
        {
            if (n.state == MapNodeState.Fogged)
            {
                float nodeCenterX = n.x - _minX + _padding + NodeW / 2f;
                float nodeLeftX = nodeCenterX - NodeW / 2f;
                if (nodeLeftX < fogStartX)
                    fogStartX = nodeLeftX;
            }
        }

        // 没有迷雾节点，不绘制
        if (fogStartX == float.MaxValue) return;

        // 向左偏移，为渐变边缘留出空间
        float edgeWidth = 80f;
        fogStartX -= 10f;

        float contentW = _content.sizeDelta.x;
        float fogWidth = contentW - fogStartX + edgeWidth;

        if (fogWidth <= 0) return;

        // 计算所有节点的 y 范围
        float minY_center = float.MaxValue, maxY_center = float.MinValue;
        foreach (var n in mapData.nodes)
        {
            Vector2 pos = NodeToUIPos(n);
            if (pos.y < minY_center) minY_center = pos.y;
            if (pos.y > maxY_center) maxY_center = pos.y;
        }
        float fogHeight = (maxY_center - minY_center) + NodeH + _padding * 2;
        float fogCenterY = (minY_center + maxY_center) / 2f;
        float fogCenterX = fogStartX + fogWidth / 2f;

        // 主迷雾遮罩：深灰色完全不透明
        var fogGo = new GameObject("FogOverlay");
        fogGo.transform.SetParent(_content, false);
        fogGo.transform.SetAsLastSibling();

        var fogRect = fogGo.AddComponent<RectTransform>();
        fogRect.anchorMin = new Vector2(0, 1);
        fogRect.anchorMax = new Vector2(0, 1);
        fogRect.pivot = new Vector2(0.5f, 0.5f);
        fogRect.sizeDelta = new Vector2(fogWidth, fogHeight);
        fogRect.anchoredPosition = new Vector2(fogCenterX, fogCenterY);

        var fogImg = fogGo.AddComponent<Image>();
        fogImg.color = new Color(0.12f, 0.12f, 0.15f, 1f);
        fogImg.raycastTarget = false;

        // 渐变边缘效果
        var edgeGo = new GameObject("FogEdge");
        edgeGo.transform.SetParent(_content, false);
        edgeGo.transform.SetAsLastSibling();

        var edgeRect = edgeGo.AddComponent<RectTransform>();
        edgeRect.anchorMin = new Vector2(0, 1);
        edgeRect.anchorMax = new Vector2(0, 1);
        edgeRect.pivot = new Vector2(0.5f, 0.5f);
        edgeRect.sizeDelta = new Vector2(edgeWidth, fogHeight);
        edgeRect.anchoredPosition = new Vector2(fogCenterX - fogWidth / 2f - edgeWidth / 2f, fogCenterY);

        var edgeImg = edgeGo.AddComponent<Image>();
        edgeImg.color = new Color(0.12f, 0.12f, 0.15f, 0.5f);
        edgeImg.raycastTarget = false;

        // 迷雾提示文字
        var hintGo = new GameObject("FogHint");
        hintGo.transform.SetParent(_content, false);
        hintGo.transform.SetAsLastSibling();

        var hintRect = hintGo.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0, 1);
        hintRect.anchorMax = new Vector2(0, 1);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.sizeDelta = new Vector2(200, 60);
        hintRect.anchoredPosition = new Vector2(fogCenterX - edgeWidth / 2f, fogCenterY);

        var hintTxt = hintGo.AddComponent<Text>();
        hintTxt.text = "迷雾封锁";
        hintTxt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
        hintTxt.fontSize = 28;
        hintTxt.color = new Color(0.5f, 0.5f, 0.55f, 0.9f);
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.raycastTarget = false;
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
        txt.fontSize = 22;
        txt.color = node.state == MapNodeState.Visited ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;

        // 已访问节点：加标记
        if (node.state == MapNodeState.Visited)
        {
            var checkGo = new GameObject("Check");
            checkGo.transform.SetParent(go.transform, false);
            var checkRect = checkGo.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(1, 1);
            checkRect.anchorMax = new Vector2(1, 1);
            checkRect.pivot = new Vector2(1, 1);
            checkRect.sizeDelta = new Vector2(24, 24);
            checkRect.anchoredPosition = new Vector2(-3, -3);
            var checkTxt = checkGo.AddComponent<Text>();
            checkTxt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk");
            checkTxt.fontSize = 18;
            checkTxt.alignment = TextAnchor.MiddleCenter;
            checkTxt.raycastTarget = false;

            // 判断是否为战斗关卡且失败
            bool isBattleNode = node.nodeType == MapNodeType.Battle ||
                               node.nodeType == MapNodeType.EliteBattle ||
                               node.nodeType == MapNodeType.Boss;
            bool isDefeat = isBattleNode && node.battleCompleted && !node.battleVictory;

            if (isDefeat)
            {
                // 战斗失败：显示红色X
                checkTxt.text = "✗";
                checkTxt.color = new Color(1f, 0.3f, 0.3f);
            }
            else
            {
                // 胜利或非战斗关卡：显示绿色勾
                checkTxt.text = "✓";
                checkTxt.color = new Color(0.5f, 1f, 0.5f);
            }
        }

        // 可用节点：加入呼吸动画列表
        if (node.state == MapNodeState.Available)
            _availableNodes.Add(rect);
    }

    /// <summary>
    /// 在战斗类节点上显示敌方单位信息
    /// </summary>
    private void CreateEnemyIcons(GameObject nodeGo, MapNode node)
    {
        var campaign = GameManager.Instance?.BattleCampaignRuntime;
        if (campaign == null) return;

        int[] enemyIds = node.enemyUnitIds;
        if (enemyIds == null || enemyIds.Length == 0)
        {
            enemyIds = campaign.GetEnemyUnitIdsForBattle(node.battleNumber);
        }
        if (enemyIds == null || enemyIds.Length == 0) return;

        // 统计每种敌人的数量
        var typeCounts = new Dictionary<int, int>();
        foreach (int id in enemyIds)
        {
            if (!typeCounts.ContainsKey(id)) typeCounts[id] = 0;
            typeCounts[id]++;
        }

        // 构建描述文本：如 "鼠辈×2 苍蝇猫×2"
        var parts = new List<string>();
        foreach (var kv in typeCounts)
        {
            var cfg = TribeConfigLoader.Instance?.GetFighterConfig(kv.Key);
            string name = cfg?.fighterName ?? $"ID{kv.Key}";
            parts.Add($"{name}×{kv.Value}");
        }
        string desc = string.Join(" ", parts);

        // 显示文本
        var hintGo = new GameObject("EnemyHint");
        hintGo.transform.SetParent(nodeGo.transform, false);
        var hintRect = hintGo.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0, 0);
        hintRect.anchorMax = new Vector2(1, 0.5f);
        hintRect.sizeDelta = Vector2.zero;
        var hintTxt = hintGo.AddComponent<Text>();
        hintTxt.text = desc;
        try { hintTxt.font = GameManager.Instance.ResourceManager.LoadResource<Font>("assets/bundle/font/fzy3k_gbk"); } catch { }
        hintTxt.fontSize = 14;
        hintTxt.color = new Color(0.85f, 0.85f, 0.85f);
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.raycastTarget = false;
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
            case MapNodeType.Wish: return "祈愿";
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
            case MapNodeType.Wish: return new Color(0.6f, 0.2f, 0.8f);
            default: return Color.white;
        }
    }

    /// <summary>
    /// 创建红心显示（固定在顶部中间上方）
    /// </summary>
    private void CreateLivesDisplay()
    {
        int livesRemaining = GameManager.Instance?.DataManager?.GetLivesRemaining() ?? 3;

        // 创建红心容器
        var livesGo = new GameObject("LivesDisplay");
        livesGo.transform.SetParent(transform, false);
        var livesRect = livesGo.AddComponent<RectTransform>();
        livesRect.anchorMin = new Vector2(0.5f, 1);
        livesRect.anchorMax = new Vector2(0.5f, 1);
        livesRect.pivot = new Vector2(0.5f, 1);
        livesRect.anchoredPosition = new Vector2(0, -70);
        livesRect.sizeDelta = new Vector2(300, 50);

        // 创建3颗红心
        float heartSize = 40f;
        float spacing = 10f;
        float startX = -((livesRemaining - 1) * (heartSize + spacing)) * 0.5f;

        for (int i = 0; i < 3; i++)
        {
            var heartGo = new GameObject($"Heart_{i}");
            heartGo.transform.SetParent(livesGo.transform, false);
            var heartRect = heartGo.AddComponent<RectTransform>();
            heartRect.anchorMin = new Vector2(0.5f, 0.5f);
            heartRect.anchorMax = new Vector2(0.5f, 0.5f);
            heartRect.pivot = new Vector2(0.5f, 0.5f);
            heartRect.anchoredPosition = new Vector2(startX + i * (heartSize + spacing), 0);
            heartRect.sizeDelta = new Vector2(heartSize, heartSize);

            var heartImg = heartGo.AddComponent<Image>();
            // 根据红心数量设置颜色：有红心为红色，无红心为灰色
            heartImg.color = i < livesRemaining ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);
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
