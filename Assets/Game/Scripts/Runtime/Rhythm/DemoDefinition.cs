using System.Collections.Generic;
using UnityEngine;

namespace Orpheus
{
    [CreateAssetMenu(menuName = "Orpheus/Demo")]
    public sealed class DemoDefinition : ScriptableObject
    {
        public List<ChapterDefinition> chapters = new List<ChapterDefinition>();
        [Tooltip("仅为框架测试顺序，待正式谜题规则确定后替换。")]
        public List<string> prototypeTrueOrder = new List<string>();

        public ChapterDefinition FindSegment(string segment) => chapters.Find(c => c != null && c.segment == segment);
    }
}
