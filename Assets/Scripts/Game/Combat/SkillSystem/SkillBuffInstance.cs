using System.Collections.Generic;
using Combat.Fighter;
using UnityEngine;

namespace Combat.SkillSystem
{
    public class SkillBuffInstance
    {
        private readonly Dictionary<SkillTriggerDefinition, float> _triggerCooldowns =
            new Dictionary<SkillTriggerDefinition, float>();
        private readonly Dictionary<SkillTriggerDefinition, int> _triggerCounts =
            new Dictionary<SkillTriggerDefinition, int>();

        public SkillBuffDefinition Definition { get; }
        public BattleFighter Owner { get; }
        public SkillExecutionContext SourceContext { get; }
        public float RemainingDuration { get; private set; }
        public float TickTimer { get; private set; }
        public int StackCount { get; private set; }
        public bool IsExpired => RemainingDuration >= 0f && RemainingDuration <= 0f;

        public SkillBuffInstance(BattleFighter owner, SkillExecutionContext sourceContext, SkillBuffDefinition definition)
        {
            Owner = owner;
            SourceContext = sourceContext;
            Definition = definition;
            RemainingDuration = definition.duration;
            StackCount = 1;
        }

        public void Refresh()
        {
            RemainingDuration = Definition.duration;
        }

        public void AddStack()
        {
            StackCount++;
            Refresh();
        }

        public void Tick(float deltaTime, BattleSkillRuntime runtime)
        {
            if (Definition.duration > 0f)
            {
                RemainingDuration -= deltaTime;
            }

            if (Definition.tickInterval > 0f && Definition.onTick.Count > 0)
            {
                TickTimer += deltaTime;
                while (TickTimer >= Definition.tickInterval)
                {
                    TickTimer -= Definition.tickInterval;
                    ExecuteEffects(Definition.onTick, runtime);
                }
            }

            if (_triggerCooldowns.Count > 0)
            {
                List<SkillTriggerDefinition> keys = new List<SkillTriggerDefinition>(_triggerCooldowns.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    SkillTriggerDefinition trigger = keys[i];
                    _triggerCooldowns[trigger] = Mathf.Max(0f, _triggerCooldowns[trigger] - deltaTime);
                }
            }
        }

        public void OnAttach(BattleSkillRuntime runtime)
        {
            ExecuteEffects(Definition.onAttach, runtime);
        }

        public void OnDetach(BattleSkillRuntime runtime)
        {
            ExecuteEffects(Definition.onDetach, runtime);
        }

        public void HandleEvent(SkillEventData skillEvent, BattleSkillRuntime runtime)
        {
            for (int i = 0; i < Definition.triggers.Count; i++)
            {
                SkillTriggerDefinition trigger = Definition.triggers[i];
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

                SkillExecutionContext context = SourceContext.ChangeBinder(Owner).WithEvent(skillEvent);
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

        private void ExecuteEffects(List<ISkillEffect> effects, BattleSkillRuntime runtime)
        {
            if (effects == null || effects.Count == 0)
            {
                return;
            }

            SkillExecutionContext context = SourceContext.ChangeBinder(Owner);
            for (int i = 0; i < effects.Count; i++)
            {
                effects[i]?.Execute(context, runtime);
            }
        }
    }
}
