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
            maxHp = 1000f;
            currentHp = 1000f;
            attack = 50f;
            attackRange = 10f;
            attackSpeed = 1f;
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

        private float _attackCooldown;
        private float _dropTimer;
        private const float DROP_INTERVAL = 1f; // 掉落间隔（秒）
        private const int DROP_AMOUNT = 10;      // 每次掉落数量

        // 事件
        public event Action<BillboardCamp, float> OnBillboardDamaged;
        public event Action<BillboardCamp> OnBillboardDestroyed;
        public event Action<BillboardCamp, BillboardState> OnBillboardStateChanged;
        public event Action<int> OnCurrencyDropped;

        public BillboardSystem()
        {
            var loader = Camp.TribeConfigLoader.Instance;

            var playerCfg = loader.GetFighterConfig(9001);
            _playerBillboard = new BillboardData
            {
                camp = BillboardCamp.Player,
                maxHp = playerCfg?.hp ?? 1000f,
                currentHp = playerCfg?.hp ?? 1000f,
                attack = playerCfg?.attack ?? 50f,
                attackRange = playerCfg?.attackRange ?? 10f,
                attackSpeed = playerCfg?.attackSpeed ?? 1f
            };

            var enemyCfg = loader.GetFighterConfig(9002);
            _enemyBillboard = new BillboardData
            {
                camp = BillboardCamp.Enemy,
                maxHp = enemyCfg?.hp ?? 200f,
                currentHp = enemyCfg?.hp ?? 200f,
                attack = enemyCfg?.attack ?? 0f,
                attackRange = enemyCfg?.attackRange ?? 10f,
                attackSpeed = enemyCfg?.attackSpeed ?? 1f
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
        /// 更新看板状态
        /// </summary>
        public void Update(float deltaTime, bool playerSoldiersAlive, bool enemySoldiersAlive)
        {
            // 更新我方看板状态
            UpdateBillboardState(_playerBillboard, playerSoldiersAlive, enemySoldiersAlive);

            // 更新敌方看板状态
            UpdateBillboardState(_enemyBillboard, enemySoldiersAlive, playerSoldiersAlive);

            // 更新攻击冷却
            _attackCooldown -= deltaTime;
            if (_attackCooldown < 0)
                _attackCooldown = 0;

            // 更新掉落计时器
            if (_enemyBillboard.state == BillboardState.Active)
            {
                _dropTimer += deltaTime;
                if (_dropTimer >= DROP_INTERVAL)
                {
                    _dropTimer -= DROP_INTERVAL;
                    DropCurrency();
                }
            }
        }

        /// <summary>
        /// 更新单个看板状态
        /// </summary>
        private void UpdateBillboardState(BillboardData billboard, bool ownSoldiersAlive, bool enemySoldiersAlive)
        {
            BillboardState newState = billboard.state;

            if (!ownSoldiersAlive)
            {
                // 我方小兵全灭，看板激活
                if (billboard.state == BillboardState.Dormant)
                {
                    newState = BillboardState.Active;
                }
            }
            else
            {
                // 我方小兵存活，看板休眠
                if (billboard.state == BillboardState.Active)
                {
                    newState = BillboardState.Dormant;
                }
            }

            // 敌方小兵全灭，敌方看板激活（可被攻击）
            if (!enemySoldiersAlive && billboard.camp == BillboardCamp.Enemy)
            {
                if (billboard.state == BillboardState.Dormant)
                {
                    newState = BillboardState.Active;
                }
            }

            if (newState != billboard.state)
            {
                billboard.state = newState;
                OnBillboardStateChanged?.Invoke(billboard.camp, newState);
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

                // 摧毁看板时，爆发大量货币
                if (camp == BillboardCamp.Enemy)
                {
                    int burstAmount = 500;
                    OnCurrencyDropped?.Invoke(burstAmount);
                }
            }
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
            if (_attackCooldown > 0)
                return null;

            // 找到最近的目标
            BattleFighter nearestTarget = FindNearestTarget(billboard, targets);
            if (nearestTarget == null)
                return null;

            // 执行攻击
            _attackCooldown = 1f / billboard.attackSpeed;

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
        /// 掉落货币
        /// </summary>
        private void DropCurrency()
        {
            OnCurrencyDropped?.Invoke(DROP_AMOUNT);
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

            _attackCooldown = 0;
            _dropTimer = 0;
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
