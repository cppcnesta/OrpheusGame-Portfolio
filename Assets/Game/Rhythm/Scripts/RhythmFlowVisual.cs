using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class RhythmFlowVisual : MonoBehaviour
{
    public const double MaximumHandoffGap = 1.5;
    const int CometCount = 3, TailCount = 6, BurstCount = 3, FragmentCount = 6;
    sealed class Comet
    {
        public HitObject target;
        public Vector2 from, bend;
        public double start;
        public float duration, strength;
        public bool active;
        public Image halo;
        public CircleApproachRing head;
        public readonly Image[] tail = new Image[TailCount];
    }
    sealed class Burst
    {
        public HitObject follow;
        public Vector2 position;
        public double start;
        public float strength, radius;
        public bool active, release;
        public Image halo;
        public CircleApproachRing ring;
        public readonly CircleApproachRing[] stars = new CircleApproachRing[FragmentCount];
    }
    sealed class SliderStream
    {
        public SliderHitObject note;
        public readonly Image[] lights = new Image[4];
    }
    sealed class SpinnerCharge
    {
        public SpinnerHitObject note;
        public Image halo;
        public readonly CircleApproachRing[] orbit = new CircleApproachRing[3];
    }

    readonly List<HitObject> chart = new List<HitObject>();
    readonly List<SliderStream> sliders = new List<SliderStream>();
    readonly List<SpinnerCharge> spinners = new List<SpinnerCharge>();
    readonly Comet[] comets = new Comet[CometCount];
    readonly Burst[] bursts = new Burst[BurstCount];
    RhythmDemoController demo;
    RhythmClock clock;
    RectTransform root;
    Texture2D glowTexture;
    Sprite glow;
    int nextComet, nextBurst;
    bool cleared;
    double SongTime => clock != null ? clock.songTime : 0.0;

    public void Initialize(RhythmDemoController owner)
    {
        bool same = demo == owner && chart.Count == (owner != null ? owner.FlowNotes.Count : 0);
        if (same)
            for (int i = 0; i < chart.Count; i++) if (!System.Linq.Enumerable.Contains(owner.FlowNotes, chart[i])) { same = false; break; }
        if (root != null && same) { chart.Sort((a, b) => a.hitTime.CompareTo(b.hitTime)); ResetRun(); return; }
        if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
        if (glow != null) Destroy(glow);
        if (glowTexture != null) Destroy(glowTexture);
        chart.Clear(); sliders.Clear(); spinners.Clear();
        demo = owner;
        if (owner == null || owner.FlowNotes == null) return;
        foreach (HitObject note in owner.FlowNotes) if (note != null) chart.Add(note);
        chart.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        if (chart.Count == 0) return;
        clock = chart[0].clock;
        Canvas canvas = chart[0].GetComponentInParent<Canvas>();
        if (canvas == null) return;
        root = (RectTransform)new GameObject("RhythmPhraseFlow", typeof(RectTransform), typeof(CanvasGroup)).transform;
        root.SetParent(canvas.transform, false);
        root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
        root.sizeDelta = Vector2.zero;
        root.SetSiblingIndex(1);
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        BuildGlow();
        for (int i = 0; i < comets.Length; i++)
        {
            var comet = new Comet { halo = Light("CometHalo"), head = Star("CometCore") };
            for (int j = 0; j < TailCount; j++) comet.tail[j] = Light("CometDust");
            comets[i] = comet;
        }
        for (int i = 0; i < bursts.Length; i++)
        {
            var burst = new Burst { halo = Light("FlowCatchHalo"), ring = Star("FlowCatchRing", false) };
            burst.ring.LineWidth = 4f;
            for (int j = 0; j < FragmentCount; j++) burst.stars[j] = Star("FlowFragment");
            bursts[i] = burst;
        }
        foreach (HitObject note in chart)
        {
            if (note is SliderHitObject slider)
            {
                var stream = new SliderStream { note = slider };
                for (int i = 0; i < stream.lights.Length; i++) stream.lights[i] = Light("SliderEnergy");
                sliders.Add(stream);
            }
            if (note is SpinnerHitObject spinner)
            {
                var charge = new SpinnerCharge { note = spinner, halo = Light("SpinnerStoredEnergy") };
                for (int i = 0; i < charge.orbit.Length; i++) charge.orbit[i] = Star("SpinnerOrbit");
                spinners.Add(charge);
            }
        }
    }

    public static HitObject FindNext(IReadOnlyList<HitObject> ordered, HitObject source, double now)
    {
        if (ordered == null || source == null) return null;
        foreach (HitObject note in ordered)
            if (note != null && note != source && note.hitTime > source.hitTime && !note.IsJudged
                && note.isActiveAndEnabled && now <= note.FlowEndTime)
                return note;
        return null;
    }

    public void OnSuccessfulHit(HitObject source)
    {
        if (root == null || source == null || clock == null || clock.IsPaused || (demo != null && demo.Ended)) return;
        double now = SongTime;
        bool spinner = source is SpinnerHitObject, special = source is SpecialNoteHitObject;
        bool milestone = source.gameManager != null && (source.gameManager.combo + 1) % 10 == 0;
        float strength = special ? 1.5f : spinner ? 1.25f : milestone ? 1.15f : 1f;
        if (spinner || special) EmitBurst(source.FlowExitPosition, null, strength, spinner ? 520f : 480f, true);
        else if (source is SliderHitObject) EmitBurst(source.FlowExitPosition, null, strength, 110f, false);
        HitObject target = FindNext(chart, source, now);
        double gap = target != null ? target.hitTime - now : double.PositiveInfinity;
        if (gap < .10 || gap > MaximumHandoffGap) return;
        if (target is SpecialNoteHitObject) strength *= 1.2f;
        float duration = target is SpinnerHitObject ? .30f : spinner ? .30f
            : source is SliderHitObject ? .27f : target is SliderHitObject ? .25f : .20f;
        float gather = source is SliderHitObject ? .04f : 0f;
        duration = Mathf.Min(.35f, Mathf.Min(duration + (special ? .035f : 0f), ((float)gap - gather) * .70f));
        var comet = comets[nextComet++ % CometCount];
        comet.target = target;
        comet.from = Local(source.FlowExitPosition);
        Vector2 to = Local(target.FlowEntryPosition), delta = to - comet.from;
        float distance = delta.magnitude;
        float curve = target is SpinnerHitObject ? .25f : source is SliderHitObject ? .12f
            : target is SliderHitObject ? .10f : .16f;
        float side = target is SpinnerHitObject || spinner ? 1f : delta.x >= 0f ? 1f : -1f;
        comet.bend = distance > .01f ? new Vector2(-delta.y, delta.x) / distance * Mathf.Min(180f, distance * curve) * side : Vector2.zero;
        comet.duration = duration;
        comet.start = System.Math.Max(now + gather, target.hitTime - target.approachTime - duration);
        comet.strength = Mathf.Min(1.6f, strength);
        comet.active = true;
        cleared = false;
        if ((spinner || special || milestone) && demo != null && demo.FlowConstellation != null)
            demo.FlowConstellation.PulseFlow(source.FlowExitPosition, strength * .6f);
        PaintComet(comet, now);
    }

    static Vector2 Arc(Vector2 from, Vector2 to, Vector2 bend, float t)
    {
        t = Mathf.Clamp01(t);
        Vector2 control = (from + to) * .5f + bend;
        return from * ((1f - t) * (1f - t)) + control * (2f * (1f - t) * t) + to * (t * t);
    }

    void LateUpdate()
    {
        if (root == null || clock == null || !clock.IsRunning) return;
        if (demo != null && demo.Ended) { Clear(); return; }
        if (clock.IsPaused) return;
        cleared = false;
        double now = SongTime;
        foreach (Comet comet in comets) PaintComet(comet, now);
        foreach (Burst burst in bursts) PaintBurst(burst, now);
        foreach (SliderStream stream in sliders) PaintSlider(stream);
        foreach (SpinnerCharge charge in spinners) PaintSpinner(charge);
    }

    void PaintComet(Comet comet, double now)
    {
        if (!comet.active) { Hide(comet); return; }
        if (comet.target == null || comet.target.IsJudged || !comet.target.isActiveAndEnabled)
        { comet.active = false; Hide(comet); return; }
        float t = (float)((now - comet.start) / comet.duration);
        if (t < 0f) { Hide(comet); return; }
        if (t >= 1f)
        {
            comet.active = false;
            Hide(comet);
            comet.target.ReceiveFlow(comet.strength);
            EmitBurst(comet.target.FlowEntryPosition, comet.target, comet.strength, 110f, false);
            return;
        }
        Vector2 to = Local(comet.target.FlowEntryPosition);
        float eased = t * t * (3f - 2f * t);
        Vector2 point = Arc(comet.from, to, comet.bend, eased);
        float alpha = Mathf.Lerp(.30f, .70f, Mathf.Sin(Mathf.PI * t))
            * (1f - Mathf.SmoothStep(.85f, 1f, t)) * comet.strength;
        Place(comet.halo, point, 55f, alpha * .65f);
        Place(comet.head, point, 19f, alpha);
        for (int i = 0; i < TailCount; i++)
        {
            float behind = Mathf.Clamp01(eased - (i + 1) * .055f);
            Place(comet.tail[i], Arc(comet.from, to, comet.bend, behind), 30f - i * 3f,
                alpha * (1f - (i + 1f) / (TailCount + 1f)) * .36f);
        }
    }

    void EmitBurst(Vector3 world, HitObject follow, float strength, float radius, bool release)
    {
        Burst burst = bursts[nextBurst++ % BurstCount];
        burst.position = Local(world);
        burst.follow = follow;
        burst.start = SongTime;
        burst.strength = strength;
        burst.radius = radius;
        burst.release = release;
        burst.active = true;
        cleared = false;
        PaintBurst(burst, SongTime);
    }

    void PaintBurst(Burst burst, double now)
    {
        if (!burst.active) { Hide(burst); return; }
        float t = Mathf.Clamp01((float)((now - burst.start) / (burst.release ? .32 : .22)));
        if (t >= 1f || (burst.follow != null && (burst.follow.IsJudged || !burst.follow.isActiveAndEnabled)))
        { burst.active = false; Hide(burst); return; }
        Vector2 center = burst.follow != null ? Local(burst.follow.FlowEntryPosition) : burst.position;
        float alpha = (1f - t) * burst.strength;
        float radius = burst.radius * (burst.release ? .7f + .65f * t : .65f + .12f * Mathf.Sin(t * Mathf.PI));
        Place(burst.halo, center, radius * 1.5f, alpha * .14f);
        Place(burst.ring, center, radius, alpha * (burst.release ? .45f : .25f));
        for (int i = 0; i < FragmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / FragmentCount + .3f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (.40f + t * .30f);
            Place(burst.stars[i], center + offset, burst.release ? 14f : 8f,
                alpha * (burst.release ? .65f : i < 2 ? .30f : 0f));
        }
    }

    void PaintSlider(SliderStream stream)
    {
        SliderHitObject note = stream.note;
        bool tracking = note != null && note.isActiveAndEnabled && note.IsFlowTracking;
        float progress = tracking ? note.FlowProgress : 0f;
        for (int i = 0; i < stream.lights.Length; i++)
        {
            float p = Mathf.Clamp01(progress - i * .065f);
            Vector3 world = note != null ? Vector3.Lerp(note.FlowTrackStart, note.FlowExitPosition, p) : Vector3.zero;
            Place(stream.lights[i], Local(world), i == 0 ? 54f : 32f - i * 4f,
                tracking ? (i == 0 ? .36f : .20f / i) : 0f);
        }
    }

    void PaintSpinner(SpinnerCharge charge)
    {
        SpinnerHitObject note = charge.note;
        bool live = note != null && note.isActiveAndEnabled && !note.IsJudged && SongTime >= note.hitTime - note.approachTime && SongTime <= note.endTime;
        float energy = live ? note.FlowCharge : 0f, incoming = live ? note.IncomingFlow : 0f;
        Vector2 center = note != null ? Local(note.FlowEntryPosition) : Vector2.zero;
        Place(charge.halo, center, 330f + energy * 80f, energy * .16f + incoming * .08f);
        for (int i = 0; i < charge.orbit.Length; i++)
        {
            float angle = (note != null ? note.FlowAngle : 0f) / Mathf.Rad2Deg + i * Mathf.PI * 2f / 3f + incoming * .18f;
            Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (200f + energy * 22f);
            Place(charge.orbit[i], position, 10f + energy * 6f, energy * .45f + incoming * .15f);
        }
    }

    Vector2 Local(Vector3 world) => root != null ? (Vector2)root.InverseTransformPoint(world) : Vector2.zero;
    static void Place(Image image, Vector2 position, float size, float alpha)
    {
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = Vector2.one * size;
        image.color = new Color(1f, .91f, .69f, Mathf.Clamp01(alpha));
    }
    static void Hide(Comet c)
    {
        c.head.color = c.halo.color = Color.clear;
        foreach (Image tail in c.tail) tail.color = Color.clear;
    }
    static void Hide(Burst b)
    {
        b.halo.color = b.ring.color = Color.clear;
        foreach (Image star in b.stars) star.color = Color.clear;
    }
    void Clear()
    {
        if (cleared) return;
        foreach (Comet c in comets) if (c != null) { c.active = false; Hide(c); }
        foreach (Burst b in bursts) if (b != null) { b.active = false; Hide(b); }
        foreach (SliderStream s in sliders) foreach (Image i in s.lights) i.color = Color.clear;
        foreach (SpinnerCharge s in spinners) { s.halo.color = Color.clear; foreach (Image i in s.orbit) i.color = Color.clear; }
        cleared = true;
    }
    void BuildGlow()
    {
        glowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        glowTexture.hideFlags = HideFlags.HideAndDontSave;
        glowTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float dx = (x + .5f - 16f) / 16f, dy = (y + .5f - 16f) / 16f;
            pixels[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2f));
        }
        glowTexture.SetPixels(pixels);
        glowTexture.Apply(false, true);
        glow = Sprite.Create(glowTexture, new Rect(0, 0, 32, 32), Vector2.one * .5f);
        glow.hideFlags = HideFlags.HideAndDontSave;
    }
    Image Light(string name)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(root, false);
        image.sprite = glow;
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }
    CircleApproachRing Star(string name, bool pointed = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var image = go.AddComponent<CircleApproachRing>();
        image.rectTransform.SetParent(root, false);
        image.StarShape = pointed;
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }
    public void ResetRun()
    {
        cleared = false;
        Clear();
        nextComet = nextBurst = 0;
        foreach (Comet c in comets) if (c != null) c.target = null;
        foreach (Burst b in bursts) if (b != null) b.follow = null;
        cleared = false;
    }
    void OnDisable() => ResetRun();
    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        if (glow != null) Destroy(glow);
        if (glowTexture != null) Destroy(glowTexture);
    }
}
