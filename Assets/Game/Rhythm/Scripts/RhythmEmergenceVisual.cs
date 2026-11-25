using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public sealed class RhythmEmergenceVisual : MonoBehaviour
{
    [SerializeField] RhythmClock clock;
    [SerializeField] Color traceColor = new Color(0.86f, 0.78f, 0.60f, 1f);
    const float PrepEnd = .35f, ConvergenceEnd = 1.15f, ExpansionEnd = 1.70f, PeakEnd = 1.90f;
    const float DispersalSeconds = .95f;
    public const float FinalRevealSeconds = 2.85f;
    public const float InventoryRevealDelay = PeakEnd + DispersalSeconds * .5f;
    const int DispersalParticleCount = 28;
    const int BackgroundStarCount = 32;

    static readonly Vector2[] LyreAnchors = {
        new Vector2(-.28f,.32f), new Vector2(-.23f,.24f), new Vector2(-.19f,.045f),
        new Vector2(-.16f,-.13f), new Vector2(-.115f,-.215f), new Vector2(0,-.245f),
        new Vector2(.115f,-.215f), new Vector2(.16f,-.13f), new Vector2(.19f,.045f),
        new Vector2(.23f,.24f), new Vector2(.28f,.32f),
        new Vector2(-.115f,.24f), new Vector2(-.0575f,.24f), new Vector2(0,.24f),
        new Vector2(.0575f,.24f), new Vector2(.115f,.24f),
        new Vector2(-.0575f,-.2375f), new Vector2(.0575f,-.2375f)
    };
    static readonly int[] UnlockAt = { 1,2,3,4,5,7,9,11,13,15,17,19,21,23,25,27,29,32 };
    static readonly int[] UnlockSlots = { 0,7,5,10,3,1,9,2,8,4,6,11,15,13,16,17,12,14 };
    static readonly int[,] LyreEdges = {
        {0,1},{1,2},{2,3},{3,4},{4,5},{5,6},{6,7},{7,8},{8,9},{9,10},
        {1,11},{11,12},{12,13},{13,14},{14,15},{15,9},
        {11,4},{12,16},{13,5},{14,17},{15,6}
    };

    sealed class Star
    {
        public int slot;
        public Vector2 position, rendered;
        public RectTransform root;
        public Image halo, core, heart;
        public float quality, size, phase;
        public Vector3 endingScale;
        public float endingCoreAlpha;
    }
    sealed class SkyStar
    {
        public Vector2 position;
        public Image core, halo;
        public float phase, brightness;
    }
    sealed class Connection
    {
        public Star from, to;
        public Image halo, core;
        public bool isString;
    }
    sealed class EndingParticle
    {
        public Vector2 direction;
        public float speed, delay, size;
        public Image halo, core;
    }
    readonly List<EndingParticle> endingParticles = new List<EndingParticle>();
    RectTransform endingOverlay;
    Texture2D endingTexture;
    Sprite endingSprite;
    Color[] endingPixels;
    Image endingOuter, endingBloom, endingHalo, endingCore, endingRays, endingCrossRays;
    CanvasGroup inventoryFade, rewardFade;
    float inventoryOriginalAlpha = 1f, rewardOriginalAlpha = 1f;
    readonly List<Star> stars = new List<Star>();
    readonly List<SkyStar> background = new List<SkyStar>();
    readonly List<Connection> connections = new List<Connection>();
    readonly Star[] slots = new Star[LyreAnchors.Length];
    readonly bool[] edgeCreated = new bool[LyreEdges.GetLength(0)];
    RectTransform root, field, skyLayer, glowLayer, connectionLayer, starLayer;
    CanvasGroup visibility;
    Texture2D glowAtlas;
    Sprite glowSprite, starSprite;
    Image ambient, bloom;
    Vector2 canvasSize, specialCenter, spinnerCenter, endingCenter;
    float specialRadius = 1f, revealStarted;
    double specialAt = double.NegativeInfinity, spinnerAt = double.NegativeInfinity;
    double resonanceAt = double.NegativeInfinity;
    Star resonatingStar;
    int successfulHits;
    bool skyReady, finalReveal, hiddenForInventory;
    public int SuccessfulHitCount => successfulHits;
    public int MainStarCount => stars.Count;
    public int VisualStage => successfulHits >= 17 ? 3 : successfulHits >= 9 ? 2 : successfulHits >= 4 ? 1 : 0;
    double VisualTime => clock != null ? clock.songTime : Time.unscaledTime;

    void Awake()
    {
        root = (RectTransform)transform;
        visibility = GetComponent<CanvasGroup>();
        visibility.blocksRaycasts = false;
        visibility.interactable = false;
        BuildGlowAtlas();
        field = Layer("CelestialField", root);
        skyLayer = Layer("BackgroundStars", field);
        glowLayer = Layer("GlowLayer", field);
        connectionLayer = Layer("LyreConnections", field);
        starLayer = Layer("LyreStars", field);
        ambient = MakeImage("QuietStarRiver", glowLayer, glowSprite);
        bloom = MakeImage("LyreResonanceBloom", glowLayer, glowSprite);
    }

    void Start()
    {
        Canvas.ForceUpdateCanvases();
        InitializeSky();
        Paint();
    }

    void InitializeSky()
    {
        if (skyReady) return;
        canvasSize = new Vector2(root.rect.width, root.rect.height);
        if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;
        skyReady = true;
        field.sizeDelta = new Vector2(canvasSize.x * .94f, canvasSize.y * .86f);
        ambient.rectTransform.sizeDelta = new Vector2(canvasSize.x * .82f, canvasSize.y * .62f);
        AddBackground(new Vector2(-.47f,.34f));
        AddBackground(new Vector2(.47f,-.34f));
        AddBackground(new Vector2(.32f,.43f));
        AddBackground(new Vector2(-.32f,-.43f));
        for (int n = 0; n < 1024 && background.Count < BackgroundStarCount; n++)
        {
            var p = new Vector2((Mathf.Repeat(.17f+n*.618034f,1f)-.5f)*.94f,
                (Mathf.Repeat(.31f+n*.754878f,1f)-.5f)*.86f);
            Vector2 point = new Vector2(p.x*canvasSize.x,p.y*canvasSize.y);
            bool clear = true;
            foreach (SkyStar star in background)
                if ((point-star.position).sqrMagnitude < canvasSize.x*canvasSize.x*.065f*.065f) { clear=false; break; }
            foreach (Vector2 anchor in LyreAnchors)
                if ((point-AnchorPosition(anchor)).sqrMagnitude < canvasSize.x*canvasSize.x*.035f*.035f) { clear=false; break; }
            if (clear) AddBackground(p);
        }
    }

    Vector2 AnchorPosition(Vector2 anchor) => new Vector2((anchor.x-.06f)*canvasSize.x,anchor.y*canvasSize.y);

    void AddBackground(Vector2 normalized)
    {
        int i = background.Count;
        var star = new SkyStar { position=new Vector2(normalized.x*canvasSize.x,normalized.y*canvasSize.y),
            phase=i*2.399963f, brightness=.055f+(i%4)*.012f,
            halo=MakeImage("SkyHalo_"+i,skyLayer,glowSprite), core=MakeImage("SkyStar_"+i,skyLayer,glowSprite) };
        star.halo.rectTransform.anchoredPosition=star.core.rectTransform.anchoredPosition=star.position;
        star.halo.rectTransform.sizeDelta=Vector2.one*(24f+(i%3)*6f);
        star.core.rectTransform.sizeDelta=Vector2.one*(5f+(i%3)*1.5f);
        background.Add(star);
    }

    void BuildGlowAtlas()
    {
        const int size = 64;
        glowAtlas = new Texture2D(size * 2, size, TextureFormat.RGBA32, false);
        glowAtlas.name = "RuntimeConstellationGlow";
        glowAtlas.hideFlags = HideFlags.HideAndDontSave;
        glowAtlas.wrapMode = TextureWrapMode.Clamp;
        glowAtlas.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size * 2];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
            float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            float radial = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2.4f);
            float diamond = Mathf.Pow(Mathf.Abs(dx), 0.55f) + Mathf.Pow(Mathf.Abs(dy), 0.55f);
            pixels[y * size * 2 + x] = new Color(1f, 1f, 1f, radial);
            pixels[y * size * 2 + size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - diamond) * 7f));
        }
        glowAtlas.SetPixels(pixels);
        glowAtlas.Apply(false, true);
        glowSprite = Sprite.Create(glowAtlas, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        starSprite = Sprite.Create(glowAtlas, new Rect(size, 0, size, size), Vector2.one * 0.5f);
        glowSprite.hideFlags = starSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    RectTransform Layer(string label, RectTransform parent)
    {
        var rect = (RectTransform)new GameObject(label, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    Image MakeImage(string label, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = Vector2.one * 0.5f;
        image.sprite = sprite;
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }

    Vector2 NotePosition(RectTransform note) => root.InverseTransformPoint(note.TransformPoint(note.rect.center));

    Star Awaken(int slot, bool perfect)
    {
        RectTransform starRoot = Layer("LyreStar_"+slot,starLayer);
        var star = new Star { slot=slot, position=AnchorPosition(LyreAnchors[slot]), root=starRoot,
            quality=perfect?1.12f:1f, size=(slot==0?1.25f:.9f+(slot%4)*.07f), phase=slot*2.399963f,
            halo=MakeImage("Halo",starRoot,glowSprite), core=MakeImage("FourPointStar",starRoot,starSprite),
            heart=MakeImage("BrightCenter",starRoot,glowSprite) };
        star.halo.rectTransform.sizeDelta=Vector2.one*104f;
        star.core.rectTransform.sizeDelta=new Vector2(16f,26f);
        star.heart.rectTransform.sizeDelta=Vector2.one*14f;
        stars.Add(star);
        slots[slot]=star;
        for (int i=0; i<edgeCreated.Length; i++)
            if (!edgeCreated[i] && slots[LyreEdges[i,0]]!=null && slots[LyreEdges[i,1]]!=null)
            {
                edgeCreated[i]=true;
                connections.Add(new Connection { from=slots[LyreEdges[i,0]],to=slots[LyreEdges[i,1]],isString=i>=16,
                    halo=MakeImage("LyreThreadGlow",connectionLayer,glowSprite),core=MakeImage("LyreThread",connectionLayer,glowSprite) });
            }
        return star;
    }

    public void RecordSuccess(RectTransform note, bool special, bool spinner, bool perfect=false)
    {
        if (note==null || finalReveal || hiddenForInventory) return;
        InitializeSky();
        if (!skyReady) return;
        successfulHits++;
        Star awakened=null;
        for (int i=0; i<UnlockAt.Length; i++)
            if (successfulHits==UnlockAt[i]) awakened=Awaken(UnlockSlots[i],perfect);
        Vector2 point=NotePosition(note);
        resonatingStar=awakened;
        if (resonatingStar==null)
        {
            float closest=float.PositiveInfinity;
            foreach (Star star in stars)
            {
                float d=(point-star.position).sqrMagnitude;
                if (d<closest) { closest=d; resonatingStar=star; }
            }
        }
        resonanceAt=VisualTime;
        if (resonatingStar!=null && perfect) resonatingStar.quality=1.12f;
        if (special)
        {
            specialAt=VisualTime;
            specialCenter=point;
            specialRadius=1f;
            foreach (Star star in stars) specialRadius=Mathf.Max(specialRadius,(star.position-point).magnitude);
        }
        if (spinner) { spinnerAt=VisualTime; spinnerCenter=point; }
        Paint();
    }

    public void BeginFinalReveal(CanvasGroup inventory = null, CanvasGroup reward = null)
    {
        finalReveal = true;
        hiddenForInventory = false;
        revealStarted = Time.unscaledTime;
        endingCenter = new Vector2(0f, canvasSize.y * .04f);
        inventoryFade = inventory;
        rewardFade = reward;
        if (inventoryFade != null) { inventoryOriginalAlpha = inventoryFade.alpha; inventoryFade.alpha = 0f; }
        if (rewardFade != null) { rewardOriginalAlpha = rewardFade.alpha; rewardFade.alpha = 0f; }
        foreach (Star star in stars)
        {
            star.endingScale = star.root.localScale;
            star.endingCoreAlpha = star.core.color.a;
        }
        BuildEndingLight();
        Paint();
    }

    public void HideForInventory()
    {
        hiddenForInventory = true;
        visibility.alpha = 0f;
        if (inventoryFade != null) inventoryFade.alpha = inventoryOriginalAlpha;
        if (rewardFade != null) rewardFade.alpha = rewardOriginalAlpha;
    }

    void LateUpdate()
    {
        if (hiddenForInventory) return;
        InitializeSky();
        Paint();
    }

    static float Smooth(float from,float to,float t) => Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(from,to,t));
    static float Bell(float center,float width,float t) => Mathf.Pow(Mathf.Clamp01(1f-Mathf.Abs(t-center)/width),2f);
    Color Ink(float alpha,float ivory=.65f)
    {
        Color color=Color.Lerp(traceColor,Color.white,ivory);
        color.a=alpha;
        return color;
    }
    Color Starlight(float alpha,int slot)
    {
        Color color=Ink(alpha,.72f);
        if (slot%6==3) color=Color.Lerp(color,new Color(.90f,.94f,1f,alpha),.22f);
        return color;
    }
    float AwakeningWave(Vector2 position,float progress)
    {
        if (progress<0f || progress>1f) return 0f;
        float radius=(position-specialCenter).magnitude/specialRadius;
        return Mathf.Clamp01(1f-Mathf.Abs(progress*1.3f-radius)/.18f);
    }

    Vector2 flowCenter;
    double flowAt = -100.0;
    float flowStrength;
    public void PulseFlow(Vector3 worldPosition, float strength)
    {
        if (finalReveal || field == null) return;
        flowCenter = field.InverseTransformPoint(worldPosition);
        flowAt = VisualTime;
        flowStrength = Mathf.Clamp01(strength);
    }

    void PaintFlowResponse()
    {
        float pulse = flowStrength * Mathf.Pow(1f - Mathf.Clamp01((float)((VisualTime - flowAt) / .28)), 2f);
        if (pulse <= 0f) return;
        foreach (SkyStar star in background)
        {
            float nearby = 1f - Mathf.Clamp01((star.position - flowCenter).magnitude / (canvasSize.x * .28f));
            Color color = star.halo.color;
            color.a += pulse * nearby * .045f;
            star.halo.color = color;
        }
        foreach (Star star in stars)
        {
            float nearby = 1f - Mathf.Clamp01((star.position - flowCenter).magnitude / (canvasSize.x * .28f));
            Color color = star.halo.color;
            color.a += pulse * nearby * .055f;
            star.halo.color = color;
        }
    }

    void Paint()
    {
        if (!skyReady) return;
        if (finalReveal) { PaintEnding(); return; }
        int stage=VisualStage;
        float special=(float)((VisualTime-specialAt)/1.8);
        float spinner=(float)((VisualTime-spinnerAt)/2.2);
        bool spinning=spinner>=0f && spinner<=1f;
        float pull=spinning?Smooth(0f,.38f,spinner)*(1f-Smooth(.58f,1f,spinner)):0f;
        float flash=spinning?Bell(.52f,.25f,spinner):0f;
        visibility.alpha=1f;
        field.localScale=Vector3.one;
        float common=special>=0f && special<=1f?Mathf.Sin(special*Mathf.PI)*.08f:0f;
        for (int i=0; i<background.Count; i++)
        {
            SkyStar star=background[i];
            float breath=Mathf.Sin((float)VisualTime*(.45f+(i%4)*.035f)+star.phase);
            star.core.color=Ink(star.brightness+breath*.012f,.78f);
            star.halo.color=Ink(.018f+breath*.004f);
            star.core.rectTransform.localScale=Vector3.one*(1f+breath*.045f);
        }
        foreach (Star star in stars)
        {
            float twinkle=Mathf.Sin((float)VisualTime*(.74f+(star.slot%5)*.075f)+star.phase);
            float wave=AwakeningWave(star.position,special);
            float resonance=star==resonatingStar?Mathf.Clamp01(1f-(float)(VisualTime-resonanceAt)/.6f):0f;
            star.rendered=Vector2.Lerp(star.position,spinnerCenter,pull*.24f);
            star.root.anchoredPosition=star.rendered;
            star.root.localScale=Vector3.one*star.size*(1f+twinkle*.065f+wave*.25f+pull*.17f+flash*.14f+resonance*.10f);
            float brightness=(.44f+stage*.065f+twinkle*(.025f+stage*.007f))*star.quality;
            brightness+=wave*.28f+common+pull*.18f+flash*.10f+resonance*.08f;
            star.core.color=Starlight(Mathf.Min(.95f,brightness),star.slot);
            star.heart.color=Ink(Mathf.Min(.98f,brightness+.13f),.86f);
            star.halo.color=Ink(.095f+stage*.017f+twinkle*.012f+wave*.09f+pull*.07f+resonance*.035f);
        }
        foreach (Connection line in connections)
        {
            float wave=AwakeningWave((line.from.position+line.to.position)*.5f,special);
            SetLine(line.core,line.from.rendered,line.to.rendered,line.isString?2f:3f);
            SetLine(line.halo,line.from.rendered,line.to.rendered,line.isString?11f:15f);
            float alpha=stage==0?.02f:stage==1?.10f:stage==2?.16f:.22f;
            alpha+=wave*.14f+common+pull*.11f+flash*.06f;
            if (line.isString) alpha*=.74f;
            line.core.color=Ink(Mathf.Min(.48f,alpha),.48f);
            line.halo.color=Ink(alpha*.20f);
        }
        ambient.rectTransform.anchoredPosition=new Vector2(-canvasSize.x*.06f,0);
        ambient.color=Ink(.006f+stage*.008f+common*.2f);
        bloom.rectTransform.anchoredPosition=spinnerCenter;
        bloom.rectTransform.sizeDelta=Vector2.one*650f;
        bloom.color=Ink(flash*.27f,.82f);
        PaintFlowResponse();
    }

    static void SetLine(Image image,Vector2 from,Vector2 to,float width)
    {
        Vector2 delta=to-from;
        image.rectTransform.anchoredPosition=(from+to)*.5f;
        image.rectTransform.sizeDelta=new Vector2(delta.magnitude,width);
        image.rectTransform.localEulerAngles=new Vector3(0f,0f,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
    }
    void BuildEndingLight()
    {
        if (endingOverlay != null) { endingOverlay.gameObject.SetActive(true); return; }
        endingOverlay = Layer("EndingLightOverlay", root);
        endingOverlay.anchorMin = Vector2.zero;
        endingOverlay.anchorMax = Vector2.one;
        Canvas canvas = endingOverlay.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        Canvas parentCanvas = root.GetComponentInParent<Canvas>();
        canvas.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 50;
        endingTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        endingTexture.name = "RuntimeEndingBloom";
        endingTexture.hideFlags = HideFlags.HideAndDontSave;
        endingTexture.wrapMode = TextureWrapMode.Clamp;
        endingTexture.filterMode = FilterMode.Bilinear;
        endingPixels = new Color[128 * 128];
        UpdateBloomAperture(-1f);
        endingSprite = Sprite.Create(endingTexture, new Rect(0, 0, 128, 128), Vector2.one * .5f,
            100f, 0, SpriteMeshType.FullRect);
        endingSprite.hideFlags = HideFlags.HideAndDontSave;
        endingOuter = MakeImage("OuterCelestialLight", endingOverlay, endingSprite);
        endingBloom = MakeImage("ExpandingCelestialLight", endingOverlay, endingSprite);
        endingHalo = MakeImage("ConcentratedHalo", endingOverlay, glowSprite);
        endingRays = MakeImage("FourPointStarFlare", endingOverlay, starSprite);
        endingCrossRays = MakeImage("SoftDiagonalStarFlare", endingOverlay, starSprite);
        endingRays.rectTransform.localEulerAngles = new Vector3(0f, 0f, 12f);
        endingCrossRays.rectTransform.localEulerAngles = new Vector3(0f, 0f, 57f);
        endingCore = MakeImage("MergedStarCore", endingOverlay, starSprite);
        for (int i = 0; i < DispersalParticleCount; i++)
        {
            float angle = i * 2.399963f + .13f;
            var particle = new EndingParticle {
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                speed = .78f + (i % 6) * .072f, delay = (i % 4) * .018f,
                size = 12f + (i % 5) * 4f,
                halo = MakeImage("ReleasedStarHalo_" + i, endingOverlay, glowSprite),
                core = MakeImage("ReleasedStar_" + i, endingOverlay, starSprite)
            };
            endingParticles.Add(particle);
        }
    }

    static float BloomMask(float radius, float opening)
    {
        float outside = Mathf.Exp(-.9f * radius * radius) * (1f - Smooth(.78f, 1f, radius));
        return outside * (opening < 0f ? 1f : Smooth(opening - .07f, opening + .07f, radius));
    }

    void UpdateBloomAperture(float opening)
    {
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++)
        {
            float dx = (x + .5f - 64f) / 64f, dy = (y + .5f - 64f) / 64f;
            endingPixels[y * 128 + x] = new Color(1f, 1f, 1f, BloomMask(Mathf.Sqrt(dx * dx + dy * dy), opening));
        }
        endingTexture.SetPixels(endingPixels);
        endingTexture.Apply(false, false);
    }

    static Color EndingIvory(float alpha) => new Color(1f, .985f, .945f, alpha);

    void PaintEnding()
    {
        float elapsed = Mathf.Max(0f, Time.unscaledTime - revealStarted);
        float prep = Smooth(0f, PrepEnd, elapsed);
        float collapse = Smooth(PrepEnd, ConvergenceEnd, elapsed);
        float expand = Smooth(ConvergenceEnd, ExpansionEnd, elapsed);
        float dispersal = Mathf.Clamp01((elapsed - PeakEnd) / DispersalSeconds);
        float release = elapsed >= PeakEnd ? 1f - Smooth(0f, .20f, dispersal) : 1f;
        visibility.alpha = 1f;
        field.localScale = Vector3.one;
        foreach (Star star in stars)
        {
            star.rendered = Vector2.Lerp(star.position, endingCenter, collapse);
            star.root.anchoredPosition = star.rendered;
            star.root.localScale = star.endingScale * (1f + prep * .06f + collapse * .65f);
            star.core.color = Starlight(Mathf.Min(1f, star.endingCoreAlpha + prep * .12f + collapse * .30f) * release, star.slot);
            star.heart.color = EndingIvory(Mathf.Min(1f, star.endingCoreAlpha + .25f + collapse * .4f) * release);
            star.halo.color = EndingIvory((.20f + collapse * .58f) * release);
        }
        foreach (Connection line in connections)
        {
            SetLine(line.core, line.from.rendered, line.to.rendered, line.isString ? 2f : 3f);
            SetLine(line.halo, line.from.rendered, line.to.rendered, 18f);
            line.core.color = EndingIvory((.29f + collapse * .35f) * release);
            line.halo.color = EndingIvory((.08f + collapse * .16f) * release);
        }
        foreach (SkyStar star in background)
        {
            star.core.rectTransform.anchoredPosition = star.halo.rectTransform.anchoredPosition = Vector2.Lerp(star.position, endingCenter, collapse);
            star.core.rectTransform.localScale = Vector3.one;
            star.core.color = EndingIvory((star.brightness + collapse * .35f) * release);
            star.halo.color = EndingIvory((.012f + collapse * .16f) * release);
        }
        ambient.color = Color.clear;
        bloom.color = Color.clear;
        endingOuter.rectTransform.anchoredPosition = endingBloom.rectTransform.anchoredPosition = endingHalo.rectTransform.anchoredPosition = endingCore.rectTransform.anchoredPosition = endingCenter;
        endingHalo.rectTransform.sizeDelta = Vector2.one * (180f + collapse * 600f);
        endingCore.rectTransform.sizeDelta = Vector2.one * (24f + collapse * 420f);
        endingHalo.color = EndingIvory(collapse * .92f * release);
        endingCore.color = EndingIvory(collapse * release);
        endingRays.rectTransform.anchoredPosition = endingCrossRays.rectTransform.anchoredPosition = endingCenter;
        endingRays.rectTransform.sizeDelta = Vector2.one * (180f + collapse * 500f + expand * 1600f);
        endingCrossRays.rectTransform.sizeDelta = Vector2.one * (100f + collapse * 350f + expand * 900f);
        float rayFade = 1f - Smooth(0f, .65f, dispersal);
        endingRays.color = EndingIvory(collapse * .72f * rayFade);
        endingCrossRays.color = new Color(.79f, .88f, 1f, collapse * .30f * rayFade);
        endingBloom.rectTransform.sizeDelta = Vector2.Lerp(Vector2.one * 90f, canvasSize * 1.60f, expand);
        endingOuter.rectTransform.sizeDelta = Vector2.Lerp(Vector2.one * 150f, canvasSize * 1.60f, Mathf.Sqrt(expand));
        endingBloom.color = EndingIvory(expand * .78f);
        endingOuter.color = new Color(1f, .84f, .52f, expand * .45f);
        if (elapsed >= PeakEnd) UpdateBloomAperture(Smooth(0f, 1f, dispersal) * 1.28f);
        foreach (EndingParticle particle in endingParticles)
        {
            float u = Mathf.Clamp01((elapsed - PeakEnd - particle.delay) / (DispersalSeconds - particle.delay));
            float travel = Smooth(0f, 1f, u) * particle.speed;
            Vector2 offset = new Vector2(particle.direction.x * canvasSize.x * .62f, particle.direction.y * canvasSize.y * .65f);
            Vector2 position = endingCenter + offset * travel;
            particle.core.rectTransform.anchoredPosition = particle.halo.rectTransform.anchoredPosition = position;
            float size = particle.size * (.65f + Mathf.Sin(u * Mathf.PI) * .4f);
            particle.core.rectTransform.sizeDelta = new Vector2(size, size * 1.5f);
            particle.halo.rectTransform.sizeDelta = Vector2.one * size * 4f;
            float alpha = Smooth(0f, .14f, u) * (1f - Smooth(.45f, 1f, u));
            particle.core.color = EndingIvory(alpha * .88f);
            particle.halo.color = EndingIvory(alpha * .24f);
        }
        float boardAlpha = Smooth(InventoryRevealDelay, FinalRevealSeconds, elapsed);
        if (inventoryFade != null) inventoryFade.alpha = inventoryOriginalAlpha * boardAlpha;
        if (rewardFade != null) rewardFade.alpha = rewardOriginalAlpha * boardAlpha;
        if (elapsed >= FinalRevealSeconds) HideForInventory();
    }

    public void ResetRun()
    {
        if (root == null) return;
        if (inventoryFade != null) inventoryFade.alpha = inventoryOriginalAlpha;
        if (rewardFade != null) rewardFade.alpha = rewardOriginalAlpha;
        inventoryFade = rewardFade = null;
        foreach (Star star in stars) { star.root.gameObject.SetActive(false); Destroy(star.root.gameObject); }
        foreach (Connection line in connections)
        {
            line.core.gameObject.SetActive(false); line.halo.gameObject.SetActive(false);
            Destroy(line.core.gameObject); Destroy(line.halo.gameObject);
        }
        stars.Clear(); connections.Clear();
        System.Array.Clear(slots, 0, slots.Length);
        System.Array.Clear(edgeCreated, 0, edgeCreated.Length);
        successfulHits = 0;
        finalReveal = hiddenForInventory = false;
        specialAt = spinnerAt = resonanceAt = double.NegativeInfinity;
        flowAt = -100;
        flowStrength = 0;
        resonatingStar = null;
        revealStarted = 0;
        if (endingOverlay != null)
        {
            foreach (Image image in endingOverlay.GetComponentsInChildren<Image>(true)) image.color = Color.clear;
            endingOverlay.gameObject.SetActive(false);
        }
        visibility.alpha = 1;
        root.localScale = Vector3.one;
        Paint();
    }

    void OnDestroy()
    {
        if (inventoryFade != null) inventoryFade.alpha = inventoryOriginalAlpha;
        if (rewardFade != null) rewardFade.alpha = rewardOriginalAlpha;
        if (endingSprite != null) Destroy(endingSprite);
        if (endingTexture != null) Destroy(endingTexture);
        if (glowSprite!=null) Destroy(glowSprite);
        if (starSprite!=null) Destroy(starSprite);
        if (glowAtlas!=null) Destroy(glowAtlas);
    }
}
