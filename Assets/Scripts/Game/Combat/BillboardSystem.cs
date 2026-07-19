using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.Fighter;

namespace Combat
{
    /// <summary>
    /// 看板状态
    /// </summary>
    public enum BillboardState
    {
        Dormant,    // 休眠（不攻击、不可被攻击、无敌）
        Active      // 激活（开始攻击，可被攻击）
    }

    /// <summary>
    /// 看板阵营
    /// </summary>
    public enum BillboardCamp
    {
        Player,     // 我方看板
        Enemy       // 敌方看板
    }

    /// <summary>
    /// 看板数据
    /// </summary>
    [Serializable]
    public class BillboardData
    {
        public BillboardCamp camp;
        public BillboardState state;
        public float maxHp;
        public float currentHp;
        public float attack;
        public float attackRange;
        public float attackSpeed;
        public Vector3 position;

        public BillboardData()
        {
            camp = BillboardCamp.Player;
            state = BillboardState.Dormant;
            maxHp = 100f;
            currentHp = 100f;
            attack = 5f;
            attackRange = 5f;
            attackSpeed = 0.5f;
            position = Vector3.zero;
        }
    }

    /// <summary>
    /// 看板系统 - 管理战场后方的大型单位
    /// 看板拥有极高血量和可观攻击力，战斗中根据条件激活或休眠
    /// </summary>
    public class BillboardSystem
    {
        private BillboardData _playerBillboard;
        private BillboardData _enemyBillboard;

        private float _playerAttackCooldown;
        private float _enemyAttackCooldown;

        // 事件
        public event Action<BillboardCamp, float> OnBillboardDamaged;
        public event Action<BillboardCamp> OnBillboardDestroyed;
        public event Action<BillboardCamp, BillboardState> OnBillboardStateChanged;

        public BillboardSystem()
        {
            var loader = Camp.TribeConfigLoader.Instance;

            var playerCfg = loader.GetFighterConfig(9001);
            _playerBillboard = new BillboardData
            {
                camp = BillboardCamp.Player,
                maxHp = playerCfg?.hp ?? 100f,
                currentHp = playerCfg?.hp ?? 100f,
                attack = playerCfg?.attack ?? 5f,
                attackRange = playerCfg?.attackRange ?? 5f,
                attackSpeed = playerCfg?.attackSpeed ?? 0.5f
            };

            var enemyCfg = loader.GetFighterConfig(9002);
            _enemyBillboard = new BillboardData
            {
                camp = BillboardCamp.Enemy,
                maxHp = enemyCfg?.hp ?? 100f,
                currentHp = enemyCfg?.hp ?? 100f,
                attack = enemyCfg?.attack ?? 5f,
                attackRange = enemyCfg?.attackRange ?? 5f,
                attackSpeed = enemyCfg?.attackSpeed ?? 0.5f
            };
        }

        /// <summary>
        /// 初始化看板
        /// </summary>
        public void Initialize(Vector3 playerPosition, Vector3 enemyPosition)
        {
            _playerBillboard.position = playerPosition;
            _enemyBillboard.position = enemyPosition;

            _playerBillboard.state = BillboardState.Dormant;
            _enemyBillboard.state = BillboardState.Dormant;

            _playerBillboard.currentHp = _playerBillboard.maxHp;
            _enemyBillboard.currentHp = _enemyBillboard.maxHp;
        }

        /// <summary>
        /// 获取看板数据
        /// </summary>
        public BillboardData GetBillboard(BillboardCamp camp)
        {
            return camp == BillboardCamp.Player ? _playerBillboard : _enemyBillboard;
        }

        /// <summary>
        /// 更新看板状态和攻击冷却
        /// </summary>
        public void Update(float deltaTime, bool playerSoldiersAlive, bool enemySoldiersAlive)
        {
            // 更新看板状态（一方全灭则该方看板激活，只激活一个）
            UpdateBillboardStates(playerSoldiersAlive, enemySoldiersAlive);

            // 更新攻击冷却
            _playerAttackCooldown -= deltaTime;
            if (_playerAttackCooldown < 0)
                _playerAttackCooldown = 0;

            _enemyAttackCooldown -= deltaTime;
            if (_enemyAttackCooldown < 0)
                _enemyAttackCooldown = 0;
        }

        /// <summary>
        /// 是否已有任一看板激活
        /// </summary>
        public bool HasAnyBillboardActivated()
        {
            return _playerBillboard.state == BillboardState.Active ||
                   _enemyBillboard.state == BillboardState.Active;
        }

