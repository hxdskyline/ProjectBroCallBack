using UnityEngine;

namespace Combat.Fighter
{
    /// <summary>
    /// 看板子弹 - 从攻击者飞向看板位置，到达时造成伤害
    /// </summary>
    public class BillboardBullet : MonoBehaviour
    {
        private BillboardSystem _billboardSystem;
        private BillboardCamp _targetCamp;
        private float _damage;
        private Vector3 _targetPos;
        private float _speed = 12f;
        private bool _hasHit;

        public void Setup(Vector3 startPos, Vector3 targetPos, BillboardSystem billboardSystem, BillboardCamp camp, float damage)
        {
            _targetPos = targetPos;
            _billboardSystem = billboardSystem;
            _targetCamp = camp;
            _damage = damage;
            _hasHit = false;
            transform.position = startPos;

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

            Vector3 dir = (_targetPos - transform.position).normalized;
            transform.position += dir * (_speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _targetPos) < 0.3f)
            {
                _hasHit = true;
                _billboardSystem?.DamageBillboard(_targetCamp, _damage);
                Destroy(gameObject);
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
