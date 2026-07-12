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
        private int _quotaFate = 3;

        private float _columnSpacing = 333f;
        private float _rowSpacing = 107f;

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
                    _quotaFate = ReadInt(q, "fate", _quotaFate);
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

            // Phase E: 为每个战斗节点填充敌方单位（同层不同敌人）
            AssignEnemies(layers, regionIndex);

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
            // 已固定：1温泉(14层) + 1商店(14层)。剩余：2温泉+2商店+3精英+3事件+3命运 = 13
            var specialPool = new List<MapNodeType>();
            for (int i = 0; i < _quotaHotSpring - 1; i++) specialPool.Add(MapNodeType.HotSpring);
            for (int i = 0; i < _quotaShop - 1; i++) specialPool.Add(MapNodeType.Shop);
            for (int i = 0; i < _quotaElite; i++) specialPool.Add(MapNodeType.EliteBattle);
            for (int i = 0; i < _quotaEvent; i++) specialPool.Add(MapNodeType.Event);
            for (int i = 0; i < _quotaFate; i++) specialPool.Add(MapNodeType.Fate);
            Shuffle(specialPool);

            // Step 3: 按层段分配
            // 前段(层4-8, index 3-7)：5个特殊节点分布到5层
            DistributeSpecialsToLayers(specialPool, 0, 5, layers, 3, 7);

            // 中段(层9-12, index 8-11)：4个特殊节点，每层恰好1个
            DistributeExactOnePerLayer(specialPool, 5, 4, layers, 8, 11);

            // 后段(层13, index 12)：4个特殊节点（如果该层节点数足够）
            var layer13 = layers[12];
            int layer13Count = Mathf.Min(layer13.Count, 4);
            for (int i = 0; i < layer13Count; i++)
            {
                layer13[i].nodeType = specialPool[9 + i];
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
            // 第1层 → 第2层：起点连全部（不受出度限制）
            var startNodes = layers[_startLayer - 1];  // layer index 0
            var secondLayer = layers[_startLayer];      // layer index 1
            foreach (var node in secondLayer)
                Connect(startNodes[0], node);

            // 第14层 → 第15层：全部连Boss（不受入度限制）
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

                for (int y = 0; y < srcLayer.Count; y++)
                {
                    var srcNode = srcLayer[y];
                    int srcLayerCount = srcLayer.Count;

                    // 边缘节点（首/末）只有1条出边，中间节点随机1~2条
                    bool isEdge = (y == 0 || y == srcLayerCount - 1);
                    int outDegree = isEdge ? 1 : (_rng.Next(2) == 0 ? 1 : 2);

                    bool hasOutgoing = false;

                    // 主路径：必定连接正上方节点
                    int mainTargetY = y; // 正上方（同索引）
                    if (mainTargetY >= 0 && mainTargetY < dstLayer.Count)
                    {
                        var mainTarget = dstLayer[mainTargetY];
                        if (mainTarget.prevNodeIds.Count < 2)
                        {
                            Connect(srcNode, mainTarget);
                            hasOutgoing = true;
                        }
                    }

                    // 副路径：出边数=2时，尝试左上或右上
                    if (outDegree == 2)
                    {
                        // 随机选择方向顺序
                        int[] sideOffsets = _rng.Next(2) == 0 ? new[] { -1, 1 } : new[] { 1, -1 };
                        foreach (int offset in sideOffsets)
                        {
                            int sideTargetY = y + offset;
                            if (sideTargetY < 0 || sideTargetY >= dstLayer.Count) continue;

                            var sideTarget = dstLayer[sideTargetY];
                            if (sideTarget.prevNodeIds.Count < 2)
                            {
                                Connect(srcNode, sideTarget);
                                break; // 只加一条副路径
                            }
                        }
                    }

                    // 后置保证：非终节点至少1条出边
                    if (!hasOutgoing && srcIdx < _specialLayerHSShop - 2)
                    {
                        int fallbackY = Mathf.Clamp(y, 0, dstLayer.Count - 1);
                        Connect(srcNode, dstLayer[fallbackY]);
                    }
                }
            }

            // 后置验证：确保所有节点都有入边
            ValidateConnections(layers);
        }

        private void ValidateConnections(List<List<MapNode>> layers)
        {
            // 保证每个节点都有入边（尊重入度≤2限制）
            for (int i = 1; i < layers.Count; i++)
            {
                for (int j = 0; j < layers[i].Count; j++)
                {
                    if (layers[i][j].prevNodeIds.Count == 0)
                    {
                        var prevLayer = layers[i - 1];
                        // 优先连接同索引的上层节点，如果入度已满则尝试相邻节点
                        int nearest = Mathf.Min(j, prevLayer.Count - 1);
                        bool connected = false;

                        // 尝试同索引
                        if (prevLayer[nearest].nextNodeIds.Count < 2)
                        {
                            Connect(prevLayer[nearest], layers[i][j]);
                            connected = true;
                        }

                        // 尝试相邻节点
                        if (!connected && nearest > 0 && prevLayer[nearest - 1].nextNodeIds.Count < 2)
                        {
                            Connect(prevLayer[nearest - 1], layers[i][j]);
                            connected = true;
                        }
                        if (!connected && nearest < prevLayer.Count - 1 && prevLayer[nearest + 1].nextNodeIds.Count < 2)
                        {
                            Connect(prevLayer[nearest + 1], layers[i][j]);
                            connected = true;
                        }

                        // 最后兜底：强制连接（可能突破限制，但保证不会孤立）
                        if (!connected)
                        {
                            Connect(prevLayer[nearest], layers[i][j]);
                        }
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

        // ====== Phase E: Assign Enemies ======

        /// <summary>
        /// 为每个战斗类节点分配敌方单位，根据节点类型生成不同的敌人组合
        /// </summary>
        private void AssignEnemies(List<List<MapNode>> layers, int regionIndex)
        {
            var campaign = UnityEngine.GameObject.FindObjectOfType<GameManager>()?.BattleCampaignRuntime;
            if (campaign == null) return;

            for (int layerIdx = 0; layerIdx < layers.Count; layerIdx++)
            {
                var layerNodes = layers[layerIdx];
                int battleNum = regionIndex * _layersPerRegion + (layerIdx + 1);

                // 按节点类型分组
                var normalNodes = new List<MapNode>();
                var eliteNodes = new List<MapNode>();
                var bossNodes = new List<MapNode>();
                foreach (var n in layerNodes)
                {
                    switch (n.nodeType)
                    {
                        case MapNodeType.Battle: normalNodes.Add(n); break;
                        case MapNodeType.EliteBattle: eliteNodes.Add(n); break;
                        case MapNodeType.Boss: bossNodes.Add(n); break;
                    }
                }

                // 根据节点类型生成对应的敌人组合
                if (normalNodes.Count > 0)
                    AssignEnemiesForNodes(campaign, normalNodes, battleNum, MapNodeType.Battle);
                if (eliteNodes.Count > 0)
                    AssignEnemiesForNodes(campaign, eliteNodes, battleNum, MapNodeType.EliteBattle);
                if (bossNodes.Count > 0)
                    AssignEnemiesForNodes(campaign, bossNodes, battleNum, MapNodeType.Boss);
            }
        }

        /// <summary>
        /// 为指定类型的节点生成敌人组合
        /// </summary>
        private void AssignEnemiesForNodes(Combat.BattleCampaignRuntime campaign, List<MapNode> nodes, int battleNum, MapNodeType nodeType)
        {
            // 根据节点类型获取对应的人口上限
            int cap = campaign.GetEnemyPopulationCap(battleNum);

            // 根据节点类型生成敌人组合（复用 BattleCampaignRuntime 的生成逻辑）
            int[] baseIds;
            switch (nodeType)
            {
                case MapNodeType.Boss:
                    baseIds = GenerateBossCompositionForMap(campaign, cap);
                    break;
                case MapNodeType.EliteBattle:
                    baseIds = GenerateEliteCompositionForMap(campaign, cap);
                    break;
                default:
                    baseIds = GenerateNormalCompositionForMap(cap);
                    break;
            }
            if (baseIds == null || baseIds.Length == 0) return;

            // 统计每种敌人的数量
            var typeCounts = new Dictionary<int, int>();
            foreach (int id in baseIds)
            {
                if (!typeCounts.ContainsKey(id)) typeCounts[id] = 0;
                typeCounts[id]++;
            }

            var uniqueTypes = new List<int>(typeCounts.Keys);
            int totalBase = baseIds.Length;

            // 只有一种兵种 → 无法差异化，都一样
            if (uniqueTypes.Count <= 1)
            {
                foreach (var n in nodes)
                    n.enemyUnitIds = baseIds;
                return;
            }

            // 为每个节点生成不同的兵种组合
            var usedSignatures = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                int[] composition;
                int attempts = 0;
                do
                {
                    composition = RollComposition(uniqueTypes, totalBase);
                    attempts++;
                }
                while (usedSignatures.Contains(Signature(composition)) && attempts < 20);

                usedSignatures.Add(Signature(composition));
                nodes[i].enemyUnitIds = composition;
            }
        }

        /// <summary>
        /// 普通关卡敌人组合：鼠辈(5000) + 长矛猫(5010) + 苍蝇猫(1002) 任意比例
        /// </summary>
        private int[] GenerateNormalCompositionForMap(int cap)
        {
            var result = new List<int>();
            int remaining = cap;
            while (remaining > 0)
            {
                int roll = _rng.Next(3);
                result.Add(roll == 0 ? 5000 : roll == 1 ? 5010 : 1002);
                remaining--;
            }
            return result.ToArray();
        }

        /// <summary>
        /// 精英关卡敌人组合：至少一只游侠或猫骑士，加入奶爸猫/巫毒猫/苍蝇猫
        /// </summary>
        private int[] GenerateEliteCompositionForMap(Combat.BattleCampaignRuntime campaign, int cap)
        {
            var result = new List<int>();
            int remaining = cap;

            // 先确保至少有一只游侠(5040, cost=5)或猫骑士(5020, cost=5)
            if (remaining >= 5 && _rng.Next(2) == 0)
            {
                result.Add(5020);
                remaining -= 5;
            }
            else if (remaining >= 5)
            {
                result.Add(5040);
                remaining -= 5;
            }
            else if (remaining >= 1)
            {
                result.Add(PickRandomCost1EnemyForMap());
                remaining -= 1;
            }

            // 填充剩余人口
            while (remaining > 0)
            {
                int roll = _rng.Next(100);
                if (remaining >= 5 && roll < 20)
                {
                    result.Add(5020);
                    remaining -= 5;
                }
                else if (remaining >= 2 && roll < 55)
                {
                    result.Add(PickRandomCost2EnemyForMap());
                    remaining -= 2;
                }
                else
                {
                    result.Add(PickRandomCost1EnemyForMap());
                    remaining -= 1;
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Boss关卡敌人组合：必定至少一只奶牛猫族长(5030, cost=8)，加入奶爸猫/巫毒猫/苍蝇猫
        /// </summary>
        private int[] GenerateBossCompositionForMap(Combat.BattleCampaignRuntime campaign, int cap)
        {
            var result = new List<int>();
            int remaining = cap;

            // 必定放一只奶牛猫族长
            result.Add(5030);
            remaining -= 8;

            // 填充剩余人口
            while (remaining > 0)
            {
                if (remaining >= 8 && _rng.Next(100) < 20)
                {
                    result.Add(5030);
                    remaining -= 8;
                }
                else if (remaining >= 5 && _rng.Next(100) < 25)
                {
                    result.Add(5020);
                    remaining -= 5;
                }
                else if (remaining >= 2 && _rng.Next(100) < 35)
                {
                    result.Add(PickRandomCost2EnemyForMap());
                    remaining -= 2;
                }
                else
                {
                    result.Add(PickRandomCost1EnemyForMap());
                    remaining -= 1;
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// 随机选择一个 cost=1 的敌方单位：鼠辈(5000) / 长矛猫(5010) / 苍蝇猫(1002)
        /// </summary>
        private int PickRandomCost1EnemyForMap()
        {
            int roll = _rng.Next(3);
            return roll == 0 ? 5000 : roll == 1 ? 5010 : 1002;
        }

        /// <summary>
        /// 随机选择一个 cost=2 的敌方单位：奶爸猫(1005) / 巫毒猫(1101)
        /// </summary>
        private int PickRandomCost2EnemyForMap()
        {
            return _rng.Next(2) == 0 ? 1005 : 1101;
        }

        /// <summary>
        /// 随机生成一个敌人组合：总数 = totalCount，从 types 中随机分配
        /// </summary>
        private int[] RollComposition(List<int> types, int totalCount)
        {
            // 随机决定每种兵种的数量，总和 = totalCount
            var weights = new float[types.Count];
            float sum = 0;
            for (int i = 0; i < types.Count; i++)
            {
                weights[i] = (float)(_rng.NextDouble() + 0.1);
                sum += weights[i];
            }

            var result = new List<int>();
            int remaining = totalCount;
            for (int i = 0; i < types.Count; i++)
            {
                int count = (i == types.Count - 1) ? remaining : Mathf.RoundToInt(totalCount * weights[i] / sum);
                count = Mathf.Clamp(count, 0, remaining);
                remaining -= count;
                for (int j = 0; j < count; j++)
                    result.Add(types[i]);
            }

            // 如果还有剩余（四舍五入误差），随机分配
            while (remaining > 0)
            {
                result.Add(types[_rng.Next(types.Count)]);
                remaining--;
            }

            // 打乱顺序
            var arr = result.ToArray();
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                int tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
            }
            return arr;
        }

        private static string Signature(int[] composition)
        {
            var counts = new Dictionary<int, int>();
            foreach (int id in composition)
            {
                if (!counts.ContainsKey(id)) counts[id] = 0;
                counts[id]++;
            }
            var parts = new List<string>();
            foreach (var kv in counts)
                parts.Add($"{kv.Key}:{kv.Value}");
            parts.Sort();
            return string.Join(",", parts);
        }
    }
}
