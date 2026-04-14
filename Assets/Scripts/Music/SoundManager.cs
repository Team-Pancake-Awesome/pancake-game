using System;
using UnityEngine;

public class SoundManager : AudioManager<SoundManager>
{
    #region Inspector

    public SoundCueClipList soundCueClips;
    private SoundCueClipList runtimeSoundCueClips;

    #endregion

    #region Runtime State

    private readonly AudioSource[] sources = new AudioSource[Enum.GetValues(typeof(SoundCues)).Length];

    private SoundCueClipList ActiveSoundCueClips => runtimeSoundCueClips != null ? runtimeSoundCueClips : soundCueClips;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }

        runtimeSoundCueClips = CreateRuntimeCueList(soundCueClips);
    }

    #endregion

    #region Public API

    public bool PlayFromCue(SoundCues cue, CuePlaybackPolicy<SoundCues> playbackPolicy = default)
    {
        return PlayFromCue(cue, transform.position, playbackPolicy);
    }

    public bool PlayFromCue(SoundCues cue, Vector3 position, CuePlaybackPolicy<SoundCues> playbackPolicy = default)
    {
        SoundCueClipList cueClips = ActiveSoundCueClips;
        if (cueClips == null || !cueClips.TryGetClip(cue, out SoundCueClip soundCueClip))
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

                int index = (int)cue;
                AudioSource trackedSource = GetOrCreateSource(index, cue);
                if (trackedSource == null)
                {
                    return false;
                }

                soundCueClip.Play(trackedSource, position);
                return true;
            }
            case CuePlaybackMode.IgnorePlayingCues:
            {
                return PlayTransient(soundCueClip, cue, position);
            }
            case CuePlaybackMode.TrackCue:
            default:
            {
                int index = (int)cue;
                AudioSource trackedSource = GetOrCreateSource(index, cue);
                if (trackedSource == null)
                {
                    return false;
                }

                soundCueClip.Play(trackedSource, position);
                return true;
            }
        }
    }

    public bool IsCuePlaying(SoundCues cue)
    {
        int index = (int)cue;
        if (index < 0 || index >= sources.Length)
        {
            return false;
        }

        AudioSource source = sources[index];
        return source != null && source.isPlaying;
    }

    #endregion

    #region Playback Helpers

    private bool PlayTransient(SoundCueClip soundCueClip, SoundCues cue, Vector3 position)
    {
        if (soundCueClip == null || soundCueClip.clip == null)
        {
            return false;
        }

        if (soundCueClip.loop)
        {
            Debug.LogWarning($"Transient playback requested for looping cue {cue}. Falling back to tracked playback.");
            int index = (int)cue;
            AudioSource trackedSource = GetOrCreateSource(index, cue);
            if (trackedSource == null)
            {
                return false;
            }

            soundCueClip.Play(trackedSource, position);
            return true;
        }

        GameObject transientObject = new($"SoundSourceTransient_{cue}");
        transientObject.transform.SetParent(transform, false);

        AudioSource transientSource = transientObject.AddComponent<AudioSource>();
        transientSource.transform.position = position;
        transientSource.volume = soundCueClip.volume;
        transientSource.pitch = soundCueClip.pitch;
        transientSource.loop = false;
        transientSource.spatialBlend = soundCueClip.spatialBlend;
        transientSource.maxDistance = soundCueClip.maxDistance;
        transientSource.rolloffMode = soundCueClip.rolloffMode;
        transientSource.minDistance = soundCueClip.minDistance;
        transientSource.spatialize = soundCueClip.spatialize;

        transientSource.PlayOneShot(soundCueClip.clip);
        float lifetime = Mathf.Max(0.1f, soundCueClip.clip.length / Mathf.Max(0.01f, Mathf.Abs(soundCueClip.pitch)) + 0.1f);
        Destroy(transientObject, lifetime);
        return true;
    }

    #endregion

    #region Source Helpers

    private AudioSource GetOrCreateSource(int index, SoundCues cue)
    {
        return GetOrCreatePooledSource(sources, index, $"SoundSource_{cue}");
    }

    #endregion
}
