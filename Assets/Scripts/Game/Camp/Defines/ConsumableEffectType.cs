namespace Camp
{
    /// <summary>
    /// 消耗品效果类型
    /// </summary>
    public enum ConsumableEffectType
    {
        Bomb = 0,          // 炸弹：总500伤害，由全体敌人平摊
        FreezeTrap = 1,    // 冰冻陷阱：冻结全体 3 秒
        HealPotion = 2,    // 治疗药水：全体回复 50% HP
        AttackBuff = 3,    // 攻击增益：全体攻击 +30%（本场战斗）
        DefenseBuff = 4    // 防御增益：全体防御 +30%（本场战斗）
    }
}
