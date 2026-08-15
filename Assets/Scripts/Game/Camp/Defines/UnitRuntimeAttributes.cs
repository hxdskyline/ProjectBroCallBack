using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// TickBuff 结果
    /// </summary>
    public struct TickBuffResult
    {
        public int dotDamage;
        public float freezeDuration;
        public bool needsRecalculate;
    }

    /// <summary>
    /// 单位运行时属性 — 战斗中使用，包含 buff 修改器和回调
    /// </summary>
    public class UnitRuntimeAttributes
    {
        // 基础值（来自 UnitStaticAttributes）
        private int _baseAttack;
        private int _baseDefense;
        private int _baseMaxHp;
        private float _baseMoveSpeed;
        private float _baseAttackSpeed;
        private float _baseAttackRange;

        // 计算后的最终值
        public int Attack { get; set; }
        public int Defense { get; private set; }
        public int MaxHp { get; private set; }
        public float CorrectedMoveSpeed { get; private set; }
        public float MoveSpeed => CorrectedMoveSpeed;
        public float CorrectedAttackSpeed { get; private set; }
        public float AttackRange { get; private set; }
        public int CurrentHp { get; set; }

        // Buff 修改器（百分比）
        public float AttackPercentBuff;
        public float DefensePercentBuff;
        public float HpPercentBuff;
        public float SpeedPercentBuff;
        public float SpeedPercentDebuff;
        public float AttackSpeedPercentBuff;
        public float DamageReceivePercentBuff;
        public int DamageReceiveFlatBuff;
        public float SkillMultiplier;
        public int TrueDamage;

        // Buff 修改器（固定值）
        public int AttackFlatBuff;
        public int DefenseFlatBuff;
        public int HpFlatBuff;

        // Buff 列表
        public List<UnifiedBuff> ActiveBuffs { get; private set; }

        // 战斗回调引用（由 BattleSpawner 设置）
        public object OwnerFighter;
        public object[] Allies;
        public object[] Enemies;
        private int _pendingElementDamage;

        public UnitRuntimeAttributes(UnitStaticAttributes stats)
        {
            _baseAttack = stats.Attack;
            _baseDefense = stats.Defense;
            _baseMaxHp = stats.MaxHp;
            _baseMoveSpeed = stats.MoveSpeed;
            _baseAttackSpeed = stats.AttackSpeed;
            _baseAttackRange = stats.AttackRange;

            CurrentHp = _baseMaxHp;
            ActiveBuffs = new List<UnifiedBuff>();

            AttackPercentBuff = 0f;
            DefensePercentBuff = 0f;
            HpPercentBuff = 0f;
            SpeedPercentBuff = 0f;
            SpeedPercentDebuff = 0f;
            AttackSpeedPercentBuff = 0f;
            DamageReceivePercentBuff = 0f;
            DamageReceiveFlatBuff = 0;
            SkillMultiplier = 1f;
            TrueDamage = 0;

            Recalculate();
        }

        /// <summary>
        /// 根据 base + buff 修改器重新计算所有最终属性
        /// </summary>
        public void Recalculate()
        {
            Attack = Mathf.RoundToInt(_baseAttack * (1f + AttackPercentBuff)) + AttackFlatBuff;
            Defense = Mathf.RoundToInt(_baseDefense * (1f + DefensePercentBuff)) + DefenseFlatBuff;
            MaxHp = Mathf.RoundToInt(_baseMaxHp * (1f + HpPercentBuff)) + HpFlatBuff;

            float moveSpeed = _baseMoveSpeed * (1f + SpeedPercentBuff - SpeedPercentDebuff);
            CorrectedMoveSpeed = Mathf.Max(0.001f, moveSpeed);

            float atkSpeed = _baseAttackSpeed * (1f + AttackSpeedPercentBuff);
            CorrectedAttackSpeed = Mathf.Max(0.1f, atkSpeed);
            AttackRange = Mathf.Max(0.1f, _baseAttackRange);
        }

        // ── 控制状态查询 ──

        /// <summary>是否被缠绕（无法移动，可攻击）</summary>
        public bool IsRooted => HasActiveEffect(GameEffect.Root);

        /// <summary>是否被眩晕（无法行动和攻击）</summary>
        public bool IsStunned => HasActiveEffect(GameEffect.Stun) ||
                                 HasActiveEffect(GameEffect.Freeze) ||
                                 HasActiveEffect(GameEffect.KnockUp) ||
                                 HasActiveEffect(GameEffect.KnockDown);

        /// <summary>是否被沉默（无法触发技能）</summary>
        public bool IsSilenced => HasActiveEffect(GameEffect.Silence) && !HasActiveEffect(GameEffect.Taunt);

        /// <summary>是否处于霸体状态（免疫控制）</summary>
        public bool HasSuperArmor => HasActiveEffect(GameEffect.SuperArmor);

        /// <summary>是否被嘲讽</summary>
        public bool IsTaunted => HasActiveEffect(GameEffect.Taunt);

        public bool HasActiveEffect(GameEffect effect)
        {
            if (ActiveBuffs == null) return false;
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].gameEffect == effect && !ActiveBuffs[i].IsExpired)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 从 ActiveBuffs 同步所有属性修正到运行时字段，应在 Recalculate() 前调用
        /// </summary>
        public void SyncStatBuffs()
        {
            // 重置所有修正字段
            AttackPercentBuff = 0f;
            DefensePercentBuff = 0f;
            HpPercentBuff = 0f;
            SpeedPercentBuff = 0f;
            AttackSpeedPercentBuff = 0f;
            AttackFlatBuff = 0;
            DefenseFlatBuff = 0;
            HpFlatBuff = 0;

            if (ActiveBuffs == null) return;

            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                var buff = ActiveBuffs[i];
                if (buff.IsExpired) continue;

                // 只处理有 statType 的属性 buff
                if (buff.statType == StatType.Attack)
                {
                    if (buff.isPercent) AttackPercentBuff += buff.value;
                    else AttackFlatBuff += Mathf.RoundToInt(buff.value);
                }
                else if (buff.statType == StatType.Defense)
                {
                    if (buff.isPercent) DefensePercentBuff += buff.value;
                    else DefenseFlatBuff += Mathf.RoundToInt(buff.value);
                }
                else if (buff.statType == StatType.Hp)
                {
                    if (buff.isPercent) HpPercentBuff += buff.value;
                    else HpFlatBuff += Mathf.RoundToInt(buff.value);
                }
                else if (buff.statType == StatType.MoveSpeed)
                {
                    if (buff.isPercent) SpeedPercentBuff += buff.value;
                    // 固定值移速暂不处理（需要换算）
                }
                else if (buff.statType == StatType.AttackSpeed)
                {
                    if (buff.isPercent) AttackSpeedPercentBuff += buff.value;
                    // 固定值攻速暂不处理
                }
            }
        }

        /// <summary>
        /// 添加 buff，按叠加规则处理
        /// 控制效果叠加规则：同一控制不叠加取最高值，不同控制可叠加，
        /// 位移型控制解除束缚型/仇恨型控制，嘲讽与沉默互斥
        /// </summary>
        public void ApplyBuff(UnifiedBuff buff)
        {
            if (buff == null) return;
            buff = buff.Clone();

            // 冰火联动统一在状态入口处理，保证技能、道具和攻击附带效果行为一致。
            if (buff.gameEffect == GameEffect.Burn && HasActiveEffect(GameEffect.Freeze))
            {
                int breakDamage = GetFreezeBreakDamage() * 2;
                RemoveEffect(GameEffect.Freeze);
                _pendingElementDamage += breakDamage;
                GameLogger.LogFileOnly("Element",
                    $"BurnBreakFreeze target={GetOwnerLogName()} queuedDamage={breakDamage} hp={CurrentHp} freezeRemoved=true burnDuration={buff.remainingDuration:F2}");
            }
            else if (buff.gameEffect == GameEffect.Freeze && HasActiveEffect(GameEffect.Burn))
            {
                // 文档要求先进入0.1秒冰冻，结束时结算双倍破冰伤害。
                float requestedDuration = buff.remainingDuration;
                buff.remainingDuration = Mathf.Min(buff.remainingDuration, 0.1f);
                buff.effectParam2 *= 2f;
                GameLogger.LogFileOnly("Element",
                    $"FreezeOnBurn target={GetOwnerLogName()} requestedDuration={requestedDuration:F2} actualDuration={buff.remainingDuration:F2} breakDamage={buff.effectParam2:F0} hp={CurrentHp}");
            }
            if (buff.gameEffect == GameEffect.Freeze)
            {
                GameLogger.LogFileOnly("Buff",
                    $"ApplyFreeze buffId={buff.buffId} source={buff.source} sourceId={buff.sourceId} duration={buff.remainingDuration:F2} breakDamage={buff.effectParam2:F2} currentHp={CurrentHp}");
            }
            else if (buff.gameEffect == GameEffect.Burn)
            {
                GameLogger.LogFileOnly("Element",
                    $"ApplyBurn target={GetOwnerLogName()} dps={buff.effectParam1:F2} duration={buff.remainingDuration:F2} hp={CurrentHp}");
            }

            // 霸体免疫控制效果
            if (HasSuperArmor && Combat.Effects.StatusEffectFactory.IsControlEffect(buff.gameEffect))
                return;

            // 嘲讽与沉默互斥：被嘲讽时无法被沉默
            if (buff.gameEffect == GameEffect.Silence && HasActiveEffect(GameEffect.Taunt))
                return;

            // 位移型控制解除束缚型和仇恨型控制
            if (Combat.Effects.StatusEffectFactory.IsDisplacementControl(buff.gameEffect))
            {
                RemoveEffect(GameEffect.Root);
                RemoveEffect(GameEffect.Freeze);
                RemoveEffect(GameEffect.Taunt);
            }

            // 同一控制效果不叠加，取最高值（刷新持续时间）
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].gameEffect == buff.gameEffect &&
                    Combat.Effects.StatusEffectFactory.IsControlEffect(buff.gameEffect))
                {
                    var existing = ActiveBuffs[i];
                    // 取较高值
                    if (buff.effectParam1 > existing.effectParam1)
                        existing.effectParam1 = buff.effectParam1;
                    existing.remainingDuration = buff.remainingDuration;
                    ActiveBuffs[i] = existing;
                    return;
                }
            }

            // 查找是否已有同 id 的 buff
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].buffId == buff.buffId)
                {
                    var existing = ActiveBuffs[i];

                    switch (existing.stackRule)
                    {
                        case BuffStackRule.None:
                            existing.remainingDuration = buff.remainingDuration;
                            ActiveBuffs[i] = existing;
                            return;

                        case BuffStackRule.Stack:
                            if (existing.currentStacks < existing.maxStacks)
                            {
                                existing.currentStacks++;
                            }
                            existing.remainingDuration = buff.remainingDuration;
                            ActiveBuffs[i] = existing;
                            return;

                        case BuffStackRule.RefreshDuration:
                            existing.remainingDuration = buff.remainingDuration;
                            ActiveBuffs[i] = existing;
                            return;
                    }
                }
            }

            // 新 buff
            ActiveBuffs.Add(buff);
        }

        private int GetFreezeBreakDamage()
        {
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].gameEffect == GameEffect.Freeze && !ActiveBuffs[i].IsExpired)
                    return Mathf.Max(1, Mathf.RoundToInt(ActiveBuffs[i].effectParam2));
            }
            return 10;
        }

        private string GetOwnerLogName()
        {
            var fighter = OwnerFighter as Combat.Fighter.BattleFighter;
            return fighter == null ? "unknown" : $"{fighter.Name}(id={fighter.FighterId},camp={fighter.Camp})";
        }

        /// <summary>
        /// 移除指定 GameEffect 的所有 buff
        /// </summary>
        public void RemoveEffect(GameEffect effect)
        {
            if (ActiveBuffs == null) return;
            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                if (ActiveBuffs[i].gameEffect == effect)
                    ActiveBuffs.RemoveAt(i);
            }
        }

        public void MultiplyActiveEffectParam2(GameEffect effect, float multiplier)
        {
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].gameEffect == effect && !ActiveBuffs[i].IsExpired)
                    ActiveBuffs[i].effectParam2 *= multiplier;
            }
        }

        /// <summary>
        /// 每帧 tick 所有 buff：递减持续时间、执行 DoT、移除过期 buff
        /// </summary>
        public TickBuffResult TickBuffs(float deltaTime)
        {
            TickBuffResult result = new TickBuffResult();
            if (ActiveBuffs == null) return result;

            if (_pendingElementDamage > 0)
            {
                result.dotDamage += _pendingElementDamage;
                GameLogger.LogFileOnly("Element",
                    $"ResolveElementDamage target={GetOwnerLogName()} damage={_pendingElementDamage} hpBefore={CurrentHp}");
                _pendingElementDamage = 0;
            }

            int pendingHeal = 0;
            int pendingFreezeBreakDamage = 0;

            for (int i = ActiveBuffs.Count - 1; i >= 0; i--)
            {
                var buff = ActiveBuffs[i];

                // 永久 buff（duration == -1）不递减
                if (buff.remainingDuration > 0f)
                {
                    buff.remainingDuration -= deltaTime;
                }

                // DoT tick
                if (buff.tickInterval > 0f)
                {
                    buff.tickTimer += deltaTime;
                    if (buff.tickTimer >= buff.tickInterval)
                    {
                        buff.tickTimer -= buff.tickInterval;

                        switch (buff.gameEffect)
                        {
                            case GameEffect.Poison:
                            case GameEffect.Bleed:
                            case GameEffect.Burn:
                                int dotDmg = Mathf.RoundToInt(buff.effectParam1 * buff.currentStacks);
                                result.dotDamage += dotDmg;
                                if (buff.gameEffect == GameEffect.Burn)
                                {
                                    GameLogger.LogFileOnly("Element",
                                        $"DotTick target={GetOwnerLogName()} effect={buff.gameEffect} damage={dotDmg} stacks={buff.currentStacks} remaining={buff.remainingDuration:F2} hpBefore={CurrentHp}");
                                }
                                break;
                        }
                    }
                }

                // 治疗效果（立即回复）
                if (buff.gameEffect == GameEffect.Heal && buff.remainingDuration > 0f)
                {
                    pendingHeal += Mathf.RoundToInt(buff.effectParam1);
                    buff.remainingDuration = 0f;
                }

                // 冻结效果
                if (buff.gameEffect == GameEffect.Freeze && buff.remainingDuration > 0f)
                {
                    result.freezeDuration = Mathf.Max(result.freezeDuration, buff.remainingDuration);
                }

                // 减速效果
                if (buff.gameEffect == GameEffect.Slow)
                {
                    SpeedPercentDebuff = buff.effectParam1 * buff.currentStacks;
                    result.needsRecalculate = true;
                }

                // 过期检测
                bool expired = buff.remainingDuration <= 0f ||
                               buff.remainingDuration < 0.001f;

                if (expired)
                {
                    // 冰冻结束时受到破冰伤害
                    if (buff.gameEffect == GameEffect.Freeze)
                    {
                        pendingFreezeBreakDamage += Mathf.RoundToInt(buff.effectParam2);
                        GameLogger.LogFileOnly("Element",
                            $"FreezeExpired target={GetOwnerLogName()} breakDamage={buff.effectParam2:F0} hpBefore={CurrentHp}");
                    }

                    // 移除时清理
                    if (buff.gameEffect == GameEffect.Slow)
                    {
                        SpeedPercentDebuff = 0f;
                        result.needsRecalculate = true;
                    }

                    ActiveBuffs.RemoveAt(i);
                    result.needsRecalculate = true;
                    continue;
                }

                ActiveBuffs[i] = buff;
            }

            // 应用治疗
            if (pendingHeal > 0)
            {
                CurrentHp = Mathf.Min(MaxHp, CurrentHp + pendingHeal);
            }

            // 应用破冰伤害
            if (pendingFreezeBreakDamage > 0)
            {
                result.dotDamage += pendingFreezeBreakDamage;
            }

            return result;
        }

        /// <summary>
        /// 攻击命中时触发回调
        /// </summary>
        public void TriggerAttackEffects(object target)
        {
            // 由 BattleSimulation.ApplyAttackTriggeredEffects 处理
        }

        /// <summary>
        /// 死亡时触发回调
        /// </summary>
        public void TriggerDeathEffects()
        {
            // 预留：击杀触发、亡语等
        }

        /// <summary>
        /// 击杀时触发回调
        /// </summary>
        public void TriggerKillEffects(object victim)
        {
            // 预留：击杀回血、击杀护盾等
        }
    }
}
