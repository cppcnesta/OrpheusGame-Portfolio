using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orpheus.VN
{
    [CreateAssetMenu(menuName = "Orpheus/VN/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Tooltip("程序里用的名字，英文小写，比如 orpheus")]
        public string id;

        [Tooltip("对话框里显示的名字，比如 俄耳甫斯")]
        public string displayName;

        [Tooltip("剧本里的其他写法，比如 俄尔甫斯。导入和查找时都当成同一个人。")]
        public string[] aliases = new string[0];

        [Header("对话框立绘：说话时显示在对话框旁边（俄耳甫斯、哈迪斯这类）")]
        [Tooltip("对话框里的大立绘。表情差分在下面的 Expressions 里配。")]
        public Sprite portrait;

        [Header("舞台立绘：[登场] 后站在舞台上（演员这类）")]
        [Tooltip("[登场 角色 - 站位] 后，这张图站在舞台的站位上。留空 = 不上台。站位在场景的 VN Actors 里。")]
        public Sprite stageSprite;
        [Tooltip("说话时用气泡，而不是底部的对话框。")]
        public bool stageBubble;
        [Tooltip("不在台上时，气泡固定在右边（勾上）还是左边。在台上时气泡跟着人走。")]
        public bool bubbleOnRight;

        public List<Expression> expressions = new List<Expression>();

        [Serializable]
        public class Expression
        {
            [Tooltip("剧本里写的表情名，比如 悲")]
            public string key;

            [Tooltip("这个表情的对话框立绘")]
            public Sprite sprite;
            [Tooltip("临时用默认立绘替代未交付的差分；检查工具仍会报告。")]
            public bool provisional;
        }

        public bool Matches(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name == id || name == displayName) return true;
            return aliases != null && Array.IndexOf(aliases, name) >= 0;
        }
    }
}
