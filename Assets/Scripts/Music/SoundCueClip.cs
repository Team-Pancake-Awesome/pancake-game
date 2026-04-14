using System;
using UnityEngine;

[Serializable]
public class SoundCueClip : CueClip
{
    public void Play(AudioSource source, Vector3 position)
    {
        if (source == null || clip == null)
        {
            return;
        }

        source.transform.position = position;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = loop;
        source.spatialBlend = spatialBlend;
        source.maxDistance = maxDistance;
        source.rolloffMode = rolloffMode;
        source.minDistance = minDistance;
        source.spatialize = spatialize;

        if (loop)
        {
            source.clip = clip;
            source.Play();
            return;
        }

        source.Stop();
        source.clip = null;
        source.PlayOneShot(clip);
    }
}
