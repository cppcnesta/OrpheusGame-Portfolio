using System;
using System.Collections;
using Orpheus;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class RhythmChapterAdapter : MonoBehaviour, IRhythmAdapter
{
    [SerializeField] RhythmDemoController controller;
    int generation;
    bool running, cancelling;
    EventSystem inputOwner;
    GameObject previousSelection;

    public IEnumerator Play(ChapterDefinition chapter, bool retry, Action<bool> completed)
    {
        Cancel();
        if (chapter == null || chapter.background == null || chapter.music == null || chapter.rhythmChart == null)
            throw new ArgumentException("Chapter requires a background, music and rhythm chart.", nameof(chapter));
        if (controller == null) throw new InvalidOperationException("Rhythm controller is not assigned.");
        int token = ++generation;
        running = true;
        inputOwner = EventSystem.current;
        previousSelection = inputOwner != null ? inputOwner.currentSelectedGameObject : null;
        if (inputOwner != null) inputOwner.SetSelectedGameObject(null);
        gameObject.SetActive(true);
        try
        {
            controller.BeginPerformance(chapter, retry);
            while (running && token == generation && !controller.ResultAcknowledged) yield return null;
            if (!running || token != generation) yield break;
            bool passed = controller.EarnedMusicNote;
            running = false;
            Cancel();
            completed?.Invoke(passed);
        }
        finally
        {
            if (token == generation && running) Cancel();
        }
    }

    public void Cancel()
    {
        if (cancelling) return;
        cancelling = true;
        try
        {
            ++generation;
            running = false;
            StopAllCoroutines();
            if (controller != null) controller.CancelPerformance();
            gameObject.SetActive(false);
            EventSystem owner = inputOwner;
            GameObject selected = previousSelection;
            inputOwner = null;
            previousSelection = null;
            if (owner != null && owner == EventSystem.current &&
                (owner.currentSelectedGameObject == null || owner.currentSelectedGameObject.transform.IsChildOf(transform)))
                owner.SetSelectedGameObject(selected);
        }
        finally { cancelling = false; }
    }

    void OnDisable() { if (running) Cancel(); }
}
