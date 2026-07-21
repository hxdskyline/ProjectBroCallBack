using UnityEngine;
using Camp;
using System.Collections.Generic;
using Combat.Avatar;

namespace Combat.Fighter
{
    public class BattleFighter : IBattleUnit
    {
        public string Name;
        public BattleCamp Camp;
        public BattleUnitTypeConfig UnitType;
        public UnitStaticAttributes StaticAttributes;
        public UnitRuntimeAttributes RuntimeAttributes { get; set; }
        public BattleAvatar Avatar;
        public Transform Transform;
        public float AttackCooldownTimer;
        public float PendingHitTimer;
        public BattleFighter PendingTarget;
        public float BaseScale;
        public bool IsDying;
        public bool IsRemoved;
        public float DeathTimer;
        public float FreezeTimer { get; set; }
        public TribeType TribeType;
        public int FighterId;
        public List<string> Tags;
        public List<int> InnateBuffIds;
        public bool HasDoubleHit;
        public bool IsInvulnerable;
        public bool IsStealthed;

        // ── 击退位移 ──
        public Vector3 KnockbackVelocity;   // 当前击退速度（米/秒）
        public float KnockbackRemaining;    // 剩余击退距离

        // ── 倒地状态 ──
        public float KnockdownTimer;        // 倒地剩余时间（秒），>0表示正在倒地
        public Vector3 KnockdownDir;       // 倒地方向
        public GameObject HitEffect;
        public GameObject FrozenEffect;
        public float HitEffectTimer;

        // ── 被动技能状态 ──
        public string SkillId;              // 当前生效的技能ID
        public int EnhanceLevel;            // 强化等级
        public float SkillTimer;            // 定时技能计时器
        public int AttackCount;             // 攻击次数计数器
        public bool SkillInitialized;       // 技能是否已初始化

        // ── 战斗统计 ──
        public int TotalDamageDealt;
        public int TotalDamageTaken;
        public int TotalHealingDone;

        public int CurrentHp => RuntimeAttributes?.CurrentHp ?? 0;
        public bool IsDead => CurrentHp <= 0;
        public bool IsAlive => !IsRemoved && !IsDying && CurrentHp > 0;
    }
}
