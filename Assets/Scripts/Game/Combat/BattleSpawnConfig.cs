using UnityEngine;
using Camp;
using Combat.Fighter;
using Combat.Avatar;

namespace Combat
{
    public struct BattleSpawnConfig
    {
        public GameObject FighterPrefab;
        public AvatarAnimationDefinition PlayerAvatarDefinition;
        public AvatarAnimationDefinition EnemyAvatarDefinition;
        public BattleFighterSpawnDefinition[] PlayerFighterDefinitions;
        public BattleUnitTypeConfig PlayerUnitType;
        public BattleUnitTypeConfig EnemyUnitType;
        public UnitStaticAttributes? EnemyStaticAttributes;

        public int FightersPerCamp;
        public int EnemyFighterCount;
        public Vector2 SpawnAreaMin;
        public Vector2 SpawnAreaMax;
        public float SpawnMinDistance;
        public int SpawnTryCount;
        public float FighterScale;

        public Color PlayerTint;
        public Color EnemyTint;
    }

    public struct BattleSpawnResult
    {
        public BattleFighter[] PlayerFighters;
        public BattleFighter[] EnemyFighters;
    }
}
