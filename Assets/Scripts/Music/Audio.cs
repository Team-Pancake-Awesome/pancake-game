using System;
using UnityEngine;

[Serializable]
public abstract class CueClip
{
    public abstract int CueId { get; }
    public float volume = 1f;
    public float pitch = 1f;
    public bool loop = true;
    public bool spatialize = false;
    public float spatialBlend = 0f; // 0 for 2D, 1 for 3D
    public float maxDistance = 500f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    public float minDistance = 1f;

    public AudioClip clip;

    public virtual void ApplyToSource(AudioSource source, Transform transform, float volumeMultiplier)
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

public abstract class CueClipList : ScriptableObject
{
    public abstract CueClip GetClip(int cue);
    public abstract bool TryGetClip(int cue, out CueClip cueClip);
}