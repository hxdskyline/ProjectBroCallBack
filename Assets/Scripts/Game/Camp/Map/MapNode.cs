using System;
using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 地图节点
    /// </summary>
    [Serializable]
    public class MapNode
    {
        public int id;
        public int layer;         // 层号 (1-15)
        public int index;         // 层内序号 (1-N)
        public string nodeCode;   // 节点编号 "x-y"
        public int column;        // = layer - 1，向后兼容
        public int row;           // = index - 1，向后兼容
        public MapNodeType nodeType;
        public MapNodeState state;
        public int battleNumber;  // 对应关卡编号
        public int[] enemyUnitIds; // 该节点的敌方单位组成
        public List<int> nextNodeIds = new List<int>();
        public List<int> prevNodeIds = new List<int>();

        public float x;           // UI 坐标
        public float y;
    }
}
