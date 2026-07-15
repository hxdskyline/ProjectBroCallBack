using System.Collections.Generic;

namespace Combat.SkillSystem
{
    public class SkillScheduler
    {
        private struct ScheduledEffect
        {
            public float fireTime;
            public SkillExecutionContext context;
            public ISkillEffect effect;
        }

        private readonly List<ScheduledEffect> _scheduledEffects = new List<ScheduledEffect>();

        public void Schedule(float fireTime, SkillExecutionContext context, ISkillEffect effect)
        {
            if (effect == null)
            {
                return;
            }

            _scheduledEffects.Add(new ScheduledEffect
            {
                fireTime = fireTime,
                context = context,
                effect = effect,
            });
        }

        public void Tick(float now, BattleSkillRuntime runtime)
        {
            for (int i = _scheduledEffects.Count - 1; i >= 0; i--)
            {
                ScheduledEffect item = _scheduledEffects[i];
                if (item.fireTime > now)
                {
                    continue;
                }

                _scheduledEffects.RemoveAt(i);
                item.effect.Execute(item.context, runtime);
            }
        }
    }
}
