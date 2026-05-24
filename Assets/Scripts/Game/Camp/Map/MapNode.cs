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
        public int column;        // 层级索引 (0-14)
        public int row;           // 行索引
        public MapNodeType nodeType;
        public MapNodeState state;
        public int battleNumber;  // 对应关卡编号
        public List<int> nextNodeIds = new List<int>();
        public List<int> prevNodeIds = new List<int>();

        public float x;           // UI 坐标
        public float y;
    }
}
