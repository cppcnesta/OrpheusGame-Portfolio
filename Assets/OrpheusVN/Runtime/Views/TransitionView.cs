using System;
using System.Collections;
using UnityEngine;

namespace Orpheus.VN
{
    public class TransitionView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [Tooltip("游戏开始时是不是全黑。序幕从黑屏淡入时打勾。")]
        [SerializeField] bool startBlack = false;

        Coroutine fading;
        public void Cancel()
        {
            StopAllCoroutines(); fading = null;
            group.alpha = 0f; group.blocksRaycasts = false;
        }

        void Awake()
        {
            group.alpha = startBlack ? 1f : 0f;
            group.blocksRaycasts = startBlack;
        }

        public void Fade(bool toBlack, float duration, Action done)
        {
            if (fading != null) StopCoroutine(fading);
            fading = StartCoroutine(FadeRoutine(toBlack ? 1f : 0f, duration, done));
        }

        IEnumerator FadeRoutine(float target, float duration, Action done)
        {
            group.blocksRaycasts = true;
            float start = group.alpha;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            group.alpha = target;
            group.blocksRaycasts = target > 0.5f;
            fading = null;
            if (done != null) done();
        }
    }
}
