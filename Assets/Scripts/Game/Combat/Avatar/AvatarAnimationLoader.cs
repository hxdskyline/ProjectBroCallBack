using System;
using UnityEngine;

namespace Combat.Avatar
{
    public static class AvatarAnimationLoader
    {
        public static AvatarAnimationSet Load(AvatarAnimationDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("[AvatarAnimationLoader] Definition is null.");
                return null;
            }

            ResourceManager resourceManager = GameManager.Instance.ResourceManager;
            AvatarAnimationSet animationSet = new AvatarAnimationSet(definition.AvatarId);

            for (int i = 0; i < definition.Actions.Count; i++)
            {
                AvatarAnimationDefinition.ActionDefinition action = definition.Actions[i];
                AvatarFrameClip clip = new AvatarFrameClip(action.ActionType, action.FPS, action.Loop);

                for (int j = 0; j < action.FrameAddresses.Count; j++)
                {
                    string address = action.FrameAddresses[j];
                    Sprite frame = resourceManager.LoadSprite(address);
                    if (frame == null)
                    {
                        Debug.LogWarning($"[AvatarAnimationLoader] Missing frame: {address} ({action.ActionType})");
                        continue;
                    }

                    clip.AddFrame(address, frame);
                }

                animationSet.AddClip(clip);
            }

            Debug.Log($"[AvatarAnimationLoader] Loaded avatar animation set: {definition.AvatarId}");
            return animationSet;
        }

        public static void LoadAsync(AvatarAnimationDefinition definition, Action<AvatarAnimationSet> onComplete)
        {
            if (definition == null)
            {
                Debug.LogError("[AvatarAnimationLoader] Definition is null.");
                onComplete?.Invoke(null);
                return;
            }

            ResourceManager resourceManager = GameManager.Instance.ResourceManager;
            AvatarAnimationSet animationSet = new AvatarAnimationSet(definition.AvatarId);

            int totalFrames = 0;
            for (int i = 0; i < definition.Actions.Count; i++)
            {
                totalFrames += definition.Actions[i].FrameAddresses.Count;
            }

            if (totalFrames == 0)
            {
                onComplete?.Invoke(animationSet);
                return;
            }

            int finishedFrames = 0;

            for (int i = 0; i < definition.Actions.Count; i++)
            {
                AvatarAnimationDefinition.ActionDefinition action = definition.Actions[i];
                AvatarFrameClip clip = new AvatarFrameClip(action.ActionType, action.FPS, action.Loop);
                clip.EnsureFrameCapacity(action.FrameAddresses.Count);
                animationSet.AddClip(clip);

                for (int j = 0; j < action.FrameAddresses.Count; j++)
                {
                    string address = action.FrameAddresses[j];
                    int frameIndex = j;
                    resourceManager.LoadSpriteAsync(address, sprite =>
                    {
                        if (sprite != null)
                        {
                            clip.SetFrameAt(frameIndex, address, sprite);
                        }
                        else
                        {
                            Debug.LogWarning($"[AvatarAnimationLoader] Missing frame async: {address} ({action.ActionType})");
                        }

                        finishedFrames++;
                        if (finishedFrames >= totalFrames)
                        {
                            for (int actionIndex = 0; actionIndex < definition.Actions.Count; actionIndex++)
                            {
                                AvatarActionType actionType = definition.Actions[actionIndex].ActionType;
                                if (animationSet.TryGetClip(actionType, out AvatarFrameClip loadedClip))
                                {
                                    loadedClip.CompactInvalidFrames();
                                }
                            }

                            Debug.Log($"[AvatarAnimationLoader] Loaded avatar animation set async: {definition.AvatarId}");
                            onComplete?.Invoke(animationSet);
                        }
                    });
                }
            }
        }

        public static void Unload(AvatarAnimationSet animationSet)
        {
            if (animationSet == null)
            {
                return;
            }

            ResourceManager resourceManager = GameManager.Instance.ResourceManager;
            animationSet.Unload(resourceManager);
            //Debug.Log($"[AvatarAnimationLoader] Unloaded avatar animation set: {animationSet.AvatarId}");
        }
    }
}
