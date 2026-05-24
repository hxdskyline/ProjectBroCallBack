namespace Camp
{
    /// <summary>
    /// Buff 叠加规则
    /// </summary>
    public enum BuffStackRule
    {
        None = 0,            // 不叠加，刷新持续时间
        Stack = 1,           // 叠加层数
        RefreshDuration = 2  // 刷新持续时间（不叠加层数）
    }
}
