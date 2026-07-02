using UnityEngine;
using System.Collections.Generic;
using Camp;
using Combat.Effects;
using Combat.Fighter;

namespace Combat
{
    /// <summary>
    /// 被动技能系统 — 管理17个兵种的原版/强化版被动技能
    /// 设计参考：正式文档/401_单位_兵种设计.md §6
    /// 技能触发时机：OnBattleStart / OnTick / OnAttackHit / OnKill / OnDeath
    /// </summary>
    public class PassiveSkillSystem
    {
        private BattleFighter[] _playerFighters;
        private BattleFighter[] _enemyFighters;
        private BattleSimulation _simulation;
        private static readonly System.Random _rng = new System.Random();

        public PassiveSkillSystem(BattleFighter[] playerFighters, BattleFighter[] enemyFighters, BattleSimulation simulation)
        {
            _playerFighters = playerFighters;
            _enemyFighters = enemyFighters;
            _simulation = simulation;
        }

        /// <summary>
        /// 初始化所有单位的被动技能
        /// </summary>
        public void InitializeSkills()
        {
            InitFighterSkills(_playerFighters);
            InitFighterSkills(_enemyFighters);
        }

        private void InitFighterSkills(BattleFighter[] fighters)
        {
            if (fighters == null) return;
            foreach (var f in fighters)
            {
                if (f == null || f.FighterId <= 0) continue;
                var cfg = TribeConfigLoader.Instance?.GetFighterConfig(f.FighterId);
                if (cfg == null) continue;

                f.SkillId = cfg.GetSkillId(f.EnhanceLevel);
                f.SkillTimer = 0f;
                f.AttackCount = 0;
                f.SkillInitialized = true;

                OnBattleStart(f);
            }
        }

        // ── 触发入口 ──

        /// <summary>
        /// 战斗开始时触发
        /// </summary>
        public void OnBattleStart(BattleFighter fighter)
        {
            if (fighter == null || !fighter.SkillInitialized) return;
            ProcessSkill(fighter, SkillTrigger.OnBattleStart, null, 0f);
        }

        /// <summary>
        /// 每帧触发
        /// </summary>
        public void OnTick(BattleFighter fighter, float deltaTime)
        {
            if (fighter == null || !fighter.IsAlive || !fighter.SkillInitialized) return;
            fighter.SkillTimer += deltaTime;
            ProcessSkill(fighter, SkillTrigger.OnTick, null, deltaTime);
        }

        /// <summary>
        /// 攻击出手时触发（分裂等在出手时判定概率的效果）
        /// </summary>
        public void OnAttackLaunch(BattleFighter attacker, BattleFighter target)
        {
            if (attacker == null || !attacker.SkillInitialized) return;
            ProcessSkill(attacker, SkillTrigger.OnAttackLaunch, target, 0f);
        }

        /// <summary>
        /// 攻击命中时触发（弹射、状态效果等在命中时触发）
        /// </summary>
        public void OnAttackHit(BattleFighter attacker, BattleFighter target)
        {
            if (attacker == null || !attacker.SkillInitialized) return;
            attacker.AttackCount++;
            ProcessSkill(attacker, SkillTrigger.OnAttackHit, target, 0f);
        }

        /// <summary>
        /// 击杀敌人时触发
        /// </summary>
        public void OnKill(BattleFighter killer, BattleFighter victim)
        {
            if (killer == null || !killer.SkillInitialized) return;
            ProcessSkill(killer, SkillTrigger.OnKill, victim, 0f);
        }

        /// <summary>
        /// 自身死亡时触发
        /// </summary>
        public void OnDeath(BattleFighter dying)
        {
            if (dying == null || !dying.SkillInitialized) return;
            ProcessSkill(dying, SkillTrigger.OnDeath, null, 0f);
        }

        // ── 技能分发 ──

        private enum SkillTrigger
        {
            OnBattleStart,
            OnTick,
            OnAttackLaunch,   // 出手时（分裂等）
            OnAttackHit,      // 命中时（弹射、状态效果等）
            OnKill,
            OnDeath
        }

