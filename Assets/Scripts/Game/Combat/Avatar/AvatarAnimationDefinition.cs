using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Avatar
{
    [CreateAssetMenu(fileName = "AvatarAnimationDefinition", menuName = "Game/Avatar/Animation Definition")]
    public class AvatarAnimationDefinition : ScriptableObject
    {
        [SerializeField] private string _avatarId;
        [SerializeField] private List<ActionDefinition> _actions = new List<ActionDefinition>();

        public string AvatarId => _avatarId;
        public IReadOnlyList<ActionDefinition> Actions => _actions;

        public ActionDefinition GetAction(AvatarActionType actionType)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].ActionType == actionType)
                {
                    return _actions[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 运行时创建 AvatarAnimationDefinition（用于族群战斗单位）
        /// idleAddress: 站立帧地址, attackAddress: 攻击帧地址, deathAddress: 死亡帧地址
        /// </summary>
        public static AvatarAnimationDefinition CreateRuntime(string avatarId, string idleAddress, string attackAddress, string deathAddress = null)
        {
            var def = CreateInstance<AvatarAnimationDefinition>();
            def._avatarId = avatarId;
            def._actions = new List<ActionDefinition>
            {
                new ActionDefinition(AvatarActionType.Idle,   new List<string> { idleAddress },   4f, true),
                new ActionDefinition(AvatarActionType.Run,    new List<string> { idleAddress },   4f, true),
                new ActionDefinition(AvatarActionType.Attack, new List<string> { attackAddress, idleAddress }, 8f, false),
                new ActionDefinition(AvatarActionType.Death,  new List<string> { string.IsNullOrEmpty(deathAddress) ? attackAddress : deathAddress }, 4f, false),
            };
            return def;
        }

        [Serializable]
        public class ActionDefinition
        {
            [SerializeField] private AvatarActionType _actionType;
            [SerializeField] private List<string> _frameAddresses = new List<string>();
            [SerializeField] private float _fps = 12f;
            [SerializeField] private bool _loop = true;

            public AvatarActionType ActionType => _actionType;
            public IReadOnlyList<string> FrameAddresses => _frameAddresses;
            public float FPS => Mathf.Max(1f, _fps);
            public bool Loop => _loop;

            public ActionDefinition(AvatarActionType actionType, List<string> frameAddresses, float fps, bool loop)
            {
                _actionType = actionType;
                _frameAddresses = frameAddresses;
                _fps = fps;
                _loop = loop;
            }
        }
    }
}
