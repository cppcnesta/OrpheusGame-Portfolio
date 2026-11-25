using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Orpheus.VN
{
    [CreateAssetMenu(menuName = "Orpheus/Asset Library")]
    public class VNAssetLibrary : ScriptableObject
    {
        [Serializable]
        public class NamedSprite
        {
            public string name;
            public Sprite sprite;
        }

        [Serializable]
        public class NamedClip
        {
            public string name;
            public AudioClip clip;
        }

        [Serializable]
        public class NamedVideo
        {
            public string name;
            public VideoClip clip;
            [Tooltip("勾上：循环播放，剧本不等它。不勾：播一遍，剧本等它播完再往下")]
            public bool loop;
        }

        public List<NamedSprite> backgrounds = new List<NamedSprite>();
        public List<NamedSprite> cgs = new List<NamedSprite>();
        public List<NamedClip> bgm = new List<NamedClip>();
        public List<NamedClip> se = new List<NamedClip>();
        [Tooltip("CG 动画。剧本里和图片 CG 一样写 [CG 名字]")]
        public List<NamedVideo> videos = new List<NamedVideo>();

        public Sprite GetBackground(string name) { return Find(backgrounds, name, "背景"); }
        public Sprite GetCG(string name) { return Find(cgs, name, "CG"); }
        public AudioClip GetBgm(string name) { return Find(bgm, name, "BGM"); }
        public AudioClip GetSe(string name) { return Find(se, name, "音效"); }

        public NamedVideo FindVideo(string name) { return videos.Find(x => x.name == name); }

        public string Problem(string kind, string name)
        {
            switch (kind)
            {
                case "背景": return Check(backgrounds, name);
                case "CG":
                    var video = FindVideo(name);
                    if (video != null) return video.clip == null ? "登记了名字，但没有拖视频" : null;
                    return Check(cgs, name);
                case "BGM": return Check(bgm, name);
                case "音效": return Check(se, name);
            }
            return null;
        }

        static string Check(List<NamedSprite> list, string name)
        {
            foreach (var item in list)
            {
                if (item.name == name) return item.sprite == null ? "登记了名字，但没有拖图片" : null;
            }
            return "没有登记";
        }

        static string Check(List<NamedClip> list, string name)
        {
            foreach (var item in list)
            {
                if (item.name == name) return item.clip == null ? "登记了名字，但没有拖音频" : null;
            }
            return "没有登记";
        }

        static Sprite Find(List<NamedSprite> list, string name, string kind)
        {
            foreach (var item in list)
            {
                if (item.name == name) return item.sprite;
            }
            Debug.LogWarning("[VN] 资源表里没有" + kind + "「" + name + "」");
            return null;
        }

        static AudioClip Find(List<NamedClip> list, string name, string kind)
        {
            foreach (var item in list)
            {
                if (item.name == name) return item.clip;
            }
            Debug.LogWarning("[VN] 资源表里没有" + kind + "「" + name + "」");
            return null;
        }
    }
}
