using Orpheus.VN;
using UnityEngine;

namespace Orpheus
{
    public sealed class SpotlightEffect : MonoBehaviour, IStageEffect
    {
        public StageSceneryView scenery;

        public string Id => "灯光";
        void OnEnable() => VNRegistry.Register(this);
        void OnDisable() => VNRegistry.Unregister(this);

        public void Play(string[] args) => scenery.Light(args.Length > 0 ? args[0] : "亮");
    }
}
