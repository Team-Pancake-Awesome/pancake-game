using UnityEngine;

[CreateAssetMenu(fileName = "SoundCueClipList", menuName = "Pancake Game/SoundCueClipList", order = 1)]
public class SoundCueClipList : CueClipList
{
    [SerializeField]
    private SoundCueClip[] clips;

    public override CueClip GetClip(int cue)
    {
        if (clips == null)
        {
            Debug.LogError($"Sound cue {cue} not found!");
            return null;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            SoundCueClip soundCueClip = clips[i];
            if (soundCueClip != null && soundCueClip.CueId == cue)
            {
                return soundCueClip;
            }
        }

        Debug.LogError($"Sound cue {cue} not found!");
        return null;
    }

    public override bool TryGetClip(int cue, out CueClip cueClip)
    {
        cueClip = GetClip(cue);
        return cueClip != null;
    }

    public SoundCueClip GetClip(SoundCues cue)
    {
        return (SoundCueClip)GetClip((int)cue);
    }

    public bool TryGetClip(SoundCues cue, out SoundCueClip soundCueClip)
    {
        var clip = GetClip(cue);
        soundCueClip = clip;
        return clip != null;
    }
}