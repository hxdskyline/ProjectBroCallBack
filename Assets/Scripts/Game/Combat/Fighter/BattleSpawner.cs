using System.Collections.Generic;
using UnityEngine;
using Camp;
using Combat.Avatar;

namespace Combat.Fighter
{
    public static class BattleSpawner
    {
        public static BattleSpawnResult Spawn(Transform parent, BattleSpawnConfig config)
        {
            int fightersPerCamp = Mathf.Max(1, config.FightersPerCamp);
            int enemyFighterCount = Mathf.Max(1, config.EnemyFighterCount > 0 ? config.EnemyFighterCount : fightersPerCamp);
            int playerCount = HasCustomPlayerDefinitions(config)
                ? config.PlayerFighterDefinitions.Length
                : fightersPerCamp;
            List<Vector3> occupiedPositions = new List<Vector3>(Mathf.Max(1, playerCount + enemyFighterCount));

            BattleFighter[] playerFighters = HasCustomPlayerDefinitions(config)
                ? CreateFighterGroupFromDefinitions(
                    parent,
                    BattleCamp.Player,
                    config.PlayerFighterDefinitions,
                    config.PlayerAvatarDefinition,
                    occupiedPositions,
                    true,
                    config.PlayerTint,
                    config)
                : CreateFighterGroup(
                    parent,
                    "PlayerAvatar",
                    BattleCamp.Player,
                    config.PlayerUnitType,
                    config.PlayerAvatarDefinition,
                    fightersPerCamp,
                    occupiedPositions,
                    true,
                    config.PlayerTint,
                    config);

            BattleFighter[] enemyFighters = CreateFighterGroup(
                parent,
                "EnemyAvatar",
                BattleCamp.Enemy,
                config.EnemyUnitType,
                config.EnemyAvatarDefinition,
                enemyFighterCount,
                occupiedPositions,
                false,
                config.EnemyTint,
                config,
                config.EnemyStaticAttributes);

            LoadGroupIdle(playerFighters);
            LoadGroupIdle(enemyFighters);

            // 设置每个战斗单位的 OwnerFighter / Allies / Enemies，供 IBuffEffect 回调使用
            for (int i = 0; i < playerFighters.Length; i++)
            {
                if (playerFighters[i]?.RuntimeAttributes != null)
                {
                    playerFighters[i].RuntimeAttributes.OwnerFighter = playerFighters[i];
                    playerFighters[i].RuntimeAttributes.Allies = playerFighters;
                    playerFighters[i].RuntimeAttributes.Enemies = enemyFighters;
                }
            }
            for (int i = 0; i < enemyFighters.Length; i++)
            {
                if (enemyFighters[i]?.RuntimeAttributes != null)
                {
                    enemyFighters[i].RuntimeAttributes.OwnerFighter = enemyFighters[i];
                    enemyFighters[i].RuntimeAttributes.Allies = enemyFighters;
                    enemyFighters[i].RuntimeAttributes.Enemies = playerFighters;
                }
            }

            return new BattleSpawnResult
            {
                PlayerFighters = playerFighters,
                EnemyFighters = enemyFighters
            };
        }

        private static bool HasCustomPlayerDefinitions(BattleSpawnConfig config)
        {
            return config.PlayerFighterDefinitions != null && config.PlayerFighterDefinitions.Length > 0;
        }

