using System.Collections;
using Orpheus.VN;
using UnityEngine;

namespace Orpheus
{
    public sealed class SceneryEffect : MonoBehaviour, IStageEffect, IBlockingStageEffect
    {
        [Tooltip("剧本里 [效果 名字] 写的名字：animate1 或 animate2")]
        public string id;
        public StageSceneryView scenery;

        public string Id => id;
        void OnEnable() => VNRegistry.Register(this);
        void OnDisable() => VNRegistry.Unregister(this);

        public void Play(string[] args) => StartCoroutine(PlayRoutine(args));
        public IEnumerator PlayRoutine(string[] args) => scenery.Perform(id);
    }
}
