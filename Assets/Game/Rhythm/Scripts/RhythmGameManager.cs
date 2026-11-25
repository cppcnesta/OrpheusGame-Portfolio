using UnityEngine;
using System;
using Orpheus;

public class RhythmGameManager : MonoBehaviour
{
    public int score = 0;
    public int combo = 0;
    public int LastScoreMultiplier { get; private set; } = 1;

    public string ChapterId { get; private set; }
    public bool Retry { get; private set; }
    public bool EarnedMusicNote { get; private set; }
    public bool CanPlayRumorPaper { get; private set; }
    [SerializeField] RhythmPaperEffect paperFeedback;

    public void ResetRun(ChapterDefinition chapter = null, bool retry = false)
    {
        score = combo = 0;
        LastScoreMultiplier = 1;
        EarnedMusicNote = false;
        ChapterId = chapter != null ? chapter.id : null;
        CanPlayRumorPaper = chapter != null && chapter.rhythmPaperEnabled;
        Retry = retry;
    }

    public void PlayPaper(string text)
    {
        if (CanPlayRumorPaper && paperFeedback != null) paperFeedback.Play(text);
    }

    public void AwardMusicNote() => EarnedMusicNote = true;

    public event Action<Judgement> OnJudged;

    public void RegisterJudgement(Judgement result) => RegisterJudgement(result, 1);

    public void RegisterJudgement(Judgement result, int rewardMultiplier)
    {
        rewardMultiplier = Mathf.Max(1, rewardMultiplier);
        LastScoreMultiplier = result == Judgement.Miss ? 1 : rewardMultiplier;
        if (result == Judgement.Perfect)
        {
            score = score + 300 * rewardMultiplier;
            combo = combo + 1;
        }
        else if (result == Judgement.Good)
        {
            score = score + 100 * rewardMultiplier;
            combo = combo + 1;
        }
        else
        {
            combo = 0;
        }

        Debug.Log(
            result +
            " | Score: " + score +
            " | Combo: " + combo
        );

        if (OnJudged != null)
        {
            OnJudged(result);
        }
    }
}
