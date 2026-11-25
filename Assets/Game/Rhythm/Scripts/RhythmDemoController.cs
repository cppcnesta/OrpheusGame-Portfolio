using System.Collections;
using Orpheus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-50)]
public sealed class RhythmDemoController : MonoBehaviour
{
    [SerializeField] RhythmClock clock;
    [SerializeField] RhythmGameManager gameManager;
    [SerializeField] PaperSpawner papers;
    [SerializeField] HitObject[] notes;
    [SerializeField] CanvasGroup gameplayInput;
    [SerializeField] GameObject tutorial;
    [SerializeField] TMP_Text tutorialTitle;
    [SerializeField] TMP_Text tutorialBody;
    [SerializeField] Button tutorialContinue;
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject rewardNotice;
    [SerializeField] GameObject scoreHud;
    [SerializeField] ScoreUI scoreView;
    [SerializeField] Button resultContinue;
    [SerializeField] Image chapterBackground;
    [SerializeField] RhythmHitFeedback feedback;
    [SerializeField] RhythmPaperEffect paperFeedback;
    [SerializeField] HitObject[] noteTemplates;
    [SerializeField] RectTransform noteParent;
    readonly System.Collections.Generic.List<PooledNote> pool = new System.Collections.Generic.List<PooledNote>();
    sealed class PooledNote
    {
        public RhythmNoteKind kind;
        public HitObject note;
        public Transform[] visuals;
    }
    ChapterDefinition hostedChapter;
    bool initialized, running;
    public bool Retry { get; private set; }
    public bool ResultReady { get; private set; }
    public bool ResultAcknowledged { get; private set; }
    public double SongTime => clock != null ? clock.songTime : 0;
    public bool EarnedMusicNote => gameManager.EarnedMusicNote;

    public void BeginPerformance(ChapterDefinition chapter, bool retry)
    {
        CancelPerformance();
        hostedChapter = chapter;
        Retry = retry;
        gameManager.ResetRun(chapter, retry);
        circleLesson = sliderLesson = spinnerLesson = !chapter.rhythmTutorial || retry;
        chapterBackground.sprite = chapter.background;
        if (paperFeedback != null) paperFeedback.Configure(chapter.rhythmPaperAct);
        RhythmChartData chart = chapter.rhythmChart;
        if (chart.notes == null || chart.notes.Length == 0) throw new System.ArgumentException("Empty rhythm chart.");
        demoDuration = chart.duration;
        circleTutorialTime = chart.circleTutorialTime;
        sliderTutorialTime = chart.sliderTutorialTime;
        spinnerTutorialTime = chart.spinnerTutorialTime;
        ConfigureNotes(chart);
        scoreView.ResetRun();
        feedback.ConfigureFlow();
        gameplayInput.interactable = gameplayInput.blocksRaycasts = true;
        scoreHud.SetActive(true);
        running = true;
        clock.Begin(chapter.music, chart.musicStart);
        if (chart.musicStart > 0) StartCoroutine(FadeInMusic(musicFadeSeconds));
    }

    void ConfigureNotes(RhythmChartData chart)
    {
        bool shapeMatches = pool.Count == chart.notes.Length;
        if (shapeMatches)
            for (int i = 0; i < pool.Count; i++) if (pool[i].kind != chart.notes[i].kind || pool[i].visuals.Length != chart.notes[i].visuals.Length) { shapeMatches = false; break; }
        if (!shapeMatches)
        {
            foreach (var item in pool) { item.note.gameObject.SetActive(false); Destroy(item.note.gameObject); }
            pool.Clear();
        }
        notes = new HitObject[chart.notes.Length];
        nextLine = nextPaper = 0;
        for (int i = 0; i < notes.Length; i++)
        {
            RhythmNoteData data = chart.notes[i];
            PooledNote item;
            if (!shapeMatches)
            {
                var note = Instantiate(noteTemplates[(int)data.kind], noteParent);
                item = new PooledNote { kind = data.kind, note = note, visuals = new Transform[data.visuals.Length] };
                for (int v = 0; v < data.visuals.Length; v++)
                {
                    Transform target = note.transform;
                    string path = data.visuals[v].childPath;
                    if (!string.IsNullOrEmpty(path)) foreach (string child in path.Split('/')) target = target.GetChild(int.Parse(child));
                    item.visuals[v] = target;
                }
                pool.Add(item);
            }
            else item = pool[i];
            ApplyNote(item, data);
            item.note.CapturePose();
            item.note.gameObject.SetActive(true);
            item.note.ResetRun();
            notes[i] = item.note;
        }
    }

