using UnityEngine;
using Camp;

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

        public void Setup(BattleFighter attacker, BattleFighter target, int damage, bool isCritical = false)
        {
            _attacker = attacker;
            _target = target;
            _damage = damage;
            _isCritical = isCritical;
            _hasHit = false;

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

            int newHp = Mathf.Max(0, defenderRuntime.CurrentHp - _damage);
            defenderRuntime.CurrentHp = newHp;

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

            if (defenderRuntime.CurrentHp <= 0)
            {
                _target.IsDying = true;
            }

            // 攻击触发状态效果
            BattleSimulation.ApplyAttackTriggeredEffects(_attacker, _target);

            // IBuffEffect.OnAttackHit 回调（穿刺箭、毒箭等）
            if (_attacker?.RuntimeAttributes != null)
                _attacker.RuntimeAttributes.TriggerAttackEffects(_target);
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
