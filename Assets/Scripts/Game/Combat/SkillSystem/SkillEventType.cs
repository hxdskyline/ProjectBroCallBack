namespace Combat.SkillSystem
{
    public enum SkillEventType
    {
        None = 0,
        BattleStart = 1,
        Tick = 2,
        AttackLaunch = 3,
        AttackHit = 4,
        ReceiveHit = 5,
        CastSkill = 6,
        CastSkillEnd = 7,
        BuffAdded = 8,
        BuffRemoved = 9,
        HpChanged = 10,
        UnitDied = 11,
        UnitKilled = 12,
    }
}
