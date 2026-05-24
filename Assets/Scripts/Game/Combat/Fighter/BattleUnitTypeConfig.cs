using UnityEngine;
using Camp;

namespace Combat.Fighter
{
    [CreateAssetMenu(fileName = "BattleUnitTypeConfig", menuName = "Game/Battle/Unit Type Config")]
    public class BattleUnitTypeConfig : ScriptableObject
    {
        [SerializeField] private int _unitTypeId;
        [SerializeField] private string _unitTypeName = "Unit";
        [SerializeField] private UnitStaticAttributes _baseAttributes = UnitStaticAttributes.Default;

        public int UnitTypeId => _unitTypeId;
        public string UnitTypeName => string.IsNullOrEmpty(_unitTypeName) ? name : _unitTypeName;
        public UnitStaticAttributes BaseAttributes => _baseAttributes;

        public UnitRuntimeAttributes CreateRuntimeAttributes()
        {
            return new UnitRuntimeAttributes(_baseAttributes);
        }
    }
}
