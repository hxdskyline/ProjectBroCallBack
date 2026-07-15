using System.Collections.Generic;

namespace Combat.SkillSystem
{
    public class SkillNote
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public SkillNote Parent { get; }

        public SkillNote(SkillNote parent = null)
        {
            Parent = parent;
        }

        public void Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            _values[key] = value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (_values.TryGetValue(key, out object raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            return Parent != null && Parent.TryGet(key, out value);
        }

        public T GetOrDefault<T>(string key, T defaultValue = default)
        {
            return TryGet(key, out T value) ? value : defaultValue;
        }

        public SkillNote CreateChild()
        {
            return new SkillNote(this);
        }
    }
}