    void ApplyNote(PooledNote item, RhythmNoteData data)
    {
        HitObject note = item.note;
        note.name = data.name;
        note.hitTime = data.hitTime; note.approachTime = data.approachTime;
        note.perfectWindow = data.perfectWindow; note.goodWindow = data.goodWindow;
        note.Configure(clock, gameManager, feedback, emergenceVisual);
        for (int v = 0; v < data.visuals.Length; v++)
        {
            var visual = data.visuals[v];
            var rect = (RectTransform)item.visuals[v];
            rect.anchorMin = visual.anchorMin; rect.anchorMax = visual.anchorMax; rect.pivot = visual.pivot;
            rect.anchoredPosition = visual.position; rect.sizeDelta = visual.size;
            rect.localScale = visual.scale; rect.localRotation = visual.rotation;
            var image = rect.GetComponent<Image>();
            if (visual.hasImage && image != null) { image.sprite = visual.sprite; image.color = visual.color; image.preserveAspect = visual.preserveAspect; }
            var text = rect.GetComponent<TMP_Text>();
            if (text != null && visual.label != null) text.text = visual.label;
        }
        if (note is CircleHitObject circle)
        {
            var papers = hostedChapter != null ? hostedChapter.papers : null;
            string text = data.triggerPaperEffect && papers != null && papers.Count > 0 ? papers[nextPaper++ % papers.Count] : data.rumorText;
            circle.ConfigureCircle(data.visuals[0].sprite, data.triggerPaperEffect, text);
        }
        if (note is SliderHitObject slider)
        {
            slider.endTime = data.endTime; slider.dragSensitivity = data.dragSensitivity; slider.smoothTime = data.smoothTime;
            var lines = hostedChapter != null ? hostedChapter.lines : null;
            bool more = lines != null && lines.Count > 0 && (nextLine < lines.Count || hostedChapter.repeatLines);
            var line = more ? lines[nextLine++ % lines.Count] : default;
            var speaker = characters != null && !string.IsNullOrEmpty(line.speaker) ? characters.Find(line.speaker) : null;
            slider.ConfigureDialogue(dialogue, speaker != null && speaker.stageBubble ? speaker : null, line.speaker, line.text);
        }
        if (note is SpinnerHitObject spinner) { spinner.endTime = data.endTime; spinner.requiredRotation = data.requiredRotation; }
    }

    public void CancelPerformance()
    {
        if (!initialized) return;
        running = false;
        StopAllCoroutines();
        clock.Stop();
        if (clock.musicSource != null) clock.musicSource.volume = musicVolume;
        TutorialOpen = ended = ResultReady = ResultAcknowledged = false;
        Retry = false;
        hostedChapter = null;
        resultAt = countStartsAt = -1;
        FinalScore = 0;
        circleLesson = sliderLesson = spinnerLesson = false;
        gameplayInput.interactable = gameplayInput.blocksRaycasts = false;
        tutorial.SetActive(false);
        resultPanel.SetActive(false);
        rewardNotice.SetActive(false);
        if (rewardPaper != null) rewardPaper.transform.localScale = rewardScale;
        if (rewardIcon != null) { ((RectTransform)rewardIcon.transform).anchoredPosition = rewardIconHome; rewardIcon.transform.localScale = Vector3.one; rewardIcon.alpha = 0; }
        resultContinue.gameObject.SetActive(false);
        if (dialogue != null) dialogue.Hide();
        if (paperFeedback != null) paperFeedback.ResetRun();
        feedback.ResetRun();
        foreach (var item in pool)
        {
            item.note.ResetRun();
            item.note.gameObject.SetActive(false);
        }
        emergenceVisual.ResetRun();
        gameManager.ResetRun();
        scoreView.ResetRun();
    }

