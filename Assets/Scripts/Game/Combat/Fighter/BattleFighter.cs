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
        public List<int> InnateBuffIds;
        public bool HasDoubleHit;
        public bool IsInvulnerable;
        public bool IsStealthed;
        public GameObject HitEffect;
        public float HitEffectTimer;

        // ── 战斗统计 ──
        public int TotalDamageDealt;
        public int TotalDamageTaken;
        public int TotalHealingDone;

        public int CurrentHp => RuntimeAttributes?.CurrentHp ?? 0;
        public bool IsDead => CurrentHp <= 0;
        public bool IsAlive => !IsRemoved && !IsDying && CurrentHp > 0;
    }
}
