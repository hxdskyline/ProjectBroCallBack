using System.Collections.Generic;

namespace Combat.SkillSystem
{
    public interface ISkillCondition
    {
        bool Evaluate(SkillExecutionContext context);
    }

    public interface ISkillEffect
    {
        void Execute(SkillExecutionContext context, BattleSkillRuntime runtime);
    }

    public class SkillTriggerDefinition
    {
        public SkillEventType eventType;
        public float cooldown;
        public int maxTriggerCount;
        public ISkillCondition condition;
        public List<ISkillEffect> effects = new List<ISkillEffect>();
    }

    public class SkillDefinition
    {
        public string skillId;
        public List<SkillTriggerDefinition> triggers = new List<SkillTriggerDefinition>();
    }

    public class SkillBuffDefinition
    {
        public string buffId;
        public float duration;
        public int maxStacks = 1;
        public float tickInterval;
        public List<ISkillEffect> onAttach = new List<ISkillEffect>();
        public List<ISkillEffect> onDetach = new List<ISkillEffect>();
        public List<ISkillEffect> onTick = new List<ISkillEffect>();
        public List<SkillTriggerDefinition> triggers = new List<SkillTriggerDefinition>();
    }
}
