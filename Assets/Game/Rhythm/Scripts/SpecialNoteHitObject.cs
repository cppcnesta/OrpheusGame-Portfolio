using UnityEngine;

public class SpecialNoteHitObject : CircleHitObject
{
    [Tooltip("Inventory reward icon; the demo reveals it only after the song ends.")]
    public GameObject rewardIcon;

    protected override int ScoreMultiplier => 2;

    protected override void OnSuccessfulHit(Judgement result)
    {
        if (gameManager != null)
            gameManager.AwardMusicNote();
    }
}
