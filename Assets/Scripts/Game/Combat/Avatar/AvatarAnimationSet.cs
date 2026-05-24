using System.Collections.Generic;
using UnityEngine;

namespace Combat.Avatar
{
    public class AvatarAnimationSet
    {
        private readonly Dictionary<AvatarActionType, AvatarFrameClip> _clips = new Dictionary<AvatarActionType, AvatarFrameClip>();

        public string AvatarId { get; }

        public AvatarAnimationSet(string avatarId)
        {
            AvatarId = avatarId;
        }

        public void AddClip(AvatarFrameClip clip)
        {
            if (clip == null)
            {
                return;
            }

            _clips[clip.ActionType] = clip;
        }

        public bool TryGetClip(AvatarActionType actionType, out AvatarFrameClip clip)
        {
            return _clips.TryGetValue(actionType, out clip);
        }

        public void Unload(ResourceManager resourceManager)
        {
            if (resourceManager == null)
            {
                return;
            }

            foreach (AvatarFrameClip clip in _clips.Values)
            {
                clip.Unload(resourceManager);
            }

            _clips.Clear();
        }
    }

    public class AvatarFrameClip
    {
        public AvatarActionType ActionType { get; }
        public float FPS { get; }
        public bool Loop { get; }
        public IReadOnlyList<Sprite> Frames => _frames;

        private readonly List<Sprite> _frames = new List<Sprite>();
        private readonly List<string> _addresses = new List<string>();

        public AvatarFrameClip(AvatarActionType actionType, float fps, bool loop)
        {
            ActionType = actionType;
            FPS = Mathf.Max(1f, fps);
            Loop = loop;
        }

        public void AddFrame(string address, Sprite sprite)
        {
            _addresses.Add(address);
            _frames.Add(sprite);
        }

        public void EnsureFrameCapacity(int count)
        {
            if (count <= 0)
            {
                return;
            }

            while (_frames.Count < count)
            {
                _frames.Add(null);
                _addresses.Add(string.Empty);
            }
        }

        public void SetFrameAt(int index, string address, Sprite sprite)
        {
            if (index < 0)
            {
                return;
            }

            EnsureFrameCapacity(index + 1);
            _addresses[index] = address;
            _frames[index] = sprite;
        }

        public void Unload(ResourceManager resourceManager)
        {
            for (int i = 0; i < _addresses.Count; i++)
            {
                if (!string.IsNullOrEmpty(_addresses[i]))
                {
                    resourceManager.UnloadResource(_addresses[i]);
                }
            }

            _addresses.Clear();
            _frames.Clear();
        }

        public void CompactInvalidFrames()
        {
            for (int i = _frames.Count - 1; i >= 0; i--)
            {
                if (_frames[i] == null || string.IsNullOrEmpty(_addresses[i]))
                {
                    _frames.RemoveAt(i);
                    _addresses.RemoveAt(i);
                }
            }
        }
    }
}
