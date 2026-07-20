using UnityEngine;
using System.Collections.Generic;
using Camp;
using Combat.Effects;
using Combat.Fighter;

namespace Combat
{
    /// <summary>
    /// 琚姩鎶€鑳界郴缁?鈥?绠＄悊17涓叺绉嶇殑鍘熺増/寮哄寲鐗堣鍔ㄦ妧鑳?
    /// 璁捐鍙傝€冿細姝ｅ紡鏂囨。/401_鍗曚綅_鍏电璁捐.md 搂6
    /// 鎶€鑳借Е鍙戞椂鏈猴細OnBattleStart / OnTick / OnAttackHit / OnKill / OnDeath
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
        /// 鍒濆鍖栨墍鏈夊崟浣嶇殑琚姩鎶€鑳?
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

        // 鈹€鈹€ 瑙﹀彂鍏ュ彛 鈹€鈹€

        /// <summary>
        /// 鎴樻枟寮€濮嬫椂瑙﹀彂
        /// </summary>
        public void OnBattleStart(BattleFighter fighter)
        {
            if (fighter == null || !fighter.SkillInitialized) return;
            ProcessSkill(fighter, SkillTrigger.OnBattleStart, null, 0f);
        }

        /// <summary>
        /// 姣忓抚瑙﹀彂
        /// </summary>
        public void OnTick(BattleFighter fighter, float deltaTime)
        {
            if (fighter == null || !fighter.IsAlive || !fighter.SkillInitialized) return;
            fighter.SkillTimer += deltaTime;
            ProcessSkill(fighter, SkillTrigger.OnTick, null, deltaTime);
        }

        /// <summary>
        /// 鏀诲嚮鍑烘墜鏃惰Е鍙戯紙鍒嗚绛夊湪鍑烘墜鏃跺垽瀹氭鐜囩殑鏁堟灉锛?
        /// </summary>
        public void OnAttackLaunch(BattleFighter attacker, BattleFighter target)
        {
            if (attacker == null || !attacker.SkillInitialized) return;
            ProcessSkill(attacker, SkillTrigger.OnAttackLaunch, target, 0f);
        }

        /// <summary>
        /// 鏀诲嚮鍛戒腑鏃惰Е鍙戯紙寮瑰皠銆佺姸鎬佹晥鏋滅瓑鍦ㄥ懡涓椂瑙﹀彂锛?
        /// </summary>
        public void OnAttackHit(BattleFighter attacker, BattleFighter target)
        {
            if (attacker == null || !attacker.SkillInitialized) return;
            attacker.AttackCount++;
            ProcessSkill(attacker, SkillTrigger.OnAttackHit, target, 0f);
        }

        /// <summary>
        /// 鍑绘潃鏁屼汉鏃惰Е鍙?
        /// </summary>
        public void OnKill(BattleFighter killer, BattleFighter victim)
        {
            if (killer == null || !killer.SkillInitialized) return;
            ProcessSkill(killer, SkillTrigger.OnKill, victim, 0f);
        }

        /// <summary>
        /// 鑷韩姝讳骸鏃惰Е鍙?
        /// </summary>
        public void OnDeath(BattleFighter dying)
        {
            if (dying == null || !dying.SkillInitialized) return;
            ProcessSkill(dying, SkillTrigger.OnDeath, null, 0f);
        }

        // 鈹€鈹€ 鎶€鑳藉垎鍙?鈹€鈹€

        private enum SkillTrigger
        {
            OnBattleStart,
            OnTick,
            OnAttackLaunch,   // 鍑烘墜鏃讹紙鍒嗚绛夛級
            OnAttackHit,      // 鍛戒腑鏃讹紙寮瑰皠銆佺姸鎬佹晥鏋滅瓑锛?
            OnKill,
            OnDeath
        }

