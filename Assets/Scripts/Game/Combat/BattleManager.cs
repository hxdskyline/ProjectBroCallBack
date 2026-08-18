using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using Camp;
using Combat.Fighter;
using Combat.Avatar;
using Combat.Effects;

namespace Combat
{
    /// <summary>
    /// 战斗管理器 - 管理战斗逻辑
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [Header("Demo Avatar Setup")]
        [SerializeField] private GameObject _fighterPrefab;
        [SerializeField] private AvatarAnimationDefinition _playerAvatarDefinition;
        [SerializeField] private AvatarAnimationDefinition _enemyAvatarDefinition;
        [SerializeField] private int _fightersPerCamp = 15;
        [SerializeField] private Vector2 _spawnAreaMin = new Vector2(-6.5f, -3.5f);
        [SerializeField] private Vector2 _spawnAreaMax = new Vector2(6.5f, 3.5f);
        [SerializeField] private float _spawnMinDistance = 1.5f;
        [SerializeField] private int _spawnTryCount = 24;
        [SerializeField] private float _fighterScale = 0.45f;
        [SerializeField] private Color _playerTint = new Color(0.6f, 0.9f, 1f, 1f);
        [SerializeField] private Color _enemyTint = new Color(1f, 0.7f, 0.7f, 1f);
        [SerializeField] private BattleUnitTypeConfig _playerUnitType;
        [SerializeField] private BattleUnitTypeConfig _enemyUnitType;

        [Header("Battlefield Ring")]
        [SerializeField] private BattlefieldRing _battlefieldRing;

        [Header("Demo Battle Stats")]
        [SerializeField] private float _attackResolveDelay = 0.45f;
        [SerializeField] private float _attackCooldown = 0.6f;
        [SerializeField] private float _seekDelay = 1.0f;
        [SerializeField] private float _deathDuration = 2.0f;

        private int _levelId;
        private bool _isInBattle;
        private Coroutine _battleCoroutine;
        private BattleFighter[] _playerFighters;
        private BattleFighter[] _enemyFighters;
        private BattleSimulation _simulation;
        private BattleFighterSpawnDefinition[] _playerFighterDefinitions;
        private int _enemyFighterCount;
        private UnitStaticAttributes? _enemyStaticAttributes;
        private BattleFighterSpawnDefinition[] _enemyDefinitions;
        private TerrainType _currentTerrain = TerrainType.Plain;
        private WeatherType _currentWeather = WeatherType.Sunny;
        private int _artifactAtkPerDeadCat;
        private int _artifactLeaderLastDeadCount;

        private int _lastEnemyDeathCount;

        // 胜利延迟
        private bool _victoryPending;
        private float _victoryTimer;
        private bool _victoryHealApplied;
        private const float VICTORY_DELAY = 1.5f;
        private const float VICTORY_HEAL_PERCENT = 0.2f;

        // 重新布阵按钮
        private GameObject _retryButtonGo;
        private GameObject _instantWinButtonGo;
        private GameObject _consumableBarGo;
        private GameObject _livesDisplayGo;
        private readonly List<ConsumableSlotUi> _consumableSlotUis = new List<ConsumableSlotUi>();
        private float _consumableSharedCooldownRemaining;
        private const float ConsumableSharedCooldown = 5f;
        private const string BattleUiFontAddress = "assets/bundle/font/fzy3k_gbk";

        // 区域遮罩系统
        private GameObject _overlay1Neutral, _overlay1Green, _overlay1Red;   // Layer 1, sortingOrder -999, 外圈
        private GameObject _overlay2Neutral, _overlay2Green, _overlay2Red;   // Layer 2, sortingOrder -998, 中圈
        private GameObject _overlay3Neutral, _overlay3Green, _overlay3Red;   // Layer 3, sortingOrder -997, 内圈

        // 椭圆区域边界（中心均为 0,0）
        public const float OUTER_A = 10.047f, OUTER_B = 4.629f;
        public const float MIDDLE_A = 6.175f,  MIDDLE_B = 3.21f;
        public const float INNER_A = 3.29f,    INNER_B = 1.905f;

        public System.Action<bool, int> BattleEnded;

        public bool IsInBattle => _isInBattle;
        public int LevelId => _levelId;
        public BattleFighter[] PlayerFighters => _playerFighters;
        public BattleFighter[] EnemyFighters => _enemyFighters;

        private sealed class ConsumableSlotUi
        {
            public ConsumableItem Item;
            public Button Button;
            public Image Background;
            public Text StateText;
        }

        public void Initialize(int levelId)
        {
            _levelId = levelId;
            Debug.Log($"[BattleManager] Initialized for level: {levelId}");
        }

        public void ConfigureDemoAvatars(AvatarAnimationDefinition playerDefinition, AvatarAnimationDefinition enemyDefinition)
        {
            _playerAvatarDefinition = playerDefinition;
            _enemyAvatarDefinition = enemyDefinition;
        }

        public void ConfigureFighterPrefab(GameObject fighterPrefab)
        {
            _fighterPrefab = fighterPrefab;
        }

        public void ConfigurePlayerFighters(BattleFighterSpawnDefinition[] playerFighterDefinitions)
        {
            _playerFighterDefinitions = playerFighterDefinitions;
        }

        public void ConfigureEnemyFighterCount(int enemyFighterCount)
        {
            _enemyFighterCount = Mathf.Max(1, enemyFighterCount);
        }

        public void ConfigureEnemyStats(UnitStaticAttributes stats)
        {
            _enemyStaticAttributes = stats;
        }

        public void ConfigureEnemyDefinitions(BattleFighterSpawnDefinition[] defs)
        {
            _enemyDefinitions = defs;
        }

        public void ConfigureTerrainWeather(TerrainType terrain, WeatherType weather)
        {
            _currentTerrain = terrain;
            _currentWeather = weather;
        }

        public void StartBattle()
        {
            if (_isInBattle)
            {
                return;
            }

            _isInBattle = true;
            Debug.Log("[BattleManager] Battle started");

            BuildDemoFighters();

            // 初始化奇物动态效果（亡猫之力等）— 从 runEquipments 读取
            InitArtifactEffects();

            // 应用地形/天气 BUFF 到玩家单位（通过运行时修正体系）
            ApplyTerrainWeatherBuffs();

            // 应用词缀 buff（从 ownedAffixes 读取并应用到所有友方单位）
            ApplyAffixBuffs();

            // 应用光环 buff（从 leader/cat 的 ActiveBuffs 传播到 RuntimeAttributes）
            ApplyAuraBuffs();

            // 同步所有 fighter 的 HUD 最大生命值（buff 可能改变了 MaxHp）
            SyncFighterHudMaxHp(_playerFighters);

            // 应用天生特殊 buff
            // 初始化战斗模拟
            _simulation = new BattleSimulation(
                _playerFighters,
                _enemyFighters,
                new BattleSimulationConfig
                {
                    AttackResolveDelay = _attackResolveDelay,
                    AttackCooldown = _attackCooldown,
                    SeekDelay = _seekDelay,
                    DeathDuration = _deathDuration
                });

            CreateConsumableBar();
            BattleSimulation.OnBulletFired += SpawnBullet;
            _battleCoroutine = StartCoroutine(DemoBattleLoop());
        }

