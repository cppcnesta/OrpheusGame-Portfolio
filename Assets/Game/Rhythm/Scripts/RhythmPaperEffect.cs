using Orpheus;
using UnityEngine;

public sealed class RhythmPaperEffect : MonoBehaviour
{
    [SerializeField] PaperSpawner paperSpawner;
    public void Play(string text)
    {
        if (paperSpawner != null) paperSpawner.Drop(text);
    }
    public void Configure(int act)
    {
        if (paperSpawner == null) return;
        paperSpawner.SetAct(act);
    }
    public void ResetRun()
    {
        StopAllCoroutines();
        if (paperSpawner != null)
        {
            paperSpawner.Clear();
            paperSpawner.RestoreWritten(System.Array.Empty<string>());
            paperSpawner.SetPileVisible(true, 0);
        }
    }
}
