namespace Camp
{
    /// <summary>
    /// 地图节点状态
    /// </summary>
    public enum MapNodeState
    {
        Locked = 0,
        Available = 1,
        Visited = 2,
        Fogged = 3       // 迷雾封锁（超过当前层3层以上的节点）
    }
}
