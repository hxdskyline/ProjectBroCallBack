using UnityEngine;

namespace Combat.Avatar
{
    [RequireComponent(typeof(AvatarSequencePlayer))]
    public class BattleAvatar : MonoBehaviour
    {
        [SerializeField] private AvatarAnimationDefinition _animationDefinition;
        [SerializeField] private AvatarSequencePlayer _sequencePlayer;
        [SerializeField] private bool _loadOnStart = true;

        private AvatarAnimationSet _animationSet;
        private bool _isLoaded;
        private bool _isDestroyed;
        private AvatarActionType _currentAction = AvatarActionType.Idle;
        private bool _hasPlayedAction;

        private void Awake()
        {
            if (_sequencePlayer == null)
            {
                _sequencePlayer = GetComponent<AvatarSequencePlayer>();
            }
        }

        private void Start()
        {
            if (_loadOnStart)
            {
                LoadAndPlayIdle();
            }
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
            UnloadAnimation();
        }

        public void SetAnimationDefinition(AvatarAnimationDefinition definition)
        {
            _animationDefinition = definition;
            _hasPlayedAction = false;
        }

        public void LoadAndPlayIdle()
        {
            if (_animationDefinition == null)
            {
                Debug.LogWarning("[BattleAvatar] Animation definition is null.");
                return;
            }

            if (_isLoaded)
            {
                PlayIdle();
                return;
            }

            _animationSet = AvatarAnimationLoader.Load(_animationDefinition);
            _isLoaded = _animationSet != null;

            if (_isLoaded)
            {
                _sequencePlayer.SetAnimationSet(_animationSet);
                PlayIdle();
            }
        }

        public void LoadAndPlayIdleAsync()
        {
            if (_animationDefinition == null)
            {
                Debug.LogWarning("[BattleAvatar] Animation definition is null.");
                return;
            }

            if (_isLoaded)
            {
                PlayIdle();
                return;
            }

            AvatarAnimationLoader.LoadAsync(_animationDefinition, loadedSet =>
            {
                if (_isDestroyed)
                {
                    AvatarAnimationLoader.Unload(loadedSet);
                    return;
                }

                _animationSet = loadedSet;
                _isLoaded = _animationSet != null;

                if (_isLoaded)
                {
                    _sequencePlayer.SetAnimationSet(_animationSet);
                    _hasPlayedAction = false;
                    PlayIdle();
                }
            });
        }

        public bool PlayIdle()
        {
            if (!_isLoaded)
            {
                return false;
            }

            if (_currentAction == AvatarActionType.Death)
            {
                return false;
            }

            if (_currentAction == AvatarActionType.Idle && _hasPlayedAction)
            {
                return true;
            }

            bool played = _sequencePlayer.Play(AvatarActionType.Idle);
            if (played)
            {
                _currentAction = AvatarActionType.Idle;
                _hasPlayedAction = true;
            }

            return played;
        }

        public bool PlayRun()
        {
            if (!_isLoaded)
            {
                return false;
            }

            if (_currentAction == AvatarActionType.Death)
            {
                return false;
            }

            if (_currentAction == AvatarActionType.Run && _hasPlayedAction)
            {
                return true;
            }

            bool played = _sequencePlayer.Play(AvatarActionType.Run);
            if (played)
            {
                _currentAction = AvatarActionType.Run;
                _hasPlayedAction = true;
            }

            return played;
        }

        public bool PlayAttackAndReturnIdle()
        {
            if (!_isLoaded)
            {
                return false;
            }

            if (_currentAction == AvatarActionType.Death)
            {
                return false;
            }

            bool attackStarted = _sequencePlayer.Play(AvatarActionType.Attack, OnAttackAnimationFinished, false);
            if (!attackStarted)
            {
                return PlayIdle();
            }

            _currentAction = AvatarActionType.Attack;
            _hasPlayedAction = true;

            return true;
        }

        public bool PlayDeath()
        {
            if (!_isLoaded)
            {
                return false;
            }

            if (_currentAction == AvatarActionType.Death)
            {
                return true;
            }

            bool played = _sequencePlayer.Play(AvatarActionType.Death, null, false);
            if (played)
            {
                _currentAction = AvatarActionType.Death;
                _hasPlayedAction = true;
            }

            return played;
        }

        public void UnloadAnimation()
        {
            if (!_isLoaded)
            {
                return;
            }

            AvatarAnimationLoader.Unload(_animationSet);
            _animationSet = null;
            _isLoaded = false;
            _hasPlayedAction = false;
        }

        private void OnAttackAnimationFinished()
        {
            _currentAction = AvatarActionType.Idle;
            PlayIdle();
        }
    }
}
