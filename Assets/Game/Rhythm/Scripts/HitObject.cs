
using System.Collections;
using UnityEngine;

public enum Judgement
{
    Perfect,
    Good,
    Miss
}

public abstract class HitObject : MonoBehaviour
{
    public RhythmClock clock;
    public RhythmGameManager gameManager;

    public double hitTime = 3.0;
    [Tooltip("Seconds before hitTime that this note becomes visible (shared song clock).")]
    public double approachTime = 1.0;

    public double perfectWindow = 0.10;
    public double goodWindow = 0.25;

    const float FadeInSeconds = .4f, HitExplodeSeconds = .4f, HitExplodeScale = 1.5f, MissFadeSeconds = .1f;

    [SerializeField] RhythmHitFeedback hitFeedback;
    [SerializeField] RhythmEmergenceVisual emergenceVisual;

    public virtual Vector3 FlowEntryPosition => transform.position;
    public virtual Vector3 FlowExitPosition => transform.position;
    public virtual double FlowEndTime => hitTime + goodWindow;
    double receivedFlowAt = -100.0;
    float receivedFlowStrength;
    public float IncomingFlow => clock == null ? 0f : receivedFlowStrength *
        Mathf.Pow(1f - Mathf.Clamp01((float)((clock.songTime - receivedFlowAt) / .22)), 2f);
    public void ReceiveFlow(float strength)
    {
        if (judged || clock == null) return;
        receivedFlowAt = clock.songTime;
        receivedFlowStrength = Mathf.Clamp(strength, 0f, 1.6f);
    }

    public void Configure(RhythmClock songClock, RhythmGameManager manager,
        RhythmHitFeedback feedback, RhythmEmergenceVisual emergence)
    {
        clock = songClock;
        gameManager = manager;
        hitFeedback = feedback;
        emergenceVisual = emergence;
    }

    Vector3 initialScale;
    Quaternion initialRotation;
    Vector2 initialPosition;
    bool poseCaptured;
    public void CapturePose()
    {
        initialScale = transform.localScale;
        initialRotation = transform.localRotation;
        initialPosition = ((RectTransform)transform).anchoredPosition;
        poseCaptured = true;
    }

    public virtual void ResetRun()
    {
        StopAllCoroutines();
        shaking = false;
        if (poseCaptured)
        {
            transform.localScale = initialScale;
            transform.localRotation = initialRotation;
            ((RectTransform)transform).anchoredPosition = initialPosition;
        }
        judged = false;
        LastJudgement = null;
        receivedFlowAt = -100.0;
        receivedFlowStrength = 0;
        Hide();
    }

    protected bool judged = false;
    public bool IsJudged => judged;
    public Judgement? LastJudgement { get; private set; }
    protected virtual int ScoreMultiplier => 1;

    public void ResolveMissAtEnd()
    {
        if (!judged) Finish(Judgement.Miss);
    }

    private CanvasGroup canvasGroup;

    protected virtual void Awake()
    {
        if (!poseCaptured) CapturePose();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Hide();
    }

    protected virtual void Update()
    {
        if (judged || clock == null || !clock.IsRunning || clock.IsPaused)
        {
            return;
        }

        double currentTime = clock.songTime;

        if (currentTime >= hitTime - approachTime &&
            currentTime <= hitTime + goodWindow)
        {
            Show();
        }

        if (currentTime > hitTime + goodWindow)
        {
            Finish(Judgement.Miss);
        }
    }

    protected void TryJudge()
    {
        if (judged || clock == null || !clock.IsRunning || clock.IsPaused)
        {
            return;
        }

        double difference =
            System.Math.Abs(clock.songTime - hitTime);

        if (difference <= perfectWindow)
        {
            Finish(Judgement.Perfect);
        }
        else if (difference <= goodWindow)
        {
            Finish(Judgement.Good);
        }
    }

    protected void ShowEarlyFeedback()
    {
        if (hitFeedback != null)
            hitFeedback.PlayEarly(transform as RectTransform);
        if (!shaking && isActiveAndEnabled) StartCoroutine(Shake());
    }

    protected RhythmHitFeedback Feedback => hitFeedback;

    bool shaking;
    IEnumerator Shake()
    {
        shaking = true;
        var rect = (RectTransform)transform;
        Vector2 home = rect.anchoredPosition;
        foreach (float x in new[] { 14f, -14f, 9f, -9f, 4f, 0f })
        {
            rect.anchoredPosition = home + new Vector2(x, 0);
            yield return new WaitForSecondsRealtime(.03f);
        }
        rect.anchoredPosition = home;
        shaking = false;
    }

    protected void Finish(Judgement result)
    {
        if (judged) return;
        judged = true;
        LastJudgement = result;

        try
        {
            if (hitFeedback != null)
                hitFeedback.Play(result, transform as RectTransform,
                    this is SliderHitObject ? RhythmFeedbackKind.Slider
                    : this is SpinnerHitObject ? RhythmFeedbackKind.Spinner
                    : this is SpecialNoteHitObject ? RhythmFeedbackKind.Special : RhythmFeedbackKind.Circle, this);
            if (result != Judgement.Miss)
            {
                OnSuccessfulHit(result);
                if (emergenceVisual != null)
                    emergenceVisual.RecordSuccess(transform as RectTransform,
                        this is SpecialNoteHitObject, this is SpinnerHitObject, result == Judgement.Perfect);
            }
        }
        finally
        {
            Exit(result);
            if (gameManager != null)
            {
                gameManager.RegisterJudgement(result, ScoreMultiplier);
                if (hitFeedback != null) hitFeedback.UpdateComboSound(gameManager.combo);
            }
            Debug.Log(gameObject.name + ": " + result);
        }
    }

    protected virtual void OnSuccessfulHit(Judgement result) { }

    protected void Show()
    {
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        float appear = clock != null ? (float)((clock.songTime - (hitTime - approachTime)) / FadeInSeconds) : 1f;
        canvasGroup.alpha = Mathf.Clamp01(appear);
    }

    void Exit(Judgement result)
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (!isActiveAndEnabled || canvasGroup.alpha <= 0f) { Hide(); return; }
        StartCoroutine(ExitRoutine(result != Judgement.Miss));
    }

    IEnumerator ExitRoutine(bool hit)
    {
        Vector3 from = transform.localScale;
        float alpha = canvasGroup.alpha, seconds = hit ? HitExplodeSeconds : MissFadeSeconds;
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / seconds)
        {
            float ease = 1f - (1f - t) * (1f - t);
            transform.localScale = from * (hit ? Mathf.Lerp(1f, HitExplodeScale, ease) : Mathf.Lerp(1f, .94f, t));
            canvasGroup.alpha = alpha * (1f - t);
            yield return null;
        }
        transform.localScale = from;
        Hide();
    }

    protected void Hide()
    {
        if (canvasGroup == null) return;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
    }
}