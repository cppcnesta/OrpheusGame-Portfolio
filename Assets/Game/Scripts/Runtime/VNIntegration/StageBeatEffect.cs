using Orpheus.VN;
using UnityEngine;

namespace Orpheus
{
    public sealed class StageBeatEffect : MonoBehaviour, IStageEffect
    {
        public StageSceneryView scenery;

        public string Id => "布景";
        void OnEnable() => VNRegistry.Register(this);
        void OnDisable() => VNRegistry.Unregister(this);

        public void Play(string[] args)
        {
            if (args.Length > 0 && int.TryParse(args[0], out int beat)) scenery.Cue(beat);
            else Debug.LogWarning("[布景] 要写第几段，比如 [效果 布景 2]");
        }
    }
}
