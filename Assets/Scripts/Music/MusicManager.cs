using System;
using System.Collections;
using UnityEngine;

public class MusicManager : AudioManager<MusicManager>
{
    #region Inspector

    public MusicCueClipList musicCueClips;
    
    [Header("Dynamic Music")]
    [Min(0f)]
    public float defaultTransitionSeconds = 0.75f;

    [Min(0f)]
    public float introToNormalAdvanceSeconds = 0.1925f;

    [Range(0f, 1f)]
    public float minIntensityVolumeMultiplier = 0.35f;

    [Header("Playback")]
    public bool force2DMusic = true;

    public float TimeTransitionBackToNormalSeconds = 15f;
    public bool TransitionBackToNormalAfterSeconds = true; // only if no transitions are requested

    #endregion

    #region Runtime State

    private readonly AudioSource[] sources = new AudioSource[Enum.GetValues(typeof(MusicCues)).Length];
    private readonly bool[] additiveActive = new bool[Enum.GetValues(typeof(MusicCues)).Length];
    private MusicCueClipList runtimeMusicCueClips;
    private readonly Coroutine[] additiveFadeRoutines = new Coroutine[Enum.GetValues(typeof(MusicCues)).Length];

    private Coroutine transitionRoutine;
    private Coroutine pendingLoadRoutine;
    private Coroutine introTransitionRoutine;
    private Coroutine scheduledIntroToNormalCommitRoutine;
    private MusicCues currentCue;
    private bool hasCurrentCue;
    private double transportSeconds;
    private float intensity01 = 1f;
    private (float time, MusicCues cue)? lastTransition;

