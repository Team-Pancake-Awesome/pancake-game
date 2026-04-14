using System;
using UnityEngine;

[Serializable]
public class MusicCueClip : CueClip
{
    [SerializeField]
    private MusicCues cue;

    public override int CueId => (int)cue;

    public bool additive = false; // if true, this music will play on top of any existing music instead of replacing it
    public float additiveTransitionTime = 0f;
}
