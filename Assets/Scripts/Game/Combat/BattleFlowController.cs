using System;
using UnityEngine;
using Camp;
using Combat.Fighter;
using Combat.Avatar;

namespace Combat
{
    /// <summary>
    /// 战斗流程控制器 - 负责战斗管理器的创建、配置、启动和销毁。
    /// </summary>
    public class BattleFlowController
    {
        private BattleManager _battleManager;
        private bool _isPaused;

        public bool IsPaused => _isPaused;
        public BattleManager BattleManager => _battleManager;

        public void StartBattle(
            int levelId,
            GameObject fighterPrefab,
            AvatarAnimationDefinition playerDefinition,
            AvatarAnimationDefinition enemyDefinition,
            int enemyFighterCount,
            BattleFighterSpawnDefinition[] playerFighterDefinitions,
            Action<bool, int> onBattleEnded,
            UnitStaticAttributes? enemyStats = null,
            TerrainType terrain = TerrainType.Plain,
            WeatherType weather = WeatherType.Sunny,
            BattleFighterSpawnDefinition[] enemyDefinitions = null)
        {
            EnsureBattleManager();

            _battleManager.BattleEnded -= onBattleEnded;
            _battleManager.BattleEnded += onBattleEnded;
            _battleManager.ConfigureFighterPrefab(fighterPrefab);
            _battleManager.ConfigureDemoAvatars(playerDefinition, enemyDefinition);
            _battleManager.ConfigureEnemyFighterCount(enemyFighterCount);
            _battleManager.ConfigurePlayerFighters(playerFighterDefinitions);
            _battleManager.ConfigureTerrainWeather(terrain, weather);

            if (enemyDefinitions != null && enemyDefinitions.Length > 0)
            {
                _battleManager.ConfigureEnemyDefinitions(enemyDefinitions);
            }
            else if (enemyStats.HasValue)
            {
                _battleManager.ConfigureEnemyStats(enemyStats.Value);
            }

            _battleManager.Initialize(levelId);
            _battleManager.StartBattle();

            _isPaused = false;
        }

        /// <summary>
        /// 准备阶段：创建战场背景 + 敌方单位（不开始模拟）
        /// </summary>
        public void StartBattlePrepare(
            int levelId,
            AvatarAnimationDefinition enemyDefinition,
            int enemyFighterCount,
            UnitStaticAttributes? enemyStats = null,
            TerrainType terrain = TerrainType.Plain,
            WeatherType weather = WeatherType.Sunny,
            BattleFighterSpawnDefinition[] enemyDefinitions = null)
        {
            EnsureBattleManager();

            _battleManager.ConfigureDemoAvatars(null, enemyDefinition);
            _battleManager.ConfigureEnemyFighterCount(enemyFighterCount);
            _battleManager.ConfigureTerrainWeather(terrain, weather);

            if (enemyDefinitions != null && enemyDefinitions.Length > 0)
            {
                _battleManager.ConfigureEnemyDefinitions(enemyDefinitions);
            }
            else if (enemyStats.HasValue)
            {
                _battleManager.ConfigureEnemyStats(enemyStats.Value);
            }

            _battleManager.Initialize(levelId);
            _battleManager.BuildPrepareScene();
        }

        public bool TogglePause()
        {
            if (_battleManager == null)
            {
                return _isPaused;
            }

            _isPaused = !_isPaused;
            if (_isPaused)
            {
                _battleManager.PauseBattle();
            }
            else
            {
                _battleManager.ResumeBattle();
            }

            return _isPaused;
        }

        public void StopAndDispose(Action<bool, int> onBattleEnded)
        {
            _isPaused = false;
            Time.timeScale = 1f;

            if (_battleManager == null)
            {
                return;
            }

            _battleManager.BattleEnded -= onBattleEnded;
            UnityEngine.Object.Destroy(_battleManager.gameObject);
            _battleManager = null;
        }

        private void EnsureBattleManager()
        {
            if (_battleManager != null)
            {
                return;
            }

            _battleManager = UnityEngine.Object.FindObjectOfType<BattleManager>();
            if (_battleManager == null)
            {
                GameObject battleGo = new GameObject("BattleManager");
                _battleManager = battleGo.AddComponent<BattleManager>();
            }
        }
    }
}
