using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LitJson;

namespace Camp
{
    /// <summary>
    /// 地图生成器 — v2.0 配额制+分段分配+概率连线
    /// 设计参考：正式文档/100_系统_选关.md
    /// </summary>
    public class MapGenerator
    {
        private int _layersPerRegion = 15;
        private int _regionCount = 3;
        private int _specialLayerBoss = 15;       // 1-based
        private int _specialLayerHSShop = 14;     // 1-based
        private int _startLayer = 1;              // 1-based
        private List<int> _normalLayerCounts = new List<int> { 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4 };
        private int _quotaHotSpring = 3;
        private int _quotaShop = 3;
        private int _quotaElite = 3;
        private int _quotaEvent = 3;

        private float _columnSpacing = 500f;
        private float _rowSpacing = 160f;

        private static readonly System.Random _rng = new System.Random();

        public MapGenerator()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Tables", "map_config.json");
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                JsonData data = JsonMapper.ToObject(json);

                _layersPerRegion = ReadInt(data, "layersPerRegion", _layersPerRegion);
                _regionCount = ReadInt(data, "regionCount", _regionCount);
                _specialLayerBoss = ReadInt(data, "specialLayerBoss", _specialLayerBoss);
                _specialLayerHSShop = ReadInt(data, "specialLayerHotSpringAndShop", _specialLayerHSShop);
                _startLayer = ReadInt(data, "startLayer", _startLayer);

                if (data.ContainsKey("normalLayerCounts") && data["normalLayerCounts"].IsArray)
                {
                    _normalLayerCounts = new List<int>();
                    for (int i = 0; i < data["normalLayerCounts"].Count; i++)
                        _normalLayerCounts.Add((int)data["normalLayerCounts"][i]);
                }

                if (data.ContainsKey("specialQuotaPerRegion"))
                {
                    var q = data["specialQuotaPerRegion"];
                    _quotaHotSpring = ReadInt(q, "hotSpring", _quotaHotSpring);
                    _quotaShop = ReadInt(q, "shop", _quotaShop);
                    _quotaElite = ReadInt(q, "elite", _quotaElite);
                    _quotaEvent = ReadInt(q, "event", _quotaEvent);
                }

