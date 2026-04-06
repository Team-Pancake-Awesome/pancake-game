using System;
using UnityEngine;

[Serializable]
public class MusicCueClip
{
    public MusicCues cue;
    public float volume = 1f;
    public float pitch = 1f;
    public bool loop = true;
    public bool spatialize = false;
    public float spatialBlend = 0f; // 0 for 2D, 1 for 3D
    public float maxDistance = 500f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    public float minDistance = 1f;
    public bool additive = false; // if true, this music will play on top of any existing music instead of replacing it
    public float additiveTransitionTime = 0f;

    public AudioClip clip;

    public void ApplyToSource(AudioSource source, Transform transform, float volumeMultiplier)
    {
        source.transform.parent = transform;
        source.transform.localPosition = Vector3.zero;
        source.volume = Mathf.Clamp01(volume * Mathf.Max(0f, volumeMultiplier));
        source.pitch = pitch;
        source.loop = loop;
        source.spatialBlend = spatialBlend;
        source.maxDistance = maxDistance;
        source.rolloffMode = rolloffMode;
        source.minDistance = minDistance;
        source.spatialize = spatialize;
        source.clip = clip;
    }
}

[CreateAssetMenu(fileName = "MusicCueClips", menuName = "Pancake Game/MusicCueClips", order = 2)]
public class MusicCueClips : ScriptableObject
{
    public MusicCueClip[] clips;

    public MusicCueClip GetClip(MusicCues cue)
    {
        foreach (MusicCueClip musicCueClip in clips)
        {
            if (musicCueClip.cue == cue)
            {
                return musicCueClip;
            }
        }

        Debug.LogError($"Music cue {cue} not found!");
        return null;
    }

    public bool TryGetClip(MusicCues cue, out MusicCueClip musicCueClip)
    {
        var clip = GetClip(cue);
        musicCueClip = clip;
        return clip != null;
    }
}