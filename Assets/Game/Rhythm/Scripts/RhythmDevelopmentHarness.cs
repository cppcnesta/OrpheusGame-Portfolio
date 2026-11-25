using System.Collections;
using Orpheus;
using UnityEngine;

public sealed class RhythmDevelopmentHarness : MonoBehaviour
{
    [SerializeField] RhythmChapterAdapter adapter;
    [SerializeField] ChapterDefinition chapter;
    [SerializeField] bool retry;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] bool playOnStart = true;
    Coroutine hosted;
    RhythmChapterAdapter activeAdapter;
    int generation;
    public bool IsRunning { get; private set; }

    void Start()
    {
        if (playOnStart) StartPerformance();
    }

    public void StartPerformance()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || IsRunning) return;
        if (adapter == null || chapter == null)
        {
            Debug.LogWarning("Assign an adapter and chapter before starting a rhythm test.", this);
            return;
        }

        int token = ++generation;
        activeAdapter = adapter;
        IsRunning = true;
        Coroutine started = StartCoroutine(Host(activeAdapter, chapter, retry, token));
        if (IsRunning && generation == token) hosted = started;
    }

    IEnumerator Host(RhythmChapterAdapter selectedAdapter, ChapterDefinition selectedChapter, bool selectedRetry, int token)
    {
        bool reported = false;
        IEnumerator performance = null;
        try
        {
            IRhythmAdapter contract = selectedAdapter;
            performance = contract.Play(selectedChapter, selectedRetry, passed =>
            {
                if (!IsRunning || token != generation || reported) return;
                reported = true;
                Debug.Log("Rhythm result: " + (passed ? "pass" : "fail"), this);
            });
            while (token == generation && performance.MoveNext()) yield return performance.Current;
        }
        finally
        {
            try { (performance as System.IDisposable)?.Dispose(); }
            finally
            {
                if (token == generation)
                {
                    hosted = null;
                    activeAdapter = null;
                    IsRunning = false;
                    if (selectedAdapter != null) selectedAdapter.Cancel();
                }
            }
        }
    }

    public void CancelPerformance()
    {
        ++generation;
        Coroutine previous = hosted;
        RhythmChapterAdapter previousAdapter = activeAdapter != null ? activeAdapter : adapter;
        hosted = null;
        activeAdapter = null;
        IsRunning = false;
        try { if (previousAdapter != null) previousAdapter.Cancel(); }
        finally { if (previous != null) StopCoroutine(previous); }
    }

    public void RestartPerformance()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        CancelPerformance();
        StartPerformance();
    }

    void OnDisable()
    {
        if (IsRunning || hosted != null) CancelPerformance();
    }
#endif
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(RhythmDevelopmentHarness))]
sealed class RhythmDevelopmentHarnessEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var harness = (RhythmDevelopmentHarness)target;
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.LabelField("Performance", harness.IsRunning ? "Running" : "Idle");
        using (new UnityEditor.EditorGUI.DisabledScope(!Application.isPlaying || !harness.isActiveAndEnabled))
        {
            using (new UnityEditor.EditorGUI.DisabledScope(harness.IsRunning))
                if (GUILayout.Button("Start Performance")) harness.StartPerformance();
            if (GUILayout.Button("Cancel Performance")) harness.CancelPerformance();
            if (GUILayout.Button("Restart Performance")) harness.RestartPerformance();
        }
        if (Application.isPlaying) Repaint();
    }
}
#endif
