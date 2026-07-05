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
        private static BattleSimulation _currentSimulation;

        /// <summary>
        /// 当前战斗模拟实例（供子弹等外部逻辑访问）
        /// </summary>
        public static BattleSimulation CurrentSimulation => _currentSimulation;

        /// <summary>
        /// 外部调用：发射子弹（供 PassiveSkillSystem 等使用）
        /// </summary>
        public static void FireBullet(BulletData data)
        {
            OnBulletFired?.Invoke(data);
        }

        /// <summary>
        /// 外部调用：发射带弹射回调的子弹（供 PassiveSkillSystem 使用）
        /// 弹射子弹命中后从命中位置发射新子弹打另一个敌人
        /// </summary>
        public static void FireBulletWithBounce(BattleFighter attacker, BattleFighter target, int damage,
            System.Action<BattleFighter, BattleFighter, Vector3> onHitBounce)
        {
            // 先通过正常流程创建子弹，但这里需要直接创建 BattleBullet
            // 由于 OnBulletFired 事件由 BattleManager.SpawnBullet 处理，不支持回调
            // 所以这里直接创建 GameObject
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
        private float _comboCheckTimer;
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
            _passiveSkillSystem = new PassiveSkillSystem(playerFighters, enemyFighters, this);
            _passiveSkillSystem.InitializeSkills();
            _comboSkillSystem = new ComboSkillSystem();
            _currentSimulation = this;
        }

        /// <summary>
        /// 获取连携技系统
        /// </summary>
        public ComboSkillSystem ComboSkillSystem => _comboSkillSystem;

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
                int actualHeal = Mathf.Min(heal, f.RuntimeAttributes.MaxHp - f.RuntimeAttributes.CurrentHp);
                f.RuntimeAttributes.CurrentHp += actualHeal;
                f.TotalHealingDone += actualHeal;
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
                    f.TotalDamageTaken += result.dotDamage;
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
            TickKnockbacks(_playerFighters, deltaTime);
            TickKnockbacks(_enemyFighters, deltaTime);
            TickKnockdowns(_playerFighters, deltaTime);
            TickKnockdowns(_enemyFighters, deltaTime);
            TickAllBuffs(deltaTime);
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

                // 倒地状态：无法行动
                if (self.KnockdownTimer > 0f)
                {
                    self.PendingTarget = null;
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

                // 出手时触发被动技能（分裂等在出手时判定概率的效果）
                _passiveSkillSystem?.OnAttackLaunch(self, target);

                // 远程单位：发射子弹，不走 PendingHit
                // 判断标准：攻击距离 > 3.5m 视为远程
                bool isRanged = GetAttackRange(self) > 3.5f;
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

                // 近战：走 PendingHit
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

                // 战斗统计
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
            }

            if (defenderRuntime.CurrentHp <= 0)
            {
                StartDeath(defender);
            }

            // 攻击触发状态效果
            ApplyAttackTriggeredEffects(attacker, defender);

            // 击退判定（近战范围内）
            ApplyMeleeKnockback(attacker, defender);

            // IBuffEffect.OnAttackHit 回调（穿刺箭、毒箭等）
            attackerRuntime.TriggerAttackEffects(defender);
        }

        /// <summary>
        /// 每帧检查连携技
        /// </summary>
        private void TickComboSkills(float deltaTime)
        {
            if (_comboSkillSystem == null) return;
            _comboSkillSystem.Update(deltaTime);

            _comboCheckTimer += deltaTime;
            if (_comboCheckTimer < 2f) return; // 每2秒检查一次
            _comboCheckTimer = 0f;

            // 收集存活玩家单位
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
                    GameLogger.Log("Combo", $"连携技触发: {combo.config.skillName}");
                }
            }
        }

        /// <summary>
        /// 每帧触发被动技能
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
        /// 攻击命中时触发被动技能
        /// </summary>
        public static void ApplyAttackTriggeredEffects(BattleFighter attacker, BattleFighter defender)
        {
            if (attacker == null || defender == null || !defender.IsAlive) return;
            var attackerRuntime = attacker.RuntimeAttributes;
            var defenderRuntime = defender.RuntimeAttributes;
            if (attackerRuntime == null || defenderRuntime == null) return;

            // 触发被动技能系统：攻击命中
            var sim = _currentSimulation;
            if (sim?._passiveSkillSystem != null)
            {
                sim._passiveSkillSystem.OnAttackHit(attacker, defender);
            }

            // 橘猫被攻击触发
            if (!string.IsNullOrEmpty(defender.SkillId) && defender.SkillId.StartsWith("jumao"))
            {
                sim?._passiveSkillSystem?.OnJuMaoHit(defender);
            }

            if (attackerRuntime.ActiveBuffs == null) return;

            for (int i = 0; i < attackerRuntime.ActiveBuffs.Count; i++)
            {
                var buff = attackerRuntime.ActiveBuffs[i];
                if (buff.IsExpired) continue;
                // 只处理战斗内限时 buff 的状态效果触发，跳过永久属性 buff
                if (buff.persistence != Camp.BuffPersistence.BattleOnly) continue;

                switch (buff.gameEffect)
                {
                    case GameEffect.Poison:
                        // 攻击附加毒：effectParam1 = 每秒伤害, effectParam2 = 持续时间
                        var poisonBuff = StatusEffectFactory.CreatePoison(buff.effectParam1, buff.effectParam2);
                        // 检查荆棘王冠圣物：中毒无限叠加
                        if (HasRelic("Relic_ThornCrown", attacker))
                        {
                            poisonBuff.maxStacks = 999;
                        }
                        defenderRuntime.ApplyBuff(poisonBuff);
                        break;
                    case GameEffect.Bleed:
                        // 攻击附加流血
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateBleed(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.Burn:
                        // 攻击附加燃烧
                        // 冰火联动：对冰冻单位施加灼烧 → 破冰双倍伤害20点，冰冻解除
                        if (defenderRuntime.HasActiveEffect(GameEffect.Freeze))
                        {
                            int breakDamage = 20;
                            // 检查霜之哀伤圣物：破冰伤害翻倍
                            if (HasRelic("Relic_FrostSorrow", attacker))
                                breakDamage *= 2;
                            defenderRuntime.CurrentHp = Mathf.Max(0, defenderRuntime.CurrentHp - breakDamage);
                            defenderRuntime.RemoveEffect(GameEffect.Freeze);
                            GameLogger.Log("Link", $"冰火联动: 灼烧→破冰 {breakDamage}伤害");
                            // 仍然施加灼烧
                        }
                        defenderRuntime.ApplyBuff(StatusEffectFactory.CreateBurn(buff.effectParam1, buff.effectParam2));
                        break;
                    case GameEffect.Freeze:
                        // 攻击附加冰冻
                        // 冰火联动：对灼烧单位施加冰冻 → 0.1秒后解除+20点伤害
                        if (defenderRuntime.HasActiveEffect(GameEffect.Burn))
                        {
                            int breakDamage = 20;
                            if (HasRelic("Relic_FrostSorrow", attacker))
                                breakDamage *= 2;
                            defenderRuntime.CurrentHp = Mathf.Max(0, defenderRuntime.CurrentHp - breakDamage);
                            defenderRuntime.RemoveEffect(GameEffect.Burn);
                            GameLogger.Log("Link", $"冰火联动: 冰冻→破灼 {breakDamage}伤害");
                            // 冰冻仅持续0.1秒
                            var shortFreeze = StatusEffectFactory.CreateFreeze(0.1f, 0);
                            defenderRuntime.ApplyBuff(shortFreeze);
                        }
                        else
                        {
                            defenderRuntime.ApplyBuff(StatusEffectFactory.CreateFreeze(buff.effectParam1));
                        }
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

            // 缠绕+位移联动：被缠绕的敌人受到击退/击倒/击飞时+50%伤害（拉断伤害）
            if (defenderRuntime.IsRooted && IsDisplacementAttack(attackerRuntime))
            {
                dmgPercentMod *= 1.5f;
                GameLogger.Log("Link", "缠绕+位移联动: +50%拉断伤害");
            }

            float finalF = rawDmg * dr * skillMult * dmgPercentMod + dmgFlatMod;
            return Mathf.Max(1, Mathf.RoundToInt(finalF)) + attackerRuntime.TrueDamage;
        }

        /// <summary>
        /// 判断攻击者是否携带位移型控制效果
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

        // ── 击退系统 ──

        private const float KnockbackDistance = 1.5f;   // 击退距离（米）
        private const float KnockbackChance = 0.05f;    // 击退概率 15%
        private const float MeleeRangeMax = 3.5f;        // 近战范围上限

        private const float KnockbackSpeed = 12f;          // 初始击退速度（米/秒）
        private const float KnockbackDeceleration = 20f;    // 减速度（米/秒²）
        private const float KnockdownDuration = 1.0f;       // 倒地持续时间（秒）

        /// <summary>
        /// 击退规则：在近战范围内每次命中都有15%概率施加一个带减速的推力
        /// 远程单位进入近战范围（距离 ≤ 3.5m）同样遵循此规则
        /// </summary>
        public static void ApplyMeleeKnockback(BattleFighter attacker, BattleFighter defender)
        {
            if (attacker == null || defender == null || !defender.IsAlive) return;
            if (attacker.Transform == null || defender.Transform == null) return;
            // 正在倒地时不重复击退
            if (defender.KnockdownTimer > 0f || defender.KnockbackRemaining > 0f) return;

            // 确认在近战范围内
            float distance = Vector3.Distance(attacker.Transform.position, defender.Transform.position);
            if (distance > MeleeRangeMax) return;

            // 15% 概率击退
            if (UnityEngine.Random.value >= KnockbackChance) return;

            // 击退方向：从攻击者指向防御者（远离攻击者）
            Vector3 knockDir = (defender.Transform.position - attacker.Transform.position).normalized;
            if (knockDir.sqrMagnitude < 0.001f) knockDir = new Vector3(attacker.Transform.localScale.x > 0 ? 1 : -1, 0, 0);
            knockDir.y = 0; // 只在水平方向击退

            // 给一个初速度，由 TickKnockback 处理减速运动
            defender.KnockbackVelocity = knockDir * KnockbackSpeed;
            defender.KnockbackRemaining = KnockbackDistance;

            GameLogger.Log("Combat", $"击退: {attacker.Name} 击退 {defender.Name}");
        }

        /// <summary>
        /// 处理击退位移：匀减速运动直到停下
        /// </summary>
        private void TickKnockback(BattleFighter fighter, float deltaTime)
        {
            if (fighter == null || fighter.Transform == null) return;
            if (fighter.KnockbackRemaining <= 0f) return;

            // 记录击退方向（用于倒地）
            Vector3 knockDir = fighter.KnockbackVelocity.normalized;

            // 当前帧移动距离 = v₀t - ½at²，但不能超过剩余距离
            float v0 = fighter.KnockbackVelocity.magnitude;
            float moveDist = v0 * deltaTime - 0.5f * KnockbackDeceleration * deltaTime * deltaTime;
            moveDist = Mathf.Max(0f, moveDist);

            if (moveDist >= fighter.KnockbackRemaining || v0 <= 0f)
            {
                // 到达终点，清除击退状态
                fighter.Transform.position += knockDir * fighter.KnockbackRemaining;
                fighter.KnockbackVelocity = Vector3.zero;
                fighter.KnockbackRemaining = 0f;

                // 触发倒地
                fighter.KnockdownTimer = KnockdownDuration;
                fighter.KnockdownDir = knockDir;
                return;
            }

            // 移动
            Vector3 delta = knockDir * moveDist;
            fighter.Transform.position += delta;
            fighter.KnockbackRemaining -= moveDist;

            // 更新速度：v = v₀ - at
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

        // ── 倒地系统 ──

        /// <summary>
        /// 处理倒地：角色向后倒下（X轴旋转），1秒后恢复站立
        /// </summary>
        private void TickKnockdown(BattleFighter fighter, float deltaTime)
        {
            if (fighter == null || fighter.Transform == null) return;

            fighter.KnockdownTimer -= deltaTime;

            if (fighter.KnockdownTimer <= 0f)
            {
                // 恢复站立：旋转回直立
                fighter.Transform.localEulerAngles = Vector3.zero;
                fighter.KnockdownDir = Vector3.zero;
                return;
            }

            // 倒地动画：向后倒下（绕X轴旋转），用 easing 模拟倒下→贴地
            float progress = 1f - (fighter.KnockdownTimer / KnockdownDuration); // 0→1
            // 前30%时间快速倒下，后70%保持贴地
            float leanAngle;
            if (progress < 0.3f)
            {
                // 倒下阶段：加速后仰
                float t = progress / 0.3f;
                leanAngle = -90f * (t * t); // 二次缓出，向后倒90度
            }
            else
            {
                // 贴地阶段：保持平躺
                leanAngle = -90f;
            }

            // 倒地方向：沿击退方向向后倒
            float sign = fighter.KnockdownDir.x >= 0 ? 1f : -1f;
            fighter.Transform.localEulerAngles = new Vector3(leanAngle * sign, 0f, 0f);
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
        /// 检查玩家是否拥有指定圣物
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
            // 缠绕/眩晕/冰冻/击倒/击飞 时无法移动
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

        public void StartDeath(BattleFighter fighter)
        {
            if (fighter == null || fighter.IsRemoved || fighter.IsDying)
            {
                Debug.Log($"[StartDeath] Skipped: fighter={fighter?.Name} isRemoved={fighter?.IsRemoved} isDying={fighter?.IsDying}");
                return;
            }

            Debug.Log($"[StartDeath] Killing {fighter.Name} fighterId={fighter.FighterId} hp={fighter.CurrentHp}");
            fighter.IsDying = true;
            fighter.PendingHitTimer = 0f;
            fighter.AttackCooldownTimer = 0f;
            fighter.PendingTarget = null;
            fighter.DeathTimer = Mathf.Max(0.1f, _config.DeathDuration);

            // 触发被动技能：死亡
            _passiveSkillSystem?.OnDeath(fighter);

            // 触发死亡者的 OnDeath 回调
            fighter.RuntimeAttributes?.TriggerDeathEffects();

            // 触发击杀回调 + 被动技能：击杀
            if (!IsPlayerFighter(fighter))
            {
                for (int i = 0; i < _playerFighters.Length; i++)
                {
                    var pf = _playerFighters[i];
                    if (pf != null && pf.IsAlive)
                    {
                        pf.RuntimeAttributes?.TriggerKillEffects(fighter);
                        _passiveSkillSystem?.OnKill(pf, fighter);
                    }
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

            // 触发特效奇物（击杀方为玩家时）
            TriggerArtifactOnDeath(fighter);
        }

        /// <summary>
        /// 触发特效奇物：击杀时效果
        /// </summary>
        private void TriggerArtifactOnDeath(BattleFighter deadFighter)
        {
            var dm = GameManager.Instance?.DataManager;
            if (dm == null) return;
            var equipments = dm.PlayerData?.runEquipments;
            if (equipments == null) return;

            bool isEnemyDead = !IsPlayerFighter(deadFighter);
            if (!isEnemyDead) return;

            // 读取 artifact_config 的 subType 信息
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
                                GameLogger.Log("Artifact", $"猫九命触发: 击杀回血{heal}");
                            }
                            break;
                        }
                    case "KillShield":
                        {
                            // 为全体队友添加护盾（简化：增加减伤百分比）
                            if (_playerFighters != null)
                            {
                                foreach (var ally in _playerFighters)
                                {
                                    if (ally != null && ally.IsAlive && ally.RuntimeAttributes != null)
                                    {
                                        ally.RuntimeAttributes.DamageReceivePercentBuff -= value / 1000f; // value=200→减伤20%
                                        GameLogger.Log("Artifact", $"猫守护触发: 全体护盾");
                                    }
                                }
                            }
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// 每帧检查特效奇物：DamageReduce 和 LowHpBonus
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

                // DamageReduce: 受到伤害降低
                if (damageReducePercent > 0)
                {
                    f.RuntimeAttributes.DamageReceivePercentBuff = Mathf.Min(
                        f.RuntimeAttributes.DamageReceivePercentBuff, -damageReducePercent);
                }

                // LowHpBonus: HP<30%时攻击力+
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
        /// 从装备列表中提取特效奇物的 subType 和 value
        /// </summary>
        private List<(string subType, float value)> GetArtifactSubTypes(List<Camp.EquipmentRecord> equipments)
        {
            var result = new List<(string, float)>();
            // 读取 artifact_config.json 获取 subType
            // 简化：通过 effects 中的 gameEffectType 判断
            foreach (var eq in equipments)
            {
                if (eq.effects == null) continue;
                string subType = null;
                float value = 0f;

                // 通过 equipmentId 匹配已知特效奇物
                if (eq.equipmentId == "Artifact_KillHeal15")
                {
                    subType = "KillHeal";
                    value = 0.15f;
                }
                else if (eq.equipmentId == "Artifact_DmgReduce10")
                {
                    subType = "DamageReduce";
                    value = 0.1f;
                }
                else if (eq.equipmentId == "Artifact_LowHpDmg30")
                {
                    subType = "LowHpBonus";
                    value = 0.3f;
                }
                else if (eq.equipmentId == "Artifact_ShieldOnKill")
                {
                    subType = "KillShield";
                    value = 200f;
                }

                if (subType != null)
                    result.Add((subType, value));
            }
            return result;
        }

        private BattleFighter FindKiller(BattleFighter deadFighter)
        {
            // 简化：返回存活的第一个玩家单位
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
        /// 清理所有受击火花效果（战斗结束时调用）
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
