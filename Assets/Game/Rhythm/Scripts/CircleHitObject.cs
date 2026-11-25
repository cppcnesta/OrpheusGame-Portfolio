using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CircleHitObject : HitObject
{
    static readonly List<CircleHitObject> live = new List<CircleHitObject>();
    static int pressHandledFrame = -1;
    const float ApproachRingStartScale = 3.2f, ApproachRingAlpha = .9f, ApproachRingFadeIn = .8f;

    [SerializeField, Tooltip("Stable presentation sprite for this note; does not affect judgment.")]
    Sprite displaySprite;
    [SerializeField] bool triggerPaperEffect = true;
    [SerializeField, TextArea] string rumorText = "听说，歌声能唤回记忆。";
    [SerializeField, Min(1f)] float approachStartScale = 1.8f;
    [SerializeField, Min(1f)] float approachRingWidth = 5f;

    CircleApproachRing approachRing;
    Vector3 restingScale;

    protected override void Awake()
    {
        base.Awake();
        restingScale = transform.localScale;

        if (displaySprite != null)
        {
            Image circleImage = GetComponent<Image>();
            if (circleImage != null)
            {
                circleImage.sprite = displaySprite;
                circleImage.preserveAspect = true;
            }
        }

        var ringObject = new GameObject("ApproachRing", typeof(RectTransform), typeof(CanvasRenderer));
        var ringRect = (RectTransform)ringObject.transform;
        ringRect.SetParent(transform, false);
        ringRect.SetAsFirstSibling();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = Vector2.zero;
        ringRect.offsetMax = Vector2.zero;
        ringRect.pivot = new Vector2(0.5f, 0.5f);

        approachRing = ringObject.AddComponent<CircleApproachRing>();
        approachRing.LineWidth = approachRingWidth;
        approachRing.raycastTarget = false;
        approachRing.enabled = false;
    }

    void OnEnable() => live.Add(this);
    void OnDisable() => live.Remove(this);

    bool Hittable => !judged && clock != null && clock.IsRunning && !clock.IsPaused
        && clock.songTime >= hitTime - approachTime && clock.songTime <= hitTime + goodWindow;

    bool UnderPointer()
    {
        var rect = (RectTransform)transform;
        return RhythmInput.LocalPointer(rect).magnitude <= rect.rect.width * .5f;
    }

    static void ResolvePress()
    {
        CircleHitObject target = null;
        foreach (var circle in live)
            if (circle.Hittable && circle.UnderPointer() && (target == null || circle.hitTime < target.hitTime)) target = circle;
        if (target != null) target.Press();
    }

    void Press()
    {
        if (clock.songTime < hitTime - goodWindow) { ShowEarlyFeedback(); return; }
        TryJudge();
    }

    protected override void Update()
    {
        base.Update();
        if (approachRing == null) return;

        bool approaching = !judged && clock != null && clock.IsRunning
            && clock.songTime >= hitTime - System.Math.Max(0.0, approachTime)
            && clock.songTime <= hitTime + goodWindow;
        approachRing.enabled = approaching;
        if (RhythmInput.PressedThisFrame && pressHandledFrame != Time.frameCount)
        {
            pressHandledFrame = Time.frameCount;
            ResolvePress();
        }
        if (!approaching || judged) return;

        float remaining = approachTime > 0.0
            ? Mathf.Clamp01((float)((hitTime - clock.songTime) / approachTime)) : 0f;
        float scale = Mathf.Lerp(1f, Mathf.Max(ApproachRingStartScale, approachStartScale), remaining);
        float entrance = approachTime > 0.0 ? Mathf.Clamp01((float)(
            (clock.songTime - (hitTime - approachTime)) / System.Math.Min(.16, approachTime))) : 1f;
        float bodyScale = Mathf.Lerp(.88f, 1f, 1f - Mathf.Pow(1f - entrance, 3f));
        transform.localScale = restingScale * bodyScale;
        approachRing.rectTransform.localScale = Vector3.one * (scale / bodyScale);
        approachRing.color = System.Math.Abs(clock.songTime - hitTime) <= perfectWindow
            ? Color.white : new Color(1f, 0.8f, 0.25f, 0.9f);
        approachRing.color = Color.Lerp(approachRing.color, new Color(1f, .96f, .80f, 1f), IncomingFlow * .65f);
        float ringIn = Mathf.Clamp01((float)((clock.songTime - (hitTime - approachTime)) / System.Math.Min(ApproachRingFadeIn, approachTime)));
        float ringOut = 1f - Mathf.Clamp01((float)((clock.songTime - hitTime) / .05));
        var c = approachRing.color; c.a *= ApproachRingAlpha * ringIn * ringOut; approachRing.color = c;
    }

    public void ConfigureCircle(Sprite sprite, bool paper, string text)
    {
        displaySprite = sprite;
        triggerPaperEffect = paper;
        rumorText = text;
    }

    public override void ResetRun()
    {
        base.ResetRun();
        restingScale = transform.localScale;
        if (approachRing != null)
        {
            approachRing.enabled = false;
            approachRing.rectTransform.localScale = Vector3.one;
        }
    }

    protected override void OnSuccessfulHit(Judgement result)
    {
        if (triggerPaperEffect && gameManager != null && gameManager.CanPlayRumorPaper)
            gameManager.PlayPaper(rumorText);
    }
}
