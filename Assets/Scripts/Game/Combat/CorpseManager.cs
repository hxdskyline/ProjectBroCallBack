using System.Collections.Generic;
using UnityEngine;
using Combat.Fighter;

namespace Combat
{
    /// <summary>
    /// 尸体管理器 — 追踪战场上的尸体，支持消耗和存储
    /// </summary>
    public class CorpseManager
    {
        private List<CorpseRecord> _corpses = new List<CorpseRecord>();
        private int _maxCorpses = 50;

        /// <summary>
        /// 当前尸体数量
        /// </summary>
        public int Count => _corpses.Count;

        /// <summary>
        /// 添加一具尸体
        /// </summary>
        public void AddCorpse(BattleFighter fighter, Vector3 position, bool isPlayerUnit)
        {
            if (fighter == null) return;

            var record = new CorpseRecord
            {
                fighter = fighter,
                position = position,
                isPlayerUnit = isPlayerUnit,
                spawnTime = Time.time,
                lifetime = 30f // 尸体 30 秒后消失
            };

            _corpses.Add(record);

            // 超出上限时移除最旧的
            while (_corpses.Count > _maxCorpses)
            {
                _corpses.RemoveAt(0);
            }

            Debug.Log($"[CorpseManager] Corpse added at {position}, total: {_corpses.Count}");
        }

        /// <summary>
        /// 消耗一具尸体（返回是否成功）
        /// </summary>
        public bool ConsumeCorpse()
        {
            if (_corpses.Count == 0) return false;

            // 消耗最新的尸体
            int idx = _corpses.Count - 1;
            _corpses.RemoveAt(idx);
            Debug.Log($"[CorpseManager] Corpse consumed, remaining: {_corpses.Count}");
            return true;
        }

        /// <summary>
        /// 消耗指定索引的尸体（返回是否成功）
        /// </summary>
        public bool ConsumeCorpseAt(int index)
        {
            if (index < 0 || index >= _corpses.Count) return false;
            _corpses.RemoveAt(index);
            Debug.Log($"[CorpseManager] Corpse at {index} consumed, remaining: {_corpses.Count}");
            return true;
        }

        /// <summary>
        /// 消耗最新的一具友方尸体（返回被消耗的尸体，无则返回 null）
        /// </summary>
        public CorpseRecord ConsumeLatestPlayerCorpse()
        {
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                if (_corpses[i].isPlayerUnit)
                {
                    var record = _corpses[i];
                    _corpses.RemoveAt(i);
                    Debug.Log($"[CorpseManager] Player corpse consumed, remaining: {_corpses.Count}");
                    return record;
                }
            }
            return null;
        }

        /// <summary>
        /// 消耗指定位置附近的尸体
        /// </summary>
        public bool ConsumeCorpseNear(Vector3 position, float range)
        {
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(_corpses[i].position, position) <= range)
                {
                    _corpses.RemoveAt(i);
                    Debug.Log($"[CorpseManager] Corpse consumed near {position}, remaining: {_corpses.Count}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取指定范围内的尸体数量
        /// </summary>
        public int GetCorpseCountNear(Vector3 position, float range)
        {
            int count = 0;
            foreach (var corpse in _corpses)
            {
                if (Vector3.Distance(corpse.position, position) <= range)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取所有玩家尸体
        /// </summary>
        public List<CorpseRecord> GetPlayerCorpses()
        {
            var result = new List<CorpseRecord>();
            foreach (var corpse in _corpses)
            {
                if (corpse.isPlayerUnit)
                    result.Add(corpse);
            }
            return result;
        }

        /// <summary>
        /// 获取所有敌人尸体
        /// </summary>
        public List<CorpseRecord> GetEnemyCorpses()
        {
            var result = new List<CorpseRecord>();
            foreach (var corpse in _corpses)
            {
                if (!corpse.isPlayerUnit)
                    result.Add(corpse);
            }
            return result;
        }

        /// <summary>
        /// Tick：移除过期尸体
        /// </summary>
        public void Tick(float deltaTime)
        {
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                _corpses[i].elapsed += deltaTime;
                if (_corpses[i].elapsed >= _corpses[i].lifetime)
                {
                    _corpses.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 清空所有尸体（战斗结束时调用）
        /// </summary>
        public void Clear()
        {
            _corpses.Clear();
        }
    }

    /// <summary>
    /// 尸体记录
    /// </summary>
    public class CorpseRecord
    {
        public BattleFighter fighter;
        public Vector3 position;
        public bool isPlayerUnit;
        public float spawnTime;
        public float lifetime;
        public float elapsed;
    }
}
