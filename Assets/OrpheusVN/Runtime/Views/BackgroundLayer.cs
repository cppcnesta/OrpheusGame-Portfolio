using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Orpheus.VN
{
    public class BackgroundLayer : MonoBehaviour
    {
        [SerializeField] SpriteRenderer front;
        [SerializeField] SpriteRenderer back;
        [SerializeField] Image uiFront;
        [SerializeField] Image uiBack;

        Coroutine fading;
        public event System.Action<Sprite, float> Changed;
        public Sprite CurrentSprite => uiFront != null ? uiFront.sprite : front.sprite;

        void Awake()
        {
            if (uiFront != null) return;
            front.sortingLayerID = back.sortingLayerID;
            front.sortingOrder = back.sortingOrder + 1;
        }

        public void Show(Sprite sprite, float duration)
        {
            Debug.Log("[VN-Background] " + (sprite != null ? sprite.name : "missing") + " fade=" + duration);
            Changed?.Invoke(sprite, duration);
            if (fading != null) StopCoroutine(fading);
            if (uiFront != null)
            {
                uiBack.sprite = uiFront.sprite; uiBack.color = Color.white;
                uiFront.sprite = sprite;
                fading = StartCoroutine(FadeUI(duration));
                return;
            }

            back.sprite = front.sprite;
            SetAlpha(back, 1f);
            front.sprite = sprite;

            if (duration <= 0f)
            {
                SetAlpha(front, 1f);
                back.sprite = null;
                return;
            }
            fading = StartCoroutine(FadeIn(duration));
        }

        IEnumerator FadeUI(float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            { uiFront.color = new Color(1, 1, 1, t / duration); yield return null; }
            uiFront.color = Color.white;
            uiBack.color = Color.clear;
            fading = null;
        }

        IEnumerator FadeIn(float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetAlpha(front, t / duration);
                yield return null;
            }
            SetAlpha(front, 1f);
            back.sprite = null;
            fading = null;
        }

        static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }
}
