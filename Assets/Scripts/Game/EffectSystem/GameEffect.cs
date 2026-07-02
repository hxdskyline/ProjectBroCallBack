using System;

namespace Camp
{
    /// <summary>
    /// 统一游戏效果类型 — 装备、招募、祭祀、消耗品等所有系统共用
    /// </summary>
    public enum GameEffect
    {
        // ── 属性修改类（百分比） ──
        AttackPercent = 0,       // 攻击 +value%
        DefensePercent = 1,      // 防御 +value%
        HpPercent = 2,           // 生命 +value%
        SpeedPercent = 3,        // 速度 +value%
        AllPercent = 4,          // 全属性 +value%

        // ── 属性修改类（固定值） ──
        AttackFlat = 10,         // 攻击 +value
        DefenseFlat = 11,        // 防御 +value
        HpFlat = 12,             // 生命 +value
        SpeedFlat = 13,          // 速度 +value
        LeaderSpeedFlat = 14,    // 族长速度 +value（仅族长生效）
        LeaderAttackPerDeadCat = 15, // 每有一只死去的小猫，族长攻击 +value

        // ── 战斗能力类 ──
        DoubleHit = 20,          // 攻击两次（概率 value）
        Lifesteal = 21,          // 吸血（回复造成伤害的 value%）
        DamageReflect = 22,      // 反伤（反弹受到伤害的 value%）
        CritDamage = 23,         // 暴击伤害 +value%
        CritChance = 24,         // 暴击率 +value%
        DamageReduce = 25,       // 受到伤害 -value（固定值）

        // ── 招募/经济类 ──
        ExtraCatOnRecruit = 30,  // 招募时额外获得 value 只小猫
        RecruitCostReduce = 31,  // 招募费用降低 value%
        CatFoodGain = 32,        // 直接获得 value 猫粮

        // ── 召唤类 ──
        SummonTotem = 40,        // 战斗开始召唤图腾（value = 图腾模板 ID）

        // ── 消耗品效果类 ──
        Bomb = 50,               // 全体造成 value 伤害
        FreezeAll = 51,          // 冻结全体敌人 value 秒
        HealAll = 52,            // 全体回复 value% 生命
        BuffAttack = 53,         // 全体攻击 +value%（临时）
        BuffDefense = 54,        // 全体防御 +value%（临时）

        // ── 状态效果类（DoT / 控制 / 减益） ──
        Poison = 60,             // 毒：每秒 effectParam1 点，currentStacks 层
        Bleed = 61,              // 流血：每秒 effectParam1 点
        Burn = 62,               // 燃烧：每秒 effectParam1 点
        Freeze = 63,             // 冻结：定身 effectParam1 秒
        Slow = 64,               // 减速：移速 -effectParam1%，持续 effectParam2 秒
        HuntMark = 65,           // 狸花猫狩猎标记：受到伤害 +effectParam1%

        // ── 控制效果类 ──
        Root = 70,               // 缠绕：无法移动，可攻击，持续 effectParam1 秒
        Silence = 71,            // 沉默：无法触发技能，持续 effectParam1 秒
        Stun = 72,               // 眩晕：无法行动和攻击，持续 effectParam1 秒
        KnockBack = 73,          // 击退：位移 effectParam1 米
        KnockDown = 74,          // 击倒：倒地 effectParam1 秒
        KnockUp = 75,            // 击飞：浮空+倒地 effectParam1 秒
        Taunt = 76,              // 嘲讽：强制攻击施法者，持续 effectParam1 秒

        // ── 防御/增益类 ──
        Heal = 80,               // 治疗：立即回复 effectParam1 点HP
        ShareDamage = 81,        // 分摊：连接分摊伤害和治疗，持续 effectParam1 秒
        SuperArmor = 82,         // 霸体：免疫所有控制效果，持续 effectParam1 秒
        FreezeBreakDamage = 83,  // 破冰伤害：冰冻结束时受到 effectParam1 点伤害

        // ── 特殊效果类 ──
        Split = 90,              // 分裂：攻击/技能多触发1次
        Bounce = 91,             // 弹射：伤害后弹射至另一敌人
        Summon = 92,             // 召唤：召唤 effectParam1 个单位

        // ── 战斗内成长类（可叠加层数） ──
        FullnessStack = 66,      // 橘猫饱食：每层 +value 生命 / +value 攻击
        DragonCharge = 67,       // 无毛猫龙语充能：每层 +value% 法术伤害
        HunterFocus = 68,        // 狸花猫猎手专注：每层 +value% 对标记目标伤害
    }

    /// <summary>
    /// 效果条目 — 一个物品/选择可以携带多个效果
    /// </summary>
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
