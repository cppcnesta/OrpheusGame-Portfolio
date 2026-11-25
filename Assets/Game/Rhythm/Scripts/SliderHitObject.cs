using UnityEngine;
using UnityEngine.EventSystems;

public class SliderHitObject : HitObject,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    public RectTransform knob;

    Orpheus.VN.DialogueView dialogueView;
    Orpheus.VN.CharacterData dialogueCharacter;
    string dialogueSpeaker, dialogueText;

    public double endTime = 11.0;

    public float dragSensitivity = 0.8f;

    public float smoothTime = 0.08f;

    private RectTransform track;
    public override Vector3 FlowEntryPosition => knob != null ? knob.position : transform.position;
    public override Vector3 FlowExitPosition => track != null ? track.TransformPoint(new Vector2(endX, 0f)) : transform.position;
    public Vector3 FlowTrackStart => track != null ? track.TransformPoint(new Vector2(startX, 0f)) : transform.position;
    public override double FlowEndTime => endTime;
    public bool IsFlowTracking => dragging && !judged && clock != null && !clock.IsPaused && knob != null
        && Mathf.Abs(targetX - knob.anchoredPosition.x) < knob.rect.width * .4f;
    public float FlowProgress => GetTargetProgress();
    UnityEngine.UI.Image trackImage;
    Color trackColor;
    UnityEngine.UI.Image handleImage;
    Color handleColor;
    Vector3 handleScale;
    CircleApproachRing trackingGlow;

    void PaintTracking()
    {
        if (knob == null || trackingGlow == null) return;
        bool tracking = IsFlowTracking;
        float pulse = tracking ? .5f + .5f * Mathf.Sin((float)clock.songTime * 16f) : 0f;
        knob.localScale = handleScale * (tracking ? 1.025f + .035f * pulse : 1f);
        if (handleImage != null) handleImage.color = Color.Lerp(handleColor, new Color(1f, .91f, .64f, handleColor.a), tracking ? .20f : 0f);
        float incoming = IncomingFlow;
        trackingGlow.color = new Color(1f, .85f, .38f, Mathf.Max(tracking ? .35f + .25f * pulse : 0f, incoming * .55f));
        if (trackImage != null) trackImage.color = Color.Lerp(trackColor, new Color(1f, .94f, .76f, trackColor.a),
            tracking ? .08f + FlowProgress * .08f : incoming * .08f);
    }

    private bool dragging = false;

    private float startX;
    private float endX;

    private float pointerStartX;
    private float knobStartX;

    private float targetX;
    private float smoothVelocity;

    protected override void Awake()
    {
        base.Awake();

        track = GetComponent<RectTransform>();
        trackImage = GetComponent<UnityEngine.UI.Image>();
        if (trackImage != null) trackColor = trackImage.color;

        if (knob != null)
        {
            float halfKnob = knob.rect.width / 2f;

            startX = track.rect.xMin + halfKnob;
            endX = track.rect.xMax - halfKnob;

            Vector2 position = knob.anchoredPosition;

            position.x = startX;
            position.y = 0;

            knob.anchoredPosition = position;

            targetX = startX;
            handleImage = knob.GetComponent<UnityEngine.UI.Image>();
            if (handleImage != null) handleColor = handleImage.color;
            handleScale = knob.localScale;
            var glow = new GameObject("SliderTrackingGlow", typeof(RectTransform), typeof(CanvasRenderer));
            trackingGlow = glow.AddComponent<CircleApproachRing>();
            trackingGlow.rectTransform.SetParent(knob, false);
            trackingGlow.rectTransform.sizeDelta = knob.rect.size * 1.06f;
            trackingGlow.LineWidth = 6f;
            trackingGlow.raycastTarget = false;
            trackingGlow.color = Color.clear;
        }
    }

    public void ConfigureDialogue(Orpheus.VN.DialogueView view, Orpheus.VN.CharacterData character, string speaker, string text)
    {
        dialogueView = view;
        dialogueCharacter = character;
        dialogueSpeaker = speaker;
        dialogueText = text;
    }

    public override void ResetRun()
    {
        base.ResetRun();
        dragging = false;
        pointerStartX = knobStartX = smoothVelocity = 0;
        if (knob != null)
        {
            startX = track.rect.xMin + knob.rect.width / 2f;
            endX = track.rect.xMax - knob.rect.width / 2f;
            targetX = startX;
            knob.anchoredPosition = new Vector2(startX, 0);
            knob.localScale = handleScale;
        }
        if (handleImage != null) handleImage.color = handleColor;
        if (trackImage != null) trackImage.color = trackColor;
        if (trackingGlow != null) trackingGlow.color = Color.clear;
    }

    protected override void Update()
    {
        if (judged || clock == null || !clock.IsRunning || clock.IsPaused)
        {
            return;
        }

        double currentTime = clock.songTime;

        if (currentTime >= hitTime - approachTime &&
            currentTime <= endTime)
        {
            Show();
        }

        if (knob != null)
        {
            Vector2 position = knob.anchoredPosition;

            position.x = Mathf.SmoothDamp(
                position.x,
                targetX,
                ref smoothVelocity,
                smoothTime
            );

            knob.anchoredPosition = position;
        }

        PaintTracking();
        if (currentTime > endTime)
        {
            Finish(Judgement.Miss);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (judged || clock == null || !clock.IsRunning || clock.IsPaused || knob == null)
        {
            return;
        }

        if (clock.songTime < hitTime - approachTime ||
            clock.songTime > endTime)
        {
            return;
        }

        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            track,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        dragging = true;

        pointerStartX = localPoint.x;
        knobStartX = knob.anchoredPosition.x;
        targetX = knob.anchoredPosition.x;
        PaintTracking();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || judged || clock == null || !clock.IsRunning || clock.IsPaused)
        {
            return;
        }

        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            track,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        float mouseMovement =
            (localPoint.x - pointerStartX) * dragSensitivity;

        targetX = knobStartX + mouseMovement;

        targetX = Mathf.Clamp(
            targetX,
            startX,
            endX
        );
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!dragging || judged || clock == null || !clock.IsRunning || clock.IsPaused)
        {
            return;
        }

        dragging = false;
        PaintTracking();

        if (GetTargetProgress() >= 0.95f)
        {
            double difference =
                System.Math.Abs(clock.songTime - hitTime);

            if (difference <= perfectWindow)
            {
                Finish(Judgement.Perfect);
            }
            else
            {
                Finish(Judgement.Good);
            }
        }
        else
        {
            Finish(Judgement.Miss);
        }
    }

    protected override void OnSuccessfulHit(Judgement result)
    {
        if (dialogueView == null || string.IsNullOrEmpty(dialogueText)) return;
        dialogueView.SetInputEnabled(false);
        dialogueView.PrepareSpeaker(dialogueCharacter, "");
        dialogueView.Show(dialogueSpeaker, dialogueText);
    }

    private float GetTargetProgress()
    {
        return Mathf.InverseLerp(
            startX,
            endX,
            targetX
        );
    }
}