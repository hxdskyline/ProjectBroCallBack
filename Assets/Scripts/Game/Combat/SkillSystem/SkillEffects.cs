using System;
using System.Collections.Generic;
using Camp;
using Combat.Effects;
using Combat.Fighter;

namespace Combat.SkillSystem
{
    public sealed class AlwaysTrueCondition : ISkillCondition
    {
        public bool Evaluate(SkillExecutionContext context)
        {
            return true;
        }
    }

    public sealed class ChanceCondition : ISkillCondition
    {
        private readonly float _chance;
        private readonly Random _random;

        public ChanceCondition(float chance, Random random = null)
        {
            _chance = chance;
            _random = random ?? new Random();
        }

        public bool Evaluate(SkillExecutionContext context)
        {
            return _random.NextDouble() < _chance;
        }
    }

    public sealed class SequenceEffect : ISkillEffect
    {
        private readonly List<ISkillEffect> _effects;

        public SequenceEffect(params ISkillEffect[] effects)
        {
            _effects = new List<ISkillEffect>(effects ?? Array.Empty<ISkillEffect>());
        }

        public void Execute(SkillExecutionContext context, BattleSkillRuntime runtime)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                _effects[i]?.Execute(context, runtime);
            }
        }
    }

    public sealed class DelayEffect : ISkillEffect
    {
        private readonly float _delaySeconds;
        private readonly ISkillEffect _innerEffect;

        public DelayEffect(float delaySeconds, ISkillEffect innerEffect)
        {
            _delaySeconds = delaySeconds;
            _innerEffect = innerEffect;
        }

        public void Execute(SkillExecutionContext context, BattleSkillRuntime runtime)
        {
            if (_innerEffect == null)
            {
                return;
            }

            runtime.Schedule(_delaySeconds, context.WithNote(context.note?.CreateChild()), _innerEffect);
        }
    }

    public sealed class SetBlackboardValueEffect : ISkillEffect
    {
        private readonly string _key;
        private readonly object _value;

        public SetBlackboardValueEffect(string key, object value)
        {
            _key = key;
            _value = value;
        }

        public void Execute(SkillExecutionContext context, BattleSkillRuntime runtime)
        {
            context.blackboard?.Set(_key, _value);
        }
    }

    public sealed class ApplyRuntimeBuffEffect : ISkillEffect
    {
        private readonly Func<UnifiedBuff> _factory;

        public ApplyRuntimeBuffEffect(Func<UnifiedBuff> factory)
        {
            _factory = factory;
        }

        public void Execute(SkillExecutionContext context, BattleSkillRuntime runtime)
        {
            BattleFighter target = context.binder;
            if (target?.RuntimeAttributes == null || _factory == null)
            {
                return;
            }

            UnifiedBuff buff = _factory.Invoke();
            if (buff == null)
            {
                return;
            }

            target.RuntimeAttributes.ApplyBuff(buff);
        }
    }

    public sealed class ApplyRuntimeBuffToEventTargetEffect : ISkillEffect
    {
        private readonly Func<UnifiedBuff> _factory;

        public ApplyRuntimeBuffToEventTargetEffect(Func<UnifiedBuff> factory)
        {
            _factory = factory;
        }

        public void Execute(SkillExecutionContext context, BattleSkillRuntime runtime)
        {
            BattleFighter target = context.skillEvent.target;
            if (target?.RuntimeAttributes == null || _factory == null)
            {
                return;
            }

            UnifiedBuff buff = _factory.Invoke();
            if (buff == null)
            {
                return;
            }

            target.RuntimeAttributes.ApplyBuff(buff);
        }
    }

    public sealed class ApplySkillBuffEffect : ISkillEffect
    {
        private readonly SkillBuffDefinition _definition;

        public ApplySkillBuffEffect(SkillBuffDefinition definition)
        {
            _definition = definition;
        }

        public void Execute(SkillExecutionContext context, BattleSkillRuntime runtime)
        {
            BattleFighter target = context.binder;
            if (target == null || _definition == null)
            {
                return;
            }

            runtime.ApplyBuff(target, context, _definition);
        }
    }

    public static class SkillBuiltinFactories
    {
        public static ApplyRuntimeBuffEffect Freeze(float duration)
        {
            return new ApplyRuntimeBuffEffect(() => StatusEffectFactory.CreateFreeze(duration));
        }
    }
}
