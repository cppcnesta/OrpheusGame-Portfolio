using System;
using UnityEngine;

namespace Orpheus
{
    public enum RhythmNoteKind { Circle, Slider, Spinner, Special }

    [Serializable]
    public sealed class RhythmNoteVisual
    {
        public string childPath;
        public Vector2 anchorMin, anchorMax, pivot, position, size;
        public Vector3 scale = Vector3.one;
        public Quaternion rotation = Quaternion.identity;
        public Sprite sprite;
        public Color color = Color.white;
        public bool hasImage, preserveAspect;
        public string label;
    }

    [Serializable]
    public sealed class RhythmNoteData
    {
        public string name;
        public RhythmNoteKind kind;
        public double hitTime, endTime;
        public double approachTime = 1, perfectWindow = .10, goodWindow = .25;
        public float dragSensitivity = .8f, smoothTime = .08f, requiredRotation = 360;
        public bool triggerPaperEffect;
        [Tooltip("这一幕剧本里没有白纸时，这个音符落下的纸条写的字（有白纸时用剧本里的）")]
        public string rumorText;
        public RhythmNoteVisual[] visuals;
    }

    [CreateAssetMenu(menuName = "Orpheus/Rhythm Chart")]
    public sealed class RhythmChartData : ScriptableObject
    {
        public double duration = 60;
        [Tooltip("歌从第几秒开始放（谱面时间 0 = 歌的这一秒）。比如只用歌的 1:00–3:00 就填 60")]
        public double musicStart;
        [Tooltip("教程：每种音符第一次出现时弹出它的那一页（圆圈、长条、旋转各一个时间）。打中第一个圆圈后再讲分数")]
        public double circleTutorialTime = 2, sliderTutorialTime = 17.2, spinnerTutorialTime = 35;
        public RhythmNoteData[] notes;
    }
}
