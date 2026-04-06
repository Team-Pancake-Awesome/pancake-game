using System;
using System.Collections;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public MusicCueClips musicCueClips;

    [Header("Dynamic Music")]
    [Min(0f)]
    public float defaultTransitionSeconds = 0.75f;

    [Range(0f, 1f)]
    public float minIntensityVolumeMultiplier = 0.35f;

    [Header("Playback")]
    public bool force2DMusic = true;

    public float TimeTransitionBackToNormalSeconds = 15f;
    public bool TransitionBackToNormalAfterSeconds = true; // only if no transitions are requested

    private readonly AudioSource[] sources = new AudioSource[Enum.GetValues(typeof(MusicCues)).Length];

    private static MusicManager _instance;

    private Coroutine transitionRoutine;
    private Coroutine pendingLoadRoutine;
    private MusicCues currentCue;
    private bool hasCurrentCue;
    private double transportSeconds;
    private float intensity01 = 1f;
    private (float time, MusicCues cue)? lastTransition;

    public static MusicManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MusicManager>();
                if (_instance == null)
                {
                    GameObject managerObject = new("MusicManager");
                    _instance = managerObject.AddComponent<MusicManager>();
                }
            }
            return _instance;
        }
    }

    private MusicManager() { }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public MusicCues CurrentCue => currentCue;
    public bool HasCurrentCue => hasCurrentCue;

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

    public void SetIntensity(float intensity)
    {
        intensity01 = Mathf.Clamp01(intensity);
        RefreshCurrentVolume();
    }

    public void PlayMusic(MusicCues cue)
    {
        TransitionTo(cue, defaultTransitionSeconds);
        lastTransition = (Time.time, cue); // includes failed transitions
    }

    public void PlayMusicNow(MusicCues cue)
    {
        TransitionTo(cue, 0f);
        lastTransition = (Time.time, cue); // includes failed transitions
    }

    public void StopMusic(float fadeOutSeconds = 0f)
    {
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
            hasCurrentCue = false;
            return;
        }

        transitionRoutine = StartCoroutine(FadeOutRoutine(currentIndex, fadeOutSeconds));
    }

    public void TransitionTo(MusicCues cue, float transitionSeconds)
    {
        if (musicCueClips == null)
        {
            Debug.LogError("MusicManager is missing MusicCueClips reference.");
            return;
        }

        if (!musicCueClips.TryGetClip(cue, out MusicCueClip nextCueClip) || nextCueClip == null || nextCueClip.clip == null)
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

        nextCueClip.ApplyToSource(nextSource, GetPlaybackAnchor(), EvaluateIntensityMultiplier());
        ApplyPlaybackOverrides(nextSource);

        if (hasCurrentCue && currentCue == cue)
        {
            if (!nextSource.isPlaying)
            {
                SyncSourceToTransport(nextSource, nextCueClip.clip, transitionTransportSeconds);
                nextSource.Play();
            }

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
            SyncSourceToTransport(nextSource, nextCueClip.clip, transitionTransportSeconds);
            nextSource.Play();
            currentCue = cue;
            hasCurrentCue = true;
            return;
        }

        transitionRoutine = StartCoroutine(CrossfadeRoutine(currentCue, cue, transitionSeconds, transitionTransportSeconds));
    }

    private AudioSource GetOrCreateSource(int index)
    {
        if (index < 0 || index >= sources.Length)
        {
            return null;
        }

        if (sources[index] == null)
        {
            MusicCues cue = (MusicCues)index;
            GameObject sourceObject = new($"MusicSource_{cue}");
            sourceObject.transform.SetParent(transform, false);
            sources[index] = sourceObject.AddComponent<AudioSource>();
        }

        return sources[index];
    }

    private IEnumerator CrossfadeRoutine(MusicCues fromCue, MusicCues toCue, float transitionSeconds, double transportAtTransitionStart)
    {
        int fromIndex = (int)fromCue;
        int toIndex = (int)toCue;

        AudioSource fromSource = GetOrCreateSource(fromIndex);
        AudioSource toSource = GetOrCreateSource(toIndex);

        if (!musicCueClips.TryGetClip(fromCue, out MusicCueClip fromCueClip) || fromCueClip == null)
        {
            fromCueClip = new MusicCueClip { volume = fromSource != null ? fromSource.volume : 1f };
        }

        if (!musicCueClips.TryGetClip(toCue, out MusicCueClip toCueClip) || toCueClip == null)
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
            SyncSourceToTransport(toSource, toCueClip.clip, transportAtTransitionStart);
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

    private void RefreshCurrentVolume()
    {
        if (!hasCurrentCue || musicCueClips == null)
        {
            return;
        }

        int index = (int)currentCue;
        AudioSource source = GetOrCreateSource(index);
        if (source == null)
        {
            return;
        }

        if (!musicCueClips.TryGetClip(currentCue, out MusicCueClip cueClip) || cueClip == null)
        {
            return;
        }

        source.volume = cueClip.volume * EvaluateIntensityMultiplier();
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

        if (clip.loadState == AudioDataLoadState.Loaded)
        {
            return true;
        }

        // On some imports the clip can still play directly even when not preloaded.
        return clip.preloadAudioData;
    }

    private void OnDestroy()
    {
        if (pendingLoadRoutine != null)
        {
            StopCoroutine(pendingLoadRoutine);
            pendingLoadRoutine = null;
        }

        if (_instance == this)
        {
            _instance = null;
        }
    }
}