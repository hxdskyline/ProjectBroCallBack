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

        // 战斗结果记录（用于显示标记）
        public bool battleCompleted;  // 是否完成过战斗
        public bool battleVictory;    // 战斗是否胜利
    }
}