        /// <summary>
        /// 更新看板状态
        /// 规则：当己方小兵全灭且对方看板未激活时，己方看板激活
        /// </summary>
        public void UpdateBillboardStates(bool playerSoldiersAlive, bool enemySoldiersAlive)
        {
            // 已有看板激活则不再切换
            if (HasAnyBillboardActivated()) return;

            // 敌方小兵全灭 → 敌方看板激活
            if (!enemySoldiersAlive)
            {
                _enemyBillboard.state = BillboardState.Active;
                OnBillboardStateChanged?.Invoke(BillboardCamp.Enemy, BillboardState.Active);
            }
            // 我方小兵全灭 → 我方看板激活
            else if (!playerSoldiersAlive)
            {
                _playerBillboard.state = BillboardState.Active;
                OnBillboardStateChanged?.Invoke(BillboardCamp.Player, BillboardState.Active);
            }
        }

        /// <summary>
        /// 看板受到伤害
        /// </summary>
        public void DamageBillboard(BillboardCamp camp, float damage)
        {
            BillboardData billboard = GetBillboard(camp);

            // 休眠状态下不可被攻击
            if (billboard.state == BillboardState.Dormant)
                return;

            billboard.currentHp -= damage;
            OnBillboardDamaged?.Invoke(camp, damage);

            if (billboard.currentHp <= 0)
            {
                billboard.currentHp = 0;
                OnBillboardDestroyed?.Invoke(camp);
            }
        }

        /// <summary>
        /// 获取敌方看板剩余血量百分比（0~1）
        /// </summary>
        public float GetEnemyBillboardHpPercent()
        {
            if (_enemyBillboard.maxHp <= 0) return 0f;
            return Mathf.Clamp01(_enemyBillboard.currentHp / _enemyBillboard.maxHp);
        }

        /// <summary>
        /// 根据敌方看板剩余血量百分比计算货币奖励
        /// 100%血量 = 0个，0%血量 = 200个，线性插值，向上取整
        /// </summary>
        public int CalculateBillboardCurrencyReward()
        {
            float hpPercent = GetEnemyBillboardHpPercent();
            // 剩余血量百分比越高，奖励越少
            // 100% -> 0, 0% -> 200
            float reward = (1f - hpPercent) * 200f;
            return Mathf.CeilToInt(reward);
        }

        /// <summary>
        /// 看板攻击
        /// </summary>
        public BillboardAttackResult Attack(BillboardCamp camp, List<BattleFighter> targets)
        {
            BillboardData billboard = GetBillboard(camp);

            // 休眠状态下不攻击
            if (billboard.state == BillboardState.Dormant)
                return null;

            // 检查攻击冷却
            float cooldown = camp == BillboardCamp.Player ? _playerAttackCooldown : _enemyAttackCooldown;
            if (cooldown > 0)
                return null;

            // 找到最近的目标
            BattleFighter nearestTarget = FindNearestTarget(billboard, targets);
            if (nearestTarget == null)
                return null;

            // 执行攻击
            if (camp == BillboardCamp.Player)
                _playerAttackCooldown = 1f / billboard.attackSpeed;
            else
                _enemyAttackCooldown = 1f / billboard.attackSpeed;

            return new BillboardAttackResult
            {
                attacker = billboard,
                target = nearestTarget,
                damage = billboard.attack
            };
        }

        /// <summary>
        /// 找到最近的目标
        /// </summary>
        private BattleFighter FindNearestTarget(BillboardData billboard, List<BattleFighter> targets)
        {
            BattleFighter nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive)
                    continue;

                float distance = Vector3.Distance(billboard.position, target.Transform.position);
                if (distance < nearestDistance && distance <= billboard.attackRange)
                {
                    nearestDistance = distance;
                    nearest = target;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 重置看板
        /// </summary>
        public void Reset()
        {
            _playerBillboard.currentHp = _playerBillboard.maxHp;
            _enemyBillboard.currentHp = _enemyBillboard.maxHp;

            _playerBillboard.state = BillboardState.Dormant;
            _enemyBillboard.state = BillboardState.Dormant;

            _playerAttackCooldown = 0;
            _enemyAttackCooldown = 0;
        }
    }

    /// <summary>
    /// 看板攻击结果
    /// </summary>
    public class BillboardAttackResult
    {
        public BillboardData attacker;
        public BattleFighter target;
        public float damage;
    }
}
