using System.Collections.Generic;
using Combat.Fighter;
using UnityEngine;

namespace Combat.SkillSystem
{
    public class BattleSkillActorState
    {
        private readonly Dictionary<SkillTriggerDefinition, float> _triggerCooldowns =
            new Dictionary<SkillTriggerDefinition, float>();
        private readonly Dictionary<SkillTriggerDefinition, int> _triggerCounts =
            new Dictionary<SkillTriggerDefinition, int>();

        public BattleFighter Fighter { get; }
        public SkillBlackboard Blackboard { get; } = new SkillBlackboard();
        public List<SkillBuffInstance> Buffs { get; } = new List<SkillBuffInstance>();
        public List<SkillDefinition> Skills { get; } = new List<SkillDefinition>();

        public BattleSkillActorState(BattleFighter fighter)
        {
            Fighter = fighter;
        }

        public void Tick(float deltaTime, BattleSkillRuntime runtime)
        {
            if (_triggerCooldowns.Count > 0)
            {
                List<SkillTriggerDefinition> keys = new List<SkillTriggerDefinition>(_triggerCooldowns.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    SkillTriggerDefinition trigger = keys[i];
                    _triggerCooldowns[trigger] = Mathf.Max(0f, _triggerCooldowns[trigger] - deltaTime);
                }
            }

            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                SkillBuffInstance buff = Buffs[i];
                buff.Tick(deltaTime, runtime);
                if (!buff.IsExpired)
                {
                    continue;
                }

                buff.OnDetach(runtime);
                Buffs.RemoveAt(i);
            }
        }

        public void HandleEvent(SkillEventData skillEvent, BattleSkillRuntime runtime)
        {
            for (int i = 0; i < Skills.Count; i++)
            {
                SkillDefinition skill = Skills[i];
                if (skill == null)
                {
                    continue;
                }

                for (int triggerIndex = 0; triggerIndex < skill.triggers.Count; triggerIndex++)
                {
                    SkillTriggerDefinition trigger = skill.triggers[triggerIndex];
                    if (trigger == null || trigger.eventType != skillEvent.eventType)
                    {
                        continue;
                    }

                    if (_triggerCooldowns.TryGetValue(trigger, out float cooldown) && cooldown > 0f)
                    {
                        continue;
                    }

                    if (_triggerCounts.TryGetValue(trigger, out int count) &&
                        trigger.maxTriggerCount > 0 &&
                        count >= trigger.maxTriggerCount)
                    {
                        continue;
                    }

                    SkillExecutionContext context = runtime.CreateContext(Fighter, Fighter, Fighter, Fighter, skill.skillId)
                        .WithEvent(skillEvent);
                    if (trigger.condition != null && !trigger.condition.Evaluate(context))
                    {
                        continue;
                    }

                    for (int effectIndex = 0; effectIndex < trigger.effects.Count; effectIndex++)
                    {
                        trigger.effects[effectIndex]?.Execute(context, runtime);
                    }

                    _triggerCooldowns[trigger] = trigger.cooldown;
                    _triggerCounts[trigger] = count + 1;
                }
            }

            for (int i = 0; i < Buffs.Count; i++)
            {
                Buffs[i].HandleEvent(skillEvent, runtime);
            }
        }
    }
}
