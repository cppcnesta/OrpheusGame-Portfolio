using Orpheus.VN;
using UnityEngine;

namespace Orpheus
{
    public sealed class DialogueChime : MonoBehaviour
    {
        public DialogueView dialogue;
        public AudioSource source;
        public AudioClip[] notes;
        [Tooltip("剧情的声音层：取 BGM 的音量设置")]
        public VNAudio music;
        [Tooltip("每个音相对 BGM 音量的系数，让它听起来比背景音乐低三成左右（和 notes 一一对应）")]
        public float[] gains = { .78f, .56f, .6f, .79f };
        [Tooltip("音游进行时不响")]
        public RhythmSegment rhythm;
        [Tooltip("只在这些剧本段落里响（段落 = 剧本里的 ## 或 ### 标题）。不在名单里的段落（序幕、三幕舞台、尾声、真假结局……）都不响")]
        public string[] chimeSections = { "第一幕", "第二幕", "第三幕", "检查·第一幕", "检查·第二幕", "检查·第三幕", "解谜",
            "补奏", "补奏·序幕", "补奏·第一幕", "补奏·第二幕", "补奏·第三幕" };
        GameFlow flow;
        int next;
        public int ChimeCount { get; private set; }

        void OnEnable() { if (dialogue != null) dialogue.LineShown += Chime; }
        void OnDisable() { if (dialogue != null) dialogue.LineShown -= Chime; }

        void Chime()
        {
            if (source == null || notes == null || notes.Length == 0) return;
            if (rhythm != null && rhythm.Playing) return;
            if (InSilentSection()) return;
            source.Stop();
            source.clip = notes[next];
            float gain = gains != null && next < gains.Length ? gains[next] : .7f;
            source.volume = Mathf.Clamp01(gain * (music != null ? music.BgmVolume : 1f));
            source.Play();
            ChimeCount++;
            next = (next + 1) % notes.Length;
        }

        public string CurrentSection()
        {
            if (flow == null) flow = FindAnyObjectByType<GameFlow>();
            var entry = flow != null && flow.runner != null ? flow.runner.CurrentEntry : null;
            int at = entry != null && flow.script != null ? flow.script.entries.IndexOf(entry) : -1;
            if (at < 0) return null;
            string section = "";
            foreach (var label in flow.script.labels)
                if (label.index <= at) section = label.name; else break;
            return section;
        }

        bool InSilentSection()
        {
            var section = CurrentSection();
            return section != null && (chimeSections == null || System.Array.IndexOf(chimeSections, section) < 0);
        }
    }
}
