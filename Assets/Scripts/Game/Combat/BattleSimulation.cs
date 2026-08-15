using UnityEngine;
using Camp;
using System;
using System.Collections.Generic;
using Combat.Fighter;
using Combat.Effects;
using Combat.SkillSystem;

namespace Combat
{
    public struct BulletData
    {
        public BattleFighter Attacker;
        public BattleFighter Target;
        public int Damage;
        public bool IsCritical;
    }

    public struct BattleSimulationConfig
    {
        public float AttackResolveDelay;
        public float AttackCooldown;
        public float SeekDelay;
        public float DeathDuration;
    }

    public class BattleSimulation
    {
        public static event Action<BulletData> OnBulletFired;

        private static readonly Color SlowTint = new Color(0.6f, 0.9f, 1f, 1f); // #99E6FF
        private static BattleSimulation _currentSimulation;
        private const string FrozenEffectAddress = "2deffect/frozen";
        private static Sprite _frozenEffectSprite;
        private static bool _frozenEffectSpriteLoaded;

        /// <summary>
        /// 褰撳墠鎴樻枟妯℃嫙瀹炰緥锛堜緵瀛愬脊绛夊閮ㄩ€昏緫璁块棶锛?
        /// </summary>
        public static BattleSimulation CurrentSimulation => _currentSimulation;

        /// <summary>
        /// 澶栭儴璋冪敤锛氬彂灏勫瓙寮癸紙渚?PassiveSkillSystem 绛変娇鐢級
        /// </summary>
        public static void FireBullet(BulletData data)
        {
            OnBulletFired?.Invoke(data);
        }

        public void NotifyConfirmedKill(BattleFighter killer, BattleFighter victim)
        {
            _passiveSkillSystem?.OnConfirmedKill(killer, victim);
        }

        /// <summary>
        /// 澶栭儴璋冪敤锛氬彂灏勫甫寮瑰皠鍥炶皟鐨勫瓙寮癸紙渚?PassiveSkillSystem 浣跨敤锛?
        /// 寮瑰皠瀛愬脊鍛戒腑鍚庝粠鍛戒腑浣嶇疆鍙戝皠鏂板瓙寮规墦鍙︿竴涓晫浜?
        /// </summary>
        public static void FireBulletWithBounce(BattleFighter attacker, BattleFighter target, int damage,
            System.Action<BattleFighter, BattleFighter, Vector3> onHitBounce)
        {
            // 鍏堥€氳繃姝ｅ父娴佺▼鍒涘缓瀛愬脊锛屼絾杩欓噷闇€瑕佺洿鎺ュ垱寤?BattleBullet
            // 鐢变簬 OnBulletFired 浜嬩欢鐢?BattleManager.SpawnBullet 澶勭悊锛屼笉鏀寔鍥炶皟
            // 鎵€浠ヨ繖閲岀洿鎺ュ垱寤?GameObject
            if (attacker?.Transform == null || target == null) return;

            var go = new GameObject("BounceBullet");
            go.transform.position = attacker.Transform.position;

            var bullet = go.AddComponent<Combat.Fighter.BattleBullet>();
            bullet.Setup(attacker, target, damage, false, onHitBounce);
        }

        private readonly BattleFighter[] _playerFighters;
        private readonly BattleFighter[] _enemyFighters;
        private readonly BattleSimulationConfig _config;

        private float _battleElapsed;
        private CorpseManager _corpseManager;
        private SummonManager _summonManager;
        private PassiveSkillSystem _passiveSkillSystem;
        private ComboSkillSystem _comboSkillSystem;
        private BattleSkillRuntime _skillRuntime;
        private float _comboCheckTimer;
        private static readonly Dictionary<BattleFighter, float> _hitEffectTimers = new Dictionary<BattleFighter, float>();
        private static readonly Dictionary<BattleFighter, bool> _freezeVisualStates = new Dictionary<BattleFighter, bool>();

        public bool IsReady =>
            _playerFighters != null && _enemyFighters != null &&
            _playerFighters.Length > 0 && _enemyFighters.Length > 0;

        public BattleSimulation(BattleFighter[] playerFighters, BattleFighter[] enemyFighters, BattleSimulationConfig config)
        {
            _playerFighters = playerFighters;
            _enemyFighters = enemyFighters;
            _config = config;
            _battleElapsed = 0f;

            _corpseManager = new CorpseManager();
            _summonManager = new SummonManager();
            _summonManager.Initialize(playerFighters, enemyFighters);
            _passiveSkillSystem = new PassiveSkillSystem(playerFighters, enemyFighters, this);
            _passiveSkillSystem.InitializeSkills();
            _comboSkillSystem = new ComboSkillSystem();
            _skillRuntime = new BattleSkillRuntime(this);
            _skillRuntime.RegisterFighters(playerFighters);
            _skillRuntime.RegisterFighters(enemyFighters);
            NotifyBattleStart(playerFighters);
            NotifyBattleStart(enemyFighters);
            _currentSimulation = this;
        }

        /// <summary>
        /// 鑾峰彇杩炴惡鎶€绯荤粺
        /// </summary>
        public ComboSkillSystem ComboSkillSystem => _comboSkillSystem;

        /// <summary>
        /// 鑾峰彇灏镐綋绠＄悊鍣?
        /// </summary>
        public CorpseManager CorpseManager => _corpseManager;

        /// <summary>
        /// 鑾峰彇鍙敜鐗╃鐞嗗櫒
        /// </summary>
        public SummonManager SummonManager => _summonManager;

        /// <summary>
        /// 鏂芥斁娑堣€楀搧鏁堟灉锛堝鍏ㄤ綋鐩爣鐢熸晥锛屾棤闇€閫変綅锛?
        /// </summary>
        public void ApplyConsumable(ConsumableEffectType effectType)
        {
            switch (effectType)
            {
                case ConsumableEffectType.Bomb:
                    ApplyBomb();
                    break;
                case ConsumableEffectType.FreezeTrap:
                    ApplyFreezeTrap();
                    break;
                case ConsumableEffectType.HealPotion:
                    ApplyHealPotion();
                    break;
                case ConsumableEffectType.AttackBuff:
                    ApplyAttackBuff();
                    break;
                case ConsumableEffectType.DefenseBuff:
                    ApplyDefenseBuff();
                    break;
            }
        }

        private void ApplyBomb()
        {
            if (_enemyFighters == null) return;

            // 统计存活敌人数量
            int aliveCount = 0;
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                var f = _enemyFighters[i];
                if (f != null && f.IsAlive && f.RuntimeAttributes != null)
                    aliveCount++;
            }
            if (aliveCount == 0) return;

