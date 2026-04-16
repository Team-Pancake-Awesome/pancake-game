using UnityEngine;

[CreateAssetMenu(fileName = "MusicCueClipList", menuName = "Pancake Game/MusicCueClipList", order = 2)]
public class MusicCueClipList : CueClipList
{
    [SerializeField]
    private MusicCueClip[] clips;

    public override CueClip GetClip(int cue)
    {
        if (clips == null)
        {
            Debug.LogError($"Music cue {cue} not found!");
            return null;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            MusicCueClip musicCueClip = clips[i];
            if (musicCueClip != null && musicCueClip.CueId == cue)
            {
                return musicCueClip;
            }
        }

        Debug.LogError($"Music cue {cue} not found!");
        return null;
    }

    public override bool TryGetClip(int cue, out CueClip cueClip)
    {
        cueClip = GetClip(cue);
        return cueClip != null;
    }

    public MusicCueClip GetClip(MusicCues cue)
    {
        return (MusicCueClip)GetClip((int)cue);
    }

    public bool TryGetClip(MusicCues cue, out MusicCueClip musicCueClip)
    {
        var clip = GetClip(cue);
        musicCueClip = clip;
        return clip != null;
    }
}