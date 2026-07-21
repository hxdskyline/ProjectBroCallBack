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
        public int fogViewDistance = 3; // 迷雾可视距离（可从 map_config.json 配置）

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

            // 更新迷雾：当前层+3以内的可见，超过的设为 Fogged
            UpdateFog(visited.layer);
        }

        /// <summary>
        /// 更新迷雾状态：当前层+fogViewDistance以内可见，超过的设为 Fogged
        /// 第12层后全部解锁
        /// </summary>
        public void UpdateFog(int currentLayer)
        {
            int fogThreshold = currentLayer + fogViewDistance;
            bool unlockAll = currentLayer >= 12;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null) continue;
                if (node.state == MapNodeState.Visited || node.state == MapNodeState.Available)
                    continue; // 已访问或可选的节点不受迷雾影响

                if (unlockAll || node.layer <= fogThreshold)
                {
                    // 可见：如果之前是 Fogged，恢复为 Locked
                    if (node.state == MapNodeState.Fogged)
                        node.state = MapNodeState.Locked;
                }
                else
                {
                    // 超过3层：设为 Fogged
                    node.state = MapNodeState.Fogged;
                }
            }
        }
    }
}