            // 总伤害500，由所有存活敌人平摊
            int totalDamage = 500;
            int damagePerEnemy = totalDamage / aliveCount;

            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                var f = _enemyFighters[i];
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
                f.RuntimeAttributes.CurrentHp = Mathf.Max(0, f.RuntimeAttributes.CurrentHp - damagePerEnemy);
                RefreshFighterHud(f, true, damagePerEnemy);
                if (f.RuntimeAttributes.CurrentHp <= 0) StartDeath(f);
            }
            GameLogger.Log("Combat", $"Consumable Bomb used: {totalDamage} total damage, {damagePerEnemy} per enemy ({aliveCount} enemies)");
        }

        private void ApplyFreezeTrap()
        {
            if (_enemyFighters == null) return;
            GameLogger.LogFileOnly("Combat", $"FreezeTrap begin enemyCount={_enemyFighters.Length} playerCount={(_playerFighters == null ? 0 : _playerFighters.Length)}");
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                var f = _enemyFighters[i];
                if (f == null || !f.IsAlive) continue;
                GameLogger.LogFileOnly("Combat", $"FreezeTrap target enemy[{i}] name={f.Name} camp={f.Camp} fighterId={f.FighterId}");
                f.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateFreeze(3f));
                f.FreezeTimer = Mathf.Max(f.FreezeTimer, 3f);
            }
            if (_playerFighters != null)
            {
                for (int i = 0; i < _playerFighters.Length; i++)
                {
                    var f = _playerFighters[i];
                    if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
                    if (f.RuntimeAttributes.HasActiveEffect(GameEffect.Freeze))
                    {
                        GameLogger.LogWarningFileOnly("Combat", $"FreezeTrap aftercast playerFrozen name={f.Name} camp={f.Camp} fighterId={f.FighterId} freezeTimer={f.FreezeTimer:F2}");
                    }
                }
            }
            GameLogger.Log("Combat", "Consumable FreezeTrap used: freeze all enemies for 3s");
        }

        private void ApplyHealPotion()
        {
            if (_playerFighters == null) return;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
                int heal = Mathf.RoundToInt(f.RuntimeAttributes.MaxHp * 0.5f);
                int actualHeal = Mathf.Min(heal, f.RuntimeAttributes.MaxHp - f.RuntimeAttributes.CurrentHp);
                f.RuntimeAttributes.CurrentHp += actualHeal;
                f.TotalHealingDone += actualHeal;
                RefreshFighterHud(f, false, 0);
            }
            GameLogger.Log("Combat", "Consumable HealPotion used: heal all allies for 50% max HP");
        }

        private void ApplyAttackBuff()
        {
            if (_playerFighters == null) return;
            var buff = UnifiedBuff.CreateTimedBuff(
                "consumable_attack_buff", "鏀诲嚮寮哄寲",
                BuffSource.Consumable, "AttackBuff",
                StatType.Attack, true, 0.3f,
                15f, BuffStackRule.None, 1);
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
                f.RuntimeAttributes.ApplyBuff(buff);
                f.RuntimeAttributes.AttackPercentBuff += 0.3f;
                f.RuntimeAttributes.Recalculate();
            }
            Debug.Log("[Consumable] AttackBuff: +30% ATK for 15s");
        }

        private void ApplyDefenseBuff()
        {
            if (_playerFighters == null) return;
            var buff = UnifiedBuff.CreateTimedBuff(
                "consumable_defense_buff", "闃插尽寮哄寲",
                BuffSource.Consumable, "DefenseBuff",
                StatType.Defense, true, 0.3f,
                15f, BuffStackRule.None, 1);
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
                f.RuntimeAttributes.ApplyBuff(buff);
                f.RuntimeAttributes.DefensePercentBuff += 0.3f;
                f.RuntimeAttributes.Recalculate();
            }
            Debug.Log("[Consumable] DefenseBuff: +30% DEF for 15s");
        }

        private void UpdateTimers(float deltaTime)
        {
            // Freeze timers锛堜繚鐣欙紝鐢ㄤ簬闈?buff 绯荤粺鐨勫喕缁擄級
            UpdateFreezeTimers(_playerFighters, deltaTime);
            UpdateFreezeTimers(_enemyFighters, deltaTime);
        }

        /// <summary>
        /// Tick 鎵€鏈?fighter 鐨?UnifiedBuff锛氶€掑噺 duration銆佹墽琛?DoT銆佺Щ闄よ繃鏈?buff
        /// </summary>
        private void TickAllBuffs(float deltaTime)
        {
            TickFighterBuffs(_playerFighters, deltaTime);
            TickFighterBuffs(_enemyFighters, deltaTime);
        }

        private void TickFighterBuffs(BattleFighter[] fighters, float deltaTime)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;

                var result = f.RuntimeAttributes.TickBuffs(deltaTime);

                // 搴旂敤 DoT 浼ゅ
                if (result.dotDamage > 0)
                {
                    f.RuntimeAttributes.CurrentHp = Mathf.Max(0, f.RuntimeAttributes.CurrentHp - result.dotDamage);
                    f.TotalDamageTaken += result.dotDamage;
                    // 鏄剧ず浼ゅ鏁板瓧
                    if (f.Transform != null)
                    {
                        var hud = f.Transform.GetComponent<FighterHUD>();
                        if (hud != null)
                        {
                            hud.ShowDamage(result.dotDamage);
                            hud.UpdateHp(f.RuntimeAttributes.CurrentHp);
                        }
                    }
                    if (f.RuntimeAttributes.CurrentHp <= 0)
                        StartDeath(f);
                }

                // 搴旂敤鍐荤粨
                if (result.freezeDuration > 0f)
                {
                    f.FreezeTimer = Mathf.Max(f.FreezeTimer, result.freezeDuration);
                }
                else if (!f.RuntimeAttributes.HasActiveEffect(GameEffect.Freeze))
                {
                    f.FreezeTimer = 0f;
                    ClearFrozenVisual(f);
                }

                // 闇€瑕侀噸鏂拌绠楀睘鎬э紙鍑忛€熻繃鏈熺瓑锛?
                if (result.needsRecalculate)
                    f.RuntimeAttributes.Recalculate();

                // 妫€鏌ョ浉浣嶈浆绉?闅愬尶 buff 鏄惁杩囨湡锛屾竻闄ょ姸鎬佹爣璁?
                if (f.IsInvulnerable && !HasBuff(f, "phase_shift_ally") && !HasBuff(f, "phase_shift_enemy"))
                    f.IsInvulnerable = false;
                if (f.IsStealthed && !HasBuff(f, "stealth_atk") && !HasBuff(f, "stealth"))
                    f.IsStealthed = false;
            }
        }

        /// <summary>
        /// 妫€鏌?fighter 鏄惁鎷ユ湁鎸囧畾 buffId 鐨?buff
        /// </summary>
        private bool HasBuff(BattleFighter f, string buffId)
        {
            if (f?.RuntimeAttributes?.ActiveBuffs == null) return false;
            for (int i = 0; i < f.RuntimeAttributes.ActiveBuffs.Count; i++)
            {
                if (f.RuntimeAttributes.ActiveBuffs[i].buffId == buffId)
                    return true;
            }
            return false;
        }

        private void UpdateFreezeTimers(BattleFighter[] fighters, float deltaTime)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f != null && f.FreezeTimer > 0f)
                    f.FreezeTimer = Mathf.Max(0f, f.FreezeTimer - deltaTime);
            }
        }

        private void UpdateVisualEffects(BattleFighter[] fighters)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f == null || !f.IsAlive || f.Transform == null) continue;

                bool frozen = f.RuntimeAttributes != null
                    && f.RuntimeAttributes.HasActiveEffect(GameEffect.Freeze);

                bool previousFrozen;
                if (!_freezeVisualStates.TryGetValue(f, out previousFrozen) || previousFrozen != frozen)
                {
                    _freezeVisualStates[f] = frozen;
                    GameLogger.LogFileOnly("Combat",
                        $"FreezeState name={f.Name} camp={f.Camp} fighterId={f.FighterId} frozen={frozen} freezeTimer={f.FreezeTimer:F2} speedDebuff={(f.RuntimeAttributes == null ? 0f : f.RuntimeAttributes.SpeedPercentDebuff):F2}");
                }

                var sr = f.Transform.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = frozen ? SlowTint : Color.white;

                UpdateFrozenEffect(f, frozen);
            }
        }

        private static bool IsFrozen(BattleFighter fighter)
        {
            return fighter != null
                && fighter.RuntimeAttributes != null
                && fighter.RuntimeAttributes.HasActiveEffect(GameEffect.Freeze);
        }

        private static void ClearFrozenVisual(BattleFighter fighter)
        {
            if (fighter == null)
            {
                return;
            }

            _freezeVisualStates[fighter] = false;

            if (fighter.Transform != null)
            {
                var sr = fighter.Transform.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = Color.white;
                }
            }

            if (fighter.FrozenEffect != null)
            {
                fighter.FrozenEffect.SetActive(false);
            }
        }

        private static void UpdateFrozenEffect(BattleFighter fighter, bool isFrozen)
        {
            if (fighter?.FrozenEffect == null)
            {
                return;
            }

            if (!isFrozen || fighter.Transform == null || fighter.IsRemoved || fighter.IsDying)
            {
                fighter.FrozenEffect.SetActive(false);
                return;
            }

            if (!_frozenEffectSpriteLoaded)
            {
                _frozenEffectSpriteLoaded = true;
                var resourceManager = GameManager.Instance?.ResourceManager;
                if (resourceManager != null)
                {
                    _frozenEffectSprite = resourceManager.LoadSprite(FrozenEffectAddress);
                    GameLogger.LogFileOnly("Combat", _frozenEffectSprite != null
                        ? $"Frozen effect sprite loaded: {FrozenEffectAddress}"
                        : $"Frozen effect sprite load failed: {FrozenEffectAddress}");
                }
            }

            if (_frozenEffectSprite == null)
            {
                GameLogger.LogWarningFileOnly("Combat", $"Frozen effect sprite is null for fighter={fighter.Name}");
                return;
            }

            var sr = fighter.FrozenEffect.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                return;
            }

            var fighterSr = fighter.Transform.GetComponent<SpriteRenderer>();
            if (fighterSr != null)
            {
                sr.sortingOrder = fighterSr.sortingOrder + 1;
            }

            sr.sprite = _frozenEffectSprite;
            sr.flipX = fighter.Transform.localScale.x < 0f;
            if (!fighter.FrozenEffect.activeSelf)
            {
                GameLogger.LogFileOnly("Combat", $"FrozenEffectActive name={fighter.Name} camp={fighter.Camp} fighterId={fighter.FighterId}");
            }
            fighter.FrozenEffect.SetActive(true);
        }

        private static void RefreshFighterHud(BattleFighter fighter, bool showDamage, int damageAmount)
        {
            if (fighter?.Transform == null || fighter.RuntimeAttributes == null)
            {
                return;
            }

            var hud = fighter.Transform.GetComponent<FighterHUD>();
            if (hud == null)
            {
                return;
            }

            if (showDamage && damageAmount > 0)
            {
                hud.ShowDamage(damageAmount);
            }

            hud.UpdateHp(fighter.RuntimeAttributes.CurrentHp);
        }

        public bool Tick(float deltaTime, out bool playerVictory)
        {
            playerVictory = false;
            _battleElapsed += deltaTime;

            UpdateTimers(deltaTime);
            TickKnockbacks(_playerFighters, deltaTime);
            TickKnockbacks(_enemyFighters, deltaTime);
            TickKnockdowns(_playerFighters, deltaTime);
            TickKnockdowns(_enemyFighters, deltaTime);
            TickAllBuffs(deltaTime);
            _skillRuntime?.Tick(deltaTime);
            TickPassiveSkills(deltaTime);
            TickComboSkills(deltaTime);
            TickArtifactEffects();
            UpdateVisualEffects(_playerFighters);
            UpdateVisualEffects(_enemyFighters);
            UpdateHitEffects(deltaTime);
            _corpseManager?.Tick(deltaTime);
            _summonManager?.Tick(deltaTime);
            UpdatePendingHits(_playerFighters, deltaTime);
            UpdatePendingHits(_enemyFighters, deltaTime);
            UpdateDeathStates(_playerFighters, deltaTime);
            UpdateDeathStates(_enemyFighters, deltaTime);

            if (AreAllRemoved(_playerFighters) || AreAllRemoved(_enemyFighters))
            {
                playerVictory = AreAllRemoved(_enemyFighters) && !AreAllRemoved(_playerFighters);
                return true;
            }

            if (_battleElapsed >= _config.SeekDelay)
            {
                UpdateGroupAI(_playerFighters, _enemyFighters, deltaTime);
                UpdateGroupAI(_enemyFighters, _playerFighters, deltaTime);
            }
            else
            {
                PlayGroupIdle(_playerFighters);
                PlayGroupIdle(_enemyFighters);
            }

            return false;
        }

        private void PlayGroupIdle(BattleFighter[] fighters)
        {
            if (fighters == null)
            {
                return;
            }

            for (int i = 0; i < fighters.Length; i++)
            {
                fighters[i]?.Avatar?.PlayIdle();
            }
        }

        private void UpdateGroupAI(BattleFighter[] group, BattleFighter[] targets, float deltaTime)
        {
            if (group == null || targets == null)
            {
                return;
            }

            for (int i = 0; i < group.Length; i++)
            {
                BattleFighter self = group[i];
                if (self == null || !self.IsAlive)
                {
                    continue;
                }

                if (_passiveSkillSystem != null && _passiveSkillSystem.ShouldHoldPosition(self))
                {
                    self.PendingTarget = null;
                    self.Avatar?.PlayIdle();
                    continue;
                }

                // 鍊掑湴鐘舵€侊細鏃犳硶琛屽姩
                if (self.KnockdownTimer > 0f)
                {
                    self.PendingTarget = null;
                    continue;
                }

                BattleFighter target = IsAllyTargetSupport(self)
                    ? FindNearestAllyTarget(self, group)
                    : FindNearestTarget(self, targets);
                if (target != null)
                {
                    UpdateFighterAI(self, target, deltaTime);
                }
                else
                {
                    // No valid enemy remains (or all enemies are in death state), stop running and return to idle.
                    self.PendingTarget = null;
                    self.Avatar?.PlayIdle();
                }
            }
        }

        private BattleFighter FindNearestTarget(BattleFighter self, BattleFighter[] targets)
        {
            BattleFighter nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < targets.Length; i++)
            {
                BattleFighter candidate = targets[i];
                if (candidate == null || !candidate.IsAlive || candidate.Transform == null || self.Transform == null)
                {
                    continue;
                }

                // 闅愬尶鐘舵€侊細涓嶅彲琚€変负鐩爣
                if (candidate.IsStealthed)
                {
                    continue;
                }

                Vector3 delta = candidate.Transform.position - self.Transform.position;
                float sqr = delta.sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private BattleFighter FindNearestAllyTarget(BattleFighter self, BattleFighter[] allies)
        {
            BattleFighter nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < allies.Length; i++)
            {
                BattleFighter candidate = allies[i];
                if (candidate == null || candidate == self || !candidate.IsAlive ||
                    candidate.Transform == null || self.Transform == null)
                {
                    continue;
                }

                Vector3 delta = candidate.Transform.position - self.Transform.position;
                float sqr = delta.sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private void UpdateFighterAI(BattleFighter self, BattleFighter target, float deltaTime)
        {
            if (self == null || target == null || self.Transform == null || target.Transform == null)
            {
                return;
            }

            if (!self.IsAlive || !target.IsAlive)
            {
                return;
            }

            // Frozen: cannot move or attack
            if (IsFrozen(self))
            {
                self.Avatar?.PlayIdle();
                return;
            }

            if (self.AttackCooldownTimer > 0f)
            {
                self.AttackCooldownTimer -= deltaTime;
            }

            Vector3 toTarget = target.Transform.position - self.Transform.position;
            float distance = toTarget.magnitude;

            float attackRange = GetAttackRange(self);
            if (distance > attackRange)
            {
                Vector3 direction = toTarget.normalized;
                self.Transform.position += direction * (GetMoveSpeed(self) * deltaTime);
                UpdateFacing(self, direction.x);
                self.Avatar?.PlayRun();
                return;
            }

            UpdateFacing(self, toTarget.x);

            if (self.PendingHitTimer > 0f)
            {
                return;
            }

            if (self.AttackCooldownTimer <= 0f)
            {
                // 攻击冷却: attackSpeed为次/秒，冷却=1/attackSpeed
                float atkSpd = self.RuntimeAttributes?.CorrectedAttackSpeed ?? 1f;
                float attackCooldown = atkSpd > 0f ? 1f / atkSpd : 1f;
                self.AttackCooldownTimer = Mathf.Max(0.1f, attackCooldown);
                self.Avatar?.PlayAttackAndReturnIdle();

                // 鍑烘墜鏃惰Е鍙戣鍔ㄦ妧鑳斤紙鍒嗚绛夊湪鍑烘墜鏃跺垽瀹氭鐜囩殑鏁堟灉锛?
                _passiveSkillSystem?.OnAttackLaunch(self, target);
                _skillRuntime?.RaiseEvent(self, new SkillEventData(SkillEventType.AttackLaunch, self, target));

                if (IsAllyTargetSupport(self))
                {
                    self.PendingHitTimer = _config.AttackResolveDelay;
                    self.PendingTarget = target;
                    return;
                }

                // 远程单位：发射子弹，不走 PendingHit
                bool isRanged = self.Tags != null && self.Tags.Contains("ranged");
                if (isRanged)
                {
                    int damage = CalculateDamage(self, target);
                    OnBulletFired?.Invoke(new BulletData
                    {
                        Attacker = self,
                        Target = target,
                        Damage = damage,
                        IsCritical = false
                    });
                    return;
                }

                // 杩戞垬锛氳蛋 PendingHit
                self.PendingHitTimer = _config.AttackResolveDelay;
                self.PendingTarget = target;
                return;
            }

            self.Avatar?.PlayIdle();
        }

        private void UpdateFacing(BattleFighter fighter, float xDirection)
        {
            if (fighter == null || fighter.Transform == null)
            {
                return;
            }

            if (Mathf.Abs(xDirection) < 0.001f)
            {
                return;
            }

            float scale = Mathf.Max(0.1f, fighter.BaseScale);
            float signedX = xDirection >= 0f ? -scale : scale;
            Vector3 localScale = fighter.Transform.localScale;
            fighter.Transform.localScale = new Vector3(signedX, Mathf.Abs(localScale.y), 1f);
        }

        private void UpdatePendingHits(BattleFighter[] attackers, float deltaTime)
        {
            if (attackers == null)
            {
                return;
            }

            for (int i = 0; i < attackers.Length; i++)
            {
                UpdatePendingHit(attackers[i], deltaTime);
            }
        }

        private void UpdatePendingHit(BattleFighter attacker, float deltaTime)
        {
            if (attacker == null || attacker.PendingHitTimer <= 0f)
            {
                return;
            }

            attacker.PendingHitTimer -= deltaTime;
            if (attacker.PendingHitTimer > 0f)
            {
                return;
            }

            BattleFighter defender = attacker.PendingTarget;
            attacker.PendingTarget = null;

            if (defender == null || !defender.IsAlive)
            {
                return;
            }

            if (IsAllyTargetSupport(attacker) && IsSameCamp(attacker, defender))
            {
                _skillRuntime?.RaiseEvent(attacker, new SkillEventData(SkillEventType.AttackHit, attacker, defender));
                _passiveSkillSystem?.OnAttackHit(attacker, defender);
                return;
            }

            // 鐩镐綅杞Щ/鏃犳晫鐘舵€侊細璺宠繃浼ゅ
            if (defender.IsInvulnerable)
            {
                attacker.PendingTarget = null;
                return;
            }

            UnitRuntimeAttributes attackerRuntime = attacker.RuntimeAttributes;
            UnitRuntimeAttributes defenderRuntime = defender.RuntimeAttributes;
            if (attackerRuntime == null || defenderRuntime == null)
            {
                return;
            }

            // 鐙歌姳杩炲嚮锛氭敾鍑?娆?
            int hitCount = attacker.HasDoubleHit ? 2 : 1;

            for (int hit = 0; hit < hitCount; hit++)
            {
                if (!defender.IsAlive) break;

                // 闇€姹傚叕寮? FDMG = MAX[DMG * DR * SKILLMULT * PBUFF + ABUFF, 1] + TD
                // DMG = MAX(CATK - CDEF, 0)
                // DR = MAX(1 - CDEF/(CDEF+100), 0.2)
                int rawDmg = Mathf.Max(0, attackerRuntime.Attack - defenderRuntime.Defense);
                float dr = Mathf.Max(0.2f, 1f - (float)defenderRuntime.Defense / (defenderRuntime.Defense + 100f));
                float skillMult = attackerRuntime.SkillMultiplier;
                float dmgPercentMod = 1f + defenderRuntime.DamageReceivePercentBuff;
                int dmgFlatMod = defenderRuntime.DamageReceiveFlatBuff;
                float finalF = rawDmg * dr * skillMult * dmgPercentMod + dmgFlatMod;
                int damage = Mathf.Max(1, Mathf.RoundToInt(finalF)) + attackerRuntime.TrueDamage;
                int newHp = Mathf.Max(0, defenderRuntime.CurrentHp - damage);
                defenderRuntime.CurrentHp = newHp;

                // 鎴樻枟缁熻
                attacker.TotalDamageDealt += damage;
                defender.TotalDamageTaken += damage;

                // Show damage popup and update HUD if present
                if (defender != null && defender.Transform != null)
                {
                    var hud = defender.Transform.GetComponent<FighterHUD>();
                    if (hud != null)
                    {
                        hud.ShowDamage(damage);
                        hud.UpdateHp(defenderRuntime.CurrentHp);
                    }

                    ShowHitEffect(defender);
                }

                _skillRuntime?.RaiseEvent(attacker,
                    new SkillEventData(SkillEventType.AttackHit, attacker, defender, 0f, damage));
                _skillRuntime?.RaiseEvent(defender,
                    new SkillEventData(SkillEventType.ReceiveHit, attacker, defender, 0f, damage));
            }

            if (defenderRuntime.CurrentHp <= 0)
            {
                StartDeath(defender);
                if (defender.IsDying)
                    _passiveSkillSystem?.OnConfirmedKill(attacker, defender);
            }

            // 鏀诲嚮瑙﹀彂鐘舵€佹晥鏋?
            ApplyAttackTriggeredEffects(attacker, defender);

            // 鍑婚€€鍒ゅ畾锛堣繎鎴樿寖鍥村唴锛?
            ApplyMeleeKnockback(attacker, defender);

            // IBuffEffect.OnAttackHit 鍥炶皟锛堢┛鍒虹銆佹瘨绠瓑锛?
            attackerRuntime.TriggerAttackEffects(defender);
        }

        /// <summary>
        /// 姣忓抚妫€鏌ヨ繛鎼烘妧
        /// </summary>
        private void TickComboSkills(float deltaTime)
        {
            if (_comboSkillSystem == null) return;
            _comboSkillSystem.Update(deltaTime);

            _comboCheckTimer += deltaTime;
            if (_comboCheckTimer < 2f) return; // 姣?绉掓鏌ヤ竴娆?
            _comboCheckTimer = 0f;

            // 鏀堕泦瀛樻椿鐜╁鍗曚綅
            var alivePlayers = new List<BattleFighter>();
            if (_playerFighters != null)
            {
                foreach (var f in _playerFighters)
                {
                    if (f != null && f.IsAlive) alivePlayers.Add(f);
                }
            }

            var availableCombos = _comboSkillSystem.CheckAvailableCombos(alivePlayers);
            foreach (var combo in availableCombos)
            {
                if (combo.remainingCooldown <= 0)
                {
                    var aliveEnemies = new List<BattleFighter>();
                    if (_enemyFighters != null)
                    {
                        foreach (var f in _enemyFighters)
                        {
                            if (f != null && f.IsAlive) aliveEnemies.Add(f);
                        }
                    }
                    _comboSkillSystem.TriggerCombo(combo, alivePlayers, aliveEnemies);
                    GameLogger.Log("Combo", $"杩炴惡鎶€瑙﹀彂: {combo.config.skillName}");
                }
            }
        }

        /// <summary>
        /// 姣忓抚瑙﹀彂琚姩鎶€鑳?
        /// </summary>
        private void TickPassiveSkills(float deltaTime)
        {
            if (_passiveSkillSystem == null) return;
            TickPassiveSkillsForGroup(_playerFighters, deltaTime);
            TickPassiveSkillsForGroup(_enemyFighters, deltaTime);
        }

        private void TickPassiveSkillsForGroup(BattleFighter[] fighters, float deltaTime)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f == null || !f.IsAlive) continue;
                _passiveSkillSystem.OnTick(f, deltaTime);
            }
        }

        /// <summary>
        /// 鏀诲嚮鍛戒腑鏃惰Е鍙戣鍔ㄦ妧鑳?
        /// </summary>
        public static void ApplyAttackTriggeredEffects(BattleFighter attacker, BattleFighter defender)
        {
            if (attacker == null || defender == null || !defender.IsAlive) return;
            var attackerRuntime = attacker.RuntimeAttributes;
            var defenderRuntime = defender.RuntimeAttributes;
            if (attackerRuntime == null || defenderRuntime == null) return;

            // 瑙﹀彂琚姩鎶€鑳界郴缁燂細鏀诲嚮鍛戒腑
            var sim = _currentSimulation;
            if (sim?._passiveSkillSystem != null)
            {
                sim._passiveSkillSystem.OnAttackHit(attacker, defender);
            }

            // 姗樼尗琚敾鍑昏Е鍙?
            if (!string.IsNullOrEmpty(defender.SkillId) && defender.SkillId.StartsWith("jumao"))
            {
                sim?._passiveSkillSystem?.OnJuMaoHit(defender);
            }

            if (attackerRuntime.ActiveBuffs == null) return;

            for (int i = 0; i < attackerRuntime.ActiveBuffs.Count; i++)
            {
                var buff = attackerRuntime.ActiveBuffs[i];
                if (buff.IsExpired) continue;
                // 鍙鐞嗘垬鏂楀唴闄愭椂 buff 鐨勭姸鎬佹晥鏋滆Е鍙戯紝璺宠繃姘镐箙灞炴€?buff
                if (buff.persistence != Camp.BuffPersistence.BattleOnly) continue;

                switch (buff.gameEffect)
                {
                    case GameEffect.Poison:
                        // 鏀诲嚮闄勫姞姣掞細effectParam1 = 姣忕浼ゅ, effectParam2 = 鎸佺画鏃堕棿
                        var poisonBuff = StatusEffectFactory.CreatePoison(buff.effectParam1, buff.effectParam2);
                        // 妫€鏌ヨ崋妫樼帇鍐犲湥鐗╋細涓瘨鏃犻檺鍙犲姞
                        if (HasRelic("Relic_ThornCrown", attacker))
                        {
                            poisonBuff.maxStacks = 999;
                        }
                        defenderRuntime.ApplyBuff(poisonBuff);
                        break;
                    case GameEffect.Bleed:
                        // 鏀诲嚮闄勫姞娴佽
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateBleed(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.Burn:
                        // 鏀诲嚮闄勫姞鐕冪儳
                        if (defenderRuntime.HasActiveEffect(GameEffect.Freeze) && HasRelic("Relic_FrostSorrow", attacker))
                            defenderRuntime.MultiplyActiveEffectParam2(GameEffect.Freeze, 2f);
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateBurn(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.Slow:
                        // 鏀诲嚮闄勫姞鍑忛€?
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateSlow(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.HuntMark:
                        // 鏀诲嚮鏍囪鐩爣
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateHuntMark(buff.effectParam1, buff.effectParam2));
                        break;
                }
            }
        }

        /// <summary>
        /// 璁＄畻鍗曟鏀诲嚮浼ゅ
        /// </summary>
        private int CalculateDamage(BattleFighter attacker, BattleFighter defender)
        {
            UnitRuntimeAttributes attackerRuntime = attacker.RuntimeAttributes;
            UnitRuntimeAttributes defenderRuntime = defender.RuntimeAttributes;
            if (attackerRuntime == null || defenderRuntime == null) return 0;

            int rawDmg = Mathf.Max(0, attackerRuntime.Attack - defenderRuntime.Defense);
            float dr = Mathf.Max(0.2f, 1f - (float)defenderRuntime.Defense / (defenderRuntime.Defense + 100f));
            float skillMult = attackerRuntime.SkillMultiplier;
            float dmgPercentMod = 1f + defenderRuntime.DamageReceivePercentBuff;
            int dmgFlatMod = defenderRuntime.DamageReceiveFlatBuff;

            // 缂犵粫+浣嶇Щ鑱斿姩锛氳缂犵粫鐨勬晫浜哄彈鍒板嚮閫€/鍑诲€?鍑婚鏃?50%浼ゅ锛堟媺鏂激瀹筹級
            if (defenderRuntime.IsRooted && IsDisplacementAttack(attackerRuntime))
            {
                dmgPercentMod *= 1.5f;
                GameLogger.Log("Link", "缂犵粫+浣嶇Щ鑱斿姩: +50%鎷夋柇浼ゅ");
            }

            float finalF = rawDmg * dr * skillMult * dmgPercentMod + dmgFlatMod;
            return Mathf.Max(1, Mathf.RoundToInt(finalF)) + attackerRuntime.TrueDamage;
        }

        /// <summary>
        /// 鍒ゆ柇鏀诲嚮鑰呮槸鍚︽惡甯︿綅绉诲瀷鎺у埗鏁堟灉
        /// </summary>
        private bool IsDisplacementAttack(UnitRuntimeAttributes attackerRuntime)
        {
            if (attackerRuntime?.ActiveBuffs == null) return false;
            foreach (var buff in attackerRuntime.ActiveBuffs)
            {
                if (buff.IsExpired) continue;
                if (buff.gameEffect == GameEffect.KnockBack ||
                    buff.gameEffect == GameEffect.KnockDown ||
                    buff.gameEffect == GameEffect.KnockUp)
                    return true;
            }
            return false;
        }

        // 鈹€鈹€ 鍑婚€€绯荤粺 鈹€鈹€

        private const float KnockbackDistance = 1.5f;   // 鍑婚€€璺濈锛堢背锛?
        private const float KnockbackChance = 0.05f;    // 鍑婚€€姒傜巼 15%
        private const float MeleeRangeMax = 3.5f;        // 杩戞垬鑼冨洿涓婇檺

        private const float KnockbackSpeed = 12f;          // 鍒濆鍑婚€€閫熷害锛堢背/绉掞級
        private const float KnockbackDeceleration = 20f;    // 鍑忛€熷害锛堢背/绉捖诧級
        private const float KnockdownDuration = 1.0f;       // 鍊掑湴鎸佺画鏃堕棿锛堢锛?

        /// <summary>
        /// 鍑婚€€瑙勫垯锛氬湪杩戞垬鑼冨洿鍐呮瘡娆″懡涓兘鏈?5%姒傜巼鏂藉姞涓€涓甫鍑忛€熺殑鎺ㄥ姏
        /// 杩滅▼鍗曚綅杩涘叆杩戞垬鑼冨洿锛堣窛绂?鈮?3.5m锛夊悓鏍烽伒寰瑙勫垯
        /// </summary>
        public static void ApplyMeleeKnockback(BattleFighter attacker, BattleFighter defender)
        {
            if (attacker == null || defender == null || !defender.IsAlive) return;
            if (attacker.Transform == null || defender.Transform == null) return;
            // 姝ｅ湪鍊掑湴鏃朵笉閲嶅鍑婚€€
            if (defender.KnockdownTimer > 0f || defender.KnockbackRemaining > 0f) return;

            // 纭鍦ㄨ繎鎴樿寖鍥村唴
            float distance = Vector3.Distance(attacker.Transform.position, defender.Transform.position);
            if (distance > MeleeRangeMax) return;

            // 15% 姒傜巼鍑婚€€
            if (UnityEngine.Random.value >= KnockbackChance) return;

            // 鍑婚€€鏂瑰悜锛氫粠鏀诲嚮鑰呮寚鍚戦槻寰¤€咃紙杩滅鏀诲嚮鑰咃級
            Vector3 knockDir = (defender.Transform.position - attacker.Transform.position).normalized;
            if (knockDir.sqrMagnitude < 0.001f) knockDir = new Vector3(attacker.Transform.localScale.x > 0 ? 1 : -1, 0, 0);
            knockDir.y = 0; // 鍙湪姘村钩鏂瑰悜鍑婚€€

            // 缁欎竴涓垵閫熷害锛岀敱 TickKnockback 澶勭悊鍑忛€熻繍鍔?
            defender.KnockbackVelocity = knockDir * KnockbackSpeed;
            defender.KnockbackRemaining = KnockbackDistance;

            GameLogger.Log("Combat", $"鍑婚€€: {attacker.Name} 鍑婚€€ {defender.Name}");
        }

        /// <summary>
        /// 澶勭悊鍑婚€€浣嶇Щ锛氬寑鍑忛€熻繍鍔ㄧ洿鍒板仠涓?
        /// </summary>
        private void TickKnockback(BattleFighter fighter, float deltaTime)
        {
            if (fighter == null || fighter.Transform == null) return;
            if (fighter.KnockbackRemaining <= 0f) return;

            // 璁板綍鍑婚€€鏂瑰悜锛堢敤浜庡€掑湴锛?
            Vector3 knockDir = fighter.KnockbackVelocity.normalized;

            // 褰撳墠甯хЩ鍔ㄨ窛绂?= v鈧€t - 陆at虏锛屼絾涓嶈兘瓒呰繃鍓╀綑璺濈
            float v0 = fighter.KnockbackVelocity.magnitude;
            float moveDist = v0 * deltaTime - 0.5f * KnockbackDeceleration * deltaTime * deltaTime;
            moveDist = Mathf.Max(0f, moveDist);

            if (moveDist >= fighter.KnockbackRemaining || v0 <= 0f)
            {
                // 鍒拌揪缁堢偣锛屾竻闄ゅ嚮閫€鐘舵€?
                fighter.Transform.position += knockDir * fighter.KnockbackRemaining;
                fighter.KnockbackVelocity = Vector3.zero;
                fighter.KnockbackRemaining = 0f;

                // 瑙﹀彂鍊掑湴
                fighter.KnockdownTimer = KnockdownDuration;
                fighter.KnockdownDir = knockDir;
                return;
            }

            // 绉诲姩
            Vector3 delta = knockDir * moveDist;
            fighter.Transform.position += delta;
            fighter.KnockbackRemaining -= moveDist;

            // 鏇存柊閫熷害锛歷 = v鈧€ - at
            float newSpeed = Mathf.Max(0f, v0 - KnockbackDeceleration * deltaTime);
            fighter.KnockbackVelocity = knockDir * newSpeed;
        }

        private void TickKnockbacks(BattleFighter[] fighters, float deltaTime)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                if (fighters[i] != null && fighters[i].KnockbackRemaining > 0f)
                    TickKnockback(fighters[i], deltaTime);
            }
        }

        // 鈹€鈹€ 鍊掑湴绯荤粺 鈹€鈹€

        /// <summary>
        /// 澶勭悊鍊掑湴锛氳鑹插悜鍚庡€掍笅锛圶杞存棆杞級锛?绉掑悗鎭㈠绔欑珛
        /// </summary>
        private void TickKnockdown(BattleFighter fighter, float deltaTime)
        {
            if (fighter == null || fighter.Transform == null) return;

            if (!IsFrozen(fighter))
            {
                ClearFrozenVisual(fighter);
            }

            fighter.KnockdownTimer -= deltaTime;

            if (fighter.KnockdownTimer <= 0f)
            {
                // 鎭㈠绔欑珛锛氭棆杞洖鐩寸珛
                fighter.Transform.localEulerAngles = Vector3.zero;
                fighter.KnockdownDir = Vector3.zero;
                return;
            }

            // 鍊掑湴鍔ㄧ敾锛氬悜鍚庡€掍笅锛堢粫X杞存棆杞級锛岀敤 easing 妯℃嫙鍊掍笅鈫掕创鍦?
            float progress = 1f - (fighter.KnockdownTimer / KnockdownDuration); // 0鈫?
            // 鍓?0%鏃堕棿蹇€熷€掍笅锛屽悗70%淇濇寔璐村湴
            float leanAngle;
            if (progress < 0.3f)
            {
                // 鍊掍笅闃舵锛氬姞閫熷悗浠?
                float t = progress / 0.3f;
                leanAngle = -90f * (t * t); // 浜屾缂撳嚭锛屽悜鍚庡€?0搴?
            }
            else
            {
                // 璐村湴闃舵锛氫繚鎸佸钩韬?
                leanAngle = -90f;
            }

            // 鍊掑湴鏂瑰悜锛氭部鍑婚€€鏂瑰悜鍚戝悗鍊?
            float sign = fighter.KnockdownDir.x >= 0 ? 1f : -1f;
            fighter.Transform.localEulerAngles = new Vector3(0f, 0f, leanAngle * sign);
        }

        private void TickKnockdowns(BattleFighter[] fighters, float deltaTime)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                if (fighters[i] != null && fighters[i].KnockdownTimer > 0f)
                    TickKnockdown(fighters[i], deltaTime);
            }
        }

        /// <summary>
        /// 妫€鏌ョ帺瀹舵槸鍚︽嫢鏈夋寚瀹氬湥鐗?
        /// </summary>
        private static bool HasRelic(string relicId, BattleFighter fighter)
        {
            var dm = GameManager.Instance?.DataManager;
            if (dm == null) return false;
            var relics = dm.GetOwnedRelics();
            if (relics == null) return false;
            foreach (var relic in relics)
            {
                if (relic.relicId == relicId) return true;
            }
            return false;
        }

        private float GetMoveSpeed(BattleFighter fighter)
        {
            if (fighter?.RuntimeAttributes == null) return 2.2f;
            // 缂犵粫/鐪╂檿/鍐板喕/鍑诲€?鍑婚 鏃舵棤娉曠Щ鍔?
            if (fighter.RuntimeAttributes.IsRooted || fighter.RuntimeAttributes.IsStunned)
                return 0f;
            return Mathf.Max(0.001f, fighter.RuntimeAttributes.CorrectedMoveSpeed);
        }

        private float GetAttackRange(BattleFighter fighter)
        {
            return fighter?.RuntimeAttributes != null
                ? Mathf.Max(0.1f, fighter.RuntimeAttributes.AttackRange)
                : 1.0f;
        }

        private bool IsAllyTargetSupport(BattleFighter fighter)
        {
            if (fighter == null || fighter.FighterId <= 0)
            {
                return false;
            }

            FighterConfig config = TribeConfigLoader.Instance?.GetFighterConfig(fighter.FighterId);
            return config != null && config.targetPriority == "nearest_ally";
        }

        private static bool IsSameCamp(BattleFighter a, BattleFighter b)
        {
            return a != null && b != null && a.Camp == b.Camp;
        }

        private void NotifyBattleStart(BattleFighter[] fighters)
        {
            if (fighters == null)
            {
                return;
            }

            for (int i = 0; i < fighters.Length; i++)
            {
                BattleFighter fighter = fighters[i];
                if (fighter == null)
                {
                    continue;
                }

                _skillRuntime?.RaiseEvent(fighter, new SkillEventData(SkillEventType.BattleStart, fighter, fighter));
            }
        }

        public void StartDeath(BattleFighter fighter)
        {
            if (fighter == null || fighter.IsRemoved || fighter.IsDying)
            {
                Debug.Log($"[StartDeath] Skipped: fighter={fighter?.Name} isRemoved={fighter?.IsRemoved} isDying={fighter?.IsDying}");
                return;
            }

            if (_passiveSkillSystem != null && _passiveSkillSystem.TryCowLeaderRescue(fighter))
                return;

            Debug.Log($"[StartDeath] Killing {fighter.Name} fighterId={fighter.FighterId} hp={fighter.CurrentHp}");
            _skillRuntime?.RaiseEvent(fighter, new SkillEventData(SkillEventType.UnitDied, fighter, fighter));
            fighter.IsDying = true;
            fighter.PendingHitTimer = 0f;
            fighter.AttackCooldownTimer = 0f;
            fighter.PendingTarget = null;
            fighter.DeathTimer = Mathf.Max(0.1f, _config.DeathDuration);

            // 瑙﹀彂琚姩鎶€鑳斤細姝讳骸
            _passiveSkillSystem?.OnDeath(fighter);

            // 瑙﹀彂姝讳骸鑰呯殑 OnDeath 鍥炶皟
            fighter.RuntimeAttributes?.TriggerDeathEffects();

            // 瑙﹀彂鍑绘潃鍥炶皟 + 琚姩鎶€鑳斤細鍑绘潃
            if (!IsPlayerFighter(fighter))
            {
                for (int i = 0; i < _playerFighters.Length; i++)
                {
                    var pf = _playerFighters[i];
                    if (pf != null && pf.IsAlive)
                    {
                        pf.RuntimeAttributes?.TriggerKillEffects(fighter);
                        _passiveSkillSystem?.OnKill(pf, fighter);
                        _skillRuntime?.RaiseEvent(pf, new SkillEventData(SkillEventType.UnitKilled, pf, fighter));
                    }
                }
            }

            // 璁板綍灏镐綋
            bool isPlayerUnit = IsPlayerFighter(fighter);
            Vector3 deathPos = fighter.Transform != null ? fighter.Transform.position : Vector3.zero;
            _corpseManager?.AddCorpse(fighter, deathPos, isPlayerUnit);

            // Keep death presentation consistent: face left from death start until removal.
            if (fighter.Transform != null)
            {
                float scale = Mathf.Max(0.1f, fighter.BaseScale);
                Vector3 localScale = fighter.Transform.localScale;
                fighter.Transform.localScale = new Vector3(scale, Mathf.Abs(localScale.y), 1f);
            }

            fighter.Avatar?.PlayDeath();

            // 瑙﹀彂鐗规晥濂囩墿锛堝嚮鏉€鏂逛负鐜╁鏃讹級
            TriggerArtifactOnDeath(fighter);
        }

        /// <summary>
        /// 瑙﹀彂鐗规晥濂囩墿锛氬嚮鏉€鏃舵晥鏋?
        /// </summary>
        private void TriggerArtifactOnDeath(BattleFighter deadFighter)
        {
            var dm = GameManager.Instance?.DataManager;
            if (dm == null) return;
            var equipments = dm.PlayerData?.runEquipments;
            if (equipments == null) return;

            bool isEnemyDead = !IsPlayerFighter(deadFighter);
            if (!isEnemyDead) return;

            // 璇诲彇 artifact_config 鐨?subType 淇℃伅
            var artifactSubTypes = GetArtifactSubTypes(equipments);

            foreach (var (subType, value) in artifactSubTypes)
            {
                switch (subType)
                {
                    case "KillHeal":
                        {
                            var killer = FindKiller(deadFighter);
                            if (killer != null && killer.RuntimeAttributes != null)
                            {
                                int heal = Mathf.RoundToInt(killer.RuntimeAttributes.MaxHp * value);
                                killer.RuntimeAttributes.CurrentHp = Mathf.Min(
                                    killer.RuntimeAttributes.MaxHp, killer.CurrentHp + heal);
                                GameLogger.Log("Artifact", $"鐚節鍛借Е鍙? 鍑绘潃鍥炶{heal}");
                            }
                            break;
                        }
                    case "KillShield":
                        {
                            // 涓哄叏浣撻槦鍙嬫坊鍔犳姢鐩撅紙绠€鍖栵細澧炲姞鍑忎激鐧惧垎姣旓級
                            if (_playerFighters != null)
                            {
                                foreach (var ally in _playerFighters)
                                {
                                    if (ally != null && ally.IsAlive && ally.RuntimeAttributes != null)
                                    {
                                        ally.RuntimeAttributes.DamageReceivePercentBuff -= value / 1000f; // value=200鈫掑噺浼?0%
                                        GameLogger.Log("Artifact", $"鐚畧鎶よЕ鍙? 鍏ㄤ綋鎶ょ浘");
                                    }
                                }
                            }
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// 姣忓抚妫€鏌ョ壒鏁堝鐗╋細DamageReduce 鍜?LowHpBonus
        /// </summary>
        private void TickArtifactEffects()
        {
            var dm = GameManager.Instance?.DataManager;
            if (dm == null) return;
            var equipments = dm.PlayerData?.runEquipments;
            if (equipments == null || _playerFighters == null) return;

            var subTypes = GetArtifactSubTypes(equipments);
            float damageReducePercent = 0f;
            float lowHpBonusPercent = 0f;

            foreach (var (subType, value) in subTypes)
            {
                if (subType == "DamageReduce")
                    damageReducePercent = Mathf.Max(damageReducePercent, value);
                if (subType == "LowHpBonus")
                    lowHpBonusPercent = Mathf.Max(lowHpBonusPercent, value);
            }

            foreach (var f in _playerFighters)
            {
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;

                // DamageReduce: 鍙楀埌浼ゅ闄嶄綆
                if (damageReducePercent > 0)
                {
                    f.RuntimeAttributes.DamageReceivePercentBuff = Mathf.Min(
                        f.RuntimeAttributes.DamageReceivePercentBuff, -damageReducePercent);
                }

                // LowHpBonus: HP<30%鏃舵敾鍑诲姏+
                float hpPercent = f.RuntimeAttributes.MaxHp > 0 ?
                    (float)f.CurrentHp / f.RuntimeAttributes.MaxHp : 1f;
                if (lowHpBonusPercent > 0 && hpPercent < 0.3f)
                {
                    f.RuntimeAttributes.AttackPercentBuff = Mathf.Max(
                        f.RuntimeAttributes.AttackPercentBuff, lowHpBonusPercent);
                }
            }
        }

        /// <summary>
        /// 浠庤澶囧垪琛ㄤ腑鎻愬彇鐗规晥濂囩墿鐨?subType 鍜?value
        /// </summary>
        private List<(string subType, float value)> GetArtifactSubTypes(List<Camp.EquipmentRecord> equipments)
        {
            var result = new List<(string, float)>();
            foreach (var eq in equipments)
            {
                if (eq == null || string.IsNullOrEmpty(eq.equipmentId)) continue;

                var artifact = TribeConfigLoader.Instance?.GetArtifact(eq.equipmentId);
                if (artifact == null || string.IsNullOrEmpty(artifact.subType)) continue;

                float value = 0f;
                if (artifact.effects != null && artifact.effects.Count > 0)
                    value = artifact.effects[0].value;

                result.Add((artifact.subType, value));
            }
            return result;
        }

        private BattleFighter FindKiller(BattleFighter deadFighter)
        {
            // 绠€鍖栵細杩斿洖瀛樻椿鐨勭涓€涓帺瀹跺崟浣?
            if (_playerFighters == null) return null;
            foreach (var f in _playerFighters)
            {
                if (f != null && f.IsAlive) return f;
            }
            return null;
        }

        private bool IsPlayerFighter(BattleFighter fighter)
        {
            if (_playerFighters == null) return false;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                if (_playerFighters[i] == fighter) return true;
            }
            return false;
        }

        private void UpdateDeathStates(BattleFighter[] fighters, float deltaTime)
        {
            if (fighters == null)
            {
                return;
            }

            for (int i = 0; i < fighters.Length; i++)
            {
                BattleFighter fighter = fighters[i];
                if (fighter == null || !fighter.IsDying || fighter.IsRemoved)
                {
                    continue;
                }

                fighter.DeathTimer -= deltaTime;
                if (fighter.DeathTimer > 0f)
                {
                    continue;
                }

                Debug.Log($"[UpdateDeathStates] Removing {fighter.Name} fighterId={fighter.FighterId} deathTimer={fighter.DeathTimer}");

                if (fighter.Transform != null)
                {
                    UnityEngine.Object.Destroy(fighter.Transform.gameObject);
                }

                fighter.Transform = null;
                fighter.Avatar = null;
                fighter.IsRemoved = true;
            }
        }

        private bool AreAllRemoved(BattleFighter[] fighters)
        {
            if (fighters == null || fighters.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < fighters.Length; i++)
            {
                if (fighters[i] != null && !fighters[i].IsRemoved)
                {
                    return false;
                }
            }

            return true;
        }

        private const string HitEffectAddress = "2deffect/daji";
        private const float HitEffectDuration = 0.3f;
        private static Sprite _hitEffectSprite;
        private static bool _hitEffectSpriteLoaded;

        /// <summary>
        /// 鏇存柊鍙楀嚮鐏姳瀹氭椂鍣?
        /// </summary>
        private static void UpdateHitEffects(float deltaTime)
        {
            if (_hitEffectTimers.Count == 0) return;

            List<BattleFighter> toRemove = null;
            foreach (var kv in _hitEffectTimers)
            {
                // fighter 宸查攢姣侊紝鐩存帴娓呯悊
                if (kv.Key.Transform == null)
                {
                    if (toRemove == null) toRemove = new List<BattleFighter>();
                    toRemove.Add(kv.Key);
                    continue;
                }
                kv.Key.HitEffectTimer -= deltaTime;
                if (kv.Key.HitEffectTimer <= 0f)
                {
                    if (kv.Key.HitEffect != null)
                        kv.Key.HitEffect.SetActive(false);
                    if (toRemove == null) toRemove = new List<BattleFighter>();
                    toRemove.Add(kv.Key);
                }
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                    _hitEffectTimers.Remove(toRemove[i]);
            }
        }

        /// <summary>
        /// 娓呯悊鎵€鏈夊彈鍑荤伀鑺辨晥鏋滐紙鎴樻枟缁撴潫鏃惰皟鐢級
        /// </summary>
        public static void ClearAllHitEffects()
        {
            foreach (var kv in _hitEffectTimers)
            {
                if (kv.Key.HitEffect != null)
                    kv.Key.HitEffect.SetActive(false);
            }
            _hitEffectTimers.Clear();
        }

        /// <summary>
        /// 鍦ㄧ洰鏍囦綅缃樉绀哄彈鍑荤伀鑺辨晥鏋?
        /// </summary>
        public static void ShowHitEffect(BattleFighter target)
        {
            if (target?.Transform == null || target.HitEffect == null) return;

            // 鍔犺浇鐏姳鍥剧墖锛堝彧鍔犺浇涓€娆★級
            if (!_hitEffectSpriteLoaded)
            {
                _hitEffectSpriteLoaded = true;
                var resourceManager = GameManager.Instance?.ResourceManager;
                if (resourceManager != null)
                    _hitEffectSprite = resourceManager.LoadSprite(HitEffectAddress);
            }

            if (_hitEffectSprite == null) return;

            // 璁剧疆鍥剧墖骞舵樉绀?
            var sr = target.HitEffect.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = _hitEffectSprite;

            target.HitEffect.SetActive(true);
            target.HitEffectTimer = HitEffectDuration;
            _hitEffectTimers[target] = HitEffectDuration;
        }
    }
}