        private void ProcessSkill(BattleFighter fighter, SkillTrigger trigger, BattleFighter target, float deltaTime)
        {
            if (string.IsNullOrEmpty(fighter.SkillId)) return;

            switch (fighter.SkillId)
            {
                // ── 普通品质 ──
                case "jumao_original":
                case "jumao_enhanced":
                    Skill_JuMao(fighter, trigger, target);
                    break;
                case "cangyingmao_original":
                case "cangyingmao_enhanced":
                    Skill_CangYingMao(fighter, trigger, target);
                    break;
                case "zhandanmao_original":
                case "zhandanmao_enhanced":
                    Skill_ZhanDanMao(fighter, trigger, target);
                    break;
                case "changmaomao_original":
                case "changmaomao_enhanced":
                    Skill_ChangMaoMao(fighter, trigger, target);
                    break;
                case "nailuomao_original":
                case "nailuomao_enhanced":
                    Skill_NaiLuoMao(fighter, trigger, target);
                    break;

                // ── 高级品质 ──
                case "wudumao_original":
                case "wudumao_enhanced":
                    Skill_WuDuMao(fighter, trigger, target);
                    break;
                case "senlinmao_original":
                case "senlinmao_enhanced":
                    Skill_SenLinMao(fighter, trigger, target, deltaTime);
                    break;
                case "bianbianmao_original":
                case "bianbianmao_enhanced":
                    Skill_BianBianMao(fighter, trigger, target);
                    break;
                case "zhenzhenmao_original":
                case "zhenzhenmao_enhanced":
                    Skill_ZhenZhenMao(fighter, trigger, target);
                    break;
                case "bingbingmao_original":
                case "bingbingmao_enhanced":
                    Skill_BingBingMao(fighter, trigger, target, deltaTime);
                    break;
                case "huoqiumao_original":
                case "huoqiumao_enhanced":
                    Skill_HuoQiuMao(fighter, trigger, target, deltaTime);
                    break;
                case "qiubitmao_original":
                case "qiubitmao_enhanced":
                    Skill_QiuBiTeMao(fighter, trigger, target, deltaTime);
                    break;

                // ── 稀有品质 ──
                case "qishimao_original":
                case "qishimao_enhanced":
                    Skill_QiShiMao(fighter, trigger, target, deltaTime);
                    break;
                case "jinglingmao_original":
                case "jinglingmao_enhanced":
                    Skill_JingLingMao(fighter, trigger, target);
                    break;
                case "xuanguangmao_original":
                case "xuanguangmao_enhanced":
                    Skill_XuanGuangMao(fighter, trigger, target);
                    break;
                case "you235mao_original":
                case "you235mao_enhanced":
                    Skill_You235Mao(fighter, trigger, target);
                    break;
                case "fenghetao_original":
                case "fenghetao_enhanced":
                    Skill_FengHeTao(fighter, trigger, target, deltaTime);
                    break;
            }
        }

        private bool IsEnhanced(BattleFighter f) => f.EnhanceLevel >= 1;

        // ════════════════════════════════════════════
        // 普通品质技能
        // ════════════════════════════════════════════

        /// <summary>
        /// 橘猫：受到攻击时30%概率触发3秒霸体；强化版追加嘲讽3秒
        /// </summary>
        private void Skill_JuMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit) return;
            // 橘猫是被攻击方时触发，OnAttackHit是攻击方触发
            // 需要在被攻击时调用——这里作为被攻击方的被动
            // 实际应在BattleSimulation攻击命中防守方时调用
            if (target == f) return; // 橘猫是target时不在此触发

