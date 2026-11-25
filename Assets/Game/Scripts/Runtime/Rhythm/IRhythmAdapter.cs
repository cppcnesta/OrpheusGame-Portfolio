using System;
using System.Collections;

namespace Orpheus
{
    public interface IRhythmAdapter
    {
        IEnumerator Play(ChapterDefinition chapter, bool retry, Action<bool> completed);

        void Cancel();
    }
}