        private void ProcessSkill(BattleFighter fighter, SkillTrigger trigger, BattleFighter target, float deltaTime)
        {
            if (string.IsNullOrEmpty(fighter.SkillId)) return;

            switch (fighter.SkillId)
            {
                // 鈹€鈹€ 鏅€氬搧璐?鈹€鈹€
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

                // 鈹€鈹€ 楂樼骇鍝佽川 鈹€鈹€
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

                // 鈹€鈹€ 绋€鏈夊搧璐?鈹€鈹€
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

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鏅€氬搧璐ㄦ妧鑳?
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>
        /// 姗樼尗锛氬彈鍒版敾鍑绘椂30%姒傜巼瑙﹀彂3绉掗湼浣擄紱寮哄寲鐗堣拷鍔犲槻璁?绉?
        /// </summary>
        private void Skill_JuMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit) return;
            // 姗樼尗鏄鏀诲嚮鏂规椂瑙﹀彂锛孫nAttackHit鏄敾鍑绘柟瑙﹀彂
            // 闇€瑕佸湪琚敾鍑绘椂璋冪敤鈥斺€旇繖閲屼綔涓鸿鏀诲嚮鏂圭殑琚姩
            // 瀹為檯搴斿湪BattleSimulation鏀诲嚮鍛戒腑闃插畧鏂规椂璋冪敤
            if (target == f) return; // 姗樼尗鏄痶arget鏃朵笉鍦ㄦ瑙﹀彂

            // 浣滀负鏀诲嚮鏂癸細鏃犵壒娈婃晥鏋?
            // 浣滀负琚敾鍑绘柟锛氱敱BattleSimulation璋冪敤OnHit澶勭悊
        }

