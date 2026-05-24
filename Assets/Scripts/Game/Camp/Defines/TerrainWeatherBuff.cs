namespace Camp
{
    /// <summary>
    /// 地形天气属性修正
    /// </summary>
    [System.Serializable]
    public struct TerrainWeatherBuff
    {
        public float attackPercent;
        public float defensePercent;
        public float hpPercent;
        public float speedPercent;

        public bool IsNeutral =>
            attackPercent == 0f &&
            defensePercent == 0f &&
            hpPercent == 0f &&
            speedPercent == 0f;

        public static TerrainWeatherBuff None => new TerrainWeatherBuff();
    }
}