    const float ScoreCountSeconds = 1.25f;
    [Tooltip("剧情的对话框（场景里的 VN Dialogue）。长条音符的台词用它显示；不拖就不显示台词")]
    [SerializeField] Orpheus.VN.DialogueView dialogue;
    [Tooltip("剧情的角色表：演员的台词按角色用舞台气泡")]
    [SerializeField] Orpheus.VN.CharacterDatabase characters;
    int nextLine, nextPaper;
    [Header("获得音符：白纸展开（和剧情里的白纸同一张纸，不落地）")]
    [SerializeField] PaperArtView rewardPaper;
    [Tooltip("获得的音符：纸展开后飞进道具栏")]
    [SerializeField] CanvasGroup rewardIcon;
    [Tooltip("道具栏（UI 里的背包图）和它的 9 个格子；序幕、第一幕……依次放进第 0、1……格")]
    [SerializeField] RectTransform rewardBag;
    [SerializeField] Image[] rewardSlots;
    [SerializeField, Min(.05f)] float rewardFlySeconds = .6f;
    Vector2 rewardIconHome;
    [SerializeField] string rewardText = "获得了一枚音符";
    [SerializeField, Min(.05f)] float rewardPopSeconds = .45f;
    Vector3 rewardScale = Vector3.one;
    [Tooltip("谱面结束时歌淡出的秒数（谱面比歌短时不会突然断掉）")]
    [SerializeField, Min(0)] float musicFadeSeconds = 2f;
    float musicVolume = 1f;
    [SerializeField] RhythmEmergenceVisual emergenceVisual;
    [SerializeField] double circleTutorialTime = 2.0, sliderTutorialTime = 17.2, spinnerTutorialTime = 35;
    [SerializeField] double demoDuration = 60.0;

    public System.Collections.Generic.IReadOnlyList<HitObject> FlowNotes => notes;
    public RhythmEmergenceVisual FlowConstellation => emergenceVisual;

    bool ended;
    float resultAt = -1f;
    float countStartsAt = -1f;
    bool circleLesson, sliderLesson, spinnerLesson;
    public bool TutorialOpen { get; private set; }
    public bool Ended => ended;
    public int FinalScore { get; private set; }

    void Awake()
    {
        initialized = true;
        if (rewardPaper != null) rewardScale = rewardPaper.transform.localScale;
        if (clock != null && clock.musicSource != null) musicVolume = clock.musicSource.volume;
        if (rewardIcon != null) rewardIconHome = ((RectTransform)rewardIcon.transform).anchoredPosition;
        tutorialContinue.onClick.AddListener(ContinueTutorial);
        resultContinue.onClick.AddListener(AcknowledgeResult);
    }

    void OnDestroy()
    {
        if (tutorialContinue != null) tutorialContinue.onClick.RemoveListener(ContinueTutorial);
        if (resultContinue != null) resultContinue.onClick.RemoveListener(AcknowledgeResult);
    }

