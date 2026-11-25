using System.Collections;
using Orpheus.VN;
using UnityEngine;

namespace Orpheus
{
    public sealed class PaperEffect : MonoBehaviour, IStageEffect, IBlockingStageEffect
    {
        [Tooltip("剧本里 [效果 名字] 写的名字")]
        public string id = "白纸";
        [Tooltip("1 = 单张（有字时先全屏展开）；大于 1 = 白纸雨")]
        public int count = 1;
        [Tooltip("勾上：不落纸，而是清空地上所有的纸")]
        public bool clear;
        [Tooltip("勾上：落 count 张纸，全是之前展开过的纸条")]
        public bool replayWritten;
        [Tooltip("勾上：纸先铺满整个画面，再一起落下")]
        public bool storm;
        public PaperSpawner papers;

        public string Id => id;
        void OnEnable() => VNRegistry.Register(this);
        void OnDisable() => VNRegistry.Unregister(this);

        public void Play(string[] args) => StartCoroutine(PlayRoutine(args));

        public IEnumerator PlayRoutine(string[] args)
        {
            if (clear) { papers.Clear(); yield break; }
            string text = string.Join(" ", args);
            if (replayWritten) { papers.DropWritten(count); yield break; }
            if (storm)
            {
                var notes = new System.Collections.Generic.List<string>();
                foreach (var note in text.Split(';', '；')) if (note.Trim().Length > 0) notes.Add(note.Trim());
                yield return papers.Storm(notes);
                yield break;
            }
            if (count == 1 && text.Length > 0) { yield return papers.Reveal(text); yield break; }
            for (int i = 0; i < count; i++) papers.Drop(text);
        }
    }
}