        /// <summary>
        /// 准备阶段：只生成背景 + 敌方单位（不开始模拟）
        /// </summary>
        public void BuildPrepareScene()
        {
            ClearOldAvatars();
            SpawnBattleBackground();

            BattleSpawnResult result;

            if (_enemyDefinitions != null && _enemyDefinitions.Length > 0)
            {
                // 使用每个敌人的独立定义（支持混合敌人类型）
                result = BattleSpawner.SpawnEnemiesFromDefinitions(
                    transform,
                    _enemyDefinitions,
                    _enemyAvatarDefinition,
                    new BattleSpawnConfig
                    {
                        FighterPrefab = _fighterPrefab,
                        SpawnAreaMin = _spawnAreaMin,
                        SpawnAreaMax = _spawnAreaMax,
                        SpawnMinDistance = _spawnMinDistance,
                        SpawnTryCount = _spawnTryCount,
                        FighterScale = _fighterScale,
                        EnemyTint = _enemyTint,
                        EnemyUnitType = _enemyUnitType
                    });
            }
            else
            {
                // 使用统一属性（兼容旧配置）
                result = BattleSpawner.SpawnEnemiesOnly(
                    transform,
                    new BattleSpawnConfig
                    {
                        FighterPrefab = _fighterPrefab,
                        EnemyAvatarDefinition = _enemyAvatarDefinition,
                        EnemyFighterCount = _enemyFighterCount > 0 ? _enemyFighterCount : _fightersPerCamp,
                        SpawnAreaMin = _spawnAreaMin,
                        SpawnAreaMax = _spawnAreaMax,
                        SpawnMinDistance = _spawnMinDistance,
                        SpawnTryCount = _spawnTryCount,
                        FighterScale = _fighterScale,
                        EnemyTint = _enemyTint,
                        EnemyUnitType = _enemyUnitType,
                        EnemyStaticAttributes = _enemyStaticAttributes
                    });
            }

            _enemyFighters = result.EnemyFighters;
            _playerFighters = new BattleFighter[0];

            Debug.Log($"[BattleManager] Prepare scene: {_enemyFighters.Length} enemies placed (fromDefs={_enemyDefinitions != null})");
        }

