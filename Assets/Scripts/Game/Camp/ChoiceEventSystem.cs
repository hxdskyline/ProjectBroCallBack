using System;
using System.Collections.Generic;
using UnityEngine;

namespace Camp
{
    /// <summary>
    /// 抉择选项类型
    /// </summary>
    public enum ChoiceOptionType
    {
        LowRisk = 0,    // 低风险（绿）
        HighRisk = 1,   // 高风险（红）
        AllIn = 2       // "我全要了"（金）
    }

    /// <summary>
    /// 抉择事件选项
    /// </summary>
    public class ChoiceOption
    {
        public ChoiceOptionType type;
        public string name;
        public string description;
        public List<BuffEffectItem> effects;
        public int catFoodReward;
        public bool causesWeatherPenalty;   // "我全要了"触发天气惩罚
        public bool causesShopPenalty;      // 奸商陷阱：商店加价+禁刷新
        public int equipmentReward;         // 饰品数量奖励
        public int consumableReward;        // 消耗品奖励
    }

    /// <summary>
    /// 抉择事件
    /// </summary>
    public class ChoiceEvent
    {
        public string eventId;
        public string name;
        public string description;
        public int levelGroup;              // 所属关卡组（5/10/15）
        public List<ChoiceOption> options;
    }

    /// <summary>
    /// 抉择系统 — 三选一随机事件
    /// 设计参考：正式文档/106_系统_抉择.md
    /// </summary>
    public class ChoiceEventSystem
    {
        private static readonly System.Random _rng = new System.Random();
        private List<ChoiceEvent> _allEvents;

        public void Initialize()
        {
            _allEvents = CreateAllEvents();
            GameLogger.Log("Choice", $"初始化完成，共 {_allEvents.Count} 个事件");
        }

        /// <summary>
        /// 根据当前回合获取候选事件，随机选1个
        /// </summary>
        public ChoiceEvent GetEventForLevel(int battleNumber)
        {
            int levelGroup = GetLevelGroup(battleNumber);
            var pool = _allEvents.FindAll(e => e.levelGroup == levelGroup);
            if (pool.Count == 0) return null;
            return pool[_rng.Next(pool.Count)];
        }

        /// <summary>
        /// 应用选项效果
        /// </summary>
        public void ApplyOption(ChoiceEvent evt, int optionIndex)
        {
            if (evt == null || optionIndex < 0 || optionIndex >= evt.options.Count) return;
            var option = evt.options[optionIndex];

            var dm = GameManager.Instance?.DataManager;
            if (dm == null) return;

            // 发放猫币
            if (option.catFoodReward > 0)
            {
                dm.AddCatFood(option.catFoodReward);
            }

            // 天气惩罚（我全要了）
            if (option.causesWeatherPenalty)
            {
                dm.SetExtraWeatherCount(dm.GetExtraWeatherCount() + 2);
                GameLogger.Log("Choice", "触发天气惩罚 +2");
            }

            // 商店惩罚（奸商陷阱）
            if (option.causesShopPenalty)
            {
                dm.SetShopPriceModifier(1.2f);
                dm.SetShopRefreshLocked(true);
                GameLogger.Log("Choice", "奸商陷阱：商店价格+20%，禁止刷新");
            }

            // 应用buff效果
            if (option.effects != null && option.effects.Count > 0)
            {
                var choice = new GameChoice
                {
                    choiceId = evt.eventId + "_" + option.type,
                    displayName = evt.name + " - " + option.name,
                    description = option.description,
                    category = (int)ChoiceCategory.Buff,
                    buffApplyType = (int)BuffApplyType.Aura,
                    buffScopeFilter = "all",
                    buffScopeText = "all",
                    buffEffects = option.effects,
                    targetTribeType = (int)TribeType.None
                };

                dm.PlayerData.runChoices.Add(choice);
                dm.RebuildAllBuffs();
            }

            // 饰品奖励
            if (option.equipmentReward > 0)
            {
                for (int i = 0; i < option.equipmentReward; i++)
                {
                    var equip = new EquipmentRecord
                    {
                        equipmentId = $"equip_{evt.eventId}_{i}",
                        displayName = $"{evt.name}饰品",
                        description = "抉择奖励",
                        buffApplyType = (int)BuffApplyType.Aura,
                        buffScopeText = "all",
                        effects = option.effects ?? new List<BuffEffectItem>(),
                    };
                    dm.PlayerData.runEquipments.Add(equip);
                }
                dm.RebuildAllBuffs();
            }

            dm.SavePlayerData();
            GameLogger.Log("Choice", $"确认抉择: {evt.name} - {option.name}");
        }

        // ── 内部方法 ──

        private int GetLevelGroup(int battleNumber)
        {
            // 每地区15关，在第5/10/15关触发
            int localBattle = ((battleNumber - 1) % 15) + 1;
            if (localBattle <= 5) return 5;
            if (localBattle <= 10) return 10;
            return 15;
        }

