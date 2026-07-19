using UnityEngine;
using Camp;
using System.Collections.Generic;
using Combat.Avatar;

namespace Combat.Fighter
{
    [System.Serializable]
    public struct BattleFighterSpawnDefinition
    {
        public string Name;
        public UnitStaticAttributes StaticAttributes;
        public AvatarAnimationDefinition AvatarDefinition;
        public float ScaleMultiplier;
        public TribeType TribeType;
        public int FighterId; // fighter_config.json 中的 fighterId
        public List<UnifiedBuff> AuraBuffs; // 从 FighterData 传入的光环 buff
        public int EnhanceLevel; // 强化等级（0或1）
        public int CurrentHp;   // 战斗开始时的HP，0表示满血
        public int DeployZones; // 部署区域位标志（inner=1, middle=2, outer=4）

        public BattleFighterSpawnDefinition(string name, UnitStaticAttributes staticAttributes, AvatarAnimationDefinition avatarDefinition = null, float scaleMultiplier = 1.0f, TribeType tribeType = TribeType.Tabby, int fighterId = 0)
        {
            Name = name;
            StaticAttributes = staticAttributes;
            AvatarDefinition = avatarDefinition;
            ScaleMultiplier = scaleMultiplier;
            TribeType = tribeType;
            FighterId = fighterId;
            AuraBuffs = null;
            EnhanceLevel = 0;
            CurrentHp = 0;
            DeployZones = 0;
        }
    }
}