    private MusicCueClipList ActiveMusicCueClips => runtimeMusicCueClips != null ? runtimeMusicCueClips : musicCueClips;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        runtimeMusicCueClips = CreateRuntimeCueList(musicCueClips);
        PreloadCueClip(MusicCues.Intro);
        PreloadCueClip(MusicCues.Normal);
    }

    void Update()
    {
        UpdateTransportClock();

        if (TransitionBackToNormalAfterSeconds && hasCurrentCue && currentCue != MusicCues.Normal)
        {
            if (lastTransition.HasValue)
            {
                var (time, cue) = lastTransition.Value;
                if (cue == currentCue && Time.time - time >= TimeTransitionBackToNormalSeconds)
                {
                    TransitionTo(MusicCues.Normal, defaultTransitionSeconds);
                    lastTransition = null;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (pendingLoadRoutine != null)
        {
            StopCoroutine(pendingLoadRoutine);
            pendingLoadRoutine = null;
        }

        if (introTransitionRoutine != null)
        {
            StopCoroutine(introTransitionRoutine);
            introTransitionRoutine = null;
        }

        if (scheduledIntroToNormalCommitRoutine != null)
        {
            StopCoroutine(scheduledIntroToNormalCommitRoutine);
            scheduledIntroToNormalCommitRoutine = null;
        }

        for (int i = 0; i < additiveFadeRoutines.Length; i++)
        {
            if (additiveFadeRoutines[i] == null)
            {
                continue;
            }

            StopCoroutine(additiveFadeRoutines[i]);
            additiveFadeRoutines[i] = null;
        }

    }

    #endregion

    #region Public API

    public MusicCues CurrentCue => currentCue;
    public bool HasCurrentCue => hasCurrentCue;

    public void SetIntensity(float intensity)
    {
        intensity01 = Mathf.Clamp01(intensity);
        RefreshCurrentVolume();
    }

    public void PlayMusic(MusicCues cue)
    {
        PlayMusic(cue, CuePlaybackPolicy<MusicCues>.TrackCue);
    }

    public bool PlayMusic(MusicCues cue, CuePlaybackPolicy<MusicCues> playbackPolicy)
    {
        return PlayMusicInternal(cue, defaultTransitionSeconds, playbackPolicy);
    }

    public void PlayMusicNow(MusicCues cue)
    {
        PlayMusicNow(cue, CuePlaybackPolicy<MusicCues>.TrackCue);
    }

    public bool PlayMusicNow(MusicCues cue, CuePlaybackPolicy<MusicCues> playbackPolicy)
    {
        return PlayMusicInternal(cue, 0f, playbackPolicy);
    }

    public bool IsCuePlaying(MusicCues cue)
    {
        int index = (int)cue;
        if (index < 0 || index >= sources.Length)
        {
            return false;
        }

        AudioSource source = sources[index];
        return source != null && source.isPlaying;
    }

    public void StopMusic(float fadeOutSeconds = 0f)
    {
        StopAllAdditiveLayers(fadeOutSeconds);

        if (!hasCurrentCue)
        {
            return;
        }

        int currentIndex = (int)currentCue;
        if (sources[currentIndex] == null)
        {
            hasCurrentCue = false;
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (fadeOutSeconds <= 0f)
        {
            transportSeconds = GetCurrentTransportSeconds();
            sources[currentIndex].Stop();
            if (introTransitionRoutine != null)
            {
                StopCoroutine(introTransitionRoutine);
                introTransitionRoutine = null;
            }

            hasCurrentCue = false;
            return;
        }

        transitionRoutine = StartCoroutine(FadeOutRoutine(currentIndex, fadeOutSeconds));
    }

    public void TransitionTo(MusicCues cue, float transitionSeconds)
    {
        MusicCueClipList cueClips = ActiveMusicCueClips;
        if (cueClips == null)
        {
            Debug.LogError("MusicManager is missing MusicCueClips reference.");
            return;
        }

        if (!cueClips.TryGetClip(cue, out MusicCueClip nextCueClip) || nextCueClip == null || nextCueClip.clip == null)
        {
            Debug.LogError($"Music cue {cue} is missing a valid audio clip.");
            return;
        }

        if (!IsClipReady(nextCueClip.clip))
        {
            if (pendingLoadRoutine != null)
            {
                StopCoroutine(pendingLoadRoutine);
            }

            pendingLoadRoutine = StartCoroutine(WaitForClipThenTransition(nextCueClip.clip, cue, transitionSeconds));
            return;
        }

        int nextIndex = (int)cue;
        AudioSource nextSource = GetOrCreateSource(nextIndex);
        if (nextSource == null)
        {
            return;
        }

        double transitionTransportSeconds = GetCurrentTransportSeconds();
        if (hasCurrentCue && currentCue == MusicCues.Intro && cue == MusicCues.Normal)
        {
            transitionTransportSeconds = 0d;
        }

        nextCueClip.ApplyToSource(nextSource, GetPlaybackAnchor(), EvaluateIntensityMultiplier());
        ApplyPlaybackOverrides(nextSource);

        if (nextCueClip.additive)
        {
            if (!nextCueClip.allowAdditive)
            {
                StopAllAdditiveLayers(nextCueClip.additiveTransitionTime);

                if (hasCurrentCue && currentCue != cue)
                {
                    int currentIndex = (int)currentCue;
                    AudioSource existing = sources[currentIndex];
                    existing?.Stop();

                    hasCurrentCue = false;
                }
            }

            PlayAdditiveLayer(cue, nextCueClip, nextSource, transitionTransportSeconds);

            // If exclusive, treat this additive cue as the current/only cue.
            if (!nextCueClip.allowAdditive)
            {
                currentCue = cue;
                hasCurrentCue = true;
            }

            return;
        }

        StopAllAdditiveLayers(nextCueClip.additiveTransitionTime);

        if (hasCurrentCue && currentCue == cue)
        {
            if (!nextSource.isPlaying)
            {
                SyncSourceToCueTransport(nextSource, nextCueClip.clip, cue, transitionTransportSeconds);
                nextSource.Play();
            }

            HandleIntroTransition(cue, nextCueClip.clip);

            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (!hasCurrentCue || transitionSeconds <= 0f)
        {
            if (hasCurrentCue)
            {
                AudioSource existing = sources[(int)currentCue];
                existing?.Stop();
            }

            nextSource.volume = nextCueClip.volume * EvaluateIntensityMultiplier();
            SyncSourceToCueTransport(nextSource, nextCueClip.clip, cue, transitionTransportSeconds);
            nextSource.Play();
            currentCue = cue;
            hasCurrentCue = true;
            HandleIntroTransition(cue, nextCueClip.clip);
            return;
        }

        transitionRoutine = StartCoroutine(CrossfadeRoutine(currentCue, cue, transitionSeconds, transitionTransportSeconds));
    }

    #endregion

    #region Playback Decisions

    private bool PlayMusicInternal(MusicCues cue, float transitionSeconds, CuePlaybackPolicy<MusicCues> playbackPolicy)
    {
        if (!CanPlayCue(cue))
        {
            return false;
        }

        switch (playbackPolicy.Mode)
        {
            case CuePlaybackMode.YieldToPlayingCue:
            {
                if (IsCuePlaying(cue))
                {
                    return false;
                }

                break;
            }
            case CuePlaybackMode.IgnorePlayingCues:
            {
                int index = (int)cue;
                AudioSource source = GetOrCreateSource(index);
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                }

                break;
            }
            case CuePlaybackMode.TrackCue:
            default:
                break;
        }

        TransitionTo(cue, transitionSeconds);
        lastTransition = (Time.time, cue); // includes failed transitions
        return true;
    }

    private bool CanPlayCue(MusicCues cue)
    {
        MusicCueClipList cueClips = ActiveMusicCueClips;
        if (cueClips == null)
        {
            Debug.LogError("MusicManager is missing MusicCueClips reference.");
            return false;
        }

        if (!cueClips.TryGetClip(cue, out MusicCueClip cueClip) || cueClip == null || cueClip.clip == null)
        {
            Debug.LogError($"Music cue {cue} is missing a valid audio clip.");
            return false;
        }

        return true;
    }

    #endregion

    #region Source Helpers

    private AudioSource GetOrCreateSource(int index)
    {
        return GetOrCreatePooledSource(sources, index, $"MusicSource_{(MusicCues)index}");
    }

    #endregion

    #region Coroutines

    private IEnumerator CrossfadeRoutine(MusicCues fromCue, MusicCues toCue, float transitionSeconds, double transportAtTransitionStart)
    {
        int fromIndex = (int)fromCue;
        int toIndex = (int)toCue;

        AudioSource fromSource = GetOrCreateSource(fromIndex);
        AudioSource toSource = GetOrCreateSource(toIndex);

        MusicCueClipList cueClips = ActiveMusicCueClips;
        if (cueClips == null || !cueClips.TryGetClip(fromCue, out MusicCueClip fromCueClip) || fromCueClip == null)
        {
            fromCueClip = new MusicCueClip { volume = fromSource != null ? fromSource.volume : 1f };
        }

        if (cueClips == null || !cueClips.TryGetClip(toCue, out MusicCueClip toCueClip) || toCueClip == null)
        {
            transitionRoutine = null;
            yield break;
        }

        if (toSource == null)
        {
            transitionRoutine = null;
            yield break;
        }

        toCueClip.ApplyToSource(toSource, GetPlaybackAnchor(), EvaluateIntensityMultiplier());
        ApplyPlaybackOverrides(toSource);
        float targetToVolume = toCueClip.volume * EvaluateIntensityMultiplier();

        if (!toSource.isPlaying)
        {
            toSource.volume = 0f;
            SyncSourceToCueTransport(toSource, toCueClip.clip, toCue, transportAtTransitionStart);
            toSource.Play();
        }

        float duration = Mathf.Max(0.01f, transitionSeconds);
        float elapsed = 0f;
        float fromStartVolume = fromSource != null ? fromSource.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (fromSource != null)
            {
                fromSource.volume = Mathf.Lerp(fromStartVolume, 0f, t);
            }

            toSource.volume = Mathf.Lerp(0f, targetToVolume, t);
            yield return null;
        }

        if (fromSource != null)
        {
            fromSource.Stop();
            fromSource.volume = fromCueClip.volume * EvaluateIntensityMultiplier();
        }

        toSource.volume = targetToVolume;
        currentCue = toCue;
        hasCurrentCue = true;
        HandleIntroTransition(toCue, toCueClip.clip);
        transitionRoutine = null;
    }

    private IEnumerator FadeOutRoutine(int sourceIndex, float fadeOutSeconds)
    {
        AudioSource source = GetOrCreateSource(sourceIndex);
        if (source == null)
        {
            hasCurrentCue = false;
            transitionRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0.01f, fadeOutSeconds);
        float elapsed = 0f;
        float startVolume = source.volume;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        source.Stop();
        source.volume = startVolume;
        hasCurrentCue = false;
        transitionRoutine = null;
    }

    private void PlayAdditiveLayer(MusicCues cue, MusicCueClip cueClip, AudioSource source, double transportSecondsAtStart)
    {
        if (cueClip == null || cueClip.clip == null || source == null)
        {
            return;
        }

        int index = (int)cue;
        if (index < 0 || index >= additiveActive.Length)
        {
            return;
        }

        float targetVolume = cueClip.volume * EvaluateIntensityMultiplier();

        if (!source.isPlaying)
        {
            source.volume = 0f;
            SyncSourceToTransport(source, cueClip.clip, transportSecondsAtStart);
            source.Play();
        }

        additiveActive[index] = true;

        float fadeSeconds = Mathf.Max(0f, cueClip.additiveTransitionTime);
        if (fadeSeconds <= 0f)
        {
            source.volume = targetVolume;
            return;
        }

        if (additiveFadeRoutines[index] != null)
        {
            StopCoroutine(additiveFadeRoutines[index]);
        }

        additiveFadeRoutines[index] = StartCoroutine(FadeSourceVolumeRoutine(index, source, targetVolume, fadeSeconds, stopSourceAtEnd: false));
    }

    private void StopAllAdditiveLayers(float fadeOutSeconds)
    {
        MusicCueClipList cueClips = ActiveMusicCueClips;

        for (int i = 0; i < additiveActive.Length; i++)
        {
            if (!additiveActive[i])
            {
                continue;
            }

            AudioSource source = GetOrCreateSource(i);
            if (source == null)
            {
                additiveActive[i] = false;
                continue;
            }

            float duration = Mathf.Max(0f, fadeOutSeconds);
            if (cueClips != null && cueClips.TryGetClip((MusicCues)i, out MusicCueClip cueClip) && cueClip != null)
            {
                duration = Mathf.Max(duration, cueClip.additiveTransitionTime);
            }

            if (additiveFadeRoutines[i] != null)
            {
                StopCoroutine(additiveFadeRoutines[i]);
                additiveFadeRoutines[i] = null;
            }

            if (duration <= 0f)
            {
                source.Stop();
                additiveActive[i] = false;
                continue;
            }

            additiveFadeRoutines[i] = StartCoroutine(FadeSourceVolumeRoutine(i, source, 0f, duration, stopSourceAtEnd: true));
        }
    }

    private IEnumerator FadeSourceVolumeRoutine(int index, AudioSource source, float targetVolume, float duration, bool stopSourceAtEnd)
    {
        if (source == null)
        {
            additiveFadeRoutines[index] = null;
            additiveActive[index] = false;
            yield break;
        }

        float startVolume = source.volume;
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            source.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        source.volume = targetVolume;

        if (stopSourceAtEnd)
        {
            source.Stop();
            additiveActive[index] = false;
        }

        additiveFadeRoutines[index] = null;
    }

    private IEnumerator WaitForClipThenTransition(AudioClip clip, MusicCues cue, float transitionSeconds)
    {
        if (clip == null)
        {
            pendingLoadRoutine = null;
            yield break;
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        const float timeoutSeconds = 8f;
        float elapsed = 0f;
        while (clip.loadState == AudioDataLoadState.Unloaded || clip.loadState == AudioDataLoadState.Loading)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeoutSeconds)
            {
                Debug.LogError($"Timed out loading audio data for cue {cue}.");
                pendingLoadRoutine = null;
                yield break;
            }

            yield return null;
        }

        pendingLoadRoutine = null;

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Debug.LogError($"Failed to load audio data for cue {cue}. Current state: {clip.loadState}");
            yield break;
        }

        TransitionTo(cue, transitionSeconds);
    }

    #endregion

    #region Utility

    private void RefreshCurrentVolume()
    {
        MusicCueClipList cueClips = ActiveMusicCueClips;
        if (cueClips == null)
        {
            return;
        }

        if (hasCurrentCue)
        {
            int index = (int)currentCue;
            AudioSource source = GetOrCreateSource(index);
            if (source != null && cueClips.TryGetClip(currentCue, out MusicCueClip cueClip) && cueClip != null)
            {
                source.volume = cueClip.volume * EvaluateIntensityMultiplier();
            }
        }

        for (int i = 0; i < additiveActive.Length; i++)
        {
            if (!additiveActive[i])
            {
                continue;
            }

            if (!cueClips.TryGetClip((MusicCues)i, out MusicCueClip additiveCueClip) || additiveCueClip == null)
            {
                continue;
            }

            AudioSource additiveSource = GetOrCreateSource(i);
            if (additiveSource == null)
            {
                continue;
            }

            additiveSource.volume = additiveCueClip.volume * EvaluateIntensityMultiplier();
        }
    }

    private float EvaluateIntensityMultiplier()
    {
        return Mathf.Lerp(minIntensityVolumeMultiplier, 1f, intensity01);
    }

    private void UpdateTransportClock()
    {
        transportSeconds = GetCurrentTransportSeconds();
    }

    private double GetCurrentTransportSeconds()
    {
        if (!hasCurrentCue)
        {
            return transportSeconds;
        }

        int currentIndex = (int)currentCue;
        if (currentIndex < 0 || currentIndex >= sources.Length)
        {
            return transportSeconds;
        }

        AudioSource currentSource = sources[currentIndex];
        if (currentSource == null || !currentSource.isPlaying || currentSource.clip == null)
        {
            return transportSeconds;
        }

        int frequency = Mathf.Max(1, currentSource.clip.frequency);
        return (double)currentSource.timeSamples / frequency;
    }

    private static void SyncSourceToTransport(AudioSource source, AudioClip clip, double transport)
    {
        if (source == null || clip == null)
        {
            return;
        }

        double clipLength = clip.length;
        if (clipLength <= 0d)
        {
            return;
        }

        double wrapped = transport % clipLength;
        if (wrapped < 0d)
        {
            wrapped += clipLength;
        }

        int clipSamples = Mathf.Max(1, clip.samples);
        int frequency = Mathf.Max(1, clip.frequency);
        int targetSamples = (int)Math.Round(wrapped * frequency);
        source.timeSamples = Mathf.Clamp(targetSamples, 0, clipSamples - 1);
    }

    private Transform GetPlaybackAnchor()
    {
        Camera camera = Camera.main;
        return camera != null ? camera.transform : transform;
    }

    private void ApplyPlaybackOverrides(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.minDistance = Mathf.Max(0.01f, source.minDistance);

        if (!force2DMusic)
        {
            return;
        }

        source.spatialBlend = 0f;
        source.spatialize = false;
    }

    private static bool IsClipReady(AudioClip clip)
    {
        if (clip == null)
        {
            return false;
        }

        return clip.loadState == AudioDataLoadState.Loaded;
    }

    private void HandleIntroTransition(MusicCues cue, AudioClip clip)
    {
        if (introTransitionRoutine != null)
        {
            StopCoroutine(introTransitionRoutine);
            introTransitionRoutine = null;
        }

        if (cue != MusicCues.Intro || clip == null)
        {
            return;
        }

        PreloadCueClip(MusicCues.Normal);
        introTransitionRoutine = StartCoroutine(WaitForIntroThenTransitionToNormal(clip));
    }

    private IEnumerator WaitForIntroThenTransitionToNormal(AudioClip introClip)
    {
        int introIndex = (int)MusicCues.Intro;
        MusicCueClip normalCueClip = null;
        bool hasNormalCue = ActiveMusicCueClips != null
            && ActiveMusicCueClips.TryGetClip(MusicCues.Normal, out normalCueClip)
            && normalCueClip != null
            && normalCueClip.clip != null;
        AudioClip normalClip = hasNormalCue ? normalCueClip.clip : null;
        float preloadWindow = Mathf.Max(0.1f, defaultTransitionSeconds);
        const float introEndThresholdSeconds = 0.01f;
        const float introToNormalTransitionSeconds = 0f;

        while (hasCurrentCue && currentCue == MusicCues.Intro)
        {
            AudioSource introSource = GetOrCreateSource(introIndex);
            if (introSource == null || introSource.clip != introClip)
            {
                introTransitionRoutine = null;
                yield break;
            }

            double remainingSeconds = GetSourceRemainingSeconds(introSource, introClip);

            if (!introSource.isPlaying)
            {
                remainingSeconds = 0d;
            }

            if (normalClip != null && remainingSeconds <= Mathf.Max(preloadWindow, 1f))
            {
                PreloadCueClip(MusicCues.Normal);
            }

            if (normalClip != null && introSource.isPlaying && IsClipReady(normalClip))
            {
                if (TryScheduleIntroToNormal(introSource, normalClip, remainingSeconds))
                {
                    yield break;
                }
            }

            if (normalClip != null && remainingSeconds <= introEndThresholdSeconds && IsClipReady(normalClip))
            {
                if (TryScheduleIntroToNormal(introSource, normalClip, remainingSeconds))
                {
                    yield break;
                }

                introTransitionRoutine = null;
                TransitionTo(MusicCues.Normal, introToNormalTransitionSeconds);
                lastTransition = (Time.time, MusicCues.Normal);
                yield break;
            }

            if (remainingSeconds <= introEndThresholdSeconds)
            {
                break;
            }

            yield return null;
        }

        introTransitionRoutine = null;

        if (hasCurrentCue && currentCue == MusicCues.Intro)
        {
            AudioSource introSource = GetOrCreateSource(introIndex);
            if (normalClip != null && IsClipReady(normalClip) && TryScheduleIntroToNormal(introSource, normalClip, 0d))
            {
                yield break;
            }

            if (normalClip != null && !IsClipReady(normalClip))
            {
                Debug.LogWarning($"Intro ended before Normal was loaded. Normal loadState: {normalClip.loadState}");
            }

            TransitionTo(MusicCues.Normal, introToNormalTransitionSeconds);
            lastTransition = (Time.time, MusicCues.Normal);
        }
    }

    private bool TryScheduleIntroToNormal(AudioSource introSource, AudioClip normalClip, double delaySeconds)
    {
        if (normalClip == null)
        {
            return false;
        }

        MusicCueClipList cueClips = ActiveMusicCueClips;
        if (cueClips == null || !cueClips.TryGetClip(MusicCues.Normal, out MusicCueClip normalCueClip) || normalCueClip == null)
        {
            return false;
        }

        AudioSource normalSource = GetOrCreateSource((int)MusicCues.Normal);
        if (normalSource == null)
        {
            return false;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        StopAllAdditiveLayers(normalCueClip.additiveTransitionTime);
        normalCueClip.ApplyToSource(normalSource, GetPlaybackAnchor(), EvaluateIntensityMultiplier());
        ApplyPlaybackOverrides(normalSource);
        normalSource.volume = normalCueClip.volume * EvaluateIntensityMultiplier();
        normalSource.timeSamples = 0;

        if (normalSource.isPlaying)
        {
            normalSource.Stop();
        }

        double earlyAdvanceSeconds = Math.Max(0d, introToNormalAdvanceSeconds);
        double adjustedDelaySeconds = Math.Max(0d, delaySeconds - earlyAdvanceSeconds);
        const double minLeadSeconds = 0.005d;
        double startDspTime = AudioSettings.dspTime + Math.Max(minLeadSeconds, adjustedDelaySeconds);
        normalSource.PlayScheduled(startDspTime);

        if (introSource != null && introSource.isPlaying)
        {
            introSource.SetScheduledEndTime(startDspTime);
        }

        if (scheduledIntroToNormalCommitRoutine != null)
        {
            StopCoroutine(scheduledIntroToNormalCommitRoutine);
        }

        scheduledIntroToNormalCommitRoutine = StartCoroutine(CommitScheduledIntroToNormal(startDspTime, normalClip));
        introTransitionRoutine = null;
        lastTransition = (Time.time, MusicCues.Normal);
        return true;
    }

    private static double GetSourceRemainingSeconds(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null)
        {
            return 0d;
        }

        int frequency = Math.Max(1, clip.frequency);
        int remainingSamples = Math.Max(0, clip.samples - source.timeSamples);
        return (double)remainingSamples / frequency;
    }

    private IEnumerator CommitScheduledIntroToNormal(double normalStartDspTime, AudioClip normalClip)
    {
        while (AudioSettings.dspTime < normalStartDspTime)
        {
            if (!hasCurrentCue || currentCue != MusicCues.Intro)
            {
                scheduledIntroToNormalCommitRoutine = null;
                yield break;
            }

            yield return null;
        }

        int normalIndex = (int)MusicCues.Normal;
        AudioSource normalSource = GetOrCreateSource(normalIndex);
        if (normalSource == null || normalSource.clip != normalClip)
        {
            scheduledIntroToNormalCommitRoutine = null;
            yield break;
        }

        currentCue = MusicCues.Normal;
        hasCurrentCue = true;
        HandleIntroTransition(MusicCues.Normal, normalClip);
        scheduledIntroToNormalCommitRoutine = null;
    }

    private void PreloadCueClip(MusicCues cue)
    {
        MusicCueClipList cueClips = ActiveMusicCueClips;
        if (cueClips == null)
        {
            return;
        }

        if (!cueClips.TryGetClip(cue, out MusicCueClip cueClip) || cueClip == null || cueClip.clip == null)
        {
            return;
        }

        AudioClip clip = cueClip.clip;
        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    private static void SyncSourceToCueTransport(AudioSource source, AudioClip clip, MusicCues cue, double transport)
    {
        if (source == null || clip == null)
        {
            return;
        }

        if (cue == MusicCues.Intro)
        {
            source.timeSamples = 0;
            return;
        }

        SyncSourceToTransport(source, clip, transport);
    }

    #endregion
}