        private static BattleFighter[] CreateFighterGroupFromDefinitions(
            Transform parent,
            BattleCamp camp,
            BattleFighterSpawnDefinition[] definitions,
            AvatarAnimationDefinition defaultDefinition,
            List<Vector3> occupiedPositions,
            bool faceRight,
            Color tint,
            BattleSpawnConfig config)
        {
            BattleFighter[] fighters = new BattleFighter[definitions.Length];

            for (int i = 0; i < definitions.Length; i++)
            {
                Vector3 spawnPosition = GetRandomSpawnPosition(config, occupiedPositions);
                BattleFighterSpawnDefinition fighterDefinition = definitions[i];
                string fighterName = string.IsNullOrEmpty(fighterDefinition.Name)
                    ? $"PlayerAvatar_{i + 1}"
                    : fighterDefinition.Name;
                AvatarAnimationDefinition fighterAvatarDefinition = fighterDefinition.AvatarDefinition != null
                    ? fighterDefinition.AvatarDefinition
                    : defaultDefinition;

                fighters[i] = CreateFighter(
                    parent,
                    fighterName,
                    camp,
                    null,
                    fighterDefinition.StaticAttributes,
                    fighterAvatarDefinition,
                    spawnPosition,
                    faceRight,
                    tint,
                    config,
                    fighterDefinition.ScaleMultiplier > 0f ? fighterDefinition.ScaleMultiplier : 1.0f,
                    fighterDefinition.TribeType,
                    fighterDefinition.FighterId,
                    fighterDefinition.AuraBuffs);

                occupiedPositions.Add(spawnPosition);
            }

            return fighters;
        }

        private static BattleFighter[] CreateFighterGroup(
            Transform parent,
            string baseName,
            BattleCamp camp,
            BattleUnitTypeConfig unitType,
            AvatarAnimationDefinition definition,
            int count,
            List<Vector3> occupiedPositions,
            bool faceRight,
            Color tint,
            BattleSpawnConfig config,
            UnitStaticAttributes? overrideAttributes = null)
        {
            BattleFighter[] fighters = new BattleFighter[count];
            UnitStaticAttributes resolvedAttrs = overrideAttributes ?? ResolveStaticAttributes(unitType);

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPosition = GetRandomSpawnPosition(config, occupiedPositions);
                string name = count > 1 ? $"{baseName}_{i + 1}" : baseName;
                fighters[i] = CreateFighter(
                    parent,
                    name,
                    camp,
                    unitType,
                    resolvedAttrs,
                    definition,
                    spawnPosition,
                    faceRight,
                    tint,
                    config);

                occupiedPositions.Add(spawnPosition);
            }

            return fighters;
        }

        private static Vector3 GetRandomSpawnPosition(BattleSpawnConfig config, List<Vector3> occupiedPositions)
        {
            float minDistance = Mathf.Max(0.1f, config.SpawnMinDistance);
            int tryCount = Mathf.Max(1, config.SpawnTryCount);

            for (int i = 0; i < tryCount; i++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(config.SpawnAreaMin.x, config.SpawnAreaMax.x),
                    Random.Range(config.SpawnAreaMin.y, config.SpawnAreaMax.y),
                    0f);

                bool tooClose = false;
                for (int j = 0; j < occupiedPositions.Count; j++)
                {
                    if (Vector3.Distance(candidate, occupiedPositions[j]) < minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    return candidate;
                }
            }

            return new Vector3(
                Random.Range(config.SpawnAreaMin.x, config.SpawnAreaMax.x),
                Random.Range(config.SpawnAreaMin.y, config.SpawnAreaMax.y),
                0f);
        }

        /// <summary>
        /// 从定义数组生成敌方单位（支持混合敌人类型）
        /// </summary>
        public static BattleSpawnResult SpawnEnemiesFromDefinitions(
            Transform parent,
            BattleFighterSpawnDefinition[] definitions,
            AvatarAnimationDefinition defaultAvatar,
            BattleSpawnConfig config)
        {
            List<Vector3> occupiedPositions = new List<Vector3>(definitions.Length);

            BattleFighter[] enemyFighters = CreateFighterGroupFromDefinitions(
                parent,
                BattleCamp.Enemy,
                definitions,
                defaultAvatar,
                occupiedPositions,
                false,
                config.EnemyTint,
                config);

            LoadGroupIdle(enemyFighters);

            return new BattleSpawnResult
            {
                PlayerFighters = new BattleFighter[0],
                EnemyFighters = enemyFighters
            };
        }

