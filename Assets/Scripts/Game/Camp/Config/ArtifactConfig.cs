using System.Collections.Generic;

namespace Camp
{
    /// <summary>
    /// 奇物配置
    /// </summary>
    [System.Serializable]
    public class ArtifactConfig
    {
        public string id;
        public string name;
        public string description;
        public string scope;        // "all" / "tribe" / 等
        public string subType;      // 特效类型：KillHeal / DamageReduce / LowHpBonus / KillShield 等
        public List<BuffEffectItem> effects;
    }
}
