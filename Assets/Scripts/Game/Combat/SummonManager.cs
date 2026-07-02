using System.Collections.Generic;
using UnityEngine;
using Combat.Fighter;

namespace Combat
{
    /// <summary>
    /// 召唤物管理器 — 管理召唤物的生成、AI、生命周期
    /// </summary>
    public class SummonManager
    {
        private List<SummonRecord> _summons = new List<SummonRecord>();
        private BattleFighter[] _alliedFighters;
        private BattleFighter[] _enemyFighters;

        /// <summary>
        /// 当前召唤物数量
        /// </summary>
        public int Count => _summons.Count;

        /// <summary>
        /// 初始化，设置友军和敌军引用
        /// </summary>
        public void Initialize(BattleFighter[] allies, BattleFighter[] enemies)
        {
            _alliedFighters = allies;
            _enemyFighters = enemies;
        }

        /// <summary>
        /// 生成召唤物
        /// </summary>
        public void SpawnSummon(SummonData data, Vector3 position)
        {
            if (data == null) return;

            var record = new SummonRecord
            {
                data = data,
                position = position,
                spawnTime = Time.time,
                lifetime = data.lifetime,
                currentHp = data.hp,
                maxHp = data.hp,
                attack = data.attack,
                isAlive = true
            };

            _summons.Add(record);
            Debug.Log($"[SummonManager] Spawned summon: {data.summonName} at {position}, HP: {data.hp}");
        }

        /// <summary>
        /// Tick：更新召唤物状态
        /// </summary>
        public void Tick(float deltaTime)
        {
            for (int i = _summons.Count - 1; i >= 0; i--)
            {
                var summon = _summons[i];
                if (!summon.isAlive)
                {
                    _summons.RemoveAt(i);
                    continue;
                }

                summon.elapsed += deltaTime;

                // 检查生命周期
                if (summon.elapsed >= summon.lifetime)
                {
                    RemoveSummon(i);
                    continue;
                }

                // 简单 AI：攻击最近的敌人
                UpdateSummonAI(summon, deltaTime);
            }
        }

        /// <summary>
        /// 对召唤物造成伤害
        /// </summary>
        public void DamageSummon(int index, int damage)
        {
            if (index < 0 || index >= _summons.Count) return;
            var summon = _summons[index];
            if (!summon.isAlive) return;

            summon.currentHp -= damage;
            if (summon.currentHp <= 0)
            {
                summon.currentHp = 0;
                summon.isAlive = false;
                Debug.Log($"[SummonManager] Summon {summon.data.summonName} killed");
            }
        }

        /// <summary>
        /// 获取所有存活的召唤物
        /// </summary>
        public List<SummonRecord> GetAliveSummons()
        {
            var result = new List<SummonRecord>();
            foreach (var summon in _summons)
            {
                if (summon.isAlive)
                    result.Add(summon);
            }
            return result;
        }

        /// <summary>
        /// 获取友方召唤物数量
        /// </summary>
        public int GetAliveAllySummonCount()
        {
            int count = 0;
            foreach (var summon in _summons)
            {
                if (summon.isAlive && summon.data.isPlayerOwned)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 清空所有召唤物（战斗结束时调用）
        /// </summary>
        public void Clear()
        {
            _summons.Clear();
        }

        /// <summary>
        /// 召唤分身（铀235猫裂变用）
        /// </summary>
        public void SummonClone(BattleFighter source, int count)
        {
            if (source == null) return;
            var pos = source.Transform != null ? source.Transform.position : Vector3.zero;
            int cloneHp = Mathf.Max(1, source.StaticAttributes.MaxHp / 2);
            int cloneAtk = Mathf.Max(1, source.RuntimeAttributes.Attack / 2);

            for (int i = 0; i < count; i++)
            {
                var data = new SummonData
                {
                    summonName = source.Name + "_分身",
                    hp = cloneHp,
                    attack = cloneAtk,
                    moveSpeed = source.RuntimeAttributes.MoveSpeed,
                    attackSpeed = source.RuntimeAttributes.CorrectedAttackSpeed > 0 ? 1f / source.RuntimeAttributes.CorrectedAttackSpeed : 1f,
                    lifetime = -1f,
                    isPlayerOwned = source.Camp == BattleCamp.Player
                };
                Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f);
                SpawnSummon(data, pos + offset);
            }
        }

        /// <summary>
        /// 召唤骷髅猫（缝合猫用）
        /// </summary>
        public void SummonSkeleton(BattleFighter source)
        {
            if (source == null) return;
            var pos = source.Transform != null ? source.Transform.position : Vector3.zero;

            int skeletonHp = Mathf.Max(1, Mathf.RoundToInt(source.RuntimeAttributes.MaxHp * 0.2f));
            int skeletonAtk = source.RuntimeAttributes.Attack;

            var data = new SummonData
            {
                summonName = "骷髅猫",
                hp = skeletonHp,
                attack = skeletonAtk,
                moveSpeed = 4f,
                attackSpeed = 1f,
                lifetime = -1f,
                isPlayerOwned = source.Camp == BattleCamp.Player
            };
            Vector3 offset = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0f);
            SpawnSummon(data, pos + offset);
        }

        private void UpdateSummonAI(SummonRecord summon, float deltaTime)
        {
            if (_enemyFighters == null) return;

            // 找最近的敌人
            BattleFighter nearestEnemy = null;
            float nearestDist = float.MaxValue;

            foreach (var enemy in _enemyFighters)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                float dist = Vector3.Distance(summon.position, enemy.Transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestEnemy = enemy;
                }
            }

            if (nearestEnemy == null) return;

            // 移向敌人
            if (nearestDist > 1.0f)
            {
                Vector3 dir = (nearestEnemy.Transform.position - summon.position).normalized;
                summon.position += dir * summon.data.moveSpeed * deltaTime;
            }
            else
            {
                // 攻击
                summon.attackCooldown -= deltaTime;
                if (summon.attackCooldown <= 0)
                {
                    nearestEnemy.RuntimeAttributes.CurrentHp -= summon.attack;
                    summon.attackCooldown = summon.data.attackSpeed;

                    if (nearestEnemy.RuntimeAttributes.CurrentHp <= 0)
                    {
                        nearestEnemy.RuntimeAttributes.CurrentHp = 0;
                    }

                    Debug.Log($"[SummonManager] {summon.data.summonName} attacked {nearestEnemy.Name} for {summon.attack} damage");
                }
            }
        }

        private void RemoveSummon(int index)
        {
            if (index >= 0 && index < _summons.Count)
            {
                Debug.Log($"[SummonManager] Summon {_summons[index].data.summonName} expired");
                _summons.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// 召唤物数据（模板）
    /// </summary>
    public class SummonData
    {
        public string summonName;
        public int hp;
        public int attack;
        public float moveSpeed;
        public float attackSpeed;
        public float lifetime;
        public bool isPlayerOwned;
    }

    /// <summary>
    /// 召唤物运行时记录
    /// </summary>
    public class SummonRecord
    {
        public SummonData data;
        public Vector3 position;
        public float spawnTime;
        public float lifetime;
        public float elapsed;
        public int currentHp;
        public int maxHp;
        public int attack;
        public float attackCooldown;
        public bool isAlive;
    }
}
