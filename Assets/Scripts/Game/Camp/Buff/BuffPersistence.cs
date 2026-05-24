namespace Camp
{
    /// <summary>
    /// Buff 持久化类型
    /// </summary>
    public enum BuffPersistence
    {
        BattleOnly = 0,           // 仅本场战斗
        Persistent = 1,           // 持久化（跨战斗）
        TemporaryRoundBased = 2   // 临时（按回合数）
    }
}
