using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Orpheus
{
    public sealed class PaperArtView : MonoBehaviour
    {
        public Image bottom, textMask, top;
        public TMP_Text words;
        [Tooltip("中文小字（翻译）。斜杠后面的部分写在这里")]
        public TMP_Text translation;
        public Sprite[] bottomFrames, maskFrames, topFrames;
        [Min(.01f)] public float unfoldSeconds = .45f;

        public float Progress { get; private set; }

        public void SetText(string text)
        {
            text = text ?? "";
            int split = text.IndexOf('/');
            string english = split >= 0 ? text.Substring(0, split).Trim() : text.Trim();
            string chinese = split >= 0 ? text.Substring(split + 1).Trim() : "";
            words.text = english;
            if (translation != null) translation.text = chinese;
        }

        public void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            Frame(bottom, bottomFrames);
            Frame(textMask, maskFrames);
            Frame(top, topFrames);

            float ink = Mathf.SmoothStep(0, 1, (Progress - .35f) / .6f);
            if (words != null) words.alpha = ink;
            if (translation != null) translation.alpha = ink;
        }

        void Frame(Image image, Sprite[] frames)
        {
            if (image == null || frames == null || frames.Length == 0) return;
            image.sprite = frames[Mathf.RoundToInt(Progress * (frames.Length - 1))];
        }

        public void Unfold(string text)
        {
            StopAllCoroutines();
            SetText(text);
            SetProgress(0);
            StartCoroutine(Animate());
        }

        IEnumerator Animate()
        {
            for (float t = 0; t < unfoldSeconds; t += Time.deltaTime)
            {
                SetProgress(t / unfoldSeconds);
                yield return null;
            }
            SetProgress(1);
        }
    }
}
