using System;
using System.Collections.Generic;
using Orpheus.VN;
using UnityEngine;

namespace Orpheus
{
    public sealed class RhythmSegment : MonoBehaviour, IGameplaySegment
    {
        [Tooltip("实现 IRhythmAdapter 的组件：音游 prefab（RhythmPerformance）上的 RhythmChapterAdapter。")]
        [SerializeField] MonoBehaviour adapter;
        [SerializeField] DemoDefinition definition;
        [Tooltip("正式剧本（Orpheus.orph）。音游里的台词和纸条从这里取，见 RhythmScriptContent")]
        [SerializeField] VNScript script;
        [Tooltip("剧本执行器。读变量（X旋律 = pass）知道之前哪几段已经拿到了音符，结算时放进道具栏")]
        [SerializeField] VNRunner runner;
        [Tooltip("剧情的 BGM。真音游自己放歌，演奏时把它暂停，结束后接着放")]
        [SerializeField] AudioSource storyMusic;
        AudioClip pausedClip;
        bool pausedStoryMusic;

        void RestoreStoryMusic()
        {
            if (pausedStoryMusic && storyMusic != null && storyMusic.clip == pausedClip && !storyMusic.isPlaying)
                storyMusic.UnPause();
            pausedStoryMusic = false;
            pausedClip = null;
        }

        public string Type => "rhythm";
        IRhythmAdapter Adapter => (IRhythmAdapter)adapter;
        public MonoBehaviour AdapterComponent => adapter;
        public bool Playing => running != null;

        public void UseAdapter(MonoBehaviour value)
        {
            if (!(value is IRhythmAdapter)) throw new ArgumentException(value + " 没有实现 IRhythmAdapter");
            Cancel();
            adapter = value;
        }
        readonly HashSet<string> played = new HashSet<string>();
        Coroutine running;

        void OnEnable() => VNRegistry.Register(this);
        void OnDisable() { Cancel(); VNRegistry.Unregister(this); }

        void OnValidate()
        {
            if (adapter != null && !(adapter is IRhythmAdapter))
            {
                Debug.LogError(adapter.GetType().Name + " 没有实现 IRhythmAdapter", this);
                adapter = null;
            }
        }

        public void Begin(string segment, Action<string> finished)
        {
            Cancel();
            var chapter = definition.FindSegment(segment);
            if (chapter == null) throw new InvalidOperationException("DemoDefinition 里没有音游段：" + segment);
            bool retry = !played.Add(segment);
            RhythmScriptContent.Fill(chapter, script);
            FillInventory(chapter);
            if (!(adapter is MockRhythmAdapter) && storyMusic != null && storyMusic.isPlaying)
            {
                pausedStoryMusic = true;
                pausedClip = storyMusic.clip;
                storyMusic.Pause();
            }
            try
            {
                running = StartCoroutine(Adapter.Play(chapter, retry, passed =>
                {
                    running = null;
                    RestoreStoryMusic();
                    finished(passed ? "pass" : "fail");
                }));
            }
            catch { Cancel(); throw; }
        }

        void FillInventory(ChapterDefinition chapter)
        {
            chapter.noteSlot = definition.chapters.IndexOf(chapter);
            chapter.earnedSlots.Clear();
            var variables = runner != null && runner.Context != null ? runner.Context.Variables : null;
            if (variables == null) return;
            for (int i = 0; i < definition.chapters.Count; i++)
                if (i != chapter.noteSlot && definition.chapters[i] != null
                    && variables.TryGetValue(definition.chapters[i].segment + "旋律", out var result) && result == "pass")
                    chapter.earnedSlots.Add(i);
        }

        public void Cancel()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            try { if (adapter != null) Adapter.Cancel(); }
            finally { RestoreStoryMusic(); }
        }
    }
}