    void LateUpdate()
    {
        if (!running) return;
        if (ended)
        {
            if (resultAt >= 0f && Time.unscaledTime >= resultAt)
            {
                resultAt = -1f;
                ShowResult();
            }
            if (countStartsAt >= 0f && Time.unscaledTime >= countStartsAt)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - countStartsAt) / ScoreCountSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                scoreView.SetFinalScore(t >= 1f ? FinalScore : Mathf.RoundToInt(FinalScore * eased));
                if (t >= 1f)
                {
                    countStartsAt = -1f;
                    if (gameManager.EarnedMusicNote) StartCoroutine(RevealReward());
                    ResultReady = true;
                    resultContinue.gameObject.SetActive(hostedChapter != null);
                    if (hostedChapter != null) resultContinue.Select();
                }
            }
            return;
        }
        if (clock == null || TutorialOpen) return;
        double time = clock.songTime;
        if (time >= demoDuration)
        {
            Complete();
            return;
        }

        double due = double.MaxValue; int lesson = -1;
        if (!circleLesson && time >= circleTutorialTime && circleTutorialTime < due) { due = circleTutorialTime; lesson = 0; }
        if (!sliderLesson && time >= sliderTutorialTime && sliderTutorialTime < due) { due = sliderTutorialTime; lesson = 1; }
        if (!spinnerLesson && time >= spinnerTutorialTime && spinnerTutorialTime < due) { due = spinnerTutorialTime; lesson = 2; }
        if (lesson == 0)
        {
            circleLesson = true;
            OpenTutorial("圆圈音符", "外圈收缩到与音符重合时点击。\n鼠标左右键、键盘 Z / X 都可以。");
        }
        else if (lesson == 1)
        {
            sliderLesson = true;
            OpenTutorial("长条音符", "按住长条上的圆钮，\n把它拖到尽头再松开。");
        }
        else if (lesson == 2)
        {
            spinnerLesson = true;
            OpenTutorial("旋转音符", "按住任意位置，绕着旋转音符转圈，\n在它消失前转满一圈。");
        }
    }

    void OpenTutorial(string title, string body)
    {
        clock.Pause();
        TutorialOpen = true;
        gameplayInput.blocksRaycasts = false;
        gameplayInput.interactable = false;
        tutorialTitle.text = title;
        tutorialBody.text = body;
        tutorial.SetActive(true);
        tutorialContinue.Select();
    }

    public void ContinueTutorial()
    {
        if (!TutorialOpen) return;
        RhythmInput.IgnorePressesThisFrame();
        tutorial.SetActive(false);
        TutorialOpen = false;
        gameplayInput.blocksRaycasts = true;
        gameplayInput.interactable = true;
        clock.Resume();
    }

    void Complete()
    {
        foreach (HitObject note in notes)
            if (note != null) note.ResolveMissAtEnd();
        FinalScore = gameManager.score;
        ended = true;
        StartCoroutine(FadeOutMusic(musicFadeSeconds));
        tutorial.SetActive(false);
        if (dialogue != null) dialogue.Hide();
        papers.Clear();
        scoreView.BeginFinalScore();
        if (emergenceVisual != null && emergenceVisual.SuccessfulHitCount > 0)
        {
            emergenceVisual.BeginFinalReveal(EndingFadeGroup(scoreHud));
            resultAt = Time.unscaledTime + RhythmEmergenceVisual.InventoryRevealDelay;
            countStartsAt = Time.unscaledTime + RhythmEmergenceVisual.FinalRevealSeconds;
        }
        else
        {
            ShowResult();
            countStartsAt = Time.unscaledTime;
        }
    }

    static CanvasGroup EndingFadeGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    IEnumerator RevealReward()
    {
        rewardNotice.SetActive(true);
        if (rewardPaper == null) yield break;
        int slot = hostedChapter != null ? hostedChapter.noteSlot : -1;
        var icon = rewardIcon != null ? rewardIcon.GetComponent<Image>() : null;
        for (int i = 0; i < (rewardSlots?.Length ?? 0); i++)
        {
            bool earned = hostedChapter != null && hostedChapter.earnedSlots.Contains(i);
            rewardSlots[i].gameObject.SetActive(earned);
            if (icon != null) rewardSlots[i].sprite = icon.sprite;
        }
        var paper = rewardPaper.transform;
        rewardPaper.gameObject.SetActive(true);
        rewardPaper.Unfold(rewardText);
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / rewardPopSeconds)
        {
            float back = 1 + 2.2f * Mathf.Pow(t - 1, 3) + 1.2f * Mathf.Pow(t - 1, 2);
            paper.localScale = rewardScale * Mathf.LerpUnclamped(.6f, 1f, back);
            if (rewardBag != null) rewardBag.localScale = Vector3.one * Mathf.LerpUnclamped(.6f, 1f, back);
            if (rewardIcon != null) rewardIcon.alpha = rewardPaper.Progress;
            yield return null;
        }
        paper.localScale = rewardScale;
        if (rewardBag != null) rewardBag.localScale = Vector3.one;
        while (rewardPaper.Progress < 1) { if (rewardIcon != null) rewardIcon.alpha = rewardPaper.Progress; yield return null; }
        if (rewardIcon == null) yield break;
        rewardIcon.alpha = 1;
        if (rewardSlots == null || slot < 0 || slot >= rewardSlots.Length) yield break;
        yield return new WaitForSecondsRealtime(.35f);
        var flying = (RectTransform)rewardIcon.transform;
        var target = rewardSlots[slot].rectTransform;
        Vector3 from = flying.position, to = target.position;
        float endScale = target.rect.width / Mathf.Max(1f, flying.rect.width);
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / rewardFlySeconds)
        {
            float e = Mathf.SmoothStep(0, 1, t);
            flying.position = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 60f * flying.lossyScale.y;
            flying.localScale = Vector3.one * Mathf.Lerp(1f, endScale, e);
            yield return null;
        }
        rewardSlots[slot].gameObject.SetActive(true);
        rewardIcon.alpha = 0;
        flying.anchoredPosition = rewardIconHome; flying.localScale = Vector3.one;
    }

    IEnumerator FadeInMusic(float seconds)
    {
        var source = clock.musicSource;
        source.volume = 0;
        while (clock.songTime <= 0) yield return null;
        for (double from = clock.songTime; clock.songTime - from < seconds;)
        { source.volume = musicVolume * (float)((clock.songTime - from) / seconds); yield return null; }
        source.volume = musicVolume;
    }

    IEnumerator FadeOutMusic(float seconds)
    {
        var source = clock.musicSource;
        for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) { source.volume = musicVolume * (1 - t / seconds); yield return null; }
        source.Stop();
        source.volume = musicVolume;
    }

    void AcknowledgeResult()
    {
        if (ResultReady && hostedChapter != null) ResultAcknowledged = true;
    }

    void ShowResult()
    {
        resultPanel.SetActive(true);
        rewardNotice.SetActive(false);
    }
}
