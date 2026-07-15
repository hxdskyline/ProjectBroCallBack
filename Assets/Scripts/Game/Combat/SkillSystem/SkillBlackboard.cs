using System.Collections.Generic;

namespace Combat.SkillSystem
{
    public class SkillBlackboard
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

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

            if (!_values.TryGetValue(key, out object raw))
            {
                return false;
            }

            if (raw is T typed)
            {
                value = typed;
                return true;
            }

            return false;
        }

        public T GetOrDefault<T>(string key, T defaultValue = default)
        {
            return TryGet(key, out T value) ? value : defaultValue;
        }

        public bool Contains(string key)
        {
            return !string.IsNullOrEmpty(key) && _values.ContainsKey(key);
        }

        public void Remove(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _values.Remove(key);
            }
        }
    }
}
