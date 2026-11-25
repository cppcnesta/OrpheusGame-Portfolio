using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orpheus
{
    [CreateAssetMenu(menuName = "Orpheus/Chapter")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        public string id;
        [Tooltip("剧本里 [玩法 rhythm 段落名] 写的段落名，比如 序幕")]
        public string segment;
        public string title;
        public Sprite background;
        public AudioClip music;
        public RhythmChartData rhythmChart;
        public bool rhythmPaperEnabled;
        [Range(1, 3)] public int rhythmPaperAct = 1;
        [Tooltip("演奏前弹教程（一般只有序幕；补奏不弹）")]
        public bool rhythmTutorial;
        [Tooltip("模拟音游的时长（秒）")]
        [Min(1)] public float mockDuration = 12;
        [Tooltip("排序谜题里没有音符美术时显示的文字")]
        public string noteLabel;

        [Header("音游里的台词和纸条：从剧本里取，不另写一份")]
        [Tooltip("剧本里的段落标题（## 或 ### 后面的名字）。这一段里 [效果 白纸] 写的字，就是音游里落下的纸条")]
        public string scriptSection;
        [Tooltip("长条音符打中时显示谁的台词：说话人名字以这些开头，比如 演员、俄耳甫斯")]
        public string[] lineSpeakers;
        [Tooltip("勾上：取这一段里音游命令之后的台词（序幕：第一日、第七日）；不勾：取之前的（三幕：开演后演员们的对话）")]
        public bool linesAfterRhythm;
        [Tooltip("最多取几个人的几段话（剧本里写一次说话人算一段；0 = 不限）")]
        [Min(0)] public int maxLineBlocks;
        [Tooltip("台词按顺序用完以后从头再来（三幕：演员们反复议论）；不勾就到此为止（序幕的祷词）")]
        public bool repeatLines;

        [NonSerialized] public readonly List<RhythmLine> lines = new List<RhythmLine>();
        [NonSerialized] public readonly List<string> papers = new List<string>();
        [NonSerialized] public int noteSlot;
        [NonSerialized] public readonly List<int> earnedSlots = new List<int>();
    }
}
