using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;

public class ScoreUI : MonoBehaviour
{
    public RhythmGameManager gameManager;
    public TMP_Text scoreText;
    public TMP_Text comboText;
    public TMP_Text multiplierText;

    [SerializeField] GameObject[] gameplayOnly = new GameObject[0];
    bool finalScoreMode;
    bool capturedParchment;
    Image.Type parchmentType;
    Image.FillMethod parchmentMethod;
    int parchmentOrigin;
    float parchmentAmount;
    void CaptureParchment()
    {
        if (capturedParchment) return;
        capturedParchment = true;
        Image image = GetComponent<Image>();
        if (image == null) return;
        parchmentType = image.type;
        parchmentMethod = image.fillMethod;
        parchmentOrigin = image.fillOrigin;
        parchmentAmount = image.fillAmount;
    }

    public void ResetRun()
    {
        CaptureParchment();
        finalScoreMode = false;
        displayedScore = scoreFrom = targetScore = shownCombo = 0;
        scoreAt = punchAt = brokenAt = -10f;
        punchAmount = 0;
        milestone = false;
        foreach (GameObject item in gameplayOnly) if (item != null) item.SetActive(true);
        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.type = parchmentType;
            image.fillMethod = parchmentMethod;
            image.fillOrigin = parchmentOrigin;
            image.fillAmount = parchmentAmount;
        }
        var group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1;
        if (comboText != null) { comboText.text = "0"; comboText.transform.localScale = comboScale; comboText.color = comboInk; }
        if (scoreText != null) scoreText.text = "0";
        if (multiplierText != null) multiplierText.text = "1×";
        if (comboGhost != null) comboGhost.gameObject.SetActive(false);
        foreach (var accent in accents) if (accent != null) accent.color = Color.clear;
    }
    public void BeginFinalScore()
    {
        finalScoreMode = true;
        foreach (GameObject item in gameplayOnly) if (item != null) item.SetActive(false);
        comboText.transform.localScale = comboScale;
        if (comboGhost != null) comboGhost.gameObject.SetActive(false);
        foreach (var accent in accents) if (accent != null) accent.color = Color.clear;
        Image parchment = GetComponent<Image>();
        if (parchment != null)
        {
            parchment.type = Image.Type.Filled;
            parchment.fillMethod = Image.FillMethod.Vertical;
            parchment.fillOrigin = 1;
            parchment.fillAmount = .5f;
        }
        SetFinalScore(0);
    }

    public void SetFinalScore(int value)
    {
        if (finalScoreMode) scoreText.text = Mathf.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);
    }

    const float ScoreSeconds = .16f;
    float displayedScore, scoreFrom, scoreAt = -10f;
    int targetScore, shownCombo;
    float punchAt = -10f, punchAmount, punchSeconds = .18f, brokenAt = -10f;
    bool milestone;
    Vector3 comboScale;
    Color comboInk;
    TMP_Text comboGhost;
    readonly CircleApproachRing[] accents = new CircleApproachRing[2];

    void OnEnable()
    {
        CaptureParchment();
        if (gameManager == null || comboText == null || scoreText == null) return;
        comboScale = comboText.transform.localScale;
        comboInk = comboText.color;
        if (comboGhost == null)
        {
            comboGhost = Instantiate(comboText, comboText.transform.parent);
            comboGhost.name = "BrokenComboEcho";
            comboGhost.raycastTarget = false;
            comboGhost.gameObject.SetActive(false);
            for (int i = 0; i < accents.Length; i++)
            {
                var star = new GameObject("ComboMilestoneStar_" + i, typeof(RectTransform), typeof(CanvasRenderer));
                accents[i] = star.AddComponent<CircleApproachRing>();
                accents[i].StarShape = true;
                accents[i].rectTransform.SetParent(comboText.transform.parent, false);
                accents[i].rectTransform.sizeDelta = Vector2.one * 26f;
                accents[i].raycastTarget = false;
                accents[i].color = Color.clear;
            }
        }
        displayedScore = targetScore = gameManager.score;
        shownCombo = gameManager.combo;
        comboText.text = shownCombo.ToString();
        scoreText.text = targetScore.ToString("N0", CultureInfo.InvariantCulture);
        if (multiplierText != null) multiplierText.text = gameManager.LastScoreMultiplier + "×";
        gameManager.OnJudged += OnJudged;
    }

    void OnDisable()
    {
        if (gameManager != null) gameManager.OnJudged -= OnJudged;
        if (comboText != null) { comboText.transform.localScale = comboScale; comboText.color = comboInk; }
        if (comboGhost != null) comboGhost.gameObject.SetActive(false);
        foreach (var accent in accents) if (accent != null) accent.color = Color.clear;
        punchAt = brokenAt = -10f;
    }

    void OnJudged(Judgement result)
    {
        if (finalScoreMode) return;
        if (targetScore != gameManager.score)
        {
            scoreFrom = displayedScore;
            targetScore = gameManager.score;
            scoreAt = Time.unscaledTime;
        }
        int previous = shownCombo;
        shownCombo = gameManager.combo;
        comboText.text = shownCombo.ToString();
        if (multiplierText != null) multiplierText.text = gameManager.LastScoreMultiplier + "×";
        if (shownCombo > previous)
        {
            milestone = shownCombo % 10 == 0;
            punchAmount = milestone ? .38f : result == Judgement.Perfect ? .25f : .18f;
            punchSeconds = milestone ? .22f : .18f;
            punchAt = Time.unscaledTime;
            brokenAt = -10f;
            comboGhost.gameObject.SetActive(false);
            PaintCombo();
        }
        else if (result == Judgement.Miss && previous > 0)
        {
            punchAt = -10f;
            brokenAt = Time.unscaledTime;
            comboGhost.text = previous.ToString();
            comboGhost.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        if (finalScoreMode || gameManager == null || scoreText == null || comboText == null) return;
        float s = Mathf.Clamp01((Time.unscaledTime - scoreAt) / ScoreSeconds);
        displayedScore = Mathf.Lerp(scoreFrom, targetScore, 1f - Mathf.Pow(1f - s, 3f));
        scoreText.text = Mathf.RoundToInt(displayedScore).ToString("N0", CultureInfo.InvariantCulture);
        PaintCombo();
        if (comboGhost != null && comboGhost.gameObject.activeSelf)
        {
            float broken = Mathf.Clamp01((Time.unscaledTime - brokenAt) / .16f);
            comboGhost.rectTransform.anchoredPosition = comboText.rectTransform.anchoredPosition + new Vector2(0f, -8f - 24f * broken);
            comboGhost.transform.localScale = comboScale * Mathf.Lerp(1f, .72f, broken);
            comboGhost.color = new Color(comboInk.r, comboInk.g, comboInk.b, .65f * (1f - broken));
            if (broken >= 1f) comboGhost.gameObject.SetActive(false);
        }
    }
    void PaintCombo()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - punchAt) / punchSeconds);
        float pulse = Mathf.Pow(1f - t, 2f);
        comboText.transform.localScale = comboScale * (1f + punchAmount * pulse);
        comboText.color = Color.Lerp(comboInk, new Color(.52f, .29f, .07f), milestone ? pulse : 0f);
        for (int i = 0; i < accents.Length; i++)
        {
            if (accents[i] == null) continue;
            accents[i].rectTransform.anchoredPosition = comboText.rectTransform.anchoredPosition
                + new Vector2((i == 0 ? -1f : 1f) * (88f + t * 18f), 0f);
            accents[i].rectTransform.localScale = Vector3.one * (.75f + pulse * .5f);
            accents[i].color = new Color(.60f, .36f, .09f, milestone ? pulse * .9f : 0f);
        }
    }
}
