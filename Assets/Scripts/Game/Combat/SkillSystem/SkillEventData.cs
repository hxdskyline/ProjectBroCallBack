using Combat.Fighter;

namespace Combat.SkillSystem
{
    public struct SkillEventData
    {
        public SkillEventType eventType;
        public BattleFighter source;
        public BattleFighter target;
        public float deltaTime;
        public int intValue;
        public string stringValue;

        public SkillEventData(
            SkillEventType eventType,
            BattleFighter source,
            BattleFighter target = null,
            float deltaTime = 0f,
            int intValue = 0,
            string stringValue = null)
        {
            this.eventType = eventType;
            this.source = source;
            this.target = target;
            this.deltaTime = deltaTime;
            this.intValue = intValue;
            this.stringValue = stringValue;
        }
    }
}
