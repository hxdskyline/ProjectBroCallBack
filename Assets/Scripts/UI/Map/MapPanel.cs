using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Camp;

/// <summary>
/// 地图面板 — 显示分支路径地图，支持鼠标拖拽滚动，处理节点选择
/// </summary>
public class MapPanel : UIPanel
{
    private MapData _mapData;
    private Action<int, MapNodeType> _onNodeSelected;
    private RectTransform _content;
    private ScrollRect _scrollRect;

    /// <summary>
    /// 显示地图
    /// </summary>
    public void ShowMap(MapData mapData, int currentNodeId, Action<int, MapNodeType> onNodeSelected)
    {
        _mapData = mapData;
        _onNodeSelected = onNodeSelected;

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

        // 计算 content 尺寸：找到节点实际占据的边界，四周留 40px
        const float padding = 40f;
        const float nodeW = 160f;
        const float nodeH = 100f;

        float minX = float.MaxValue, maxX = float.MinValue;
        float maxY = 0;

        foreach (var n in mapData.nodes)
        {
            if (n.x - nodeW / 2f < minX) minX = n.x - nodeW / 2f;
            if (n.x + nodeW / 2f > maxX) maxX = n.x + nodeW / 2f;
            if (n.y + nodeH / 2f > maxY) maxY = n.y + nodeH / 2f;
        }

        // Content 左上对齐：节点原样定位，content 尺寸包裹所有节点 + padding
        float contentW = (maxX - minX) + padding * 2f;
        float contentH = maxY + nodeH / 2f + padding * 2f;

        _content.sizeDelta = new Vector2(contentW, contentH);

        // 绘制节点到 content 中（加上 padding 偏移和 minX 修正）
        for (int i = 0; i < mapData.nodes.Count; i++)
        {
            CreateNodeButton(mapData.nodes[i], padding, minX);
        }

        // 自动滚动到起始节点（第一个 Available 节点）
        ScrollToFirstAvailableNode(mapData, currentNodeId, padding, minX);
    }

    private void CreateScrollView()
    {
        // 背景层（独立于 Mask 之外）
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        bgGo.transform.SetAsFirstSibling(); // 确保在最底层
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

        // Viewport（遮罩层，不显示自身 graphic）
        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(transform, false);
        var viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0, 0.5f);
        viewportRect.anchorMax = new Vector2(1, 0.5f);
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.sizeDelta = new Vector2(0, 900);
        viewportRect.anchoredPosition = new Vector2(0, -472.54f);
        viewportGo.AddComponent<Image>().color = Color.clear;

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

    private void CreateNodeButton(MapNode node, float padding, float minX)
    {
        string label = GetNodeLabel(node);
        Color color = GetNodeColor(node);

        GameObject go = new GameObject($"Node_{node.id}");
        go.transform.SetParent(_content, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(160, 100);
        // 左对齐：x 减去 minX 使最左节点从 0 开始，再加 padding
        // y 翻转（地图 y 向下增长，UI y 向下为正）
        float x = node.x - minX + padding;
        float y = -node.y + padding;
        rect.anchoredPosition = new Vector2(x, y);

        var image = go.AddComponent<Image>();
        image.color = color;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.interactable = (node.state == MapNodeState.Available);
        btn.onClick.AddListener(() => OnNodeClicked(node));

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
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
    }

    private void ScrollToFirstAvailableNode(MapData mapData, int currentNodeId, float padding, float minX)
    {
        // 找到当前节点或第一个可用节点
        MapNode targetNode = null;
        if (currentNodeId >= 0)
            targetNode = mapData.GetNode(currentNodeId);
        if (targetNode == null)
        {
            foreach (var n in mapData.nodes)
            {
                if (n.state == MapNodeState.Available) { targetNode = n; break; }
            }
        }
        if (targetNode == null) return;

        float contentWidth = _content.sizeDelta.x;
        float viewportWidth = ((RectTransform)_scrollRect.viewport).rect.width;
        float scrollableWidth = contentWidth - viewportWidth;

        if (scrollableWidth <= 0) return;

        // 节点在 content 中的实际 x（与 CreateNodeButton 一致）
        float nodeActualX = targetNode.x - minX + padding;
        float targetScrollX = nodeActualX - viewportWidth / 2f;
        float normalizedPos = Mathf.Clamp01(targetScrollX / scrollableWidth);

        _scrollRect.horizontalNormalizedPosition = normalizedPos;
    }

    private void OnNodeClicked(MapNode node)
    {
        GameLogger.Log("MapP", $"Node={node.id} type={node.nodeType}");
        if (node.state != MapNodeState.Available) return;
        _onNodeSelected?.Invoke(node.id, node.nodeType);
    }

    private string GetNodeLabel(MapNode node)
    {
        switch (node.nodeType)
        {
            case MapNodeType.Battle: return "战";
            case MapNodeType.EliteBattle: return "精";
            case MapNodeType.Shop: return "商";
            case MapNodeType.Event: return "事";
            case MapNodeType.HotSpring: return "泉";
            case MapNodeType.Boss: return "BOSS";
            default: return "?";
        }
    }

    private Color GetNodeColor(MapNode node)
    {
        switch (node.state)
        {
            case MapNodeState.Visited: return new Color(0.3f, 0.3f, 0.3f);
            case MapNodeState.Available: return new Color(1f, 0.9f, 0.2f);
            case MapNodeState.Locked: return new Color(0.2f, 0.2f, 0.2f, 0.5f);
            default: return Color.gray;
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
