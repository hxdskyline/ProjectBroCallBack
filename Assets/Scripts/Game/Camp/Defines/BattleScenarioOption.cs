namespace Camp
{
    /// <summary>
    /// 战斗场景选项（地形 + 天气 + 阵型）
    /// </summary>
    [System.Serializable]
    public struct BattleScenarioOption
    {
        public TerrainType terrain;
        public WeatherType weather;
        public EnemyFormationType formationType;

        public BattleScenarioOption(TerrainType terrain, WeatherType weather, EnemyFormationType formationType)
        {
            this.terrain = terrain;
            this.weather = weather;
            this.formationType = formationType;
        }
    }
}
