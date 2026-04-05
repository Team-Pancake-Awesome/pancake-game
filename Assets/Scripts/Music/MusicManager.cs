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


    private readonly AudioSource[] sources = new AudioSource[Enum.GetValues(typeof(MusicCues)).Length];

    private static MusicManager _instance;

    private Coroutine transitionRoutine;
    private MusicCues currentCue;
    private bool hasCurrentCue;
    private float intensity01 = 1f;

    public static MusicManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject managerObject = new("MusicManager");
                _instance = managerObject.AddComponent<MusicManager>();
            }
            return _instance;
        }
    }

    private MusicManager() { }

    public MusicCues CurrentCue => currentCue;
    public bool HasCurrentCue => hasCurrentCue;

    public void SetIntensity(float intensity)
    {
        intensity01 = Mathf.Clamp01(intensity);
        RefreshCurrentVolume();
    }

    public void PlayMusic(MusicCues cue)
    {
        TransitionTo(cue, defaultTransitionSeconds);
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

        int nextIndex = (int)cue;
        AudioSource nextSource = GetOrCreateSource(nextIndex);
        if (nextSource == null)
        {
            return;
        }

        nextCueClip.ApplyToSource(nextSource, transform.position, EvaluateIntensityMultiplier());

        if (hasCurrentCue && currentCue == cue)
        {
            if (!nextSource.isPlaying)
            {
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
            nextSource.Play();
            currentCue = cue;
            hasCurrentCue = true;
            return;
        }

        transitionRoutine = StartCoroutine(CrossfadeRoutine(currentCue, cue, transitionSeconds));
    }

    private AudioSource GetOrCreateSource(int index)
    {
        if (index < 0 || index >= sources.Length)
        {
            return null;
        }

        if (sources[index] == null)
        {
            sources[index] = gameObject.AddComponent<AudioSource>();
        }

        return sources[index];
    }

    private IEnumerator CrossfadeRoutine(MusicCues fromCue, MusicCues toCue, float transitionSeconds)
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

        toCueClip.ApplyToSource(toSource, transform.position, EvaluateIntensityMultiplier());
        float targetToVolume = toCueClip.volume * EvaluateIntensityMultiplier();

        if (!toSource.isPlaying)
        {
            toSource.volume = 0f;
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

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}