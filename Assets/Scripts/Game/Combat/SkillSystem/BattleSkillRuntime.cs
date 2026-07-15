using System.Collections.Generic;
using Combat.Fighter;

namespace Combat.SkillSystem
{
    public class BattleSkillRuntime
    {
        private readonly Dictionary<BattleFighter, BattleSkillActorState> _actorStates =
            new Dictionary<BattleFighter, BattleSkillActorState>();

        private readonly SkillScheduler _scheduler = new SkillScheduler();
        private readonly BattleSimulation _simulation;
        private float _time;

        public BattleSkillRuntime(BattleSimulation simulation)
        {
            _simulation = simulation;
        }

        public float CurrentTime => _time;

        public void RegisterFighters(BattleFighter[] fighters)
        {
            if (fighters == null)
            {
                return;
            }

            for (int i = 0; i < fighters.Length; i++)
            {
                RegisterFighter(fighters[i]);
            }
        }

        public void RegisterFighter(BattleFighter fighter)
        {
            if (fighter == null || _actorStates.ContainsKey(fighter))
            {
                return;
            }

            BattleSkillActorState state = new BattleSkillActorState(fighter);
            _actorStates.Add(fighter, state);

            if (!string.IsNullOrEmpty(fighter.SkillId))
            {
                SkillDefinition skill = SkillLibrary.GetSkill(fighter.SkillId);
                if (skill != null)
                {
                    state.Skills.Add(skill);
                }
            }
        }

        public void Tick(float deltaTime)
        {
            _time += deltaTime;

            foreach (KeyValuePair<BattleFighter, BattleSkillActorState> pair in _actorStates)
            {
                if (pair.Key == null || !pair.Key.IsAlive)
                {
                    continue;
                }

                pair.Value.Tick(deltaTime, this);
            }

            RaiseGlobalEvent(new SkillEventData(SkillEventType.Tick, null, null, deltaTime));
            _scheduler.Tick(_time, this);
        }

        public void RaiseGlobalEvent(SkillEventData skillEvent)
        {
            foreach (KeyValuePair<BattleFighter, BattleSkillActorState> pair in _actorStates)
            {
                if (pair.Key == null || pair.Key.IsRemoved)
                {
                    continue;
                }

                pair.Value.HandleEvent(skillEvent, this);
            }
        }

        public void RaiseEvent(BattleFighter actor, SkillEventData skillEvent)
        {
            if (actor == null)
            {
                RaiseGlobalEvent(skillEvent);
                return;
            }

            if (_actorStates.TryGetValue(actor, out BattleSkillActorState state))
            {
                state.HandleEvent(skillEvent, this);
            }
        }

        public void ApplyBuff(BattleFighter target, SkillExecutionContext sourceContext, SkillBuffDefinition definition)
        {
            if (target == null || definition == null)
            {
                return;
            }

            RegisterFighter(target);
            BattleSkillActorState state = _actorStates[target];

            for (int i = 0; i < state.Buffs.Count; i++)
            {
                SkillBuffInstance existing = state.Buffs[i];
                if (existing.Definition.buffId != definition.buffId)
                {
                    continue;
                }

                if (definition.maxStacks > 1 && existing.StackCount < definition.maxStacks)
                {
                    existing.AddStack();
                }
                else
                {
                    existing.Refresh();
                }
                return;
            }

            SkillBuffInstance instance = new SkillBuffInstance(target, sourceContext, definition);
            state.Buffs.Add(instance);
            instance.OnAttach(this);
        }

        public void Schedule(float delaySeconds, SkillExecutionContext context, ISkillEffect effect)
        {
            _scheduler.Schedule(_time + delaySeconds, context, effect);
        }

        public SkillExecutionContext CreateContext(
            BattleFighter caster,
            BattleFighter sender,
            BattleFighter anySender,
            BattleFighter binder,
            string skillId)
        {
            SkillBlackboard blackboard = null;
            if (caster != null && _actorStates.TryGetValue(caster, out BattleSkillActorState state))
            {
                blackboard = state.Blackboard;
            }

            return new SkillExecutionContext
            {
                caster = caster,
                sender = sender,
                anySender = anySender,
                binder = binder,
                skillId = skillId,
                simulation = _simulation,
                blackboard = blackboard,
                note = new SkillNote(),
            };
        }
    }
}
