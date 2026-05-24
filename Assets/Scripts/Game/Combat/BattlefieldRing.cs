using UnityEngine;
using UnityEngine.UI;

namespace Combat
{
    /// <summary>
    /// 环形战场 UI — 显示战斗双方的血条和状态
    /// </summary>
    public class BattlefieldRing : MonoBehaviour
    {
        private Image _playerHpBar;
        private Image _enemyHpBar;

        public void Initialize(Canvas canvas, RectTransform parent)
        {
            // 玩家血条
            var playerGo = CreateBar("PlayerHpBar", new Color(0.3f, 0.8f, 0.3f), new Vector2(0, -280));
            _playerHpBar = playerGo;

            // 敌方血条
            var enemyGo = CreateBar("EnemyHpBar", new Color(0.9f, 0.3f, 0.3f), new Vector2(0, 260));
            _enemyHpBar = enemyGo;
        }

        public void UpdateBillboardState(bool isPlayer, bool isActive, float currentHp, float maxHp)
        {
            var bar = isPlayer ? _playerHpBar : _enemyHpBar;
            if (bar != null && maxHp > 0)
            {
                bar.fillAmount = currentHp / maxHp;
            }
        }

        private Image CreateBar(string name, Color color, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600, 20);
            rect.anchoredPosition = position;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fill = fillGo.AddComponent<Image>();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return fill;
        }
    }
}
