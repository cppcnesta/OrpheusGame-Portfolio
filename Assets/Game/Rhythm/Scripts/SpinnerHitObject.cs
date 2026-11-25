using UnityEngine;

public class SpinnerHitObject : HitObject
{
    public double endTime = 15.0;
    public float requiredRotation = 360f;

    private RectTransform spinnerRect;

    public override double FlowEndTime => endTime;
    public float FlowCharge => judged ? 0f : Mathf.Clamp01(totalRotation / Mathf.Max(1f, requiredRotation));
    public float FlowAngle => spinnerRect != null ? spinnerRect.localEulerAngles.z : 0f;
    public bool IsFlowSpinning => spinning && !judged;
    CircleApproachRing motionGlow;
    float rotationShownAt = -10f;
    float rotationStrength;

    void PaintRotation()
    {
        if (motionGlow == null) return;
        float strength = rotationStrength * (1f - Mathf.Clamp01((Time.unscaledTime - rotationShownAt) / .20f));
        float charge = IsFlowSpinning ? FlowCharge * .32f : 0f;
        motionGlow.color = new Color(.95f, .85f, .48f, Mathf.Max(strength * .70f + charge, IncomingFlow * .65f));
        motionGlow.rectTransform.localScale = Vector3.one * (1.10f + strength * .08f);
    }

    private bool spinning = false;
    private float lastPointerAngle;
    private float totalRotation = 0f;

    protected override void Awake()
    {
        base.Awake();
        spinnerRect = GetComponent<RectTransform>();
        var glow = new GameObject("SpinnerRotationGlow", typeof(RectTransform), typeof(CanvasRenderer));
        motionGlow = glow.AddComponent<CircleApproachRing>();
        motionGlow.rectTransform.SetParent(transform, false);
        motionGlow.rectTransform.sizeDelta = spinnerRect.rect.size;
        motionGlow.LineWidth = 7f;
        motionGlow.raycastTarget = false;
        motionGlow.color = Color.clear;
    }

    public override void ResetRun()
    {
        base.ResetRun();
        spinning = false;
        totalRotation = lastPointerAngle = rotationStrength = 0;
        rotationShownAt = -10f;
        if (motionGlow != null)
        {
            motionGlow.color = Color.clear;
            motionGlow.rectTransform.localScale = Vector3.one;
        }
    }

    protected override void Update()
    {
        if (judged || clock == null || !clock.IsRunning || clock.IsPaused)
            return;

        PaintRotation();
        double currentTime = clock.songTime;

        if (currentTime >= hitTime - approachTime &&
            currentTime <= endTime)
        {
            Show();
        }

        if (currentTime >= hitTime - .1 && currentTime <= endTime && RhythmInput.Held)
        {
            float angle = GetPointerAngle();
            if (spinning)
            {
                float angleChange = Mathf.DeltaAngle(lastPointerAngle, angle);
                totalRotation += Mathf.Abs(angleChange);
                rotationStrength = Mathf.Clamp01(Mathf.Abs(angleChange) / 18f);
                rotationShownAt = Time.unscaledTime;
                spinnerRect.localEulerAngles += new Vector3(0f, 0f, angleChange);
            }
            spinning = true;
            lastPointerAngle = angle;
        }
        else spinning = false;

        if (currentTime > endTime)
        {
            if (totalRotation >= requiredRotation)
            {
                Finish(Judgement.Perfect);
            }
            else if (totalRotation >= requiredRotation * 0.6f)
            {
                Finish(Judgement.Good);
            }
            else
            {
                Finish(Judgement.Miss);
            }
        }
    }

    private float GetPointerAngle()
    {
        Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(null, spinnerRect.position);
        Vector2 direction = RhythmInput.Pointer - centerScreen;
        if (direction.sqrMagnitude < 25f) return lastPointerAngle;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
}
