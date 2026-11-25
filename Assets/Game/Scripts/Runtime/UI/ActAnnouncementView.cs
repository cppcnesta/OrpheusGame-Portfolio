using System;
using System.Collections;
using Orpheus.VN;
using TMPro;
using UnityEngine;

namespace Orpheus
{
    public sealed class ActAnnouncementView : MonoBehaviour, IStageEffect, IBlockingStageEffect
    {
        public CanvasGroup lettering;
        public TMP_Text title, subtitle;
        [Tooltip("报幕时收起对话框")]
        public Orpheus.VN.DialogueView dialogue;
        public float fadeSeconds = .55f, holdSeconds = 1.5f;
        [Tooltip("剧本里写的幕名，和下面的副标题一一对应")]
        public string[] acts = { "第一幕", "第二幕", "第三幕" };
        public string[] subtitles = { "众说", "咏叹", "神话" };

        public int ShownCount { get; private set; }
        public string Id => "报幕";

        void Awake()
        {
            lettering.alpha = 0;
            lettering.blocksRaycasts = false;
        }

        void OnEnable() => VNRegistry.Register(this);
        void OnDisable() => VNRegistry.Unregister(this);

        public void Play(string[] args) => StartCoroutine(PlayRoutine(args));

        public IEnumerator PlayRoutine(string[] args)
        {
            string act = args.Length > 0 ? args[0] : "";
            int index = Array.IndexOf(acts, act);
            title.text = act;
            subtitle.text = index >= 0 && index < subtitles.Length ? subtitles[index] : "";
            if (dialogue != null) dialogue.Hide();
            ShownCount++;
            Debug.Log("[ActAnnouncement] " + act);

            yield return Fade(0, 1);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1, 0);
        }

        IEnumerator Fade(float a, float b)
        {
            for (float t = 0; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                lettering.alpha = Mathf.Lerp(a, b, t / fadeSeconds);
                yield return null;
            }
            lettering.alpha = b;
        }
    }
}
