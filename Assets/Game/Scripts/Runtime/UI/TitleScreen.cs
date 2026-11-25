using System;
using System.Collections;
using Orpheus.VN;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Orpheus
{
    public sealed class TitleScreen : MonoBehaviour
    {
        public CanvasGroup group;
        public Button start, load, credits, quit;
        [Tooltip("功能还没做的按钮：选项设置")]
        public Button[] pending;
        public float fadeSeconds = .8f;

        [Header("读档")]
        [Tooltip("鼠标移到「加载」上时显示存档信息")]
        public TMP_Text savePreview;
        [Tooltip("读档时盖住画面的黑幕，上面写着「读取中」")]
        public CanvasGroup loadingCover;

        [Header("制作人员名单")]
        public CanvasGroup creditsPanel;
        [Tooltip("主页的背景音乐（循环）。点「开始」或「加载」后淡出")]
        public AudioSource music;

        [Header("鼠标波纹")]
        public RectTransform rippleSpace;
        [Tooltip("鼠标移动多远（画布像素）留下一圈波纹")]
        public float rippleSpacing = 70;

        [Header("声音（可以不拖）")]
        public AudioSource sfx;
        public AudioClip hoverSound, startSound, idleCall, loadEnterSound, loadCompleteSound, savePreviewSound;
        [Tooltip("在主页上多少秒没有操作，传来第一声呼唤；之后每隔 idleRepeat 秒再呼唤一次")]
        public float idleSeconds = 12, idleRepeat = 30;

        public bool LoadRequested { get; private set; }

        bool started, creditsOpen;
        float idle;
        static readonly int RipplesId = Shader.PropertyToID("_MouseRipples");
        readonly Vector4[] ripples = new Vector4[12];
        int nextRipple;
        Vector2 lastRipple = new Vector2(99999, 99999);

        void Awake()
        {
            start.onClick.AddListener(() => Choose(false));
            load.onClick.AddListener(() => Choose(true));
            credits.onClick.AddListener(() => StartCoroutine(ShowCredits()));
            quit.onClick.AddListener(Quit);

            foreach (var button in pending)
                button.interactable = false;

            load.interactable = VNSaveStore.Exists;
            foreach (var button in new[] { start, quit })
                AddHover(button);

            AddPreview();
            savePreview.gameObject.SetActive(false);

            loadingCover.alpha = 0;
            loadingCover.blocksRaycasts = false;

            creditsPanel.alpha = 0;
            creditsPanel.blocksRaycasts = false;
            creditsPanel.gameObject.SetActive(false);
        }

        void Choose(bool fromSave)
        {
            if (started) return;
            started = true;
            LoadRequested = fromSave;
            Play(fromSave ? loadEnterSound : startSound);
        }

        void AddHover(Button button)
        {
            Trigger(button, EventTriggerType.PointerEnter, () =>
            {
                if (button.interactable && !started)
                    Play(hoverSound);
            });
        }

        void AddPreview()
        {
            Trigger(load, EventTriggerType.PointerEnter, () =>
            {
                if (!load.interactable || started || !VNSaveStore.TryRead(out var data)) return;
                savePreview.text = data.title + "　·　" + data.savedAt;
                savePreview.gameObject.SetActive(true);
            });
            Trigger(load, EventTriggerType.PointerExit, () => savePreview.gameObject.SetActive(false));
        }

        static void Trigger(Component target, EventTriggerType type, Action action)
        {
            var trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        void Play(AudioClip clip)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip);
        }

        void OnEnable()
        {
            Array.Clear(ripples, 0, ripples.Length);
            Shader.SetGlobalVectorArray(RipplesId, ripples);
            if (music != null && !music.isPlaying) music.Play();
        }

        void Update()
        {
            UpdateRipples();
            if (started || creditsOpen) return;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            bool input = (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0 || mouse.leftButton.isPressed))
                || (keyboard != null && keyboard.anyKey.isPressed);

            idle = input ? 0 : idle + Time.unscaledDeltaTime;
            if (idle >= idleSeconds)
            {
                Play(idleCall);
                idle = idleSeconds - idleRepeat;
            }
        }

        void UpdateRipples()
        {
            var mouse = Mouse.current;
            if (mouse == null || rippleSpace == null || creditsOpen) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rippleSpace, mouse.position.ReadValue(), null, out var local))
                return;

            float moved = Vector2.Distance(local, lastRipple);
            if (moved < rippleSpacing && !mouse.leftButton.wasPressedThisFrame) return;

            float strength = mouse.leftButton.wasPressedThisFrame
                ? 1.6f
                : Mathf.Clamp(moved / (rippleSpacing * 2), .5f, 1f);
            if (lastRipple.x > 9999) strength = .5f;

            AddRipple(local, strength);
        }

        public void AddRipple(Vector2 local, float strength)
        {
            ripples[nextRipple] = new Vector4(local.x, local.y, Time.timeSinceLevelLoad, strength);
            nextRipple = (nextRipple + 1) % ripples.Length;
            lastRipple = local;
            Shader.SetGlobalVectorArray(RipplesId, ripples);
        }

        public IEnumerator WaitForStart()
        {
            if (!isActiveAndEnabled) yield break;

            group.alpha = 1;
            group.blocksRaycasts = true;
            while (!started) yield return null;

            group.blocksRaycasts = false;
            savePreview.gameObject.SetActive(false);
            if (music != null) StartCoroutine(FadeOutMusic(fadeSeconds + .6f));

            if (LoadRequested)
            {
                loadingCover.blocksRaycasts = true;
                yield return Fade(loadingCover, 0, 1, .4f);
                yield break;
            }

            yield return Fade(group, 1, 0, fadeSeconds);
            gameObject.SetActive(false);
        }

        public IEnumerator RevealAfterLoad()
        {
            if (!isActiveAndEnabled) yield break;

            foreach (Transform child in transform)
            {
                if (child != loadingCover.transform)
                    child.gameObject.SetActive(false);
            }

            yield return new WaitForSecondsRealtime(.4f);
            Play(loadCompleteSound);
            yield return Fade(loadingCover, 1, 0, fadeSeconds);
            gameObject.SetActive(false);
        }

        IEnumerator ShowCredits()
        {
            if (creditsOpen || started) yield break;

            creditsOpen = true;
            creditsPanel.gameObject.SetActive(true);
            creditsPanel.blocksRaycasts = true;
            yield return Fade(creditsPanel, 0, 1, .5f);
            yield return null;

            while (true)
            {
                var mouse = Mouse.current;
                var keyboard = Keyboard.current;
                if ((mouse != null && mouse.leftButton.wasPressedThisFrame)
                    || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
                {
                    break;
                }
                yield return null;
            }

            creditsPanel.blocksRaycasts = false;
            yield return Fade(creditsPanel, 1, 0, .4f);
            creditsPanel.gameObject.SetActive(false);
            creditsOpen = false;
            idle = 0;
        }

        IEnumerator FadeOutMusic(float seconds)
        {
            float from = music.volume;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                music.volume = from * (1 - t / seconds);
                yield return null;
            }
            music.Stop();
            music.volume = from;
        }

        static IEnumerator Fade(CanvasGroup g, float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            g.alpha = to;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
