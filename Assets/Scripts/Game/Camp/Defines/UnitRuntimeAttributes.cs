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

            CorrectedAttackSpeed = Mathf.Max(0.1f, _baseAttackSpeed);
            AttackRange = Mathf.Max(0.1f, _baseAttackRange);
        }

        /// <summary>
        /// 添加 buff
        /// </summary>
        public void ApplyBuff(UnifiedBuff buff)
        {
            if (buff == null) return;

            // 查找是否已有同 id 的 buff
            for (int i = 0; i < ActiveBuffs.Count; i++)
            {
                if (ActiveBuffs[i].buffId == buff.buffId)
                {
                    var existing = ActiveBuffs[i];

                    switch (existing.stackRule)
                    {
                        case BuffStackRule.None:
                            // 刷新持续时间
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

        /// <summary>
        /// 每帧 tick 所有 buff：递减持续时间、执行 DoT、移除过期 buff
        /// </summary>
        public TickBuffResult TickBuffs(float deltaTime)
        {
            TickBuffResult result = new TickBuffResult();
            if (ActiveBuffs == null) return result;

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
                                break;
                        }
                    }
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

                // 过期移除
                if (buff.remainingDuration >= 0f && buff.remainingDuration < 0f)
                {
                    // 浮点误差容忍
                }
                bool expired = buff.remainingDuration == 0f ||
                               (buff.remainingDuration > 0f && buff.remainingDuration < 0.001f);

                if (expired)
                {
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
