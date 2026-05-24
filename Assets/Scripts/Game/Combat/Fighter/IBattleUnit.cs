namespace Combat.Fighter
{
    /// <summary>
    /// 战斗单位接口 — 定义战斗中单位的基本契约
    /// </summary>
    public interface IBattleUnit
    {
        int CurrentHp { get; }
        bool IsDead { get; }
        bool IsAlive { get; }
        float FreezeTimer { get; set; }
    }
}