        /// <summary>
        /// 准备阶段后：在指定位置添加玩家单位
        /// </summary>
        public void AddPlayerFighters(BattleFighterSpawnDefinition[] playerDefs, Vector3[] positions)
        {
            _playerFighters = new BattleFighter[playerDefs.Length];
            for (int i = 0; i < playerDefs.Length; i++)
            {
                Vector3 pos = i < positions.Length ? positions[i] : new Vector3(0f, 0f, 0f);
                _playerFighters[i] = BattleSpawner.CreateSingleFighter(
                    transform,
                    string.IsNullOrEmpty(playerDefs[i].Name) ? $"PlayerAvatar_{i + 1}" : playerDefs[i].Name,
                    BattleCamp.Player,
                    playerDefs[i],
                    pos,
                    _fighterScale,
                    _playerTint,
                    _playerUnitType);

                _playerFighters[i].Avatar?.LoadAndPlayIdle();
            }

            // 设置 Allies/Enemies 交叉引用
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                if (_playerFighters[i]?.RuntimeAttributes != null)
                {
                    _playerFighters[i].RuntimeAttributes.OwnerFighter = _playerFighters[i];
                    _playerFighters[i].RuntimeAttributes.Allies = _playerFighters;
                    _playerFighters[i].RuntimeAttributes.Enemies = _enemyFighters;
                }
            }
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                if (_enemyFighters[i]?.RuntimeAttributes != null)
                {
                    _enemyFighters[i].RuntimeAttributes.OwnerFighter = _enemyFighters[i];
                    _enemyFighters[i].RuntimeAttributes.Allies = _enemyFighters;
                    _enemyFighters[i].RuntimeAttributes.Enemies = _playerFighters;
                }
            }

            Debug.Log($"[BattleManager] Added {_playerFighters.Length} player fighters");
        }

        /// <summary>
        /// 从准备阶段开始战斗（玩家已摆放完毕）
        /// </summary>
        public void StartBattleFromPrepare()
        {
            _isInBattle = true;

            // 初始化奇物动态效果
            InitArtifactEffects();

            // 应用地形/天气 BUFF
            ApplyTerrainWeatherBuffs();

            // 应用词缀 buff
            ApplyAffixBuffs();

            // 同步 HUD
            SyncFighterHudMaxHp(_playerFighters);

            // 初始化战斗模拟
            _simulation = new BattleSimulation(
                _playerFighters,
                _enemyFighters,
                new BattleSimulationConfig
                {
                    AttackResolveDelay = _attackResolveDelay,
                    AttackCooldown = _attackCooldown,
                    SeekDelay = _seekDelay,
                    DeathDuration = _deathDuration
                });

            CreateConsumableBar();
            BattleSimulation.OnBulletFired += SpawnBullet;
            _battleCoroutine = StartCoroutine(DemoBattleLoop());

            // 显示重新布阵按钮
            CreateRetryButton();

            // 显示红心
            CreateLivesDisplay();

            Debug.Log("[BattleManager] Battle started from prepare");
        }

        public void EndBattle(bool victory)
        {
            if (!_isInBattle)
            {
                return;
            }

            _isInBattle = false;
            BattleSimulation.OnBulletFired -= SpawnBullet;
            BattleSimulation.ClearAllHitEffects();

            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }

            if (victory)
            {
                Debug.Log("[BattleManager] Battle ended - Victory!");
            }
            else
            {
                Debug.Log("[BattleManager] Battle ended - Defeat!");
            }

            // Battle summary log
            LogBattleSummary(victory);

            // 将战斗内 Persistent buff 同步回 FighterData（饱食层等）
            BattleBuffService.SyncPersistentBuffsToUnits(_playerFighters);

            // 同步战斗后的HP状态回FighterData
            SyncHealthToFighterData(victory);

            // 清除所有战斗内 buff（BattleOnly 类型）
            BuffService.ClearAllBattleBuffs();

            // 清理尸体和召唤物
            _simulation?.CorpseManager?.Clear();
            _simulation?.SummonManager?.Clear();

            // 处理HP持久化（战后回血等）
            // Boss关检测：通过 GameFlowController 的当前节点类型判断，而非仅检查最后一关
            var gfc = GameFlowController.Instance;
            bool isBossBattle = gfc != null &&
                gfc.CurrentRegionMap != null &&
                gfc.CurrentNodeId >= 0 &&
                gfc.CurrentRegionMap.GetNode(gfc.CurrentNodeId)?.nodeType == Camp.MapNodeType.Boss;
            var healthPersistence = new HealthPersistenceSystem();
            healthPersistence.OnBattleEnd(victory, isBossBattle);

            // Ensure settlement UI appears over a clean battlefield.
            // 注意：ClearBattlefield 会将 _playerFighters 置 null，
            // 必须在 BattleEnded 事件触发之后才能清理，否则外部无法收集战斗统计。
            BattleEnded?.Invoke(victory, 0);
            ClearBattlefield();
        }

        /// <summary>
        /// 取消战斗（重新布阵用）— 不同步 HP、不触发 BattleEnded、不应用战后效果
        /// </summary>
        public void CancelBattle()
        {
            if (!_isInBattle)
            {
                return;
            }

            _isInBattle = false;
            BattleSimulation.OnBulletFired -= SpawnBullet;
            BattleSimulation.ClearAllHitEffects();

            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }

            _simulation?.CorpseManager?.Clear();
            _simulation?.SummonManager?.Clear();

            ClearBattlefield();
            Debug.Log("[BattleManager] Battle cancelled (retry)");
        }

        public void PauseBattle()
        {
            Time.timeScale = 0;
            Debug.Log("[BattleManager] Battle paused");
        }

        public void ResumeBattle()
        {
            Time.timeScale = 1;
            Debug.Log("[BattleManager] Battle resumed");
        }

        public bool TryUseConsumable(ConsumableEffectType effectType)
        {
            if (_simulation == null || !_isInBattle)
            {
                Debug.LogWarning("[BattleManager] Cannot use consumable: not in battle");
                return false;
            }

            _simulation.ApplyConsumable(effectType);
            return true;
        }

        private void OnDestroy()
        {
            BattleSimulation.OnBulletFired -= SpawnBullet;
            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }
            DestroyConsumableBar();
        }

        /// <summary>
        /// 同步战斗后的HP状态回FighterData
        /// </summary>
        private void SyncHealthToFighterData(bool victory)
        {
            if (_playerFighters == null) return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            var tribes = dataManager?.PlayerData?.tribes;
            if (tribes == null) return;

            foreach (BattleFighter fighter in _playerFighters)
            {
                if (fighter == null || fighter.RuntimeAttributes == null) continue;

                // 查找对应的FighterData
                FighterData unit = FindUnit(tribes, fighter.TribeType, fighter.FighterId);
                if (unit == null) continue;

                // 同步HP
                if (fighter.IsDead || fighter.IsDying || fighter.IsRemoved)
                {
                    unit.currentHp = 0;
                }
                else
                {
                    unit.currentHp = fighter.RuntimeAttributes.CurrentHp;
                }
            }
        }

        /// <summary>
        /// 查找对应的FighterData
        /// </summary>
        private FighterData FindUnit(List<Camp.TribeRecord> tribes, Camp.TribeType tribeType, int fighterId)
        {
            foreach (var tribe in tribes)
            {
                if ((Camp.TribeType)tribe.tribeType != tribeType) continue;
                if (tribe.units == null) continue;

                foreach (var unit in tribe.units)
                {
                    if (unit.fighterId == fighterId)
                    {
                        return unit;
                    }
                }
            }
            return null;
        }

        private void BuildDemoFighters()
        {
            if (_playerAvatarDefinition == null || _enemyAvatarDefinition == null)
            {
                Debug.LogWarning("[BattleManager] AvatarAnimationDefinition missing. Please assign player/enemy definitions from BattlePanel.");
            }

            ClearOldAvatars();
            SpawnBattleBackground();

            // 初始化环形战场
            if (_battlefieldRing != null)
            {
                _battlefieldRing.Initialize(GetComponent<Canvas>(), transform as RectTransform);
            }

            BattleSpawnResult result;

            if (_enemyDefinitions != null && _enemyDefinitions.Length > 0)
            {
                // 混合敌人类型：分别生成玩家和敌人
                var playerResult = BattleSpawner.Spawn(
                    transform,
                    new BattleSpawnConfig
                    {
                        FighterPrefab = _fighterPrefab,
                        PlayerAvatarDefinition = _playerAvatarDefinition,
                        EnemyAvatarDefinition = _enemyAvatarDefinition,
                        FightersPerCamp = 0,
                        EnemyFighterCount = 0,
                        SpawnAreaMin = _spawnAreaMin,
                        SpawnAreaMax = _spawnAreaMax,
                        SpawnMinDistance = _spawnMinDistance,
                        SpawnTryCount = _spawnTryCount,
                        FighterScale = _fighterScale,
                        PlayerTint = _playerTint,
                        EnemyTint = _enemyTint,
                        PlayerFighterDefinitions = _playerFighterDefinitions,
                        PlayerUnitType = _playerUnitType,
                        EnemyUnitType = _enemyUnitType
                    });

                var enemyResult = BattleSpawner.SpawnEnemiesFromDefinitions(
                    transform,
                    _enemyDefinitions,
                    _enemyAvatarDefinition,
                    new BattleSpawnConfig
                    {
                        FighterPrefab = _fighterPrefab,
                        SpawnAreaMin = _spawnAreaMin,
                        SpawnAreaMax = _spawnAreaMax,
                        SpawnMinDistance = _spawnMinDistance,
                        SpawnTryCount = _spawnTryCount,
                        FighterScale = _fighterScale,
                        EnemyTint = _enemyTint,
                        EnemyUnitType = _enemyUnitType
                    });

                // 设置交叉引用
                var playerFighters = playerResult.PlayerFighters;
                var enemyFighters = enemyResult.EnemyFighters;
                for (int i = 0; i < playerFighters.Length; i++)
                {
                    if (playerFighters[i]?.RuntimeAttributes != null)
                    {
                        playerFighters[i].RuntimeAttributes.OwnerFighter = playerFighters[i];
                        playerFighters[i].RuntimeAttributes.Allies = playerFighters;
                        playerFighters[i].RuntimeAttributes.Enemies = enemyFighters;
                    }
                }
                for (int i = 0; i < enemyFighters.Length; i++)
                {
                    if (enemyFighters[i]?.RuntimeAttributes != null)
                    {
                        enemyFighters[i].RuntimeAttributes.OwnerFighter = enemyFighters[i];
                        enemyFighters[i].RuntimeAttributes.Allies = enemyFighters;
                        enemyFighters[i].RuntimeAttributes.Enemies = playerFighters;
                    }
                }

                _playerFighters = playerFighters;
                _enemyFighters = enemyFighters;
                result = new BattleSpawnResult { PlayerFighters = playerFighters, EnemyFighters = enemyFighters };
            }
            else
            {
                result = BattleSpawner.Spawn(
                    transform,
                    new BattleSpawnConfig
                    {
                        FighterPrefab = _fighterPrefab,
                        PlayerAvatarDefinition = _playerAvatarDefinition,
                        EnemyAvatarDefinition = _enemyAvatarDefinition,
                        FightersPerCamp = _fightersPerCamp,
                        EnemyFighterCount = _enemyFighterCount > 0 ? _enemyFighterCount : _fightersPerCamp,
                        SpawnAreaMin = _spawnAreaMin,
                        SpawnAreaMax = _spawnAreaMax,
                        SpawnMinDistance = _spawnMinDistance,
                        SpawnTryCount = _spawnTryCount,
                        FighterScale = _fighterScale,
                        PlayerTint = _playerTint,
                        EnemyTint = _enemyTint,
                        PlayerFighterDefinitions = _playerFighterDefinitions,
                        PlayerUnitType = _playerUnitType,
                        EnemyUnitType = _enemyUnitType,
                        EnemyStaticAttributes = _enemyStaticAttributes
                    });

                _playerFighters = result.PlayerFighters;
                _enemyFighters = result.EnemyFighters;
            }

            Debug.Log($"[BattleManager] Demo fighters ready. Player={_playerFighters.Length}, Enemy={_enemyFighters.Length} (fromDefs={_enemyDefinitions != null})");
        }

        private IEnumerator DemoBattleLoop()
        {
            if (_simulation == null || !_simulation.IsReady)
            {
                Debug.LogError("[BattleManager] Demo fighters are not ready.");
                EndBattle(false);
                yield break;
            }

            while (_isInBattle)
            {
                float dt = Time.deltaTime;
                UpdateConsumableCooldown(dt);

                // 动态更新奇物：每死一只小猫+攻击
                UpdateArtifactLeaderBuff();
                // 战斗内成长触发
                UpdateBattleGrowth();

                // 战斗模拟
                _simulation.Tick(dt, out bool _);

                // 检查敌方是否全灭（胜利条件）
                bool enemySoldiersAlive = AreSoldiersAlive(_enemyFighters);
                if (!enemySoldiersAlive && !_victoryPending)
                {
                    Debug.Log("[BattleManager] 敌方猫全部死亡，进入胜利延迟");
                    _victoryPending = true;
                    _victoryTimer = VICTORY_DELAY;
                    _victoryHealApplied = false;
                }

                // 胜利延迟：延迟结束后结算
                if (_victoryPending)
                {
                    if (!_victoryHealApplied)
                    {
                        ApplyVictoryHeal();
                        _victoryHealApplied = true;
                    }
                    _victoryTimer -= dt;
                    if (_victoryTimer <= 0f)
                    {
                        Debug.Log("[BattleManager] 胜利延迟结束，战斗结束");
                        EndBattle(true);
                        yield break;
                    }
                }

                // 检查我方是否全灭（失败条件）
                bool playerSoldiersAlive = AreSoldiersAlive(_playerFighters);
                if (!playerSoldiersAlive)
                {
                    Debug.Log("[BattleManager] 我方猫全部死亡，玩家失败");
                    EndBattle(false);
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>
        /// 检查是否有小兵存活
        /// </summary>
        private bool AreSoldiersAlive(BattleFighter[] fighters)
        {
            if (fighters == null) return false;

            for (int i = 0; i < fighters.Length; i++)
            {
                if (fighters[i] != null && fighters[i].IsAlive)
                    return true;
            }

            return false;
        }

        private void ClearOldAvatars()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private void SpawnBattleBackground()
        {
            var go = new GameObject("BattleBackground");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -1000;

            var handle = Addressables.LoadAssetAsync<Sprite>("map/bg");
            handle.Completed += op =>
            {
                if (op.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    sr.sprite = op.Result;
                else
                    Debug.LogWarning("[BattleManager] Failed to load battle background sprite");
            };

            // 区域遮罩覆盖层：Layer1(-999) 外圈, Layer2(-998) 中圈, Layer3(-997) 内圈
            _overlay1Neutral = CreateOverlay("ZoneOverlay_L1_Neutral", "map/1-1", -999, new Vector3(-0.05f, 0.18f, 0));
            _overlay1Green   = CreateOverlay("ZoneOverlay_L1_Green",   "map/1-2", -999, new Vector3(-0.05f, 0.18f, 0));
            _overlay1Red     = CreateOverlay("ZoneOverlay_L1_Red",     "map/1-3", -999, new Vector3(-0.05f, 0.18f, 0));
            _overlay2Neutral = CreateOverlay("ZoneOverlay_L2_Neutral", "map/2-1", -998, new Vector3(-0.03f, -0.34f, 0));
            _overlay2Green   = CreateOverlay("ZoneOverlay_L2_Green",   "map/2-2", -998, new Vector3(-0.03f, -0.34f, 0));
            _overlay2Red     = CreateOverlay("ZoneOverlay_L2_Red",     "map/2-3", -998, new Vector3(-0.03f, -0.34f, 0));
            _overlay3Neutral = CreateOverlay("ZoneOverlay_L3_Neutral", "map/3-1", -997, new Vector3(0f, -0.24f, 0));
            _overlay3Green   = CreateOverlay("ZoneOverlay_L3_Green",   "map/3-2", -997, new Vector3(0f, -0.24f, 0));
            _overlay3Red     = CreateOverlay("ZoneOverlay_L3_Red",     "map/3-3", -997, new Vector3(0f, -0.24f, 0));

            // 默认全部隐藏
            HideAllOverlays();
        }

        private GameObject CreateOverlay(string name, string spriteAddress, int sortingOrder, Vector3 offset)
        {
            var overlay = new GameObject(name);
            overlay.transform.SetParent(transform, false);
            overlay.transform.localPosition = offset;
            overlay.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

            var sr = overlay.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;

            var handle = Addressables.LoadAssetAsync<Sprite>(spriteAddress);
            handle.Completed += op =>
            {
                if (op.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    sr.sprite = op.Result;
                else
                    Debug.LogWarning($"[BattleManager] Failed to load overlay sprite: {spriteAddress}");
            };

            return overlay;
        }

        /// <summary>
        /// 判定世界坐标所在的部署区域（位标志：inner=1, middle=2, outer=4）
        /// </summary>
        public static int GetDeployZone(Vector3 pos)
        {
            float innerVal = (pos.x * pos.x) / (INNER_A * INNER_A) + (pos.y * pos.y) / (INNER_B * INNER_B);
            if (innerVal <= 1f) return 1;

            float middleVal = (pos.x * pos.x) / (MIDDLE_A * MIDDLE_A) + (pos.y * pos.y) / (MIDDLE_B * MIDDLE_B);
            if (middleVal <= 1f) return 2;

            float outerVal = (pos.x * pos.x) / (OUTER_A * OUTER_A) + (pos.y * pos.y) / (OUTER_B * OUTER_B);
            if (outerVal <= 1f) return 4;

            return 0;
        }

        /// <summary>
        /// 拖拽时实时高亮：hoveredRing 所在层显示绿/红，其余层显示原色
        /// hoveredRing: 1=内, 2=中, 4=外, 0=隐藏全部
        /// </summary>
        public void SetDragZoneHighlight(int hoveredZone, bool canDeploy)
        {
            if (hoveredZone == 0)
            {
                // 不在战场内：全部原色
                SetLayerState(1, "neutral");
                SetLayerState(2, "neutral");
                SetLayerState(3, "neutral");
                return;
            }

            string color = canDeploy ? "green" : "red";

            // Layer 1 (外圈): 只在 hoveredZone==4 时变色，否则原色
            SetLayerState(1, hoveredZone == 4 ? color : "neutral");
            // Layer 2 (中圈): 只在 hoveredZone==2 时变色，否则原色
            SetLayerState(2, hoveredZone == 2 ? color : "neutral");
            // Layer 3 (内圈): 只在 hoveredZone==1 时变色，否则原色
            SetLayerState(3, hoveredZone == 1 ? color : "neutral");
        }

        /// <summary>
        /// 隐藏所有遮罩层
        /// </summary>
        public void HideAllOverlays()
        {
            _overlay1Neutral.SetActive(false); _overlay1Green.SetActive(false); _overlay1Red.SetActive(false);
            _overlay2Neutral.SetActive(false); _overlay2Green.SetActive(false); _overlay2Red.SetActive(false);
            _overlay3Neutral.SetActive(false); _overlay3Green.SetActive(false); _overlay3Red.SetActive(false);
        }

        private void SetLayerState(int layer, string state)
        {
            GameObject neutral, green, red;
            if (layer == 1)      { neutral = _overlay1Neutral; green = _overlay1Green; red = _overlay1Red; }
            else if (layer == 2) { neutral = _overlay2Neutral; green = _overlay2Green; red = _overlay2Red; }
            else                 { neutral = _overlay3Neutral; green = _overlay3Green; red = _overlay3Red; }

            neutral.SetActive(state == "neutral");
            green.SetActive(state == "green");
            red.SetActive(state == "red");
        }

        /// <summary>
        /// 胜利延迟时：存活玩家单位回血 +20% MaxHp（战斗内视觉表现）
        /// </summary>
        private void ApplyVictoryHeal()
        {
            if (_playerFighters == null) return;

            for (int i = 0; i < _playerFighters.Length; i++)
            {
                BattleFighter fighter = _playerFighters[i];
                if (fighter == null || !fighter.IsAlive || fighter.RuntimeAttributes == null)
                    continue;

                int healAmount = Mathf.RoundToInt(fighter.RuntimeAttributes.MaxHp * VICTORY_HEAL_PERCENT);
                fighter.RuntimeAttributes.CurrentHp = Mathf.Min(fighter.RuntimeAttributes.CurrentHp + healAmount, fighter.RuntimeAttributes.MaxHp);

                // 更新 HUD 血条
                var hud = fighter.Transform?.GetComponent<FighterHUD>();
                if (hud != null)
                {
                    hud.UpdateHp(fighter.RuntimeAttributes.CurrentHp);
                }

                Debug.Log($"[BattleManager] {fighter.Name} 胜利回血 +{healAmount}，当前 HP={fighter.RuntimeAttributes.CurrentHp}/{fighter.RuntimeAttributes.MaxHp}");
            }
        }

        private void ClearBattlefield()
        {
            _simulation = null;
            _playerFighters = null;
            _enemyFighters = null;
            _artifactAtkPerDeadCat = 0;
            _artifactLeaderLastDeadCount = 0;
            _lastEnemyDeathCount = 0;
            _victoryPending = false;
            _victoryTimer = 0f;
            _victoryHealApplied = false;
            _consumableSharedCooldownRemaining = 0f;
            DestroyRetryButton();
            DestroyConsumableBar();
            DestroyLivesDisplay();
            ClearOldAvatars();
        }

        private void CreateConsumableBar()
        {
            DestroyConsumableBar();

            DataManager dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null)
            {
                return;
            }

            List<ConsumableItem> consumables = dataManager.GetConsumables();
            if (consumables == null || consumables.Count == 0)
            {
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            Transform topLayer = GetOrCreateTopLayer(canvas.transform);
            Font font = GameManager.Instance?.ResourceManager?.LoadResource<Font>(BattleUiFontAddress);

            _consumableBarGo = new GameObject("ConsumableBar", typeof(RectTransform), typeof(Image));
            _consumableBarGo.transform.SetParent(topLayer, false);

            RectTransform barRect = _consumableBarGo.GetComponent<RectTransform>();
            int itemCount = consumables.Count;
            float slotWidth = 150f;
            float spacing = 12f;
            float barWidth = 24f + itemCount * slotWidth + Mathf.Max(0, itemCount - 1) * spacing;
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 24f);
            barRect.sizeDelta = new Vector2(Mathf.Max(220f, barWidth), 96f);

            Image barBg = _consumableBarGo.GetComponent<Image>();
            barBg.color = new Color(0.08f, 0.08f, 0.08f, 0.82f);

            _consumableSlotUis.Clear();
            float startX = -((itemCount - 1) * (slotWidth + spacing)) * 0.5f;
            for (int i = 0; i < consumables.Count; i++)
            {
                ConsumableItem item = consumables[i];
                GameObject slotGo = new GameObject(
                    $"Consumable_{i}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                slotGo.transform.SetParent(_consumableBarGo.transform, false);

                RectTransform slotRect = slotGo.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.anchoredPosition = new Vector2(startX + i * (slotWidth + spacing), 0f);
                slotRect.sizeDelta = new Vector2(slotWidth, 68f);

                Image slotBg = slotGo.GetComponent<Image>();
                slotBg.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);

                Button button = slotGo.GetComponent<Button>();
                button.targetGraphic = slotBg;
                ConsumableItem clickItem = item;
                button.onClick.AddListener(() => TryUseConsumableItem(clickItem));

                CreateConsumableText(
                    slotGo.transform,
                    "Name",
                    font,
                    GetConsumableDisplayName(item),
                    20,
                    new Color(1f, 0.92f, 0.68f),
                    new Vector2(0f, 12f));

                Text stateText = CreateConsumableText(
                    slotGo.transform,
                    "State",
                    font,
                    "\u70B9\u51FB\u4F7F\u7528",
                    15,
                    new Color(0.82f, 0.82f, 0.82f),
                    new Vector2(0f, -16f));

                // \u9053\u5177\u6570\u91CF\u663E\u793A\uFF08\u53F3\u4E0B\u89D2\uFF09
                if (item.count > 1)
                {
                    CreateConsumableCountText(slotGo.transform, font, item.count);
                }

                _consumableSlotUis.Add(new ConsumableSlotUi
                {
                    Item = item,
                    Button = button,
                    Background = slotBg,
                    StateText = stateText
                });
            }

            RefreshConsumableBarState();
        }

        private Text CreateConsumableText(
            Transform parent,
            string name,
            Font font,
            string text,
            int fontSize,
            Color color,
            Vector2 anchoredPosition)
        {
            GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);

            RectTransform rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(130f, 28f);

            Text txt = textGo.GetComponent<Text>();
            txt.font = font;
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>
        /// 创建道具数量角标（右下角）
        /// </summary>
        private void CreateConsumableCountText(Transform parent, Font font, int count)
        {
            // 背景圆角
            GameObject bgGo = new GameObject("CountBg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(parent, false);
            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(1f, 0f);
            bgRect.anchorMax = new Vector2(1f, 0f);
            bgRect.pivot = new Vector2(1f, 0f);
            bgRect.anchoredPosition = new Vector2(-2f, 2f);
            bgRect.sizeDelta = new Vector2(32f, 24f);
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.9f, 0.2f, 0.2f, 0.95f);

            // 数量文字
            GameObject textGo = new GameObject("CountText", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(bgGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            Text txt = textGo.GetComponent<Text>();
            txt.font = font;
            txt.text = count.ToString();
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
        }

        private void TryUseConsumableItem(ConsumableItem item)
        {
            if (item == null)
            {
                return;
            }

            if (_consumableSharedCooldownRemaining > 0f)
            {
                GameLogger.Log("Combat", $"Consumable shared cooldown: {_consumableSharedCooldownRemaining:F1}s remaining");
                return;
            }

            if (!TryResolveConsumableEffectType(item, out ConsumableEffectType effectType))
            {
                GameLogger.LogError("Combat", $"Unknown consumable effect: id={item.id}, name={item.name}, effectType={item.effectType}, value={item.value}");
                return;
            }

            if (!TryUseConsumable(effectType))
            {
                return;
            }

            GameManager.Instance.DataManager.RemoveConsumable(item.id);
            _consumableSharedCooldownRemaining = ConsumableSharedCooldown;
            GameLogger.Log("Combat", $"Consumable used: {GetConsumableDisplayName(item)} id={item.id} effect={effectType}");
            CreateConsumableBar();
        }

        private void UpdateConsumableCooldown(float deltaTime)
        {
            if (_consumableSharedCooldownRemaining <= 0f)
            {
                return;
            }

            _consumableSharedCooldownRemaining = Mathf.Max(0f, _consumableSharedCooldownRemaining - deltaTime);
            RefreshConsumableBarState();
        }

        private void RefreshConsumableBarState()
        {
            bool isCoolingDown = _consumableSharedCooldownRemaining > 0f;
            for (int i = 0; i < _consumableSlotUis.Count; i++)
            {
                ConsumableSlotUi slot = _consumableSlotUis[i];
                if (slot == null || slot.Button == null || slot.Background == null)
                {
                    continue;
                }

                slot.Button.interactable = !isCoolingDown;
                slot.Background.color = isCoolingDown
                    ? new Color(0.16f, 0.16f, 0.16f, 0.92f)
                    : new Color(0.22f, 0.22f, 0.22f, 0.95f);

                if (slot.StateText != null)
                {
                    slot.StateText.text = isCoolingDown
                        ? $"CD {_consumableSharedCooldownRemaining:F1}s"
                        : "\u70B9\u51FB\u4F7F\u7528";
                    slot.StateText.color = isCoolingDown
                        ? new Color(0.58f, 0.84f, 1f)
                        : new Color(0.82f, 0.82f, 0.82f);
                }
            }
        }

        private void DestroyConsumableBar()
        {
            _consumableSlotUis.Clear();
            if (_consumableBarGo != null)
            {
                Destroy(_consumableBarGo);
                _consumableBarGo = null;
            }
        }

        private Transform GetOrCreateTopLayer(Transform canvasTransform)
        {
            Transform topLayer = canvasTransform.Find("Top");
            if (topLayer != null)
            {
                return topLayer;
            }

            GameObject topGo = new GameObject("Top", typeof(RectTransform));
            topGo.transform.SetParent(canvasTransform, false);
            RectTransform rectT = topGo.GetComponent<RectTransform>();
            rectT.anchorMin = Vector2.zero;
            rectT.anchorMax = Vector2.one;
            rectT.offsetMin = Vector2.zero;
            rectT.offsetMax = Vector2.zero;
            return topGo.transform;
        }

        private bool TryResolveConsumableEffectType(ConsumableItem item, out ConsumableEffectType effectType)
        {
            string name = item == null || string.IsNullOrEmpty(item.name)
                ? string.Empty
                : item.name.Trim().ToLowerInvariant();

            switch (name)
            {
                case "bomb":
                case "\u70B8\u5F39":
                    effectType = ConsumableEffectType.Bomb;
                    return true;
                case "freezetrap":
                case "\u51B0\u51BB\u9677\u9631":
                    effectType = ConsumableEffectType.FreezeTrap;
                    return true;
                case "healpotion":
                case "\u56DE\u590D\u836F\u6C34":
                case "\u6062\u590D\u836F\u6C34":
                    effectType = ConsumableEffectType.HealPotion;
                    return true;
                case "attackbuff":
                case "\u653B\u51FB\u5F3A\u5316":
                    effectType = ConsumableEffectType.AttackBuff;
                    return true;
                case "defensebuff":
                case "\u9632\u5FA1\u5F3A\u5316":
                    effectType = ConsumableEffectType.DefenseBuff;
                    return true;
            }

            if (item != null)
            {
                if (item.value >= 150f)
                {
                    effectType = ConsumableEffectType.Bomb;
                    return true;
                }

                if (item.value >= 40f)
                {
                    effectType = ConsumableEffectType.HealPotion;
                    return true;
                }

                if (item.value >= 2.5f)
                {
                    effectType = ConsumableEffectType.FreezeTrap;
                    return true;
                }

                switch (item.effectType)
                {
                    case 0:
                        effectType = item.value > 0f ? ConsumableEffectType.HealPotion : ConsumableEffectType.Bomb;
                        return true;
                    case 1:
                        effectType = item.value > 0f ? ConsumableEffectType.AttackBuff : ConsumableEffectType.FreezeTrap;
                        return true;
                    case 2:
                        effectType = item.value > 0f && item.value < 1f ? ConsumableEffectType.DefenseBuff : ConsumableEffectType.HealPotion;
                        return true;
                    case 3:
                        effectType = item.value >= 100f ? ConsumableEffectType.Bomb : ConsumableEffectType.AttackBuff;
                        return true;
                    case 4:
                        effectType = item.value >= 1f ? ConsumableEffectType.FreezeTrap : ConsumableEffectType.DefenseBuff;
                        return true;
                }
            }

            effectType = ConsumableEffectType.Bomb;
            return false;
        }

        private string GetConsumableDisplayName(ConsumableItem item)
        {
            if (item != null && !string.IsNullOrEmpty(item.name))
            {
                switch (item.name.Trim().ToLowerInvariant())
                {
                    case "bomb":
                        return "\u70B8\u5F39";
                    case "freezetrap":
                        return "\u51B0\u51BB\u9677\u9631";
                    case "healpotion":
                        return "\u56DE\u590D\u836F\u6C34";
                    case "attackbuff":
                        return "\u653B\u51FB\u5F3A\u5316";
                    case "defensebuff":
                        return "\u9632\u5FA1\u5F3A\u5316";
                    default:
                        return item.name;
                }
            }

            if (TryResolveConsumableEffectType(item, out ConsumableEffectType effectType))
            {
                switch (effectType)
                {
                    case ConsumableEffectType.Bomb:
                        return "\u70B8\u5F39";
                    case ConsumableEffectType.FreezeTrap:
                        return "\u51B0\u51BB\u9677\u9631";
                    case ConsumableEffectType.HealPotion:
                        return "\u56DE\u590D\u836F\u6C34";
                    case ConsumableEffectType.AttackBuff:
                        return "\u653B\u51FB\u5F3A\u5316";
                    case ConsumableEffectType.DefenseBuff:
                        return "\u9632\u5FA1\u5F3A\u5316";
                }
            }

            return "Consumable";
        }

        /// <summary>
        /// 创建红心显示（战斗UI我方UI上方）
        /// </summary>
        private void CreateLivesDisplay()
        {
            DestroyLivesDisplay();

            int livesRemaining = GameManager.Instance?.DataManager?.GetLivesRemaining() ?? 3;

            // 找到主 Canvas
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 找到或创建 Top 层
            var topLayer = GetOrCreateTopLayer(canvas.transform);

            _livesDisplayGo = new GameObject("LivesDisplay", typeof(RectTransform));
            _livesDisplayGo.transform.SetParent(topLayer, false);

            var rect = _livesDisplayGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 120);
            rect.sizeDelta = new Vector2(300, 50);

            // 创建3颗红心
            float heartSize = 40f;
            float spacing = 10f;
            float startX = -((3 - 1) * (heartSize + spacing)) * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                var heartGo = new GameObject($"Heart_{i}");
                heartGo.transform.SetParent(_livesDisplayGo.transform, false);
                var heartRect = heartGo.AddComponent<RectTransform>();
                heartRect.anchorMin = new Vector2(0.5f, 0.5f);
                heartRect.anchorMax = new Vector2(0.5f, 0.5f);
                heartRect.pivot = new Vector2(0.5f, 0.5f);
                heartRect.anchoredPosition = new Vector2(startX + i * (heartSize + spacing), 0);
                heartRect.sizeDelta = new Vector2(heartSize, heartSize);

                var heartImg = heartGo.AddComponent<Image>();
                // 根据红心数量设置颜色：有红心为红色，无红心为灰色
                heartImg.color = i < livesRemaining ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);
            }
        }

        /// <summary>
        /// 销毁红心显示
        /// </summary>
        private void DestroyLivesDisplay()
        {
            if (_livesDisplayGo != null)
            {
                Destroy(_livesDisplayGo);
                _livesDisplayGo = null;
            }
        }

        /// <summary>
        /// 创建重新布阵按钮（战斗中显示，点击后返回布阵界面）
        /// </summary>
        private void CreateRetryButton()
        {
            DestroyRetryButton();

            // 找到主 Canvas
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 找到或创建 Top 层
            var topLayer = GetOrCreateTopLayer(canvas.transform);

            _retryButtonGo = new GameObject("RetryButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _retryButtonGo.transform.SetParent(topLayer, false);

            var rect = _retryButtonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(120, 40);

            var img = _retryButtonGo.GetComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var btn = _retryButtonGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                GameFlowController.Instance?.ReturnToPreparation();
            });

            // 按钮文字
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textGo.transform.SetParent(_retryButtonGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textGo.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "重新布阵";
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            CreateInstantWinButton(topLayer);
        }

        private void CreateInstantWinButton(Transform topLayer)
        {
            DestroyInstantWinButton();

            _instantWinButtonGo = new GameObject("InstantWinButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _instantWinButtonGo.transform.SetParent(topLayer, false);

            var rect = _instantWinButtonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-10, -10);
            rect.sizeDelta = new Vector2(120, 40);

            var img = _instantWinButtonGo.GetComponent<Image>();
            img.color = new Color(0.7f, 0.2f, 0.2f, 0.85f);

            var btn = _instantWinButtonGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                if (_isInBattle)
                {
                    EndBattle(true);
                }
            });

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textGo.transform.SetParent(_instantWinButtonGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textGo.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "直接获胜";
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private void DestroyInstantWinButton()
        {
            if (_instantWinButtonGo != null)
            {
                Destroy(_instantWinButtonGo);
                _instantWinButtonGo = null;
            }
        }

        private void DestroyRetryButton()
        {
            if (_retryButtonGo != null)
            {
                Destroy(_retryButtonGo);
                _retryButtonGo = null;
            }
            DestroyInstantWinButton();
        }

        /// <summary>
        /// 将地形/天气 BUFF 应用到所有玩家单位的运行时修正属性上
        /// </summary>
        private void ApplyTerrainWeatherBuffs()
        {
            if (_playerFighters == null || _playerFighters.Length == 0)
                return;

            for (int i = 0; i < _playerFighters.Length; i++)
            {
                BattleFighter fighter = _playerFighters[i];
                if (fighter == null || fighter.RuntimeAttributes == null)
                    continue;

                TerrainWeatherBuff buff = TribeBattleBuffProvider.GetBuff(
                    fighter.TribeType, _currentTerrain, _currentWeather);

                if (buff.IsNeutral)
                    continue;

                UnitRuntimeAttributes attrs = fighter.RuntimeAttributes;
                attrs.AttackPercentBuff += buff.attackPercent;
                attrs.DefensePercentBuff += buff.defensePercent;
                attrs.HpPercentBuff += buff.hpPercent;
                attrs.SpeedPercentBuff += buff.speedPercent;
                attrs.Recalculate();
            }

            Debug.Log($"[BattleManager] Applied terrain/weather BUFFs: " +
                $"Terrain={_currentTerrain}, Weather={_currentWeather}");
        }

        /// <summary>
        /// 初始化奇物动态效果（亡猫之力等）— 从 runEquipments 读取特殊效果类型
        /// </summary>
        private void InitArtifactEffects()
        {
            if (_playerFighters == null || _playerFighters.Length == 0)
                return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var equipments = dataManager.PlayerData?.runEquipments;
            if (equipments == null || equipments.Count == 0)
                return;

            foreach (var equip in equipments)
            {
                if (equip.effects == null) continue;
                foreach (var eff in equip.effects)
                {
                    if (eff.gameEffectType < 0) continue;
                    switch ((Camp.GameEffect)eff.gameEffectType)
                    {
                        case Camp.GameEffect.LeaderAttackPerDeadCat:
                            _artifactAtkPerDeadCat += Mathf.RoundToInt(eff.value);
                            break;
                    }
                }
            }

            if (_artifactAtkPerDeadCat > 0)
                Debug.Log($"[BattleManager] InitArtifactEffects: 亡猫之力 atkPerDeadCat={_artifactAtkPerDeadCat}");
        }

        /// <summary>
        /// 应用光环 buff — 从 leader/cat 的 ActiveBuffs 传播到 RuntimeAttributes
        /// 注意：主要的 buff 传递已在 BattleSpawner.CreateFighter 中通过 AuraBuffs 参数完成。
        /// 此方法处理额外的非标准修正（如地形/天气等外部字段）。
        /// </summary>
        private void ApplyAuraBuffs()
        {
            // 光环 buff 已在 BattleSpawner.CreateFighter 中通过 AuraBuffs 参数应用到 RuntimeAttributes
            // 此方法保留用于未来的扩展需求
        }

        /// <summary>
        /// 应用词缀 buff — 从 playerData.ownedAffixes 读取词缀，应用到所有友方单位
        /// </summary>
        private void ApplyAffixBuffs()
        {
            if (_playerFighters == null || _playerFighters.Length == 0)
                return;

            DataManager dataManager = GameManager.Instance?.DataManager;
            if (dataManager == null) return;

            var ownedAffixes = dataManager.PlayerData?.ownedAffixes;
            if (ownedAffixes == null || ownedAffixes.Count == 0)
            {
                Debug.Log("[BattleManager] ApplyAffixBuffs: ownedAffixes is null or empty, skipping");
                return;
            }

            Debug.Log($"[BattleManager] ApplyAffixBuffs: ownedAffixes count={ownedAffixes.Count}, ids=[{string.Join(",", ownedAffixes)}]");

            // 加载词缀数据
            var allAffixes = LoadAllAffixes();
            if (allAffixes == null || allAffixes.Count == 0)
            {
                Debug.LogWarning("[BattleManager] ApplyAffixBuffs: failed to load affix config");
                return;
            }

            Debug.Log($"[BattleManager] ApplyAffixBuffs: loaded {allAffixes.Count} affixes from config");

            // 汇总所有词缀的效果（只应用 fighterId=0 的通用词缀）
            float atkFlatBonus = 0f, defFlatBonus = 0f, hpFlatBonus = 0f;
            float atkPercentBonus = 0f, defPercentBonus = 0f, hpPercentBonus = 0f, spdPercentBonus = 0f;

            foreach (var affixId in ownedAffixes)
            {
                if (!allAffixes.TryGetValue(affixId, out var affix))
                {
                    Debug.LogWarning($"[BattleManager] ApplyAffixBuffs: affix '{affixId}' not found in config");
                    continue;
                }

                // 只应用通用词缀（fighterId=0），兵种词缀需要在创建 fighter 时单独处理
                if (affix.fighterId != 0)
                {
                    Debug.Log($"[BattleManager] ApplyAffixBuffs: skipping tribe-specific affix '{affixId}' (fighterId={affix.fighterId})");
                    continue;
                }

                Debug.Log($"[BattleManager] ApplyAffixBuffs: applying affix '{affixId}' ({affix.displayName})");

                var affixEffects = affix.ResolveEffects();
                if (affixEffects == null || affixEffects.Count == 0) continue;

                foreach (var eff in affixEffects)
                {
                    if (eff.isPercent)
                    {
                        switch (eff.statType)
                        {
                            case "Attack": atkPercentBonus += eff.value; break;
                            case "Defense": defPercentBonus += eff.value; break;
                            case "Hp": hpPercentBonus += eff.value; break;
                            case "MoveSpeed": spdPercentBonus += eff.value; break;
                        }
                    }
                    else
                    {
                        switch (eff.statType)
                        {
                            case "Attack": atkFlatBonus += eff.value; break;
                            case "Defense": defFlatBonus += eff.value; break;
                            case "Hp": hpFlatBonus += eff.value; break;
                        }
                    }
                }
            }

            // 应用到所有玩家单位
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                BattleFighter fighter = _playerFighters[i];
                if (fighter == null || fighter.RuntimeAttributes == null)
                    continue;

                UnitRuntimeAttributes attrs = fighter.RuntimeAttributes;
                attrs.AttackFlatBuff += (int)atkFlatBonus;
                attrs.DefenseFlatBuff += (int)defFlatBonus;
                attrs.HpFlatBuff += (int)hpFlatBonus;
                attrs.AttackPercentBuff += atkPercentBonus;
                attrs.DefensePercentBuff += defPercentBonus;
                attrs.HpPercentBuff += hpPercentBonus;
                attrs.SpeedPercentBuff += spdPercentBonus;

                attrs.Recalculate();
            }

            if (atkFlatBonus != 0 || defFlatBonus != 0 || hpFlatBonus != 0 ||
                atkPercentBonus != 0 || defPercentBonus != 0 || hpPercentBonus != 0 || spdPercentBonus != 0)
            {
                Debug.Log($"[BattleManager] Applied affix BUFFs: ATK+{atkFlatBonus}({atkPercentBonus:P0}) DEF+{defFlatBonus}({defPercentBonus:P0}) HP+{hpFlatBonus}({hpPercentBonus:P0}) SPD+{spdPercentBonus:P0}");
            }
        }

        /// <summary>
        /// 同步所有 fighter 的 HUD 最大生命值（buff 可能改变了 MaxHp）
        /// </summary>
        private void SyncFighterHudMaxHp(BattleFighter[] fighters)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f == null || f.Transform == null || f.RuntimeAttributes == null) continue;
                var hud = f.Transform.GetComponent<FighterHUD>();
                if (hud != null)
                {
                    hud.SetMaxHp(f.RuntimeAttributes.MaxHp);
                    hud.UpdateHp(f.RuntimeAttributes.CurrentHp);
                }
            }
        }

        /// <summary>
        /// 从 affix_config.json 加载所有词缀数据
        /// </summary>
        private Dictionary<string, Camp.AffixData> LoadAllAffixes()
        {
            var allAffixes = new Dictionary<string, Camp.AffixData>();
            try
            {
                string configPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Tables/affix_config.json");
                if (!System.IO.File.Exists(configPath))
                {
                    Debug.LogError($"[BattleManager] 词缀配置文件不存在: {configPath}");
                    return allAffixes;
                }

                string json = System.IO.File.ReadAllText(configPath);
                var root = LitJson.JsonMapper.ToObject(json);

                if (root != null && root.Keys.Contains("affixes"))
                {
                    var affixesJson = root["affixes"];
                    for (int i = 0; i < affixesJson.Count; i++)
                    {
                        var item = affixesJson[i];
                        var affix = new Camp.AffixData
                        {
                            affixId = ReadString(item, "affixId", ""),
                            displayName = ReadString(item, "displayName", ""),
                            fighterId = ReadInt(item, "fighterId", 0),
                            buffIds = new List<int>()
                        };

                        // 解析 buffIds
                        if (item.Keys.Contains("buffIds") && item["buffIds"].IsArray)
                        {
                            var buffIdsJson = item["buffIds"];
                            for (int b = 0; b < buffIdsJson.Count; b++)
                            {
                                if (int.TryParse(buffIdsJson[b].ToString(), out int buffId))
                                    affix.buffIds.Add(buffId);
                            }
                        }

                        // 从 buff_config 解析描述
                        affix.description = affix.ResolveDescription();

                        if (!string.IsNullOrEmpty(affix.affixId))
                        {
                            allAffixes[affix.affixId] = affix;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleManager] 加载词缀数据失败: {e.Message}");
            }

            return allAffixes;
        }

        private static string ReadString(LitJson.JsonData json, string key, string defaultValue)
        {
            return json.Keys.Contains(key) ? json[key].ToString() : defaultValue;
        }

        private static int ReadInt(LitJson.JsonData json, string key, int defaultValue)
        {
            return json.Keys.Contains(key) && int.TryParse(json[key].ToString(), out int v) ? v : defaultValue;
        }

        private static float ReadFloat(LitJson.JsonData json, string key, float defaultValue)
        {
            return json.Keys.Contains(key) && float.TryParse(json[key].ToString(), out float v) ? v : defaultValue;
        }

        private static bool ReadBool(LitJson.JsonData json, string key)
        {
            return json.Keys.Contains(key)
                && bool.TryParse(json[key].ToString(), out bool v)
                && v;
        }


        /// <summary>
        /// 动态更新奇物效果：每有一只死去的单位，所有存活单位增加攻击力
        /// </summary>
        private void UpdateArtifactLeaderBuff()
        {
            if (_artifactAtkPerDeadCat <= 0 || _playerFighters == null)
                return;

            // 统计已死亡的单位数量
            int deadCount = 0;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null) continue;
                if (f.IsDead || f.IsDying || f.IsRemoved) deadCount++;
            }

            // 数量没变化则跳过
            if (deadCount == _artifactLeaderLastDeadCount)
                return;

            // 回退旧的 buff，应用新的到所有存活单位
            int delta = deadCount - _artifactLeaderLastDeadCount;
            _artifactLeaderLastDeadCount = deadCount;

            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || f.IsDead || f.IsRemoved) continue;
                UnitRuntimeAttributes attrs = f.RuntimeAttributes;
                if (attrs == null) continue;
                attrs.AttackFlatBuff += delta * _artifactAtkPerDeadCat;
                attrs.Recalculate();
            }

            Debug.Log($"[BattleManager] 奇物(亡猫之力)更新: {deadCount}只单位死亡，+{delta * _artifactAtkPerDeadCat}攻击");
        }

        /// <summary>
        /// 战斗内成长触发：检测敌人死亡，为橘猫族长添加饱食层
        /// </summary>
        private void UpdateBattleGrowth()
        {
            if (_enemyFighters == null || _playerFighters == null) return;

            // 统计当前死亡/已移除的敌人数量
            int enemyDeathCount = 0;
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                if (_enemyFighters[i] != null && (_enemyFighters[i].IsDying || _enemyFighters[i].IsRemoved))
                    enemyDeathCount++;
            }

            if (enemyDeathCount <= _lastEnemyDeathCount) return;
            int newKills = enemyDeathCount - _lastEnemyDeathCount;
            _lastEnemyDeathCount = enemyDeathCount;

            // 找到橘猫单位
            BattleFighter orangeUnit = null;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || !f.IsAlive) continue;
                if (f.TribeType != TribeType.Orange) continue;
                if (f.RuntimeAttributes == null) continue;
                orangeUnit = f;
                break;
            }
            if (orangeUnit == null) return;

            // 旧饱食机制已移除，改为被动技能系统处理
        }

        /// <summary>
        /// 生成子弹（狸花远程攻击）
        /// </summary>
        private void SpawnBullet(BulletData data)
        {
            if (data.Attacker == null || data.Target == null) return;

            GameObject bulletGo = new GameObject("Bullet");
            bulletGo.transform.position = data.Attacker.Transform.position;
            bulletGo.transform.SetParent(transform);

            var bullet = bulletGo.AddComponent<BattleBullet>();
            bullet.Setup(data.Attacker, data.Target, data.Damage, data.IsCritical);
        }

        private void LogBattleSummary(bool victory)
        {
            if (_playerFighters == null || _enemyFighters == null) return;

            int pAlive = 0, pDead = 0;
            int pTotalHp = 0, pMaxHp = 0;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null) continue;
                pMaxHp += f.StaticAttributes.MaxHp;
                if (f.IsRemoved || f.IsDying)
                {
                    pDead++;
                }
                else
                {
                    pAlive++;
                    pTotalHp += f.CurrentHp;
                }
            }

            int eAlive = 0, eDead = 0;
            int eTotalHp = 0, eMaxHp = 0;
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                var f = _enemyFighters[i];
                if (f == null) continue;
                eMaxHp += f.StaticAttributes.MaxHp;
                if (f.IsRemoved || f.IsDying)
                {
                    eDead++;
                }
                else
                {
                    eAlive++;
                    eTotalHp += f.CurrentHp;
                }
            }

            string firstPlayerStats = "";
            if (_playerFighters.Length > 0 && _playerFighters[0] != null)
            {
                var s = _playerFighters[0].StaticAttributes;
                firstPlayerStats = $" | Leader: ATK={s.Attack} DEF={s.Defense} HP={s.MaxHp} SPD={s.MoveSpeed}";
            }
            string firstEnemyStats = "";
            if (_enemyFighters.Length > 0 && _enemyFighters[0] != null)
            {
                var s = _enemyFighters[0].StaticAttributes;
                firstEnemyStats = $" | Enemy: ATK={s.Attack} DEF={s.Defense} HP={s.MaxHp} SPD={s.MoveSpeed}";
            }

            Debug.Log($"[BattleSummary] {(victory ? "WIN" : "LOSE")} | " +
                $"Player: {pAlive}/{_playerFighters.Length} alive, {pTotalHp}/{pMaxHp} HP{firstPlayerStats} | " +
                $"Enemy: {eAlive}/{_enemyFighters.Length} alive, {eTotalHp}/{eMaxHp} HP{firstEnemyStats}");
        }
    }
}