            // 作为攻击方：无特殊效果
            // 作为被攻击方：由BattleSimulation调用OnHit处理
        }

        /// <summary>
        /// 橘猫被攻击时触发（由BattleSimulation调用）
        /// </summary>
        public void OnJuMaoHit(BattleFighter defender)
        {
            float chance = 0.3f;
            if (_rng.NextDouble() < chance)
            {
                defender.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateSuperArmor(3f));
                if (IsEnhanced(defender))
                {
                    // 强化版：嘲讽最近敌人3秒
                    defender.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateTaunt(3f));
                }
                GameLogger.Log("Skill", $"橘猫触发霸体 enhanced={IsEnhanced(defender)}");
            }
        }

        /// <summary>
        /// 苍蝇猫：攻击时20%/40%概率施加1层中毒
        /// </summary>
        private void Skill_CangYingMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit || target == null) return;
            float chance = IsEnhanced(f) ? 0.4f : 0.2f;
            if (_rng.NextDouble() < chance)
            {
                target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreatePoison(1f, 2f, 3));
                GameLogger.Log("Skill", $"苍蝇猫施毒 chance={chance}");
            }
        }

        /// <summary>
        /// 炸弹猫：死亡时爆炸，范围5m/10m，造成20点伤害+灼烧
        /// </summary>
        private void Skill_ZhanDanMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnDeath) return;
            float radius = IsEnhanced(f) ? 10f : 5f;
            var enemies = GetEnemies(f);
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                if (f.Transform != null && enemy.Transform != null)
                {
                    float dist = Vector3.Distance(f.Transform.position, enemy.Transform.position);
                    if (dist <= radius)
                    {
                        enemy.RuntimeAttributes.CurrentHp = Mathf.Max(0, enemy.CurrentHp - 20);
                        enemy.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(5f, 3f));
                        GameLogger.Log("Skill", $"炸弹猫爆炸命中 {enemy.Name} dist={dist:F1} hp={enemy.CurrentHp}");
                        TryKill(enemy);
                    }
                }
            }
        }

        /// <summary>
        /// 长矛猫：攻击出手时30%概率分裂（向主目标旁边的其他敌人发射额外子弹）；强化版追加30%概率3秒100%攻速提升
        /// </summary>
        private void Skill_ChangMaoMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackLaunch || target == null) return;
            float chance = 0.3f;
            if (_rng.NextDouble() < chance)
            {
                // 分裂：找主目标旁边的其他敌人，每个发射一发额外子弹
                var enemies = GetEnemies(f);
                int splitDmg = Mathf.Max(1, f.RuntimeAttributes.Attack - target.RuntimeAttributes.Defense / 2);
                int splitCount = 0;

                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.IsAlive || enemy == target) continue;
                    // 发射额外子弹打旁边的敌人
                    FireBullet(f, enemy, splitDmg);
                    splitCount++;
                    GameLogger.Log("Skill", $"长矛猫分裂→发射子弹 target={enemy.Name} dmg={splitDmg}");
                }

                if (splitCount == 0)
                {
                    GameLogger.Log("Skill", $"长矛猫分裂触发但旁边无其他敌人");
                }
            }
            if (IsEnhanced(f) && _rng.NextDouble() < 0.3f)
            {
                // 强化版：3秒100%攻速提升
                f.RuntimeAttributes.AttackSpeedPercentBuff += 1.0f;
                f.RuntimeAttributes.Recalculate();
                GameLogger.Log("Skill", "长矛猫强化版攻速提升");
            }
        }

        /// <summary>
        /// 奶爸猫：不攻击敌方，攻击为友方回复攻击力(6点)血量；强化版追加20%概率弹射
        /// </summary>
        private void Skill_NaiLuoMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit) return;
            // 奶爸猫的"攻击"实际是治疗——需要找到最需要治疗的友方
            var allies = GetAllies(f);
            BattleFighter healTarget = null;
            int lowestHpPercent = int.MaxValue;
            foreach (var ally in allies)
            {
                if (ally == null || !ally.IsAlive || ally == f) continue;
                int hpPercent = ally.RuntimeAttributes.MaxHp > 0 ?
                    ally.CurrentHp * 100 / ally.RuntimeAttributes.MaxHp : 100;
                if (hpPercent < lowestHpPercent)
                {
                    lowestHpPercent = hpPercent;
                    healTarget = ally;
                }
            }
            if (healTarget == null) return;

            int healAmount = f.RuntimeAttributes.Attack;
            healTarget.RuntimeAttributes.CurrentHp = Mathf.Min(
                healTarget.RuntimeAttributes.MaxHp, healTarget.CurrentHp + healAmount);
            f.TotalHealingDone += healAmount;
            GameLogger.Log("Skill", $"奶爸猫治疗 {healTarget.Name} +{healAmount}HP");

            // 强化版：20%概率弹射至血量最低的另一个友方
            if (IsEnhanced(f) && _rng.NextDouble() < 0.2f)
            {
                BattleFighter bounceTarget = null;
                lowestHpPercent = int.MaxValue;
                foreach (var ally in allies)
                {
                    if (ally == null || !ally.IsAlive || ally == f || ally == healTarget) continue;
                    int hpPercent = ally.RuntimeAttributes.MaxHp > 0 ?
                        ally.CurrentHp * 100 / ally.RuntimeAttributes.MaxHp : 100;
                    if (hpPercent < lowestHpPercent)
                    {
                        lowestHpPercent = hpPercent;
                        bounceTarget = ally;
                    }
                }
                if (bounceTarget != null)
                {
                    bounceTarget.RuntimeAttributes.CurrentHp = Mathf.Min(
                        bounceTarget.RuntimeAttributes.MaxHp, bounceTarget.CurrentHp + healAmount);
                    f.TotalHealingDone += healAmount;
                    GameLogger.Log("Skill", $"奶爸猫弹射治疗 {bounceTarget.Name} +{healAmount}HP");
                }
            }
        }

        // ════════════════════════════════════════════
        // 高级品质技能
        // ════════════════════════════════════════════

        /// <summary>
        /// 巫毒猫：攻击10%概率施毒；存在其他中毒敌方时50%/100%概率弹射1/2次
        /// </summary>
        private void Skill_WuDuMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit || target == null) return;
            // 10%概率施毒
            if (_rng.NextDouble() < 0.1f)
            {
                target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreatePoison(1f, 2f, 3));
            }
            // 弹射：检查场上其他中毒敌方
            var enemies = GetEnemies(f);
            var poisonedEnemies = new List<BattleFighter>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy == target) continue;
                if (enemy.RuntimeAttributes?.HasActiveEffect(GameEffect.Poison) == true)
                    poisonedEnemies.Add(enemy);
            }
            // 弹射：从当前目标位置发射新子弹打其他中毒敌人
            if (poisonedEnemies.Count > 0 && target.Transform != null)
            {
                int bounceCount = IsEnhanced(f) ? 2 : 1;
                float bounceChance = IsEnhanced(f) ? 1.0f : 0.5f;
                Vector3 hitPos = target.Transform.position;

                for (int i = 0; i < bounceCount && poisonedEnemies.Count > 0; i++)
                {
                    if (_rng.NextDouble() < bounceChance)
                    {
                        int idx = _rng.Next(poisonedEnemies.Count);
                        var bounceTarget = poisonedEnemies[idx];
                        poisonedEnemies.RemoveAt(idx);

                        // 从命中位置发射弹射子弹
                        int dmg = Mathf.Max(1, f.RuntimeAttributes.Attack / 2);
                        var go = new GameObject("BounceBullet");
                        go.transform.position = hitPos;
                        var bullet = go.AddComponent<Combat.Fighter.BattleBullet>();
                        bullet.Setup(f, bounceTarget, dmg, false, null);

                        // 弹射有概率施毒
                        if (_rng.NextDouble() < 0.1f)
                            bounceTarget.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreatePoison(1f, 2f, 3));
                        GameLogger.Log("Skill", $"巫毒猫弹射→从命中点发射子弹 target={bounceTarget.Name} dmg={dmg}");
                    }
                }
            }
        }

        /// <summary>
        /// 森林猫：每10秒缠绕当前目标/强化版追加1名随机敌人，2秒
        /// </summary>
        private void Skill_SenLinMao(BattleFighter f, SkillTrigger trigger, BattleFighter target, float dt)
        {
            if (trigger != SkillTrigger.OnTick) return;
            if (f.SkillTimer >= 10f)
            {
                f.SkillTimer = 0f;
                var currentTarget = f.PendingTarget;
                if (currentTarget != null && currentTarget.IsAlive)
                {
                    currentTarget.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateRoot(2f));
                    GameLogger.Log("Skill", $"森林猫缠绕→{currentTarget.Name}");
                }
                if (IsEnhanced(f))
                {
                    var enemies = GetEnemies(f);
                    if (enemies != null && enemies.Length > 0)
                    {
                        var randomEnemy = enemies[_rng.Next(enemies.Length)];
                        if (randomEnemy != null && randomEnemy.IsAlive && randomEnemy != currentTarget)
                        {
                            randomEnemy.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateRoot(2f));
                            GameLogger.Log("Skill", $"森林猫强化缠绕→{randomEnemy.Name}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 便便猫：攻击时随机触发减速/沉默/嘲讽/增伤
        /// </summary>
        private void Skill_BianBianMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit || target == null) return;
            bool enhanced = IsEnhanced(f);
            float chance = enhanced ? 0.2f : 0.1f;
            float duration = enhanced ? 2f : 1f;

            int effect = _rng.Next(4);
            if (_rng.NextDouble() < chance)
            {
                switch (effect)
                {
                    case 0: // 减速
                        target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateSlow(0.1f, duration));
                        break;
                    case 1: // 沉默
                        target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateSilence(duration));
                        break;
                    case 2: // 嘲讽
                        target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateTaunt(duration));
                        break;
                    case 3: // 敌方增伤（不利效果）
                        target.RuntimeAttributes.DamageReceivePercentBuff += enhanced ? 0.2f : 0.1f;
                        break;
                }
                GameLogger.Log("Skill", $"便便猫触发效果{effect} chance={chance}");
            }
        }

        /// <summary>
        /// 震震猫：每第5次攻击击退/强化版击飞5m+倒地2秒
        /// </summary>
        private void Skill_ZhenZhenMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit || target == null) return;
            if (f.AttackCount % 5 != 0) return;

            if (IsEnhanced(f))
            {
                target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateKnockUp(2f));
                GameLogger.Log("Skill", $"震震猫击飞→{target.Name}");
            }
            else
            {
                target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateKnockBack(5f));
                GameLogger.Log("Skill", $"震震猫击退→{target.Name}");
            }
        }

        /// <summary>
        /// 冰冰猫：每10秒冰冻当前目标/强化版追加攻击力最高敌人，2秒
        /// </summary>
        private void Skill_BingBingMao(BattleFighter f, SkillTrigger trigger, BattleFighter target, float dt)
        {
            if (trigger != SkillTrigger.OnTick) return;
            if (f.SkillTimer >= 10f)
            {
                f.SkillTimer = 0f;
                var currentTarget = f.PendingTarget;
                if (currentTarget != null && currentTarget.IsAlive)
                {
                    currentTarget.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateFreeze(2f, 10f));
                    GameLogger.Log("Skill", $"冰冰猫冰冻→{currentTarget.Name}");
                }
                if (IsEnhanced(f))
                {
                    // 找攻击力最高的敌人
                    var enemies = GetEnemies(f);
                    BattleFighter highestAtk = null;
                    int highestAttack = 0;
                    foreach (var enemy in enemies)
                    {
                        if (enemy == null || !enemy.IsAlive || enemy == currentTarget) continue;
                        if (enemy.RuntimeAttributes?.Attack > highestAttack)
                        {
                            highestAttack = enemy.RuntimeAttributes.Attack;
                            highestAtk = enemy;
                        }
                    }
                    if (highestAtk != null)
                    {
                        highestAtk.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateFreeze(2f, 10f));
                        GameLogger.Log("Skill", $"冰冰猫强化冰冻→{highestAtk.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// 火球猫：每10秒召唤火球攻击随机/血量最低敌人，50%弹射，施加灼烧
        /// 强化版：智能索敌+击杀弹射
        /// </summary>
        private void Skill_HuoQiuMao(BattleFighter f, SkillTrigger trigger, BattleFighter target, float dt)
        {
            if (trigger != SkillTrigger.OnTick) return;
            if (f.SkillTimer >= 10f)
            {
                f.SkillTimer = 0f;
                var enemies = GetEnemies(f);
                if (enemies == null || enemies.Length == 0) return;

                BattleFighter fireballTarget;
                if (IsEnhanced(f))
                {
                    // 强化版：选血量最低
                    fireballTarget = null;
                    int lowestHp = int.MaxValue;
                    foreach (var enemy in enemies)
                    {
                        if (enemy == null || !enemy.IsAlive) continue;
                        if (enemy.CurrentHp < lowestHp)
                        {
                            lowestHp = enemy.CurrentHp;
                            fireballTarget = enemy;
                        }
                    }
                }
                else
                {
                    // 原版：随机
                    var aliveEnemies = new List<BattleFighter>();
                    foreach (var enemy in enemies)
                    {
                        if (enemy != null && enemy.IsAlive) aliveEnemies.Add(enemy);
                    }
                    if (aliveEnemies.Count == 0) return;
                    fireballTarget = aliveEnemies[_rng.Next(aliveEnemies.Count)];
                }

                if (fireballTarget != null)
                {
                    // 火球：发射子弹，附带灼烧，命中后50%概率弹射
                    int dmg = f.RuntimeAttributes.Attack;
                    bool bounceOnHit = _rng.NextDouble() < 0.5f;
                    bool killBounce = IsEnhanced(f) && _rng.NextDouble() < 0.5f;
                    var enemiesRef = enemies;

                    // 先施加灼烧（子弹命中前预挂）
                    fireballTarget.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(5f, 3f));

                    BattleSimulation.FireBulletWithBounce(f, fireballTarget, dmg,
                        (attacker, hitTarget, hitPos) =>
                        {
                            // 命中弹射50%
                            if (bounceOnHit)
                            {
                                FireBounceBullet(attacker, hitTarget, hitPos, enemiesRef, dmg / 2);
                            }
                            // 强化版击杀弹射50%
                            if (killBounce && hitTarget.CurrentHp <= 0)
                            {
                                FireBounceBullet(attacker, hitTarget, hitPos, enemiesRef, dmg / 2);
                            }
                        });
                    GameLogger.Log("Skill", $"火球猫火球→发射子弹 target={fireballTarget.Name} dmg={dmg} bounceOnHit={bounceOnHit}");
                }
            }
        }

        /// <summary>
        /// 弹射：从命中位置发射新子弹打另一个敌人
        /// </summary>
        private void FireBounceBullet(BattleFighter attacker, BattleFighter currentTarget, Vector3 hitPos, BattleFighter[] enemies, int bounceDmg)
        {
            // 找另一个敌人
            BattleFighter bounceTarget = null;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy == currentTarget) continue;
                bounceTarget = enemy;
                break;
            }
            if (bounceTarget != null)
            {
                // 弹射附带灼烧
                bounceTarget.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(5f, 3f));
                // 从命中位置创建子弹
                var go = new GameObject("BounceBullet");
                go.transform.position = hitPos;
                var bullet = go.AddComponent<Combat.Fighter.BattleBullet>();
                bullet.Setup(attacker, bounceTarget, bounceDmg, false, null);
                GameLogger.Log("Skill", $"弹射→从命中点发射子弹 target={bounceTarget.Name} dmg={bounceDmg}");
            }
        }

        /// <summary>
        /// 丘比特猫：每8秒连接血量最高和最低友方，平摊伤害和治疗，3秒/强化版5秒+治疗加成减伤
        /// </summary>
        private void Skill_QiuBiTeMao(BattleFighter f, SkillTrigger trigger, BattleFighter target, float dt)
        {
            if (trigger != SkillTrigger.OnTick) return;
            if (f.SkillTimer >= 8f)
            {
                f.SkillTimer = 0f;
                var allies = GetAllies(f);
                BattleFighter highestHp = null, lowestHp = null;
                int highest = 0, lowest = int.MaxValue;
                foreach (var ally in allies)
                {
                    if (ally == null || !ally.IsAlive || ally == f) continue;
                    if (ally.CurrentHp > highest) { highest = ally.CurrentHp; highestHp = ally; }
                    if (ally.CurrentHp < lowest) { lowest = ally.CurrentHp; lowestHp = ally; }
                }
                if (highestHp != null && lowestHp != null && highestHp != lowestHp)
                {
                    float duration = IsEnhanced(f) ? 5f : 3f;
                    highestHp.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateShareDamage(duration));
                    lowestHp.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateShareDamage(duration));
                    if (IsEnhanced(f))
                    {
                        // 强化版：治疗+10%，伤害-10%
                        highestHp.RuntimeAttributes.DamageReceivePercentBuff -= 0.1f;
                        lowestHp.RuntimeAttributes.DamageReceivePercentBuff -= 0.1f;
                    }
                    GameLogger.Log("Skill", $"丘比特猫连接 {highestHp.Name}↔{lowestHp.Name} dur={duration}");
                }
            }
        }

        // ════════════════════════════════════════════
        // 稀有品质技能
        // ════════════════════════════════════════════

        /// <summary>
        /// 骑士猫：每10秒获得3秒霸体；有霸体友方时减伤20%
        /// 强化版：所有霸体友军减伤20%
        /// </summary>
        private void Skill_QiShiMao(BattleFighter f, SkillTrigger trigger, BattleFighter target, float dt)
        {
            if (trigger == SkillTrigger.OnTick && f.SkillTimer >= 10f)
            {
                f.SkillTimer = 0f;
                f.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateSuperArmor(3f));
                GameLogger.Log("Skill", "骑士猫获得霸体3秒");
            }
            if (trigger == SkillTrigger.OnTick)
            {
                // 检查场上是否有霸体友方（含自己）
                var allies = GetAllies(f);
                bool hasArmoredAlly = false;
                foreach (var ally in allies)
                {
                    if (ally?.RuntimeAttributes?.HasSuperArmor == true)
                    {
                        hasArmoredAlly = true;
                        if (IsEnhanced(f))
                        {
                            // 强化版：所有霸体友军减伤20%
                            ally.RuntimeAttributes.DamageReceivePercentBuff = Mathf.Max(ally.RuntimeAttributes.DamageReceivePercentBuff, -0.2f);
                        }
                    }
                }
                if (hasArmoredAlly && !IsEnhanced(f))
                {
                    // 原版：仅自身减伤20%
                    f.RuntimeAttributes.DamageReceivePercentBuff = Mathf.Max(f.RuntimeAttributes.DamageReceivePercentBuff, -0.2f);
                }
            }
        }

        /// <summary>
        /// 精灵猫：场上存在被缠绕的敌人时攻速+10%伤害+10%
        /// 强化版：每存在1名被缠绕敌人叠1层，无限叠加
        /// </summary>
        private void Skill_JingLingMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnTick) return;
            var enemies = GetEnemies(f);
            int rootedCount = 0;
            foreach (var enemy in enemies)
            {
                if (enemy?.RuntimeAttributes?.IsRooted == true)
                    rootedCount++;
            }
            if (rootedCount > 0)
            {
                float atkBonus, spdBonus;
                if (IsEnhanced(f))
                {
                    // 强化版：每名被缠绕敌人 +10%，无限叠加
                    atkBonus = rootedCount * 0.1f;
                    spdBonus = rootedCount * 0.1f;
                }
                else
                {
                    // 原版：固定 +10%
                    atkBonus = 0.1f;
                    spdBonus = 0.1f;
                }
                // 直接加到百分比buff（每帧重设，不会累积）
                f.RuntimeAttributes.AttackPercentBuff = atkBonus;
                f.RuntimeAttributes.AttackSpeedPercentBuff = spdBonus;
                f.RuntimeAttributes.Recalculate();
            }
        }

        /// <summary>
        /// 炫光猫：HP<50%时眩晕周围3m敌人2秒（10秒CD）
        /// 强化版：全场被减速/嘲讽/缠绕的敌人眩晕2秒
        /// </summary>
        private void Skill_XuanGuangMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnTick) return;
            float hpPercent = f.RuntimeAttributes.MaxHp > 0 ?
                (float)f.CurrentHp / f.RuntimeAttributes.MaxHp : 1f;
            if (hpPercent >= 0.5f) return;
            if (f.SkillTimer < 10f) return; // 10秒CD
            f.SkillTimer = 0f;

            var enemies = GetEnemies(f);
            if (IsEnhanced(f))
            {
                // 强化版：全场被减速/嘲讽/缠绕的敌人眩晕
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    var attrs = enemy.RuntimeAttributes;
                    if (attrs == null) continue;
                    if (attrs.HasActiveEffect(GameEffect.Slow) ||
                        attrs.HasActiveEffect(GameEffect.Taunt) ||
                        attrs.HasActiveEffect(GameEffect.Root))
                    {
                        attrs.ApplyBuff(StatusEffectFactory.CreateStun(2f));
                        GameLogger.Log("Skill", $"炫光猫强化眩晕→{enemy.Name}");
                    }
                }
            }
            else
            {
                // 原版：周围3m眩晕
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    if (f.Transform != null && enemy.Transform != null)
                    {
                        float dist = Vector3.Distance(f.Transform.position, enemy.Transform.position);
                        if (dist <= 3f)
                        {
                            enemy.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateStun(2f));
                            GameLogger.Log("Skill", $"炫光猫眩晕→{enemy.Name} dist={dist:F1}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 铀235猫：死亡时裂变为2个属性减半的分身（HP=1不再裂变）
        /// 强化版：场上有铀235猫时，分裂↔弹射交叉触发50%
        /// </summary>
        private void Skill_You235Mao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger == SkillTrigger.OnDeath)
            {
                // 裂变：HP<=1时不裂变
                if (f.StaticAttributes.MaxHp <= 1) return;
                // 简化：召唤2个分身（由SummonManager处理）
                _simulation?.SummonManager?.SummonClone(f, 2);
                GameLogger.Log("Skill", $"铀235猫裂变 hp={f.StaticAttributes.MaxHp}");
            }
            if (trigger == SkillTrigger.OnAttackHit && IsEnhanced(f))
            {
                // 强化版光环：场上存活时，其他友方触发分裂→50%弹射，弹射→50%分裂
                // 简化：给攻击方额外添加分裂和弹射buff
                if (target != null && _rng.NextDouble() < 0.5f)
                {
                    // 分裂→弹射
                    int extraDmg = Mathf.Max(1, f.RuntimeAttributes.Attack / 2);
                    var enemies = GetEnemies(f);
                    foreach (var enemy in enemies)
                    {
                        if (enemy == null || !enemy.IsAlive || enemy == target) continue;
                        FireBullet(f, enemy, extraDmg);
                        GameLogger.Log("Skill", $"铀235猫光环分裂弹射→发射子弹 target={enemy.Name} dmg={extraDmg}");
                        break; // 只弹射一次
                    }
                }
            }
        }

        /// <summary>
        /// 缝合猫：每5秒召唤1个骷髅猫；每有1只己方猫死亡召唤1个骷髅猫
        /// 强化版：骷髅猫复制场上随机1个存活友方猫的技能
        /// </summary>
        private void Skill_FengHeTao(BattleFighter f, SkillTrigger trigger, BattleFighter target, float dt)
        {
            if (trigger == SkillTrigger.OnTick && f.SkillTimer >= 5f)
            {
                f.SkillTimer = 0f;
                _simulation?.SummonManager?.SummonSkeleton(f);
                GameLogger.Log("Skill", "缝合猫定时召唤骷髅猫");
            }
            if (trigger == SkillTrigger.OnKill)
            {
                // 击杀时也召唤（简化：用OnKill代替死亡检测）
                _simulation?.SummonManager?.SummonSkeleton(f);
                GameLogger.Log("Skill", "缝合猫击杀触发召唤骷髅猫");
            }
        }

        // ── 辅助方法 ──

        /// <summary>
        /// 检查目标是否死亡，如果是则走统一死亡流程
        /// </summary>
        private void TryKill(BattleFighter target)
        {
            if (target == null || target.IsDying || target.IsRemoved) return;
            if (target.CurrentHp > 0) return;
            GameLogger.Log("Skill", $"TryKill: {target.Name} hp=0，调用StartDeath");
            _simulation.StartDeath(target);
        }

        /// <summary>
        /// 发射子弹（通过 BattleSimulation.OnBulletFired 事件触发，由 BattleManager.SpawnBullet 处理表现）
        /// </summary>
        private void FireBullet(BattleFighter attacker, BattleFighter target, int damage)
        {
            if (attacker == null || target == null || !target.IsAlive) return;
            BattleSimulation.FireBullet(new BulletData
            {
                Attacker = attacker,
                Target = target,
                Damage = damage,
                IsCritical = false
            });
        }

        private BattleFighter[] GetEnemies(BattleFighter f)
        {
            if (f.Camp == BattleCamp.Player) return _enemyFighters ?? new BattleFighter[0];
            return _playerFighters ?? new BattleFighter[0];
        }

        private BattleFighter[] GetAllies(BattleFighter f)
        {
            if (f.Camp == BattleCamp.Player) return _playerFighters ?? new BattleFighter[0];
            return _enemyFighters ?? new BattleFighter[0];
        }
    }
}
