using Camp;

namespace Combat
{
    /// <summary>
    /// 种族地形天气 Buff 矩阵
    /// 根据设计文档: DesignDocs/新需求/新的需求改动很大.txt:318-325
    /// </summary>
    public static class TribeBattleBuffProvider
    {
        /// <summary>
        /// 获取指定种族在指定地形和天气下的属性修正
        /// </summary>
        public static TerrainWeatherBuff GetBuff(TribeType tribe, TerrainType terrain, WeatherType weather)
        {
            TerrainWeatherBuff buff = new TerrainWeatherBuff();

            // 地形 buff
            switch (terrain)
            {
                case TerrainType.Plain:
                    buff = ApplyPlainBuff(tribe, buff);
                    break;
                case TerrainType.Brush:
                    buff = ApplyBrushBuff(tribe, buff);
                    break;
            }

            // 天气 buff
            switch (weather)
            {
                case WeatherType.Sunny:
                    buff = ApplySunnyBuff(tribe, buff);
                    break;
                case WeatherType.Rainy:
                    buff = ApplyRainyBuff(tribe, buff);
                    break;
                case WeatherType.Night:
                    buff = ApplyNightBuff(tribe, buff);
                    break;
                case WeatherType.Windy:
                    buff = ApplyWindyBuff(tribe, buff);
                    break;
            }

            return buff;
        }

        // 平地 buff
        // 大橘: 移速+20%
        private static TerrainWeatherBuff ApplyPlainBuff(TribeType tribe, TerrainWeatherBuff buff)
        {
            switch (tribe)
            {
                case TribeType.Orange:
                    buff.speedPercent += 0.2f;
                    break;
            }
            return buff;
        }

        // 灌木 buff
        // 狸花、奶牛、暹罗: 移速+20%
        private static TerrainWeatherBuff ApplyBrushBuff(TribeType tribe, TerrainWeatherBuff buff)
        {
            switch (tribe)
            {
                case TribeType.Tabby:
                case TribeType.Cow:
                case TribeType.Siamese:
                    buff.speedPercent += 0.2f;
                    break;
            }
            return buff;
        }

        // 晴天 buff
        // 暹罗: 攻击+20%
        private static TerrainWeatherBuff ApplySunnyBuff(TribeType tribe, TerrainWeatherBuff buff)
        {
            switch (tribe)
            {
                case TribeType.Siamese:
                    buff.attackPercent += 0.2f;
                    break;
            }
            return buff;
        }

        // 雨天 buff
        // 奶牛: 攻击+20%
        // 大橘: 攻击-20%
        private static TerrainWeatherBuff ApplyRainyBuff(TribeType tribe, TerrainWeatherBuff buff)
        {
            switch (tribe)
            {
                case TribeType.Cow:
                    buff.attackPercent += 0.2f;
                    break;
                case TribeType.Orange:
                    buff.attackPercent -= 0.2f;
                    break;
            }
            return buff;
        }

        // 夜晚 buff
        // 狸花: 攻击+20%
        // 奶牛: 防御-20%
        // 暹罗: 血量-20%
        private static TerrainWeatherBuff ApplyNightBuff(TribeType tribe, TerrainWeatherBuff buff)
        {
            switch (tribe)
            {
                case TribeType.Tabby:
                    buff.attackPercent += 0.2f;
                    break;
                case TribeType.Cow:
                    buff.defensePercent -= 0.2f;
                    break;
                case TribeType.Siamese:
                    buff.hpPercent -= 0.2f;
                    break;
            }
            return buff;
        }

        // 大风 buff
        // 大橘: 防御+20%
        // 狸花: 移速-20%
        private static TerrainWeatherBuff ApplyWindyBuff(TribeType tribe, TerrainWeatherBuff buff)
        {
            switch (tribe)
            {
                case TribeType.Orange:
                    buff.defensePercent += 0.2f;
                    break;
                case TribeType.Tabby:
                    buff.speedPercent -= 0.2f;
                    break;
            }
            return buff;
        }

        /// <summary>
        /// 判断特定地形对该种族是增益(1)、减益(-1)还是无影响(0)
        /// </summary>
        public static int GetTerrainBuffStatus(TribeType tribe, TerrainType terrain)
        {
            TerrainWeatherBuff buff = new TerrainWeatherBuff();
            switch (terrain)
            {
                case TerrainType.Plain: buff = ApplyPlainBuff(tribe, buff); break;
                case TerrainType.Brush: buff = ApplyBrushBuff(tribe, buff); break;
            }

            if (buff.attackPercent > 0 || buff.defensePercent > 0 || buff.hpPercent > 0 || buff.speedPercent > 0) return 1;
            if (buff.attackPercent < 0 || buff.defensePercent < 0 || buff.hpPercent < 0 || buff.speedPercent < 0) return -1;
            return 0;
        }

        /// <summary>
        /// 判断特定天气对该种族是增益(1)、减益(-1)还是无影响(0)
        /// </summary>
        public static int GetWeatherBuffStatus(TribeType tribe, WeatherType weather)
        {
            TerrainWeatherBuff buff = new TerrainWeatherBuff();
            switch (weather)
            {
                case WeatherType.Sunny: buff = ApplySunnyBuff(tribe, buff); break;
                case WeatherType.Rainy: buff = ApplyRainyBuff(tribe, buff); break;
                case WeatherType.Night: buff = ApplyNightBuff(tribe, buff); break;
                case WeatherType.Windy: buff = ApplyWindyBuff(tribe, buff); break;
            }

            if (buff.attackPercent > 0 || buff.defensePercent > 0 || buff.hpPercent > 0 || buff.speedPercent > 0) return 1;
            if (buff.attackPercent < 0 || buff.defensePercent < 0 || buff.hpPercent < 0 || buff.speedPercent < 0) return -1;
            return 0;
        }
    }
}
