using System;
using Orpheus.VN;
using UnityEngine;

namespace Orpheus
{
    public sealed class PuzzleSegment : MonoBehaviour, IGameplaySegment
    {
        [SerializeField] PuzzlePanel panel;
        [Tooltip("剧情的声音：排序时暂停它的 BGM")]
        [SerializeField] VNAudio music;
        [SerializeField] float musicFadeSeconds = 1f;

        public string Type => "notes";
        Coroutine running;

        void OnEnable() => VNRegistry.Register(this);
        void OnDisable() { Cancel(); VNRegistry.Unregister(this); }

        public void Begin(string arg, Action<string> finished)
        {
            if (music != null) music.PauseBgm(musicFadeSeconds);
            running = StartCoroutine(panel.Play(correct =>
            {
                running = null;
                if (music != null) music.ResumeBgm(musicFadeSeconds);
                finished(correct ? "calling" : "false");
            }));
        }

        public void Cancel()
        {
            bool wasRunning = running != null;
            if (running != null) StopCoroutine(running);
            running = null;
            if (panel != null) panel.Cancel();
            if (wasRunning && music != null) music.ResumeBgm(musicFadeSeconds);
        }
    }
}
