using System.Collections.Generic;
using UnityEngine;

namespace Orpheus.VN
{
    public static class VNRegistry
    {
        static readonly Dictionary<string, IStageEffect> effects = new Dictionary<string, IStageEffect>();
        static readonly Dictionary<string, IGameplaySegment> segments = new Dictionary<string, IGameplaySegment>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            effects.Clear();
            segments.Clear();
        }

        public static void Register(IStageEffect effect)
        {
            effects[effect.Id] = effect;
        }

        public static void Unregister(IStageEffect effect)
        {
            IStageEffect current;
            if (effects.TryGetValue(effect.Id, out current) && current == effect) effects.Remove(effect.Id);
        }

        public static void Register(IGameplaySegment segment)
        {
            segments[segment.Type] = segment;
        }

        public static void Unregister(IGameplaySegment segment)
        {
            IGameplaySegment current;
            if (segments.TryGetValue(segment.Type, out current) && current == segment) segments.Remove(segment.Type);
        }

        public static bool TryGetEffect(string id, out IStageEffect effect)
        {
            return effects.TryGetValue(id, out effect);
        }

        public static bool TryGetSegment(string type, out IGameplaySegment segment)
        {
            return segments.TryGetValue(type, out segment);
        }
    }

    public static class VNEffects
    {
        public static void Play(string id, params string[] args)
        {
            IStageEffect effect;
            if (VNRegistry.TryGetEffect(id, out effect)) effect.Play(args);
            else Debug.LogWarning("[VN] 找不到舞台效果「" + id + "」。它所在的场景加载了吗？");
        }
    }
}
