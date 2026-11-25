using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Orpheus
{
    public sealed class PuzzlePanel : MonoBehaviour
    {
        public DemoDefinition definition;
        public RectTransform slotsRoot;
        public GameObject slotTemplate;
        public Button listen;
        public Button submit;
        public Button reset;
        public AudioSource audioSource;
        public bool Locked { get; private set; }
        public Sprite[] noteSprites;
        [Tooltip("四枚音符的声音，按章节顺序（序幕、第一幕……）。没拖时用程序合成的音")]
        public AudioClip[] noteSounds;
        [Tooltip("试听 / 提交时两个音之间最多隔几秒（长的音会被下一个音盖过去）")]
        public float noteSpacing = 2.2f;
        [Tooltip("面板背后的白纸（和剧情里的白纸同一张纸），打开时展开")]
        public PaperArtView paper;
        public float slotSpacing = 210;

        public IReadOnlyList<string> Order => order;

        readonly List<string> order = new List<string>();
        readonly List<GameObject> slots = new List<GameObject>();
        Action<bool> callback;
        bool? resolved;
        int selected = -1;
        AudioClip[] tones;

        void Awake()
        {
            listen.onClick.AddListener(() => { if (!Locked) StartCoroutine(PlayMelody(false)); });
            submit.onClick.AddListener(() => { if (!Locked) StartCoroutine(PlayMelody(true)); });
            reset.onClick.AddListener(() => { if (!Locked) ResetOrder(); });
        }

        public IEnumerator Play(Action<bool> completed)
        {
            Cancel();
            gameObject.SetActive(true);
            callback = completed;
            resolved = null;
            if (paper != null) paper.Unfold("");
            ResetOrder();

            while (!resolved.HasValue)
                yield return null;

            var outcome = resolved.Value;
            var handler = callback;
            Cancel();
            handler?.Invoke(outcome);
        }

        void ResetOrder()
        {
            order.Clear();
            order.AddRange(definition.chapters.Select(x => x.id).Reverse());
            selected = -1;

            foreach (var slot in slots)
                Destroy(slot);
            slots.Clear();

            for (var i = 0; i < order.Count; i++)
            {
                var slot = Instantiate(slotTemplate, slotsRoot);
                slot.SetActive(true);

                var rect = (RectTransform)slot.transform;
                rect.anchoredPosition = new Vector2((i - (order.Count - 1) / 2f) * slotSpacing, 0);

                var target = slot.GetComponent<NoteSlot>();
                target.owner = this;
                target.index = i;

                var token = slot.GetComponentInChildren<NoteToken>();
                token.owner = this;
                token.index = i;

                int index = i;
                token.GetComponent<Button>().onClick.AddListener(() => Click(index));
                slots.Add(slot);
            }

            Refresh();
        }

        void Click(int index)
        {
            if (Locked) return;
            PlayNote(order[index]);
            if (selected < 0)
            {
                selected = index;
            }
            else
            {
                Swap(selected, index);
                selected = -1;
            }
            Refresh();
        }

        public void Swap(int from, int to)
        {
            if (Locked || from < 0 || to < 0 || from >= order.Count || to >= order.Count) return;
            (order[from], order[to]) = (order[to], order[from]);
            selected = -1;
            Refresh();
        }

        void Refresh()
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var chapter = definition.chapters.Find(x => x.id == order[i]);
                var token = slots[i].GetComponentInChildren<NoteToken>();
                int number = definition.chapters.IndexOf(chapter) + 1;
                var art = noteSprites != null && number < noteSprites.Length ? noteSprites[number] : null;
                token.GetComponentInChildren<TMP_Text>().text = art != null ? "" : chapter.noteLabel;

                var image = token.GetComponent<Image>();
                if (art != null)
                {
                    image.sprite = art;
                    image.preserveAspect = true;
                }
                image.color = i == selected
                    ? new Color(1f, .8f, .48f)
                    : art != null ? Color.white : new Color(.23f, .19f, .14f);
            }
        }

        IEnumerator PlayMelody(bool commit)
        {
            Locked = true;
            var snapshot = order.ToArray();
            EnsureTones();

            foreach (var id in snapshot)
            {
                var clip = PlayNote(id);
                float wait = clip != null && clip != Tone(id)
                    ? Mathf.Min(clip.length, noteSpacing)
                    : 0.32f;
                yield return new WaitForSecondsRealtime(wait);
            }

            if (commit)
                resolved = snapshot.SequenceEqual(definition.prototypeTrueOrder);
            Locked = false;
        }

        AudioClip Tone(string id) =>
            tones[Mathf.Clamp(definition.chapters.FindIndex(x => x.id == id), 0, tones.Length - 1)];

        AudioClip PlayNote(string id)
        {
            EnsureTones();
            int index = definition.chapters.FindIndex(x => x.id == id);
            bool real = noteSounds != null && index >= 0 && index < noteSounds.Length && noteSounds[index] != null;
            var clip = real ? noteSounds[index] : Tone(id);
            audioSource.PlayOneShot(clip, real ? .8f : .2f);
            return clip;
        }

        // 未配置音符音频时，合成四段衰减正弦波作为原型兜底音
        void EnsureTones()
        {
            if (tones != null) return;
            tones = new AudioClip[4];
            for (var n = 0; n < tones.Length; n++)
            {
                var samples = new float[11025];
                float frequency = 220 * Mathf.Pow(2, n * 2 / 12f);
                for (var i = 0; i < samples.Length; i++)
                    samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / 44100) * Mathf.Exp(-i / 2600f);

                tones[n] = AudioClip.Create("PrototypeTone" + n, samples.Length, 1, 44100, false);
                tones[n].SetData(samples, 0);
            }
        }

        public void Cancel()
        {
            StopAllCoroutines();
            if (audioSource != null) audioSource.Stop();
            callback = null;
            Locked = false;
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (tones == null) return;
            foreach (var tone in tones)
                Destroy(tone);
        }
    }
}
