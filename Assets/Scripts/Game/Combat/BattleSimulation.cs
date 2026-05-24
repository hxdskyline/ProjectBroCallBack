using UnityEngine;
using Camp;
using System;
using System.Collections.Generic;
using Combat.Fighter;
using Combat.Effects;

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

        private readonly BattleFighter[] _playerFighters;
        private readonly BattleFighter[] _enemyFighters;
        private readonly BattleSimulationConfig _config;

        private float _battleElapsed;
        private CorpseManager _corpseManager;
        private SummonManager _summonManager;
        private static readonly Dictionary<BattleFighter, float> _hitEffectTimers = new Dictionary<BattleFighter, float>();

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
        }

        /// <summary>
        /// 获取尸体管理器
        /// </summary>
        public CorpseManager CorpseManager => _corpseManager;

        /// <summary>
        /// 获取召唤物管理器
        /// </summary>
        public SummonManager SummonManager => _summonManager;

        /// <summary>
        /// 施放消耗品效果（对全体目标生效，无需选位）
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
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                var f = _enemyFighters[i];
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
                f.RuntimeAttributes.CurrentHp = Mathf.Max(0, f.RuntimeAttributes.CurrentHp - 200);
                if (f.RuntimeAttributes.CurrentHp <= 0) StartDeath(f);
            }
            Debug.Log("[Consumable] Bomb: 200 damage to all enemies");
        }

        private void ApplyFreezeTrap()
        {
            if (_enemyFighters == null) return;
            var freezeBuff = StatusEffectFactory.CreateFreeze(3f);
            for (int i = 0; i < _enemyFighters.Length; i++)
            {
                var f = _enemyFighters[i];
                if (f == null || !f.IsAlive) continue;
                f.RuntimeAttributes?.ApplyBuff(freezeBuff);
                f.FreezeTimer = Mathf.Max(f.FreezeTimer, 3f);
            }
            Debug.Log("[Consumable] FreezeTrap: all enemies frozen for 3s");
        }

        private void ApplyHealPotion()
        {
            if (_playerFighters == null) return;
            for (int i = 0; i < _playerFighters.Length; i++)
            {
                var f = _playerFighters[i];
                if (f == null || !f.IsAlive || f.RuntimeAttributes == null) continue;
                int heal = Mathf.RoundToInt(f.RuntimeAttributes.MaxHp * 0.5f);
                f.RuntimeAttributes.CurrentHp = Mathf.Min(f.RuntimeAttributes.CurrentHp + heal, f.RuntimeAttributes.MaxHp);
            }
            Debug.Log("[Consumable] HealPotion: healed all allies for 50% MaxHp");
        }

        private void ApplyAttackBuff()
        {
            if (_playerFighters == null) return;
            var buff = UnifiedBuff.CreateTimedBuff(
                "consumable_attack_buff", "攻击强化",
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
                "consumable_defense_buff", "防御强化",
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
            // Freeze timers（保留，用于非 buff 系统的冻结）
            UpdateFreezeTimers(_playerFighters, deltaTime);
            UpdateFreezeTimers(_enemyFighters, deltaTime);
        }

        /// <summary>
        /// Tick 所有 fighter 的 UnifiedBuff：递减 duration、执行 DoT、移除过期 buff
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

                // 应用 DoT 伤害
                if (result.dotDamage > 0)
                {
                    f.RuntimeAttributes.CurrentHp = Mathf.Max(0, f.RuntimeAttributes.CurrentHp - result.dotDamage);
                    // 显示伤害数字
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

                // 应用冻结
                if (result.freezeDuration > 0f)
                    f.FreezeTimer = Mathf.Max(f.FreezeTimer, result.freezeDuration);

                // 需要重新计算属性（减速过期等）
                if (result.needsRecalculate)
                    f.RuntimeAttributes.Recalculate();

                // 检查相位转移/隐匿 buff 是否过期，清除状态标记
                if (f.IsInvulnerable && !HasBuff(f, "phase_shift_ally") && !HasBuff(f, "phase_shift_enemy"))
                    f.IsInvulnerable = false;
                if (f.IsStealthed && !HasBuff(f, "stealth_atk") && !HasBuff(f, "stealth"))
                    f.IsStealthed = false;
            }
        }

        /// <summary>
        /// 检查 fighter 是否拥有指定 buffId 的 buff
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
                    f.FreezeTimer -= deltaTime;
            }
        }

        private void UpdateVisualEffects(BattleFighter[] fighters)
        {
            if (fighters == null) return;
            for (int i = 0; i < fighters.Length; i++)
            {
                var f = fighters[i];
                if (f == null || !f.IsAlive || f.Transform == null) continue;

                bool slowed = f.FreezeTimer > 0f
                    || (f.RuntimeAttributes != null && f.RuntimeAttributes.SpeedPercentDebuff > 0f);

                var sr = f.Transform.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.color = slowed ? SlowTint : Color.white;
            }
        }

        public bool Tick(float deltaTime, out bool playerVictory)
        {
            playerVictory = false;
            _battleElapsed += deltaTime;

            UpdateTimers(deltaTime);
            TickAllBuffs(deltaTime);
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

                BattleFighter target = FindNearestTarget(self, targets);
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

                // 隐匿状态：不可被选为目标
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
            if (self.FreezeTimer > 0f)
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
                // 攻击冷却: 直接使用 CorrectedAttackSpeed 作为冷却时间(秒)
                float attackCooldown = self.RuntimeAttributes?.CorrectedAttackSpeed ?? 1f;
                self.AttackCooldownTimer = Mathf.Max(0.1f, attackCooldown);
                self.Avatar?.PlayAttackAndReturnIdle();

                // 狸花（远程）：发射子弹，不走 PendingHit
                if (self.TribeType == TribeType.Tabby)
                {
                    int damage = CalculateDamage(self, target);
                    bool isCritical = false;
                    // 15%概率造成双倍伤害（单次伤害，不再发射两颗子弹）
                    if (self.HasDoubleHit && UnityEngine.Random.value < 0.15f)
                    {
                        damage *= 2;
                        isCritical = true;
                    }
                    OnBulletFired?.Invoke(new BulletData
                    {
                        Attacker = self,
                        Target = target,
                        Damage = damage,
                        IsCritical = isCritical
                    });
                    return;
                }

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

            // 相位转移/无敌状态：跳过伤害
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

            // 狸花连击：攻击2次
            int hitCount = attacker.HasDoubleHit ? 2 : 1;

            for (int hit = 0; hit < hitCount; hit++)
            {
                if (!defender.IsAlive) break;

                // 需求公式: FDMG = MAX[DMG * DR * SKILLMULT * PBUFF + ABUFF, 1] + TD
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
            }

            if (defenderRuntime.CurrentHp <= 0)
            {
                StartDeath(defender);
            }

            // 攻击触发状态效果
            ApplyAttackTriggeredEffects(attacker, defender);

            // IBuffEffect.OnAttackHit 回调（穿刺箭、毒箭等）
            attackerRuntime.TriggerAttackEffects(defender);
        }

        /// <summary>
        /// 攻击命中时触发状态效果（毒、流血等）。
        /// 由 UpdatePendingHit（近战）和 BattleBullet（远程）调用。
        /// </summary>
        public static void ApplyAttackTriggeredEffects(BattleFighter attacker, BattleFighter defender)
        {
            if (attacker == null || defender == null || !defender.IsAlive) return;
            var attackerRuntime = attacker.RuntimeAttributes;
            var defenderRuntime = defender.RuntimeAttributes;
            if (attackerRuntime == null || defenderRuntime == null) return;
            if (attackerRuntime.ActiveBuffs == null) return;

            for (int i = 0; i < attackerRuntime.ActiveBuffs.Count; i++)
            {
                var buff = attackerRuntime.ActiveBuffs[i];
                if (buff.IsExpired) continue;

                switch (buff.gameEffect)
                {
                    case GameEffect.Poison:
                        // 攻击附加毒：effectParam1 = 每秒伤害, effectParam2 = 持续时间
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreatePoison(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.Bleed:
                        // 攻击附加流血
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateBleed(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.Burn:
                        // 攻击附加燃烧
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateBurn(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.Slow:
                        // 攻击附加减速
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateSlow(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.HuntMark:
                        // 攻击标记目标
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateHuntMark(buff.effectParam1, buff.effectParam2));
                        break;
                }
            }
        }

        /// <summary>
        /// 计算单次攻击伤害
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
            float finalF = rawDmg * dr * skillMult * dmgPercentMod + dmgFlatMod;
            return Mathf.Max(1, Mathf.RoundToInt(finalF)) + attackerRuntime.TrueDamage;
        }

        private float GetMoveSpeed(BattleFighter fighter)
        {
            return fighter?.RuntimeAttributes != null
                ? Mathf.Max(0.001f, fighter.RuntimeAttributes.CorrectedMoveSpeed)
                : 2.2f;
        }

        private float GetAttackRange(BattleFighter fighter)
        {
            return fighter?.RuntimeAttributes != null
                ? Mathf.Max(0.1f, fighter.RuntimeAttributes.AttackRange)
                : 1.0f;
        }

        public void StartDeath(BattleFighter fighter)
        {
            if (fighter == null || fighter.IsRemoved || fighter.IsDying)
            {
                return;
            }

            fighter.IsDying = true;
            fighter.PendingHitTimer = 0f;
            fighter.AttackCooldownTimer = 0f;
            fighter.PendingTarget = null;
            fighter.DeathTimer = Mathf.Max(0.1f, _config.DeathDuration);

            // 触发死亡者的 OnDeath 回调
            fighter.RuntimeAttributes?.TriggerDeathEffects();

            // 触发击杀回调（通知所有存活的玩家单位）
            if (!IsPlayerFighter(fighter))
            {
                for (int i = 0; i < _playerFighters.Length; i++)
                {
                    var pf = _playerFighters[i];
                    if (pf != null && pf.IsAlive)
                        pf.RuntimeAttributes?.TriggerKillEffects(fighter);
                }
            }

            // 记录尸体
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
        /// 更新受击火花定时器
        /// </summary>
        private static void UpdateHitEffects(float deltaTime)
        {
            if (_hitEffectTimers.Count == 0) return;

            List<BattleFighter> toRemove = null;
            foreach (var kv in _hitEffectTimers)
            {
                // fighter 已销毁，直接清理
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
        /// 在目标位置显示受击火花效果
        /// </summary>
        public static void ShowHitEffect(BattleFighter target)
        {
            if (target?.Transform == null || target.HitEffect == null) return;

            // 加载火花图片（只加载一次）
            if (!_hitEffectSpriteLoaded)
            {
                _hitEffectSpriteLoaded = true;
                var resourceManager = GameManager.Instance?.ResourceManager;
                if (resourceManager != null)
                    _hitEffectSprite = resourceManager.LoadSprite(HitEffectAddress);
            }

            if (_hitEffectSprite == null) return;

            // 设置图片并显示
            var sr = target.HitEffect.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = _hitEffectSprite;

            target.HitEffect.SetActive(true);
            target.HitEffectTimer = HitEffectDuration;
            _hitEffectTimers[target] = HitEffectDuration;
        }
    }
}