                Debug.Log($"[MapGenerator] Config loaded: layers={_layersPerRegion}, regions={_regionCount}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MapGenerator] Failed to load config: {e.Message}, using defaults");
            }
        }

        private static int ReadInt(JsonData data, string key, int defaultValue)
        {
            if (data == null || !data.ContainsKey(key)) return defaultValue;
            return data[key].IsInt ? (int)data[key] : defaultValue;
        }

        // ====== Public API ======

        public List<MapData> GenerateFullMap()
        {
            var maps = new List<MapData>();
            for (int region = 0; region < _regionCount; region++)
                maps.Add(GenerateRegion(region));
            return maps;
        }

        // ====== Region Generation ======

        private MapData GenerateRegion(int regionIndex)
        {
            var map = new MapData();
            int nodeIdCounter = 0;

            // Phase A: 确定每层节点数
            int[] layerCounts = GenerateLayerNodeCounts();

            // Phase B: 创建所有节点（先占位 Battle）
            var layers = new List<List<MapNode>>();
            for (int layerIdx = 0; layerIdx < _layersPerRegion; layerIdx++)
            {
                int layer = layerIdx + 1; // 1-based
                int count = layerCounts[layerIdx];
                int battleNum = regionIndex * _layersPerRegion + layer;

                var layerNodes = new List<MapNode>();
                for (int i = 0; i < count; i++)
                {
                    layerNodes.Add(CreateNode(nodeIdCounter++, layer, i, count, MapNodeType.Battle, battleNum));
                }
                layers.Add(layerNodes);
                map.nodes.AddRange(layerNodes);
            }

            // Phase C: 分配节点类型
            AssignNodeTypes(layers);

            // Phase D: 生成连线
            GenerateEdges(layers);

            return map;
        }

        // ====== Phase A: Layer Node Counts ======

        private int[] GenerateLayerNodeCounts()
        {
            int[] counts = new int[_layersPerRegion];

            // 固定层
            counts[_startLayer - 1] = 1;                    // 第1层：1节点
            counts[_specialLayerHSShop - 1] = 2;            // 第14层：2节点
            counts[_specialLayerBoss - 1] = 1;               // 第15层：1节点

            // 通用层(第2-13层)：从池 shuffle
            var pool = new List<int>(_normalLayerCounts);
            Shuffle(pool);

            int poolIdx = 0;
            for (int i = _startLayer; i < _specialLayerHSShop - 1; i++) // indices 1..12
            {
                if (counts[i] == 0) // 不是固定层
                {
                    counts[i] = poolIdx < pool.Count ? pool[poolIdx++] : 3;
                }
            }

            return counts;
        }

        // ====== Phase C: Node Type Assignment ======

        private void AssignNodeTypes(List<List<MapNode>> layers)
        {
            // Step 1: 固定特殊层
            layers[_startLayer - 1][0].nodeType = MapNodeType.Battle; // 起点

            // 第14层：shuffle(HotSpring, Shop)
            var hs14 = new List<MapNodeType> { MapNodeType.HotSpring, MapNodeType.Shop };
            Shuffle(hs14);
            layers[_specialLayerHSShop - 1][0].nodeType = hs14[0];
            layers[_specialLayerHSShop - 1][1].nodeType = hs14[1];

            // 第15层：Boss
            layers[_specialLayerBoss - 1][0].nodeType = MapNodeType.Boss;

            // Step 2: 构建特殊节点池
            // 已固定：1温泉(14层) + 1商店(14层)。剩余：2温泉+2商店+3精英+3事件 = 10
            var specialPool = new List<MapNodeType>();
            for (int i = 0; i < _quotaHotSpring - 1; i++) specialPool.Add(MapNodeType.HotSpring);
            for (int i = 0; i < _quotaShop - 1; i++) specialPool.Add(MapNodeType.Shop);
            for (int i = 0; i < _quotaElite; i++) specialPool.Add(MapNodeType.EliteBattle);
            for (int i = 0; i < _quotaEvent; i++) specialPool.Add(MapNodeType.Event);
            Shuffle(specialPool);

            // Step 3: 按层段分配
            // 前段(层4-8, index 3-7)：4个特殊节点分布到5层
            DistributeSpecialsToLayers(specialPool, 0, 4, layers, 3, 7);

            // 中段(层9-12, index 8-11)：4个特殊节点，每层恰好1个
            DistributeExactOnePerLayer(specialPool, 4, 4, layers, 8, 11);

            // 后段(层13, index 12)：2个特殊节点
            var layer13 = layers[12];
            if (layer13.Count >= 2)
            {
                layer13[0].nodeType = specialPool[8];
                layer13[1].nodeType = specialPool[9];
            }
            else if (layer13.Count == 1)
            {
                layer13[0].nodeType = specialPool[8];
            }

            // Step 4: 剩余槽位已经是 Battle（占位值），无需额外填充
        }

        /// <summary>
        /// 将 N 个特殊类型随机分布到指定层段中
        /// </summary>
        private void DistributeSpecialsToLayers(List<MapNodeType> pool, int poolStart, int count,
            List<List<MapNode>> layers, int layerStart, int layerEnd)
        {
            // 随机选 count 个不同的层
            var layerIndices = new List<int>();
            for (int i = layerStart; i <= layerEnd; i++) layerIndices.Add(i);
            Shuffle(layerIndices);

            for (int i = 0; i < count && i < layerIndices.Count; i++)
            {
                int layerIdx = layerIndices[i];
                var layer = layers[layerIdx];
                if (layer.Count == 0) continue;

                // 随机选该层中一个节点
                int nodeIdx = _rng.Next(layer.Count);
                layer[nodeIdx].nodeType = pool[poolStart + i];
            }
        }

        /// <summary>
        /// 将 N 个特殊类型每层恰好分配 1 个（shuffle 后按序分配）
        /// </summary>
        private void DistributeExactOnePerLayer(List<MapNodeType> pool, int poolStart, int count,
            List<List<MapNode>> layers, int layerStart, int layerEnd)
        {
            for (int i = 0; i < count; i++)
            {
                int layerIdx = layerStart + i;
                if (layerIdx > layerEnd) break;

                var layer = layers[layerIdx];
                if (layer.Count == 0) continue;

                int nodeIdx = _rng.Next(layer.Count);
                layer[nodeIdx].nodeType = pool[poolStart + i];
            }
        }

        // ====== Phase D: Edge Generation ======

        private void GenerateEdges(List<List<MapNode>> layers)
        {
            // 第1层 → 第2层：起点连全部
            var startNodes = layers[_startLayer - 1];  // layer index 0
            var secondLayer = layers[_startLayer];      // layer index 1
            foreach (var node in secondLayer)
                Connect(startNodes[0], node);

            // 第14层 → 第15层：全部连Boss
            var preBoss = layers[_specialLayerHSShop - 1]; // layer index 13
            var boss = layers[_specialLayerBoss - 1];       // layer index 14
            foreach (var node in preBoss)
                Connect(node, boss[0]);

            // 通用层：第2层→第3层 至 第13层→第14层
            // 即 layer index 1→2 至 12→13
            for (int srcIdx = _startLayer; srcIdx <= _specialLayerHSShop - 2; srcIdx++)
            {
                var srcLayer = layers[srcIdx];
                var dstLayer = layers[srcIdx + 1];

                // 按 index(行号) 从小到大处理
                for (int y = 0; y < srcLayer.Count; y++)
                {
                    var srcNode = srcLayer[y];
                    int[] offsets = { -1, 0, 1, 2 };

                    foreach (int offset in offsets)
                    {
                        int targetY = y + offset;
                        if (targetY < 0 || targetY >= dstLayer.Count) continue;

                        var dstNode = dstLayer[targetY];
                        int existingIncoming = dstNode.prevNodeIds.Count;

                        if (existingIncoming >= 3) continue;

                        double prob;
                        if (existingIncoming == 0) prob = 1.0;
                        else if (existingIncoming == 1) prob = 0.5;
                        else prob = 0.3;

                        if (_rng.NextDouble() < prob)
                            Connect(srcNode, dstNode);
                    }
                }
            }

            // 后置验证：确保所有节点都有入边
            ValidateConnections(layers);
        }

        private void ValidateConnections(List<List<MapNode>> layers)
        {
            // 保证每个节点都有入边
            for (int i = 1; i < layers.Count; i++)
            {
                for (int j = 0; j < layers[i].Count; j++)
                {
                    if (layers[i][j].prevNodeIds.Count == 0)
                    {
                        var prevLayer = layers[i - 1];
                        int nearest = Mathf.Min(j, prevLayer.Count - 1);
                        Connect(prevLayer[nearest], layers[i][j]);
                    }
                }
            }

            // 保证每个非末层节点都有出边
            for (int i = 0; i < layers.Count - 1; i++)
            {
                for (int j = 0; j < layers[i].Count; j++)
                {
                    if (layers[i][j].nextNodeIds.Count == 0)
                    {
                        var nextLayer = layers[i + 1];
                        int nearest = Mathf.Min(j, nextLayer.Count - 1);
                        Connect(layers[i][j], nextLayer[nearest]);
                    }
                }
            }
        }

        // ====== Node Creation ======

        private MapNode CreateNode(int id, int layer, int indexInLayer, int layerCount, MapNodeType type, int battleNumber)
        {
            float centerRow = (layerCount - 1) / 2f;

            return new MapNode
            {
                id = id,
                layer = layer,
                index = indexInLayer + 1,  // 1-based
                nodeCode = $"{layer}-{indexInLayer + 1}",
                column = layer - 1,        // backward compat
                row = indexInLayer,         // backward compat
                nodeType = type,
                state = layer == _startLayer ? MapNodeState.Available : MapNodeState.Locked,
                battleNumber = battleNumber,
                nextNodeIds = new List<int>(),
                prevNodeIds = new List<int>(),
                x = (layer - 1) * _columnSpacing,
                y = (indexInLayer - centerRow) * _rowSpacing
            };
        }

        // ====== Helpers ======

        private void Connect(MapNode from, MapNode to)
        {
            if (!from.nextNodeIds.Contains(to.id))
                from.nextNodeIds.Add(to.id);
            if (!to.prevNodeIds.Contains(from.id))
                to.prevNodeIds.Add(from.id);
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
