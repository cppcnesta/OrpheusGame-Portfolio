using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Orpheus.VN
{
    public class CGLayer : MonoBehaviour
    {
        [SerializeField] Image image;
        [SerializeField] CanvasGroup group;
        [SerializeField] RawImage video;
        [SerializeField] VideoPlayer player;

        Coroutine fading;
        RenderTexture target;
        bool videoEnded;

        public bool PlayingVideo { get { return player != null && player.clip != null && !player.isLooping && !videoEnded; } }

        void Awake()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            if (player != null)
            {
                player.loopPointReached += _ => videoEnded = true;
                player.errorReceived += (_, message) => { Debug.LogWarning("[VN] CG 动画播放失败：" + message); videoEnded = true; };
            }
            if (video != null) video.enabled = false;
        }

        public void Show(Sprite sprite, float duration)
        {
            StopVideo();
            image.enabled = true;
            image.sprite = sprite;
            FadeTo(1f, duration);
        }

        public void ShowVideo(VideoClip clip, bool loop, float duration)
        {
            if (target == null || target.width != (int)clip.width || target.height != (int)clip.height)
            {
                if (target != null) target.Release();
                target = new RenderTexture((int)clip.width, (int)clip.height, 0);
            }
            image.enabled = false;
            video.texture = target;
            video.enabled = true;
            player.targetTexture = target;
            player.clip = clip;
            player.isLooping = loop;
            videoEnded = false;
            player.Play();
            FadeTo(1f, duration);
        }

        public void Hide(float duration)
        {
            FadeTo(0f, duration);
        }

        void StopVideo()
        {
            if (player == null) return;
            player.Stop();
            player.clip = null;
            if (video != null) video.enabled = false;
        }

        void FadeTo(float target, float duration)
        {
            if (fading != null) StopCoroutine(fading);
            fading = StartCoroutine(Fade(target, duration));
        }

        IEnumerator Fade(float target, float duration)
        {
            float start = group.alpha;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            group.alpha = target;
            if (target <= 0f) StopVideo();
            fading = null;
        }

        void OnDestroy()
        {
            if (target != null) target.Release();
        }
    }
}
