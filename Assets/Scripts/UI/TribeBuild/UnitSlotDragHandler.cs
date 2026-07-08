using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Camp;
using Combat;

namespace UI.TribeBuild
{
    /// <summary>
    /// 战斗准备阶段 — 单位槽拖放组件
    /// 从底部单位栏拖拽到战场上放置
    /// </summary>
    public class UnitSlotDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private FighterData _unitData;
        private FighterConfig _config;
        private BattlePreparePanel _panel;
        private GameObject _worldPreview;
        private RectTransform _slotRect;

        private const float FIGHTER_SCALE = 0.45f;

        public void Setup(FighterData unit, FighterConfig config, BattlePreparePanel panel)
        {
            _unitData = unit;
            _config = config;
            _panel = panel;
            _slotRect = GetComponent<RectTransform>();

            var nameTxt = transform.Find("Name")?.GetComponent<Text>();
            if (nameTxt != null && _unitData != null && _config != null)
            {
                int displayLevel = _unitData.tier > 0 ? _unitData.tier : _config.tier;
                int maxHp = _config.GetEffectiveMaxHp(_unitData.enhanceLevel);
                int currentHp = Mathf.Clamp(Mathf.RoundToInt(_unitData.currentHp), 0, maxHp);
                nameTxt.fontSize = 15;
                nameTxt.text = $"{_config.fighterName}\nLv:{displayLevel}  HP:{currentHp}/{maxHp}";
            }
        }

        public FighterData UnitData => _unitData;
        public FighterConfig Config => _config;
        public RectTransform SlotRect => _slotRect;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_panel == null) return;

            // 检查人口是否超限（含正在拖拽的）
            int popCost = _config.populationCost > 0 ? _config.populationCost : 1;
            if (!_panel.CanDeployPopulation(popCost))
            {
                _worldPreview = null;
                return;
            }

            // 通知面板开始拖拽（更新人口格显示）
            _panel.OnDragStart(popCost);

            // 在世界空间创建半透明预览
            _worldPreview = new GameObject("DragPreview");
            var sr = _worldPreview.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.6f, 0.9f, 1f, 0.6f);
            sr.sortingOrder = 500;

            string spriteAddress = $"avatartemp/{_config.avatarId}1";
            var sprite = GameManager.Instance.ResourceManager.LoadResource<Sprite>(spriteAddress);
            if (sprite != null)
                sr.sprite = sprite;

            _worldPreview.transform.localScale = new Vector3(FIGHTER_SCALE, FIGHTER_SCALE, 1f);

            var col = _worldPreview.AddComponent<BoxCollider2D>();
            if (sprite != null)
            {
                float ppu = sprite.pixelsPerUnit > 0 ? sprite.pixelsPerUnit : 100f;
                col.size = new Vector2(sprite.rect.width / ppu, sprite.rect.height / ppu);
            }
            else
            {
                col.size = new Vector2(4f, 4f);
            }

            // 立即更新位置
            UpdatePreviewPosition();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_worldPreview == null) return;
            UpdatePreviewPosition();

            // 实时更新区域高亮
            Vector3 worldPos = ScreenToWorldPoint(Input.mousePosition);
            _panel.OnDragUpdate(worldPos, _config);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_panel != null)
                _panel.OnDragEnd();

            if (_worldPreview == null) return;

            Vector3 worldPos = ScreenToWorldPoint(Input.mousePosition);

            // 检查是否在战场外圈椭圆内
            int zone = Combat.BattleManager.GetDeployZone(worldPos);
            bool inBattlefield = zone != 0;

            // 检查单位是否允许部署在该区域
            bool zoneAllowed = (zone & _config.deployZones) != 0;

            string zoneName = zone == 1 ? "内" : zone == 2 ? "中" : zone == 4 ? "外" : "场外";
            GameLogger.Log("Drag", $"[{_config.fighterName}] pos=({worldPos.x:F1},{worldPos.y:F1}) zone={zoneName}({zone}) allowed={_config.deployZones} pass={inBattlefield && zoneAllowed}");

            if (inBattlefield && zoneAllowed && _panel != null)
            {
                _panel.TryDeployUnit(_unitData, _config, worldPos, this, _worldPreview);
            }
            else
            {
                Destroy(_worldPreview);
            }

            _worldPreview = null;
        }

        private void UpdatePreviewPosition()
        {
            if (_worldPreview == null) return;

            Vector3 worldPos = ScreenToWorldPoint(Input.mousePosition);
            _worldPreview.transform.position = worldPos;
        }

        private static Vector3 ScreenToWorldPoint(Vector2 screenPos)
        {
            Vector3 sp = new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z);
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(sp);
            worldPos.z = 0f;
            return worldPos;
        }
    }
}
