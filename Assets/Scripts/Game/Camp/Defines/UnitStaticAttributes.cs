namespace Camp
{
    /// <summary>
    /// 单位静态属性（基础属性，从配置读取）
    /// </summary>
    [System.Serializable]
    public struct UnitStaticAttributes
    {
        public int Attack;
        public int Defense;
        public int MaxHp;
        public float MoveSpeed;
        public float AttackSpeed;
        public float AttackRange;

        public static UnitStaticAttributes Default => new UnitStaticAttributes
        {
            Attack = 10,
            Defense = 5,
            MaxHp = 100,
            MoveSpeed = 2.2f,
            AttackSpeed = 1.0f,
            AttackRange = 1.0f
        };
    }
}
