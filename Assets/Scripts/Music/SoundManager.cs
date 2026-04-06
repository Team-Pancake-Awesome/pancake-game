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

    public void PlaySound(SoundCues cue)
    {
        PlaySound(cue, transform.position);
    }

    public void PlaySound(SoundCues cue, Vector3 position)
    {
        if (!soundCueClips.TryGetClip(cue, out SoundCueClip soundCueClip))
        {
            return;
        }

        int index = (int)cue;
        AudioSource source = GetOrCreateSource(index, cue);
        if (source == null)
        {
            return;
        }

        soundCueClip.Play(source, position);
    }

    private AudioSource GetOrCreateSource(int index, SoundCues cue)
    {
        if (index < 0 || index >= sources.Length)
        {
            return null;
        }

        if (sources[index] == null)
        {
            GameObject sourceObject = new($"SoundSource_{cue}");
            sourceObject.transform.SetParent(transform, false);
            sources[index] = sourceObject.AddComponent<AudioSource>();
        }

        return sources[index];
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
