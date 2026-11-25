using UnityEngine;

public class RhythmClock : MonoBehaviour
{
    public AudioSource musicSource;
    public double songTime;
    double startTime, pausedAt;
    bool started;
    public bool IsRunning => started;
    public bool IsPaused { get; private set; }

    public void Begin(AudioClip clip, double offset = 0)
    {
        Stop();
        musicSource.clip = clip;
        musicSource.loop = false;
        musicSource.timeSamples = Mathf.Clamp((int)(offset * clip.frequency), 0, clip.samples - 1);
        startTime = AudioSettings.dspTime + 1.0;
        started = true;
        musicSource.PlayScheduled(startTime);
    }

    public void Stop()
    {
        if (musicSource != null) musicSource.Stop();
        started = false;
        IsPaused = pausedByFocus = false;
        songTime = startTime = pausedAt = 0;
    }

    void Update()
    {
        if (started && !IsPaused)
            songTime = System.Math.Max(0.0, AudioSettings.dspTime - startTime);
    }

    public void Pause()
    {
        if (!started || IsPaused) return;
        pausedAt = AudioSettings.dspTime;
        songTime = System.Math.Max(0.0, pausedAt - startTime);
        IsPaused = true;
        musicSource.Pause();
    }

    public void Resume()
    {
        if (!started || !IsPaused) return;
        startTime += AudioSettings.dspTime - pausedAt;
        IsPaused = false;
        musicSource.UnPause();
    }

    bool pausedByFocus;
    public static bool PauseOnFocusLoss = true;
    void OnApplicationFocus(bool focused)
    {
        if (!focused && PauseOnFocusLoss && started && !IsPaused) { Pause(); pausedByFocus = true; }
        else if (focused && pausedByFocus) { pausedByFocus = false; Resume(); }
    }
    void OnApplicationPause(bool paused) => OnApplicationFocus(!paused);

    void OnDisable() => Stop();
}
