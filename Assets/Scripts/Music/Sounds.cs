using System;
using UnityEngine;

[Serializable]
public class SoundCueClip
{
    public SoundCues cue;
    public float volume = 1f;
    public float pitch = 1f;
    public bool loop = false;
    public bool spatialize = false;
    public float spatialBlend = 0f; // 0 for 2D, 1 for 3D
    public float maxDistance = 500f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    public float minDistance = 1f;

    public AudioClip clip;

    public void Play(AudioSource source, Vector3 position)
    {
        source.transform.position = position; // move the audio source gameobject to the desired position
        source.volume = volume;
        source.pitch = pitch;
        source.loop = loop;
        source.spatialBlend = spatialBlend;
        source.maxDistance = maxDistance;
        source.rolloffMode = rolloffMode;
        source.minDistance = minDistance;
        source.spatialize = spatialize;

        source.PlayOneShot(clip);
    }
}

[CreateAssetMenu(fileName = "SoundCueClips", menuName = "Pancake Game/SoundCueClips", order = 1)]
public class SoundCueClips : ScriptableObject
{
    public SoundCueClip[] clips;

    public SoundCueClip GetClip(SoundCues cue)
    {
        foreach (SoundCueClip soundCueClip in clips)
        {
            if (soundCueClip.cue == cue)
            {
                return soundCueClip;
            }
        }

        Debug.LogError($"Sound cue {cue} not found!");
        return null;
    }

    public bool TryGetClip(SoundCues cue, out SoundCueClip soundCueClip)
    {
        foreach (SoundCueClip clip in clips)
        {
            if (clip.cue == cue)
            {
                soundCueClip = clip;
                return true;
            }
        }

        Debug.LogError($"Sound cue {cue} not found!");
        soundCueClip = null;
        return false;
    }
}