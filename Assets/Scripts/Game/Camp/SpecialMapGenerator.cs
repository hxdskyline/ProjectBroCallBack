using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 特殊模式地图生成器
    /// 固定结构：15层，第1层1节点，第2-14层每层2节点，第15层1节点
    /// 连线规则：每个节点连接下一层的所有节点（全连接）
    /// </summary>
    public class SpecialMapGenerator
    {
        private readonly SpecialLevelConfig _levelConfig;

        private float _columnSpacing = 333f;
        private float _rowSpacing = 107f;

        public SpecialMapGenerator(SpecialLevelConfig levelConfig)
        {
            _levelConfig = levelConfig;
        }

        /// <summary>
        /// 生成特殊模式地图（单大关）
        /// </summary>
        public List<MapData> Generate()
        {
            var map = new MapData();
            int nodeIdCounter = 0;

            var layers = new List<List<MapNode>>();

            // Phase A: 创建所有节点
            for (int layer = 1; layer <= _levelConfig.LayerCount; layer++)
            {
                var layerCfg = _levelConfig.GetLayer(layer);
                if (layerCfg == null) continue;

                var layerNodes = new List<MapNode>();

                // node1
                var node1 = CreateNode(nodeIdCounter++, layer, 0, layerCfg.node1);
                layerNodes.Add(node1);

                // node2（如果存在）
                if (layerCfg.node2 != null)
                {
                    var node2 = CreateNode(nodeIdCounter++, layer, 1, layerCfg.node2);
                    layerNodes.Add(node2);
                }

                layers.Add(layerNodes);
                map.nodes.AddRange(layerNodes);
            }

            // Phase B: 生成连线（全连接：每个节点连接下一层所有节点）
            GenerateEdges(layers);

            // Phase C: 设置起点状态
            if (layers.Count > 0 && layers[0].Count > 0)
            {
                layers[0][0].state = MapNodeState.Available;
            }

            // Phase D: 应用迷雾
            ApplyFogStates(layers);

            return new List<MapData> { map };
        }

        private MapNode CreateNode(int id, int layer, int indexInLayer, SpecialNodeConfig config)
        {
            int nodeCount = (config == null) ? 1 : 2; // 简化：固定2节点层用2计算居中
            if (layer == 1 || layer == _levelConfig.LayerCount) nodeCount = 1;

            float centerRow = (nodeCount - 1) / 2f;

            MapNodeType nodeType = ParseNodeType(config?.type ?? "battle");

            return new MapNode
            {
                id = id,
                layer = layer,
                index = indexInLayer + 1,
                nodeCode = $"{layer}-{indexInLayer + 1}",
                column = layer - 1,
                row = indexInLayer,
                nodeType = nodeType,
                state = MapNodeState.Locked,
                battleNumber = layer,
                enemyUnitIds = config?.enemies,
                nextNodeIds = new List<int>(),
                prevNodeIds = new List<int>(),
                x = (layer - 1) * _columnSpacing,
                y = (indexInLayer - centerRow) * _rowSpacing
            };
        }

        private MapNodeType ParseNodeType(string type)
        {
            switch (type)
            {
                case "battle": return MapNodeType.Battle;
                case "eliteBattle": return MapNodeType.EliteBattle;
                case "boss": return MapNodeType.Boss;
                case "shop": return MapNodeType.Shop;
                case "event": return MapNodeType.Event;
                case "hotSpring": return MapNodeType.HotSpring;
                case "wish": return MapNodeType.Wish;
                default: return MapNodeType.Battle;
            }
        }

        /// <summary>
        /// 全连接：每个节点连接下一层的所有节点
        /// </summary>
        private void GenerateEdges(List<List<MapNode>> layers)
        {
            for (int i = 0; i < layers.Count - 1; i++)
            {
                var currentLayer = layers[i];
                var nextLayer = layers[i + 1];

                foreach (var src in currentLayer)
                {
                    foreach (var dst in nextLayer)
                    {
                        Connect(src, dst);
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

        private void ApplyFogStates(List<List<MapNode>> layers)
        {
            int fogDistance = 3;
            for (int i = 0; i < layers.Count; i++)
            {
                if (i > fogDistance)
                {
                    foreach (var node in layers[i])
                    {
                        if (node.state != MapNodeState.Available)
                            node.state = MapNodeState.Fogged;
                    }
                }
            }
        }
    }
}