        /// <summary>
        /// 姗樼尗琚敾鍑绘椂瑙﹀彂锛堢敱BattleSimulation璋冪敤锛?
        /// </summary>
        public void OnJuMaoHit(BattleFighter defender)
        {
            float chance = 0.3f;
            if (_rng.NextDouble() < chance)
            {
                defender.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateSuperArmor(3f));
                if (IsEnhanced(defender))
                {
                    // 寮哄寲鐗堬細鍢茶鏈€杩戞晫浜?绉?
                    defender.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateTaunt(3f));
                }
                GameLogger.Log("Skill", $"姗樼尗瑙﹀彂闇镐綋 enhanced={IsEnhanced(defender)}");
            }
        }

        /// <summary>
        /// 鑻嶈潎鐚細鏀诲嚮鏃?0%/40%姒傜巼鏂藉姞1灞備腑姣?
        /// </summary>
        private void Skill_CangYingMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit || target == null) return;
            float chance = IsEnhanced(f) ? 0.4f : 0.2f;
            if (_rng.NextDouble() < chance)
            {
                target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreatePoison());
                GameLogger.Log("Skill", $"鑻嶈潎鐚柦姣?chance={chance}");
            }
        }

        /// <summary>
        /// 鐐稿脊鐚細姝讳骸鏃剁垎鐐革紝鑼冨洿5m/10m锛岄€犳垚20鐐逛激瀹?鐏肩儳
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
                        GameLogger.Log("Skill", $"鐐稿脊鐚垎鐐稿懡涓?{enemy.Name} dist={dist:F1} hp={enemy.CurrentHp}");
                        TryKill(enemy);
                    }
                }
            }
        }

        /// <summary>
        /// 闀跨煕鐚細鏀诲嚮鍑烘墜鏃?0%姒傜巼鍒嗚锛堝悜涓荤洰鏍囨梺杈圭殑鍏朵粬鏁屼汉鍙戝皠棰濆瀛愬脊锛夛紱寮哄寲鐗堣拷鍔?0%姒傜巼3绉?00%鏀婚€熸彁鍗?
        /// </summary>
        private void Skill_ChangMaoMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackLaunch || target == null) return;
            float chance = 0.3f;
            if (_rng.NextDouble() < chance)
            {
                // 鍒嗚锛氭壘涓荤洰鏍囨梺杈圭殑鍏朵粬鏁屼汉锛屾瘡涓彂灏勪竴鍙戦澶栧瓙寮?
                var enemies = GetEnemies(f);
                int splitDmg = Mathf.Max(1, f.RuntimeAttributes.Attack - target.RuntimeAttributes.Defense / 2);
                int splitCount = 0;

                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.IsAlive || enemy == target) continue;
                    // 鍙戝皠棰濆瀛愬脊鎵撴梺杈圭殑鏁屼汉
                    FireBullet(f, enemy, splitDmg);
                    splitCount++;
                    GameLogger.Log("Skill", $"闀跨煕鐚垎瑁傗啋鍙戝皠瀛愬脊 target={enemy.Name} dmg={splitDmg}");
                }

                if (splitCount == 0)
                {
                    GameLogger.Log("Skill", "ChangMaoMao split triggered but no extra enemy");
                }
                else if (HasEnhancedYou235Alive(f) && _rng.NextDouble() < 0.5f)
                {
                    // 閾€235寮哄寲鐗堝厜鐜細鍏朵粬鐚Е鍙戝垎瑁傛椂锛屾湁姒傜巼棰濆寮瑰皠涓€娆?
                    foreach (var enemy in enemies)
                    {
                        if (enemy == null || !enemy.IsAlive || enemy == target) continue;
                        FireBullet(f, enemy, splitDmg);
                        GameLogger.Log("Skill", $"閾€235寮哄寲鍏夌幆鍒嗚鈫掑脊灏?target={enemy.Name} dmg={splitDmg}");
                        break;
                    }
                }
            }
            if (IsEnhanced(f) && _rng.NextDouble() < 0.3f)
            {
                // 寮哄寲鐗堬細3绉?00%鏀婚€熸彁鍗?
                f.RuntimeAttributes.AttackSpeedPercentBuff += 1.0f;
                f.RuntimeAttributes.Recalculate();
                GameLogger.Log("Skill", "ChangMaoMao enhanced attack speed up");
            }
        }

        /// <summary>
        /// 濂剁埜鐚細涓嶆敾鍑绘晫鏂癸紝鏀诲嚮涓哄弸鏂瑰洖澶嶆敾鍑诲姏(6鐐?琛€閲忥紱寮哄寲鐗堣拷鍔?0%姒傜巼寮瑰皠
        /// </summary>
        /// <summary>
        /// 奶爸猫：不攻击敌方，为攻击范围内血量最低的友方（含自身）回复攻击力血量
        /// 强化版：额外20%概率弹射至另一个受伤的随机友方
        /// </summary>
        private void Skill_NaiLuoMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit) return;

            var allies = GetAllies(f);
            float attackRange = f.RuntimeAttributes?.AttackRange ?? 5f;
            int healAmount = Mathf.Max(1, f.RuntimeAttributes?.Attack ?? 10);

            // 选择攻击范围内血量最低的友方（含自身）
            BattleFighter healTarget = FindLowestHpAllyInRange(f, allies, attackRange);
            if (healTarget == null) return;

            // 治疗
            int actualHeal = Mathf.Min(healAmount, healTarget.RuntimeAttributes.MaxHp - healTarget.CurrentHp);
            if (actualHeal <= 0) return;

            healTarget.RuntimeAttributes.CurrentHp += actualHeal;
            f.TotalHealingDone += actualHeal;
            RefreshHud(healTarget);
            ShowHealPopup(healTarget, actualHeal);
            GameLogger.Log("Skill", $"奶爸猫治疗 {healTarget.Name} +{actualHeal}HP");

            // 强化版：20%概率弹射至另一个受伤的随机友方
            if (IsEnhanced(f) && _rng.NextDouble() < 0.2f)
            {
                var injuredAllies = new List<BattleFighter>();
                foreach (var ally in allies)
                {
                    if (ally == null || !ally.IsAlive || ally == healTarget || ally.RuntimeAttributes == null) continue;
                    if (ally.CurrentHp < ally.RuntimeAttributes.MaxHp)
                        injuredAllies.Add(ally);
                }
                // 自身也算受伤友方
                if (f.IsAlive && f.CurrentHp < f.RuntimeAttributes.MaxHp && f != healTarget)
                    injuredAllies.Add(f);

                if (injuredAllies.Count > 0)
                {
                    var bounceTarget = injuredAllies[_rng.Next(injuredAllies.Count)];
                    int bounceHeal = Mathf.Min(healAmount, bounceTarget.RuntimeAttributes.MaxHp - bounceTarget.CurrentHp);
                    if (bounceHeal > 0)
                    {
                        bounceTarget.RuntimeAttributes.CurrentHp += bounceHeal;
                        f.TotalHealingDone += bounceHeal;
                        RefreshHud(bounceTarget);
                        ShowHealPopup(bounceTarget, bounceHeal);
                        GameLogger.Log("Skill", $"奶爸猫弹射治疗 {bounceTarget.Name} +{bounceHeal}HP");
                    }
                }
            }
        }

        /// <summary>
        /// 找攻击范围内血量最低的友方（含自身）
        /// </summary>
        private BattleFighter FindLowestHpAllyInRange(BattleFighter self, BattleFighter[] allies, float range)
        {
            BattleFighter lowest = null;
            int lowestHp = int.MaxValue;
            float rangeSqr = range * range;

            // 检查自身
            if (self.IsAlive && self.RuntimeAttributes != null && self.CurrentHp < self.RuntimeAttributes.MaxHp)
            {
                lowest = self;
                lowestHp = self.CurrentHp;
            }

            // 检查友方
            foreach (var ally in allies)
            {
                if (ally == null || !ally.IsAlive || ally.RuntimeAttributes == null) continue;
                if (ally.CurrentHp >= ally.RuntimeAttributes.MaxHp) continue; // 满血跳过

                float distSqr = (ally.Transform.position - self.Transform.position).sqrMagnitude;
                if (distSqr > rangeSqr) continue; // 超出攻击范围

                if (ally.CurrentHp < lowestHp)
                {
                    lowestHp = ally.CurrentHp;
                    lowest = ally;
                }
            }

            return lowest;
        }

        /// <summary>
        /// 在目标头顶弹出绿色治疗数字
        /// </summary>
        private void ShowHealPopup(BattleFighter target, int amount)
        {
            if (target?.Transform == null) return;
            var hud = target.Transform.GetComponent<FighterHUD>();
            hud?.ShowHeal(amount);
        }

        // ??????????????????????????????????????????????????????????????????
        // ??????????
        // ??????????????????????????????????????????????????????????????????

        /// <summary>
        /// ?????????10%?????????????????????50%/100%??????1/2??
        /// </summary>
        private void Skill_WuDuMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit || target == null) return;
            if (_rng.NextDouble() < 0.5f)
            {
                target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreatePoison());
            }

            var enemies = GetEnemies(f);
            var poisonedEnemies = new List<BattleFighter>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy == target) continue;
                if (enemy.RuntimeAttributes?.HasActiveEffect(GameEffect.Poison) == true)
                    poisonedEnemies.Add(enemy);
            }

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

                        int dmg = CalculateSkillDamage(f, bounceTarget);
                        FireBulletFromPosition(f, bounceTarget, dmg, hitPos);

                        if (_rng.NextDouble() < 0.1f)
                            bounceTarget.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreatePoison());
                        GameLogger.Log("Skill", $"????????????????????? target={bounceTarget.Name} dmg={dmg}");
                    }
                }
            }
        }

        /// <summary>
        /// 妫灄鐚細姣?0绉掔紶缁曞綋鍓嶇洰鏍?寮哄寲鐗堣拷鍔?鍚嶉殢鏈烘晫浜猴紝2绉?
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
                    GameLogger.Log("Skill", $"SenLinMao root -> {currentTarget.Name}");
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
                            GameLogger.Log("Skill", $"SenLinMao enhanced root -> {randomEnemy.Name}");
                        }
                    }
                }
            }
        }


        /// <summary>
        /// 渚夸究鐚細鏀诲嚮鏃堕殢鏈鸿Е鍙戝噺閫?娌夐粯/鍢茶/澧炰激
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
                    case 0: // 鍑忛€?
                        target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateSlow(0.1f, duration));
                        break;
                    case 1: // 娌夐粯
                        target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateSilence(duration));
                        break;
                    case 2: // 鍢茶
                        target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateTaunt(duration));
                        break;
                    case 3: // 鏁屾柟澧炰激锛堜笉鍒╂晥鏋滐級
                        target.RuntimeAttributes.DamageReceivePercentBuff += enhanced ? 0.2f : 0.1f;
                        break;
                }
                GameLogger.Log("Skill", $"BianBianMao effect={effect} chance={chance}");
            }
        }

        /// <summary>
        /// 闇囬渿鐚細姣忕5娆℃敾鍑诲嚮閫€/寮哄寲鐗堝嚮椋?m+鍊掑湴2绉?
        /// </summary>
        private void Skill_ZhenZhenMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnAttackHit || target == null) return;
            if (f.AttackCount % 5 != 0) return;

            if (IsEnhanced(f))
            {
                target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateKnockUp(2f));
                GameLogger.Log("Skill", $"闇囬渿鐚嚮椋炩啋{target.Name}");
            }
            else
            {
                target.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateKnockBack(5f));
                GameLogger.Log("Skill", $"ZhenZhenMao knockback -> {target.Name}");
            }
        }

        /// <summary>
        /// 鍐板啺鐚細姣?0绉掑啺鍐诲綋鍓嶇洰鏍?寮哄寲鐗堣拷鍔犳敾鍑诲姏鏈€楂樻晫浜猴紝2绉?
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
                    GameLogger.Log("Skill", $"鍐板啺鐚啺鍐烩啋{currentTarget.Name}");
                }
                if (IsEnhanced(f))
                {
                    // 鎵炬敾鍑诲姏鏈€楂樼殑鏁屼汉
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
                        GameLogger.Log("Skill", $"鍐板啺鐚己鍖栧啺鍐烩啋{highestAtk.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// 鐏悆鐚細姣?0绉掑彫鍞ょ伀鐞冩敾鍑婚殢鏈?琛€閲忔渶浣庢晫浜猴紝50%寮瑰皠锛屾柦鍔犵伡鐑?
        /// 寮哄寲鐗堬細鏅鸿兘绱㈡晫+鍑绘潃寮瑰皠
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
                    // 寮哄寲鐗堬細閫夎閲忔渶浣?
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
                    // 鍘熺増锛氶殢鏈?
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
                    // 鐏悆锛氬彂灏勫瓙寮癸紝闄勫甫鐏肩儳锛屽懡涓悗50%姒傜巼寮瑰皠
                    int dmg = f.RuntimeAttributes.Attack;
                    bool bounceOnHit = _rng.NextDouble() < 0.5f;
                    bool killBounce = IsEnhanced(f) && _rng.NextDouble() < 0.5f;
                    var enemiesRef = enemies;

                    // 鍏堟柦鍔犵伡鐑э紙瀛愬脊鍛戒腑鍓嶉鎸傦級
                    fireballTarget.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(5f, 3f));

                    BattleSimulation.FireBulletWithBounce(f, fireballTarget, dmg,
                        (attacker, hitTarget, hitPos) =>
                        {
                            // 鍛戒腑寮瑰皠50%
                            if (bounceOnHit)
                            {
                                FireBounceBullet(attacker, hitTarget, hitPos, enemiesRef, dmg / 2);
                                if (HasEnhancedYou235Alive(attacker) && _rng.NextDouble() < 0.5f)
                                {
                                    foreach (var enemy in enemiesRef)
                                    {
                                        if (enemy == null || !enemy.IsAlive || enemy == hitTarget) continue;
                                        FireBullet(attacker, enemy, Mathf.Max(1, dmg / 2));
                                        GameLogger.Log("Skill", $"閾€235寮哄寲鍏夌幆寮瑰皠鈫掑垎瑁?target={enemy.Name} dmg={Mathf.Max(1, dmg / 2)}");
                                        break;
                                    }
                                }
                            }
                            // 寮哄寲鐗堝嚮鏉€寮瑰皠50%
                            if (killBounce && hitTarget.CurrentHp <= 0)
                            {
                                FireBounceBullet(attacker, hitTarget, hitPos, enemiesRef, dmg / 2);
                                if (HasEnhancedYou235Alive(attacker) && _rng.NextDouble() < 0.5f)
                                {
                                    foreach (var enemy in enemiesRef)
                                    {
                                        if (enemy == null || !enemy.IsAlive || enemy == hitTarget) continue;
                                        FireBullet(attacker, enemy, Mathf.Max(1, dmg / 2));
                                        GameLogger.Log("Skill", $"閾€235寮哄寲鍏夌幆寮瑰皠鈫掑垎瑁?target={enemy.Name} dmg={Mathf.Max(1, dmg / 2)}");
                                        break;
                                    }
                                }
                            }
                        });
                    GameLogger.Log("Skill", $"鐏悆鐚伀鐞冣啋鍙戝皠瀛愬脊 target={fireballTarget.Name} dmg={dmg} bounceOnHit={bounceOnHit}");
                }
            }
        }

        /// <summary>
        /// 寮瑰皠锛氫粠鍛戒腑浣嶇疆鍙戝皠鏂板瓙寮规墦鍙︿竴涓晫浜?
        /// </summary>
        private void FireBounceBullet(BattleFighter attacker, BattleFighter currentTarget, Vector3 hitPos, BattleFighter[] enemies, int bounceDmg)
        {
            // 鎵惧彟涓€涓晫浜?
            BattleFighter bounceTarget = null;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy == currentTarget) continue;
                bounceTarget = enemy;
                break;
            }
            if (bounceTarget != null)
            {
                // 寮瑰皠闄勫甫鐏肩儳
                bounceTarget.RuntimeAttributes.ApplyBuff(StatusEffectFactory.CreateBurn(5f, 3f));
                // 浠庡懡涓綅缃垱寤哄瓙寮?
                var go = new GameObject("BounceBullet");
                go.transform.position = hitPos;
                var bullet = go.AddComponent<Combat.Fighter.BattleBullet>();
                bullet.Setup(attacker, bounceTarget, bounceDmg, false, null);
                GameLogger.Log("Skill", $"寮瑰皠鈫掍粠鍛戒腑鐐瑰彂灏勫瓙寮?target={bounceTarget.Name} dmg={bounceDmg}");
            }
        }

        /// <summary>
        /// 涓樻瘮鐗圭尗锛氭瘡8绉掕繛鎺ヨ閲忔渶楂樺拰鏈€浣庡弸鏂癸紝骞虫憡浼ゅ鍜屾不鐤楋紝3绉?寮哄寲鐗?绉?娌荤枟鍔犳垚鍑忎激
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
                        // 寮哄寲鐗堬細娌荤枟+10%锛屼激瀹?10%
                        highestHp.RuntimeAttributes.DamageReceivePercentBuff -= 0.1f;
                        lowestHp.RuntimeAttributes.DamageReceivePercentBuff -= 0.1f;
                    }
                    GameLogger.Log("Skill", $"QiuBiTeMao link {highestHp.Name} <-> {lowestHp.Name} dur={duration}");
                }
            }
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 绋€鏈夊搧璐ㄦ妧鑳?
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>
        /// 楠戝＋鐚細姣?0绉掕幏寰?绉掗湼浣擄紱鏈夐湼浣撳弸鏂规椂鍑忎激20%
        /// 寮哄寲鐗堬細鎵€鏈夐湼浣撳弸鍐涘噺浼?0%
        /// </summary>
        private void Skill_QiShiMao(BattleFighter f, SkillTrigger trigger, BattleFighter target, float dt)
        {
            if (trigger == SkillTrigger.OnTick && f.SkillTimer >= 10f)
            {
                f.SkillTimer = 0f;
                f.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateSuperArmor(3f));
                GameLogger.Log("Skill", "QiShiMao gained super armor");
            }
            if (trigger == SkillTrigger.OnTick)
            {
                // 妫€鏌ュ満涓婃槸鍚︽湁闇镐綋鍙嬫柟锛堝惈鑷繁锛?
                var allies = GetAllies(f);
                bool hasArmoredAlly = false;
                foreach (var ally in allies)
                {
                    if (ally?.RuntimeAttributes?.HasSuperArmor == true)
                    {
                        hasArmoredAlly = true;
                        if (IsEnhanced(f))
                        {
                            // 寮哄寲鐗堬細鎵€鏈夐湼浣撳弸鍐涘噺浼?0%
                            ally.RuntimeAttributes.DamageReceivePercentBuff = Mathf.Max(ally.RuntimeAttributes.DamageReceivePercentBuff, -0.2f);
                        }
                    }
                }
                if (hasArmoredAlly && !IsEnhanced(f))
                {
                    // 鍘熺増锛氫粎鑷韩鍑忎激20%
                    f.RuntimeAttributes.DamageReceivePercentBuff = Mathf.Max(f.RuntimeAttributes.DamageReceivePercentBuff, -0.2f);
                }
            }
        }

        /// <summary>
        /// 绮剧伒鐚細鍦轰笂瀛樺湪琚紶缁曠殑鏁屼汉鏃舵敾閫?10%浼ゅ+10%
        /// 寮哄寲鐗堬細姣忓瓨鍦?鍚嶈缂犵粫鏁屼汉鍙?灞傦紝鏃犻檺鍙犲姞
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
                    // 寮哄寲鐗堬細姣忓悕琚紶缁曟晫浜?+10%锛屾棤闄愬彔鍔?
                    atkBonus = rootedCount * 0.1f;
                    spdBonus = rootedCount * 0.1f;
                }
                else
                {
                    // 鍘熺増锛氬浐瀹?+10%
                    atkBonus = 0.1f;
                    spdBonus = 0.1f;
                }
                // 鐩存帴鍔犲埌鐧惧垎姣攂uff锛堟瘡甯ч噸璁撅紝涓嶄細绱Н锛?
                f.RuntimeAttributes.AttackPercentBuff = atkBonus;
                f.RuntimeAttributes.AttackSpeedPercentBuff = spdBonus;
                f.RuntimeAttributes.Recalculate();
            }
        }

        /// <summary>
        /// 鐐厜鐚細HP<50%鏃剁湬鏅曞懆鍥?m鏁屼汉2绉掞紙10绉扖D锛?
        /// 寮哄寲鐗堬細鍏ㄥ満琚噺閫?鍢茶/缂犵粫鐨勬晫浜虹湬鏅?绉?
        /// </summary>
        private void Skill_XuanGuangMao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger != SkillTrigger.OnTick) return;
            float hpPercent = f.RuntimeAttributes.MaxHp > 0 ?
                (float)f.CurrentHp / f.RuntimeAttributes.MaxHp : 1f;
            if (hpPercent >= 0.5f) return;
            if (f.SkillTimer < 10f) return; // 10绉扖D
            f.SkillTimer = 0f;

            var enemies = GetEnemies(f);
            if (IsEnhanced(f))
            {
                // 寮哄寲鐗堬細鍏ㄥ満琚噺閫?鍢茶/缂犵粫鐨勬晫浜虹湬鏅?
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
                        GameLogger.Log("Skill", $"鐐厜鐚己鍖栫湬鏅曗啋{enemy.Name}");
                    }
                }
            }
            else
            {
                // 鍘熺増锛氬懆鍥?m鐪╂檿
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    if (f.Transform != null && enemy.Transform != null)
                    {
                        float dist = Vector3.Distance(f.Transform.position, enemy.Transform.position);
                        if (dist <= 3f)
                        {
                            enemy.RuntimeAttributes?.ApplyBuff(StatusEffectFactory.CreateStun(2f));
                            GameLogger.Log("Skill", $"鐐厜鐚湬鏅曗啋{enemy.Name} dist={dist:F1}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 閾€235鐚細姝讳骸鏃惰鍙樹负2涓睘鎬у噺鍗婄殑鍒嗚韩锛圚P=1涓嶅啀瑁傚彉锛?
        /// 寮哄寲鐗堬細鍦轰笂鏈夐搥235鐚椂锛屽垎瑁傗啍寮瑰皠浜ゅ弶瑙﹀彂50%
        /// </summary>
        private void Skill_You235Mao(BattleFighter f, SkillTrigger trigger, BattleFighter target)
        {
            if (trigger == SkillTrigger.OnDeath)
            {
                // 瑁傚彉锛欻P<=1鏃朵笉瑁傚彉
                if (f.StaticAttributes.MaxHp <= 1) return;
                // 绠€鍖栵細鍙敜2涓垎韬紙鐢盨ummonManager澶勭悊锛?
                _simulation?.SummonManager?.SummonClone(f, 2);
                GameLogger.Log("Skill", $"閾€235鐚鍙?hp={f.StaticAttributes.MaxHp}");
            }
        }

        /// <summary>
        /// 缂濆悎鐚細姣?绉掑彫鍞?涓楂呯尗锛涙瘡鏈?鍙繁鏂圭尗姝讳骸鍙敜1涓楂呯尗
        /// 寮哄寲鐗堬細楠烽珔鐚鍒跺満涓婇殢鏈?涓瓨娲诲弸鏂圭尗鐨勬妧鑳?
        /// </summary>
        private void Skill_FengHeTao(BattleFighter f, SkillTrigger trigger, BattleFighter target, float dt)
        {
            if (trigger == SkillTrigger.OnTick && f.SkillTimer >= 5f)
            {
                f.SkillTimer = 0f;
                _simulation?.SummonManager?.SummonSkeleton(f);
                GameLogger.Log("Skill", "缂濆悎鐚畾鏃跺彫鍞ら楂呯尗");
            }
            if (trigger == SkillTrigger.OnKill)
            {
                // 鍑绘潃鏃朵篃鍙敜锛堢畝鍖栵細鐢∣nKill浠ｆ浛姝讳骸妫€娴嬶級
                _simulation?.SummonManager?.SummonSkeleton(f);
                GameLogger.Log("Skill", "FengHeTao summon skeleton on kill");
            }
        }

        // 鈹€鈹€ 杈呭姪鏂规硶 鈹€鈹€

        /// <summary>
        /// 妫€鏌ョ洰鏍囨槸鍚︽浜★紝濡傛灉鏄垯璧扮粺涓€姝讳骸娴佺▼
        /// </summary>
        private void TryKill(BattleFighter target)
        {
            if (target == null || target.IsDying || target.IsRemoved) return;
            if (target.CurrentHp > 0) return;
            GameLogger.Log("Skill", $"TryKill: {target.Name} hp=0锛岃皟鐢⊿tartDeath");
            _simulation.StartDeath(target);
        }

        /// <summary>
        /// ??????????? BattleSimulation.OnBulletFired ????????? BattleManager.SpawnBullet ????????
        /// </summary>
        private void RefreshHud(BattleFighter fighter)
        {
            if (fighter?.Transform == null || fighter.RuntimeAttributes == null)
            {
                return;
            }

            FighterHUD hud = fighter.Transform.GetComponent<FighterHUD>();
            if (hud == null)
            {
                return;
            }

            hud.UpdateHp(fighter.RuntimeAttributes.CurrentHp);
        }

        private int CalculateSkillDamage(BattleFighter attacker, BattleFighter defender)
        {
            if (attacker == null || defender == null || attacker.RuntimeAttributes == null || defender.RuntimeAttributes == null)
            {
                return 1;
            }

            int atk = attacker.RuntimeAttributes.Attack;
            int def = defender.RuntimeAttributes.Defense;
            int raw = Mathf.Max(0, atk - def);
            float damageReduction = Mathf.Max(0.2f, 1f - (def / (def + 100f)));
            float finalDamage = raw * damageReduction;
            finalDamage *= attacker.RuntimeAttributes.SkillMultiplier;
            finalDamage *= 1f + defender.RuntimeAttributes.DamageReceivePercentBuff;
            finalDamage += defender.RuntimeAttributes.DamageReceiveFlatBuff;
            int damage = Mathf.Max(1, Mathf.RoundToInt(finalDamage));
            damage += attacker.RuntimeAttributes.TrueDamage;
            return Mathf.Max(1, damage);
        }

        private void FireBulletFromPosition(BattleFighter attacker, BattleFighter target, int damage, Vector3 origin)
        {
            if (attacker == null || target == null || !target.IsAlive) return;
            var go = new GameObject("BounceBullet");
            go.transform.position = origin;
            var bullet = go.AddComponent<Combat.Fighter.BattleBullet>();
            bullet.Setup(attacker, target, damage, false, null);
        }

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

        private bool HasEnhancedYou235Alive(BattleFighter f)
        {
            var allies = GetAllies(f);
            foreach (var ally in allies)
            {
                if (ally == null || !ally.IsAlive) continue;
                if (ally.SkillId == "you235mao_enhanced")
                    return true;
            }
            return false;
        }
    }
}
