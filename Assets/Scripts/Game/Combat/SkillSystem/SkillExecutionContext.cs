using Combat.Fighter;

namespace Combat.SkillSystem
{
    public struct SkillExecutionContext
    {
        public BattleFighter caster;
        public BattleFighter sender;
        public BattleFighter anySender;
        public BattleFighter binder;
        public string skillId;
        public BattleSimulation simulation;
        public SkillBlackboard blackboard;
        public SkillNote note;
        public SkillEventData skillEvent;

        public SkillExecutionContext ChangeBinder(BattleFighter newBinder)
        {
            SkillExecutionContext next = this;
            next.anySender = binder;
            next.sender = newBinder != null && newBinder == binder ? sender : binder;
            next.binder = newBinder;
            return next;
        }

        public SkillExecutionContext WithEvent(SkillEventData evt)
        {
            SkillExecutionContext next = this;
            next.skillEvent = evt;
            return next;
        }

        public SkillExecutionContext WithNote(SkillNote newNote)
        {
            SkillExecutionContext next = this;
            next.note = newNote;
            return next;
        }
    }
}
