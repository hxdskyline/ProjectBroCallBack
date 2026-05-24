using System;
using UnityEngine;

namespace Combat.Avatar
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class AvatarSequencePlayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private AvatarAnimationSet _animationSet;
        private AvatarFrameClip _currentClip;
        private bool _currentLoop;
        private int _frameIndex;
        private float _timer;
        private Action _onComplete;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        public void SetAnimationSet(AvatarAnimationSet animationSet)
        {
            _animationSet = animationSet;
        }

        public bool Play(AvatarActionType actionType, Action onComplete = null, bool? loopOverride = null)
        {
            if (_animationSet == null)
            {
                Debug.LogWarning("[AvatarSequencePlayer] Animation set is null.");
                return false;
            }

            if (!_animationSet.TryGetClip(actionType, out AvatarFrameClip clip))
            {
                Debug.LogWarning($"[AvatarSequencePlayer] Action not found: {actionType}");
                return false;
            }

            if (clip.Frames.Count == 0)
            {
                Debug.LogWarning($"[AvatarSequencePlayer] Action has no frames: {actionType}");
                return false;
            }

            _currentClip = clip;
            _currentLoop = loopOverride ?? _currentClip.Loop;
            _frameIndex = 0;
            _timer = 0f;
            _onComplete = onComplete;
            _spriteRenderer.sprite = _currentClip.Frames[0];

            return true;
        }

        private void Update()
        {
            if (_currentClip == null)
            {
                return;
            }

            if (_currentClip.Frames.Count <= 1)
            {
                return;
            }

            _timer += Time.deltaTime;
            float frameInterval = 1f / _currentClip.FPS;

            while (_timer >= frameInterval)
            {
                _timer -= frameInterval;
                _frameIndex++;

                if (_frameIndex >= _currentClip.Frames.Count)
                {
                    if (_currentLoop)
                    {
                        _frameIndex = 0;
                    }
                    else
                    {
                        _frameIndex = _currentClip.Frames.Count - 1;
                        _spriteRenderer.sprite = _currentClip.Frames[_frameIndex];
                        Action callback = _onComplete;
                        _onComplete = null;
                        callback?.Invoke();
                        _currentClip = null;
                        return;
                    }
                }

                _spriteRenderer.sprite = _currentClip.Frames[_frameIndex];
            }
        }
    }
}
