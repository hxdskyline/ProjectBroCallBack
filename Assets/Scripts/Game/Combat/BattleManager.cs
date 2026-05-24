using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        [SerializeField] private float _fighterScale = 0.6f;
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
        private TerrainType _currentTerrain = TerrainType.Plain;
        private WeatherType _currentWeather = WeatherType.Sunny;
        private int _artifactAtkPerDeadCat;
        private int _artifactLeaderLastDeadCount;

        // 看板系统
        private BillboardSystem _billboardSystem;
        private int _lastEnemyDeathCount;
        private bool _soldiersPhaseEnded;
        private bool _enemyBillboardDestroyed;
        private bool _playerBillboardDestroyed;

        public System.Action<bool> BattleEnded;

        public bool IsInBattle => _isInBattle;
        public int LevelId => _levelId;
        public BattleFighter[] PlayerFighters => _playerFighters;
        public BattleFighter[] EnemyFighters => _enemyFighters;

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

            // 初始化看板系统
            InitializeBillboardSystem();

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

            BattleSimulation.OnBulletFired += SpawnBullet;
            _battleCoroutine = StartCoroutine(DemoBattleLoop());
        }

        public void EndBattle(bool victory)
        {
            if (!_isInBattle)
            {
                return;
            }

            _isInBattle = false;
            BattleSimulation.OnBulletFired -= SpawnBullet;

            // 清理看板系统
            CleanupBillboardSystem();

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

            // 处理HP持久化（满目疮痍debuff等）
            var campaign = GameManager.Instance?.BattleCampaignRuntime;
            bool isBossBattle = campaign != null && _levelId >= campaign.MaxBattleCount;
            var healthPersistence = new HealthPersistenceSystem();
            healthPersistence.OnBattleEnd(victory, isBossBattle);

            // Ensure settlement UI appears over a clean battlefield.
            ClearBattlefield();

            BattleEnded?.Invoke(victory);
        }

        /// <summary>
        /// 清理看板系统
        /// </summary>
        private void CleanupBillboardSystem()
        {
            if (_billboardSystem != null)
            {
                // 取消订阅事件
                _billboardSystem.OnBillboardStateChanged -= OnBillboardStateChanged;
                _billboardSystem.OnCurrencyDropped -= OnCurrencyDropped;
                _billboardSystem.OnBillboardDamaged -= OnBillboardDamaged;
                _billboardSystem.OnBillboardDestroyed -= OnBillboardDestroyed;

                _billboardSystem = null;
            }
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

            BattleSpawnResult result = BattleSpawner.Spawn(
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

            Debug.Log($"[BattleManager] Demo fighters ready. Player={_playerFighters.Length}, Enemy={_enemyFighters.Length}");
        }

        /// <summary>
        /// 初始化看板系统
        /// </summary>
        private void InitializeBillboardSystem()
        {
            _billboardSystem = new BillboardSystem();

            // 设置看板位置（我方在左侧，敌方在右侧）
            Vector3 playerBillboardPos = new Vector3(-7f, 0f, 0f);
            Vector3 enemyBillboardPos = new Vector3(7f, 0f, 0f);
            _billboardSystem.Initialize(playerBillboardPos, enemyBillboardPos);

            // 订阅看板事件
            _billboardSystem.OnBillboardStateChanged += OnBillboardStateChanged;
            _billboardSystem.OnCurrencyDropped += OnCurrencyDropped;
            _billboardSystem.OnBillboardDamaged += OnBillboardDamaged;
            _billboardSystem.OnBillboardDestroyed += OnBillboardDestroyed;

            Debug.Log("[BattleManager] BillboardSystem initialized");
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

                // 动态更新奇物：每死一只小猫+攻击
                UpdateArtifactLeaderBuff();
                // 战斗内成长触发
                UpdateBattleGrowth();

                bool playerSoldiersAlive = AreSoldiersAlive(_playerFighters);
                bool enemySoldiersAlive = AreSoldiersAlive(_enemyFighters);

                // 更新看板状态（休眠/激活切换）
                if (_billboardSystem != null)
                {
                    _billboardSystem.Update(dt, playerSoldiersAlive, enemySoldiersAlive);
                }

                if (!_soldiersPhaseEnded)
                {
                    // 正常战斗阶段：双方小兵互殴
                    if (_simulation.Tick(dt, out bool playerVictory))
                    {
                        // 一方小兵全灭 → 进入看板阶段，不直接结束战斗
                        _soldiersPhaseEnded = true;
                        Debug.Log("[BattleManager] 小兵阶段结束，进入看板阶段");
                    }
                    else
                    {
                        // 正常阶段：看板攻击小兵
                        BillboardAttackOnSoldiers();
                    }
                }
                else
                {
                    // 看板阶段：存活小兵自动攻击看板，看板攻击小兵
                    HandleBillboardCombat(dt, playerSoldiersAlive, enemySoldiersAlive);
                }

                yield return null;
            }
        }

        /// <summary>
        /// 看板阶段：存活小兵自动攻击敌方看板，看板攻击小兵
        /// 文档要求：胜利条件=敌方小兵全灭+敌方看板被摧毁
        /// </summary>
        private void HandleBillboardCombat(float deltaTime, bool playerSoldiersAlive, bool enemySoldiersAlive)
        {
            if (_billboardSystem == null) return;

            bool enemySoldiersAllDead = !enemySoldiersAlive;
            bool playerSoldiersAllDead = !playerSoldiersAlive;

            // 存活小兵自动攻击敌方看板（简化DPS模型）
            if (enemySoldiersAllDead && !_enemyBillboardDestroyed && playerSoldiersAlive)
            {
                float dps = CalculateSurvivingSoldierDps(_playerFighters);
                if (dps > 0)
                {
                    _billboardSystem.DamageBillboard(BillboardCamp.Enemy, dps * deltaTime);
                }
            }

            if (playerSoldiersAllDead && !_playerBillboardDestroyed && enemySoldiersAlive)
            {
                float dps = CalculateSurvivingSoldierDps(_enemyFighters);
                if (dps > 0)
                {
                    _billboardSystem.DamageBillboard(BillboardCamp.Player, dps * deltaTime);
                }
            }

            // 看板攻击小兵
            BillboardAttackOnSoldiers();
        }

        /// <summary>
        /// 看板攻击小兵
        /// </summary>
        private void BillboardAttackOnSoldiers()
        {
            if (_billboardSystem == null) return;

            BillboardAttackResult playerAttack = _billboardSystem.Attack(BillboardCamp.Player,
                _enemyFighters != null ? new System.Collections.Generic.List<BattleFighter>(_enemyFighters) : new System.Collections.Generic.List<BattleFighter>());
            if (playerAttack != null && playerAttack.target != null)
            {
                ApplyBillboardDamage(playerAttack.target, playerAttack.damage);
            }

            BillboardAttackResult enemyAttack = _billboardSystem.Attack(BillboardCamp.Enemy,
                _playerFighters != null ? new System.Collections.Generic.List<BattleFighter>(_playerFighters) : new System.Collections.Generic.List<BattleFighter>());
            if (enemyAttack != null && enemyAttack.target != null)
            {
                ApplyBillboardDamage(enemyAttack.target, enemyAttack.damage);
            }
        }

        /// <summary>
        /// 计算存活小兵的总DPS（用于自动攻击看板）
        /// </summary>
        private float CalculateSurvivingSoldierDps(BattleFighter[] fighters)
        {
            if (fighters == null) return 0f;
            float totalDps = 0f;
            for (int i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
                float attackSpeed = Mathf.Max(0.1f, f.RuntimeAttributes.CorrectedAttackSpeed);
                totalDps += f.RuntimeAttributes.Attack / attackSpeed;
            }
            return totalDps;
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

        /// <summary>
        /// 应用看板伤害
        /// </summary>
        private void ApplyBillboardDamage(BattleFighter target, float damage)
        {
            if (target == null || target.IsDead || target.IsDying) return;

            target.RuntimeAttributes.CurrentHp -= Mathf.RoundToInt(damage);
            if (target.RuntimeAttributes.CurrentHp <= 0)
            {
                target.RuntimeAttributes.CurrentHp = 0;
                // 触发死亡
                _simulation?.StartDeath(target);
            }

            // 更新HUD
            var hud = target.Transform?.GetComponent<FighterHUD>();
            if (hud != null)
            {
                hud.UpdateHp(target.RuntimeAttributes.CurrentHp);
            }
        }

        /// <summary>
        /// 看板状态改变事件处理
        /// </summary>
        private void OnBillboardStateChanged(BillboardCamp camp, BillboardState state)
        {
            Debug.Log($"[BattleManager] 看板状态改变: {camp} -> {state}");

            // 更新环形战场UI中的看板状态
            if (_battlefieldRing != null)
            {
                bool isPlayer = camp == BillboardCamp.Player;
                bool isActive = state == BillboardState.Active;
                BillboardData billboardData = _billboardSystem.GetBillboard(camp);
                _battlefieldRing.UpdateBillboardState(isPlayer, isActive, billboardData.currentHp, billboardData.maxHp);
            }
        }

        /// <summary>
        /// 看板受到伤害事件处理
        /// </summary>
        private void OnBillboardDamaged(BillboardCamp camp, float damage)
        {
            Debug.Log($"[BattleManager] 看板受到伤害: {camp}, 伤害: {damage}");
        }

        /// <summary>
        /// 看板被摧毁事件处理
        /// </summary>
        private void OnBillboardDestroyed(BillboardCamp camp)
        {
            Debug.Log($"[BattleManager] 看板被摧毁: {camp}");

            // 看板被摧毁隐含对应方小兵已全灭（看板只在激活时可被攻击，激活条件=小兵全灭）
            // 因此看板被摧毁 = 文档要求的"小兵全灭 + 看板被摧毁"条件满足
            if (camp == BillboardCamp.Enemy)
            {
                _enemyBillboardDestroyed = true;
                EndBattle(true);
            }
            else if (camp == BillboardCamp.Player)
            {
                _playerBillboardDestroyed = true;
                EndBattle(false);
            }
        }

        /// <summary>
        /// 货币掉落事件处理
        /// </summary>
        private void OnCurrencyDropped(int amount)
        {
            Debug.Log($"[BattleManager] 货币掉落: {amount} 木天蓼叶");

            // 添加货币到玩家数据
            DataManager dataManager = GameManager.Instance?.DataManager;
            if (dataManager != null)
            {
                dataManager.AddCatFood(amount);
            }
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

            var handle = Addressables.LoadAssetAsync<Sprite>("ui/sprite/common/greenbg");
            handle.Completed += op =>
            {
                if (op.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    sr.sprite = op.Result;
                else
                    Debug.LogWarning("[BattleManager] Failed to load battle background sprite");
            };
        }

        private void ClearBattlefield()
        {
            _simulation = null;
            _playerFighters = null;
            _enemyFighters = null;
            _artifactAtkPerDeadCat = 0;
            _artifactLeaderLastDeadCount = 0;
            _lastEnemyDeathCount = 0;
            _soldiersPhaseEnded = false;
            _enemyBillboardDestroyed = false;
            _playerBillboardDestroyed = false;
            ClearOldAvatars();
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

            // 应用饱食层（Persistent buff，自动叠加）
            for (int k = 0; k < newKills; k++)
            {
                int prevMaxHp = orangeUnit.RuntimeAttributes.MaxHp;
                orangeUnit.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFullnessStack(60f, 4f));
                orangeUnit.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateFullnessAtkStack(4f));
                orangeUnit.RuntimeAttributes.Recalculate();
                orangeUnit.RuntimeAttributes.CurrentHp += orangeUnit.RuntimeAttributes.MaxHp - prevMaxHp;
            }
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