        /// <summary>
        /// 仅生成敌方单位（准备阶段用）
        /// </summary>
        public static BattleSpawnResult SpawnEnemiesOnly(Transform parent, BattleSpawnConfig config)
        {
            int enemyFighterCount = Mathf.Max(1, config.EnemyFighterCount > 0 ? config.EnemyFighterCount : config.FightersPerCamp);
            List<Vector3> occupiedPositions = new List<Vector3>(enemyFighterCount);

            BattleFighter[] enemyFighters = CreateFighterGroup(
                parent,
                "EnemyAvatar",
                BattleCamp.Enemy,
                config.EnemyUnitType,
                config.EnemyAvatarDefinition,
                enemyFighterCount,
                occupiedPositions,
                false,
                config.EnemyTint,
                config,
                config.EnemyStaticAttributes);

            LoadGroupIdle(enemyFighters);

            return new BattleSpawnResult
            {
                PlayerFighters = new BattleFighter[0],
                EnemyFighters = enemyFighters
            };
        }

        /// <summary>
        /// 在指定位置创建单个战斗单位（准备阶段部署玩家单位用）
        /// </summary>
        public static BattleFighter CreateSingleFighter(
            Transform parent,
            string objectName,
            BattleCamp camp,
            BattleFighterSpawnDefinition definition,
            Vector3 position,
            float fighterScale,
            Color tint,
            BattleUnitTypeConfig unitType)
        {
            UnitStaticAttributes attrs = definition.StaticAttributes;
            AvatarAnimationDefinition avatarDef = definition.AvatarDefinition;

            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent);
            go.transform.position = position;

            float scale = Mathf.Max(0.1f, fighterScale * (definition.ScaleMultiplier > 0f ? definition.ScaleMultiplier : 1.0f));
            Vector3 baseScale = new Vector3(scale, scale, 1f);
            bool faceRight = position.x >= 0f;
            go.transform.localScale = faceRight ? baseScale : new Vector3(-baseScale.x, baseScale.y, baseScale.z);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.color = Color.white;

            SortByY sortBy = go.AddComponent<SortByY>();
            sortBy.BaseOrder = 10;
            sortBy.Multiplier = 100;

            AvatarSequencePlayer sequencePlayer = go.AddComponent<AvatarSequencePlayer>();
            BattleAvatar battleAvatar = go.AddComponent<BattleAvatar>();
            battleAvatar.SetAnimationDefinition(avatarDef);

            FighterHUD hud = go.AddComponent<FighterHUD>();
            int maxHp = Mathf.Max(1, attrs.MaxHp);
            hud.Initialize(maxHp, camp == BattleCamp.Enemy);

            UnitRuntimeAttributes runtimeAttributes = new UnitRuntimeAttributes(attrs);

            // 用持久化的当前HP覆盖满血初始值
            if (definition.CurrentHp > 0)
                runtimeAttributes.CurrentHp = Mathf.Min(definition.CurrentHp, runtimeAttributes.MaxHp);

            // 天生 buff
            List<int> innateBuffIds = new List<int>();
            if (definition.FighterId > 0)
            {
                var fighterConfig = Camp.TribeConfigLoader.Instance?.GetFighterConfig(definition.FighterId);
                if (fighterConfig?.innateBuffIds != null)
                    innateBuffIds.AddRange(fighterConfig.innateBuffIds);
            }

            foreach (int buffId in innateBuffIds)
            {
                var buffConfig = Camp.TribeConfigLoader.Instance?.GetBuffConfig(buffId);
                if (buffConfig == null) continue;
                var unifiedBuff = new Camp.UnifiedBuff
                {
                    buffId = buffConfig.buffId.ToString(),
                    displayName = buffConfig.buffName,
                    source = Camp.BuffSource.Innate,
                    sourceId = buffConfig.buffId.ToString(),
                    stackRule = Camp.BuffStackRule.None,
                    currentStacks = 1,
                    gameEffectType = buffConfig.gameEffectType,
                    effectParam1 = buffConfig.effectParam1,
                    remainingDuration = -1f,
                };
                runtimeAttributes.ApplyBuff(unifiedBuff);
            }

            // 光环 buff
            if (definition.AuraBuffs != null)
            {
                foreach (var buff in definition.AuraBuffs)
                {
                    runtimeAttributes.ApplyBuff(buff.Clone());
                }
            }

