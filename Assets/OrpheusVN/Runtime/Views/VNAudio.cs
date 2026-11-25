using System.Collections;
using UnityEngine;

namespace Orpheus.VN
{
    public class VNAudio : MonoBehaviour
    {
        [SerializeField] AudioSource bgmSource;
        [SerializeField] AudioSource seSource;
        [SerializeField, Range(0f, 1f)] float bgmVolume = 0.8f;
        public float BgmVolume => bgmVolume;
        public bool BgmPlaying => bgmSource.clip != null && bgmSource.isPlaying;

        Coroutine fading;

        public void PlayBgm(AudioClip clip, float fadeIn)
        {
            if (clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            if (fading != null) StopCoroutine(fading);
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = fadeIn > 0f ? 0f : bgmVolume;
            bgmSource.Play();
            if (fadeIn > 0f) fading = StartCoroutine(FadeVolume(bgmVolume, fadeIn, false));
        }

        public void StopBgm(float fadeOut)
        {
            if (fading != null) StopCoroutine(fading);
            if (fadeOut <= 0f) bgmSource.Stop();
            else fading = StartCoroutine(FadeVolume(0f, fadeOut, true));
        }

        public void PauseBgm(float fadeOut)
        {
            if (!bgmSource.isPlaying) return;
            if (fading != null) StopCoroutine(fading);
            if (fadeOut <= 0f) { bgmSource.Pause(); return; }
            fading = StartCoroutine(FadeVolume(0f, fadeOut, false, true));
        }

        public void ResumeBgm(float fadeIn)
        {
            if (bgmSource.clip == null || bgmSource.isPlaying && bgmSource.volume >= bgmVolume) return;
            if (fading != null) StopCoroutine(fading);
            if (!bgmSource.isPlaying) { if (fadeIn > 0f) bgmSource.volume = 0f; bgmSource.UnPause(); }
            if (fadeIn <= 0f) bgmSource.volume = bgmVolume;
            else fading = StartCoroutine(FadeVolume(bgmVolume, fadeIn, false));
        }

        public void PlaySe(AudioClip clip)
        {
            if (clip != null) seSource.PlayOneShot(clip);
        }

        IEnumerator FadeVolume(float target, float duration, bool stopAtEnd, bool pauseAtEnd = false)
        {
            float start = bgmSource.volume;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                bgmSource.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            bgmSource.volume = target;
            if (stopAtEnd) bgmSource.Stop();
            if (pauseAtEnd) bgmSource.Pause();
            fading = null;
        }
    }
}
