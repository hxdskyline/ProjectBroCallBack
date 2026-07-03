using System;

namespace Camp
{
    /// <summary>
    /// Unified gameplay effect types shared by combat, recruitment, items, relics and events.
    /// </summary>
    public enum GameEffect
    {
        // Attribute modifiers (percent)
        AttackPercent = 0,
        DefensePercent = 1,
        HpPercent = 2,
        SpeedPercent = 3,
        AllPercent = 4,

        // Attribute modifiers (flat)
        AttackFlat = 10,
        DefenseFlat = 11,
        HpFlat = 12,
        SpeedFlat = 13,
        LeaderSpeedFlat = 14,
        LeaderAttackPerDeadCat = 15,

        // Combat
        DoubleHit = 20,
        Lifesteal = 21,
        DamageReflect = 22,
        CritDamage = 23,
        CritChance = 24,
        DamageReduce = 25,

        // Recruitment / economy
        ExtraCatOnRecruit = 30,
        RecruitCostReduce = 31,
        CatFoodGain = 32,

        // Summon
        SummonTotem = 40,

        // Consumables
        Bomb = 50,
        FreezeAll = 51,
        HealAll = 52,
        BuffAttack = 53,
        BuffDefense = 54,

        // Status effects
        Poison = 60,
        Bleed = 61,
        Burn = 62,
        Freeze = 63,
        Slow = 64,
        HuntMark = 65,

        // Control
        Root = 70,
        Silence = 71,
        Stun = 72,
        KnockBack = 73,
        KnockDown = 74,
        KnockUp = 75,
        Taunt = 76,

        // Defense / utility
        Heal = 80,
        ShareDamage = 81,
        SuperArmor = 82,
        FreezeBreakDamage = 83,

        // Special mechanics
        Split = 90,
        Bounce = 91,
        Summon = 92,

        // Combat growth
        FullnessStack = 66,
        DragonCharge = 67,
        HunterFocus = 68,
    }

    [Serializable]
    public struct GameEffectEntry
    {
        public GameEffect type;
        public float value;

        public GameEffectEntry(GameEffect type, float value)
        {
            this.type = type;
            this.value = value;
        }
    }
}