            // 强化 buff：enhanceLevel == 1 时，全属性 +50%
            if (definition.EnhanceLevel >= 1)
            {
                var statTypes = new[] { Camp.StatType.Attack, Camp.StatType.Defense, Camp.StatType.Hp, Camp.StatType.MoveSpeed, Camp.StatType.AttackSpeed };
                foreach (var stat in statTypes)
                {
                    var enhanceBuff = Camp.UnifiedBuff.CreateStatBuff(
                        $"enhance_{stat}", "强化",
                        Camp.BuffSource.Enhancement, "enhance",
                        stat, true, 0.5f);
                    runtimeAttributes.ApplyBuff(enhanceBuff);
                }
            }

            runtimeAttributes.Recalculate();

            // 受击火花
            GameObject hitEffect = new GameObject("HitEffect");
            hitEffect.transform.SetParent(go.transform, false);
            hitEffect.transform.localPosition = Vector3.zero;
            hitEffect.transform.localScale = Vector3.one;
            SpriteRenderer hitSr = hitEffect.AddComponent<SpriteRenderer>();
            hitSr.sortingOrder = 200;
            hitEffect.SetActive(false);

            return new BattleFighter
            {
                Name = objectName,
                Camp = camp,
                UnitType = unitType,
                StaticAttributes = attrs,
                RuntimeAttributes = runtimeAttributes,
                Avatar = battleAvatar,
                Transform = go.transform,
                BaseScale = scale,
                TribeType = definition.TribeType,
                FighterId = definition.FighterId,
                InnateBuffIds = innateBuffIds,
                HitEffect = hitEffect
            };
        }

        private static BattleFighter CreateFighter(
            Transform parent,
            string objectName,
            BattleCamp camp,
            BattleUnitTypeConfig unitType,
            UnitStaticAttributes staticAttributes,
            AvatarAnimationDefinition definition,
            Vector3 position,
            bool faceRight,
            Color tint,
            BattleSpawnConfig config,
            float scaleMultiplier = 1.0f,
            TribeType tribeType = TribeType.None,
            int fighterId = 0,
            List<UnifiedBuff> auraBuffs = null)
        {
            GameObject go;
            if (config.FighterPrefab != null)
            {
                go = Object.Instantiate(config.FighterPrefab, parent);
                go.name = objectName;
            }
            else
            {
                go = new GameObject(objectName);
                go.transform.SetParent(parent);
            }

            go.transform.position = position;
            float scale = Mathf.Max(0.1f, config.FighterScale * scaleMultiplier);
            Vector3 baseScale = new Vector3(scale, scale, 1f);
            bool initialFaceRight = position.x < 0f ? false : (position.x > 0f ? true : faceRight);
            go.transform.localScale = initialFaceRight ? baseScale : new Vector3(-baseScale.x, baseScale.y, baseScale.z);

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }

            renderer.color = Color.white;
            SortByY sortBy = go.GetComponent<SortByY>();
            if (sortBy == null)
            {
                sortBy = go.AddComponent<SortByY>();
            }
            sortBy.BaseOrder = 10;
            sortBy.Multiplier = 100;

            AvatarSequencePlayer sequencePlayer = go.GetComponent<AvatarSequencePlayer>();
            if (sequencePlayer == null)
            {
                sequencePlayer = go.AddComponent<AvatarSequencePlayer>();
            }

            BattleAvatar battleAvatar = go.GetComponent<BattleAvatar>();
            if (battleAvatar == null)
            {
                battleAvatar = go.AddComponent<BattleAvatar>();
            }

            battleAvatar.SetAnimationDefinition(definition);

            FighterHUD hud = go.GetComponent<FighterHUD>();
            if (hud == null)
            {
                hud = go.AddComponent<FighterHUD>();
            }
            int maxHp = Mathf.Max(1, staticAttributes.MaxHp);
            hud.Initialize(maxHp, camp == BattleCamp.Enemy);

            UnitRuntimeAttributes runtimeAttributes = new UnitRuntimeAttributes(staticAttributes);

            // 从 fighter_config.json 读取天生 buff IDs
            List<int> innateBuffIds = new List<int>();
            if (fighterId > 0)
            {
                var fighterConfig = Camp.TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
                if (fighterConfig?.innateBuffIds != null)
                {
                    innateBuffIds.AddRange(fighterConfig.innateBuffIds);
                }
            }

            // 将天生 buff ID 转为 UnifiedBuff 加到 RuntimeAttributes
            bool hasInnateBuffs = innateBuffIds.Count > 0;
            foreach (int buffId in innateBuffIds)
            {
                var buffConfig = Camp.TribeConfigLoader.Instance?.GetBuffConfig(buffId);
                if (buffConfig == null) continue;

                var unifiedBuff = new Camp.UnifiedBuff
                {
                    buffId = buffConfig.buffId.ToString(),
                    displayName = buffConfig.buffName,
                    source = Camp.BuffSource.Innate,
                    sourceId = buffConfig.buffId.ToString(),
                    stackRule = Camp.BuffStackRule.None,
                    currentStacks = 1,
                    gameEffectType = buffConfig.gameEffectType,
                    effectParam1 = buffConfig.effectParam1,
                    remainingDuration = -1f,
                    tickInterval = 0f,
                    tickTimer = 0f,
                };
                runtimeAttributes.ApplyBuff(unifiedBuff);
            }

            // 应用光环 buff（从 FighterData 传入）
            // 所有类型的 buff（Persistent / TemporaryRoundBased / BattleOnly）统一在此应用
            Debug.Log($"[CreateFighter] {objectName} tribe={tribeType}, auraBuffs count={auraBuffs?.Count ?? 0}");
            if (auraBuffs != null)
            {
                foreach (var buff in auraBuffs)
                {
                    Debug.Log($"[CreateFighter] {objectName} applying aura buff: id={buff.buffId}, stat={buff.statType}, isPercent={buff.isPercent}, value={buff.value}, persistence={buff.persistence}, stacks={buff.currentStacks}");
                    var clone = buff.Clone();
                    runtimeAttributes.ApplyBuff(clone);
                }
            }

            // 重新计算属性（确保 innate buff 和 aura buff 的修正生效）
            runtimeAttributes.Recalculate();

            // 创建受击火花（初始隐藏）
            GameObject hitEffect = new GameObject("HitEffect");
            hitEffect.transform.SetParent(go.transform, false);
            hitEffect.transform.localPosition = Vector3.zero;
            hitEffect.transform.localScale = Vector3.one;
            SpriteRenderer hitSr = hitEffect.AddComponent<SpriteRenderer>();
            hitSr.sortingOrder = 200;
            hitEffect.SetActive(false);

            // === 诊断日志：CreateFighter 完成后 ===
            Debug.Log($"[CreateFighter] {objectName} final stats: ATK={runtimeAttributes.Attack}, DEF={runtimeAttributes.Defense}, HP={runtimeAttributes.MaxHp}, SPD={runtimeAttributes.MoveSpeed}, totalBuffs={runtimeAttributes.ActiveBuffs.Count}");
            for (int bi = 0; bi < runtimeAttributes.ActiveBuffs.Count; bi++)
            {
                var bb = runtimeAttributes.ActiveBuffs[bi];
                Debug.Log($"  [Buff] {bb.buffId} src={bb.source} stat={bb.statType} isPercent={bb.isPercent} val={bb.value} persistence={bb.persistence} stacks={bb.currentStacks}");
            }

            return new BattleFighter
            {
                Name = objectName,
                Camp = camp,
                UnitType = unitType,
                StaticAttributes = staticAttributes,
                RuntimeAttributes = runtimeAttributes,
                Avatar = battleAvatar,
                Transform = go.transform,
                BaseScale = scale,
                TribeType = tribeType,
                FighterId = fighterId,
                InnateBuffIds = innateBuffIds,
                HitEffect = hitEffect
            };
        }

        private static UnitStaticAttributes ResolveStaticAttributes(BattleUnitTypeConfig unitType)
        {
            if (unitType != null)
            {
                return unitType.BaseAttributes;
            }

            return UnitStaticAttributes.Default;
        }

        private static void LoadGroupIdle(BattleFighter[] fighters)
        {
            if (fighters == null)
            {
                return;
            }

            for (int i = 0; i < fighters.Length; i++)
            {
                fighters[i]?.Avatar?.LoadAndPlayIdle();
            }
        }
    }
}
