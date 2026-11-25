using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Orpheus
{
    public sealed class MockRhythmAdapter : MonoBehaviour, IRhythmAdapter
    {
        public TMP_Text title;
        public TMP_Text timer;
        public Button pass;
        public Button fail;
        public Button pause;
        [Tooltip("暂停时一起暂停的背景音乐")]
        public AudioSource music;

        public bool IsRunning => running;
        public bool IsPaused => paused;
        public ChapterDefinition Chapter { get; private set; }
        public bool Retry { get; private set; }
        public double SongTime { get; private set; }
        public float Duration { get; private set; }
        bool? result;
        bool running;
        bool paused;

        void Awake()
        {
            pass.onClick.AddListener(() => Resolve(true));
            fail.onClick.AddListener(() => Resolve(false));
            pause.onClick.AddListener(() => SetPaused(!paused));
        }

        public void Resolve(bool passed) { if (running && !paused && result == null) result = passed; }

        public IEnumerator Play(ChapterDefinition chapter, bool retry, Action<bool> completed)
        {
            Cancel();
            gameObject.SetActive(true);
            Chapter = chapter;
            Retry = retry;
            running = true;
            result = null;
            SongTime = 0;
            Duration = chapter.mockDuration;
            title.text = (retry ? "重奏 / " : "演奏 / ") + chapter.title;
            while (running && result == null)
            {
                if (!paused) SongTime += Time.unscaledDeltaTime;
                timer.text = paused ? "已暂停" : $"{SongTime:0.0}s / {Duration:0.0}s · 模拟时间线";
                if (SongTime > Duration) timer.text += " · 请选择本段结果";
                yield return null;
            }
            var resolved = result;
            Cancel();
            if (resolved.HasValue) completed?.Invoke(resolved.Value);
        }

        void SetPaused(bool value)
        {
            paused = value;
            if (music == null) return;
            if (paused) music.Pause(); else music.UnPause();
        }

        public void Cancel()
        {
            if (paused) SetPaused(false);
            running = false;
            gameObject.SetActive(false);
        }

        void OnDisable() { running = false; }
    }
}
