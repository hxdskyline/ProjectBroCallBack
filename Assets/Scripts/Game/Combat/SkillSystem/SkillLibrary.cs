using System.Collections.Generic;
using Combat.Effects;

namespace Combat.SkillSystem
{
    public static class SkillLibrary
    {
        private static readonly Dictionary<string, SkillDefinition> Skills =
            new Dictionary<string, SkillDefinition>();

        static SkillLibrary()
        {
            RegisterBuiltins();
        }

        public static void Register(SkillDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.skillId))
            {
                return;
            }

            Skills[definition.skillId] = definition;
        }

        public static SkillDefinition GetSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return null;
            }

            Skills.TryGetValue(skillId, out SkillDefinition definition);
            return definition;
        }

        private static void RegisterBuiltins()
        {
            Register(new SkillDefinition
            {
                skillId = "sample_freeze_on_hit",
                triggers = new List<SkillTriggerDefinition>
                {
                    new SkillTriggerDefinition
                    {
                        eventType = SkillEventType.AttackHit,
                        condition = new ChanceCondition(0.2f),
                        effects = new List<ISkillEffect>
                        {
                            new ApplyRuntimeBuffToEventTargetEffect(() => StatusEffectFactory.CreateFreeze(1.5f))
                        }
                    }
                }
            });
        }
    }
}
