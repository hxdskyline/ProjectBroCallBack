using UnityEngine;

namespace Combat.Fighter
{
    /// <summary>
    /// 看板→小兵的子弹，到达目标位置时对目标造成伤害
    /// </summary>
    public class BillboardToFighterBullet : MonoBehaviour
    {
        private BattleFighter _target;
        private BillboardCamp _attackerCamp;
        private int _damage;
        private BattleSimulation _simulation;
        private float _speed = 12f;
        private bool _hasHit;

        public void Setup(BattleFighter target, BillboardCamp attackerCamp, int damage, BattleSimulation simulation)
        {
            _target = target;
            _attackerCamp = attackerCamp;
            _damage = damage;
            _simulation = simulation;
            _hasHit = false;
        }

        private void Update()
        {
            if (_hasHit || _target == null || _target.IsDead || _target.Transform == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 targetPos = _target.Transform.position;
            Vector3 dir = (targetPos - transform.position).normalized;
            transform.position += dir * (_speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPos) < 0.3f)
            {
                _hasHit = true;
                ApplyDamage();
                Destroy(gameObject);
            }
        }

        private void ApplyDamage()
        {
            if (_target == null || _target.IsDead || _target.IsDying) return;

            int newHp = Mathf.Max(0, _target.RuntimeAttributes.CurrentHp - _damage);
            _target.RuntimeAttributes.CurrentHp = newHp;

            // 战斗统计
            _target.TotalDamageTaken += _damage;

            if (newHp <= 0)
            {
                _simulation?.StartDeath(_target);
            }

            var hud = _target.Transform?.GetComponent<FighterHUD>();
            if (hud != null)
                hud.UpdateHp(newHp);
        }
    }
}