        private List<ChoiceEvent> CreateAllEvents()
        {
            return new List<ChoiceEvent>
            {
                // ── 关卡组5：流浪猫的馈赠、古老的训练场、秘密训练营 ──
                new ChoiceEvent
                {
                    eventId = "stray_cat_gift", name = "流浪猫的馈赠",
                    description = "一只流浪猫向你献上了礼物", levelGroup = 5,
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption { type = ChoiceOptionType.LowRisk, name = "收下礼物", description = "获得200小鱼干", catFoodReward = 200 },
                        new ChoiceOption { type = ChoiceOptionType.HighRisk, name = "贪婪索取", description = "获得500小鱼干，但下场战斗攻击力降低10%", catFoodReward = 500,
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "Attack", isPercent = true, value = -0.1f, gameEffectType = 0 } } },
                        new ChoiceOption { type = ChoiceOptionType.AllIn, name = "我全要了", description = "获得700小鱼干，但下场战斗出现2种天气效果", catFoodReward = 700, causesWeatherPenalty = true },
                    }
                },
                new ChoiceEvent
                {
                    eventId = "ancient_training", name = "古老的训练场",
                    description = "你发现了一处古老的训练场", levelGroup = 5,
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption { type = ChoiceOptionType.LowRisk, name = "轻松训练", description = "3回合内攻击力+20%",
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "Attack", isPercent = true, value = 0.2f, gameEffectType = 0 } } },
                        new ChoiceOption { type = ChoiceOptionType.HighRisk, name = "极限训练", description = "3回合内攻击力-10%，之后永久攻击力+5%",
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "Attack", isPercent = true, value = 0.05f, gameEffectType = 0 } } },
                        new ChoiceOption { type = ChoiceOptionType.AllIn, name = "我全要了", description = "3回合内攻击力+30%，但下场战斗出现2种天气效果",
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "Attack", isPercent = true, value = 0.3f, gameEffectType = 0 } },
                            causesWeatherPenalty = true },
                    }
                },
                new ChoiceEvent
                {
                    eventId = "secret_camp", name = "秘密训练营",
                    description = "一个秘密的训练营出现在前方", levelGroup = 5,
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption { type = ChoiceOptionType.LowRisk, name = "派遣新兵", description = "3回合内防御力+20%",
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "Defense", isPercent = true, value = 0.2f, gameEffectType = 1 } } },
                        new ChoiceOption { type = ChoiceOptionType.HighRisk, name = "全员训练", description = "2只猫咪前往训练，2回合后获得4只强化猫咪" },
                        new ChoiceOption { type = ChoiceOptionType.AllIn, name = "我全要了", description = "3回合内防御力+30%，但下场战斗出现2种天气效果",
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "Defense", isPercent = true, value = 0.3f, gameEffectType = 1 } },
                            causesWeatherPenalty = true },
                    }
                },
                // ── 关卡组10：神秘商人、禁忌的力量、奸商的陷阱 ──
                new ChoiceEvent
                {
                    eventId = "mysterious_merchant", name = "神秘商人",
                    description = "一位神秘的商人向你走来", levelGroup = 10,
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption { type = ChoiceOptionType.LowRisk, name = "普通交易", description = "获得1个普通饰品", equipmentReward = 1 },
                        new ChoiceOption { type = ChoiceOptionType.HighRisk, name = "以物易物", description = "获得1个稀有饰品，但下场战斗移动速度降低15%",
                            equipmentReward = 1,
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "MoveSpeed", isPercent = true, value = -0.15f, gameEffectType = 3 } } },
                        new ChoiceOption { type = ChoiceOptionType.AllIn, name = "我全要了", description = "获得2个饰品，但下场战斗出现2种天气效果", equipmentReward = 2, causesWeatherPenalty = true },
                    }
                },
                new ChoiceEvent
                {
                    eventId = "forbidden_power", name = "禁忌的力量",
                    description = "一股禁忌的力量在你面前涌动", levelGroup = 10,
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption { type = ChoiceOptionType.LowRisk, name = "谨慎汲取", description = "2回合内全属性+10%",
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "AllPercent", isPercent = true, value = 0.1f, gameEffectType = 4 } } },
                        new ChoiceOption { type = ChoiceOptionType.HighRisk, name = "过度汲取", description = "2回合内全属性-15%，之后永久全属性+8%",
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "AllPercent", isPercent = true, value = 0.08f, gameEffectType = 4 } } },
                        new ChoiceOption { type = ChoiceOptionType.AllIn, name = "我全要了", description = "2回合内全属性+20%，但下场战斗出现2种天气效果",
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "AllPercent", isPercent = true, value = 0.2f, gameEffectType = 4 } },
                            causesWeatherPenalty = true },
                    }
                },
                new ChoiceEvent
                {
                    eventId = "merchant_trap", name = "奸商的陷阱",
                    description = "一个看起来不太可靠的商人向你招手", levelGroup = 10,
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption { type = ChoiceOptionType.LowRisk, name = "小赚一笔", description = "获得300小鱼干", catFoodReward = 300 },
                        new ChoiceOption { type = ChoiceOptionType.HighRisk, name = "大捞一笔", description = "获得800小鱼干，但下回合商店价格+20%且无法刷新", catFoodReward = 800, causesShopPenalty = true },
                        new ChoiceOption { type = ChoiceOptionType.AllIn, name = "我全要了", description = "获得1100小鱼干，但下场战斗出现2种天气效果", catFoodReward = 1100, causesWeatherPenalty = true },
                    }
                },
                // ── 关卡组15：流浪猫的馈赠、禁忌的力量、秘密训练营（复用带升级描述）──
                new ChoiceEvent
                {
                    eventId = "stray_cat_gift_15", name = "流浪猫的馈赠",
                    description = "又一只流浪猫出现了，这次带来了更好的东西", levelGroup = 15,
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption { type = ChoiceOptionType.LowRisk, name = "收下馈赠", description = "获得400小鱼干", catFoodReward = 400 },
                        new ChoiceOption { type = ChoiceOptionType.HighRisk, name = "讨价还价", description = "获得900小鱼干，但下场战斗防御力降低10%", catFoodReward = 900,
                            effects = new List<BuffEffectItem> { new BuffEffectItem { statType = "Defense", isPercent = true, value = -0.1f, gameEffectType = 1 } } },
                        new ChoiceOption { type = ChoiceOptionType.AllIn, name = "我全要了", description = "获得1300小鱼干，但下场战斗出现2种天气效果", catFoodReward = 1300, causesWeatherPenalty = true },
                    }
                },
            };
        }
    }
}
