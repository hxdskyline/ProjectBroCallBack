using UnityEngine;
using Camp;
using System;

namespace Combat.Fighter
{
    /// <summary>
    /// 战斗子弹 - 从攻击者飞向目标，到达时造成伤害
    /// </summary>
    public class BattleBullet : MonoBehaviour
    {
        private BattleFighter _attacker;
        private BattleFighter _target;
        private int _damage;
        private bool _isCritical;
        private float _speed = 12f;
        private bool _hasHit;

        /// <summary>
        /// 子弹命中后触发弹射的回调
        /// </summary>
        private Action<BattleFighter, BattleFighter, Vector3> _onHitBounceCallback;

        public void Setup(BattleFighter attacker, BattleFighter target, int damage,
            bool isCritical = false,
            Action<BattleFighter, BattleFighter, Vector3> onHitBounce = null)
        {
            _attacker = attacker;
            _target = target;
            _damage = damage;
            _isCritical = isCritical;
            _hasHit = false;
            _onHitBounceCallback = onHitBounce;

            var sr = gameObject.GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.color = Color.white;
            sr.sortingOrder = 100;

            var sprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>("2deffect/changmao");
            if (sprite != null)
                sr.sprite = sprite;
            else
                sr.sprite = CreateSquareSprite();

            transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        }

        private void Update()
        {
            if (_hasHit) return;

            if (_target == null || !_target.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 targetPos = _target.Transform.position;
            Vector3 dir = (targetPos - transform.position).normalized;
            transform.position += dir * (_speed * Time.deltaTime);

            float dist = Vector3.Distance(transform.position, targetPos);
            if (dist < 0.2f)
            {
                _hasHit = true;
                ApplyDamage();
                Destroy(gameObject);
            }
        }

        private void ApplyDamage()
        {
            if (_target == null || !_target.IsAlive) return;

            var defenderRuntime = _target.RuntimeAttributes;
            if (defenderRuntime == null) return;

            int oldHp = defenderRuntime.CurrentHp;
            int newHp = Mathf.Max(0, oldHp - _damage);
            defenderRuntime.CurrentHp = newHp;

            // 战斗统计
            if (_attacker != null)
                _attacker.TotalDamageDealt += _damage;
            _target.TotalDamageTaken += _damage;

            // 显示伤害数字
            if (_target.Transform != null)
            {
                var hud = _target.Transform.GetComponent<FighterHUD>();
                if (hud != null)
                {
                    hud.ShowDamage(_damage, _isCritical);
                    hud.UpdateHp(defenderRuntime.CurrentHp);
                }
                BattleSimulation.ShowHitEffect(_target);
            }

            // 死亡处理
            if (defenderRuntime.CurrentHp <= 0)
            {
                var sim = BattleSimulation.CurrentSimulation;
                if (sim != null)
                {
                    sim.StartDeath(_target);
                    if (_target.IsDying)
                        sim.NotifyConfirmedKill(_attacker, _target);
                }
                else
                    _target.IsDying = true;
            }

            // 击退判定（近战范围内）
            BattleSimulation.ApplyMeleeKnockback(_attacker, _target);

            // 攻击命中时触发状态效果（弹射、施毒等）
            BattleSimulation.ApplyAttackTriggeredEffects(_attacker, _target);

            // IBuffEffect.OnAttackHit 回调
            if (_attacker?.RuntimeAttributes != null)
                _attacker.RuntimeAttributes.TriggerAttackEffects(_target);

            // 子弹命中后触发弹射（从命中位置发射新子弹）
            if (_onHitBounceCallback != null && _target.Transform != null)
            {
                _onHitBounceCallback.Invoke(_attacker, _target, _target.Transform.position);
            }
        }

        private static Sprite CreateSquareSprite()
        {
            var tex = new Texture2D(4, 4);
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100);
        }
    }
}
