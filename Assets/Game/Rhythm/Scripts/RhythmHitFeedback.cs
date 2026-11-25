using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum RhythmFeedbackKind { Circle, Slider, Spinner, Special }

public sealed class RhythmHitFeedback : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] RhythmDemoController flowDemo;
    RhythmFlowVisual flow;
    readonly CircleApproachRing[] sparks = new CircleApproachRing[8];
    int sparkCount;
    [SerializeField] AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] float hitVolume = 0.65f;
    [SerializeField] Canvas feedbackCanvas;
    [SerializeField] RectTransform popup;
    [SerializeField] CanvasGroup popupGroup;
    [SerializeField] Image flash;
    [SerializeField] TMP_Text judgmentText;
    [SerializeField, Min(0.05f)] float displaySeconds = 0.4f;

    [SerializeField] AudioClip circleSound, perfectSound, missSound, sliderSound, spinnerSound, comboSound;
    int lastComboMilestone;

    float shownAt;
    float initialAlpha;
    float initialScale;
    bool missStyle;
    Vector2 popupHome;
    const float MissSeconds = .8f;
    float targetScale = 1.4f;
    bool showing;

    void Awake()
    {
        popupGroup.alpha = 0f;
        popupGroup.blocksRaycasts = false;
        popupGroup.interactable = false;
        flash.raycastTarget = false;
        judgmentText.raycastTarget = false;
        if (flowDemo != null)
        {
            flow = gameObject.AddComponent<RhythmFlowVisual>();
        }
        for (int i = 0; i < sparks.Length; i++)
        {
            var star = new GameObject("HitSpark_" + i, typeof(RectTransform), typeof(CanvasRenderer));
            sparks[i] = star.AddComponent<CircleApproachRing>();
            sparks[i].StarShape = true;
            sparks[i].rectTransform.SetParent(popup, false);
            sparks[i].rectTransform.sizeDelta = Vector2.one * (i % 2 == 0 ? 15f : 10f);
            sparks[i].color = Color.clear;
            sparks[i].raycastTarget = false;
        }
        if (hitSound != null) hitSound.LoadAudioData();
        foreach (AudioClip clip in new[] { circleSound, perfectSound, missSound, sliderSound, spinnerSound, comboSound })
            if (clip != null) clip.LoadAudioData();
    }

    public void Play(Judgement result, RectTransform note, RhythmFeedbackKind kind = RhythmFeedbackKind.Circle, HitObject completedNote = null)
    {
        bool success = result != Judgement.Miss;
        Color color = result == Judgement.Perfect ? new Color(1f, 0.85f, 0.3f)
            : result == Judgement.Good ? new Color(0.35f, 1f, 0.8f)
            : new Color(0.65f, 0.65f, 0.7f);

        ShowPopup(result.ToString(), color, note, success);
        if (result == Judgement.Perfect)
        {
            initialScale = 1.28f;
            targetScale = 1.65f;
            popup.localScale = Vector3.one * initialScale;
            flash.color = new Color(color.r, color.g, color.b, 0.85f);
        }
        if (success)
        {
            initialScale = kind == RhythmFeedbackKind.Special ? 1.45f : kind == RhythmFeedbackKind.Spinner ? 1.38f
                : result == Judgement.Perfect ? 1.28f : 1.16f;
            targetScale = 1f;
            popup.localScale = Vector3.one * initialScale;
            sparkCount = kind == RhythmFeedbackKind.Special || kind == RhythmFeedbackKind.Spinner ? 8
                : result == Judgement.Perfect || kind == RhythmFeedbackKind.Slider ? 6 : 4;
            PaintSparks(0f);
        }
        if (success && completedNote != null && flow != null) flow.OnSuccessfulHit(completedNote);
        AudioClip sound = !success ? missSound : kind == RhythmFeedbackKind.Slider ? sliderSound
            : kind == RhythmFeedbackKind.Spinner ? spinnerSound : circleSound;
        float volume = !success ? .30f : kind == RhythmFeedbackKind.Slider ? .50f
            : kind == RhythmFeedbackKind.Spinner ? .46f : .38f;
        PlaySound(sound != null ? sound : (success ? hitSound : null), volume);
        if (result == Judgement.Perfect) PlaySound(perfectSound, .08f);
    }

    void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volume * Mathf.Clamp01(hitVolume / .65f));
    }

    public void UpdateComboSound(int combo)
    {
        if (combo == 0) { lastComboMilestone = 0; return; }
        if (combo % 10 != 0 || combo == lastComboMilestone) return;
        lastComboMilestone = combo;
        PlaySound(comboSound, .22f);
    }

    public void PlayEarly(RectTransform note)
    {
        ShowPopup("Early", new Color(1f, 0.65f, 0.25f), note, false);
    }

    void ShowPopup(string label, Color color, RectTransform note, bool strong)
    {
        if (note != null)
        {
            Canvas noteCanvas = note.GetComponentInParent<Canvas>();
            Camera noteCamera = noteCanvas != null && noteCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? noteCanvas.worldCamera : null;
            Camera feedbackCamera = feedbackCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : feedbackCanvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(noteCamera, note.TransformPoint(note.rect.center));
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)popup.parent, screen, feedbackCamera, out local))
                popup.anchoredPosition = local;
        }
        popupHome = popup.anchoredPosition;
        popup.localRotation = Quaternion.identity;
        missStyle = label == "Miss";

        sparkCount = 0;
        foreach (var spark in sparks) if (spark != null) spark.color = Color.clear;
        judgmentText.text = label;
        judgmentText.color = color;
        flash.color = new Color(color.r, color.g, color.b, strong ? 0.65f : 0.2f);
        initialAlpha = strong ? 1f : 0.5f;
        initialScale = strong ? 1.15f : 0.85f;
        targetScale = 1.4f;
        popupGroup.alpha = initialAlpha;
        popup.localScale = Vector3.one * initialScale;
        shownAt = Time.unscaledTime;
        showing = true;
    }

    void Update()
    {
        if (!showing) return;
        if (missStyle)
        {
            float age = Time.unscaledTime - shownAt, k = Mathf.Clamp01(age / MissSeconds), inQuint = k * k * k * k * k;
            popupGroup.alpha = 1f - k;
            popup.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(age / .1f));
            popup.anchoredPosition = popupHome + Vector2.down * 100f * inQuint;
            popup.localRotation = Quaternion.Euler(0, 0, -40f * inQuint);
            if (k >= 1f) { showing = false; popup.anchoredPosition = popupHome; popup.localRotation = Quaternion.identity; }
            return;
        }
        float progress = Mathf.Clamp01((Time.unscaledTime - shownAt) / Mathf.Max(0.05f, displaySeconds));
        popupGroup.alpha = initialAlpha * (1f - progress);
        popup.localScale = Vector3.one * Mathf.Lerp(initialScale, targetScale, 1f - Mathf.Pow(1f - progress, 3f));
        PaintSparks(progress);
        if (progress >= 1f) showing = false;
    }
    void PaintSparks(float progress)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] == null) continue;
            float angle = i * Mathf.PI * 2f / Mathf.Max(1, sparkCount) + .35f;
            float radius = Mathf.Lerp(48f, 112f, 1f - Mathf.Pow(1f - progress, 2f));
            sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            sparks[i].color = new Color(1f, .88f, .52f, i < sparkCount ? (1f - progress) * .85f : 0f);
        }
    }

    public void ConfigureFlow()
    {
        if (flow != null) flow.Initialize(flowDemo);
    }

    public void ResetRun()
    {
        if (audioSource != null) audioSource.Stop();
        showing = false;
        shownAt = 0;
        lastComboMilestone = sparkCount = 0;
        if (popupGroup != null) popupGroup.alpha = 0;
        if (flash != null) flash.color = Color.clear;
        foreach (var spark in sparks) if (spark != null) spark.color = Color.clear;
        if (flow != null) flow.ResetRun();
    }

    void OnDisable() => ResetRun();
}
