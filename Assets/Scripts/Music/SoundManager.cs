using System;
using UnityEngine;

public class SoundManager : MonoBehaviour
{

    public SoundCueClips soundCueClips;

    private readonly AudioSource[] sources = new AudioSource[Enum.GetValues(typeof(SoundCues)).Length];

    private static SoundManager _instance;

    public static SoundManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject managerObject = new("SoundManager");
                _instance = managerObject.AddComponent<SoundManager>();
            }
            return _instance;
        }
    }

    private SoundManager() { }

    public void PlaySound(SoundCues cue)
    {
        if (!soundCueClips.TryGetClip(cue, out SoundCueClip soundCueClip))
        {
            return;
        }

        int index = (int)cue;
        if (sources[index] == null)
        {
            sources[index] = gameObject.AddComponent<AudioSource>();
        }

        soundCueClip.Play(sources[index], transform.position);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
