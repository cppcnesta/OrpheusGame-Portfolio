using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orpheus.VN
{
    public class VNContext
    {
        public VNRunner Runner;
        public DialogueView Dialogue;
        public BackgroundLayer Background;
        public ActorLayer Actors;
        public CGLayer CG;
        public VNAudio Audio;
        public TransitionView Transition;

        public CharacterDatabase Characters;
        public VNAssetLibrary Library;

        public readonly Dictionary<string, string> Variables = new Dictionary<string, string>();

        public VNStageState Stage = new VNStageState();

        public float LineGap = 0.5f;

        public bool PauseAtBlankLines = true;

        public IGameplaySegment ActiveSegment;
        public bool SegmentHoldsInputLock;

        public int Session { get; private set; }

        public bool IsCurrent(int session)
        {
            return session == Session;
        }

        public void BeginSession()
        {
            Session++;

            if (ActiveSegment != null)
            {
                try { ActiveSegment.Cancel(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            ActiveSegment = null;
            if (Transition != null) Transition.Cancel();
            if (Background != null) Background.StopAllCoroutines();
            if (CG != null) CG.StopAllCoroutines();
            if (Audio != null) Audio.StopAllCoroutines();
            SegmentHoldsInputLock = false;

            inputLocks = 0;
            if (Dialogue != null)
            {
                Dialogue.SetInputEnabled(true);
                Dialogue.Hide();
            }
        }

        int inputLocks;

        public bool InputLocked { get { return inputLocks > 0; } }

        public void LockInput()
        {
            inputLocks++;
            if (Dialogue != null) Dialogue.SetInputEnabled(false);
        }

        public void UnlockInput()
        {
            inputLocks = Mathf.Max(0, inputLocks - 1);
            if (inputLocks == 0 && Dialogue != null) Dialogue.SetInputEnabled(true);
        }

        public void ReleaseSegmentInputLock()
        {
            if (!SegmentHoldsInputLock) return;
            SegmentHoldsInputLock = false;
            UnlockInput();
        }
    }
}
