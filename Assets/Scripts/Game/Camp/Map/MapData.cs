using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 地图数据 — 一个地区的完整地图
    /// </summary>
    [Serializable]
    public class MapData
    {
        public List<MapNode> nodes = new List<MapNode>();

        /// <summary>
        /// 获取指定 id 的节点
        /// </summary>
        public MapNode GetNode(int nodeId)
        {
            if (nodes == null) return null;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].id == nodeId)
                    return nodes[i];
            }
            return null;
        }

        /// <summary>
        /// 获取所有可用节点
        /// </summary>
        public List<MapNode> GetAvailableNodes()
        {
            var result = new List<MapNode>();
            if (nodes == null) return result;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].state == MapNodeState.Available)
                    result.Add(nodes[i]);
            }
            return result;
        }

        /// <summary>
        /// 标记节点为已访问
        /// </summary>
        public void MarkNodeVisited(int nodeId)
        {
            var node = GetNode(nodeId);
            if (node != null)
                node.state = MapNodeState.Visited;
        }

        /// <summary>
        /// 根据已访问节点更新可用节点
        /// </summary>
        public void UpdateAvailableNodes(int visitedNodeId)
        {
            // 先将所有 Available 节点重置为 Locked（错过的就不能再选了）
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].state == MapNodeState.Available)
                    nodes[i].state = MapNodeState.Locked;
            }

            // 将已访问节点的后续节点设为 Available
            var visited = GetNode(visitedNodeId);
            if (visited == null || visited.nextNodeIds == null) return;

            for (int i = 0; i < visited.nextNodeIds.Count; i++)
            {
                var next = GetNode(visited.nextNodeIds[i]);
                if (next != null && next.state == MapNodeState.Locked)
                {
                    next.state = MapNodeState.Available;
                }
            }
        }
    }
}
