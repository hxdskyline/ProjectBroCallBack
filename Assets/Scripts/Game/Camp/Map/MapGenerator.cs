using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 地图生成器 — 生成类杀戮尖塔的分支路径地图
    /// 设计参考：正式文档/100_系统_选关.md
    /// </summary>
    public class MapGenerator
    {
        private int _layersPerRegion = 15;
        private int _minNodesPerLayer = 2;
        private int _maxNodesPerLayer = 4;
        private int[] _hotSpringLayers = { 5, 10 };
        private float _shopChance = 0.15f;
        private float _eventChance = 0.15f;

        private static readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 生成整局地图（3 个地区）
        /// </summary>
        public List<MapData> GenerateFullMap()
        {
            var maps = new List<MapData>();
            for (int region = 0; region < 3; region++)
            {
                maps.Add(GenerateRegion(region));
            }
            return maps;
        }

        /// <summary>
        /// 生成单个地区的地图
        /// </summary>
        private MapData GenerateRegion(int regionIndex)
        {
            var map = new MapData();
            int nodeIdCounter = 0;

            // 按层生成节点
            var layers = new List<List<MapNode>>();

            for (int col = 0; col < _layersPerRegion; col++)
            {
                var layer = new List<MapNode>();

                if (col == 0)
                {
                    // 起始层：单个节点
                    var node = CreateNode(nodeIdCounter++, col, 0, MapNodeType.Battle, 1 + regionIndex * 15);
                    layer.Add(node);
                }
                else if (col == _layersPerRegion - 1)
                {
                    // Boss 层：单个节点
                    int battleNum = (regionIndex + 1) * 15;
                    var node = CreateNode(nodeIdCounter++, col, 0, MapNodeType.Boss, battleNum);
                    layer.Add(node);
                }
                else
                {
                    // 中间层
                    int nodeCount = _rng.Next(_minNodesPerLayer, _maxNodesPerLayer + 1);
                    MapNodeType type = GetNodeTypeForLayer(col);
                    int battleNumBase = regionIndex * 15 + col + 1;

                    for (int r = 0; r < nodeCount; r++)
                    {
                        var node = CreateNode(nodeIdCounter++, col, r, type, battleNumBase);
                        layer.Add(node);
                    }
                }

                layers.Add(layer);
                map.nodes.AddRange(layer);
            }

            // 连接边
            ConnectLayers(layers);

            return map;
        }

        private MapNode CreateNode(int id, int col, int row, MapNodeType type, int battleNumber)
        {
            float spacingX = 200f;
            float spacingY = 120f;

            return new MapNode
            {
                id = id,
                column = col,
                row = row,
                nodeType = type,
                state = col == 0 ? MapNodeState.Available : MapNodeState.Locked,
                battleNumber = battleNumber,
                nextNodeIds = new List<int>(),
                prevNodeIds = new List<int>(),
                x = col * spacingX,
                y = row * spacingY
            };
        }

        private MapNodeType GetNodeTypeForLayer(int col)
        {
            // 温泉层
            foreach (int hs in _hotSpringLayers)
            {
                if (col == hs) return MapNodeType.HotSpring;
            }

            // 概率分配
            double roll = _rng.NextDouble();
            if (roll < _shopChance) return MapNodeType.Shop;
            if (roll < _shopChance + _eventChance) return MapNodeType.Event;

            // 精英层
            foreach (int hs in _hotSpringLayers)
            {
                if (col == hs - 1 || col == hs + 1) return MapNodeType.EliteBattle;
            }

            return MapNodeType.Battle;
        }

        /// <summary>
        /// 连接相邻层的节点
        /// </summary>
        private void ConnectLayers(List<List<MapNode>> layers)
        {
            for (int i = 0; i < layers.Count - 1; i++)
            {
                var current = layers[i];
                var next = layers[i + 1];

                // 每个 current 节点至少连接一个 next 节点
                for (int c = 0; c < current.Count; c++)
                {
                    // 连接到下一层最近的 1-2 个节点
                    int targetIdx = Mathf.Min(c, next.Count - 1);
                    Connect(current[c], next[targetIdx]);

                    // 额外连接
                    if (next.Count > 1 && _rng.NextDouble() < 0.5)
                    {
                        int extra = targetIdx + (_rng.Next(2) == 0 ? -1 : 1);
                        extra = Mathf.Clamp(extra, 0, next.Count - 1);
                        if (extra != targetIdx)
                            Connect(current[c], next[extra]);
                    }
                }

                // 确保下一层每个节点都有入边
                for (int n = 0; n < next.Count; n++)
                {
                    if (next[n].prevNodeIds.Count == 0)
                    {
                        int srcIdx = Mathf.Min(n, current.Count - 1);
                        Connect(current[srcIdx], next[n]);
                    }
                }
            }
        }

        private void Connect(MapNode from, MapNode to)
        {
            if (!from.nextNodeIds.Contains(to.id))
                from.nextNodeIds.Add(to.id);
            if (!to.prevNodeIds.Contains(from.id))
                to.prevNodeIds.Add(from.id);
        }
    }
}
