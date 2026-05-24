using UnityEngine;

namespace Combat.Avatar
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SortByY : MonoBehaviour
    {
        [Tooltip("Base sorting order to offset from (keeps groups separated)")]
        public int BaseOrder = 0;

        [Tooltip("Multiplier applied to Y position when computing order. Higher gives finer granularity.")]
        public int Multiplier = 100;

        [Tooltip("Enable Y-based scale effect (bottom +15%, top -15%)")]
        public bool EnableScaleEffect = true;

        private SpriteRenderer _renderer;
        private Vector3 _baseScale;

        private const float SCALE_RANGE = 0.08f; // ±8%
        private const float Y_RANGE = 3.5f;       // 屏幕上下边界范围

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            }
            _baseScale = transform.localScale;
        }

        private void LateUpdate()
        {
            if (_renderer == null)
            {
                return;
            }

            float currentY = transform.position.y;

            // 按 Y 排序
            int order = BaseOrder + Mathf.RoundToInt(-currentY * Multiplier);
            if (_renderer.sortingOrder != order)
            {
                _renderer.sortingOrder = order;
            }

            // 缩放：屏幕下方 +15%，屏幕上方 -15%
            if (EnableScaleEffect)
            {
                float normalizedY = Mathf.Clamp(currentY / Y_RANGE, -1f, 1f); // -1=底部, +1=顶部
                float scaleMultiplier = 1f - normalizedY * SCALE_RANGE;      // 底部=1.15, 顶部=0.85
                // 保留当前 X 朝向符号（UpdateFacing 设置的），只应用 Y 缩放
                float currentXSign = transform.localScale.x >= 0f ? 1f : -1f;
                float baseAbsX = Mathf.Abs(_baseScale.x);
                float baseY = _baseScale.y;
                Vector3 targetScale = new Vector3(currentXSign * baseAbsX * scaleMultiplier, baseY * scaleMultiplier, 1f);
                if (transform.localScale != targetScale)
                {
                    transform.localScale = targetScale;
                }
            }
        }
    }
}
